using Microsoft.Win32.SafeHandles;

namespace WiimoteApi.Internal;

internal sealed class HidDeviceHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    internal HidDeviceHandle(IntPtr handle)
        : base(ownsHandle: true) =>
        SetHandle(handle);

    protected override bool ReleaseHandle()
    {
        HIDapi.Close(handle);
        return true;
    }
}
