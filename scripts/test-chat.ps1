<#
.SYNOPSIS
    Uji Koneksi Cepat & Inference Chat Completions API vLLM
.DESCRIPTION
    Script ini menguji endpoint OpenAI-compatible /v1/models dan /v1/chat/completions.
    Dapat dijalankan dari localhost maupun dari PC rekan kantor di jaringan LAN.
.EXAMPLE
    .\test-chat.ps1
    .\test-chat.ps1 -HostAddress 192.168.1.50 -ApiKey "sk-local-vllm-secret-key"
#>

[CmdletBinding()]
param (
    [string]$HostAddress = "127.0.0.1",
    [int]$Port = 8000,
    [string]$ApiKey = "",
    [string]$Model = "",
    [string]$Prompt = "Halo! Jelaskan dalam 2 kalimat mengapa arsitektur PagedAttention pada vLLM sangat efisien untuk throughput tinggi.",
    [int]$MaxTokens = 256,
    [double]$Temperature = 0.7
)

# Jika ApiKey kosong, coba baca dari .env lokal jika ada
if (-not $ApiKey) {
    $envPath = "$PSScriptRoot\..\.env"
    if (Test-Path $envPath) {
        $envLines = Get-Content $envPath
        foreach ($line in $envLines) {
            if ($line -match '^API_KEY\s*=\s*(.*)$') {
                $ApiKey = $matches[1].Trim()
                break
            }
        }
    }
}

$baseUrl = "http://${HostAddress}:${Port}"
$headers = @{
    "Content-Type" = "application/json"
}
if ($ApiKey) {
    $headers["Authorization"] = "Bearer $ApiKey"
}

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " [vLLM Endpoint Health & Chat Completion Test]            " -ForegroundColor Cyan
Write-Host " Target URL: $baseUrl/v1                                  " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# ------------------------------------------------------------------------------
# 1. Test Endpoint /v1/models
# ------------------------------------------------------------------------------
Write-Host "`n[1/2] Memeriksa daftar model di endpoint /v1/models..." -ForegroundColor Yellow
$modelsUrl = "$baseUrl/v1/models"

try {
    $modelsResponse = Invoke-RestMethod -Uri $modelsUrl -Headers $headers -Method Get -TimeoutSec 10
    $modelList = $modelsResponse.data
    if ($modelList.Count -gt 0) {
        Write-Host "  [OK] Model ditemukan di server:" -ForegroundColor Green
        foreach ($m in $modelList) {
            Write-Host "    - $($m.id)" -ForegroundColor White
        }
        if (-not $Model) {
            $Model = $modelList[0].id
        }
    } else {
        Write-Warning "Daftar model kosong."
        if (-not $Model) { $Model = "default" }
    }
} catch {
    Write-Host "  [GAGAL] Tidak dapat menghubungi /v1/models: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "  Pastikan server vLLM aktif, port terbuka di firewall, dan API Key valid jika diproteksi." -ForegroundColor DarkGray
    exit 1
}

# ------------------------------------------------------------------------------
# 2. Test Endpoint /v1/chat/completions
# ------------------------------------------------------------------------------
Write-Host "`n[2/2] Mengirim Chat Completion request (Model: $Model)..." -ForegroundColor Yellow
Write-Host "Prompt: `"$Prompt`"" -ForegroundColor DarkGray

$chatUrl = "$baseUrl/v1/chat/completions"
$bodyObj = @{
    model = $Model
    messages = @(
        @{
            role = "user"
            content = $Prompt
        }
    )
    max_tokens = $MaxTokens
    temperature = $Temperature
    stream = $false
}
$jsonBody = $bodyObj | ConvertTo-Json -Depth 5

$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()

try {
    $chatResponse = Invoke-RestMethod -Uri $chatUrl -Headers $headers -Method Post -Body $jsonBody -TimeoutSec 60
    $stopwatch.Stop()
    $latencySec = [math]::Round($stopwatch.Elapsed.TotalSeconds, 2)

    $reply = $chatResponse.choices[0].message.content
    $promptTokens = $chatResponse.usage.prompt_tokens
    $compTokens = $chatResponse.usage.completion_tokens
    $totalTokens = $chatResponse.usage.total_tokens
    $tps = if ($latencySec -gt 0) { [math]::Round($compTokens / $latencySec, 1) } else { 0 }

    Write-Host "`n========================= RESPON MODEL =========================" -ForegroundColor Green
    Write-Host $reply -ForegroundColor White
    Write-Host "================================================================" -ForegroundColor Green

    Write-Host "`nStatistik Performa:" -ForegroundColor Cyan
    Write-Host "  Latency        : ${latencySec} detik" -ForegroundColor White
    Write-Host "  Prompt Tokens  : $promptTokens" -ForegroundColor White
    Write-Host "  Output Tokens  : $compTokens" -ForegroundColor White
    Write-Host "  Total Tokens   : $totalTokens" -ForegroundColor White
    Write-Host "  Throughput     : ~$tps tokens/detik" -ForegroundColor Yellow

    Write-Host "`n[BERHASIL] API vLLM berfungsi 100% normal dan siap digunakan!" -ForegroundColor Green

} catch {
    $stopwatch.Stop()
    Write-Host "`n[GAGAL] Request chat completion gagal: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.ErrorDetails.Message) {
        Write-Host "Detail Pesan Server: $($_.ErrorDetails.Message)" -ForegroundColor Yellow
    }
    exit 1
}

