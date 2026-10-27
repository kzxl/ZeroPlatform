param(
    [string]$Configuration = "Release",
    [string]$OutputDir = "nupkgs"
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "    Building & Packing ZeroRfid NuGet   " -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

if (Test-Path $OutputDir) {
    Remove-Item -Recurse -Force $OutputDir
}

$projects = @(
    "src/ZeroRfid.Core/ZeroRfid.Core.csproj",
    "src/ZeroRfid.Adapters.Abstractions/ZeroRfid.Adapters.Abstractions.csproj",
    "src/ZeroRfid.Pipeline/ZeroRfid.Pipeline.csproj",
    "src/ZeroRfid.Simulator/ZeroRfid.Simulator.csproj"
)

foreach ($proj in $projects) {
    Write-Host "`n📦 Packing $proj..." -ForegroundColor Green
    dotnet pack $proj -c $Configuration -o $OutputDir
    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ Failed to pack $proj" -ForegroundColor Red
        exit 1
    }
}

Write-Host "`n✅ Packaging succeeded! Packages in ${OutputDir}:" -ForegroundColor Green
Get-ChildItem -Path $OutputDir -Filter *.nupkg | ForEach-Object {
    Write-Host "   📦 $($_.Name) ($([math]::Round($_.Length / 1KB, 2)) KB)" -ForegroundColor Yellow
    Write-Host "      Path: $($_.FullName)" -ForegroundColor DarkGray
}
