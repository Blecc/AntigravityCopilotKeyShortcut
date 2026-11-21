<#
.SYNOPSIS
    Downloads (via Git or ZIP fallback), builds, and installs AntigravityCopilotKeyShortcut.

.DESCRIPTION
    Supports both one-line remote execution:
        irm https://raw.githubusercontent.com/Blecc/AntigravityCopilotKeyShortcut/main/install.ps1 | iex

    And custom install folder via environment variable or direct invocation:
        $env:ANTIGRAVITY_COPILOT_INSTALL_DIR = "C:\Utility\Programmi\AntigravityCopilotKeyShortcut"
        irm https://raw.githubusercontent.com/Blecc/AntigravityCopilotKeyShortcut/main/install.ps1 | iex
#>

$ErrorActionPreference = "Stop"

function Install-AntigravityCopilotShortcut {
    param(
        [string]$InstallDir = $(if ($env:ANTIGRAVITY_COPILOT_INSTALL_DIR) {
            $env:ANTIGRAVITY_COPILOT_INSTALL_DIR
        } else {
            Join-Path $env:LOCALAPPDATA "AntigravityCopilotKeyShortcut"
        }),
        [string]$RepoUrl = "https://github.com/Blecc/AntigravityCopilotKeyShortcut.git",
        [string]$ZipUrl  = "https://github.com/Blecc/AntigravityCopilotKeyShortcut/archive/refs/heads/main.zip"
    )

    try {
        [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
    } catch {}

    Write-Host "=== Antigravity Copilot Key Shortcut - Installer ===" -ForegroundColor Cyan
    Write-Host "Install folder: $InstallDir" -ForegroundColor Gray

    # Stop any running instance so files can be updated cleanly
    $running = Get-Process -Name "CopilotToAntigravity" -ErrorAction SilentlyContinue
    if ($running) {
        Write-Host "Stopping running CopilotToAntigravity instance..." -ForegroundColor Cyan
        $running | Stop-Process -Force
        Start-Sleep -Milliseconds 300
    }

    $git = Get-Command "git.exe" -ErrorAction SilentlyContinue
    $usedGit = $false

    if ($git) {
        $gitDir = Join-Path $InstallDir ".git"
        if (Test-Path $gitDir) {
            Write-Host "Updating existing Git repository..." -ForegroundColor Cyan
            & $git.Source -C $InstallDir pull --ff-only
            if ($LASTEXITCODE -eq 0) {
                $usedGit = $true
            } else {
                Write-Host "[WARN] git pull failed; falling back to ZIP download..." -ForegroundColor Yellow
            }
        } elseif (-not (Test-Path $InstallDir) -or ((Get-ChildItem -Force $InstallDir -ErrorAction SilentlyContinue | Measure-Object).Count -eq 0)) {
            $parent = Split-Path $InstallDir -Parent
            if ($parent -and -not (Test-Path $parent)) {
                New-Item -ItemType Directory -Force -Path $parent | Out-Null
            }
            Write-Host "Cloning repository via Git..." -ForegroundColor Cyan
            & $git.Source clone --depth 1 $RepoUrl $InstallDir
            if ($LASTEXITCODE -eq 0) {
                $usedGit = $true
            } else {
                Write-Host "[WARN] git clone failed; falling back to ZIP download..." -ForegroundColor Yellow
            }
        }
    }

    if (-not $usedGit) {
        Write-Host "Downloading latest version from GitHub..." -ForegroundColor Cyan
        $tempRoot = Join-Path $env:TEMP ("AntigravityCopilotKeyShortcut_" + [Guid]::NewGuid().ToString("N"))
        $zipPath  = Join-Path $tempRoot "repo.zip"
        $extractDir = Join-Path $tempRoot "extract"

        New-Item -ItemType Directory -Force -Path $tempRoot | Out-Null
        try {
            Invoke-WebRequest -Uri $ZipUrl -OutFile $zipPath -UseBasicParsing
            Expand-Archive -Path $zipPath -DestinationPath $extractDir -Force

            $extractedRepo = Get-ChildItem -Path $extractDir -Directory | Select-Object -First 1
            if (-not $extractedRepo) {
                throw "Failed to locate extracted repository folder."
            }

            if (-not (Test-Path $InstallDir)) {
                New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null
            }

            # Copy files while preserving any existing config.ini
            Get-ChildItem -Path $extractedRepo.FullName -Force | ForEach-Object {
                $dest = Join-Path $InstallDir $_.Name
                if ($_.Name -ieq "config.ini" -and (Test-Path $dest)) {
                    return
                }
                Copy-Item -Path $_.FullName -Destination $dest -Recurse -Force
            }
        } finally {
            Remove-Item -Path $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
        }
    }

    $buildScript = Join-Path $InstallDir "Build-And-Install.ps1"
    if (-not (Test-Path $buildScript)) {
        throw "Build-And-Install.ps1 was not found in $InstallDir."
    }

    Write-Host ""
    & $buildScript
}

Install-AntigravityCopilotShortcut
