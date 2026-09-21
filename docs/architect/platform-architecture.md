# 🏛️ ZeroPlatform: Architecture & Subsystem Specification

**ZeroPlatform** is a sovereign, enterprise-grade software ecosystem engineered in 100% pure C# for industrial automation, computer vision, digital signal processing (DSP), edge AI, high-speed time-series persistence, and hardware-accelerated HMI/SCADA visual controls.

---

## 1. Multi-Repository Satellite Topology

ZeroPlatform employs a decentralized **Satellite Architecture** where each subsystem is an autonomous repository with independent release cycles, decoupled CI/CD, and zero external runtime dependencies:

```mermaid
graph TD
    subgraph Presentation ["Layer 5: Presentation & Orchestration"]
        UI["ZeroUI (Virtual Grid & SCADA)"]
        Pipe["ZeroPipeline (DAG Scheduler & Canvas)"]
        Reports["ZeroReports (PDF & Thermal Labels)"]
    end

    subgraph Graphics3D ["Layer 4: Graphics, 3D & Vision"]
        Graph["ZeroGraphics (RHI, D3D11, Vision)"]
        Twin["ZeroTwin3D (3D Digital Twin, glTF)"]
        Charts["ZeroCharts (Direct2D Telemetry)"]
        AV["ZeroAudioVisual (Acoustic Beamforming)"]
    end

    subgraph Intelligence ["Layer 3: Intelligence & Analytics"]
        Infer["ZeroInference (ONNX Parser & YOLO)"]
        Neural["ZeroNeural (Autograd & Layers)"]
        Signal["ZeroSignal (DSP, FFT, EKF)"]
        Geom["ZeroGeometry (3D ICP, KdTree)"]
    end

    subgraph StorageComm ["Layer 2: Storage, Comm & Network"]
        Data["ZeroData (Columnar DataFrame, Arrow)"]
        Store["ZeroStorage (Gorilla TSDB & WAL)"]
        Comm["ZeroComm (Modbus, MC 3E, FINS)"]
        IoT["ZeroIoT (MQTT, OPC UA)"]
        Rfid["ZeroRfid (EPC Gen2 RFID)"]
        Net["ZeroNetwork (CIDR, ARP, HTTP)"]
        Sec["ZeroSecurity (BLAKE3, ChaCha20)"]
    end

    subgraph Foundation ["Layer 1: Memory, Compute & System"]
        Prim["ZeroPrimitives (CRC32C, Fast Parsing)"]
        Tensor["ZeroTensor (Strided N-D Array & BLAS)"]
        Compute["ZeroCompute (D3D11 Compute Shaders)"]
        Docs["ZeroDocuments (Excel & CSV)"]
        Sys["ZeroSystem (Hardware Telemetry)"]
    end

    Presentation --> Graphics3D
    Presentation --> Intelligence
    Presentation --> StorageComm
    Graphics3D --> ComputeMath
    Intelligence --> Foundation
    StorageComm --> Foundation
```

---

## 2. Layered Subsystems Matrix

