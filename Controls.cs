using System.Collections.Generic;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

public static class UiUtil {
    public static GraphicsPath Round(Rectangle r, int rad) {
        GraphicsPath p = new GraphicsPath(); int d = rad * 2;
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure(); return p;
    }
    public static Color Mix(Color a, Color b, float t) {
        return Color.FromArgb((int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
    }
}

public class RButton : Button {
    public Color Fill = Color.FromArgb(15, 118, 110);
    public Color TextColor = Color.White;
    public Color Border = Color.Transparent;
    public int Radius = 8;
    bool hover, down;
    public RButton() {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Cursor = Cursors.Hand;
    }
    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; down = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { down = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
    protected override void OnPaint(PaintEventArgs e) {
        Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
        Color bg = Parent != null ? Parent.BackColor : SystemColors.Control;
        g.Clear(bg);
        Color c = Fill;
        if (!Enabled) c = UiUtil.Mix(Fill, bg, 0.6f);
        else if (down) c = UiUtil.Mix(Fill, Color.Black, 0.18f);
        else if (hover) c = UiUtil.Mix(Fill, Color.Black, 0.08f);
        Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
        using (GraphicsPath p = UiUtil.Round(r, Radius)) {
            using (SolidBrush b = new SolidBrush(c)) g.FillPath(b, p);
            if (Border.A > 0) using (Pen pen = new Pen(Border)) g.DrawPath(pen, p);
        }
        if (Focused && ShowFocusCues)
            using (GraphicsPath fp = UiUtil.Round(new Rectangle(2, 2, Width - 5, Height - 5), Math.Max(2, Radius - 2)))
            using (Pen fpn = new Pen(Color.FromArgb(150, 15, 118, 110), 2)) g.DrawPath(fpn, fp);
        Color tc = Enabled ? TextColor : UiUtil.Mix(TextColor, bg, 0.5f);
        TextRenderer.DrawText(g, Text, Font, ClientRectangle, tc,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.HidePrefix | TextFormatFlags.EndEllipsis);
    }
}

public class CardPanel : Panel {
    public Color Border = Color.FromArgb(226, 232, 240);
    public int Radius = 12;
    public CardPanel() {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }
    protected override void OnPaintBackground(PaintEventArgs e) { }
    protected override void OnPaint(PaintEventArgs e) {
        Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Parent != null ? Parent.BackColor : SystemColors.Control);
        Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
        using (GraphicsPath p = UiUtil.Round(r, Radius)) {
            using (SolidBrush b = new SolidBrush(BackColor)) g.FillPath(b, p);
            using (Pen pen = new Pen(Border)) g.DrawPath(pen, p);
        }
    }
}

public class BusyBar : Control {
    Timer t = new Timer();
    int pos;
    public BusyBar() {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        t.Interval = 25;
        t.Tick += delegate { pos = (pos + 9) % (Width + 140); Invalidate(); };
    }
    int val = -1;   // -1 = bar bergerak (tak tahu berapa lama); 0..100 = peratus sebenar
    public int Value { get { return val; } set { if (val != value) { val = value; Invalidate(); } } }
    public bool Running { get { return t.Enabled; } set { t.Enabled = value; if (!value) val = -1; Invalidate(); } }
    protected override void OnPaint(PaintEventArgs e) {
        Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Parent != null ? Parent.BackColor : SystemColors.Control);
        if (!Running) return;
        Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
        using (GraphicsPath p = UiUtil.Round(r, Height / 2)) {
            using (SolidBrush b = new SolidBrush(Color.FromArgb(226, 232, 240))) g.FillPath(b, p);
            g.SetClip(p);
            using (SolidBrush b = new SolidBrush(Color.FromArgb(15, 118, 110)))
                if (val >= 0) g.FillRectangle(b, 0, 0, (int)((Width - 1) * Math.Min(100, val) / 100.0), Height);
                else g.FillRectangle(b, pos - 140, 0, 140, Height);
        }
    }
}

public class DriveItem { public string Root = "", Name = "", Kind = "", Detail = ""; public bool Usb; }

// Pemilih drive moden: butang bersudut bulat yang menunjukkan drive terpilih, dan senarai timbul dengan butiran setiap drive.
public class DriveSelect : Control {
    public List<DriveItem> Items = new List<DriveItem>();
    public string Placeholder = "";
    public Color Accent = Color.FromArgb(15, 118, 110);
    public event EventHandler SelectedIndexChanged;
    int sel = -1; bool hover, open;
    ToolStripDropDown dd;
    Font bold;
    public DriveSelect() {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        TabStop = true; Cursor = Cursors.Hand;
    }
    public int SelectedIndex {
        get { return sel; }
        set {
            if (value >= Items.Count) value = Items.Count - 1;
            if (sel != value) { sel = value; Invalidate(); if (SelectedIndexChanged != null) SelectedIndexChanged(this, EventArgs.Empty); }
        }
    }
    public DriveItem Selected { get { return sel >= 0 && sel < Items.Count ? Items[sel] : null; } }
    public void Reset() { Items.Clear(); sel = -1; Invalidate(); }
    internal Font Bold { get { if (bold == null) bold = new Font("Segoe UI Semibold", Font.Size); return bold; } }
    internal void Pick(int i) { SelectedIndex = i; if (dd != null) dd.Close(); }

