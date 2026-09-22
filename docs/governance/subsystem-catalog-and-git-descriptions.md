# 📋 ZeroPlatform: Subsystem Catalog & Git Repository Standards

| Document ID | Version | Status | Effective Date | Target Audience |
| :--- | :---: | :---: | :---: | :--- |
| **GOV-REPO-001** | `v2.0.0` | **Active / Approved** | 2026-09-22 | Repository Maintainers, Release Engineers, AI Coding Agents |

---

## 1. Purpose & Scope

This specification establishes official GitHub metadata, short repository descriptions, searchable topic tags, tier classification badges, and dependency boundaries for all **28 autonomous satellite repositories** and the root orchestrator in the `kzxl` organization.

Maintaining consistent repository descriptions and topic tags ensures:
1. **Discoverability**: Standardized GitHub Topics make subsystems easily searchable on GitHub and NuGet.
2. **Architectural Transparency**: Developers instantly know which tier a repository belongs to and what it may depend upon.
3. **Ecosystem Cohesion**: Every subsystem README displays an official color-coded Tier Badge linking to the central ZeroPlatform architecture specification.

---

## 2. Standard Tier Badges

Include the corresponding badge markdown at the top of each repository's `README.md`:

| Tier | Name | Hex Color | Badge Markdown |
| :---: | :--- | :---: | :--- |
| **0** | Core Foundation | `#0284c7` | `[![ZeroPlatform Tier](https://img.shields.io/badge/ZeroPlatform-Tier%200%20(Core%20Foundation)-0284c7.svg)](https://github.com/kzxl/ZeroPlatform)` |
| **1** | Compute & System | `#4f46e5` | `[![ZeroPlatform Tier](https://img.shields.io/badge/ZeroPlatform-Tier%201%20(Compute%20%26%20System)-4f46e5.svg)](https://github.com/kzxl/ZeroPlatform)` |
| **2** | Transport & Storage | `#059669` | `[![ZeroPlatform Tier](https://img.shields.io/badge/ZeroPlatform-Tier%202%20(Transport%20%26%20Storage)-059669.svg)](https://github.com/kzxl/ZeroPlatform)` |
| **3** | Perception & AI | `#7c3aed` | `[![ZeroPlatform Tier](https://img.shields.io/badge/ZeroPlatform-Tier%203%20(Perception%20%26%20AI)-7c3aed.svg)](https://github.com/kzxl/ZeroPlatform)` |
| **4** | Graphics & Spatial 3D | `#ea580c` | `[![ZeroPlatform Tier](https://img.shields.io/badge/ZeroPlatform-Tier%204%20(Graphics%20%26%20Spatial%203D)-ea580c.svg)](https://github.com/kzxl/ZeroPlatform)` |
| **5** | Presentation & Apps | `#e11d48` | `[![ZeroPlatform Tier](https://img.shields.io/badge/ZeroPlatform-Tier%205%20(Presentation%20%26%20Apps)-e11d48.svg)](https://github.com/kzxl/ZeroPlatform)` |
| **-** | Root Orchestrator | `#0f172a` | `[![ZeroPlatform](https://img.shields.io/badge/ZeroPlatform-Ecosystem%20Orchestrator-0f172a.svg)](https://github.com/kzxl/ZeroPlatform)` |

---

## 3. Subsystem Catalog & GitHub Metadata Directory

### Tier 0: Core Foundation (The Bedrock)
> **Dependency Invariant**: $L_0 \rightarrow \emptyset$. Pure standard .NET BCL only. Absolutely zero internal or external platform dependencies.

#### 1. [`kzxl/ZeroPrimitives`](https://github.com/kzxl/ZeroPrimitives)
- **Tier**: Tier 0 (Core Foundation)
- **GitHub Description**: Pure C# zero-allocation primitive conversions, SSE4.2 CRC32C, span/pointer parsers, fast hex/base64, and compiled object mappers.
- **GitHub Topics**: `csharp`, `dotnet`, `zeroplatform`, `tier-0`, `zero-allocation`, `span`, `crc32c`, `simd`, `high-performance`
- **Permitted Dependencies**: None (BCL only)
- **Downstream Consumers**: All Tiers ($L_1 \dots L_5$)

