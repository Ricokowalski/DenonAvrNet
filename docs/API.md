# API documentation

This document describes the publicly usable types and intended lifecycle of `DenonAvrNet`.

## Typical lifecycle

1. Create a `DenonAvrClient` for a host name or IP address.
2. Call `InitializeAsync()` once.
3. Read a confirmed state with `UpdateAsync()`.
4. Optionally extend the result with `ProbeReceiverCapabilitiesAsync()`.
5. Send control commands through `Auto`, HTTP, or Telnet.
6. If necessary, read the state again after a short processing delay.
7. Release the client using `Dispose()` or `using`.

```csharp
using DenonAvrNet;

using var receiver = new DenonAvrClient(
    "10.37.0.190",
    requestTimeout: TimeSpan.FromSeconds(5));

await receiver.InitializeAsync();
var capabilities = await receiver.ProbeReceiverCapabilitiesAsync();

var before = await receiver.UpdateAsync();
await receiver.SetVolumeAsync(-35.5);
await Task.Delay(350);
var after = await receiver.UpdateAsync();
```

## `DenonAvrClient`

### Constructor

```csharp
new DenonAvrClient(string host, TimeSpan? requestTimeout = null)
```

`host` can be an IP address, host name, or HTTP/HTTPS address without a required command path. For a complete address, the client uses only the host portion. Status and device queries use HTTP/XML; Main Zone control commands can use HTTP or Telnet.

The default `requestTimeout` is five seconds. The internal connection setup and `HttpClient` use the same time limit.

### Properties

| Property | Type | Description |
| --- | --- | --- |
| `Host` | `string` | Normalized host name or IP address |
| `PreferredControlProtocol` | `DenonControlProtocol` | Default transport for Main Zone control commands; default `Auto` |
| `HttpPort` | `int?` | Port detected after initialization |
| `DeviceInfo` | `DenonDeviceInfo?` | Most recently detected device information |
| `ReceiverCapabilities` | `DenonReceiverCapabilities?` | Hardware capabilities detected for this AVR model |
| `State` | `DenonReceiverState?` | Most recently confirmed status snapshot |

`HttpPort`, `DeviceInfo`, `ReceiverCapabilities`, and `State` are `null` before their respective successful query.

### `InitializeAsync`

```csharp
Task<DenonDeviceInfo> InitializeAsync(
    CancellationToken cancellationToken = default)
```

The method queries `Deviceinfo.xml` on port 80 first and then port 8080. Initialization completes only when the Denon response is syntactically and semantically valid.

On success, `HttpPort`, `DeviceInfo`, and initially known `ReceiverCapabilities` are set. If both ports fail, a `DenonConnectionException` is thrown; its inner `AggregateException` contains the individual errors.

### `UpdateAsync`

```csharp
Task<DenonReceiverState> UpdateAsync(
    CancellationToken cancellationToken = default)
```

The method reads Main Zone status and also writes the result to `State`.

On port 8080, these four base queries are initially sent together to `AppCommand.xml`:

- `GetAllZonePowerStatus`
- `GetAllZoneVolume`
- `GetAllZoneMuteStatus`
- `GetAllZoneSource`

When the response can be evaluated completely, the client continues using this compact query. For an incomplete or empty response, the same four commands are automatically repeated individually. This compatibility decision is retained until the next `InitializeAsync()`.

`GetDeletedSource` supplies the enabled input list. It is read separately with the first status query and then cached in the client.

When available, the following commands are then queried through `AppCommand0300.xml`:

- `GetAudioInfo`
- `GetActiveSpeaker`

Port 80 uses the legacy Main Zone status XML. Extended audio information is generally unavailable there.

After the first `UpdateAsync()`, `ReceiverCapabilities.SupportsAppCommand0300` is set to `true` or `false`. Before that query it is `null`, because the extension has not been checked yet.

Requests are not performed in parallel, avoiding firmware issues caused by simultaneous AppCommand requests.

On receivers with further zones, the status snapshot also contains `Zone2` and `Zone3`. Each optional `DenonZoneState` contains power, input, volume, and mute.

