namespace WiimoteApi;

public enum RegisterType
{
    Eeprom = 0x00,
    Control = 0x04,

    // Source-compatible names from the Unity package.
    EEPROM = Eeprom,
    CONTROL = Control
}

public enum OutputDataType
{
    Led = 0x11,
    DataReportMode = 0x12,
    IrCameraEnable = 0x13,
    SpeakerEnable = 0x14,
    StatusInfoRequest = 0x15,
    WriteMemoryRegisters = 0x16,
    ReadMemoryRegisters = 0x17,
    SpeakerData = 0x18,
    SpeakerMute = 0x19,
    IrCameraEnableSecondary = 0x1a,

    LED = Led,
    DATA_REPORT_MODE = DataReportMode,
    IR_CAMERA_ENABLE = IrCameraEnable,
    SPEAKER_ENABLE = SpeakerEnable,
    STATUS_INFO_REQUEST = StatusInfoRequest,
    WRITE_MEMORY_REGISTERS = WriteMemoryRegisters,
    READ_MEMORY_REGISTERS = ReadMemoryRegisters,
    SPEAKER_DATA = SpeakerData,
    SPEAKER_MUTE = SpeakerMute,
    IR_CAMERA_ENABLE_2 = IrCameraEnableSecondary
}

public enum InputDataType
{
    StatusInfo = 0x20,
    ReadMemoryRegisters = 0x21,
    AcknowledgeOutputReport = 0x22,
    ReportButtons = 0x30,
    ReportButtonsAccelerometer = 0x31,
    ReportButtonsExtension8 = 0x32,
    ReportButtonsAccelerometerIr12 = 0x33,
    ReportButtonsExtension19 = 0x34,
    ReportButtonsAccelerometerExtension16 = 0x35,
    ReportButtonsIr10Extension9 = 0x36,
    ReportButtonsAccelerometerIr10Extension6 = 0x37,
    ReportExtension21 = 0x3d,
    ReportInterleaved = 0x3e,
    ReportInterleavedAlternate = 0x3f,

    STATUS_INFO = StatusInfo,
    READ_MEMORY_REGISTERS = ReadMemoryRegisters,
    ACKNOWLEDGE_OUTPUT_REPORT = AcknowledgeOutputReport,
    REPORT_BUTTONS = ReportButtons,
    REPORT_BUTTONS_ACCEL = ReportButtonsAccelerometer,
    REPORT_BUTTONS_EXT8 = ReportButtonsExtension8,
    REPORT_BUTTONS_ACCEL_IR12 = ReportButtonsAccelerometerIr12,
    REPORT_BUTTONS_EXT19 = ReportButtonsExtension19,
    REPORT_BUTTONS_ACCEL_EXT16 = ReportButtonsAccelerometerExtension16,
    REPORT_BUTTONS_IR10_EXT9 = ReportButtonsIr10Extension9,
    REPORT_BUTTONS_ACCEL_IR10_EXT6 = ReportButtonsAccelerometerIr10Extension6,
    REPORT_EXT21 = ReportExtension21,
    REPORT_INTERLEAVED = ReportInterleaved,
    REPORT_INTERLEAVED_ALT = ReportInterleavedAlternate
}

public enum IRDataType
{
    Basic = 1,
    Extended = 3,
    Full = 5,

    BASIC = Basic,
    EXTENDED = Extended,
    FULL = Full
}

public enum ExtensionController
{
    None,
    Nunchuck,
    Classic,
    ClassicPro,
    WiiUPro,
    MotionPlus,
    MotionPlusNunchuck,
    MotionPlusClassic,

    NONE = None,
    NUNCHUCK = Nunchuck,
    CLASSIC = Classic,
    CLASSIC_PRO = ClassicPro,
    WIIU_PRO = WiiUPro,
    MOTIONPLUS = MotionPlus,
    MOTIONPLUS_NUNCHUCK = MotionPlusNunchuck,
    MOTIONPLUS_CLASSIC = MotionPlusClassic
}

public enum AccelCalibrationStep
{
    AButtonUp,
    ExpansionUp,
    LeftSideUp,

    A_BUTTON_UP = AButtonUp,
    EXPANSION_UP = ExpansionUp,
    LEFT_SIDE_UP = LeftSideUp
}

public enum WiimoteType
{
    Original,
    RemotePlus,
    ProController,

    WIIMOTE = Original,
    WIIMOTEPLUS = RemotePlus,
    PROCONTROLLER = ProController
}