#### 2. [`kzxl/ZeroConcurrency`](https://github.com/kzxl/ZeroConcurrency)
- **Tier**: Tier 0 (Core Foundation)
- **GitHub Description**: Pure C# lock-free SPSC/MPMC ring buffers, CSP channels, pooled ValueTask sources, and execution-context bypassing schedulers.
- **GitHub Topics**: `csharp`, `dotnet`, `zeroplatform`, `tier-0`, `lock-free`, `concurrency`, `ringbuffer`, `channels`, `valuetask`
- **Permitted Dependencies**: None (BCL only)
- **Downstream Consumers**: All Tiers ($L_1 \dots L_5$)

#### 3. [`kzxl/ZeroSecurity`](https://github.com/kzxl/ZeroSecurity)
- **Tier**: Tier 0 (Core Foundation)
- **GitHub Description**: Pure C# cryptographic suite: BLAKE3, FastSha256, HMAC, HKDF, ChaCha20/Poly1305, X25519 ECDH, and Cuckoo/Bloom filters.
- **GitHub Topics**: `csharp`, `dotnet`, `zeroplatform`, `tier-0`, `cryptography`, `blake3`, `chacha20-poly1305`, `ecdh`, `security`
- **Permitted Dependencies**: None (BCL only)
- **Downstream Consumers**: All Tiers ($L_1 \dots L_5$)

---

### Tier 1: Compute & System (Hardware & Numerics)
> **Dependency Invariant**: $L_1 \rightarrow L_0$. May only depend on Tier 0 (`ZeroPrimitives`, `ZeroConcurrency`, `ZeroSecurity`).

#### 4. [`kzxl/ZeroSystem`](https://github.com/kzxl/ZeroSystem)
- **Tier**: Tier 1 (Compute & System)
- **GitHub Description**: Sovereign Windows OS native subsystem, hardware inventory telemetry (CPU, GPU, RAM, Storage, Network), and OS diagnostics.
- **GitHub Topics**: `csharp`, `dotnet`, `zeroplatform`, `tier-1`, `system-telemetry`, `hardware-monitoring`, `win32`, `os-diagnostics`
- **Permitted Dependencies**: `ZeroPrimitives`, `ZeroConcurrency`
- **Downstream Consumers**: $L_2 \dots L_5$

#### 5. [`kzxl/ZeroCompression`](https://github.com/kzxl/ZeroCompression)
- **Tier**: Tier 1 (Compute & System)
- **GitHub Description**: Streaming multi-codec compression (Zstandard, LZMA, Brotli, Gorilla TSDB codec), heuristic data classifier, and TAR/ZIP containers.
- **GitHub Topics**: `csharp`, `dotnet`, `zeroplatform`, `tier-1`, `compression`, `zstandard`, `lzma`, `brotli`, `gorilla`, `streaming`
- **Permitted Dependencies**: `ZeroPrimitives`, `ZeroConcurrency`, `ZeroSecurity`
- **Downstream Consumers**: $L_2$ (`ZeroStorage`), $L_5$ (`ZeroDocuments`)

#### 6. [`kzxl/ZeroTensor`](https://github.com/kzxl/ZeroTensor)
- **Tier**: Tier 1 (Compute & System)
- **GitHub Description**: Pure C# N-dimensional tensor engine with strided slicing, Level-3 BLAS (GEMM), and SVD/QR/Cholesky matrix decompositions.
- **GitHub Topics**: `csharp`, `dotnet`, `zeroplatform`, `tier-1`, `tensor`, `numpy`, `linear-algebra`, `blas`, `matrix-decomposition`
- **Permitted Dependencies**: `ZeroPrimitives`, `ZeroConcurrency`
- **Downstream Consumers**: $L_3$ (`ZeroInference`, `ZeroNeural`, `ZeroSignal`), $L_4$ (`ZeroGraphics`)

#### 7. [`kzxl/ZeroCompute`](https://github.com/kzxl/ZeroCompute)
- **Tier**: Tier 1 (Compute & System)
- **GitHub Description**: Direct3D 11 Compute Shader dispatcher via COM VTable, UAV buffer/texture binding, and AVX2 SIMD fallback kernels.
- **GitHub Topics**: `csharp`, `dotnet`, `zeroplatform`, `tier-1`, `compute-shaders`, `direct3d11`, `gpu-computing`, `simd`, `avx2`
- **Permitted Dependencies**: `ZeroPrimitives`, `ZeroConcurrency`, `ZeroTensor`
- **Downstream Consumers**: $L_3$ (`ZeroInference`), $L_4$ (`ZeroGraphics`)

