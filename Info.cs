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
    // ---------- maklumat PC: spesifikasi dan kesihatan cakera (baca sahaja, melalui WMI) ----------
    Panel pnlSpec; SpecCanvas canvas; Label specNote; RButton bSpecRef, bSpecCopy;
    static List<ManagementObject> Wq(string scope, string q)
    {
        List<ManagementObject> r = new List<ManagementObject>();
        try { using (ManagementObjectSearcher s = new ManagementObjectSearcher(scope, q)) foreach (ManagementObject m in s.Get()) r.Add(m); } catch { }
        return r;
    }
    static string WP(ManagementBaseObject m, string n) { try { object v = m[n]; return v == null ? "" : v.ToString().Trim(); } catch { return ""; } }
    static long WL(ManagementBaseObject m, string n) { long v; return long.TryParse(WP(m, n), out v) ? v : -1; }
    static string RamType(long t) { switch (t) { case 20: return "DDR"; case 21: return "DDR2"; case 24: return "DDR3"; case 26: return "DDR4"; case 34: return "DDR5"; } return ""; }
    // kad-kad maklumat PC; setiap kad ada ikon dan warna status. 'note' = nasihat di bawah senarai
    List<SpecSec> SpecData(out string note)
    {
        const string CIM = @"root\cimv2", STO = @"root\Microsoft\Windows\Storage";
        List<SpecSec> L = new List<SpecSec>();
        Func<string, string, SpecSec> Add = (title, glyph) => { SpecSec c = new SpecSec(); c.Title = T(title); c.Glyph = glyph; L.Add(c); return c; };
        Action<SpecSec, string, string> R = (c, k, v) => { if (!string.IsNullOrEmpty(v) && v.Trim().Length > 0) c.Rows.Add(new string[] { T(k), v }); };
        SpecSec pc = Add("Komputer", ""), cpu = Add("Pemproses", ""), ram = Add("Memori (RAM)", ""), gpu = Add("Kad grafik", "");
        foreach (ManagementObject m in Wq(CIM, "SELECT Name, Manufacturer, Model, TotalPhysicalMemory FROM Win32_ComputerSystem"))
        {
            pc.Title = WP(m, "Name").Length > 0 ? WP(m, "Name") : pc.Title; pc.Sub = (WP(m, "Manufacturer") + " " + WP(m, "Model")).Trim();
            long tm = WL(m, "TotalPhysicalMemory"); if (tm > 0) ram.Sub = Sz(tm);
        }
        foreach (ManagementObject m in Wq(CIM, "SELECT Caption, Version, OSArchitecture, LastBootUpTime FROM Win32_OperatingSystem"))
        {
            R(pc, "Windows", WP(m, "Caption") + " " + WP(m, "OSArchitecture") + " (build " + WP(m, "Version") + ")");
            try { TimeSpan up = DateTime.Now - ManagementDateTimeConverter.ToDateTime(WP(m, "LastBootUpTime")); R(pc, "Dihidupkan sejak", string.Format(T("{0} hari {1} jam"), up.Days, up.Hours)); } catch { }
        }
        foreach (ManagementObject m in Wq(CIM, "SELECT Manufacturer, Product FROM Win32_BaseBoard")) R(pc, "Papan induk", (WP(m, "Manufacturer") + " " + WP(m, "Product")).Trim());
        foreach (ManagementObject m in Wq(CIM, "SELECT SMBIOSBIOSVersion FROM Win32_BIOS")) R(pc, "BIOS", WP(m, "SMBIOSBIOSVersion"));
        foreach (ManagementObject m in Wq(CIM, "SELECT Name, NumberOfCores, NumberOfLogicalProcessors, MaxClockSpeed FROM Win32_Processor"))
        {
            cpu.Sub = Regex.Replace(WP(m, "Name"), @"\s+", " "); R(cpu, "Teras", string.Format(T("{0} teras, {1} thread"), WP(m, "NumberOfCores"), WP(m, "NumberOfLogicalProcessors")));
            long mhz = WL(m, "MaxClockSpeed"); if (mhz > 0) R(cpu, "Kelajuan", (mhz / 1000.0).ToString("0.0") + " GHz");
        }
        foreach (ManagementObject m in Wq(CIM, "SELECT Capacity, Speed, SMBIOSMemoryType, DeviceLocator FROM Win32_PhysicalMemory"))
        {
            long cap = WL(m, "Capacity"), sp = WL(m, "Speed"); string loc = WP(m, "DeviceLocator");
            R(ram, "Modul", ((cap > 0 ? Sz(cap) : "") + " " + RamType(WL(m, "SMBIOSMemoryType")) + (sp > 0 ? " " + sp + " MHz" : "") + (loc.Length > 0 ? "  (" + loc + ")" : "")).Trim());
        }
        bool first = true;
        foreach (ManagementObject m in Wq(CIM, "SELECT Name, DriverVersion FROM Win32_VideoController"))
        {
            if (first) { gpu.Sub = WP(m, "Name"); R(gpu, "Pemacu", WP(m, "DriverVersion")); first = false; }
            else R(gpu, "Kad grafik", WP(m, "Name"));
        }
        // ruang drive
        SpecSec fr = Add("Ruang drive", ""); fr.Level = 1; fr.Sub = T("Ruang mencukupi");
        foreach (DriveInfo d in DriveInfo.GetDrives())
            if (d.DriveType == DriveType.Fixed && d.IsReady)
            {
                double pct = 100.0 * d.AvailableFreeSpace / d.TotalSize;
                R(fr, d.Name, Math.Round(pct) + "% " + T("bebas") + "  (" + Sz(d.AvailableFreeSpace) + " / " + Sz(d.TotalSize) + ")");
                if (pct < 10) { fr.Level = 2; fr.Sub = T("Ada drive hampir penuh, ini melambatkan PC"); }
            }
        // setiap cakera fizikal
        List<ManagementObject> pd = Wq(STO, "SELECT * FROM MSFT_PhysicalDisk"), rc = Wq(STO, "SELECT * FROM MSFT_StorageReliabilityCounter");
        List<ManagementObject> sm = Wq(@"root\wmi", "SELECT * FROM MSStorageDriver_FailurePredictStatus"), sd = Wq(@"root\wmi", "SELECT * FROM MSStorageDriver_FailurePredictData");
        foreach (ManagementObject dd in Wq(CIM, "SELECT Index, Model, Size, PNPDeviceID, Status, InterfaceType FROM Win32_DiskDrive"))
        {
            string idx = WP(dd, "Index"), pnp = WP(dd, "PNPDeviceID").ToLower();
            ManagementObject phys = null, cnt = null, fs = null, fd = null;
            foreach (ManagementObject m in pd) if (WP(m, "DeviceId") == idx) phys = m;
            foreach (ManagementObject m in rc) if (WP(m, "DeviceId") == idx) cnt = m;
            if (pnp.Length > 0) { foreach (ManagementObject m in sm) if (WP(m, "InstanceName").ToLower().StartsWith(pnp)) fs = m; foreach (ManagementObject m in sd) if (WP(m, "InstanceName").ToLower().StartsWith(pnp)) fd = m; }
            long size = WL(dd, "Size"), mt = phys != null ? WL(phys, "MediaType") : -1, bt = phys != null ? WL(phys, "BusType") : -1;
            string media = mt == 3 ? "HDD" : mt == 4 ? "SSD" : mt == 5 ? "SCM" : "", bus = bt == 17 ? "NVMe" : bt == 11 ? "SATA" : bt == 7 ? "USB" : bt == 3 ? "ATA" : bt == 8 ? "RAID" : (phys == null ? WP(dd, "InterfaceType") : "");
            string nm = phys != null && WP(phys, "FriendlyName").Length > 0 ? WP(phys, "FriendlyName") : WP(dd, "Model");
            SpecSec c = Add(nm, bus == "USB" ? "" : media == "HDD" ? "" : ""); c.Title = nm;
            R(c, "Jenis", (media + " " + bus).Trim()); R(c, "Saiz", size > 0 ? Sz(size) : "");
            // atribut S.M.A.R.T lama (SATA): 12 bait setiap atribut bermula pada bait ke-2; nilai mentah 6 bait little-endian
            Dictionary<int, long> at = new Dictionary<int, long>();
            byte[] vs = null; try { vs = fd != null ? fd["VendorSpecific"] as byte[] : null; } catch { }
            if (vs != null) for (int o = 2; o + 12 <= vs.Length && o < 362; o += 12) { int id = vs[o]; if (id == 0) continue; long raw = 0; for (int k = 5; k >= 0; k--) raw = raw * 256 + vs[o + 5 + k]; at[id] = raw; }
            long hs = phys != null ? WL(phys, "HealthStatus") : -1;
            long temp = cnt != null ? WL(cnt, "Temperature") : -1; if (temp <= 0 && at.ContainsKey(194)) temp = at[194] & 0xFF;
            long poh = cnt != null ? WL(cnt, "PowerOnHours") : -1; if (poh <= 0 && at.ContainsKey(9)) poh = at[9] & 0xFFFFFFFFL;
            long wear = cnt != null ? WL(cnt, "Wear") : -1;
            long realloc = at.ContainsKey(5) ? at[5] : -1, pend = at.ContainsKey(197) ? at[197] : -1;
            bool bad = hs == 2 || (fs != null && WP(fs, "PredictFailure").ToLower() == "true");
            bool warn = hs == 1 || (temp >= 55) || realloc > 0 || pend > 0 || (wear >= 80 && (media == "SSD" || bus == "NVMe"));
            bool known = hs >= 0 || fs != null || cnt != null;
            if (bad) { c.Level = 3; c.Sub = T("BERISIKO: sandarkan data anda SEKARANG"); }
            else if (warn) { c.Level = 2; c.Sub = T("AMARAN: semak butiran di bawah dan sandarkan data"); }
            else if (known) { c.Level = 1; c.Sub = T("Sihat: tiada tanda masalah"); }
            else { c.Level = 0; c.Sub = T("Tak dapat disahkan (cakera ini tak melapor data kesihatan)"); }
            if (temp > 0) R(c, "Suhu", temp + " C" + (temp >= 55 ? "   <- " + T("terlalu panas") : ""));
            if (poh > 0) R(c, "Dipasang hidup", string.Format(T("{0} jam (kira-kira {1} hari)"), poh, poh / 24));
            if (wear >= 0 && (media == "SSD" || bus == "NVMe")) R(c, "Kehausan SSD", wear + "% (" + T("baki hayat kira-kira") + " " + (100 - wear) + "%)");
            if (realloc >= 0) R(c, "Sektor rosak dipindahkan", realloc.ToString());
            if (pend >= 0) R(c, "Sektor menunggu", pend.ToString());
        }
        note = IsAdmin ? "" : T("Mahu suhu dan jam cakera? Klik Jalankan sebagai Admin, kemudian Muat semula.");
        return L;
    }
    // versi teks biasa untuk butang Salin
    static string SpecPlain(List<SpecSec> L, string note)
    {
        StringBuilder b = new StringBuilder(); string nl = "\r\n";
        foreach (SpecSec c in L)
        {
            b.Append("== " + c.Title + " ==" + nl);
            if (c.Sub.Length > 0) b.Append("  " + c.Sub + nl);
            foreach (string[] r in c.Rows) b.Append("  " + r[0] + ": " + r[1] + nl);
            b.Append(nl);
        }
        if (note.Length > 0) b.Append(note + nl);
        return b.ToString();
    }
    string specText = "";
    void ActSpec()
    {
        Work("membaca maklumat PC", () =>
        {
            string note; List<SpecSec> L = SpecData(out note); string txt = SpecPlain(L, note);
            Ui(() => { specText = txt; canvas.Secs = L; canvas.Rebuild(); specNote.Tag = note; specNote.Text = note; pnlSpec.AutoScrollPosition = new Point(0, 0); });
        });
    }
    void ActSpecCopy()
    {
        if (specText.Length == 0) { Info("Tiada maklumat untuk disalin. Klik Muat semula dahulu."); return; }
        try { Clipboard.SetText(specText); Info("Maklumat PC sudah disalin ke papan klip."); } catch (Exception ex) { Info(T("Gagal menyalin") + ": " + ex.Message); }
    }

    // pelan kuasa: guna powercfg; pelan asal disimpan supaya boleh dipulihkan
    const string HighPlan = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";
    string PowBackup { get { return Path.Combine(Path.GetDirectoryName(SettingsPath), "kuasa-sandaran.txt"); } }
    static string RunOut(string exe, string args)
    {
        ProcessStartInfo psi = new ProcessStartInfo(exe, args);
        psi.UseShellExecute = false; psi.CreateNoWindow = true; psi.RedirectStandardOutput = true;
        using (Process p = Process.Start(psi)) { string o = p.StandardOutput.ReadToEnd(); p.WaitForExit(); return p.ExitCode == 0 ? o : null; }
    }
    static string ActivePlan()
    {
        string o = RunOut("powercfg", "/getactivescheme"); if (o == null) return null;
        Match m = Regex.Match(o, @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}");
        return m.Success ? m.Value.ToLower() : null;
    }
    void PowerHigh()
    {
        string cur = ActivePlan();
        if (cur == null) { Log("Pelan kuasa tak dapat dibaca."); return; }
        if (cur == HighPlan) { Log("Pelan High performance sudah aktif."); return; }
        Directory.CreateDirectory(Path.GetDirectoryName(PowBackup)); File.WriteAllText(PowBackup, cur, Utf8);
        if (RunOut("powercfg", "/setactive " + HighPlan) == null) { Log("Pelan High performance tiada pada PC ini."); File.Delete(PowBackup); return; }
        Log("Pelan kuasa ditukar ke High performance. Klik Pelan kuasa asal untuk memulihkan.");
    }
    void PowerBack()
    {
        string g = File.Exists(PowBackup) ? File.ReadAllText(PowBackup, Utf8).Trim() : null;
        if (g == null || !Regex.IsMatch(g, "^[0-9a-f-]{36}$")) { Log("Tiada pelan kuasa asal yang disimpan."); return; }
        if (RunOut("powercfg", "/setactive " + g) == null) { Log("Gagal memulihkan pelan kuasa."); return; }
        File.Delete(PowBackup); Log("Pelan kuasa asal dipulihkan.");
    }
    void ActPowerHigh()
    {
        if (!Ask("Pelan High performance menggunakan lebih banyak kuasa (bateri komputer riba cepat habis). Teruskan?", MessageBoxIcon.Question)) return;
        Work("menukar pelan kuasa", () => PowerHigh());
    }
    void ActPowerBack() { Work("memulihkan pelan kuasa", () => PowerBack()); }

    // kesan visual: matikan animasi/pudar melalui SystemParametersInfo; nilai asal disimpan untuk dipulihkan
    [DllImport("user32.dll", EntryPoint = "SystemParametersInfo")] static extern bool SpiGet(uint a, uint b, ref int v, uint f);
    [DllImport("user32.dll", EntryPoint = "SystemParametersInfo")] static extern bool SpiSet(uint a, uint b, IntPtr v, uint f);
    [StructLayout(LayoutKind.Sequential)] struct AnimInfo { public uint cbSize; public int iMinAnimate; }
    [DllImport("user32.dll", EntryPoint = "SystemParametersInfo")] static extern bool SpiAnim(uint a, uint b, ref AnimInfo v, uint f);
    // pasangan {kod GET, kod SET}: menu, combobox, skrol licin, tooltip, pudar pilihan/menu/tooltip, animasi kawasan klien
    static readonly uint[][] Fx = {
        new uint[] { 0x1002, 0x1003 }, new uint[] { 0x1004, 0x1005 }, new uint[] { 0x1006, 0x1007 }, new uint[] { 0x1016, 0x1017 },
        new uint[] { 0x1014, 0x1015 }, new uint[] { 0x1012, 0x1013 }, new uint[] { 0x1018, 0x1019 }, new uint[] { 0x1042, 0x1043 } };
    const uint SpiSave = 3;   // SPIF_UPDATEINIFILE | SPIF_SENDCHANGE
    string FxBackup { get { return Path.Combine(Path.GetDirectoryName(SettingsPath), "efek-sandaran.txt"); } }
    void FxOff()
    {
        if (!File.Exists(FxBackup))   // simpan nilai asal sekali sahaja
        {
            List<string> keep = new List<string>(); int v;
            foreach (uint[] f in Fx) { v = 0; if (SpiGet(f[0], 0, ref v, 0)) keep.Add(f[1] + "=" + v); }
            AnimInfo a = new AnimInfo(); a.cbSize = 8; if (SpiAnim(0x48, 8, ref a, 0)) keep.Add("73=" + a.iMinAnimate);   // 0x49 = 73
            Directory.CreateDirectory(Path.GetDirectoryName(FxBackup)); File.WriteAllLines(FxBackup, keep.ToArray(), Utf8);
        }
        foreach (uint[] f in Fx) SpiSet(f[1], 0, IntPtr.Zero, SpiSave);
        AnimInfo z = new AnimInfo(); z.cbSize = 8; z.iMinAnimate = 0; SpiAnim(0x49, 8, ref z, SpiSave);
        Log("Kesan visual dikurangkan. Klik Pulihkan visual untuk mengembalikannya.");
    }
    void FxBack()
    {
        if (!File.Exists(FxBackup)) { Log("Tiada kesan visual asal yang disimpan."); return; }
        foreach (string l in File.ReadAllLines(FxBackup, Utf8))
        {
            string[] kv = l.Split('='); int code, val; if (kv.Length != 2 || !int.TryParse(kv[0], out code) || !int.TryParse(kv[1], out val)) continue;
            if (code == 73) { AnimInfo a = new AnimInfo(); a.cbSize = 8; a.iMinAnimate = val; SpiAnim(0x49, 8, ref a, SpiSave); }
            else SpiSet((uint)code, 0, (IntPtr)val, SpiSave);
        }
        File.Delete(FxBackup); Log("Kesan visual asal dipulihkan.");
    }
    void ActFxOff()
    {
        if (!Ask("Animasi menu/tetingkap dan kesan pudar akan dimatikan supaya PC terasa lebih laju. Anda boleh memulihkannya. Teruskan?", MessageBoxIcon.Question)) return;
        Work("mengurangkan kesan visual", () => FxOff());
    }
    void ActFxBack() { Work("memulihkan kesan visual", () => FxBack()); }

    // ---------- logik: program dipasang, nyahpasang, dan kemas kini melalui winget (Microsoft; Windows 10/11) ----------
    // payload baris: "I|perintah-nyahpasang" (program dipasang) atau "U|id-winget" (kemas kini tersedia)
    ListView listPr; Label detPr; RButton bPrList, bPrUpd, bPrDo, bPrUn;
    string wingetExe;
    string Winget()
    {
        if (wingetExe == null)
        {
            wingetExe = "";
            string la = Environment.GetEnvironmentVariable("LOCALAPPDATA");
            if (la != null && File.Exists(Path.Combine(la, @"Microsoft\WindowsApps\winget.exe"))) wingetExe = Path.Combine(la, @"Microsoft\WindowsApps\winget.exe");
            else foreach (string d in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';'))
                try { string q = Path.Combine(d.Trim('"'), "winget.exe"); if (d.Length > 0 && File.Exists(q)) { wingetExe = q; break; } } catch { }
        }
        return wingetExe.Length > 0 ? wingetExe : null;
    }
    const string NoWinget = "winget tiada. Ia perlu Windows 10/11 dengan App Installer (Microsoft Store). Di Windows 7 pilihan kemas kini ini tak tersedia.";
    void ProgLoad()
    {
        List<string[]> rows = new List<string[]>(); Dictionary<string, bool> seen = new Dictionary<string, bool>();
        foreach (RegistryKey root in new RegistryKey[] { Registry.LocalMachine, Registry.CurrentUser })
            foreach (string sub in new string[] { @"Software\Microsoft\Windows\CurrentVersion\Uninstall", @"Software\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall" })
                using (RegistryKey k = root.OpenSubKey(sub))
                {
                    if (k == null) continue;
                    foreach (string n in k.GetSubKeyNames())
                        try
                        {
                            using (RegistryKey a = k.OpenSubKey(n))
                            {
                                string name = Convert.ToString(a.GetValue("DisplayName")), us = Convert.ToString(a.GetValue("UninstallString"));
                                if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(us)) continue;
                                if (Convert.ToInt32(a.GetValue("SystemComponent") ?? 0) == 1 || a.GetValue("ParentKeyName") != null) continue;   // komponen sistem / kemas kini Windows
                                string ver = Convert.ToString(a.GetValue("DisplayVersion")), pub = Convert.ToString(a.GetValue("Publisher"));
                                if (seen.ContainsKey(name + "|" + ver)) continue; seen[name + "|" + ver] = true;
                                long kb = 0; try { kb = Convert.ToInt64(a.GetValue("EstimatedSize") ?? 0); } catch { }
                                rows.Add(new string[] { name, ver, pub, kb.ToString(), us });
                            }
                        }
                        catch { }
                }
        rows.Sort((x, y) => { int c = long.Parse(y[3]).CompareTo(long.Parse(x[3])); return c != 0 ? c : string.Compare(x[0], y[0], true); });   // terbesar dahulu
        Ui(() =>
        {
            prInst.Clear(); prAll = prInst; prKind = 1; prInstLoaded = true; prEmpty = "Tiada program ditemui."; PrCol("Penerbit");
            foreach (string[] r in rows) prAll.Add(MakeItem("I|" + r[4], r[0] + (r[1].Length > 0 ? "  " + r[1] : "") + (r[2].Length > 0 ? "   (" + r[2] + ")" : ""), r[0], r[1], r[2], r[3] == "0" ? "" : Sz(long.Parse(r[3]) * 1024)));
            foreach (ListViewItem pi in prAll) pi.ImageIndex = 4;
            detPr.Text = T(Hint); PrFilter();
        });
        Log("Senarai program dimuat: {0} program (terbesar dahulu).", rows.Count);
    }
    // jadual "winget upgrade": lajur diambil daripada kedudukan kepala jadual (Nama, Id, Versi, Tersedia); baris tanpa versi baharu diabaikan
    static List<string[]> ParseUpgrade(string text)
    {
        List<string[]> res = new List<string[]>();
        string[] ls = text.Split('\n'); int[] cols = null;
        for (int i = 0; i < ls.Length; i++)
        {
            string l = ls[i]; int cr = l.LastIndexOf('\r'); if (cr >= 0 && cr < l.Length - 1) l = l.Substring(cr + 1); l = l.TrimEnd('\r', '\n');
            if (Regex.IsMatch(l, @"^-{10,}\s*$") && i > 0)
            {
                string h = ls[i - 1].TrimEnd('\r'); cr = h.LastIndexOf('\r'); if (cr >= 0) h = h.Substring(cr + 1);
                MatchCollection mc = Regex.Matches(h, @"\S+"); if (mc.Count < 4) { cols = null; continue; }
                cols = new int[mc.Count]; for (int k = 0; k < mc.Count; k++) cols[k] = mc[k].Index;
                continue;
            }
            if (cols == null) continue;
            if (l.Trim().Length == 0) { cols = null; continue; }   // jadual tamat
            if (l.Length <= cols[3]) continue;
            string name = l.Substring(0, cols[1]).Trim(), id = l.Substring(cols[1], cols[2] - cols[1]).Trim(), cur = l.Substring(cols[2], cols[3] - cols[2]).Trim();
            string av = (cols.Length > 4 && l.Length > cols[4] ? l.Substring(cols[3], cols[4] - cols[3]) : l.Substring(cols[3])).Trim();
            if (id.Length > 0 && av.Length > 0 && Regex.IsMatch(id, @"^[\w.\-+]+$")) res.Add(new string[] { name, id, cur, av });
        }
        return res;
    }
    // jalankan winget; pulang output (null jika dibatalkan atau tamat masa)
    // wingetLive: dipanggil (benang latar) dengan peratus/baris terkini;
    DateTime wgLast = DateTime.MinValue; string wgLastTxt = "";
    string wgNm; int wgCn, wgTot;   // program yang sedang dikemas kini (null = tiada)
    static double ToMb(string num, string unit)
    {
        double v; if (!double.TryParse(num.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v)) return 0;
        return unit == "KB" ? v / 1024 : unit == "GB" ? v * 1024 : v;
    }
    // baca output winget (bar muat turun "17.9 MB / 17.9 MB" dan fasa) dan paparkan dalam app: bar peratus sebenar + teks status + log
    void WgLive(string seg)
    {
        string nm = wgNm; if (nm == null) return;
        string s = seg.Trim(); if (s.Length == 0) return;
        string txt; int pct = -1; bool phase = false;
        Match mb = Regex.Match(s, @"([\d.,]+)\s*(KB|MB|GB)\s*/\s*([\d.,]+)\s*(KB|MB|GB)"), mp = Regex.Match(s, @"(\d{1,3})\s*%");
        if (mb.Success)
        {
            double a = ToMb(mb.Groups[1].Value, mb.Groups[2].Value), b = ToMb(mb.Groups[3].Value, mb.Groups[4].Value);
            if (b > 0) pct = (int)Math.Min(100, 100 * a / b);
            txt = T("memuat turun") + " " + mb.Groups[1].Value + " / " + mb.Groups[3].Value + " " + mb.Groups[4].Value + (pct >= 0 ? " (" + pct + "%)" : "");
        }
        else if (mp.Success) { pct = Math.Min(100, int.Parse(mp.Groups[1].Value)); txt = T("memuat turun") + " " + pct + "%"; }
        else if (s.StartsWith("Downloading")) { txt = T("memuat turun"); phase = true; }
        else if (s.StartsWith("Successfully verified installer hash")) { txt = T("cap pengesahan fail sah"); phase = true; }
        else if (s.StartsWith("Starting package install")) { txt = T("memasang (boleh ambil masa)"); phase = true; }
        else if (s.StartsWith("Successfully installed")) { txt = T("berjaya dipasang"); phase = true; }
        else return;
        if (!phase && (txt == wgLastTxt || (DateTime.Now - wgLast).TotalMilliseconds < 200)) return;   // elak banjir kemas kini UI
        wgLastTxt = txt; wgLast = DateTime.Now;
        Status("mengemas kini {0} ({1}/{2}): {3}", nm, wgCn, wgTot, txt);
        int v = phase ? -1 : pct; Ui(() => bar.Value = v);   // fasa tanpa peratus: bar bergerak semula
        if (phase) Log("  {0}", txt);
    }
    string WingetRun(string args, int timeoutMs, out int code) { return WingetRun(args, timeoutMs, false, out code); }
    string WingetRun(string args, int timeoutMs, bool visible, out int code)
    {
        code = -1;
        ProcessStartInfo psi;
        if (visible)   // tetingkap konsol winget dibiarkan terbuka kalau gagal supaya mesejnya sempat dibaca
        {
            psi = new ProcessStartInfo("cmd.exe", "/c \"\"" + Winget() + "\" " + args + " || (echo. & echo Gagal. Tekan sebarang kekunci untuk menutup. & pause >nul & exit /b 1)\"");
            psi.UseShellExecute = false; psi.CreateNoWindow = false;
        }
        else
        {
            psi = new ProcessStartInfo(Winget(), args);
            psi.UseShellExecute = false; psi.CreateNoWindow = true; psi.RedirectStandardOutput = true; psi.StandardOutputEncoding = Encoding.UTF8;
        }
        StringBuilder sb = new StringBuilder(); Thread rd = null;
        using (Process p = Process.Start(psi))
        {
            if (!visible)
            {
                // baca aksara demi aksara: bar kemajuan winget guna \r (bukan baris baharu)
                rd = new Thread(() =>
                {
                    StringBuilder cur = new StringBuilder(); int ch;
                    try { while ((ch = p.StandardOutput.Read()) >= 0) { char c = (char)ch; lock (sb) sb.Append(c); if (c == '\r' || c == '\n') { WgLive(cur.ToString()); cur.Length = 0; } else cur.Append(c); } } catch { }
                });
                rd.IsBackground = true; rd.Start();
            }
            DateTime t0 = DateTime.Now;
            while (!p.WaitForExit(250))
                if (cancelReq || (DateTime.Now - t0).TotalMilliseconds > timeoutMs)
                {
                    try { if (visible) Process.Start(new ProcessStartInfo("taskkill", "/PID " + p.Id + " /T /F") { CreateNoWindow = true, UseShellExecute = false }); else p.Kill(); } catch { }
                    return null;
                }
            p.WaitForExit(); if (rd != null) rd.Join(3000); code = p.ExitCode;
        }
        lock (sb) return sb.ToString();
    }
    // semakan kemas kini harian: simpan bilangan terkini; updN -1 = tak dapat disemak (tiada winget atau internet)
    int updN = -1; string updDay = "";
    static string Today() { return DateTime.Now.ToString("yyyyMMdd"); }
    void UpdProbe()
    {
        if (string.IsNullOrEmpty(Winget())) return;
        int rc; string o = WingetRun("upgrade --accept-source-agreements", 180000, out rc);
        if (o == null) return;
        List<string[]> ups = ParseUpgrade(o);
        if (ups.Count == 0 && rc != 0 && o.IndexOf("No installed package", StringComparison.OrdinalIgnoreCase) < 0 && o.IndexOf("No applicable", StringComparison.OrdinalIgnoreCase) < 0) return;   // gagal, bukan "tiada kemas kini"
        updN = ups.Count; updDay = Today();
    }
    void UpdBg()
    {
        if (updDay == Today() || string.IsNullOrEmpty(Winget())) return;
        Thread t = new Thread(() => { try { UpdProbe(); if (updDay == Today()) Ui(() => { SaveSettings(); UpdCard(); }); } catch { } });
        t.IsBackground = true; t.Start();
    }
    void UpdCard() { int n = updN; HcSet(4, n < 0 ? 0 : n == 0 ? 1 : 2, n < 0 ? T("Kemas kini program tak dapat disemak (perlu winget dan internet).") : n == 0 ? T("Semua program sudah terkini.") : string.Format(T("{0} program ada kemas kini."), n), n > 0); }
    void ProgUpdates()
    {
        Log("Menyemak kemas kini dengan winget (perlu internet, boleh ambil masa) ...");
        int rc; string o = WingetRun("upgrade --accept-source-agreements", 180000, out rc);
        if (o == null) { Log("Semakan kemas kini dibatalkan atau tamat masa."); return; }
        List<string[]> ups = ParseUpgrade(o);
        Ui(() =>
        {
            prUpd.Clear(); prAll = prUpd; prKind = 2; prEmpty = "Semua program sudah terkini (atau tiada yang winget kenali)."; PrCol("Versi baharu");
            foreach (string[] u in ups) prAll.Add(MakeItem("U|" + u[1], u[0] + "  " + u[2] + " -> " + u[3] + "   (" + u[1] + ")", u[0], u[2], u[3], ""));
            foreach (ListViewItem pu in prAll) pu.ImageIndex = 1;
            detPr.Text = T(Hint); PrFilter();
        });
        Log("Kemas kini tersedia: {0}.", ups.Count);
    }
    // senarai penuh disimpan di prAll; ListView hanya memaparkan yang sepadan dengan kotak carian
    List<ListViewItem> prAll = new List<ListViewItem>(); string prEmpty = "Klik Senarai program dipasang atau Semak kemas kini."; TextBox txtPr; RButton bPrAll;
    void PrCol(string ms) { ColumnHeader h = listPr.Columns[2]; h.Tag = ms; h.Text = T(ms); }
    void PrFilter()
    {
        string q = txtPr.Text.Trim();
        listPr.BeginUpdate(); listPr.Items.Clear();
        foreach (ListViewItem i in prAll) if (q.Length == 0 || i.ToolTipText.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) listPr.Items.Add(i);
        listPr.EndUpdate();
        SetEmpty(listPr, prAll.Count > 0 && listPr.Items.Count == 0 ? "Tiada program sepadan dengan carian itu." : prEmpty);
    }
    // tanda semua baris yang dipaparkan; kalau semuanya sudah ditanda, nyahtanda semua
    void PrToggleAll()
    {
        bool all = listPr.Items.Count > 0; foreach (ListViewItem i in listPr.Items) if (!i.Checked) { all = false; break; }
        foreach (ListViewItem i in listPr.Items) i.Checked = !all;
    }
    // sebab gagal winget dalam bahasa mudah (dibaca daripada output winget)
    string WingetWhy(string o, int rc)
    {
        if (o == null) return T("dibatalkan atau tamat masa");
        if (Regex.IsMatch(o, @"(?i)\b404\b|not found|0x80190194")) return T("pautan muat turun sudah rosak di katalog winget (bukan salah anda). Cuba lagi beberapa hari lagi, atau muat turun dari laman rasmi program itu");
        if (Regex.IsMatch(o, @"(?i)hash")) return T("fail muat turun tak sepadan dengan cap pengesahan, jadi ditolak demi keselamatan");
        if (Regex.IsMatch(o, @"(?i)0x80072ee|0x80072efd|internet|network|timed out|connection")) return T("masalah sambungan internet");
        if (Regex.IsMatch(o, @"(?i)0x80070005|access is denied|administrator|elevat")) return T("perlu kebenaran Administrator (klik Jalankan sebagai Admin)");
        if (Regex.IsMatch(o, @"(?i)in use|is running|being used|close the app")) return T("program itu sedang dibuka. Tutup dahulu, kemudian cuba lagi");
        if (Regex.IsMatch(o, @"(?i)no applicable|no newer|no available upgrade")) return T("tiada versi baharu yang sesuai untuk PC ini");
        if (Regex.IsMatch(o, @"(?i)installer failed")) return T("pemasang program itu sendiri gagal. Tutup program itu dan cuba lagi");
        return T("sebab tak diketahui, kod") + " " + rc;
    }
    // baris terakhir output winget yang bermakna, untuk log
    static string LastLine(string o)
    {
        if (o == null) return "";
        string[] ls = o.Split('\n'); string res = "";
        foreach (string x in ls) { string l = x.TrimEnd('\r'); int cr = l.LastIndexOf('\r'); if (cr >= 0) l = l.Substring(cr + 1); l = l.Trim(); if (Regex.IsMatch(l, @"[A-Za-z]{4,}")) res = l; }
        return res.Length > 140 ? res.Substring(0, 140) : res;
    }
    void ActProgList() { Work("memuatkan senarai program", () => ProgLoad()); }
    void ActProgUpd()
    {
        if (Winget() == null) { Info(NoWinget); return; }
        Work("menyemak kemas kini (winget)", () => ProgUpdates(), true);
    }
    void ActProgDo()
    {
        List<ListViewItem> sel = new List<ListViewItem>();
        foreach (ListViewItem i in listPr.CheckedItems) if (((string)i.Tag).StartsWith("U|")) sel.Add(i);
        if (sel.Count == 0) { Info("Tanda program dalam senarai kemas kini (klik Semak kemas kini dahulu)."); return; }
        if (Winget() == null) { Info(NoWinget); return; }
        List<string> names = new List<string>(); foreach (ListViewItem i in sel) names.Add(i.ToolTipText);
        if (!Ask(T("Kemas kini program ini melalui winget? Pemasang rasmi akan dimuat turun dan dijalankan. Sesetengah program perlu ditutup dahulu atau meminta kebenaran Administrator.") + "\n\n" + string.Join("\n", names.ToArray()), MessageBoxIcon.Question)) return;
        List<string[]> todo = new List<string[]>(); foreach (ListViewItem i in sel) todo.Add(new string[] { ((string)i.Tag).Substring(2), i.Text });
        Work("mengemas kini program (winget)", () =>
        {
            List<string> done = new List<string>(), bad = new List<string>();
            for (int n = 0; n < todo.Count; n++)
            {
                string[] t = todo[n];
                if (cancelReq) { Log("Dibatalkan."); break; }
                Status("mengemas kini {0} ({1}/{2}) ...", t[1], n + 1, todo.Count);
                wgLastTxt = ""; wgCn = n + 1; wgTot = todo.Count; wgNm = t[1];
                int rc; string o = WingetRun("upgrade --id " + t[0] + " --exact --silent --accept-package-agreements --accept-source-agreements --disable-interactivity", 900000, false, out rc);
                wgNm = null; Ui(() => bar.Value = -1);
                if (o != null && rc == 0) { done.Add(t[0]); Log("Dikemas kini: {0}", t[1]); }
                else
                {
                    string why = WingetWhy(o, rc); bad.Add(t[1] + ": " + why);
                    Log("Gagal: {0}. Sebab: {1}", t[1], why);
                    string ll = LastLine(o); if (ll.Length > 0) Log("Mesej winget: {0}", ll);
                }
            }
            if (done.Count + bad.Count > 0)
            {
                string sum = Fmt("Kemas kini selesai: {0} berjaya, {1} gagal.", new object[] { done.Count, bad.Count });
                if (bad.Count > 0) sum += "\n\n" + string.Join("\n", bad.ToArray());
                pendingAfter = () => Msg(sum, "Kemas kini program", MessageBoxButtons.OK, bad.Count > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
            }
            Ui(() => { for (int k = prAll.Count - 1; k >= 0; k--) if (done.Contains(((string)prAll[k].Tag).Substring(2))) prAll.RemoveAt(k); PrFilter(); });
        }, true);
    }
    // nyahpasang: buka pemasang nyahpasang program itu sendiri, satu demi satu
    void ActProgUn()
    {
        List<ListViewItem> sel = new List<ListViewItem>();
        foreach (ListViewItem i in listPr.CheckedItems) if (((string)i.Tag).StartsWith("I|")) sel.Add(i);
        if (sel.Count == 0) { Info("Tanda program dalam senarai program dipasang (klik Senarai program dahulu)."); return; }
        List<string> names = new List<string>(); foreach (ListViewItem i in sel) names.Add(i.Text);
        if (!Ask(T("Nyahpasang program ini? Pemasang nyahpasang program itu akan dibuka; ikut arahannya.") + "\n\n" + string.Join("\n", names.ToArray()), MessageBoxIcon.Warning)) return;
        List<string[]> todo = new List<string[]>(); foreach (ListViewItem i in sel) todo.Add(new string[] { ((string)i.Tag).Substring(2), i.Text });
        Work("menyahpasang program", () =>
        {
            foreach (string[] t in todo)
            {
                if (cancelReq) { Log("Dibatalkan."); break; }
                string us = Regex.Replace(t[0], @"(?i)(msiexec(\.exe)?\s+)/I", "$1/X");   // MSI: /I (pasang) -> /X (nyahpasang)
                Status("menunggu nyahpasang {0} ...", t[1]);
                try
                {
                    ProcessStartInfo psi = new ProcessStartInfo("cmd.exe", "/c \"" + us + "\""); psi.UseShellExecute = false; psi.CreateNoWindow = true;
                    using (Process p = Process.Start(psi))
                        while (!p.WaitForExit(500)) if (cancelReq) { Log("Dibatalkan. Pemasang yang sudah dibuka perlu ditutup sendiri."); return; }
                    Log("Pemasang nyahpasang ditutup: {0}", t[1]);
                }
                catch (Exception ex) { Log("GAGAL: {0} ({1})", t[1], T(ex.Message)); }
            }
            ProgLoad();
        }, true);
    }

}
