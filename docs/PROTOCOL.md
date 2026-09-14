# Denon-HTTP/XML-Protokollnotizen

Dieses Dokument beschreibt die von `DenonAvrNet` tatsächlich verwendeten
Protokollteile. Es ist keine vollständige Dokumentation aller Denon-Befehle.
Unterstützte Funktionen können je nach Modell und Firmware abweichen.

## Port- und Geräteerkennung

Die Initialisierung versucht der Reihe nach:

1. `GET http://HOST:80/goform/Deviceinfo.xml`
2. `GET http://HOST:8080/goform/Deviceinfo.xml`

Eine HTTP-200-Antwort allein reicht nicht. Das XML muss ein
`Device_Info`-Wurzelelement und einen nicht leeren `ModelName` enthalten.

Der erkannte Port wird anschließend für Status- und Steuerbefehle verwendet.

## Verwendete Endpunkte

| Methode | Endpunkt | Verwendung |
| --- | --- | --- |
| `GET` | `/goform/Deviceinfo.xml` | Geräteerkennung und Eigenschaften |
| `POST` | `/goform/AppCommand.xml` | Basisstatus auf modernen Receivern |
| `POST` | `/goform/AppCommand0300.xml` | Audio- und Lautsprecherdetails |
| `GET` | `/goform/formMainZone_MainZoneXmlStatus.xml` | Status älterer Receiver |
| `GET` | `/goform/formiPhoneAppPower.xml?...` | Power |
| `GET` | `/goform/formiPhoneAppVolume.xml?...` | absolute Lautstärke |
| `GET` | `/goform/formiPhoneAppMute.xml?...` | Mute |
| `GET` | `/goform/formiPhoneAppDirect.xml?...` | direkte Steuerbefehle |

## AppCommand auf dem AVC-X6800H

Der XML-Parser des getesteten AVC-X6800H verarbeitet eine formal gültige,
vollständig einzeilige Anfrage nicht korrekt. Folgender Body führt zu einer
leeren Antwort:

```xml
<?xml version="1.0" encoding="utf-8"?><tx><cmd id="1">GetAllZonePowerStatus</cmd></tx>
```

Der Receiver antwortet dabei irreführend mit HTTP 200:

```xml
<?xml version="1.0" encoding="utf-8" ?>
<rx>
</rx>
```

Zwischen XML-Deklaration und Dokumentelement muss deshalb explizit `CRLF`
stehen:

```xml
<?xml version="1.0" encoding="utf-8"?>
<tx><cmd id="1">GetAllZonePowerStatus</cmd></tx>
```

Der tatsächlich erzeugte String verwendet `\r\n`, unabhängig vom
Betriebssystem. Der Request wird UTF-8 ohne BOM und mit folgendem Header
gesendet:

```text
Content-Type: text/xml; charset=utf-8
```

## Basisstatus: `AppCommand.xml`

Basisbefehle verwenden `cmd id="1"` und den Befehlsnamen als Textinhalt:

```xml
<?xml version="1.0" encoding="utf-8"?>
<tx><cmd id="1">GetAllZonePowerStatus</cmd></tx>
```

Beispielantwort:

```xml
<rx>
  <cmd>
    <zone1>ON</zone1>
    <zone2>OFF</zone2>
    <zone3>OFF</zone3>
  </cmd>
</rx>
```

Die Bibliothek bündelt Power, Lautstärke, Mute und Quelle zunächst in einem
Request. Die vier `<cmd>`-Antworten werden in derselben Reihenfolge ausgewertet.
Die Eingangsliste wird separat gelesen und anschließend zwischengespeichert.

Enthält die gebündelte Antwort nicht genau vier Ergebnisse oder keinen
auswertbaren Main-Zone-Wert, sendet der Client die vier Statusbefehle nochmals
einzeln. Er merkt sich dieses Receiververhalten bis zur nächsten
Initialisierung, sodass spätere Aktualisierungen den erfolglosen gebündelten
Versuch überspringen.

