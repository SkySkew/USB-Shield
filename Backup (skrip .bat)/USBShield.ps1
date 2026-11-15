Add-Type -AssemblyName System.Windows.Forms, System.Drawing
[System.Windows.Forms.Application]::EnableVisualStyles()
$sysDrive = $env:SystemDrive + '\'
$sus = '.exe', '.vbs', '.vbe', '.js', '.jse', '.wsf', '.hta', '.pif', '.com', '.bat', '.cmd', '.scr'
$dblExt = '(?i)\.(doc|docx|xls|xlsx|ppt|pptx|pdf|jpg|jpeg|png|gif|txt|mp3|mp4|zip|rar)\.(exe|scr|bat|cmd|vbs|com|pif)$'
$runKeys = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run', 'HKLM:\Software\Microsoft\Windows\CurrentVersion\Run'
$apKey1 = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Policies\Explorer'
$apKey2 = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\AutoplayHandlers'
$utf8 = New-Object System.Text.UTF8Encoding $false
$qDir = Join-Path $env:USERPROFILE 'Kuarantin Pembersih'
$qIdx = Join-Path $qDir 'senarai.txt'
$script:lang = 'ms'

# ---------- bahasa: semua teks ditulis dalam Melayu, jadual ini menterjemah ke English ----------
$guideMs = "1. (Pilihan) Kemas kini Defender.`n2. Cucuk flashdisk dan pilih drive.`n3. Klik Bersihkan Semua.`n4. Semak senarai fail mencurigakan: tanda yang BUKAN milik anda dan kuarantin.`n5. Imbas seluruh PC dengan Windows Security > Full Scan."
$aboutMs = "USB Shield`nMembaiki kesan shortcut virus dan memanggil Microsoft Defender untuk membuang virus sebenar. Bukan enjin antivirus sendiri."
$en = @{
    'Pulihkan flashdisk, buang virus' = 'Restore flash drives, remove viruses'
    'Pembersih' = 'Cleaner'; 'Imbas Virus' = 'Virus Scan'; 'Lajukan PC' = 'Speed Up PC'; 'Kuarantin' = 'Quarantine'; 'Lanjutan' = 'Advanced'
    'Drive flashdisk:' = 'Flash drive:'; 'Muat semula' = 'Refresh'; 'Simpan log' = 'Save log'
    'Bersihkan Semua' = 'Clean Everything'
    'Defender, pulihkan, kuarantin, vaksin dan semak PC sekali gus (disyorkan).' = 'Defender, restore, quarantine, vaccinate and PC check in one go (recommended).'
    'Pulihkan fail tersembunyi' = 'Restore hidden files'; 'Kuarantin .lnk && autorun' = 'Quarantine .lnk && autorun'
    'Imbas fail mencurigakan' = 'Scan suspicious files'; 'Vaksin flashdisk' = 'Vaccinate drive'
    'Halang virus menulis autorun.inf (folder palsu read-only).' = 'Stops viruses writing autorun.inf (read-only decoy folder).'
    'Fail mencurigakan di root flashdisk (tanda yang BUKAN milik anda):' = 'Suspicious files in the drive root (tick the ones that are NOT yours):'
    'Kuarantin yang ditanda' = 'Quarantine ticked'
    'Imbas drive yang dipilih dan rawat ancaman secara automatik. Kemas kini definisi dahulu (perlu internet).' = 'Scan the selected drive and treat threats automatically. Update definitions first (needs internet).'
    'Imbas drive' = 'Scan drive'; 'Kemas kini definisi' = 'Update definitions'
    'Buka Malwarebytes (atau halaman muat turun rasmi). Pilih imbasan Custom dan drive flashdisk.' = 'Opens Malwarebytes (or its official download page). Choose a Custom scan and the flash drive.'
    'Buka Malwarebytes' = 'Open Malwarebytes'
    'Buka ESET Online Scanner (atau halaman muat turun rasmi). Pilih imbasan Custom dan drive flashdisk.' = 'Opens ESET Online Scanner (or its official download page). Choose a Custom scan and the flash drive.'
    'Buka ESET Online Scanner' = 'Open ESET Online Scanner'
    'Fail sementara (Temp)' = 'Temporary files (Temp)'
    'Padam fail sampah sementara untuk membebaskan ruang. Saiz ditunjuk dahulu dan awak sahkan sebelum dipadam.' = 'Delete temporary junk files to free space. The size is shown first and you confirm before deleting.'
    'Bersihkan fail sementara' = 'Clean temporary files'
    'Kosongkan Recycle Bin secara kekal untuk membebaskan ruang cakera.' = 'Permanently empty the Recycle Bin to free disk space.'
    'Kosongkan Recycle Bin' = 'Empty Recycle Bin'
    'Cakera dan program permulaan' = 'Disk and startup programs'
    'Semak ruang cakera. Kurangkan program permulaan supaya PC lebih cepat dihidupkan.' = 'Check disk space. Reduce startup programs so the PC boots faster.'
    'Semak ruang cakera' = 'Check disk space'; 'Senarai program permulaan' = 'List startup programs'
    'Fail dalam kuarantin:' = 'Files in quarantine:'
    'Fail dinamakan semula .kuarantin supaya tak boleh dijalankan secara tak sengaja. Pulihkan hanya jika anda pasti fail itu selamat.' = 'Files are renamed .kuarantin so they cannot run by accident. Restore only if you are sure a file is safe.'
    'Pulihkan yang ditanda' = 'Restore ticked'; 'Padam kekal yang ditanda' = 'Delete ticked permanently'; 'Buka folder kuarantin' = 'Open quarantine folder'
    'Semak permulaan PC' = 'Check PC startup'
    'Program permulaan PC yang mencurigakan (tanda untuk buang):' = 'Suspicious PC startup programs (tick to remove):'
    'Buang entri ditanda' = 'Remove ticked entries'
    'Keselamatan USB (tetapan akaun semasa)' = 'USB safety (current account setting)'
    'Matikan AutoPlay/AutoRun' = 'Turn off AutoPlay/AutoRun'; 'Pulihkan tetapan asal' = 'Restore default'
    'Fail' = 'File'; 'Alat' = 'Tools'; 'Bantuan' = 'Help'; 'Bahasa' = 'Language'
    'Muat semula drive' = 'Refresh drives'; 'Simpan log ke fail...' = 'Save log to file...'; 'Keluar' = 'Exit'
    'Imbas virus (Defender)' = 'Scan virus (Defender)'; 'Kemas kini Defender' = 'Update Defender'
    'Imbas dengan Malwarebytes' = 'Scan with Malwarebytes'; 'Imbas dengan ESET Online Scanner' = 'Scan with ESET Online Scanner'
    'Panduan' = 'Guide'; 'Tentang' = 'About'
    'Sedang berjalan' = 'Running'; '-- selesai: {0} --' = '-- done: {0} --'; 'RALAT: {0}' = 'ERROR: {0}'
    'pembersihan penuh (Defender mungkin ambil masa)' = 'full cleanup (Defender may take a while)'
    'memulihkan fail tersembunyi' = 'restoring hidden files'; 'mengkuarantin .lnk dan autorun' = 'quarantining .lnk and autorun'
    'mengimbas fail mencurigakan' = 'scanning suspicious files'; 'imbasan Defender' = 'Defender scan'
    'menyemak permulaan PC' = 'checking PC startup'; 'mengemas kini definisi Defender' = 'updating Defender definitions'
    'fail sementara' = 'temporary files'; 'memasang vaksin' = 'installing vaccine'
    'mengira {0} fail sementara ...' = 'counting {0} temporary files ...'; 'memadam {0} / {1} ...' = 'deleting {0} / {1} ...'; 'mengira baki ...' = 'counting the rest ...'
    'Tiada flashdisk dikesan. Cucuk flashdisk lalu klik Muat semula.' = 'No flash drive detected. Plug one in and click Refresh.'
    'Pilih drive dahulu.' = 'Select a drive first.'; 'Tiada item ditanda.' = 'No items ticked.'; 'Sahkan' = 'Confirm'
    'Memulihkan fail tersembunyi di {0} ...' = 'Restoring hidden files on {0} ...'; 'Siap: fail dan folder dipulihkan.' = 'Done: files and folders restored.'
    'Dikuarantin: {0}' = 'Quarantined: {0}'; 'Siap: {0} fail .lnk/autorun.inf dialihkan ke kuarantin.' = 'Done: {0} .lnk/autorun.inf files moved to quarantine.'
    'Vaksin dipasang: folder autorun.inf (read-only) menghalang virus menulis fail autorun.' = 'Vaccine installed: a read-only autorun.inf folder stops viruses writing an autorun file.'
    'Drive ini sudah divaksin.' = 'This drive is already vaccinated.'
    'sama nama dengan folder (ciri shortcut virus)' = 'same name as a folder (shortcut-virus sign)'
    'sambungan berganda, menyamar sebagai dokumen' = 'double extension, disguised as a document'
    'skrip di root flashdisk' = 'script in drive root'; 'fail boleh laku di root flashdisk' = 'executable in drive root'
    'Imbasan siap: {0} fail mencurigakan di root (semak sebelum kuarantin).' = 'Scan done: {0} suspicious files in the root (review before quarantining).'
    'Selesai: {0}' = 'Done: {0}'; 'GAGAL: {0} ({1}) - cuba jalankan sebagai Administrator' = 'FAILED: {0} ({1}) - try running as Administrator'
    'Padam item ini secara kekal?' = 'Permanently delete these items?'
    'Alihkan fail ini ke kuarantin? Anda boleh memulihkannya kemudian.' = 'Move these files to quarantine? You can restore them later.'
    'Pulihkan fail ini ke lokasi asal? Jika ia virus, ia akan aktif semula.' = 'Restore these files to their original location? If they are viruses they will be active again.'
    'Padam kekal fail kuarantin ini? Ia tak boleh dipulihkan.' = 'Permanently delete these quarantined files? They cannot be restored.'
    'Imbasan Defender pada {0} ... (boleh ambil masa, tunggu sehingga siap)' = 'Defender scan on {0} ... (can take a while, wait until it finishes)'
    'Defender/Security Essentials tiada. Guna antivirus lain untuk imbas drive ini.' = 'Defender/Security Essentials not found. Use another antivirus to scan this drive.'
    'Imbasan siap: tiada ancaman.' = 'Scan done: no threats.'
    'Imbasan siap (kod {0}): ancaman mungkin dikesan/dirawat - semak Sejarah (History) dalam Defender/Security Essentials.' = 'Scan done (code {0}): threats may have been found/treated - check History in Defender/Security Essentials.'
    'Imbasan Defender gagal (Defender dimatikan / perlu Administrator).' = 'Defender scan failed (Defender off / Administrator needed).'
    'Dikesan dan dirawat Defender: {0}' = 'Detected and treated by Defender: {0}'; 'Defender: tiada ancaman dikesan.' = 'Defender: no threats detected.'
    'Mengemas kini definisi virus Defender (perlu internet) ...' = 'Updating Defender virus definitions (needs internet) ...'
    'Defender/Security Essentials tiada.' = 'Defender/Security Essentials not found.'
    'Definisi virus dikemas kini.' = 'Virus definitions updated.'; 'Kemas kini gagal (semak internet).' = 'Update failed (check internet).'
    'Membuka {0} - pilih imbasan Custom dan pilih drive flashdisk.' = 'Opening {0} - choose a Custom scan and pick the flash drive.'
    '{0} belum dipasang/dimuat turun. Membuka halaman rasmi: muat turun, jalankan, kemudian imbas drive flashdisk.' = '{0} is not installed/downloaded. Opening the official page: download it, run it, then scan the flash drive.'
    'Semakan permulaan PC: {0} entri.' = 'PC startup check: {0} entries.'
    'Mengira fail sementara (boleh ambil masa jika banyak) ...' = 'Counting temporary files (can take a while if there are many) ...'
    'Tiada fail sementara.' = 'No temporary files.'; 'Padam {0} fail sementara (~{1} MB)?' = 'Delete {0} temporary files (~{1} MB)?'
    'Fail yang sedang digunakan akan dilangkau. Folder Windows\Temp perlu Administrator.' = 'Files in use are skipped. The Windows\Temp folder needs Administrator.'
    'Siap: ~{0} MB dibebaskan ({1} fail sedang digunakan dilangkau).' = 'Done: ~{0} MB freed ({1} files in use were skipped).'
    'Windows ini tiada arahan kosongkan Recycle Bin. Kosongkan secara manual.' = 'This Windows has no empty-Recycle-Bin command. Empty it manually.'
    'Kosongkan Recycle Bin secara kekal? Fail di dalamnya tak boleh dipulihkan.' = 'Permanently empty the Recycle Bin? Files in it cannot be restored.'
    'Recycle Bin dikosongkan.' = 'Recycle Bin emptied.'; '{0} bebas {1} GB daripada {2} GB' = '{0} free {1} GB of {2} GB'
    'Kuarantin dimuat semula: {0} item.' = 'Quarantine reloaded: {0} items.'
    'fail kuarantin tiada' = 'quarantined file missing'; 'lokasi asal tiada (cucuk semula drive)' = 'original location missing (plug the drive in again)'
    'Matikan AutoPlay/AutoRun untuk akaun ini? Flashdisk tak akan dibuka atau dijalankan sendiri. Berkuat kuasa selepas log keluar dan masuk semula.' = 'Turn off AutoPlay/AutoRun for this account? Flash drives will not open or run by themselves. Takes effect after you sign out and in again.'
    'AutoPlay/AutoRun dimatikan untuk akaun ini.' = 'AutoPlay/AutoRun turned off for this account.'
    'Pulihkan tetapan AutoPlay/AutoRun ke lalai Windows?' = 'Restore AutoPlay/AutoRun to Windows defaults?'
    'Tetapan AutoPlay/AutoRun dipulihkan.' = 'AutoPlay/AutoRun settings restored.'
    'Log disimpan: {0}' = 'Log saved: {0}'
    'Bersihkan flashdisk' = 'Clean your flash drive'
    'ClamAV tiada. Letak folder ClamAV di sebelah program ini.' = 'ClamAV not found. Put the ClamAV folder next to this program.'
    'Pangkalan data ClamAV tiada atau kosong (folder database).' = 'ClamAV database missing or empty (database folder).'
    'Imbasan ClamAV pada {0} ... (boleh ambil masa, tunggu sehingga siap)' = 'ClamAV scan on {0} ... (can take a while, wait until it finishes)'
    'ClamAV melapor ralat (kod {0}) - semak pangkalan data dan versi enjin.' = 'ClamAV reported an error (code {0}) - check the database and engine version.'
    'Dikesan ClamAV: {0}' = 'Detected by ClamAV: {0}'
    'ClamAV: {0} ancaman dikesan dan ditanda di senarai. Semak, kemudian Kuarantin yang ditanda.' = 'ClamAV: {0} threats found and ticked in the list. Review, then Quarantine the ticked ones.'
    'imbasan ClamAV' = 'ClamAV scan'
    'Imbas dengan ClamAV (luar talian)' = 'Scan with ClamAV (offline)'
}
$en[$guideMs] = "1. (Optional) Update Defender.`n2. Plug in the flash drive and select it.`n3. Click Clean Everything.`n4. Review the suspicious files: tick the ones that are NOT yours and quarantine them.`n5. Scan the whole PC with Windows Security > Full Scan."
$en[$aboutMs] = "USB Shield`nRepairs the damage of shortcut viruses and calls Microsoft Defender to remove real viruses. It is not an antivirus engine itself."

