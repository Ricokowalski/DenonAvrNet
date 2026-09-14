# DenonAvrNet

`DenonAvrNet` ist eine asynchrone C#/.NET-Bibliothek zur Steuerung kompatibler
Denon- und Marantz-AV-Receiver über deren lokale HTTP/XML- und Telnet-Schnittstelle.

Die Bibliothek wurde insbesondere mit einem **Denon AVC-X6800H** und dessen
Kommunikations-API `0301` auf HTTP-Port `8080` entwickelt. Ältere Geräte mit
der Statusschnittstelle auf Port `80` werden ebenfalls berücksichtigt.

## Funktionsumfang

- automatische Erkennung der HTTP-Schnittstelle auf Port `80` oder `8080`
- Auslesen von Modell, API-Version, Zonenanzahl, MAC-Adresse und Gerätekategorie
- Status der Main Zone:
  - Power
  - gewählter Eingang
  - Master-Lautstärke
  - Mute
  - verfügbare Eingänge
- Status für Zone 2 und Zone 3 (Power, Eingang, Lautstärke, Mute)
- zwischengespeicherte Eingangsliste mit gezielter Aktualisierung
- automatische Kompatibilitätsumschaltung bei unvollständigen gebündelten
  AppCommand-Antworten
- erweiterte Audioinformationen auf kompatiblen Geräten:
  - Audio-Eingangsmodus
  - Audioausgang
  - erkanntes Audioformat
  - aktiver Soundmodus
  - Samplerate
  - aktive Lautsprecherkanäle
- Main Zone ein- und ausschalten
- Lautstärke erhöhen, verringern oder absolut in dB setzen
- Mute ein- und ausschalten
- Eingang anhand des sichtbaren Denon-Namens auswählen
- Speaker Preset 1 oder 2 umschalten
- Surround-Modus wählen: Auto, Stereo, Dolby Surround, DTS Neural:X,
  Multi Ch Stereo oder Pure Direct
- digitalen Eingangsdecoder auf Auto, PCM oder DTS setzen
- Zone 2 und Zone 3 über Telnet-Port 23 steuern:
  - ein-/ausschalten
  - Lautstärke erhöhen, verringern oder absolut in dB setzen
  - Mute ein-/ausschalten
  - Eingang auswählen
- dauerhafte Statusüberwachung für Headless-Betrieb:
  - Telnet-Ereignisse werden sofort empfangen
  - `OPINFASP`-Speaker-Matrizen werden dekodiert und doppelte Telemetrie wird gefiltert
  - automatisches Wiederverbinden nach einer Telnet-Unterbrechung
  - HTTP-Statusabfrage alle 15 Sekunden als Rückfallebene
- beliebige rohe Denon-HTTP-Befehlspfade senden
- Unterstützung von `CancellationToken`

## Voraussetzungen

- .NET 10 SDK
- Receiver und Anwendung im selben erreichbaren Netzwerk
- aktivierte Netzwerksteuerung am Receiver

## Projektstruktur

| Projekt | Inhalt |
| --- | --- |
| `DenonAvrNet` | wiederverwendbare Klassenbibliothek |
| `DenonAvrNet.Sample` | interaktives Konsolenbeispiel |
| `DenonAvrNet.Tests` | Unit- und Protokolltests |

## Einbinden

Solange kein NuGet-Paket veröffentlicht ist, wird die Bibliothek als
Projektverweis eingebunden:

```xml
<ItemGroup>
  <ProjectReference Include="..\DenonAvrNet\DenonAvrNet.csproj" />
</ItemGroup>
```

## Schnellstart

