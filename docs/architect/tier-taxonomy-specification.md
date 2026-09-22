# 🏛️ ZeroPlatform: Tier Taxonomy & Architectural Governance Specification

| Document ID | Version | Status | Effective Date | Target Audience |
| :--- | :---: | :---: | :---: | :--- |
| **SPEC-ARCH-001** | `v2.0.0` | **Active / Approved** | 2026-09-22 | Core Architects, Subsystem Maintainers, AI Coding Agents |

---

## 1. Executive Summary & Architectural Motivation

ZeroPlatform is engineered as a sovereign, pure C# industrial software ecosystem operating across 27 autonomous satellite repositories. 

Historically, each satellite maintained a strictly isolated "Zero Runtime Dependencies" philosophy. While this preserved autonomy, it resulted in **accidental duplication of foundation primitives** (e.g., custom ring buffers in `ZeroComm`, custom CRCs in `ZeroStorage`, custom unmanaged queues in `ZeroGraphics`).

This specification establishes the **ZeroPlatform 6-Tier Strict Directed Acyclic Graph (DAG) Taxonomy**. It establishes formal boundaries, dependency invariants, metadata tagging conventions, and integration rules governing all 27 subsystems.

---

## 2. The 6-Tier Hierarchy Overview

```mermaid
graph TD
    classDef l0 fill:#0f172a,stroke:#38bdf8,stroke-width:2px,color:#fff;
    classDef l1 fill:#1e293b,stroke:#818cf8,stroke-width:2px,color:#fff;
    classDef l2 fill:#14532d,stroke:#4ade80,stroke-width:2px,color:#fff;
    classDef l3 fill:#701a75,stroke:#f472b6,stroke-width:2px,color:#fff;
    classDef l4 fill:#7c2d12,stroke:#fb923c,stroke-width:2px,color:#fff;
    classDef l5 fill:#831843,stroke:#f43f5e,stroke-width:2px,color:#fff;

    subgraph L5 ["Tier 5: Presentation & Orchestration (User Layer)"]
        UI["ZeroUI & ZeroUI.React"]:::l5
        Pipe["ZeroPipeline"]:::l5
        Docs["ZeroDocuments & ZeroReports"]:::l5
    end

    subgraph L4 ["Tier 4: Graphics & Spatial 3D (GPU Rendering)"]
        Gfx["ZeroGraphics"]:::l4
        Charts["ZeroCharts"]:::l4
        Twin["ZeroTwin3D & Zero3D"]:::l4
    end

    subgraph L3 ["Tier 3: Perception & Intelligence (Vision & AI)"]
        Video["ZeroVideo"]:::l3
        Infer["ZeroInference & ZeroNeural"]:::l3
        Sig["ZeroSignal & ZeroAudioVisual"]:::l3
        Geom["ZeroGeometry"]:::l3
    end

    subgraph L2 ["Tier 2: Transport & Storage (Data & Comm Pipelines)"]
        Comm["ZeroComm, ZeroIoT & ZeroRfid"]:::l2
        Net["ZeroNetwork"]:::l2
        Store["ZeroStorage & ZeroData"]:::l2
    end

    subgraph L1 ["Tier 1: Compute & System (Hardware & Numerics)"]
        Tensor["ZeroTensor"]:::l1
        Comp["ZeroCompute"]:::l1
        Compres["ZeroCompression"]:::l1
        Sys["ZeroSystem"]:::l1
    end

    subgraph L0 ["Tier 0: Core Foundation (The Bedrock)"]
        Prim["ZeroPrimitives"]:::l0
        Conc["ZeroConcurrency"]:::l0
        Sec["ZeroSecurity"]:::l0
    end

    L5 --> L4 & L3 & L2 & L1 & L0
    L4 --> L3 & L1 & L0
    L3 --> L2 & L1 & L0
    L2 --> L1 & L0
    L1 --> L0
```

---

## 3. Detailed Tier Definitions & Subsystems

### Tier 0: Core Foundation (The Bedrock)
* **Architectural Invariant**: **Zero Platform Dependencies ($L_0 \rightarrow \emptyset$)**. Tier 0 libraries MUST NEVER reference any other ZeroPlatform project or package.
* **Responsibilities**: Microsecond/nanosecond primitive data structures, lock-free concurrency, memory management, cryptographic algorithms.

| Subsystem | Primary Capabilities |
| :--- | :--- |
| **`ZeroPrimitives`** | Zero-allocation span parsers, pointer arithmetic, `FastBinary`, `FastConvert`, `ByteRingBuffer`, compiled object mapping. |
| **`ZeroConcurrency`** | Lock-free SPSC (`ZeroRingBuffer`) & MPMC (`ZeroMpmcRingBuffer`), CSP channels (`ZeroChannel`), pooled `ValueTask` sources (`ZeroPromise`), dedicated pinned threads (`ZeroDedicatedWorker`), ExecutionContext bypass. |
| **`ZeroSecurity`** | Cryptographic primitives: BLAKE3, FastSha256, HMAC, HKDF, PBKDF2, ChaCha20/Poly1305, X25519 ECDH, Cuckoo/Bloom Filters. |

