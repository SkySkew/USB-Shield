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
    // ---------- logik: drive ----------
    bool drivesLoaded;
    void LoadDrives()
    {
        string prev = cb.Selected != null ? cb.Selected.Root : null;
        newUsb = null;
        List<string> before = new List<string>();
        foreach (DriveItem x in cb.Items) before.Add(x.Root);
        cb.Reset(); cb.Placeholder = T("Tiada flashdisk dikesan");
        int pick = -1, firstRem = -1, newRem = -1, idx = 0;
        foreach (DriveInfo d in DriveInfo.GetDrives())
        {
            if (!d.IsReady || d.Name == sysDrive) continue;
            DriveItem it = new DriveItem();
            it.Root = d.Name; it.Usb = d.DriveType == DriveType.Removable;
            try { it.Name = string.IsNullOrEmpty(d.VolumeLabel) ? T("Tanpa nama") : d.VolumeLabel; } catch { it.Name = T("Tanpa nama"); }
            it.Kind = it.Usb ? T("flashdisk") : d.DriveType == DriveType.Fixed ? T("cakera tetap") : d.DriveType.ToString();
            try { it.Detail = string.Format(T("{0} GB bebas daripada {1} GB"), Math.Round(d.AvailableFreeSpace / 1073741824.0, 1), Math.Round(d.TotalSize / 1073741824.0, 1)); } catch { }
            cb.Items.Add(it);
            if (it.Usb) { if (firstRem < 0) firstRem = idx; if (drivesLoaded && !before.Contains(d.Name)) newRem = idx; }
            if (d.Name == prev) pick = idx;
            idx++;
        }
        drivesLoaded = true;
        if (cb.Items.Count == 0) { cb.Invalidate(); Log("Tiada flashdisk dikesan. Cucuk flashdisk lalu klik Muat semula."); return; }
        cb.SelectedIndex = newRem >= 0 ? newRem : pick >= 0 ? pick : firstRem >= 0 ? firstRem : 0;
        cb.Invalidate();
        if (newRem >= 0) { newUsb = cb.Selected.Root; Log("Flashdisk dikesan: {0}", newUsb); }
    }
    // flashdisk baharu dicucuk: tanya dahulu, jangan buat apa-apa tanpa kebenaran
    // dengan semakan automatik: periksa akar flashdisk (baca sahaja); tanya hanya kalau ada tanda virus pintasan, kalau bersih cuma catat di log
    bool avAuto; CheckBox chkAv;   // imbas antivirus juga semasa semakan automatik (lalai mati)
    bool appUpd = true; string appChk = "", appSeen = ""; CheckBox chkAppUpd;   // semakan versi USB Shield di GitHub
    bool autoCheck = true;
    void OfferClean()
    {
        if (newUsb == null || busy || inPrompt) return;
        string u = newUsb; newUsb = null;
        if (!autoCheck) { OfferAsk(string.Format(T("Flashdisk {0} dikesan. Bersihkan sekarang?"), u)); return; }
        string res = "", av = "";
        Work(avAuto ? "memeriksa flashdisk dan mengimbas antivirus" : "memeriksa flashdisk", () =>
        {
            res = QuickCheck(u);
            if (res.Length == 0 && avAuto)   // ada tanda virus: soalan "Bersihkan?" sudah imbas antivirus; jika bersih, imbas di sini
            {
                av = Defender(u);
                if (av == T("tidak tersedia") && ClamExe() != null) av = ClamScan(u);
                AddHistory(u, "Imbasan automatik", av);
            }
            pendingAfter = () =>
            {
                if (res.Length > 0) OfferAsk(res);
                else if (av.Length > 0 && av != T("tiada ancaman") && av != T("tidak tersedia") && av != T("dibatalkan"))
                    Info(string.Format(T("Imbasan antivirus pada {0}: {1}. Semak halaman Pembersih Flashdisk atau Sejarah."), u, av));
                else Log("Flashdisk {0} diperiksa: tiada tanda virus pintasan. (Semakan automatik boleh dimatikan di Pilihan.)", u);
            };
        }, avAuto);
    }
    void OfferAsk(string text)
    {
        inPrompt = true;
        try
        {
            if (!Visible) Show();   // tersembunyi di dulang: papar semula untuk tanya
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            Activate();
            if (Ask(text, MessageBoxIcon.Question)) ActAll();
        }
        finally { inPrompt = false; }
    }
    // semakan cepat di akar drive sahaja; pulang mesej (kosong = bersih)
    string QuickCheck(string r)
    {
        int lnk = 0, dup = 0, hid = 0; bool auto = false;
        try
        {
            DirectoryInfo root = new DirectoryInfo(r); List<string> dirs = new List<string>();
            foreach (DirectoryInfo d in root.GetDirectories())
            {
                string n = d.Name.ToLower(); if (n == "system volume information" || n == "$recycle.bin") continue;
                dirs.Add(n); if ((d.Attributes & FileAttributes.Hidden) != 0) hid++;
            }
            foreach (FileInfo f in root.GetFiles())
            {
                string ext = f.Extension.ToLower();
                if (ext == ".lnk") lnk++;
                else if (f.Name.ToLower() == "autorun.inf") auto = true;
                else if (Array.IndexOf(Sus, ext) >= 0 && dirs.Contains(Path.GetFileNameWithoutExtension(f.Name).ToLower())) dup++;
            }
        }
        catch { return ""; }
        if (lnk == 0 && dup == 0 && !auto) return "";   // folder tersembunyi sahaja tak cukup: Windows pun menyembunyikan sesetengah folder
        StringBuilder b = new StringBuilder(string.Format(T("Flashdisk {0} dikesan dan diperiksa. Ditemui:"), r));
        if (lnk > 0) b.Append("\n- " + string.Format(T("{0} fail pintasan (.lnk) di akar drive"), lnk));
        if (dup > 0) b.Append("\n- " + string.Format(T("{0} fail program sama nama dengan folder"), dup));
        if (auto) b.Append("\n- " + T("fail autorun.inf"));
        if (hid > 0) b.Append("\n- " + string.Format(T("{0} folder tersembunyi"), hid));
        b.Append("\n\n" + T("Ini ciri virus pintasan. Bersihkan sekarang?"));
        return b.ToString();
    }
    string Root()
    {
        if (cb.Selected == null) { Info("Pilih drive dahulu."); return null; }
        return cb.Selected.Root;
    }
    // tindakan yang mengubah drive: minta pengesahan jika drive itu cakera tetap (bukan flashdisk)
    string RootEdit()
    {
        string r = Root(); if (r == null) return null;
        if (new DriveInfo(r).DriveType == DriveType.Fixed && !Ask(string.Format(T("Drive {0} ialah cakera tetap, bukan flashdisk. Teruskan?"), r), MessageBoxIcon.Warning)) return null;
        return r;
    }
    static bool IsRemovable(string r) { return new DriveInfo(r).DriveType == DriveType.Removable; }

    // ---------- logik: pulihkan fail ----------
    int Unhide(string r)
    {
        Log("Memulihkan fail tersembunyi di {0} ...", r);
        int n = 0, changed = 0; UnhideDir(new DirectoryInfo(r), ref n, ref changed);
        Log("Siap: {0} fail/folder dipulihkan.", changed);
        return changed;
    }
    void UnhideDir(DirectoryInfo d, ref int n, ref int changed)
    {
        FileSystemInfo[] items;
        try { items = d.GetFileSystemInfos(); } catch { return; }
        foreach (FileSystemInfo i in items)
        {
            try
            {
                FileAttributes a = i.Attributes;
                FileAttributes na = a & ~(FileAttributes.Hidden | FileAttributes.System | FileAttributes.ReadOnly);
                if (na != a) { i.Attributes = na; changed++; }
            }
            catch { }
            if (++n % 200 == 0) Status("memulihkan {0} item ...", n);
            DirectoryInfo sub = i as DirectoryInfo;
            if (sub != null && (sub.Attributes & FileAttributes.ReparsePoint) == 0 && sub.Name != "System Volume Information" && sub.Name != "$RECYCLE.BIN")
                UnhideDir(sub, ref n, ref changed);
        }
    }

    // ---------- logik: kuarantin (alih ke PC, nama .kuarantin, indeks id|laluan-asal) ----------
    void Quar(string path)
    {
        Directory.CreateDirectory(qDir);
        string id = Guid.NewGuid().ToString("N").Substring(0, 8);
        string dest = Path.Combine(qDir, id + "-" + Path.GetFileName(path) + ".kuarantin");
        File.SetAttributes(path, FileAttributes.Normal);
        try { File.Move(path, dest); } catch (IOException) { File.Copy(path, dest); File.Delete(path); }
        File.AppendAllText(QIdx, id + "|" + path + "\r\n", Utf8);
    }
    static string QId(string item) { Match m = Regex.Match(item, @"\[([0-9a-f]{8})\]$"); return m.Success ? m.Groups[1].Value : null; }
    string QStored(string id) { string[] f = Directory.Exists(qDir) ? Directory.GetFiles(qDir, id + "-*") : new string[0]; return f.Length > 0 ? f[0] : null; }
    void QForget(string id)
    {
        List<string> keep = new List<string>();
        foreach (string l in File.ReadAllLines(QIdx, Utf8)) if (l.Length > 0 && !l.StartsWith(id + "|")) keep.Add(l);
        File.WriteAllLines(QIdx, keep.ToArray(), Utf8);
    }
    void QLoad()
    {
        listQ.Items.Clear();
        if (!File.Exists(QIdx)) { SetEmpty(listQ, "Tiada fail dalam kuarantin."); return; }
        foreach (string l in File.ReadAllLines(QIdx, Utf8))
        {
            int p = l.IndexOf('|'); if (p < 1) continue;
            string id = l.Substring(0, p), orig = l.Substring(p + 1);
            if (QStored(id) != null)
                AddRow(listQ, orig + "  [" + id + "]", orig, Path.GetFileName(orig), id, Path.GetDirectoryName(orig));
        }
        detQ.Text = T(Hint); SetEmpty(listQ, "Tiada fail dalam kuarantin.");
    }
    void QRestore(string item)
    {
        string id = QId(item), orig = Regex.Replace(item, @"\s+\[[0-9a-f]{8}\]$", ""), st = QStored(id);
        if (st == null) throw new Exception(T("fail kuarantin tiada"));
        if (!Directory.Exists(Path.GetDirectoryName(orig))) throw new Exception(T("lokasi asal tiada (cucuk semula drive)"));
        if (File.Exists(orig)) orig += ".dipulihkan";
        try { File.Move(st, orig); } catch (IOException) { File.Copy(st, orig); File.Delete(st); }
        QForget(id);
    }
    void QDelete(string item)
    {
        string id = QId(item), st = QStored(id);
        if (st != null) { File.SetAttributes(st, FileAttributes.Normal); File.Delete(st); }
        QForget(id);
    }

    int Lnk(string r)
    {
        int n = 0;
        foreach (FileInfo f in new DirectoryInfo(r).GetFiles())
            if (f.Extension.ToLower() == ".lnk" || f.Name.ToLower() == "autorun.inf") { Quar(f.FullName); Log("Dikuarantin: {0}", f.Name); n++; }
        Log("Siap: {0} fail .lnk/autorun.inf dialihkan ke kuarantin.", n);
        return n;
    }

    // vaksin: folder autorun.inf read-only/hidden/system, jadi virus tak boleh mencipta fail autorun.inf
    bool Vaccinate(string r)
    {
        string p = r + "autorun.inf";
        if (Directory.Exists(p)) { Log("Drive ini sudah divaksin."); return false; }
        if (File.Exists(p)) Quar(p);
        Directory.CreateDirectory(p);
        File.SetAttributes(p, FileAttributes.ReadOnly | FileAttributes.Hidden | FileAttributes.System | FileAttributes.Directory);
        Log("Vaksin dipasang: folder autorun.inf (read-only) menghalang virus menulis fail autorun.");
        return true;
    }

    // pulang {jumlah fail mencurigakan, bilangan yang hampir pasti virus}; yang pasti ditanda automatik
    int[] Scan(string r)
    {
        List<string[]> rows = new List<string[]>();
        DirectoryInfo root = new DirectoryInfo(r);
        List<string> dirs = new List<string>();
        foreach (DirectoryInfo d in root.GetDirectories()) dirs.Add(d.Name.ToLower());
        int strong = 0;
        foreach (FileInfo x in root.GetFiles())
        {
            string ext = x.Extension.ToLower();
            if (Array.IndexOf(Sus, ext) < 0) continue;
            string why; bool sure = false;
            if (dirs.Contains(Path.GetFileNameWithoutExtension(x.Name).ToLower())) { why = "sama nama dengan folder (ciri shortcut virus)"; sure = true; }
            else if (DblExt.IsMatch(x.Name)) { why = "sambungan berganda, menyamar sebagai dokumen"; sure = true; }
            else if (Array.IndexOf(ScriptExt, ext) >= 0) why = "skrip di folder utama flashdisk";
            else why = "fail boleh laku di folder utama flashdisk";
            if (sure) strong++;
            rows.Add(new string[] { x.Name, T(why), r, x.FullName, sure ? "1" : "0" });
        }
        Ui(() =>
        {
            list.Items.Clear();
            foreach (string[] w in rows) { ListViewItem it = AddRow(list, w[3], w[3] + "\n" + w[1], w[0], w[1], w[2]); it.ImageIndex = w[4] == "1" ? 2 : 1; if (w[4] == "1") it.Checked = true; }
            det1.Tag = Leg1; det1.Text = T(Leg1); SetEmpty(list, rows.Count == 0 ? "Tiada fail mencurigakan ditemui." : "");
        });
        Log("Imbasan siap: {0} fail mencurigakan di folder utama (semak sebelum kuarantin).", rows.Count);
        return new int[] { rows.Count, strong };
    }

    // lakukan action pada payload setiap baris ditanda selepas pengesahan
    void DoChecked(ListView lv, Action<string> action, string msg)
    {
        List<ListViewItem> sel = new List<ListViewItem>();
        foreach (ListViewItem i in lv.CheckedItems) sel.Add(i);
        if (sel.Count == 0) { Info("Tiada item ditanda."); return; }
        List<string> names = new List<string>();
        foreach (ListViewItem i in sel) names.Add(i.ToolTipText.Replace("\n", " : "));
        if (!Ask(T(msg) + "\n\n" + string.Join("\n", names.ToArray()), MessageBoxIcon.Warning)) return;
        foreach (ListViewItem i in sel)
        {
            try { action((string)i.Tag); lv.Items.Remove(i); Log("Selesai: {0}", i.Text); }
            catch (Exception ex) { Log("GAGAL: {0} ({1}) - cuba jalankan sebagai Administrator", i.Text, T(ex.Message)); }
        }
        SetEmpty(lv, lv.Items.Count == 0 ? "Tiada item lagi." : "");
    }

    // ---------- logik: Defender (MpCmdRun.exe, tanpa PowerShell) ----------
    static string MpExe()
    {
        string pf = Environment.GetEnvironmentVariable("ProgramFiles");
        foreach (string p in new string[] { pf + @"\Microsoft Security Client\MpCmdRun.exe", pf + @"\Windows Defender\MpCmdRun.exe" })
            if (File.Exists(p)) return p;
        return null;
    }
    // seperti Run tetapi boleh dibatalkan (cancelReq); pulang -999 jika dibatalkan
    int RunCancel(string exe, string args)
    {
        ProcessStartInfo psi = new ProcessStartInfo(exe, args);
        psi.UseShellExecute = false; psi.CreateNoWindow = true;
        using (Process p = Process.Start(psi))
        {
            while (!p.WaitForExit(250)) if (cancelReq) { try { p.Kill(); } catch { } return -999; }
            return p.ExitCode;
        }
    }
    static int Run(string exe, string args)
    {
        ProcessStartInfo psi = new ProcessStartInfo(exe, args);
        psi.UseShellExecute = false; psi.CreateNoWindow = true;
        using (Process p = Process.Start(psi)) { p.WaitForExit(); return p.ExitCode; }
    }
    string Defender(string r)
    {
        string mp = MpExe();
        if (mp == null) { Log("Defender/Security Essentials tiada. Guna antivirus lain untuk imbas drive ini."); return T("tidak tersedia"); }
        Log("Imbasan Defender pada {0} ... (boleh ambil masa, tunggu sehingga siap)", r);
        DateTime t0 = DateTime.Now;
        int c = RunCancel(mp, "-Scan -ScanType 3 -File \"" + r + "\\\"");
        if (c == -999) { Log("Imbasan dibatalkan."); return T("dibatalkan"); }   // \\" supaya garis condong hujung tak melarikan petikan
        if (c == 0) { Log("Imbasan siap: tiada ancaman."); return T("tiada ancaman"); }
        int n = Threats(t0);
        if (n > 0) return string.Format(T("{0} ancaman dirawat"), n);
        if (n == 0) { Log("Defender tak dapat mengimbas (mungkin dimatikan kerana antivirus lain aktif)."); return T("tidak tersedia"); }   // tiada rekod ancaman dan kod bukan 0: imbasan tak berlaku
        Log("Imbasan siap (kod {0}): ancaman mungkin dikesan/dirawat - semak Sejarah (History) dalam Defender/Security Essentials.", c);
        return T("selesai (semak Sejarah Defender)");
    }
    // senaraikan ancaman yang direkod Defender selepas 'since'; pulang bilangan, atau -1 jika WMI Defender tiada (cth. Windows 7)
    int Threats(DateTime since)
    {
        try
        {
            int n = 0;
            using (ManagementObjectSearcher s = new ManagementObjectSearcher(@"root\Microsoft\Windows\Defender", "SELECT * FROM MSFT_MpThreatDetection"))
                foreach (ManagementObject o in s.Get())
                {
                    if (ManagementDateTimeConverter.ToDateTime((string)o["InitialDetectionTime"]) <= since) continue;
                    string[] res = o["Resources"] as string[];
                    Log("Dikesan dan dirawat Defender: {0}", res == null ? "?" : string.Join(", ", res));
                    n++;
                }
            return n;
        }
        catch { return -1; }
    }
    void UpdateDef()
    {
        Log("Mengemas kini definisi virus Defender (perlu internet) ...");
        string mp = MpExe();
        if (mp == null) { Log("Defender/Security Essentials tiada."); return; }
        if (Run(mp, "-SignatureUpdate") == 0) Log("Definisi virus dikemas kini."); else Log("Kemas kini gagal (semak internet).");
    }

    // Malwarebytes/ESET percuma tiada mod arahan: kita hanya bukanya (atau halaman muat turun rasmi)
    void Ext(string nama, string[] paths, string url)
    {
        foreach (string p in paths)
            if (File.Exists(p)) { Log("Membuka {0} - pilih imbasan Custom dan pilih drive flashdisk.", nama); Process.Start(p); return; }
        Log("{0} belum dipasang/dimuat turun. Membuka halaman rasmi: muat turun, jalankan, kemudian imbas drive flashdisk.", nama);
        Process.Start(url);
    }

    // ---------- logik: pengurus program permulaan PC ----------
    // sumber (hive): HKCU / HKLM / HKLM32 = kunci Run; FU / FC = folder Startup pengguna / semua pengguna.
    // payload baris senarai: KEADAAN|hive|nama|perintah; KEADAAN = ON (hidup), OFF (dimatikan oleh program ini), WIN (dimatikan oleh Windows/Task Manager)
    // dimatikan = nilai Run dibuang / fail dialih ke folder program ini, dengan sandaran "hive|nama|perintah" supaya boleh dihidupkan semula
    const string Run32Path = @"Software\Wow6432Node\Microsoft\Windows\CurrentVersion\Run";
    const string ApprPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\";
    static bool IsFolderHive(string h) { return h == "FU" || h == "FC"; }
    static RegistryKey RootOf(string h) { return (h == "HKCU" || h == "FU") ? Registry.CurrentUser : Registry.LocalMachine; }
    static string RunSub(string h) { return h == "HKLM32" ? Run32Path : RunPath; }
    static string ApprSub(string h) { return ApprPath + (IsFolderHive(h) ? "StartupFolder" : (h == "HKLM32" ? "Run32" : "Run")); }
    static string StartDir(string h)
    {
        if (h == "FU") return Environment.GetFolderPath(Environment.SpecialFolder.Startup);
        string pd = Environment.GetEnvironmentVariable("ProgramData") ?? Environment.GetEnvironmentVariable("ALLUSERSPROFILE");   // .NET 3.5 tiada CommonStartup
        return pd == null ? null : Path.Combine(pd, @"Microsoft\Windows\Start Menu\Programs\Startup");
    }
    string DisDir { get { return Path.Combine(Path.GetDirectoryName(SettingsPath), "permulaan-dimatikan"); } }
    string SrcLabel(string h) { return T(h == "HKCU" ? "Pengguna" : h == "HKLM" ? "Sistem" : h == "HKLM32" ? "Sistem (32-bit)" : h == "FU" ? "Folder Startup (pengguna)" : "Folder Startup (semua pengguna)"); }
    // Windows 8+: Task Manager menyimpan keadaan hidup/mati dalam StartupApproved (bait pertama ganjil = mati)
    static bool WinOff(string h, string name)
    {
        try
        {
            using (RegistryKey k = RootOf(h).OpenSubKey(ApprSub(h)))
            {
                byte[] b = k == null ? null : k.GetValue(name) as byte[];
                return b != null && b.Length > 0 && (b[0] & 1) == 1;
            }
        }
        catch { return false; }
    }
    static void SetApproved(string h, string name, bool on)
    {
        using (RegistryKey k = RootOf(h).CreateSubKey(ApprSub(h))) { byte[] b = new byte[12]; b[0] = (byte)(on ? 2 : 3); k.SetValue(name, b, RegistryValueKind.Binary); }
    }
    void StartRow(string h, string n, string v, List<char> fixedL, bool all, List<string[]> flagged, List<string[]> normal, List<string[]> off)
    {
        bool wOff = WinOff(h, n);
        List<string> why = new List<string>();
        if (Regex.IsMatch(v, @"wscript|cscript|\.vbs\b|\.scr\b", RegexOptions.IgnoreCase)) why.Add(T("skrip (wscript/cscript/.vbs/.scr)"));
        if (Regex.IsMatch(v, @"\\Temp\\|\\RECYCLER", RegexOptions.IgnoreCase)) why.Add(T("dalam folder Temp/RECYCLER"));
        Match m = Regex.Match(v, "^\"?([A-Za-z]):\\\\");
        if (m.Success && !fixedL.Contains(char.ToUpper(m.Groups[1].Value[0]))) why.Add(T("dari drive luar") + " (" + m.Groups[1].Value.ToUpper() + ":)");
        string pub = "", path = ExePath(v);
        try
        {
            if (path != null && Path.IsPathRooted(path))
            {
                if (!File.Exists(path)) why.Add(T("fail program tiada (entri rosak)"));
                else if (path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) pub = FileVersionInfo.GetVersionInfo(path).CompanyName ?? "";
            }
        }
        catch { }
        string note = why.Count > 0 ? string.Join("; ", why.ToArray()) : (pub.Length > 0 ? pub : T("Biasa (tiada tanda bahaya)"));
        string lvl = why.Count == 0 ? "0" : (why.Count == 1 && why[0] == T("fail program tiada (entri rosak)") ? "1" : "2");   // hijau / jingga (rosak) / merah (mencurigakan)
        string[] row = new string[] { n, T(wOff ? "Mati (Windows)" : "Hidup"), note, v, (wOff ? "WIN" : "ON") + "|" + h + "|" + n + "|" + v, n + " : " + v + "   (" + SrcLabel(h) + ")", lvl };
        if (why.Count > 0 && !wOff) flagged.Add(row); else if (all) (wOff ? off : normal).Add(row);
    }
    void Startup(bool all)
    {
        List<string[]> flagged = new List<string[]>(), normal = new List<string[]>(), off = new List<string[]>();
        List<char> fixedL = new List<char>();
        foreach (DriveInfo d in DriveInfo.GetDrives()) if (d.DriveType == DriveType.Fixed) fixedL.Add(char.ToUpper(d.Name[0]));
        foreach (string h in new string[] { "HKCU", "HKLM", "HKLM32" })
        {
            try
            {
                using (RegistryKey k = RootOf(h).OpenSubKey(RunSub(h)))
                {
                    if (k == null) continue;
                    foreach (string n in k.GetValueNames()) StartRow(h, n, Convert.ToString(k.GetValue(n)), fixedL, all, flagged, normal, off);
                }
            }
            catch { }
        }
        foreach (string h in new string[] { "FU", "FC" })
        {
            try
            {
                string dir = StartDir(h);
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) continue;
                foreach (string f in Directory.GetFiles(dir))
                {
                    string n = Path.GetFileName(f); if (n.ToLower() == "desktop.ini") continue;
                    StartRow(h, n, "\"" + f + "\"", fixedL, all, flagged, normal, off);
                }
            }
            catch { }
        }
        if (all)
            foreach (string l in ReadBackup())
            {
                string[] p = l.Split(new char[] { '|' }, 3);
                off.Add(new string[] { p[1], T("Mati"), T("Dimatikan oleh USB Shield"), IsFolderHive(p[0]) ? "\"" + p[2] + "\"" : p[2], "OFF|" + l, p[1] + " : " + p[2] + "   (" + SrcLabel(p[0]) + ")" });
            }
        normal.Sort((x, y) => string.Compare(x[0], y[0], true)); off.Sort((x, y) => string.Compare(x[0], y[0], true));
        flagged.AddRange(normal); flagged.AddRange(off);
        Ui(() =>
        {
            list2.Items.Clear();
            foreach (string[] w in flagged)
            {
                ListViewItem it = AddRow(list2, w[4], w[5], w[0], w[1], w[2], w[3]);
                if (!w[4].StartsWith("ON|")) { it.ForeColor = Col("#9CA3AF"); it.ImageIndex = 3; }   // dimatikan: kelabu
                else it.ImageIndex = w.Length > 6 ? int.Parse(w[6]) : 0;
            }
            det2.Tag = Leg2; det2.Text = T(Leg2); SetEmpty(list2, flagged.Count == 0 ? (all ? "Tiada program permulaan ditemui." : "Tiada program mencurigakan ditemui.") : "");
        });
        Log("Semakan permulaan PC: {0} entri.", flagged.Count);
    }
    void ActOpenLoc()
    {
        if (list2.SelectedItems.Count == 0) { Info("Pilih satu baris dahulu."); return; }
        string cmd = list2.SelectedItems[0].SubItems[3].Text, p = ExePath(cmd);
        if (p != null && File.Exists(p)) Process.Start("explorer.exe", "/select,\"" + p + "\"");
        else Log("Fail tidak ditemui: {0}", cmd);
    }
    static string ExePath(string cmd)
    {
        cmd = Environment.ExpandEnvironmentVariables(cmd.Trim());
        if (cmd.StartsWith("\"")) { int q = cmd.IndexOf('"', 1); return q > 1 ? cmd.Substring(1, q - 1) : null; }
        Match m = Regex.Match(cmd, @"^(.+?\.(exe|bat|cmd|vbs|scr|com|js|lnk))(\s|$)", RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value : cmd;
    }
    // sandaran entri yang dimatikan oleh program ini: baris "hive|nama|perintah"
    string BackupPath { get { return Path.Combine(Path.GetDirectoryName(SettingsPath), "permulaan-sandaran.txt"); } }
    List<string> ReadBackup()
    {
        List<string> l = new List<string>();
        if (File.Exists(BackupPath)) foreach (string x in File.ReadAllLines(BackupPath, Utf8)) if (x.Split('|').Length >= 3) l.Add(x);
        return l;
    }
    void DisableItem(string payload)
    {
        string[] p = payload.Split(new char[] { '|' }, 4);
        if (p.Length < 4 || p[0] != "ON") return;   // sudah mati
        string h = p[1], n = p[2], keep = p[3];
        if (IsFolderHive(h))
        {
            keep = Path.Combine(StartDir(h), n);
            Directory.CreateDirectory(DisDir); File.Move(keep, Path.Combine(DisDir, h + "-" + n));
        }
        else using (RegistryKey k = RootOf(h).OpenSubKey(RunSub(h), true)) k.DeleteValue(n, true);
        Directory.CreateDirectory(Path.GetDirectoryName(BackupPath));
        File.AppendAllText(BackupPath, h + "|" + n + "|" + keep + "\r\n", Utf8);   // ditulis selepas berjaya supaya tiada rekod palsu
    }
    void EnableItem(string payload)
    {
        string[] p = payload.Split(new char[] { '|' }, 4);
        if (p[0] == "WIN") { SetApproved(p[1], p[2], true); return; }
        if (p[0] != "OFF") return;   // sudah hidup
        string line = payload.Substring(4), h = p[1], n = p[2], cmd = p.Length > 3 ? p[3] : "";
        if (IsFolderHive(h))
        {
            string src = Path.Combine(DisDir, h + "-" + n);
            if (!File.Exists(src)) throw new FileNotFoundException(T("Fail asal tiada dalam folder dimatikan."));
            File.Move(src, Path.Combine(StartDir(h), n));
        }
        else using (RegistryKey k = RootOf(h).CreateSubKey(RunSub(h))) k.SetValue(n, cmd, RegistryValueKind.String);
        List<string> left = new List<string>(); bool dropped = false;
        foreach (string l in ReadBackup()) { if (!dropped && l == line) { dropped = true; continue; } left.Add(l); }
        File.WriteAllLines(BackupPath, left.ToArray(), Utf8);
    }
    void ActDisable()
    {
        string m = T("Matikan program ini daripada permulaan? Program tidak dipadam, ia cuma tak berjalan sendiri lagi. Anda boleh menghidupkannya semula dengan butang Hidupkan ditanda.");
        foreach (ListViewItem x in list2.CheckedItems)
            if (x.SubItems[2].Text.IndexOf("Microsoft", StringComparison.OrdinalIgnoreCase) >= 0) { m += "\n\n" + T("Nota: sebahagian entri ini daripada Microsoft/Windows. Matikan hanya jika anda tahu ia tak diperlukan."); break; }
        DoChecked(list2, i => DisableItem(i), m);
        Startup(true);
    }
    void ActEnable() { DoChecked(list2, i => EnableItem(i), "Hidupkan program ini semula supaya berjalan bila Windows bermula?"); Startup(true); }
    void ActStartAll() { Work("menyemak permulaan PC", () => Startup(true)); }

    // ---------- logik: AutoPlay, log ----------
    void AutoOff()
    {
        if (!Ask("Matikan AutoPlay/AutoRun untuk akaun ini? Flashdisk tak akan dibuka atau dijalankan sendiri. Berkuat kuasa selepas log keluar dan masuk semula.", MessageBoxIcon.Question)) return;
        using (RegistryKey k = Registry.CurrentUser.CreateSubKey(ApKey1)) k.SetValue("NoDriveTypeAutoRun", 255, RegistryValueKind.DWord);
        using (RegistryKey k = Registry.CurrentUser.CreateSubKey(ApKey2)) k.SetValue("DisableAutoplay", 1, RegistryValueKind.DWord);
        Log("AutoPlay/AutoRun dimatikan untuk akaun ini.");
    }
    void AutoOn()
    {
        if (!Ask("Pulihkan tetapan AutoPlay/AutoRun ke lalai Windows?", MessageBoxIcon.Question)) return;
        using (RegistryKey k = Registry.CurrentUser.OpenSubKey(ApKey1, true)) if (k != null) k.DeleteValue("NoDriveTypeAutoRun", false);
        using (RegistryKey k = Registry.CurrentUser.OpenSubKey(ApKey2, true)) if (k != null) k.DeleteValue("DisableAutoplay", false);
        Log("Tetapan AutoPlay/AutoRun dipulihkan.");
    }
    void SaveLog()
    {
        SaveFileDialog d = new SaveFileDialog();
        d.Filter = "Teks (*.txt)|*.txt"; d.FileName = "laporan-" + DateTime.Now.ToString("yyyyMMdd-HHmm") + ".txt";
        if (d.ShowDialog(this) == DialogResult.OK)
        {
            File.WriteAllText(d.FileName, "USB Shield - " + DateTime.Now + "\r\n\r\n" + log.Text, Utf8);
            Log("Log disimpan: {0}", d.FileName);
        }
    }

}
