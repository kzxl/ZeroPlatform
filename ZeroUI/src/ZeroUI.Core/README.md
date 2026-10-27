# ZeroUI.Core ⚡

Ultra-high-performance, zero-allocation core runtime, analytical engines, and industrial automation infrastructure for .NET (`netstandard2.0`, `net462`, `net8.0`).

> [!IMPORTANT]
> **Zero External Dependencies:** `ZeroUI.Core` contains **0 third-party package dependencies**. All memory pools, math decimation, TagEngine, validation, serialization, and state machines run purely on .NET BCL.

[![ZeroPlatform Ecosystem](https://img.shields.io/badge/ZeroPlatform-Ecosystem-blueviolet.svg)](https://github.com/kzxl/ZeroPlatform)
[![NuGet Version](https://img.shields.io/badge/nuget-v1.10.0-blue.svg)](https://github.com/kzxl/ZeroUI)
[![Dependencies](https://img.shields.io/badge/dependencies-0%20external-brightgreen.svg)](#-key-features)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](https://github.com/kzxl/ZeroUI)
[![GitHub](https://img.shields.io/badge/GitHub-kzxl%2FZeroUI-blue.svg)](https://github.com/kzxl/ZeroUI)

---

## 🌟 Key Features

### ⚡ Automatic Render Optimizer Engine
* **Mathematical Cost Router (`ZeroRenderAnalyzer`):** Analyzes rendering primitives in real-time, mathematically comparing software convolution $O(W \times H \times R^2)$ with parallel GPU ALU $O(1)$. Evaluates **6.95 Million decisions/sec** (143.8 ns/op, 0 allocations).
* **Analytical SDF Shader Registry (`ZeroShaderRegistry`):** Closed-form Signed Distance Field (SDF) box shadow, normalized Gaussian kernel, and radial neon bloom falloff formulas (>50M evaluations/sec).
* **9-Slice Shadow Atlas (`ZeroShadowAtlas`):** Geometrically instanced 9-slice corner/edge cache eliminating 95% of GPU draw calls with 100% cache hit rate (15.4M lookups/sec, 0 heap allocations).
* **Adaptive Frame-Budget Monitor (`ZeroAdaptiveRenderMonitor`):** Rolling 16.6ms frame-budget tracking with automated fidelity transitions (`Ultra` 144 FPS, `Balanced` 60 FPS, `PowerSaver` throttled).
* **Hardware GPU Tier Telemetry (`ZeroGpuCapabilities`, `HardwareGpuTier`):** Direct DXGI hardware adapter enumeration via COM VTable (`Tier0_Software`, `Tier1_Integrated`, `Tier2_Discrete`) and dedicated VRAM querying.

### ⚡ Zero-Alloc Virtualization & Analytical Engines
* **Zero-Allocation Data Virtualization (`IZeroVirtualSource`, `RowIndexMap`):** High-speed pointer swap sorting, debounced search filtering (150ms), and streaming CSV export.
* **Hierarchical Row Mapping (`GroupedRowIndexMap`):** Virtualized multi-level row grouping with expand/collapse states and group summary aggregations.
* **OLAP Pivot Engine (`PivotDataEngine`):** Multidimensional cross-tab aggregation matrix engine supporting Row/Column dimensions, summary measures (`Sum`, `Count`, `Average`, `Min`, `Max`), and hierarchical rollups.
* **Visual Range & Timeline Engine (`RangeModel`, `DateTimeRangeModel`):** High-precision range math supporting continuous/discrete numeric values, DateTime intervals, and snap-to-tick rounding.

### 🛡️ Form Validation & Internationalization Engine
* **Declarative Validation Engine (`ValidationProvider`, `IControlValidationRule`):** Extensible validation framework with built-in rules (`NotEmpty`, `Range`, `Regex`, `Email`, `Length`, `CustomPredicate`).
* **Runtime Dynamic Localization (`LocalizationManager`, `Localizer`, `L`):** Ultra-fast, lock-free, zero-allocation runtime string localization engine with instant culture hot-switching (`en-US`, `vi-VN`) without restarting the application, streaming JSON scanner (`JsonScanner`), cascading fallback resolution, and built-in enterprise dictionaries.
* **Standardized Editor & Binding Contracts (`IZeroEditor`, `ZeroDataBinder`):** Unified contract (`EditValue`, `IsModified`, `ReadOnly`, `ResetModified()`) enabling fluent two-way binding, dirty state tracking, and validation integration.

### 🏭 Industrial Telemetry, SCADA & Decimation
* **Real-Time Tag Engine (`ZeroTagEngine` v2, `TagStorage`):** High-throughput in-memory tag registry with deadband jitter filtering, OPC DA/UA quality codes (`Good`, `Bad`, `Uncertain`), and multi-worker concurrent updates (>48M writes/s, >244M reads/s).
* **Cross-Platform Industrial State Machines (`ZeroUI.Core.Industrial`):** Pure platform-neutral state definitions such as `SevenSegmentState`, `SevenSegmentColorProfile`, and `SevenSegmentDisplayMode` providing bitmask encoding/decoding and segment computations shared between WinForms and WPF.
* **Telemetry Historian Interfaces (`ITelemetryHistorian`, `InMemoryHistorian`):** Extensible time-series persistence contracts. Production SQLite WAL engine is provided via companion package `ZeroUI.Historian.Sqlite`.
* **Store & Forward Worker (`StoreAndForwardWorker`):** Resilient edge-to-cloud disk caching during network disconnections with auto-draining.
* **LTTB Decimation (`LttbDecimation`):** Zero-alloc Largest-Triangle-Three-Buckets algorithm compressing 10,000,000 raw points to 2,000 screen pixels in ~29 ms with 0 bytes GC allocated.
* **TimeSeries Continuous Rollups (`TimeSeriesPyramid`):** Multi-resolution continuous rollups (L0: raw, L1: 100ms, L2: 1s, L3: 10s, L4: 1min, L5: 10min) powering instant $O(\text{screen pixels})$ chart zoom.
* **ISA-18.2 Alarm Engine (`ScadaAlarmEngine`):** Thread-safe alarm lifecycle management (`ActiveUnack`, `ActiveAck`, `ClearedUnack`, `Normal`, `Shelved`, `Suppressed`) with audit trails.

### 🔌 Industrial Field Communication & Protocols
* **Modbus TCP Master (`ModbusTcpAdapter`):** Native zero-allocation client supporting FC 01, 02, 03, 04, 05, 06, 15, 16.
* **Siemens S7 Adapter (`SiemensS7Adapter`):** High-speed ISO-on-TCP (RFC 1006 / COTP) and S7 PDU client for Siemens S7-300, S7-400, S7-1200, and S7-1500 PLCs.
* **Modbus Address Planner (`ModbusAddressPlanner`):** Industrial register address coalescer grouping disjoint tags into optimized contiguous block reads (up to 98.3% network packet reduction).

### ⚙️ MES & Manufacturing Engines
* **PackML State Machine (`PackMlStateMachine`):** Complete ISA-TR88.00.02 packaging machine state machine modeling all 17 standard states.
* **OEE Engine (`OeeEngine`):** Real-time Overall Equipment Effectiveness calculator (Availability, Performance, Quality, scrap defect tracking).
* **Smart Warehouse Route Optimizer (`GuidedPickingEngine`):** Warehouse picking route optimization with 5-tier spatial coordinate routing (`WarehouseLocation`) and FIFO/FEFO priority enforcement.

---

## 📦 Installation

```powershell
dotnet add package ZeroUI.Core --version 1.10.0
```

---

## 🚀 Quick Example

```csharp
using ZeroUI.Core.Localization;
using ZeroUI.Core.Validation;

// 1. Dynamic Runtime Localization
LocalizationManager.SetLanguage("vi-VN");
string saveText = L.T("Common.Save"); // "Lưu"
string okText = Localizer.GetString(StringId.Ok); // "Đồng ý"

// 2. High-Performance Form Validation
var validator = new ValidationProvider();
validator.RuleFor("Quantity")
         .NotEmpty()
         .Range(1, 1000)
         .WithMessage("Quantity must be between 1 and 1,000 units.");

var result = validator.ValidateField("Quantity", 1500);
if (!result.IsValid)
{
    Console.WriteLine($"Validation Error: {result.ErrorMessage}");
}
```

Full documentation and source code: [github.com/kzxl/ZeroUI](https://github.com/kzxl/ZeroUI)
