# ZeroPlatform: Unified Industrial .NET Ecosystem

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET Multi-Targeting](https://img.shields.io/badge/.NET-8.0%20%7C%204.6.2%20%7C%20Standard%202.0-purple.svg)](https://dotnet.microsoft.com/)
[![Zero External Dependencies](https://img.shields.io/badge/Dependencies-0%20(Pure%20C%23)-brightgreen.svg)]()
[![Ecosystem Docs](https://img.shields.io/badge/Docs-Ecosystem%20Catalog-brightgreen.svg)](docs/ZERO_PLATFORM_ECOSYSTEM.md)

**ZeroPlatform** is a sovereign, enterprise-grade software ecosystem for industrial automation, computer vision, digital signal processing (DSP), edge AI, high-speed time-series persistence, and hardware-accelerated HMI/SCADA visual studio controls.

Engineered around the **Multi-Repository Satellite Architecture**, all 12 subsystems operate as completely independent repositories with independent release lifecycles and CI/CD pipelines, unified under this workspace orchestrator.

👉 **[Read the Full Ecosystem Architecture & Repository Matrix](docs/ZERO_PLATFORM_ECOSYSTEM.md)**

---

## 🏛️ Ecosystem Architecture & Subsystem Repositories

| Subsystem | Repository | Key Capabilities |
| :--- | :--- | :--- |
| **`ZeroPrimitives`** | [`kzxl/ZeroPrimitives`](https://github.com/kzxl/ZeroPrimitives) | Pure C# ultra-fast primitive conversions, span/pointer number parsers, compiled mapper. |
| **`ZeroTensor`** | [`kzxl/ZeroTensor`](https://github.com/kzxl/ZeroTensor) | N-D strided memory layout, zero-copy slicing, Level-3 BLAS, SVD/QR/Cholesky. |
| **`ZeroCompute`** | [`kzxl/ZeroCompute`](https://github.com/kzxl/ZeroCompute) | Direct3D 11 Compute Shader dispatcher via COM VTable & CPU AVX2 SIMD fallback. |
| **`ZeroData`** | [`kzxl/ZeroData`](https://github.com/kzxl/ZeroData) | Columnar DataFrame, relational hash joins (Inner/Left/Right/Outer), Arrow IPC. |
| **`ZeroStorage`** | [`kzxl/ZeroStorage`](https://github.com/kzxl/ZeroStorage) | Embedded TSDB, Facebook Gorilla Delta-of-Delta + XOR float compression, CRC32 WAL. |
| **`ZeroInference`** | [`kzxl/ZeroInference`](https://github.com/kzxl/ZeroInference) | Pure C# ONNX binary model parser, inference graph, Int8 quantizer, Vision NMS. |
| **`ZeroNeural`** | [`kzxl/ZeroNeural`](https://github.com/kzxl/ZeroNeural) | PyTorch-like reverse-mode automatic differentiation (Autograd), neural layers, AdamW. |
| **`ZeroSignal`** | [`kzxl/ZeroSignal`](https://github.com/kzxl/ZeroSignal) | In-place FFT, STFT spectrogram, zero-phase Butterworth `FiltFilt`, Extended Kalman Filter. |
| **`ZeroGeometry`** | [`kzxl/ZeroGeometry`](https://github.com/kzxl/ZeroGeometry) | 3D ICP rigid cloud alignment, KdTree3D/RTree2D, polygon clipping, Delaunay triangulation. |
| **`ZeroComm`** | [`kzxl/ZeroComm`](https://github.com/kzxl/ZeroComm) | Asynchronous TCP transport, Modbus TCP/RTU master, Mitsubishi 3E Binary, Omron FINS. |
| **`ZeroGraphics`** | [`kzxl/ZeroGraphics`](https://github.com/kzxl/ZeroGraphics) | Direct3D 11 GPU rendering, Direct2D 60 FPS waveforms, analytical SDF cards, CV algorithms. |
| **`ZeroUI`** | [`kzxl/ZeroUI`](https://github.com/kzxl/ZeroUI) | 10M+ rows virtual grid, 60 FPS SCADA/HMI controls, dark theme system (`#12151C`). |
| **`ZeroPipeline`** | [`kzxl/ZeroPipeline`](https://github.com/kzxl/ZeroPipeline) | DAG scheduler (Kahn sort), industrial inspection nodes, JSON recipes, interactive node canvas. |

---

## ⚡ Quick Start

### 1. Synchronize All 12 Subsystems
```powershell
.\clone-ecosystem.ps1
```

### 2. Build Entire Solution
```bash
dotnet build ZeroPlatform.slnx
```

### 3. Run Full Test Suite (719 Tests, 100% Pass Rate)
```bash
dotnet test ZeroPlatform.slnx
```

### 4. Launch Unified Showcase Application
```bash
dotnet run --project samples/ZeroPlatform.Samples.Showcase/ZeroPlatform.Samples.Showcase.csproj -f net8.0-windows
```

---

## 📄 Authors & License

Architected and developed by **Phong Võ** (`kzxl`). Released under the **MIT License**.
