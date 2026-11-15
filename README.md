# USB Shield

Aplikasi Windows (C#, .NET 3.5, tanpa PowerShell semasa berjalan) untuk membuang virus pintasan daripada flashdisk, serta membersihkan dan mengoptimumkan PC.

## Bina

Perlu .NET Framework 3.5 (sedia ada dalam Windows). Klik dua kali `build-exe.bat` untuk menghasilkan `USB Shield.exe`.

## ClamAV (pilihan, luar talian)

Letak folder `ClamAV` (clamscan.exe, DLL dan folder `database`) di sebelah exe. Lihat panduan dalam folder itu. Folder ini tidak dimasukkan dalam repo kerana besar.

## Struktur kod

| Fail | Isi |
|---|---|
| `UsbShield.cs` | titik masuk, keadaan, terjemahan, log |
| `Drive.cs` | flashdisk, kuarantin, Defender, permulaan PC |
| `Tuning.cs` | prestasi, pembersih, fail pendua, padam selamat |
| `Info.cs` | maklumat PC, program dan kemas kini (winget) |
| `Scan.cs` | sejarah, ClamAV, tindakan |
| `Ui.cs` | paparan, menu kiri, Semak Kesihatan |
| `Controls.cs` | kawalan tersuai |
| `Strings.cs` | terjemahan English |