function T($s) {
    if ($script:lang -ne 'en' -or -not $s) { return $s }
    if ($en.ContainsKey($s)) { return $en[$s] }
    return $s
}
# Log/Status/Msg: templat {0},{1} + argumen, supaya boleh diterjemah
function Fmt($a) {
    $t = T $a[0]
    if ($a.Count -gt 1) { $t = $t -f @($a[1..($a.Count - 1)]) }
    $t
}
function Log { $log.AppendText((Fmt $args) + "`r`n") }
function Msg($text, $title, $buttons, $icon, $fmt) {
    if (-not $buttons) { $buttons = 'OK' }
    if (-not $icon) { $icon = 'None' }
    $t = T $text
    if ($fmt) { $t = $t -f $fmt }
    [System.Windows.Forms.MessageBox]::Show($t, (T $title), $buttons, $icon)
}

# ---------- logik ----------
# jalankan proses tersembunyi, UI kekal hidup; pulangkan exit code
function Run($exe, $a) {
    $p = Start-Process $exe -ArgumentList $a -WindowStyle Hidden -PassThru
    while (-not $p.HasExited) { [System.Windows.Forms.Application]::DoEvents(); Start-Sleep -Milliseconds 50 }
    $p.ExitCode
}
function RunHidden($cmd) { Run 'cmd' "/c $cmd" }
function RunPS($cmd) { Run 'powershell' "-NoProfile -Command `"$cmd`"" }
# Windows 7: Microsoft Security Essentials / Windows Defender lama tiada cmdlet, guna MpCmdRun.exe
function MpExe {
    foreach ($p in "$env:ProgramFiles\Microsoft Security Client\MpCmdRun.exe", "$env:ProgramFiles\Windows Defender\MpCmdRun.exe") {
        if (Test-Path $p) { return $p }
    }
}

function LoadDrives {
    $cb.Items.Clear()
    [System.IO.DriveInfo]::GetDrives() | Where-Object { $_.IsReady -and $_.Name -ne $sysDrive } | ForEach-Object {
        [void]$cb.Items.Add("$($_.Name)   $($_.VolumeLabel)   [$($_.DriveType)]")
    }
    if ($cb.Items.Count) { $cb.SelectedIndex = 0 } else { Log 'Tiada flashdisk dikesan. Cucuk flashdisk lalu klik Muat semula.' }
}

function Root {
    if ($cb.SelectedIndex -lt 0) { [void](Msg 'Pilih drive dahulu.' 'Pembersih'); return $null }
    $cb.Text.Substring(0, 3)
}
function IsRemovable($r) { (New-Object System.IO.DriveInfo $r).DriveType -eq 'Removable' }

function Unhide($r) {
    Log 'Memulihkan fail tersembunyi di {0} ...' $r
    [void](RunHidden "attrib -h -s -r `"$($r)*.*`" /s /d")
    Log 'Siap: fail dan folder dipulihkan.'
}