Ein nicht unterstützter Einzelbefehl kann als `<error>` zurückgegeben werden.
Solange mindestens ein auswertbarer Main-Zone-Basiswert vorhanden ist, können
die übrigen Werte weiterverwendet werden.

## Detailstatus: `AppCommand0300.xml`

Detailbefehle verwenden `cmd id="3"`, ein `name`-Element und eine Parameterliste.

### Audioinformationen

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

Beispielantwort:

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

### Aktive Lautsprecher

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

In der Antwort kennzeichnet `control="2"` einen aktuell aktiven Kanal.
`control="1"` bezeichnet einen verfügbaren bzw. konfigurierten, aber aktuell
nicht aktiven Kanal. `control="0"` wird nicht als aktiv ausgewertet.

Die Bibliothek übernimmt den Textinhalt aktiver Parameter, beispielsweise
`FL`, `FR`, `C`, `SW`, `SL`, `SR`, `TFL` oder `TFR`.

## Eingangssteuerung

Die Eingangswahl erfolgt über:

```text
/goform/formiPhoneAppDirect.xml?SI<PROTOKOLLNAME>
```

Beispiele:

| Sichtbarer Name | HTTP-Befehl |
| --- | --- |
| `CBL/SAT` | `...formiPhoneAppDirect.xml?SISAT/CBL` |
| `Media Player` | `...formiPhoneAppDirect.xml?SIMPLAY` |
| `TV AUDIO` | `...formiPhoneAppDirect.xml?SITV` |
| `Blu-ray` | `...formiPhoneAppDirect.xml?SIBD` |

Der Slash in `SAT/CBL` muss im Query-Befehl erhalten bleiben. Eine Übertragung
als `%2F` wird von manchen Denon-Firmwareständen nicht als derselbe
Protokollbefehl behandelt.

## XML-Verarbeitung

Antworten werden mit folgenden Sicherheitsvorgaben gelesen:

- DTD-Verarbeitung ist deaktiviert.
- Externe XML-Resolver sind deaktiviert.
- Die maximale Dokumentgröße ist begrenzt.
- Element- und Attributnamen werden ohne Beachtung der Groß-/Kleinschreibung
  verglichen.
- Textwerte werden getrimmt; leere Werte werden als nicht vorhanden behandelt.

## Typische Fehlerbilder

### HTTP 403 bei älteren Statuspfaden

Auf aktuellen Receivern können ältere Endpunkte wie
`formMainZone_MainZoneXmlStatus.xml` auf Port 8080 mit HTTP 403 antworten.
`DenonAvrNet` verwendet dort stattdessen die AppCommand-Endpunkte.

### HTTP 200 mit leerem `<rx>`

Beim AVC-X6800H deutet dies insbesondere auf einen fehlenden Zeilenumbruch
zwischen XML-Deklaration und `<tx>` hin. Der Serverstatus allein darf deshalb
nicht als erfolgreicher Protokollrequest interpretiert werden.

### Nicht alle Werte vorhanden

Mögliche Ursachen sind nicht unterstützte Kommandos, der Standby-Zustand oder
gebündelte/parallele Requests. Die Bibliothek wechselt bei einer unvollständigen
gebündelten Basisantwort automatisch auf sequenzielle Einzelabfragen und
behandelt die Audioerweiterung als optional.

### `AudioFormat` ist `Unknown`

Dieser Wert stammt direkt aus `GetAudioInfo/signal`. Er wird nicht aus dem
Soundmodus abgeleitet. `SoundMode` kann trotzdem beispielsweise
`Dolby Surround` enthalten.

## Telnet

Viele Receiver stellen zusätzlich das Denon-IP-Steuerprotokoll auf TCP-Port 23
bereit. Der AVC-X6800H-Test bestätigte die Erreichbarkeit dieses Ports.
`DenonAvrNet` verwendet Telnet derzeit jedoch noch nicht; die Angabe dient nur
der Abgrenzung des aktuellen Funktionsumfangs.