    internal static void Icon(Graphics g, bool usb, int x, int cy, Color c) {
        using (SolidBrush b = new SolidBrush(c)) {
            if (usb) {
                g.FillRectangle(b, x + 7, cy - 11, 3, 5); g.FillRectangle(b, x + 12, cy - 11, 3, 5);
                using (GraphicsPath p = UiUtil.Round(new Rectangle(x + 4, cy - 7, 14, 18), 3)) g.FillPath(b, p);
            } else {
                using (GraphicsPath p = UiUtil.Round(new Rectangle(x, cy - 7, 22, 14), 3)) g.FillPath(b, p);
                g.FillEllipse(Brushes.White, x + 16, cy + 1, 3, 3);
            }
        }
    }
    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
    protected override void OnMouseClick(MouseEventArgs e) { Focus(); Open(); base.OnMouseClick(e); }
    protected override bool IsInputKey(Keys k) { return k == Keys.Up || k == Keys.Down || base.IsInputKey(k); }
    protected override void OnKeyDown(KeyEventArgs e) {
        if (e.KeyCode == Keys.Down && sel < Items.Count - 1) SelectedIndex = sel + 1;
        else if (e.KeyCode == Keys.Up && sel > 0) SelectedIndex = sel - 1;
        else if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space || e.KeyCode == Keys.F4) Open();
        base.OnKeyDown(e);
    }
    void Open() {
        if (Items.Count == 0 || !Enabled) return;
        DrivePopup pl = new DrivePopup(this);
        ToolStripControlHost host = new ToolStripControlHost(pl);
        host.Margin = Padding.Empty; host.Padding = Padding.Empty; host.AutoSize = false; host.Size = pl.Size;
        dd = new ToolStripDropDown(); dd.Padding = Padding.Empty; dd.AutoSize = true; dd.Items.Add(host);
        dd.Closed += delegate { open = false; Invalidate(); };
        open = true; Invalidate();
        dd.Show(this, new Point(0, Height + 2));
    }
    protected override void OnPaint(PaintEventArgs e) {
        Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Parent != null ? Parent.BackColor : SystemColors.Control);
        Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
        Color border = (open || Focused) ? Accent : hover ? Color.FromArgb(156, 163, 175) : Color.FromArgb(209, 213, 219);
        using (GraphicsPath p = UiUtil.Round(r, 8)) {
            using (SolidBrush b = new SolidBrush(Enabled ? Color.White : Color.FromArgb(243, 244, 246))) g.FillPath(b, p);
            using (Pen pen = new Pen(border, (open || Focused) ? 1.6f : 1f)) g.DrawPath(pen, p);
        }
        Color ink = Enabled ? Color.FromArgb(31, 41, 55) : Color.FromArgb(156, 163, 175), muted = Color.FromArgb(107, 114, 128);
        DriveItem it = Selected;
        int cy = Height / 2;
        if (it == null) {
            TextRenderer.DrawText(g, Placeholder, Font, new Rectangle(14, 0, Width - 50, Height), muted, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
        } else {
            Icon(g, it.Usb, 12, cy, Enabled ? Accent : Color.FromArgb(156, 163, 175));
            string title = it.Root + "  " + it.Name;
            Size ts = TextRenderer.MeasureText(g, title, Bold, new Size(1000, Height), TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
            int tx = 44, right = Width - 34;
            TextRenderer.DrawText(g, title, Bold, new Rectangle(tx, 0, Math.Min(ts.Width + 4, right - tx), Height), ink, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
            int dx = tx + ts.Width + 14;
            if (dx < right - 40)
                TextRenderer.DrawText(g, it.Kind, Font, new Rectangle(dx, 0, right - dx, Height), muted, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
        }
        using (Pen cp = new Pen(muted, 1.8f)) {
            cp.StartCap = LineCap.Round; cp.EndCap = LineCap.Round;
            int cx = Width - 22;
            g.DrawLines(cp, new Point[] { new Point(cx - 4, cy - 2), new Point(cx, cy + 2), new Point(cx + 4, cy - 2) });
        }
    }
}

class DrivePopup : Control {
    readonly DriveSelect owner; int hot = -1; const int RowH = 50;
    public DrivePopup(DriveSelect o) {
        owner = o; SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        BackColor = Color.White; Font = o.Font; Cursor = Cursors.Hand;
        Size = new Size(Math.Max(o.Width, 320), o.Items.Count * RowH + 8);
    }
    int Row(int y) { int i = (y - 4) / RowH; return (y >= 4 && i >= 0 && i < owner.Items.Count) ? i : -1; }
    protected override void OnMouseMove(MouseEventArgs e) { int h = Row(e.Y); if (h != hot) { hot = h; Invalidate(); } base.OnMouseMove(e); }
    protected override void OnMouseLeave(EventArgs e) { hot = -1; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseClick(MouseEventArgs e) { int i = Row(e.Y); if (i >= 0) owner.Pick(i); base.OnMouseClick(e); }
    protected override void OnPaint(PaintEventArgs e) {
        Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.Clear(Color.White);
        Color muted = Color.FromArgb(107, 114, 128), ink = Color.FromArgb(31, 41, 55);
        for (int i = 0; i < owner.Items.Count; i++) {
            DriveItem it = owner.Items[i]; Rectangle r = new Rectangle(4, 4 + i * RowH, Width - 9, RowH - 2);
            bool selected = i == owner.SelectedIndex;
            if (selected || i == hot)
                using (GraphicsPath p = UiUtil.Round(r, 6))
                using (SolidBrush b = new SolidBrush(selected ? Color.FromArgb(222, 245, 241) : Color.FromArgb(243, 244, 246))) g.FillPath(b, p);
            DriveSelect.Icon(g, it.Usb, r.X + 10, r.Y + RowH / 2 - 1, owner.Accent);
            TextRenderer.DrawText(g, it.Root + "  " + it.Name, owner.Bold, new Rectangle(r.X + 44, r.Y + 5, r.Width - 90, 20), ink, TextFormatFlags.Left | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
            string sub = it.Kind + (it.Detail.Length > 0 ? "  \u00B7  " + it.Detail : "");
            TextRenderer.DrawText(g, sub, Font, new Rectangle(r.X + 44, r.Y + 24, r.Width - 90, 20), muted, TextFormatFlags.Left | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
            if (selected)
                using (Pen cp = new Pen(owner.Accent, 2f)) { cp.StartCap = LineCap.Round; cp.EndCap = LineCap.Round; cp.LineJoin = LineJoin.Round;
                    int cx = r.Right - 22, cy = r.Y + RowH / 2 - 1;
                    g.DrawLines(cp, new Point[] { new Point(cx - 5, cy), new Point(cx - 1, cy + 4), new Point(cx + 6, cy - 4) }); }
        }
    }
}

// kad maklumat PC: ikon bulat berwarna + tajuk + status + baris kunci/nilai; dilukis sendiri, dilungsur oleh Panel induk (AutoScroll)
public class SpecSec {
    public string Title = "", Sub = "", Glyph = ""; public int Level;   // Level: 0 biasa (biru), 1 baik (hijau), 2 amaran (jingga), 3 bahaya (merah)
    public List<string[]> Rows = new List<string[]>();
}
public class SpecCanvas : Control {
    public List<SpecSec> Secs = new List<SpecSec>();
    static readonly Font fT = new Font("Segoe UI Semibold", 11f), fR = new Font("Segoe UI", 9.5f), fS = new Font("Segoe UI Semibold", 9.5f);
    static bool hasGlyph;
    static SpecCanvas() { foreach (FontFamily ff in FontFamily.Families) if (ff.Name == "Segoe MDL2 Assets") hasGlyph = true; }
    public SpecCanvas() {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = Color.White; Dock = DockStyle.Top;
    }
    public static Color[] Fg = { Color.FromArgb(15, 118, 110), Color.FromArgb(21, 128, 61), Color.FromArgb(180, 83, 9), Color.FromArgb(185, 28, 28) };
    public static Color[] Bg = { Color.FromArgb(222, 245, 241), Color.FromArgb(231, 246, 236), Color.FromArgb(254, 243, 199), Color.FromArgb(254, 226, 226) };
    float Sc() { using (Graphics g = CreateGraphics()) return g.DpiX / 96f; }
    int CardH(SpecSec c, float s) { return (int)((60 + c.Rows.Count * 21 + (c.Rows.Count > 0 ? 6 : -8)) * s); }
    public void Rebuild() {
        float s = Sc(); int h = (int)(10 * s);
        foreach (SpecSec c in Secs) h += CardH(c, s) + (int)(10 * s);
        Height = Math.Max(h, 10); Invalidate();
    }
    protected override void OnPaint(PaintEventArgs e) {
        Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.Clear(BackColor);
        float s = g.DpiX / 96f; int y = (int)(4 * s), x = (int)(4 * s), w = Width - (int)(8 * s);
        TextFormatFlags tf = TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine;
        foreach (SpecSec c in Secs) {
            int h = CardH(c, s); Rectangle r = new Rectangle(x, y, w - 1, h - 1);
            using (GraphicsPath p = UiUtil.Round(r, (int)(10 * s))) {
                using (SolidBrush b = new SolidBrush(Color.FromArgb(248, 250, 252))) g.FillPath(b, p);
                using (Pen pen = new Pen(Color.FromArgb(226, 232, 240))) g.DrawPath(pen, p);
            }
            int d = (int)(40 * s), ix = x + (int)(14 * s), iy = y + (int)(10 * s);
            using (SolidBrush b = new SolidBrush(Bg[c.Level])) g.FillEllipse(b, ix, iy, d, d);
            if (hasGlyph) using (Font gf = new Font("Segoe MDL2 Assets", 16f))
                TextRenderer.DrawText(g, c.Glyph, gf, new Rectangle(ix, iy, d, d), Fg[c.Level], TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            int tx = ix + d + (int)(14 * s), tw = x + w - tx - (int)(12 * s);
            TextRenderer.DrawText(g, c.Title, fT, new Rectangle(tx, y + (int)(8 * s), tw, (int)(22 * s)), Color.FromArgb(31, 41, 55), tf);
            if (c.Sub.Length > 0) TextRenderer.DrawText(g, c.Sub, fS, new Rectangle(tx, y + (int)(30 * s), tw, (int)(20 * s)), c.Level == 0 ? Color.FromArgb(107, 114, 128) : Fg[c.Level], tf);
            int ry = y + (int)(60 * s), kw = (int)(130 * s);
            foreach (string[] row in c.Rows) {
                TextRenderer.DrawText(g, row[0], fR, new Rectangle(tx, ry, kw, (int)(21 * s)), Color.FromArgb(107, 114, 128), tf);
                TextRenderer.DrawText(g, row[1], fR, new Rectangle(tx + kw, ry, Math.Max(20, tw - kw), (int)(21 * s)), Color.FromArgb(31, 41, 55), tf);
                ry += (int)(21 * s);
            }
            y += h + (int)(10 * s);
        }
    }
}

// ikon bulat berwarna (glyph Segoe MDL2) untuk kad
public class IconDot : Control {
    public string Glyph = "\uE7F4"; public int Level;
    static bool hasGlyph;
    static IconDot() { foreach (FontFamily ff in FontFamily.Families) if (ff.Name == "Segoe MDL2 Assets") hasGlyph = true; }
    public IconDot() { SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true); BackColor = Color.Transparent; }
    protected override void OnPaint(PaintEventArgs e) {
        Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
        if (Parent != null) g.Clear(Parent.BackColor);
        int d = Math.Min(Width, Height) - 1;
        using (SolidBrush b = new SolidBrush(SpecCanvas.Bg[Level])) g.FillEllipse(b, 0, 0, d, d);
        if (hasGlyph) using (Font gf = new Font("Segoe MDL2 Assets", d * 0.45f, FontStyle.Regular, GraphicsUnit.Pixel))
            TextRenderer.DrawText(g, Glyph, gf, new Rectangle(0, 0, d + 1, d + 1), SpecCanvas.Fg[Level], TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }
}

// skrin memuatkan: kad di tengah kawasan halaman dengan pemutar, tajuk tugas, butiran semasa, bar kemajuan, masa berlalu dan butang Batal.
// Muncul hanya jika kerja berlarutan lebih 300 ms supaya tugas pendek tak berkelip.
public class LoadingOverlay : Panel {
    public string Title = "", Detail = ""; public bool Cancellable; public BusyBar Bar; public Bitmap LogoImg; public Func<string, string> Tr = delegate(string x) { return x; };
    public event EventHandler CancelClicked;
    Timer tick = new Timer(); DateTime start; bool pending; RButton cancel; bool cancelling;
    static readonly Font fTitle = new Font("Segoe UI Semibold", 13f), fDet = new Font("Segoe UI", 10f), fSmall = new Font("Segoe UI", 9f), fPct = new Font("Segoe UI Semibold", 14f);
    public LoadingOverlay() {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = Color.FromArgb(245, 246, 248); Visible = false; tick.Interval = 30;
        tick.Tick += delegate {
            if (pending && (DateTime.Now - start).TotalMilliseconds > 300) { pending = false; Place(); Visible = true; BringToFront(); }
            if (Visible) Invalidate();
        };
    }
    float Sc() { using (Graphics g = CreateGraphics()) return g.DpiX / 96f; }
    public void Begin(string title, bool canCancel) {
        Title = title; Detail = ""; Cancellable = canCancel; cancelling = false; start = DateTime.Now; pending = true; tick.Start();
        if (canCancel && cancel == null) {
            cancel = new RButton(); cancel.Text = Tr("Batal"); cancel.Font = fDet; cancel.Fill = Color.White; cancel.TextColor = Color.FromArgb(185, 28, 28); cancel.Border = Color.FromArgb(252, 165, 165);
            cancel.Click += delegate { if (cancelling) return; cancelling = true; Detail = Tr("Membatalkan ..."); cancel.Enabled = false; if (CancelClicked != null) CancelClicked(this, EventArgs.Empty); };
            Controls.Add(cancel);
        }
        if (cancel != null) { cancel.Visible = canCancel; cancel.Enabled = true; cancel.Text = Tr("Batal"); }
    }
    public void End() { pending = false; tick.Stop(); Visible = false; }
    public void SetDetail(string d) { Detail = d; }
    void Geom(float s, out Rectangle card) {
        int cw = (int)Math.Min(Width - 40 * s, 460 * s), ch = (int)((Cancellable ? 318 : 270) * s);
        card = new Rectangle((Width - cw) / 2, Math.Max((int)(8 * s), (Height - ch) / 2 - (int)(8 * s)), cw, ch);
    }
    void Place() {
        float s = Sc(); Rectangle c; Geom(s, out c);
        if (cancel != null && Cancellable) { cancel.Size = new Size((int)(130 * s), (int)(36 * s)); cancel.Location = new Point(c.X + (c.Width - cancel.Width) / 2, c.Bottom - cancel.Height - (int)(20 * s)); }
    }
    protected override void OnResize(EventArgs e) { base.OnResize(e); if (Visible) Place(); }
    protected override void OnPaint(PaintEventArgs e) {
        Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.Clear(BackColor);
        float s = g.DpiX / 96f; Rectangle c; Geom(s, out c);
        for (int i = 5; i >= 1; i--) using (GraphicsPath sp = UiUtil.Round(new Rectangle(c.X - i * 2, c.Y - i + 4, c.Width + i * 4, c.Height + i * 2), (int)(18 * s)))
            using (SolidBrush b = new SolidBrush(Color.FromArgb(9, 15, 23, 42))) g.FillPath(b, sp);
        using (GraphicsPath p = UiUtil.Round(c, (int)(16 * s))) {
            using (SolidBrush b = new SolidBrush(Color.White)) g.FillPath(b, p);
            using (Pen pen = new Pen(Color.FromArgb(226, 232, 240))) g.DrawPath(pen, p);
        }
        // pemutar: gelang kelabu + lengkok biru (berputar; jika ada peratus, lengkok mengisi gelang)
        int d = (int)(76 * s), px = c.X + (c.Width - d) / 2, py = c.Y + (int)(24 * s); Rectangle rg = new Rectangle(px, py, d, d);
        int val = Bar != null ? Bar.Value : -1; double secs = (DateTime.Now - start).TotalSeconds;
        float pw = 7 * s;
        using (Pen tr = new Pen(Color.FromArgb(226, 232, 240), pw)) g.DrawEllipse(tr, rg);
        using (Pen pen = new Pen(Color.FromArgb(15, 118, 110), pw)) {
            pen.StartCap = LineCap.Round; pen.EndCap = LineCap.Round;
            if (val >= 0) { float sw = 360f * Math.Min(100, val) / 100f; if (sw >= 1) g.DrawArc(pen, rg, -90, sw); }
            else {
                float a = (float)((secs * 280) % 360), sw = 70 + 40 * (float)Math.Sin(secs * 3.2);   // lengkok yang memanjang dan memendek
                g.DrawArc(pen, rg, a, sw);
            }
        }
        if (val >= 0) TextRenderer.DrawText(g, val + "%", fPct, rg, Color.FromArgb(31, 41, 55), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        else if (LogoImg != null) { int ls = (int)(34 * s); g.DrawImage(LogoImg, rg.X + (d - ls) / 2, rg.Y + (d - ls) / 2, ls, ls); }
        TextFormatFlags tf = TextFormatFlags.HorizontalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter;
        int tx = c.X + (int)(24 * s), tw = c.Width - (int)(48 * s), y = py + d + (int)(16 * s);
        string title = Title.Length > 0 ? char.ToUpper(Title[0]) + Title.Substring(1) : "";
        TextRenderer.DrawText(g, title, fTitle, new Rectangle(tx, y, tw, (int)(28 * s)), Color.FromArgb(31, 41, 55), tf); y += (int)(30 * s);
        TextRenderer.DrawText(g, Detail, fDet, new Rectangle(tx, y, tw, (int)(24 * s)), Color.FromArgb(107, 114, 128), tf); y += (int)(34 * s);
        // bar kemajuan mendatar
        Rectangle br = new Rectangle(tx, y, tw, (int)(8 * s));
        using (GraphicsPath bp = UiUtil.Round(br, br.Height / 2)) {
            using (SolidBrush b = new SolidBrush(Color.FromArgb(226, 232, 240))) g.FillPath(b, bp);
            g.SetClip(bp);
            using (SolidBrush b = new SolidBrush(Color.FromArgb(15, 118, 110))) {
                if (val >= 0) g.FillRectangle(b, br.X, br.Y, br.Width * Math.Min(100, val) / 100f, br.Height);
                else { float w = br.Width * 0.3f, pos = (float)((secs * 0.7) % 1.3) * br.Width - w * 0.3f; g.FillRectangle(b, br.X + pos, br.Y, w, br.Height); }
            }
            g.ResetClip();
        }
        y += (int)(16 * s);
        TimeSpan el = TimeSpan.FromSeconds(secs);
        string t = el.TotalHours >= 1 ? string.Format("{0}:{1:00}:{2:00}", (int)el.TotalHours, el.Minutes, el.Seconds) : string.Format("{0:00}:{1:00}", el.Minutes, el.Seconds);
        TextRenderer.DrawText(g, Tr("Masa") + ": " + t, fSmall, new Rectangle(tx, y, tw / 2, (int)(20 * s)), Color.FromArgb(107, 114, 128), TextFormatFlags.Left | TextFormatFlags.NoPadding | TextFormatFlags.VerticalCenter);
        TextRenderer.DrawText(g, val >= 0 ? val + "%" : Tr("Sila tunggu ..."), fSmall, new Rectangle(tx + tw / 2, y, tw / 2, (int)(20 * s)), Color.FromArgb(107, 114, 128), TextFormatFlags.Right | TextFormatFlags.NoPadding | TextFormatFlags.VerticalCenter);
    }
}

// menu kiri gelap berkumpulan (cara CCleaner): item atas dengan ikon, kumpulan boleh kembang (satu terbuka pada satu masa)
public class SideItem {
    public string Text = "", Glyph = ""; public int Page = -1, Mode; public bool Expanded; public SideItem Parent;
    public List<SideItem> Kids = new List<SideItem>();
    public bool IsGroup { get { return Kids.Count > 0; } }
}
public class SideNav : Control {
    public List<SideItem> Items = new List<SideItem>(); public SideItem Active;
    public Func<string, string> Tr = delegate(string x) { return x; };
    public event Action<SideItem> ItemClicked;
    SideItem hover; float sc = 1f; static bool hasGlyph;
    static readonly Font fTop = new Font("Segoe UI Semibold", 10f), fKid = new Font("Segoe UI", 9.5f);
    public static readonly Color Bg = Color.FromArgb(10, 52, 50), Accent = Color.FromArgb(45, 212, 191);
    static SideNav() { foreach (FontFamily ff in FontFamily.Families) if (ff.Name == "Segoe MDL2 Assets") hasGlyph = true; }
    public SideNav() {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = Bg; Cursor = Cursors.Hand;
    }
    int TopH { get { return (int)(34 * sc); } } int KidH { get { return (int)(28 * sc); } }
    List<KeyValuePair<SideItem, Rectangle>> Rows() {
        List<KeyValuePair<SideItem, Rectangle>> r = new List<KeyValuePair<SideItem, Rectangle>>(); int y = 0;
        foreach (SideItem t in Items) {
            r.Add(new KeyValuePair<SideItem, Rectangle>(t, new Rectangle(0, y, Width, TopH))); y += TopH;
            if (t.Expanded) foreach (SideItem k in t.Kids) { r.Add(new KeyValuePair<SideItem, Rectangle>(k, new Rectangle(0, y, Width, KidH))); y += KidH; }
        }
        return r;
    }
    SideItem At(Point p) { foreach (KeyValuePair<SideItem, Rectangle> kv in Rows()) if (kv.Value.Contains(p)) return kv.Key; return null; }
    // tandakan halaman aktif; kumpulan induknya dikembangkan dan kumpulan lain dikuncupkan
    public void SetActive(int page, int mode) {
        SideItem found = null;
        foreach (SideItem t in Items) {
            if (t.Page == page && t.Mode == mode) found = t;
            foreach (SideItem k in t.Kids) if (k.Page == page && k.Mode == mode) found = k;
        }
        if (found == null) foreach (SideItem t in Items) {
            if (t.Page == page) { found = t; break; }
            foreach (SideItem k in t.Kids) if (k.Page == page) { found = k; break; }
            if (found != null) break;
        }
        Active = found;
        if (found != null && found.Parent != null) foreach (SideItem t in Items) t.Expanded = t == found.Parent;
        else if (found != null) foreach (SideItem t in Items) t.Expanded = false;
        Invalidate();
    }
    protected override void OnMouseMove(MouseEventArgs e) { SideItem h = At(e.Location); if (h != hover) { hover = h; Invalidate(); } base.OnMouseMove(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = null; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseUp(MouseEventArgs e) {
        base.OnMouseUp(e); if (e.Button != MouseButtons.Left) return;
        SideItem it = At(e.Location); if (it == null) return;
        if (it.IsGroup) {
            if (!it.Expanded) { foreach (SideItem t in Items) t.Expanded = t == it; Invalidate(); if (ItemClicked != null) ItemClicked(it.Kids[0]); }
            else { it.Expanded = false; Invalidate(); }
            return;
        }
        if (ItemClicked != null) ItemClicked(it);
    }
    protected override void OnPaint(PaintEventArgs e) {
        Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; sc = g.DpiX / 96f; g.Clear(BackColor);
        foreach (KeyValuePair<SideItem, Rectangle> kv in Rows()) {
            SideItem it = kv.Key; Rectangle r = kv.Value; bool kid = it.Parent != null;
            bool act = it == Active, grpAct = it.IsGroup && Active != null && Active.Parent == it;
            Rectangle pr = new Rectangle(r.X + (int)(8 * sc), r.Y + 1, r.Width - (int)(16 * sc), r.Height - 2);
            if (act || hover == it) using (GraphicsPath p = UiUtil.Round(pr, (int)(8 * sc)))
                using (SolidBrush b = new SolidBrush(act ? Color.FromArgb(18, 98, 90) : Color.FromArgb(15, 74, 68))) g.FillPath(b, p);
            if (act) using (SolidBrush b = new SolidBrush(Accent)) g.FillRectangle(b, r.X + (int)(2 * sc), r.Y + (int)(9 * sc), (int)(3 * sc), r.Height - (int)(18 * sc));
            Color tc = act || grpAct ? Color.White : Color.FromArgb(172, 214, 207);
            int gx = r.X + (int)((kid ? 30 : 16) * sc);
            if (hasGlyph && it.Glyph.Length > 0) using (Font gf = new Font("Segoe MDL2 Assets", kid ? 10.5f : 13f))
                TextRenderer.DrawText(g, it.Glyph, gf, new Rectangle(gx, r.Y, (int)(26 * sc), r.Height), act || grpAct ? Accent : tc, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
            int tx = gx + (int)((kid ? 28 : 31) * sc), rr = r.Right - (int)((it.IsGroup ? 34 : 12) * sc);
            TextRenderer.DrawText(g, Tr(it.Text), kid ? fKid : fTop, new Rectangle(tx, r.Y, rr - tx, r.Height), tc, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.HidePrefix | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
            if (it.IsGroup && hasGlyph) using (Font cf = new Font("Segoe MDL2 Assets", 8f))
                TextRenderer.DrawText(g, it.Expanded ? "" : "", cf, new Rectangle(r.Right - (int)(34 * sc), r.Y, (int)(20 * sc), r.Height), tc, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
    }
}
