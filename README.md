# DenonAvrNet

`DenonAvrNet` is an asynchronous C#/.NET library for controlling compatible Denon and Marantz AV receivers through their local HTTP/XML and Telnet interfaces.

The library was developed primarily with a **Denon AVC-X6800H** and its `0301` communication API on HTTP port `8080`. Older devices with their status interface on port `80` are also supported.

## Features

- Automatic detection of the HTTP interface on port `80` or `8080`
- Reads the model, API version, number of zones, MAC address, and device category
- Main Zone status:
  - Power
  - Selected input
  - Master volume
  - Mute
  - Available inputs
- Zone 2 and Zone 3 status (power, input, volume, mute)
- Cached input list with explicit refresh
- Automatic compatibility fallback for incomplete bundled AppCommand responses
- Extended audio information on compatible devices:
  - Audio input mode
  - Audio output
  - Detected audio format
  - Active sound mode
  - Sample rate
  - Active speaker channels
- Turn the Main Zone on and off
- Increase/decrease volume or set it on Denon's absolute `0..98` scale; explicit dB setters are also available
- Enable and disable mute
- Select an input by its visible Denon name
- Switch speaker preset 1 or 2
- Select surround mode: Auto, Stereo, Dolby Surround, DTS Neural:X, Multi Ch Stereo, or Pure Direct
- Set the digital input decoder to Auto, PCM, or DTS
- Control Zone 2 and Zone 3 through the unified HTTP/Telnet API:
  - Turn on/off
  - Increase/decrease volume or set it on Denon's absolute `0..98` scale; explicit dB setters are also available
  - Enable/disable mute
  - Select input
- Read and control the temporary channel levels of the current surround mode through Telnet (`CV`):
  - Read individual channels or all configured channels (`CV?` / `CVEND`)
  - Set levels from -12.0 to +12.0 dB or change them in steps
  - Set subwoofer channels to OFF and reset all channel levels to Denon factory values
- Read and set the actual levels of the active speaker preset through the receiver-specific speaker web interface (X6800H: HTTP/11080; X6700H: HTTPS/10443)
- Expose the number of speaker presets known for the detected receiver profile
- Capture and restore persistent speaker-preset level snapshots
- Read and switch the active speaker preset through the receiver-specific speaker web interface
- Combine persistent speaker levels and distances into configured physical speakers with `GetConfiguredSpeakersAsync()`
- Read and set persistent speaker distances; public distance values are normalized to meters
- Persistent status monitoring for headless operation:
  - Telnet events are received immediately
  - `OPINFASP` speaker matrices are decoded and duplicate telemetry is filtered
  - Automatic reconnection after a Telnet interruption
  - HTTP status query every 15 seconds as a fallback
- Send arbitrary raw Denon HTTP command paths
- Unified Main Zone control through HTTP or Telnet with automatic selection
- `CancellationToken` support

## Requirements

- .NET 10 SDK
- Receiver and application on the same reachable network
- Network control enabled on the receiver

## Project structure

| Project | Contents |
| --- | --- |
| `DenonAvrNet` | Reusable class library |
| `DenonAvrNet.Sample` | Interactive console sample |
| `DenonAvrNet.Tests` | Unit and protocol tests |

## Referencing the library

Until a NuGet package is published, add the library as a project reference:

```xml
<ItemGroup>
  <ProjectReference Include="..\DenonAvrNet\DenonAvrNet.csproj" />
</ItemGroup>
```

## Quick start

