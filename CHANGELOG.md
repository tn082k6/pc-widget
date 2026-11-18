# Änderungen

## 1.7 – 18.11.2026

- **Großer Griff zum An-den-Rand-Schieben:** Die Klickfläche an der Widget-Kante geht jetzt über die ganze Höhe. Mit der Maus darüber wird daraus eine helle Leiste mit Pfeil – Klick schiebt das Widget an den Rand.
- **Doppelklick** auf eine freie Stelle im Widget schiebt es ebenfalls an den Rand.
- Behoben: Update konnte fehlschlagen („PC-Widget konnte nicht eingerichtet werden“), wenn die App im Hintergrund gerade nach Updates schaute.

## 1.6 – 15.11.2026

- **Größerer Pfeil-Griff** (24 × 90) mit einer unsichtbaren, noch größeren Hover-Zone – viel leichter zu treffen.
- **Kein Flackern mehr beim Aufklappen:** Das Widget klappt nur noch auf, wenn es zu ist, und erst wieder zu, wenn die Maus eine Drittelsekunde weg ist.
- Behoben: Nach dem Laden war das Widget manchmal schon aufgeklappt.
- Behoben: Tooltip „Dieser PC öffnen“ tauchte über dem halben Widget auf.
- Behoben: doppelte Reste in der Widget-Datei aus 1.4/1.5 aufgeräumt.
- Beim Update wird die Position des Pfeil-Griffs immer passend neu gesetzt.

## 1.5 – 15.11.2026

- **Maus auf den Pfeil = Widget kommt raus:** Kurz (¼ Sekunde) mit der Maus auf den Pfeil-Griff – das Widget erscheint. Maus wieder weg, und es geht zurück an den Rand. Klick auf den Pfeil holt es dauerhaft zurück.
- **Postleitzahl wird gemerkt:** Die eingegebene PLZ wird mitgespeichert und im Einrichtungsfenster angezeigt („Aktuell: Neu-Anspach · 61267“).
- **Einstellungen überleben jedes Update sicher:** Stadt, PLZ und Koordinaten liegen zusätzlich in einer eigenen Datei (`einstellungen.ini`), die bei Updates nie ersetzt wird.

## 1.4 – 15.11.2026

- **Neues Logo:** App-Icon im iOS-Stil (Squircle mit Mini-Widget: Ring, Equalizer, Wertebalken); in klein ein klarer blauer Ring.
- **Schalter überarbeitet:** kein dunkler Kasten mehr um die Schalter, weichere Animation, Knopf wird beim Drücken breiter (wie iOS), ganze Zeile anklickbar, per Tastatur bedienbar.
- **Zurückholen zuverlässiger:** Das Widget kommt jetzt auch zurück, wenn man mit Win+D oder über die Taskleiste auf den Desktop geht (vorher blieb es manchmal weg).
- **Schneller:** Widget und Pfeil-Griff reagieren ca. 4× pro Sekunde statt 1×.
- Fenster öffnen sich immer normal im Vordergrund (nicht minimiert).

## 1.3 – 15.11.2026

**Automatische Updates**
- Die App schaut beim Windows-Start (höchstens alle 6 Stunden) auf GitHub nach neuen Versionen und fragt dann „Update verfügbar – Jetzt aktualisieren / Später“.
- Läuft gerade ein **Spiel im Vollbild**, wird nicht gefragt – dann beim nächsten Mal.
- Einstellungen (Stadt, Position, Autostart …) bleiben beim Update erhalten.
- Im Einrichtungsfenster: „Version 1.3 · Nach Updates suchen“.
- Hinweis: Wer noch 1.2 hat, muss 1.3 einmal selbst installieren – ab dann kommen Updates automatisch.

**Seitenleiste**
- Ist ein Programm im Vordergrund, schiebt sich das Widget weg und lässt am Bildschirmrand einen kleinen **Pfeil-Griff** stehen. Klick darauf holt es zurück. Auf dem Desktop ist es ganz normal da.
- Kleiner **Griff an der Widget-Kante** zum Wegschieben per Hand.
- Im Einrichtungsfenster an-/abschaltbar: „An den Rand bei offenen Fenstern“.

**Beenden & Menü**
- **Rechtsklick aufs Widget:** Einstellungen, An den Rand schieben, **Widget beenden**.
- Im Einrichtungsfenster gibt es „Widget beenden“, solange das Widget läuft.

## 1.2 – 15.11.2026

**Einfacher zu verschicken**
- Es gibt jetzt nur noch **eine Datei**: `PC-Widget.exe`. Doppelklick reicht – auch direkt aus der ZIP heraus, ohne Entpacken.
- Die App richtet sich beim ersten Start selbst ein (`%LOCALAPPDATA%\PC-Widget`) und legt einen Eintrag im **Startmenü** an.
- Eintrag unter *Einstellungen → Apps → Installierte Apps* zum normalen **Deinstallieren**.
- Behoben: Windows hat bei **jedem Start** gefragt „Möchten Sie diese Datei ausführen?“ (Internet-Markierung wird jetzt entfernt).
- Behoben: Fehlermeldung „Ordner Rainmeter fehlt“, wenn die exe direkt in der ZIP gestartet wurde.

**Neues Einrichtungsfenster**
- Dunkles Design im iOS-Look mit eigenem App-Icon, runden Schaltern und Knöpfen.
- **Ortssuche mit Vorschlägen** beim Tippen – per Stadtname oder **Postleitzahl** (Deutschland, Österreich, Schweiz).
- Position in **allen vier Ecken**: oben/unten, links/rechts. Unten klappt das Widget nach oben auf.
- Schalter für „Mit Windows starten“ und „Immer im Vordergrund“.

**Widget**
- Klick auf die Ringe **CPU, GPU oder RAM** öffnet den **Task-Manager**, Klick auf **C:** öffnet „Dieser PC“. Beim Drüberfahren leuchtet der Ring leicht auf.
- GPU-Auslastung wird jetzt vom Helfer gemessen (das Rainmeter-Modul dafür ist in der portablen Version abgestürzt).
- Behoben: **kaputte Umlaute** (z. B. „lÃ¤uft“ statt „läuft“) in Anleitung, Tooltips und README.

## 1.0 – 15.11.2026

Erste Version.

- Widget im **iOS-18-Look** mit OLED-Schwarz und runden Ecken.
- **Immer sichtbar:** Uhrzeit, Datum und Spotify (Cover, Titel mit Laufschrift, Künstler, Mini-Equalizer, Songzeit, Zurück/Play/Weiter, Klick aufs Cover öffnet Spotify).
- **Farbschimmer** und Balken in der Farbe des aktuellen Album-Covers, weich überblendet.
- **Maus drüber** → klappt sanft auf: Spotify-Lautstärke (Mausrad/Klick), Wetter mit Höchst-/Tiefstwert, CPU/GPU/RAM/Festplatte als Ringe (orange/rot bei hoher Last).
- Fertige App zum Verschicken: portables Rainmeter + Starter-App mit Einrichtungsfenster.