# kuarantin: alih ke PC (bukan padam), dinamakan semula .kuarantin; indeks id|laluan-asal
function Quar($path) {
    if (-not (Test-Path -LiteralPath $qDir)) { [void](New-Item $qDir -ItemType Directory) }
    $id = [guid]::NewGuid().ToString('N').Substring(0, 8)
    [System.IO.File]::SetAttributes($path, 'Normal')
    [System.IO.File]::Move($path, (Join-Path $qDir "$id-$(Split-Path $path -Leaf).kuarantin"))
    [System.IO.File]::AppendAllText($qIdx, "$id|$path`r`n", $utf8)
}
function QId($item) { if ($item -match '\[([0-9a-f]{8})\]$') { $Matches[1] } }
function QStored($id) { Get-ChildItem -LiteralPath $qDir -Filter "$id-*" | Select-Object -First 1 }
function QForget($id) {
    $keep = @([System.IO.File]::ReadAllLines($qIdx, $utf8) | Where-Object { $_ -and -not $_.StartsWith("$id|") })
    [System.IO.File]::WriteAllLines($qIdx, [string[]]$keep, $utf8)
}
function QLoad {
    $listQ.Items.Clear()
    if (-not (Test-Path -LiteralPath $qIdx)) { return }
    foreach ($l in [System.IO.File]::ReadAllLines($qIdx, $utf8)) {
        $id = $l.Split('|')[0]
        if ($id -and (Get-ChildItem -LiteralPath $qDir -Filter "$id-*" -ErrorAction SilentlyContinue)) {
            [void]$listQ.Items.Add($l.Substring($id.Length + 1) + "  [$id]")
        }
    }
}
function QRestore($item) {
    $id = QId $item
    $orig = $item -replace '\s+\[[0-9a-f]{8}\]$', ''
    $st = QStored $id
    if (-not $st) { throw (T 'fail kuarantin tiada') }
    if (-not (Test-Path -LiteralPath (Split-Path $orig))) { throw (T 'lokasi asal tiada (cucuk semula drive)') }
    if (Test-Path -LiteralPath $orig) { $orig += '.dipulihkan' }
    [System.IO.File]::Move($st.FullName, $orig)
    QForget $id
}
function QDelete($item) {
    $id = QId $item; $st = QStored $id
    if ($st) { Remove-Item -LiteralPath $st.FullName -Force }
    QForget $id
}

function Lnk($r) {
    $n = 0
    @(Get-ChildItem -LiteralPath $r -Force | Where-Object { -not $_.PSIsContainer -and ($_.Name -like '*.lnk' -or $_.Name -ieq 'autorun.inf') }) | ForEach-Object {
        Quar $_.FullName; Log 'Dikuarantin: {0}' $_.Name; $n++
    }
    Log 'Siap: {0} fail .lnk/autorun.inf dialihkan ke kuarantin.' $n
}

