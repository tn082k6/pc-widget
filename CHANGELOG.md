# Änderungen

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
