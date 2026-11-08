<#
.SYNOPSIS
    Konfigurasi Otomatis Host Windows (.wslconfig & Windows Firewall) untuk vLLM Turnkey API.
.DESCRIPTION
    Script ini mengonfigurasi WSL2 dengan 'networkingMode=mirrored' dan 'firewall=true'
    untuk mem-bypass pembatasan ICS (Internet Connection Sharing) pada jaringan intranet kantor,
    serta membuka port firewall 8000 agar LLM API dapat diakses oleh rekan kerja di LAN.
#>

[CmdletBinding()]
param (
    [int]$Port = 8000,
    [switch]$NoWslRestart
)

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " [vLLM Host Configuration: WSL2 Mirrored Mode & Firewall] " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# ------------------------------------------------------------------------------
# 1. Konfigurasi %USERPROFILE%\.wslconfig (Bypass ICS & Mirrored Mode)
# ------------------------------------------------------------------------------
$wslConfigPath = Join-Path $env:USERPROFILE ".wslconfig"
Write-Host "`n[1/3] Mengonfigurasi $wslConfigPath..." -ForegroundColor Green

$requiredSettings = @{
    "networkingMode" = "mirrored"
    "firewall"       = "true"
    "dnsTunneling"   = "true"
    "autoProxy"      = "true"
}

if (Test-Path $wslConfigPath) {
    $backupPath = "$wslConfigPath.bak_$(Get-Date -Format 'yyyyMMdd_HHmmss')"
    Copy-Item -Path $wslConfigPath -Destination $backupPath -Force
    Write-Host "  -> Backup dibuat di: $backupPath" -ForegroundColor DarkGray
    $lines = Get-Content -Path $wslConfigPath
} else {
    $lines = @()
}

$newLines = [System.Collections.Generic.List[string]]::new()
$inWsl2Section = $false
$wsl2SectionFound = $false
$keysApplied = @{}

foreach ($line in $lines) {
    $trimmed = $line.Trim()
    if ($trimmed -match '^\[(.+)\]$') {
        $sectionName = $matches[1].ToLower()
        if ($sectionName -eq "wsl2") {
            $inWsl2Section = $true
            $wsl2SectionFound = $true
            $newLines.Add($line)
            continue
        } else {
            if ($inWsl2Section) {
                foreach ($k in $requiredSettings.Keys) {
                    if (-not $keysApplied.ContainsKey($k)) {
                        $newLines.Add("$k=$($requiredSettings[$k])")
                        $keysApplied[$k] = $true
                    }
                }
                $inWsl2Section = $false
            }
            $newLines.Add($line)
            continue
        }
    }

    if ($inWsl2Section) {
        $matchedKey = $null
        foreach ($k in $requiredSettings.Keys) {
            if ($trimmed -match "^$k\s*=") {
                $matchedKey = $k
                break
            }
        }
        if ($matchedKey) {
            $newLines.Add("$matchedKey=$($requiredSettings[$matchedKey])")
            $keysApplied[$matchedKey] = $true
        } else {
            $newLines.Add($line)
        }
    } else {
        $newLines.Add($line)
    }
}

if (-not $wsl2SectionFound) {
    $newLines.Add("[wsl2]")
    foreach ($k in $requiredSettings.Keys) {
        $newLines.Add("$k=$($requiredSettings[$k])")
    }
} else {
    foreach ($k in $requiredSettings.Keys) {
        if (-not $keysApplied.ContainsKey($k)) {
            $newLines.Add("$k=$($requiredSettings[$k])")
        }
    }
}

try {
    Set-Content -Path $wslConfigPath -Value $newLines -Encoding UTF8 -Force
    Write-Host "  [OK] Konfigurasi networkingMode=mirrored & firewall=true berhasil diterapkan di .wslconfig!" -ForegroundColor Green
} catch {
    Write-Host "  [GAGAL] Tidak dapat menulis ke $wslConfigPath : $($_.Exception.Message)" -ForegroundColor Red
}

# ------------------------------------------------------------------------------
# 2. Konfigurasi Inbound Windows Defender Firewall (Butuh Administrator)
# ------------------------------------------------------------------------------
Write-Host "`n[2/3] Mengonfigurasi Windows Defender Firewall untuk Port $Port..." -ForegroundColor Green

