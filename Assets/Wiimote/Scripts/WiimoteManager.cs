using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;

namespace WiimoteApi;

/// <summary>Discovers Wii controllers and coordinates access to HIDAPI.</summary>
public static class WiimoteManager
{
    private const ushort NintendoVendorId = 0x057e;
    private const ushort WiimoteProductId = 0x0306;
    private const ushort WiimotePlusProductId = 0x0330;

    private static readonly object DevicesLock = new();
    private static readonly ConcurrentQueue<WriteRequest> WriteQueue = new();
    private static readonly SemaphoreSlim WriteSignal = new(0);
    private static readonly CancellationTokenSource ShutdownSource = new();
    private static readonly Lazy<Task> WriterTask = new(
        () => Task.Run(() => ProcessWritesAsync(ShutdownSource.Token)),
        LazyThreadSafetyMode.ExecutionAndPublication);

    private static readonly List<Wiimote> MutableDevices = [];
    private static int _initialized;
    private static bool _isShutDown;
    private static TimeSpan _minimumWriteInterval = TimeSpan.FromMilliseconds(20);

    /// <summary>Raised for diagnostic messages. No console logger is imposed on consumers.</summary>
    public static event Action<WiimoteLogLevel, string>? LogMessage;

    /// <summary>Gets a stable snapshot of the currently connected controllers.</summary>
    public static IReadOnlyList<Wiimote> Devices
    {
        get
        {
            lock (DevicesLock)
                return new ReadOnlyCollection<Wiimote>([.. MutableDevices]);
        }
    }

    /// <summary>Enables verbose HID traffic messages through <see cref="LogMessage"/>.</summary>
    public static bool EnableDebugLogging { get; set; }

