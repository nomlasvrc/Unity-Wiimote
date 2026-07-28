using System.Numerics;
using System.Buffers.Binary;
using WiimoteApi.Internal;
using WiimoteApi.Util;

namespace WiimoteApi;

public sealed class WiimoteDataReceivedEventArgs(InputDataType reportType, int byteCount) : EventArgs
{
    public InputDataType ReportType { get; } = reportType;

    public int ByteCount { get; } = byteCount;
}

public partial class Wiimote
{
    public bool IsMotionPlusAttached => _wmp_attached;

    public ExtensionController ExtensionType => _current_ext;

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
        if (RumbleEnabled && report.Length > 1)
            report[1] |= 0x01;

        return _manager.SendRawAsync(Handle, report, cancellationToken);
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

    public async ValueTask<int> SetReportModeAsync(
        InputDataType reportType,
        CancellationToken cancellationToken = default)
    {
        if (!IsDataReportMode(reportType))
            throw new ArgumentOutOfRangeException(nameof(reportType), reportType, "Unsupported input report type.");

        last_report_type = reportType;
        ExpectingSecondInterleavedPacket = false;
        return await SendAsync(
                OutputDataType.DataReportMode,
                new byte[] { 0x00, (byte)reportType },
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async ValueTask<int> RequestStatusAsync(CancellationToken cancellationToken = default)
    {
        expecting_status_report = true;
        try
        {
            return await SendAsync(
                    OutputDataType.StatusInfoRequest,
                    new byte[] { 0x00 },
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch
        {
            expecting_status_report = false;
            throw;
        }
    }

    /// <summary>Changes rumble state immediately by sending a status request.</summary>
    public async ValueTask<int> SetRumbleAsync(
        bool enabled,
        CancellationToken cancellationToken = default)
    {
        bool previous = RumbleEnabled;
        RumbleEnabled = enabled;
        try
        {
            return await RequestStatusAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            RumbleEnabled = previous;
            throw;
        }
    }

    public async ValueTask<int> WriteRegisterAsync(
        RegisterType registerType,
        int offset,
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default)
    {
        if (data.Length > 16)
            throw new ArgumentOutOfRangeException(nameof(data), "A register write is limited to 16 bytes.");

        byte[] payload = new byte[21];
        payload[0] = (byte)registerType;
        WriteUInt24BigEndian(payload.AsSpan(1, 3), offset);
        payload[4] = (byte)data.Length;
        data.Span.CopyTo(payload.AsSpan(5));
        return await SendAsync(
                OutputDataType.WriteMemoryRegisters,
                payload,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<byte[]> ReadRegisterAsync(
        RegisterType registerType,
        int offset,
        int size,
        CancellationToken cancellationToken = default)
    {
        if (size is < 1 or > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(size));

        var completion = new TaskCompletionSource<byte[]>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var request = new RegisterReadData(
            offset,
            size,
            data => completion.TrySetResult(data),
            exception => completion.TrySetException(exception));

        lock (_registerReadLock)
        {
            if (CurrentReadData is not null)
                throw new InvalidOperationException("Another register read is already pending.");

            CurrentReadData = request;
        }

        using CancellationTokenRegistration registration = cancellationToken.Register(() =>
        {
            lock (_registerReadLock)
            {
                if (ReferenceEquals(CurrentReadData, request))
                    CurrentReadData = null;
            }

            completion.TrySetCanceled(cancellationToken);
        });

        try
        {
            byte[] payload = new byte[6];
            payload[0] = (byte)registerType;
            WriteUInt24BigEndian(payload.AsSpan(1, 3), offset);
            BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(4, 2), checked((ushort)size));
            await SendAsync(
                    OutputDataType.ReadMemoryRegisters,
                    payload,
                    cancellationToken)
                .ConfigureAwait(false);
            return await completion.Task.ConfigureAwait(false);
        }
        catch
        {
            lock (_registerReadLock)
            {
                if (ReferenceEquals(CurrentReadData, request))
                    CurrentReadData = null;
            }

            throw;
        }
    }

    public async Task<bool> IdentifyMotionPlusAsync(CancellationToken cancellationToken = default)
    {
        byte[] data = await ReadRegisterAsync(
                RegisterType.Control,
                0xA600FA,
                6,
                cancellationToken)
            .ConfigureAwait(false);
        RespondIdentifyWiiMotionPlus(data);
        return IsMotionPlusAttached;
    }

    public async Task ActivateMotionPlusAsync(CancellationToken cancellationToken = default)
    {
        if (!IsMotionPlusAttached)
            throw new InvalidOperationException("A Wii MotionPlus has not been identified.");

        await WriteRegisterAsync(
            RegisterType.Control,
            0xA600F0,
            new byte[] { 0x55 },
            cancellationToken);
        await WriteRegisterAsync(
            RegisterType.Control,
            0xA600FE,
            new byte[] { 0x04 },
            cancellationToken);
        _current_ext = ExtensionController.MotionPlus;
        ExpectingWiiMotionPlusSwitch = true;
    }

    public async Task DeactivateMotionPlusAsync(CancellationToken cancellationToken = default)
    {
        if (ExtensionType is not (
            ExtensionController.MotionPlus or
            ExtensionController.MotionPlusClassic or
            ExtensionController.MotionPlusNunchuck))
        {
            throw new InvalidOperationException("Wii MotionPlus is not active.");
        }

        await WriteRegisterAsync(
            RegisterType.Control,
            0xA400F0,
            new byte[] { 0x55 },
            cancellationToken);
        ExpectingWiiMotionPlusSwitch = true;
    }

    public async Task SetupIrCameraAsync(
        IRDataType type = IRDataType.Extended,
        CancellationToken cancellationToken = default)
    {
        await SetIrCameraEnabledAsync(true, cancellationToken);
        await WriteRegisterAsync(RegisterType.Control, 0xb00030, new byte[] { 0x08 }, cancellationToken);
        await WriteRegisterAsync(
            RegisterType.Control,
            0xb00000,
            new byte[] { 0x02, 0x00, 0x00, 0x71, 0x01, 0x00, 0xaa, 0x00, 0x64 },
            cancellationToken);
        await WriteRegisterAsync(RegisterType.Control, 0xb0001a, new byte[] { 0x63, 0x03 }, cancellationToken);
        await WriteRegisterAsync(RegisterType.Control, 0xb00033, new byte[] { (byte)type }, cancellationToken);
        await WriteRegisterAsync(RegisterType.Control, 0xb00030, new byte[] { 0x08 }, cancellationToken);

        InputDataType reportType = type switch
        {
            IRDataType.Basic => InputDataType.ReportButtonsAccelerometerIr10Extension6,
            IRDataType.Extended when ExtensionType == ExtensionController.None =>
                InputDataType.ReportButtonsAccelerometerIr12,
            IRDataType.Full => InputDataType.ReportInterleaved,
            _ => throw new InvalidOperationException(
                "Extended IR mode cannot be combined with extension data.")
        };
        await SetReportModeAsync(reportType, cancellationToken);
    }

    public async Task SetSpeakerEnabledAsync(
        bool enabled,
        CancellationToken cancellationToken = default) =>
        _ = await SendAsync(
            OutputDataType.SpeakerEnable,
            new byte[] { enabled ? (byte)0x04 : (byte)0x00 },
            cancellationToken);

    public async Task SetSpeakerMutedAsync(
        bool muted,
        CancellationToken cancellationToken = default) =>
        _ = await SendAsync(
            OutputDataType.SpeakerMute,
            new byte[] { muted ? (byte)0x04 : (byte)0x00 },
            cancellationToken);

    private async Task SetIrCameraEnabledAsync(
        bool enabled,
        CancellationToken cancellationToken)
    {
        byte value = enabled ? (byte)0x04 : (byte)0x00;
        await SendAsync(OutputDataType.IrCameraEnable, new byte[] { value }, cancellationToken);
        await SendAsync(
            OutputDataType.IrCameraEnableSecondary,
            new byte[] { value },
            cancellationToken);
    }

    private async Task ActivateExtensionAsync(CancellationToken cancellationToken = default)
    {
        if (!Status.IsExtensionConnected)
            throw new InvalidOperationException("No extension controller is connected.");

        await WriteRegisterAsync(
            RegisterType.Control,
            0xA400F0,
            new byte[] { 0x55 },
            cancellationToken);
        await WriteRegisterAsync(
            RegisterType.Control,
            0xA400FB,
            new byte[] { 0x00 },
            cancellationToken);
    }

    private async Task IdentifyExtensionAsync(CancellationToken cancellationToken = default)
    {
        byte[] data = await ReadRegisterAsync(
                RegisterType.Control,
                0xA400FA,
                6,
                cancellationToken)
            .ConfigureAwait(false);
        RespondIdentifyExtension(data);
    }

    private async Task HandleExtensionConnectedAsync()
    {
        await ActivateExtensionAsync().ConfigureAwait(false);
        await IdentifyExtensionAsync().ConfigureAwait(false);
    }

    private async Task ObserveBackgroundOperationAsync(Task operation)
    {
        try
        {
            await operation.ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            LogError($"Background controller operation failed: {exception.Message}");
        }
    }

    private static void WriteUInt24BigEndian(Span<byte> destination, int value)
    {
        if ((uint)value > 0x00ff_ffff)
            throw new ArgumentOutOfRangeException(nameof(value));

        destination[0] = (byte)(value >> 16);
        destination[1] = (byte)(value >> 8);
        destination[2] = (byte)value;
    }
}

public partial class ButtonData
{
    public bool DPadLeft => _d_left;
    public bool DPadRight => _d_right;
    public bool DPadUp => _d_up;
    public bool DPadDown => _d_down;
    public bool A => _a;
    public bool B => _b;
    public bool One => _one;
    public bool Two => _two;
    public bool Plus => _plus;
    public bool Minus => _minus;
    public bool Home => _home;
}

public partial class AccelData
{
    public ReadOnlyMemory<int> RawAcceleration => _accel;

    public Vector3 RawAccelerationVector => new(_accel[0], _accel[1], _accel[2]);

    public Vector3 ZeroPoint => new(
        (_accelCalibration[0, 0] + _accelCalibration[1, 0]) / 2f,
        (_accelCalibration[0, 1] + _accelCalibration[2, 1]) / 2f,
        (_accelCalibration[1, 2] + _accelCalibration[2, 2]) / 2f);

    public Vector3 CalibratedAcceleration
    {
        get
        {
            Vector3 zero = ZeroPoint;
            return new Vector3(
                (_accel[0] - zero.X) / (_accelCalibration[2, 0] - zero.X),
                (_accel[1] - zero.Y) / (_accelCalibration[1, 1] - zero.Y),
                (_accel[2] - zero.Z) / (_accelCalibration[0, 2] - zero.Z));
        }
    }

    public void Calibrate(AccelCalibrationStep step)
    {
        for (var axis = 0; axis < 3; axis++)
            _accelCalibration[(int)step, axis] = _accel[axis];
    }
}

public partial class NunchuckData
{
    public ReadOnlyMemory<int> RawAcceleration => _accel;
    public ReadOnlyMemory<byte> RawStick => _stick;
    public bool C => _c;
    public bool Z => _z;
    public Vector2 NormalizedStick => new(
        (_stick[0] - 35f) / 193f,
        (_stick[1] - 27f) / 193f);
}

public partial class ClassicControllerData
{
    public ReadOnlyMemory<byte> LeftStick => _lstick;
    public ReadOnlyMemory<byte> RightStick => _rstick;
    public Vector2 NormalizedLeftStick => new(_lstick[0] / 63f, _lstick[1] / 63f);
    public Vector2 NormalizedRightStick => new(_rstick[0] / 31f, _rstick[1] / 31f);
    public byte LeftTrigger => _ltrigger_range;
    public byte RightTrigger => _rtrigger_range;
    public bool LeftTriggerPressed => _ltrigger_switch;
    public bool RightTriggerPressed => _rtrigger_switch;
    public bool A => _a;
    public bool B => _b;
    public bool X => _x;
    public bool Y => _y;
    public bool Plus => _plus;
    public bool Minus => _minus;
    public bool Home => _home;
    public bool ZL => _zl;
    public bool ZR => _zr;
    public bool DPadUp => _dpad_up;
    public bool DPadDown => _dpad_down;
    public bool DPadLeft => _dpad_left;
    public bool DPadRight => _dpad_right;
}

public partial class WiiUProData
{
    public ReadOnlyMemory<ushort> LeftStick => _lstick;
    public ReadOnlyMemory<ushort> RightStick => _rstick;
    public Vector2 NormalizedLeftStick => new(
        _lstick[0] / (float)ushort.MaxValue,
        _lstick[1] / (float)ushort.MaxValue);
    public Vector2 NormalizedRightStick => new(
        _rstick[0] / (float)ushort.MaxValue,
        _rstick[1] / (float)ushort.MaxValue);
    public bool LeftStickPressed => _lstick_button;
    public bool RightStickPressed => _rstick_button;
    public bool A => _a;
    public bool B => _b;
    public bool X => _x;
    public bool Y => _y;
    public bool Plus => _plus;
    public bool Minus => _minus;
    public bool Home => _home;
    public bool L => _l;
    public bool R => _r;
    public bool ZL => _zl;
    public bool ZR => _zr;
    public bool DPadUp => _dpad_up;
    public bool DPadDown => _dpad_down;
    public bool DPadLeft => _dpad_left;
    public bool DPadRight => _dpad_right;
}

public partial class StatusData
{
    public ReadOnlyMemory<bool> PlayerLeds => _led;
    public bool IsBatteryLow => _battery_low;
    public bool IsExtensionConnected => _ext_connected;
    public bool IsSpeakerEnabled => _speaker_enabled;
    public bool IsIrEnabled => _ir_enabled;
    public byte BatteryLevel => _battery_level;
    public float BatteryLevelNormalized => _battery_level / 255f;
}

public partial class IRData
{
    public ReadOnlyMatrix<int> Points => _ir_readonly;

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
