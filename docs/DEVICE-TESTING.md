# Testing with a physical receiver

This checklist tests the library against a real Denon or Marantz receiver. It complements the unit tests because AppCommand responses can differ by model and firmware.

## Prerequisites

- The receiver and computer are on the same reachable network.
- Network control is enabled on the receiver.
- The .NET 10 SDK is installed.
- The repository has been built:

```powershell
dotnet build
dotnet test
dotnet run --project .\DenonAvrNet.Sample
```

## Read-only stability test

After the sample displays its first automatic status result, select menu option `D`. It performs five status queries and does not send power, volume, mute, or input commands.

Expect five successful lines with plausible power, input, and volume values. With an active audio signal, the audio format and speakers should be displayed as well. `Unknown` can be a valid value reported directly by the receiver.

This test also covers these internal paths:

- Loading and subsequently reusing the input list
- The bundled AppCommand base-status query
- An automatic session-persistent fallback to individual queries
- Optional queries for the audio format and active speakers

## Functional test

Run control tests deliberately and one at a time. Before powering off, the sample asks for confirmation again.

| Test | Procedure in the sample | Expected result |
| --- | --- | --- |
| Input list | Open option `9` | Only enabled inputs are shown |
| CBL/SAT | Select `CBL/SAT` | Receiver switches to SAT/CBL |
| Media Player | Select `Media Player` | Receiver switches to MPLAY |
| Volume | Options `4`, `5`, `6` | The new volume appears in the status; absolute `0..98` and explicit dB APIs represent the same level |
| Mute | Options `7`, `8` | Mute status changes |
| Audio format | Play a source with an active signal | Format and sound mode are plausible |
| Speakers | Play multichannel material | Active channels match playback |
| Speaker-preset levels | On an X6800H or X6700H, open `L → 1` | Persistent Setup → Speakers → Levels values and indices are plausible |
| Speaker-preset write | On an X6800H or X6700H, use `L → H` on a known channel and restore it afterwards | Setup value changes by the requested amount |
| Configured speakers | Select `S` | Configured physical speakers are listed with persistent level and distance; current playback activity must not remove configured speakers |
| Speaker distance write | Select `SD`, change one known speaker by a small amount, then restore it | Distance changes and rereads correctly in meters |
| HTTP speaker preset | Select `P`, read preset, switch 1 ↔ 2, then restore the original preset | The selected preset is confirmed by the receiver |

### Receiver-profile speaker setup

For an X6800H, verify `ReceiverProfileId == "avc-x6800h"`; for an X6700H, verify `ReceiverProfileId == "avc-x6700h"`. Both profiles currently report `SpeakerPresetCount == 2` and support persistent speaker levels, speaker distances, and HTTP/HTTPS speaker-preset selection.

The X6800H speaker setup uses HTTP/11080. The X6700H uses HTTPS/10443; the library accepts the receiver's local device certificate only for this explicitly profiled speaker-setup transport. A TLS error here indicates that the receiver-specific HTTPS path or certificate handling is not active.

Programmatic distance tests can use `GetSpeakerDistancesAsync()` and `SetSpeakerDistanceAsync()`. Read values are always returned in meters. On the captured X6700H interface, raw values are meters × 100 (`476` = `4.76 m`); after a write, reread the value and restore the original distance.

`GetConfiguredSpeakersAsync()` must return the persistent physical speaker setup even when the current input signal does not activate all channels. Do not compare its count directly with `state.Audio.ActiveSpeakerChannels`: those channels describe current playback activity, not speaker configuration.

Snapshot tests should capture the current persistent levels with `CreateSpeakerLevelSnapshotAsync()`, make only a deliberate temporary level change, and restore the captured snapshot before finishing. Do not run destructive write tests unattended.

## Recording an error

For a device-specific error, please record at least:

- Receiver model and firmware/API version
- Detected HTTP port
- Selected sample option
- Complete error message
- If possible, the associated raw XML response from the debugger

An HTTP 200 status with an empty `<rx>` is not valid protocol content. On the AVC-X6800H, this was caused by a missing line break after the XML declaration; the library already adds that line break automatically.
