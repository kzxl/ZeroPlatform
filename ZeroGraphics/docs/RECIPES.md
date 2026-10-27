# ZeroGraphics: Developer Recipes & Code Cookbook

A collection of copy-paste ready, production-grade code snippets demonstrating the capabilities of ZeroGraphics.

---

## 1. Real-Time Oscilloscope Telemetry (WinForms)

```csharp
using System.Drawing;
using System.Windows.Forms;
using ZeroGraphics.Waveform.Controls;
using ZeroGraphics.Waveform.Pipeline;

var oscilloscope = new ZeroWaveformCanvas
{
    Dock = DockStyle.Fill,
    BackColor = Color.FromArgb(13, 17, 23),
    TraceColor = Color.FromArgb(16, 185, 129), // Phosphor emerald
    DecimationMode = WaveformDecimationMode.MinMax,
    AutoScale = true
};
this.Controls.Add(oscilloscope);

// Push multi-million point array directly to GPU
float[] sensorData = GetSensorReadings(); // 1,000,000 points
oscilloscope.SetData(sensorData);
```

---

## 2. Hardware-Accelerated SDF Rounded Card with Glow

```csharp
using System.Drawing;
using System.Windows.Forms;
using ZeroGraphics.DirectX.Controls;

var card = new ZeroDirectXCanvas
{
    Dock = DockStyle.None,
    Size = new Size(320, 180),
    Elevation = 10f,
    BlurRadius = 20f,
    CornerRadius = 14f,
    BorderWidth = 1.5f,
    GlowIntensity = 0.8f,
    GlowColor = Color.FromArgb(6, 182, 212),     // Cyan bloom
    CardColor = Color.FromArgb(22, 27, 38),     // Obsidian dark
    CardBorderColor = Color.FromArgb(56, 189, 248)
};
this.Controls.Add(card);
```

---

## 3. Direct2D & DirectWrite Crisp Typography

```csharp
using System.Drawing;
using System.Windows.Forms;
using ZeroGraphics.Direct2D.Controls;

var canvas2D = new ZeroDirect2DCanvas
{
    Dock = DockStyle.Fill,
    TextFontFamily = "Segoe UI",
    TextSize = 18f,
    SampleText = "ZeroGraphics DirectWrite: Subpixel ClearType & Per-Monitor V2 DPI"
};
this.Controls.Add(canvas2D);
```

---

## 4. Standalone Peak-Preserving MinMax Decimation

```csharp
using System;
using ZeroGraphics.Core.Data;

// Raw high-frequency signal (e.g., 500,000 samples)
TimePoint[] rawSeries = FetchTelemetryBuffer();

// Downsample to 1,000 display buckets for standard GDI+ or UI views
TimePoint[] displayPoints = new TimePoint[1000];
int count = MinMaxDecimation.Downsample(rawSeries, displayPoints, 1000);

// displayPoints preserves 100% of minimums, maximums, and transient spikes!
```

---

## 5. Headless Direct2D Offscreen Rendering (WebAPI / Background Service)

```csharp
using System.Drawing;
using ZeroGraphics.Direct2D.Core;

// Initialize headless offscreen target (e.g. 1920x1080 Full HD)
using (var offscreen = new D2DOffscreenTarget(1920, 1080))
{
    offscreen.Render(rt =>
    {
        rt.Clear(Color.FromArgb(20, 24, 33)); // Obsidian dark
        using (var brush = rt.CreateSolidColorBrush(Color.FromArgb(0, 200, 115)))
        {
            rt.FillRoundedRectangle(50, 50, 800, 400, 16f, brush);
        }
    });

    // Save directly to file, stream, or byte[] for ASP.NET WebAPI responses
    offscreen.SaveToPng(@"C:\Reports\shift_chart.png");
}
```

---

## 6. High-Performance Spatial Indexing & Viewport Culling

```csharp
using System.Collections.Generic;
using ZeroGraphics.Core.Spatial;

// Floor dimensions: 10,000m x 10,000m
var floorBounds = new BoundingBox2D(0, 0, 10000, 10000);
var quadTree = new QuadTree<StorageRack>(floorBounds, maxItemsPerNode: 16);

// Populate racks
foreach (var rack in warehouseDatabase.GetRacks())
{
    quadTree.Insert(rack);
}

// Interactive Pan/Zoom: Cull 99% of unseen items in < 0.1ms
var viewport = new BoundingBox2D(viewX, viewY, viewX + screenWidth, viewY + screenHeight);
var visibleRacks = new List<StorageRack>();
quadTree.Query(viewport, visibleRacks);
```

