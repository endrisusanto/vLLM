@echo off
setlocal
title vLLM Turnkey Setup

echo ==========================================================
echo  vLLM Turnkey Private LLM - Host Setup Launcher
echo ==========================================================
echo.

:: Memeriksa hak administrator
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo [INFO] Memerlukan hak akses Administrator untuk konfigurasi jaringan...
    powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process powershell -ArgumentList '-NoProfile -ExecutionPolicy Bypass -File ""%~dp0scripts\setup.ps1""' -Verb RunAs"
    exit /b 0
)

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\setup.ps1"

echo.
pause

