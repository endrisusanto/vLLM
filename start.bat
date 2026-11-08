@echo off
setlocal
title vLLM Server Runner

echo ==========================================================
echo  vLLM Turnkey Private LLM - Start Server
echo ==========================================================
echo.

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\start.ps1"

if %errorLevel% neq 0 (
    echo.
    echo [ERROR] Terjadi kesalahan saat menjalankan start.ps1.
)

echo.
pause