# vaksin: folder autorun.inf read-only/hidden/system, jadi virus tak boleh mencipta fail autorun.inf
function Vaccinate($r) {
    $p = "$($r)autorun.inf"
    if (Test-Path -LiteralPath $p) {
        if ((Get-Item -LiteralPath $p -Force).PSIsContainer) { Log 'Drive ini sudah divaksin.'; return }
        Quar $p
    }
    [void](New-Item -ItemType Directory -Path $p)
    [void](RunHidden "attrib +r +h +s `"$p`"")
    Log 'Vaksin dipasang: folder autorun.inf (read-only) menghalang virus menulis fail autorun.'
}

function Scan($r) {
    $list.Items.Clear()
    $all = @(Get-ChildItem -LiteralPath $r -Force)
    $dirs = @($all | Where-Object { $_.PSIsContainer } | ForEach-Object { $_.Name.ToLower() })
    foreach ($x in $all) {
        $ext = $x.Extension.ToLower()
        if ($x.PSIsContainer -or $sus -notcontains $ext) { continue }
        if ($dirs -contains [System.IO.Path]::GetFileNameWithoutExtension($x.Name).ToLower()) { $why = 'sama nama dengan folder (ciri shortcut virus)' }
        elseif ($x.Name -match $dblExt) { $why = 'sambungan berganda, menyamar sebagai dokumen' }
        elseif ('.vbs', '.vbe', '.js', '.jse', '.wsf', '.hta' -contains $ext) { $why = 'skrip di root flashdisk' }
        else { $why = 'fail boleh laku di root flashdisk' }
        [void]$list.Items.Add("$($x.FullName) :: $(T $why)")
    }
    Log 'Imbasan siap: {0} fail mencurigakan di root (semak sebelum kuarantin).' $list.Items.Count
}

# lakukan $action pada setiap item ditanda selepas pengesahan; item senarai boleh berbentuk "nilai :: sebab"
function DoChecked($lb, $action, $msg) {
    $sel = @($lb.CheckedItems)
    if (-not $sel) { [void](Msg 'Tiada item ditanda.' 'Pembersih'); return }
    $q = (T $msg) + "`n`n" + (($sel | ForEach-Object { ($_ -split ' :: ')[0] }) -join "`n")
    if ((Msg $q 'Sahkan' 'YesNo' 'Warning') -ne 'Yes') { return }
    foreach ($i in $sel) {
        $name = ($i -split ' :: ')[0]
        try { & $action $name; $lb.Items.Remove($i); Log 'Selesai: {0}' $name }
        catch { Log 'GAGAL: {0} ({1}) - cuba jalankan sebagai Administrator' $name (T $_.Exception.Message) }
    }
}

# Defender membuang virus sebenar (trojan, worm dll) - kita cuma memanggilnya
function Defender($r) {
    Log 'Imbasan Defender pada {0} ... (boleh ambil masa, tunggu sehingga siap)' $r
    if (-not (Get-Command Start-MpScan -ErrorAction SilentlyContinue)) {
        $mp = MpExe
        if (-not $mp) { Log 'Defender/Security Essentials tiada. Guna antivirus lain untuk imbas drive ini.'; return 'none' }
        # \\" supaya garis condong hujung tidak melarikan tanda petikan
        $c = Run $mp "-Scan -ScanType 3 -File `"$r\`""
        if ($c -eq 0) { Log 'Imbasan siap: tiada ancaman.' } else { Log 'Imbasan siap (kod {0}): ancaman mungkin dikesan/dirawat - semak Sejarah (History) dalam Defender/Security Essentials.' $c }
        return
    }
    $t0 = Get-Date
    if ((RunPS "Start-MpScan -ScanType CustomScan -ScanPath '$r'") -ne 0) { Log 'Imbasan Defender gagal (Defender dimatikan / perlu Administrator).'; return }
    $hits = @(Get-MpThreatDetection -ErrorAction SilentlyContinue | Where-Object { $_.InitialDetectionTime -gt $t0 })
    if ($hits) { $hits | ForEach-Object { Log 'Dikesan dan dirawat Defender: {0}' ($_.Resources -join ', ') } } else { Log 'Defender: tiada ancaman dikesan.' }
}

function UpdateDef {
    Log 'Mengemas kini definisi virus Defender (perlu internet) ...'
    if (Get-Command Update-MpSignature -ErrorAction SilentlyContinue) { $c = RunPS 'Update-MpSignature' }
    else {
        $mp = MpExe
        if (-not $mp) { Log 'Defender/Security Essentials tiada.'; return }
        $c = Run $mp '-SignatureUpdate'
    }
    if ($c -eq 0) { Log 'Definisi virus dikemas kini.' } else { Log 'Kemas kini gagal (semak internet).' }
}

# ClamAV luar talian: letak folder ClamAV (clamscan.exe + folder database) sebelah skrip ini; hasil masuk senarai (untuk dikuarantin)
function Clam($r) {
    $cd = Join-Path $here 'ClamAV'; if (-not (Test-Path $cd)) { $cd = Join-Path (Split-Path -Parent $here) 'ClamAV' }   # folder ClamAV di sini atau di folder atas
    $exe = Join-Path $cd 'clamscan.exe'; $db = $null
    if (-not (Test-Path $exe)) { Log 'ClamAV tiada. Letak folder ClamAV di sebelah program ini.'; return 'none' }
    foreach ($n in 'database', 'db') {
        $d = Join-Path $cd $n
        if ((Test-Path $d) -and @(Get-ChildItem "$d\*.cvd", "$d\*.cld" -ErrorAction SilentlyContinue).Count) { $db = $d; break }
    }
    if (-not $db) { Log 'Pangkalan data ClamAV tiada atau kosong (folder database).'; return 'none' }
    Log 'Imbasan ClamAV pada {0} ... (boleh ambil masa, tunggu sehingga siap)' $r
    $out = Join-Path $env:TEMP 'usbshield-clam.txt'
    # -i: hanya fail dijangkiti dilaporkan; \\" supaya garis condong hujung tidak melarikan petikan
    $p = Start-Process $exe -ArgumentList "-r -i --database=`"$db`" `"$r\`"" -WindowStyle Hidden -PassThru -RedirectStandardOutput $out -WorkingDirectory (Split-Path $exe)
    while (-not $p.HasExited) { Pump; Start-Sleep -Milliseconds 100 }
    $code = $p.ExitCode
    if ($code -gt 1) { Log 'ClamAV melapor ralat (kod {0}) - semak pangkalan data dan versi enjin.' $code; return }
    $n = 0
    foreach ($l in @(Get-Content $out -ErrorAction SilentlyContinue)) {
        if ($l -match '^(.+): (.+) FOUND$') { [void]$list.Items.Add("$($Matches[1]) :: ClamAV: $($Matches[2])", $true); $n++; Log 'Dikesan ClamAV: {0}' "$($Matches[1]) ($($Matches[2]))" }
    }
    Remove-Item $out -ErrorAction SilentlyContinue
    if ($n) { Log 'ClamAV: {0} ancaman dikesan dan ditanda di senarai. Semak, kemudian Kuarantin yang ditanda.' $n } else { Log 'Imbasan siap: tiada ancaman.' }
}

# Malwarebytes/ESET percuma tiada mod arahan: kita hanya bukanya (atau halaman muat turun rasmi), pengguna klik Scan
if ($env:PEMBERSIH_DIR) { $here = $env:PEMBERSIH_DIR } else { $here = Split-Path -Parent $MyInvocation.MyCommand.Path }
function Ext($nama, $paths, $url) {
    foreach ($p in $paths) {
        if (Test-Path $p) { Log 'Membuka {0} - pilih imbasan Custom dan pilih drive flashdisk.' $nama; [void](Start-Process $p); return }
    }
    Log '{0} belum dipasang/dimuat turun. Membuka halaman rasmi: muat turun, jalankan, kemudian imbas drive flashdisk.' $nama
    Start-Process $url
}

# program permulaan PC yang menuding ke drive bukan-tetap / skrip / Temp = tanda virus tinggal di PC
function Startup($all) {
    $list2.Items.Clear()
    $fix = [System.IO.DriveInfo]::GetDrives() | Where-Object { $_.DriveType -eq 'Fixed' } | ForEach-Object { $_.Name.Substring(0, 1).ToUpper() }
    foreach ($k in $runKeys) {
        $p = Get-ItemProperty $k -ErrorAction SilentlyContinue
        if (-not $p) { continue }
        foreach ($e in $p.PSObject.Properties | Where-Object { $_.Name -notlike 'PS*' }) {
            $v = [string]$e.Value
            $bad = [bool]$all -or ($v -match '(?i)wscript|cscript|\.vbs\b|\.scr\b|\\Temp\\|\\RECYCLER')
            if ($v -match '^"?([A-Za-z]):\\' -and $fix -notcontains $Matches[1].ToUpper()) { $bad = $true }
            if ($bad) { [void]$list2.Items.Add("$k|$($e.Name)|$v") }
        }
    }
    Log 'Semakan permulaan PC: {0} entri.' $list2.Items.Count
}

function AutoOff {
    if ((Msg 'Matikan AutoPlay/AutoRun untuk akaun ini? Flashdisk tak akan dibuka atau dijalankan sendiri. Berkuat kuasa selepas log keluar dan masuk semula.' 'Sahkan' 'YesNo' 'Question') -ne 'Yes') { return }
    foreach ($k in $apKey1, $apKey2) { if (-not (Test-Path $k)) { [void](New-Item $k -Force) } }
    Set-ItemProperty $apKey1 -Name NoDriveTypeAutoRun -Value 255 -Type DWord
    Set-ItemProperty $apKey2 -Name DisableAutoplay -Value 1 -Type DWord
    Log 'AutoPlay/AutoRun dimatikan untuk akaun ini.'
}
function AutoOn {
    if ((Msg 'Pulihkan tetapan AutoPlay/AutoRun ke lalai Windows?' 'Sahkan' 'YesNo' 'Question') -ne 'Yes') { return }
    Remove-ItemProperty $apKey1 -Name NoDriveTypeAutoRun -ErrorAction SilentlyContinue
    Remove-ItemProperty $apKey2 -Name DisableAutoplay -ErrorAction SilentlyContinue
    Log 'Tetapan AutoPlay/AutoRun dipulihkan.'
}

function SaveLog {
    $d = New-Object System.Windows.Forms.SaveFileDialog
    $d.Filter = 'Teks (*.txt)|*.txt'; $d.FileName = 'laporan-' + (Get-Date -Format 'yyyyMMdd-HHmm') + '.txt'
    if ($d.ShowDialog() -eq 'OK') {
        [System.IO.File]::WriteAllText($d.FileName, "USB Shield - $(Get-Date)`r`n`r`n" + $log.Text, $utf8)
        Log 'Log disimpan: {0}' $d.FileName
    }
}

# ---------- lajukan PC (selamat: hanya fail sementara, Recycle Bin, maklumat) ----------
$tempDirs = @($env:TEMP, "$env:windir\Temp")
function Pump { [System.Windows.Forms.Application]::DoEvents() }
function Status { $logLbl.Text = "$(T 'Sedang berjalan'): $(Fmt $args)"; Pump }

# tunjuk bahawa program sedang bekerja: bar bergerak, label status, butang dimatikan, kursor tunggu
function Busy($on, $text) {
    $bar.Running = $on
    if ($on) { $logLbl.Text = "$(T 'Sedang berjalan'): $(T $text)"; $logLbl.ForeColor = '#2E9E5B' } else { $logLbl.Text = T 'Log'; $logLbl.ForeColor = 'Black' }
    foreach ($pg in $pages) { $pg.Enabled = -not $on }
    $cb.Enabled = -not $on; $bRef.Enabled = -not $on; $ms.Enabled = -not $on
    $f.UseWaitCursor = $on
    Pump
}
function Work($text, $sb) {
    Busy $true $text
    try { & $sb } catch { Log 'RALAT: {0}' $_.Exception.Message } finally { Busy $false; Log '-- selesai: {0} --' (T $text) }
}

function TempStat {
    $n = 0; $sum = 0
    foreach ($d in $tempDirs) {
        if (-not (Test-Path $d)) { continue }
        Get-ChildItem -LiteralPath $d -Recurse -Force -ErrorAction SilentlyContinue | ForEach-Object {
            if (-not $_.PSIsContainer) { $n++; $sum += $_.Length; if ($n % 200 -eq 0) { Status 'mengira {0} fail sementara ...' $n } }
        }
    }
    @{ Count = $n; Sum = $sum }
}
function TempClean {
    Log 'Mengira fail sementara (boleh ambil masa jika banyak) ...'
    $m = TempStat
    if (-not $m.Count) { Log 'Tiada fail sementara.'; return }
    $q = ((T 'Padam {0} fail sementara (~{1} MB)?') -f $m.Count, [math]::Round($m.Sum / 1MB, 1)) + "`n" + (T 'Fail yang sedang digunakan akan dilangkau. Folder Windows\Temp perlu Administrator.')
    if ((Msg $q 'Sahkan' 'YesNo' 'Question') -ne 'Yes') { return }
    $items = @(foreach ($d in $tempDirs) { if (Test-Path $d) { Get-ChildItem -LiteralPath $d -Force -ErrorAction SilentlyContinue } })
    $i = 0
    foreach ($it in $items) {
        $i++; Status 'memadam {0} / {1} ...' $i $items.Count
        Remove-Item -LiteralPath $it.FullName -Recurse -Force -ErrorAction SilentlyContinue
    }
    Status 'mengira baki ...'
    $left = TempStat
    Log 'Siap: ~{0} MB dibebaskan ({1} fail sedang digunakan dilangkau).' ([math]::Round(($m.Sum - $left.Sum) / 1MB, 1)) $left.Count
}
function RecycleClean {
    if (-not (Get-Command Clear-RecycleBin -ErrorAction SilentlyContinue)) { Log 'Windows ini tiada arahan kosongkan Recycle Bin. Kosongkan secara manual.'; return }
    if ((Msg 'Kosongkan Recycle Bin secara kekal? Fail di dalamnya tak boleh dipulihkan.' 'Sahkan' 'YesNo' 'Warning') -ne 'Yes') { return }
    Clear-RecycleBin -Force -ErrorAction SilentlyContinue
    Log 'Recycle Bin dikosongkan.'
}
function DiskInfo {
    [System.IO.DriveInfo]::GetDrives() | Where-Object { $_.DriveType -eq 'Fixed' -and $_.IsReady } | ForEach-Object {
        Log '{0} bebas {1} GB daripada {2} GB' $_.Name ([math]::Round($_.AvailableFreeSpace / 1GB, 1)) ([math]::Round($_.TotalSize / 1GB, 1))
    }
}

# ---------- UI moden: kawalan dilukis sendiri (butang bulat, kad, bar sibuk), dikompil sekali semasa program dibuka ----------
$src = @'
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
    public Color Fill = Color.FromArgb(22, 163, 74);
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
        Color tc = Enabled ? TextColor : UiUtil.Mix(TextColor, bg, 0.5f);
        TextRenderer.DrawText(g, Text, Font, ClientRectangle, tc,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.HidePrefix | TextFormatFlags.EndEllipsis);
    }
}

