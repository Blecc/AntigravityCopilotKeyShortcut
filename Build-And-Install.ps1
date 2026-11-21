<#
.SYNOPSIS
    Auto-detects Antigravity and the C# compiler, compiles CopilotToAntigravity.exe,
    registers it in Windows Startup, and launches it.

.PARAMETER NoLaunch
    Compile and configure Startup without launching the helper at the end.

.PARAMETER NoStartup
    Compile and launch without creating/updating the Windows Startup shortcut.
#>
[CmdletBinding()]
param(
    [switch]$NoLaunch,
    [switch]$NoStartup
)

$ErrorActionPreference = "Stop"

$AppDir     = $PSScriptRoot
$SourceFile = Join-Path $AppDir "CopilotToAntigravity.cs"
$OutputExe  = Join-Path $AppDir "CopilotToAntigravity.exe"
$StartupLnk = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\Startup\CopilotToAntigravity.lnk"

function Find-CSharpCompiler {
    $candidates = @(
        (Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"),
        (Join-Path $env:WINDIR "Microsoft.NET\Framework\v4.0.30319\csc.exe")
    )

    foreach ($path in $candidates) {
        if (Test-Path $path) {
            return $path
        }
    }

    $cmd = Get-Command "csc.exe" -ErrorAction SilentlyContinue
    if ($cmd -and (Test-Path $cmd.Source)) {
        return $cmd.Source
    }

    return $null
}

function Find-AntigravityExe {
    $candidates = @(
        (Join-Path $env:LOCALAPPDATA "Programs\antigravity\Antigravity.exe"),
        (Join-Path $env:LOCALAPPDATA "Programs\Antigravity\Antigravity.exe"),
        (Join-Path $env:ProgramFiles "antigravity\Antigravity.exe"),
        (Join-Path $env:ProgramFiles "Antigravity\Antigravity.exe")
    )
    if (${env:ProgramFiles(x86)}) {
        $candidates += (Join-Path ${env:ProgramFiles(x86)} "antigravity\Antigravity.exe")
        $candidates += (Join-Path ${env:ProgramFiles(x86)} "Antigravity\Antigravity.exe")
    }

    foreach ($path in $candidates) {
        if ($path -and (Test-Path $path)) {
            return $path
        }
    }

    # Check running process
    $proc = Get-Process -Name "Antigravity" -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($proc) {
        try {
            if ($proc.Path -and (Test-Path $proc.Path)) {
                return $proc.Path
            }
        } catch {}
    }

    # Check App Paths & Uninstall registry keys
    $appPaths = @(
        "HKCU:\Software\Microsoft\Windows\CurrentVersion\App Paths\Antigravity.exe",
        "HKLM:\Software\Microsoft\Windows\CurrentVersion\App Paths\Antigravity.exe"
    )
    foreach ($key in $appPaths) {
        try {
            $val = (Get-ItemProperty -Path $key -ErrorAction SilentlyContinue).'(default)'
            if ($val) {
                $clean = $val.Trim().Trim('"')
                if (Test-Path $clean) { return $clean }
            }
        } catch {}
    }

    $uninstallRoots = @(
        "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\*",
        "HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\*",
        "HKLM:\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*"
    )
    foreach ($root in $uninstallRoots) {
        $entries = Get-ItemProperty -Path $root -ErrorAction SilentlyContinue |
            Where-Object { $_.DisplayName -like "*Antigravity*" }
        foreach ($entry in $entries) {
            if ($entry.InstallLocation) {
                $exe = Join-Path ($entry.InstallLocation.Trim().Trim('"')) "Antigravity.exe"
                if (Test-Path $exe) { return $exe }
            }
            if ($entry.DisplayIcon) {
                $iconPath = ($entry.DisplayIcon -split ',')[0].Trim().Trim('"')
                if ($iconPath -and (Test-Path $iconPath)) {
                    if ([System.IO.Path]::GetFileName($iconPath) -ieq "Antigravity.exe") {
                        return $iconPath
                    }
                    $sibling = Join-Path (Split-Path $iconPath -Parent) "Antigravity.exe"
                    if (Test-Path $sibling) { return $sibling }
                }
            }
            if ($entry.UninstallString) {
                $uninst = ($entry.UninstallString -split '"')[1]
                if (-not $uninst) { $uninst = $entry.UninstallString.Trim() }
                $dir = Split-Path $uninst -Parent -ErrorAction SilentlyContinue
                if ($dir) {
                    $exe = Join-Path $dir "Antigravity.exe"
                    if (Test-Path $exe) { return $exe }
                }
            }
        }
    }

    $inPath = Get-Command "Antigravity.exe" -ErrorAction SilentlyContinue
    if ($inPath -and (Test-Path $inPath.Source)) {
        return $inPath.Source
    }

    return $null
}

Write-Host "=== Antigravity Copilot Key Shortcut - Setup ===" -ForegroundColor Cyan

if (-not (Test-Path $SourceFile)) {
    Write-Host "[ERROR] Source file not found: $SourceFile" -ForegroundColor Red
    exit 1
}

# 1. Auto-detect Antigravity installation
$antigravityExe = Find-AntigravityExe
if ($antigravityExe) {
    Write-Host "[OK] Antigravity detected at: $antigravityExe" -ForegroundColor Green
} else {
    Write-Host "[WARN] Antigravity.exe was not found on this PC yet." -ForegroundColor Yellow
    Write-Host "       The helper will still compile and auto-detect Antigravity once installed." -ForegroundColor Yellow
}

# 2. Detect C# compiler state
$cscPath = Find-CSharpCompiler
if (-not $cscPath) {
    Write-Host ""
    Write-Host "[MISSING] C# compiler (csc.exe / .NET Framework 4.x) was NOT found." -ForegroundColor Red
    Write-Host "          A C# compiler is required to build CopilotToAntigravity.exe." -ForegroundColor Yellow

    $answer = Read-Host "Would you like to install .NET Framework 4.8 Developer Pack now? (Y/N)"
    if ($answer -match '^(y|yes)$') {
        $winget = Get-Command "winget.exe" -ErrorAction SilentlyContinue
        if ($winget) {
            Write-Host "Installing Microsoft .NET Framework 4.8 Developer Pack via winget..." -ForegroundColor Cyan
            & $winget.Source install --id Microsoft.DotNet.Framework.DeveloperPack_4 -e --accept-source-agreements --accept-package-agreements
            $cscPath = Find-CSharpCompiler
        } else {
            Write-Host "winget was not found. Opening Microsoft's official .NET Framework 4.8 download page..." -ForegroundColor Cyan
            Start-Process "https://dotnet.microsoft.com/en-us/download/dotnet-framework/net48"
        }

        if (-not $cscPath) {
            Write-Host ""
            Write-Host "Please finish installing .NET Framework 4.8 (and restart this terminal if needed), then re-run Build-And-Install.bat." -ForegroundColor Yellow
            exit 1
        }
    } else {
        Write-Host "Installation cancelled. You can download .NET Framework 4.8 manually from:" -ForegroundColor Yellow
        Write-Host "https://dotnet.microsoft.com/en-us/download/dotnet-framework/net48"
        exit 1
    }
}

Write-Host "[OK] C# compiler detected at: $cscPath" -ForegroundColor Green

# 3. Stop any running instance so the executable can be updated cleanly
$running = Get-Process -Name "CopilotToAntigravity" -ErrorAction SilentlyContinue
if ($running) {
    Write-Host "Stopping running CopilotToAntigravity instance..." -ForegroundColor Cyan
    $running | Stop-Process -Force
    Start-Sleep -Milliseconds 300
}

# 4. Compile CopilotToAntigravity.exe
Write-Host "Compiling CopilotToAntigravity.exe..." -ForegroundColor Cyan
& $cscPath /nologo /target:winexe /platform:x64 /optimize+ `
    "/out:$OutputExe" `
    /r:System.Windows.Forms.dll `
    /r:System.Drawing.dll `
    "$SourceFile"

if ($LASTEXITCODE -ne 0 -or -not (Test-Path $OutputExe)) {
    Write-Host "[ERROR] Compilation failed (exit code $LASTEXITCODE)." -ForegroundColor Red
    exit 1
}

$exeInfo = Get-Item $OutputExe
Write-Host "[OK] Compiled successfully: $($exeInfo.FullName) ($($exeInfo.Length) bytes)" -ForegroundColor Green

# 5. Create/update Windows Startup shortcut
if (-not $NoStartup) {
    $ws  = New-Object -ComObject WScript.Shell
    $lnk = $ws.CreateShortcut($StartupLnk)
    $lnk.TargetPath       = $OutputExe
    $lnk.WorkingDirectory = $AppDir
    $lnk.IconLocation     = if ($antigravityExe) { "$antigravityExe,0" } else { "$OutputExe,0" }
    $lnk.Description      = "Remap Copilot key to toggle Antigravity"
    $lnk.Save()
    Write-Host "[OK] Startup shortcut updated: $StartupLnk" -ForegroundColor Green
}

# 6. Launch helper
if (-not $NoLaunch) {
    Start-Process -FilePath $OutputExe -WorkingDirectory $AppDir
    Start-Sleep -Milliseconds 600
    $started = Get-Process -Name "CopilotToAntigravity" -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($started) {
        Write-Host "[OK] CopilotToAntigravity is now running (PID $($started.Id))." -ForegroundColor Green
    } else {
        Write-Host "[WARN] Launched CopilotToAntigravity.exe, but could not verify its process." -ForegroundColor Yellow
    }
}

Write-Host ""
Write-Host "Done! Press your Copilot key to open, focus, or minimize Antigravity." -ForegroundColor Cyan
