<#
.SYNOPSIS
    Deteksi Cerdas Folder Model Lokal vLLM di ./models/
.DESCRIPTION
    Script ini memindai folder ./models/ untuk mendeteksi bobot model (safetensors/AWQ/GPTQ/GGUF),
    menentukan konfigurasi kuantisasi secara otomatis, dan memperbarui file .env.
#>

[CmdletBinding()]
param (
    [string]$ModelsPath = "",
    [string]$EnvFile = "",
    [switch]$NonInteractive
)

if (-not $ModelsPath) {
    $ModelsPath = Join-Path $PSScriptRoot "..\models"
}
if (-not $EnvFile) {
    $EnvFile = Join-Path $PSScriptRoot "..\.env"
}

$modelsDir = Resolve-Path $ModelsPath -ErrorAction SilentlyContinue
if (-not $modelsDir -or -not (Test-Path $modelsDir)) {
    New-Item -ItemType Directory -Path $ModelsPath -Force | Out-Null
    $modelsDir = Resolve-Path $ModelsPath
}

Write-Host "Memindai direktori model: $modelsDir" -ForegroundColor Cyan

# Mencari subdirektori di ./models yang berisi file bobot model atau konfigurasi
$detectedFolders = Get-ChildItem -Path $modelsDir -Directory | Where-Object {
    $folder = $_.FullName
    $hasFiles = Get-ChildItem -Path $folder -File -Recurse -Depth 2 | Where-Object {
        $_.Extension -in @(".safetensors", ".bin", ".gguf", ".pt") -or $_.Name -eq "config.json"
    }
    return ($null -ne $hasFiles)
}

# Jika tidak ada subfolder dengan file bobot, periksa apakah ada subfolder biasa
if ($detectedFolders.Count -eq 0) {
    $detectedFolders = Get-ChildItem -Path $modelsDir -Directory
}

function Update-EnvFile {
    param (
        [string]$FilePath,
        [string]$Key,
        [string]$Value
    )
    if (Test-Path $FilePath) {
        $content = Get-Content $FilePath
        $updated = $false
        $newContent = foreach ($line in $content) {
            if ($line -match "^$Key\s*=") {
                "$Key=$Value"
                $updated = $true
            } else {
                $line
            }
        }
        if (-not $updated) {
            $newContent += "$Key=$Value"
        }
        Set-Content -Path $FilePath -Value $newContent -Encoding UTF8
    }
}

function Detect-Quantization {
    param ([string]$Name)
    $lower = $Name.ToLower()
    if ($lower -match 'awq') { return 'awq' }
    if ($lower -match 'gptq') { return 'gptq' }
    if ($lower -match 'fp8') { return 'fp8' }
    if ($lower -match 'gguf') { return 'gguf' }
    return '' # Default auto / unquantized
}

# Skenario 1: Folder kosong (tidak ada model ditemukan)
if ($detectedFolders.Count -eq 0) {
    Write-Host "`n[PERINGATAN] Tidak ada folder model ditemukan di ./models/ !" -ForegroundColor Yellow
    Write-Host "Untuk menjalankan vLLM, Anda perlu meletakkan folder bobot model di ./models/" -ForegroundColor White
    Write-Host "`nRekomendasi Model untuk GPU 16GB VRAM:" -ForegroundColor Cyan
    Write-Host "  1. Qwen/Qwen2.5-7B-Instruct-AWQ (Sangat cepat, hemat VRAM, context 8k-32k)" -ForegroundColor White
    Write-Host "  2. hugging-quants/Meta-Llama-3.1-8B-Instruct-AWQ-INT4" -ForegroundColor White
    Write-Host "  3. deepseek-ai/DeepSeek-R1-Distill-Qwen-7B (AWQ/FP8)" -ForegroundColor White
    Write-Host "`nContoh Perintah Download (PowerShell / CMD):" -ForegroundColor Gray
    Write-Host "  pip install huggingface_hub" -ForegroundColor DarkGray
    Write-Host "  huggingface-cli download Qwen/Qwen2.5-7B-Instruct-AWQ --local-dir ./models/Qwen2.5-7B-Instruct-AWQ" -ForegroundColor Green
    Write-Host "`nSilakan letakkan model Anda, lalu jalankan kembali start.bat.`n" -ForegroundColor Yellow
    exit 1
}

$selectedModel = $null

# Skenario 2: Ditemukan tepat 1 model
if ($detectedFolders.Count -eq 1) {
    $selectedModel = $detectedFolders[0].Name
    Write-Host "`n[OK] 1 Model terdeteksi secara otomatis: $selectedModel" -ForegroundColor Green
}
# Skenario 3: Ditemukan lebih dari 1 model
else {
    Write-Host "`nDitemukan $($detectedFolders.Count) model di dalam folder ./models/:" -ForegroundColor Yellow
    for ($i = 0; $i -lt $detectedFolders.Count; $i++) {
        Write-Host "  [$($i + 1)] $($detectedFolders[$i].Name)" -ForegroundColor White
    }

    if ($NonInteractive) {
        $selectedModel = $detectedFolders[0].Name
        Write-Host "Mode Non-Interactive: Memilih [$selectedModel]" -ForegroundColor Cyan
    } else {
        $choice = 0
        while ($choice -lt 1 -or $choice -gt $detectedFolders.Count) {
            $inputVal = Read-Host "`nPilih nomor model yang ingin diaktifkan (1-$($detectedFolders.Count))"
            $choice = [int]$inputVal
        }
        $selectedModel = $detectedFolders[$choice - 1].Name
    }
}

# Update file .env
$quant = Detect-Quantization -Name $selectedModel
Write-Host "`nMemperbarui konfigurasi .env:" -ForegroundColor Cyan
Write-Host "  -> MODEL_NAME   = $selectedModel" -ForegroundColor White
Write-Host "  -> QUANTIZATION = $(if ($quant) { $quant } else { '(auto/none)' })" -ForegroundColor White

Update-EnvFile -FilePath $EnvFile -Key "MODEL_NAME" -Value $selectedModel
Update-EnvFile -FilePath $EnvFile -Key "QUANTIZATION" -Value $quant

Write-Host "Model '$selectedModel' siap dijalankan!`n" -ForegroundColor Green
exit 0
