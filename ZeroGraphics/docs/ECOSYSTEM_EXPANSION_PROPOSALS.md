# ZeroGraphics Ecosystem Expansion Proposals 🚀

> **Document Status:** Architectural Proposal & Roadmap  
> **Author:** Phong Võ  
> **Target Platform:** .NET Framework 4.6.2, .NET 8.0-windows, .NET Standard 2.0  

---

## 📌 Executive Context
Following the successful stabilization and production hardening of `ZeroGraphics.Imaging` (featuring high-performance CPU unmanaged image buffers, GPU Texture Recycling Pools, and a 13-kernel Direct3D 11 Render Graph with Operation Fusion), this document captures the official architectural proposals for expanding the ZeroGraphics ecosystem beyond Core, DirectX, Direct2D, Waveform, and Vision.

The prioritized active branch is **`ZeroGraphics.Vision`** (Machine Vision, Industrial Metrology & Template Matching). The proposals below represent the designated subsequent phases for future roadmap execution.

---

## 🖥️ Proposal A: `ZeroGraphics.Wpf` (Native Direct3D 11 Interop via `D3DImage`)

### 1. Problem Statement & Motivation
- WPF desktop applications in MES, SCADA, and telemetry typically host DirectX or WinForms canvases using `WindowsFormsHost`.
- **The Airspace Problem:** `WindowsFormsHost` creates a separate Win32 child window, preventing WPF popup menus, dropdowns, combo boxes, or floating tooltips from overlaying the rendering surface. WPF controls placed above the canvas are obscured or clipped.
- **Heavyweight Dependencies:** Existing workarounds rely on SharpDX (deprecated) or bulky interop libraries with high runtime allocations and GC overhead.

### 2. Architectural Design
- **Zero-Dependency Direct3D 9Ex / Direct3D 11 Sharing:**
  - Create a Direct3D 9Ex device (`IDirect3DDevice9Ex`) via COM VTable.
  - Allocate a shared render target texture on Direct3D 11 (`D3D11_RESOURCE_MISC_SHARED`).
  - Open the shared resource handle in Direct3D 9Ex via `IDirect3DDevice9Ex.CreateTexture`.
  - Bind the Direct3D 9Ex surface directly to WPF's native `System.Windows.Interop.D3DImage.SetBackBuffer`.
- **Performance Characteristics:**
  - **Zero-Copy VRAM Bridge:** GPU renders directly on Direct3D 11; WPF Desktop Window Manager (DWM) composites the surface natively.
  - **Airspace Elimination:** WPF tooltips, context menus, and vector overlays render cleanly on top with alpha blending.
  - **144+ FPS Sustained:** Renders in sync with WPF `CompositionTarget.Rendering`.

### 3. Package Structure & API Surface
- **Assembly:** `ZeroGraphics.Wpf.dll`
- **Target Frameworks:** `net462`, `net8.0-windows`
- **Core Components:**
  - `D3D11ImageSource`: Native wrapper around `D3DImage` with automatic shared texture synchronization and device loss recovery.
  - `ZeroWpfCanvas`: Reusable WPF custom control supporting direct GPU rendering callbacks.

---

## 📊 Proposal B: `ZeroGraphics.Chart` (High-Performance Industrial Dashboard & SCADA Charting)

### 1. Problem Statement & Motivation
- While `ZeroGraphics.Waveform` excels at continuous oscilloscope line strips (10M+ points at 144Hz), modern SCADA, MES, and manufacturing dashboards require multi-dimensional time-series, categorical metrics, statistical process control visualizers, and interactive inspection overlays.
- Traditional .NET chart libraries (LiveCharts, OxyPlot, MS Chart) allocate thousands of heap objects per redraw and drop below 15 FPS when rendering dense dataset series.

### 2. Architectural Design
- **Direct2D & Direct3D 11 Dual-Pipeline Architecture:**
  - **Data Layer:** Zero-allocation ring buffers and peak-preserving decimation (`MinMaxDecimation`, `LttbDecimation`).
  - **High-Speed Series Rasterizer:** Direct3D 11 instanced quads for Bar/Column charts; LineStrip for trend curves; point sprites for Scatter Plots.
  - **Subpixel Annotation & Typography:** Direct2D hardware-accelerated text formats for crisp legends, dynamic axis ticks, and tooltips.
  - **SPC & Quality Integration:** Native integration with `ZeroGraphics.Core.Analytics.SpcAnalysis` to plot dynamic Upper/Lower Control Limits ($UCL$, $LCL$), mean lines, and Nelson rule alert markers.
