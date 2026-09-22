# ==============================================================================
# ZeroPlatform Ecosystem Subsystem Clone Utility
# Synchronizes all 28 autonomous Zero repositories into the local workspace.
# Categorized according to the ZeroPlatform 6-Tier Strict DAG Architecture.
# ==============================================================================

$ErrorActionPreference = "Stop"

$ecosystemTiers = [ordered]@{
    "Tier 0: Core Foundation (The Bedrock - Zero Dependencies)" = @(
        "ZeroPrimitives",
        "ZeroConcurrency",
        "ZeroSecurity"
    );
    "Tier 1: Compute & System (Hardware & Numerics)" = @(
        "ZeroSystem",
        "ZeroCompression",
        "ZeroTensor",
        "ZeroCompute"
    );
    "Tier 2: Transport & Storage (Data & Comm Pipelines)" = @(
        "ZeroNetwork",
        "ZeroComm",
        "ZeroIoT",
        "ZeroRfid",
        "ZeroStorage",
        "ZeroData"
    );
    "Tier 3: Perception & Intelligence (Signal, Vision & AI)" = @(
        "ZeroSignal",
        "ZeroGeometry",
        "ZeroVideo",
        "ZeroAudioVisual",
        "ZeroInference",
        "ZeroNeural"
    );
    "Tier 4: Graphics & Spatial 3D (GPU Rendering)" = @(
        "ZeroGraphics",
        "ZeroCharts",
        "ZeroTwin3D",
        "Zero3D"
    );
    "Tier 5: Presentation & Orchestration (User Layer & Reporting)" = @(
        "ZeroDocuments",
        "ZeroReports",
        "ZeroPipeline",
        "ZeroUI",
        "ZeroUI.React"
    )
}

$totalRepos = 0
foreach ($tier in $ecosystemTiers.Keys) {
    $totalRepos += $ecosystemTiers[$tier].Count
}

Write-Host "`n==============================================================================" -ForegroundColor Cyan
Write-Host " [ZeroPlatform] Synchronizing Ecosystem ($totalRepos Autonomous Subsystems)" -ForegroundColor Cyan
Write-Host " Architecture: 6-Tier Strict Directed Acyclic Graph (DAG)" -ForegroundColor Cyan
Write-Host "==============================================================================`n" -ForegroundColor Cyan

$clonedCount = 0
$presentCount = 0

foreach ($tier in $ecosystemTiers.Keys) {
    Write-Host "[Tier] $tier" -ForegroundColor Magenta
    foreach ($sub in $ecosystemTiers[$tier]) {
        $targetPath = Join-Path $PSScriptRoot $sub
        if (-not (Test-Path (Join-Path $targetPath ".git"))) {
            Write-Host "   [Clone] Downloading kzxl/$sub into $targetPath..." -ForegroundColor Yellow
            git clone "https://github.com/kzxl/$sub.git" $targetPath
            $clonedCount++
        } else {
            Write-Host "   [OK] $sub is present" -ForegroundColor Green
            $presentCount++
        }
    }
    Write-Host ""
}

Write-Host "==============================================================================" -ForegroundColor Cyan
Write-Host " [Done] ZeroPlatform Synchronization: $presentCount present, $clonedCount cloned." -ForegroundColor Green
Write-Host " All $totalRepos subsystems ready for local compilation and orchestration." -ForegroundColor Cyan
Write-Host "==============================================================================`n" -ForegroundColor Cyan
