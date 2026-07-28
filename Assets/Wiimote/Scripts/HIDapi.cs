using System.Runtime.InteropServices;
using System.Text;

namespace WiimoteApi;

internal static class HIDapi
{
    private const string LibraryName = "hidapi";

    internal static int Init() => NativeMethods.hid_init();

    internal static int Exit() => NativeMethods.hid_exit();

    internal static string? GetError(IntPtr device)
    {
        IntPtr message = NativeMethods.hid_error(device);
        return GetWideString(message);
    }

    internal static string? GetWideString(IntPtr value)
    {
        if (value == IntPtr.Zero)
            return null;
        if (OperatingSystem.IsWindows())
            return Marshal.PtrToStringUni(value);

        // wchar_t is UTF-32 on the Unix platforms supported by HIDAPI.
        var result = new StringBuilder();
        for (var offset = 0; offset < 4096; offset += sizeof(int))
        {
            int codePoint = Marshal.ReadInt32(value, offset);
            if (codePoint == 0)
                return result.ToString();

            result.Append(Rune.TryCreate(codePoint, out Rune rune) ? rune.ToString() : Rune.ReplacementChar.ToString());
        }

        throw new InvalidDataException("HIDAPI returned an unterminated string.");
    }

    internal static IntPtr Enumerate(ushort vendorId, ushort productId) =>
        NativeMethods.hid_enumerate(vendorId, productId);

    internal static void FreeEnumeration(IntPtr devices) =>
        NativeMethods.hid_free_enumeration(devices);

    internal static IntPtr OpenPath(string path) =>
        NativeMethods.hid_open_path(path);

    internal static void Close(IntPtr device) =>
        NativeMethods.hid_close(device);

    internal static int Read(IntPtr device, byte[] buffer) =>
        NativeMethods.hid_read(device, buffer, checked((nuint)buffer.Length));

    internal static int ReadTimeout(IntPtr device, byte[] buffer, TimeSpan timeout) =>
        NativeMethods.hid_read_timeout(
            device,
            buffer,
            checked((nuint)buffer.Length),
            checked((int)timeout.TotalMilliseconds));

    internal static int Write(IntPtr device, byte[] data) =>
        NativeMethods.hid_write(device, data, checked((nuint)data.Length));

    internal static int SetNonBlocking(IntPtr device, bool enabled) =>
        NativeMethods.hid_set_nonblocking(device, enabled ? 1 : 0);

    private static class NativeMethods
    {
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int hid_init();

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int hid_exit();

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr hid_error(IntPtr device);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr hid_enumerate(ushort vendorId, ushort productId);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void hid_free_enumeration(IntPtr devices);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void hid_close(IntPtr device);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        internal static extern IntPtr hid_open_path([MarshalAs(UnmanagedType.LPUTF8Str)] string path);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int hid_read(IntPtr device, [Out] byte[] data, nuint length);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int hid_read_timeout(
            IntPtr device,
            [Out] byte[] data,
            nuint length,
            int milliseconds);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int hid_set_nonblocking(IntPtr device, int nonBlocking);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int hid_write(IntPtr device, byte[] data, nuint length);
    }
}

[StructLayout(LayoutKind.Sequential)]
internal readonly struct HidDeviceInfo
{
    internal readonly IntPtr Path;
    internal readonly ushort VendorId;
    internal readonly ushort ProductId;
    internal readonly IntPtr SerialNumber;
    internal readonly ushort ReleaseNumber;
    internal readonly IntPtr ManufacturerString;
    internal readonly IntPtr ProductString;
    internal readonly ushort UsagePage;
    internal readonly ushort Usage;
    internal readonly int InterfaceNumber;
    internal readonly IntPtr Next;
}
