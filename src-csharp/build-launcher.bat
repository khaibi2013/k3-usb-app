@echo off
set CSC="C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist %CSC% (
    set CSC="C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe"
)
if not exist %CSC% (
    echo Could not find csc.exe.
    exit /b 1
)

echo Compiling K3 AutoLauncher...
%CSC% /target:winexe /out:K3AutoLauncher.exe /win32icon:app.ico /nologo /optimize+ /reference:System.Windows.Forms.dll,System.Drawing.dll K3AutoLauncher.cs
if %errorlevel% neq 0 (
    echo Compilation Failed!
    exit /b %errorlevel%
)
echo Compilation Succeeded! Created K3AutoLauncher.exe