```csharp
using DenonAvrNet;
using DenonAvrNet.Models;

using var receiver = new DenonAvrClient("10.37.0.190");

var device = await receiver.InitializeAsync();
Console.WriteLine($"{device.ModelName}, API {device.CommunicationApiVersion}");
Console.WriteLine($"HTTP-Port: {receiver.HttpPort}");

var state = await receiver.UpdateAsync();
Console.WriteLine($"Power: {state.Power}");
Console.WriteLine($"Eingang: {state.Input}");
Console.WriteLine($"Lautstärke: {state.VolumeDb} dB");
Console.WriteLine($"Audioformat: {state.Audio?.AudioFormat ?? "unbekannt"}");
Console.WriteLine($"Soundmodus: {state.Audio?.SoundMode ?? "unbekannt"}");
Console.WriteLine($"Aktive Lautsprecher: {string.Join(", ", state.Audio?.ActiveSpeakers ?? [])}");
Console.WriteLine($"Aktive Kanäle: {state.Audio?.ActiveSpeakerChannels ?? SpeakerChannel.None}");
```

`InitializeAsync()` muss einmal erfolgreich ausgeführt werden, bevor Status-
oder Steuerbefehle verwendet werden können.

## Receiver steuern

```csharp
await receiver.PowerOnAsync();
await receiver.VolumeUpAsync();
await receiver.VolumeDownAsync();
await receiver.SetVolumeAsync(-40.0);
await receiver.SetMuteAsync(true);
await receiver.SetMuteAsync(false);
await receiver.SetInputAsync("Media Player");
```

Ein Steuerbefehl verändert `receiver.State` nicht vorab. Für einen vom Gerät
bestätigten Zustand muss anschließend erneut abgefragt werden:

```csharp
await receiver.SetInputAsync("CBL/SAT");
await Task.Delay(350);
var confirmedState = await receiver.UpdateAsync();
```

Die Liste `AvailableInputs` wird beim ersten Statusabruf geladen und danach
zwischengespeichert. Nach Änderungen an der Eingangs-Konfiguration des Receivers
kann sie ausdrücklich neu gelesen werden:

```csharp
var currentInputs = await receiver.RefreshInputsAsync();
```

## Eingangsnamen

Denon verwendet teilweise andere Protokollnamen als in der Bedienoberfläche.
`SetInputAsync()` bildet die wichtigsten Anzeigenamen automatisch ab:

| Anzeigename | Denon-Protokollname |
| --- | --- |
| `CBL/SAT` | `SAT/CBL` |
| `Media Player` | `MPLAY` |
| `TV AUDIO` | `TV` |
| `Blu-ray` | `BD` |
| `Bluetooth` | `BT` |
| `NETWORK` | `NET` |
| `iPod/USB` | `USB/IPOD` |
| `AUX` | `AUX1` |
| `Tuner` oder `FM` | `TUNER` |

Die Zuordnung arbeitet ohne Beachtung der Groß-/Kleinschreibung. Bereits
bekannte Protokollnamen wie `MPLAY` oder `SAT/CBL` können ebenfalls direkt
übergeben werden.

## Statusmodell

`UpdateAsync()` liefert einen `DenonReceiverState` und speichert denselben
Snapshot in `receiver.State`.

| Eigenschaft | Bedeutung |
| --- | --- |
| `IsPoweredOn` | vereinfachter boolescher Power-Status |
| `Power` | unveränderter Power-Text des Receivers |
| `Input` | aktueller Denon-Protokollname des Eingangs |
| `VolumeDb` | Master-Lautstärke in dB oder `null` |
| `IsMuted` | Mute-Status oder `null` |
| `AvailableInputs` | nicht deaktivierte Standardeingänge |
| `Audio` | optionale erweiterte Audioinformationen |
| `Zone2`, `Zone3` | optionale Status-Snapshots der zusätzlichen Zonen |

`DenonAudioInfo` enthält:

| Eigenschaft | Bedeutung |
| --- | --- |
| `InputMode` | beispielsweise `HDMI` |
| `Output` | beispielsweise `Speaker` |
| `AudioFormat` | erkanntes Eingangssignal, z. B. Dolby Digital Plus |
| `SoundMode` | aktiver Wiedergabe-/Upmix-Modus |
| `SampleRate` | beispielsweise `48 kHz` |
| `ActiveSpeakers` | aktive Denon-Kanalkürzel wie `FL`, `C`, `SW` oder `TFL` |
| `ActiveSpeakerChannels` | dieselben Kanäle als kombinierbares `SpeakerChannel`-Flags-Enum |