---

## 7. Statistical Process Control (SPC) & Quality Control Engine

```csharp
using ZeroGraphics.Core.Analytics;

float[] sensorSamples = GetDiameterMeasurements(); // e.g. 100 samples
float usl = 10.05f; // Upper Spec Limit
float lsl = 9.95f;  // Lower Spec Limit

// 1. Calculate statistical metrics & process capability (Cp, Cpk)
SpcSummary summary = SpcAnalysis.Calculate(sensorSamples, usl, lsl);
Console.WriteLine($"Mean: {summary.Mean:F3}, UCL: {summary.UCL:F3}, LCL: {summary.LCL:F3}, Cpk: {summary.Cpk:F3}");

// 2. Automated defect rule evaluation
var alarms = SpcRuleEngine.EvaluateRules(sensorSamples, summary);
foreach (var alarm in alarms)
{
    Console.WriteLine($"[ALERT] {alarm.Severity}: {alarm.Message}");
}
```

---

## 8. Industrial Vision & Image Processing Pipeline

```csharp
using System.Drawing;
using ZeroGraphics.Imaging.Core;
using ZeroGraphics.Imaging.Filters;

// 1. Wrap camera frame without copy overhead
using (Bitmap cameraBitmap = GetLiveInspectionFrame())
using (var src = ImageBuffer.FromBitmap(cameraBitmap))
using (var gray = ImageBuffer.CreateGray8(src.Width, src.Height))
using (var binary = ImageBuffer.CreateGray8(src.Width, src.Height))
using (var edges = ImageBuffer.CreateGray8(src.Width, src.Height))
{
    // 2. High-speed ITU-R BT.709 Grayscale conversion
    ColorTransform.ToGrayscale(src, gray);

    // 3. Automated Otsu Binarization for defect extraction
    byte threshold = Thresholding.OtsuBinarize(gray, binary);

    // 4. Mathematical Morphology: Eliminate salt noise via Opening
    Morphology.Open(binary, binary, radius: 1);

    // 5. Unrolled Sobel Gradient Edge Detection
    ConvolutionFilters.SobelEdgeDetection(gray, edges);

    // 6. Export back to Bitmap or Stream for UI display
    using (Bitmap resultBmp = binary.ToBitmap())
    {
        inspectionPictureBox.Image = (Bitmap)resultBmp.Clone();
    }
}
```

---

## 9. GPU Image Pipeline & Multi-Pass Operation Fusion (Fluent API)

```csharp
using ZeroGraphics.Imaging.Core;
using ZeroGraphics.Imaging.Gpu;

// 1. Initialize GPU context (Direct3D 11 device, texture pool, shaders)
using (var context = GpuImageContext.CreateDefault())
{
    // 2. Build execution graph via fluent API
    // Consecutive operations (Resize -> ColorAdjust -> Sharpen -> Threshold)
    // are automatically fused into a single PS_Fused pass!
    var pipeline = new ImagePipelineBuilder(context)
        .AddResize(640, 480, bilinear: true)
        .AddColorAdjust(brightness: 0.1f, contrast: 1.25f, grayscale: true)
        .AddSharpen(strength: 1.2f)
        .AddThreshold(cutoff: 0.45f)
        .Compile();

    Console.WriteLine($"Original passes: {pipeline.OriginalPassCount}, Optimized passes: {pipeline.OptimizedPassCount}");

    // Option: Zero-Copy Host -> GPU -> Host roundtrip
    using (var src = ImageBuffer.CreateBgra32(1920, 1080))
    using (var dst = pipeline.Execute(src))
    {
        Console.WriteLine($"Output size: {dst.Width}x{dst.Height}");
    }
}
```

---

## 10. Industrial Machine Vision: NCC Matching, 1D Caliper & Blob Analysis

