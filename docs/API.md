# API-Dokumentation

Diese Dokumentation beschreibt die öffentlich nutzbaren Typen und den
vorgesehenen Lebenszyklus von `DenonAvrNet`.

## Typischer Lebenszyklus

1. Einen `DenonAvrClient` für Hostname oder IP-Adresse erzeugen.
2. Einmal `InitializeAsync()` aufrufen.
3. Mit `UpdateAsync()` einen bestätigten Status lesen.
4. Optional die Receiver-Fähigkeiten mit `ProbeReceiverCapabilitiesAsync()` ergänzen.
5. Steuerbefehle über `Auto`, HTTP oder Telnet senden.
6. Bei Bedarf nach einer kurzen Verarbeitungszeit erneut den Status lesen.
7. Den Client mit `Dispose()` bzw. `using` freigeben.

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

### Konstruktor

```csharp
new DenonAvrClient(string host, TimeSpan? requestTimeout = null)
```

`host` darf eine IP-Adresse, ein Hostname oder eine HTTP-/HTTPS-Adresse ohne
benötigten Befehlspfad sein. Bei einer vollständigen Adresse übernimmt der
Client nur den Hostanteil. Status- und Geräteabfragen erfolgen über HTTP/XML;
Main-Zone-Steuerbefehle können über HTTP oder Telnet gesendet werden.

Der Standardwert für `requestTimeout` beträgt fünf Sekunden. Der interne
Verbindungsaufbau und `HttpClient` verwenden dasselbe Zeitlimit.

### Eigenschaften

| Eigenschaft | Typ | Beschreibung |
| --- | --- | --- |
| `Host` | `string` | normalisierter Hostname bzw. IP-Adresse |
| `PreferredControlProtocol` | `DenonControlProtocol` | Standardübertragungsweg für Main-Zone-Steuerbefehle; Voreinstellung `Auto` |
| `HttpPort` | `int?` | nach der Initialisierung erkannter Port |
| `DeviceInfo` | `DenonDeviceInfo?` | zuletzt erkannte Geräteinformationen |
| `ReceiverCapabilities` | `DenonReceiverCapabilities?` | für dieses AVR-Modell ermittelte Hardware-Fähigkeiten |
| `State` | `DenonReceiverState?` | zuletzt bestätigter Status-Snapshot |

`HttpPort`, `DeviceInfo`, `ReceiverCapabilities` und `State` sind vor der
jeweiligen erfolgreichen Abfrage `null`.

### `InitializeAsync`

```csharp
Task<DenonDeviceInfo> InitializeAsync(
    CancellationToken cancellationToken = default)
```

Die Methode fragt `Deviceinfo.xml` zuerst auf Port 80 und anschließend auf Port
8080 ab. Erst eine syntaktisch und inhaltlich gültige Denon-Antwort schließt die
Initialisierung ab.

Bei Erfolg werden `HttpPort`, `DeviceInfo` und die zunächst bekannten
`ReceiverCapabilities` gesetzt. Schlagen beide Ports fehl, wird eine `DenonConnectionException` ausgelöst, deren innere
`AggregateException` die einzelnen Fehler enthält.

### `UpdateAsync`

```csharp
Task<DenonReceiverState> UpdateAsync(
    CancellationToken cancellationToken = default)
```

Die Methode liest den Main-Zone-Status und schreibt das Ergebnis zugleich nach
`State`.

Auf Port 8080 werden folgende vier Basisabfragen zunächst gemeinsam an
`AppCommand.xml` gesendet:

- `GetAllZonePowerStatus`
- `GetAllZoneVolume`
- `GetAllZoneMuteStatus`
- `GetAllZoneSource`

Ist die Antwort vollständig auswertbar, verwendet der Client diesen kompakten
Abruf weiter. Bei einer unvollständigen oder leeren Antwort werden dieselben
vier Befehle automatisch einzeln wiederholt. Diese Kompatibilitätsentscheidung
bleibt bis zum nächsten `InitializeAsync()` gespeichert.

