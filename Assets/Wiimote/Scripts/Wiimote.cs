using System.Buffers.Binary;
using WiimoteApi.Internal;
using WiimoteApi.Util;

namespace WiimoteApi
{

    public sealed partial class Wiimote : IDisposable
    {
        /// Represents whether or not to turn on rumble when sending reports to
        /// the Wii Remote.  This will only be applied when a data report is sent.
        /// That is, simply setting this flag will not instantly enable rumble.
        public bool RumbleEnabled { get; private set; }

        /// Accelerometer data component
        public AccelData Accel => _Accel;
        private AccelData _Accel;

        /// If a Nunchuck is currently connected to the Wii Remote's extension port,
        /// this contains all relevant Nunchuck controller data as it is reported by
        /// the Wiimote.  If no Nunchuck is connected, this is \c null.
        ///
        /// \sa _current_ext
        public NunchuckData? Nunchuck
        {
            get
            {
                if (_current_ext == ExtensionController.Nunchuck)
                    return (NunchuckData?)_Extension;
                return null;
            }
        }

        /// If a Classic Controller is currently connected to the Wii Remote's extension port,
        /// this contains all relevant Classic Controller data as it is reported by
        /// the Wiimote.  If no Classic Controller is connected, this is \c null.
        ///
        /// \sa _current_ext
        public ClassicControllerData? ClassicController
        {
            get
            {
                if (_current_ext == ExtensionController.Classic)
                    return (ClassicControllerData?)_Extension;
                return null;
            }
        }

        /// If a Wii Motion Plus is currently connected to the Wii Remote's extension port,
        /// and has been activated by ActivateWiiMotionPlus(), this contains all relevant 
        /// Wii Motion Plus controller data as it is reported by the Wiimote.  If no
        /// WMP is connected, this is \c null.
        ///
        /// \sa _current_ext, _wmp_attached, ActivateWiiMotionPlus()
        public MotionPlusData? MotionPlus
        {
            get
            {
                if (_current_ext == ExtensionController.MotionPlus)
                    return (MotionPlusData?)_Extension;
                return null;
            }
        }

        /// If this Wiimote is a Wii U Pro Controller,
        /// this contains all relevant Pro Controller data as it is reported by
        /// the Controller.  If this Wiimote is not a Wii U Pro Controller, this is \c null.
        ///
        /// \sa _current_ext
        public WiiUProData? WiiUPro
        {
            get
            {
                if (_current_ext == ExtensionController.WiiUPro)
                    return (WiiUProData?)_Extension;
                return null;
            }
        }

        private IWiimoteData? _Extension;

        /// Button data component.
        public ButtonData Button => _Button;
        private ButtonData _Button;
        /// IR data component.
        public IRData Ir => _Ir;
        private IRData _Ir;
        /// Status info data component.
        public StatusData Status => _Status;
        private StatusData _Status;

        /// A pointer representing HIDApi's low-level device handle to this
        /// Wii Remote.  Use this when interfacing directly with HIDApi.
        private readonly HidDeviceHandle _handle;
        private readonly WiimoteManager _manager;

        /// <summary>Gets whether this controller still owns an open HID handle.</summary>
        public bool IsConnected => !_handle.IsClosed && !_handle.IsInvalid;

        internal HidDeviceHandle Handle => _handle;

        /// The RAW (unprocessesed) extension data reported by the Wii Remote.  This could
        /// be used for debugging new / undocumented extension controllers.
        public ReadOnlyMemory<byte> RawExtension => _rawExtension;
        private byte[] _rawExtension = [];

        /// The low-level bluetooth HID path of this Wii Remote.  Use this
        /// when interfacing directly with HIDApi.
        public string DevicePath { get; }

        public WiimoteType Type => _Type;
        private WiimoteType _Type;

        private RegisterReadData? CurrentReadData = null;
        private readonly object _registerReadLock = new();

        private InputDataType last_report_type = InputDataType.ReportButtons;
        private bool expecting_status_report = false;

        /// <summary>Raised after a complete input report has been interpreted.</summary>
        public event EventHandler<WiimoteDataReceivedEventArgs>? DataReceived;

        /// <summary>Gets the most recently interpreted input report type.</summary>
        public InputDataType? LastReportType { get; private set; }

