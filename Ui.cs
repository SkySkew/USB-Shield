using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Management;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

// MainForm dipecah ikut topik supaya fail kod lebih mudah diurus.
public partial class MainForm : Form
{
    // ---------- UI ----------
    // satu warna aksen (biru) + neutral; butang berbahaya hanya berbingkai merah
    static readonly Dictionary<string, string[]> Styles = new Dictionary<string, string[]>() {
        { "primary", new string[] { "#0F766E", "#FFFFFF", "" } }, { "secondary", new string[] { "#FFFFFF", "#1F2937", "#D1D5DB" } },
        { "danger", new string[] { "#FFFFFF", "#C62828", "#E5B8B8" } }, { "info", new string[] { "#0F766E", "#FFFFFF", "" } },
        { "violet", new string[] { "#0F766E", "#FFFFFF", "" } }, { "teal", new string[] { "#FFFFFF", "#1F2937", "#D1D5DB" } },
        { "ghost", new string[] { "#146459", "#FFFFFF", "#2B8C80" } }
    };
    static RButton Btn(string t, int x, int y, int w, int h, string style, EventHandler click)
    {
        string[] s = Styles[style];
        RButton b = new RButton(); b.Text = t; b.Location = new Point(x, y); b.Size = new Size(w, h); b.Font = FReg;
        b.Fill = Col(s[0]); b.TextColor = Col(s[1]); if (s[2] != "") b.Border = Col(s[2]);
        if (click != null) b.Click += click;
        return b;
    }
    static Label Lbl(string t, int x, int y, int w, int h, bool bold, bool muted)
    {
        Label l = new Label(); l.Text = t; l.Location = new Point(x, y); l.AutoSize = false; l.Size = new Size(w, h);
        l.BackColor = Color.Transparent; l.ForeColor = Col(muted ? "#6B7280" : "#1F2937"); l.Font = bold ? FBold : FReg;
        return l;
    }
    static CardPanel Box(int x, int y, int w, int h)
    {
        CardPanel c = new CardPanel(); c.Location = new Point(x, y); c.Size = new Size(w, h); c.BackColor = Color.White; return c;
    }
    static CardPanel InfoCard(int y, string title, string desc) { return InfoCard(y, title, desc, 92); }
    static CardPanel InfoCard(int y, string title, string desc, int h) { return InfoCard(y, title, desc, h, null); }
    // glyph (Segoe MDL2) tidak null: ikon bulat di kiri kad, teks beralih ke kanan
    static CardPanel InfoCard(int y, string title, string desc, int h, string glyph)
    {
        CardPanel c = Box(20, y, 570, h);
        int ty = h < 90 ? 8 : 12, dy = h < 90 ? 34 : 40, x0 = glyph != null ? 62 : 18, w = glyph != null ? 288 : 330;
        Label t = Lbl(title, x0, ty, w, 26, false, false); t.Font = FCard;
        Label d = Lbl(desc, x0, dy, w, h - dy - 4, false, true); d.Anchor = TLR;
        c.Controls.Add(t); c.Controls.Add(d);
        if (glyph != null) { IconDot ic = new IconDot(); ic.Glyph = glyph; ic.SetBounds(14, (h - 40) / 2, 40, 40); c.Controls.Add(ic); }   // paling akhir: Controls[1] kekal label penerangan
        return c;
    }
    // tajuk halaman + penerangan ringkas di atas setiap halaman
    static void Head(Panel p, string title, string desc, int descH)
    {
        Label t = Lbl(title, 20, 0, 570, 30, false, false); t.Font = FHead; t.Anchor = TLR;
        Label d = Lbl(desc, 20, 34, 570, descH, false, true); d.Anchor = TLR;
        p.Controls.Add(t); p.Controls.Add(d);
    }
    const AnchorStyles TLR = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, TR = AnchorStyles.Top | AnchorStyles.Right,
        BL = AnchorStyles.Bottom | AnchorStyles.Left, BLR = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
        BR = AnchorStyles.Bottom | AnchorStyles.Right, ALL = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
    static T A<T>(T c, AnchorStyles a) where T : Control { c.Anchor = a; return c; }
    static ListViewItem MakeItem(string payload, string detail, params string[] cells)
    {
        ListViewItem it = new ListViewItem(cells[0]);
        for (int i = 1; i < cells.Length; i++) it.SubItems.Add(cells[i]);
        it.Tag = payload; it.ToolTipText = detail;
        return it;
    }
    static ListViewItem AddRow(ListView lv, string payload, string detail, params string[] cells)
    {
        ListViewItem it = MakeItem(payload, detail, cells); lv.Items.Add(it);
        return it;
    }
    // senarai baris besar: klik mana-mana bahagian baris untuk tanda, lajur akhir mengisi ruang, butiran penuh dipapar di bawah
    static ListView MakeList(CardPanel card, string[] cols, int[] widths, Label det)
    {
        ListView lv = new ListView(); lv.View = View.Details; lv.CheckBoxes = true; lv.FullRowSelect = true; lv.HideSelection = false; lv.MultiSelect = false;
        lv.BorderStyle = BorderStyle.None; lv.Dock = DockStyle.Fill; lv.BackColor = Color.White; lv.Font = FReg; lv.ShowItemToolTips = true;
        lv.SmallImageList = new ImageList(); lv.SmallImageList.ImageSize = new Size(1, 28);
        for (int i = 0; i < cols.Length; i++) lv.Columns.Add(cols[i], widths[i]);
        EventHandler fit = (s, e) =>
        {
            int w = lv.ClientSize.Width; for (int i = 0; i < lv.Columns.Count - 1; i++) w -= lv.Columns[i].Width;
            lv.Columns[lv.Columns.Count - 1].Width = Math.Max(120, w);
            Label emp = lv.Controls["empty"] as Label; if (emp != null) emp.SetBounds(12, 64, Math.Max(40, lv.ClientSize.Width - 24), 48);
        };
        lv.SizeChanged += fit;
        lv.EnabledChanged += (s, e) => lv.BackColor = lv.Enabled ? Color.White : Color.FromArgb(240, 240, 240);   // sama dengan warna ListView yang dinyahaktifkan
        lv.MouseUp += (s, e) =>
        {
            ListViewHitTestInfo h = lv.HitTest(e.Location);
            if (h.Item != null && h.Location != ListViewHitTestLocations.StateImage) h.Item.Checked = !h.Item.Checked;
        };
        lv.SelectedIndexChanged += (s, e) => { det.Text = lv.SelectedItems.Count > 0 ? lv.SelectedItems[0].ToolTipText.Replace("\n", "   ") : (string)det.Tag; };
        Label em = new Label(); em.Name = "empty"; em.AutoSize = false; em.TextAlign = ContentAlignment.TopCenter; em.ForeColor = Col("#6B7280");
        em.BackColor = Color.Transparent; em.Font = FReg; em.SetBounds(12, 64, 300, 48); lv.Controls.Add(em);
        card.Padding = new Padding(6, 6, 6, 6); card.Controls.Add(lv);
        return lv;
    }
    // papar mesej bila senarai kosong; jalankan juga pengemas kini butang (disimpan dalam Tag)
    void SetEmpty(ListView lv, string text)
    {
        Label l = lv.Controls["empty"] as Label;
        if (l != null) { if (!string.IsNullOrEmpty(text)) { l.Tag = text; l.Text = T(text); } l.Visible = lv.Items.Count == 0; }
        EventHandler upd = lv.Tag as EventHandler; if (upd != null) upd(lv, EventArgs.Empty);
    }
    // butang hanya aktif bila sekurang-kurangnya satu baris ditanda
    static void Bind(ListView lv, params RButton[] btns)
    {
        EventHandler upd = (s, e) => { foreach (RButton b in btns) b.Enabled = lv.CheckedItems.Count > 0; };
        lv.Tag = upd; lv.ItemChecked += (s, e) => upd(s, EventArgs.Empty); upd(lv, EventArgs.Empty);
    }
    // titik status berwarna di sebelah baris: 0 hijau (biasa), 1 jingga (semak), 2 merah (bahaya), 3 kelabu (mati), 4 biru (maklumat)
    static ImageList dotImgs;
    static ImageList Dots()
    {
        if (dotImgs != null) return dotImgs;
        ImageList il = new ImageList(); il.ColorDepth = ColorDepth.Depth32Bit; il.ImageSize = new Size(22, 28);
        foreach (string c in new string[] { "#16A34A", "#EA8A00", "#DC2626", "#9CA3AF", "#64748B" })
        {
            Bitmap b = new Bitmap(22, 28);
            using (Graphics g = Graphics.FromImage(b)) { g.SmoothingMode = SmoothingMode.AntiAlias; using (SolidBrush br = new SolidBrush(Col(c))) g.FillEllipse(br, 5, 9, 11, 11); }
            il.Images.Add(b);
        }
        return dotImgs = il;
    }
    const string Leg1 = "Merah: hampir pasti virus (sudah ditanda). Jingga: mencurigakan, semak dahulu.";
    const string Leg2 = "Hijau: biasa. Jingga: entri rosak. Merah: mencurigakan. Kelabu: dimatikan.";
    static Label Det(int y, int h) { Label l = Lbl(Hint, 20, y, 570, h, false, true); l.Tag = Hint; return l; }
    static Panel Page() { Panel p = new Panel(); p.Location = new Point(0, 76); p.Size = new Size(610, 400); p.Visible = false; return p; }