- **Interactive Gestures:**
  - Sub-4ms interactive panning and box zooming (`IDXGIDevice1.SetMaximumFrameLatency = 1`).
  - Real-time crosshair cursor with nearest-point snapping in $O(\log N)$ time.

### 3. Package Structure & API Surface
- **Assembly:** `ZeroGraphics.Chart.dll`
- **Target Frameworks:** `net462`, `net8.0-windows`
- **Supported Series:**
  - `LineSeries`, `AreaSeries`, `BarSeries`, `ScatterSeries`, `CandlestickSeries`, `HeatmapSeries`.
- **Key Controls:**
  - `ZeroChartCanvas`: Multi-series industrial dashboard viewport.

---

## 🌐 Proposal C: `ZeroGraphics.3D` (Digital Twin, CAD/STL Viewer & Factory Floor 3D)

### 1. Problem Statement & Motivation
- Industry 4.0 and Smart Factory applications increasingly demand 3D visualization for:
  - Computer-Aided Manufacturing (CAM) workpiece verification.
  - Automated Storage and Retrieval Systems (AS/RS) 3D rack visualization.
  - Robotic arm 6-axis kinematics and machine cell monitoring.
- Existing 3D engines (Helix Toolkit, OpenTK) pull substantial third-party dependency chains and lack integration with pure COM VTable zero-dependency architectures.

### 2. Architectural Design
- **Pure COM Direct3D 11 3D Pipeline:**
  - Universal 3D Vertex layout (`Position3D`, `Normal3D`, `TexCoord2D`).
  - Camera Controller: Orbit, Pan, Dolly, and First-Person camera models with quaternion rotation.
  - Shading Pipeline: Precompiled HLSL Vertex/Pixel shaders implementing Blinn-Phong lighting, directional sunlight, and ambient occlusion.
- **Industrial Mesh Loaders (Zero External NuGet):**
  - **Binary & ASCII STL Loader:** Fast streaming parser reading 3D triangle meshes directly into native GPU vertex/index buffers.
  - **Wavefront OBJ Loader:** High-throughput geometric face and material parser.
  - Frustum Culling: 3D Bounding Box (`BoundingBox3D`) and Octree spatial indexing for massive factory models.

### 3. Package Structure & API Surface
- **Assembly:** `ZeroGraphics.3D.dll`
- **Target Frameworks:** `netstandard2.0`, `net462`, `net8.0-windows`
- **Key Components:**
  - `StlMeshReader`: Zero-allocation streaming STL binary/ASCII parser.
  - `OrbitCamera`: 60–144Hz smooth interactive 3D viewport control.
  - `Zero3DCanvas`: Direct3D 11 3D scene viewport with hardware anti-aliasing (MSAA).

---

## 📋 Implementation Priority Matrix

| Phase | Package | Value Proposition | Dependencies |
| :---: | :--- | :--- | :--- |
| **Phase 1** *(In Progress)* | **`ZeroGraphics.Vision`** | Automated Optical Inspection (AOI), Template Matching, Metrology Calipers | `ZeroGraphics.Imaging`, `ZeroGraphics.Core` |
| **Phase 2** | **`ZeroGraphics.Wpf`** | WPF `D3DImage` native interop, zero airspace issues, alpha blending | `ZeroGraphics.DirectX`, `ZeroUI.Wpf` |
| **Phase 3** | **`ZeroGraphics.Chart`** | SCADA/MES multi-series charting, SPC limits, high-frequency streaming | `ZeroGraphics.Core`, `ZeroGraphics.Direct2D` |
| **Phase 4** | **`ZeroGraphics.3D`** | STL CAD viewer, robot kinematics, 3D smart factory layouts | `ZeroGraphics.DirectX`, `ZeroGraphics.Core` |
