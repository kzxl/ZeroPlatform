# ZeroPlatform: Unified Industrial .NET Ecosystem

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET Multi-Targeting](https://img.shields.io/badge/.NET-8.0%20%7C%204.6.2%20%7C%20Standard%202.0-purple.svg)](https://dotnet.microsoft.com/)
[![Zero External Dependencies](https://img.shields.io/badge/Dependencies-0%20(Pure%20C%23)-brightgreen.svg)]()
[![Ecosystem Docs](https://img.shields.io/badge/Docs-Ecosystem%20Catalog-brightgreen.svg)](docs/ZERO_PLATFORM_ECOSYSTEM.md)
[![Architecture Spec](https://img.shields.io/badge/Spec-6--Tier%20DAG%20Taxonomy-orange.svg)](docs/architect/tier-taxonomy-specification.md)

**ZeroPlatform** is a sovereign, enterprise-grade software ecosystem for industrial automation, computer vision, digital signal processing (DSP), edge AI, high-speed time-series persistence, and hardware-accelerated HMI/SCADA visual studio controls.

👉 **[6-Tier Architecture Spec](docs/architect/platform-architecture.md)** | **[Tier Governance Spec (SPEC-ARCH-001)](docs/architect/tier-taxonomy-specification.md)** | **[ISA-101 Industrial HMI Handbook (STD-HMI-001)](docs/standards/industrial-hmi-design.md)** | **[Git Standards & Repo Directory (GOV-REPO-001)](docs/governance/subsystem-catalog-and-git-descriptions.md)** | **[Subsystem Catalog](docs/ZERO_PLATFORM_ECOSYSTEM.md)**

---

## 🏛️ Ecosystem Architecture (6-Tier Strict DAG)

```mermaid
graph TD
    classDef l0 fill:#0f172a,stroke:#38bdf8,stroke-width:2px,color:#fff;
    classDef l1 fill:#1e293b,stroke:#818cf8,stroke-width:2px,color:#fff;
    classDef l2 fill:#14532d,stroke:#4ade80,stroke-width:2px,color:#fff;
    classDef l3 fill:#701a75,stroke:#f472b6,stroke-width:2px,color:#fff;
    classDef l4 fill:#7c2d12,stroke:#fb923c,stroke-width:2px,color:#fff;
    classDef l5 fill:#831843,stroke:#f43f5e,stroke-width:2px,color:#fff;

    subgraph L5 ["Tier 5: Presentation & Orchestration (User Layer)"]
        UI["ZeroUI, ZeroUI.React, ZeroAgent & ZeroPrompt"]:::l5
        Pipe["ZeroPipeline"]:::l5
        Docs["ZeroDocuments & ZeroReports"]:::l5
    end

    subgraph L4 ["Tier 4: Graphics & Spatial 3D (GPU Rendering)"]
        Gfx["ZeroGraphics"]:::l4
        Charts["ZeroCharts"]:::l4
        Twin["ZeroTwin3D & Zero3D"]:::l4
    end

    subgraph L3 ["Tier 3: Perception & Intelligence (Vision, OCR, Audio & AI)"]
        Video["ZeroVideo"]:::l3
        Infer["ZeroInference, ZeroNeural, ZeroTokenizer & ZeroLlm"]:::l3
        Ocr["ZeroOcr"]:::l3
        Sig["ZeroSignal, ZeroAudio & ZeroAudioVisual"]:::l3
        Geom["ZeroGeometry"]:::l3
    end

    subgraph L2 ["Tier 2: Transport & Storage (Data & Comm Pipelines)"]
        Comm["ZeroComm, ZeroIoT & ZeroRfid"]:::l2
        Net["ZeroNetwork"]:::l2
        Store["ZeroStorage, ZeroData & ZeroVector"]:::l2
    end

    subgraph L1 ["Tier 1: Compute & System (Hardware & Numerics)"]
        Tensor["ZeroTensor"]:::l1
        Comp["ZeroCompute"]:::l1
        Compres["ZeroCompression"]:::l1
        Sys["ZeroSystem"]:::l1
        Asset["ZeroAsset"]:::l1
    end

    subgraph L0 ["Tier 0: Core Foundation (The Bedrock)"]
        Prim["ZeroPrimitives"]:::l0
        Conc["ZeroConcurrency"]:::l0
        Sec["ZeroSecurity"]:::l0
        Txt["ZeroText"]:::l0
    end

    L5 --> L4 & L3 & L2 & L1 & L0
    L4 --> L3 & L1 & L0
    L3 --> L2 & L1 & L0
    L2 --> L1 & L0
    L1 --> L0
```

---

## 📜 Subsystems Classified by Architectural Tier

### Tier 0: Core Foundation (The Bedrock - Zero Dependencies)
| Subsystem | Repository | Key Capabilities |
| :--- | :--- | :--- |
| **`ZeroPrimitives`** | [`kzxl/ZeroPrimitives`](https://github.com/kzxl/ZeroPrimitives) | Pure C# zero-allocation primitive conversions, SSE4.2 CRC32C, unmanaged polymorphic struct pooling (`StructArenaPool`), span/pointer parsers, fast hex/base64, compiled object mapper. |
| **`ZeroConcurrency`** | [`kzxl/ZeroConcurrency`](https://github.com/kzxl/ZeroConcurrency) | Pure C# lock-free SPSC/MPMC ring buffers, execution-context bypassing schedulers, zero-allocation pooled `ValueTask` sources, and Go-like CSP channels. |
| **`ZeroSecurity`** | [`kzxl/ZeroSecurity`](https://github.com/kzxl/ZeroSecurity) | Pure C# cryptographic suite: BLAKE3/FastSha256, HMAC, HKDF, PBKDF2, Cuckoo/Bloom Filter, X25519 ECDH, ChaCha20/Poly1305. |
| **`ZeroText`** | [`kzxl/ZeroText`](https://github.com/kzxl/ZeroText) | Pure C# zero-allocation text algorithms, fast regex, Knuth-Morris-Pratt (KMP) substring search, and Vietnamese diacritics removal normalizer. |

### Tier 1: Compute & System (Hardware & Numerics)
| Subsystem | Repository | Key Capabilities |
| :--- | :--- | :--- |
| **`ZeroSystem`** | [`kzxl/ZeroSystem`](https://github.com/kzxl/ZeroSystem) | Sovereign Windows native subsystem, hardware inventory telemetry (CPU, GPU, RAM, Storage, Network), and OS diagnostics. |
| **`ZeroCompression`** | [`kzxl/ZeroCompression`](https://github.com/kzxl/ZeroCompression) | Streaming multi-codec compression (Zstandard, LZMA, Brotli, Gorilla float TSDB codec), sub-ms heuristic classifier, AES-256-GCM AEAD, TAR/ZIP containers. |
| **`ZeroTensor`** | [`kzxl/ZeroTensor`](https://github.com/kzxl/ZeroTensor) | N-D strided memory layout, BFloat16 (`BFloat16`) instant bitshift conversions, INT4 packed quantization (`GemmInt4`), Level-3 BLAS (GEMM), mixed-precision arithmetic, SVD/QR/Cholesky matrix decompositions, Safetensors/Npy. |
| **`ZeroCompute`** | [`kzxl/ZeroCompute`](https://github.com/kzxl/ZeroCompute) | 5-level CPU Parallel Compute Runtime (cache-aware tiling, SIMD Padé approximations, cross-platform physical core affinity), dual-socket NUMA awareness (`NumaTopology`), and Direct3D 11 GPU Compute Shaders. |
| **`ZeroAsset`** | [`kzxl/ZeroAsset`](https://github.com/kzxl/ZeroAsset) | Digital asset management (DAM), zero-byte variant branching (`#vc<n>`), hierarchical contiguous sorting, asset curation & cache keys. |

### Tier 2: Transport & Storage (Data & Comm Pipelines)
| Subsystem | Repository | Key Capabilities |
| :--- | :--- | :--- |
| **`ZeroNetwork`** | [`kzxl/ZeroNetwork`](https://github.com/kzxl/ZeroNetwork) | High-performance network infrastructure, IP/CIDR math, IEEE OUI filtering, ARP table, WoL, parallel port scanner, embedded micro-HTTP server. |
| **`ZeroComm`** | [`kzxl/ZeroComm`](https://github.com/kzxl/ZeroComm) | Asynchronous low-latency TCP/Serial transport (`TCP_NODELAY`), circular DMA ring buffers, Modbus TCP/RTU master, Mitsubishi 3E Binary, Omron FINS. |
| **`ZeroIoT`** | [`kzxl/ZeroIoT`](https://github.com/kzxl/ZeroIoT) | Industrial IoT edge connectors, MQTT 3.1.1/5.0 client with Exactly-Once QoS 2 (`PUBREC`/`PUBREL`/`PUBCOMP`) & in-flight tracking (`Qos2FlightTable`), OPC UA client with X.509 mTLS encryption, and TSDB streaming bridge. |
| **`ZeroRfid`** | [`kzxl/ZeroRfid`](https://github.com/kzxl/ZeroRfid) | EPC Gen2 / ISO 18000-6C suite, UHF reader adapters, sliding-window anti-collision deduplication pipeline & simulator. |
| **`ZeroStorage`** | [`kzxl/ZeroStorage`](https://github.com/kzxl/ZeroStorage) | Embedded TSDB, Facebook Gorilla Delta-of-Delta + XOR float compression (1.37 B/sample), MMF zero-copy persistence, IoT out-of-order ingestion, CRC32 WAL. |
| **`ZeroData`** | [`kzxl/ZeroData`](https://github.com/kzxl/ZeroData) | Columnar DataFrame, SIMD relational hash joins, dynamic NL-to-SQL builder & schema metadata, compiled SQL expressions, Arrow IPC, Roslyn-less CodeGen. |
| **`ZeroVector`** | [`kzxl/ZeroVector`](https://github.com/kzxl/ZeroVector) | High-throughput embedded Vector Database & SIMD similarity engine, AVX2/FMA metrics (Cosine, DotProduct, Euclidean, Manhattan, Hamming), Flat contiguous index & HNSW graph index. |

### Tier 3: Perception & Intelligence (Signal, Vision, OCR, Audio & AI)
| Subsystem | Repository | Key Capabilities |
| :--- | :--- | :--- |
| **`ZeroSignal`** | [`kzxl/ZeroSignal`](https://github.com/kzxl/ZeroSignal) | In-place Cooley-Tukey FFT, real-time STFT spectrogram, zero-phase Butterworth `FiltFilt`, Extended Kalman Filter (EKF), VAD voice activity detector. |
| **`ZeroAudio`** | [`kzxl/ZeroAudio`](https://github.com/kzxl/ZeroAudio) | High-performance audio capture/playback, WAV/PCM codecs, channel mixing, circular DMA audio buffer, and sub-ms acoustic streaming. |
| **`ZeroGeometry`** | [`kzxl/ZeroGeometry`](https://github.com/kzxl/ZeroGeometry) | 3D ICP rigid cloud alignment, KdTree3D/RTree2D spatial queries, surface normal estimation, Sutherland-Hodgman clipping, 2D Delaunay triangulation, 2D Homography DLT & RANSAC. |
| **`ZeroVideo`** | [`kzxl/ZeroVideo`](https://github.com/kzxl/ZeroVideo) | Industrial Motion JPEG client, RTSP 1.0 session transport, RFC 3550 RTP demuxing, H.264 NALU scanner & Exp-Golomb SPS parser, zero-LOH `VideoFramePool`, PTS playback. |
| **`ZeroAudioVisual`** | [`kzxl/ZeroAudioVisual`](https://github.com/kzxl/ZeroAudioVisual) | Acoustic predictive maintenance, multi-channel microphone array beamforming & audio-visual defect localization. |
| **`ZeroInference`** | [`kzxl/ZeroInference`](https://github.com/kzxl/ZeroInference) | Polymorphic `IInferenceSession`, Pure C# ONNX binary model parser, Transformer operators (RoPE `RotaryEmbedding`, CPU cache-tiled `FlashAttentionKernel`), CPU execution graph & OnnxRuntime GPU providers, YOLOv8/v11 decoders. |
| **`ZeroNeural`** | [`kzxl/ZeroNeural`](https://github.com/kzxl/ZeroNeural) | PyTorch-like reverse-mode automatic differentiation (Autograd) DAG tape, neural layers (`Linear`, `Sequential`, `Conv2D`, `Dropout`), AdamW/SGD. |
| **`ZeroOcr`** | [`kzxl/ZeroOcr`](https://github.com/kzxl/ZeroOcr) | Pure C# zero-allocation OCR abstractions, AVX2 SIMD preprocessor (ITU-R BT.601, binarization), dot-matrix morphology, HPP projection deskewing, parallel multi-ROI inspection, and native Windows WinRT OCR engine. |
| **`ZeroTokenizer`** | [`kzxl/ZeroTokenizer`](https://github.com/kzxl/ZeroTokenizer) | Pure C# Byte-Pair Encoding (BPE), Tiktoken regex-aware tokenization (`cl100k_base`, `o200k_base`, LLaMA-3), and Knapsack context token budgeter. |
| **`ZeroLlm`** | [`kzxl/ZeroLlm`](https://github.com/kzxl/ZeroLlm) | Pure C# Small Language Model runtime, GGUF v2/v3 binary parser, Paged KV-Cache allocator, Transformer decoder (RMSNorm, RoPE, SwiGLU, GQA), and token sampling engine. |

### Tier 4: Graphics & Spatial 3D (GPU Rendering)
| Subsystem | Repository | Key Capabilities |
| :--- | :--- | :--- |
| **`ZeroGraphics`** | [`kzxl/ZeroGraphics`](https://github.com/kzxl/ZeroGraphics) | Render Hardware Interface (RHI - Null, D3D11, and cross-platform Vulkan 1.0+ `VulkanRhiDevice`), COM VTable D3D11/D2D, pure C# Graphics Interception (`ComVTableHook`), Standardized Color Spaces & Bradford adaptation, Zero-LOH NCC & blur, 144Hz waveforms, Barcode HRI suite. |
| **`ZeroCharts`** | [`kzxl/ZeroCharts`](https://github.com/kzxl/ZeroCharts) | Direct2D GPU high-density telemetry strip charts, dynamic multi-axis graphs, and real-time streaming data visualizers. |
| **`ZeroTwin3D`** | [`kzxl/ZeroTwin3D`](https://github.com/kzxl/ZeroTwin3D) | Pure C# 3D digital twin spatial scene graph, Wavefront OBJ & glTF 2.0 / GLB 3D model loaders, Direct3D 11 rendering pipeline, orbit/fly camera navigation. |
| **`Zero3D`** | [`kzxl/Zero3D`](https://github.com/kzxl/Zero3D) | General-purpose 3D mathematics, camera matrices, lighting models, and geometry mesh rendering. |

### Tier 5: Presentation & Orchestration (User Layer & Reporting)
| Subsystem | Repository | Key Capabilities |
| :--- | :--- | :--- |
| **`ZeroDocuments`** | [`kzxl/ZeroDocuments`](https://github.com/kzxl/ZeroDocuments) | Pure C# zero-dependency OpenXML Excel (.xlsx) reader/writer and RFC 4180 CSV tokenizer/parser. |
| **`ZeroReports`** | [`kzxl/ZeroReports`](https://github.com/kzxl/ZeroReports) | Pure C# high-speed PDF & industrial thermal barcode label rendering without GDI+ (ZPL/TSPL/ESC-POS emulation). |
| **`ZeroPipeline`** | [`kzxl/ZeroPipeline`](https://github.com/kzxl/ZeroPipeline) | Directed acyclic graph (DAG) scheduler (Kahn sort), industrial inspection & vision nodes (Color Space, Homography 2D, Caliper, Barcode, OCR Inspection, AI), declarative JSON recipes, and visual node canvas. |
| **`ZeroUI`** | [`kzxl/ZeroUI`](https://github.com/kzxl/ZeroUI) | ISA-101 Industrial HMI Design System Handbook (`STD-HMI-001`), `ZAiChatBox` AI copilot streaming chat (WPF & WinForms), `ZOcrViewer` & `ZDocumentDeskew` inspection, Obsidian Dark ergonomics (`#11131F`), 48px+ touch targets, 10M+ rows virtual grid, single-HWND D3DCanvas, 40+ SCADA controls, Media & Creative Editors Suite. |
| **`ZeroUI.React`** | [`kzxl/ZeroUI.React`](https://github.com/kzxl/ZeroUI.React) | Enterprise & Industrial React component suite for SCADA, connected button clusters, universal theme token synchronization with Desktop. |
| **`ZeroAgent`** | [`kzxl/ZeroAgent`](https://github.com/kzxl/ZeroAgent) | Autonomous AI Agent framework, deterministic ReAct reasoning loop, zero-reflection tool execution, semantic episodic memory via `ZeroVector`, `DynamicDatabaseQueryTool` NL-to-SQL engine, and CSP multi-agent swarm coordination. |
| **`ZeroPrompt`** | [`kzxl/ZeroPrompt`](https://github.com/kzxl/ZeroPrompt) | Pure C# prompt templating, Pushdown Automaton (PDA) JSON Grammar state machine, grammar-constrained logit masking, and dynamic few-shot exemplar selector. |

---

## 🚀 Cross-Library Synergy & Architectural Combinations (Tiềm năng kết hợp)

The defining power of the ZeroPlatform is not merely each individual library's zero-dependency purity, but their **compound architectural synergy**. Subsystems seamlessly connect along the strict 6-tier DAG to form end-to-end, ultra-high-performance industrial solutions without bringing in external runtime dependencies:

```mermaid
flowchart TB
    subgraph S1 ["🤖 Pattern 1: Autonomous Industrial Copilot & NL-to-SQL Analytics"]
        direction LR
        UI1["ZeroUI<br/>(ZAiChatBox)"] -->|User Query| AG1["ZeroAgent<br/>(ReAct Loop)"]
        AG1 -->|Grammar Constraints| PR1["ZeroPrompt<br/>(PDA JSON Grammar)"]
        PR1 -->|Constrained Tokens| LLM1["ZeroLlm / ZeroTokenizer<br/>(Local SLM Engine)"]
        AG1 -->|Execute SQL Tool| DT1["ZeroData<br/>(Dynamic NL-to-SQL)"]
        DT1 -->|Columnar Scan| VEC1["ZeroVector<br/>(Schema & Vector Memory)"]
        DT1 -->|SIMD Aggregations| UI1
    end

    subgraph S2 ["🔍 Pattern 2: Edge Vision OCR & Compliance Audit Suite"]
        direction LR
        VID2["ZeroVideo<br/>(RTSP / MJPEG)"] -->|Frame Buffer| GFX2["ZeroGraphics<br/>(DIBSection / RHI)"]
        GFX2 -->|Binarized Image| OCR2["ZeroOcr<br/>(AVX2 Deskew & WinRT OCR)"]
        OCR2 -->|Bounding Boxes| UI2["ZeroUI<br/>(ZOcrViewer)"]
        UI2 -->|Anomaly Triage| AG2["ZeroAgent<br/>(Defect Reasoning)"]
        AG2 -->|21 CFR Part 11| REP2["ZeroReports / ZeroDocuments<br/>(PDF Label & Excel Export)"]
    end

    subgraph S3 ["📈 Pattern 3: SCADA Telemetry & Edge Predictive Maintenance"]
        direction LR
        IOT3["ZeroIoT / ZeroComm<br/>(MQTT 5.0 / Modbus)"] -->|100k samples/s| STR3["ZeroStorage<br/>(Gorilla TSDB WAL)"]
        STR3 -->|Sliding Window| SIG3["ZeroSignal<br/>(FFT & Spectrogram)"]
        SIG3 -->|Frequency Tensors| TNS3["ZeroTensor / ZeroCompute<br/>(INT4 / BFloat16 Inference)"]
        TNS3 -->|Anomaly Score| UI3["ZeroCharts / ZeroUI<br/>(144Hz Telemetry Strip)"]
    end
```

### 1. Autonomous Industrial Copilot & Natural Language Data Analytics
* **Problem**: Operators on factory floors need instantaneous answers from complex relational databases ("Which machines had >15% OEE drop this week?") without writing SQL or relying on cloud APIs.
* **Platform Combination**:
  * **`ZeroUI` (`ZAiChatBox`)**: Provides a native, ergonomic streaming chat interface embedded directly into SCADA desktops with markdown bubbles, intent chips, and responsive auto-scrolling.
  * **`ZeroAgent` (`ReActLoop`)**: Coordinates step-by-step reasoning, deciding when to look up schema metadata, formulate queries, or summarize results.
  * **`ZeroPrompt` (`PushdownAutomaton` / `JsonGrammar`)**: Enforces strict grammar-constrained generation at token level, preventing hallucinated SQL syntax or invalid JSON tool parameters.
  * **`ZeroLlm` & `ZeroTokenizer`**: Runs pure C# local SLMs (GGUF v2/v3, Q4_K_M) with Paged KV-Cache on CPU/GPU without Python or external servers.
  * **`ZeroData` (`DynamicDatabaseQueryTool` & `DataFrame`)**: Introspects table schemas, validates generated SQL, executes SIMD-accelerated queries on million-row datasets, and streams tabular summaries.
  * **`ZeroVector` (`HnswIndex`)**: Stores vector embeddings of database catalogs and business dictionaries for instant contextual schema lookup.

### 2. Edge Vision Inspection, OCR Verification & Regulatory Compliance Reporting
* **Problem**: High-speed packaging and manufacturing lines require real-time text recognition (lot numbers, expiration dates, serial barcodes) with zero memory leaks and automated regulatory documentation.
* **Platform Combination**:
  * **`ZeroVideo`**: Ingests multi-camera RTSP/MJPEG industrial video streams with zero Large Object Heap (LOH) churn using pooled memory buffers.
  * **`ZeroGraphics` & `ZeroOcr`**: Applies AVX2 morphological binarization, horizontal projection profile (HPP) deskewing, and sub-pixel character bounding box extraction.
  * **`ZeroUI` (`ZOcrViewer` & `ZDocumentDeskew`)**: Renders interactive inspection viewports with confidence overlays, search highlights, and angle adjustment calipers.
  * **`ZeroAgent`**: Analyzes OCR text deviations against MES orders, triggering triage actions on defect detection.
  * **`ZeroReports` & `ZeroDocuments`**: Renders 21 CFR Part 11 compliant audit trail PDFs, barcode shipping labels (ZPL/TSPL), and lot inspection workbooks (.xlsx) with 0 third-party dependencies.

### 3. Industrial SCADA Telemetry Anomaly Detection & Predictive Maintenance
* **Problem**: Multi-sensor vibration, temperature, and electrical telemetry streams generate millions of samples per minute, demanding edge anomaly detection before hardware failure occurs.
* **Platform Combination**:
  * **`ZeroIoT` & `ZeroComm`**: Ingests sensor packets over MQTT 5.0 (QoS 2) and industrial fieldbuses (Modbus TCP/RTU, Mitsubishi 3E, Omron FINS).
  * **`ZeroStorage`**: Compresses telemetry off-heap using Facebook Gorilla Delta-of-Delta + XOR encoding down to 1.37 bytes/sample with monotonic `Uuid7` WAL logging.
  * **`ZeroData`**: Bridges time-series streams into SIMD columnar dataframes with sliding-window aggregations.
  * **`ZeroSignal`**: Computes in-place Cooley-Tukey FFT, STFT spectrograms, and zero-phase Butterworth filtering for vibration spectral analysis.
  * **`ZeroTensor` & `ZeroCompute`**: Executes quantized neural models (INT4/BFloat16) via Direct3D 11 GPU compute shaders or cache-aware CPU threads to calculate bearing degradation indices.
  * **`ZeroCharts` & `ZeroUI`**: Visualizes real-time 144Hz telemetry waveforms and ISA-101 alarm thresholds at 60 FPS without UI stutter.

### 4. 3D Spatial Digital Twin & Multi-Channel Acoustic Defect Mapping
* **Problem**: Maintenance teams need to inspect spatial equipment layouts and localize airborne defects (gas leaks, corona discharges) directly within 3D plant CAD models.
* **Platform Combination**:
  * **`ZeroTwin3D` & `Zero3D`**: Parses standard CAD formats (Wavefront OBJ, glTF 2.0 / GLB) and renders interactive 3D spatial scenes via Direct3D 11 with orbit/fly navigation.
  * **`ZeroAudioVisual`**: Analyzes multi-channel microphone array signals, computing acoustic delay-and-sum beamforming to locate high-frequency acoustic emission peaks.
  * **`ZeroGeometry`**: Performs 3D Iterative Closest Point (ICP) point-cloud alignment and ray-mesh intersection to project acoustic heatmaps directly onto 3D equipment surfaces.
  * **`ZeroIoT` & `ZeroUI`**: Overlays live machine operational metrics as interactive HUD tags pinned to 3D equipment coordinates.

### 5. High-Throughput Offline Hybrid RAG & Knowledge Synthesis
* **Problem**: Secure air-gapped industrial facilities need offline documentation search and procedure retrieval combining full-text lexical search and deep semantic vector search.
* **Platform Combination**:
  * **`ZeroText`**: Provides zero-allocation Vietnamese text normalization (diacritics stripping, accent-insensitive search) and Knuth-Morris-Pratt substring indexing.
  * **`ZeroTokenizer`**: Tokenizes technical manuals using Tiktoken BPE while enforcing tight prompt token budgets via a dynamic Knapsack optimizer.
  * **`ZeroVector`**: Performs AVX2 SIMD similarity searches over HNSW graph indices across embedded vector collections.
  * **`ZeroData`**: Applies fused `SelectionMask` bitwise filters (combining metadata constraints like equipment model, year, department) directly on vector search candidates.
  * **`ZeroPrompt` & `ZeroAgent`**: Formulates synthesized step-by-step repair instructions and delivers them to field engineers via `ZAiChatBox`.

---

### 🧩 Subsystem Synergy & Interoperability Matrix

| Initiating Subsystem | Complementary Subsystem | Primary Integration Interface | Practical Industrial Value |
| :--- | :--- | :--- | :--- |
| **`ZeroUI`** | **`ZeroAgent`** | `ZAiChatBox.OnUserQuerySubmitted` ➔ `ReActEngine.ExecuteAsync` | Embedded AI copilot for operator assistance and troubleshooting |
| **`ZeroAgent`** | **`ZeroData`** | `DynamicDatabaseQueryTool` ➔ `ZeroData.Sql.Catalog` | Natural Language to SQL query execution with zero GC churn |
| **`ZeroAgent`** | **`ZeroPrompt`** | `PromptTemplate` & `PushdownAutomaton` | Guaranteed deterministic JSON schema & tool argument generation |
| **`ZeroAgent`** | **`ZeroVector`** | `VectorStoreEpisodicMemory` ➔ `HnswIndex` | Long-term conversational memory and semantic knowledge retrieval |
| **`ZeroUI`** | **`ZeroOcr`** | `ZOcrViewer.SetOcrResult(OcrPageResult)` | Interactive inspection viewport with sub-pixel bounding boxes |
| **`ZeroOcr`** | **`ZeroVideo`** | `VideoFramePool` ➔ `ZeroOcrEngine.ProcessSpan` | Real-time 60 FPS text and barcode extraction from video feeds |
| **`ZeroStorage`** | **`ZeroData`** | `DurablePersistentQueue` ➔ `DataFrame.FromStorage` | Zero-copy columnar analysis of durable time-series telemetry |
| **`ZeroSignal`** | **`ZeroCharts`** | `StftSpectrogram.Data` ➔ `Direct2DWaterfallControl` | High-frequency acoustic and vibration waterfall displays |
| **`ZeroTwin3D`** | **`ZeroAudioVisual`**| `BeamformerResult` ➔ `SceneGraph.HeatmapOverlay` | 3D visual projection of acoustic air leaks on CAD equipment |
| **`ZeroTensor`** | **`ZeroCompute`** | `Tensor<T>.Memory` ➔ `D3D11ComputeDispatcher` | Hardware-accelerated Level-3 BLAS and neural layer execution |
| **`ZeroReports`**| **`ZeroDocuments`**| `OpenXmlPackage` ➔ `PdfReportGenerator` | Automated production batch runs with PDF labels and Excel logs |

---

## ⚡ Quick Start

### 1. Synchronize All 37 Subsystems
```powershell
.\clone-ecosystem.ps1
```

### 2. Build Entire Solution
```bash
dotnet build ZeroPlatform.slnx
```

### 3. Run Full Test Suite (>1,500 Tests, 100% Pass Rate)
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