```csharp
using System;
using ZeroGraphics.Imaging.Core;
using ZeroGraphics.Vision.Blob;
using ZeroGraphics.Vision.Matching;
using ZeroGraphics.Vision.Metrology;

// 1. Sub-Pixel Normalized Cross-Correlation (NCC) Pattern Search
using (var cameraFrame = ImageBuffer.CreateGray8(1920, 1080))
using (var goldenTemplate = ImageBuffer.CreateGray8(64, 64))
{
    var match = NccTemplateMatcher.Match(cameraFrame, goldenTemplate, minScore: 0.85, subPixelRefinement: true);
    if (match.IsFound)
    {
        Console.WriteLine($"Fiducial located at ({match.CenterX:F2}, {match.CenterY:F2}) with confidence {match.Score:F4}");
    }
}

// 2. 1D Sub-Pixel Edge Caliper & Geometric Line Fitting
var edge = EdgeCaliper1D.FindStrongestEdge(cameraFrame, x1: 100, y1: 50, x2: 100, y2: 450, minMagnitude: 25.0);
if (edge.HasValue)
{
    Console.WriteLine($"Boundary edge at ({edge.Value.X:F3}, {edge.Value.Y:F3}), gradient={edge.Value.Magnitude:F1}");
}

// 3. Automated 8-Way Connected Component Blob Analysis
var blobs = BlobAnalyzer.ExtractBlobs(cameraFrame, threshold: 140, minArea: 50);
foreach (var blob in blobs)
{
    Console.WriteLine($"Part #{blob.Id}: Area={blob.Area} px, Center=({blob.CentroidX:F1}, {blob.CentroidY:F1}), Circ={blob.Circularity:F3}");
}
```

---

## 11. Industrial 1D/2D Barcodes & Reed-Solomon Error Correction

```csharp
using ZeroGraphics.Vision.Codes;

// 1. Decode Code 128 barcode
string scannedText = Code128Decoder.Decode(barcodePixelPattern);
Console.WriteLine($"Scanned Serial: {scannedText}");

// 2. Decode DataMatrix ECC200 with Reed-Solomon Error Correction
var dmResult = DataMatrixDecoder.Decode(imageBuffer);
if (dmResult.Success)
{
    Console.WriteLine($"Decoded DPM: {dmResult.Payload} (Fixed {dmResult.CorrectedErrors} byte errors)");
}
```

---

## 12. Multi-View Homography & Perspective Stitching

```csharp
using ZeroGraphics.Vision.Stitching;

Point2D[] srcQuad = { new(0, 0), new(640, 0), new(640, 480), new(0, 480) };
Point2D[] dstQuad = { new(50, 80), new(600, 30), new(620, 450), new(20, 420) };
var H = Homography2D.Estimate(srcQuad, dstQuad);

using (var rectified = PerspectiveWarper.Warp(cameraFrame, H, outputWidth: 800, outputHeight: 600))
{
    rectified.SaveToPng(@"C:\Inspection\rectified_part.png");
}
```

---

## 13. CIEDE2000 ($\Delta E_{00}$) Industrial Color Tolerance Metrology

```csharp
using ZeroGraphics.Imaging.ColorScience;

CieLabColor nominalColor = ColorTransform.RgbToLab(220, 180, 50);
CieLabColor sampleColor  = ColorTransform.RgbToLab(218, 178, 52);

double deltaE = ColorDifference.Ciede2000(nominalColor, sampleColor);
bool isPass = deltaE < 1.5; // Automotive/Paint tolerance
Console.WriteLine($"Color Difference: ΔE00 = {deltaE:F2} -> {(isPass ? "PASS" : "FAIL")}");
```

---

## 14. RANSAC Robust Geometric Fitting & Oriented Bounding Box (OBB)

```csharp
using ZeroGraphics.Vision.Metrology;

// 1. Robust line fit rejecting outliers (dust, scratches)
Point2D[] noisyEdgePoints = FetchEdgePoints();
var lineModel = RansacFitter.FitLine(noisyEdgePoints, distanceThreshold: 1.5, maxIterations: 100);

// 2. Minimum-area Oriented Bounding Box (OBB) for component orientation
var hull = ConvexHull2D.Compute(noisyEdgePoints);
RotatedRect2D obb = RotatedRect2D.ComputeMinimumAreaBoundingBox(hull);
Console.WriteLine($"Part Orientation: Center=({obb.Center.X:F1}, {obb.Center.Y:F1}), Size={obb.Width:F1}x{obb.Height:F1}, Angle={obb.AngleDegrees:F2}°");
```