| Layer | Subsystem | Target Repository | Architectural Responsibility |
| :--- | :--- | :--- | :--- |
| **Layer 1: Foundation & Compute** | **`ZeroPrimitives`** | [`kzxl/ZeroPrimitives`](https://github.com/kzxl/ZeroPrimitives) | Allocation-free byte manipulation, integer/float span parsers, compiled fast object mappers. |
| | **`ZeroTensor`** | [`kzxl/ZeroTensor`](https://github.com/kzxl/ZeroTensor) | Multi-dimensional memory strides, zero-copy slicing, matrix multiplications, SVD/QR decomposition. |
| | **`ZeroCompute`** | [`kzxl/ZeroCompute`](https://github.com/kzxl/ZeroCompute) | Direct3D 11 GPGPU compute dispatcher via COM VTable interop, CPU SIMD Vector256 fallback. |
| | **`ZeroDocuments`** | [`kzxl/ZeroDocuments`](https://github.com/kzxl/ZeroDocuments) | Pure C# zero-dependency OpenXML Excel (.xlsx) reader/writer and RFC 4180 CSV engine. |
| | **`ZeroSystem`** | [`kzxl/ZeroSystem`](https://github.com/kzxl/ZeroSystem) | Sovereign Windows native subsystem, hardware inventory telemetry, and OS diagnostics. |
| **Layer 2: Storage & Comm** | **`ZeroData`** | [`kzxl/ZeroData`](https://github.com/kzxl/ZeroData) | Columnar data tables, zero-copy Arrow memory serialization, relational hash joins. |
| | **`ZeroStorage`** | [`kzxl/ZeroStorage`](https://github.com/kzxl/ZeroStorage) | Gorilla Delta-of-Delta + XOR floating-point time-series engine, write-ahead logging (WAL). |
| | **`ZeroComm`** | [`kzxl/ZeroComm`](https://github.com/kzxl/ZeroComm) | Asynchronous industrial master drivers (Modbus TCP/RTU, Mitsubishi MC Protocol 3E, Omron FINS). |
| | **`ZeroIoT`** | [`kzxl/ZeroIoT`](https://github.com/kzxl/ZeroIoT) | Industrial IoT edge connectors, MQTT client, OPC UA client and sensor telemetry bridge. |
| | **`ZeroRfid`** | [`kzxl/ZeroRfid`](https://github.com/kzxl/ZeroRfid) | EPC Gen2 / ISO 18000-6C RFID reader suite, sliding-window deduplication & hardware simulator. |
| | **`ZeroNetwork`** | [`kzxl/ZeroNetwork`](https://github.com/kzxl/ZeroNetwork) | High-performance network infrastructure, IP/CIDR math, IEEE OUI filtering, micro-HTTP server. |
| | **`ZeroSecurity`** | [`kzxl/ZeroSecurity`](https://github.com/kzxl/ZeroSecurity) | Pure C# cryptographic suite: BLAKE3/FastSha256, HMAC, HKDF, PBKDF2, ChaCha20/Poly1305. |
| **Layer 3: Intelligence & DSP** | **`ZeroInference`** | [`kzxl/ZeroInference`](https://github.com/kzxl/ZeroInference) | Polymorphic `IInferenceSession`, ONNX parser, CPU & OnnxRuntime GPU providers, YOLOv8/v11 decoders. |
| | **`ZeroNeural`** | [`kzxl/ZeroNeural`](https://github.com/kzxl/ZeroNeural) | Reverse-mode automatic differentiation (Autograd), deep learning layers, AdamW/SGD optimizer. |
| | **`ZeroSignal`** | [`kzxl/ZeroSignal`](https://github.com/kzxl/ZeroSignal) | In-place radix-2 FFT, STFT spectrograms, zero-phase Butterworth filter, Extended Kalman Filter. |
| | **`ZeroGeometry`** | [`kzxl/ZeroGeometry`](https://github.com/kzxl/ZeroGeometry) | 3D Iterative Closest Point (ICP), KdTree3D, 2D polygon Boolean clipping, Delaunay triangulation. |
| **Layer 4: Graphics & 3D** | **`ZeroGraphics`** | [`kzxl/ZeroGraphics`](https://github.com/kzxl/ZeroGraphics) | Render Hardware Interface (RHI - Null & D3D11), D3D11/D2D, 144Hz waveforms, computational photography, CV & Barcode HRI suite. |
| | **`ZeroTwin3D`** | [`kzxl/ZeroTwin3D`](https://github.com/kzxl/ZeroTwin3D) | Pure C# 3D digital twin spatial scene graph, Wavefront OBJ & glTF 2.0 / GLB 3D model loaders, D3D11 renderer. |
| | **`ZeroCharts`** | [`kzxl/ZeroCharts`](https://github.com/kzxl/ZeroCharts) | Direct2D GPU high-density telemetry strip charts, dynamic multi-axis graphs. |
| | **`ZeroAudioVisual`** | [`kzxl/ZeroAudioVisual`](https://github.com/kzxl/ZeroAudioVisual) | Acoustic predictive maintenance, multi-channel microphone array beamforming & defect localization. |
| **Layer 5: Presentation** | **`ZeroUI`** | [`kzxl/ZeroUI`](https://github.com/kzxl/ZeroUI) | 10M+ rows virtual grid, single-HWND D3DCanvas, 40+ SCADA controls, Media & Creative Suite, dark theme. |
| | **`ZeroPipeline`** | [`kzxl/ZeroPipeline`](https://github.com/kzxl/ZeroPipeline) | Kahn-sorted DAG inspection pipeline, JSON recipes, interactive graphical node graph canvas. |
| | **`ZeroReports`** | [`kzxl/ZeroReports`](https://github.com/kzxl/ZeroReports) | Pure C# high-speed PDF & industrial thermal barcode label rendering without GDI+ (ZPL/TSPL). |

---

## 3. Related Documentation

- For the exhaustive repository-by-repository technical catalog and test suite breakdown, see **[ZERO_PLATFORM_ECOSYSTEM.md](../ZERO_PLATFORM_ECOSYSTEM.md)**.
- For architectural proposals and roadmap expansions, see **[ZERO_PLATFORM_EXPANSION_PROPOSALS.md](../ZERO_PLATFORM_EXPANSION_PROPOSALS.md)**.
