<#
.SYNOPSIS
    Script Peluncur vLLM Private LLM Turnkey Server
.DESCRIPTION
    Menjalankan pemeriksaan model, mendeteksi IP LAN host, menjalankan container
    via docker compose, melakukan polling healthcheck hingga model siap di VRAM,
    dan menampilkan dashboard koneksi untuk localhost dan rekan kerja di LAN.
#>

[CmdletBinding()]
param()

$rootPath = Resolve-Path "$PSScriptRoot\.."
Set-Location $rootPath

Clear-Host
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "      vLLM Private LLM API - Starting Server...          " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# ------------------------------------------------------------------------------
# 1. Pastikan file .env ada
# ------------------------------------------------------------------------------
$envPath = Join-Path $rootPath ".env"
$envExamplePath = Join-Path $rootPath ".env.example"
if (-not (Test-Path $envPath)) {
    if (Test-Path $envExamplePath) {
        Copy-Item -Path $envExamplePath -Destination $envPath
        Write-Host "[INFO] File .env diinisialisasi dari .env.example." -ForegroundColor DarkGray
    } else {
        Write-Host "[ERROR] File .env.example tidak ditemukan!" -ForegroundColor Red
        exit 1
    }
}

# ------------------------------------------------------------------------------
# 2. Deteksi & Pemilihan Model di ./models/
# ------------------------------------------------------------------------------
Write-Host "`n[1/4] Memeriksa ketersediaan bobot model..." -ForegroundColor Yellow
& "$PSScriptRoot\detect-model.ps1"
if ($LASTEXITCODE -ne 0 -or -not $?) {
    Write-Host "`n[INFO] Peluncuran dibatalkan karena belum ada model yang dipilih." -ForegroundColor Yellow
    exit 1
}

# ------------------------------------------------------------------------------
# 3. Baca Variabel dari .env yang Aktif
# ------------------------------------------------------------------------------
$envConfig = @{}
Get-Content $envPath | Where-Object { $_ -match '^\s*([A-Za-z_0-9]+)\s*=\s*(.*)$' } | ForEach-Object {
    $key = $matches[1].Trim()
    $val = $matches[2].Trim()
    $envConfig[$key] = $val
}

$hostPort = if ($envConfig["HOST_PORT"]) { [int]$envConfig["HOST_PORT"] } else { 8000 }
$modelName = $envConfig["MODEL_NAME"]
$apiKey = $envConfig["API_KEY"]
$quantization = $envConfig["QUANTIZATION"]

# ------------------------------------------------------------------------------
# 4. Deteksi Alamat IP Host (LAN Intranet)
# ------------------------------------------------------------------------------
Write-Host "`n[2/4] Mendeteksi Alamat IP Host Jaringan LAN..." -ForegroundColor Yellow
$lanIps = Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue | Where-Object {
    $_.InterfaceAlias -notmatch '(?i)vEthernet|Loopback|Docker|WSL|Tailscale|ZeroTier' -and
    $_.IPAddress -notmatch '^(127\.|169\.254\.)'
} | Select-Object -ExpandProperty IPAddress -Unique

$primaryLanIp = if ($lanIps.Count -gt 0) { $lanIps[0] } else { "127.0.0.1" }
Write-Host "  -> IP LAN Host Utama : $primaryLanIp" -ForegroundColor Green
if ($lanIps.Count -gt 1) {
    Write-Host "  -> IP Tambahan Lain  : $($lanIps[1..($lanIps.Count - 1)] -join ', ')" -ForegroundColor DarkGray
}

# ------------------------------------------------------------------------------
# 5. Jalankan Container via Docker Compose
# ------------------------------------------------------------------------------
Write-Host "`n[3/4] Meluncurkan Container vLLM via Docker Compose..." -ForegroundColor Yellow

$composeCmd = $null
if (Get-Command docker -ErrorAction SilentlyContinue) {
    $composeCmd = "docker compose"
} elseif (Get-Command podman -ErrorAction SilentlyContinue) {
    $composeCmd = "podman compose"
}

if (-not $composeCmd) {
    Write-Host "[ERROR] Tidak ditemukan Docker atau Podman pada sistem!" -ForegroundColor Red
    exit 1
}

Write-Host "Eksekusi: $composeCmd up -d" -ForegroundColor DarkGray
Invoke-Expression "$composeCmd up -d"

