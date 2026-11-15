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
    // ---------- logik: lajukan PC ----------
    string[] TempDirs()
    {
        string a = Path.GetTempPath().TrimEnd('\\'), b = Environment.GetEnvironmentVariable("windir") + @"\Temp";
        return string.Compare(a, b, true) == 0 ? new string[] { a } : new string[] { a, b };
    }
    void CountDir(DirectoryInfo d, ref long n, ref long sum)
    {
        FileInfo[] fs; try { fs = d.GetFiles(); } catch { return; }
        foreach (FileInfo f in fs)
        {
            n++; try { sum += f.Length; } catch { }
            if (n % 200 == 0) Status("mengira {0} fail sementara ...", n);
        }
        DirectoryInfo[] ds; try { ds = d.GetDirectories(); } catch { return; }
        foreach (DirectoryInfo s in ds) if ((s.Attributes & FileAttributes.ReparsePoint) == 0) CountDir(s, ref n, ref sum);
    }
    // cache pelayar (Chrome, Edge, Brave, Vivaldi, Firefox) dan muat turun Windows Update; hanya cache, bukan kata laluan atau sejarah
    string[] CacheDirs()
    {
        List<string> r = new List<string>();
        string la = Environment.GetEnvironmentVariable("LOCALAPPDATA");
        try
        {
            if (!string.IsNullOrEmpty(la))
            {
                foreach (string v in new string[] { @"Google\Chrome", @"Microsoft\Edge", @"BraveSoftware\Brave-Browser", "Vivaldi" })
                {
                    string ud = Path.Combine(la, v + @"\User Data");
                    if (!Directory.Exists(ud)) continue;
                    foreach (string prof in Directory.GetDirectories(ud))
                    {
                        string n = Path.GetFileName(prof);
                        if (n != "Default" && !n.StartsWith("Profile ")) continue;
                        foreach (string c in new string[] { "Cache", "Code Cache", "GPUCache" }) { string p = Path.Combine(prof, c); if (Directory.Exists(p)) r.Add(p); }
                    }
                }
                string ff = Path.Combine(la, @"Mozilla\Firefox\Profiles");
                if (Directory.Exists(ff)) foreach (string prof in Directory.GetDirectories(ff)) { string p = Path.Combine(prof, "cache2"); if (Directory.Exists(p)) r.Add(p); }
            }
        }
        catch { }
        string sd = Environment.GetEnvironmentVariable("windir") + @"\SoftwareDistribution\Download";
        if (Directory.Exists(sd)) r.Add(sd);
        return r.ToArray();
    }
    long[] TempStat(string[] ds)
    {
        long n = 0, sum = 0;
        foreach (string d in ds) if (Directory.Exists(d)) CountDir(new DirectoryInfo(d), ref n, ref sum);
        return new long[] { n, sum };
    }
    static void DeleteTree(FileSystemInfo i)
    {
        DirectoryInfo d = i as DirectoryInfo;
        if (d != null)
        {
            if ((d.Attributes & FileAttributes.ReparsePoint) == 0)
            {
                FileSystemInfo[] kids; try { kids = d.GetFileSystemInfos(); } catch { return; }
                foreach (FileSystemInfo k in kids) DeleteTree(k);
            }
            try { d.Delete(); } catch { }
        }
        else try { File.SetAttributes(i.FullName, FileAttributes.Normal); i.Delete(); } catch { }
    }
    void TempClean(string[] ds, bool cache)
    {
        Log(cache ? "Mengira fail cache (boleh ambil masa jika banyak) ..." : "Mengira fail sementara (boleh ambil masa jika banyak) ...");
        long[] m = TempStat(ds);
        if (m[0] == 0) { Log(cache ? "Tiada fail cache." : "Tiada fail sementara."); return; }
        string q = cache
            ? string.Format(T("Padam {0} fail cache (~{1} MB)?"), m[0], Math.Round(m[1] / 1048576.0, 1)) + "\n" + T("Tutup pelayar dahulu. Hanya cache dipadam, bukan kata laluan atau sejarah. Fail yang sedang digunakan akan dilangkau. Folder Windows perlu Administrator.")
            : string.Format(T("Padam {0} fail sementara (~{1} MB)?"), m[0], Math.Round(m[1] / 1048576.0, 1)) + "\n" + T("Fail yang sedang digunakan akan dilangkau. Folder Windows\\Temp perlu Administrator.");
        if (!Ask(q, MessageBoxIcon.Question)) return;
        List<FileSystemInfo> items = new List<FileSystemInfo>();
        foreach (string d in ds)
        {
            if (!Directory.Exists(d)) continue;
            try { items.AddRange(new DirectoryInfo(d).GetFileSystemInfos()); } catch { }
        }
        for (int i = 0; i < items.Count; i++) { Status("memadam {0} / {1} ...", i + 1, items.Count); DeleteTree(items[i]); }
        Status("mengira baki ...");
        long[] left = TempStat(ds);
        Log("Siap: ~{0} MB dibebaskan ({1} fail sedang digunakan dilangkau).", Math.Round((m[1] - left[1]) / 1048576.0, 1), left[0]);
    }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    static extern int SHEmptyRecycleBin(IntPtr hwnd, string root, uint flags);
    void RecycleClean()
    {
        if (!Ask("Kosongkan Recycle Bin secara kekal? Fail di dalamnya tak boleh dipulihkan.", MessageBoxIcon.Warning)) return;
        SHEmptyRecycleBin(IntPtr.Zero, null, 7);   // 7 = tanpa pengesahan, kemajuan dan bunyi
        Log("Recycle Bin dikosongkan.");
    }
    void DiskInfo()
    {
        foreach (DriveInfo d in DriveInfo.GetDrives())
            if (d.DriveType == DriveType.Fixed && d.IsReady)
                Log("{0} bebas {1} GB daripada {2} GB", d.Name, Math.Round(d.AvailableFreeSpace / 1073741824.0, 1), Math.Round(d.TotalSize / 1073741824.0, 1));
    }

    // ---------- logik: pembersih PC berkategori (Analisis dahulu, kemudian Bersihkan) ----------
    class CCat { public string Id, Group, Name, Note; public string[] Dirs = new string[0], Files = new string[0]; public bool Admin; }   // Files: "folder|corak"
    List<CCat> cats = new List<CCat>(); Dictionary<string, long[]> cStat = new Dictionary<string, long[]>();   // id -> {bilangan fail, saiz}
    ListView listC; Label detC, lblCTot; RButton bCAn, bCRun;
    [StructLayout(LayoutKind.Sequential)] struct RbInfo64 { public int cbSize; public long size; public long items; }
    [StructLayout(LayoutKind.Sequential, Pack = 1)] struct RbInfo32 { public int cbSize; public long size; public long items; }
    [DllImport("shell32.dll", EntryPoint = "SHQueryRecycleBin", CharSet = CharSet.Unicode)] static extern int RbQ64(string root, ref RbInfo64 i);
    [DllImport("shell32.dll", EntryPoint = "SHQueryRecycleBin", CharSet = CharSet.Unicode)] static extern int RbQ32(string root, ref RbInfo32 i);
    static long[] RecycleStat()
    {
        try
        {
            if (IntPtr.Size == 8) { RbInfo64 i = new RbInfo64(); i.cbSize = Marshal.SizeOf(i); if (RbQ64(null, ref i) == 0) return new long[] { i.items, i.size }; }
            else { RbInfo32 i = new RbInfo32(); i.cbSize = Marshal.SizeOf(i); if (RbQ32(null, ref i) == 0) return new long[] { i.items, i.size }; }
        }
        catch { }
        return new long[] { 0, 0 };
    }
    static string[] BrowserDirs(string vendor)
    {
        List<string> r = new List<string>(); string la = Environment.GetEnvironmentVariable("LOCALAPPDATA");
        try
        {
            if (string.IsNullOrEmpty(la)) return r.ToArray();
            if (vendor == "Firefox")
            {
                string ff = Path.Combine(la, @"Mozilla\Firefox\Profiles");
                if (Directory.Exists(ff)) foreach (string prof in Directory.GetDirectories(ff)) { string p = Path.Combine(prof, "cache2"); if (Directory.Exists(p)) r.Add(p); }
                return r.ToArray();
            }
            string ud = Path.Combine(la, vendor + @"\User Data");
            if (!Directory.Exists(ud)) return r.ToArray();
            foreach (string prof in Directory.GetDirectories(ud))
            {
                string n = Path.GetFileName(prof);
                if (n != "Default" && !n.StartsWith("Profile ")) continue;
                foreach (string c in new string[] { "Cache", "Code Cache", "GPUCache" }) { string p = Path.Combine(prof, c); if (Directory.Exists(p)) r.Add(p); }
            }
        }
        catch { }
        return r.ToArray();
    }
    CCat Cat(string id, string group, string name, string note, bool admin)
    {
        CCat c = new CCat(); c.Id = id; c.Group = group; c.Name = name; c.Note = note; c.Admin = admin; cats.Add(c); return c;
    }
    void BuildCats()
    {
        string la = Environment.GetEnvironmentVariable("LOCALAPPDATA") ?? "", wd = Environment.GetEnvironmentVariable("windir") ?? "", pd = Environment.GetEnvironmentVariable("ProgramData") ?? "";
        CCat c;
        c = Cat("temp", "Windows", "Fail sementara pengguna", "Sampah yang ditinggalkan program", false); c.Dirs = new string[] { Path.GetTempPath().TrimEnd('\\') };
        c = Cat("thumb", "Windows", "Cache lakaran gambar", "Windows bina semula bila perlu", false);
        c.Files = new string[] { la + @"\Microsoft\Windows\Explorer|thumbcache_*.db", la + @"\Microsoft\Windows\Explorer|iconcache_*.db" };
        c = Cat("wer", "Windows", "Laporan ralat Windows", "Laporan program yang ranap", false); c.Dirs = new string[] { la + @"\Microsoft\Windows\WER", pd + @"\Microsoft\Windows\WER" };
        c = Cat("recycle", "Windows", "Recycle Bin", "Dikosongkan kekal, tak boleh dipulihkan", false);
        c = Cat("dns", "Windows", "Cache DNS", "Menyegarkan senarai alamat laman web", false);
        c = Cat("wtemp", "Windows", "Fail sementara Windows", "Perlu Administrator", true); c.Dirs = new string[] { wd + @"\Temp" };
        c = Cat("wu", "Windows", "Muat turun Windows Update", "Perlu Administrator", true); c.Dirs = new string[] { wd + @"\SoftwareDistribution\Download" };
        c = Cat("dump", "Windows", "Fail dump ranap", "Perlu Administrator", true); c.Dirs = new string[] { wd + @"\Minidump" }; c.Files = new string[] { wd + "|MEMORY.DMP" };
        foreach (string[] b in new string[][] { new string[] { "chrome", "Google Chrome", @"Google\Chrome" }, new string[] { "edge", "Microsoft Edge", @"Microsoft\Edge" },
            new string[] { "firefox", "Mozilla Firefox", "Firefox" }, new string[] { "brave", "Brave", @"BraveSoftware\Brave-Browser" }, new string[] { "vivaldi", "Vivaldi", "Vivaldi" } })
        {
            string[] ds = BrowserDirs(b[2]); if (ds.Length == 0) continue;   // pelayar tak dipasang: tak dipaparkan
            c = Cat(b[0], "Pelayar", b[1] + " (cache)", "Tutup pelayar dahulu (kata laluan tak disentuh)", false); c.Dirs = ds;
        }
    }
    long[] CatStat(CCat c)
    {
        if (c.Id == "recycle") return RecycleStat();
        if (c.Id == "dns") return new long[] { 0, 0 };
        long n = 0, sum = 0;
        foreach (string d in c.Dirs) if (Directory.Exists(d)) CountDir(new DirectoryInfo(d), ref n, ref sum);
        foreach (string f in c.Files)
        {
            int p = f.IndexOf('|'); string dir = f.Substring(0, p), pat = f.Substring(p + 1);
            try { if (Directory.Exists(dir)) foreach (string x in Directory.GetFiles(dir, pat)) { n++; try { sum += new FileInfo(x).Length; } catch { } } } catch { }
        }
        return new long[] { n, sum };
    }
    void CatClean(CCat c)
    {
        if (c.Id == "recycle") { SHEmptyRecycleBin(IntPtr.Zero, null, 7); return; }
        if (c.Id == "dns")
        {
            try { ProcessStartInfo psi = new ProcessStartInfo("ipconfig", "/flushdns"); psi.CreateNoWindow = true; psi.UseShellExecute = false; using (Process p = Process.Start(psi)) p.WaitForExit(15000); } catch { }
            return;
        }
        foreach (string d in c.Dirs)
        {
            if (!Directory.Exists(d)) continue;
            FileSystemInfo[] kids; try { kids = new DirectoryInfo(d).GetFileSystemInfos(); } catch { continue; }
            foreach (FileSystemInfo k in kids) DeleteTree(k);
        }
        foreach (string f in c.Files)
        {
            int p = f.IndexOf('|'); string dir = f.Substring(0, p), pat = f.Substring(p + 1);
            try { if (Directory.Exists(dir)) foreach (string x in Directory.GetFiles(dir, pat)) try { File.SetAttributes(x, FileAttributes.Normal); File.Delete(x); } catch { } } catch { }
        }
    }
    // isi senarai (benang UI); tanda dan saiz sedia ada dikekalkan
    void CRefreshRows()
    {
        Dictionary<string, bool> chk = new Dictionary<string, bool>(); foreach (ListViewItem i in listC.Items) chk[(string)i.Tag] = i.Checked;
        Dictionary<string, ListViewGroup> gs = new Dictionary<string, ListViewGroup>();
        listC.BeginUpdate(); listC.Items.Clear(); listC.Groups.Clear();
        foreach (CCat c in cats)
        {
            string g = T(c.Group); ListViewGroup grp; if (!gs.TryGetValue(g, out grp)) { grp = new ListViewGroup(g); listC.Groups.Add(grp); gs[g] = grp; }
            long[] s; string size = !cStat.TryGetValue(c.Id, out s) || c.Id == "dns" ? "-" : s[1] == 0 ? T("bersih") : Sz(s[1]);
            ListViewItem it = MakeItem(c.Id, T(c.Name) + " - " + T(c.Note), T(c.Name), size, T(c.Note)); it.Group = grp;
            it.Checked = chk.ContainsKey(c.Id) ? chk[c.Id] : (!c.Admin || IsAdmin) && c.Id != "recycle";
            listC.Items.Add(it);
        }
        listC.EndUpdate(); CTotal(); SetEmpty(listC, "");
    }
    void CTotal()
    {
        if (lblCTot == null || listC == null) return;
        long sum = 0; bool any = false;
        foreach (ListViewItem i in listC.CheckedItems) { long[] s; if (cStat.TryGetValue((string)i.Tag, out s)) { sum += s[1]; any = true; } }
        string t = any ? T("Boleh dibebaskan") + ": " + Sz(sum) : ""; lblCTot.Tag = t; lblCTot.Text = t;
    }
    void ActCAnalyze() { Work("menganalisis PC", () => CAnalyze(), true); }
    void CAnalyze()
    {
        Dictionary<string, long[]> res = new Dictionary<string, long[]>(); long total = 0;
        foreach (CCat c in cats)
        {
            if (cancelReq) { Log("Dibatalkan."); break; }
            Status("menganalisis {0} ...", T(c.Name));
            long[] s = CatStat(c); res[c.Id] = s; total += s[1];
        }
        Ui(() => { foreach (KeyValuePair<string, long[]> kv in res) cStat[kv.Key] = kv.Value; CRefreshRows(); });
        Log("Analisis siap: {0} boleh dibersihkan.", Sz(total));
    }
    void ActCClean()
    {
        List<CCat> sel = new List<CCat>(); List<string> names = new List<string>(); long est = 0; bool known = false, rb = false;
        foreach (ListViewItem i in listC.CheckedItems)
            foreach (CCat c in cats)
                if (c.Id == (string)i.Tag)
                {
                    sel.Add(c); names.Add("- " + T(c.Name)); if (c.Id == "recycle") rb = true;
                    long[] s; if (cStat.TryGetValue(c.Id, out s)) { est += s[1]; known = true; }
                }
        if (sel.Count == 0) { Info("Tanda kategori yang mahu dibersihkan."); return; }
        string q = T("Bersihkan kategori ini?") + "\n\n" + string.Join("\n", names.ToArray()) + (known ? "\n\n" + T("Anggaran") + ": " + Sz(est) : "")
            + "\n\n" + T("Fail yang sedang digunakan akan dilangkau. Tutup pelayar dahulu.") + (rb ? "\n" + T("Recycle Bin dikosongkan secara kekal.") : "");
        if (!Ask(q, MessageBoxIcon.Question)) return;
        Work("membersihkan PC", () =>
        {
            Dictionary<string, long[]> res = new Dictionary<string, long[]>(); long freed = 0;
            foreach (CCat c in sel)
            {
                if (cancelReq) { Log("Dibatalkan."); break; }
                Status("membersihkan {0} ...", T(c.Name));
                long[] b = CatStat(c); CatClean(c); long[] a = CatStat(c); res[c.Id] = a;
                long f = Math.Max(0, b[1] - a[1]); freed += f; Log("{0}: {1} dibebaskan.", T(c.Name), Sz(f));
            }
            Ui(() => { foreach (KeyValuePair<string, long[]> kv in res) cStat[kv.Key] = kv.Value; CRefreshRows(); });
            Log("Siap: {0} dibebaskan.", Sz(freed));
            pendingAfter = () => Msg("Siap. Ruang dibebaskan: {0}", "Bersihkan PC", MessageBoxButtons.OK, MessageBoxIcon.Information, Sz(freed));
        }, true);
    }

    // ---------- logik: ruang cakera (pencari fail/folder terbesar, hanya membaca) ----------
    class DiskRow { public string Full, Name; public long Size; }
    volatile bool cancelReq; bool cancellable;
    string scanRoot = Environment.GetEnvironmentVariable("USERPROFILE");
    Label scanPath, detD; ListView listD;
    RButton bPick, bFold, bFile, bOpenD, bCancel;
    static string Sz(long b)
    {
        string[] u = { "B", "KB", "MB", "GB", "TB" }; double v = b; int i = 0;
        while (v >= 1024 && i < 4) { v /= 1024; i++; }
        return (i == 0 ? v.ToString("0") : v.ToString("0.#")) + " " + u[i];
    }
    long DirSize(DirectoryInfo d, ref long counter)
    {
        long sum = 0;
        FileInfo[] fs; try { fs = d.GetFiles(); } catch { return 0; }
        foreach (FileInfo f in fs)
        {
            if (cancelReq) return sum;
            try { sum += f.Length; } catch { }
            if (++counter % 500 == 0) Status("mengimbas {0} fail ...", counter);
        }
        DirectoryInfo[] ds; try { ds = d.GetDirectories(); } catch { return sum; }
        foreach (DirectoryInfo s in ds)
        {
            if (cancelReq) return sum;
            if ((s.Attributes & FileAttributes.ReparsePoint) == 0) sum += DirSize(s, ref counter);
        }
        return sum;
    }
    void FindFolders(string root)
    {
        List<DiskRow> rows = new List<DiskRow>(); long counter = 0, loose = 0;
        DirectoryInfo di = new DirectoryInfo(root);
        FileInfo[] fs; try { fs = di.GetFiles(); } catch { fs = new FileInfo[0]; }
        foreach (FileInfo f in fs) { try { loose += f.Length; } catch { } }
        DirectoryInfo[] ds; try { ds = di.GetDirectories(); } catch { ds = new DirectoryInfo[0]; }
        foreach (DirectoryInfo s in ds)
        {
            if (cancelReq) break;
            if ((s.Attributes & FileAttributes.ReparsePoint) != 0) continue;
            Status("mengimbas {0} ...", s.Name);
            DiskRow r = new DiskRow(); r.Full = s.FullName; r.Name = s.Name; r.Size = DirSize(s, ref counter); rows.Add(r);
        }
        if (cancelReq) { Log("Imbasan dibatalkan."); return; }
        if (loose > 0) { DiskRow r = new DiskRow(); r.Full = root; r.Name = T("(fail dalam folder ini)"); r.Size = loose; rows.Add(r); }
        rows.Sort(delegate (DiskRow a, DiskRow b) { return b.Size.CompareTo(a.Size); });
        if (rows.Count > 30) rows.RemoveRange(30, rows.Count - 30);
        ShowDisk(rows);
        Log("Folder terbesar: {0} item.", rows.Count);
    }
    void WalkFiles(DirectoryInfo d, List<DiskRow> top, ref long counter)
    {
        FileInfo[] fs; try { fs = d.GetFiles(); } catch { return; }
        foreach (FileInfo f in fs)
        {
            if (cancelReq) return;
            long len; try { len = f.Length; } catch { continue; }
            if (++counter % 500 == 0) Status("mengimbas {0} fail ...", counter);
            if (top.Count >= 30 && len <= top[top.Count - 1].Size) continue;
            DiskRow r = new DiskRow(); r.Full = f.FullName; r.Name = f.Name; r.Size = len;
            int at = top.FindIndex(delegate (DiskRow x) { return x.Size < len; });
            if (at < 0) top.Add(r); else top.Insert(at, r);
            if (top.Count > 30) top.RemoveAt(30);
        }
        DirectoryInfo[] ds; try { ds = d.GetDirectories(); } catch { return; }
        foreach (DirectoryInfo s in ds)
        {
            if (cancelReq) return;
            if ((s.Attributes & FileAttributes.ReparsePoint) == 0) WalkFiles(s, top, ref counter);
        }
    }
    void FindFiles(string root)
    {
        List<DiskRow> top = new List<DiskRow>(); long counter = 0;
        WalkFiles(new DirectoryInfo(root), top, ref counter);
        if (cancelReq) { Log("Imbasan dibatalkan."); return; }
        ShowDisk(top);
        Log("Fail terbesar: {0} item.", top.Count);
    }
    void ShowDisk(List<DiskRow> rows)
    {
        Ui(() =>
        {
            dupMode = false; listD.CheckBoxes = false; listD.Groups.Clear(); bDupDel.Visible = false;
            listD.Items.Clear();
            foreach (DiskRow w in rows)
            {
                string loc = w.Name.StartsWith("(") ? w.Full : (Path.GetDirectoryName(w.Full) ?? w.Full);
                AddRow(listD, w.Full, w.Full + "\n" + Sz(w.Size), w.Name, Sz(w.Size), loc);
            }
            detD.Tag = Hint; detD.Text = T(Hint); SetEmpty(listD, rows.Count == 0 ? "Tiada hasil." : "");
        });
    }
    void ActPickFolder()
    {
        using (FolderBrowserDialog d = new FolderBrowserDialog())
        {
            d.Description = T("Pilih folder untuk diimbas"); d.SelectedPath = scanRoot;
            if (d.ShowDialog(this) == DialogResult.OK) { scanRoot = d.SelectedPath; scanPath.Text = scanRoot; scanPath.Tag = scanRoot; }
        }
    }
    void ActFolders() { string r = scanRoot; Work("mengimbas folder terbesar", () => FindFolders(r), true); }
    void ActFiles() { string r = scanRoot; Work("mengimbas fail terbesar", () => FindFiles(r), true); }
    void ActOpenD()
    {
        if (listD.SelectedItems.Count == 0) { Info("Pilih satu baris dahulu."); return; }
        string p = (string)listD.SelectedItems[0].Tag;
        if (Directory.Exists(p)) Process.Start("explorer.exe", "\"" + p + "\"");
        else if (File.Exists(p)) Process.Start("explorer.exe", "/select,\"" + p + "\"");
        else Log("Fail tidak ditemui: {0}", p);
    }
    // ---------- fail pendua: kumpulkan ikut saiz, kemudian hash 64 KB pertama, kemudian hash penuh (hanya yang sama sahaja dibaca penuh) ----------
    bool dupMode; RButton bDup, bDupDel;
    const long DupMin = 1048576;   // fail kurang 1 MB diabaikan: jarang berbaloi dan terlalu banyak
    static string HashFile(string path, long limit)
    {
        try
        {
            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 65536))
            using (System.Security.Cryptography.SHA1 h = System.Security.Cryptography.SHA1.Create())
            {
                byte[] buf = new byte[65536]; long left = limit < 0 ? long.MaxValue : limit; int n;
                while (left > 0 && (n = fs.Read(buf, 0, (int)Math.Min(buf.Length, left))) > 0) { h.TransformBlock(buf, 0, n, null, 0); left -= n; }
                h.TransformFinalBlock(buf, 0, 0);
                return BitConverter.ToString(h.Hash);
            }
        }
        catch { return null; }
    }
    void DupCollect(DirectoryInfo d, Dictionary<long, List<string>> bySize, ref long counter)
    {
        FileInfo[] fs; try { fs = d.GetFiles(); } catch { return; }
        foreach (FileInfo f in fs)
        {
            if (cancelReq) return;
            try
            {
                if ((f.Attributes & (FileAttributes.System | FileAttributes.ReparsePoint)) != 0 || f.Length < DupMin) continue;
                List<string> l; if (!bySize.TryGetValue(f.Length, out l)) { l = new List<string>(); bySize[f.Length] = l; }
                l.Add(f.FullName);
            }
            catch { }
            if (++counter % 500 == 0) Status("mengimbas {0} fail ...", counter);
        }
        DirectoryInfo[] ds; try { ds = d.GetDirectories(); } catch { return; }
        foreach (DirectoryInfo s in ds)
        {
            if (cancelReq) return;
            string n = s.Name.ToLower();
            if ((s.Attributes & FileAttributes.ReparsePoint) == 0 && n != "$recycle.bin" && n != "system volume information" && n != "windows") DupCollect(s, bySize, ref counter);
        }
    }
    void FindDups(string root)
    {
        Dictionary<long, List<string>> bySize = new Dictionary<long, List<string>>(); long counter = 0;
        DupCollect(new DirectoryInfo(root), bySize, ref counter);
        List<List<string>> groups = new List<List<string>>();
        foreach (KeyValuePair<long, List<string>> kv in bySize)
        {
            if (cancelReq) { Log("Imbasan dibatalkan."); return; }
            if (kv.Value.Count < 2) continue;
            Dictionary<string, List<string>> part = new Dictionary<string, List<string>>();
            foreach (string f in kv.Value)
            {
                if (cancelReq) { Log("Imbasan dibatalkan."); return; }
                Status("membandingkan {0} ...", Path.GetFileName(f));
                string h = HashFile(f, 65536); if (h == null) continue;
                List<string> l; if (!part.TryGetValue(h, out l)) { l = new List<string>(); part[h] = l; }
                l.Add(f);
            }
            foreach (List<string> g in part.Values)
            {
                if (g.Count < 2) continue;
                Dictionary<string, List<string>> full = new Dictionary<string, List<string>>();
                foreach (string f in g)
                {
                    if (cancelReq) { Log("Imbasan dibatalkan."); return; }
                    Status("memeriksa {0} ...", Path.GetFileName(f));
                    string h = HashFile(f, -1); if (h == null) continue;
                    List<string> l; if (!full.TryGetValue(h, out l)) { l = new List<string>(); full[h] = l; }
                    l.Add(f);
                }
                foreach (List<string> g2 in full.Values) if (g2.Count >= 2) groups.Add(g2);
            }
        }
        // paling banyak ruang terbazir dahulu
        long totalWaste = 0; foreach (List<string> g in groups) totalWaste += new FileInfo(g[0]).Length * (g.Count - 1);
        groups.Sort((x, y) => (new FileInfo(y[0]).Length * (y.Count - 1)).CompareTo(new FileInfo(x[0]).Length * (x.Count - 1)));
        if (groups.Count > 300) groups.RemoveRange(300, groups.Count - 300);   // senarai terlalu panjang tak berguna
        Ui(() =>
        {
            dupMode = true; listD.CheckBoxes = true; bDupDel.Visible = true; bDupDel.Enabled = false;
            listD.BeginUpdate(); listD.Items.Clear(); listD.Groups.Clear();
            foreach (List<string> g in groups)
            {
                g.Sort((x, y) => { int c = File.GetLastWriteTime(x).CompareTo(File.GetLastWriteTime(y)); return c != 0 ? c : x.Length.CompareTo(y.Length); });   // paling lama dahulu = yang dikekalkan
                long sz = new FileInfo(g[0]).Length;
                ListViewGroup grp = new ListViewGroup(string.Format(T("{0} ({1}) - {2} salinan"), Path.GetFileName(g[0]), Sz(sz), g.Count)); listD.Groups.Add(grp);
                for (int k = 0; k < g.Count; k++)
                {
                    ListViewItem it = MakeItem(g[k], g[k] + "\n" + Sz(sz), Path.GetFileName(g[k]), Sz(sz), Path.GetDirectoryName(g[k]));
                    it.Group = grp; it.Checked = k > 0; listD.Items.Add(it);
                }
            }
            listD.EndUpdate();
            string sum = groups.Count == 0 ? "" : string.Format(T("{0} kumpulan fail pendua; ruang terbazir kira-kira {1}. Salinan paling lama dikekalkan (tidak ditanda)."), groups.Count, Sz(totalWaste));
            if (sum.Length > 0) { detD.Tag = sum; detD.Text = sum; } else { detD.Tag = Hint; detD.Text = T(Hint); }
            SetEmpty(listD, "Tiada fail pendua ditemui (fail 1 MB ke atas).");
        });
        Log("Fail pendua: {0} kumpulan, ruang terbazir ~{1}.", groups.Count, Sz(totalWaste));
    }
    void ActDups() { string r = scanRoot; Work("mencari fail pendua", () => FindDups(r), true); }
    void ActDupDel()
    {
        List<ListViewItem> sel = new List<ListViewItem>(); Dictionary<ListViewGroup, int> cnt = new Dictionary<ListViewGroup, int>(); long total = 0;
        foreach (ListViewItem i in listD.CheckedItems) { sel.Add(i); int c; cnt.TryGetValue(i.Group, out c); cnt[i.Group] = c + 1; try { total += new FileInfo((string)i.Tag).Length; } catch { } }
        if (sel.Count == 0) return;
        foreach (KeyValuePair<ListViewGroup, int> kv in cnt) if (kv.Value >= kv.Key.Items.Count) { Info("Dalam satu kumpulan semua salinan ditanda. Biarkan sekurang-kurangnya satu salinan tidak ditanda."); return; }
        if (!Ask(string.Format(T("Pindahkan {0} fail pendua (~{1}) ke Recycle Bin?"), sel.Count, Sz(total)) + "\n\n" + T("Satu salinan setiap kumpulan dikekalkan. Fail yang terlalu besar untuk Recycle Bin mungkin dipadam terus."), MessageBoxIcon.Warning)) return;
        List<string> paths = new List<string>(); foreach (ListViewItem i in sel) paths.Add((string)i.Tag);
        Work("memadam fail pendua", () =>
        {
            int ok = 0; long freed = 0; List<string> gone = new List<string>();
            foreach (string p in paths)
            {
                if (cancelReq) { Log("Dibatalkan."); break; }
                Status("memadam {0} ...", Path.GetFileName(p));
                try
                {
                    long len = new FileInfo(p).Length;
                    Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(p, Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs, Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
                    ok++; freed += len; gone.Add(p);
                }
                catch (Exception ex) { Log("GAGAL: {0} ({1})", p, T(ex.Message)); }
            }
            Ui(() => { for (int k = listD.Items.Count - 1; k >= 0; k--) if (gone.Contains((string)listD.Items[k].Tag)) listD.Items.RemoveAt(k); SetEmpty(listD, "Tiada fail pendua ditemui (fail 1 MB ke atas)."); });
            Log("Siap: {0} fail dipindahkan ke Recycle Bin (~{1}).", ok, Sz(freed));
        }, true);
    }
    // ---------- padam ruang kosong: tulis data ke seluruh ruang kosong dalam folder sementara, kemudian buang (fail sedia ada tak disentuh) ----------
    RadioButton rbW1, rbW2; RButton bWipe; long wipeCap;   // wipeCap > 0 hanya untuk ujian: had bait yang ditulis
    const string WipeTag = "USBShield-wipe-";
    // folder sementara yang tertinggal kalau app ditutup paksa semasa proses: buang supaya ruang dikembalikan
    void WipeLeftovers()
    {
        if (busy) return;
        foreach (DriveInfo d in DriveInfo.GetDrives())
        {
            try
            {
                if (!d.IsReady) continue;
                foreach (string p in Directory.GetDirectories(d.Name, WipeTag + "*")) { Directory.Delete(p, true); Log("Folder sementara padam-ruang yang tertinggal dibuang: {0}", p); }
            }
            catch { }
        }
    }
    // pulang true jika siap (bukan dibatalkan)
    bool WipeFree(string root, int passes)
    {
        string dir = Path.Combine(root, WipeTag + Guid.NewGuid().ToString("N").Substring(0, 8));
        long target = new DriveInfo(root).AvailableFreeSpace - 64L * 1048576;   // 64 MB dibiarkan kosong supaya sistem tak lumpuh
        if (wipeCap > 0) target = Math.Min(target, wipeCap);
        if (target < 16L * 1048576 && wipeCap == 0) { Log("Ruang kosong terlalu sedikit untuk dipadam."); return false; }
        Directory.CreateDirectory(dir);
        byte[] buf = new byte[4 * 1048576]; Random rnd = new Random();
        long done = 0, grand = target * passes; DateTime t0 = DateTime.Now, lastUi = DateTime.MinValue;
        try
        {
            for (int pass = 1; pass <= passes && !cancelReq; pass++)
            {
                int mode = passes == 1 ? 2 : pass - 1;   // 0 = sifar, 1 = satu (0xFF), 2 = rawak
                if (mode == 0) Array.Clear(buf, 0, buf.Length); else if (mode == 1) for (int k = 0; k < buf.Length; k++) buf[k] = 0xFF;
                long written = 0; int fn = 0;
                while (written < target && !cancelReq)
                {
                    try
                    {
                        using (FileStream fs = new FileStream(Path.Combine(dir, "w" + (fn++) + ".tmp"), FileMode.Create, FileAccess.Write, FileShare.None, 65536, FileOptions.WriteThrough))
                            while (fs.Length < (1L << 30) && written < target && !cancelReq)
                            {
                                if (mode == 2) rnd.NextBytes(buf);
                                int n = (int)Math.Min(buf.Length, target - written);
                                fs.Write(buf, 0, n); written += n; done += n;
                                if ((DateTime.Now - lastUi).TotalMilliseconds > 300)
                                {
                                    lastUi = DateTime.Now; double sec = Math.Max(1, (DateTime.Now - t0).TotalSeconds), rate = done / sec;
                                    TimeSpan left = TimeSpan.FromSeconds((grand - done) / Math.Max(1, rate));
                                    int pct = (int)(100.0 * done / grand);
                                    string eta = left.TotalHours >= 1 ? string.Format("{0}j {1}m", (int)left.TotalHours, left.Minutes) : string.Format("{0}m {1}s", left.Minutes, left.Seconds);
                                    Status("menimpa ruang kosong {0}% ({1} / {2}), {3}/s, baki ~{4}", pct, Sz(done), Sz(grand), Sz((long)rate), eta);
                                    Ui(() => bar.Value = pct);
                                }
                            }
                    }
                    catch (IOException) { break; }   // cakera penuh lebih awal daripada jangkaan: tamat pas ini
                }
                foreach (string f in Directory.GetFiles(dir)) try { File.Delete(f); } catch { }   // pulangkan ruang sebelum pas seterusnya
            }
        }
        finally { try { Directory.Delete(dir, true); } catch { } Ui(() => bar.Value = -1); }
        return !cancelReq;
    }
    void ActWipe()
    {
        string r = Root(); if (r == null) return;
        if (string.Compare(r, sysDrive, true) == 0) { Info("Drive sistem tak boleh dipilih untuk ciri ini."); return; }
        long free; try { free = new DriveInfo(r).AvailableFreeSpace; } catch { Info("Drive tak dapat dibaca."); return; }
        int passes = rbW2.Checked ? 3 : 1;
        string q = string.Format(T("Timpa ruang kosong di {0} ({1})?"), r, Sz(free)) + "\n\n"
            + T("Fail sedia ada tidak disentuh. Drive akan penuh sementara waktu dan ini boleh mengambil masa yang lama. Jangan cabut drive. Boleh dibatalkan; ruang akan dikembalikan.")
            + (passes == 3 ? "\n" + T("Mod teliti: 3 pas.") : "") + "\n\n" + T("Pada SSD, penimpaan tak menjamin semua data lama hilang.");
        if (!Ask(q, MessageBoxIcon.Warning)) return;
        Work("memadam ruang kosong", () =>
        {
            bool ok = WipeFree(r, passes);
            Log(ok ? "Siap: ruang kosong di {0} sudah ditimpa." : "Dihentikan. Ruang di {0} dikembalikan.", r);
            AddHistory(r, "Padam ruang kosong", ok ? "selesai" : "dihentikan");
        }, true);
    }
    // alat Windows sendiri (lebih teliti daripada yang boleh kita tulis)
    void OpenTool(string name, string exe, string args)
    {
        try { Process.Start(exe, args); } catch { Log("Gagal membuka {0}.", name); }
    }

    // mulakan semula dengan UAC; pengguna boleh menolak
    void ActElevate()
    {
        if (!Ask("Program akan dimulakan semula dengan hak Administrator. Teruskan?", MessageBoxIcon.Question)) return;
        try
        {
            SaveSettings();
            ProcessStartInfo psi = new ProcessStartInfo(Application.ExecutablePath); psi.UseShellExecute = true; psi.Verb = "runas";
            Process.Start(psi); Close();
        }
        catch (System.ComponentModel.Win32Exception) { Log("Dibatalkan."); }   // UAC ditolak
    }

    // ---------- logik: prestasi (program makan RAM, kesihatan cakera, pelan kuasa, kesan visual; semua boleh dipulihkan) ----------
    ListView listP; Label detP; RButton bPRef, bPKill, bDiskH, bPow, bPowOff, bFx, bFxOff;
    static readonly string[] Protect = { "system", "idle", "registry", "memory compression", "csrss", "wininit", "winlogon", "services", "lsass", "smss", "svchost", "dwm", "explorer", "fontdrvhost", "sihost", "ctfmon", "taskhostw", "searchindexer", "msmpeng", "audiodg", "spoolsv" };
    // senarai program ikut nama (digabung), RAM dan CPU (disampel 1 saat); proses sistem dan program ini sendiri tak disenaraikan
    void ProcLoad()
    {
        Status("membaca program yang berjalan ...");
        Process me = Process.GetCurrentProcess();
        Process[] ps = Process.GetProcesses();
        Dictionary<int, double> c0 = new Dictionary<int, double>();
        foreach (Process p in ps) { try { c0[p.Id] = p.TotalProcessorTime.TotalMilliseconds; } catch { } }
        DateTime t0 = DateTime.Now; Thread.Sleep(1000); double el = (DateTime.Now - t0).TotalMilliseconds;
        Dictionary<string, double[]> g = new Dictionary<string, double[]>(StringComparer.OrdinalIgnoreCase);
        foreach (Process p in ps)
        {
            try
            {
                if (p.Id == me.Id || p.SessionId != me.SessionId || Array.IndexOf(Protect, p.ProcessName.ToLower()) >= 0) continue;
                p.Refresh();
                double cpu = 0, a; if (c0.TryGetValue(p.Id, out a)) cpu = p.TotalProcessorTime.TotalMilliseconds - a;
                double[] v; if (!g.TryGetValue(p.ProcessName, out v)) { v = new double[3]; g[p.ProcessName] = v; }
                v[0] += p.WorkingSet64; v[1] += cpu; v[2]++;
            }
            catch { }   // proses sudah tamat / akses ditolak
        }
        List<KeyValuePair<string, double[]>> rows = new List<KeyValuePair<string, double[]>>(g);
        rows.Sort((x, y) => y.Value[0].CompareTo(x.Value[0]));
        if (rows.Count > 25) rows.RemoveRange(25, rows.Count - 25);
        Ui(() =>
        {
            listP.Items.Clear();
            foreach (KeyValuePair<string, double[]> r in rows)
            {
                string name = r.Key + (r.Value[2] > 1 ? " (" + r.Value[2] + ")" : "");
                double cpuPct = Math.Min(100, r.Value[1] / el / Environment.ProcessorCount * 100);
                AddRow(listP, r.Key, name + " : " + Sz((long)r.Value[0]) + ", CPU " + Math.Round(cpuPct) + "%", name, Sz((long)r.Value[0]), Math.Round(cpuPct) + "%");
            }
            detP.Text = T(Hint); SetEmpty(listP, rows.Count == 0 ? "Tiada program ditemui." : "");
        });
        Log("Senarai dimuat: {0} program (diisih ikut RAM).", rows.Count);
    }
    void ActProcs() { Work("membaca program yang berjalan", () => ProcLoad()); }
    void KillByName(string name)
    {
        if (Array.IndexOf(Protect, name.ToLower()) >= 0) throw new InvalidOperationException("Program sistem tak boleh ditutup.");
        Process me = Process.GetCurrentProcess();
        foreach (Process p in Process.GetProcessesByName(name))
        {
            if (p.Id == me.Id || p.SessionId != me.SessionId) continue;
            try { if (!p.CloseMainWindow() || !p.WaitForExit(3000)) p.Kill(); } catch { }
        }
    }
    void ActKillProcs() { DoChecked(listP, n => KillByName(n), "Tutup program ini? Data yang belum disimpan akan hilang."); }

    // kesihatan cakera: ruang kosong dan status yang dilapor cakera (SMART) melalui WMI
    void DiskHealth()
    {
        List<string> L = new List<string>();
        foreach (DriveInfo d in DriveInfo.GetDrives())
            if (d.DriveType == DriveType.Fixed && d.IsReady)
            {
                double pct = 100.0 * d.AvailableFreeSpace / d.TotalSize;
                L.Add(string.Format(T("{0} bebas {1}% ({2} daripada {3})"), d.Name, Math.Round(pct), Sz(d.AvailableFreeSpace), Sz(d.TotalSize)) + (pct < 10 ? "   <- " + T("hampir penuh, ini melambatkan PC") : ""));
            }
        try
        {
            using (ManagementObjectSearcher q = new ManagementObjectSearcher("SELECT Model, Status FROM Win32_DiskDrive"))
                foreach (ManagementObject m in q.Get())
                {
                    string st = "" + m["Status"];
                    L.Add(string.Format(T("Cakera {0}: status {1}"), m["Model"], st) + (st == "OK" ? "" : "   <- " + T("amaran, sandarkan data anda")));
                }
        }
        catch { L.Add(T("Status cakera tak dapat dibaca.")); }
        string txt = string.Join("\n", L.ToArray()) + "\n\n" + T("Status OK bermaksud cakera tak melapor masalah. Ini bukan jaminan; sandarkan fail penting.");
        foreach (string l in L) Log("{0}", l);
        pendingAfter = () => Msg(txt, "Kesihatan cakera", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
    void ActDiskHealth() { Work("menyemak kesihatan cakera", () => DiskHealth()); }

}