public class NavButton : Button {
    public bool Active;
    public string Glyph = "";
    static bool hasGlyphFont;
    bool hover;
    static NavButton() {
        foreach (FontFamily ff in FontFamily.Families) if (ff.Name == "Segoe MDL2 Assets") hasGlyphFont = true;
    }
    public NavButton() {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Cursor = Cursors.Hand;
    }
    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
    protected override void OnPaint(PaintEventArgs e) {
        Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Parent != null ? Parent.BackColor : SystemColors.Control);
        Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
        if (Active || hover) {
            using (GraphicsPath p = UiUtil.Round(r, 8))
            using (SolidBrush b = new SolidBrush(Active ? Color.FromArgb(30, 41, 59) : Color.FromArgb(22, 32, 52))) g.FillPath(b, p);
        }
        if (Active) using (SolidBrush b = new SolidBrush(Color.FromArgb(34, 197, 94))) g.FillRectangle(b, 0, 11, 3, Height - 22);
        Color tc = Active ? Color.White : Color.FromArgb(148, 163, 184);
        int tx = 16;
        if (Glyph != "" && hasGlyphFont) {
            using (Font gf = new Font("Segoe MDL2 Assets", 12f))
                TextRenderer.DrawText(g, Glyph, gf, new Rectangle(14, 0, 28, Height), tc,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            tx = 46;
        }
        TextRenderer.DrawText(g, Text, Font, new Rectangle(tx, 0, Width - tx, Height), tc,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.HidePrefix | TextFormatFlags.EndEllipsis);
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
    public bool Running { get { return t.Enabled; } set { t.Enabled = value; Invalidate(); } }
    protected override void OnPaint(PaintEventArgs e) {
        Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Parent != null ? Parent.BackColor : SystemColors.Control);
        if (!Running) return;
        Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
        using (GraphicsPath p = UiUtil.Round(r, Height / 2)) {
            using (SolidBrush b = new SolidBrush(Color.FromArgb(226, 232, 240))) g.FillPath(b, p);
            g.SetClip(p);
            using (SolidBrush b = new SolidBrush(Color.FromArgb(22, 163, 74))) g.FillRectangle(b, pos - 140, 0, 140, Height);
        }
    }
}
'@
Add-Type -TypeDefinition $src -ReferencedAssemblies System.Windows.Forms, System.Drawing