---

### Tier 2: Transport & Storage (Data & Comm Pipelines)
> **Dependency Invariant**: $L_2 \rightarrow L_0, L_1$. May depend on Tier 0 and Tier 1.

#### 8. [`kzxl/ZeroNetwork`](https://github.com/kzxl/ZeroNetwork)
- **Tier**: Tier 2 (Transport & Storage)
- **GitHub Description**: High-performance network infrastructure, IP/CIDR math, IEEE OUI identification, ARP table scanning, and embedded micro-HTTP server.
- **GitHub Topics**: `csharp`, `dotnet`, `zeroplatform`, `tier-2`, `networking`, `http-server`, `arp-scan`, `ip-math`, `raw-sockets`
- **Permitted Dependencies**: `ZeroPrimitives`, `ZeroConcurrency`
- **Downstream Consumers**: `ZeroComm`, `ZeroIoT`, `ZeroVideo`, `ZeroUI`

#### 9. [`kzxl/ZeroComm`](https://github.com/kzxl/ZeroComm)
- **Tier**: Tier 2 (Transport & Storage)
- **GitHub Description**: Asynchronous industrial fieldbus drivers (Modbus TCP/RTU, Siemens S7, Mitsubishi MC 3E, Omron FINS) with circular DMA ingestion.
- **GitHub Topics**: `csharp`, `dotnet`, `zeroplatform`, `tier-2`, `industrial-automation`, `modbus`, `mitsubishi-mc`, `omron-fins`, `fieldbus`
- **Permitted Dependencies**: `ZeroPrimitives`, `ZeroConcurrency`, `ZeroNetwork`
- **Downstream Consumers**: `ZeroIoT`, `ZeroPipeline`, `ZeroUI`

#### 10. [`kzxl/ZeroIoT`](https://github.com/kzxl/ZeroIoT)
- **Tier**: Tier 2 (Transport & Storage)
- **GitHub Description**: Industrial IoT edge connectors, lightweight MQTT v3.1.1/v5.0 client, OPC UA binary client, and telemetry sensor bridge.
- **GitHub Topics**: `csharp`, `dotnet`, `zeroplatform`, `tier-2`, `iiot`, `mqtt`, `opc-ua`, `telemetry`, `edge-gateway`
- **Permitted Dependencies**: `ZeroPrimitives`, `ZeroConcurrency`, `ZeroSecurity`, `ZeroNetwork`, `ZeroComm`
- **Downstream Consumers**: `ZeroPipeline`, `ZeroUI`

#### 11. [`kzxl/ZeroRfid`](https://github.com/kzxl/ZeroRfid)
- **Tier**: Tier 2 (Transport & Storage)
- **GitHub Description**: EPC Gen2 / ISO 18000-6C RFID reader adapters, sliding-window anti-collision deduplication pipeline, and physical simulator.
- **GitHub Topics**: `csharp`, `dotnet`, `zeroplatform`, `tier-2`, `rfid`, `epc-gen2`, `inventory-tracking`, `hardware-simulator`
- **Permitted Dependencies**: `ZeroPrimitives`, `ZeroConcurrency`, `ZeroNetwork`
- **Downstream Consumers**: `ZeroPipeline`, `ZeroUI`

#### 12. [`kzxl/ZeroStorage`](https://github.com/kzxl/ZeroStorage)
- **Tier**: Tier 2 (Transport & Storage)
- **GitHub Description**: Embedded TSDB, Facebook Gorilla Delta-of-Delta + XOR float compression, MMF zero-copy persistence, and CRC32 WAL.
- **GitHub Topics**: `csharp`, `dotnet`, `zeroplatform`, `tier-2`, `tsdb`, `gorilla-compression`, `memory-mapped-files`, `time-series`, `wal`
- **Permitted Dependencies**: `ZeroPrimitives`, `ZeroConcurrency`, `ZeroCompression`
- **Downstream Consumers**: `ZeroData`, `ZeroCharts`, `ZeroPipeline`, `ZeroUI`