        /// True if a Wii Motion Plus is attached to the Wii Remote, and it
        /// has NOT BEEN ACTIVATED.  When the WMP is activated this value is
        /// false.  This is only updated when WMP state is requested from
        /// Wii Remote registers (see: RequestIdentifyWiiMotionPlus())
        private bool _wmp_attached = false;

        /// The current extension connected to the Wii Remote.  This is only updated
        /// when the Wii Remote reports an extension change (this should update
        /// automatically).
        private ExtensionController _current_ext = ExtensionController.None;


        private byte[] InterleavedDataBuffer = new byte[18];
        private bool ExpectingSecondInterleavedPacket = false;

        private bool ExpectingWiiMotionPlusSwitch = false;

        internal Wiimote(
            WiimoteManager manager,
            HidDeviceHandle handle,
            string devicePath,
            WiimoteType type)
        {
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
            _handle = handle ?? throw new ArgumentNullException(nameof(handle));
            DevicePath = devicePath ?? throw new ArgumentNullException(nameof(devicePath));
            _Type = type;

            _Accel = new AccelData();
            _Button = new ButtonData();
            _Ir = new IRData(this);
            _Status = new StatusData();
            _Extension = null;

            //RequestIdentifyWiiMotionPlus(); // why not?
        }

        public void Dispose()
        {
            _handle.Dispose();
            _manager.Detach(this);
            GC.SuppressFinalize(this);
        }

        private static byte[] ID_InactiveMotionPlus = new byte[] { 0x00, 0x00, 0xA6, 0x20, 0x00, 0x05 };

        private void RespondIdentifyWiiMotionPlus(byte[] data)
        {
            if (data.Length != ID_InactiveMotionPlus.Length)
            {
                _wmp_attached = false;
                return;
            }

            if (data[0] == 0x01)                    // This is a weird inconsistency with some Wii Remote Pluses.  They don't have the -TR suffix
                _Type = WiimoteType.RemotePlus;    // or a different PID as an identifier.  Instead they have a different WMP extension identifier.
                                                    // It occurs on some of the oldest Wii Remote Pluses available (pre-2012).

            for (int x = 0; x < data.Length; x++)
            {
                // [x != 4] is necessary because byte 5 of the identifier changes based on the state of the remote
                // It is 0x00 on startup, 0x04 when deactivated, 0x05 when deactivated nunchuck passthrough,
                // and 0x07 when deactivated classic passthrough
                //
                // [x != 0] is necessary due to the inconsistency noted above.
                if (x != 4 && x != 0 && data[x] != ID_InactiveMotionPlus[x])
                {
                    _wmp_attached = false;
                    return;
                }
            }
            _wmp_attached = true;
        }

        private const long ID_ActiveMotionPlus = 0x0000A4200405;
        private const long ID_ActiveMotionPlus_Nunchuck = 0x0000A4200505;
        private const long ID_ActiveMotionPlus_Classic = 0x0000A4200705;
        private const long ID_Nunchuck = 0x0000A4200000;
        private const long ID_Classic = 0x0000A4200101;
        private const long ID_ClassicPro = 0x0100A4200101;
        private const long ID_WiiUPro = 0x0000A4200120;


        private void RespondIdentifyExtension(byte[] data)
        {
            if (data.Length != 6)
                return;

            byte[] resized = new byte[8];
            for (int x = 0; x < 6; x++) resized[x] = data[5 - x];
            long val = BitConverter.ToInt64(resized, 0);

            // Disregard bytes 0 and 5 - see RespondIdentifyWiiMotionPlus()
            if ((val | 0xff000000ff00) == (ID_ActiveMotionPlus | 0xff000000ff00))
            {
                _current_ext = ExtensionController.MotionPlus;
                if (_Extension == null || _Extension.GetType() != typeof(MotionPlusData))
                    _Extension = new MotionPlusData();
            }
            else if (val == ID_ActiveMotionPlus_Nunchuck)
            {
                _current_ext = ExtensionController.MotionPlusNunchuck;
                _Extension = null;
            }
            else if (val == ID_ActiveMotionPlus_Classic)
            {
                _current_ext = ExtensionController.MotionPlusClassic;
                _Extension = null;
            }
            else if (val == ID_ClassicPro)
            {
                _current_ext = ExtensionController.ClassicPro;
                _Extension = null;
            }
            else if (val == ID_Nunchuck)
            {
                _current_ext = ExtensionController.Nunchuck;
                if (_Extension == null || _Extension.GetType() != typeof(NunchuckData))
                    _Extension = new NunchuckData();
            }
            else if (val == ID_Classic)
            {
                _current_ext = ExtensionController.Classic;
                if (_Extension == null || _Extension.GetType() != typeof(ClassicControllerData))
                    _Extension = new ClassicControllerData();
            }
            else if (val == ID_WiiUPro)
            {
                _current_ext = ExtensionController.WiiUPro;
                _Type = WiimoteType.ProController;
                if (_Extension == null || _Extension.GetType() != typeof(WiiUProData))
                    _Extension = new WiiUProData();
            }
            else
            {
                _current_ext = ExtensionController.None;
                _Extension = null;
            }
        }