### Unified HTTP/Telnet control

Main Zone methods use a shared API. Without a protocol argument, `PreferredControlProtocol` is used, which defaults to `Auto`:

```csharp
await receiver.PowerOnAsync();
await receiver.SetVolumeAsync(-30.0);
await receiver.SetInputAsync("CBL/SAT");

// Force a single command:
await receiver.SetVolumeAsync(-25.0, DenonControlProtocol.Telnet);

// Use Telnet for all following Main Zone commands:
receiver.PreferredControlProtocol = DenonControlProtocol.Telnet;
```

`Auto` initially uses HTTP after a successful `InitializeAsync()`. If initialization has not run yet, Telnet is used. If an HTTP control command throws `HttpRequestException` (for example HTTP 403), `Auto` falls back to Telnet once. Explicitly selected `Http` or `Telnet` does not fall back.

### `AvrFeature` and `DenonReceiverCapabilities`

`AvrFeature` describes a function offered by the **library**. Use `GetSupportedProtocols()` to determine the implemented transport:

```csharp
var transports = receiver.GetSupportedProtocols(AvrFeature.MainZoneVolume);
// Contains DenonControlProtocol.Http and DenonControlProtocol.Telnet.

var eventTransport = receiver.GetSupportedProtocols(AvrFeature.LiveEvents);
// Contains only DenonControlProtocol.Telnet.
```

| `AvrFeature` | Library transport |
| --- | --- |
| `MainZonePower`, `MainZoneVolume`, `MainZoneMute`, `MainZoneInput` | HTTP and Telnet |
| `MainZoneStatus`, `AudioInformation`, `ActiveSpeakerStatus`, `SpeakerPresetLevelControl` | HTTP |
| `Zone2Control`, `Zone3Control`, `LiveEvents`, `ChannelLevelRead`, `ChannelLevelControl`, `SpeakerPresetControl`, `SurroundModeControl`, `DigitalInputModeControl` | Telnet |

`DenonReceiverCapabilities` instead describes the **detected AVR model**. After `InitializeAsync()`, HTTP, AppCommand, the zone count, and Zone 2/3 are known. `SupportsTelnet` initially remains `null` so an untested port is not incorrectly treated as unsupported. A read-only `PW?` command checks the port without changing state:

```csharp
var receiverCapabilities = await receiver.ProbeReceiverCapabilitiesAsync();
Console.WriteLine(receiverCapabilities.SupportsTelnet); // true or false

var hasZone3 = receiver.IsFeatureAvailable(AvrFeature.Zone3Control);
```

`IsFeatureAvailable()` combines hardware capabilities with the requested feature. Before `InitializeAsync()`, it returns `false` because no specific receiver has been detected.

### `DenonTelnetClient` — direct Telnet control

For direct special commands and additional zones, the library provides `DenonTelnetClient`. The console sample provides complete zone control in menu item `T`. Status itself is still read through the HTTP API, because a Telnet response such as `Z3SOURCE` describes only the configured input, not the power state.

The Main Zone can be used directly, although applications should normally prefer the unified `DenonAvrClient` API:

```csharp
var telnet = new DenonTelnetClient("10.37.0.190");
await telnet.PowerOnAsync();
await telnet.SetVolumeAsync(-30.0);
await telnet.SetMuteAsync(false);
await telnet.SetInputAsync("Media Player");
```

```csharp
var zones = new DenonTelnetClient("10.37.0.190");

await zones.SetZone2PowerAsync(true);
await zones.SetZone2VolumeAsync(-35.5);
await zones.SetZone2MuteAsync(false);
await zones.SetZone2InputAsync("MEDIA PLAYER"); // Sends Z2MPLAY.

await zones.SetZone3InputAsync("CBL/SAT");      // Sends Z3SAT/CBL.
```

`SetZone2VolumeAsync()` and `SetZone3VolumeAsync()` accept values from `-80.0` to `+18.0 dB` and round to half-decibel steps.

### Temporary speaker-channel levels through Telnet

`DenonTelnetClient` supports Denon's `CV` commands for temporary channel levels of the current surround mode. These are **not** the persistent values under **Setup → Speakers → Levels** in the web interface.