function Col($h) { [System.Drawing.ColorTranslator]::FromHtml($h) }
$C = @{ Bg = '#F4F6FA'; Side = '#0F172A'; Ink = '#0F172A'; Muted = '#64748B' }
$fReg = New-Object System.Drawing.Font('Segoe UI', 9.5)
$fBold = New-Object System.Drawing.Font('Segoe UI', 10, [System.Drawing.FontStyle]::Bold)
$fTitle = New-Object System.Drawing.Font('Segoe UI', 13, [System.Drawing.FontStyle]::Bold)
$fBig = New-Object System.Drawing.Font('Segoe UI', 11, [System.Drawing.FontStyle]::Bold)
$fSmall = New-Object System.Drawing.Font('Segoe UI', 9)
# gaya butang: isi, warna teks, garis
$styles = @{
    primary = @('#16A34A', '#FFFFFF', ''); secondary = @('#FFFFFF', '#0F172A', '#CBD5E1'); danger = @('#DC2626', '#FFFFFF', '')
    info = @('#2563EB', '#FFFFFF', ''); violet = @('#7C3AED', '#FFFFFF', ''); teal = @('#0D9488', '#FFFFFF', ''); muted = @('#64748B', '#FFFFFF', '')
}
function Btn($t, $x, $y, $w, $h, $style) {
    $s = $styles[$style]
    $b = New-Object RButton
    $b.Text = $t; $b.Location = "$x,$y"; $b.Size = "$w,$h"; $b.Font = $fReg
    $b.Fill = (Col $s[0]); $b.TextColor = (Col $s[1])
    if ($s[2]) { $b.Border = (Col $s[2]) }
    $b
}
function Lbl($t, $x, $y, $w, $h, $bold) {
    $l = New-Object System.Windows.Forms.Label
    $l.Text = $t; $l.Location = "$x,$y"; $l.AutoSize = $false; $l.Size = "$w,$h"; $l.BackColor = 'Transparent'
    $l.ForeColor = (Col $C.Ink); $l.Font = $fReg
    if ($bold) { $l.Font = $fBold }
    $l
}
function Muted($l) { $l.ForeColor = (Col $C.Muted); $l }
function Box($x, $y, $w, $h) {
    $c = New-Object CardPanel
    $c.Location = "$x,$y"; $c.Size = "$w,$h"; $c.BackColor = (Col '#FFFFFF')
    $c
}
function ListCard($y, $h) {
    $c = Box 20 $y 570 $h; $c.Padding = '10,8,10,8'
    $l = New-Object System.Windows.Forms.CheckedListBox
    $l.Dock = 'Fill'; $l.BorderStyle = 'None'; $l.BackColor = (Col '#FFFFFF'); $l.CheckOnClick = $true
    $l.HorizontalScrollbar = $true; $l.Font = $fReg
    $c.Controls.Add($l); $c
}
function InfoCard($y, $title, $desc) {
    $c = Box 20 $y 570 100
    $c.Controls.AddRange(@((Lbl $title 18 14 320 24 $true), (Muted (Lbl $desc 18 40 320 52 $false))))
    $c
}
function Page { $p = New-Object System.Windows.Forms.Panel; $p.Location = '0,76'; $p.Size = '610,340'; $p.Visible = $false; $p }

# logo: perisai gradien hijau-teal dengan simbol USB putih (dilukis dengan kod, skala bebas)
function LogoBmp($px) {
    $bmp = New-Object System.Drawing.Bitmap $px, $px
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'; $g.Clear([System.Drawing.Color]::Transparent)
    $g.ScaleTransform($px / 256, $px / 256)
    $sh = New-Object System.Drawing.Drawing2D.GraphicsPath
    $sh.AddBezier(128, 12, 158, 34, 194, 44, 226, 44); $sh.AddLine(226, 44, 226, 120)
    $sh.AddBezier(226, 120, 226, 176, 186, 218, 128, 244); $sh.AddBezier(128, 244, 70, 218, 30, 176, 30, 120)
    $sh.AddLine(30, 120, 30, 44); $sh.AddBezier(30, 44, 62, 44, 98, 34, 128, 12); $sh.CloseFigure()
    $g1 = [System.Drawing.ColorTranslator]::FromHtml('#22C55E'); $g2 = [System.Drawing.ColorTranslator]::FromHtml('#0D9488')
    $br = New-Object System.Drawing.Drawing2D.LinearGradientBrush (New-Object System.Drawing.Point 30, 12), (New-Object System.Drawing.Point 226, 244), $g1, $g2
    $g.FillPath($br, $sh)
    $w = [System.Drawing.Color]::White; $wb = New-Object System.Drawing.SolidBrush $w
    $pen = New-Object System.Drawing.Pen $w, 12; $pen.StartCap = 'Round'; $pen.EndCap = 'Round'; $pen.LineJoin = 'Round'
    $g.DrawLine($pen, 128, 66, 128, 172); $g.FillEllipse($wb, 113, 171, 30, 30)
    $g.FillPolygon($wb, @((New-Object System.Drawing.Point 110, 76), (New-Object System.Drawing.Point 146, 76), (New-Object System.Drawing.Point 128, 44)))
    $g.DrawLines($pen, @((New-Object System.Drawing.Point 128, 142), (New-Object System.Drawing.Point 88, 114), (New-Object System.Drawing.Point 88, 96)))
    $g.FillEllipse($wb, 76, 72, 24, 24)
    $g.DrawLines($pen, @((New-Object System.Drawing.Point 128, 122), (New-Object System.Drawing.Point 168, 96), (New-Object System.Drawing.Point 168, 84)))
    $g.FillRectangle($wb, 157, 63, 22, 22)
    $g.Dispose(); $bmp
}

$f = New-Object System.Windows.Forms.Form
$f.Text = 'USB Shield'; $f.ClientSize = '800,600'; $f.StartPosition = 'CenterScreen'
$f.Font = $fReg; $f.BackColor = (Col $C.Bg)
$f.Icon = [System.Drawing.Icon]::FromHandle((LogoBmp 48).GetHicon())
$f.FormBorderStyle = 'FixedSingle'; $f.MaximizeBox = $false

