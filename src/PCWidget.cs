// PC-Widget Starter: startet das mitgelieferte (portable) Rainmeter mit dem Widget.
// Erster Start oder erneuter Doppelklick, waehrend das Widget laeuft = Einstellungen.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: System.Reflection.AssemblyTitle("PC-Widget")]
[assembly: System.Reflection.AssemblyProduct("PC-Widget")]
[assembly: System.Reflection.AssemblyVersion("1.8.7.0")]

static class Program
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern bool WritePrivateProfileString(string s, string k, string v, string f);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern int GetPrivateProfileString(string s, string k, string d, StringBuilder r, int n, string f);
    [DllImport("user32.dll")] static extern bool SetProcessDPIAware();

    public static string Base = AppDomain.CurrentDomain.BaseDirectory;
    public static string RmDir = Path.Combine(Base, "Rainmeter");
    public static string RmExe = Path.Combine(RmDir, "Rainmeter.exe");
    public static string RmIni = Path.Combine(RmDir, "Rainmeter.ini");
    public static string SkinIni = Path.Combine(RmDir, @"Skins\PCWidget\PCWidget.ini");
    static string SetupFlag = Path.Combine(RmDir, "einrichtung.ok");
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string RunName = "PC-Widget";

    [STAThread]
    static void Main(string[] args)
    {
        try { SetProcessDPIAware(); } catch { }
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        string arg = args.Length > 0 ? args[0].ToLowerInvariant() : "";
        if (arg == "/fps") { Fps.RunWorker(); return; }
        if (!File.Exists(RmExe) || !File.Exists(SkinIni))
        {
            // Einzelne exe (z. B. frisch heruntergeladen): sich selbst einrichten
            if (Installer.HasPayload()) { Installer.InstallAndLaunch(); return; }
            MessageBox.Show("Der Ordner \"Rainmeter\" fehlt neben PC-Widget.exe.\nBitte den ganzen Ordner entpacken, nicht nur die .exe.", "PC-Widget", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        try { if (HasCfg(SettingsFile)) Tr.Lang = LoadCfg(SettingsFile).Lang; } catch { }
        if (arg == "/uninstall") { Installer.Uninstall(); return; }
        Installer.Unblock(Base);   // Internet-Markierung entfernen -> keine Sicherheitsabfragen bei jedem Start
        bool autostart = arg == "/autostart";
        bool running = IsRunning();
        if (autostart)
        {
            if (!running) Start();
            Updater.AutoCheck();   // hoechstens alle 6 h, nie wenn ein Spiel im Vollbild vorne ist
            return;
        }
        if (running || !File.Exists(SetupFlag)) { Application.Run(new SetupForm(running)); return; }
        Start();
    }

    public static bool IsRunning() { return FindProcess() != null; }
    static Process FindProcess()
    {
        foreach (var p in Process.GetProcessesByName("Rainmeter"))
        {
            try { if (string.Equals(Path.GetFullPath(p.MainModule.FileName), Path.GetFullPath(RmExe), StringComparison.OrdinalIgnoreCase)) return p; }
            catch { }
        }
        return null;
    }
    // ohne ShellExecute -> keine "Datei ausfuehren?"-Abfrage von Windows
    public static void Start() { Process.Start(new ProcessStartInfo(RmExe) { WorkingDirectory = RmDir, UseShellExecute = false }); }
    public static void Quit(bool wait)
    {
        var p = FindProcess();
        if (p == null) return;
        Process.Start(new ProcessStartInfo(RmExe, "!Quit") { WorkingDirectory = RmDir, UseShellExecute = false, CreateNoWindow = true });
        if (wait) { if (!p.WaitForExit(6000)) { try { p.Kill(); } catch { } } }
    }

    public static string Get(string file, string sec, string key, string def)
    {
        var sb = new StringBuilder(512); GetPrivateProfileString(sec, key, def, sb, 512, file); return sb.ToString();
    }
    public static void Set(string file, string sec, string key, string val) { WritePrivateProfileString(sec, key, val, file); }

    public static bool AutostartOn() { using (var k = Registry.CurrentUser.OpenSubKey(RunKey)) return k != null && k.GetValue(RunName) != null; }
    public static void SetAutostart(bool on)
    {
        using (var k = Registry.CurrentUser.CreateSubKey(RunKey))
        {
            if (on) k.SetValue(RunName, "\"" + Application.ExecutablePath + "\" /autostart");
            else if (k.GetValue(RunName) != null) k.DeleteValue(RunName);
        }
    }
    public static void MarkSetupDone() { File.WriteAllText(SetupFlag, DateTime.Now.ToString("s")); }

    // Wetter-Ort zusaetzlich hier merken: liegt ausserhalb des Rainmeter-Ordners und ueberlebt jedes Update
    public static string SettingsFile { get { return Path.Combine(Base, "einstellungen.ini"); } }
    public static void SaveWeather(string ort, string plz, string lat, string lon)
    {
        Set(SkinIni, "Variables", "Ort", ort); Set(SkinIni, "Variables", "PLZ", plz);
        Set(SkinIni, "Variables", "Lat", lat); Set(SkinIni, "Variables", "Lon", lon);
        Set(SettingsFile, "Wetter", "Ort", ort); Set(SettingsFile, "Wetter", "PLZ", plz);
        Set(SettingsFile, "Wetter", "Lat", lat); Set(SettingsFile, "Wetter", "Lon", lon);
    }

    public static string TabIni { get { return Path.Combine(RmDir, @"Skins\PCWidget\Tab\Tab.ini"); } }

    // Pfeil-Griff an die richtige Kante setzen (rechts/links, oben/unten wie das Widget)
    public static void WriteTabPosition(bool right, bool bottom, string rmIni = null, string tabIni = null)
    {
        rmIni = rmIni ?? RmIni; tabIni = tabIni ?? TabIni;
        const string sec = @"PCWidget\Tab";
        Set(rmIni, sec, "Active", "1");
        Set(rmIni, sec, "WindowX", right ? "0R" : "0");
        Set(rmIni, sec, "AnchorX", right ? "100%" : "0%");
        Set(rmIni, sec, "WindowY", bottom ? "53B" : "5");   // mittig neben dem kompakten Widget
        Set(rmIni, sec, "AnchorY", bottom ? "100%" : "0%");
        Set(rmIni, sec, "AlwaysOnTop", "2");
        Set(rmIni, sec, "Draggable", "0");
        Set(rmIni, sec, "KeepOnScreen", "1");
        Set(rmIni, sec, "SnapEdges", "0");
        Set(tabIni, "Variables", "Arrow", right ? "[\\xE76B]" : "[\\xE76C]");
        Set(tabIni, "Variables", "TabX", right ? "0R" : "0");
        Set(tabIni, "Variables", "TabY", bottom ? "53B" : "5");
        Set(tabIni, "Variables", "TabAX", right ? "100%" : "0%");
        Set(tabIni, "Variables", "TabAY", bottom ? "100%" : "0%");
    }

    // Seitenleiste an/aus: Widget versteckt sich, wenn ein Programm vorne ist, und laesst den Pfeil-Griff da
    public static bool SidebarOn() { return Get(SkinIni, "Variables", "HideAction", "[!HideFade]").Trim() != ""; }
    public static void SetSidebar(bool on, bool right)
    {
        Set(SkinIni, "Variables", "HideAction", on ? "[!HideFade]" : "");
        Set(SkinIni, "Variables", "ShowAction", on ? "[!ShowFade]" : "");
        Set(SkinIni, "Variables", "GrabX", right ? "0" : "264"); Set(SkinIni, "Variables", "GrabArrow", right ? "[\\xE76C]" : "[\\xE76B]");
        Set(TabIni, "Variables", "TabOnApp", on ? "[!ShowFade]" : "");
        Set(TabIni, "Variables", "TabOnDesk", on ? "[!HideFade]" : "");
    }


    // ======================= Einstellungen (zentral) =======================
    // Alle Einstellungen stehen in einstellungen.ini neben der exe und werden von dort in die Widget-Dateien geschrieben.
    // So ueberleben sie jedes Update.
    public class Cfg
    {
        public bool Top = true, Sidebar = true, Game = true, Fps = false;
        public int Corner = 1;      // 0 = oben links, 1 = oben rechts, 2 = unten links, 3 = unten rechts
        public int Monitor = 0;     // 0 = Hauptbildschirm, sonst Nummer (1, 2, 3 ...)
        public string Lang = "de";
    }

    static string Rd(string file, string sec, string key)
    {
        if (!File.Exists(file)) return null;
        var sb = new StringBuilder(1024); GetPrivateProfileString(sec, key, "\u0001", sb, 1024, file);
        return sb.ToString() == "\u0001" ? null : sb.ToString();
    }

    public static bool HasCfg(string sf) { return Rd(sf, "App", "Ecke") != null; }

    public static Cfg LoadCfg(string sf)
    {
        var c = new Cfg();
        Func<string, string, string> g = (k, d) => Rd(sf, "App", k) ?? d;
        c.Top = g("Vordergrund", "1") == "1"; c.Sidebar = g("Seitenleiste", "1") == "1"; c.Game = g("Spielmodus", "1") == "1"; c.Fps = g("FPS", "0") == "1";
        int.TryParse(g("Ecke", "1"), out c.Corner); int.TryParse(g("Monitor", "0"), out c.Monitor);
        c.Lang = g("Sprache", "de") == "en" ? "en" : "de";
        return c;
    }

    // Aus den Dateien einer aelteren Version ablesen (die noch keine [App]-Einstellungen hatten)
    public static Cfg CfgFromFiles(string rmIni, string skin)
    {
        var c = new Cfg();
        bool right = Rd(rmIni, "PCWidget", "AnchorX") != "0%", bottom = Rd(rmIni, "PCWidget", "AnchorY") == "100%";
        c.Corner = (bottom ? 2 : 0) + (right ? 1 : 0);
        c.Top = (Rd(rmIni, "PCWidget", "AlwaysOnTop") ?? "2") == "2";
        var ha = Rd(skin, "Variables", "HideAction"); c.Sidebar = ha == null || ha.Contains("HideFade");
        var m = System.Text.RegularExpressions.Regex.Match(Rd(rmIni, "PCWidget", "WindowX") ?? "", @"@(\d+)");
        if (m.Success) int.TryParse(m.Groups[1].Value, out c.Monitor);
        return c;
    }

    public static void SaveCfg(Cfg c, string sf)
    {
        Set(sf, "App", "Vordergrund", c.Top ? "1" : "0"); Set(sf, "App", "Seitenleiste", c.Sidebar ? "1" : "0");
        Set(sf, "App", "Spielmodus", c.Game ? "1" : "0"); Set(sf, "App", "FPS", c.Fps ? "1" : "0"); Set(sf, "App", "Ecke", c.Corner.ToString());
        Set(sf, "App", "Monitor", c.Monitor.ToString()); Set(sf, "App", "Sprache", c.Lang);
    }

    // Schreibt alle Einstellungen in die Rainmeter-Dateien eines Ordners (Rainmeter darf dabei nicht laufen)
    public static void ApplyCfg(Cfg c, string rmDir)
    {
        string rmIni = Path.Combine(rmDir, "Rainmeter.ini");
        string skin = Path.Combine(rmDir, @"Skins\PCWidget\PCWidget.ini");
        string tab = Path.Combine(rmDir, @"Skins\PCWidget\Tab\Tab.ini");
        string mini = Path.Combine(rmDir, @"Skins\PCWidget\Mini\Mini.ini");
        bool right = c.Corner % 2 == 1, bottom = c.Corner >= 2;
        string mon = c.Monitor > 0 ? "@" + c.Monitor : "";
        string wx = (right ? "20R" : "20") + mon, ax = right ? "100%" : "0%";
        string wy = (bottom ? "68B" : "20") + mon, ay = bottom ? "100%" : "0%";

        // Widget
        Set(rmIni, "PCWidget", "Active", "1");
        Set(rmIni, "PCWidget", "WindowX", wx); Set(rmIni, "PCWidget", "AnchorX", ax);
        Set(rmIni, "PCWidget", "WindowY", wy); Set(rmIni, "PCWidget", "AnchorY", ay);
        Set(rmIni, "PCWidget", "AlwaysOnTop", c.Top ? "2" : "-2");
        Set(rmIni, "PCWidget", "Draggable", "0");
        Set(skin, "Variables", "PosBang", bottom ? "[!SetWindowPosition " + wx + " " + wy + " " + ax + " " + ay + "]" : "");
        string hide = c.Sidebar ? "[!HideFade]" : "[!ShowFade]";
        Set(skin, "Variables", "HideAction", hide);
        Set(skin, "Variables", "ShowAction", "[!ShowFade]");
        Set(skin, "Variables", "GameAction", c.Game ? "[!HideFade]" : "[!ShowFade]");
        Set(skin, "Variables", "AutoSide", c.Sidebar ? "1" : "0");
        Set(skin, "Variables", "Docked", "0");
        Set(skin, "Variables", "GrabX", right ? "0" : "264");
        Set(skin, "Variables", "GrabArrow", right ? "[\\xE76C]" : "[\\xE76B]");

        // Pfeil-Griff
        string tx = (right ? "0R" : "0") + mon, ty = (bottom ? "53B" : "5") + mon;
        Set(rmIni, @"PCWidget\Tab", "Active", "1");
        Set(rmIni, @"PCWidget\Tab", "WindowX", tx); Set(rmIni, @"PCWidget\Tab", "AnchorX", ax);
        Set(rmIni, @"PCWidget\Tab", "WindowY", ty); Set(rmIni, @"PCWidget\Tab", "AnchorY", ay);
        Set(rmIni, @"PCWidget\Tab", "AlwaysOnTop", "2"); Set(rmIni, @"PCWidget\Tab", "Draggable", "0");
        Set(rmIni, @"PCWidget\Tab", "KeepOnScreen", "1"); Set(rmIni, @"PCWidget\Tab", "SnapEdges", "0");
        Set(tab, "Variables", "Arrow", right ? "[\\xE76B]" : "[\\xE76C]");
        Set(tab, "Variables", "TabX", tx); Set(tab, "Variables", "TabY", ty);
        Set(tab, "Variables", "TabAX", ax); Set(tab, "Variables", "TabAY", ay);
        Set(tab, "Variables", "TabOnApp", c.Sidebar ? "[!ShowFade]" : "[!HideFade]");
        Set(tab, "Variables", "TabOnDesk", "[!HideFade]");   // Griff verschwindet immer, wenn kein Programm vorne ist

        // Spiel-Modus-Leiste (gleiche Ecke wie das Widget)
        Set(rmIni, @"PCWidget\Mini", "Active", "1");
        Set(rmIni, @"PCWidget\Mini", "WindowX", wx); Set(rmIni, @"PCWidget\Mini", "AnchorX", ax);
        Set(rmIni, @"PCWidget\Mini", "WindowY", wy); Set(rmIni, @"PCWidget\Mini", "AnchorY", ay);
        Set(rmIni, @"PCWidget\Mini", "AlwaysOnTop", "2"); Set(rmIni, @"PCWidget\Mini", "Draggable", "0");
        Set(rmIni, @"PCWidget\Mini", "KeepOnScreen", "1"); Set(rmIni, @"PCWidget\Mini", "ClickThrough", "1");
        Set(mini, "Variables", "MiniX", wx); Set(mini, "Variables", "MiniY", wy);
        Set(mini, "Variables", "MiniAX", ax); Set(mini, "Variables", "MiniAY", ay);
        Set(mini, "Variables", "MiniOnGame", c.Game ? "[!ShowFade]" : "");
        Set(mini, "Variables", "MiniOff", "[!HideFade]");

        // Sprache
        bool en = c.Lang == "en";
        Set(skin, "Variables", "Locale", en ? "en-US" : "de-DE");
        Set(skin, "Variables", "DateFmt", en ? "%B %#d" : "%#d. %B");
        Set(skin, "Variables", "TxtNothing", en ? "Nothing playing" : "Nichts läuft");
        Set(skin, "Variables", "TxtMusic", en ? "Music" : "Musik");
        Set(skin, "Variables", "TxtHT", en ? "H: %1°  L: %2°" : "H: %1°  T: %2°");
        string[] wde = { "Klar", "Leicht bewölkt", "Bewölkt", "Nebel", "Nieselregen", "Regen", "Schnee", "Gewitter" };
        string[] wen = { "Clear", "Partly cloudy", "Cloudy", "Fog", "Drizzle", "Rain", "Snow", "Thunderstorm" };
        for (int i = 0; i < 8; i++) Set(skin, "Variables", "W" + i, en ? wen[i] : wde[i]);
    }

    // Monitore in der Reihenfolge, wie Rainmeter sie zaehlt (@1, @2, ...)
    public static Screen[] Monitors() { return Screen.AllScreens; }

    public class Place { public string Name, Detail, Lat, Lon, Plz = ""; }

    // Orte suchen ueber open-meteo.com (Name oder PLZ, kostenlos, kein Konto)
    public static List<Place> Search(string q)
    {
        var list = new List<Place>();
        ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
        // Postleitzahl (DE 5-stellig, AT/CH 4-stellig) ueber zippopotam.us
        if (System.Text.RegularExpressions.Regex.IsMatch(q, @"^\d{4,5}$"))
        {
            foreach (var cc in q.Length == 5 ? new[] { "de" } : new[] { "at", "ch" })
            {
                try
                {
                    using (var wc = new WebClient())
                    {
                        wc.Encoding = Encoding.UTF8;
                        var root = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(wc.DownloadString("https://api.zippopotam.us/" + cc + "/" + q));
                        string country = Convert.ToString(root["country"]);
                        foreach (Dictionary<string, object> p in (System.Collections.ArrayList)root["places"])
                            list.Add(new Place { Name = Convert.ToString(p["place name"]), Detail = q + "  ·  " + Convert.ToString(p["state"]) + ", " + country, Lat = Convert.ToString(p["latitude"], System.Globalization.CultureInfo.InvariantCulture), Lon = Convert.ToString(p["longitude"], System.Globalization.CultureInfo.InvariantCulture), Plz = q });
                    }
                }
                catch (WebException) { } // 404 = PLZ unbekannt
            }
            return list;
        }
        using (var wc = new WebClient())
        {
            wc.Encoding = Encoding.UTF8;
            string json = wc.DownloadString("https://geocoding-api.open-meteo.com/v1/search?count=6&language=de&format=json&name=" + Uri.EscapeDataString(q));
            var root = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(json);
            object res;
            if (root == null || !root.TryGetValue("results", out res) || res == null) return list;
            foreach (Dictionary<string, object> r in (System.Collections.ArrayList)res)
            {
                Func<string, string> s = k => r.ContainsKey(k) && r[k] != null ? Convert.ToString(r[k], System.Globalization.CultureInfo.InvariantCulture) : "";
                var det = new List<string>();
                if (s("admin1") != "") det.Add(s("admin1"));
                if (s("country") != "") det.Add(s("country"));
                string plz = "";
                if (r.ContainsKey("postcodes") && r["postcodes"] is System.Collections.ArrayList && ((System.Collections.ArrayList)r["postcodes"]).Count > 0) plz = Convert.ToString(((System.Collections.ArrayList)r["postcodes"])[0]);
                list.Add(new Place { Name = s("name"), Detail = string.Join(", ", det), Lat = s("latitude"), Lon = s("longitude"), Plz = plz });
            }
        }
        return list;
    }
}

// ======================= Selbst-Einrichtung =======================
static class Installer
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern bool DeleteFile(string path);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern bool WritePrivateProfileString(string s, string k, string v, string f);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern int GetPrivateProfileString(string s, string k, string d, StringBuilder r, int n, string f);

    public const string Version = "1.8.7";
    const string PayloadName = "payload.zip";
    const string UninstKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\PC-Widget";
    public static string Dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PC-Widget");
    static string StartMenuLnk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "PC-Widget.lnk");

    public static bool HasPayload()
    {
        using (var s = typeof(Installer).Assembly.GetManifestResourceStream(PayloadName)) return s != null;
    }

    // Entfernt die "aus dem Internet"-Markierung (Zone.Identifier) von allen Dateien
    public static void Unblock(string dir)
    {
        try { foreach (var f in Directory.GetFiles(dir, "*", SearchOption.AllDirectories)) DeleteFile(f + ":Zone.Identifier"); } catch { }
    }

    static string Read(string file, string sec, string key)
    {
        if (!File.Exists(file)) return null;
        var sb = new StringBuilder(1024); GetPrivateProfileString(sec, key, "\u0001", sb, 1024, file);
        return sb.ToString() == "\u0001" ? null : sb.ToString();
    }

    public static void InstallAndLaunch()
    {
        string exe = Path.Combine(Dir, "PC-Widget.exe");
        string rm = Path.Combine(Dir, "Rainmeter");
        string ver = Path.Combine(Dir, "version.txt");
        bool same = File.Exists(exe) && File.Exists(Path.Combine(rm, "Rainmeter.exe")) && File.Exists(ver) && File.ReadAllText(ver).Trim() == Version;
        if (!same)
        {
            Cursor.Current = Cursors.WaitCursor;
            try
            {
                // laufendes Widget dieser Installation beenden
                foreach (var p in Process.GetProcessesByName("Rainmeter"))
                {
                    try
                    {
                        if (p.MainModule.FileName.StartsWith(Dir, StringComparison.OrdinalIgnoreCase))
                        {
                            Process.Start(new ProcessStartInfo(p.MainModule.FileName, "!Quit") { UseShellExecute = false, CreateNoWindow = true });
                            if (!p.WaitForExit(6000)) p.Kill();
                        }
                    }
                    catch { }
                }
                Fps.StopWorker(Dir);   // FPS-Messung anhalten, sonst sind Dateien gesperrt
                // Einstellungen einer alten Version merken
                string rmIni = Path.Combine(rm, "Rainmeter.ini"), skin = Path.Combine(rm, @"Skins\PCWidget\PCWidget.ini"), flag = Path.Combine(rm, "einrichtung.ok");
                byte[] oldRmIni = File.Exists(rmIni) ? File.ReadAllBytes(rmIni) : null;
                var keep = new Dictionary<string, string>();
                foreach (var k in new[] { "Lat", "Lon", "Ort", "PLZ", "PosBang", "HideAction", "ShowAction", "GrabX" }) { var v = Read(skin, "Variables", k); if (v != null) keep[k] = v; }
                bool wasSetup = File.Exists(flag);

                string tabIni = Path.Combine(rm, @"Skins\PCWidget\Tab\Tab.ini");
                var keepTab = new Dictionary<string, string>();
                foreach (var k in new[] { "TabOnApp", "TabOnDesk" }) { var v = Read(tabIni, "Variables", k); if (v != null) keepTab[k] = v; }

                for (int i = 0; Directory.Exists(rm); i++)
                {
                    try { Directory.Delete(rm, true); }
                    catch { if (i > 20) throw; Thread.Sleep(300); }
                }
                Directory.CreateDirectory(Dir);
                using (var s = typeof(Installer).Assembly.GetManifestResourceStream(PayloadName))
                using (var zip = new System.IO.Compression.ZipArchive(s))
                {
                    foreach (var e in zip.Entries)
                    {
                        if (string.IsNullOrEmpty(e.Name)) continue;
                        string target = Path.GetFullPath(Path.Combine(Dir, e.FullName.Replace('/', '\\')));
                        if (!target.StartsWith(Dir, StringComparison.OrdinalIgnoreCase)) continue;
                        Directory.CreateDirectory(Path.GetDirectoryName(target));
                        using (var src = e.Open()) using (var dst = File.Create(target)) src.CopyTo(dst);
                    }
                }
                if (oldRmIni != null) File.WriteAllBytes(rmIni, oldRmIni);
                foreach (var kv in keep) WritePrivateProfileString("Variables", kv.Key, kv.Value, skin);
                foreach (var kv in keepTab) WritePrivateProfileString("Variables", kv.Key, kv.Value, tabIni);
                // Alle Einstellungen (Ecke, Monitor, Sprache, Seitenleiste, Spiel-Modus) neu anwenden
                {
                    string sf0 = Path.Combine(Dir, "einstellungen.ini");
                    var c = Program.HasCfg(sf0) ? Program.LoadCfg(sf0) : Program.CfgFromFiles(rmIni, skin);
                    Program.ApplyCfg(c, rm);
                    Program.SaveCfg(c, sf0);
                }
                if (wasSetup) File.WriteAllText(flag, DateTime.Now.ToString("s"));
                // Wetter-Ort aus der eigenen Einstellungsdatei wiederherstellen (hat Vorrang)
                string sf = Path.Combine(Dir, "einstellungen.ini");
                foreach (var k in new[] { "Ort", "PLZ", "Lat", "Lon" }) { var v = Read(sf, "Wetter", k); if (v != null) WritePrivateProfileString("Variables", k, v, skin); }
                // aeltere Versionen hatten noch keine Einstellungsdatei -> jetzt anlegen
                if (Read(sf, "Wetter", "Ort") == null && keep.ContainsKey("Ort"))
                    foreach (var k in new[] { "Ort", "PLZ", "Lat", "Lon" }) if (keep.ContainsKey(k)) WritePrivateProfileString("Wetter", k, keep[k], sf);

                // andere laufende PC-Widget.exe dieser Installation (z. B. Autostart, der gerade nach Updates schaut) beenden,
                // sonst ist die Datei gesperrt
                int me = Process.GetCurrentProcess().Id;
                foreach (var p in Process.GetProcessesByName("PC-Widget"))
                {
                    try { if (p.Id != me && p.MainModule.FileName.Equals(exe, StringComparison.OrdinalIgnoreCase)) { p.Kill(); p.WaitForExit(3000); } } catch { }
                }
                Fps.StopWorker(Dir);
                // alte exe kann sich nach einem Update noch beenden -> kurz warten
                for (int i = 0; ; i++)
                {
                    try { File.Copy(Application.ExecutablePath, exe, true); break; }
                    catch (IOException) { if (i > 30) throw; Thread.Sleep(300); }
                }
                File.WriteAllText(ver, Version);
                // FPS-Messung erst jetzt wieder starten (sonst sperrt sie die exe)
                try { if (Program.HasCfg(Path.Combine(Dir, "einstellungen.ini")) && Program.LoadCfg(Path.Combine(Dir, "einstellungen.ini")).Fps && Fps.TaskExists()) Fps.StartTask(Dir); } catch { }
                Unblock(Dir);
                CreateShortcut(StartMenuLnk, exe);
                Register(exe);
            }
            catch (Exception ex)
            {
                MessageBox.Show("PC-Widget konnte nicht eingerichtet werden:\n" + ex.Message, "PC-Widget", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
        }
        Process.Start(new ProcessStartInfo(exe) { WorkingDirectory = Dir, UseShellExecute = false });
    }

    static void CreateShortcut(string lnk, string target)
    {
        try
        {
            var t = Type.GetTypeFromProgID("WScript.Shell");
            object sh = Activator.CreateInstance(t);
            object sc = t.InvokeMember("CreateShortcut", System.Reflection.BindingFlags.InvokeMethod, null, sh, new object[] { lnk });
            var st = sc.GetType();
            st.InvokeMember("TargetPath", System.Reflection.BindingFlags.SetProperty, null, sc, new object[] { target });
            st.InvokeMember("WorkingDirectory", System.Reflection.BindingFlags.SetProperty, null, sc, new object[] { Path.GetDirectoryName(target) });
            st.InvokeMember("IconLocation", System.Reflection.BindingFlags.SetProperty, null, sc, new object[] { target + ",0" });
            st.InvokeMember("Description", System.Reflection.BindingFlags.SetProperty, null, sc, new object[] { "PC-Widget – Einstellungen" });
            st.InvokeMember("Save", System.Reflection.BindingFlags.InvokeMethod, null, sc, null);
        }
        catch { }
    }

    // Eintrag unter Einstellungen > Apps, damit man es normal deinstallieren kann
    static void Register(string exe)
    {
        using (var k = Registry.CurrentUser.CreateSubKey(UninstKey))
        {
            k.SetValue("DisplayName", "PC-Widget");
            k.SetValue("DisplayVersion", Version);
            k.SetValue("Publisher", "PC-Widget");
            k.SetValue("DisplayIcon", exe + ",0");
            k.SetValue("InstallLocation", Dir);
            k.SetValue("UninstallString", "\"" + exe + "\" /uninstall");
            k.SetValue("NoModify", 1, RegistryValueKind.DWord);
            k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            long kb = 0; foreach (var f in Directory.GetFiles(Dir, "*", SearchOption.AllDirectories)) kb += new FileInfo(f).Length / 1024;
            k.SetValue("EstimatedSize", (int)kb, RegistryValueKind.DWord);
        }
    }

    public static void Uninstall()
    {
        if (MessageBox.Show("PC-Widget wirklich entfernen?", "PC-Widget", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        Program.Quit(true);
        Fps.Remove();
        Program.SetAutostart(false);
        try { if (File.Exists(StartMenuLnk)) File.Delete(StartMenuLnk); } catch { }
        try { Registry.CurrentUser.DeleteSubKeyTree(UninstKey, false); } catch { }
        string dir = Program.Base.TrimEnd('\\');
        // Ordner erst loeschen, wenn diese exe beendet ist
        if (dir.Equals(Dir, StringComparison.OrdinalIgnoreCase))
            Process.Start(new ProcessStartInfo("cmd.exe", "/c timeout /t 4 /nobreak >nul & rmdir /s /q \"" + dir + "\"") { CreateNoWindow = true, UseShellExecute = false, WindowStyle = ProcessWindowStyle.Hidden });
        MessageBox.Show("PC-Widget wurde entfernt.", "PC-Widget", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
}


// ======================= FPS-Messung (PresentMon, braucht Admin) =======================
// Laeuft als "PC-Widget.exe /fps" ueber eine geplante Aufgabe mit hoechsten Rechten.
// Zaehlt die Bilder pro Sekunde des Programms im Vordergrund und schreibt sie in Media\fps.txt.
static class Fps
{
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint p);

    public const string TaskName = "PC-Widget FPS";
    public static string Dir { get { return Program.Base.TrimEnd('\\'); } }
    public static string PmExe { get { return Path.Combine(Dir, @"Tools\PresentMon.exe"); } }
    public static string RunFlag { get { return Path.Combine(Dir, "fps.running"); } }
    public static string StopFlag { get { return Path.Combine(Dir, "fps.stop"); } }
    static string OutFile { get { return Path.Combine(Program.RmDir, @"Skins\PCWidget\@Resources\Media\fps.txt"); } }

    static readonly object sync = new object();
    static readonly Dictionary<int, Queue<long>> frames = new Dictionary<int, Queue<long>>();
    static int colPid = -1;
    static Stopwatch clock = Stopwatch.StartNew();

    // Pro Zeichenflaeche (Prozess + SwapChain) die Zeiten zwischen den Bildern sammeln (PresentMons eigene Messung,
    // daher egal, ob die Zeilen in Schueben ankommen). FPS = Bilder / Summe der Bildzeiten der letzten ~1 Sekunde.
    class Chain { public int Pid; public string App; public readonly Queue<double> Ms = new Queue<double>(); public double Sum; public long Seen; }
    static readonly Dictionary<string, Chain> chains = new Dictionary<string, Chain>();
    static int colApp = -1, colSwap = -1, colMs = -1, firstLogged = 0; static long linesTotal = 0;

    // Spaltensuche ohne Gross-/Kleinschreibung (PresentMon schreibt z. B. "msBetweenPresents")
    static int Col(string[] parts, string name) { for (int i = 0; i < parts.Length; i++) if (string.Equals(parts[i].Trim(), name, StringComparison.OrdinalIgnoreCase)) return i; return -1; }

    static void OnLine(string line)
    {
        if (string.IsNullOrEmpty(line)) return;
        var parts = line.Split(',');
        if (colPid < 0)
        {
            colPid = Col(parts, "ProcessID"); colApp = Col(parts, "Application");
            colSwap = Col(parts, "SwapChainAddress");
            foreach (var cand in new[] { "MsBetweenPresents", "FrameTime", "MsBetweenAppStart", "MsBetweenDisplayChange", "MsBetweenSimulationStart" })
                if (colMs < 0) colMs = Col(parts, cand);
            Log("Spalten: " + line + "  -> Bildzeit-Spalte " + (colMs >= 0 ? parts[colMs] : "nicht gefunden"));
            return;   // Kopfzeile
        }
        int pid; if (colPid >= parts.Length || !int.TryParse(parts[colPid], out pid)) return;
        if (linesTotal == 0 && firstLogged++ == 0) Log("Erste Zeile: " + line);
        double ms;
        if (colMs < 0 || colMs >= parts.Length || !double.TryParse(parts[colMs], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out ms) || ms <= 0 || ms > 1000) return;
        string swap = colSwap >= 0 && colSwap < parts.Length ? parts[colSwap] : "";
        string app = colApp >= 0 && colApp < parts.Length ? parts[colApp] : "";
        string key = pid + "|" + swap;
        lock (sync)
        {
            linesTotal++;
            Chain c; if (!chains.TryGetValue(key, out c)) { c = new Chain { Pid = pid, App = app }; chains[key] = c; }
            c.Ms.Enqueue(ms); c.Sum += ms; c.Seen = clock.ElapsedMilliseconds;
            while (c.Ms.Count > 1 && c.Sum - c.Ms.Peek() >= 1000) c.Sum -= c.Ms.Dequeue();
        }
    }

    // FPS des Vordergrund-Programms: schnellste aktive Zeichenflaeche dieses Prozesses bzw. (Browser) gleichen Programmnamens
    static int FpsOf(int pid)
    {
        long now = clock.ElapsedMilliseconds;
        string name = null; try { name = Process.GetProcessById(pid).ProcessName + ".exe"; } catch { }
        double best = 0;
        lock (sync)
        {
            var dead = new List<string>();
            foreach (var kv in chains)
            {
                var c = kv.Value;
                if (now - c.Seen > 5000) { dead.Add(kv.Key); continue; }
                if (now - c.Seen > 1500 || c.Ms.Count < 2 || c.Sum <= 0) continue;   // gerade keine Bilder (Pause, minimiert)
                if (c.Pid != pid && !(name != null && string.Equals(c.App, name, StringComparison.OrdinalIgnoreCase))) continue;
                best = Math.Max(best, c.Ms.Count * 1000.0 / c.Sum);
            }
            foreach (var k in dead) chains.Remove(k);
        }
        return (int)Math.Round(best);
    }
    static bool WidgetRunning()
    {
        foreach (var p in Process.GetProcessesByName("Rainmeter"))
            try { if (p.MainModule.FileName.StartsWith(Dir, StringComparison.OrdinalIgnoreCase)) return true; } catch { }
        return false;
    }

    static bool Enabled() { return !Program.HasCfg(Program.SettingsFile) || Program.LoadCfg(Program.SettingsFile).Fps; }

    static string LogFile { get { return Path.Combine(Dir, "fps.log"); } }
    static void Log(string s) { try { File.AppendAllText(LogFile, DateTime.Now.ToString("HH:mm:ss") + "  " + s + Environment.NewLine); } catch { } }
    static readonly Queue<string> errLines = new Queue<string>();

    // Haengengebliebene Mess-Sitzung von einem frueheren Lauf beenden (sonst kann PresentMon nicht neu starten)
    // eigener Sitzungsname pro Lauf: eine haengengebliebene alte Sitzung kann so nicht mehr blockieren
    static readonly string Session = "PCWidgetFPS_" + Process.GetCurrentProcess().Id;

    static void StopSession()
    {
        try
        {
            var si = new ProcessStartInfo("logman.exe", "stop " + Session + " -ets") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            using (var p = Process.Start(si)) { p.WaitForExit(5000); }
        }
        catch { }
    }

    public static void RunWorker()
    {
        bool created; var mutex = new Mutex(true, "Local\\PCWidgetFpsWorker", out created);
        if (!created) return;
        try { File.WriteAllText(LogFile, ""); } catch { }
        Log("FPS-Messung gestartet (Version " + Installer.Version + ")");
        try { File.WriteAllText(RunFlag, Process.GetCurrentProcess().Id.ToString()); } catch { }
        Process pm = null; int fails = 0; bool blocked = false; DateTime nextTry = DateTime.MinValue, lastStat = DateTime.Now;
        try
        {
            while (!File.Exists(StopFlag))
            {
                bool want = File.Exists(PmExe) && WidgetRunning() && Enabled();
                if (pm != null && pm.HasExited)
                {
                    string err; lock (errLines) err = string.Join(" / ", errLines.ToArray());
                    Log("PresentMon beendet, Code " + pm.ExitCode + (err != "" ? ": " + err : ""));
                    blocked = err.IndexOf("access denied", StringComparison.OrdinalIgnoreCase) >= 0;
                    pm = null; fails++;
                    nextTry = DateTime.Now.AddSeconds(Math.Min(60, 2 * fails));   // nicht im Sekundentakt neu starten
                }
                if (want && pm == null && DateTime.Now >= nextTry)
                {
                    colPid = -1; colMs = -1; colApp = -1; colSwap = -1; firstLogged = 0; lock (errLines) errLines.Clear();
                    StopSession();
                    var si = new ProcessStartInfo(PmExe, "--output_stdout --no_console_stats --v1_metrics --no_track_display --no_track_input --no_track_gpu --session_name " + Session + " --stop_existing_session")
                    { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, WindowStyle = ProcessWindowStyle.Hidden, WorkingDirectory = Path.GetTempPath() };
                    pm = new Process { StartInfo = si };
                    pm.OutputDataReceived += (s, e) => OnLine(e.Data);
                    pm.ErrorDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) lock (errLines) { errLines.Enqueue(e.Data.Trim()); while (errLines.Count > 6) errLines.Dequeue(); } };
                    try { pm.Start(); pm.BeginOutputReadLine(); pm.BeginErrorReadLine(); blocked = false; Log("PresentMon gestartet (PID " + pm.Id + ")"); }
                    catch (Exception ex) { Log("PresentMon konnte nicht starten: " + ex.Message); pm = null; fails++; nextTry = DateTime.Now.AddSeconds(Math.Min(60, 2 * fails)); }
                }
                if (!want && pm != null) { try { if (!pm.HasExited) pm.Kill(); } catch { } pm = null; StopSession(); try { File.Delete(OutFile); } catch { } }
                if (want)
                {
                    uint fg; GetWindowThreadProcessId(GetForegroundWindow(), out fg);
                    try { File.WriteAllText(OutFile, pm != null ? FpsOf((int)fg).ToString() : (blocked ? "AC" : "")); } catch { }
                    if (pm != null && colPid >= 0) fails = 0;
                }
                if (want && (DateTime.Now - lastStat).TotalSeconds >= 60) { lastStat = DateTime.Now; long lt; lock (sync) lt = linesTotal; Log("Bilder empfangen bisher: " + lt); }
                Thread.Sleep(want ? 250 : 5000);   // 4x pro Sekunde aktualisieren
            }
        }
        finally
        {
            try { if (pm != null && !pm.HasExited) pm.Kill(); } catch { }
            StopSession();
            try { File.Delete(OutFile); } catch { }
            try { File.Delete(RunFlag); } catch { }
            Log("FPS-Messung beendet");
            mutex.ReleaseMutex();
        }
    }
    static int Schtasks(string args, bool elevated)
    {
        var si = new ProcessStartInfo("schtasks.exe", args) { CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
        if (elevated) { si.UseShellExecute = true; si.Verb = "runas"; } else si.UseShellExecute = false;
        using (var p = Process.Start(si)) { p.WaitForExit(20000); return p.HasExited ? p.ExitCode : -1; }
    }

    public static bool TaskExists() { try { return Schtasks("/query /tn \"" + TaskName + "\"", false) == 0; } catch { return false; } }

    // Legt die Aufgabe an (eine Admin-Abfrage) und startet die Messung. false = abgelehnt/fehlgeschlagen.
    public static bool Install(string exe)
    {
        try
        {
            string tr = "\\\"" + exe + "\\\" /fps";
            int rc = Schtasks("/create /tn \"" + TaskName + "\" /tr \"" + tr + "\" /sc onlogon /rl highest /f", true);
            if (rc != 0) return false;
            StartTask(); return true;
        }
        catch { return false; }   // z. B. Admin-Abfrage abgelehnt
    }

    public static void StartTask(string dir = null) { try { File.Delete(Path.Combine(dir ?? Dir, "fps.stop")); } catch { } try { Schtasks("/run /tn \"" + TaskName + "\"", false); } catch { } }

    // Messung anhalten (z. B. vor einem Update) und warten, bis sie beendet ist
    public static void StopWorker(string dir)
    {
        string run = Path.Combine(dir, "fps.running"), stop = Path.Combine(dir, "fps.stop");
        if (!File.Exists(run)) return;
        try { File.WriteAllText(stop, "1"); } catch { return; }
        for (int i = 0; i < 40 && File.Exists(run); i++) Thread.Sleep(200);
    }

    public static void Remove() { try { StopWorker(Dir); if (TaskExists()) Schtasks("/delete /tn \"" + TaskName + "\" /f", true); } catch { } }
}

// ======================= Updates von GitHub =======================
static class Updater
{
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr h, StringBuilder s, int n);
    struct RECT { public int L, T, R, B; }

    const string Repo = "tn082k6/pc-widget";
    const string Api = "https://api.github.com/repos/" + Repo + "/releases/latest";
    const string AllowedPrefix = "https://github.com/" + Repo + "/releases/download/";
    static string StampFile { get { return Path.Combine(Program.Base, "update.check"); } }

    public class Info { public string Version, Url, Notes; }

    static Version V(string s)
    {
        s = (s ?? "").Trim().TrimStart('v', 'V');
        if (!s.Contains(".")) s += ".0";
        Version v; return System.Version.TryParse(s, out v) ? v : new Version(0, 0);
    }

    // null = kein Update. Wirft bei Netzwerkfehlern.
    public static Info Check()
    {
        ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
        using (var wc = new WebClient())
        {
            wc.Headers["User-Agent"] = "PC-Widget/" + Installer.Version;
            wc.Encoding = Encoding.UTF8;
            var root = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(wc.DownloadString(Api));
            string tag = Convert.ToString(root["tag_name"]);
            if (V(tag) <= V(Installer.Version)) return null;
            string url = null;
            foreach (Dictionary<string, object> a in (System.Collections.ArrayList)root["assets"])
                if (string.Equals(Convert.ToString(a["name"]), "PC-Widget.exe", StringComparison.OrdinalIgnoreCase)) url = Convert.ToString(a["browser_download_url"]);
            if (url == null || !url.StartsWith(AllowedPrefix, StringComparison.OrdinalIgnoreCase)) return null;
            string body = root.ContainsKey("body") ? Convert.ToString(root["body"]) : "";
            body = body.Replace("**", "").Replace("`", "");
            body = System.Text.RegularExpressions.Regex.Replace(body, @"(?m)^\s*[-*]\s+", "•  ");
            return new Info { Version = tag.TrimStart('v', 'V'), Url = url, Notes = body.Trim() };
        }
    }

    // Laeuft ein Spiel/Programm im Vollbild vorne?
    public static bool FullscreenInFront()
    {
        try
        {
            IntPtr h = GetForegroundWindow(); if (h == IntPtr.Zero) return false;
            var sb = new StringBuilder(256); GetClassName(h, sb, 256);
            string c = sb.ToString(); if (c == "Progman" || c == "WorkerW" || c == "Shell_TrayWnd") return false;
            RECT r; GetWindowRect(h, out r);
            var s = Screen.FromHandle(h).Bounds;
            return r.L <= s.Left && r.T <= s.Top && r.R >= s.Right && r.B >= s.Bottom;
        }
        catch { return false; }
    }

    public static void AutoCheck()
    {
        try
        {
            if (File.Exists(StampFile) && (DateTime.Now - File.GetLastWriteTime(StampFile)).TotalHours < 6) return;
            Thread.Sleep(15000);   // nach dem Hochfahren etwas warten, bis das Internet da ist
            Info info = Check();
            File.WriteAllText(StampFile, DateTime.Now.ToString("s"));
            if (info == null || FullscreenInFront()) return;
            Application.Run(new UpdateForm(info));
        }
        catch { }
    }

    // Neue exe laden und starten; sie aktualisiert die Installation und behaelt die Einstellungen
    public static void Apply(Info info)
    {
        string dir = Path.Combine(Path.GetTempPath(), "PC-Widget-Update");
        Directory.CreateDirectory(dir);
        string file = Path.Combine(dir, "PC-Widget.exe");
        ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
        using (var wc = new WebClient()) { wc.Headers["User-Agent"] = "PC-Widget/" + Installer.Version; wc.DownloadFile(info.Url, file); }
        var head = new byte[2];
        using (var fs = File.OpenRead(file)) { if (fs.Length < 1000000 || fs.Read(head, 0, 2) != 2 || head[0] != 'M' || head[1] != 'Z') throw new Exception("Download unvollständig"); }
        Process.Start(new ProcessStartInfo(file, "/update") { UseShellExecute = false, WorkingDirectory = dir });
        Environment.Exit(0);
    }
}

class UpdateForm : Form
{
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int a, ref int v, int s);
    Updater.Info info; Label status; PillButton go, later;

    public UpdateForm(Updater.Info info)
    {
        this.info = info;
        using (var g = CreateGraphics()) UI.Scale = g.DpiX / 96f;
        try { using (var f = new Font(UI.FontName, 10)) if (f.Name != UI.FontName) UI.FontName = "Segoe UI"; } catch { UI.FontName = "Segoe UI"; }
        Text = "PC-Widget Update";
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
        FormBorderStyle = FormBorderStyle.None; StartPosition = FormStartPosition.Manual; TopMost = true;
        BackColor = UI.Bg; ForeColor = UI.Text; Font = UI.F(10); DoubleBuffered = true;
        int W = UI.S(400), L = UI.S(22), CW = W - 2 * L;
        string notes = info.Notes ?? "";
        var lines = notes.Split('\n'); if (lines.Length > 9) notes = string.Join("\n", lines, 0, 9).TrimEnd() + "\n…";
        var title = new Label { Text = Tr.T("Update verfügbar", "Update available"), Font = UI.F(15, FontStyle.Bold), AutoSize = true, Location = new Point(L, UI.S(18)), ForeColor = UI.Text };
        var sub = new Label { Text = "PC-Widget " + info.Version + Tr.T(" ist da – du hast ", " is here – you have ") + Installer.Version + ".", Font = UI.F(9.5f), AutoSize = true, Location = new Point(L + 1, UI.S(50)), ForeColor = UI.Sub };
        var body = new Label { Text = notes, Font = UI.F(9), AutoSize = false, Location = new Point(L + 1, UI.S(82)), Size = new Size(CW, UI.S(170)), ForeColor = UI.Text };
        status = new Label { Text = Tr.T("Deine Einstellungen bleiben erhalten.", "Your settings will be kept."), Font = UI.F(8.5f), AutoSize = false, TextAlign = ContentAlignment.MiddleCenter, Location = new Point(L, UI.S(258)), Size = new Size(CW, UI.S(20)), ForeColor = UI.Sub };
        later = new PillButton { Text = Tr.T("Später", "Later"), Fill = Color.FromArgb(44, 44, 48), Font = UI.F(10, FontStyle.Bold), Location = new Point(L, UI.S(286)), Size = new Size((CW - UI.S(10)) / 2, UI.S(40)) };
        go = new PillButton { Text = Tr.T("Jetzt aktualisieren", "Update now"), Font = UI.F(10, FontStyle.Bold), Location = new Point(L + (CW + UI.S(10)) / 2, UI.S(286)), Size = new Size((CW - UI.S(10)) / 2, UI.S(40)) };
        later.Click += (s, e) => Close();
        go.Click += (s, e) => Go();
        Controls.AddRange(new Control[] { title, sub, body, status, later, go });
        ClientSize = new Size(W, UI.S(346));
        Load += (s, e) => { var wa = Screen.PrimaryScreen.WorkingArea; Location = new Point(wa.Right - Width - UI.S(24), wa.Bottom - Height - UI.S(24)); };
        Shown += (s, e) => { WindowState = FormWindowState.Normal; Activate(); };
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        try { int v = 2; DwmSetWindowAttribute(Handle, 33, ref v, 4); } catch { }
        try { int v = 1; DwmSetWindowAttribute(Handle, 20, ref v, 4); } catch { }
    }
    protected override CreateParams CreateParams { get { var cp = base.CreateParams; cp.ClassStyle |= 0x20000; return cp; } }

    void Go()
    {
        go.Enabled = false; later.Enabled = false; status.ForeColor = UI.Sub; status.Text = Tr.T("Lade Update herunter …", "Downloading update …"); status.Refresh();
        try { Updater.Apply(info); }
        catch (Exception ex) { status.ForeColor = UI.Red; status.Text = Tr.T("Update fehlgeschlagen: ", "Update failed: ") + ex.Message; go.Enabled = true; later.Enabled = true; }
    }
}

// ======================= Look & Feel =======================
static class UI
{
    public static float Scale = 1f;
    public static int S(float v) { return (int)Math.Round(v * Scale); }
    public static readonly Color Bg = Color.FromArgb(14, 14, 16);
    public static readonly Color Card = Color.FromArgb(30, 30, 33);
    public static readonly Color CardHi = Color.FromArgb(44, 44, 48);
    public static readonly Color Line = Color.FromArgb(48, 48, 52);
    public static readonly Color Text = Color.FromArgb(245, 245, 247);
    public static readonly Color Sub = Color.FromArgb(142, 142, 147);
    public static readonly Color Blue = Color.FromArgb(10, 132, 255);
    public static readonly Color Green = Color.FromArgb(48, 209, 88);
    public static readonly Color Red = Color.FromArgb(255, 69, 58);
    public static string FontName = "Segoe UI Variable Text";
    public static string IconFont = "Segoe Fluent Icons";
    public static Font F(float size, FontStyle st = FontStyle.Regular) { return new Font(FontName, size, st, GraphicsUnit.Point); }

    public static GraphicsPath Round(RectangleF r, float rad)
    {
        var p = new GraphicsPath(); float d = rad * 2;
        if (d > r.Height) d = r.Height; if (d > r.Width) d = r.Width;
        p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure(); return p;
    }
    public static void Hq(Graphics g) { g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit; g.PixelOffsetMode = PixelOffsetMode.HighQuality; }
}

class PillButton : Control
{
    public Color Fill = UI.Blue; bool hover, down;
    public PillButton() { SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true); Cursor = Cursors.Hand; ForeColor = Color.White; }
    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; down = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { down = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; UI.Hq(g); g.Clear(Parent.BackColor);
        Color c = Fill;
        if (!Enabled) c = Color.FromArgb(120, c);
        else if (down) c = ControlPaint.Dark(c, 0.08f);
        else if (hover) c = ControlPaint.Light(c, 0.15f);
        using (var p = UI.Round(new RectangleF(0, 0, Width - 1, Height - 1), (Height - 1) / 2f)) using (var b = new SolidBrush(c)) g.FillPath(b, p);
        TextRenderer.DrawText(g, Text, Font, ClientRectangle, Enabled ? ForeColor : Color.FromArgb(180, ForeColor), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
    }
}

class Toggle : Control
{
    bool on, hover, down; float anim, press; System.Windows.Forms.Timer t;
    public event EventHandler Changed;
    public bool Checked { get { return on; } set { on = value; anim = on ? 1 : 0; Invalidate(); } }
    public Toggle()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        Cursor = Cursors.Hand; Size = new Size(UI.S(51), UI.S(31)); TabStop = true;
        t = new System.Windows.Forms.Timer { Interval = 10 };
        t.Tick += (s, e) =>
        {
            float target = on ? 1 : 0, pt = down ? 1 : 0;
            anim += (target - anim) * 0.28f; press += (pt - press) * 0.35f;
            bool done = Math.Abs(target - anim) < 0.01f && Math.Abs(pt - press) < 0.01f;
            if (done) { anim = target; press = pt; t.Stop(); }
            Invalidate();
        };
    }
    // umschalten (auch von aussen, z. B. Klick auf die Beschriftung)
    public void Flip() { on = !on; t.Start(); if (Changed != null) Changed(this, EventArgs.Empty); }
    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; down = false; t.Start(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { down = true; t.Start(); Focus(); } base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { if (down && ClientRectangle.Contains(e.Location)) Flip(); down = false; t.Start(); base.OnMouseUp(e); }
    protected override void OnKeyDown(KeyEventArgs e) { if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter) { Flip(); e.Handled = true; } base.OnKeyDown(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

    static Color Mix(Color a, Color b, float k) { return Color.FromArgb((int)(a.R + (b.R - a.R) * k), (int)(a.G + (b.G - a.G) * k), (int)(a.B + (b.B - a.B) * k)); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; UI.Hq(g);
        g.Clear(BackColor);   // Hintergrund = Kartenfarbe, kein dunkler Kasten mehr
        float w = Width - 1, h = Height - 1;
        // Spur: grau -> gruen (iOS)
        Color off = hover ? Color.FromArgb(72, 72, 76) : Color.FromArgb(62, 62, 66);
        Color c = Mix(off, hover ? ControlPaint.Light(UI.Green, 0.1f) : UI.Green, anim);
        using (var p = UI.Round(new RectangleF(0, 0, w, h), h / 2)) using (var b = new SolidBrush(c)) g.FillPath(b, p);
        // Knopf: beim Druecken etwas breiter (wie iOS)
        float m = UI.S(2.5f), k = h - 2 * m, stretch = k * 0.22f * press;
        float x = m + (w - k - 2 * m - stretch) * anim;
        var knob = new RectangleF(x, m, k + stretch, k);
        using (var sh = new SolidBrush(Color.FromArgb(55, 0, 0, 0))) using (var p = UI.Round(new RectangleF(knob.X, knob.Y + UI.S(1.5f), knob.Width, knob.Height), k / 2)) g.FillPath(sh, p);
        using (var p = UI.Round(knob, k / 2)) using (var b = new SolidBrush(Color.White)) g.FillPath(b, p);
        if (Focused && ShowFocusCues) using (var pen = new Pen(Color.FromArgb(160, UI.Blue), UI.S(1.5f))) using (var p = UI.Round(new RectangleF(0.5f, 0.5f, w - 1, h - 1), h / 2)) g.DrawPath(pen, p);
    }
}

class Segmented : Control
{
    public string[] Items = new string[0]; public int Cols = 0; int sel; int hov = -1;
    public event EventHandler Changed;
    public int Selected { get { return sel; } set { sel = value; Invalidate(); } }
    public Segmented() { SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true); Cursor = Cursors.Hand; }
    int C { get { return Cols > 0 ? Cols : Math.Max(1, Items.Length); } }
    int R { get { return (Items.Length + C - 1) / C; } }
    int Hit(Point pt)
    {
        if (Items.Length == 0) return -1;
        int c = Math.Max(0, Math.Min(C - 1, pt.X * C / Width)), r = Math.Max(0, Math.Min(R - 1, pt.Y * R / Height));
        return Math.Min(Items.Length - 1, r * C + c);
    }
    protected override void OnMouseMove(MouseEventArgs e) { int h = Hit(e.Location); if (h != hov) { hov = h; Invalidate(); } base.OnMouseMove(e); }
    protected override void OnMouseLeave(EventArgs e) { hov = -1; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseClick(MouseEventArgs e) { sel = Hit(e.Location); Invalidate(); if (Changed != null) Changed(this, e); base.OnMouseClick(e); }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; UI.Hq(g); g.Clear(Parent.BackColor);
        float w = Width - 1, h = Height - 1, r = UI.S(9);
        using (var p = UI.Round(new RectangleF(0, 0, w, h), r)) using (var b = new SolidBrush(UI.Card)) g.FillPath(b, p);
        float sw = (w - 4) / C, sh = (h - 4) / R;
        for (int i = 0; i < Items.Length; i++)
        {
            var rc = new RectangleF(2 + (i % C) * sw, 2 + (i / C) * sh, sw, sh);
            if (i == sel) using (var p = UI.Round(rc, r - 2)) using (var b = new SolidBrush(Color.FromArgb(99, 99, 102))) g.FillPath(b, p);
            else if (i == hov) using (var p = UI.Round(rc, r - 2)) using (var b = new SolidBrush(UI.CardHi)) g.FillPath(b, p);
            TextRenderer.DrawText(g, Items[i], Font, Rectangle.Round(rc), UI.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }
    }
}

// ======================= Sprache =======================
static class Tr
{
    public static string Lang = "de";
    public static string T(string de, string en) { return Lang == "en" ? en : de; }
}

// Scrollbare Seite, auf die die Karten gemalt werden
class Page : Panel
{
    public Page() { DoubleBuffered = true; AutoScroll = true; SetStyle(ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true); }
    protected override void OnScroll(ScrollEventArgs se) { base.OnScroll(se); Invalidate(); }
    protected override void OnMouseWheel(MouseEventArgs e) { base.OnMouseWheel(e); Invalidate(); }
}

// ======================= Fenster =======================
// Richtiges App-Fenster: Titelleiste mit Minimieren/Maximieren/Schliessen, Groesse verstellbar,
// bleibt nach dem Speichern offen.
class SetupForm : Form
{
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int a, ref int v, int s);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr SendMessage(IntPtr h, int m, IntPtr w, string l);

    bool running;
    Page page; Panel bar;
    TextBox search; ListBox results; Label chosen, status, searchIcon, updLink;
    Toggle tAuto, tTop, tSide, tGame, tFps; Segmented segPos, segMon, segLang; PillButton save, stop;
    Program.Place picked; Updater.Info update;
    System.Windows.Forms.Timer debounce; int searchId;
    Icon appIcon;
    // Layout (in Seiten-Koordinaten, ohne Scroll)
    Rectangle head, searchCard, setCard; int secWetter, secSet, secMon, secPos, secLang, rowH;
    Toggle[] rowToggles;

    public SetupForm(bool running)
    {
        this.running = running;
        using (var g = CreateGraphics()) UI.Scale = g.DpiX / 96f;
        try { using (var f = new Font(UI.FontName, 10)) if (f.Name != UI.FontName) UI.FontName = "Segoe UI"; } catch { UI.FontName = "Segoe UI"; }
        try { appIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); Icon = appIcon; } catch { }
        Text = "PC-Widget";
        FormBorderStyle = FormBorderStyle.Sizable; MaximizeBox = true; MinimizeBox = true; ShowInTaskbar = true;
        StartPosition = FormStartPosition.Manual;
        BackColor = UI.Bg; ForeColor = UI.Text; Font = UI.F(10); KeyPreview = true;
        MinimumSize = new Size(UI.S(420), UI.S(480));
        ClientSize = new Size(UI.S(520), UI.S(760));

        BuildUi();

        Load += (s, e) =>
        {
            var wa = Screen.FromPoint(Cursor.Position).WorkingArea;
            if (Height > wa.Height) Height = wa.Height;
            Location = new Point(wa.X + (wa.Width - Width) / 2, wa.Y + (wa.Height - Height) / 2);
        };
        Shown += (s, e) => { WindowState = FormWindowState.Normal; Activate(); if (!running) search.Focus(); CheckUpdate(false); };
        Resize += (s, e) => DoLayout();
        KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape && results.Visible) { results.Visible = false; e.Handled = true; } };
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        try { int v = 1; DwmSetWindowAttribute(Handle, 20, ref v, 4); } catch { }   // dunkle Titelleiste
        try { int v = 2; DwmSetWindowAttribute(Handle, 33, ref v, 4); } catch { }   // runde Ecken (Win11)
    }

    // ---------- Aufbau ----------
    void BuildUi()
    {
        SuspendLayout();
        Controls.Clear();
        Text = running ? Tr.T("PC-Widget – Einstellungen", "PC-Widget – Settings") : "PC-Widget";
        var cfg = Program.HasCfg(Program.SettingsFile) ? Program.LoadCfg(Program.SettingsFile) : Program.CfgFromFiles(Program.RmIni, Program.SkinIni);

        page = new Page { Dock = DockStyle.Fill, BackColor = UI.Bg };
        page.Paint += PaintPage;
        page.MouseClick += (s, e) => { var t = RowAt(e.Location); if (t != null && e.Button == MouseButtons.Left) t.Flip(); };
        page.MouseMove += (s, e) => { page.Cursor = RowAt(e.Location) != null ? Cursors.Hand : Cursors.Default; };
        bar = new Panel { Dock = DockStyle.Bottom, Height = UI.S(104), BackColor = UI.Bg };
        bar.Paint += (s, e) => { using (var p = new Pen(UI.Line)) e.Graphics.DrawLine(p, 0, 0, bar.Width, 0); };

        // Wetter
        searchIcon = new Label { Text = "", Font = new Font(UI.IconFont, 11), ForeColor = UI.Sub, BackColor = UI.Card, AutoSize = false, TextAlign = ContentAlignment.MiddleCenter, Size = new Size(UI.S(36), UI.S(36)) };
        search = new TextBox { BorderStyle = BorderStyle.None, BackColor = UI.Card, ForeColor = UI.Text, Font = UI.F(11) };
        search.HandleCreated += (s, e) => SendMessage(search.Handle, 0x1501, (IntPtr)1, Tr.T("Stadt oder PLZ suchen …", "Search city or postcode …"));
        results = new ListBox { DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = UI.S(46), BorderStyle = BorderStyle.None, BackColor = UI.Card, ForeColor = UI.Text, Visible = false, IntegralHeight = false, Cursor = Cursors.Hand };
        results.DrawItem += DrawResult;
        results.MouseMove += (s, e) => { int i = results.IndexFromPoint(e.Location); if (i >= 0 && i != results.SelectedIndex) results.SelectedIndex = i; };
        results.MouseClick += (s, e) => Pick(results.IndexFromPoint(e.Location));
        chosen = new Label { AutoSize = false, ForeColor = UI.Sub, Font = UI.F(9.5f), BackColor = UI.Bg };
        string ort = Program.Get(Program.SkinIni, "Variables", "Ort", ""), plz = Program.Get(Program.SkinIni, "Variables", "PLZ", "");
        if (picked != null) { chosen.ForeColor = UI.Green; chosen.Text = "✓  " + picked.Name + (picked.Detail != "" ? "  ·  " + picked.Detail : ""); }
        else chosen.Text = "✓  " + Tr.T("Aktuell: ", "Current: ") + ort + (plz != "" ? "  ·  " + plz : "");
        debounce = new System.Windows.Forms.Timer { Interval = 380 };
        debounce.Tick += (s, e) => { debounce.Stop(); RunSearch(); };
        search.TextChanged += (s, e) => { debounce.Stop(); if (search.Text.Trim().Length >= 2) debounce.Start(); else results.Visible = false; };
        search.KeyDown += (s, e) =>
        {
            if (!results.Visible) return;
            if (e.KeyCode == Keys.Down) { results.SelectedIndex = Math.Min(results.Items.Count - 1, results.SelectedIndex + 1); e.Handled = true; }
            else if (e.KeyCode == Keys.Up) { results.SelectedIndex = Math.Max(0, results.SelectedIndex - 1); e.Handled = true; }
            else if (e.KeyCode == Keys.Enter) { Pick(Math.Max(0, results.SelectedIndex)); e.Handled = true; e.SuppressKeyPress = true; }
        };

        // Schalter
        tAuto = new Toggle { BackColor = UI.Card, Checked = running ? Program.AutostartOn() : true };
        tTop = new Toggle { BackColor = UI.Card, Checked = cfg.Top };
        tSide = new Toggle { BackColor = UI.Card, Checked = cfg.Sidebar };
        tGame = new Toggle { BackColor = UI.Card, Checked = cfg.Game };
        tFps = new Toggle { BackColor = UI.Card, Checked = cfg.Fps && Fps.TaskExists() };
        rowToggles = new[] { tAuto, tTop, tSide, tGame, tFps };

        // Monitor
        var mons = Program.Monitors();
        var monItems = new List<string> { Tr.T("Hauptbildschirm", "Main screen") };
        for (int i = 0; i < mons.Length; i++)
            if (!mons[i].Primary) monItems.Add(Tr.T("Bildschirm ", "Screen ") + (i + 1) + "  ·  " + mons[i].Bounds.Width + "×" + mons[i].Bounds.Height);
        var monIndex = new List<int> { 0 }; for (int i = 0; i < mons.Length; i++) if (!mons[i].Primary) monIndex.Add(i + 1);
        segMon = new Segmented { Items = monItems.ToArray(), Cols = Math.Min(2, monItems.Count), Font = UI.F(9.5f, FontStyle.Bold), Tag = monIndex };
        segMon.Selected = Math.Max(0, monIndex.IndexOf(cfg.Monitor));

        segPos = new Segmented { Items = new[] { "↖  " + Tr.T("Oben links", "Top left"), Tr.T("Oben rechts", "Top right") + "  ↗", "↙  " + Tr.T("Unten links", "Bottom left"), Tr.T("Unten rechts", "Bottom right") + "  ↘" }, Cols = 2, Font = UI.F(10, FontStyle.Bold) };
        segPos.Selected = cfg.Corner;
        segLang = new Segmented { Items = new[] { "Deutsch", "English" }, Font = UI.F(10, FontStyle.Bold), Selected = cfg.Lang == "en" ? 1 : 0 };

        // Knoepfe + Fusszeile
        status = new Label { AutoSize = false, ForeColor = UI.Sub, Font = UI.F(9), TextAlign = ContentAlignment.MiddleCenter, BackColor = UI.Bg };
        save = new PillButton { Text = running ? Tr.T("Speichern", "Save") : Tr.T("Speichern & Starten", "Save & start"), Font = UI.F(10.5f, FontStyle.Bold) };
        save.Click += (s, e) => Save();
        stop = new PillButton { Text = Tr.T("Widget beenden", "Quit widget"), Fill = Color.FromArgb(44, 44, 48), Font = UI.F(10.5f, FontStyle.Bold), Visible = running };
        stop.Click += (s, e) => { Program.Quit(true); running = false; status.ForeColor = UI.Sub; status.Text = Tr.T("Widget beendet.", "Widget stopped."); RefreshButtons(); };
        updLink = new Label { Text = Tr.T("Version ", "Version ") + Installer.Version + "  ·  " + Tr.T("Nach Updates suchen", "Check for updates"), AutoSize = false, TextAlign = ContentAlignment.MiddleCenter, ForeColor = UI.Sub, Font = UI.F(9), Cursor = Cursors.Hand, BackColor = UI.Bg };
        updLink.MouseEnter += (s, e) => { if (update == null) updLink.ForeColor = UI.Text; };
        updLink.MouseLeave += (s, e) => { if (update == null) updLink.ForeColor = UI.Sub; };
        updLink.Click += (s, e) => { if (update != null) new UpdateForm(update).ShowDialog(this); else CheckUpdate(true); };

        page.Controls.AddRange(new Control[] { results, searchIcon, search, chosen, tAuto, tTop, tSide, tGame, tFps, segMon, segPos, segLang });
        bar.Controls.AddRange(new Control[] { status, stop, save, updLink });
        Controls.Add(page); Controls.Add(bar);
        results.BringToFront();
        ResumeLayout();
        DoLayout();
    }

    void RefreshButtons()
    {
        stop.Visible = running;
        save.Text = running ? Tr.T("Speichern", "Save") : Tr.T("Speichern & Starten", "Save & start");
        Text = running ? Tr.T("PC-Widget – Einstellungen", "PC-Widget – Settings") : "PC-Widget";
        DoLayout(); page.Invalidate();
    }

    // ---------- Layout: passt sich der Fenstergroesse an ----------
    void DoLayout()
    {
        if (page == null) return;
        int W = Math.Min(page.ClientSize.Width, UI.S(720));
        int L0 = Math.Max(UI.S(20), (page.ClientSize.Width - W) / 2 + UI.S(24));
        int CW = Math.Max(UI.S(300), Math.Min(page.ClientSize.Width - 2 * UI.S(24), UI.S(640)));
        L0 = (page.ClientSize.Width - CW) / 2;
        var sp = page.AutoScrollPosition;
        Func<int, int> Y = y => y + sp.Y;
        int y0 = UI.S(20);
        head = new Rectangle(L0, y0, CW, UI.S(70));
        secWetter = head.Bottom + UI.S(14);
        searchCard = new Rectangle(L0, secWetter + UI.S(24), CW, UI.S(44));
        searchIcon.Location = new Point(searchCard.X + UI.S(6), Y(searchCard.Y + UI.S(4)));
        search.Location = new Point(searchCard.X + UI.S(44), Y(searchCard.Y + UI.S(11))); search.Width = CW - UI.S(56);
        chosen.Location = new Point(L0 + UI.S(2), Y(searchCard.Bottom + UI.S(8))); chosen.Size = new Size(CW, UI.S(22));
        results.Location = new Point(L0, Y(searchCard.Bottom + UI.S(6))); results.Width = CW;
        secSet = searchCard.Bottom + UI.S(44);
        rowH = UI.S(62);
        setCard = new Rectangle(L0, secSet + UI.S(24), CW, rowH * 5);
        for (int i = 0; i < 5; i++) rowToggles[i].Location = new Point(setCard.Right - rowToggles[i].Width - UI.S(14), Y(setCard.Y + i * rowH + (rowH - rowToggles[i].Height) / 2));
        secMon = setCard.Bottom + UI.S(22);
        int monRows = (segMon.Items.Length + 1) / 2;
        segMon.Location = new Point(L0, Y(secMon + UI.S(24))); segMon.Size = new Size(CW, UI.S(38) * monRows);
        secPos = secMon + UI.S(24) + segMon.Height + UI.S(22);
        segPos.Location = new Point(L0, Y(secPos + UI.S(24))); segPos.Size = new Size(CW, UI.S(76));
        secLang = secPos + UI.S(24) + segPos.Height + UI.S(22);
        segLang.Location = new Point(L0, Y(secLang + UI.S(24))); segLang.Size = new Size(CW, UI.S(38));
        page.AutoScrollMinSize = new Size(0, secLang + UI.S(24) + segLang.Height + UI.S(20));

        // untere Leiste
        int bw = bar.ClientSize.Width, bL = (bw - CW) / 2;
        status.Location = new Point(bL, UI.S(6)); status.Size = new Size(CW, UI.S(20));
        int by = UI.S(28);
        if (running) { stop.Location = new Point(bL, by); stop.Size = new Size((CW - UI.S(12)) / 2, UI.S(42)); save.Location = new Point(bL + (CW + UI.S(12)) / 2, by); save.Size = new Size((CW - UI.S(12)) / 2, UI.S(42)); }
        else { save.Location = new Point(bL, by); save.Size = new Size(CW, UI.S(42)); }
        updLink.Location = new Point(bL, UI.S(76)); updLink.Size = new Size(CW, UI.S(22));
        page.Invalidate();
    }

    Toggle RowAt(Point pt)
    {
        var sp = page.AutoScrollPosition; var p = new Point(pt.X - sp.X, pt.Y - sp.Y);
        if (!setCard.Contains(p)) return null;
        int i = (p.Y - setCard.Y) / rowH; return i >= 0 && i < 5 ? rowToggles[i] : null;
    }

    // ---------- Malen ----------
    void PaintPage(object sender, PaintEventArgs e)
    {
        var g = e.Graphics; UI.Hq(g);
        g.TranslateTransform(page.AutoScrollPosition.X, page.AutoScrollPosition.Y);
        if (appIcon != null) using (var ic = new Icon(appIcon, UI.S(64), UI.S(64))) g.DrawIcon(ic, new Rectangle(head.X, head.Y + UI.S(4), UI.S(56), UI.S(56)));
        Func<Point, Point> P = p => new Point(p.X + page.AutoScrollPosition.X, p.Y + page.AutoScrollPosition.Y);
        using (var f = UI.F(18, FontStyle.Bold)) TextRenderer.DrawText(g, running ? Tr.T("Einstellungen", "Settings") : "PC-Widget", f, P(new Point(head.X + UI.S(68), head.Y + UI.S(4))), UI.Text);
        using (var f = UI.F(9.5f)) TextRenderer.DrawText(g, Tr.T("Uhr, Musik, Wetter und PC-Werte im iOS-Look.", "Clock, music, weather and PC stats in iOS style."), f, P(new Point(head.X + UI.S(70), head.Y + UI.S(38))), UI.Sub);

        Section(g, Tr.T("WETTER", "WEATHER"), secWetter, head.X);
        Card(g, searchCard);
        Section(g, Tr.T("EINSTELLUNGEN", "SETTINGS"), secSet, head.X);
        Card(g, setCard);
        string[,] rows = {
            { Tr.T("Mit Windows starten", "Start with Windows"), Tr.T("Widget startet automatisch beim Hochfahren", "Widget starts automatically") },
            { Tr.T("Immer im Vordergrund", "Always on top"), Tr.T("Auch über Spielen im randlosen Fenster", "Also over borderless games") },
            { Tr.T("An den Rand bei offenen Fenstern", "Hide to edge when apps are open"), Tr.T("Pfeil am Rand – Maus drauf holt es zurück", "Arrow at the edge – hover to bring it back") },
            { Tr.T("Spiel-Modus", "Game mode"), Tr.T("Beim Zocken nur eine kleine Leiste (Uhr, Song, Ping)", "Small bar while gaming (clock, song, ping)") },
            { Tr.T("FPS anzeigen", "Show FPS"), Tr.T("Braucht einmal Admin-Rechte (PresentMon von Intel)", "Needs admin rights once (PresentMon by Intel)") } };
        for (int i = 0; i < 5; i++)
        {
            int ry = setCard.Y + i * rowH;
            if (i > 0) using (var p = new Pen(UI.Line)) g.DrawLine(p, setCard.X + UI.S(16), ry, setCard.Right, ry);
            int tw = setCard.Width - UI.S(90);
            using (var f = UI.F(11)) TextRenderer.DrawText(g, rows[i, 0], f, new Rectangle(P(new Point(setCard.X + UI.S(16), ry + UI.S(10))), new Size(tw, UI.S(24))), UI.Text, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            using (var f = UI.F(8.5f)) TextRenderer.DrawText(g, rows[i, 1], f, new Rectangle(P(new Point(setCard.X + UI.S(16), ry + UI.S(34))), new Size(tw, UI.S(20))), UI.Sub, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }
        Section(g, Tr.T("BILDSCHIRM", "SCREEN"), secMon, head.X);
        Section(g, Tr.T("POSITION", "POSITION"), secPos, head.X);
        Section(g, Tr.T("SPRACHE", "LANGUAGE"), secLang, head.X);
    }
    void Section(Graphics g, string t, int y, int x)
    {
        // Die Grafik ist schon um den Scroll verschoben, TextRenderer aber nicht -> selbst verschieben
        using (var f = UI.F(8, FontStyle.Bold)) TextRenderer.DrawText(g, t, f, new Point(x + UI.S(2) + page.AutoScrollPosition.X, y + page.AutoScrollPosition.Y), UI.Sub);
    }
    void Card(Graphics g, Rectangle r) { using (var p = UI.Round(r, UI.S(12))) using (var b = new SolidBrush(UI.Card)) g.FillPath(b, p); }

    void DrawResult(object sender, DrawItemEventArgs e)
    {
        if (e.Index < 0) return;
        var g = e.Graphics; UI.Hq(g);
        var pl = (Program.Place)results.Items[e.Index];
        bool sel = (e.State & DrawItemState.Selected) != 0;
        using (var b = new SolidBrush(sel ? UI.CardHi : UI.Card)) g.FillRectangle(b, e.Bounds);
        using (var f = new Font(UI.IconFont, 11)) TextRenderer.DrawText(g, "", f, new Rectangle(e.Bounds.X + UI.S(8), e.Bounds.Y, UI.S(32), e.Bounds.Height), sel ? UI.Blue : UI.Sub, TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);
        using (var f = UI.F(10.5f, FontStyle.Bold)) TextRenderer.DrawText(g, pl.Name, f, new Point(e.Bounds.X + UI.S(44), e.Bounds.Y + UI.S(5)), UI.Text);
        using (var f = UI.F(9)) TextRenderer.DrawText(g, pl.Detail, f, new Point(e.Bounds.X + UI.S(44), e.Bounds.Y + UI.S(25)), UI.Sub);
    }

    // ---------- Ortssuche ----------
    void RunSearch()
    {
        string q = search.Text.Trim(); int id = ++searchId;
        status.ForeColor = UI.Sub; status.Text = Tr.T("Suche …", "Searching …");
        ThreadPool.QueueUserWorkItem(_ =>
        {
            List<Program.Place> r = null; string err = null;
            try { r = Program.Search(q); } catch { err = Tr.T("Keine Verbindung zum Wetterdienst – Internet prüfen.", "Can't reach the weather service – check your internet."); }
            try
            {
                BeginInvoke((Action)(() =>
                {
                    if (id != searchId) return;
                    status.Text = err ?? "";
                    results.Items.Clear();
                    if (r != null) foreach (var p in r) results.Items.Add(p);
                    if (r != null && r.Count == 0) status.Text = Tr.T("Nichts gefunden – andere Schreibweise probieren.", "Nothing found – try another spelling.");
                    results.Height = Math.Min(4, results.Items.Count) * results.ItemHeight;
                    results.Visible = results.Items.Count > 0;
                    if (results.Visible) { results.SelectedIndex = 0; results.BringToFront(); }
                }));
            }
            catch { }
        });
    }

    void Pick(int i)
    {
        if (i < 0 || i >= results.Items.Count) return;
        picked = (Program.Place)results.Items[i];
        results.Visible = false; search.Text = ""; debounce.Stop();
        chosen.ForeColor = UI.Green;
        chosen.Text = "✓  " + picked.Name + (picked.Detail != "" ? "  ·  " + picked.Detail : "");
        status.Text = "";
    }

    // ---------- Updates ----------
    void CheckUpdate(bool manual)
    {
        if (manual) { updLink.ForeColor = UI.Sub; updLink.Text = Tr.T("Suche nach Updates …", "Checking for updates …"); }
        ThreadPool.QueueUserWorkItem(_ =>
        {
            Updater.Info info = null; bool failed = false;
            try { info = Updater.Check(); } catch { failed = true; }
            try
            {
                BeginInvoke((Action)(() =>
                {
                    update = info;
                    if (info != null) { updLink.ForeColor = UI.Blue; updLink.Text = Tr.T("Update ", "Update ") + info.Version + Tr.T(" verfügbar – jetzt installieren", " available – install now"); }
                    else if (manual) { updLink.ForeColor = failed ? UI.Red : UI.Green; updLink.Text = failed ? Tr.T("Keine Verbindung zu GitHub", "Can't reach GitHub") : "✓  " + Tr.T("Du hast die neueste Version (", "You have the latest version (") + Installer.Version + ")"; }
                }));
            }
            catch { }
        });
    }

    // ---------- Speichern: Fenster bleibt offen ----------
    void Save()
    {
        save.Enabled = false; status.ForeColor = UI.Sub; status.Text = Tr.T("Speichere …", "Saving …"); status.Refresh();
        try
        {
            var c = new Program.Cfg
            {
                Top = tTop.Checked, Sidebar = tSide.Checked, Game = tGame.Checked, Fps = tFps.Checked,
                Corner = segPos.Selected,
                Monitor = ((List<int>)segMon.Tag)[Math.Max(0, segMon.Selected)],
                Lang = segLang.Selected == 1 ? "en" : "de"
            };
            if (picked != null) Program.SaveWeather(picked.Name, picked.Plz ?? "", picked.Lat, picked.Lon);
            Program.SetAutostart(tAuto.Checked);
            if (Program.IsRunning()) Program.Quit(true);
            Program.ApplyCfg(c, Program.RmDir);
            Program.SaveCfg(c, Program.SettingsFile);
            Program.MarkSetupDone();
            Program.Start();
            // FPS: Aufgabe einmalig mit Admin-Rechten anlegen
            string fpsNote = null;
            if (c.Fps)
            {
                if (!Fps.TaskExists())
                {
                    status.Text = Tr.T("Bitte die Windows-Abfrage bestätigen …", "Please confirm the Windows prompt …"); status.Refresh();
                    if (!Fps.Install(Application.ExecutablePath)) { c.Fps = false; tFps.Checked = false; Program.SaveCfg(c, Program.SettingsFile); fpsNote = Tr.T("FPS nicht eingeschaltet – Admin-Abfrage abgelehnt.", "FPS not enabled – admin prompt declined."); }
                }
                else Fps.StartTask();
            }
            running = true;
            bool langChanged = c.Lang != Tr.Lang;
            Tr.Lang = c.Lang;
            if (langChanged) BuildUi(); else RefreshButtons();
            status.ForeColor = fpsNote == null ? UI.Green : UI.Red; status.Text = fpsNote ?? "✓  " + Tr.T("Gespeichert – das Widget läuft.", "Saved – the widget is running.");
        }
        catch (Exception ex) { status.ForeColor = UI.Red; status.Text = Tr.T("Fehler: ", "Error: ") + ex.Message; }
        save.Enabled = true;
    }
}