`DenonSpeakerLevelChannel` deliberately is not a flags enum: every output—including `Subwoofer`, `Subwoofer2`, `Subwoofer3`, and `Subwoofer4`—is an individually writable target channel.

```csharp
using DenonAvrNet.Models;

var telnet = new DenonTelnetClient("10.37.0.190");

var frontLeft = await telnet.GetSpeakerLevelAsync(DenonSpeakerLevelChannel.FrontLeft);
await telnet.SetSpeakerLevelAsync(DenonSpeakerLevelChannel.FrontLeft, -1.5);
await telnet.ChangeSpeakerLevelAsync(DenonSpeakerLevelChannel.Center, increase: true);

var allLevels = await telnet.GetSpeakerLevelsAsync();
await telnet.SetSpeakerLevelOffAsync(DenonSpeakerLevelChannel.Subwoofer2);
await telnet.ResetSpeakerLevelsToFactoryDefaultsAsync();
```

The same features are available through the unified facade. As the library currently implements them only through Telnet, `Auto` uses Telnet immediately; `DenonControlProtocol.Http` throws `NotSupportedException`.

```csharp
var levels = await receiver.GetSpeakerLevelsAsync();
await receiver.SetSpeakerLevelAsync(
    DenonSpeakerLevelChannel.TopFrontLeft,
    1.0,
    DenonControlProtocol.Telnet);
```

`GetSpeakerLevelsAsync()` sends `CV?` and waits for the final `CVEND` message. It returns only channels that are present in the receiver's current speaker configuration. `SetSpeakerLevelAsync()` accepts `-12.0` to `+12.0 dB` and rounds to 0.5 dB steps. An `OFF` level is valid only for subwoofer channels. `ResetSpeakerLevelsToFactoryDefaultsAsync()` resets levels to Denon factory values; the library does not store or restore a prior session snapshot.

### Web-interface speaker-preset levels (HTTP)

On the AVC-X6800H, actual values of the active speaker preset are read and set through the separate web interface on port `11080`. This port is deliberately independent from `HttpPort` (usually `8080`) for the normal HTTP/XML API.

```csharp
var presetLevels = await receiver.GetSpeakerPresetLevelsAsync();

foreach (var level in presetLevels)
{
    Console.WriteLine($"{level.Channel}: {level.Decibels:0.0} dB");
}

await receiver.SetSpeakerPresetLevelAsync(speakerIndex: 2, decibels: -3.5);
```

`GetSpeakerPresetLevelsAsync()` reads `/ajax/speakers/get_config?type=5`. `SetSpeakerPresetLevelAsync()` sends the value in tenths of a dB (`-35` for `-3.5 dB`) to `/ajax/speakers/set_config?type=20`. `SpeakerIndex` is the web-interface index supplied by the receiver, not a `DenonSpeakerLevelChannel` enum value.

Only the test-tone stop request `<StopTestTone></StopTestTone>` has been observed so far. The library therefore does not yet implement start or stop commands until the corresponding start request has been recorded unambiguously.

Additional read-only Telnet queries are available through `QueryPowerAsync()`, `QueryVolumeAsync()`, `QueryMuteAsync()`, `QueryInputAsync()`, `QuerySurroundModeAsync()`, and `QueryDigitalInputModeAsync()`.

### `DenonReceiverMonitor` — events and fallback polling

`DenonReceiverMonitor` opens a persistent Telnet connection on port 23. Every relevant status message sent by the receiver immediately triggers a refresh through `TelnetEventReceived`. In parallel, the complete HTTP status is reread every 15 seconds by default. After a connection interruption, the Telnet listener automatically reconnects.

The monitor deliberately owns a separate HTTP client. It can therefore run alongside interactive console control without causing simultaneous `UpdateAsync()` calls on the same `DenonAvrClient`.