`Audio` kann bei älteren oder inkompatiblen Receivern `null` sein. Auch einzelne
Werte können fehlen. Ein vom Receiver geliefertes `Unknown` wird bewusst nicht
umgedeutet.

## Fehlerbehandlung und Abbruch

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
    Console.Error.WriteLine($"Receiver nicht erreichbar: {exception.Message}");
}
catch (DenonProtocolException exception)
{
    Console.Error.WriteLine($"Unerwartete Receiver-Antwort: {exception.Message}");
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("Vorgang abgebrochen oder Zeitlimit erreicht.");
}
```

| Ausnahme | Bedeutung |
| --- | --- |
| `DenonConnectionException` | auf keinem unterstützten Port wurden gültige Geräteinformationen gefunden |
| `DenonProtocolException` | XML oder erwartete Denon-Statuswerte sind ungültig bzw. fehlen |
| `HttpRequestException` | HTTP- oder Netzwerkfehler während einer Anfrage |
| `InvalidOperationException` | der Client wurde noch nicht initialisiert |
| `ArgumentOutOfRangeException` | Lautstärke außerhalb von `-80,0` bis `+18,0 dB` |

## Raw Commands

Noch nicht als eigene Methode verfügbare Receiverfunktionen können über einen
vollständigen Denon-Befehlspfad angesprochen werden:

```csharp
await receiver.SendCommandAsync(
    "/goform/formiPhoneAppDirect.xml?VSMONI2");
```

Der Pfad muss mit `/` beginnen. Die aufrufende Anwendung ist dafür
verantwortlich, dass der Befehl vom jeweiligen Receiver unterstützt wird.

## Sample und Tests starten

```powershell
dotnet run --project .\DenonAvrNet.Sample
dotnet test
```

Im Sample startet die Menüoption `D` fünf aufeinanderfolgende Statusabfragen.
Diese Diagnose ist rein lesend und ändert keine Receiver-Einstellung.

## Technische Hinweise

- Port `8080` verwendet für den Basisstatus `AppCommand.xml` mit
  `cmd id="1"`. Vier Statusbefehle werden zunächst in einem Request gebündelt.
- Liefert ein Receiver darauf keine vollständig auswertbare Antwort, wiederholt
  der Client den Abruf mit Einzelrequests und merkt sich diese Variante bis zur
  nächsten Initialisierung.
- Audioformat und aktive Lautsprecher werden über `AppCommand0300.xml` mit
  `cmd id="3"` abgefragt.
- Die Detailabfragen erfolgen sequenziell; parallele Zugriffe werden vermieden.
- Die Eingangsliste wird separat geladen und im Client zwischengespeichert.
- Der AVC-X6800H benötigt nach der XML-Deklaration ein CRLF. Ohne diesen
  Zeilenumbruch antwortet er mit HTTP 200 und einem leeren `<rx>`-Element.
- `DenonAvrClient` sollte nicht gleichzeitig über mehrere parallele
  `UpdateAsync()`-Aufrufe verwendet werden.

Weitere Details stehen in der [API-Dokumentation](docs/API.md), den
[Protokollnotizen](docs/PROTOCOL.md) und der
[Geräte-Testanleitung](docs/DEVICE-TESTING.md).

## Aktuelle Grenzen

- Der Schwerpunkt liegt derzeit auf der Main Zone.
- Umbenannte Eingänge werden noch nicht separat auf ihre benutzerdefinierten
  Anzeigenamen abgebildet.
- Ereignisse werden über eine dauerhaft geöffnete Telnet-Verbindung empfangen;
  eigene Ereignis-Handler sollten keine lang laufenden Arbeiten ausführen.

## Referenz und Lizenz

Die Implementierung wurde durch
[`ol-iver/denonavr`](https://github.com/ol-iver/denonavr) inspiriert. Die
ursprüngliche Referenzversion basiert auf Commit
`98566b286efab12496ef623eda498db8bcb5ea09` (`1.4.0-dev`).

Lizenz- und Herkunftshinweise befinden sich in [LICENSE.txt](LICENSE.txt) und
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
