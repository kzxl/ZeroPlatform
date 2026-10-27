Add-Type -AssemblyName System.Drawing

$outputDir = "e:\15. Other\dotnet\libs\ZeroUI\src\ZeroUI.WinForms\Icons"
if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
}

function New-ToolboxIcon {
    param(
        [string]$name,
        [scriptblock]$drawAction
    )
    $bmp = New-Object System.Drawing.Bitmap 16, 16, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::None
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
    $g.Clear([System.Drawing.Color]::Transparent)

    & $drawAction $g

    $g.Dispose()
    $filePath = Join-Path $outputDir "$name.bmp"
    $bmp.Save($filePath, [System.Drawing.Imaging.ImageFormat]::Bmp)
    $bmp.Dispose()
    Write-Host "Generated $name.bmp"
}

# 1. ZeroDefaultControl - Electric Lightning ⚡
New-ToolboxIcon "ZeroDefaultControl" {
    param($g)
    $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(2, 132, 199)), 1
    $brush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(14, 165, 233))
    $points = @(
        New-Object System.Drawing.Point 9, 1
        New-Object System.Drawing.Point 4, 8
        New-Object System.Drawing.Point 8, 8
        New-Object System.Drawing.Point 6, 14
        New-Object System.Drawing.Point 12, 7
        New-Object System.Drawing.Point 8, 7
    )
    $g.FillPolygon($brush, $points)
    $g.DrawPolygon($pen, $points)
}

# 2. ZeroGridControl - Virtual Big Data Grid 🗗
New-ToolboxIcon "ZeroGridControl" {
    param($g)
    $borderPen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(30, 41, 59)), 1
    $headerBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(14, 165, 233))
    $rowBrush1 = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::White)
    $rowBrush2 = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(241, 245, 249))
    $gridPen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(203, 213, 225)), 1

    $g.FillRectangle($headerBrush, 1, 1, 13, 4)
    $g.FillRectangle($rowBrush1, 1, 5, 13, 4)
    $g.FillRectangle($rowBrush2, 1, 9, 13, 5)
    $g.DrawRectangle($borderPen, 1, 1, 13, 13)
    $g.DrawLine($gridPen, 1, 5, 14, 5)
    $g.DrawLine($gridPen, 1, 9, 14, 9)
    $g.DrawLine($gridPen, 6, 1, 6, 14)
    $g.DrawLine($gridPen, 10, 1, 10, 14)
}

# 3. ZeroButton - Modern Pill Button
New-ToolboxIcon "ZeroButton" {
    param($g)
    $brush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(59, 130, 246))
    $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(29, 78, 216)), 1
    $textBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::White)

    $g.FillRectangle($brush, 2, 4, 12, 8)
    $g.DrawRectangle($pen, 2, 4, 12, 8)
    $g.FillRectangle($textBrush, 5, 7, 6, 2)
}

# 4. ZeroDatePicker - Calendar with Event
New-ToolboxIcon "ZeroDatePicker" {
    param($g)
    $headerBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(239, 68, 68))
    $bodyBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::White)
    $borderPen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(71, 85, 105)), 1
    $gridPen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(148, 163, 184)), 1
    $activeBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(14, 165, 233))

    $g.FillRectangle($bodyBrush, 2, 4, 12, 10)
    $g.FillRectangle($headerBrush, 2, 2, 12, 3)
    $g.DrawRectangle($borderPen, 2, 2, 12, 12)
    $g.FillRectangle($activeBrush, 8, 8, 3, 3)
    $g.DrawLine($borderPen, 5, 1, 5, 3)
    $g.DrawLine($borderPen, 11, 1, 11, 3)
}

# 5. ZeroSwitch - Modern Toggle Switch
New-ToolboxIcon "ZeroSwitch" {
    param($g)
    $trackBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(16, 185, 129))
    $trackPen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(5, 150, 105)), 1
    $thumbBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::White)
    $thumbPen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(203, 213, 225)), 1

    $g.FillRectangle($trackBrush, 2, 5, 12, 6)
    $g.DrawRectangle($trackPen, 2, 5, 12, 6)
    $g.FillRectangle($thumbBrush, 8, 4, 6, 8)
    $g.DrawRectangle($thumbPen, 8, 4, 6, 8)
}

