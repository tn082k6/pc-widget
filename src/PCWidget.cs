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
[assembly: System.Reflection.AssemblyVersion("1.1.0.0")]

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
        if (!File.Exists(RmExe) || !File.Exists(SkinIni))
        {
            MessageBox.Show("Der Ordner \"Rainmeter\" fehlt neben PC-Widget.exe.\nBitte den ganzen ZIP-Ordner entpacken, nicht nur die .exe.", "PC-Widget", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        bool autostart = args.Length > 0 && args[0] == "/autostart";
        bool running = IsRunning();
        if (autostart) { if (!running) Start(); return; }
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
    public static void Start() { Process.Start(new ProcessStartInfo(RmExe) { WorkingDirectory = RmDir, UseShellExecute = true }); }
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
    bool on; float anim; System.Windows.Forms.Timer t;
    public event EventHandler Changed;
    public bool Checked { get { return on; } set { on = value; anim = on ? 1 : 0; Invalidate(); } }
    public Toggle()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        Cursor = Cursors.Hand; Size = new Size(UI.S(51), UI.S(31));
        t = new System.Windows.Forms.Timer { Interval = 12 };
        t.Tick += (s, e) => { float target = on ? 1 : 0; anim += (target - anim) * 0.35f; if (Math.Abs(target - anim) < 0.02f) { anim = target; t.Stop(); } Invalidate(); };
    }
    protected override void OnClick(EventArgs e) { on = !on; t.Start(); if (Changed != null) Changed(this, e); base.OnClick(e); }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; UI.Hq(g); g.Clear(Parent.BackColor);
        Color off = Color.FromArgb(57, 57, 61);
        Color c = Color.FromArgb((int)(off.R + (UI.Green.R - off.R) * anim), (int)(off.G + (UI.Green.G - off.G) * anim), (int)(off.B + (UI.Green.B - off.B) * anim));
        float h = Height - 1, w = Width - 1;
        using (var p = UI.Round(new RectangleF(0, 0, w, h), h / 2)) using (var b = new SolidBrush(c)) g.FillPath(b, p);
        float k = h - 4; float x = 2 + (w - k - 4) * anim;
        using (var sh = new SolidBrush(Color.FromArgb(40, 0, 0, 0))) g.FillEllipse(sh, x, 3, k, k);
        using (var b = Brushes.White.Clone() as Brush) g.FillEllipse(b, x, 2, k, k);
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
    Toggle tAuto, tTop; Segmented seg; PillButton save;
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
        ClientSize = new Size(W, UI.S(600));
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
        settingsCard = new Rectangle(L, UI.S(250), CW, UI.S(124));
        tAuto = new Toggle { BackColor = UI.Card };
        tAuto.Location = new Point(settingsCard.Right - tAuto.Width - UI.S(14), settingsCard.Y + UI.S(15));
        tAuto.Checked = running ? Program.AutostartOn() : true;
        tTop = new Toggle { BackColor = UI.Card };
        tTop.Location = new Point(settingsCard.Right - tTop.Width - UI.S(14), settingsCard.Y + UI.S(77));
        tTop.Checked = Program.Get(Program.RmIni, "PCWidget", "AlwaysOnTop", "2") == "2";

        seg = new Segmented { Items = new[] { "↖  Oben links", "Oben rechts  ↗", "↙  Unten links", "Unten rechts  ↘" }, Cols = 2, Font = UI.F(10, FontStyle.Bold), Location = new Point(L, UI.S(414)), Size = new Size(CW, UI.S(76)) };
        bool right = Program.Get(Program.RmIni, "PCWidget", "AnchorX", "100%") != "0%";
        bool bottom = Program.Get(Program.RmIni, "PCWidget", "AnchorY", "0%") == "100%";
        seg.Selected = (bottom ? 2 : 0) + (right ? 1 : 0);

        // --- Knoepfe ---
        int by = ClientSize.Height - UI.S(66);
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

        Controls.AddRange(new Control[] { results, searchIcon, search, chosen, tAuto, tTop, seg, status, save });
        results.BringToFront();

        MouseDown += (s, e) => { if (e.Button == MouseButtons.Left && e.Y < UI.S(110)) { ReleaseCapture(); SendMessage(Handle, 0xA1, (IntPtr)2, IntPtr.Zero); } };
        KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape && !results.Visible) Close(); };
        Load += (s, e) => { var wa = Screen.FromPoint(Cursor.Position).WorkingArea; Location = new Point(wa.X + (wa.Width - Width) / 2, wa.Y + (wa.Height - Height) / 2); };
        Shown += (s, e) => { if (!running) search.Focus(); else ActiveControl = null; };
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
        Section(g, "POSITION AUF DEM HAUPTBILDSCHIRM", UI.S(390));
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