`GetDeletedSource` liefert die aktivierte Eingangsliste. Sie wird beim ersten
Statusabruf separat gelesen und anschließend im Client zwischengespeichert.

Danach werden – sofern verfügbar – über `AppCommand0300.xml` abgefragt:

- `GetAudioInfo`
- `GetActiveSpeaker`

Auf Port 80 wird die ältere Main-Zone-Status-XML verwendet. Dort stehen die
erweiterten Audioinformationen normalerweise nicht zur Verfügung.

Nach dem ersten `UpdateAsync()` ist `ReceiverCapabilities.SupportsAppCommand0300`
auf `true` oder `false` gesetzt. Vor diesem Abruf ist der Wert `null`, weil die
Erweiterung noch nicht geprüft wurde.

Die Requests werden nicht parallel ausgeführt. Dadurch werden Firmwareprobleme
mit gleichzeitig eintreffenden AppCommand-Anfragen vermieden.

Der Status-Snapshot enthält bei Receivern mit weiteren Zonen zusätzlich `Zone2`
und `Zone3`. Jeder dieser optionalen `DenonZoneState`-Werte enthält Power,
Eingang, Lautstärke und Mute.

### Gemeinsame HTTP-/Telnet-Steuerung

Die Main-Zone-Methoden verwenden eine gemeinsame API. Ohne Protokollparameter
gilt `PreferredControlProtocol`, standardmäßig `Auto`:

```csharp
await receiver.PowerOnAsync();
await receiver.SetVolumeAsync(-30.0);
await receiver.SetInputAsync("CBL/SAT");

// Einzelnen Befehl erzwingen:
await receiver.SetVolumeAsync(-25.0, DenonControlProtocol.Telnet);

// Für alle nachfolgenden Main-Zone-Befehle:
receiver.PreferredControlProtocol = DenonControlProtocol.Telnet;
```

`Auto` nutzt nach einer erfolgreichen `InitializeAsync()` zunächst HTTP. Wurde
noch nicht initialisiert, wird Telnet verwendet. Liefert der HTTP-Steuerbefehl
einen `HttpRequestException` (beispielsweise HTTP 403), fällt `Auto` einmalig
auf Telnet zurück. Bei ausdrücklich gewähltem `Http` oder `Telnet` gibt es keinen
Fallback.

### `AvrFeature` und `DenonReceiverCapabilities`

`AvrFeature` beschreibt, welche Funktion die **Bibliothek** anbietet. Mit
`GetSupportedProtocols()` kann pro Funktion abgefragt werden, über welche
Übertragungswege sie implementiert ist:

```csharp
var transports = receiver.GetSupportedProtocols(AvrFeature.MainZoneVolume);
// enthält DenonControlProtocol.Http und DenonControlProtocol.Telnet

var eventTransport = receiver.GetSupportedProtocols(AvrFeature.LiveEvents);
// enthält nur DenonControlProtocol.Telnet
```

| `AvrFeature` | Transport in der Library |
| --- | --- |
| `MainZonePower`, `MainZoneVolume`, `MainZoneMute`, `MainZoneInput` | HTTP und Telnet |
| `MainZoneStatus`, `AudioInformation`, `ActiveSpeakerStatus`, `SpeakerPresetLevelControl` | HTTP |
| `Zone2Control`, `Zone3Control`, `LiveEvents`, `ChannelLevelRead`, `ChannelLevelControl`, `SpeakerPresetControl`, `SurroundModeControl`, `DigitalInputModeControl` | Telnet |

`DenonReceiverCapabilities` beschreibt dagegen das **erkannte AVR-Modell**.
Nach `InitializeAsync()` sind HTTP, AppCommand, die Zonenanzahl sowie Zone 2/3
bekannt. `SupportsTelnet` bleibt zunächst `null`, damit ein nicht getesteter
Port nicht fälschlich als nicht unterstützt gilt. Ein lesender `PW?`-Befehl
prüft den Port ohne Zustandsänderung:

```csharp
var receiverCapabilities = await receiver.ProbeReceiverCapabilitiesAsync();
Console.WriteLine(receiverCapabilities.SupportsTelnet); // true oder false

var hasZone3 = receiver.IsFeatureAvailable(AvrFeature.Zone3Control);
```

`IsFeatureAvailable()` kombiniert die Hardware-Fähigkeiten mit dem jeweiligen
Feature. Vor `InitializeAsync()` gibt die Methode `false` zurück, weil noch kein
konkreter Receiver erkannt ist.

### `DenonTelnetClient` – direkte Telnet-Steuerung

Für direkte Spezialbefehle und die zusätzlichen Zonen stellt die Bibliothek
`DenonTelnetClient` bereit. Das Konsolenbeispiel stellt die vollständige
Zonensteuerung über den Menüpunkt `T` bereit. Der Status selbst wird weiterhin
über die HTTP-API gelesen, weil eine Telnet-Antwort wie `Z3SOURCE` nur den
hinterlegten Eingang, nicht den Stromzustand beschreibt.

Die Main Zone kann direkt verwendet werden; in Anwendungen ist normalerweise
die gemeinsame API von `DenonAvrClient` vorzuziehen:

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
await zones.SetZone2InputAsync("MEDIA PLAYER"); // sendet Z2MPLAY

await zones.SetZone3InputAsync("CBL/SAT");      // sendet Z3SAT/CBL
```

`SetZone2VolumeAsync()` und `SetZone3VolumeAsync()` akzeptieren Werte von
`-80,0` bis `+18,0 dB` und runden auf halbe dB-Schritte.

### Temporäre Lautsprecher-Kanalpegel über Telnet

`DenonTelnetClient` unterstützt Denons `CV`-Befehle für die temporären
Kanalpegel des aktuellen Surroundmodus. Diese Werte sind **nicht** identisch
mit den dauerhaften Werten unter **Setup → Speakers → Levels** der
Weboberfläche.
`DenonSpeakerLevelChannel` ist absichtlich kein Flags-Enum: Jeder Ausgang –
auch `Subwoofer`, `Subwoofer2`, `Subwoofer3` und `Subwoofer4` – ist ein eigener
schreibbarer Zielkanal.

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

Dieselben Funktionen sind über die gemeinsame Fassade verfügbar. Da die
Library hierfür derzeit nur Telnet implementiert, verwendet `Auto` unmittelbar
Telnet; `DenonControlProtocol.Http` löst eine `NotSupportedException` aus.

```csharp
var levels = await receiver.GetSpeakerLevelsAsync();
await receiver.SetSpeakerLevelAsync(
    DenonSpeakerLevelChannel.TopFrontLeft,
    1.0,
    DenonControlProtocol.Telnet);
```

`GetSpeakerLevelsAsync()` fragt `CV?` ab und wartet auf die Abschlussmeldung
`CVEND`. Es liefert nur Kanäle zurück, die in der aktuellen Lautsprecher-
Konfiguration des Receivers vorhanden sind. `SetSpeakerLevelAsync()` akzeptiert
`-12,0` bis `+12,0 dB` und rundet auf 0,5-dB-Schritte. Ein Pegel `OFF` ist nur
für Subwoofer-Kanäle zulässig. `ResetSpeakerLevelsToFactoryDefaultsAsync()`
setzt die Pegel auf Denon-Werkswerte zurück; die Library speichert oder stellt
keinen vorherigen Sitzungssnapshot wieder her.

### Speaker-Preset-Pegel der Weboberfläche (HTTP)

Die tatsächlichen Werte des aktiven Speaker-Presets werden beim AVC-X6800H
über die getrennte Weboberfläche auf Port `11080` gelesen und gesetzt. Der Port
ist bewusst unabhängig von `HttpPort` (typischerweise `8080`) der normalen
HTTP/XML-API.

```csharp
var presetLevels = await receiver.GetSpeakerPresetLevelsAsync();