# 6. ZeroIndustrialPump - Centrifugal Pump ⚙️
New-ToolboxIcon "ZeroIndustrialPump" {
    param($g)
    $bodyBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(37, 99, 235))
    $bodyPen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(30, 58, 138)), 1
    $flangeBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(148, 163, 184))
    $centerBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(250, 204, 21))

    $g.FillEllipse($bodyBrush, 3, 3, 10, 10)
    $g.DrawEllipse($bodyPen, 3, 3, 10, 10)
    $g.FillRectangle($flangeBrush, 11, 6, 4, 4)
    $g.FillRectangle($flangeBrush, 6, 0, 4, 4)
    $g.FillEllipse($centerBrush, 6, 6, 4, 4)
}

# 7. ZeroIndustrialMotor - 3-Phase Induction Motor
New-ToolboxIcon "ZeroIndustrialMotor" {
    param($g)
    $bodyBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(71, 85, 105))
    $bodyPen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(15, 23, 42)), 1
    $shaftBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(203, 213, 225))
    $baseBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(30, 41, 59))

    $g.FillRectangle($bodyBrush, 3, 3, 8, 9)
    $g.DrawRectangle($bodyPen, 3, 3, 8, 9)
    # Fins
    $finPen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(100, 116, 139)), 1
    $g.DrawLine($finPen, 4, 5, 9, 5)
    $g.DrawLine($finPen, 4, 7, 9, 7)
    $g.DrawLine($finPen, 4, 9, 9, 9)
    # Shaft
    $g.FillRectangle($shaftBrush, 11, 6, 4, 3)
    # Mount base
    $g.FillRectangle($baseBrush, 2, 12, 10, 3)
}

# 8. ZeroIndustrialValve - Bowtie Control Valve
New-ToolboxIcon "ZeroIndustrialValve" {
    param($g)
    $bodyBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(239, 68, 68))
    $bodyPen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(185, 28, 28)), 1
    $actuatorBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(100, 116, 139))

    # Actuator diaphragm
    $g.FillRectangle($actuatorBrush, 5, 1, 6, 3)
    $g.DrawLine($bodyPen, 8, 4, 8, 7)
    # Left cone
    $leftPts = @(
        New-Object System.Drawing.Point 2, 7
        New-Object System.Drawing.Point 8, 10
        New-Object System.Drawing.Point 2, 13
    )
    $g.FillPolygon($bodyBrush, $leftPts)
    $g.DrawPolygon($bodyPen, $leftPts)
    # Right cone
    $rightPts = @(
        New-Object System.Drawing.Point 14, 7
        New-Object System.Drawing.Point 8, 10
        New-Object System.Drawing.Point 14, 13
    )
    $g.FillPolygon($bodyBrush, $rightPts)
    $g.DrawPolygon($bodyPen, $rightPts)
}

# 9. ZeroTank3D - Cylindrical Vessel with Liquid
New-ToolboxIcon "ZeroTank3D" {
    param($g)
    $tankBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(226, 232, 240))
    $tankPen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(71, 85, 105)), 1
    $liquidBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(14, 165, 233))
    $gaugePen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(239, 68, 68)), 1

    $g.FillRectangle($tankBrush, 3, 2, 10, 12)
    $g.FillRectangle($liquidBrush, 3, 7, 10, 7)
    $g.DrawRectangle($tankPen, 3, 2, 10, 12)
    # Sight glass tube
    $g.DrawLine($gaugePen, 11, 4, 11, 12)
}