```csharp
await using var monitor = new DenonReceiverMonitor(
    "10.37.0.190",
    TimeSpan.FromSeconds(15));

monitor.TelnetEventReceived += eventMessage =>
    Console.WriteLine($"Telnet: {eventMessage.Message}");
monitor.StateRefreshed += state =>
    Console.WriteLine($"Status: {state.Power}, {state.Input}");
monitor.Error += exception =>
    Console.Error.WriteLine(exception.Message);

await monitor.StartAsync();
```

`CurrentState` always contains the latest successful status snapshot.

#### `OPINFASP`: active speaker outputs

During playback, the AVC-X6800H repeatedly sends messages such as:

```text
OPINFASP 22222200222000022000000002200000
```

`OPINFASP` is a compact matrix of speaker-output positions. Its 32 positions are numbered from left to right. `2` means *active*; `0` means *inactive or unavailable*. In the example, positions `1–6`, `9–11`, `16–17`, and `26–27` are active—**13 active output positions** in total.

Denon does not publish the exact mapping from the 32 positions to channel names. For reliable names such as `FL`, `C`, `SW`, `TFL`, or `TRR`, use `state.Audio.ActiveSpeakers` from the HTTP `GetActiveSpeaker` query. For type-safe checks, `state.Audio.ActiveSpeakerChannels` is also available as a `SpeakerChannel` flags enum.

The event exposes the already decoded matrix:

```csharp
monitor.TelnetEventReceived += item =>
{
    if (item.ActiveSpeakerMatrix is { } speakers)
    {
        Console.WriteLine($"{speakers.ActivePositionCount}/{speakers.PositionCount} active");
        Console.WriteLine(string.Join(", ", speakers.ActivePositions));
    }
};
```

Repeated identical `OPINFASP` matrices do **not** trigger an additional HTTP refresh. If the matrix changes, status is loaded once; `StateRefreshed` then supplies the precise channel names.

### Speaker presets and audio modes through `DenonTelnetClient`

```csharp
var telnet = new DenonTelnetClient("10.37.0.190");

await telnet.SelectSpeakerPresetAsync(1);          // SPPR 1
await telnet.SelectSpeakerPresetAsync(2);          // SPPR 2

await telnet.SetSurroundModeAsync("Dolby Surround");
await telnet.SetSurroundModeAsync("DTS Neural:X");
await telnet.SetSurroundModeAsync("Pure Direct");

await telnet.SetDigitalInputModeAsync("Auto");    // DCAUTO
await telnet.SetDigitalInputModeAsync("PCM");     // DCPCM
await telnet.SetDigitalInputModeAsync("DTS");     // DCDTS
```

Speaker presets themselves are configured in the receiver's Setup menu; the library switches between presets 1 and 2.

### `RefreshInputsAsync`

```csharp
Task<IReadOnlyList<string>> RefreshInputsAsync(
    CancellationToken cancellationToken = default)
```

Rereads the currently enabled inputs from the receiver and replaces the internal cache. If a `State` already exists, its `AvailableInputs` receives the new list while all other status values remain unchanged.

A manual refresh is useful after inputs have been enabled, disabled, or reconfigured in receiver setup. The first call to `UpdateAsync()` does this automatically.

### Power

```csharp
Task PowerOnAsync(CancellationToken cancellationToken = default)
Task PowerOffAsync(CancellationToken cancellationToken = default)
Task PowerOnAsync(DenonControlProtocol protocol, CancellationToken cancellationToken = default)
Task PowerOffAsync(DenonControlProtocol protocol, CancellationToken cancellationToken = default)
```

`PowerOffAsync()` places the Main Zone in standby. Whether the receiver remains reachable over the network depends on its network-control-in-standby setting.

### Volume

```csharp
Task VolumeUpAsync(CancellationToken cancellationToken = default)
Task VolumeDownAsync(CancellationToken cancellationToken = default)
Task VolumeUpAsync(DenonControlProtocol protocol, CancellationToken cancellationToken = default)
Task VolumeDownAsync(DenonControlProtocol protocol, CancellationToken cancellationToken = default)
Task SetVolumeAsync(double volumeDb, CancellationToken cancellationToken = default)
Task SetVolumeAsync(double volumeDb, DenonControlProtocol protocol, CancellationToken cancellationToken = default)
```