if ($LASTEXITCODE -ne 0) {
    Write-Host "`n[ERROR] Gagal menjalankan container docker compose!" -ForegroundColor Red
    exit 1
}

# ------------------------------------------------------------------------------
# 6. Polling Kesiapan (Readiness Health Check)
# ------------------------------------------------------------------------------
Write-Host "`n[4/4] Memeriksa status readiness API..." -ForegroundColor Yellow
& "$PSScriptRoot\healthcheck.ps1" -Port $hostPort -ApiKey $apiKey -TimeoutSeconds 240

if ($LASTEXITCODE -ne 0 -or -not $?) {
    Write-Host "`n[PERINGATAN] Server belum siap atau mengalami kegagalan." -ForegroundColor Yellow
    exit 1
}

# ------------------------------------------------------------------------------
# 7. Dashboard Informasi Endpoint & Panduan Rekan Kerja
# ------------------------------------------------------------------------------
Write-Host "`n"
Write-Host "==========================================================================" -ForegroundColor Green
Write-Host "              vLLM PRIVATE LLM SERVER IS READY & ONLINE!                  " -ForegroundColor Green
Write-Host "==========================================================================" -ForegroundColor Green
Write-Host " Model Aktif     : " -NoNewline -ForegroundColor White
Write-Host "$modelName" -ForegroundColor Cyan
Write-Host " Kuantisasi      : " -NoNewline -ForegroundColor White
Write-Host "$(if ($quantization) { $quantization.ToUpper() } else { 'FP16/BF16 (Auto)' })" -ForegroundColor Cyan
Write-Host " Port Layanan    : " -NoNewline -ForegroundColor White
Write-Host "$hostPort" -ForegroundColor Cyan
Write-Host " Proteksi API Key: " -NoNewline -ForegroundColor White
Write-Host "$(if ($apiKey) { $apiKey } else { '(Tanpa API Key - Public Akses LAN)' })" -ForegroundColor Yellow

Write-Host "`n---------------------------- URL ENDPOINTS -------------------------------" -ForegroundColor White
Write-Host " [Akses PC Lokal]    : " -NoNewline -ForegroundColor White
Write-Host "http://localhost:$hostPort/v1" -ForegroundColor Green
Write-Host " [Akses Rekan LAN]   : " -NoNewline -ForegroundColor White
Write-Host "http://${primaryLanIp}:$hostPort/v1" -ForegroundColor Green
Write-Host "--------------------------------------------------------------------------" -ForegroundColor White

Write-Host "`n[Contoh Pengujian Cepat cURL dari Terminal PC Rekan Kerja]:" -ForegroundColor Yellow
$authHeader = if ($apiKey) { "-H ""Authorization: Bearer $apiKey"" " } else { "" }
Write-Host "curl http://${primaryLanIp}:$hostPort/v1/models $authHeader" -ForegroundColor DarkGray

Write-Host "`n[Integrasi Python OpenAI Client]:" -ForegroundColor Yellow
Write-Host "from openai import OpenAI" -ForegroundColor DarkGray
Write-Host "client = OpenAI(base_url='http://${primaryLanIp}:$hostPort/v1', api_key='$apiKey')" -ForegroundColor DarkGray
Write-Host "response = client.chat.completions.create(" -ForegroundColor DarkGray
Write-Host "    model='$modelName'," -ForegroundColor DarkGray
Write-Host "    messages=[{'role': 'user', 'content': 'Halo dari LAN!'}]" -ForegroundColor DarkGray
Write-Host ")" -ForegroundColor DarkGray

Write-Host "`n[Integrasi Chatbox / Open WebUI / LibreChat / VS Code Cline]:" -ForegroundColor Yellow
Write-Host "  - API Provider : OpenAI Compatible" -ForegroundColor White
Write-Host "  - Base URL     : http://${primaryLanIp}:$hostPort/v1" -ForegroundColor White
Write-Host "  - API Key      : $apiKey" -ForegroundColor White
Write-Host "  - Model Name   : $modelName" -ForegroundColor White

Write-Host "`n==========================================================================" -ForegroundColor Green
Write-Host " Untuk menghentikan server secara bersih, jalankan file 'stop.bat'." -ForegroundColor White
Write-Host "==========================================================================`n" -ForegroundColor Green