# 10. ZeroGauge - Circular Dial Instrument
New-ToolboxIcon "ZeroGauge" {
    param($g)
    $faceBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(248, 250, 252))
    $rimPen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(51, 65, 85)), 1
    $needlePen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(220, 38, 38)), 1
    $zonePen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(16, 185, 129)), 1

    $g.FillEllipse($faceBrush, 1, 1, 13, 13)
    $g.DrawEllipse($rimPen, 1, 1, 13, 13)
    $g.DrawArc($zonePen, 3, 3, 9, 9, 135, 180)
    # Needle pointing upper-right
    $g.DrawLine($needlePen, 7, 7, 11, 4)
    $hubBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::Black)
    $g.FillEllipse($hubBrush, 6, 6, 3, 3)
}

# 11. ZeroTrendChart - Oscilloscope Telemetry
New-ToolboxIcon "ZeroTrendChart" {
    param($g)
    $bgBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(15, 23, 42))
    $borderPen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(51, 65, 85)), 1
    $wavePen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(34, 197, 94)), 1
    $gridPen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(30, 41, 59)), 1

    $g.FillRectangle($bgBrush, 1, 2, 14, 12)
    $g.DrawRectangle($borderPen, 1, 2, 14, 12)
    $g.DrawLine($gridPen, 1, 8, 14, 8)
    $pts = @(
        New-Object System.Drawing.Point 2, 10
        New-Object System.Drawing.Point 5, 5
        New-Object System.Drawing.Point 8, 11
        New-Object System.Drawing.Point 11, 4
        New-Object System.Drawing.Point 14, 7
    )
    $g.DrawLines($wavePen, $pts)
}

# 12. ZeroAlarmGrid - Warning Alert Triangle
New-ToolboxIcon "ZeroAlarmGrid" {
    param($g)
    $brush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(245, 158, 11))
    $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(180, 83, 9)), 1
    $markPen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(15, 23, 42)), 1

    $tri = @(
        New-Object System.Drawing.Point 8, 1
        New-Object System.Drawing.Point 1, 14
        New-Object System.Drawing.Point 15, 14
    )
    $g.FillPolygon($brush, $tri)
    $g.DrawPolygon($pen, $tri)
    # Exclamation mark
    $g.DrawLine($markPen, 8, 6, 8, 10)
    $dotBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(15, 23, 42))
    $g.FillRectangle($dotBrush, 7, 12, 2, 2)
}

# 13. ZeroLedTower - Andon Light Tower
New-ToolboxIcon "ZeroLedTower" {
    param($g)
    $redBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(239, 68, 68))
    $yellowBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(245, 158, 11))
    $greenBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(16, 185, 129))
    $stemPen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(100, 116, 139)), 1
    $borderPen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(15, 23, 42)), 1

    $g.FillRectangle($redBrush, 5, 1, 6, 3)
    $g.DrawRectangle($borderPen, 5, 1, 6, 3)
    $g.FillRectangle($yellowBrush, 5, 4, 6, 3)
    $g.DrawRectangle($borderPen, 5, 4, 6, 3)
    $g.FillRectangle($greenBrush, 5, 7, 6, 3)
    $g.DrawRectangle($borderPen, 5, 7, 6, 3)
    # Pole and base
    $g.DrawLine($stemPen, 8, 10, 8, 14)
    $g.DrawLine($borderPen, 4, 14, 12, 14)
}

# 14. ZeroWarehouseRack - Logistics Rack
New-ToolboxIcon "ZeroWarehouseRack" {
    param($g)
    $framePen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(51, 65, 85)), 1
    $bin1 = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(59, 130, 246))
    $bin2 = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(16, 185, 129))
    $bin3 = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(245, 158, 11))

    # Rack uprights & beams
    $g.DrawLine($framePen, 2, 1, 2, 15)
    $g.DrawLine($framePen, 13, 1, 13, 15)
    $g.DrawLine($framePen, 2, 6, 13, 6)
    $g.DrawLine($framePen, 2, 11, 13, 11)
    $g.DrawLine($framePen, 2, 15, 13, 15)
    # Pallet boxes
    $g.FillRectangle($bin1, 4, 2, 4, 4)
    $g.FillRectangle($bin2, 9, 2, 3, 4)
    $g.FillRectangle($bin3, 4, 7, 7, 4)
}

