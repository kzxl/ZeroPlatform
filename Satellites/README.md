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

| Satellite | Responsibility | Target Technology |
| :--- | :--- | :--- |
| **`ZeroPlatform.Charts`** | Financial, industrial telemetry, and Gantt charts | D3D11 LineStrip + Instanced Quads |
| **`ZeroPlatform.IoT`** | Industrial protocol connectors (Modbus, OPC-UA, MQTT) | High-throughput memory pipeline |
| **`ZeroPlatform.Reports`** | Document and label generation, thermal printer engine | DirectWrite vector rendering |
| **`ZeroPlatform.AudioVisual`** | Real-time audio spectrum analyzer and video canvas | D3D11 Texture streaming |