foreach (var level in presetLevels)
{
    Console.WriteLine($"{level.SpeakerIndex}: {level.Decibels:0.0} dB");
}

await receiver.SetSpeakerPresetLevelAsync(speakerIndex: 2, decibels: -3.5);
```

`GetSpeakerPresetLevelsAsync()` liest `/ajax/speakers/get_config?type=20`.
`SetSpeakerPresetLevelAsync()` sendet den Wert als Zehntel-dB (`-35` für
`-3,5 dB`) an `/ajax/speakers/set_config?type=20`. `SpeakerIndex` ist der
vom Receiver gelieferten Weboberflächen-Index und keine
`DenonSpeakerLevelChannel`-Enum-Nummer.

Für den Testton ist bisher nur der Stopp-Aufruf
`<StopTestTone></StopTestTone>` beobachtet worden. Die Library implementiert
deshalb noch keinen Start- oder Stopp-Befehl, bis der zugehörige Start-Request
eindeutig aufgezeichnet ist.

Weitere lesende Telnet-Abfragen sind verfügbar über `QueryPowerAsync()`,
`QueryVolumeAsync()`, `QueryMuteAsync()`, `QueryInputAsync()`,
`QuerySurroundModeAsync()` und `QueryDigitalInputModeAsync()`.

### `DenonReceiverMonitor` – Ereignisse und Rückfall-Polling

`DenonReceiverMonitor` öffnet eine dauerhafte Telnet-Verbindung auf Port 23.
Jede relevante vom Receiver gesendete Statusmeldung löst über `TelnetEventReceived`
sofort eine Aktualisierung aus. Parallel wird der vollständige HTTP-Status standardmäßig
alle 15 Sekunden neu gelesen. Nach einem Verbindungsabbruch verbindet sich der
Telnet-Listener automatisch erneut.

Der Monitor besitzt bewusst einen eigenen HTTP-Client. Dadurch kann er parallel
zur interaktiven Konsolensteuerung laufen, ohne gleichzeitige `UpdateAsync()`-
Aufrufe auf demselben `DenonAvrClient` zu verursachen.

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

`CurrentState` enthält stets den letzten erfolgreichen Status-Snapshot.

#### `OPINFASP`: aktive Lautsprecher-Ausgänge

Der AVC-X6800H sendet während der Wiedergabe wiederholt Meldungen wie:

```text
OPINFASP 22222200222000022000000002200000
```

`OPINFASP` ist eine kompakte Matrix der Lautsprecher-Ausgangspositionen. Die
32 Stellen werden von links nach rechts nummeriert. `2` bedeutet *aktiv*, `0`
bedeutet *nicht aktiv bzw. nicht verfügbar*. Im Beispiel sind die Positionen
`1–6`, `9–11`, `16–17` und `26–27` aktiv – also **13 aktive Ausgangspositionen**.

Die genaue Zuordnung der 32 Positionen zu Kanalnamen veröffentlicht Denon nicht.
Für belastbare Namen wie `FL`, `C`, `SW`, `TFL` oder `TRR` ist deshalb weiterhin
`state.Audio.ActiveSpeakers` aus der HTTP-Abfrage `GetActiveSpeaker` maßgeblich.
Für typsichere Abfragen steht zusätzlich `state.Audio.ActiveSpeakerChannels`
als `SpeakerChannel`-Flags-Enum bereit.

Der Event stellt die bereits dekodierte Matrix bereit:

```csharp
monitor.TelnetEventReceived += item =>
{
    if (item.ActiveSpeakerMatrix is { } speakers)
    {
        Console.WriteLine($"{speakers.ActivePositionCount}/{speakers.PositionCount} aktiv");
        Console.WriteLine(string.Join(", ", speakers.ActivePositions));
    }
};
```

Wiederholte identische `OPINFASP`-Matrizen lösen im Monitor **keine** zusätzliche
HTTP-Aktualisierung mehr aus. Bei einer geänderten Matrix wird der Status einmal
neu geladen; dadurch liefert `StateRefreshed` anschließend die kanalgenauen Namen.

### Speaker-Presets und Audio-Modi über `DenonTelnetClient`

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

Die Speaker-Presets selbst werden im Setup-Menü des Receivers konfiguriert;
die Bibliothek schaltet zwischen Preset 1 und 2 um.

### `RefreshInputsAsync`

```csharp
Task<IReadOnlyList<string>> RefreshInputsAsync(
    CancellationToken cancellationToken = default)
