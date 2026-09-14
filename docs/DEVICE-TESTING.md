# Test an einem echten Receiver

Diese Checkliste prüft die Bibliothek gegen einen realen Denon- oder
Marantz-Receiver. Sie ergänzt die Unit-Tests, weil sich AppCommand-Antworten je
nach Modell und Firmware unterscheiden können.

## Vorbereitung

- Receiver und Rechner befinden sich im selben erreichbaren Netzwerk.
- Die Netzwerksteuerung des Receivers ist aktiviert.
- Das .NET 10 SDK ist installiert.
- Das Repository wurde gebaut:

```powershell
dotnet build
dotnet test
dotnet run --project .\DenonAvrNet.Sample
```

## Rein lesender Stabilitätstest

Nach der automatischen ersten Statusausgabe im Sample die Menüoption `D`
wählen. Sie führt fünf Statusabfragen aus und sendet keine Power-, Lautstärke-,
Mute- oder Eingangsbefehle.

Erwartet werden fünf erfolgreiche Zeilen mit plausiblen Werten für Power,
Eingang und Lautstärke. Bei einem aktiven Audiosignal sollten zusätzlich
Audioformat und Lautsprecher erscheinen. `Unknown` kann ein gültiger, direkt
vom Receiver gemeldeter Wert sein.

Der Test deckt zugleich folgende interne Pfade ab:

- Laden und anschließendes Wiederverwenden der Eingangsliste
- gebündelte AppCommand-Basisabfrage
- automatischer, für die Sitzung gespeicherter Fallback auf Einzelabfragen
- optionale Abfrage von Audioformat und aktiven Lautsprechern

## Funktionstest

Steuernde Tests sollten bewusst einzeln ausgeführt werden. Vor dem
Ausschalten fragt das Sample nochmals nach einer Bestätigung.

| Test | Vorgehen im Sample | Erwartung |
| --- | --- | --- |
| Eingangsliste | Option `9` öffnen | nur aktivierte Eingänge erscheinen |
| CBL/SAT | `CBL/SAT` auswählen | Receiver wechselt auf SAT/CBL |
| Media Player | `Media Player` auswählen | Receiver wechselt auf MPLAY |
| Lautstärke | Optionen `4`, `5`, `6` | neue Lautstärke erscheint im Status |
| Mute | Optionen `7`, `8` | Mute-Status wechselt |
| Audioformat | Quelle mit aktivem Signal abspielen | Format und Soundmodus sind plausibel |
| Lautsprecher | Mehrkanalmaterial abspielen | aktive Kanäle passen zur Wiedergabe |

## Fehler protokollieren

Bei einem gerätespezifischen Fehler bitte mindestens folgende Angaben sichern:

- Receiver-Modell und Firmware-/API-Version
- erkannter HTTP-Port
- ausgewählte Sample-Option
- vollständige Fehlermeldung
- sofern möglich die zugehörige rohe XML-Antwort aus dem Debugger

Ein HTTP-Status 200 mit leerem `<rx>` gilt nicht als erfolgreicher
Protokollinhalt. Beim AVC-X6800H war dafür ein fehlender Zeilenumbruch nach der
XML-Deklaration die Ursache; die Bibliothek erzeugt diesen Zeilenumbruch bereits
automatisch.
