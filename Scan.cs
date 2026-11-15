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
    // ---------- sejarah imbasan (fail teks: tarikh|drive|jenis|hasil) ----------
    // shortcut: hasil disimpan dalam bahasa semasa; jenis diterjemah semula bila dipapar
    ListView listH; Label detH;
    string HistPath { get { return Path.Combine(Path.GetDirectoryName(SettingsPath), "sejarah.txt"); } }
    void AddHistory(string drive, string kind, string result)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(HistPath));
            File.AppendAllText(HistPath, DateTime.Now.ToString("yyyy-MM-dd HH:mm") + "|" + drive + "|" + kind + "|" + (result ?? "").Replace("|", "/").Replace("\r", " ").Replace("\n", " ") + "\r\n", Utf8);
        }
        catch { }
    }
    void HLoad()
    {
        listH.Items.Clear();
        try
        {
            if (File.Exists(HistPath))
            {
                string[] ls = File.ReadAllLines(HistPath, Utf8);
                for (int i = ls.Length - 1; i >= 0; i--)   // terkini di atas
                {
                    string[] f = ls[i].Split(new char[] { '|' }, 4); if (f.Length < 4) continue;
                    AddRow(listH, "", f[0] + "   " + f[1] + "   " + T(f[2]) + "\n" + f[3], f[0], f[1], T(f[2]), f[3]);
                }
            }
        }
        catch { }
        detH.Text = T(Hint); SetEmpty(listH, "Belum ada sejarah. Rekod muncul selepas imbasan atau pembersihan.");
    }
    void HClear()
    {
        if (!Ask("Padam semua sejarah imbasan? Fail yang dikuarantin tidak terjejas.", MessageBoxIcon.Warning)) return;
        try { File.Delete(HistPath); } catch { }
        HLoad();
    }

    // ---------- logik: ClamAV (antivirus sumber terbuka, luar talian; folder ClamAV diletak di sebelah program) ----------
    int lastClamHits;
    RButton bClam;
    string ClamDir { get { return Path.Combine(exeDir, "ClamAV"); } }
    string ClamExe() { string p = Path.Combine(ClamDir, "clamscan.exe"); return File.Exists(p) ? p : null; }
    string ClamDb()
    {
        foreach (string n in new string[] { "database", "db" })
        {
            string d = Path.Combine(ClamDir, n);
            if (Directory.Exists(d) && (Directory.GetFiles(d, "*.cvd").Length + Directory.GetFiles(d, "*.cld").Length) > 0) return d;
        }
        return null;
    }
    // tarikh binaan pangkalan data daripada kepala fail .cvd/.cld ("ClamAV-VDB:07 Aug 2026 ...")
    static DateTime? DbDate(string f)
    {
        try
        {
            byte[] b = new byte[64];
            using (FileStream fs = File.OpenRead(f)) { int n = fs.Read(b, 0, 64); if (n < 30) return null; }
            string h = System.Text.Encoding.ASCII.GetString(b);
            if (!h.StartsWith("ClamAV-VDB:")) return null;
            DateTime d;
            return DateTime.TryParseExact(h.Substring(11, 11), "dd MMM yyyy", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out d) ? (DateTime?)d : null;
        }
        catch { return null; }
    }
    static string DbFile(string dir, string name)
    {
        foreach (string e in new string[] { ".cld", ".cvd" }) { string p = Path.Combine(dir, name + e); if (File.Exists(p)) return p; }
        return null;
    }
    Label clamDesc; RButton bClamDb;
    void RefreshClam()
    {
        if (clamDesc == null) return;
        string t, db = ClamDb();
        if (ClamExe() == null) t = Fmt("Letak folder ClamAV di sebelah program ini untuk imbas tanpa internet (Windows 7).", null);
        else if (db == null) t = Fmt("ClamAV ditemui, tetapi pangkalan data belum ada. Tekan Import pangkalan data.", null);
        else
        {
            DateTime? d = null;   // guna fail daily (paling kerap dikemas kini), jika tiada ambil yang lain
            foreach (string n in new string[] { "daily", "main", "bytecode" }) { string f = DbFile(db, n); if (f != null) { d = DbDate(f); if (d != null) break; } }
            t = d == null ? Fmt("Imbas tanpa internet. Tarikh pangkalan data tidak diketahui.", null)
                          : Fmt("Imbas tanpa internet. Pangkalan data: {0} ({1} hari lalu).", new object[] { d.Value.ToString("dd/MM/yyyy"), Math.Max(0, (int)(DateTime.Now - d.Value).TotalDays) });
        }
        clamDesc.Tag = t; clamDesc.Text = t;
    }
    // salin main/daily/bytecode (.cvd/.cld) dari folder yang dipilih ke ClamAV\database; hanya yang sah dan tidak lebih lama
    void ActClamDb()
    {
        if (ClamExe() == null) { Info("ClamAV belum ada. Letak folder ClamAV (mengandungi clamscan.exe dan folder database) di sebelah program ini, kemudian cuba lagi. Lihat fail BACA-SAYA dalam folder ClamAV."); return; }
        string src;
        using (FolderBrowserDialog d = new FolderBrowserDialog())
        {
            d.Description = T("Pilih folder yang mengandungi main.cvd, daily.cvd dan bytecode.cvd");
            if (d.ShowDialog(this) != DialogResult.OK) return;
            src = d.SelectedPath;
        }
        ImportDb(src);
    }
    void ImportDb(string src)
    {
        string dest = Path.Combine(ClamDir, "database");
        Work("import pangkalan data ClamAV", () =>
        {
            string from = null;   // folder itu sendiri atau subfolder database/db
            foreach (string sub in new string[] { "", "database", "db", "ClamAV\\database" })
            {
                string p = sub.Length == 0 ? src : Path.Combine(src, sub);
                if (Directory.Exists(p) && (DbFile(p, "main") != null || DbFile(p, "daily") != null || DbFile(p, "bytecode") != null)) { from = p; break; }
            }
            if (from == null) { Log("Tiada fail main/daily/bytecode (.cvd atau .cld) dalam folder itu."); return; }
            Directory.CreateDirectory(dest);
            int ok = 0;
            foreach (string n in new string[] { "main", "daily", "bytecode" })
            {
                string f = DbFile(from, n); if (f == null) continue;
                DateTime? nd = DbDate(f);
                if (nd == null) { Log("Dilangkau (bukan pangkalan data ClamAV yang sah): {0}", Path.GetFileName(f)); continue; }
                string old = DbFile(dest, n); DateTime? od = old == null ? null : DbDate(old);
                if (od != null && od.Value > nd.Value) { Log("Dilangkau (lebih lama daripada yang ada): {0}", Path.GetFileName(f)); continue; }
                string tmp = Path.Combine(dest, n + ".tmp"), fin = Path.Combine(dest, Path.GetFileName(f));
                try
                {
                    File.Copy(f, tmp, true);
                    foreach (string e in new string[] { ".cvd", ".cld" }) { string o = Path.Combine(dest, n + e); if (File.Exists(o)) File.Delete(o); }   // elak dua salinan dimuatkan
                    File.Move(tmp, fin); ok++; Log("Disalin: {0}", Path.GetFileName(f));
                }
                catch (Exception ex) { Log("Gagal menyalin {0}: {1}", Path.GetFileName(f), ex.Message); try { File.Delete(tmp); } catch { } }
            }
            Log("Import selesai: {0} fail dikemas kini.", ok);
            Ui(RefreshClam);
        });
    }
    // tambah hasil ClamAV ke senarai fail mencurigakan (sudah ditanda); fail yang sama dikemas kini, bukan digandakan
    void ShowClamHits(List<string[]> hits)
    {
        Ui(() =>
        {
            foreach (string[] h in hits)
            {
                ListViewItem found = null;
                foreach (ListViewItem x in list.Items) if ((string)x.Tag == h[0]) found = x;
                string reason = "ClamAV: " + h[1];
                if (found != null) { found.SubItems[1].Text += " / " + reason; found.Checked = true; found.ImageIndex = 2; }
                else { ListViewItem hit = AddRow(list, h[0], h[0] + "\n" + reason, Path.GetFileName(h[0]), reason, Path.GetDirectoryName(h[0]) ?? ""); hit.Checked = true; hit.ImageIndex = 2; }
            }
            SetEmpty(list, "");
        });
    }
    string ClamScan(string r)
    {
        lastClamHits = 0;
        string exe = ClamExe(), db = ClamDb();
        if (exe == null) { Log("ClamAV tiada. Letak folder ClamAV di sebelah program ini."); return T("tidak tersedia"); }
        if (db == null) { Log("Pangkalan data ClamAV tiada atau kosong (folder database)."); return T("tidak tersedia"); }
        Log("Imbasan ClamAV pada {0} ... (boleh ambil masa, tunggu sehingga siap)", r);
        ProcessStartInfo psi = new ProcessStartInfo(exe, "-r --database=\"" + db + "\" \"" + r + "\\\"");   // \\" supaya garis condong hujung tak melarikan petikan
        psi.UseShellExecute = false; psi.CreateNoWindow = true; psi.RedirectStandardOutput = true; psi.WorkingDirectory = ClamDir;
        List<string[]> hits = new List<string[]>(); int count = 0, code; string cur = "";
        using (Process p = Process.Start(psi))
        {
            p.OutputDataReceived += (s, e) =>
            {
                if (e.Data == null) return;
                int i = e.Data.LastIndexOf(": "); if (i < 0) return;
                string st = e.Data.Substring(i + 2);
                lock (hits) { count++; cur = Path.GetFileName(e.Data.Substring(0, i)); if (st.EndsWith(" FOUND")) hits.Add(new string[] { e.Data.Substring(0, i), st.Substring(0, st.Length - 6) }); }
            };
            p.BeginOutputReadLine();
            while (!p.WaitForExit(250))
            {
                if (cancelReq) { try { p.Kill(); } catch { } break; }
                string c; lock (hits) c = cur; if (c.Length > 34) c = c.Substring(0, 31) + "..."; Status("mengimbas {0} fail: {1}", count, c);
            }
            if (cancelReq) { Log("Imbasan dibatalkan."); return T("dibatalkan"); }
            p.WaitForExit(); code = p.ExitCode;
        }
        if (code > 1) { Log("ClamAV melapor ralat (kod {0}) - semak pangkalan data dan versi enjin.", code); return string.Format(T("ClamAV melapor ralat (kod {0})"), code); }
        lastClamHits = hits.Count;
        foreach (string[] h in hits) Log("Dikesan ClamAV: {0}", h[0] + " (" + h[1] + ")");
        if (hits.Count == 0) { Log("Imbasan siap: tiada ancaman."); return T("tiada ancaman"); }
        ShowClamHits(hits);
        return string.Format(T("{0} ancaman dikesan (ClamAV, sudah ditanda)"), hits.Count);
    }
    void ActClam()
    {
        string r = Root(); if (r == null) return;
        if (ClamExe() == null) { Info("ClamAV belum ada. Letak folder ClamAV (mengandungi clamscan.exe dan folder database) di sebelah program ini, kemudian cuba lagi. Lihat fail BACA-SAYA dalam folder ClamAV."); return; }
        Work("imbasan ClamAV", () =>
        {
            AddHistory(r, "Imbasan ClamAV", ClamScan(r));
            if (lastClamHits > 0) pendingAfter = () => { ShowPage(0); Info("Imbasan ClamAV selesai. Fail yang dikesan sudah ditanda di halaman Pembersih; semak, kemudian klik Kuarantin yang ditanda."); };
        }, true);
    }

    // ---------- tindakan (butang & menu) ----------
    string BuildSummary(string r, string def, int fixedN, int q, int total, int strong, string vac)
    {
        return string.Format(T("Pembersihan {0} selesai.\n\nAntivirus: {1}\nFail/folder tersembunyi dipulihkan: {2}\nPintasan dan autorun dikuarantin: {3}\nFail mencurigakan: {4} ({5} hampir pasti virus, sudah ditanda)\nVaksin: {6}\n\nSemak senarai fail mencurigakan di skrin utama. Buka flashdisk sekarang untuk menyemak fail anda?"), r, def, fixedN, q, total, strong, vac);
    }
    void ActAll()
    {
        string r = RootEdit(); if (r == null) return;
        Work("pembersihan penuh (Defender mungkin ambil masa)", () =>
        {
            string def = Defender(r);
            int fixedN = Unhide(r), q = Lnk(r);
            int[] sc = Scan(r);
            if (def == T("tidak tersedia") && ClamExe() != null) def = ClamScan(r);   // Windows 7 tanpa Defender
            string vac = IsRemovable(r) ? T(Vaccinate(r) ? "dipasang" : "sudah dipasang") : T("dilangkau (bukan flashdisk)");
            Startup(false);
            AddHistory(r, "Bersihkan Semua", Fmt("Antivirus: {0}; fail mencurigakan: {1}; pintasan dikuarantin: {2}", new object[] { def, sc[0], q }));
            string sum = BuildSummary(r, def, fixedN, q, sc[0], sc[1], vac);
            pendingAfter = () => { if (Msg(sum, "Ringkasan", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes) Process.Start(r); };
        });
    }
    void Act1() { string r = RootEdit(); if (r != null) Work("memulihkan fail tersembunyi", () => Unhide(r)); }
    void Act2() { string r = RootEdit(); if (r != null) Work("mengkuarantin .lnk dan autorun", () => Lnk(r)); }
    void Act3() { string r = Root(); if (r != null) Work("mengimbas fail mencurigakan", () => Scan(r)); }
    void Act4() { string r = Root(); if (r != null) Work("imbasan Defender", () => AddHistory(r, "Imbasan Defender", Defender(r))); }
    void Act5() { Work("menyemak permulaan PC", () => Startup(false)); }
    void Act6() { Work("mengemas kini definisi Defender", () => UpdateDef()); }
    void ActT() { Work("fail sementara", () => TempClean(TempDirs(), false)); }
    void ActCache() { Work("cache pelayar", () => TempClean(CacheDirs(), true)); }
    // jika ada beberapa flashdisk dicucuk, tawarkan vaksin untuk semuanya sekali gus
    void ActV()
    {
        List<string> usb = new List<string>();
        foreach (DriveInfo d in DriveInfo.GetDrives()) if (d.DriveType == DriveType.Removable && d.IsReady) usb.Add(d.Name);
        if (usb.Count < 2) { string r = RootEdit(); if (r != null) Work("memasang vaksin", () => Vaccinate(r)); return; }
        if (!Ask(string.Format(T("Pasang vaksin pada {0} flashdisk ({1})?"), usb.Count, string.Join(", ", usb.ToArray())), MessageBoxIcon.Question)) return;
        Work("memasang vaksin", () =>
        {
            int ok = 0;
            foreach (string r in usb) { try { Log("{0}", r); if (Vaccinate(r)) ok++; } catch (Exception ex) { Log("Gagal pada {0}: {1}", r, ex.Message); } }
            Log("Vaksin baharu dipasang pada {0} daripada {1} flashdisk.", ok, usb.Count);
        });
    }
    void Act7() { Ext("Malwarebytes", new string[] { Environment.GetEnvironmentVariable("ProgramFiles") + @"\Malwarebytes\Anti-Malware\mbam.exe" }, "https://www.malwarebytes.com/mwb-download"); }
    void Act8() { Ext("ESET Online Scanner", new string[] { Path.Combine(exeDir, "esetonlinescanner.exe"), Path.Combine(Environment.GetEnvironmentVariable("USERPROFILE"), @"Downloads\esetonlinescanner.exe") }, "https://www.eset.com/int/home/online-scanner/"); }
    void ActOpenQ() { Directory.CreateDirectory(qDir); Process.Start(qDir); }

}
