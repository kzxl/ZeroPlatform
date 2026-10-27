# ==============================================================================
# ZeroPlatform: Ecosystem Subsystem Clone Utility [RETIRED / ARCHIVED]
# NOTE: As of October 2026, ZeroPlatform is a unified Sovereign Monorepo.
# All 40 autonomous subsystems are directly tracked in this single repository.
# You do NOT need to clone satellites individually. Simply git clone ZeroPlatform!
# ==============================================================================

$ErrorActionPreference = "Stop"

$ecosystemTiers = [ordered]@{
    "Tier 0: Core Foundation (The Bedrock - Zero Dependencies)" = @(
        "ZeroPrimitives",
        "ZeroConcurrency",
        "ZeroSecurity",
        "ZeroText"
    );
    "Tier 1: Compute & System (Hardware & Numerics)" = @(
        "ZeroSystem",
        "ZeroCompression",
        "ZeroTensor",
        "ZeroCompute",
        "ZeroAsset"
    );
    "Tier 2: Transport & Storage (Data & Comm Pipelines)" = @(
        "ZeroNetwork",
        "ZeroComm",
        "ZeroIoT",
        "ZeroRfid",
        "ZeroStorage",
        "ZeroData",
        "ZeroVector"
    );
    "Tier 3: Perception & Intelligence (Signal, Vision, OCR & AI)" = @(
        "ZeroSignal",
        "ZeroAudio",
        "ZeroGeometry",
        "ZeroVideo",
        "ZeroAudioVisual",
        "ZeroInference",
        "ZeroNeural",
        "ZeroOcr",
        "ZeroTokenizer",
        "ZeroLlm"
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
        "ZeroUI.React",
        "ZeroAgent",
        "ZeroPrompt"
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