```csharp
using DenonAvrNet;
using DenonAvrNet.Models;

using var receiver = new DenonAvrClient("10.37.0.190");

var device = await receiver.InitializeAsync();
Console.WriteLine($"{device.ModelName}, API {device.CommunicationApiVersion}");
Console.WriteLine($"HTTP port: {receiver.HttpPort}");

var state = await receiver.UpdateAsync();
Console.WriteLine($"Power: {state.Power}");
Console.WriteLine($"Input: {state.Input}");
Console.WriteLine($"Volume: {state.VolumeDb} dB");
Console.WriteLine($"Audio format: {state.Audio?.AudioFormat ?? "unknown"}");
Console.WriteLine($"Sound mode: {state.Audio?.SoundMode ?? "unknown"}");
Console.WriteLine($"Active speakers: {string.Join(", ", state.Audio?.ActiveSpeakers ?? [])}");
Console.WriteLine($"Active channels: {state.Audio?.ActiveSpeakerChannels ?? SpeakerChannel.None}");
```

`InitializeAsync()` must complete successfully once before status or control commands can be used.

## Controlling the receiver

```csharp
await receiver.PowerOnAsync();
await receiver.VolumeUpAsync();
await receiver.VolumeDownAsync();
await receiver.SetVolumeAsync(40.0);       // Denon absolute scale
await receiver.SetVolumeDbAsync(-40.0);    // explicit dB alternative
await receiver.SetMuteAsync(true);
await receiver.SetMuteAsync(false);
await receiver.SetInputAsync("Media Player");
```

## Unified HTTP/Telnet control

The regular Main Zone methods (`PowerOnAsync`, `SetVolumeAsync`, `SetMuteAsync`, `SetInputAsync`, and so on) form a unified API. There are no separate HTTP and Telnet method names. Choose the transport with `DenonControlProtocol`:

```csharp
// Default: Auto. After InitializeAsync, HTTP is used; without HTTP initialization, Telnet is used.
await receiver.SetVolumeAsync(55.0); // 55 on the Denon scale = -25 dB

// Explicitly send one command over Telnet:
await receiver.SetInputAsync("CBL/SAT", DenonControlProtocol.Telnet);

// Prefer Telnet permanently in an application (for example, AVR-X4100W):
receiver.PreferredControlProtocol = DenonControlProtocol.Telnet;
```

With `Auto`, HTTP is tried after successful HTTP initialization. If the HTTP control command produces a network/HTTP error such as `403`, the library tries Telnet. When `Http` or `Telnet` is selected explicitly, there is no fallback, which enables predictable behavior and easier diagnostics.

### Web-interface speaker setup

The values under **Setup → Speakers → Levels** are not the same as Telnet `CV` values. The X6800H and X6700H profiles use Denon's AJAX speaker-setup interface, but with model-specific transport details: the X6800H uses HTTP on port `11080`, while the X6700H uses HTTPS on port `10443`. The X6700H's local device certificate is accepted only for this explicitly profiled speaker-setup connection.

```csharp
Console.WriteLine($"Speaker presets: {receiver.SpeakerPresetCount}");

var levels = await receiver.GetSpeakerPresetLevelsAsync();
await receiver.SetSpeakerPresetLevelAsync(speakerIndex: 2, decibels: -3.5);

var snapshot = await receiver.CreateSpeakerLevelSnapshotAsync();
// ... temporary persistent-level changes ...
await receiver.RestoreSpeakerLevelSnapshotAsync(snapshot);
```

`SpeakerPresetCount` comes from the selected receiver profile. The X6800H and X6700H profiles currently report two presets; legacy and unknown profiles report zero. `SpeakerIndex` is the web-interface index provided by the receiver. The sample menu item `L → 1` shows current values together with their indices; `L → H` sets a value.

The same profile-specific speaker setup interface can read and write persistent speaker distances on both the X6800H and X6700H:

```csharp
var distances = await receiver.GetSpeakerDistancesAsync();
Console.WriteLine($"Receiver unit: {distances.ReceiverUnit}, step: {distances.Step:0.###} m");

foreach (var speaker in distances.Speakers)
    Console.WriteLine($"{speaker.Channel}: {speaker.Meters:0.###} m");

await receiver.SetSpeakerDistanceAsync(speakerIndex: 0, meters: 3.25);
```

Public distance values are always expressed in meters. The captured X6700H interface reports raw distance values as meters × 100 even when the receiver display unit is configurable. The test tone is not implemented as a public feature; the X6800H profile retains its confirmed stop command, while the X6700H preset path does not send an unverified test-tone command.


