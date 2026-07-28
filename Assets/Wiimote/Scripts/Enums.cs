namespace WiimoteApi;

public enum RegisterType
{
    Eeprom = 0x00,
    Control = 0x04
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
    IrCameraEnableSecondary = 0x1a
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
    ReportInterleavedAlternate = 0x3f
}

public enum IRDataType
{
    Basic = 1,
    Extended = 3,
    Full = 5
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
    MotionPlusClassic
}

public enum AccelCalibrationStep
{
    AButtonUp,
    ExpansionUp,
    LeftSideUp
}

public enum WiimoteType
{
    Original,
    RemotePlus,
    ProController
}
