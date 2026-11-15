# USB Shield

A Windows tool that removes shortcut viruses from USB flash drives and cleans and tunes the PC. It is a native C# (.NET 3.5) WinForms app with no PowerShell at runtime. The interface is available in Malay and English.

## Features

- **USB cleaning**: restores hidden files, quarantines `.lnk` and `autorun.inf` files, flags suspicious programs, and installs an `autorun.inf` "vaccine" folder to block re-infection.
- **Auto-check on plug-in**: a quick, read-only check of the drive root, with an optional full antivirus scan (Windows Defender, or ClamAV when Defender is unavailable).
- **Health Check**: space and junk, startup programs, security, disk health, and program updates in one click, with a savable report.
- **Custom Clean**: categorized junk and browser-cache cleaner (analyze first, then clean).
- **Tools**: uninstaller, startup manager, disk analyzer, duplicate file finder, drive wiper (free space), and PC information (specs and disk SMART health).
- **Software Updater**: checks and updates installed programs through `winget` (Windows 10/11).
- **Performance**: top RAM-using programs, power plan, and visual effects (all reversible).

Nothing is deleted without confirmation. Quarantined files can be restored.

## Download

Get the latest `USB Shield.exe` from the [Releases](https://github.com/SkySkew/USB-Shield/releases) page and run it. No installation is needed.

The exe is not code-signed, so Windows SmartScreen may warn on first run. Choose **More info > Run anyway**.

## Build from source

Requires .NET Framework 3.5 (already present in Windows). Double-click `build-exe.bat`, which produces `USB Shield.exe`.

## ClamAV (optional, offline)

For PCs without Windows Defender or without internet, place a `ClamAV` folder next to the exe. It should contain `clamscan.exe`, its DLLs, and a `database` folder. The folder is not part of this repository because of its size. See the notes inside that folder for details.

## Update check and privacy

The app can check this repository's latest GitHub release once a day. This is a plain HTTPS request to the public GitHub API. It only reads the version number and opens the download page when a newer version exists. Nothing is downloaded or installed automatically, and no user data is sent. You can turn it off in **Options**.

## Source layout

| File | Contents |
|---|---|
| `UsbShield.cs` | entry point, state, translation, logging |
| `Drive.cs` | flash drives, quarantine, Defender, startup programs |
| `Tuning.cs` | performance, cleaner, duplicate finder, drive wiper |
| `Info.cs` | PC information, programs and updates (winget) |
| `Scan.cs` | history, ClamAV, actions |
| `Ui.cs` | layout, sidebar, Health Check |
| `AppUpdate.cs` | GitHub release version check |
| `Controls.cs` | custom controls |
| `Strings.cs` | English translations |