#### 13. [`kzxl/ZeroData`](https://github.com/kzxl/ZeroData)
- **Tier**: Tier 2 (Transport & Storage)
- **GitHub Description**: Pure C# columnar DataFrame, SIMD relational hash joins, compiled expression tree SQL materializers, and Apache Arrow IPC.
- **GitHub Topics**: `csharp`, `dotnet`, `zeroplatform`, `tier-2`, `dataframe`, `columnar-data`, `polars-equivalent`, `arrow`, `sql`
- **Permitted Dependencies**: `ZeroPrimitives`, `ZeroConcurrency`, `ZeroTensor`, `ZeroStorage`
- **Downstream Consumers**: `ZeroCharts`, `ZeroReports`, `ZeroUI`

---

### Tier 3: Perception & Intelligence (Signal, Vision & AI)
> **Dependency Invariant**: $L_3 \rightarrow L_0, L_1, L_2$. May depend on Tiers 0, 1, and 2.

#### 14. [`kzxl/ZeroSignal`](https://github.com/kzxl/ZeroSignal)
- **Tier**: Tier 3 (Perception & Intelligence)
- **GitHub Description**: In-place Cooley-Tukey FFT, real-time STFT spectrogram, zero-phase Butterworth FiltFilt, and Extended Kalman Filter (EKF).
- **GitHub Topics**: `csharp`, `dotnet`, `zeroplatform`, `tier-3`, `dsp`, `fft`, `spectrogram`, `butterworth`, `kalman-filter`
- **Permitted Dependencies**: `ZeroPrimitives`, `ZeroConcurrency`, `ZeroTensor`
- **Downstream Consumers**: `ZeroAudioVisual`, `ZeroCharts`, `ZeroPipeline`

#### 15. [`kzxl/ZeroGeometry`](https://github.com/kzxl/ZeroGeometry)
- **Tier**: Tier 3 (Perception & Intelligence)
- **GitHub Description**: 3D point cloud ICP registration, KdTree3D/RTree2D spatial queries, surface normal estimation, and Sutherland-Hodgman clipping.
- **GitHub Topics**: `csharp`, `dotnet`, `zeroplatform`, `tier-3`, `point-cloud`, `icp`, `kdtree`, `computational-geometry`, `delaunay`
- **Permitted Dependencies**: `ZeroPrimitives`, `ZeroConcurrency`, `ZeroTensor`
- **Downstream Consumers**: `ZeroTwin3D`, `Zero3D`, `ZeroGraphics`

#### 16. [`kzxl/ZeroVideo`](https://github.com/kzxl/ZeroVideo)
- **Tier**: Tier 3 (Perception & Intelligence)
- **GitHub Description**: Pure C# video streaming engine, RTSP/RTP transport, H.264 SPS Exp-Golomb parser, industrial MJPEG client, and zero-LOH frame pool.
- **GitHub Topics**: `csharp`, `dotnet`, `zeroplatform`, `tier-3`, `video-streaming`, `rtsp`, `rtp`, `h264`, `mjpeg`, `camera-stream`
- **Permitted Dependencies**: `ZeroPrimitives`, `ZeroConcurrency`, `ZeroNetwork`
- **Downstream Consumers**: `ZeroGraphics`, `ZeroPipeline`, `ZeroUI`

#### 17. [`kzxl/ZeroAudioVisual`](https://github.com/kzxl/ZeroAudioVisual)
- **Tier**: Tier 3 (Perception & Intelligence)
- **GitHub Description**: Industrial acoustic predictive maintenance, microphone array beamforming, and synchronized audio-visual defect localization.
- **GitHub Topics**: `csharp`, `dotnet`, `zeroplatform`, `tier-3`, `acoustic-monitoring`, `predictive-maintenance`, `beamforming`, `audiovisual`
- **Permitted Dependencies**: `ZeroPrimitives`, `ZeroConcurrency`, `ZeroSignal`, `ZeroVideo`
- **Downstream Consumers**: `ZeroCharts`, `ZeroPipeline`, `ZeroUI`

#### 18. [`kzxl/ZeroInference`](https://github.com/kzxl/ZeroInference)
- **Tier**: Tier 3 (Perception & Intelligence)
- **GitHub Description**: Pure C# ONNX binary model parser, CPU execution graph & OnnxRuntime GPU providers, and YOLOv8/v11 anchor-free decoders.
- **GitHub Topics**: `csharp`, `dotnet`, `zeroplatform`, `tier-3`, `onnx`, `ai-inference`, `yolov8`, `yolov11`, `deep-learning`, `edge-ai`
- **Permitted Dependencies**: `ZeroPrimitives`, `ZeroConcurrency`, `ZeroTensor`, `ZeroCompute`
- **Downstream Consumers**: `ZeroGraphics`, `ZeroPipeline`, `ZeroUI`