Configured speakers are intentionally different from currently active audio channels. `GetConfiguredSpeakersAsync()` joins the persistent Levels and Distances pages by `SpeakerChannel` and returns physical speakers configured in the active preset. `state.Audio.ActiveSpeakerChannels`, by contrast, describes channels currently active in the audio signal and must not be used as a configuration filter.

```csharp
var configured = await receiver.GetConfiguredSpeakersAsync();
foreach (var speaker in configured)
    Console.WriteLine($"{speaker.Channel}: {speaker.LevelDb:0.0} dB, {speaker.DistanceMeters:0.00} m");

var preset = await receiver.GetActiveSpeakerPresetAsync();
await receiver.SelectSpeakerPresetAsync(preset == 1 ? 2 : 1);
```

### Feature and device capabilities

`AvrFeature` describes a library feature, not hardware. Use `GetSupportedProtocols()` to find which transport is implemented for a feature:

```csharp
var transports = receiver.GetSupportedProtocols(AvrFeature.MainZoneVolume);
// Contains Http and Telnet.
```

`ReceiverCapabilities`, on the other hand, describes the detected AVR. After `InitializeAsync()`, HTTP, AppCommand, the zone count, and Zone 2/3 are known. Support for AppCommand0300 is detected at the first `UpdateAsync()`. Telnet is intentionally not probed automatically; `PW?` can check it without changing state:

```csharp
await receiver.InitializeAsync();
var capabilities = await receiver.ProbeReceiverCapabilitiesAsync();

Console.WriteLine(capabilities.SupportsTelnet);          // true or false
Console.WriteLine(capabilities.SupportsAppCommand0300);  // true, false, or null before UpdateAsync
Console.WriteLine(receiver.IsFeatureAvailable(AvrFeature.Zone3Control));
```

A control command does not update `receiver.State` in advance. Query the receiver again to obtain a device-confirmed state:

```csharp
await receiver.SetInputAsync("CBL/SAT");
await Task.Delay(350);
var confirmedState = await receiver.UpdateAsync();
```

`AvailableInputs` is loaded on the first status request and cached afterwards. Explicitly reload it after changing the receiver's input configuration:

```csharp
var currentInputs = await receiver.RefreshInputsAsync();
```

## Input names

Denon sometimes uses protocol names that differ from the user interface. `SetInputAsync()` maps the most important display names automatically:

| Display name | Denon protocol name |
| --- | --- |
| `CBL/SAT` | `SAT/CBL` |
| `Media Player` | `MPLAY` |
| `TV AUDIO` | `TV` |
| `Blu-ray` | `BD` |
| `Bluetooth` | `BT` |
| `NETWORK` | `NET` |
| `iPod/USB` | `USB/IPOD` |
| `AUX` | `AUX1` |
| `Tuner` or `FM` | `TUNER` |

Mapping is case-insensitive. Known protocol names such as `MPLAY` or `SAT/CBL` can also be passed directly.

## Status model

`UpdateAsync()` returns a `DenonReceiverState` and stores the same snapshot in `receiver.State`.

| Property | Meaning |
| --- | --- |
| `IsPoweredOn` | Simplified Boolean power status |
| `Power` | Unchanged receiver power text |
| `Input` | Current Denon input protocol name |
| `Volume` | Master volume on Denon's absolute `0..98` scale or `null` |
| `VolumeDb` | The same master volume in relative dB or `null` |
| `IsMuted` | Mute status or `null` |
| `AvailableInputs` | Standard inputs that are not disabled |
| `Audio` | Optional extended audio information |
| `Zone2`, `Zone3` | Optional status snapshots for additional zones |

`DenonAudioInfo` contains:

| Property | Meaning |
| --- | --- |
| `InputMode` | For example, `HDMI` |
| `Output` | For example, `Speaker` |
| `AudioFormat` | Detected input signal, e.g. Dolby Digital Plus |
| `SoundMode` | Active playback/upmix mode |
| `SampleRate` | For example, `48 kHz` |
| `ActiveSpeakers` | Active Denon channel abbreviations such as `FL`, `C`, `SW`, or `TFL` |
| `ActiveSpeakerChannels` | The same channels as a combinable `SpeakerChannel` flags enum |

`Audio` can be `null` on older or incompatible receivers, and individual values can be absent. A receiver-reported `Unknown` is deliberately not reinterpreted.

## Errors and cancellation

```csharp
using DenonAvrNet.Exceptions;

try
{
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    await receiver.InitializeAsync(timeout.Token);
    var state = await receiver.UpdateAsync(timeout.Token);
}
catch (DenonConnectionException exception)
{
    Console.Error.WriteLine($"Receiver is unreachable: {exception.Message}");
}
catch (DenonProtocolException exception)
{
    Console.Error.WriteLine($"Unexpected receiver response: {exception.Message}");
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("Operation cancelled or timed out.");
}
```

| Exception | Meaning |
| --- | --- |
| `DenonConnectionException` | Valid device information was not found on any supported port |
| `DenonProtocolException` | XML or expected Denon status values are invalid or missing |
| `HttpRequestException` | HTTP or network error during a request |
| `InvalidOperationException` | The client has not been initialized yet |
| `ArgumentOutOfRangeException` | Absolute volume outside `0.0..98.0`, dB volume outside `-80.0..+18.0 dB`, or another numeric argument outside its supported range |

## Raw commands

Receiver features that do not yet have a dedicated method can be addressed with a complete Denon command path:

```csharp
await receiver.SendCommandAsync(
    "/goform/formiPhoneAppDirect.xml?VSMONI2");
```

The path must begin with `/`. The calling application is responsible for ensuring that the specific receiver supports the command.

## Running the sample and tests

```powershell
dotnet run --project .\DenonAvrNet.Sample
dotnet test
```

In the sample, menu option `D` performs five consecutive status requests. This diagnostic is read-only and does not change any receiver setting.

## Technical notes

- Port `8080` uses `AppCommand.xml` with `cmd id="1"` for base status. Four status commands are initially bundled in one request.
- If a receiver does not return a fully usable response, the client repeats the query with individual requests and remembers this variant until the next initialization.
- Audio format and active speakers are queried through `AppCommand0300.xml` with `cmd id="3"`.
- Detail queries are sequential; parallel access is avoided.
- The input list is loaded separately and cached in the client.
- The AVC-X6800H requires a CRLF after the XML declaration. Without it, the receiver returns HTTP 200 with an empty `<rx>` element.
- Do not use `DenonAvrClient` concurrently through multiple parallel `UpdateAsync()` calls.

Further details are available in the [API documentation](docs/API.md), [protocol notes](docs/PROTOCOL.md), and [device-testing guide](docs/DEVICE-TESTING.md).

## Current limitations

- The current focus is the Main Zone.
- Renamed inputs are not yet mapped separately to their custom display names.
- Events are received through a permanently open Telnet connection; custom event handlers should not perform long-running work.
- Receiver-specific web features use an internal profile selected during `InitializeAsync()`. The selected profile is exposed as `ReceiverProfileId` for diagnostics.
- Speaker-preset levels, speaker distances, and HTTP speaker-preset selection are implemented for both the AVC-X6800H and AVC-X6700H through receiver-specific AJAX profiles. The X6800H uses HTTP/11080; the X6700H uses HTTPS/10443 and an explicitly scoped local-device certificate exception. Legacy and unknown profiles currently report zero known presets and no web speaker-setup support.

## Reference and license

The implementation was inspired by [`ol-iver/denonavr`](https://github.com/ol-iver/denonavr). The original reference version is based on commit `98566b286efab12496ef623eda498db8bcb5ea09` (`1.4.0-dev`).

License and provenance notices are in [LICENSE.txt](LICENSE.txt) and [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
