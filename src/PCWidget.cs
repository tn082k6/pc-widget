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
[assembly: System.Reflection.AssemblyVersion("1.4.0.0")]

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
        if (!File.Exists(RmExe) || !File.Exists(SkinIni))
        {
            // Einzelne exe (z. B. frisch heruntergeladen): sich selbst einrichten
            if (Installer.HasPayload()) { Installer.InstallAndLaunch(); return; }
            MessageBox.Show("Der Ordner \"Rainmeter\" fehlt neben PC-Widget.exe.\nBitte den ganzen Ordner entpacken, nicht nur die .exe.", "PC-Widget", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
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

    public static string TabIni { get { return Path.Combine(RmDir, @"Skins\PCWidget\Tab\Tab.ini"); } }

    // Pfeil-Griff an die richtige Kante setzen (rechts/links, oben/unten wie das Widget)
    public static void WriteTabPosition(bool right, bool bottom, string rmIni = null, string tabIni = null)
    {
        rmIni = rmIni ?? RmIni; tabIni = tabIni ?? TabIni;
        const string sec = @"PCWidget\Tab";
        Set(rmIni, sec, "Active", "1");
        Set(rmIni, sec, "WindowX", right ? "0R" : "0");
        Set(rmIni, sec, "AnchorX", right ? "100%" : "0%");
        Set(rmIni, sec, "WindowY", bottom ? "97B" : "49");   // mittig neben dem kompakten Widget
        Set(rmIni, sec, "AnchorY", bottom ? "100%" : "0%");
        Set(rmIni, sec, "AlwaysOnTop", "2");
        Set(rmIni, sec, "Draggable", "0");
        Set(rmIni, sec, "KeepOnScreen", "1");
        Set(rmIni, sec, "SnapEdges", "0");
        Set(tabIni, "Variables", "Arrow", right ? "[\\xE76B]" : "[\\xE76C]");
        Set(tabIni, "Variables", "TabX", right ? "0R" : "0");
        Set(tabIni, "Variables", "TabY", bottom ? "97B" : "49");
        Set(tabIni, "Variables", "TabAX", right ? "100%" : "0%");
        Set(tabIni, "Variables", "TabAY", bottom ? "100%" : "0%");
    }

    // Seitenleiste an/aus: Widget versteckt sich, wenn ein Programm vorne ist, und laesst den Pfeil-Griff da
    public static bool SidebarOn() { return Get(SkinIni, "Variables", "HideAction", "[!HideFade]").Trim() != ""; }
    public static void SetSidebar(bool on, bool right)
    {
        Set(SkinIni, "Variables", "HideAction", on ? "[!HideFade]" : "");
        Set(SkinIni, "Variables", "ShowAction", on ? "[!ShowFade]" : "");
        Set(SkinIni, "Variables", "GrabX", right ? "0" : "268");
        Set(TabIni, "Variables", "TabOnApp", on ? "[!ShowFade]" : "");
        Set(TabIni, "Variables", "TabOnDesk", on ? "[!HideFade]" : "");
    }

    public class Place { public string Name, Detail, Lat, Lon; }

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
                            list.Add(new Place { Name = Convert.ToString(p["place name"]), Detail = q + "  ·  " + Convert.ToString(p["state"]) + ", " + country, Lat = Convert.ToString(p["latitude"], System.Globalization.CultureInfo.InvariantCulture), Lon = Convert.ToString(p["longitude"], System.Globalization.CultureInfo.InvariantCulture) });
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
                list.Add(new Place { Name = s("name"), Detail = string.Join(", ", det), Lat = s("latitude"), Lon = s("longitude") });
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

    public const string Version = "1.4";
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
                // Einstellungen einer alten Version merken
                string rmIni = Path.Combine(rm, "Rainmeter.ini"), skin = Path.Combine(rm, @"Skins\PCWidget\PCWidget.ini"), flag = Path.Combine(rm, "einrichtung.ok");
                byte[] oldRmIni = File.Exists(rmIni) ? File.ReadAllBytes(rmIni) : null;
                var keep = new Dictionary<string, string>();
                foreach (var k in new[] { "Lat", "Lon", "Ort", "PosBang", "HideAction", "ShowAction", "GrabX" }) { var v = Read(skin, "Variables", k); if (v != null) keep[k] = v; }
                bool wasSetup = File.Exists(flag);

                string tabIni = Path.Combine(rm, @"Skins\PCWidget\Tab\Tab.ini");
                var keepTab = new Dictionary<string, string>();
                foreach (var k in new[] { "Arrow", "TabOnApp", "TabOnDesk", "TabX", "TabY", "TabAX", "TabAY" }) { var v = Read(tabIni, "Variables", k); if (v != null) keepTab[k] = v; }

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
                // Aeltere Versionen kannten den Pfeil-Griff noch nicht -> passend zur Widget-Seite eintragen
                if (Read(rmIni, @"PCWidget\Tab", "Active") == null)
                {
                    bool r = Read(rmIni, "PCWidget", "AnchorX") != "0%", b = Read(rmIni, "PCWidget", "AnchorY") == "100%";
                    Program.WriteTabPosition(r, b, rmIni, tabIni);
                    WritePrivateProfileString("Variables", "GrabX", r ? "0" : "268", skin);
                }
                if (wasSetup) File.WriteAllText(flag, DateTime.Now.ToString("s"));

                // alte exe kann sich nach einem Update noch beenden -> kurz warten
                for (int i = 0; ; i++)
                {
                    try { File.Copy(Application.ExecutablePath, exe, true); break; }
                    catch (IOException) { if (i > 30) throw; Thread.Sleep(300); }
                }
                File.WriteAllText(ver, Version);
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
        var title = new Label { Text = "Update verfügbar", Font = UI.F(15, FontStyle.Bold), AutoSize = true, Location = new Point(L, UI.S(18)), ForeColor = UI.Text };
        var sub = new Label { Text = "PC-Widget " + info.Version + " ist da – du hast " + Installer.Version + ".", Font = UI.F(9.5f), AutoSize = true, Location = new Point(L + 1, UI.S(50)), ForeColor = UI.Sub };
        var body = new Label { Text = notes, Font = UI.F(9), AutoSize = false, Location = new Point(L + 1, UI.S(82)), Size = new Size(CW, UI.S(170)), ForeColor = UI.Text };
        status = new Label { Text = "Deine Einstellungen bleiben erhalten.", Font = UI.F(8.5f), AutoSize = false, TextAlign = ContentAlignment.MiddleCenter, Location = new Point(L, UI.S(258)), Size = new Size(CW, UI.S(20)), ForeColor = UI.Sub };
        later = new PillButton { Text = "Später", Fill = Color.FromArgb(44, 44, 48), Font = UI.F(10, FontStyle.Bold), Location = new Point(L, UI.S(286)), Size = new Size((CW - UI.S(10)) / 2, UI.S(40)) };
        go = new PillButton { Text = "Jetzt aktualisieren", Font = UI.F(10, FontStyle.Bold), Location = new Point(L + (CW + UI.S(10)) / 2, UI.S(286)), Size = new Size((CW - UI.S(10)) / 2, UI.S(40)) };
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
        go.Enabled = false; later.Enabled = false; status.ForeColor = UI.Sub; status.Text = "Lade Update herunter …"; status.Refresh();
        try { Updater.Apply(info); }
        catch (Exception ex) { status.ForeColor = UI.Red; status.Text = "Update fehlgeschlagen: " + ex.Message; go.Enabled = true; later.Enabled = true; }
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

// ======================= Fenster =======================
class SetupForm : Form
{
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int a, ref int v, int s);
    [DllImport("user32.dll")] static extern bool ReleaseCapture();
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, int m, IntPtr w, IntPtr l);

    bool running;
    TextBox search; ListBox results; Label chosen, status, searchIcon;
    Toggle tAuto, tTop, tSide; Segmented seg; PillButton save; Label updLink; Updater.Info update;
    Program.Place picked;
    System.Windows.Forms.Timer debounce;
    int searchId;
    Rectangle searchCard, settingsCard;
    Icon appIcon;

    public SetupForm(bool running)
    {
        this.running = running;
        using (var g = CreateGraphics()) UI.Scale = g.DpiX / 96f;
        try { using (var f = new Font(UI.FontName, 10)) if (f.Name != UI.FontName) UI.FontName = "Segoe UI"; } catch { UI.FontName = "Segoe UI"; }

        Text = "PC-Widget";
        try { appIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); Icon = appIcon; } catch { }
        FormBorderStyle = FormBorderStyle.None; StartPosition = FormStartPosition.Manual;
        BackColor = UI.Bg; ForeColor = UI.Text; Font = UI.F(10);
        DoubleBuffered = true; KeyPreview = true;
        int W = UI.S(440);
        ClientSize = new Size(W, UI.S(692));
        int L = UI.S(24), CW = W - 2 * L;

        // --- Kopf ---
        var close = new Label { Text = "", Font = new Font(UI.IconFont, 9), ForeColor = UI.Sub, AutoSize = false, TextAlign = ContentAlignment.MiddleCenter, Size = new Size(UI.S(32), UI.S(32)), Location = new Point(W - UI.S(44), UI.S(12)), Cursor = Cursors.Hand };
        close.MouseEnter += (s, e) => { close.BackColor = UI.Red; close.ForeColor = Color.White; };
        close.MouseLeave += (s, e) => { close.BackColor = UI.Bg; close.ForeColor = UI.Sub; };
        close.Click += (s, e) => Close();
        Controls.Add(close);

        // --- Wetter ---
        searchCard = new Rectangle(L, UI.S(140), CW, UI.S(44));
        searchIcon = new Label { Text = "", Font = new Font(UI.IconFont, 11), ForeColor = UI.Sub, BackColor = UI.Card, AutoSize = false, TextAlign = ContentAlignment.MiddleCenter, Size = new Size(UI.S(36), UI.S(36)), Location = new Point(searchCard.X + UI.S(6), searchCard.Y + UI.S(4)) };
        search = new TextBox { BorderStyle = BorderStyle.None, BackColor = UI.Card, ForeColor = UI.Text, Font = UI.F(11), Location = new Point(searchCard.X + UI.S(44), searchCard.Y + UI.S(11)), Width = CW - UI.S(56) };
        SetCue(search, "Stadt oder PLZ suchen …");
        results = new ListBox { DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = UI.S(46), BorderStyle = BorderStyle.None, BackColor = UI.Card, ForeColor = UI.Text, Location = new Point(L, searchCard.Bottom + UI.S(6)), Width = CW, Visible = false, IntegralHeight = false, Cursor = Cursors.Hand };
        results.DrawItem += DrawResult;
        results.MouseMove += (s, e) => { int i = results.IndexFromPoint(e.Location); if (i >= 0 && i != results.SelectedIndex) results.SelectedIndex = i; };
        results.MouseClick += (s, e) => Pick(results.IndexFromPoint(e.Location));
        chosen = new Label { AutoSize = false, Location = new Point(L + UI.S(2), searchCard.Bottom + UI.S(8)), Size = new Size(CW, UI.S(22)), ForeColor = UI.Sub, Font = UI.F(9.5f) };
        string ort = Program.Get(Program.SkinIni, "Variables", "Ort", "");
        chosen.Text = running || File.Exists(Path.Combine(Program.RmDir, "einrichtung.ok")) ? "✓  Aktuell: " + ort : "Noch kein Ort gewählt (Standard: " + ort + ")";

        debounce = new System.Windows.Forms.Timer { Interval = 380 };
        debounce.Tick += (s, e) => { debounce.Stop(); RunSearch(); };
        search.TextChanged += (s, e) => { debounce.Stop(); if (search.Text.Trim().Length >= 2) debounce.Start(); else results.Visible = false; };
        search.KeyDown += (s, e) =>
        {
            if (!results.Visible) return;
            if (e.KeyCode == Keys.Down) { results.SelectedIndex = Math.Min(results.Items.Count - 1, results.SelectedIndex + 1); e.Handled = true; }
            else if (e.KeyCode == Keys.Up) { results.SelectedIndex = Math.Max(0, results.SelectedIndex - 1); e.Handled = true; }
            else if (e.KeyCode == Keys.Enter) { Pick(Math.Max(0, results.SelectedIndex)); e.Handled = true; e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.Escape) { results.Visible = false; e.Handled = true; }
        };

        // --- Einstellungen ---
        settingsCard = new Rectangle(L, UI.S(250), CW, UI.S(186));
        tAuto = new Toggle { BackColor = UI.Card };
        tAuto.Location = new Point(settingsCard.Right - tAuto.Width - UI.S(14), settingsCard.Y + UI.S(15));
        tAuto.Checked = running ? Program.AutostartOn() : true;
        tTop = new Toggle { BackColor = UI.Card };
        tTop.Location = new Point(settingsCard.Right - tTop.Width - UI.S(14), settingsCard.Y + UI.S(77));
        tTop.Checked = Program.Get(Program.RmIni, "PCWidget", "AlwaysOnTop", "2") == "2";
        tSide = new Toggle { BackColor = UI.Card };
        tSide.Location = new Point(settingsCard.Right - tSide.Width - UI.S(14), settingsCard.Y + UI.S(139));
        tSide.Checked = Program.SidebarOn();

        seg = new Segmented { Items = new[] { "↖  Oben links", "Oben rechts  ↗", "↙  Unten links", "Unten rechts  ↘" }, Cols = 2, Font = UI.F(10, FontStyle.Bold), Location = new Point(L, UI.S(476)), Size = new Size(CW, UI.S(76)) };
        bool right = Program.Get(Program.RmIni, "PCWidget", "AnchorX", "100%") != "0%";
        bool bottom = Program.Get(Program.RmIni, "PCWidget", "AnchorY", "0%") == "100%";
        seg.Selected = (bottom ? 2 : 0) + (right ? 1 : 0);

        // --- Knoepfe ---
        int by = ClientSize.Height - UI.S(96);
        status = new Label { AutoSize = false, Location = new Point(L, by - UI.S(28)), Size = new Size(CW, UI.S(22)), ForeColor = UI.Sub, Font = UI.F(9), TextAlign = ContentAlignment.MiddleCenter };
        save = new PillButton { Text = running ? "Speichern" : "Speichern & Starten", Font = UI.F(10.5f, FontStyle.Bold) };
        if (running)
        {
            var stop = new PillButton { Text = "Widget beenden", Fill = Color.FromArgb(44, 44, 48), Font = UI.F(10.5f, FontStyle.Bold), Location = new Point(L, by), Size = new Size((CW - UI.S(12)) / 2, UI.S(44)) };
            stop.Click += (s, e) => { Program.Quit(false); Close(); };
            Controls.Add(stop);
            save.Location = new Point(L + (CW + UI.S(12)) / 2, by); save.Size = new Size((CW - UI.S(12)) / 2, UI.S(44));
        }
        else { save.Location = new Point(L, by); save.Size = new Size(CW, UI.S(44)); }
        save.Click += (s, e) => Save();

        // Fusszeile: Version + Update-Suche
        updLink = new Label { Text = "Version " + Installer.Version + "  ·  Nach Updates suchen", AutoSize = false, TextAlign = ContentAlignment.MiddleCenter, Location = new Point(L, ClientSize.Height - UI.S(40)), Size = new Size(CW, UI.S(24)), ForeColor = UI.Sub, Font = UI.F(9), Cursor = Cursors.Hand };
        updLink.MouseEnter += (s, e) => { if (update == null) updLink.ForeColor = UI.Text; };
        updLink.MouseLeave += (s, e) => { if (update == null) updLink.ForeColor = UI.Sub; };
        updLink.Click += (s, e) => { if (update != null) { new UpdateForm(update).ShowDialog(this); } else CheckUpdate(true); };
        Controls.Add(updLink);

        Controls.AddRange(new Control[] { results, searchIcon, search, chosen, tAuto, tTop, tSide, seg, status, save });
        results.BringToFront();

        MouseDown += (s, e) => { if (e.Button == MouseButtons.Left && e.Y < UI.S(110)) { ReleaseCapture(); SendMessage(Handle, 0xA1, (IntPtr)2, IntPtr.Zero); } };
        // Ganze Zeile anklickbar (wie bei iOS): Klick auf die Beschriftung schaltet den Schalter
        Func<Point, Toggle> rowAt = pt =>
        {
            if (!settingsCard.Contains(pt)) return null;
            int y = pt.Y - settingsCard.Y;
            return y < UI.S(62) ? tAuto : y < UI.S(124) ? tTop : tSide;
        };
        MouseMove += (s, e) => { Cursor = rowAt(e.Location) != null ? Cursors.Hand : Cursors.Default; };
        MouseClick += (s, e) => { var tg = rowAt(e.Location); if (tg != null && e.Button == MouseButtons.Left) tg.Flip(); };
        KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape && !results.Visible) Close(); };
        Load += (s, e) => { var wa = Screen.FromPoint(Cursor.Position).WorkingArea; Location = new Point(wa.X + (wa.Width - Width) / 2, wa.Y + (wa.Height - Height) / 2); };
        Shown += (s, e) => { WindowState = FormWindowState.Normal; Activate(); if (!running) search.Focus(); else ActiveControl = null; CheckUpdate(false); };
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        try { int v = 2; DwmSetWindowAttribute(Handle, 33, ref v, 4); } catch { }   // runde Ecken (Win11)
        try { int v = 1; DwmSetWindowAttribute(Handle, 20, ref v, 4); } catch { }   // dunkel
    }

    protected override CreateParams CreateParams { get { var cp = base.CreateParams; cp.ClassStyle |= 0x20000; return cp; } } // Schatten

    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr SendMessage(IntPtr h, int m, IntPtr w, string l);
    static void SetCue(TextBox tb, string cue) { tb.HandleCreated += (s, e) => SendMessage(tb.Handle, 0x1501, (IntPtr)1, cue); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; UI.Hq(g);
        int L = UI.S(24);
        // App-Icon + Titel
        if (appIcon != null) using (var ic = new Icon(appIcon, UI.S(64), UI.S(64))) g.DrawIcon(ic, new Rectangle(L, UI.S(26), UI.S(56), UI.S(56)));
        using (var f = UI.F(18, FontStyle.Bold)) TextRenderer.DrawText(g, running ? "Einstellungen" : "PC-Widget", f, new Point(L + UI.S(68), UI.S(26)), UI.Text);
        using (var f = UI.F(9.5f)) TextRenderer.DrawText(g, running ? "Änderungen gelten sofort fürs Widget." : "Uhr, Spotify, Wetter und PC-Werte im iOS-Look.", f, new Point(L + UI.S(70), UI.S(60)), UI.Sub);

        Section(g, "WETTER", UI.S(116));
        Card(g, searchCard);
        Section(g, "EINSTELLUNGEN", UI.S(226));
        Card(g, settingsCard);
        using (var f = UI.F(11)) TextRenderer.DrawText(g, "Mit Windows starten", f, new Point(settingsCard.X + UI.S(16), settingsCard.Y + UI.S(19)), UI.Text);
        using (var p = new Pen(UI.Line)) g.DrawLine(p, settingsCard.X + UI.S(16), settingsCard.Y + UI.S(62), settingsCard.Right, settingsCard.Y + UI.S(62));
        using (var f = UI.F(11)) TextRenderer.DrawText(g, "Immer im Vordergrund", f, new Point(settingsCard.X + UI.S(16), settingsCard.Y + UI.S(72)), UI.Text);
        using (var f = UI.F(8.5f)) TextRenderer.DrawText(g, "Auch über Spielen im randlosen Fenster", f, new Point(settingsCard.X + UI.S(16), settingsCard.Y + UI.S(95)), UI.Sub);
        using (var p = new Pen(UI.Line)) g.DrawLine(p, settingsCard.X + UI.S(16), settingsCard.Y + UI.S(124), settingsCard.Right, settingsCard.Y + UI.S(124));
        using (var f = UI.F(11)) TextRenderer.DrawText(g, "An den Rand bei offenen Fenstern", f, new Point(settingsCard.X + UI.S(16), settingsCard.Y + UI.S(134)), UI.Text);
        using (var f = UI.F(8.5f)) TextRenderer.DrawText(g, "Pfeil am Rand – Klick holt das Widget zurück", f, new Point(settingsCard.X + UI.S(16), settingsCard.Y + UI.S(157)), UI.Sub);
        Section(g, "POSITION AUF DEM HAUPTBILDSCHIRM", UI.S(452));
    }
    void Section(Graphics g, string t, int y) { using (var f = UI.F(8, FontStyle.Bold)) TextRenderer.DrawText(g, t, f, new Point(UI.S(26), y), UI.Sub); }
    void Card(Graphics g, Rectangle r) { using (var p = UI.Round(r, UI.S(12))) using (var b = new SolidBrush(UI.Card)) g.FillPath(b, p); }

    void DrawResult(object sender, DrawItemEventArgs e)
    {
        if (e.Index < 0) return;
        var g = e.Graphics; UI.Hq(g);
        var pl = (Program.Place)results.Items[e.Index];
        bool sel = (e.State & DrawItemState.Selected) != 0;
        using (var b = new SolidBrush(sel ? UI.CardHi : UI.Card)) g.FillRectangle(b, e.Bounds);
        TextRenderer.DrawText(g, "", new Font(UI.IconFont, 11), new Rectangle(e.Bounds.X + UI.S(8), e.Bounds.Y, UI.S(32), e.Bounds.Height), sel ? UI.Blue : UI.Sub, TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);
        using (var f = UI.F(10.5f, FontStyle.Bold)) TextRenderer.DrawText(g, pl.Name, f, new Point(e.Bounds.X + UI.S(44), e.Bounds.Y + UI.S(5)), UI.Text);
        using (var f = UI.F(9)) TextRenderer.DrawText(g, pl.Detail, f, new Point(e.Bounds.X + UI.S(44), e.Bounds.Y + UI.S(25)), UI.Sub);
        if (e.Index < results.Items.Count - 1) using (var p = new Pen(UI.Line)) g.DrawLine(p, e.Bounds.X + UI.S(44), e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
    }

    void CheckUpdate(bool manual)
    {
        if (manual) { updLink.ForeColor = UI.Sub; updLink.Text = "Suche nach Updates …"; }
        ThreadPool.QueueUserWorkItem(_ =>
        {
            Updater.Info info = null; bool failed = false;
            try { info = Updater.Check(); } catch { failed = true; }
            try
            {
                BeginInvoke((Action)(() =>
                {
                    update = info;
                    if (info != null) { updLink.ForeColor = UI.Blue; updLink.Text = "Update " + info.Version + " verfügbar – jetzt installieren"; }
                    else if (manual) { updLink.ForeColor = failed ? UI.Red : UI.Green; updLink.Text = failed ? "Keine Verbindung zu GitHub" : "\u2713  Du hast die neueste Version (" + Installer.Version + ")"; }
                }));
            }
            catch { }
        });
    }

    void RunSearch()
    {
        string q = search.Text.Trim(); int id = ++searchId;
        status.ForeColor = UI.Sub; status.Text = "Suche …";
        ThreadPool.QueueUserWorkItem(_ =>
        {
            List<Program.Place> r = null; string err = null;
            try { r = Program.Search(q); } catch { err = "Keine Verbindung zum Wetterdienst – Internet prüfen."; }
            try
            {
                BeginInvoke((Action)(() =>
                {
                    if (id != searchId) return;
                    status.Text = err ?? "";
                    results.Items.Clear();
                    if (r != null) foreach (var p in r) results.Items.Add(p);
                    if (r != null && r.Count == 0) status.Text = "Nichts gefunden – andere Schreibweise probieren.";
                    results.Height = Math.Min(4, results.Items.Count) * results.ItemHeight;
                    results.Visible = results.Items.Count > 0;
                    if (results.Visible) results.SelectedIndex = 0;
                }));
            }
            catch { }
        });
    }

    void Pick(int i)
    {
        if (i < 0 || i >= results.Items.Count) return;
        picked = (Program.Place)results.Items[i];
        results.Visible = false;
        search.Text = ""; debounce.Stop();
        chosen.ForeColor = UI.Green;
        chosen.Text = "✓  " + picked.Name + (picked.Detail != "" ? "  ·  " + picked.Detail : "");
        status.Text = "";
        save.Focus();
    }

    void Save()
    {
        save.Enabled = false; status.ForeColor = UI.Sub; status.Text = running ? "Speichere …" : "Starte Widget …"; status.Refresh();
        try
        {
            if (picked != null)
            {
                Program.Set(Program.SkinIni, "Variables", "Lat", picked.Lat);
                Program.Set(Program.SkinIni, "Variables", "Lon", picked.Lon);
                Program.Set(Program.SkinIni, "Variables", "Ort", picked.Name);
            }
            Program.SetAutostart(tAuto.Checked);

            // Position/Vordergrund stehen in Rainmeter.ini -> Rainmeter dafuer kurz beenden
            bool wasRunning = Program.IsRunning();
            if (wasRunning) Program.Quit(true);
            Program.Set(Program.RmIni, "PCWidget", "AlwaysOnTop", tTop.Checked ? "2" : "-2");
            // 0 = oben links, 1 = oben rechts, 2 = unten links, 3 = unten rechts
            bool right = seg.Selected % 2 == 1, bottom = seg.Selected >= 2;
            string wx = right ? "20R" : "20", ax = right ? "100%" : "0%";
            string wy = bottom ? "68B" : "20", ay = bottom ? "100%" : "0%";   // unten: Platz fuer die Taskleiste
            Program.Set(Program.RmIni, "PCWidget", "WindowX", wx);
            Program.Set(Program.RmIni, "PCWidget", "AnchorX", ax);
            Program.Set(Program.RmIni, "PCWidget", "WindowY", wy);
            Program.Set(Program.RmIni, "PCWidget", "AnchorY", ay);
            // Unten: beim Aufklappen Unterkante festhalten -> Widget waechst nach oben
            Program.Set(Program.SkinIni, "Variables", "PosBang", bottom ? "[!SetWindowPosition " + wx + " " + wy + " " + ax + " " + ay + "]" : "");
            Program.SetSidebar(tSide.Checked, right);
            Program.WriteTabPosition(right, bottom);
            Program.MarkSetupDone();
            Program.Start();
            Close();
        }
        catch (Exception ex)
        {
            save.Enabled = true; status.ForeColor = UI.Red; status.Text = "Fehler: " + ex.Message;
        }
    }
}
