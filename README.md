# WiimoteApi for modern .NET

WiimoteApi is a .NET 10 library for discovering and reading Nintendo Wii Remotes,
Wii Remote Pluses, and Wii U Pro Controllers through HIDAPI.

The repository originated as a Unity asset. The controller protocol parsers and
native binaries remain under `Assets/Wiimote`, while `WiimoteApi.csproj` provides
a normal SDK-style .NET build. New code should use the modern, PascalCase API;
the most common legacy members remain as compatibility aliases.

## Requirements

- .NET 10 SDK
- Bluetooth HID support
- A compatible HIDAPI native library

The build copies the bundled 64-bit Windows `hidapi.dll` to its output directory.
For another operating system or architecture, provide a compatible native library
named `hidapi` alongside the application.

## Build and test

```powershell
dotnet build WiimoteApi.slnx
dotnet run --project tests/WiimoteApi.Tests
```

The tests exercise report parsing without requiring controller hardware.

## Basic usage

```csharp
using WiimoteApi;

WiimoteManager.LogMessage += (level, message) =>
    Console.WriteLine($"[{level}] {message}");

int discovered = WiimoteManager.Discover();

foreach (Wiimote remote in WiimoteManager.Devices)
{
    remote.DataReceived += (_, report) =>
    {
        Console.WriteLine($"{report.ReportType}: A={remote.Button.A}");
        Console.WriteLine($"Acceleration={remote.Accel.CalibratedAcceleration}");
    };
}

using var stop = new CancellationTokenSource();
await WiimoteManager.Devices[0].ReadLoopAsync(
    TimeSpan.FromMilliseconds(2),
    stop.Token);
```

Call `WiimoteManager.Cleanup(remote)` to close one controller. Call
`WiimoteManager.Shutdown()` once during application shutdown to close every
native handle and stop the writer.

## API highlights

- Nullable reference types and warnings-as-errors
- Read-only device snapshots instead of a mutable global list
- `IDisposable` device lifetime
- Asynchronous, cancellation-aware HID writes
- Event-based diagnostics and input notifications
- `IReadOnlyList<T>`, `Vector2`, and `Vector3` data views
- Bounds validation for malformed HID reports

The low-level protocol implementation is based on the reverse-engineering
documentation maintained by [WiiBrew](https://wiibrew.org/wiki/Wiimote).

## License

MIT. See [LICENSE.txt](LICENSE.txt).