#### 19. [`kzxl/ZeroNeural`](https://github.com/kzxl/ZeroNeural)
- **Tier**: Tier 3 (Perception & Intelligence)
- **GitHub Description**: Pure C# reverse-mode automatic differentiation (Autograd) DAG tape, neural layers (Linear, Conv2D), and AdamW/SGD optimizers.
- **GitHub Topics**: `csharp`, `dotnet`, `zeroplatform`, `tier-3`, `autograd`, `deep-learning`, `pytorch-equivalent`, `neural-network`
- **Permitted Dependencies**: `ZeroPrimitives`, `ZeroConcurrency`, `ZeroTensor`, `ZeroCompute`
- **Downstream Consumers**: `ZeroInference`, `ZeroPipeline`

---

### Tier 4: Graphics & Spatial 3D (GPU Rendering)
> **Dependency Invariant**: $L_4 \rightarrow L_0 \dots L_3$. May depend on Tiers 0, 1, 2, and 3.

#### 20. [`kzxl/ZeroGraphics`](https://github.com/kzxl/ZeroGraphics)
- **Tier**: Tier 4 (Graphics & Spatial 3D)
- **GitHub Description**: Render Hardware Interface (RHI - D3D11), COM VTable hook, zero-LOH NCC & Gaussian blur, AVX2 SIMD filters, and Barcode HRI suite.
- **GitHub Topics**: `csharp`, `dotnet`, `zeroplatform`, `tier-4`, `computer-vision`, `direct3d11`, `direct2d`, `simd`, `image-processing`, `rhi`
- **Permitted Dependencies**: `ZeroPrimitives`, `ZeroConcurrency`, `ZeroTensor`, `ZeroCompute`, `ZeroVideo`, `ZeroInference`
- **Downstream Consumers**: `ZeroCharts`, `ZeroUI`, `ZeroPipeline`

#### 21. [`kzxl/ZeroCharts`](https://github.com/kzxl/ZeroCharts)
- **Tier**: Tier 4 (Graphics & Spatial 3D)
- **GitHub Description**: Direct2D GPU high-density telemetry strip charts, dynamic multi-axis graphs, and 60-144Hz streaming waveform visualizers.
- **GitHub Topics**: `csharp`, `dotnet`, `zeroplatform`, `tier-4`, `charts`, `telemetry`, `direct2d`, `data-visualization`, `waveform`
- **Permitted Dependencies**: `ZeroPrimitives`, `ZeroConcurrency`, `ZeroGraphics`, `ZeroStorage`, `ZeroSignal`
- **Downstream Consumers**: `ZeroUI`, `ZeroPipeline`

#### 22. [`kzxl/ZeroTwin3D`](https://github.com/kzxl/ZeroTwin3D)
- **Tier**: Tier 4 (Graphics & Spatial 3D)
- **GitHub Description**: Pure C# 3D digital twin scene graph, OBJ & glTF 2.0 / GLB loaders, Direct3D 11 rendering pipeline, and orbit/fly camera navigation.
- **GitHub Topics**: `csharp`, `dotnet`, `zeroplatform`, `tier-4`, `digital-twin`, `3d-rendering`, `gltf`, `obj-loader`, `direct3d11`
- **Permitted Dependencies**: `ZeroPrimitives`, `ZeroConcurrency`, `ZeroGeometry`, `ZeroGraphics`
- **Downstream Consumers**: `ZeroUI`, `ZeroPipeline`

#### 23. [`kzxl/Zero3D`](https://github.com/kzxl/Zero3D)
- **Tier**: Tier 4 (Graphics & Spatial 3D)
- **GitHub Description**: Pure C# 3D mathematics, camera matrices, illumination models, bounding volumes, and geometry mesh rendering.
- **GitHub Topics**: `csharp`, `dotnet`, `zeroplatform`, `tier-4`, `3d-math`, `mesh-rendering`, `camera-projection`, `spatial-computing`
- **Permitted Dependencies**: `ZeroPrimitives`, `ZeroConcurrency`, `ZeroGeometry`, `ZeroGraphics`
- **Downstream Consumers**: `ZeroTwin3D`, `ZeroUI`

---

