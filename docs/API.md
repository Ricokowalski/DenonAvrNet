# API-Dokumentation

Diese Dokumentation beschreibt die öffentlich nutzbaren Typen und den
vorgesehenen Lebenszyklus von `DenonAvrNet`.

## Typischer Lebenszyklus

1. Einen `DenonAvrClient` für Hostname oder IP-Adresse erzeugen.
2. Einmal `InitializeAsync()` aufrufen.
3. Mit `UpdateAsync()` einen bestätigten Status lesen.
4. Steuerbefehle senden.
5. Bei Bedarf nach einer kurzen Verarbeitungszeit erneut den Status lesen.
6. Den Client mit `Dispose()` bzw. `using` freigeben.

```csharp
using DenonAvrNet;

using var receiver = new DenonAvrClient(
    "10.37.0.190",
    requestTimeout: TimeSpan.FromSeconds(5));

await receiver.InitializeAsync();

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
Client nur den Hostanteil. Die Denon-Abfragen selbst werden derzeit über HTTP
ausgeführt.

Der Standardwert für `requestTimeout` beträgt fünf Sekunden. Der interne
Verbindungsaufbau und `HttpClient` verwenden dasselbe Zeitlimit.

### Eigenschaften

| Eigenschaft | Typ | Beschreibung |
| --- | --- | --- |
| `Host` | `string` | normalisierter Hostname bzw. IP-Adresse |
| `HttpPort` | `int?` | nach der Initialisierung erkannter Port |
| `DeviceInfo` | `DenonDeviceInfo?` | zuletzt erkannte Geräteinformationen |
| `State` | `DenonReceiverState?` | zuletzt bestätigter Status-Snapshot |

`HttpPort`, `DeviceInfo` und `State` sind vor der jeweiligen erfolgreichen
Abfrage `null`.

### `InitializeAsync`

```csharp
Task<DenonDeviceInfo> InitializeAsync(
    CancellationToken cancellationToken = default)
```

Die Methode fragt `Deviceinfo.xml` zuerst auf Port 80 und anschließend auf Port
8080 ab. Erst eine syntaktisch und inhaltlich gültige Denon-Antwort schließt die
Initialisierung ab.

Bei Erfolg werden `HttpPort` und `DeviceInfo` gesetzt. Schlagen beide Ports
fehl, wird eine `DenonConnectionException` ausgelöst, deren innere
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

Die Requests werden nicht parallel ausgeführt. Dadurch werden Firmwareprobleme
mit gleichzeitig eintreffenden AppCommand-Anfragen vermieden.

Der Status-Snapshot enthält bei Receivern mit weiteren Zonen zusätzlich `Zone2`
und `Zone3`. Jeder dieser optionalen `DenonZoneState`-Werte enthält Power,
Eingang, Lautstärke und Mute.

### `DenonTelnetClient` – Zone 2 und Zone 3 steuern

Für die zusätzlichen Zonen verwendet die Bibliothek Denons Telnet-Protokoll auf
Port 23. Das Konsolenbeispiel stellt die vollständige Zonensteuerung über den
Menüpunkt `T` bereit. Der Status selbst wird weiterhin über die HTTP-API gelesen,
weil eine Telnet-Antwort wie `Z3SOURCE` nur den hinterlegten Eingang, nicht den
Stromzustand beschreibt.

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
```

`PowerOffAsync()` versetzt die Main Zone in Standby. Ob der Receiver danach
weiter über das Netzwerk erreichbar bleibt, hängt von dessen Einstellung zur
Netzwerksteuerung im Standby ab.

### Lautstärke

```csharp
Task VolumeUpAsync(CancellationToken cancellationToken = default)
Task VolumeDownAsync(CancellationToken cancellationToken = default)
Task SetVolumeAsync(
    double volumeDb,
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
```

`true` aktiviert Mute, `false` deaktiviert Mute.

### Eingang auswählen

```csharp
Task SetInputAsync(
    string input,
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
