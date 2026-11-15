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

[assembly: AssemblyTitle("USB Shield")]
[assembly: AssemblyDescription("Pembaiki flashdisk dan pembersih PC - USB Shield")]
[assembly: AssemblyProduct("USB Shield")]
[assembly: AssemblyCopyright("USB Shield")]
[assembly: AssemblyVersion("2.0.0.0")]
[assembly: AssemblyFileVersion("2.0.0.0")]

static class Program
{
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool SetProcessDPIAware();
    [STAThread]
    static void Main()
    {
        try { SetProcessDPIAware(); } catch { }   // teks tajam pada skrin 125%/150% (Windows 7 ke atas)
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainForm());
    }
}

// Aplikasi .NET asli: tiada PowerShell, tiada skrip tertanam. Semua kerja panjang berjalan di benang latar.
public partial class MainForm : Form
{
    // ---------- keadaan ----------
    string lang = "ms";
    CardPanel topCard;
    Action pendingAfter;   // dijalankan di benang UI sebaik kerja latar tamat (cth. ringkasan hasil)
    string savedLang = "ms";
    NotifyIcon tray; bool toTray, exiting; CheckBox chkTray;
    bool guideSeen, inPrompt; string newUsb;
    RButton bAdmin;
    static bool IsAdmin { get { return new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator); } }
    readonly System.Windows.Forms.Timer devTimer = new System.Windows.Forms.Timer();
    bool busy;
    readonly string sysDrive = Environment.GetEnvironmentVariable("SystemDrive") + "\\";
    static readonly string[] Sus = { ".exe", ".vbs", ".vbe", ".js", ".jse", ".wsf", ".hta", ".pif", ".com", ".bat", ".cmd", ".scr" };
    static readonly string[] ScriptExt = { ".vbs", ".vbe", ".js", ".jse", ".wsf", ".hta" };
    static readonly Regex DblExt = new Regex(@"\.(doc|docx|xls|xlsx|ppt|pptx|pdf|jpg|jpeg|png|gif|txt|mp3|mp4|zip|rar)\.(exe|scr|bat|cmd|vbs|com|pif)$", RegexOptions.IgnoreCase);
    static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);
    const string RunPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string ApKey1 = @"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer";
    const string ApKey2 = @"Software\Microsoft\Windows\CurrentVersion\AutoplayHandlers";
    readonly string qDir = Path.Combine(Environment.GetEnvironmentVariable("USERPROFILE"), "Kuarantin Pembersih");
    string QIdx { get { return Path.Combine(qDir, "senarai.txt"); } }
    readonly string exeDir = AppDomain.CurrentDomain.BaseDirectory;

    const string GuideMs = "1. (Pilihan) Kemas kini Defender.\n2. Cucuk flashdisk dan pilih drive.\n3. Klik Bersihkan Semua.\n4. Semak senarai fail mencurigakan: tanda yang BUKAN milik anda dan kuarantin.\n5. Imbas seluruh PC dengan Windows Security > Full Scan.";
    const string AboutMs = "USB Shield\nMembaiki kesan shortcut virus dan memanggil Microsoft Defender untuk membuang virus sebenar. Bukan enjin antivirus sendiri.";

    // ---------- kawalan ----------
    DriveSelect cb; ListView list, list2, listQ; Label det1, det2, detQ; TextBox log; Label logLbl; BusyBar bar;
    Panel[] pages; Panel mainPanel; SideNav sideNav; int pageMode; ToolTip tip = new ToolTip();
    RButton bHClear; RButton bAll, b1, b2, b3, bV, b4, b5, b6, b7, b8, bAP1, bAP2, bQO, bRef, bSave, bDel, bDel2, bQR, bQD, bLoc, bRst;
    const string Hint = "Pilih satu baris untuk melihat butiran penuh.";

    static readonly Font FReg = new Font("Segoe UI", 10f);
    static readonly Font FBold = new Font("Segoe UI Semibold", 10.5f);
    static readonly Font FHead = new Font("Segoe UI Semibold", 17f);
    static readonly Font FCard = new Font("Segoe UI Semibold", 11f);
    static readonly Font FTitle = new Font("Segoe UI Semibold", 14f);
    static readonly Font FBig = new Font("Segoe UI Semibold", 12f);
    static readonly Font FSmall = new Font("Segoe UI", 9f);
    static Color Col(string h) { return ColorTranslator.FromHtml(h); }

    // ---------- terjemahan / log / dialog ----------
    string T(string s) { string v; return (lang == "en" && s != null && Strings.En.TryGetValue(s, out v)) ? v : s; }
    string Fmt(string tpl, object[] a) { return a != null && a.Length > 0 ? string.Format(T(tpl), a) : T(tpl); }
    void Ui(Action a) { if (InvokeRequired) Invoke(a); else a(); }
    void Log(string tpl, params object[] a) { string t = Fmt(tpl, a); Ui(() => log.AppendText(t + "\r\n")); }
    LoadingOverlay ov;
    void Status(string tpl, params object[] a) { string t = Fmt(tpl, a); Ui(() => { logLbl.Text = T("Sedang berjalan") + ": " + t; if (ov != null) ov.SetDetail(t); }); }
    DialogResult Msg(string text, string title, MessageBoxButtons b, MessageBoxIcon ic, params object[] a)
    {
        string t = Fmt(text, a); DialogResult r = DialogResult.None;
        Ui(() => r = MessageBox.Show(this, t, T(title), b, ic));
        return r;
    }
    void Info(string text) { Msg(text, "Pembersih", MessageBoxButtons.OK, MessageBoxIcon.Information); }
    bool Ask(string text, MessageBoxIcon ic) { return Msg(text, "Sahkan", MessageBoxButtons.YesNo, ic) == DialogResult.Yes; }

    // jalankan kerja di benang latar; UI dikunci dan bar sibuk bergerak
    void Work(string text, Action a) { Work(text, a, false); }
    void Work(string text, Action a, bool canCancel)
    {
        if (busy) return;
        cancelReq = false; cancellable = canCancel;
        SetBusy(true, text);
        Thread t = new Thread(() =>
        {
            try { a(); } catch (Exception ex) { Log("RALAT: {0}", ex.Message); }
            Ui(() =>
            {
                SetBusy(false, null); Log("-- selesai: {0} --", T(text)); if (ActiveForm != this && Visible) Flash();
                Action ap = pendingAfter; pendingAfter = null; if (ap != null) ap();
            });
        });
        t.IsBackground = true; t.Start();
    }
    [StructLayout(LayoutKind.Sequential)] struct FLASHWINFO { public uint cbSize; public IntPtr hwnd; public uint dwFlags, uCount, dwTimeout; }
    [DllImport("user32.dll")] static extern bool FlashWindowEx(ref FLASHWINFO f);
    void Flash() { FLASHWINFO f = new FLASHWINFO(); f.cbSize = (uint)Marshal.SizeOf(typeof(FLASHWINFO)); f.hwnd = Handle; f.dwFlags = 2 | 12; f.uCount = 0; try { FlashWindowEx(ref f); } catch { } }   // kelip di taskbar sehingga tetingkap dibuka
    void SetBusy(bool on, string text)
    {
        busy = on; bar.Running = on;
        if (ov != null) { if (on) ov.Begin(T(text), cancellable); else ov.End(); }
        bCancel.Visible = on && cancellable; bSave.Visible = !(on && cancellable);   // "Batal" ganti "Simpan log" semasa imbasan boleh batal
        if (on) { logLbl.Text = T("Sedang berjalan") + ": " + T(text); logLbl.ForeColor = Col("#0F766E"); }
        else { logLbl.Text = T("Log"); logLbl.ForeColor = Col("#1F2937"); }
        foreach (Panel p in pages) p.Enabled = !on;
        cb.Enabled = !on; bRef.Enabled = !on; UseWaitCursor = on;
    }

}
