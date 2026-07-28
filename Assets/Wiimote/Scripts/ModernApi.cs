using System.Numerics;
using WiimoteApi.Util;

namespace WiimoteApi;

public sealed class WiimoteDataReceivedEventArgs(InputDataType reportType, int byteCount) : EventArgs
{
    public InputDataType ReportType { get; } = reportType;

    public int ByteCount { get; } = byteCount;
}

public partial class Wiimote
{
    public bool IsMotionPlusAttached => wmp_attached;

    public ExtensionController ExtensionType => current_ext;

    /// <summary>Sends a report and completes after HIDAPI has written it.</summary>
    public ValueTask<int> SendAsync(
        OutputDataType reportType,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(!IsConnected, this);

        byte[] report = GC.AllocateUninitializedArray<byte>(payload.Length + 1);
        report[0] = (byte)reportType;
        payload.Span.CopyTo(report.AsSpan(1));
        if (RumbleOn && report.Length > 1)
            report[1] |= 0x01;

        return WiimoteManager.SendRawAsync(DangerousHandle, report, cancellationToken);
    }

    public ValueTask<int> SetPlayerLedsAsync(
        bool led1,
        bool led2,
        bool led3,
        bool led4,
        CancellationToken cancellationToken = default)
    {
        byte leds = 0;
        if (led1) leds |= 0x10;
        if (led2) leds |= 0x20;
        if (led3) leds |= 0x40;
        if (led4) leds |= 0x80;
        return SendAsync(OutputDataType.Led, new byte[] { leds }, cancellationToken);
    }

    public ValueTask<int> SetReportModeAsync(
        InputDataType reportType,
        CancellationToken cancellationToken = default)
    {
        if (GetInputDataTypeSize(reportType) == 0)
            throw new ArgumentOutOfRangeException(nameof(reportType), reportType, "Unsupported input report type.");

        last_report_type = reportType;
        return SendAsync(
            OutputDataType.DataReportMode,
            new byte[] { 0x00, (byte)reportType },
            cancellationToken);
    }

    public ValueTask<int> RequestStatusAsync(CancellationToken cancellationToken = default)
    {
        expecting_status_report = true;
        return SendAsync(OutputDataType.StatusInfoRequest, new byte[] { 0x00 }, cancellationToken);
    }
}

public partial class ButtonData
{
    public bool DPadLeft => d_left;
    public bool DPadRight => d_right;
    public bool DPadUp => d_up;
    public bool DPadDown => d_down;
    public bool A => a;
    public bool B => b;
    public bool One => one;
    public bool Two => two;
    public bool Plus => plus;
    public bool Minus => minus;
    public bool Home => home;
}

public partial class AccelData
{
    public IReadOnlyList<int> RawAcceleration => Accel;

    public Vector3 RawAccelerationVector => new(Accel[0], Accel[1], Accel[2]);

    public Vector3 ZeroPoint
    {
        get
        {
            float[] value = GetAccelZeroPoints();
            return new Vector3(value[0], value[1], value[2]);
        }
    }

    public Vector3 CalibratedAcceleration
    {
        get
        {
            float[] value = GetCalibratedAccelData();
            return new Vector3(value[0], value[1], value[2]);
        }
    }
}

public partial class NunchuckData
{
    public IReadOnlyList<int> RawAcceleration => accel;
    public IReadOnlyList<byte> RawStick => stick;
    public bool C => c;
    public bool Z => z;

    public Vector2 NormalizedStick
    {
        get
        {
            float[] value = GetStick01();
            return new Vector2(value[0], value[1]);
        }
    }
}

public partial class ClassicControllerData
{
    public IReadOnlyList<byte> LeftStick => lstick;
    public IReadOnlyList<byte> RightStick => rstick;
    public Vector2 NormalizedLeftStick => ToVector2(GetLeftStick01());
    public Vector2 NormalizedRightStick => ToVector2(GetRightStick01());
    public byte LeftTrigger => ltrigger_range;
    public byte RightTrigger => rtrigger_range;
    public bool LeftTriggerPressed => ltrigger_switch;
    public bool RightTriggerPressed => rtrigger_switch;
    public bool A => a;
    public bool B => b;
    public bool X => x;
    public bool Y => y;
    public bool Plus => plus;
    public bool Minus => minus;
    public bool Home => home;
    public bool ZL => zl;
    public bool ZR => zr;
    public bool DPadUp => dpad_up;
    public bool DPadDown => dpad_down;
    public bool DPadLeft => dpad_left;
    public bool DPadRight => dpad_right;

    private static Vector2 ToVector2(float[] value) => new(value[0], value[1]);
}

public partial class WiiUProData
{
    public IReadOnlyList<ushort> LeftStick => lstick;
    public IReadOnlyList<ushort> RightStick => rstick;
    public Vector2 NormalizedLeftStick => ToVector2(GetLeftStick01());
    public Vector2 NormalizedRightStick => ToVector2(GetRightStick01());
    public bool LeftStickPressed => lstick_button;
    public bool RightStickPressed => rstick_button;
    public bool A => a;
    public bool B => b;
    public bool X => x;
    public bool Y => y;
    public bool Plus => plus;
    public bool Minus => minus;
    public bool Home => home;
    public bool L => l;
    public bool R => r;
    public bool ZL => zl;
    public bool ZR => zr;
    public bool DPadUp => dpad_up;
    public bool DPadDown => dpad_down;
    public bool DPadLeft => dpad_left;
    public bool DPadRight => dpad_right;

    private static Vector2 ToVector2(float[] value) => new(value[0], value[1]);
}

public partial class StatusData
{
    public IReadOnlyList<bool> PlayerLeds => led;
    public bool IsBatteryLow => battery_low;
    public bool IsExtensionConnected => ext_connected;
    public bool IsSpeakerEnabled => speaker_enabled;
    public bool IsIrEnabled => ir_enabled;
    public byte BatteryLevel => battery_level;
    public float BatteryLevelNormalized => battery_level / 255f;
}

public partial class IRData
{
    public ReadOnlyMatrix<int> Points => ir;

    public Vector2 PointingPosition
    {
        get
        {
            float[] value = GetPointingPosition();
            return new Vector2(value[0], value[1]);
        }
    }

    public Vector2 GetMidpoint(bool predict = true)
    {
        float[] value = GetIRMidpoint(predict);
        return new Vector2(value[0], value[1]);
    }
}