# 15. ZeroTreeList - Multi-Level BOM Tree
New-ToolboxIcon "ZeroTreeList" {
    param($g)
    $treePen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(100, 116, 139)), 1
    $folderBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(245, 158, 11))
    $leafBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(14, 165, 233))

    $g.FillRectangle($folderBrush, 2, 2, 5, 4)
    $g.DrawLine($treePen, 4, 6, 4, 12)
    $g.DrawLine($treePen, 4, 9, 7, 9)
    $g.DrawLine($treePen, 4, 13, 7, 13)
    $g.FillRectangle($leafBrush, 8, 7, 5, 3)
    $g.FillRectangle($leafBrush, 8, 11, 5, 3)
}

# 16. ZeroHeatmap - 2D Thermal Matrix
New-ToolboxIcon "ZeroHeatmap" {
    param($g)
    $b1 = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(37, 99, 235))
    $b2 = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(16, 185, 129))
    $b3 = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(245, 158, 11))
    $b4 = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(239, 68, 68))

    $g.FillRectangle($b1, 2, 2, 5, 5)
    $g.FillRectangle($b2, 9, 2, 5, 5)
    $g.FillRectangle($b3, 2, 9, 5, 5)
    $g.FillRectangle($b4, 9, 9, 5, 5)
}

# 17. ZeroBarcodeScanControl - Scanner Beam
New-ToolboxIcon "ZeroBarcodeScanControl" {
    param($g)
    $barBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(30, 41, 59))
    $laserPen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(239, 68, 68)), 1

    # Vertical barcode lines
    $g.FillRectangle($barBrush, 2, 2, 2, 12)
    $g.FillRectangle($barBrush, 5, 2, 1, 12)
    $g.FillRectangle($barBrush, 7, 2, 3, 12)
    $g.FillRectangle($barBrush, 11, 2, 1, 12)
    $g.FillRectangle($barBrush, 13, 2, 2, 12)
    # Red laser cross line
    $g.DrawLine($laserPen, 0, 8, 15, 8)
}

# 18. ZeroSevenSegment - Digital 8 Display
New-ToolboxIcon "ZeroSevenSegment" {
    param($g)
    $bgBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(15, 23, 42))
    $segPen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(34, 197, 94)), 1

    $g.FillRectangle($bgBrush, 2, 1, 12, 14)
    $g.DrawRectangle($segPen, 4, 3, 8, 4)
    $g.DrawRectangle($segPen, 4, 7, 8, 5)
}

# 19. ZeroChart - Business Analytics Chart
New-ToolboxIcon "ZeroChart" {
    param($g)
    $axisPen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(71, 85, 105)), 1
    $c1 = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(59, 130, 246))
    $c2 = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(16, 185, 129))
    $c3 = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(245, 158, 11))
    $linePen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(239, 68, 68)), 1

    $g.DrawLine($axisPen, 2, 1, 2, 14)
    $g.DrawLine($axisPen, 2, 14, 15, 14)
    $g.FillRectangle($c1, 4, 9, 2, 5)
    $g.FillRectangle($c2, 8, 5, 2, 9)
    $g.FillRectangle($c3, 12, 2, 2, 12)
    $g.DrawLine($linePen, 5, 8, 9, 4)
    $g.DrawLine($linePen, 9, 4, 13, 2)
}

# 20. ZeroKanbanBoard - Shopfloor Kanban
New-ToolboxIcon "ZeroKanbanBoard" {
    param($g)
    $boardPen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(100, 116, 139)), 1
    $card1 = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(239, 68, 68))
    $card2 = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(245, 158, 11))
    $card3 = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(16, 185, 129))

    $g.DrawRectangle($boardPen, 1, 1, 13, 13)
    $g.DrawLine($boardPen, 5, 1, 5, 14)
    $g.DrawLine($boardPen, 10, 1, 10, 14)
    $g.FillRectangle($card1, 2, 3, 2, 3)
    $g.FillRectangle($card1, 2, 7, 2, 3)
    $g.FillRectangle($card2, 6, 4, 3, 4)
    $g.FillRectangle($card3, 11, 5, 2, 4)
}

Write-Host "All icons generated successfully!"