`SetVolumeAsync()` accepts `-80.0` to `+18.0 dB`. Values are rounded to half-decibel increments and transmitted with a decimal point regardless of the current system language.

### Mute

```csharp
Task SetMuteAsync(bool muted, CancellationToken cancellationToken = default)
Task SetMuteAsync(bool muted, DenonControlProtocol protocol, CancellationToken cancellationToken = default)
```

`true` enables mute; `false` disables it.

### Selecting an input

```csharp
Task SetInputAsync(string input, CancellationToken cancellationToken = default)
Task SetInputAsync(string input, DenonControlProtocol protocol, CancellationToken cancellationToken = default)
```

The method accepts visible standard names such as `CBL/SAT` or `Media Player` and direct Denon protocol names. Standard names are converted case-insensitively.

```csharp
await receiver.SetInputAsync("CBL/SAT");      // Sends SISAT/CBL.
await receiver.SetInputAsync("MEDIA PLAYER"); // Sends SIMPLAY.
await receiver.SetInputAsync("MPLAY");        // Also valid.
```

Empty names and names containing CR/LF are rejected.

### Sending an arbitrary command

```csharp
Task SendCommandAsync(
    string commandPath,
    CancellationToken cancellationToken = default)
```

The method expects a complete path from `/`, but no host name:

```csharp
await receiver.SendCommandAsync(
    "/goform/formiPhoneAppDirect.xml?DIM%20SEL");
```

This method does not check whether the receiver supports the supplied command.

## `DenonControlProtocol`

| Value | Behavior |
| --- | --- |
| `Auto` | HTTP after initialization; otherwise Telnet; falls back to Telnet on an HTTP error |
| `Http` | Forces HTTP without fallback; `InitializeAsync()` is required |
| `Telnet` | Forces Telnet on port 23 without fallback |

## `DenonReceiverCapabilities`

| Property | Type | Description |
| --- | --- | --- |
| `SupportsHttp` | `bool` | HTTP device query initialized successfully |
| `SupportsTelnet` | `bool?` | Result of `ProbeReceiverCapabilitiesAsync()`; previously `null` |
| `SupportsAppCommand` | `bool` | Current client uses the AppCommand interface on port 8080 |
| `SupportsAppCommand0300` | `bool?` | Result of extended audio query; `null` before `UpdateAsync()` |
| `SupportsZone2`, `SupportsZone3` | `bool` | Derived from the zone count reported by the receiver |
| `ZoneCount` | `int?` | Zone count reported by the receiver |

## `DenonSpeakerLevel`

| Property | Type | Description |
| --- | --- | --- |
| `Channel` | `DenonSpeakerLevelChannel` | Exact Denon CV channel addressed |
| `Decibels` | `double?` | Channel level; `null` when `OFF` |
| `IsOff` | `bool` | Whether the receiver reported the channel as `00`/`OFF` |
| `RawResponse` | `string` | Original Telnet response, e.g. `CVFL 505` |

## `DenonSpeakerPresetLevel`

| Property | Type | Description |
| --- | --- | --- |
| `SpeakerIndex` | `int` | Modern Denon web-interface index on port 11080 |
| `Decibels` | `double` | Actual level of the active speaker preset |
| `Channel` | `SpeakerChannel?` | Channel inferred from the web index, or a flags combination for combined subwoofer entries |

`DenonSpeakerPresetIndexConverter` contains the full mapping for indices `0` through `35`, derived from the AVC-X6800H web interface. This includes `Subwoofer2`, `Subwoofer3`, and `Subwoofer4`, which were added to the `SpeakerChannel` flags enum.

## `DenonDeviceInfo`

| Property | Type | Description |
| --- | --- | --- |
| `ModelName` | `string` | Model reported by the receiver |
| `ManualModelName` | `string?` | Model name for manual/product group |
| `CommunicationApiVersion` | `string?` | For example, `0301` |
| `ZoneCount` | `int?` | Reported number of zones |
| `MacAddress` | `string?` | MAC address reported by the receiver |
| `CategoryName` | `string?` | Device category, e.g. `AV RECEIVER` |

