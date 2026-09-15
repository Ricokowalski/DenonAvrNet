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
| Volume | Options `4`, `5`, `6` | The new volume appears in the status |
| Mute | Options `7`, `8` | Mute status changes |
| Audio format | Play a source with an active signal | Format and sound mode are plausible |
| Speakers | Play multichannel material | Active channels match playback |

## Recording an error

For a device-specific error, please record at least:

- Receiver model and firmware/API version
- Detected HTTP port
- Selected sample option
- Complete error message
- If possible, the associated raw XML response from the debugger

An HTTP 200 status with an empty `<rx>` is not valid protocol content. On the AVC-X6800H, this was caused by a missing line break after the XML declaration; the library already adds that line break automatically.
