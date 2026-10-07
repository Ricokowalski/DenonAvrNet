# Denon HTTP/XML and Telnet protocol notes

This document describes the protocol portions actually used by `DenonAvrNet`. It is not complete documentation for every Denon command. Supported features can vary by model and firmware.

## Port and device detection

Initialization tries, in order:

1. `GET http://HOST:80/goform/Deviceinfo.xml`
2. `GET http://HOST:8080/goform/Deviceinfo.xml`

An HTTP 200 response alone is not sufficient. The XML must contain a `Device_Info` root element and a non-empty `ModelName`.

The detected port is subsequently used for status and control commands.

## Endpoints in use

| Method | Endpoint | Purpose |
| --- | --- | --- |
| `GET` | `/goform/Deviceinfo.xml` | Device detection and properties |
| `POST` | `/goform/AppCommand.xml` | Base status on modern receivers |
| `POST` | `/goform/AppCommand0300.xml` | Audio and speaker details |
| `GET` | `/goform/formMainZone_MainZoneXmlStatus.xml` | Status of older receivers |
| `GET` | `/goform/formiPhoneAppPower.xml?...` | Power |
| `GET` | `/goform/formiPhoneAppVolume.xml?...` | Absolute volume |
| `GET` | `/goform/formiPhoneAppMute.xml?...` | Mute |
| `GET` | `/goform/formiPhoneAppDirect.xml?...` | Direct control commands |

## AppCommand on the AVC-X6800H

The XML parser of the tested AVC-X6800H does not correctly process a formally valid, fully single-line request. The following body produces an empty response:

```xml
<?xml version="1.0" encoding="utf-8"?><tx><cmd id="1">GetAllZonePowerStatus</cmd></tx>
```

The receiver misleadingly replies with HTTP 200:

```xml
<?xml version="1.0" encoding="utf-8" ?>
<rx>
</rx>
```

An explicit `CRLF` must therefore appear between the XML declaration and document element:

```xml
<?xml version="1.0" encoding="utf-8"?>
<tx><cmd id="1">GetAllZonePowerStatus</cmd></tx>
```

The generated string uses `\r\n` independently of the operating system. The request is sent as UTF-8 without a BOM and with this header:

```text
Content-Type: text/xml; charset=utf-8
```

## Base status: `AppCommand.xml`

Base commands use `cmd id="1"` and the command name as text content:

```xml
<?xml version="1.0" encoding="utf-8"?>
<tx><cmd id="1">GetAllZonePowerStatus</cmd></tx>
```

Example response:

```xml
<rx>
  <cmd>
    <zone1>ON</zone1>
    <zone2>OFF</zone2>
    <zone3>OFF</zone3>
  </cmd>
</rx>
```

The library initially bundles power, volume, mute, and source into one request. The four `<cmd>` responses are evaluated in the same order. The input list is read separately and then cached.

If the bundled response does not contain exactly four results or no usable Main Zone value, the client resends the four status commands individually. It remembers this receiver behavior until the next initialization, so later updates skip the unsuccessful bundled attempt.

An unsupported individual command can be returned as `<error>`. As long as at least one usable Main Zone base value is available, the remaining values can continue to be used.

## Detailed status: `AppCommand0300.xml`

Detail commands use `cmd id="3"`, a `name` element, and a parameter list.

### Audio information

```xml
<?xml version="1.0" encoding="utf-8"?>
<tx>
  <cmd id="3">
    <name>GetAudioInfo</name>
    <list>
      <param name="inputmode" />
      <param name="output" />
      <param name="signal" />
      <param name="sound" />
      <param name="fs" />
    </list>
  </cmd>
</tx>
```

Example response:

```xml
<rx>
  <cmd>
    <name>GetAudioInfo</name>
    <list>
      <param name="inputmode" control="1">HDMI</param>
      <param name="output" control="1">Speaker</param>
      <param name="signal" control="1">Dolby Audio - Dolby Digital Plus</param>
      <param name="sound" control="1">Dolby Surround</param>
      <param name="fs" control="1">48 kHz</param>
    </list>
  </cmd>
</rx>
```

### Active speakers

```xml
<?xml version="1.0" encoding="utf-8"?>
<tx>
  <cmd id="3">
    <name>GetActiveSpeaker</name>
    <list>
      <param name="activespall" />
    </list>
  </cmd>
</tx>
```

In the response, `control="2"` identifies a currently active channel. `control="1"` identifies an available or configured but currently inactive channel. `control="0"` is not treated as active.

The library takes the text content of active parameters, for example `FL`, `FR`, `C`, `SW`, `SL`, `SR`, `TFL`, or `TFR`.

## Input control

Input selection uses:

```text
/goform/formiPhoneAppDirect.xml?SI<PROTOCOL_NAME>
```

Examples:

| Display name | HTTP command |
| --- | --- |
| `CBL/SAT` | `...formiPhoneAppDirect.xml?SISAT/CBL` |
| `Media Player` | `...formiPhoneAppDirect.xml?SIMPLAY` |
| `TV AUDIO` | `...formiPhoneAppDirect.xml?SITV` |
| `Blu-ray` | `...formiPhoneAppDirect.xml?SIBD` |

The slash in `SAT/CBL` must remain in the query command. Some Denon firmware versions do not treat transmission as `%2F` as the same protocol command.

## XML processing

Responses are read with these security settings:

- DTD processing is disabled.
- External XML resolvers are disabled.
- The maximum document size is limited.
- Element and attribute names are compared case-insensitively.
- Text values are trimmed; empty values are treated as absent.

## Typical errors

### HTTP 403 on older status paths