---

### Tier 1: Compute & System (Hardware & Numerics)
* **Architectural Invariant**: Can only depend on **Tier 0 ($L_1 \rightarrow L_0$)**.
* **Responsibilities**: OS telemetry, hardware diagnostics, multi-codec stream compression, N-dimensional matrix mathematics, and GPU compute dispatching.

| Subsystem | Primary Capabilities |
| :--- | :--- |
| **`ZeroSystem`** | Sovereign Windows native OS subsystem, CPU/GPU/RAM/Disk hardware telemetry, process diagnostics. |
| **`ZeroCompression`** | Streaming multi-codec compression (Zstandard, LZMA, Brotli, Gorilla float TSDB codec), heuristic data classifier, AES-256-GCM AEAD, TAR/ZIP containers. |
| **`ZeroTensor`** | N-D strided memory layout, zero-copy tensor slicing, Level-3 BLAS (GEMM), SVD/QR/Cholesky matrix decompositions. |
| **`ZeroCompute`** | Direct3D 11 Compute Shader dispatcher via COM VTable, UAV buffer/texture binding, AVX2 SIMD fallback kernels. |

---

### Tier 2: Transport & Storage (Data Pipelines & Industrial Protocols)
* **Architectural Invariant**: Can only depend on **Tier 0 and Tier 1 ($L_2 \rightarrow L_0, L_1$)**.
* **Responsibilities**: In-memory columnar data representations, embedded time-series storage, network infrastructure, and fieldbus communications.

| Subsystem | Primary Capabilities |
| :--- | :--- |
| **`ZeroNetwork`** | High-speed network infrastructure, CIDR IP math, ARP table scanning, IEEE OUI identification, embedded micro-HTTP server. |
| **`ZeroComm`** | Asynchronous industrial master drivers (Modbus TCP/RTU, Siemens S7, Mitsubishi MC 3E, Omron FINS), circular DMA ingestion. |
| **`ZeroIoT`** | Industrial IoT edge connectors, MQTT v3.1.1/v5.0 client, OPC UA client and telemetry sensor bridge. |
| **`ZeroRfid`** | EPC Gen2 / ISO 18000-6C RFID reader adapters, sliding-window anti-collision deduplication pipeline, physical hardware simulator. |
| **`ZeroStorage`** | Embedded TSDB, Facebook Gorilla Delta-of-Delta + XOR compression, MMF zero-copy persistence, Write-Ahead Log (WAL). |
| **`ZeroData`** | Columnar DataFrame, SIMD relational hash joins (Inner/Left/Right/Outer), compiled expression tree SQL materializers, Arrow IPC. |

---

### Tier 3: Perception & Intelligence (Signal, Vision & AI)
* **Architectural Invariant**: Can depend on **Tier 0, Tier 1, and Tier 2 ($L_3 \rightarrow L_0, L_1, L_2$)**.
* **Responsibilities**: Signal processing, point clouds, live video ingestion/streaming, acoustic analytics, neural execution graphs, and deep learning inference.

| Subsystem | Primary Capabilities |
| :--- | :--- |
| **`ZeroSignal`** | In-place radix-2 Cooley-Tukey FFT, real-time STFT spectrograms, zero-phase Butterworth `FiltFilt`, Extended Kalman Filter (EKF), VAD voice activity detector. |
| **`ZeroGeometry`** | 3D ICP rigid cloud alignment, KdTree3D/RTree2D spatial queries, surface normal estimation, Sutherland-Hodgman clipping, Delaunay triangulation. |
| **`ZeroVideo`** | Industrial Motion JPEG client, RTSP 1.0 session transport, RFC 3550 RTP demuxing, H.264 NALU scanner & Exp-Golomb SPS parser, zero-LOH `VideoFramePool`, PTS playback. |
| **`ZeroAudioVisual`** | Acoustic predictive maintenance, multi-channel microphone array beamforming & audio-visual synchronization. |
| **`ZeroInference`** | Polymorphic `IInferenceSession`, pure C# ONNX binary model parser, CPU execution graph & OnnxRuntime GPU providers, YOLOv8/v11 anchor-free decoders (detect, pose, seg). |
| **`ZeroNeural`** | PyTorch-like reverse-mode automatic differentiation (Autograd) DAG tape, neural layers (`Linear`, `Sequential`, `Conv2D`, `Dropout`), AdamW/SGD. |

---

### Tier 4: Graphics & Spatial 3D (GPU Rendering)
* **Architectural Invariant**: Can depend on **Tier 0, Tier 1, Tier 2, and Tier 3 ($L_4 \rightarrow L_0, L_1, L_2, L_3$)**.
* **Responsibilities**: Low-level Render Hardware Interface (RHI), real-time telemetry charts, spatial digital twin rendering, and 3D scenes.