# --- sidebar ---
$side = New-Object System.Windows.Forms.Panel; $side.Dock = 'Left'; $side.Width = 190; $side.BackColor = (Col $C.Side)
$st = Lbl 'USB Shield' 66 29 120 32 $false; $st.ForeColor = [System.Drawing.Color]::White; $st.Font = $fTitle
$logoPic = New-Object System.Windows.Forms.PictureBox; $logoPic.Location = '20,22'; $logoPic.Size = '40,40'; $logoPic.SizeMode = 'Zoom'; $logoPic.BackColor = 'Transparent'; $logoPic.Image = LogoBmp 96
$ss = Lbl 'Pulihkan flashdisk, buang virus' 20 74 160 36 $false; $ss.ForeColor = (Col '#94A3B8'); $ss.Font = $fSmall
function Nav($t, $y, $glyph) {
    $b = New-Object NavButton
    $b.Text = $t; $b.Location = "10,$y"; $b.Size = '170,42'; $b.Font = $fReg; $b.Glyph = [string][char]$glyph
    $b
}
$navs = @((Nav 'Pembersih' 140 0xE74D), (Nav 'Imbas Virus' 188 0xE721), (Nav 'Lajukan PC' 236 0xE945), (Nav 'Kuarantin' 284 0xE72E), (Nav 'Lanjutan' 332 0xE713))
$side.Controls.AddRange(@($logoPic, $st, $ss) + $navs)

# --- kandungan ---
$main = New-Object System.Windows.Forms.Panel; $main.Dock = 'Fill'; $main.BackColor = (Col $C.Bg)

$top = Box 20 6 570 56
$cb = New-Object System.Windows.Forms.ComboBox; $cb.DropDownStyle = 'DropDownList'; $cb.Location = '14,26'; $cb.Size = '420,26'; $cb.Font = $fReg
$cb.BackColor = [System.Drawing.Color]::White
$bRef = Btn 'Muat semula' 446 14 110 32 'secondary'
$top.Controls.AddRange(@((Muted (Lbl 'Drive flashdisk:' 16 6 300 18 $false)), $cb, $bRef))

$logCard = Box 20 448 570 118; $logCard.BackColor = (Col '#0F172A'); $logCard.Border = (Col '#0F172A'); $logCard.Padding = '12,10,6,8'
$log = New-Object System.Windows.Forms.TextBox; $log.Multiline = $true; $log.ReadOnly = $true; $log.ScrollBars = 'Vertical'
$log.BorderStyle = 'None'; $log.Dock = 'Fill'; $log.BackColor = (Col '#0F172A'); $log.ForeColor = (Col '#CBD5E1')
$log.Font = New-Object System.Drawing.Font('Consolas', 9)
$logCard.Controls.Add($log)
$logLbl = Lbl 'Log' 20 418 300 22 $true
$bar = New-Object BusyBar; $bar.Location = '330,428'; $bar.Size = '150,4'
$bSave = Btn 'Simpan log' 500 414 90 28 'secondary'

# halaman 1: Pembersih
$p1 = Page
$hero = Box 20 0 570 92
$bAll = Btn 'Bersihkan Semua' 340 22 214 48 'primary'; $bAll.Font = $fBig
$hero.Controls.AddRange(@((Lbl 'Bersihkan flashdisk' 20 14 310 28 $false), (Muted (Lbl 'Defender, pulihkan, kuarantin, vaksin dan semak PC sekali gus (disyorkan).' 20 46 310 40 $false)), $bAll))
$hero.Controls[0].Font = $fTitle
$b1 = Btn 'Pulihkan fail tersembunyi' 20 100 281 38 'secondary'
$b2 = Btn 'Kuarantin .lnk && autorun' 309 100 281 38 'secondary'
$b3 = Btn 'Imbas fail mencurigakan' 20 142 281 38 'secondary'
$bV = Btn 'Vaksin flashdisk' 309 142 281 38 'secondary'
$listCard = ListCard 210 88; $list = $listCard.Controls[0]
$bDel = Btn 'Kuarantin yang ditanda' 20 304 220 34 'danger'
$p1.Controls.AddRange(@($hero, $b1, $b2, $b3, $bV, (Lbl 'Fail mencurigakan di root flashdisk (tanda yang BUKAN milik anda):' 20 186 570 22 $true), $listCard, $bDel))

# halaman 2: Imbas Virus
$p2 = Page
$c1 = InfoCard 0 'Microsoft Defender' 'Imbas drive yang dipilih dan rawat ancaman secara automatik. Kemas kini definisi dahulu (perlu internet).'
$b4 = Btn 'Imbas drive' 360 12 190 36 'violet'; $b6 = Btn 'Kemas kini definisi' 360 54 190 34 'secondary'
$c1.Controls.AddRange(@($b4, $b6))
$c2 = InfoCard 108 'Malwarebytes' 'Buka Malwarebytes (atau halaman muat turun rasmi). Pilih imbasan Custom dan drive flashdisk.'
$b7 = Btn 'Buka Malwarebytes' 360 30 190 40 'teal'; $c2.Controls.Add($b7)
$c3 = InfoCard 216 'ESET Online Scanner' 'Buka ESET Online Scanner (atau halaman muat turun rasmi). Pilih imbasan Custom dan drive flashdisk.'
$b8 = Btn 'Buka ESET Online Scanner' 360 30 190 40 'teal'; $c3.Controls.Add($b8)
$p2.Controls.AddRange(@($c1, $c2, $c3))

# halaman 3: Lajukan PC
$pS = Page
$s1 = InfoCard 0 'Fail sementara (Temp)' 'Padam fail sampah sementara untuk membebaskan ruang. Saiz ditunjuk dahulu dan awak sahkan sebelum dipadam.'
$bT = Btn 'Bersihkan fail sementara' 360 30 190 40 'primary'; $s1.Controls.Add($bT)
$s2 = InfoCard 108 'Recycle Bin' 'Kosongkan Recycle Bin secara kekal untuk membebaskan ruang cakera.'
$bR = Btn 'Kosongkan Recycle Bin' 360 30 190 40 'primary'; $s2.Controls.Add($bR)
$s3 = InfoCard 216 'Cakera dan program permulaan' 'Semak ruang cakera. Kurangkan program permulaan supaya PC lebih cepat dihidupkan.'
$bD = Btn 'Semak ruang cakera' 360 12 190 36 'info'; $bS = Btn 'Senarai program permulaan' 360 54 190 34 'secondary'
$s3.Controls.AddRange(@($bD, $bS))
$pS.Controls.AddRange(@($s1, $s2, $s3))

# halaman 4: Kuarantin
$pQ = Page
$listQCard = ListCard 26 190; $listQ = $listQCard.Controls[0]
$bQR = Btn 'Pulihkan yang ditanda' 20 226 180 34 'info'
$bQD = Btn 'Padam kekal yang ditanda' 210 226 200 34 'danger'
$bQO = Btn 'Buka folder kuarantin' 420 226 170 34 'secondary'
$qcap = Muted (Lbl 'Fail dinamakan semula .kuarantin supaya tak boleh dijalankan secara tak sengaja. Pulihkan hanya jika anda pasti fail itu selamat.' 20 270 570 44 $false)
$pQ.Controls.AddRange(@((Lbl 'Fail dalam kuarantin:' 20 0 570 22 $true), $listQCard, $bQR, $bQD, $bQO, $qcap))

# halaman 5: Lanjutan
$p3 = Page
$b5 = Btn 'Semak permulaan PC' 20 0 200 36 'violet'
$list2Card = ListCard 70 116; $list2 = $list2Card.Controls[0]
$bDel2 = Btn 'Buang entri ditanda' 20 194 200 34 'danger'
$bAP1 = Btn 'Matikan AutoPlay/AutoRun' 20 274 280 36 'info'
$bAP2 = Btn 'Pulihkan tetapan asal' 310 274 280 36 'secondary'
$p3.Controls.AddRange(@($b5, (Lbl 'Program permulaan PC yang mencurigakan (tanda untuk buang):' 20 44 570 22 $true), $list2Card, $bDel2,
    (Lbl 'Keselamatan USB (tetapan akaun semasa)' 20 246 570 22 $true), $bAP1, $bAP2))

