<#
.SYNOPSIS
    ZeroUI Demo Launcher Script for WinForms and WPF showcase applications.
.DESCRIPTION
    Launches either the WinForms Demo, WPF Demo, or both side-by-side.
.PARAMETER Target
    Target application to launch: "WinForms", "WPF", or "Both". Defaults to interactive prompt.
.EXAMPLE
    .\scripts\run-demo.ps1 -Target WinForms
    .\scripts\run-demo.ps1 -Target WPF
    .\scripts\run-demo.ps1 -Target Both
#>

[CmdletBinding()]
param (
    [ValidateSet("WinForms", "WPF", "Both", "All")]
    [string]$Target = ""
)

$rootDir = Split-Path -Parent $PSScriptRoot
if (-not (Test-Path "$rootDir\ZeroUI.slnx")) {
    $rootDir = (Get-Location).Path
}

Write-Host ""
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " 🚀 ZeroUI High-Performance Desktop Showcase Launcher     " -ForegroundColor White
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host ""

if ([string]::IsNullOrWhiteSpace($Target)) {
    Write-Host "Please select the demo to launch:" -ForegroundColor Yellow
    Write-Host "  [1] WinForms Demo  (Advanced Industrial, Trend Studio & Sub-Tabs)" -ForegroundColor Green
    Write-Host "  [2] WPF Demo       (Vector Business Analytics, GPU Canvas & 60+ Controls)" -ForegroundColor Cyan
    Write-Host "  [3] Both Demos     (Launch WinForms & WPF side-by-side)" -ForegroundColor Magenta
    Write-Host "  [Q] Quit" -ForegroundColor Gray
    Write-Host ""
    $choice = ReadHost "Enter selection [1/2/3/Q] (Default: 1)"

    switch ($choice.Trim()) {
        "2" { $Target = "WPF" }
        "3" { $Target = "Both" }
        "Q" { exit 0 }
        "q" { exit 0 }
        default { $Target = "WinForms" }
    }
}

function Run-Project([string]$name, [string]$projPath) {
    Write-Host "[*] Launching $name..." -ForegroundColor Cyan
    $fullPath = Join-Path $rootDir $projPath
    if (-not (Test-Path $fullPath)) {
        Write-Error "Project file not found at: $fullPath"
        return
    }

    Start-Process "dotnet" -ArgumentList "run --project `"$fullPath`"" -WorkingDirectory $rootDir
}

switch ($Target) {
    "WinForms" {
        Run-Project "WinForms Demo" "demo\WinformDemo\WinformDemo.csproj"
    }
    "WPF" {
        Run-Project "WPF Demo" "demo\WpfDemo\WpfDemo.csproj"
    }
    "Both" {
        Run-Project "WinForms Demo" "demo\WinformDemo\WinformDemo.csproj"
        Start-Sleep -Seconds 1
        Run-Project "WPF Demo" "demo\WpfDemo\WpfDemo.csproj"
    }
    "All" {
        Run-Project "WinForms Demo" "demo\WinformDemo\WinformDemo.csproj"
        Start-Sleep -Seconds 1
        Run-Project "WPF Demo" "demo\WpfDemo\WpfDemo.csproj"
    }
}

Write-Host ""
Write-Host "✅ Demo process launched successfully!" -ForegroundColor Green
Write-Host ""
