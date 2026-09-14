# ==============================================================================
# ZeroPlatform Ecosystem Subsystem Clone Utility
# Clones all 12 independent Zero repositories into the local workspace.
# ==============================================================================

$ErrorActionPreference = "Stop"

$subsystems = @(
    "ZeroTensor",
    "ZeroCompute",
    "ZeroData",
    "ZeroStorage",
    "ZeroInference",
    "ZeroNeural",
    "ZeroSignal",
    "ZeroGeometry",
    "ZeroComm",
    "ZeroGraphics",
    "ZeroUI",
    "ZeroPipeline",
    "ZeroPrimitives",
    "ZeroIoT",
    "ZeroCharts",
    "ZeroReports",
    "ZeroAudioVisual",
    "ZeroTwin3D"
)

Write-Host "`n🚀 Synchronizing ZeroPlatform Ecosystem Subsystems (17 Subsystems)...`n" -ForegroundColor Cyan

foreach ($sub in $subsystems) {
    $targetPath = Join-Path $PSScriptRoot $sub
    if (-not (Test-Path (Join-Path $targetPath ".git"))) {
        Write-Host "⬇️  Cloning kzxl/$sub into $targetPath..." -ForegroundColor Yellow
        git clone "https://github.com/kzxl/$sub.git" $targetPath
    } else {
        Write-Host "✅ $sub is already present." -ForegroundColor Green
    }
}

Write-Host "`n✨ All 12 ZeroPlatform subsystems are present and ready!`n" -ForegroundColor Cyan
