# ==============================================================================
# ZeroPlatform: GitHub Satellite Repositories Archival & README Update Tool
# Updates README.md on satellite repos with migration notices and archives them.
# ==============================================================================

param(
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
Write-Host " [ZeroPlatform] Updating READMEs & Archiving Satellite Repos ($($satellites.Count) Repos)" -ForegroundColor Cyan
Write-Host "==============================================================================`n" -ForegroundColor Cyan

$count = 0
$total = $satellites.Count

foreach ($repo in $satellites) {
    $count++
    Write-Host "[$count/$total] Processing kzxl/$repo..." -ForegroundColor Yellow

    if ($DryRun) {
        Write-Host "   [DRY-RUN] Would unarchive, update README.md, and archive kzxl/$repo" -ForegroundColor Gray
        continue
    }

    try {
        # 1. Unarchive repository
        gh repo unarchive "kzxl/$repo" --yes 2>$null

        # 2. Get current README.md
        $fileJson = gh api "repos/kzxl/$repo/contents/README.md" 2>$null
        if (-not $fileJson) {
            Write-Warning "   [WARN] README.md not found for kzxl/$repo, skipping README update."
        } else {
            $fileInfo = $fileJson | ConvertFrom-Json
            $sha = $fileInfo.sha
            $existingBytes = [System.Convert]::FromBase64String(($fileInfo.content -replace '\s',''))
            $existingContent = [System.Text.Encoding]::UTF8.GetString($existingBytes)

            if ($existingContent -notmatch "REPOSITORY CONSOLIDATION NOTICE") {
                $banner = @"
> [!IMPORTANT]
> ### 🏛️ REPOSITORY CONSOLIDATION NOTICE
> **This repository has been officially consolidated into the unified [ZeroPlatform Sovereign Monorepo](https://github.com/kzxl/ZeroPlatform).**  
> All active development, issues, pull requests, and new releases take place in the central repository:  
> 👉 **[kzxl/ZeroPlatform/tree/master/$repo](https://github.com/kzxl/ZeroPlatform/tree/master/$repo)**
> 
> *This repository is archived and preserved for historical reference and permanent package links.*

---

"@
                $newContent = $banner + "`n" + $existingContent
                $newBase64 = [System.Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes($newContent))

                $body = @{
                    message = "docs: add repository migration notice to ZeroPlatform monorepo"
                    content = $newBase64
                    sha = $sha
                } | ConvertTo-Json

                $null = $body | gh api --method PUT "repos/kzxl/$repo/contents/README.md" --input -
                Write-Host "   [UPDATED] README.md updated with migration notice." -ForegroundColor Green
            } else {
                Write-Host "   [EXISTS] Banner already present in README.md." -ForegroundColor DarkGreen
            }
        }

        # 3. Archive repository
        gh repo archive "kzxl/$repo" --yes 2>$null
        Write-Host "   [ARCHIVED] kzxl/$repo archived as Read-Only.`n" -ForegroundColor Cyan
    } catch {
        Write-Warning "   [ERROR] Failed to process kzxl/${repo}: $_"
    }
}

Write-Host "==============================================================================" -ForegroundColor Green
Write-Host " All $total satellite repositories successfully updated and archived!" -ForegroundColor Green
Write-Host "==============================================================================" -ForegroundColor Green
