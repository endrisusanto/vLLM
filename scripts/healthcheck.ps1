<#
.SYNOPSIS
    Pengecekan Kesiapan (Readiness Health Check) vLLM Container
.DESCRIPTION
    Script ini memantau endpoint /health dan /v1/models vLLM sampai engine selesai
    memuat model ke VRAM dan siap melayani request API.
#>

[CmdletBinding()]
param (
    [int]$Port = 8000,
    [string]$ApiKey = "",
    [int]$TimeoutSeconds = 240
)

$healthUrl = "http://127.0.0.1:$Port/health"
$modelsUrl = "http://127.0.0.1:$Port/v1/models"

$headers = @{}
if ($ApiKey) {
    $headers["Authorization"] = "Bearer $ApiKey"
}

Write-Host "`nMenunggu vLLM Engine memuat model ke GPU VRAM (Timeout: ${TimeoutSeconds}s)..." -ForegroundColor Cyan
Write-Host "Endpoint: $healthUrl" -ForegroundColor DarkGray

$startTime = [DateTime]::UtcNow
$spinner = @('|', '/', '-', '\')
$spinnerIndex = 0

while (($([DateTime]::UtcNow) - $startTime).TotalSeconds -lt $TimeoutSeconds) {
    $elapsed = [math]::Round(($([DateTime]::UtcNow) - $startTime).TotalSeconds)
    $spinChar = $spinner[$spinnerIndex % $spinner.Length]
    $spinnerIndex++

    Write-Host -NoNewline "`r[$spinChar] Memuat bobot model & inisialisasi KV Cache... (${elapsed}s) "

    try {
        $response = Invoke-WebRequest -Uri $healthUrl -UseBasicParsing -TimeoutSec 3 -ErrorAction Stop
        if ($response.StatusCode -eq 200) {
            Write-Host "`r[OK] vLLM Engine Siap & Aktif! Waktu startup: ${elapsed} detik.             " -ForegroundColor Green
            
            # Verifikasi model yang termuat
            try {
                $modelsResp = Invoke-RestMethod -Uri $modelsUrl -Headers $headers -TimeoutSec 3 -ErrorAction SilentlyContinue
                if ($modelsResp.data) {
                    $loadedModelId = $modelsResp.data[0].id
                    Write-Host "Model ID terdaftar: " -NoNewline -ForegroundColor White
                    Write-Host "$loadedModelId" -ForegroundColor Cyan
                }
            } catch {
                # Tidak apa-apa jika endpoint /v1/models memerlukan proteksi token
            }
            exit 0
        }
    } catch {
        # Engine masih booting
    }

    # Periksa apakah container masih berjalan atau sudah crash (misal CUDA OOM)
    $containerRunning = & docker compose ps -q vllm 2>$null
    if (-not $containerRunning) {
        # Coba periksa dengan nama container
        $containerStatus = & docker inspect -f '{{.State.Status}}' vllm-server 2>$null
        if ($containerStatus -and $containerStatus -ne "running") {
            Write-Host "`n`n[ERROR] Container vllm-server berhenti atau mengalami crash!" -ForegroundColor Red
            Write-Host "Status container: $containerStatus" -ForegroundColor Yellow
            Write-Host "`nLog container terakhir:" -ForegroundColor Red
            & docker compose logs --tail 25 vllm
            exit 1
        }
    }

    Start-Sleep -Seconds 2
}

Write-Host "`n`n[TIMEOUT] vLLM tidak merespons dalam waktu ${TimeoutSeconds} detik." -ForegroundColor Red
Write-Host "Silakan periksa log container dengan perintah: docker compose logs -f vllm" -ForegroundColor Yellow
exit 1

