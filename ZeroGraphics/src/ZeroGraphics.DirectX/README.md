# ZeroGraphics.DirectX ⚡

High-performance, zero-allocation Direct3D 11 & DXGI rendering pipeline with hardware-accelerated HLSL Signed Distance Field (SDF) effects for Windows Forms (`net462`, `net8.0-windows`).

---

## 🌟 Key Features

* **Direct3D 11 & DXGI SwapChain Pipeline (`HwndSwapChain`):** Direct hardware acceleration bound directly to native WinForms HWNDs without DWM composition overhead.
* **Pure COM VTable Dispatch (`ComVTableHelper`):** Zero external dependencies (no SharpDX or Vortice.Windows required). Featherweight footprint (< 150 KB) with 100% compatibility across .NET Framework 4.6.2 and .NET 8.0+.
* **Real-time Hardware GPU Telemetry (`D3D11DeviceManager`):** Direct DXGI 1.1 hardware adapter enumeration via COM VTable (`Tier0_Software`, `Tier1_Integrated`, `Tier2_Discrete`), populating `ZeroGpuCapabilities` with live VRAM and GPU specs.
* **Analytical Signed Distance Field (SDF) Shaders (`SdfCardPipeline`):** Embedded precompiled Shader Model 4.0 bytecode executing anti-aliased corner rounding, smooth borders, soft drop shadows, and neon bloom glows in a single GPU pass ($O(1)$) at >1,000 FPS.
* **Zero-Flicker On-Demand Canvas (`ZeroDirectXCanvas`):** Event-driven rendering engine trapping `WM_ERASEBKGND` and painting only when invalidated or driven by animation clock (0% CPU/GPU when idle).

---

## 🚀 Quick Example

```csharp
using System.Drawing;
using System.Windows.Forms;
using ZeroGraphics.DirectX.Controls;

public class MainForm : Form
{
    public MainForm()
    {
        var canvas = new ZeroDirectXCanvas
        {
            Dock = DockStyle.Fill,
            CornerRadius = 12f,
            Elevation = 10f,
            BlurRadius = 20f,
            CardColor = Color.FromArgb(22, 27, 38),
            CardBorderColor = Color.FromArgb(42, 51, 71),
            GlowColor = Color.FromArgb(0, 229, 255),
            GlowIntensity = 0.8f // Neon glow
        };

        Controls.Add(canvas);
    }
}
```
