<#
.SYNOPSIS
    Script Setup & Dependensi Host untuk vLLM Turnkey API
.DESCRIPTION
    Memeriksa NVIDIA Driver, WSL2, Docker/Podman runtime, menyiapkan folder ./models,
    menyiapkan konfigurasi .env, dan memanggil configure-host.ps1 untuk konfigurasi jaringan.
#>

[CmdletBinding()]
param(
    [switch]$NoWslRestart
)

Clear-Host
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "   vLLM Private LLM Turnkey Setup & Dependency Check     " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# ------------------------------------------------------------------------------
# 1. Cek NVIDIA Driver & GPU VRAM
# ------------------------------------------------------------------------------
Write-Host "`n[1/5] Memeriksa NVIDIA Driver & GPU Hardware..." -ForegroundColor Yellow
$nvidiaSmi = Get-Command nvidia-smi -ErrorAction SilentlyContinue
if ($nvidiaSmi) {
    try {
        $gpuInfo = & nvidia-smi --query-gpu=name,memory.total,driver_version --format=csv,noheader,nounits 2>$null
        if ($gpuInfo) {
            $parts = $gpuInfo.Split(',')
            $gpuName = $parts[0].Trim()
            $vramMb = [int]$parts[1].Trim()
            $driverVer = $parts[2].Trim()
            $vramGb = [math]::Round($vramMb / 1024, 1)

            Write-Host "  [OK] GPU Terdeteksi : $gpuName" -ForegroundColor Green
            Write-Host "  [OK] Total VRAM     : $vramMb MiB (~$vramGb GB)" -ForegroundColor Green
            Write-Host "  [OK] Driver Version : $driverVer" -ForegroundColor Green
            
            if ($vramGb -lt 15) {
                Write-Warning "VRAM Anda kurang dari 16GB ($vramGb GB). Pertimbangkan menggunakan model AWQ 7B/8B dengan max-model-len 4096 untuk mencegah OOM."
            }
        }
    } catch {
        Write-Warning "Gagal mengambil data detail GPU dari nvidia-smi."
    }
} else {
    Write-Host "  [ERROR] nvidia-smi tidak ditemukan!" -ForegroundColor Red
    Write-Host "  Pastikan NVIDIA Driver terbaru telah terinstal di Windows Host." -ForegroundColor Yellow
}

# ------------------------------------------------------------------------------
# 2. Cek WSL2 Status
# ------------------------------------------------------------------------------
Write-Host "`n[2/5] Memeriksa Subsystem WSL2..." -ForegroundColor Yellow
$wslCmd = Get-Command wsl.exe -ErrorAction SilentlyContinue
if ($wslCmd) {
    try {
        $wslDistros = & wsl.exe -l -v 2>$null
        Write-Host "  [OK] WSL2 Engine terdeteksi aktif." -ForegroundColor Green
    } catch {
        Write-Warning "Tidak dapat menjalankan wsl.exe."
    }
} else {
    Write-Host "  [ERROR] WSL tidak terdeteksi di Windows ini!" -ForegroundColor Red
    Write-Host "  Jalankan 'wsl --install' di terminal Administrator untuk mengaktifkannya." -ForegroundColor Yellow
}

# ------------------------------------------------------------------------------
# 3. Cek Container Runtime (Docker / Podman)
# ------------------------------------------------------------------------------
Write-Host "`n[3/5] Memeriksa Container Engine & Compose..." -ForegroundColor Yellow
$dockerCmd = Get-Command docker -ErrorAction SilentlyContinue
$podmanCmd = Get-Command podman -ErrorAction SilentlyContinue

if ($dockerCmd) {
    $dockerVer = & docker --version 2>$null
    Write-Host "  [OK] Container CLI : $dockerVer" -ForegroundColor Green
} elseif ($podmanCmd) {
    $podmanVer = & podman --version 2>$null
    Write-Host "  [OK] Container CLI : $podmanVer" -ForegroundColor Green
} else {
    Write-Host "  [ERROR] Docker Desktop atau Podman tidak ditemukan di PATH!" -ForegroundColor Red
    Write-Host "  Silakan instal Docker Desktop dengan WSL2 backend atau Podman Desktop." -ForegroundColor Yellow
}

# Cek Compose
$composeWorks = $false
try {
    $composeTest = & docker compose version 2>$null
    if ($composeTest) {
        Write-Host "  [OK] Compose CLI   : $composeTest" -ForegroundColor Green
        $composeWorks = $true
    }
} catch {}

if (-not $composeWorks) {
    Write-Warning "Docker Compose v2 tidak terdeteksi via 'docker compose'. Memeriksa docker-compose..."
    $legacyCompose = Get-Command docker-compose -ErrorAction SilentlyContinue
    if ($legacyCompose) {
        Write-Host "  [OK] Legacy Compose: $(& docker-compose --version)" -ForegroundColor Green
    }
}

# ------------------------------------------------------------------------------
# 4. Inisialisasi Direktori Model & File .env
# ------------------------------------------------------------------------------
Write-Host "`n[4/5] Memeriksa Struktur File Proyek..." -ForegroundColor Yellow
$modelsPath = "$PSScriptRoot\..\models"
if (-not (Test-Path $modelsPath)) {
    New-Item -ItemType Directory -Path $modelsPath -Force | Out-Null
    Write-Host "  [OK] Direktori ./models berhasil dibuat." -ForegroundColor Green
} else {
    Write-Host "  [OK] Direktori ./models sudah ada." -ForegroundColor Green
}

$envPath = "$PSScriptRoot\..\.env"
$envExamplePath = "$PSScriptRoot\..\.env.example"
if (-not (Test-Path $envPath)) {
    if (Test-Path $envExamplePath) {
        Copy-Item -Path $envExamplePath -Destination $envPath
        Write-Host "  [OK] File konfigurasi .env dibuat dari .env.example." -ForegroundColor Green
    }
} else {
    Write-Host "  [OK] File konfigurasi .env sudah ada." -ForegroundColor Green
}

# ------------------------------------------------------------------------------
# 5. Konfigurasi Host (.wslconfig Mirrored Mode & Firewall)
# ------------------------------------------------------------------------------
Write-Host "`n[5/5] Menjalankan Konfigurasi Jaringan & Host..." -ForegroundColor Yellow
$configureScript = "$PSScriptRoot\configure-host.ps1"
if (Test-Path $configureScript) {
    if ($NoWslRestart) {
        & $configureScript -NoWslRestart
    } else {
        & $configureScript
    }
}

Write-Host "`n==========================================================" -ForegroundColor Green
Write-Host " SETUP LINGKUNGAN SELESAI & SIAP DIGUNAKAN!              " -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green
Write-Host "Langkah Selanjutnya:" -ForegroundColor White
Write-Host "1. Letakkan folder model (Safetensors / AWQ / GPTQ) ke dalam:" -ForegroundColor White
Write-Host "   -> ./models/<nama-model>/" -ForegroundColor Cyan
Write-Host "2. Klik dua kali file 'start.bat' untuk meluncurkan API!" -ForegroundColor Green
Write-Host "==========================================================`n" -ForegroundColor Green
