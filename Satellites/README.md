# ZeroPlatform Satellites Architecture Guide

Satellites are modular, domain-specific extensions that build upon the core foundations of **ZeroPlatform**:
- **ZeroUI**: High-performance UI control system, docking, theme engine, and industrial SCADA components.
- **ZeroGraphics**: Direct3D 11 / Direct2D hardware-accelerated rendering pipelines, analytical SDF cards, and real-time waveform oscilloscopes.

---

## 🎯 Satellite Principles

1. **Decoupled Autonomy**:
   - Each satellite should reside in its own folder under `Satellites/` and can optionally maintain its own Git repository or package lifecycle.
   - Satellites must never modify the core codebase of `ZeroUI` or `ZeroGraphics`.

2. **Dual-Targeting Support**:
   - Satellites targeting desktop applications should support both `.NET Framework 4.6.2` (for legacy enterprise environments) and `.NET 8.0-windows` (for modern platforms).

3. **Hardware Acceleration via ZeroGraphics**:
   - If a satellite requires high-frequency rendering (e.g. streaming time-series charts, 2D vector diagrams, thermal maps), it should interface with `ZeroGraphics.DirectX` or `ZeroGraphics.Direct2D`.

---

## 🚀 Planned Satellites Pipeline

👉 **[Read the Full Satellite Systems Architecture & Expansion Roadmap](../../docs/architect/satellite-expansion-roadmap.md)**

| Satellite | Responsibility | Target Technology | Status |
| :--- | :--- | :--- | :---: |
| **`ZeroPlatform.IoT`** | Industrial protocol connectors (OPC-UA Binary, MQTT 3.1.1/5.0, Sparkplug B) | High-throughput memory pipeline & Pure C# Sockets | **Active (P0)** |
| **`ZeroPlatform.Charts`** | Financial, industrial telemetry, Gantt charts, and 3D heatmaps | D3D11 LineStrip + Instanced Quads | Planned (P1) |
| **`ZeroPlatform.Reports`** | Document and label generation, thermal printer engine (ZPL II/TSPL), vector PDF | DirectWrite vector rendering & Zero-dependency PDF | Planned (P1) |
| **`ZeroPlatform.AudioVisual`** | Real-time audio spectrum analyzer and video canvas | WASAPI + D3D11 Texture streaming | Planned (P2) |
| **`ZeroPlatform.Twin3D`** | 3D Digital Twin, STL/OBJ CAD mesh loader, 6-axis robot kinematics | Direct3D 11 + ZeroGeometry PointCloud | Planned (P2) |