    // logo: perisai gradien hijau-teal dengan simbol USB putih, dilukis dengan kod
    public static Bitmap Logo(int px)
    {
        Bitmap bmp = new Bitmap(px, px);
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias; g.Clear(Color.Transparent); g.ScaleTransform(px / 256f, px / 256f);
            using (GraphicsPath sh = new GraphicsPath())
            {
                sh.AddBezier(128, 12, 158, 34, 194, 44, 226, 44); sh.AddLine(226, 44, 226, 120);
                sh.AddBezier(226, 120, 226, 176, 186, 218, 128, 244); sh.AddBezier(128, 244, 70, 218, 30, 176, 30, 120);
                sh.AddLine(30, 120, 30, 44); sh.AddBezier(30, 44, 62, 44, 98, 34, 128, 12); sh.CloseFigure();
                using (LinearGradientBrush br = new LinearGradientBrush(new Point(30, 12), new Point(226, 244), Col("#22C55E"), Col("#0D9488"))) g.FillPath(br, sh);
            }
            bool sm = px <= 20;   // ikon paling kecil: garisan lebih tebal, butiran lebih besar
            using (SolidBrush wb = new SolidBrush(Color.White))
            using (Pen pen = new Pen(Color.White, sm ? 20 : 12))
            {
                pen.StartCap = LineCap.Round; pen.EndCap = LineCap.Round; pen.LineJoin = LineJoin.Round;
                g.DrawLine(pen, 128, 66, 128, 172); g.FillEllipse(wb, sm ? 108 : 113, sm ? 166 : 171, sm ? 40 : 30, sm ? 40 : 30);
                g.FillPolygon(wb, sm ? new Point[] { new Point(100, 80), new Point(156, 80), new Point(128, 36) } : new Point[] { new Point(110, 76), new Point(146, 76), new Point(128, 44) });
                g.DrawLines(pen, new Point[] { new Point(128, 142), new Point(88, 114), new Point(88, 96) }); g.FillEllipse(wb, sm ? 70 : 76, sm ? 66 : 72, sm ? 36 : 24, sm ? 36 : 24);
                g.DrawLines(pen, new Point[] { new Point(128, 122), new Point(168, 96), new Point(168, 84) }); g.FillRectangle(wb, sm ? 150 : 157, sm ? 56 : 63, sm ? 36 : 22, sm ? 36 : 22);
            }
        }
        return bmp;
    }

    public MainForm()
    {
        AutoScaleDimensions = new SizeF(96F, 96F); AutoScaleMode = AutoScaleMode.Dpi;   // rekaan 96 DPI, diskalakan ikut skrin
        Text = "USB Shield"; ClientSize = new Size(800, 660); StartPosition = FormStartPosition.CenterScreen;
        Font = FReg; BackColor = Col("#F5F6F8"); FormBorderStyle = FormBorderStyle.Sizable; MaximizeBox = true; DoubleBuffered = true;
        using (Stream ico = typeof(MainForm).Assembly.GetManifestResourceStream("app.ico"))
            Icon = ico != null ? new Icon(ico) : Icon.FromHandle(Logo(48).GetHicon());   // ikon sama seperti fail .exe
        BuildUi();
        MinimumSize = Size;                       // saiz rekaan = saiz paling kecil
        ClientSize = new Size(880, 680);          // saiz asal lebih selesa; boleh dibesarkan lagi
        LoadSettings();
        FormClosing += (s, e) =>
        {
            SaveSettings();
            if (toTray && !exiting && e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); tray.ShowBalloonTip(2500, "USB Shield", T("Masih berjalan di dulang sistem."), ToolTipIcon.Info); }
        };
        FormClosed += (s, e) => { if (tray != null) tray.Dispose(); };
        tray = new NotifyIcon(); tray.Icon = Icon; tray.Text = "USB Shield"; tray.Visible = toTray;
        ContextMenuStrip tm = new ContextMenuStrip();
        tm.Items.Add(T("Buka USB Shield"), null, (s, e) => ShowFromTray());
        tm.Items.Add(T("Keluar"), null, (s, e) => { exiting = true; Close(); });
        tray.ContextMenuStrip = tm; tray.DoubleClick += (s, e) => ShowFromTray();
        Shown += (s, e) =>
        {
            if (guideSeen) return;
            guideSeen = true; SaveSettings();   // tunjuk sekali sahaja
            Msg(T("Selamat datang ke USB Shield!") + "\n\n" + T(GuideMs), "Selamat datang", MessageBoxButtons.OK, MessageBoxIcon.Information);
        };
        KeyPreview = true;
        KeyDown += (s, e) => { if (e.KeyCode == Keys.F11) ToggleMax(); else if (e.KeyCode == Keys.F5 && !busy) LoadDrives(); };
        devTimer.Interval = 1500; devTimer.Tick += (s, e) => { devTimer.Stop(); if (busy) return; LoadDrives(); OfferClean(); };
        ShowPage(11); Tips(); LoadDrives();
        Shown += (s, e) => UpdBg();
        if (savedLang == "en") SetLang("en");
    }
    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        if (m.Msg == 0x0219)   // WM_DEVICECHANGE: peranti dicucuk (0x8000) atau dicabut (0x8004)
        {
            long w = m.WParam.ToInt64();
            if (w == 0x8000 || w == 0x8004) { devTimer.Stop(); devTimer.Start(); }
        }
    }
    string SettingsPath { get { return Path.Combine(Path.Combine(Environment.GetEnvironmentVariable("APPDATA"), "USB Shield"), "settings.txt"); } }
    void LoadSettings()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return;
            int w = 0, h = 0; bool max = false;
            foreach (string l in File.ReadAllLines(SettingsPath))
            {
                int p = l.IndexOf('='); if (p < 1) continue;
                string k = l.Substring(0, p), v = l.Substring(p + 1);
                if (k == "lang" && (v == "ms" || v == "en")) savedLang = v;
                else if (k == "w") int.TryParse(v, out w);
                else if (k == "h") int.TryParse(v, out h);
                else if (k == "max") max = v == "1";
                else if (k == "guide") guideSeen = v == "1";
                else if (k == "auto") autoCheck = v != "0";
                else if (k == "avauto") avAuto = v == "1";
                                else if (k == "upd") { string[] u = v.Split(','); int n; if (u.Length == 2 && int.TryParse(u[1], out n)) { updDay = u[0]; updN = n; } }
            }
            if (chkAuto != null) chkAuto.Checked = autoCheck;
            if (chkTray != null) chkTray.Checked = toTray;
            if (chkAv != null) chkAv.Checked = avAuto;
            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            if (w >= MinimumSize.Width && h >= MinimumSize.Height && w <= wa.Width && h <= wa.Height) Size = new Size(w, h);
            if (max) WindowState = FormWindowState.Maximized;
        }
        catch { }   // tetapan rosak: guna nilai asal
    }
    void SaveSettings()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
            Size sz = WindowState == FormWindowState.Normal ? Size : RestoreBounds.Size;
            float k; using (Graphics g = CreateGraphics()) k = g.DpiX / 96f;
            File.WriteAllText(SettingsPath, "lang=" + lang + "\r\nw=" + (int)(sz.Width / k) + "\r\nh=" + (int)(sz.Height / k) + "\r\nmax=" + (WindowState == FormWindowState.Maximized ? 1 : 0) + "\r\nguide=" + (guideSeen ? 1 : 0) + "\r\nauto=" + (autoCheck ? 1 : 0) + "\r\navauto=" + (avAuto ? 1 : 0) + "\r\ntray=" + (toTray ? 1 : 0) + (updDay.Length > 0 ? "\r\nupd=" + updDay + "," + updN : "") + "\r\n");
        }
        catch { }
    }
    void ShowFromTray() { Show(); if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal; Activate(); }
    void ToggleMax() { WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized; }
    void ResetSize() { WindowState = FormWindowState.Normal; ClientSize = new Size(880, 680); }

    void BuildUi()
    {
        // sidebar
        Panel side = new Panel(); side.Size = new Size(190, 636); side.Dock = DockStyle.Left; side.BackColor = SideNav.Bg;   // ukuran rekaan dahulu supaya sauh bawah betul
        PictureBox logo = new PictureBox(); logo.Location = new Point(18, 20); logo.Size = new Size(40, 40); logo.SizeMode = PictureBoxSizeMode.Zoom;
        logo.BackColor = Color.Transparent; logo.Image = Logo(96);
        Label st = Lbl("USB Shield", 64, 26, 122, 32, false, false); st.ForeColor = Color.White; st.Font = FTitle;
        Label ss = Lbl("Pulihkan flashdisk, buang virus", 20, 68, 160, 30, false, false); ss.ForeColor = Col("#9FD5CC"); ss.Font = FSmall;
        sideNav = new SideNav(); sideNav.Location = new Point(0, 104); sideNav.Size = new Size(190, 448); sideNav.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Bottom; sideNav.Tr = T;
        BuildNav(); sideNav.ItemClicked += it => ShowPage(it.Page, it.Mode);
        Label ver = A(Lbl("Versi 2.0", 20, 604, 150, 20, false, false), BL); ver.Font = FSmall; ver.ForeColor = Col("#9FD5CC");
        side.Controls.Add(logo); side.Controls.Add(st); side.Controls.Add(ss); side.Controls.Add(ver); side.Controls.Add(sideNav);
        if (IsAdmin) { Label am = A(Lbl("Mod Administrator aktif", 20, 572, 160, 20, false, false), BL); am.Font = FSmall; am.ForeColor = Col("#7DD3A8"); side.Controls.Add(am); }
        else { bAdmin = A(Btn("Jalankan sebagai Admin", 10, 556, 170, 34, "ghost", (s, e) => ActElevate()), BL); side.Controls.Add(bAdmin); }

        // kandungan (rekaan pada 610 x 576; kawalan dilabuh supaya membesar bersama tetingkap)
        Panel main = new Panel(); main.Size = new Size(610, 636); main.Dock = DockStyle.Fill; main.BackColor = Col("#F5F6F8");   // ukuran rekaan dahulu supaya sauh betul
        topCard = A(Box(20, 6, 570, 56), TLR); CardPanel top = topCard;
        cb = A(new DriveSelect(), TLR); cb.Location = new Point(14, 22); cb.Size = new Size(420, 32); cb.Font = FReg;
        bRef = A(Btn("Muat semula", 446, 12, 110, 34, "secondary", (s, e) => LoadDrives()), TR);
        top.Controls.Add(Lbl("Drive flashdisk:", 16, 3, 300, 18, false, true)); top.Controls.Add(cb); top.Controls.Add(bRef);

        CardPanel logCard = A(Box(20, 60, 570, 290), ALL); logCard.BackColor = Col("#F9FAFB"); logCard.Padding = new Padding(12, 10, 6, 8);
        log = new TextBox(); log.Multiline = true; log.ReadOnly = true; log.ScrollBars = ScrollBars.Vertical; log.BorderStyle = BorderStyle.None;
        log.Dock = DockStyle.Fill; log.BackColor = Col("#F9FAFB"); log.ForeColor = Col("#374151"); log.Font = new Font("Consolas", 9f);
        logCard.Controls.Add(log);
        // logLbl dan bar tak dipaparkan lagi (skrin memuatkan menggantikannya); dikekalkan sebagai penyimpan status dan peratus
        logLbl = A(Lbl("Log", 20, 480, 300, 24, true, false), BL);
        bar = A(new BusyBar(), BL); bar.Location = new Point(330, 490); bar.Size = new Size(150, 4);
        bSave = A(Btn("Simpan log", 20, 358, 160, 34, "secondary", (s, e) => SaveLog()), BL);
        RButton bLogClr = A(Btn("Kosongkan log", 190, 358, 160, 34, "secondary", (s, e) => log.Clear()), BL);
        bCancel = A(Btn("Batal", 500, 478, 90, 28, "danger", (s, e) => cancelReq = true), BR); bCancel.Visible = false;
        Panel pLg = A(Page(), ALL);
        Head(pLg, "Log", "Rekod apa yang app lakukan. Boleh disimpan ke fail.", 22);
        pLg.Controls.AddRange(new Control[] { logCard, bSave, bLogClr });

        // halaman 1: Pembersih
        Panel p1 = A(Page(), ALL);
        CardPanel hero = A(Box(20, 0, 570, 92), TLR);
        bAll = A(Btn("Bersihkan Semua", 340, 22, 214, 48, "primary", (s, e) => ActAll()), TR); bAll.Font = FBig;
        Label ht = Lbl("Bersihkan flashdisk", 20, 14, 310, 30, false, false); ht.Font = FHead;
        hero.Controls.Add(ht); hero.Controls.Add(A(Lbl("Defender, pulihkan, kuarantin, vaksin dan semak PC sekali gus (disyorkan).", 20, 46, 310, 40, false, true), TLR)); hero.Controls.Add(bAll);
        b1 = Btn("Pulihkan fail tersembunyi", 0, 0, 10, 10, "secondary", (s, e) => Act1());
        b2 = Btn("Kuarantin .lnk && autorun", 0, 0, 10, 10, "secondary", (s, e) => Act2());
        b3 = Btn("Imbas fail mencurigakan", 0, 0, 10, 10, "secondary", (s, e) => Act3());
        bV = Btn("Vaksin flashdisk", 0, 0, 10, 10, "secondary", (s, e) => ActV());
        TableLayoutPanel grid = A(new TableLayoutPanel(), TLR); grid.Location = new Point(20, 100); grid.Size = new Size(570, 80);
        grid.ColumnCount = 2; grid.RowCount = 2;
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50)); grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        RButton[] gb = { b1, b2, b3, bV };
        for (int i = 0; i < 4; i++) { gb[i].Dock = DockStyle.Fill; gb[i].Margin = new Padding(i % 2 == 0 ? 0 : 4, i < 2 ? 0 : 2, i % 2 == 0 ? 4 : 0, i < 2 ? 2 : 0); grid.Controls.Add(gb[i], i % 2, i / 2); }
        CardPanel listCard = A(Box(20, 212, 570, 112), ALL); det1 = A(Det(328, 30), BLR);
        list = MakeList(listCard, new string[] { "Fail", "Sebab", "Lokasi" }, new int[] { 170, 250, 150 }, det1); list.SmallImageList = Dots();
        bDel = A(Btn("Kuarantin yang ditanda", 20, 364, 220, 34, "danger", (s, e) => DoChecked(list, p => Quar(p), "Alihkan fail ini ke kuarantin? Anda boleh memulihkannya kemudian.")), BL);
        p1.Controls.AddRange(new Control[] { hero, grid, Lbl("Fail mencurigakan di flashdisk (virus yang pasti sudah ditanda):", 20, 188, 570, 22, true, false), listCard, det1, bDel });

        // halaman 2: Imbas Virus
        Panel p2 = A(Page(), ALL);
        Head(p2, "Imbas virus", "Pilih pengimbas untuk memeriksa drive yang dipilih di atas.", 22);
        CardPanel c1 = A(InfoCard(60, "Microsoft Defender", "Imbas dan rawat ancaman secara automatik. Kemas kini definisi dahulu (perlu internet).", 80, "\uE83D"), TLR);
        b4 = A(Btn("Imbas drive", 360, 6, 190, 32, "violet", (s, e) => Act4()), TR); b6 = A(Btn("Kemas kini definisi", 360, 42, 190, 32, "secondary", (s, e) => Act6()), TR);
        c1.Controls.Add(b4); c1.Controls.Add(b6);
        CardPanel c4 = A(InfoCard(146, "ClamAV (luar talian)", "", 80, "\uEBE8"), TLR);
        clamDesc = (Label)c4.Controls[1]; RefreshClam();
        bClam = A(Btn("Imbas dengan ClamAV", 360, 6, 190, 32, "secondary", (s, e) => ActClam()), TR);
        bClamDb = A(Btn("Import pangkalan data", 360, 42, 190, 32, "secondary", (s, e) => ActClamDb()), TR); c4.Controls.Add(bClam); c4.Controls.Add(bClamDb);
        CardPanel c2 = A(InfoCard(232, "Malwarebytes", "Buka Malwarebytes (atau halaman muat turun rasmi). Pilih imbasan Custom dan drive anda.", 80, "\uE7B3"), TLR);
        b7 = A(Btn("Buka Malwarebytes", 360, 20, 190, 40, "teal", (s, e) => Act7()), TR); c2.Controls.Add(b7);
        CardPanel c3 = A(InfoCard(318, "ESET Online Scanner", "Buka ESET Online Scanner (atau halaman muat turun rasmi). Pilih imbasan Custom dan drive anda.", 80, "\uE774"), TLR);
        b8 = A(Btn("Buka ESET Online Scanner", 360, 20, 190, 40, "teal", (s, e) => Act8()), TR); c3.Controls.Add(b8);
        p2.Controls.AddRange(new Control[] { c1, c4, c2, c3 });

        // halaman 3: Bersihkan PC berkategori (Analisis dahulu, kemudian Bersihkan)
        Panel pS = A(Page(), ALL);
        Head(pS, "Pembersih Tersuai", "Tanda apa yang mahu dibersihkan. Tekan Analisis untuk melihat saiz, kemudian Bersihkan.", 22);
        CardPanel lc = A(Box(20, 60, 570, 236), ALL); detC = A(Det(300, 20), BLR);
        listC = MakeList(lc, new string[] { "Kategori", "Saiz", "Catatan" }, new int[] { 240, 90, 200 }, detC); listC.SmallImageList.ImageSize = new Size(1, 26);
        bCAn = A(Btn("Analisis", 20, 330, 150, 34, "secondary", (s, e) => ActCAnalyze()), BL);
        bCRun = A(Btn("Bersihkan", 180, 330, 150, 34, "primary", (s, e) => ActCClean()), BL);
        lblCTot = A(Lbl("", 340, 337, 250, 22, true, false), BLR); lblCTot.TextAlign = ContentAlignment.MiddleRight;
        BuildCats(); listC.ShowGroups = true; listC.ItemChecked += (s, e) => CTotal();
        Bind(listC, bCRun); CRefreshRows();
        pS.Controls.AddRange(new Control[] { lc, detC, bCAn, bCRun, lblCTot });

        // halaman 4: Ruang Cakera (pencari fail dan folder terbesar, hanya membaca)
        Panel pD = A(Page(), ALL);
        Head(pD, "Penganalisis Cakera", "Cari fail dan folder terbesar supaya anda boleh membebaskan ruang.", 22);
        dTitle = (Label)pD.Controls[0]; dDesc = (Label)pD.Controls[1];
        CardPanel pathCard = A(Box(20, 60, 570, 46), TLR);
        scanPath = A(Lbl(scanRoot, 14, 12, 410, 22, false, false), TLR); scanPath.Tag = scanRoot; scanPath.AutoEllipsis = true;
        bPick = A(Btn("Pilih folder...", 442, 6, 118, 34, "secondary", (s, e) => ActPickFolder()), TR);
        pathCard.Controls.Add(scanPath); pathCard.Controls.Add(bPick);
        bFold = Btn("Cari folder terbesar", 0, 0, 10, 10, "primary", (s, e) => ActFolders());
        bFile = Btn("Cari fail terbesar", 0, 0, 10, 10, "secondary", (s, e) => ActFiles());
        bDup = Btn("Cari fail pendua", 0, 0, 10, 10, "secondary", (s, e) => ActDups());
        TableLayoutPanel g2 = A(new TableLayoutPanel(), TLR); g2d = g2; g2.Location = new Point(20, 114); g2.Size = new Size(570, 38);
        g2.ColumnCount = 3; g2.RowCount = 1;
        for (int ci = 0; ci < 3; ci++) g2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
        g2.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        bFold.Dock = DockStyle.Fill; bFold.Margin = new Padding(0, 0, 4, 0); bFile.Dock = DockStyle.Fill; bFile.Margin = new Padding(4, 0, 4, 0); bDup.Dock = DockStyle.Fill; bDup.Margin = new Padding(4, 0, 0, 0);
        g2.Controls.Add(bFold, 0, 0); g2.Controls.Add(bFile, 1, 0); g2.Controls.Add(bDup, 2, 0);
        CardPanel ld = A(Box(20, 162, 570, 164), ALL); detD = A(Det(330, 30), BLR);
        listD = MakeList(ld, new string[] { "Nama", "Saiz", "Lokasi" }, new int[] { 230, 90, 250 }, detD); listD.CheckBoxes = false;
        bOpenD = A(Btn("Buka lokasi", 20, 364, 160, 34, "secondary", (s, e) => ActOpenD()), BL);
        bDupDel = A(Btn("Pindah yang ditanda ke Recycle Bin", 190, 364, 290, 34, "danger", (s, e) => ActDupDel()), BL); bDupDel.Visible = false;
        listD.ItemChecked += (s, e) => bDupDel.Enabled = dupMode && listD.CheckedItems.Count > 0;
        pD.Controls.AddRange(new Control[] { pathCard, g2, ld, detD, bOpenD, bDupDel });

        // halaman 4: Kuarantin
        Panel pQ = A(Page(), ALL);
        Head(pQ, "Kuarantin", "Fail yang dialihkan dari flashdisk disimpan di PC ini dan dinamakan semula .kuarantin supaya tak boleh dijalankan. Pulihkan hanya jika anda pasti fail itu selamat.", 40);
        CardPanel lq = A(Box(20, 80, 570, 246), ALL); detQ = A(Det(330, 30), BLR);
        listQ = MakeList(lq, new string[] { "Fail", "ID", "Lokasi asal" }, new int[] { 200, 80, 250 }, detQ);
        bQR = A(Btn("Pulihkan yang ditanda", 20, 364, 180, 34, "info", (s, e) => { DoChecked(listQ, i => QRestore(i), "Pulihkan fail ini ke lokasi asal? Jika ia virus, ia akan aktif semula."); QLoad(); }), BL);
        bQD = A(Btn("Padam kekal yang ditanda", 210, 364, 200, 34, "danger", (s, e) => { DoChecked(listQ, i => QDelete(i), "Padam kekal fail kuarantin ini? Ia tak boleh dipulihkan."); QLoad(); }), BL);
        bQO = A(Btn("Buka folder kuarantin", 420, 364, 170, 34, "secondary", (s, e) => ActOpenQ()), BL);
        pQ.Controls.AddRange(new Control[] { lq, detQ, bQR, bQD, bQO });

        // halaman 5: Lanjutan (senarai permulaan PC)
        Panel p3 = A(Page(), ALL);
        Head(p3, "Permulaan PC", "Program yang bermula sendiri dengan Windows. Boleh dimatikan dan dihidupkan semula.", 22);
        b5 = Btn("Muat semula senarai", 20, 62, 200, 34, "violet", (s, e) => ActStartAll());
        CardPanel l2 = A(Box(20, 106, 570, 200), ALL); det2 = A(Det(310, 32), BLR);
        list2 = MakeList(l2, new string[] { "Program", "Status", "Penerbit / catatan", "Perintah" }, new int[] { 150, 90, 200, 250 }, det2); list2.SmallImageList = Dots();
        bDel2 = A(Btn("Matikan ditanda", 20, 352, 170, 34, "danger", (s, e) => ActDisable()), BL);
        bLoc = A(Btn("Buka lokasi fail", 380, 352, 210, 34, "secondary", (s, e) => ActOpenLoc()), BL);
        bRst = A(Btn("Hidupkan ditanda", 200, 352, 170, 34, "info", (s, e) => ActEnable()), BL);
        p3.Controls.AddRange(new Control[] { b5, l2, det2, bDel2, bLoc, bRst });

        SetEmpty(list, "Belum diimbas. Klik Imbas fail mencurigakan untuk menyemak flashdisk.");
        SetEmpty(list2, "Klik Muat semula senarai untuk melihat program permulaan.");
        SetEmpty(listQ, "Tiada fail dalam kuarantin.");
        SetEmpty(listD, "Pilih folder, kemudian klik Cari folder terbesar, Cari fail terbesar atau Cari fail pendua.");
        bOpenD.Enabled = false; listD.SelectedIndexChanged += (s, e) => bOpenD.Enabled = listD.SelectedItems.Count > 0;
        Bind(list, bDel); Bind(list2, bDel2, bRst); Bind(listQ, bQR, bQD);
        bLoc.Enabled = false; list2.SelectedIndexChanged += (s, e) => bLoc.Enabled = list2.SelectedItems.Count > 0;
        mainPanel = main; main.Controls.AddRange(new Control[] { top, p1, p2, pS, pD, pQ, p3, pLg });

        // halaman 6: Sejarah imbasan
        Panel pH = A(Page(), ALL);
        Head(pH, "Sejarah imbasan", "Rekod imbasan dan pembersihan yang pernah dijalankan di PC ini.", 22);
        CardPanel lh = A(Box(20, 60, 570, 266), ALL); detH = A(Det(330, 30), BLR);
        listH = MakeList(lh, new string[] { "Tarikh", "Drive", "Jenis", "Hasil" }, new int[] { 130, 60, 150, 230 }, detH); listH.CheckBoxes = false;
        bHClear = A(Btn("Kosongkan sejarah", 20, 364, 180, 34, "danger", (s, e) => HClear()), BL);
        pH.Controls.AddRange(new Control[] { lh, detH, bHClear });
        SetEmpty(listH, "Belum ada sejarah. Rekod muncul selepas imbasan atau pembersihan.");
        // halaman 7: Prestasi
        Panel pP = A(Page(), ALL);
        Head(pP, "Prestasi PC", "Program yang makan RAM, kesihatan cakera dan tetapan prestasi (boleh dipulihkan).", 22);
        CardPanel lp = A(Box(20, 60, 570, 156), ALL); detP = A(Det(220, 20), BLR);
        listP = MakeList(lp, new string[] { "Program", "RAM", "CPU" }, new int[] { 260, 100, 80 }, detP);
        bPRef = A(Btn("Muat semula senarai", 20, 246, 170, 34, "primary", (s, e) => ActProcs()), BL);
        bPKill = A(Btn("Tutup program ditanda", 200, 246, 190, 34, "danger", (s, e) => ActKillProcs()), BL);
        bDiskH = A(Btn("Kesihatan cakera", 400, 246, 190, 34, "secondary", (s, e) => ActDiskHealth()), BL);
        CardPanel fx = A(Box(20, 292, 570, 98), BLR);
        Label ft = Lbl("Tetapan prestasi", 66, 8, 200, 26, false, false); ft.Font = FCard;
        Label fd = Lbl("Pelan kuasa dan kesan visual Windows. Boleh dipulihkan.", 66, 36, 196, 54, false, true);
        IconDot fxIc = new IconDot(); fxIc.Glyph = "\uE9E9"; fxIc.SetBounds(14, 29, 40, 40);
        bPow = Btn("Mod prestasi", 270, 10, 135, 34, "secondary", (s, e) => ActPowerHigh());
        bPowOff = Btn("Pelan kuasa asal", 415, 10, 140, 34, "secondary", (s, e) => ActPowerBack());
        bFx = Btn("Kurangkan visual", 270, 52, 135, 34, "secondary", (s, e) => ActFxOff());
        bFxOff = Btn("Pulihkan visual", 415, 52, 140, 34, "secondary", (s, e) => ActFxBack());
        fx.Controls.AddRange(new Control[] { ft, fd, bPow, bPowOff, bFx, bFxOff, fxIc });
        pP.Controls.AddRange(new Control[] { lp, detP, bPRef, bPKill, bDiskH, fx });
        SetEmpty(listP, "Klik Muat semula senarai untuk melihat program yang makan RAM.");
        Bind(listP, bPKill);
        // halaman 8: Program (dipasang, nyahpasang, kemas kini winget)
        Panel pPr = A(Page(), ALL);
        Head(pPr, "Kemas Kini Perisian", "Semak kemas kini program melalui winget, kemudian tanda yang mahu dikemas kini.", 22);
        prTitle = (Label)pPr.Controls[0]; prDesc = (Label)pPr.Controls[1];
        CardPanel lpr = A(Box(20, 60, 570, 196), ALL); detPr = A(Det(260, 20), BLR);
        listPr = MakeList(lpr, new string[] { "Program", "Versi", "Penerbit", "Saiz" }, new int[] { 210, 80, 170, 80 }, detPr); listPr.SmallImageList = Dots();
        bPrAll = A(Btn("Tanda semua", 236, 1, 110, 28, "secondary", (s, e) => PrToggleAll()), TR);
        Label prFind = A(Lbl("Cari:", 358, 5, 40, 22, false, true), TR);
        txtPr = A(new TextBox(), TR); txtPr.Font = FReg; txtPr.SetBounds(400, 2, 190, 26); txtPr.TextChanged += (s, e) => PrFilter();
        bPrList = A(Btn("Muat semula senarai", 20, 286, 280, 34, "secondary", (s, e) => ActProgList()), BL);
        bPrUpd = A(Btn("Semak kemas kini", 20, 286, 280, 34, "primary", (s, e) => ActProgUpd()), BL);
        bPrDo = A(Btn("Kemas kini ditanda", 310, 286, 280, 34, "info", (s, e) => ActProgDo()), BL);
        bPrUn = A(Btn("Nyahpasang ditanda", 310, 286, 280, 34, "danger", (s, e) => ActProgUn()), BL);
        prNote = A(Lbl("", 20, 330, 570, 40, false, true), BLR); prNote.Font = FSmall;
        pPr.Controls.AddRange(new Control[] { lpr, detPr, bPrList, bPrUpd, bPrDo, bPrUn, prNote, bPrAll, prFind, txtPr });
        SetEmpty(listPr, "Klik Senarai program dipasang atau Semak kemas kini.");
        Bind(listPr, bPrDo, bPrUn);
        // halaman 9: Maklumat PC (spesifikasi dan kesihatan cakera)
        Panel pI = A(Page(), ALL);
        Head(pI, "Maklumat PC", "Spesifikasi PC dan kesihatan cakera. Hanya membaca, tiada apa diubah.", 22);
        CardPanel li = A(Box(20, 60, 570, 266), ALL);
        pnlSpec = new Panel(); pnlSpec.Dock = DockStyle.Fill; pnlSpec.AutoScroll = true; pnlSpec.BackColor = Color.White;
        canvas = new SpecCanvas(); pnlSpec.Controls.Add(canvas); pnlSpec.MouseEnter += (s, e) => pnlSpec.Focus(); canvas.MouseEnter += (s, e) => pnlSpec.Focus();
        li.Padding = new Padding(8); li.Controls.Add(pnlSpec);
        specNote = A(Lbl("", 20, 331, 570, 22, false, true), BLR); specNote.Font = FSmall;
        bSpecRef = A(Btn("Muat semula", 20, 358, 190, 34, "primary", (s, e) => ActSpec()), BL);
        bSpecCopy = A(Btn("Salin ke papan klip", 220, 358, 190, 34, "secondary", (s, e) => ActSpecCopy()), BL);
        pI.Controls.AddRange(new Control[] { li, specNote, bSpecRef, bSpecCopy });
        // halaman 10: Padam Selamat (timpa ruang kosong)
        Panel pW = A(Page(), ALL);
        Head(pW, "Padam ruang kosong", "Timpa ruang kosong supaya fail yang sudah dipadam tak boleh dipulihkan.", 22);
        CardPanel wc = A(InfoCard(58, "Bagaimana ia berfungsi", "Bila fail dipadam, Windows hanya menandanya kosong dan program khas masih boleh memulihkannya. Ciri ini menulis data ke seluruh ruang kosong, kemudian membuangnya. Fail sedia ada tak disentuh.", 100, "\uE75C"), TLR);
        wc.Controls[1].Width = 490; wc.Controls[1].Anchor = TLR;
        CardPanel wo = A(Box(20, 166, 570, 96), TLR);
        rbW1 = new RadioButton(); rbW1.Text = "Pantas: 1 pas data rawak (disyorkan)"; rbW1.Checked = true; rbW1.SetBounds(18, 10, 530, 24); rbW1.Font = FReg; rbW1.BackColor = Color.Transparent;
        rbW2 = new RadioButton(); rbW2.Text = "Teliti: 3 pas (sifar, satu, rawak), jauh lebih lama"; rbW2.SetBounds(18, 38, 530, 24); rbW2.Font = FReg; rbW2.BackColor = Color.Transparent;
        Label wn = Lbl("Pada SSD, penimpaan tak menjamin semua data lama hilang. Pilih drive di atas.", 18, 66, 540, 22, false, true); wn.Font = FSmall;
        wo.Controls.AddRange(new Control[] { rbW1, rbW2, wn });
        bWipe = A(Btn("Mula padam ruang kosong", 20, 274, 300, 40, "primary", (s, e) => ActWipe()), TLR);
        Label wnote = A(Lbl("Drive sistem (C:) tak ditunjukkan. Boleh dibatalkan bila-bila masa.", 20, 320, 570, 22, false, true), TLR); wnote.Font = FSmall;
        pW.Controls.AddRange(new Control[] { wc, wo, bWipe, wnote });
        // halaman 11: Semak Kesihatan (halaman utama)
        Panel pHc = A(Page(), ALL);
        CardPanel hcHero = A(Box(20, 0, 570, 120), TLR);
        hcDot = new IconDot(); hcDot.Glyph = ""; hcDot.SetBounds(24, 22, 76, 76);
        Label hcT = Lbl("Semak Kesihatan PC", 116, 22, 300, 30, false, false); hcT.Font = FHead;
        hcSub = Lbl("Semak ruang, kelajuan, keselamatan dan cakera dalam satu klik.", 116, 60, 270, 44, false, true);
        bHcRun = A(Btn("Mula semak", 396, 16, 158, 52, "primary", (s, e) => ActHealth()), TR); bHcRun.Font = FBig;
        RButton bRep = A(Btn("Simpan laporan", 396, 76, 158, 30, "secondary", (s, e) => ActReport()), TR);
        hcHero.Controls.AddRange(new Control[] { hcT, hcSub, bHcRun, bRep, hcDot });
        pHc.Controls.Add(hcHero);
        string[][] hcDef = new string[][] {
            new string[] { "Ruang dan Sampah", "Belum disemak. Tekan Mula semak.", "", "Bersihkan" },
            new string[] { "Kelajuan", "Belum disemak. Tekan Mula semak.", "", "Urus permulaan" },
            new string[] { "Keselamatan", "Belum disemak. Tekan Mula semak.", "", "Imbas virus" },
            new string[] { "Kesihatan Cakera", "Belum disemak. Tekan Mula semak.", "", "Lihat butiran" },
            new string[] { "Kemas Kini Program", "Belum disemak. Tekan Mula semak.", "", "Lihat kemas kini" } };
        for (int hi = 0; hi < 5; hi++)
        {
            int idx = hi; string[] d = hcDef[hi];
            CardPanel hc = A(InfoCard(132 + hi * 78, d[0], d[1], 72, d[2]), TLR);
            RButton hb = A(Btn(d[3], 396, 20, 158, 32, "secondary", (s, e) => { if (idx == 0) { ShowPage(2); ActCAnalyze(); } else if (idx == 1) ShowPage(5); else if (idx == 2) ShowPage(1); else if (idx == 3) ShowPage(9); else ShowPage(8, 0); }), TR); hb.Visible = false;
            hc.Controls.Add(hb); hcCards[hi] = hc; pHc.Controls.Add(hc);
        }
        // halaman 12: Pilihan
        Panel pOp = A(Page(), ALL);
        Head(pOp, "Pilihan", "Bahasa dan tetapan USB Shield.", 22);
        CardPanel o1 = A(InfoCard(60, "Bahasa", "Pilih bahasa paparan.", 56, ""), TLR);
        o1.Controls.Add(A(Btn("Bahasa Melayu", 290, 12, 150, 32, "secondary", (s, e) => SetLang("ms")), TR)); o1.Controls.Add(A(Btn("English", 450, 12, 100, 32, "secondary", (s, e) => SetLang("en")), TR));
        CardPanel o2 = A(InfoCard(122, "Semak flashdisk automatik", "Periksa flashdisk sebaik dicucuk dan tanya hanya jika ada tanda virus pintasan.", 76, ""), TLR);
        chkAuto = A(new CheckBox(), TR); chkAuto.Text = "Hidupkan"; chkAuto.Font = FReg; chkAuto.Checked = autoCheck; chkAuto.BackColor = Color.Transparent; chkAuto.SetBounds(360, 24, 190, 28);
        chkAuto.CheckedChanged += (s, e) => { autoCheck = chkAuto.Checked; SaveSettings(); }; o2.Controls.Add(chkAuto);
        CardPanel o2b = A(InfoCard(206, "Imbas antivirus juga", "Selepas semakan cepat, imbas flashdisk dengan Defender (atau ClamAV jika tiada). Lebih lama; boleh dibatalkan.", 76, ""), TLR);
        chkAv = A(new CheckBox(), TR); chkAv.Text = "Hidupkan"; chkAv.Font = FReg; chkAv.BackColor = Color.Transparent; chkAv.SetBounds(360, 24, 190, 28); chkAv.Checked = avAuto;
        chkAv.CheckedChanged += (s, e) => { avAuto = chkAv.Checked; SaveSettings(); }; o2b.Controls.Add(chkAv);
        CardPanel o4 = A(InfoCard(290, "Keselamatan USB (akaun ini)", "Halang flashdisk dibuka atau dijalankan sendiri oleh Windows.", 80, ""), TLR);
        bAP1 = A(Btn("Matikan AutoPlay/AutoRun", 360, 6, 190, 32, "secondary", (s, e) => AutoOff()), TR); bAP2 = A(Btn("Pulihkan tetapan asal", 360, 42, 190, 32, "secondary", (s, e) => AutoOn()), TR);
        o4.Controls.Add(bAP1); o4.Controls.Add(bAP2);
        CardPanel o3 = A(InfoCard(378, "Ikon di dulang sistem", "Bila tetingkap ditutup, app terus berjalan di dulang sistem. Klik dua kali ikon untuk membukanya.", 76, ""), TLR);
        chkTray = A(new CheckBox(), TR); chkTray.Text = "Hidupkan"; chkTray.Font = FReg; chkTray.BackColor = Color.Transparent; chkTray.SetBounds(360, 24, 190, 28); chkTray.Checked = toTray;
        chkTray.CheckedChanged += (s, e) => { toTray = chkTray.Checked; if (tray != null) tray.Visible = toTray; SaveSettings(); }; o3.Controls.Add(chkTray);
        CardPanel o5 = A(InfoCard(462, "Bantuan", "Panduan dan maklumat app.", 56, ""), TLR);
        o5.Controls.Add(A(Btn("Panduan", 290, 12, 150, 32, "secondary", (s, e) => Msg(GuideMs, "Panduan", MessageBoxButtons.OK, MessageBoxIcon.None)), TR)); o5.Controls.Add(A(Btn("Tentang", 450, 12, 100, 32, "secondary", (s, e) => Msg(AboutMs, "Tentang", MessageBoxButtons.OK, MessageBoxIcon.None)), TR));
        foreach (CardPanel oc in new CardPanel[] { o1, o5 }) { Label od = (Label)oc.Controls[1]; od.Anchor = AnchorStyles.Top | AnchorStyles.Left; od.Width = 215; }   // butang bermula x=290
        pOp.Controls.AddRange(new Control[] { o1, o2, o2b, o4, o3, o5 });
        pages = new Panel[] { p1, p2, pS, pD, pQ, p3, pH, pP, pPr, pI, pW, pHc, pOp, pLg };
        ov = A(new LoadingOverlay(), ALL); ov.Location = new Point(0, 0); ov.Size = new Size(610, 476); ov.Bar = bar; ov.Tr = T; ov.LogoImg = Logo(96); ov.CancelClicked += (s, e) => cancelReq = true;
        main.Controls.Add(pH); main.Controls.Add(pP); main.Controls.Add(pPr); main.Controls.Add(pI); main.Controls.Add(pW); main.Controls.Add(pHc); main.Controls.Add(pOp); main.Controls.Add(ov); ov.BringToFront();

        Controls.Add(main); Controls.Add(side);
    }
    static ToolStripMenuItem MakeMenu(string name, params ToolStripItem[] items) { ToolStripMenuItem m = new ToolStripMenuItem(name); m.DropDownItems.AddRange(items); return m; }
    static ToolStripMenuItem Item(string text, Action a) { ToolStripMenuItem i = new ToolStripMenuItem(text); i.Click += (s, e) => a(); return i; }

    // ---------- menu kiri berkumpulan, mod halaman dan Semak Kesihatan ----------
    SideItem NI(string text, string glyph, int page, int mode) { SideItem i = new SideItem(); i.Text = text; i.Glyph = glyph; i.Page = page; i.Mode = mode; return i; }
    void BuildNav()
    {
        SideItem tools = NI("Alat", "", -1, 0), usb = NI("Flashdisk", "", -1, 0);
        tools.Kids.AddRange(new SideItem[] { NI("Nyahpasang", "", 8, 1), NI("Permulaan", "", 5, 0), NI("Penganalisis Cakera", "", 3, 0), NI("Fail Pendua", "", 3, 1), NI("Padam Selamat", "", 10, 0), NI("Maklumat PC", "", 9, 0) });
        usb.Kids.AddRange(new SideItem[] { NI("Pembersih Flashdisk", "", 0, 0), NI("Imbas Virus", "", 1, 0), NI("Kuarantin", "", 4, 0), NI("Sejarah", "", 6, 0) });
        foreach (SideItem k in tools.Kids) k.Parent = tools;
        foreach (SideItem k in usb.Kids) k.Parent = usb;
        sideNav.Items.AddRange(new SideItem[] { NI("Semak Kesihatan", "", 11, 0), NI("Pembersih Tersuai", "", 2, 0), NI("Prestasi", "", 7, 0), NI("Kemas Kini Perisian", "", 8, 0), tools, usb, NI("Log", "", 13, 0), NI("Pilihan", "", 12, 0) });
    }
    CheckBox chkAuto;
    void SetT(Label l, string ms) { l.Tag = ms; l.Text = T(ms); }
    // halaman yang berkongsi satu panel (Program, Ruang Cakera) dipaparkan mengikut mod yang dipilih dalam menu
    Label prTitle, prDesc, prNote, dTitle, dDesc; TableLayoutPanel g2d; int diskMode = -1;
    List<ListViewItem> prInst = new List<ListViewItem>(), prUpd = new List<ListViewItem>(); bool prInstLoaded, prUpdTried; int prKind;
    void ApplyMode(int i, int mode)
    {
        if (i == 8)
        {
            bool upd = mode == 0;
            SetT(prTitle, upd ? "Kemas Kini Perisian" : "Nyahpasang Program");
            SetT(prDesc, upd ? "Semak kemas kini program melalui winget, kemudian tanda yang mahu dikemas kini." : "Program dipasang (terbesar dahulu). Tanda program yang mahu dibuang.");
            SetT(prNote, upd ? "Guna winget (Microsoft): hanya di Windows 10/11 dan perlu internet. Sesetengah kemas kini perlu program itu ditutup atau mod Administrator."
                             : "Nyahpasang membuka pemasang nyahpasang program itu sendiri; ikut arahannya.");
            bPrUpd.Visible = bPrDo.Visible = upd; bPrList.Visible = bPrUn.Visible = !upd;
            prAll = upd ? prUpd : prInst; prKind = upd ? 2 : 1; PrCol(upd ? "Versi baharu" : "Penerbit");
            prEmpty = upd ? "Klik Semak kemas kini." : "Klik Muat semula senarai.";
            if (!busy)
            {
                if (upd && !prUpdTried) { prUpdTried = true; if (Winget() != null) ActProgUpd(); else prEmpty = NoWinget; }
                else if (!upd && !prInstLoaded) ProgLoad();
            }
            PrFilter();
        }
        else if (i == 3 && diskMode != mode)
        {
            bool dup = mode == 1; diskMode = mode;
            SetT(dTitle, dup ? "Fail Pendua" : "Penganalisis Cakera");
            SetT(dDesc, dup ? "Cari fail sama kandungan (1 MB ke atas). Satu salinan kekal, yang lain ke Recycle Bin." : "Cari fail dan folder terbesar supaya anda boleh membebaskan ruang.");
            bFold.Visible = bFile.Visible = !dup; bDup.Visible = dup;
            g2d.ColumnStyles[0].Width = dup ? 0 : 50; g2d.ColumnStyles[1].Width = dup ? 0 : 50; g2d.ColumnStyles[2].Width = dup ? 100 : 0;
            dupMode = false; listD.CheckBoxes = false; listD.Groups.Clear(); listD.Items.Clear(); bDupDel.Visible = false;
            detD.Tag = Hint; detD.Text = T(Hint);
            SetEmpty(listD, dup ? "Pilih folder, kemudian klik Cari fail pendua." : "Pilih folder, kemudian klik Cari folder terbesar atau Cari fail terbesar.");
        }
    }

    // ----- Semak Kesihatan (halaman utama): ruang, kelajuan, keselamatan dan cakera dalam satu klik -----
    CardPanel[] hcCards = new CardPanel[5]; IconDot hcDot; Label hcSub; RButton bHcRun;
    static IconDot CardDot(CardPanel c) { foreach (Control k in c.Controls) { IconDot d = k as IconDot; if (d != null) return d; } return null; }
    void HcSet(int idx, int level, string text, bool showBtn)
    {
        CardPanel c = hcCards[idx]; IconDot d = CardDot(c); d.Level = level; d.Invalidate(); Label dl = (Label)c.Controls[1]; SetT(dl, text);
        RButton hb = null; foreach (Control k in c.Controls) { RButton b = k as RButton; if (b != null) { b.Visible = showBtn; hb = b; } }
        dl.Width = showBtn && hb != null ? hb.Left - dl.Left - 10 : c.Width - dl.Left - 16;   // teks lebih lebar bila tiada butang
    }
    void HealthCheck()
    {
        int[] lv = new int[5]; string[] tx = new string[5];   // aras IconDot: 0 biru, 1 hijau, 2 jingga, 3 merah
        // 1) ruang dan sampah
        long junk = 0;
        foreach (CCat c in cats) if (!c.Admin && c.Id != "recycle" && c.Id != "dns") { Status("menyemak {0} ...", T(c.Name)); junk += CatStat(c)[1]; }
        double minPct = 100; string minDrive = "";
        foreach (DriveInfo d in DriveInfo.GetDrives()) if (d.DriveType == DriveType.Fixed && d.IsReady) { double p = 100.0 * d.AvailableFreeSpace / d.TotalSize; if (p < minPct) { minPct = p; minDrive = d.Name; } }
        lv[0] = minPct < 5 ? 3 : (minPct < 10 || junk > 1073741824L) ? 2 : 1;
        tx[0] = string.Format(T("Sampah {0} boleh dibersihkan."), Sz(junk)) + (minDrive.Length > 0 ? " " + string.Format(T("Paling penuh: {0} ({1}% bebas)."), minDrive, Math.Round(minPct)) : "");
        // 2) kelajuan: program permulaan
        Status("menyemak program permulaan ...");
        Startup(true);
        int act = 0, bad = 0, red = 0;
        Ui(() => { foreach (ListViewItem it in list2.Items) if (it.ImageIndex < 3) { act++; if (it.ImageIndex >= 1) bad++; if (it.ImageIndex == 2) red++; } });
        lv[1] = red > 0 ? 3 : (bad > 0 || act > 25) ? 2 : 1;
        tx[1] = string.Format(T("{0} program permulaan aktif; {1} perlu disemak."), act, bad);
        // 3) keselamatan: antivirus dan AutoRun
        Status("menyemak keselamatan ...");
        int avOn = 0, avOld = 0; string avName = "";
        foreach (ManagementObject m in Wq(@"root\SecurityCenter2", "SELECT displayName, productState FROM AntiVirusProduct"))
        {
            long st = WL(m, "productState"); if (st < 0) continue;
            if ((st & 0xF000) == 0x1000) { avOn++; if (avName.Length == 0) avName = WP(m, "displayName"); if ((st & 0xF0) != 0) avOld++; }
        }
        bool arOff = false;
        try { using (RegistryKey k = Registry.CurrentUser.OpenSubKey(ApKey1)) arOff = k != null && Convert.ToInt32(k.GetValue("NoDriveTypeAutoRun") ?? 0) == 255; } catch { }
        if (avOn == 0) { lv[2] = 3; tx[2] = T("Tiada antivirus aktif dikesan. Aktifkan Windows Security."); }
        else if (avOld > 0) { lv[2] = 2; tx[2] = string.Format(T("{0}: definisi virus ketinggalan. Kemas kini."), avName); }
        else { lv[2] = 1; tx[2] = string.Format(T("{0}: aktif dan terkini."), avName) + " " + (arOff ? T("AutoRun flashdisk dimatikan.") : T("Disyorkan: matikan AutoRun flashdisk (Pilihan).")); }
        // 4) kesihatan cakera
        Status("menyemak cakera ...");
        List<ManagementObject> pd = Wq(@"root\Microsoft\Windows\Storage", "SELECT FriendlyName, HealthStatus FROM MSFT_PhysicalDisk");
        int nd = 0, nbad = 0; string badName = "";
        foreach (ManagementObject m in pd) { nd++; long h = WL(m, "HealthStatus"); if (h == 1 || h == 2) { nbad++; if (badName.Length == 0) badName = WP(m, "FriendlyName"); } }
        if (nd == 0) { lv[3] = 0; tx[3] = T("Status cakera tak dapat dibaca."); }
        else if (nbad > 0) { lv[3] = 3; tx[3] = string.Format(T("{0} cakera bermasalah ({1}). Sandarkan data anda."), nbad, badName); }
        else { lv[3] = 1; tx[3] = string.Format(T("{0} cakera, semuanya sihat."), nd); }
        // 5) kemas kini program (guna hasil hari ini jika sudah disemak)
        if (updDay != Today()) { Status("menyemak kemas kini program ..."); UpdProbe(); }
        lv[4] = updN < 0 ? 0 : updN == 0 ? 1 : 2;
        tx[4] = updN < 0 ? T("Kemas kini program tak dapat disemak (perlu winget dan internet).") : updN == 0 ? T("Semua program sudah terkini.") : string.Format(T("{0} program ada kemas kini."), updN);
        SaveSettings();
        int worst = 0; foreach (int x in lv) if (x > worst) worst = x;
        Ui(() =>
        {
            for (int i = 0; i < 5; i++) HcSet(i, lv[i], tx[i], lv[i] >= 2 || i == 0 && junk > 0);
            hcDot.Level = worst >= 3 ? 3 : worst == 2 ? 2 : 1; hcDot.Glyph = worst >= 3 ? "" : worst == 2 ? "" : ""; hcDot.Invalidate();
            SetT(hcSub, worst >= 3 ? "Ada masalah yang perlu perhatian segera." : worst == 2 ? "Ada beberapa perkara yang dicadangkan untuk dibaiki." : "PC anda dalam keadaan baik.");
            bHcRun.Tag = "Semak semula"; bHcRun.Text = T("Semak semula");
        });
        Log("Semak Kesihatan siap.");
    }
    // simpan keputusan Semak Kesihatan (dan Maklumat PC jika sudah dimuatkan) sebagai fail teks
    void ActReport()
    {
        if (!(bHcRun.Tag is string) || (string)bHcRun.Tag != "Semak semula") { Info("Jalankan Semak Kesihatan dahulu, kemudian simpan laporan."); return; }
        StringBuilder b = new StringBuilder(); string nl = "\r\n";
        b.Append("USB Shield - " + T("Laporan kesihatan PC") + nl + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + "  (" + Environment.MachineName + ")" + nl + nl);
        b.Append(hcSub.Text + nl + nl);
        foreach (CardPanel c in hcCards) b.Append("- " + c.Controls[0].Text + ": " + c.Controls[1].Text + nl);
        if (specText.Length > 0) b.Append(nl + specText);
        SaveFileDialog d = new SaveFileDialog();
        d.Filter = "Teks (*.txt)|*.txt"; d.FileName = "laporan-pc-" + DateTime.Now.ToString("yyyyMMdd-HHmm") + ".txt";
        if (d.ShowDialog(this) == DialogResult.OK) { File.WriteAllText(d.FileName, b.ToString(), Utf8); Log("Laporan disimpan: {0}", d.FileName); }
    }
    void ActHealth() { Work("menyemak kesihatan PC", () => HealthCheck()); }
    void ShowPage(int i) { ShowPage(i, 0); }
    void ShowPage(int i, int mode)
    {
        pageMode = mode;
        if (i == 4) QLoad();
        if (i == 6) HLoad();
        if (i == 5 && !busy) Startup(true);
        if (i == 9 && canvas.Secs.Count == 0 && !busy) ActSpec();
        if (i == 7 && listP.Items.Count == 0 && !busy) ActProcs();   // senarai terus dimuat bila halaman dibuka
        if (i == 10) WipeLeftovers();
        ApplyMode(i, mode);
        bool needDrive = i <= 1 || i == 10;                  // pilih drive hanya perlu untuk Pembersih Flashdisk, Imbas Virus dan Padam Selamat
        topCard.Visible = needDrive;
        for (int k = 0; k < pages.Length; k++)
        {
            if (k == i) { float sx = topCard.Height / 56f; int bottom = mainPanel.ClientSize.Height - (int)(10 * sx), top = (int)((needDrive ? 76 : 6) * sx); pages[k].SetBounds(pages[k].Left, top, pages[k].Width, bottom - top); }
            pages[k].Visible = k == i;
        }
        ov.SetBounds(0, 0, ov.Width, mainPanel.ClientSize.Height - (int)(10 * topCard.Height / 56f));
        if (i == 13) { log.SelectionStart = log.TextLength; log.ScrollToCaret(); }
        sideNav.SetActive(i, mode);
    }
    void Tips()
    {
        tip.SetToolTip(b1, T("Munculkan semula fail dan folder yang disembunyikan virus."));
        tip.SetToolTip(b2, T("Alihkan fail pintasan (.lnk) dan autorun.inf ke kuarantin."));
        tip.SetToolTip(b3, T("Senaraikan fail .exe dan skrip yang mencurigakan di folder utama flashdisk."));
        tip.SetToolTip(bV, T("Halang virus menulis autorun.inf (folder palsu read-only)."));
        tip.SetToolTip(bAP1, T("Matikan AutoPlay/AutoRun"));
    }

    // terjemahan antara muka: teks asal (Melayu) disimpan dalam Tag
    void TrCtl(Control c)
    {
        foreach (Control k in c.Controls)
        {
            if (k is ButtonBase || k is Label) { if (k.Tag == null) k.Tag = k.Text; k.Text = T((string)k.Tag); }
            ListView lv = k as ListView;
            if (lv != null) foreach (ColumnHeader h in lv.Columns) { if (h.Tag == null) h.Tag = h.Text; h.Text = T((string)h.Tag); }
            TrCtl(k);
        }
    }
    void TrMenu(ToolStripItemCollection items)
    {
        foreach (ToolStripItem i in items)
        {
            if (i.Tag == null) i.Tag = i.Text;
            i.Text = T((string)i.Tag);
            ToolStripMenuItem m = i as ToolStripMenuItem; if (m != null && m.DropDownItems.Count > 0) TrMenu(m.DropDownItems);
        }
    }
    void SetLang(string l) { lang = l; Text = "USB Shield"; TrCtl(this); RefreshClam(); if (listC != null) CRefreshRows(); if (sideNav != null) sideNav.Invalidate(); Tips(); LoadDrives(); }
}
