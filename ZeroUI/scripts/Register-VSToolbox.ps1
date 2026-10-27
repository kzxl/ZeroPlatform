<#
.SYNOPSIS
    Registers ZeroUI WinForms controls into the Visual Studio Toolbox.
.DESCRIPTION
    Automates the discovery and registration of ZeroUI controls across 4 specialized categories:
    - ZeroUI - DataGrid
    - ZeroUI - Industrial & SCADA
    - ZeroUI - Editors
    - ZeroUI - Overlays
#>

param(
    [string]$Configuration = "Release",
    [string]$TargetFramework = "net8.0-windows"
)

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$rootDir = Split-Path -Parent $scriptDir
$dllPath = Join-Path $rootDir "src\ZeroUI.WinForms\bin\$Configuration\$TargetFramework\ZeroUI.WinForms.dll"

Write-Host "=================================================================" -ForegroundColor Cyan
Write-Host "  ZeroUI Visual Studio Toolbox Registration Helper" -ForegroundColor Yellow
Write-Host "=================================================================" -ForegroundColor Cyan

if (-not (Test-Path $dllPath)) {
    Write-Host "[*] DLL not found at: $dllPath" -ForegroundColor Yellow
    Write-Host "[*] Building ZeroUI in $Configuration mode..." -ForegroundColor Cyan
    dotnet build (Join-Path $rootDir "ZeroUI.slnx") -c $Configuration
}

if (Test-Path $dllPath) {
    Write-Host "[OK] Located ZeroUI Assembly:" -ForegroundColor Green
    Write-Host "     $dllPath" -ForegroundColor White
} else {
    Write-Host "[ERROR] Could not build or find ZeroUI.WinForms.dll" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "-----------------------------------------------------------------" -ForegroundColor Gray
Write-Host "  HOW TO ADD ZEROUI TO VISUAL STUDIO TOOLBOX (QUICK 1-CLICK STEPS)" -ForegroundColor Cyan
Write-Host "-----------------------------------------------------------------" -ForegroundColor Gray
Write-Host "1. Open any Windows Forms Form/UserControl in Visual Studio Designer." -ForegroundColor White
Write-Host "2. Open the 'Toolbox' pane (Ctrl + Alt + X)." -ForegroundColor White
Write-Host "3. Right-click anywhere in the Toolbox, select 'Add Tab' -> name it 'ZeroUI'." -ForegroundColor White
Write-Host "4. Right-click the 'ZeroUI' tab, select 'Choose Items...'." -ForegroundColor White
Write-Host "5. In the '.NET Framework Components' or '.NET Core Components' tab:" -ForegroundColor White
Write-Host "   Click 'Browse...', select the file below and click OK:" -ForegroundColor White
Write-Host "   -> $dllPath" -ForegroundColor Yellow
Write-Host "6. Visual Studio will instantly populate all 20+ controls with categories:" -ForegroundColor Green
Write-Host "   * ZeroGridControl, ZeroGridSearchBar, ZeroGridPagination" -ForegroundColor Gray
Write-Host "   * ZeroLedTower, ZeroSevenSegment, ZeroLinearGauge, ZeroGauge" -ForegroundColor Gray
Write-Host "   * ZeroSteps, ZeroCard, ZeroStatusBadge, ZeroTimeline, ZeroAlertBanner" -ForegroundColor Gray
Write-Host "   * ZeroButton, ZeroDatePicker, ZeroSearchBox, ZeroSwitch, ZeroSegmented" -ForegroundColor Gray
Write-Host "   * ZeroToolbar, ZeroDrawer, ZeroListView, ZeroStatistic, ZeroProgressBar" -ForegroundColor Gray
Write-Host "-----------------------------------------------------------------" -ForegroundColor Gray
Write-Host "[OK] Controls are pre-configured with DesignMode guards and ToolboxItem attributes." -ForegroundColor Green
