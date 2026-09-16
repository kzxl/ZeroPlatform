# ZeroPlatform: Unified Industrial .NET Ecosystem

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET Multi-Targeting](https://img.shields.io/badge/.NET-8.0%20%7C%204.6.2%20%7C%20Standard%202.0-purple.svg)](https://dotnet.microsoft.com/)
[![Zero External Dependencies](https://img.shields.io/badge/Dependencies-0%20(Pure%20C%23)-brightgreen.svg)]()
[![Ecosystem Docs](https://img.shields.io/badge/Docs-Ecosystem%20Catalog-brightgreen.svg)](docs/ZERO_PLATFORM_ECOSYSTEM.md)

**ZeroPlatform** is a sovereign, enterprise-grade software ecosystem for industrial automation, computer vision, digital signal processing (DSP), edge AI, high-speed time-series persistence, and hardware-accelerated HMI/SCADA visual studio controls.

Engineered around the **Multi-Repository Satellite Architecture**, all 12 subsystems operate as completely independent repositories with independent release lifecycles and CI/CD pipelines, unified under this workspace orchestrator.

👉 **[Read the Full Ecosystem Architecture Specification](docs/architect/platform-architecture.md)** | **[Subsystem Catalog](docs/ZERO_PLATFORM_ECOSYSTEM.md)**

---

## 🏛️ Ecosystem Architecture & Subsystem Repositories

| Subsystem | Repository | Key Capabilities |
| :--- | :--- | :--- |
| **`ZeroPrimitives`** | [`kzxl/ZeroPrimitives`](https://github.com/kzxl/ZeroPrimitives) | Pure C# ultra-fast primitive conversions, SSE4.2 CRC32C, span/pointer parsers, compiled mapper. |
| **`ZeroTensor`** | [`kzxl/ZeroTensor`](https://github.com/kzxl/ZeroTensor) | N-D strided memory layout, zero-copy slicing, Level-3 BLAS, SVD/QR/Cholesky. |
| **`ZeroCompute`** | [`kzxl/ZeroCompute`](https://github.com/kzxl/ZeroCompute) | Direct3D 11 Compute Shader dispatcher via COM VTable & CPU AVX2 SIMD fallback. |
| **`ZeroData`** | [`kzxl/ZeroData`](https://github.com/kzxl/ZeroData) | Columnar DataFrame, relational hash joins (Inner/Left/Right/Outer), Arrow IPC. |
| **`ZeroStorage`** | [`kzxl/ZeroStorage`](https://github.com/kzxl/ZeroStorage) | Embedded TSDB, Facebook Gorilla Delta-of-Delta + XOR float compression, CRC32 WAL. |
| **`ZeroInference`** | [`kzxl/ZeroInference`](https://github.com/kzxl/ZeroInference) | Pure C# ONNX binary model parser, inference graph, Int8 quantizer, Vision NMS. |
| **`ZeroNeural`** | [`kzxl/ZeroNeural`](https://github.com/kzxl/ZeroNeural) | PyTorch-like reverse-mode automatic differentiation (Autograd), neural layers, AdamW. |
| **`ZeroSignal`** | [`kzxl/ZeroSignal`](https://github.com/kzxl/ZeroSignal) | In-place FFT, STFT spectrogram, zero-phase Butterworth `FiltFilt`, Extended Kalman Filter. |
| **`ZeroGeometry`** | [`kzxl/ZeroGeometry`](https://github.com/kzxl/ZeroGeometry) | 3D ICP rigid cloud alignment, KdTree3D/RTree2D, polygon clipping, Delaunay triangulation. |
| **`ZeroComm`** | [`kzxl/ZeroComm`](https://github.com/kzxl/ZeroComm) | Asynchronous TCP transport, Modbus TCP/RTU master, Mitsubishi 3E Binary, Omron FINS. |
| **`ZeroGraphics`** | [`kzxl/ZeroGraphics`](https://github.com/kzxl/ZeroGraphics) | Direct3D 11 GPU rendering, 144Hz waveforms, computational photography (Mertens HDR, Focus Stacking, Wavelets), CV metrology. |
| **`ZeroUI`** | [`kzxl/ZeroUI`](https://github.com/kzxl/ZeroUI) | 10M+ rows virtual grid, 60 FPS SCADA/HMI controls, Creative & Media Editors Suite, dark theme (`#12151C`). |
| **`ZeroPipeline`** | [`kzxl/ZeroPipeline`](https://github.com/kzxl/ZeroPipeline) | DAG scheduler (Kahn sort), industrial inspection nodes, JSON recipes, interactive node canvas. |
| **`ZeroSecurity`** | [`kzxl/ZeroSecurity`](https://github.com/kzxl/ZeroSecurity) | Pure C# BLAKE3/FastSha256, HMAC-SHA256, HKDF, PBKDF2, SSDEEP, Cuckoo/Bloom IOC, X25519 ECDH, ChaCha20/XChaCha20-Poly1305. |

---

## 📜 Subsystem Version Matrix

| Subsystem | Version | Status | Primary Focus |
| :--- | :---: | :---: | :--- |
| **`ZeroPrimitives`** | `v1.1.0` | Active | Hardware-accelerated CRC32C (SSE4.2/ARM64), zero-alloc span/pointer parsers & CSV tokenizer. |
| **`ZeroGraphics`** | `v1.2.0` | Active | COM VTable D3D11/D2D, 144Hz waveforms, computational photography (Mertens HDR, focus stacking, wavelets). |
| **`ZeroUI`** | `v1.8.0` | Active | 10M+ rows virtual grid, 40+ SCADA controls, Creative & Media Editors Suite, single-HWND architecture. |
| **`ZeroInference`** | `v1.1.0` | Active | Polymorphic `IInferenceSession`, ONNX parser, Pure C# & OnnxRuntime providers, Local LLM streaming. |
| **`ZeroData`** | `v1.1.0` | Active | Columnar DataFrame, Arrow IPC, compiled expression tree SQL materializers, zero-alloc unboxing. |
| **`ZeroStorage`** | `v1.1.0` | Active | Embedded TSDB, Gorilla Delta-of-Delta + XOR float compression, MultiMetricBlock wide-row compression, WAL. |
| **`ZeroComm`** | `v1.1.0` | Active | Async TCP transport, RFC 5389 STUN NAT traversal, Modbus TCP/RTU master, Mitsubishi 3E, Omron FINS. |
| **`ZeroSignal`** | `v1.1.0` | Active | In-place FFT, STFT spectrogram, zero-phase Butterworth `FiltFilt`, EKF, VadDetector, AudioJitterBuffer. |
| **`ZeroRfid`** | `v1.0.0` | Active | EPC Gen2 / ISO 18000-6C suite, UHF reader adapters, sliding-window deduplication pipeline & simulator. |
| **`ZeroSecurity`** | `v1.1.0` | Active | Pure C# BLAKE3/FastSha256, HMAC, HKDF, PBKDF2, Cuckoo/Bloom Filter, X25519 ECDH, ChaCha20/XChaCha20-Poly1305. |
| **`ZeroTensor`** | `v1.0.0` | Active | N-D strided memory layout, zero-copy slicing, Level-3 BLAS, SVD/QR/Cholesky decompositions. |
| **`ZeroCompute`** | `v1.0.0` | Active | D3D11 Compute Shader dispatcher via COM VTable & CPU AVX2 SIMD fallback kernels. |
| **`ZeroNeural`** | `v1.0.0` | Active | Reverse-mode automatic differentiation (Autograd), neural network layers, AdamW optimizer. |
| **`ZeroGeometry`** | `v1.0.0` | Active | 3D ICP rigid cloud alignment, KdTree3D/RTree2D, polygon clipping, Delaunay triangulation. |
| **`ZeroPipeline`** | `v1.0.0` | Active | DAG scheduler (Kahn sort), industrial inspection nodes, JSON recipes, interactive node canvas. |
| **`ZeroReports`** | `v1.0.0` | Active | Pure C# high-speed PDF & industrial thermal barcode label rendering without GDI+. |
| **`ZeroAudioVisual`** | `v1.0.0` | Active | Acoustic predictive maintenance, multi-channel beamforming & audio-visual synchronization. |
| **`ZeroCharts`** | `v1.0.0` | Active | Direct2D GPU high-density telemetry strip charts and dynamic multi-axis graphs. |
| **`ZeroIoT`** | `v1.0.0` | Active | Industrial IoT edge connectors, MQTT, OPC UA client and sensor telemetry bridge. |
| **`ZeroSystem`** | `v1.0.0` | Active | Sovereign Windows native subsystem, system telemetry, and hardware inventory diagnostics. |
| **`ZeroTwin3D`** | `v1.0.0` | Active | Pure C# 3D digital twin spatial scene graph and industrial asset visualization. |

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
