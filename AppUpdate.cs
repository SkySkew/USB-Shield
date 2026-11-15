using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

// Semak versi baharu USB Shield pada GitHub Releases. Hanya membaca nombor versi dan membuka halaman muat turun;
// tiada fail dimuat turun atau dipasang oleh app. Tiada data pengguna dihantar (hanya permintaan GET biasa).
public partial class MainForm : Form
{
    // semak sehari sekali (jika dibenarkan); manual = klik "Semak sekarang"
    void AppUpdBg()
    {
        if (!appUpd || appChk == DateTime.Now.ToString("yyyyMMdd")) return;
        AppUpdCheck(false);
    }
    void AppUpdCheck(bool manual)
    {
        Thread t = new Thread(() =>
        {
            string tag = null, url = null; bool ok = false;
            try
            {
                ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072 | SecurityProtocolType.Tls;   // TLS 1.2 (GitHub); .NET 3.5 tak aktifkannya sendiri
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create("https://api.github.com/repos/" + Repo + "/releases/latest");
                req.UserAgent = "USB-Shield"; req.Timeout = 15000;
                string json;
                using (WebResponse r = req.GetResponse()) using (StreamReader sr = new StreamReader(r.GetResponseStream())) json = sr.ReadToEnd();
                Match m = Regex.Match(json, "\"tag_name\"\\s*:\\s*\"v?([0-9]+(?:\\.[0-9]+){1,3})\""), u = Regex.Match(json, "\"html_url\"\\s*:\\s*\"(https://github\\.com/[^\"]+)\"");
                if (m.Success && u.Success) { tag = m.Groups[1].Value; url = u.Groups[1].Value; ok = true; }
            }
            catch { }
            try
            {
                Ui(() =>
                {
                    if (ok) { appChk = DateTime.Now.ToString("yyyyMMdd"); SaveSettings(); }
                    if (!ok) { if (manual) Info("Tak dapat menyemak kemas kini. Pastikan ada internet, kemudian cuba lagi."); return; }
                    bool newer = new Version(tag.Contains(".") && tag.Split('.').Length < 3 ? tag + ".0" : tag) > new Version(AppVer);
                    if (!newer) { if (manual) Info(string.Format(T("USB Shield {0} sudah versi terkini."), AppVer)); return; }
                    if (!manual && appSeen == tag) return;   // sudah ditanya untuk versi ini
                    appSeen = tag; SaveSettings();
                    if (Ask(string.Format(T("USB Shield {0} sudah tersedia (anda guna {1}). Buka halaman muat turun?"), tag, AppVer), MessageBoxIcon.Information))
                        try { Process.Start(url); } catch { }
                });
            }
            catch { }
        });
        t.IsBackground = true; t.Start();
    }
}
