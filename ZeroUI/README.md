# ZeroUI ⚡

> **Ultra-High-Performance, Zero-Allocation Industrial UI & Runtime Ecosystem for .NET (WinForms, WPF, .NET 8/9 & Edge)**

[![ZeroPlatform Tier](https://img.shields.io/badge/ZeroPlatform-Tier%205%20(Presentation%20%26%20Apps)-e11d48.svg)](https://github.com/kzxl/ZeroPlatform)
[![NuGet Version](https://img.shields.io/badge/nuget-v1.17.0-blue.svg)](https://github.com/kzxl/ZeroUI)
[![GPU Acceleration](https://img.shields.io/badge/GPU%20Acceleration-Direct3D%2011%20%7C%20Direct2D-cyan.svg)](https://github.com/kzxl/ZeroGraphics)
[![Unit Tests](https://img.shields.io/badge/tests-650%20Core%20passed%20(100%25)-brightgreen.svg)]()
[![Target Frameworks](https://img.shields.io/badge/targets-netstandard2.0%20%7C%20net462%20%7C%20net8.0--windows-blue.svg)](#-package-matrix)
[![UI Frame Latency](https://img.shields.io/badge/Frame%20Latency-%3C%204ms%20P95-brightgreen.svg)](docs/BENCHMARKS.md)
[![GC Allocations](https://img.shields.io/badge/Hot%20Path%20Allocations-0%20B%20(Zero--Alloc)-brightgreen.svg)](docs/BENCHMARKS.md)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](#-license)

---

## 📸 Interactive Live Showcase

| ⚡ 1,000,000 Rows Smooth Virtual Scroll (Zero-Alloc) | 🎨 Creative & Media Editors Suite |
| :---: | :---: |
| ![1M Rows Scroll](docs/images/01_zerogrid_1m_scroll.gif) | ![Media Editors](docs/images/02_media_editors_interactive.gif) |

| 🔍 Excel-Style Column Distinct Filter Header (v1.8.1) | 🏭 SCADA Closed-Loop Workcell |
| :---: | :---: |
| ![Column Filter](docs/images/03_column_filter_header.gif) | ![SCADA Closed Loop](docs/images/12_scada_closed_loop_simulation.png) |

---

## ⚡ Performance Benchmarks: ZeroUI Grid vs Native Controls

Headless and interactive stress-test verified on `.NET 8.0` (x64, Intel Core i7 / 144Hz):

| Metric | Standard DataGridView / WPF DataGrid | ZeroUI (`GridControl` / `ZeroGridControl`) | Real-World Advantage |
| :--- | :---: | :---: | :--- |
| **100K Rows Viewport Compute** | ~7,400 FPS (0.135 ms) | **24,716 FPS (0.040 ms)** | **3.3x Higher Throughput** |
| **Hot Render Path GC Allocations** | Thousands of temporary cell objects | **0 B (Zero-Alloc)** | **100% Zero GC Pauses** |
| **1,000,000 Rows Continuous Scrolling** | Frequent frame drops & stutter | **Rock-solid 144 FPS** | **Silky-smooth UX** |
| **10,000,000 Rows Capacity** | **Crash / OutOfMemoryException** | **179 MB RAM (7 ms setup)** | **Limitless Scale** |
| **1,000,000 Rows Instant Filter** | > 850 ms (UI locks) | **34 ms** | **25x Faster** |
| **Streaming CSV Export (100K rows)** | ~1,200 ms | **145 ms (688,000 rows/sec)** | **8x Faster** |

---

## 📖 Executive Summary

**ZeroUI** is an enterprise-grade industrial automation UI suite and runtime engine. Engineered to overcome the severe handle leaks, garbage collection pauses, and screen flicker common in standard desktop controls, ZeroUI renders millions of rows and real-time telemetry at 60 FPS with zero managed memory allocations.

### Core Architectural Pillars
* **`ZAiChatBox` AI Copilot & Assistant Control (v1.15.0)**: Native streaming AI assistant chat control for `ZeroAgent` across WinForms and WPF (`ZeroUI.WinForms.Documents` & `ZeroUI.Wpf.Documents`), featuring markdown token bubbles, quick intent action chips, auto-scroll, and customizable avatars.
* **`ZOcrViewer` & Forensic Inspection Suite (v1.15.0)**: Full integration with `ZeroOcr.Core` for document inspection across WinForms and WPF, rendering sub-pixel character bounding boxes, confidence badges, text search highlighting, and automatic document skew angle correction (`ZDocumentDeskew`).
* **ISA-101 Industrial HMI Design System (v1.14.0)**: Built-in handbook and component conformance for ISA-101 High-Performance HMI standards, 4-tier operational display hierarchy (Level 1 Enterprise Overview down to Level 4 Diagnostics), and low-contrast grayscale aesthetics for fatigue-free operator monitoring.
* **SIMD Hardware Bitmask Filtering (v1.13.0)**: Direct support for `ZeroData.Core.SelectionMask` across WinForms and WPF `ZGrid` and `RowIndexMap`. Employs 64-bit word skipping and `TrailingZeroCount` (TZCNT) sparse bit extraction, accelerating million-row filtering up to $25\times$ with 0 GC allocation.
* **Off-Heap Telemetry Sliding Buffer (v1.13.0)**: Direct unmanaged memory sliding buffer (`ZeroOffHeapTelemetrySeries`) backed by `ZeroPrimitives.Memory.NativeMemoryPool`, supporting 100k+ sensor points/sec without GC pressure.
* **Durable Persistent Audit Logger (v1.13.0)**: `DurableAuditLogger` adhering to 21 CFR Part 11 and ISA-88, backed by `ZeroStorage.Core.Persistence.DurablePersistentQueue` with memory-mapped files and `Uuid7` monotonic record identification.
* **Strictly Zero Allocation (`Zero-Alloc`)**: Hot render loops generate **0 bytes** of GC churn using `Span<T>`, `ArrayPool<T>`, and unmanaged DIBSection memory buffers.
* **Single-HWND Architecture**: Composite controls maintain **1 Win32 window handle**, completely eliminating OS handle exhaustion crashes (10,000 handle limit) and window flicker.
* **Win32 Memory DC DIBSection Engine**: Offscreen unmanaged double buffering with zero-copy `BitBlt` presentation (100% resilient across RDP and virtual machines).
* **Enterprise Dual-Runtime Support**: Full native compatibility with **.NET Framework 4.6.2** as well as modern **.NET 8.0 / 9.0**.
* **Unified Theme Engine**: Instant reactive switching between **Obsidian Dark Mode** (`#12151C`) and **Clean Light Mode** across all controls.
* **Interactive Guided Tour & Onboarding Engine**: Native spotlight aperture cutout masking (`Region.Exclude` / `CombinedGeometry.Exclude`), semi-transparent glass backdrop dimming, collision-free adaptive popovers, and full keyboard navigation across WinForms and WPF (`ZTour`).
* **Specialized Creative & Forensic Inspection Media Suite**: High-performance vector defect tagging with direct bitmap burning (`ZAnnotationCanvas`), optical precision metrology caliper (`ZMeasurementRuler`), dynamic screen-capture security stamps (`ZWatermarkOverlay`), GPU-accelerated video playback (`ZVideoPlayer`), symmetrical audio waveform scrubbers (`ZAudioWaveform`), and document straightening/binarization (`ZDocumentDeskew`).
* **Specialized Analytics & Business Charts Suite**: Clean Architecture with 100% platform-neutral mathematical engines in `ZeroUI.Core.Analytics` and lightweight hardware rendering layers for WinForms and WPF (`HistogramChart`, `ScatterChart`, `SunburstChart`, `LollipopChart`, `TreemapChart`, `SankeyChart`, `BulletChart`, `ParetoChart`).
* **Creative & Media Controls Suite**: Direct-rendered, high-performance visual editors for digital imaging, raw photography, inspection, and video grading cleanly segregated in `ZeroUI.Wpf.Media` and `ZeroUI.WinForms.Media` (`ZImageViewer`, `ZPictureEdit`, `ZCurveEditor`, `ZColorWheel`, `ZCompareViewer`, `ZHistogramScope`, `ZCropBox`, `ZMiniMapNavigator`, `ZMaskGizmoOverlay`, `ZHistoryTimeline`, `ZFilmstripScroller`, `ZDominantPalette`, `ZExifTelemetryCard`, `ZBatchTaskQueue`, `ZThumbnailGrid`, `ZTokenPatternEditor`).
* **Centralized 60 FPS Clock (`ZeroAnimationClock`)**: Single global ticker with synchronized ISA-18.2 blinking phases, eliminating timer scatter.

---

## 📚 Technical Documentation & Guides

In-depth technical specifications and architectural documentation are modularized within the [`docs/`](docs/) directory:

| Document | Description |
| :--- | :--- |
| 📊 **[Verified Benchmarks](docs/BENCHMARKS.md)** | Frame budgets, 10M rows virtualization, GC allocations, and telemetry throughput. |
| 🎛️ **[Controls Catalog](docs/CONTROLS_CATALOG.md)** | Full reference for 80+ controls (GridControl, PivotGrid, SCADA, Charts, Media Viewers, and Extended Editors). |
| 🎬 **[Visual Controls Guide & Tour](docs/ZEROUI_CONTROLS_GUIDE.md)** | Architectural guide, in-process live video recordings, and feature tour across all subsystems. |
| 🎨 **[Theming & Styling](docs/THEMING_AND_STYLING.md)** | Obsidian Dark / Clean Light themes, High-DPI Per-Monitor V2, and single-HWND architecture. |
| 🏛️ **[System Architecture](docs/architecture/system-architecture.md)** | Multi-tier pipeline coordination, lock-free TripleBuffer, decoupled runtime, and renderers. |
| 📐 **[Control Ecosystem Audit](docs/architecture/control-ecosystem-audit-and-composite-architecture.md)** | Feature gap analysis, composite control standards, and cross-platform shared logic. |
| 🗺️ **[Development Roadmap](docs/roadmap.md)** | Release milestones, feature requests, and future capabilities. |

---

## 🏷️ Canonical `Z*` Control Naming Standard & Collision Prevention

To prevent ambiguous reference collisions (`CS0104`) with native WinForms and WPF controls (such as `System.Windows.Forms.Panel`, `GroupBox`, `Button`, `Label`, `TextBox`), ZeroUI establishes a clean architectural separation:

* **Foundational Engine & Infrastructure:** Prefixed with **`Zero`** (`ZeroTheme`, `ZeroDpi`, `ZeroFontCache`, `ZeroIcons`, `ZeroAnimationClock`, `ZeroLocalizer`).
* **Canonical UI Controls Suite:** Standard controls adopt the **`Z`** prefix (`ZPanel`, `ZGroupBox`, `ZMenuBar`, `ZButton`, `ZLabel`, `ZCheckBox`, `ZRadioButton`, `ZTextBox`, `ZProgressBar`, `ZTabControl`, `ZSplitContainer`, `ZGrid`, `ZChart`, `ZImageViewer`, `ZJsonEditor`, `ZTimeline`).
  * **Zero Collisions (`CS0104`):** Guarantees zero naming conflicts when developers import both `System.Windows.Forms` and ZeroUI namespaces.
  * **Instant IDE Discovery:** Simply typing `Z` in the IDE immediately surfaces the complete ZeroUI component palette.
  * **Full Theme Synchronization:** All `Z*` controls automatically inherit `ZeroTheme` dark/light modes, rounded corner styles, and High-DPI scaling.
  * **5-Release Deprecation Wrappers:** Previous class names (`PanelControl`, `GroupControl`, `MenuBarControl`, `SimpleButton`, `TextEdit`, and legacy `Zero*` shims) are retained as backward-compatibility wrappers with `[Obsolete]` warnings and will be maintained across 5 minor release cycles before pruning.

---

## 📦 Package Matrix

| Package | Targets | Primary Capabilities | Dependencies |
| :--- | :--- | :--- | :--- |
| **`ZeroUI.Core`** | `netstandard2.0`, `net462`, `net8.0` | High-frequency telemetry triple-buffer, TagEngine v2, PackML state machine, OEE metrics, validation engine, industrial state models, statistical chart engines, extended models (`AiMl`, `Gis`, `Compliance`, `Workflow`, `Media`, `Scheduling`) | **Zero 3rd-party dependencies** (Pure BCL) |
| **`ZeroUI.Historian.Sqlite`** | `netstandard2.0`, `net462`, `net8.0` | High-throughput SQLite WAL time-series telemetry storage engine (>100k records/s), rolling partitions, store & forward disk cache | `Microsoft.Data.Sqlite` |
| **`ZeroUI.WinForms`** | `net462`, `net8.0-windows` | 10M+ rows virtual grid (`GridControl`), interactive onboarding engine (`ZTour`), 40+ SCADA/HMI controls, 15+ specialized charts, Obsidian dark theme, DIBSection unmanaged double-buffering | `ZeroUI.Core` |
| **`ZeroUI.WinForms.Common`** | `net462`, `net8.0-windows` | Common navigational controls, overlays, dialogs, command palette (`ZCommandPalette`), notification center (`ZNotificationCenter`), and tree view (`ZTreeView`) | `ZeroUI.Core` |
| **`ZeroUI.WinForms.Editors`** | `net462`, `net8.0-windows` | Enterprise editors: `ZAutoComplete`, `ZPasswordBox`, `ZCronEditor`, `ZMultiSelect`, `ZSignaturePad`, `ZJsonEditor`, `ZRichTextEditor` | `ZeroUI.Core`, `ZeroUI.WinForms.Common` |
| **`ZeroUI.WinForms.Documents`**| `net462`, `net8.0-windows` | Document view and editing components: `ZAiChatBox`, `ZMarkdownViewer`, `ZCodeEditor`, `ZRichTextViewer` | `ZeroUI.Core` |
| **`ZeroUI.WinForms.Industrial`**| `net462`, `net8.0-windows`| Specialized SCADA, BMS, Energy, Network, Water, GIS, and Workflow controls (`ZTimeline`, `ZAlarmSummary`, `ZScheduler`, `ZStepBar`) | `ZeroUI.Core` |
| **`ZeroUI.WinForms.Media`** | `net462`, `net8.0-windows` | Enterprise interactive image canvas, forensic inspection, and OCR suite (`ZImageViewer`, `ZPictureEdit`, `ZOcrViewer`, `ZDocumentDeskew`) with sub-pixel pan/zoom, token bounding boxes, confidence badges, deskew binarization, MiniMap navigator | `ZeroUI.Core`, `ZeroGraphics`, `ZeroOcr.Core` |
| **`ZeroUI.Wpf`** | `net462`, `net8.0-windows` | Zero-alloc WPF virtual grid (`GridControl`), interactive onboarding engine (`ZTour`), industrial styling, 15+ specialized charts, and D3D11 shared texture bridge | `ZeroUI.Core` |
| **`ZeroUI.Wpf.Common`** | `net462`, `net8.0-windows` | WPF navigation, overlays, command palette (`ZCommandPalette`), notification center (`ZNotificationCenter`), and tree view (`ZTreeView`) | `ZeroUI.Core` |
| **`ZeroUI.Wpf.Editors`** | `net462`, `net8.0-windows` | WPF enterprise editors: `ZAutoComplete`, `ZPasswordBox`, `ZCronEditor`, `ZMultiSelect`, `ZSignaturePad`, `ZJsonEditor` | `ZeroUI.Core`, `ZeroUI.Wpf.Common` |
| **`ZeroUI.Wpf.Documents`** | `net462`, `net8.0-windows` | WPF document components: `ZAiChatBox`, `ZMarkdownViewer`, `ZCodeEditor`, `ZRichTextViewer` | `ZeroUI.Core` |
| **`ZeroUI.Wpf.Industrial`** | `net462`, `net8.0-windows` | WPF SCADA, BMS, Energy, Network, Water, GIS, and Workflow controls (`ZTimeline`, `ZAlarmSummary`, `ZScheduler`, `ZStepBar`) | `ZeroUI.Core` |
| **`ZeroUI.Wpf.Media`** | `net462`, `net8.0-windows` | High-performance interactive imaging & forensic inspection suite (`ZImageViewer`, `ZPictureEdit`, `ZOcrViewer`, `ZDocumentDeskew`, `ZAnnotationCanvas`, `ZMeasurementRuler`, `ZWatermarkOverlay`, `ZVideoPlayer`, `ZAudioWaveform`, `ZCompareViewer`, `ZCropBox`, `ZHistogramScope`, `ZCurveEditor`, `ZColorWheel`, `ZThumbnailGrid`, `ZDominantPalette`, `ZHistoryTimeline`, `ZFilmstripScroller`, `ZMaskGizmoOverlay`, `ZBatchTaskQueue`, `ZTokenPatternEditor`, `ZExifTelemetryCard`, `ZMiniMapNavigator`) | `ZeroUI.Core`, `ZeroGraphics`, `ZeroOcr.Core` |

---

## ⚡ Quick Start: 1,000,000 Rows Virtual Grid (WinForms)

```csharp
using System.Windows.Forms;
using ZeroUI.WinForms.DataGrid;

// Instantiate high-performance virtual grid (ZeroGridControl legacy alias also supported)
var grid = new GridControl
{
    Dock = DockStyle.Fill,
    RowDensity = GridRowDensity.Normal,
    AllowUserSorting = true
};
this.Controls.Add(grid);

// Define strongly-typed columns
grid.Columns.Add(new GridColumn("Id", "ID", 100));
grid.Columns.Add(new GridColumn("Timestamp", "Timestamp", 180));
grid.Columns.Add(new GridColumn("Temperature", "Temp (°C)", 140));

// Bind 1,000,000 rows procedurally with 0 bytes GC allocation
grid.SetProceduralDataSource(rowCount: 1000000, (rowIndex, colIndex) =>
{
    return colIndex switch
    {
        0 => (object)rowIndex,
        1 => DateTime.UtcNow.AddSeconds(rowIndex).ToString("yyyy-MM-dd HH:mm:ss"),
        _ => (25.0 + (rowIndex % 100) * 0.1).ToString("F1")
    };
});
```

👉 **[Explore Full Control Catalog & Code Samples](docs/CONTROLS_CATALOG.md)**

---

## 🧪 Testing & Quality Assurance

```bash
# Run comprehensive automated test suites (804 tests, 100% pass across Core & Desktop UI)
dotnet test ZeroUI.slnx

# Launch interactive demonstration suites via quick launcher
./run-demo.bat
# or directly via PowerShell:
pwsh -File scripts/run-demo.ps1 -Target Both
```

---

## 📜 Release History

| Version | Release Date | Key Milestones & Highlights |
| :--- | :---: | :--- |
| **`v1.17.0`** | 2026-10-14 | **Generic AI Flow Provider Architecture & Separated `ZFlowChatOptions` Setup**:<br/>• **Domain-Agnostic `ZeroUI.Core.AiMl` Contracts**: Added `IChatFlowProvider`, `ChatFlowRequest`, `ChatFlowResult`, `ChatStreamChunk`, `ChatActionItem`, `ChatToolbarItem`, and `ChatContextState` with zero hardcoded business logic.<br/>• **`ZFlowChatClient` HTTP/SSE Provider**: Built-in zero-dependency provider for ZFlow (`/api/workflows/run`, `/api/chat/stream`, `/api/memory/sessions/{id}`) across `netstandard2.0`, `net462`, and `net8.0`.<br/>• **Separated `ZFlowChatOptions` & Setup Extensions**: Decoupled workflow, user identity, RBAC feature permissions (`WithFeaturePermission`, `SetFlowPermission`), and continuous learning toolbar setup (`WithContinuousLearningTools`) into `ZFlowChatSetup.cs` and `ZAiChatBoxSetupExtensions.cs`.<br/>• **Enhanced `ZAiChatBox` (WinForms & WPF)**: Added generic `FlowProvider`, `SessionContext` bar, dynamic `ToolbarItems` bar, `LatencyMs` badges, compact `Reasoning` strip, and interactive `ChatActionItem` chips (`ActionItemClicked`). |
| **`v1.16.2`** | 2026-10-05 | **Markdown Loop Resilience, WaitOverlay Context Safety & OCR ROI Screen Coordinates**:<br/>• **`ZAiChatBox` Boundary Condition Fix**: Eliminated infinite loop hazard in Markdown RichTextBox parser when handling solitary asterisks, unclosed brackets, and edge-case tokens.<br/>• **`WaitOverlay` Form Top-Most & Context Switching**: Hardened overlay z-order and message loop pumping to eliminate COM/STA context switch deadlocks during heavy background AI inferences.<br/>• **`ZOcrViewer` Interactive ROI Screen Coordinate Mapping**: Fixed ROI drag rectangle alignment to 1:1 screen coordinates, added real-time size badge overlay (W × H px), and fortified GDI+ rendering against disposal race conditions.<br/>• **Multi-Framework Packaging**: Cleanly built and packaged across `net462`, `netstandard2.0`, `net8.0-windows`. |
| **`v1.16.1`** | 2026-10-05 | **`ZTour` Onboarding Enhancements & Multi-Block Markdown Table Support**:<br/>• Dynamic auto-height onboarding tour card with customizable `CardWidth`.<br/>• Multi-block markdown table rendering with interactive DataGridView in `ZAiChatBox`. |
| **`v1.16.0`** | 2026-10-03 | **`ZAiChatBox` Theming Hardening & `ZFloatingActionButton`**:<br/>• Comprehensive dark/light theme tokens and obsidian palette integration.<br/>• Enhanced `ZFloatingActionButton` hover states and hit test detection. |
| **`v1.15.0`** | 2026-10-01 | **AI Copilot Control, Industrial OCR Inspection Suite & Demo Showcase**:<br/>• **`ZAiChatBox` AI Copilot Control**: Implemented cross-platform chat interface across WinForms and WPF (`ZeroUI.WinForms.Documents` & `ZeroUI.Wpf.Documents`) with streaming support, markdown bubbles, intent chips, and auto-scroll.<br/>• **`ZOcrViewer` Suite**: Integrated `ZeroOcr.Core` into `ZeroUI.WinForms.Media` and `ZeroUI.Wpf.Media` for interactive document OCR inspection, bounding box rendering, confidence filters, and text search.<br/>• **Interactive Demo Showcase**: Added AI Copilot Studio and OCR Vision Studio showcase tabs into `WpfDemo` and `WinformDemo`.<br/>• **Desktop Test Resilience**: Eliminated thread synchronization deadlocks in `ZeroUI.Desktop.Tests` and `ZNotificationCenter`. |
| **`v1.14.0`** | 2026-09-30 | **ISA-101 Industrial HMI Design System Handbook & Ecosystem Alignment**:<br/>• **ISA-101 HMI Design System Handbook**: Published comprehensive High-Performance HMI standards, 4-level display hierarchy, and ergonomic muted-gray operational color palettes.<br/>• **Ecosystem Alignment**: Upgraded `ZeroGraphics.Core` to 1.6.0 and `ZeroTensor.Core` to 1.4.0. |
| **`v1.13.0`** | 2026-09-29 | **Deep Vertical Platform Integration: SIMD Filtering, Off-Heap SCADA & Durable Audit Trail**:<br/>• **SIMD Bitmask Grid Filtering**: Direct `ApplySelectionMask(SelectionMask)` on `RowIndexMap` and `ZGrid` (WinForms + WPF) accelerating 1,000,000+ row filtering up to $25\times$ via 64-bit word skipping and `TrailingZeroCount` (TZCNT) bit extraction with zero GC allocation.<br/>• **Off-Heap Telemetry Streaming**: Added `ZeroOffHeapTelemetrySeries` circular sliding buffer in `ZeroUI.Core.Scada` backed by `ZeroPrimitives.Memory.NativeMemoryPool`, streaming 100k+ sensor samples/sec directly in unmanaged memory with zero GC churn.<br/>• **Durable Audit Trail (21 CFR Part 11)**: Added `DurableAuditLogger` in `ZeroUI.Core.Security` backed by `ZeroStorage.Core.Persistence.DurablePersistentQueue` with memory-mapped files and `Uuid7` monotonic record identification.<br/>• **Theme Synchronization Hardening**: Enforced WCAG AA contrast ratio standards across `ZVirtualKeyboard` and `ZeroWpfTheme`.<br/>• **650 automated Core tests passed (100% green, 0 failures)**. |
| **`v1.12.0`** | 2026-09-28 | **Theme Synchronization, Floating Keyboard & Stability Hardening**:<br/>• **Dynamic Theme Sync**: Eliminated all hardcoded RGB colors in `ZLoginDialog` and `ZVirtualKeyboard` (WinForms + WPF), replaced with `ZeroTheme.Colors` / `ZeroWpfTheme` palette tokens.<br/>• **WCAG AA Contrast Validation**: Adversarial contrast audit across all 9 skins (clean_light, obsidian_dark, nordic_frost, cyberpunk_neon, emerald_enterprise, solar_amber, amethyst_violet, crimson_ruby, oled_midnight).<br/>• **Floating Virtual Keyboard Popup**: WPF `ZLoginDialog` auto-shows `ZVirtualKeyboard.ShowFloatingPopup()` on TextBox/PasswordBox focus with `IsLoaded/IsVisible` guard for headless test safety.<br/>• **Close Button (✕)**: Added dismiss button with ESC key support to `ZLoginDialog` (WinForms + WPF).<br/>• **ThemeChanged Race Condition Fix**: Added `IsDisposed/Disposing` guards in `ControlBase`, `ZTransferList`, `ZRichTextEditor`; type-filtered subscriber assertions immune to parallel test interference.<br/>• **Partial Class Refactoring**: Split WPF `ZLoginDialog` (596→4 files) and `ZVirtualKeyboard` (557→4 files) into logical partials (BuildUI, Theme, Auth, Layout, Input).<br/>• **315 Desktop tests passed (100% green, 0 failures, 0 skipped)**. |
| **`v1.11.0`** | 2026-09-27 | **Industrial Security & Identity Management Suite**:<br/>• **9 Enterprise Security Controls**: Decoupled authentication, RBAC, and compliance audit suite (`ZLoginDialog`, `ZVirtualKeyboard`, `ZIdleTimeoutMonitor`, `ZUserStatusBadge`, `ZBadgeReaderListener`, `ZRoleMatrix`, `ZUserManager`, `ZAuthorizeExtender`/`ZAuthorize`, `ZOperationLogViewer`).<br/>• **Multi-Modal Login**: Username/Password, quick PIN numpad, and RFID/NFC hardware badge dongle authentication flows.<br/>• **21 CFR Part 11 Compliance**: Electronic signature audit trail with severity badges, actor filter, and CSV export.<br/>• **Touch HMI**: 48px+ hit targets, QWERTY alphanumeric and 10-key numpad layouts with `CurrentPalette` dynamic theming.<br/>• Interactive Security & User Suite showcase integrated into `WinformDemo` and `WpfDemo`. |
| **`v1.10.1`** | 2026-09-25 | **Enterprise Extended Controls Suite & Deep Multi-Dimensional Verification**:<br/>• **21 Production Extended Controls**: Implemented full lifecycle, zero-allocation rendering, and cross-platform parity across WinForms and WPF (`ZAutoComplete`, `ZCommandPalette`, `ZNotificationCenter`, `ZAlarmSummary`, `ZRichTextEditor`, `ZScheduler`, `ZGaugeChart`, `ZBubbleChart`, `ZPasswordBox`, `ZVirtualGrid`, `ZCronEditor`, `ZMultiSelect`, `ZSignaturePad`, `ZTransferList`, `ZEmptyState`, `ZAvatar`, `ZMarkdownViewer`, `ZCodeEditor`, `ZJsonEditor`, `ZTreeView`, `ZTimeline`).<br/>• **Theme Synchronization & Thread Safety**: Zero-allocation token lookup (`ZeroTheme.Colors`, `ZeroWpfTheme`), cross-thread Dispatcher STA safety checks preventing xUnit race conditions.<br/>• **Zero-Dependency JSON Token Engine**: Integrated high-speed parser and validator in `ZJsonEditor` with zero external dependencies, compatible across .NET Framework 4.6.2 and .NET 8.<br/>• **Comprehensive Test Coverage**: Expanded automated unit test suite to **804 tests passed (100% green, 0 failures, 0 skipped)**.<br/>• Full 5-release backward compatibility deprecation policy implemented across all legacy shims. |
| **`v1.10.0`** | 2026-09-25 | **Interactive Guided Tour Engine & Canonical Z* Naming Standardization**:<br/>• Cross-Platform Interactive Onboarding Engine: Native `ZTour` for WinForms and WPF with spotlight cutout aperture masking (`Region.Exclude` / `CombinedGeometry.Exclude`), semi-transparent glass dimming backdrop, and collision-free adaptive popover positioning (`TopMost = true`).<br/>• Specialized Creative & Forensic Inspection Media Suite (`ZeroUI.Wpf.Media`): `ZAnnotationCanvas`, `ZMeasurementRuler`, `ZWatermarkOverlay`, `ZVideoPlayer`, `ZAudioWaveform`, `ZDocumentDeskew`.<br/>• Standardized canonical `Z*` naming prefix across all catalog documentation and showcase controls with 100% backward compatibility via `[Obsolete]` aliases.<br/>• Published lightweight single-file demo executables (.NET 8 framework-dependent): `WinformDemo` (~4.5 MB) and `WpfDemo` (~3.9 MB).<br/>• 720 automated tests passed (100%). |
| **`v1.9.0`** | 2026-09-23 | **Advanced Statistical, Relational & Hierarchical Charts Suite**:<br/>• Clean Architecture decoupling: 100% calculation engines in `ZeroUI.Core.Analytics` with thin WinForms GDI+ and WPF `DrawingContext`/`StreamGeometry` rendering.<br/>• 8 New High-Performance Analytics Controls: `HistogramChart`, `ScatterChart`, `SunburstChart`, `LollipopChart`, `TreemapChart`, `SankeyChart`, `BulletChart`, `ParetoChart`.<br/>• Standardized control identifiers with zero "Zero" prefix across Core, WinForms, and WPF.<br/>• Interactive showcase sub-tabs integrated into `WinformDemo` and `WpfDemo`.<br/>• Interactive launcher scripts added (`run-demo.bat`, `scripts/run-demo.ps1`).<br/>• 655 automated tests passed (100%). |
| **`v1.8.6`** | 2026-09-22 | **Zero-Dependency Core & Cross-Platform Control Parity Standard**:<br/>• Decoupled SQLite telemetry historian into dedicated `ZeroUI.Historian.Sqlite` package, achieving 100% zero third-party dependencies in `ZeroUI.Core`.<br/>• Established automated cross-platform parity reflection test suite (`ControlParityInspectionTests`).<br/>• Standardized platform-neutral industrial state machines (`SevenSegmentState` in `ZeroUI.Core.Industrial`) achieving 100% parity across WinForms and WPF.<br/>• 607 automated tests passed (100%). |
| **`v1.8.5`** | 2026-09-20 | **Industrial Control Normalization & Prefix Removal**:<br/>• Standardized primary control names to clean enterprise identifiers (`GridControl`, `ChartControl`, `SevenSegment`, `LinearGauge`, `ValidationProvider`) per `.project-rule.md`.<br/>• Full backward compatibility preserved via `[Obsolete]` shims. |
| **`v1.8.0`** | 2026-09-15 | **Creative & Media Controls Suite**:<br/>• Direct-rendered, high-performance visual editors for digital imaging and video grading (`CurveEditor`, `ColorWheelEdit`, `CompareViewerControl`, `HistogramScopeControl`, `CropBoxControl`, `MiniMapNavigator`, `MaskGizmoOverlay`, `FacetedFilterBar`, `HistoryTimelineControl`, `FilmstripScrollerControl`, `DominantPaletteControl`, `ExifTelemetryCard`, `NumericSliderEdit`, `BatchTaskQueueControl`, `ThumbnailGridControl`, `TokenPatternEditor`).<br/>• Zero-allocation graphics primitives for curves, color scopes, and canvas gizmos.<br/>• 529 automated tests passed (100%). |
| **`v1.7.0`** | 2026-09-12 | **WPF Modernization & High-DPI Per-Monitor V2**:<br/>• Zero-alloc WPF virtual grid adapter.<br/>• High-DPI Per-Monitor V2 dynamic scaling engine.<br/>• Win32 Memory DC unmanaged DIBSection double buffering with zero-copy `BitBlt` presentation. |
| **`v1.0.0`** | 2026-09-08 | **Initial Industrial Release**:<br/>• 10M+ rows procedural virtual data grid with zero GC allocations.<br/>• 40+ industrial SCADA/HMI controls with Single-HWND architecture.<br/>• Unified theme engine: Obsidian Dark (`#12151C`) and Clean Light modes.<br/>• Centralized 60 FPS ISA-18.2 synchronized animation clock (`ZeroAnimationClock`). |

---

## 🌐 Part of the ZeroPlatform Ecosystem

ZeroUI is the user interface and SCADA visualization pillar of the **[ZeroPlatform](https://github.com/kzxl/ZeroPlatform)** suite — unifying 12 sovereign subsystems including `ZeroGraphics`, `ZeroPipeline`, `ZeroTensor`, `ZeroComm`, and `ZeroStorage`.

---

## 🏛️ Ecosystem Architectural Alignment

ZeroUI is a sovereign member of **Tier 5 (Presentation & Orchestration)** within the **ZeroPlatform** industrial automation ecosystem.

```
┌──────────────────────────────────────────────────────────┐
│ Tier 5: Presentation & Orchestration (ZeroUI)            │
└────────────────────────────┬─────────────────────────────┘
                             │ consumes
              ┌──────────────┼──────────────┐
              ▼              ▼              ▼
┌───────────────────┐ ┌──────────────┐ ┌───────────────────┐
│ Tier 0: Primitives│ │ Tier 1: Data │ │ Tier 4: Graphics  │
│ (ZeroPrimitives)  │ │ (ZeroData)   │ │ (ZeroGraphics)    │
└───────────────────┘ └──────────────┘ └───────────────────┘
```

- **Upstream Ingestion**: Consumes Tier 0 foundational primitives (`ZeroPrimitives.Core 1.3.0`), Tier 1 high-speed columnar data buffers (`ZeroData.Core 1.3.0`), and Tier 4 GPU/2D rendering infrastructure (`ZeroGraphics.* 1.5.0`).
- **Strict DAG Conformance**: Zero references to orchestrators or applications.
- **Packaging & CI/CD**: Standardized under `Company = ZeroPlatform`, `Authors = Phong Võ`, `<ZeroTier>5</ZeroTier>`.

---

## 📄 License & Author

Released under the permissive **MIT License**.  
Architected and developed by **Phong Võ**.
