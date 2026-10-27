# ZeroOcr

[![NuGet Version](https://img.shields.io/badge/nuget-v1.1.0-blue.svg)](https://github.com/kzxl/ZeroOcr)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET Multi-Targeting](https://img.shields.io/badge/.NET-8.0%20%7C%204.6.2%20%7C%20Standard%202.0-purple.svg)](https://dotnet.microsoft.com/)
[![Ecosystem](https://img.shields.io/badge/ZeroPlatform-Tier%203%20Perception-701a75.svg)](../../README.md)

High-performance, zero-allocation sovereign Optical Character Recognition (OCR) abstractions, spatial geometry models, memory-safe image pre-processing, and multi-engine providers (Native Windows WinRT & Edge AI) for .NET.

Part of the **ZeroPlatform** industrial ecosystem (Tier 3: Perception & Intelligence).

---

## Architecture

`ZeroOcr` is architected with a strict separation between core domain contracts and runtime engine providers:

```mermaid
graph TD
    classDef t5 fill:#831843,stroke:#f43f5e,stroke-width:2px,color:#fff;
    classDef t3 fill:#701a75,stroke:#f472b6,stroke-width:2px,color:#fff;
    classDef t1 fill:#1e293b,stroke:#818cf8,stroke-width:2px,color:#fff;

    subgraph Consumers ["Consumers (Tier 5)"]
        Pipe["ZeroPipeline (OcrInspectionNode)"]:::t5
        Docs["ZeroDocuments (ScannedDocReader)"]:::t5
        UI["ZeroUI (OcrRoiBoxControl)"]:::t5
    end

    subgraph ZeroOcr ["ZeroOcr (Tier 3 Perception)"]
        Core["ZeroOcr.Core\n(IOcrEngine, OcrResult, OcrBlock, OcrImageBuffer)"]:::t3
        Win["ZeroOcr.Windows\n(Windows.Media.Ocr / WinRT)"]:::t3
        Onnx["ZeroOcr.Inference\n(Sovereign: DBNet++ & RepSVTR Deep OCR)"]:::t3
    end

    subgraph Foundations ["Foundations (Tier 1 & 0)"]
        Gfx["ZeroGraphics / ZeroTensor / ZeroInference"]:::t1
    end

    Consumers --> Core
    Win --> Core
    Onnx --> Core
    Core --> Gfx
```

### Sub-projects

| Project | Target Frameworks | Responsibilities |
| :--- | :--- | :--- |
| **`ZeroOcr.Core`** | `netstandard2.0`, `net462`, `net8.0` | Pure C# zero-dependency OCR domain models (`OcrResult`, `OcrBlock`, `OcrLine`, `OcrWord`, `OcrRect`, `OcrQuad`), `IOcrEngine` contract, `OcrImageBuffer`, memory-safe image pre-processors (Otsu thresholding, grayscale conversion, inversion, ROI cropping), Table Structure Recognition (TSR), and `MockOcrEngine`. |
| **`ZeroOcr.Inference`** | `netstandard2.0`, `net462`, `net8.0` | Sovereign cross-platform deep learning OCR engine (`ZeroDeepOcrEngine`, `DbNetTextDetector`, `SvtrTextRecognizer`, `QuadPerspectiveTransformer`, `CtcDecoder`). Delivers full Vietnamese diacritics, genuine token confidence scores, ArrayPool tensor pooling, DirectML GPU acceleration, and industrial dot-matrix morphological bridging. |
| **`ZeroOcr.Windows`** | `net8.0-windows10.0.19041.0` | Native Windows 10/11 WinRT fallback OCR implementation via `Windows.Media.Ocr`. Features engine caching per BCP-47 language tag, zero-copy buffer transfer via `SoftwareBitmap`, skew angle detection, and structured token bounding box mapping. |
| **`ZeroOcr.Tests`** | `net8.0-windows10.0.19041.0` | Full unit and integration test suite with synthetic images, spatial geometry validations, deep learning pipeline tests, and engine lifecycles. |

---

## Key Features

- **Hierarchical Document Graph**:
  - `OcrResult` &rarr; `OcrBlock` &rarr; `OcrLine` &rarr; `OcrWord`.
  - Exact bounding boxes (`OcrRect`) and rotated 4-vertex bounding polygons (`OcrQuad`).
  - Word-level and line-level confidence scoring with mean confidence aggregation.
  - Skew/orientation angle detection in degrees.

- **Zero-Allocation Image Buffering & Pooling**:
  - `OcrImageBuffer` wraps contiguous managed/unmanaged byte buffers without LOH heap fragmentation.
  - Native `OcrImageBuffer.Rent(width, height, format)` backed by `ArrayPool<byte>.Shared` for zero GC pressure during 4K/high-res scanning.
  - Supported pixel formats: `Bgra32`, `Rgba32`, `Rgb24`, `Bgr24`, `Gray8`.
  - Zero-copy cropping for Region Of Interest (ROI) scanning.

- **SIMD AVX2 Acceleration & Pre-processing**:
  - Hardware-accelerated vectorized pre-processing kernels (`Vector256<byte>` / AVX2):
    - `OcrPreprocessor.ToGrayscale`: ITU-R BT.601 fixed-point SIMD conversion (`>> 8`).
    - `OcrPreprocessor.Binarize`: Branchless AVX2 SIMD thresholding.
    - `OcrPreprocessor.BinarizeOtsu`: Dynamic bimodal histogram thresholding.
    - `OcrPreprocessor.Invert`: Vectorized negative polarity conversion for inverted text.

- **Packaging & Manufacturing Vision Extensions**:
  - `OcrMorphology`: 3x3 structuring element morphological operations (Dilation, Erosion, Closing) designed to fuse dot-matrix inkjet dots into solid characters for high-accuracy OCR.
  - `ProjectionProfileDeskewer`: Horizontal Projection Profile (HPP) variance maximization algorithm with bilinear deskewing ($\pm 15^\circ$) for angled packaging.
  - `MultiRoiOcrInspectionPipeline`: Concurrent multi-zone inspection with engine pooling and master coordinate remapping.

- **Spatial Analysis & Inspection Judgement**:
  - `OcrKeyValueExtractor`: 2D spatial key-value pair discovery (Right/Below alignment).
  - `OcrPatternMatcher`: Regex extraction for Dates, Tax IDs, LOT/Batch codes, and Amounts.
  - `OcrInspectionJudge`: Industrial Pass/Fail verdict evaluator (Substring, Regex, Expiry Date).

- **Native Windows Engine (`WindowsOcrEngine`)**:
  - Built-in Windows 10/11 WinRT OCR (`Windows.Media.Ocr`) without external model weights or C++ dependencies.
  - Reusable concurrent engine cache (`ConcurrentDictionary<string, OcrEngine>`) to eliminate runtime engine creation overhead.
  - `OcrVisualOverlay`: High-performance GDI+ overlay renderer for bounding boxes, confidence tags, and PASS/FAIL badges.

---

## Quick Example

### 1. Recognizing Text from Image Buffer
```csharp
using ZeroOcr.Core.Imaging;
using ZeroOcr.Core.Interfaces;
using ZeroOcr.Windows;

IOcrEngine ocr = new WindowsOcrEngine();

if (ocr.IsAvailable)
{
    // Raw BGRA32 pixel buffer (e.g. from camera frame or screen capture)
    var imageBuffer = OcrImageBuffer.FromBgra32(width, height, rawPixelBytes);

    var options = new OcrOptions
    {
        LanguageTag = "en-US",
        MinConfidence = 0.8f
    };

    var result = await ocr.RecognizeAsync(imageBuffer, options);

    if (result.Success)
    {
        Console.WriteLine($"Recognized {result.Lines.Count} lines in {result.Elapsed.TotalMilliseconds:F1}ms:");
        foreach (var line in result.Lines)
        {
            Console.WriteLine($"[{line.BoundingBox}] {line.Text} (Conf: {line.Confidence:P0})");
        }
    }
}
```

### 2. Cropping Region of Interest (ROI)
```csharp
// Inspect only a specific sub-region (e.g., product barcode / date code zone)
var roi = new OcrRect(x: 100, y: 50, width: 300, height: 80);
var options = new OcrOptions { RegionOfInterest = roi };

var result = await ocr.RecognizeAsync(imageBuffer, options);
```

---

---

## Target Frameworks

- `.NET Standard 2.0` (Core Abstractions & Inference)
- `.NET Framework 4.6.2` (Core Abstractions & Inference)
- `.NET 8.0` (Core Abstractions & Inference)
- `.NET 8.0-windows10.0.19041.0` (Native Windows Engine & Tests)

---

## 📅 Version History & Milestones

| Version | Release Date | Key Milestones & Highlights |
| :--- | :---: | :--- |
| **`v1.1.0`** | 2026-10-05 | **Sovereign Deep OCR, Table Structure Recognition (TSR) & Multi-Framework Support**:<br/>• **Table Structure Recognition (TSR)**: Extract tabular grid structures directly from OCR results with export to `System.Data.DataTable` and Markdown.<br/>• **Multi-Targeting Expansion**: Added native `.NET Framework 4.6.2` (`net462`) target for both `ZeroOcr.Core` and `ZeroOcr.Inference`.<br/>• **DirectML GPU Acceleration & Tensor Pooling**: `ZeroDeepOcrEngine` integration with `ArrayPool` tensor pooling and DirectML session provider.<br/>• **Vietnamese Diacritics Accuracy**: Enhanced CTC decoder with blank token alignment and real-world benchmark verification.<br/>• **Adaptive Hybrid PDF OCR**: High-accuracy vector text extraction with fallback to raster OCR. |
| **`v1.0.0`** | 2026-09-20 | **Initial Industrial Release**:<br/>• Core domain abstractions (`IOcrEngine`, `OcrResult`, `OcrBlock`, `OcrLine`, `OcrWord`, `OcrRect`, `OcrQuad`).<br/>• Native Windows WinRT fallback engine (`WindowsOcrEngine`).<br/>• Memory-safe image pre-processing (Otsu thresholding, ROI cropping, grayscale conversion). |

---

## License

MIT License. Copyright © 2026 Phong Võ.  
Part of the ZeroPlatform Industrial Ecosystem.
