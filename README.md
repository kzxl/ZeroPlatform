# ZeroPlatform: Unified Industrial .NET Ecosystem

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET Multi-Targeting](https://img.shields.io/badge/.NET-8.0%20%7C%204.6.2%20%7C%20Standard%202.0-purple.svg)](https://dotnet.microsoft.com/)
[![Zero External Dependencies](https://img.shields.io/badge/Dependencies-0%20(Pure%20C%23)-brightgreen.svg)]()
[![Ecosystem Docs](https://img.shields.io/badge/Docs-Ecosystem%20Catalog-brightgreen.svg)](docs/ZERO_PLATFORM_ECOSYSTEM.md)

**ZeroPlatform** is a sovereign, enterprise-grade software ecosystem for industrial automation, computer vision, digital signal processing (DSP), edge AI, high-speed time-series persistence, and hardware-accelerated HMI/SCADA visual studio controls.

Engineered around the **Multi-Repository Satellite Architecture**, all 25 subsystems operate as completely independent repositories with independent release lifecycles and CI/CD pipelines, unified under this workspace orchestrator.

👉 **[Read the Full Ecosystem Architecture Specification](docs/architect/platform-architecture.md)** | **[Subsystem Catalog](docs/ZERO_PLATFORM_ECOSYSTEM.md)**

---

## 🏛️ Ecosystem Architecture & Subsystem Repositories

| Subsystem | Repository | Key Capabilities |
| :--- | :--- | :--- |
| **`ZeroPrimitives`** | [`kzxl/ZeroPrimitives`](https://github.com/kzxl/ZeroPrimitives) | Pure C# zero-allocation primitive conversions, SSE4.2 CRC32C, span/pointer parsers, fast hex/base64, compiled object mapper. |
| **`ZeroTensor`** | [`kzxl/ZeroTensor`](https://github.com/kzxl/ZeroTensor) | N-D strided memory layout, zero-copy slicing, Level-3 BLAS (GEMM), SVD/QR/Cholesky matrix decompositions. |
| **`ZeroCompute`** | [`kzxl/ZeroCompute`](https://github.com/kzxl/ZeroCompute) | Direct3D 11 Compute Shader dispatcher via COM VTable, UAV buffer/texture dispatching, and CPU AVX2 SIMD fallback kernels. |
| **`ZeroCompression`** | [`kzxl/ZeroCompression`](https://github.com/kzxl/ZeroCompression) | Streaming multi-codec compression (Zstandard, LZMA, Brotli, Gorilla float TSDB codec), sub-ms heuristic classifier (`DataClassifier`), AES-256-GCM AEAD, TAR/ZIP containers. |
| **`ZeroData`** | [`kzxl/ZeroData`](https://github.com/kzxl/ZeroData) | Columnar DataFrame, SIMD relational hash joins (Inner/Left/Right/Outer), compiled expression tree SQL materializers, pure C# Arrow IPC. |
| **`ZeroStorage`** | [`kzxl/ZeroStorage`](https://github.com/kzxl/ZeroStorage) | Embedded TSDB, Facebook Gorilla Delta-of-Delta + XOR float compression (1.37 B/sample), MMF zero-copy persistence, IoT out-of-order ingestion, CRC32 WAL. |
| **`ZeroInference`** | [`kzxl/ZeroInference`](https://github.com/kzxl/ZeroInference) | Polymorphic `IInferenceSession`, Pure C# ONNX binary model parser, CPU execution graph & OnnxRuntime GPU providers, YOLOv8/v11 decoders (detection, pose, segment). |
| **`ZeroNeural`** | [`kzxl/ZeroNeural`](https://github.com/kzxl/ZeroNeural) | PyTorch-like reverse-mode automatic differentiation (Autograd) DAG tape, neural layers (`Linear`, `Sequential`, `Conv2D`, `Dropout`), AdamW/SGD. |
| **`ZeroSignal`** | [`kzxl/ZeroSignal`](https://github.com/kzxl/ZeroSignal) | In-place Cooley-Tukey FFT, real-time STFT spectrogram, zero-phase Butterworth `FiltFilt`, Extended Kalman Filter (EKF), VAD voice activity detector. |
| **`ZeroGeometry`** | [`kzxl/ZeroGeometry`](https://github.com/kzxl/ZeroGeometry) | 3D ICP rigid cloud alignment, KdTree3D/RTree2D spatial queries, surface normal estimation, Sutherland-Hodgman clipping, 2D Delaunay triangulation. |
| **`ZeroComm`** | [`kzxl/ZeroComm`](https://github.com/kzxl/ZeroComm) | Asynchronous low-latency TCP/Serial transport (`TCP_NODELAY`), circular DMA ring buffers, Modbus TCP/RTU master, Mitsubishi 3E Binary, Omron FINS. |
| **`ZeroGraphics`** | [`kzxl/ZeroGraphics`](https://github.com/kzxl/ZeroGraphics) | Render Hardware Interface (RHI - Null & D3D11) with explicit barriers & timeline fences, COM VTable D3D11/D2D, pure C# Graphics Interception (`ComVTableHook`), Zero-LOH NCC & Gaussian blur, AVX2 SIMD thresholding & color transforms, Async Staging Ring Buffer, 144Hz waveforms, computational photography, Barcode HRI suite. |
| **`ZeroUI`** | [`kzxl/ZeroUI`](https://github.com/kzxl/ZeroUI) | 10M+ rows virtual grid, single-HWND D3DCanvas, 40+ industrial SCADA controls (PlantMimicCanvas P&ID, Gauges), Media & Creative Editors Suite, dark theme (`#12151C`). |
| **`ZeroUI.React`** | [`kzxl/ZeroUI.React`](https://github.com/kzxl/ZeroUI.React) | Enterprise & Industrial React component suite for SCADA, connected button clusters, universal theme token synchronization with Desktop. |
| **`ZeroTwin3D`** | [`kzxl/ZeroTwin3D`](https://github.com/kzxl/ZeroTwin3D) | Pure C# 3D digital twin spatial scene graph, Wavefront OBJ & glTF 2.0 / GLB 3D model loaders, Direct3D 11 rendering pipeline, orbit/fly camera navigation. |
| **`ZeroCharts`** | [`kzxl/ZeroCharts`](https://github.com/kzxl/ZeroCharts) | Direct2D GPU high-density telemetry strip charts, dynamic multi-axis graphs, and real-time streaming data visualizers. |
| **`ZeroPipeline`** | [`kzxl/ZeroPipeline`](https://github.com/kzxl/ZeroPipeline) | Directed acyclic graph (DAG) scheduler (Kahn sort), industrial inspection nodes, declarative JSON recipes, and interactive visual node canvas. |
| **`ZeroIoT`** | [`kzxl/ZeroIoT`](https://github.com/kzxl/ZeroIoT) | Industrial IoT edge connectors, MQTT client, OPC UA client and sensor telemetry bridge. |
| **`ZeroNetwork`** | [`kzxl/ZeroNetwork`](https://github.com/kzxl/ZeroNetwork) | High-performance network infrastructure, IP/CIDR math, IEEE OUI filtering, ARP table, WoL, parallel port scanner, embedded micro-HTTP server. |
| **`ZeroDocuments`** | [`kzxl/ZeroDocuments`](https://github.com/kzxl/ZeroDocuments) | Pure C# zero-dependency OpenXML Excel (.xlsx) reader/writer and RFC 4180 CSV tokenizer/parser. |
| **`ZeroRfid`** | [`kzxl/ZeroRfid`](https://github.com/kzxl/ZeroRfid) | EPC Gen2 / ISO 18000-6C suite, UHF reader adapters, sliding-window anti-collision deduplication pipeline & simulator. |
| **`ZeroReports`** | [`kzxl/ZeroReports`](https://github.com/kzxl/ZeroReports) | Pure C# high-speed PDF & industrial thermal barcode label rendering without GDI+ (ZPL/TSPL/ESC-POS emulation). |
| **`ZeroAudioVisual`** | [`kzxl/ZeroAudioVisual`](https://github.com/kzxl/ZeroAudioVisual) | Acoustic predictive maintenance, multi-channel microphone array beamforming & audio-visual defect localization. |
| **`ZeroSecurity`** | [`kzxl/ZeroSecurity`](https://github.com/kzxl/ZeroSecurity) | Pure C# BLAKE3/FastSha256, HMAC, HKDF, PBKDF2, Cuckoo/Bloom Filter, X25519 ECDH, ChaCha20/XChaCha20-Poly1305. |
| **`ZeroSystem`** | [`kzxl/ZeroSystem`](https://github.com/kzxl/ZeroSystem) | Sovereign Windows native subsystem, hardware inventory telemetry (CPU, GPU, RAM, Storage, Network), and OS diagnostics. |
| **`ZeroConcurrency`** | [`kzxl/ZeroConcurrency`](https://github.com/kzxl/ZeroConcurrency) | Pure C# lock-free SPSC/MPMC ring buffers, execution-context bypassing schedulers, zero-allocation pooled ValueTask sources, and Go-like CSP channels. |

---

## 📜 Subsystem Version Matrix

| Subsystem | Version | Status | Primary Focus |
| :--- | :---: | :---: | :--- |
| **`ZeroPrimitives`** | `v1.1.0` | Active | Hardware-accelerated CRC32C (SSE4.2/ARM64), zero-alloc span/pointer parsers & CSV tokenizer. |
| **`ZeroConcurrency`** | `v1.0.0` | Active | Lock-free SPSC/MPMC ring buffers, 0-alloc ValueTask sources, Go-like CSP channels, ExecutionContext bypass. |
| **`ZeroGraphics`** | `v1.4.2` | Active | RHI abstraction (Null & D3D11) with timeline fences & barriers, pure C# COM VTable graphics interception, Zero-LOH NCC & Gaussian blur, AVX2 SIMD filters, Async Staging Ring Buffer, Barcode HRI suite. |
| **`ZeroUI`** | `v1.8.0` | Active | 10M+ rows virtual grid, single-HWND D3DCanvas, 40+ SCADA controls, Creative & Media Editors Suite, PackML state machine. |
| **`ZeroUI.React`** | `v1.0.0` | Active | Web & Edge HMI component suite, SCADA connected button groups, unified token architecture. |
| **`ZeroInference`** | `v1.2.0` | Active | Polymorphic `IInferenceSession`, ONNX binary parser, Pure C# & OnnxRuntime GPU providers, YOLOv8/v11 anchor-free decoders (detect, pose, seg). |
| **`ZeroData`** | `v1.1.0` | Active | Columnar DataFrame, Arrow IPC, compiled expression tree SQL materializers, zero-alloc unboxing. |
| **`ZeroStorage`** | `v1.2.0` | Active | Embedded TSDB, Gorilla Delta-of-Delta + XOR float compression, IoT Out-of-Order ingestion, Auto-Rollups. |
| **`ZeroCompression`** | `v1.0.0` | Active | Streaming multi-codec compression (Zstandard, LZMA, Brotli, Gorilla float), sub-ms heuristic classifier, AES-256-GCM AEAD, TAR/ZIP containers. |
| **`ZeroComm`** | `v1.1.0` | Active | Async TCP/Serial transport (`TCP_NODELAY`), circular DMA ring buffers, Modbus TCP/RTU master, Mitsubishi 3E Binary, Omron FINS. |
| **`ZeroSignal`** | `v1.1.0` | Active | In-place FFT, STFT spectrogram, zero-phase Butterworth `FiltFilt`, EKF, VadDetector, AudioJitterBuffer. |
| **`ZeroRfid`** | `v1.0.0` | Active | EPC Gen2 / ISO 18000-6C suite, UHF reader adapters, sliding-window deduplication pipeline & simulator. |
| **`ZeroSecurity`** | `v1.1.0` | Active | Pure C# BLAKE3/FastSha256, HMAC, HKDF, PBKDF2, Cuckoo/Bloom Filter, X25519 ECDH, ChaCha20/XChaCha20-Poly1305. |
| **`ZeroTensor`** | `v1.0.0` | Active | N-D strided memory layout, zero-copy slicing, Level-3 BLAS (GEMM), SVD/QR/Cholesky decompositions. |
| **`ZeroCompute`** | `v1.0.0` | Active | D3D11 Compute Shader dispatcher via COM VTable & CPU AVX2 SIMD fallback kernels. |
| **`ZeroNeural`** | `v1.0.0` | Active | Reverse-mode automatic differentiation (Autograd), neural network layers, AdamW/SGD optimizer. |
| **`ZeroGeometry`** | `v1.0.0` | Active | 3D ICP rigid cloud alignment, KdTree3D/RTree2D, polygon clipping, Delaunay triangulation. |
| **`ZeroPipeline`** | `v1.1.0` | Active | DAG scheduler (Kahn sort), Sub-DAG macro nodes, dynamic recipe hot-reloading, interactive canvas. |
| **`ZeroReports`** | `v1.0.0` | Active | Pure C# high-speed PDF & industrial thermal barcode label rendering without GDI+ (ZPL/TSPL/ESC-POS emulation). |
| **`ZeroAudioVisual`** | `v1.0.0` | Active | Acoustic predictive maintenance, multi-channel beamforming & audio-visual synchronization. |
| **`ZeroCharts`** | `v1.0.0` | Active | Direct2D GPU high-density telemetry strip charts and dynamic multi-axis graphs. |
| **`ZeroIoT`** | `v1.0.0` | Active | Industrial IoT edge connectors, MQTT client, OPC UA client and sensor telemetry bridge. |
| **`ZeroNetwork`** | `v2.0.0` | Active | Network infrastructure, CIDR IP math, OUI discovery, diagnostics, and embedded micro-services. |
| **`ZeroDocuments`** | `v1.0.0` | Active | Pure C# OpenXML Excel (.xlsx) reader/writer and RFC 4180 CSV engine without external dependencies. |
| **`ZeroSystem`** | `v1.0.0` | Active | Sovereign Windows native subsystem, system telemetry, and hardware inventory diagnostics. |
| **`ZeroTwin3D`** | `v1.1.0` | Active | Pure C# 3D digital twin spatial scene graph, Wavefront OBJ & glTF 2.0 / GLB 3D model loaders, D3D11 renderer. |

---

## ⚡ Quick Start

### 1. Synchronize All 23 Subsystems
```powershell
.\clone-ecosystem.ps1
```

### 2. Build Entire Solution
```bash
dotnet build ZeroPlatform.slnx
```

### 3. Run Full Test Suite (>1,400 Tests, 100% Pass Rate)
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