| Subsystem | Primary Capabilities |
| :--- | :--- |
| **`ZeroGraphics`** | Render Hardware Interface (RHI - Null & D3D11) with explicit barriers & timeline fences, COM VTable D3D11/D2D, COM VTable graphics interception (`ComVTableHook`), Zero-LOH NCC & Gaussian blur, AVX2 SIMD filters, Async Staging Ring Buffer, Barcode HRI suite. |
| **`ZeroCharts`** | Direct2D GPU high-density telemetry strip charts, dynamic multi-axis graphs, 60–144Hz waveform visualizers. |
| **`ZeroTwin3D`** | Pure C# 3D digital twin spatial scene graph, Wavefront OBJ & glTF 2.0 / GLB 3D loaders, Direct3D 11 rendering pipeline, orbit/fly camera navigation. |
| **`Zero3D`** | General-purpose 3D mathematics, camera matrices, lighting models, and geometry mesh rendering. |

---

### Tier 5: Presentation, Documents & Orchestration (User Layer)
* **Architectural Invariant**: Highest layer. Can consume all underlying layers ($L_5 \rightarrow L_{0..4}$).
* **Responsibilities**: High-density desktop & web HMI/SCADA controls, interactive node graphs, industrial reporting, and document generation.

| Subsystem | Primary Capabilities |
| :--- | :--- |
| **`ZeroDocuments`** | Pure C# zero-dependency OpenXML Excel (.xlsx) reader/writer and RFC 4180 CSV engine. |
| **`ZeroReports`** | Pure C# high-speed PDF & industrial thermal barcode label rendering without GDI+ (ZPL/TSPL/ESC-POS emulation). |
| **`ZeroPipeline`** | Kahn-sorted Directed Acyclic Graph (DAG) inspection pipeline, Sub-DAG macro nodes, declarative JSON recipes, and interactive visual node canvas. |
| **`ZeroUI`** | 10M+ rows virtual grid, single-HWND D3DCanvas, 40+ industrial SCADA controls (PlantMimicCanvas P&ID, Gauges), Media & Creative Suite, dark theme. |
| **`ZeroUI.React`** | Enterprise & Industrial React component suite for SCADA, connected button clusters, universal theme token synchronization with Desktop. |

---

## 4. Architectural Rules & Governance

### Rule 1: The Downstream Invariant (Strict DAG)
A subsystem in Tier $N$ may only reference subsystems in Tier $< N$.
* **Violation**: If `ZeroComm` (Tier 2) attempts to reference `ZeroGraphics` (Tier 4) or `ZeroVideo` (Tier 3), it will be rejected at compile-time.

### Rule 2: The Core Independence Principle
Tier 0 (`ZeroPrimitives`, `ZeroConcurrency`, `ZeroSecurity`) must have **0 external and 0 internal platform dependencies**. They must remain standalone compilable with standard .NET BCL only.

### Rule 3: Autonomous Satellite Hybrid Linking
To allow each repository to be cloned, developed, and published completely independently while benefiting from full cross-project navigation in the workspace orchestrator:

```xml
<ItemGroup Condition="Exists('..\..\ZeroConcurrency\src\ZeroConcurrency\ZeroConcurrency.csproj')">
  <ProjectReference Include="..\..\ZeroConcurrency\src\ZeroConcurrency\ZeroConcurrency.csproj" />
</ItemGroup>

<ItemGroup Condition="!Exists('..\..\ZeroConcurrency\src\ZeroConcurrency\ZeroConcurrency.csproj')">
  <PackageReference Include="ZeroConcurrency" Version="1.0.0" />
</ItemGroup>
```

---

## 5. Metadata Tagging & Visual Indicators

### MSBuild Properties
Each subsystem defines its tier level in `Directory.Build.props`:
```xml
<PropertyGroup>
  <ZeroTier>0</ZeroTier>
  <ZeroTierName>CoreFoundation</ZeroTierName>
  <PackageTags>$(PackageTags);zeroplatform;tier-0;foundation</PackageTags>
</PropertyGroup>
```

### Visual Badges in README.md
Each repository includes the standardized Tier badge in its header:
```markdown
[![Tier](https://img.shields.io/badge/ZeroPlatform-Tier%200%20(Foundation)-0284c7.svg)]()
```

| Tier | Hex Color | Badge Label |
| :---: | :---: | :--- |
| **0** | `#0284c7` | `Tier 0 (Core Foundation)` |
| **1** | `#4f46e5` | `Tier 1 (Compute & System)` |
| **2** | `#059669` | `Tier 2 (Transport & Storage)` |
| **3** | `#7c3aed` | `Tier 3 (Perception & AI)` |
| **4** | `#ea580c` | `Tier 4 (Graphics & Spatial 3D)` |
| **5** | `#e11d48` | `Tier 5 (Presentation & Apps)` |