## `DenonReceiverState`

| Property | Type | Description |
| --- | --- | --- |
| `IsPoweredOn` | `bool` | `true` when `Power` is `ON` |
| `Power` | `string` | Original value or `UNKNOWN` |
| `Input` | `string?` | Current Denon input protocol name |
| `VolumeDb` | `double?` | Master volume in dB |
| `IsMuted` | `bool?` | `true`, `false`, or unknown |
| `AvailableInputs` | `IReadOnlyList<string>` | Active/not-deleted standard sources |
| `Audio` | `DenonAudioInfo?` | Optional audio and speaker information |

The object is an immutable snapshot. A previously returned object is not altered by later queries.

## `DenonAudioInfo`

| Property | Type | Denon parameter |
| --- | --- | --- |
| `InputMode` | `string?` | `inputmode` |
| `Output` | `string?` | `output` |
| `AudioFormat` | `string?` | `signal` |
| `SoundMode` | `string?` | `sound` |
| `SampleRate` | `string?` | `fs` |
| `ActiveSpeakers` | `IReadOnlyList<string>` | `GetActiveSpeaker` values with `control="2"` |
| `ActiveSpeakerChannels` | `SpeakerChannel` | Combined type-safe form of `ActiveSpeakers` |

`AudioFormat` is the input signal detected by the receiver. `SoundMode` is the playback or upmix mode currently applied. Consequently, values such as `PCM` and `Dolby Surround` can be reported at the same time.

Typical channel abbreviations:

| Abbreviation | Channel |
| --- | --- |
| `FL`, `FR` | Front left/right |
| `C` | Center |
| `SW`, `SW1`, `SW2` | Subwoofer |
| `SL`, `SR` | Surround left/right |
| `SBL`, `SBR` | Surround Back left/right |
| `TFL`, `TFR` | Top Front left/right |
| `TML`, `TMR` | Top Middle left/right |
| `TRL`, `TRR` | Top Rear left/right |
| `FHL`, `FHR` | Front Height left/right |
| `RHL`, `RHR` | Rear Height left/right |
| `FWL`, `FWR` | Front Wide left/right |
| `SB`, `SBL`, `SBR` | Single or left/right Surround Back |
| `SHL`, `SHR` | Surround Height left/right |
| `CH`, `TS` | Center Height or Top Surround |
| `FDL`, `FDR`, `SDL`, `SDR`, `BDL`, `BDR` | Dolby Atmos Enabled speakers |

Example of a type-safe channel check:

```csharp
var activeChannels = state.Audio?.ActiveSpeakerChannels ?? SpeakerChannel.None;
var hasFrontWideLeft =
    (activeChannels & SpeakerChannel.FrontWideLeft) != SpeakerChannel.None;
```

## Refreshing status periodically

```csharp
using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
using var cancellation = new CancellationTokenSource();

while (await timer.WaitForNextTickAsync(cancellation.Token))
{
    try
    {
        var state = await receiver.UpdateAsync(cancellation.Token);
        Console.WriteLine($"{state.Power}, {state.Input}, {state.VolumeDb}");
    }
    catch (HttpRequestException exception)
    {
        Console.Error.WriteLine(exception.Message);
    }
}
```

There is currently no internal background query. The calling application chooses the refresh interval. Multiple `UpdateAsync()` calls on the same client should not run in parallel.

## Exceptions

All library-specific exceptions derive from `DenonAvrException`.

| Exception | Trigger |
| --- | --- |
| `DenonConnectionException` | Initialization on ports 80 and 8080 failed |
| `DenonProtocolException` | Invalid XML, wrong root element, or missing base-status values |

Transport errors remain `HttpRequestException`; cancellations remain `OperationCanceledException`. Applications can therefore handle network failures, protocol errors, and deliberate cancellation separately.

## Resources and lifetime

The public constructor creates an internal `HttpClient`. Reuse `DenonAvrClient` and dispose it after use:

```csharp
using var receiver = new DenonAvrClient("10.37.0.190");
```

Do not create a new client for every individual request.