        #region Read
        /// \brief Reads and interprets data reported by the Wii Remote.
        /// \return On success, > 0, < 0 on failure, 0 if nothing has been recieved.
        /// 
        /// Wii Remote reads function similarly to a Queue, in FIFO (first in, first out) order.
        /// For example, if two reports were sent since the last \c ReadWiimoteData() call,
        /// this call will only read and interpret the first of those two (and "pop" it off
        /// of the queue).  So, in order to make sure you don't fall behind the Wiimote's update
        /// frequency, you can do something like this (in a game loop for example):
        ///
        /// \code
        /// Wii Remote wiimote;
        /// int ret;
        /// do
        /// {
        ///     ret = wiimote.ReadWiimoteData();
        /// } while (ret > 0);
        /// \endcode
        private int ReadOneReport()
        {
            byte[] buf = new byte[22];
            int status = _manager.ReceiveRaw(Handle, buf);
            if (status <= 0) return status; // Either there is some sort of error or we haven't recieved anything

            InputDataType reportType = (InputDataType)buf[0];
            int typesize = GetInputDataTypeSize(reportType);
            if (typesize == 0 || status < typesize + 1)
            {
                LogWarning($"Ignoring malformed or unknown report 0x{buf[0]:X2} ({status} bytes).");
                return -3;
            }

            ReadOnlySpan<byte> data = buf.AsSpan(1, typesize);

            if (_manager.EnableDebugLogging)
                Log($"Received: [{buf[0]:X2}] {Convert.ToHexString(data)}");

            ReadOnlySpan<byte> extensionData = default;

            switch (reportType) // buf[0] is the output ID byte
            {
                case InputDataType.StatusInfo: // done.
                    byte flags = data[2];
                    byte battery_level = data[5];

                    Button.InterpretData(data[..2]);

                    bool old_ext_connected = Status.IsExtensionConnected;

                    Status.InterpretData([flags, battery_level]);

                    if (expecting_status_report)
                    {
                        expecting_status_report = false;
                    }
                    else                                        // We haven't requested any data report type, meaning a controller has connected.
                    {
                        _ = ObserveBackgroundOperationAsync(
                            SetReportModeAsync(last_report_type).AsTask());
                    }

                    if (Status.IsExtensionConnected != old_ext_connected && Type != WiimoteType.ProController)
                    {
                        if (Status.IsExtensionConnected)        // The Wii Remote doesn't allow reading from the extension identifier
                        {                                        // when nothing is connected.
                            Log("An extension has been connected.");
                            if (_current_ext != ExtensionController.MotionPlus)
                            {
                                _ = ObserveBackgroundOperationAsync(
                                    HandleExtensionConnectedAsync());
                            }
                            else
                                ExpectingWiiMotionPlusSwitch = false;
                        }
                        else
                        {
                            if (!ExpectingWiiMotionPlusSwitch)
                                _current_ext = ExtensionController.None;
                            Log("An extension has been disconnected.");
                        }
                    }
                    break;
                case InputDataType.ReadMemoryRegisters: // done.
                    Button.InterpretData(data[..2]);

                    RegisterReadData? currentRead;
                    lock (_registerReadLock)
                        currentRead = CurrentReadData;

                    if (currentRead == null)
                    {
                        LogWarning("Recived Register Read Report when none was expected.  Ignoring.");
                        return status;
                    }

                    byte size = (byte)((data[2] >> 4) + 0x01);
                    byte error = (byte)(data[2] & 0x0f);
                    // Error 0x07 means reading from a write-only register
                    // Offset 0xa600fa is for the Wii Motion Plus.  This error code can be expected behavior in this case.
                    if (error == 0x07)
                    {
                        currentRead.Fail(new IOException(
                            $"The Wii Remote rejected register read 0x{currentRead.Offset:X6} with error 7."));
                        if (currentRead.Offset != 0xa600fa)
                            LogError("Wiimote reports Read Register error 7: Attempting to read from a write-only register (" + currentRead.Offset.ToString("x") + ").  Aborting read.");

                        lock (_registerReadLock)
                        {
                            if (ReferenceEquals(CurrentReadData, currentRead))
                                CurrentReadData = null;
                        }
                        return status;
                    }
                    // lowOffset is reversed because the Wii Remote reports are in Big Endian order
                    ushort lowOffset = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(3, 2));
                    ushort expected = (ushort)currentRead.ExpectedOffset;
                    if (expected != lowOffset)
                        LogWarning("Expected Register Read Offset (" + expected + ") does not match reported offset from Wii Remote (" + lowOffset + ")");
                    if (!currentRead.AppendData(data.Slice(5, size)))
                    {
                        currentRead.Fail(new InvalidDataException(
                            "The Wii Remote returned register data outside the requested range."));
                        lock (_registerReadLock)
                        {
                            if (ReferenceEquals(CurrentReadData, currentRead))
                                CurrentReadData = null;
                        }
                        return status;
                    }
                    if (currentRead.ExpectedOffset >= currentRead.Offset + currentRead.Size)
                    {
                        lock (_registerReadLock)
                        {
                            if (ReferenceEquals(CurrentReadData, currentRead))
                                CurrentReadData = null;
                        }
                    }

                    break;
                case InputDataType.AcknowledgeOutputReport:
                    Button.InterpretData(data[..2]);
                    // TODO: doesn't do any actual error handling, or do any special code about acknowledging the output report.
                    break;
                case InputDataType.ReportButtons: // done.
                    Button.InterpretData(data);
                    break;
                case InputDataType.ReportButtonsAccelerometer: // done.
                    Button.InterpretData(data[..2]);
                    Accel.InterpretData(data);
                    break;
                case InputDataType.ReportButtonsExtension8: // done.
                    Button.InterpretData(data[..2]);
                    extensionData = data.Slice(2, 8);

                    if (_Extension != null)
                        _Extension.InterpretData(extensionData);
                    break;
                case InputDataType.ReportButtonsAccelerometerIr12: // done.
                    Button.InterpretData(data[..2]);
                    Accel.InterpretData(data[..5]);
                    Ir.InterpretData(data.Slice(5, 12));
                    break;
                case InputDataType.ReportButtonsExtension19: // done.
                    Button.InterpretData(data[..2]);
                    extensionData = data.Slice(2, 19);

                    if (_Extension != null)
                        _Extension.InterpretData(extensionData);
                    break;
                case InputDataType.ReportButtonsAccelerometerExtension16: // done.
                    Button.InterpretData(data[..2]);
                    Accel.InterpretData(data[..5]);
                    extensionData = data.Slice(5, 16);

                    if (_Extension != null)
                        _Extension.InterpretData(extensionData);
                    break;
                case InputDataType.ReportButtonsIr10Extension9: // done.
                    Button.InterpretData(data[..2]);
                    Ir.InterpretData(data.Slice(2, 10));
                    extensionData = data.Slice(12, 9);

                    if (_Extension != null)
                        _Extension.InterpretData(extensionData);
                    break;
                case InputDataType.ReportButtonsAccelerometerIr10Extension6: // done.
                    Button.InterpretData(data[..2]);
                    Accel.InterpretData(data[..5]);
                    Ir.InterpretData(data.Slice(5, 10));
                    extensionData = data.Slice(15, 6);

                    if (_Extension != null)
                        _Extension.InterpretData(extensionData);
                    break;
                case InputDataType.ReportExtension21: // done.
                    extensionData = data;

                    if (_Extension != null)
                        _Extension.InterpretData(extensionData);
                    break;
                case InputDataType.ReportInterleaved:
                    if (!ExpectingSecondInterleavedPacket)
                    {
                        ExpectingSecondInterleavedPacket = true;
                        data.CopyTo(InterleavedDataBuffer);
                    }
                    else if (_manager.EnableDebugLogging)
                    {
                        LogWarning(
                            "Recieved two REPORT_INTERLEAVED (" + InputDataType.ReportInterleaved.ToString("x") + ") reports in a row!  "
                            + "Expected REPORT_INTERLEAVED_ALT (" + InputDataType.ReportInterleavedAlternate.ToString("x") + ").  Ignoring!"
                        );
                    }

                    break;
                case InputDataType.ReportInterleavedAlternate:
                    if (ExpectingSecondInterleavedPacket)
                    {
                        ExpectingSecondInterleavedPacket = false;

                        Button.InterpretData(data[..2]);
                        Ir.InterpretDataInterleaved(
                            InterleavedDataBuffer.AsSpan(3, 18),
                            data.Slice(3, 18));
                        Accel.InterpretDataInterleaved(InterleavedDataBuffer, data);
                    }
                    else if (_manager.EnableDebugLogging)
                    {
                        LogWarning(
                            "Recieved two REPORT_INTERLEAVED_ALT (" + InputDataType.ReportInterleavedAlternate.ToString("x") + ") reports in a row!  "
                            + "Expected REPORT_INTERLEAVED (" + InputDataType.ReportInterleaved.ToString("x") + ").  Ignoring!"
                        );
                    }
                    break;
            }

