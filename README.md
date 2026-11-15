# PC-Widget

Ein Desktop-Widget fÃ¼r Windows 10/11 im **iOS-18-Look** (OLED-Schwarz): Uhr, Spotify, Wetter und PC-Werte â€“ als fertige App, die man einfach entpackt und startet. Kein Installieren nÃ¶tig.

<img src="docs/icon.png" width="96" alt="App-Icon">

## Was es kann

**Immer sichtbar (kompakt)**
- Uhrzeit und Datum
- Spotify: Cover, Titel (Laufschrift bei langen Titeln), KÃ¼nstler, Mini-Equalizer, Songzeit
- KnÃ¶pfe fÃ¼r ZurÃ¼ck / Play-Pause / Weiter, Klick aufs Cover Ã¶ffnet Spotify
- Farbschimmer und Balken in der Farbe des aktuellen Album-Covers

**Maus drÃ¼ber â†’ klappt sanft auf**
- Spotify-LautstÃ¤rke (Mausrad oder Klick auf den Regler, Klick auf ðŸ”ˆ = stumm)
- Wetter mit HÃ¶chst-/Tiefstwert (open-meteo.com)
- CPU, GPU, RAM und Festplatte als Ringe wie im iOS-Batterie-Widget (orange/rot bei hoher Last)

**Einrichtungsfenster** (beim ersten Start oder Doppelklick auf `PC-Widget.exe`, wÃ¤hrend das Widget lÃ¤uft)
- Ort suchen per Name oder Postleitzahl (DE/AT/CH)
- Mit Windows starten, Immer im Vordergrund
- Position: oben/unten, links/rechts (unten klappt es nach oben auf)

## Benutzen

1. Unter **[Releases](../../releases)** `PC-Widget.zip` herunterladen.
2. Den ganzen Ordner entpacken (nicht aus der ZIP heraus starten).
3. `PC-Widget.exe` doppelklicken, Stadt eingeben, **Speichern & Starten**.

> Windows zeigt eventuell â€žDer Computer wurde durch Windows geschÃ¼tztâ€œ, weil die App nicht signiert ist â†’ *Weitere Informationen* â†’ *Trotzdem ausfÃ¼hren*.

FÃ¼r den Musik-Teil wird die Spotify-Desktop-App gebraucht (funktioniert Ã¼ber die Windows-Mediensteuerung).

## Selbst bauen

Voraussetzung: [Rainmeter 4.5](https://www.rainmeter.net) installiert.

```powershell
.\build\build.ps1
```

Das Skript kompiliert `src/PCWidget.cs` (mit dem C#-Compiler aus dem .NET Framework), kopiert Rainmeter als portable Version dazu und legt `dist/PC-Widget.zip` an.

## Aufbau

| Pfad | Inhalt |
|---|---|
| `src/PCWidget.cs` | Starter-App + Einrichtungsfenster (WinForms) |
| `skin/PCWidget/PCWidget.ini` | Das Rainmeter-Widget |
| `skin/PCWidget/@Resources/Fx.lua` | Weicher Farbwechsel + Laufschrift |
| `skin/PCWidget/@Resources/Media/nowplaying.ps1` | Helfer: Spotify-Titel/Cover/Steuerung, Spotify-LautstÃ¤rke, GPU-Last |
| `skin/Rainmeter.ini` | Portable Rainmeter-Einstellungen |

## Lizenz & Dank

- Eigener Code: MIT (siehe `LICENSE`).
- Die Release-ZIP enthÃ¤lt [Rainmeter](https://www.rainmeter.net) (GPL v2), Quellcode: <https://github.com/rainmeter/rainmeter>.
- Wetter: [open-meteo.com](https://open-meteo.com), Postleitzahlen: [zippopotam.us](https://zippopotam.us).