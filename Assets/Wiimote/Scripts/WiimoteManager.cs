using System.Buffers;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using WiimoteApi.Internal;

namespace WiimoteApi;

/// <summary>Discovers Wii controllers and coordinates access to HIDAPI.</summary>
public sealed class WiimoteManager : IDisposable
{
    private const ushort NintendoVendorId = 0x057e;
    private const ushort WiimoteProductId = 0x0306;
    private const ushort WiimotePlusProductId = 0x0330;

    private readonly object _devicesLock = new();
    private readonly Channel<WriteRequest> _writeChannel;
    private readonly Lazy<Task> _writerTask;
    private readonly List<Wiimote> _devices = [];
    private int _initialized;
    private int _isDisposed;
    private TimeSpan _minimumWriteInterval = TimeSpan.FromMilliseconds(20);

    public WiimoteManager()
    {
        _writeChannel = Channel.CreateUnbounded<WriteRequest>(
            new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false
            });
        _writerTask = new Lazy<Task>(
            ProcessWritesAsync,
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <summary>Raised for diagnostic messages. No console logger is imposed on consumers.</summary>
    public event Action<WiimoteLogLevel, string>? LogMessage;

    /// <summary>Gets a stable snapshot of the currently connected controllers.</summary>
    public IReadOnlyList<Wiimote> Devices
    {
        get
        {
            lock (_devicesLock)
                return new ReadOnlyCollection<Wiimote>([.. _devices]);
        }
    }

    /// <summary>Enables verbose HID traffic messages through <see cref="LogMessage"/>.</summary>
    public bool EnableDebugLogging { get; set; }

    /// <summary>Minimum delay between HID writes. Defaults to 20 milliseconds.</summary>
    public TimeSpan MinimumWriteInterval
    {
        get => _minimumWriteInterval;
        set => _minimumWriteInterval = value >= TimeSpan.Zero
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value));
    }

    /// <summary>Finds newly connected Wii Remotes, Wii Remote Pluses, and Wii U Pro Controllers.</summary>
    /// <returns>The number of newly opened controllers.</returns>
    public int Discover()
    {
        ThrowIfDisposed();
        EnsureInitialized();
        return Discover(WiimoteType.Original) + Discover(WiimoteType.RemotePlus);
    }

    private int Discover(WiimoteType requestedType)
    {
        ushort productId = requestedType switch
        {
            WiimoteType.Original => WiimoteProductId,
            WiimoteType.RemotePlus or WiimoteType.ProController => WiimotePlusProductId,
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

                IntPtr rawHandle = HIDapi.OpenPath(path);
                if (rawHandle == IntPtr.Zero)
                {
                    Report(WiimoteLogLevel.Warning, $"HIDAPI could not open '{path}'.");
                    continue;
                }

                var handle = new HidDeviceHandle(rawHandle);
                if (HIDapi.SetNonBlocking(handle, true) < 0)
                {
                    Report(WiimoteLogLevel.Warning, $"HIDAPI could not enable non-blocking reads for '{path}'.");
                    handle.Dispose();
                    continue;
                }

                WiimoteType actualType = productName?.EndsWith("UC", StringComparison.Ordinal) == true
                    ? WiimoteType.ProController
                    : requestedType;
                var remote = new Wiimote(this, handle, path, actualType);

                lock (_devicesLock)
                    _devices.Add(remote);

                found++;
                Report(WiimoteLogLevel.Debug, $"Found {actualType}: {path}");
            }
        }
        finally
        {
            HIDapi.FreeEnumeration(head);
        }

        return found;
    }

    private bool ContainsPath(string path)
    {
        lock (_devicesLock)
            return _devices.Any(device => string.Equals(device.DevicePath, path, StringComparison.Ordinal));
    }

    /// <summary>Closes and removes one controller.</summary>
    public void Remove(Wiimote remote)
    {
        ArgumentNullException.ThrowIfNull(remote);
        remote.Dispose();
    }

    internal void Detach(Wiimote remote)
    {
        lock (_devicesLock)
            _devices.Remove(remote);
    }

    /// <summary>Closes all controllers and permanently stops this manager.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
            return;

        _writeChannel.Writer.TryComplete();

        if (_writerTask.IsValueCreated)
            _writerTask.Value.GetAwaiter().GetResult();

        Wiimote[] devices;
        lock (_devicesLock)
        {
            devices = [.. _devices];
            _devices.Clear();
        }

        foreach (Wiimote device in devices)
            device.Dispose();

        if (Interlocked.Exchange(ref _initialized, 0) != 0)
            HIDapi.Exit();
    }

    public bool HasWiimote() => Devices.Any(device => device.IsConnected);

    /// <summary>Queues a raw HID report and asynchronously returns the native write result.</summary>
    internal ValueTask<int> SendRawAsync(
        HidDeviceHandle deviceHandle,
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ObjectDisposedException.ThrowIf(deviceHandle.IsClosed || deviceHandle.IsInvalid, deviceHandle);
        if (data.IsEmpty)
            throw new ArgumentException("A HID report cannot be empty.", nameof(data));
        cancellationToken.ThrowIfCancellationRequested();

        EnsureInitialized();
        _ = _writerTask.Value;

        bool handleLease = false;
        deviceHandle.DangerousAddRef(ref handleLease);
        var completion = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var request = new WriteRequest(deviceHandle, data.ToArray(), completion, cancellationToken);
        if (!_writeChannel.Writer.TryWrite(request))
        {
            deviceHandle.DangerousRelease();
            completion.TrySetException(new ObjectDisposedException(nameof(WiimoteManager)));
        }

        return new ValueTask<int>(completion.Task);
    }

    /// <summary>Reads a raw HID report without blocking.</summary>
    internal int ReceiveRaw(HidDeviceHandle deviceHandle, byte[] buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ObjectDisposedException.ThrowIf(deviceHandle.IsClosed || deviceHandle.IsInvalid, deviceHandle);
        if (buffer.Length == 0)
            throw new ArgumentException("The receive buffer cannot be empty.", nameof(buffer));

        EnsureInitialized();
        return HIDapi.Read(deviceHandle, buffer);
    }

    /// <summary>Reads a raw HID report without blocking.</summary>
    internal int ReceiveRaw(HidDeviceHandle deviceHandle, Span<byte> buffer)
    {
        ObjectDisposedException.ThrowIf(deviceHandle.IsClosed || deviceHandle.IsInvalid, deviceHandle);
        if (buffer.IsEmpty)
            throw new ArgumentException("The receive buffer cannot be empty.", nameof(buffer));

        EnsureInitialized();
        byte[] rented = ArrayPool<byte>.Shared.Rent(buffer.Length);
        try
        {
            int result = HIDapi.Read(deviceHandle, rented, buffer.Length);
            if (result > 0)
                rented.AsSpan(0, Math.Min(result, buffer.Length)).CopyTo(buffer);
            return result;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    internal void Report(WiimoteLogLevel level, string message)
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

    private void EnsureInitialized()
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

    private async Task ProcessWritesAsync()
    {
        await foreach (WriteRequest request in _writeChannel.Reader.ReadAllAsync())
        {
            try
            {
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
            }
            catch (Exception exception)
            {
                request.Completion.TrySetException(exception);
            }
            finally
            {
                request.Handle.DangerousRelease();
                if (MinimumWriteInterval > TimeSpan.Zero)
                    await Task.Delay(MinimumWriteInterval).ConfigureAwait(false);
            }
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(
            Volatile.Read(ref _isDisposed) != 0,
            this);
    }

    private sealed record WriteRequest(
        HidDeviceHandle Handle,
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
