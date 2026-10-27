# ZeroGraphics.Waveform ⚡

Hardware-accelerated real-time oscilloscope, sensor waveform, and industrial telemetry chart canvas for Windows Forms (`net462`, `net8.0-windows`).

---

## 🌟 Key Features

* **Direct3D 11 Hardware LineStrip Pipeline (`WaveformPipeline`):** Direct GPU primitive generation, streaming millions of sensor telemetry points without CPU/GDI overhead.
* **Continuous Subsampling & Decimation:** Seamlessly integrates with peak-preserving MinMax and LTTB decimation from `ZeroGraphics.Core` to eliminate GPU bus saturation during zoom-out.
* **Flicker-Free On-Demand Presentation (`ZeroWaveformCanvas`):** Hosts a DXGI swapchain directly on the WinForms HWND, maintaining 60–144 FPS delivery.
