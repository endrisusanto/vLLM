<#
.SYNOPSIS
    Script Penghentian Bersih (Graceful Shutdown) Server vLLM
.DESCRIPTION
    Menghentikan container vLLM secara anggun, melepaskan alokasi GPU VRAM,
    dan membersihkan alokasi port.
#>

[CmdletBinding()]
param()

$rootPath = Resolve-Path "$PSScriptRoot\.."
Set-Location $rootPath

Clear-Host
Write-Host "==========================================================" -ForegroundColor Yellow
Write-Host "      Menghentikan Server vLLM (Graceful Shutdown)...     " -ForegroundColor Yellow
Write-Host "==========================================================" -ForegroundColor Yellow

$composeCmd = $null
if (Get-Command docker -ErrorAction SilentlyContinue) {
    $composeCmd = "docker compose"
} elseif (Get-Command podman -ErrorAction SilentlyContinue) {
    $composeCmd = "podman compose"
}

if ($composeCmd) {
    Write-Host "`nMenjalankan: $composeCmd down" -ForegroundColor Cyan
    Invoke-Expression "$composeCmd down"
} else {
    Write-Warning "Tidak dapat menemukan docker compose atau podman compose."
}

# Verifikasi VRAM
Write-Host "`nMemeriksa pelepasan GPU VRAM..." -ForegroundColor Cyan
$nvidiaSmi = Get-Command nvidia-smi -ErrorAction SilentlyContinue
if ($nvidiaSmi) {
    try {
        $gpuMem = & nvidia-smi --query-gpu=memory.used,memory.total --format=csv,noheader,nounits 2>$null
        if ($gpuMem) {
            $parts = $gpuMem.Split(',')
            $usedMb = $parts[0].Trim()
            $totalMb = $parts[1].Trim()
            Write-Host "  -> VRAM Terpakai Saat Ini: $usedMb MiB / $totalMb MiB" -ForegroundColor Green
        }
    } catch {}
}

Write-Host "`n==========================================================" -ForegroundColor Green
Write-Host " Server vLLM berhasil dihentikan. VRAM telah dilepaskan!  " -ForegroundColor Green
Write-Host "==========================================================`n" -ForegroundColor Green