$pages = @($p1, $p2, $pS, $pQ, $p3)
function ShowPage($i) {
    if ($i -eq 3) { QLoad }
    for ($k = 0; $k -lt 5; $k++) {
        $pages[$k].Visible = ($k -eq $i)
        $navs[$k].Active = ($k -eq $i); $navs[$k].Invalidate()
    }
}
$navs[0].Add_Click({ ShowPage 0 }); $navs[1].Add_Click({ ShowPage 1 }); $navs[2].Add_Click({ ShowPage 2 })
$navs[3].Add_Click({ ShowPage 3 }); $navs[4].Add_Click({ ShowPage 4 })

$main.Controls.AddRange(@($top, $p1, $p2, $pS, $pQ, $p3, $logLbl, $bar, $bSave, $logCard))

$tip = New-Object System.Windows.Forms.ToolTip
function Tips {
    $tip.SetToolTip($bV, (T 'Halang virus menulis autorun.inf (folder palsu read-only).'))
    $tip.SetToolTip($bAP1, (T 'Matikan AutoPlay/AutoRun'))
}

# tindakan dikongsi oleh butang & menu
$aAll = { $r = Root; if ($r) { Work 'pembersihan penuh (Defender mungkin ambil masa)' { $dr = @(Defender $r); Unhide $r; Lnk $r; Scan $r; if ($dr -contains 'none') { Clam $r }; if (IsRemovable $r) { Vaccinate $r }; Startup } } }
$a1 = { $r = Root; if ($r) { Work 'memulihkan fail tersembunyi' { Unhide $r } } }
$a2 = { $r = Root; if ($r) { Work 'mengkuarantin .lnk dan autorun' { Lnk $r } } }
$a3 = { $r = Root; if ($r) { Work 'mengimbas fail mencurigakan' { Scan $r } } }
$a4 = { $r = Root; if ($r) { Work 'imbasan Defender' { Defender $r } } }
$aClam = { $r = Root; if ($r) { Work 'imbasan ClamAV' { Clam $r } } }
$a5 = { Work 'menyemak permulaan PC' { Startup } }
$a6 = { Work 'mengemas kini definisi Defender' { UpdateDef } }
$aT = { Work 'fail sementara' { TempClean } }
$aV = { $r = Root; if ($r) { Work 'memasang vaksin' { Vaccinate $r } } }
$a7 = { Ext 'Malwarebytes' @("$env:ProgramFiles\Malwarebytes\Anti-Malware\mbam.exe") 'https://www.malwarebytes.com/mwb-download' }
$a8 = { Ext 'ESET Online Scanner' @("$here\esetonlinescanner.exe", "$env:USERPROFILE\Downloads\esetonlinescanner.exe") 'https://www.eset.com/int/home/online-scanner/' }
$aRef = { LoadDrives }
$aOpenQ = { if (-not (Test-Path -LiteralPath $qDir)) { [void](New-Item $qDir -ItemType Directory) }; Start-Process $qDir }
$bAll.Add_Click($aAll); $b1.Add_Click($a1); $b2.Add_Click($a2); $b3.Add_Click($a3); $bV.Add_Click($aV)
$b4.Add_Click($a4); $b5.Add_Click($a5); $b6.Add_Click($a6); $b7.Add_Click($a7); $b8.Add_Click($a8); $bRef.Add_Click($aRef)
$bT.Add_Click($aT); $bR.Add_Click({ RecycleClean }); $bD.Add_Click({ DiskInfo })
$bS.Add_Click({ Startup $true; ShowPage 4 })
$bAP1.Add_Click({ AutoOff }); $bAP2.Add_Click({ AutoOn }); $bSave.Add_Click({ SaveLog }); $bQO.Add_Click($aOpenQ)
$bDel.Add_Click({ DoChecked $list { param($p) Quar $p } 'Alihkan fail ini ke kuarantin? Anda boleh memulihkannya kemudian.' })
$bDel2.Add_Click({ DoChecked $list2 { param($i) $s = $i.Split('|', 3); Remove-ItemProperty -Path $s[0] -Name $s[1] -ErrorAction Stop } 'Padam item ini secara kekal?' })
$bQR.Add_Click({ DoChecked $listQ { param($i) QRestore $i } 'Pulihkan fail ini ke lokasi asal? Jika ia virus, ia akan aktif semula.'; QLoad })
$bQD.Add_Click({ DoChecked $listQ { param($i) QDelete $i } 'Padam kekal fail kuarantin ini? Ia tak boleh dipulihkan.'; QLoad })

$ms = New-Object System.Windows.Forms.MenuStrip
$ms.RenderMode = 'System'; $ms.BackColor = [System.Drawing.Color]::White; $ms.Font = $fReg
function Menu($name, $items) {
    $m = New-Object System.Windows.Forms.ToolStripMenuItem($name)
    foreach ($it in $items) { $i = $m.DropDownItems.Add($it[0]); $i.Add_Click($it[1]) }
    [void]$ms.Items.Add($m)
}
function TrCtl($ctl) {
    foreach ($c in $ctl.Controls) {
        if ($c -is [System.Windows.Forms.Button] -or $c -is [System.Windows.Forms.Label]) {
            if ($c.Tag -eq $null) { $c.Tag = $c.Text }
            $c.Text = T $c.Tag
        }
        TrCtl $c
    }
}
function TrMenu($items) {
    foreach ($i in $items) {
        if ($i.Tag -eq $null) { $i.Tag = $i.Text }
        $i.Text = T $i.Tag
        if ($i.DropDownItems.Count) { TrMenu $i.DropDownItems }
    }
}
function SetLang($l) {
    $script:lang = $l
    $f.Text = T 'USB Shield'
    TrCtl $f; TrMenu $ms.Items; Tips
}
Menu 'Fail' @(@('Muat semula drive', $aRef), @('Simpan log ke fail...', { SaveLog }), @('Keluar', { $f.Close() }))
Menu 'Alat' @(
    @('Bersihkan Semua', $aAll), @('Pulihkan fail tersembunyi', $a1), @('Kuarantin .lnk && autorun', $a2),
    @('Imbas fail mencurigakan', $a3), @('Vaksin flashdisk', $aV), @('Imbas virus (Defender)', $a4), @('Imbas dengan ClamAV (luar talian)', $aClam), @('Semak permulaan PC', $a5),
    @('Kemas kini Defender', $a6), @('Imbas dengan Malwarebytes', $a7), @('Imbas dengan ESET Online Scanner', $a8),
    @('Bersihkan fail sementara', $aT), @('Kosongkan Recycle Bin', { RecycleClean }), @('Semak ruang cakera', { DiskInfo }),
    @('Matikan AutoPlay/AutoRun', { AutoOff }))
Menu 'Bahasa' @(@('Bahasa Melayu', { SetLang 'ms' }), @('English', { SetLang 'en' }))
Menu 'Bantuan' @(
    @('Panduan', { [void](Msg $guideMs 'Panduan') }),
    @('Tentang', { [void](Msg $aboutMs 'Tentang') }))

$f.Controls.AddRange(@($main, $side, $ms)); $f.MainMenuStrip = $ms
ShowPage 0
Tips
LoadDrives
[void]$f.ShowDialog()
