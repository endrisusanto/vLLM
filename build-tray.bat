@echo off
setlocal
echo ==========================================================
echo  Building vLLM-Tray.exe using Native Windows C# Compiler
echo ==========================================================

set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe

if not exist "%CSC%" (
    echo [ERROR] csc.exe tidak ditemukan di %CSC%
    pause
    exit /b 1
)

set ICON_FLAG=
if exist "%~dp0icon.ico" (
    set ICON_FLAG=/win32icon:"%~dp0icon.ico"
)

echo Compiling src\TrayApp.cs -^> vLLM-Tray.exe...
"%CSC%" /nologo /target:winexe /optimize+ %ICON_FLAG% /out:"%~dp0vLLM-Tray.exe" /reference:System.Windows.Forms.dll,System.Drawing.dll,System.dll "%~dp0src\TrayApp.cs"

if %errorLevel% equ 0 (
    echo [SUCCESS] vLLM-Tray.exe berhasil dibuat dengan app icon!
) else (
    echo [ERROR] Kompilasi gagal.
)
