# DenonAvrNet

An asynchronous C#/.NET port inspired by
[`ol-iver/denonavr`](https://github.com/ol-iver/denonavr).

The first development stage contains only the reusable class library. It does
not contain a test, console or WPF project.

## Current functionality

- Detect the Denon HTTP/XML API on port 80 or 8080
- Read modern receivers through `POST /goform/AppCommand.xml` on port 8080,
  with the legacy status XML retained for receivers on port 80
- Read model, communication API version, MAC address and zone count
- Read Main Zone power, input, volume, mute and available inputs
- Main Zone power on and standby
- Volume up, volume down and setting an absolute dB value
- Mute on and off
- Select an input by its Denon protocol name
- Send a raw HTTP command path

## Basic use

```csharp
using DenonAvrNet;

using var receiver = new DenonAvrClient("10.37.0.190");

var device = await receiver.InitializeAsync();
Console.WriteLine($"{device.ModelName} uses HTTP port {receiver.HttpPort}");

var state = await receiver.UpdateAsync();
Console.WriteLine($"Power: {state.Power}, volume: {state.VolumeDb} dB");

await receiver.PowerOnAsync();
await receiver.SetVolumeAsync(-40.0);
await receiver.SetMuteAsync(false);
await receiver.SetInputAsync("TV AUDIO");
```

`InitializeAsync` must be called once before status queries and control
commands. A command does not optimistically modify `State`; call `UpdateAsync`
after the receiver has processed it to obtain a confirmed state.

## Reference version

The initial port is based on Python reference commit
`98566b286efab12496ef623eda498db8bcb5ea09` (`1.4.0-dev`). See
`THIRD-PARTY-NOTICES.md` and `LICENSE` for licensing information.
