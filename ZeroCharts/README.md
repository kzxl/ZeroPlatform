# ZeroCharts: High-Density Industrial Telemetry & Analytical Charts for .NET 📊

[![ZeroPlatform Tier](https://img.shields.io/badge/ZeroPlatform-Tier%204%20(Graphics%20%26%20Spatial%203D)-ea580c.svg)](https://github.com/kzxl/ZeroPlatform)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET Multi-Targeting](https://img.shields.io/badge/.NET-8.0--windows%20%7C%204.6.2-purple.svg)](https://dotnet.microsoft.com/)
[![NuGet Version](https://img.shields.io/badge/NuGet-1.2.0-blue.svg)](https://www.nuget.org/packages/ZeroCharts)
[![Unit Tests](https://img.shields.io/badge/tests-13%20passed%20(100%25)-brightgreen.svg)](#)

**ZeroCharts** is a high-density telemetry and analytical charting suite engineered in 100% pure C# for industrial automation, manufacturing MES/SCADA dashboards, and scientific instrumentation.

Operating as a specialized visual analytics member of **Tier 4 (Graphics & Spatial 3D)** within the **[ZeroPlatform](https://github.com/kzxl/ZeroPlatform)** ecosystem.

---

## 🏛️ Ecosystem Architectural Alignment

- **Architectural Tier**: **Tier 4 (Graphics & Spatial 3D)**
- **Permitted Upstream Dependencies**: Tier 0 (`ZeroPrimitives`, `ZeroConcurrency`), Tier 1 (`ZeroCompute`, `ZeroTensor`), Tier 2 (`ZeroData`, `ZeroStorage`), Tier 4 (`ZeroGraphics`)
- **Downstream Consumers**: Tier 5 (`ZeroUI`, `ZeroReports`, `ZeroPipeline`)
- **Core Guarantees**: Pure Direct2D / DirectX acceleration, LTTB decimation, sub-millisecond refresh, zero upward dependency.

---

## 🚀 Key Features & Supported Series

### 1. Comprehensive Industrial Chart Paradigms
- **`BarSeries`**: Clustered and categorical bar charts with auto-scaling `CategoryAxis`, per-bar custom coloring, and on-bar data value labels.
- **`PieSeries` / `DoughnutSeries`**: Angular proportional pie/doughnut slices with inner hole radius ratio, slice explosion, percentage metrics, and radial angle hit-testing.
- **`AreaSeries`**: Filled area regions under curves with smooth vertical alpha gradients and LTTB downsampling for streaming telemetry envelopes.
- **`ScatterSeries`**: Multi-marker geometric points (Circle, Square, Diamond, Triangle, Cross) with spatial nearest-point search for statistical process control (SPC) and tolerance correlation.
- **`RadarSeries`**: Polar multi-axial radar/spider grids for multi-dimensional KPI performance comparison.
- **`LineSeries`**: High-frequency streaming line series with LTTB (Largest-Triangle-Three-Buckets) peak-preserving decimation.
- **`CandlestickSeries`**: High-density OHLC financial & quality inspection candlesticks with body/wick styling.
- **`HeatmapSeries`**: 2D spatial thermal matrices with smooth thermal gradient mapping.
- **`GanttSeries`**: Industrial job shop schedules and timeline task tracks.

### 2. Interactive Controls & Usability
- **Interactive Legend (`ChartLegend`)**: Dynamic placement (Top, Bottom, Left, Right) with click-to-toggle series visibility.
- **Smart Data Inspector Tooltip**: Hover card inspector displaying series badge, category, value, and target highlight circle.
- **Pan & Zoom**: Right-click drag panning and mouse-wheel zooming with auto-fit boundaries.
- **Headless Snapshot Export**: `ToBitmap()` and `SaveImage(path, format)` for headless export into reporting pipelines.

---

## 📦 Multi-Targeting

- `.NET 8.0-windows`
- `.NET Framework 4.6.2`

---

## 🧪 Testing & Verification

```bash
dotnet test tests/ZeroCharts.Tests/ZeroCharts.Tests.csproj
```

---

## 📄 License & Author

MIT License. Copyright © 2026 **Phong Võ** (`kzxl`).