    /// <summary>Minimum delay between HID writes. Defaults to 20 milliseconds.</summary>
    public static TimeSpan MinimumWriteInterval
    {
        get => _minimumWriteInterval;
        set => _minimumWriteInterval = value >= TimeSpan.Zero
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value));
    }

    /// <summary>Finds newly connected Wii Remotes, Wii Remote Pluses, and Wii U Pro Controllers.</summary>
    /// <returns>The number of newly opened controllers.</returns>
    public static int Discover()
    {
        ThrowIfShutDown();
        EnsureInitialized();
        return Discover(WiimoteType.WIIMOTE) + Discover(WiimoteType.WIIMOTEPLUS);
    }

    private static int Discover(WiimoteType requestedType)
    {
        ushort productId = requestedType switch
        {
            WiimoteType.WIIMOTE => WiimoteProductId,
            WiimoteType.WIIMOTEPLUS or WiimoteType.PROCONTROLLER => WiimotePlusProductId,
            _ => throw new ArgumentOutOfRangeException(nameof(requestedType))
        };

        IntPtr head = HIDapi.Enumerate(NintendoVendorId, productId);
        if (head == IntPtr.Zero)
            return 0;

        var found = 0;
        try
        {
            for (IntPtr current = head; current != IntPtr.Zero;)
            {
                HidDeviceInfo info = Marshal.PtrToStructure<HidDeviceInfo>(current);
                string? path = Marshal.PtrToStringUTF8(info.Path);
                string? productName = HIDapi.GetWideString(info.ProductString);
                current = info.Next;

                if (string.IsNullOrWhiteSpace(path) || ContainsPath(path))
                    continue;

                IntPtr handle = HIDapi.OpenPath(path);
                if (handle == IntPtr.Zero)
                {
                    Report(WiimoteLogLevel.Warning, $"HIDAPI could not open '{path}'.");
                    continue;
                }

                WiimoteType actualType = productName?.EndsWith("UC", StringComparison.Ordinal) == true
                    ? WiimoteType.PROCONTROLLER
                    : requestedType;
                var remote = new Wiimote(handle, path, actualType);

                lock (DevicesLock)
                    MutableDevices.Add(remote);

                found++;
                Report(WiimoteLogLevel.Debug, $"Found {actualType}: {path}");
                remote.SendDataReportMode(InputDataType.REPORT_BUTTONS);
                remote.SendStatusInfoRequest();
            }
        }
        finally
        {
            HIDapi.FreeEnumeration(head);
        }

        return found;
    }

    private static bool ContainsPath(string path)
    {
        lock (DevicesLock)
            return MutableDevices.Any(device => string.Equals(device.DevicePath, path, StringComparison.Ordinal));
    }

    /// <summary>Closes and removes one controller.</summary>
    public static void Cleanup(Wiimote remote)
    {
        ArgumentNullException.ThrowIfNull(remote);
        remote.Dispose();
        lock (DevicesLock)
            MutableDevices.Remove(remote);
    }

    /// <summary>Closes all controllers and permanently stops this manager.</summary>
    public static void Shutdown()
    {
        if (_isShutDown)
            return;

        _isShutDown = true;
        ShutdownSource.Cancel();
        WriteSignal.Release();

        Wiimote[] devices;
        lock (DevicesLock)
        {
            devices = [.. MutableDevices];
            MutableDevices.Clear();
        }

        foreach (Wiimote device in devices)
            device.Dispose();

        while (WriteQueue.TryDequeue(out WriteRequest? request))
            request.Completion.TrySetCanceled();

        if (WriterTask.IsValueCreated)
        {
            try
            {
                WriterTask.Value.GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
            }
        }

        if (Interlocked.Exchange(ref _initialized, 0) != 0)
            HIDapi.Exit();
    }

    public static bool HasWiimote() => Devices.Any(device => device.IsConnected);

    /// <summary>Queues a raw HID report and asynchronously returns the native write result.</summary>
    public static ValueTask<int> SendRawAsync(
        IntPtr deviceHandle,
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default)
    {
        ThrowIfShutDown();
        if (deviceHandle == IntPtr.Zero)
            throw new ArgumentException("A valid HID device handle is required.", nameof(deviceHandle));
        if (data.IsEmpty)
            throw new ArgumentException("A HID report cannot be empty.", nameof(data));
        cancellationToken.ThrowIfCancellationRequested();

        EnsureInitialized();
        _ = WriterTask.Value;

        var completion = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        WriteQueue.Enqueue(new WriteRequest(deviceHandle, data.ToArray(), completion, cancellationToken));
        WriteSignal.Release();
        return new ValueTask<int>(completion.Task);
    }

    /// <summary>Reads a raw HID report without blocking.</summary>
    public static int ReceiveRaw(IntPtr deviceHandle, Span<byte> buffer)
    {
        if (deviceHandle == IntPtr.Zero)
            throw new ArgumentException("A valid HID device handle is required.", nameof(deviceHandle));
        if (buffer.IsEmpty)
            throw new ArgumentException("The receive buffer cannot be empty.", nameof(buffer));

        EnsureInitialized();
        byte[] rented = GC.AllocateUninitializedArray<byte>(buffer.Length);
        HIDapi.SetNonBlocking(deviceHandle, true);
        int result = HIDapi.Read(deviceHandle, rented);
        if (result > 0)
            rented.AsSpan(0, Math.Min(result, buffer.Length)).CopyTo(buffer);
        return result;
    }

    internal static void CloseHandle(IntPtr handle)
    {
        if (handle != IntPtr.Zero)
            HIDapi.Close(handle);
    }

    internal static void Report(WiimoteLogLevel level, string message)
    {
        if (level == WiimoteLogLevel.Debug && !EnableDebugLogging)
            return;

        Action<WiimoteLogLevel, string>? handlers = LogMessage;
        if (handlers is null)
            return;

        foreach (Delegate subscriber in handlers.GetInvocationList())
        {
            try
            {
                ((Action<WiimoteLogLevel, string>)subscriber)(level, message);
            }
            catch
            {
                // Diagnostics must never terminate the HID writer.
            }
        }
    }

    private static void EnsureInitialized()
    {
        if (Interlocked.CompareExchange(ref _initialized, 1, 0) == 0)
        {
            int result = HIDapi.Init();
            if (result != 0)
            {
                Interlocked.Exchange(ref _initialized, 0);
                throw new InvalidOperationException($"HIDAPI initialization failed with error {result}.");
            }
        }
    }

    private static async Task ProcessWritesAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            await WriteSignal.WaitAsync(cancellationToken).ConfigureAwait(false);
            if (!WriteQueue.TryDequeue(out WriteRequest? request))
                continue;

            if (request.CancellationToken.IsCancellationRequested)
            {
                request.Completion.TrySetCanceled(request.CancellationToken);
                continue;
            }

            int result = HIDapi.Write(request.Handle, request.Data);
            if (result < 0)
            {
                string error = HIDapi.GetError(request.Handle) ?? "Unknown HIDAPI error";
                Report(WiimoteLogLevel.Error, $"HID write failed: {error}");
            }
            else
            {
                Report(WiimoteLogLevel.Debug, $"Sent {result} bytes: {Convert.ToHexString(request.Data)}");
            }

            request.Completion.TrySetResult(result);
            if (MinimumWriteInterval > TimeSpan.Zero)
                await Task.Delay(MinimumWriteInterval, cancellationToken).ConfigureAwait(false);
        }
    }

    private static void ThrowIfShutDown()
    {
        ObjectDisposedException.ThrowIf(_isShutDown, typeof(WiimoteManager));
    }

    private sealed record WriteRequest(
        IntPtr Handle,
        byte[] Data,
        TaskCompletionSource<int> Completion,
        CancellationToken CancellationToken);
}

public enum WiimoteLogLevel
{
    Debug,
    Warning,
    Error
}
