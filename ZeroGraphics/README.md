# ZeroGraphics ⚡

> **Ultra-High-Performance, Zero-External-Dependency GPU Acceleration Engine for .NET (WinForms, WPF & Headless)**

[![ZeroPlatform Tier](https://img.shields.io/badge/ZeroPlatform-Tier%204%20(Graphics%20%26%20Spatial%203D)-ea580c.svg)](https://github.com/kzxl/ZeroPlatform)
[![NuGet - ZeroGraphics.Core](https://img.shields.io/badge/nuget-ZeroGraphics.Core%20v1.7.0-blue.svg)](https://www.nuget.org/packages/ZeroGraphics.Core/1.7.0)
[![Unit Tests](https://img.shields.io/badge/tests-270%20passed%20(100%25)-brightgreen.svg)](#-automated-testing--verification)
[![Target Frameworks](https://img.shields.io/badge/targets-netstandard2.0%20%7C%20net462%20%7C%20net8.0--windows-blue.svg)](#-package-matrix)
[![Input Latency](https://img.shields.io/badge/Input%20Latency-%3C%201%20Frame%20(~4ms)-brightgreen.svg)](docs/BENCHMARKS.md)
[![Stream Capacity](https://img.shields.io/badge/Streaming-10M%2B%20Points%20%40%20144Hz-purple.svg)](docs/BENCHMARKS.md)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](#-license)

---

## 📖 Executive Summary

**ZeroGraphics** is a sovereign graphics, industrial computer vision, and computational photography suite engineered in 100% pure C#. It provides hardware-accelerated rendering, oscilloscope waveform streaming, multi-scale image pyramids, HDR exposure fusion, color science, cross-platform Vulkan 1.0+ RHI hardware acceleration, and automated machine vision without relying on heavyweight third-party wrappers like SharpDX, Silk.NET, or OpenCV.

Operating as a foundational member of **Tier 4 (Graphics & Spatial 3D)** within the **[ZeroPlatform](https://github.com/kzxl/ZeroPlatform)** ecosystem.

---

## 🏛️ Ecosystem Architectural Alignment

- **Architectural Tier**: **Tier 4 (Graphics & Spatial 3D)**
- **Permitted Upstream Dependencies**: Tier 0 (`ZeroPrimitives`, `ZeroConcurrency`, `ZeroSecurity`), Tier 1 (`ZeroCompute`, `ZeroTensor`), Tier 2 (`ZeroData`, `ZeroStorage`), Tier 3 (`ZeroVideo`, `ZeroGeometry`)
- **Downstream Consumers**: Tier 4 (`ZeroCharts`, `ZeroTwin3D`), Tier 5 (`ZeroUI`, `ZeroPipeline`, `ZeroReports`)
- **Core Guarantees**: Pure COM VTable interop, Zero external wrappers, Hardware Flip presentation, Sub-4ms frame latency, 0% CPU at idle.

---

### Core Architectural Pillars
- **Cross-Platform Vulkan RHI (`VulkanRhiDevice`)**: Pure C# dynamic loader binding to Vulkan 1.0+ on Linux and Windows, enabling hardware-accelerated graphics pipelines beyond Windows DirectX.
- **Pure COM VTable Interop**: Direct3D 11, DXGI, Direct2D, and DirectWrite invoked directly via pre-indexed COM VTable pointers in pure C# (0 external dependencies).
- **Graphics Interception & Detours**: Pure C# VTable hooking engine (`ComVTableHook`, `D3D11GraphicsInterceptor`) for `Present` interception, draw call counting, backbuffer capture, and runtime shader overriding.
- **Modern Flip Model (`DXGI_SWAP_EFFECT_FLIP_DISCARD`)**: Direct hardware flip to HWND eliminating DWM redirection copy stalls (0% CPU at idle).
- **Sub-4ms Input Latency**: Hardware-enforced `SetMaximumFrameLatency(1)` reducing input lag by ~67%.
- **Massive Waveform Streaming**: 10,000,000+ points rendered at 144+ FPS via dynamic `D3D11_MAP_WRITE_DISCARD` buffer renaming.
- **Self-Healing Device Recovery**: Transparent recovery from GPU driver crashes and resets (`DXGI_ERROR_DEVICE_REMOVED`, `D2DERR_RECREATE_TARGET`).
- **GPU Render Graph & Operation Fusion**: 13 precompiled HLSL kernels with automatic multi-pass fusion reducing VRAM bandwidth by up to 75%.
- **Color Science & Standardized Spaces**: Standardized RGB color spaces (sRGB, Adobe RGB, Display P3, Rec. 2020, ProPhoto RGB) with D65/D50 white point chromaticities, linear light conversions, and Bradford chromatic adaptation matrices.
- **Computational Photography & Multi-Scale Fusion**: Gaussian/Laplacian image pyramids, Mertens multi-exposure HDR fusion, multi-band focus stacking, À-Trous $B_3$-spline wavelets, fast marching inpainting, $O(1)$ fast guided filtering, and Minkowski Gray-Edge AWB.

---

## 📚 Technical Documentation & Guides

Comprehensive technical details and guides are modularized within the [`docs/`](docs/) directory:

| Document | Description |
| :--- | :--- |
| 📊 **[Verified Benchmarks](docs/BENCHMARKS.md)** | FPS, CPU load, input latency, and decimation throughput vs GDI+ and SkiaSharp. |
| 🏛️ **[Core Architecture](docs/ARCHITECTURE.md)** | COM VTable interop, Flip presentation, device loss recovery, and Render Graph. |
| 👁️ **[Vision & Metrology](docs/VISION_AND_METROLOGY.md)** | Sub-pixel Caliper, NCC template matching, RANSAC fitting, Barcodes & Reed-Solomon. |
| 🍳 **[Code Recipes Cookbook](docs/RECIPES.md)** | 14 ready-to-use code recipes from WinForms controls to headless WebAPI rendering. |
| 🔮 **[Future Proposals](docs/ECOSYSTEM_EXPANSION_PROPOSALS.md)** | Compute Shaders, Vulkan/Metal research, and 3D surface scanning roadmap. |

---

## 📦 Package Matrix

| Package | Targets | Primary Capabilities |
| :--- | :--- | :--- |
| **`ZeroGraphics.Core`** | `netstandard2.0`, `net462`, `net8.0` | Peak-preserving decimation (MinMax, LTTB), FFT, 2D QuadTree/Grid, SPC analytics |
| **`ZeroGraphics.Rhi`** | `netstandard2.0`, `net462`, `net8.0`, `net9.0` | Low-level cross-platform RHI abstraction, Vulkan RHI backend (`VulkanRhiDevice`, `VulkanNative`), explicit resource barriers, timeline fence (`IRhiFence`), pipeline states, Null CPU device |
| **`ZeroGraphics.Vector`** | `netstandard2.0`, `net462`, `net8.0`, `net9.0` | Sovereign 2D vector geometry (`Path2D`), adaptive Bézier curve flattening, stroke expansion (caps/joins), ear-clipping triangulation, Pure C# TrueType typography parser, Signed Distance Field (SDF) Font Atlas Engine, and GPU batch renderer |
| **`ZeroGraphics.DirectX`** | `net462`, `net8.0-windows` | D3D11 device management, Flip Model SwapChain, Graphics Interception (`ComVTableHook`), shader overrides, D3D11 RHI |
| **`ZeroGraphics.Direct2D`** | `net462`, `net8.0-windows` | Headless `D2DOffscreenTarget`, DirectWrite ClearType typography, High-DPI `SetDpi` |
| **`ZeroGraphics.Waveform`** | `net462`, `net8.0-windows` | LineStrip waveform pipeline, dynamic buffer map streaming, 144Hz oscilloscope |
| **`ZeroGraphics.Imaging`** | `net462`, `net8.0-windows` | GPU Image Pipeline, Render Graph, `AsyncStagingRingBuffer`, SIMD Color Transformations & Thresholding, Zero-LOH Gaussian Blur, Color Science (`ColorSpaces`, Bradford chromatic adaptation) |
| **`ZeroGraphics.Vision`** | `net462`, `net8.0-windows` | Zero-LOH sub-pixel NCC, 1D caliper, TLS/Taubin/Fitzgibbon fit, RANSAC, Barcodes (Code 128, GS1, EAN-13 HRI) |

---

## ⚡ Quick Start: 144Hz Oscilloscope (WinForms)

```csharp
using System.Drawing;
using System.Windows.Forms;
using ZeroGraphics.Waveform.Controls;
using ZeroGraphics.Waveform.Pipeline;

var oscilloscope = new ZeroWaveformCanvas
{
    Dock = DockStyle.Fill,
    BackColor = Color.FromArgb(13, 17, 23),
    TraceColor = Color.FromArgb(16, 185, 129), // Emerald phosphor
    DecimationMode = WaveformDecimationMode.MinMax,
    AutoScale = true
};
this.Controls.Add(oscilloscope);

// Push multi-million point array directly to GPU
float[] sensorData = GetSensorReadings(); // 1,000,000 points
oscilloscope.SetData(sensorData);
```

👉 **[Browse All 14 Code Recipes in the Developer Cookbook](docs/RECIPES.md)**

---

## 🧪 Automated Testing & Verification

```bash
# Run all automated tests (177 tests, 100% pass)
dotnet test tests/ZeroGraphics.Tests/ZeroGraphics.Tests.csproj

# Launch interactive 144Hz GPU demonstration application
dotnet run --project samples/ZeroGraphics.Samples.Demo/ZeroGraphics.Samples.Demo.csproj -f net8.0-windows
```

---

## 📜 Release History

| Version | Release Date | Key Milestones & Highlights |
| :--- | :--- | :--- |
| **`v1.6.0`** | 2026-09-28 | **Standardized Color Spaces & Chromatic Adaptation**:<br/>• Added standardized RGB color space definitions in `ZeroGraphics.Imaging.ColorScience` (`ColorSpaces.cs`) covering sRGB, Adobe RGB (1998), Display P3, Rec. 2020, and ProPhoto RGB.<br/>• Added standard D65 and D50 white point chromaticities, transfer functions (gamma/linear conversions), and $3\times3$ RGB-to-XYZ / XYZ-to-RGB conversion matrices.<br/>• Added Bradford chromatic adaptation transform matrix calculation between arbitrary CIE illuminants.<br/>• Zero-allocation, high-precision single-precision float kernels compatible with real-time color grading pipelines. |
| **`v1.5.0`** | 2026-09-21 | **Sovereign 2D Vector Graphics, Pure C# TrueType Typography, Vulkan RHI & SDF Font Engine**:<br/>• Added `ZeroGraphics.Vector` sovereign 2D vector graphics library across `netstandard2.0`, `net462`, `net8.0`, `net9.0`.<br/>• Added `Path2D` container for arbitrary geometric verbs (`MoveTo`, `LineTo`, `QuadTo`, `CubicTo`, `Close`, primitives, and standard SVG export).<br/>• Added `AdaptiveFlattening` (sub-pixel de Casteljau recursion) and `PathStroker` (Butt/Square/Round caps, Miter/Bevel/Round joins).<br/>• Added robust `PolygonTriangulator` with ear-clipping, convex fan optimization, and collinear/degenerate polygon filtering.<br/>• Added pure C# `TrueTypeReader` and `TrueTypeFont` binary table parser (`cmap` format 4, `head`, `maxp`, `hhea`, `hmtx`, `loca`, `glyf` simple & composite outlines).<br/>• Added pure C# Signed Distance Field (SDF) Font Atlas Engine (`SdfGenerator`, `FontAtlasPacker`, `SdfFont`) with single-channel `R8_UNorm` GPU texture generation.<br/>• Added high-throughput `VectorRenderer.DrawTextSdf` emitting 1 quad (4 vertices, 6 indices) per character for sub-pixel anti-aliased text at extreme scales.<br/>• Added cross-platform Vulkan RHI backend (`VulkanRhiDevice`, `VulkanNative`) with zero native C++ wrappers, timeline fences, and memory recycling.<br/>• Expanded automated test suite to **270 tests (100% pass rate)**. |
| **`v1.4.2`** | 2026-09-21 | **Graphics Interception Engine, Timeline Fences & Zero-LOH Convolutions**:<br/>• Added pure C# COM VTable hooking engine (`ComVTableHook`) using atomic memory protection swaps (`VirtualProtect`).<br/>• Added `D3D11GraphicsInterceptor` for SwapChain `Present` detour, frame time/FPS telemetry, backbuffer capture, draw/index counting, and runtime pixel shader replacement.<br/>• Added GPU-CPU timeline synchronization primitive (`IRhiFence`) on RHI across `NullRhiDevice` and `D3D11RhiDevice`.<br/>• Eliminated 33MB+ LOH allocation on 4K in `ConvolutionFilters.GaussianBlur` using `ArrayPool<float>`.<br/>• Hardware SIMD (`Vector256`/`Vector128`) and loop unrolling for `ColorTransform.Invert` (bitwise XOR with alpha preservation) and `ColorTransform.ToGrayscale` (ITU-R BT.709).<br/>• Unrolled fast blue/gray channel extraction in `GpuTextureTransfer.Download` and `TryReadback`.<br/>• Expanded test suite to **209 automated tests (100% pass rate)**. |
| **`v1.4.1`** | 2026-09-21 | **Performance Optimization & Explicit RHI Synchronization**:<br/>• Eliminated ~133MB LOH allocations in `NccTemplateMatcher` using `ArrayPool<double>`.<br/>• Hardware SIMD (AVX2/SSE) and branchless binary thresholding (`Thresholding.ApplyBinaryThreshold`), eliminating ~66MB LOH allocation in Bradley adaptive threshold.<br/>• Accelerated hot-path COM VTable dispatches (`Draw`, `DrawIndexed`, `Map`, `Unmap`, `UpdateSubresource`, `CopyResource`, `Dispatch`) via unmanaged function pointers (0 delegate overhead).<br/>• Added `AsyncStagingRingBuffer` in `GpuTextureTransfer` for zero-stall asynchronous double/triple-buffered GPU readback.<br/>• Introduced `RhiResourceState`, `RhiBarrier`, and `IRhiCommandBuffer.ResourceBarrier` for explicit pipeline synchronization (D3D12/Vulkan ready).<br/>• Expanded test suite to **203 automated tests (100% pass rate)**. |
| **`v1.4.0`** | 2026-09-21 | **Render Hardware Interface (RHI) & Industrial Barcode HRI Suite**:<br/>• Added `ZeroGraphics.Rhi` sovereign hardware abstraction layer (`IRhiDevice`, `IRhiBuffer`, `IRhiTexture`, `IRhiPipelineState`, `IRhiSwapChain`, `IRhiCommandBuffer`).<br/>• Added Headless `NullRhiDevice` reference rasterizer for CI/CD and unit testing without physical GPU hardware.<br/>• Added Direct3D 11 concrete RHI backend (`D3D11RhiDevice`) in `ZeroGraphics.DirectX`.<br/>• Added `Code128Encoder` supporting Auto Code Set switching (A/B/C), FNC1, and Modulo 103 checksum.<br/>• Added `Gs1HriFormatter` for standard GS1 Application Identifier (AI) parsing and bracketed HRI formatting.<br/>• Added `HriLayoutEngine` and `BarcodeCompositeRenderer` for zero-dependency EAN-13 (protruding guard bars + grouped text) and GS1-128 barcode + HRI text composite rendering.<br/>• Expanded test suite to **198 automated tests (100% pass rate)**. |
| **`v1.2.0`** | 2026-09-16 | **Computational Photography & Multi-Scale Vision Suite**:<br/>• Added Gaussian and Laplacian Multi-Scale Image Pyramids (`ImagePyramid`).<br/>• Added Mertens Multi-Exposure HDR Fusion (`MertensExposureFusion`) without tone-mapping artifacts.<br/>• Added Multi-Band Focus Stacking (`PyramidFocusStacking`) with local energy metrics.<br/>• Added À-Trous $B_3$-Spline Wavelet decomposition & multi-scale detail denoiser (`AtrousWaveletFilter`).<br/>• Added Fast Marching Method (Telea) image inpainting & defect removal (`FastMarchingInpaint`).<br/>• Added $O(1)$ Fast Guided Filter (`FastGuidedFilter`) & Directed Median Filter (`DirectedMedianFilter`).<br/>• Added Minkowski Gray-Edge Auto White Balance (`GrayEdgeAwb`).<br/>• Expanded test suite to **177 automated tests (100% pass rate)**. |
| **`v1.1.0`** | 2026-09-15 | **Vision & Industrial Metrology Engine**:<br/>• Added sub-pixel 1D caliper edge detection & profile peak extraction.<br/>• Added Normalized Cross Correlation (NCC) template matching with scale/rotation invariance.<br/>• Added TLS, Taubin, and Fitzgibbon geometric curve and conic fitting.<br/>• Added RANSAC robust outlier rejection for point clouds and feature correspondences.<br/>• Added Barcode (Code128, Code39) and DataMatrix decoders with Reed-Solomon error correction. |
| **`v1.0.1`** | 2026-09-14 | **D3D11 Pipeline Hardening & Resiliency**:<br/>• Implemented Flip Model swapchain optimization (`DXGI_SWAP_EFFECT_FLIP_DISCARD`).<br/>• Added transparent GPU driver crash and device loss recovery (`DXGI_ERROR_DEVICE_REMOVED`).<br/>• Dynamic buffer renaming for 144Hz multi-million point oscilloscope waveforms. |
| **`v1.0.0`** | 2026-09-10 | **Initial Sovereign Release**:<br/>• Direct3D 11, DXGI, Direct2D, and DirectWrite COM VTable interop in 100% pure C#.<br/>• Zero external dependencies (no SharpDX, Silk.NET, or OpenCV).<br/>• GPU render graph with 13 HLSL kernels and operation fusion.<br/>• Peak-preserving waveform decimation (MinMax, LTTB) and high-frequency FFT. |

---

## 🌐 Part of the ZeroPlatform Ecosystem

ZeroGraphics is the hardware graphics and machine vision pillar of the **[ZeroPlatform](https://github.com/kzxl/ZeroPlatform)** suite — unifying 12 sovereign subsystems including `ZeroUI`, `ZeroPipeline`, `ZeroTensor`, `ZeroCompute`, `ZeroComm`, and `ZeroStorage`.

---

## 📄 License & Author

Released under the permissive **MIT License**.  
Architected and developed by **Phong Võ**.
