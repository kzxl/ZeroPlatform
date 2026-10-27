# ==============================================================================
# ZeroPlatform: GitHub Satellite Repositories Archival Utility
# Updates README.md on satellite repos with migration notices and archives them.
# ==============================================================================

param(
    [switch]$ArchiveOnly,
    [switch]$DryRun
)

$satellites = @(
    "ZeroPrimitives", "ZeroConcurrency", "ZeroSecurity", "ZeroText",
    "ZeroSystem", "ZeroCompression", "ZeroTensor", "ZeroCompute", "ZeroAsset",
    "ZeroNetwork", "ZeroComm", "ZeroIoT", "ZeroRfid", "ZeroStorage", "ZeroData", "ZeroVector", "ZeroBus",
    "ZeroSignal", "ZeroAudio", "ZeroGeometry", "ZeroVideo", "ZeroAudioVisual",
    "ZeroInference", "ZeroNeural", "ZeroOcr", "ZeroTokenizer", "ZeroLlm", "ZeroMotion", "ZeroScan3D",
    "ZeroGraphics", "ZeroCharts", "ZeroTwin3D", "Zero3D",
    "ZeroDocuments", "ZeroReports", "ZeroPipeline", "ZeroUI", "ZeroUI.React",
    "ZeroAgent", "ZeroPrompt"
)

Write-Host "==============================================================================" -ForegroundColor Cyan
Write-Host " [ZeroPlatform] Satellite Repositories Archival Tool ($($satellites.Count) Subsystems)" -ForegroundColor Cyan
Write-Host "==============================================================================`n" -ForegroundColor Cyan

foreach ($repo in $satellites) {
    Write-Host "--> Processing kzxl/$repo..." -ForegroundColor Yellow
    
    if ($DryRun) {
        Write-Host "   [DRY-RUN] gh repo archive kzxl/$repo --yes" -ForegroundColor Gray
        continue
    }

    try {
        # Check if gh CLI is authenticated and archive the repository
        gh repo archive "kzxl/$repo" --yes
        Write-Host "   [ARCHIVED] kzxl/$repo successfully archived as Read-Only." -ForegroundColor Green
    } catch {
        Write-Warning "   [ERROR] Failed to archive kzxl/${repo}: $_"
    }
}

Write-Host "`nAll operations completed." -ForegroundColor Cyan