```

Liest die aktuell aktivierten Eingänge erneut vom Receiver und ersetzt den
internen Cache. Falls bereits ein `State` vorhanden ist, erhält auch dessen
`AvailableInputs` die neue Liste; die anderen Statuswerte bleiben unverändert.

Ein manueller Refresh ist sinnvoll, nachdem Eingänge im Setup des Receivers
aktiviert, deaktiviert oder umkonfiguriert wurden. Der erste Aufruf von
`UpdateAsync()` erledigt dies automatisch.

### Power

```csharp
Task PowerOnAsync(CancellationToken cancellationToken = default)
Task PowerOffAsync(CancellationToken cancellationToken = default)
Task PowerOnAsync(DenonControlProtocol protocol, CancellationToken cancellationToken = default)
Task PowerOffAsync(DenonControlProtocol protocol, CancellationToken cancellationToken = default)
```

`PowerOffAsync()` versetzt die Main Zone in Standby. Ob der Receiver danach
weiter über das Netzwerk erreichbar bleibt, hängt von dessen Einstellung zur
Netzwerksteuerung im Standby ab.

### Lautstärke

```csharp
Task VolumeUpAsync(CancellationToken cancellationToken = default)
Task VolumeDownAsync(CancellationToken cancellationToken = default)
Task VolumeUpAsync(DenonControlProtocol protocol, CancellationToken cancellationToken = default)
Task VolumeDownAsync(DenonControlProtocol protocol, CancellationToken cancellationToken = default)
Task SetVolumeAsync(
    double volumeDb,
    CancellationToken cancellationToken = default)
Task SetVolumeAsync(
    double volumeDb,
    DenonControlProtocol protocol,
    CancellationToken cancellationToken = default)
```

`SetVolumeAsync()` akzeptiert Werte von `-80,0` bis `+18,0 dB`. Werte werden
auf halbe Dezibel gerundet und unabhängig von der aktuellen Systemsprache mit
einem Dezimalpunkt an den Receiver übertragen.

### Mute

```csharp
Task SetMuteAsync(
    bool muted,
    CancellationToken cancellationToken = default)
Task SetMuteAsync(
    bool muted,
    DenonControlProtocol protocol,
    CancellationToken cancellationToken = default)
```

`true` aktiviert Mute, `false` deaktiviert Mute.

### Eingang auswählen

```csharp
Task SetInputAsync(
    string input,
    CancellationToken cancellationToken = default)
Task SetInputAsync(
    string input,
    DenonControlProtocol protocol,
    CancellationToken cancellationToken = default)
```

Die Methode akzeptiert sichtbare Standardnamen wie `CBL/SAT` oder
`Media Player` sowie direkte Denon-Protokollnamen. Standardnamen werden ohne
Beachtung der Groß-/Kleinschreibung umgewandelt.

```csharp
await receiver.SetInputAsync("CBL/SAT");       // sendet SISAT/CBL
await receiver.SetInputAsync("MEDIA PLAYER"); // sendet SIMPLAY
await receiver.SetInputAsync("MPLAY");        // ebenfalls zulässig
```

Leere Namen und Namen mit CR/LF werden abgewiesen.

### Beliebigen Befehl senden

```csharp
Task SendCommandAsync(
    string commandPath,
    CancellationToken cancellationToken = default)