            if (extensionData.IsEmpty)
                _rawExtension = [];
            else
                _rawExtension = extensionData.ToArray();

            LastReportType = reportType;
            DataReceived?.Invoke(this, new WiimoteDataReceivedEventArgs(reportType, status));
            return status;
        }

        /// <summary>Reads and interprets all reports currently waiting in HIDAPI.</summary>
        /// <returns>The number of reports interpreted.</returns>
        public int ReadAvailable()
        {
            int count = 0;
            int result;
            while ((result = ReadOneReport()) > 0)
                count++;

            return result < 0 ? result : count;
        }

        /// <summary>Continuously polls this controller until cancellation is requested.</summary>
        public async Task ReadLoopAsync(
            TimeSpan pollInterval,
            CancellationToken cancellationToken = default)
        {
            if (pollInterval < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(pollInterval));

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int result = ReadAvailable();
                if (result < 0)
                    throw new IOException($"HID read failed with error {result}.");

                await Task.Delay(pollInterval, cancellationToken).ConfigureAwait(false);
            }
        }

        /// The size, in bytes, of a given Wii Remote InputDataType when reported by the Wiimote.
        ///
        /// This is at most 21 bytes.
        internal static int GetInputDataTypeSize(InputDataType type)
        {
            switch (type)
            {
                case InputDataType.StatusInfo:
                    return 6;
                case InputDataType.ReadMemoryRegisters:
                    return 21;
                case InputDataType.AcknowledgeOutputReport:
                    return 4;
                case InputDataType.ReportButtons:
                    return 2;
                case InputDataType.ReportButtonsAccelerometer:
                    return 5;
                case InputDataType.ReportButtonsExtension8:
                    return 10;
                case InputDataType.ReportButtonsAccelerometerIr12:
                    return 17;
                case InputDataType.ReportButtonsExtension19:
                    return 21;
                case InputDataType.ReportButtonsAccelerometerExtension16:
                    return 21;
                case InputDataType.ReportButtonsIr10Extension9:
                    return 21;
                case InputDataType.ReportButtonsAccelerometerIr10Extension6:
                    return 21;
                case InputDataType.ReportExtension21:
                    return 21;
                case InputDataType.ReportInterleaved:
                    return 21;
                case InputDataType.ReportInterleavedAlternate:
                    return 21;
            }
            return 0;
        }

        private void Log(object? message) =>
            _manager.Report(WiimoteLogLevel.Debug, Convert.ToString(message) ?? string.Empty);

        private void LogWarning(object? message) =>
            _manager.Report(WiimoteLogLevel.Warning, Convert.ToString(message) ?? string.Empty);

        private void LogError(object? message) =>
            _manager.Report(WiimoteLogLevel.Error, Convert.ToString(message) ?? string.Empty);

        internal static bool IsDataReportMode(InputDataType type) =>
            type is
                InputDataType.ReportButtons or
                InputDataType.ReportButtonsAccelerometer or
                InputDataType.ReportButtonsExtension8 or
                InputDataType.ReportButtonsAccelerometerIr12 or
                InputDataType.ReportButtonsExtension19 or
                InputDataType.ReportButtonsAccelerometerExtension16 or
                InputDataType.ReportButtonsIr10Extension9 or
                InputDataType.ReportButtonsAccelerometerIr10Extension6 or
                InputDataType.ReportExtension21 or
                InputDataType.ReportInterleaved or
                InputDataType.ReportInterleavedAlternate;


        #endregion

    }
}
