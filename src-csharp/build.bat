@echo off
set CSC="C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist %CSC% (
    set CSC="C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe"
)
if not exist %CSC% (
    echo Could not find csc.exe.
    exit /b 1
)

echo Compiling C# Application...
%CSC% /target:winexe /out:AnToanUSB.exe /win32icon:app.ico /win32manifest:app.manifest /nologo /optimize+ /reference:System.Windows.Forms.dll,System.Drawing.dll,System.Management.dll Program.cs AppBrand.cs SplashForm.cs LoginForm.cs MainForm.cs SettingsForm.cs LanguageManager.cs CryptoEngine.cs UsbHelper.cs AntivirusScanner.cs UsbYaraRuleScanner.cs ClamAvManager.cs QuarantineManager.cs TrustedFileManager.cs TrustedFilesForm.cs RecoverySnapshotManager.cs ThreatAlertDialog.cs ConfigManager.cs IconExtractor.cs CustomIcons.cs HistoryForm.cs AntivirusForm.cs SecurityCenterForm.cs PrivacyCleanupForm.cs UsbRescueForm.cs ActionChoiceDialog.cs TextNoteEditorForm.cs Theme.cs
if %errorlevel% neq 0 (
    echo Compilation Failed!
    exit /b %errorlevel%
)
echo Compilation Succeeded! Created AnToanUSB.exe