```

Die Methode erwartet einen vollständigen Pfad ab `/`, jedoch keinen Hostnamen:

```csharp
await receiver.SendCommandAsync(
    "/goform/formiPhoneAppDirect.xml?DIM%20SEL");
```

Diese Methode prüft nicht, ob der Receiver den übergebenen Befehl unterstützt.

## `DenonControlProtocol`

| Wert | Verhalten |
| --- | --- |
| `Auto` | HTTP nach Initialisierung, sonst Telnet; bei HTTP-Fehler Fallback auf Telnet |
| `Http` | erzwingt HTTP ohne Fallback; `InitializeAsync()` ist erforderlich |
| `Telnet` | erzwingt Telnet auf Port 23 ohne Fallback |

## `DenonReceiverCapabilities`

| Eigenschaft | Typ | Beschreibung |
| --- | --- | --- |
| `SupportsHttp` | `bool` | HTTP-Geräteabfrage wurde erfolgreich initialisiert |
| `SupportsTelnet` | `bool?` | Ergebnis von `ProbeReceiverCapabilitiesAsync()`; vorher `null` |
| `SupportsAppCommand` | `bool` | aktueller Client verwendet die AppCommand-Schnittstelle auf Port 8080 |
| `SupportsAppCommand0300` | `bool?` | Ergebnis der erweiterten Audioabfrage; vor `UpdateAsync()` `null` |
| `SupportsZone2`, `SupportsZone3` | `bool` | aus der vom Receiver gemeldeten Zonenanzahl abgeleitet |
| `ZoneCount` | `int?` | vom Receiver gemeldete Zonenanzahl |

## `DenonSpeakerLevel`

| Eigenschaft | Typ | Beschreibung |
| --- | --- | --- |
| `Channel` | `DenonSpeakerLevelChannel` | exakt angesprochener Denon-CV-Kanal |
| `Decibels` | `double?` | Kanalpegel; bei `OFF` `null` |
| `IsOff` | `bool` | gibt an, ob der Receiver den Kanal mit `00`/`OFF` gemeldet hat |
| `RawResponse` | `string` | originale Telnet-Antwort, z. B. `CVFL 505` |

## `DenonSpeakerPresetLevel`

| Eigenschaft | Typ | Beschreibung |
| --- | --- | --- |
| `SpeakerIndex` | `int` | Index der modernen Denon-Weboberfläche auf Port 11080 |
| `Decibels` | `double` | tatsächlicher Pegel des aktiven Speaker-Presets |

## `DenonDeviceInfo`

| Eigenschaft | Typ | Beschreibung |
| --- | --- | --- |
| `ModelName` | `string` | vom Receiver gemeldetes Modell |
| `ManualModelName` | `string?` | Modellname für Handbuch/Produktgruppe |
| `CommunicationApiVersion` | `string?` | z. B. `0301` |
| `ZoneCount` | `int?` | gemeldete Anzahl der Zonen |
| `MacAddress` | `string?` | vom Receiver gemeldete MAC-Adresse |
| `CategoryName` | `string?` | Gerätekategorie, z. B. `AV RECEIVER` |

## `DenonReceiverState`

| Eigenschaft | Typ | Beschreibung |
| --- | --- | --- |
| `IsPoweredOn` | `bool` | `true`, wenn `Power` gleich `ON` ist |
| `Power` | `string` | Originalwert oder `UNKNOWN` |
| `Input` | `string?` | aktueller Denon-Protokollname |
| `VolumeDb` | `double?` | Master-Lautstärke in dB |
| `IsMuted` | `bool?` | `true`, `false` oder unbekannt |
| `AvailableInputs` | `IReadOnlyList<string>` | aktive/nicht gelöschte Standardquellen |
| `Audio` | `DenonAudioInfo?` | optionale Audio- und Lautsprecherangaben |

Das Objekt ist ein unveränderlicher Snapshot. Ein bereits zurückgegebenes
Objekt wird durch spätere Abfragen nicht nachträglich verändert.

## `DenonAudioInfo`

| Eigenschaft | Typ | Denon-Parameter |
| --- | --- | --- |
| `InputMode` | `string?` | `inputmode` |
| `Output` | `string?` | `output` |
| `AudioFormat` | `string?` | `signal` |
| `SoundMode` | `string?` | `sound` |
| `SampleRate` | `string?` | `fs` |
| `ActiveSpeakers` | `IReadOnlyList<string>` | Werte aus `GetActiveSpeaker` mit `control="2"` |
| `ActiveSpeakerChannels` | `SpeakerChannel` | kombinierte, typsichere Variante von `ActiveSpeakers` |

`AudioFormat` bezeichnet das vom Receiver erkannte Eingangssignal.
`SoundMode` bezeichnet dagegen den aktuell angewendeten Wiedergabe- oder
Upmix-Modus. Deshalb können beispielsweise `PCM` und `Dolby Surround`
gleichzeitig gemeldet werden.

Typische Kanalkürzel:

| Kürzel | Kanal |
| --- | --- |
| `FL`, `FR` | Front links/rechts |
| `C` | Center |
| `SW`, `SW1`, `SW2` | Subwoofer |
| `SL`, `SR` | Surround links/rechts |
| `SBL`, `SBR` | Surround Back links/rechts |
| `TFL`, `TFR` | Top Front links/rechts |
| `TML`, `TMR` | Top Middle links/rechts |
| `TRL`, `TRR` | Top Rear links/rechts |
| `FHL`, `FHR` | Front Height links/rechts |
| `RHL`, `RHR` | Rear Height links/rechts |
| `FWL`, `FWR` | Front Wide links/rechts |
| `SB`, `SBL`, `SBR` | einzelner bzw. linker/rechter Surround Back |
| `SHL`, `SHR` | Surround Height links/rechts |
| `CH`, `TS` | Center Height bzw. Top Surround |
| `FDL`, `FDR`, `SDL`, `SDR`, `BDL`, `BDR` | Dolby-Atmos-Enabled-Lautsprecher |

Beispiel für eine typsichere Kanalabfrage:

```csharp
var activeChannels = state.Audio?.ActiveSpeakerChannels ?? SpeakerChannel.None;
var hasFrontWideLeft =
    (activeChannels & SpeakerChannel.FrontWideLeft) != SpeakerChannel.None;