### Tier 5: Presentation & Orchestration (User Layer & Reporting)
> **Dependency Invariant**: $L_5 \rightarrow L_0 \dots L_4$. Highest application and integration layer.

#### 24. [`kzxl/ZeroDocuments`](https://github.com/kzxl/ZeroDocuments)
- **Tier**: Tier 5 (Presentation & Orchestration)
- **GitHub Description**: Pure C# zero-dependency OpenXML Excel (.xlsx) streaming reader/writer and RFC 4180 CSV tokenizer/parser.
- **GitHub Topics**: `csharp`, `dotnet`, `zeroplatform`, `tier-5`, `openxml`, `excel`, `xlsx`, `csv`, `document-processing`
- **Permitted Dependencies**: `ZeroPrimitives`, `ZeroConcurrency`, `ZeroCompression`
- **Downstream Consumers**: `ZeroReports`, `ZeroUI`, Enterprise Applications

#### 25. [`kzxl/ZeroReports`](https://github.com/kzxl/ZeroReports)
- **Tier**: Tier 5 (Presentation & Orchestration)
- **GitHub Description**: Pure C# high-speed PDF & industrial thermal barcode label rendering without GDI+ (ZPL, TSPL, ESC-POS emulation).
- **GitHub Topics**: `csharp`, `dotnet`, `zeroplatform`, `tier-5`, `pdf-generation`, `label-printing`, `zpl`, `barcode-label`, `thermal-printer`
- **Permitted Dependencies**: `ZeroPrimitives`, `ZeroConcurrency`, `ZeroDocuments`, `ZeroGraphics`
- **Downstream Consumers**: `ZeroUI`, Enterprise Applications

#### 26. [`kzxl/ZeroPipeline`](https://github.com/kzxl/ZeroPipeline)
- **Tier**: Tier 5 (Presentation & Orchestration)
- **GitHub Description**: Directed acyclic graph (DAG) scheduler (Kahn sort), industrial inspection nodes, declarative JSON recipes, and visual node canvas.
- **GitHub Topics**: `csharp`, `dotnet`, `zeroplatform`, `tier-5`, `dag-workflow`, `pipeline-orchestrator`, `machine-vision`, `industrial-aoi`
- **Permitted Dependencies**: All underlying tiers ($L_0 \dots L_4$)
- **Downstream Consumers**: `ZeroUI`, Showcase, Production Applications

#### 27. [`kzxl/ZeroUI`](https://github.com/kzxl/ZeroUI)
- **Tier**: Tier 5 (Presentation & Orchestration)
- **GitHub Description**: 10M+ rows virtual grid, single-HWND D3DCanvas, 40+ industrial SCADA controls (PlantMimicCanvas P&ID, Gauges), dark theme.
- **GitHub Topics**: `csharp`, `dotnet`, `zeroplatform`, `tier-5`, `winforms`, `wpf`, `scada`, `industrial-ui`, `virtual-grid`, `d3d-canvas`
- **Permitted Dependencies**: All underlying tiers ($L_0 \dots L_4$)
- **Downstream Consumers**: Enterprise Desktop HMI Applications

#### 28. [`kzxl/ZeroUI.React`](https://github.com/kzxl/ZeroUI.React)
- **Tier**: Tier 5 (Presentation & Orchestration)
- **GitHub Description**: Enterprise & industrial React component suite for SCADA, connected button clusters, and universal theme token synchronization.
- **GitHub Topics**: `typescript`, `react`, `zeroplatform`, `tier-5`, `scada-web`, `industrial-hmi`, `vite`, `theme-tokens`
- **Permitted Dependencies**: Web standard / ZeroPlatform Theme Tokens
- **Downstream Consumers**: Enterprise Web HMI Applications

---

### Root Orchestrator
#### 29. [`kzxl/ZeroPlatform`](https://github.com/kzxl/ZeroPlatform)
- **Role**: Sovereign Central Ecosystem Orchestrator & Multi-Repo Workspace
- **GitHub Description**: Sovereign pure C# industrial software ecosystem: 28 autonomous subsystems spanning foundational lock-free primitives, DSP, GPU vision, TSDB, AI, and SCADA UI.
- **GitHub Topics**: `csharp`, `dotnet`, `zeroplatform`, `monorepo-orchestrator`, `industrial-automation`, `scada`, `edge-computing`, `computer-vision`, `deep-learning`, `lock-free`
