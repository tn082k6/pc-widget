# PC-Widget

Ein Desktop-Widget für Windows 10/11 im **iOS-18-Look** (OLED-Schwarz): Uhr, Spotify, Wetter und PC-Werte – als fertige App, die man einfach entpackt und startet. Kein Installieren nötig.

<img src="docs/icon.png" width="96" alt="App-Icon">

## Was es kann

**Immer sichtbar (kompakt)**
- Uhrzeit und Datum
- Spotify: Cover, Titel (Laufschrift bei langen Titeln), Künstler, Mini-Equalizer, Songzeit
- Knöpfe für Zurück / Play-Pause / Weiter, Klick aufs Cover öffnet Spotify
- Farbschimmer und Balken in der Farbe des aktuellen Album-Covers

**Maus drüber → klappt sanft auf**
- Spotify-Lautstärke (Mausrad oder Klick auf den Regler, Klick auf 🔈 = stumm)
- Wetter mit Höchst-/Tiefstwert (open-meteo.com)
- CPU, GPU, RAM und Festplatte als Ringe wie im iOS-Batterie-Widget (orange/rot bei hoher Last)

**Einrichtungsfenster** (beim ersten Start oder Doppelklick auf `PC-Widget.exe`, während das Widget läuft)
- Ort suchen per Name oder Postleitzahl (DE/AT/CH)
- Mit Windows starten, Immer im Vordergrund
- Position: oben/unten, links/rechts (unten klappt es nach oben auf)

## Benutzen

1. Unter **[Releases](../../releases)** `PC-Widget.exe` (oder `PC-Widget.zip`) herunterladen.
2. `PC-Widget.exe` doppelklicken – auch direkt aus der ZIP heraus. Die App richtet sich beim ersten Start selbst ein (nach `%LOCALAPPDATA%\PC-Widget`, mit Startmenü-Eintrag).
3. Stadt eingeben, **Speichern & Starten**.

Einstellungen später über *Startmenü → PC-Widget*. Entfernen über *Einstellungen → Apps → Installierte Apps → PC-Widget*.

> Windows zeigt eventuell „Der Computer wurde durch Windows geschützt“, weil die App nicht signiert ist → *Weitere Informationen* → *Trotzdem ausführen*.

Für den Musik-Teil wird die Spotify-Desktop-App gebraucht (funktioniert über die Windows-Mediensteuerung).

## Änderungen

Was in welcher Version neu ist: siehe **[CHANGELOG.md](CHANGELOG.md)**.

## Selbst bauen

Voraussetzung: [Rainmeter 4.5](https://www.rainmeter.net) installiert.

```powershell
.\build\build.ps1
```

Das Skript kompiliert `src/PCWidget.cs` (mit dem C#-Compiler aus dem .NET Framework), packt Rainmeter (portabel) und das Widget in die exe und legt `dist/PC-Widget.exe` und `dist/PC-Widget.zip` an.

## Aufbau

| Pfad | Inhalt |
|---|---|
| `src/PCWidget.cs` | Starter-App + Einrichtungsfenster (WinForms) |
| `skin/PCWidget/PCWidget.ini` | Das Rainmeter-Widget |
| `skin/PCWidget/@Resources/Fx.lua` | Weicher Farbwechsel + Laufschrift |
| `skin/PCWidget/@Resources/Media/nowplaying.ps1` | Helfer: Spotify-Titel/Cover/Steuerung, Spotify-Lautstärke, GPU-Last |
| `skin/Rainmeter.ini` | Portable Rainmeter-Einstellungen |

## Lizenz & Dank

- Eigener Code: MIT (siehe `LICENSE`).
- Die Release-ZIP enthält [Rainmeter](https://www.rainmeter.net) (GPL v2), Quellcode: <https://github.com/rainmeter/rainmeter>.
- Wetter: [open-meteo.com](https://open-meteo.com), Postleitzahlen: [zippopotam.us](https://zippopotam.us).