On current receivers, older endpoints such as `formMainZone_MainZoneXmlStatus.xml` can reply with HTTP 403 on port 8080. `DenonAvrNet` uses AppCommand endpoints there instead.

### HTTP 200 with an empty `<rx>`

On the AVC-X6800H, this especially indicates a missing line break between the XML declaration and `<tx>`. The server status alone must therefore not be interpreted as a successful protocol request.

### Not all values are present

Possible causes include unsupported commands, standby, or bundled/parallel requests. For an incomplete bundled base response, the library automatically switches to sequential individual queries and treats the audio extension as optional.

### `AudioFormat` is `Unknown`

This value comes directly from `GetAudioInfo/signal`; it is not inferred from the sound mode. `SoundMode` may nevertheless contain, for example, `Dolby Surround`.

## Telnet

Many receivers also provide the Denon IP-control protocol on TCP port 23. Testing with the AVC-X6800H confirmed that this port is reachable. Commands are ASCII and end with `CR` (`\r`). Queries use `?` as their parameter; events and responses use the same line format as a command.

`DenonAvrNet` uses Telnet for Main Zone control, Zone 2/3, speaker presets, sound modes, decoders, events, and temporary channel levels for the current surround mode.

### Temporary channel levels: `CV`

`CV` is not the persistent setting under **Setup → Speakers → Levels**. It represents channel levels in the current surround mode; untouched channels can be reported as `50` (= `0.0 dB`).

Channel levels range from `38` to `62`: `50` corresponds to `0.0 dB`; `38` to `-12.0 dB`; and `62` to `+12.0 dB`. Half-decibel values use a third digit, for example `505` for `+0.5 dB`.

| Purpose | Example |
| --- | --- |
| Read Front Left | `CVFL?` |
| Set Front Left to -1.5 dB | `CVFL 485` |
| Increase Center by one step | `CVC UP` |
| Read all present channel levels | `CV?` |
| End marker for the aggregated response | `CVEND` |
| Disable the subwoofer | `CVSW 00` |
| Reset all channel levels | `CVZRL` |

For `CV?`, the receiver responds only for channels present in its current speaker configuration and ends the sequence with `CVEND`. The library therefore reads the complete sequence on one connection and converts it into `DenonSpeakerLevel` values.

The command codes correspond to the official Denon Control Protocol for TCP port 23 and its documented `CV` response format.

## Speaker setup in the web interface

The speaker-setup interface is separate from the normal HTTP/XML API and is selected by receiver profile:

| Receiver | Scheme | Port | Certificate handling |
| --- | --- | ---: | --- |
| AVC-X6800H | HTTP | `11080` | Normal HTTP |
| AVC-X6700H | HTTPS | `10443` | The local device certificate is explicitly accepted only for this profiled speaker transport |

### Speaker-preset levels

Both profiles read the active preset's **Levels** page with `get_config?type=5`, but their captured write payloads differ:

| Receiver / purpose | Path / payload |
| --- | --- |
| Read levels | `/ajax/speakers/get_config?type=5&_…` |
| X6800H write | `/ajax/speakers/set_config?type=20&data=<encoded Speaker XML>&_…` |
| X6700H write | `/ajax/speakers/set_config?type=5&data=<encoded List/Speaker XML>&_…` |

The value inside `Speaker` is in tenths of a decibel (`-35` = `-3.5 dB`). The X6700H captured write payload is conceptually `<List><Speaker index="0">-35</Speaker></List>`. `DenonAvrClient.GetSpeakerPresetLevelsAsync()` and `SetSpeakerPresetLevelAsync()` choose the correct transport and payload from the receiver profile.

### Speaker distances

The X6800H and X6700H expose persistent distances of the active speaker preset through the same logical AJAX endpoints:

| Purpose | Path |
| --- | --- |
| Read speaker distances | `/ajax/speakers/get_config?type=4&_…` |
| Set one speaker distance | `/ajax/speakers/set_config?type=4&data=<encoded Distances XML>&_…` |

The read response contains a `Distances` element with `Unit`, raw `Step`, optional `M2FConvertRatio`, and a `List` of indexed `Speaker` values. In the captured X6700H data, `Unit=1` represents meters and `Unit=2` feet. Raw distance and step values are interpreted as meters × 100 (`476` = `4.76 m`) independently of the UI display unit.

For example, the set payload is conceptually:

```xml
<Distances><List><Speaker index="0">325</Speaker></List></Distances>
```

when `325` represents `3.25 m`. `DenonAvrNet` does not expose these raw values publicly: read values and steps are normalized to meters, and writes convert meters back to the raw meters × 100 representation.

### Speaker-preset selection

The active speaker preset is read and written with `type=11` on both supported profiles:

| Purpose | Path / payload |
| --- | --- |
| Read active preset | `/ajax/speakers/get_config?type=11&_…` |
| Select preset 1 | `/ajax/speakers/set_config?type=11&data=<SpeakerPreset>1</SpeakerPreset>&_…` |
| Select preset 2 | `/ajax/speakers/set_config?type=11&data=<SpeakerPreset>2</SpeakerPreset>&_…` |

A successful read returns `<SpeakerPreset>1</SpeakerPreset>` or `<SpeakerPreset>2</SpeakerPreset>`. The library polls after selection until the requested preset is reported active.

### Configured speakers versus active audio channels

The persistent Levels and Distances pages describe the receiver's speaker setup. `GetActiveSpeaker`/`ActiveSpeakerChannels` instead describe channels currently active in the audio signal. They are intentionally not used to decide whether a speaker is configured. `GetConfiguredSpeakersAsync()` joins the persistent level and distance data by `SpeakerChannel` because the two pages use different index schemes.