$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
$ruleName = "vLLM-API-Port-$Port"

if ($isAdmin) {
    $existingRule = Get-NetFirewallRule -Name $ruleName -ErrorAction SilentlyContinue
    if ($existingRule) {
        Write-Host "  -> Firewall rule '$ruleName' sudah ada. Memperbarui pengaturan..." -ForegroundColor DarkGray
        Set-NetFirewallRule -Name $ruleName `
            -DisplayName "vLLM Private LLM API (Port $Port)" `
            -Description "Mengizinkan akses jaringan LAN ke server vLLM OpenAI API via WSL2 Mirrored Mode" `
            -Direction Inbound `
            -LocalPort $Port `
            -Protocol TCP `
            -Action Allow `
            -Profile Domain,Private,Public `
            -Enabled True
    } else {
        Write-Host "  -> Menambahkan Inbound Firewall Rule baru untuk Port $Port..." -ForegroundColor Yellow
        New-NetFirewallRule `
            -Name $ruleName `
            -DisplayName "vLLM Private LLM API (Port $Port)" `
            -Description "Mengizinkan akses jaringan LAN ke server vLLM OpenAI API via WSL2 Mirrored Mode" `
            -Direction Inbound `
            -LocalPort $Port `
            -Protocol TCP `
            -Action Allow `
            -Profile Domain,Private,Public `
            -Enabled True | Out-Null
    }
    Write-Host "  [OK] Port $Port berhasil dibuka di Windows Firewall untuk Domain, Private, dan Public!" -ForegroundColor Green
} else {
    Write-Warning "Penambahan rule Firewall memerlukan hak Administrator."
    Write-Host "Mencoba meminta izin Administrator (UAC Prompt)..." -ForegroundColor Yellow
    $argList = "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`" -Port $Port"
    if ($NoWslRestart) { $argList += " -NoWslRestart" }
    
    $elevatedSuccess = $false
    try {
        Start-Process powershell.exe -ArgumentList $argList -Verb RunAs -ErrorAction Stop
        $elevatedSuccess = $true
        Write-Host "  [OK] Menjalankan proses elevated di jendela baru." -ForegroundColor Green
    } catch {
        Write-Host "`n  [PERHATIAN: PANDUAN ELEVASI MANUAL]" -ForegroundColor Yellow
        Write-Host "  Untuk membuka port $Port di firewall secara manual, buka PowerShell Administrator dan jalankan:" -ForegroundColor White
        Write-Host "    New-NetFirewallRule -DisplayName `"vLLM API (Port $Port)`" -Direction Inbound -LocalPort $Port -Protocol TCP -Action Allow -Profile Domain,Private,Public" -ForegroundColor Cyan
    }
}

# ------------------------------------------------------------------------------
# 3. Status WSL2 Engine & Restart
# ------------------------------------------------------------------------------
Write-Host "`n[3/3] Memeriksa status WSL2 Engine..." -ForegroundColor Green
if (-not $NoWslRestart) {
    Write-Host "Agar konfigurasi mirrored mode pada .wslconfig aktif sempurna, instance WSL perlu direstart." -ForegroundColor Yellow
    $ans = Read-Host "Apakah Anda ingin merestart WSL sekarang? (Y/n)"
    if ($ans -eq '' -or $ans -match '^[Yy]') {
        Write-Host "Merestart WSL2 (wsl --shutdown)..." -ForegroundColor Cyan
        wsl.exe --shutdown
        Write-Host "  [OK] WSL2 berhasil direstart." -ForegroundColor Green
    } else {
        Write-Host "  -> Silakan jalankan 'wsl --shutdown' secara manual sebelum menjalankan vLLM." -ForegroundColor DarkGray
    }
} else {
    Write-Host "  -> Parameter -NoWslRestart aktif. WSL tidak direstart secara otomatis." -ForegroundColor DarkGray
}

Write-Host "`n==========================================================" -ForegroundColor Green
Write-Host " Konfigurasi Selesai!                                     " -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green
