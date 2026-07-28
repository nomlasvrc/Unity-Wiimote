using System.Numerics;
using WiimoteApi;
using WiimoteApi.Util;

var tests = new (string Name, Action Run)[]
{
    ("Button report parsing", TestButtons),
    ("Accelerometer report parsing", TestAccelerometer),
    ("Nunchuck report parsing", TestNunchuck),
    ("Classic Controller report parsing", TestClassicController),
    ("Status report parsing", TestStatus),
    ("Read-only array behavior", TestReadOnlyArray),
    ("Big-endian conversion", TestBigEndianConversion)
};

foreach ((string name, Action run) in tests)
{
    run();
    Console.WriteLine($"PASS: {name}");
}

Console.WriteLine($"{tests.Length} tests passed.");

static Wiimote CreateOwner() => new(IntPtr.Zero, "test-device", WiimoteType.WIIMOTE);

static void TestButtons()
{
    using Wiimote owner = CreateOwner();
    var data = new ButtonData(owner);
    Assert(data.InterpretData([0x19, 0x9f]), "Expected a valid button report.");
    Assert(data.DPadLeft && data.DPadUp && data.Plus, "D-pad/Plus bits were not parsed.");
    Assert(data.A && data.B && data.One && data.Two && data.Minus && data.Home, "Button bits were not parsed.");
}

static void TestAccelerometer()
{
    using Wiimote owner = CreateOwner();
    var data = new AccelData(owner);
    Assert(data.InterpretData([0x60, 0x60, 0x80, 0x81, 0x82]), "Expected a valid accelerometer report.");
    Assert(data.RawAccelerationVector == new Vector3(515, 517, 521), "Unexpected raw acceleration.");
}

static void TestNunchuck()
{
    using Wiimote owner = CreateOwner();
    var data = new NunchuckData(owner);
    Assert(data.InterpretData([128, 128, 1, 2, 3, 0xfc]), "Expected a valid Nunchuck report.");
    Assert(data.RawAcceleration[0] == 7 && data.RawAcceleration[1] == 11 && data.RawAcceleration[2] == 15,
        "Unexpected Nunchuck acceleration.");
    Assert(data.C && data.Z, "Active-low Nunchuck buttons were not parsed.");
}

static void TestClassicController()
{
    using Wiimote owner = CreateOwner();
    var data = new ClassicControllerData(owner);
    Assert(data.InterpretData([0x3f, 0x3f, 0x1f, 0x1f, 0xff, 0xff]), "Expected a valid Classic Controller report.");
    Assert(data.LeftStick[0] == 63 && data.LeftStick[1] == 63, "Unexpected left stick values.");
    Assert(data.NormalizedLeftStick == Vector2.One, "Unexpected normalized left stick.");
}

static void TestStatus()
{
    using Wiimote owner = CreateOwner();
    var data = new StatusData(owner);
    Assert(data.InterpretData([0x9f, 0x80]), "Expected a valid status report.");
    Assert(data.IsBatteryLow && data.IsExtensionConnected && data.IsSpeakerEnabled && data.IsIrEnabled,
        "Status flags were not parsed.");
    Assert(data.PlayerLeds[0] && data.PlayerLeds[3], "LED flags were not parsed.");
    Assert(Math.Abs(data.BatteryLevelNormalized - (128f / 255f)) < 0.0001f, "Unexpected normalized battery level.");
}

static void TestReadOnlyArray()
{
    int[] source = [1, 2, 3];
    var view = new ReadOnlyArray<int>(source);
    Assert(view.Count == 3 && view.SequenceEqual(source), "Read-only array does not implement IReadOnlyList correctly.");
    source[1] = 5;
    Assert(view[1] == 5, "The view should expose live device state.");
    Assert(view.ToArray().SequenceEqual([1, 5, 3]), "Snapshot conversion failed.");
}

static void TestBigEndianConversion()
{
    Assert(Wiimote.IntToBigEndian(0x123456, 3).SequenceEqual(new byte[] { 0x12, 0x34, 0x56 }),
        "Big-endian conversion failed.");
}

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}
