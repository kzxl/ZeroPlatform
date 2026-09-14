# ZeroPlatform Satellites Architecture Guide

Satellites are modular, domain-specific extensions that build upon the core foundations of **ZeroPlatform**:
- **ZeroUI**: High-performance UI control system, docking, theme engine, and industrial SCADA components.
- **ZeroGraphics**: Direct3D 11 / Direct2D hardware-accelerated rendering pipelines, analytical SDF cards, and real-time waveform oscilloscopes.

---

## 🎯 Satellite Principles

1. **Decoupled Autonomy**:
   - Each satellite resides in its own root-level subsystem folder (e.g. `ZeroIoT`, `ZeroCharts`, `ZeroReports`, `ZeroAudioVisual`, `ZeroTwin3D`) and maintains its own package and repository lifecycle.
   - Satellites must never modify the core codebase of `ZeroUI` or `ZeroGraphics`.

2. **Dual-Targeting Support**:
   - Satellites targeting desktop applications should support both `.NET Framework 4.6.2` (for legacy enterprise environments) and `.NET 8.0-windows` (for modern platforms).

3. **Hardware Acceleration via ZeroGraphics**:
   - If a satellite requires high-frequency rendering (e.g. streaming time-series charts, 2D vector diagrams, thermal maps), it should interface with `ZeroGraphics.DirectX` or `ZeroGraphics.Direct2D`.

---

## 🚀 Planned Satellites Pipeline

👉 **[Read the Full Satellite Systems Architecture & Expansion Roadmap](../../../docs/architect/satellite-expansion-roadmap.md)**

| Satellite | Responsibility | Target Technology | Status |
| :--- | :--- | :--- | :---: |
| **`ZeroIoT`** | Industrial protocol connectors (OPC-UA Binary, MQTT 3.1.1/5.0, Sparkplug B) | High-throughput memory pipeline & Pure C# Sockets | **Completed (P0)** |
| **`ZeroCharts`** | High-density telemetry plots (10M+ pts), LTTB decimation, Candle, Heatmap, Gantt | D3D11 Instanced Quads & WinForms/ZeroUI controls | **Completed (P1)** |
| **`ZeroReports`** | Document and label generation, thermal printer engine (ZPL II/TSPL), vector PDF | Pure C# Vector PDF 1.4 & Industrial Thermal Codecs | **Completed (P1)** |
| **`ZeroAudioVisual`** | Real-time acoustic vibration monitoring, STFT Spectrogram, Bearing fault detection, RTP/H.264 | Zero-allocation Ring Buffers & Industrial RTP streaming | **Completed (P2)** |
| **`ZeroTwin3D`** | 3D Digital Twin, STL/OBJ CAD mesh loader, 6-axis & SCARA robot kinematics, safety zones | Pure C# 3D Math, DH Solvers & Spatial Scene Graphs | **Completed (P2)** |