```

## Status regelmäßig aktualisieren

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

Derzeit existiert keine interne Hintergrundabfrage. Die aufrufende Anwendung
bestimmt das Aktualisierungsintervall selbst. Mehrere `UpdateAsync()`-Aufrufe
auf demselben Client sollten nicht parallel laufen.

## Ausnahmen

Alle bibliotheksspezifischen Ausnahmen leiten sich von `DenonAvrException` ab.

| Ausnahme | Auslöser |
| --- | --- |
| `DenonConnectionException` | Initialisierung auf Port 80 und 8080 fehlgeschlagen |
| `DenonProtocolException` | ungültiges XML, falsches Wurzelelement oder fehlende Basisstatuswerte |

Transportfehler bleiben `HttpRequestException`; Abbrüche bleiben
`OperationCanceledException`. Dadurch können Anwendungen Netzwerkfehler,
Protokollfehler und bewusste Abbrüche getrennt behandeln.

## Ressourcen und Lebensdauer

Der öffentliche Konstruktor erzeugt intern einen `HttpClient`. Deshalb sollte
`DenonAvrClient` wiederverwendet und nach Gebrauch freigegeben werden:

```csharp
using var receiver = new DenonAvrClient("10.37.0.190");
```

Nicht für jede einzelne Anfrage einen neuen Client erzeugen.
