<#
.SYNOPSIS
    Script khởi chạy các ứng dụng Demo của ZeroUI trong ZeroPlatform.
.DESCRIPTION
    Hỗ trợ khởi chạy nhanh 3 phiên bản demo:
    1. WinForms Controls & Benchmark Demo (ZeroUI.Samples.BenchmarkDemo)
    2. WPF Modern Controls Demo (ZeroUI.Samples.WpfDemo)
    3. Full Ecosystem Showcase: ZeroUI + ZeroGraphics Direct2D/DirectX + ZeroPipeline
.PARAMETER Demo
    Chỉ định loại demo cần chạy: 'winforms', 'wpf', 'showcase'. Nếu để trống sẽ hiển thị menu chọn.
#>

param(
    [ValidateSet('winforms', 'wpf', 'showcase')]
    [string]$Demo,
    [ValidateSet('net8.0-windows', 'net462')]
    [string]$Framework = 'net8.0-windows'
)

$Host.UI.RawUI.WindowTitle = "ZeroUI Demo Runner ⚡"

Write-Host ""
Write-Host "╔════════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║              ZEROUI INTERACTIVE DEMO RUNNER ⚡                ║" -ForegroundColor Cyan
Write-Host "║       Zero-Allocation High-Performance UI for .NET            ║" -ForegroundColor Cyan
Write-Host "╚════════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

$RootDir = $PSScriptRoot
if (-not $RootDir) { $RootDir = Get-Location }

$WinFormsDemoPath = Join-Path $RootDir "ZeroUI\src\ZeroUI.Samples.BenchmarkDemo\ZeroUI.Samples.BenchmarkDemo.csproj"
$WpfDemoPath      = Join-Path $RootDir "ZeroUI\src\ZeroUI.Samples.WpfDemo\ZeroUI.Samples.WpfDemo.csproj"
$ShowcasePath     = Join-Path $RootDir "samples\ZeroPlatform.Samples.Showcase\ZeroPlatform.Samples.Showcase.csproj"

if (-not $Demo) {
    Write-Host "Vui lòng chọn ứng dụng Demo bạn muốn khởi chạy:" -ForegroundColor Yellow
    Write-Host "  [1] ZeroUI WinForms Demo (Controls Showcase & Benchmark)" -ForegroundColor White
    Write-Host "  [2] ZeroUI WPF Demo (Modern WPF Controls & Dark Mode)" -ForegroundColor White
    Write-Host "  [3] ZeroPlatform Full Showcase (ZeroUI + ZeroGraphics Direct2D/DirectX + Pipeline)" -ForegroundColor White
    Write-Host "  [Q] Thoát" -ForegroundColor Gray
    Write-Host ""
    $choice = Read-Host "Nhập lựa chọn (1, 2, 3) [Mặc định: 1]"

    switch ($choice.Trim()) {
        "2" { $Demo = "wpf" }
        "3" { $Demo = "showcase" }
        "q" { exit 0 }
        "Q" { exit 0 }
        default { $Demo = "winforms" }
    }
}

$targetProj = switch ($Demo) {
    "winforms" { $WinFormsDemoPath }
    "wpf"      { $WpfDemoPath }
    "showcase" { $ShowcasePath }
}

if (-not (Test-Path $targetProj)) {
    Write-Host "[-] Không tìm thấy project tại: $targetProj" -ForegroundColor Red
    exit 1
}

Write-Host "[+] Đang khởi chạy: $Demo ($targetProj)..." -ForegroundColor Green
Write-Host "-----------------------------------------------------------------" -ForegroundColor Gray

# Khởi chạy ứng dụng qua dotnet run
if ($Demo -eq "showcase") {
    dotnet run --project "$targetProj" -f $Framework -c Debug
} else {
    dotnet run --project "$targetProj" -c Debug
}
