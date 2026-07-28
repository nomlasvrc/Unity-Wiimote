# WiimoteApi for modern .NET

WiimoteApi is a .NET 10 library for discovering and using Nintendo Wii Remotes,
Wii Remote Pluses, and Wii U Pro Controllers through HIDAPI.

The repository originated as a Unity asset. The protocol parsers and bundled
native binaries remain under `Assets/Wiimote`, while `WiimoteApi.csproj`
provides an SDK-style .NET library.

The current API is intentionally not compatible with the original Unity API.
It uses asynchronous I/O, explicit object lifetimes, PascalCase names, and
standard .NET types instead of retaining legacy aliases.

## Requirements

- .NET 10 SDK
- Bluetooth HID support
- A compatible HIDAPI native library

On Windows, the build copies the bundled 64-bit `hidapi.dll` to the output
directory. On another operating system or architecture, provide a compatible
native library named `hidapi` alongside the application.

## Build and test

From this directory:

```powershell
dotnet build WiimoteApi.slnx
dotnet run --project tests/WiimoteApi.Tests
```

The tests exercise report parsing and API validation without requiring
controller hardware.

## Basic usage

`WiimoteManager` owns discovered controllers, the HIDAPI lifetime, and the
serialized write queue. Create one manager for the application and dispose it
when the application shuts down.

```csharp
using WiimoteApi;

using var manager = new WiimoteManager();

manager.LogMessage += (level, message) =>
    Console.WriteLine($"[{level}] {message}");

int discoveredCount = manager.Discover();
Wiimote? remote = manager.Devices.FirstOrDefault();

if (remote is null)
{
    Console.WriteLine("No Wii controller was found.");
    return;
}

remote.DataReceived += (_, report) =>
{
    Console.WriteLine($"{report.ReportType}: A={remote.Button.A}");
    Console.WriteLine($"Acceleration={remote.Accel.CalibratedAcceleration}");
};

await remote.SetPlayerLedsAsync(true, false, false, false);
await remote.SetReportModeAsync(InputDataType.ReportButtonsAccelerometer);
await remote.RequestStatusAsync();

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellation.Cancel();
};

try
{
    await remote.ReadLoopAsync(
        TimeSpan.FromMilliseconds(2),
        cancellation.Token);
}
catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
{
}
```

`ReadLoopAsync` reads and interprets every available HID report, then raises
`DataReceived`. The data objects on `Wiimote`, such as `Button`, `Accel`,
`Status`, and the extension properties, always expose the most recently parsed
state.

## Controller lifetime

Dispose a controller directly or call `manager.Remove(remote)` to close and
remove one controller. Disposing the manager closes every remaining controller,
stops its write queue, and releases HIDAPI:

```csharp
manager.Remove(remote);
```

Do not use a manager after it has been disposed. Create a new manager when a
new discovery lifetime is required.

## Common operations

All output operations are asynchronous and accept an optional
`CancellationToken`.

```csharp
await remote.SetPlayerLedsAsync(true, false, true, false, cancellationToken);
await remote.SetRumbleAsync(true, cancellationToken);
await remote.SetRumbleAsync(false, cancellationToken);
await remote.SetSpeakerEnabledAsync(true, cancellationToken);
await remote.SetSpeakerMutedAsync(false, cancellationToken);
```

Select a report mode according to the state required by the application:

```csharp
await remote.SetReportModeAsync(
    InputDataType.ReportButtonsAccelerometer,
    cancellationToken);
```

Only data included in the selected report mode is updated.

## Extensions, MotionPlus, and IR

`ExtensionType` identifies the active extension. The matching typed property is
non-null while that extension is active:

```csharp
if (remote.Nunchuck is { } nunchuck)
{
    Console.WriteLine(nunchuck.NormalizedStick);
    Console.WriteLine($"C={nunchuck.C}, Z={nunchuck.Z}");
}
```

MotionPlus identification and activation are explicit:

```csharp
if (await remote.IdentifyMotionPlusAsync(cancellationToken))
{
    await remote.ActivateMotionPlusAsync(cancellationToken);
}

remote.MotionPlus?.CalibrateZero();
```

Configure the IR camera with:

```csharp
await remote.SetupIrCameraAsync(
    IRDataType.Extended,
    cancellationToken);

Console.WriteLine(remote.Ir.PointingPosition);
```

Extended IR mode cannot be combined with extension data. Use basic IR mode when
an extension is active, or full mode when interleaved reports are appropriate.

## Raw register access

High-level operations should be preferred, but asynchronous register access is
available for protocol work:

```csharp
byte[] identifier = await remote.ReadRegisterAsync(
    RegisterType.Control,
    0xA600FA,
    size: 6,
    cancellationToken: cancellationToken);

await remote.WriteRegisterAsync(
    RegisterType.Control,
    0xA600F0,
    new byte[] { 0x55 },
    cancellationToken);
```

A controller supports one pending register read at a time. Starting another
read before the previous one completes throws `InvalidOperationException`.
Register errors are reported through the returned task instead of leaving it
pending.

## Data views

Vector data uses `System.Numerics.Vector2` and `Vector3`. Raw fixed-size values
use `ReadOnlyMemory<T>`:

```csharp
ReadOnlySpan<int> acceleration = remote.Accel.RawAcceleration.Span;
ReadOnlySpan<bool> playerLeds = remote.Status.PlayerLeds.Span;
```

These are live views of the most recently parsed controller state. Call
`ToArray()` when a stable snapshot is required.

Two-dimensional IR point data is exposed through `ReadOnlyMatrix<int>`, which
supports indexed access and an explicit `ToArray()` snapshot.

## API characteristics

- `SafeHandle` ownership for native HID handles
- Instance-based manager and deterministic disposal
- Serialized, cancellation-aware HID writes
- `ReadOnlySpan<T>` parsing without per-report slice allocations
- Event-based diagnostics and input notifications
- Standard `ReadOnlyMemory<T>`, `Vector2`, and `Vector3` data views
- Bounds validation for malformed HID reports
- No legacy enum aliases or synchronous compatibility API

The protocol implementation is based on the reverse-engineering documentation
maintained by [WiiBrew](https://wiibrew.org/wiki/Wiimote).

## License

MIT. See [LICENSE.txt](LICENSE.txt).
