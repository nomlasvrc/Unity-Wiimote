using System.Numerics;
using WiimoteApi;
using WiimoteApi.Internal;

var tests = new (string Name, Action Run)[]
{
    ("Button report parsing", TestButtons),
    ("Accelerometer report parsing", TestAccelerometer),
    ("Nunchuck report parsing", TestNunchuck),
    ("Classic Controller report parsing", TestClassicController),
    ("Status report parsing", TestStatus),
    ("Read-only memory behavior", TestReadOnlyMemory),
    ("Report mode validation", TestReportModeValidation)
};

foreach ((string name, Action run) in tests)
{
    run();
    Console.WriteLine($"PASS: {name}");
}

Console.WriteLine($"{tests.Length} tests passed.");

static Wiimote CreateOwner() =>
    new(new WiimoteManager(), new HidDeviceHandle(IntPtr.Zero), "test-device", WiimoteType.Original);

static void TestButtons()
{
    using Wiimote owner = CreateOwner();
    var data = new ButtonData();
    Assert(data.InterpretData([0x19, 0x9f]), "Expected a valid button report.");
    Assert(data.DPadLeft && data.DPadUp && data.Plus, "D-pad/Plus bits were not parsed.");
    Assert(data.A && data.B && data.One && data.Two && data.Minus && data.Home, "Button bits were not parsed.");
}

static void TestAccelerometer()
{
    using Wiimote owner = CreateOwner();
    var data = new AccelData();
    Assert(data.InterpretData([0x60, 0x60, 0x80, 0x81, 0x82]), "Expected a valid accelerometer report.");
    Assert(data.RawAccelerationVector == new Vector3(515, 517, 521), "Unexpected raw acceleration.");
}

static void TestNunchuck()
{
    using Wiimote owner = CreateOwner();
    var data = new NunchuckData();
    Assert(data.InterpretData([128, 128, 1, 2, 3, 0xfc]), "Expected a valid Nunchuck report.");
    Assert(data.RawAcceleration.Span[0] == 7 && data.RawAcceleration.Span[1] == 11 && data.RawAcceleration.Span[2] == 15,
        "Unexpected Nunchuck acceleration.");
    Assert(data.C && data.Z, "Active-low Nunchuck buttons were not parsed.");
}

static void TestClassicController()
{
    using Wiimote owner = CreateOwner();
    var data = new ClassicControllerData();
    Assert(data.InterpretData([0x3f, 0x3f, 0x1f, 0x1f, 0xff, 0xff]), "Expected a valid Classic Controller report.");
    Assert(data.LeftStick.Span[0] == 63 && data.LeftStick.Span[1] == 63, "Unexpected left stick values.");
    Assert(data.NormalizedLeftStick == Vector2.One, "Unexpected normalized left stick.");
}

static void TestStatus()
{
    using Wiimote owner = CreateOwner();
    var data = new StatusData();
    Assert(data.InterpretData([0x9f, 0x80]), "Expected a valid status report.");
    Assert(data.IsBatteryLow && data.IsExtensionConnected && data.IsSpeakerEnabled && data.IsIrEnabled,
        "Status flags were not parsed.");
    Assert(data.PlayerLeds.Span[0] && data.PlayerLeds.Span[3], "LED flags were not parsed.");
    Assert(Math.Abs(data.BatteryLevelNormalized - (128f / 255f)) < 0.0001f, "Unexpected normalized battery level.");
}

static void TestReadOnlyMemory()
{
    int[] source = [1, 2, 3];
    ReadOnlyMemory<int> view = source;
    Assert(view.Length == 3 && view.Span.SequenceEqual(source), "Read-only memory did not expose the source.");
    source[1] = 5;
    Assert(view.Span[1] == 5, "The view should expose live device state.");
    Assert(view.ToArray().SequenceEqual([1, 5, 3]), "Snapshot conversion failed.");
}

static void TestReportModeValidation()
{
    using Wiimote owner = CreateOwner();
    AssertThrows<ArgumentOutOfRangeException>(
        () => owner.SetReportModeAsync(InputDataType.StatusInfo).AsTask().GetAwaiter().GetResult());
    AssertThrows<ArgumentOutOfRangeException>(
        () => owner.SetReportModeAsync(InputDataType.ReadMemoryRegisters).AsTask().GetAwaiter().GetResult());
    AssertThrows<ArgumentOutOfRangeException>(
        () => owner.SetReportModeAsync(InputDataType.AcknowledgeOutputReport).AsTask().GetAwaiter().GetResult());
}

static void AssertThrows<TException>(Action action)
    where TException : Exception
{
    try
    {
        action();
    }
    catch (TException)
    {
        return;
    }

    throw new InvalidOperationException($"Expected {typeof(TException).Name}.");
}

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}
