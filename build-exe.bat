@echo off
rem Bina "USB Shield.exe" (aplikasi .NET asli, tanpa PowerShell) guna kompiler .NET yang sedia ada dalam Windows
cd /d "%~dp0"
"%WINDIR%\Microsoft.NET\Framework\v3.5\csc.exe" /nologo /target:winexe /platform:anycpu /optimize+ /win32icon:app.ico ^
  /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Management.dll /r:Microsoft.VisualBasic.dll /resource:app.ico,app.ico /out:"USB Shield.exe" UsbShield.cs Drive.cs Tuning.cs Info.cs Scan.cs Ui.cs Controls.cs Strings.cs
if errorlevel 1 (echo GAGAL membina.) else (echo Siap: USB Shield.exe)
pause
