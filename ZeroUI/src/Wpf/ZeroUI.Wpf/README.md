# ZeroUI.Wpf ⚡

Ultra-high-performance WPF industrial UI suite with zero-allocation virtual big data grid, OLAP pivot matrices, timeline range selectors, form validation, Creative & Media Editors Suite, and reactive Obsidian Dark/Clean Light theme engine (`net462`, `net8.0-windows`).

> [!NOTE]
> **Canonical `Z*` Control Naming Standard & Collision Prevention:**
> All control classes adhere to the canonical `Z*` prefix (e.g. `ZGrid`, `ZSevenSegment`, `ZLinearGauge`, `ZRadialGauge`, `ZValidationProvider`, `ZTour`). Full backward compatibility is preserved via `[Obsolete]` shims.

[![ZeroPlatform Ecosystem](https://img.shields.io/badge/ZeroPlatform-Ecosystem-blueviolet.svg)](https://github.com/kzxl/ZeroPlatform)
[![NuGet Version](https://img.shields.io/badge/nuget-v1.10.0-blue.svg)](https://github.com/kzxl/ZeroUI)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](https://github.com/kzxl/ZeroUI)
[![GitHub](https://img.shields.io/badge/GitHub-kzxl%2FZeroUI-blue.svg)](https://github.com/kzxl/ZeroUI)

---

## 🌟 Key Features

### ⚡ Direct3D 11 GPU Bridge & Automatic Render Optimizer
* **Native D3D11 Shared Texture Bridge (`ZeroD3D11Bridge`, `ZeroD3DCanvas`):** Direct DXGI zero-copy texture sharing with WPF `D3DImage`, independent GPU rendering loop (60/120/144 FPS), and interactive cursor wave modulation.
* **Universal Attached Properties (`opt:RenderOptimizer.*`):** Attach live GPU SDF box shadows, blurs, and neon bloom effects to ANY standard WPF control (`Button`, `TextBox`, `Border`) via `opt:RenderOptimizer.Elevation`, `BlurRadius`, `GlowIntensity`, `GlowColor`, `RoutingMode`.
* **Smart Card Container (`OptimizedCard` / `ZeroOptimizedCard`):** Automated cost-based rendering routing (CPU ClearType foreground + GPU SDF shadow backdrop) with dynamic adaptive throttling (recovers 60 FPS under heavy load).
* **Real-Time Diagnostic HUD (`ZeroRenderOptimizerHUD`):** Glassmorphic heads-up display overlay showing live FPS, 16.6ms budget compliance, GPU hardware tier, dedicated VRAM, and 9-slice atlas hit rates.

### 🎨 Creative & Media Controls Suite (`ZeroUI.Wpf.Editors`)
* **Color & Tone Grading:** `CurveEditor` (monotone cubic spline per-channel RGB curve editor), `ColorWheelEdit` (360° Hue/Saturation color grading wheel), `HistogramScopeControl` (real-time RGB parade and 5-zone parametric tonal slider).
* **Inspection & Canvas Tools:** `CompareViewerControl` (Split-Curtain, Side-by-Side, and Fade-Blend inspection viewer), `CropBoxControl` (8-point bounding box with Rule of Thirds/Golden Ratio), `MaskGizmoOverlay` (linear/radial gradient masks with rotation handles), `MiniMapNavigator` (pan/zoom 2D overview canvas).
* **Asset Browser & Metadata:** `ThumbnailGridControl` (virtual light table browser with rating stars, color label flags, and pick badges), `FilmstripScrollerControl` (horizontal filmstrip carousel), `DominantPaletteControl` (harmonic color swatches and contrast analysis), `ExifTelemetryCard` (camera exposure HUD).
* **Productivity & Orchestration:** `HistoryTimelineControl` (visual undo/redo snapshot tree), `NumericSliderEdit` (drag-scrubbing slider with double-click reset), `BatchTaskQueueControl` (multi-worker background task orchestration HUD), `TokenPatternEditor` (interactive chip filename templating), `FacetedFilterBar` (multi-category pill filter chips).

### 🚀 Big Data Virtual Grid & OLAP Matrix
* **WPF Virtual Data Grid (`GridControl` / `ZeroGridControl`):**
  * Hardware-accelerated row virtualization handling millions of items with smooth 60 FPS scrolling.
  * In-place column sorting, live filtering, column pinning, cell formatting, and selection models (Cell, Row, MultiRow, Block).
  * Standardized pagination toolbar (`GridPagination`) and debounced search bar (`GridSearchBar`).
* **`PivotGridControl` / `ZeroPivotGrid`:** Cross-tab OLAP aggregation matrix with collapsible tree headers (`▶`/`▼`), multi-dimensional rollups (`Sum`, `Count`, `Average`, `Min`, `Max`), sub-totals, and grand totals.
* **`FilterControl` / `ZeroFilterControl`:** Visual Query Builder rendering hierarchical boolean condition trees with SQL WHERE clause generation.

### ⏱️ Visual Timeline & Range Selector
* **`RangeControl` / `DateTimeRangeSlider`:** Dual-thumb interactive range selector with sparkline track, continuous/discrete selection (numeric and DateTime), and draggable selection window.

### 🛡️ Form Validation & Editors Suite
* **`ValidationProvider` & `ZeroErrorProvider`:** Declarative XAML and code-behind form validation engine with animated pulsing error badges, warning glyphs, and automated binding.
* **`GridLookupEdit` / `ZeroGridLookup`:** Multi-column dropdown editor with embedded virtual DataGrid and cross-column search.
* **`CheckedComboBoxEdit` / `ZeroCheckedComboBox`:** Multi-select dropdown with checkboxes, "(Select All)", and search filter.
* **`TokenEdit` / `ZeroTokenEdit`:** Tag & badge input with dismissible chips and keyboard navigation.
* **`ColorPickEdit` / `ZeroColorPicker` & `DateRangePicker`:** Enterprise swatch color picker and dual-date range presets.

### 🎨 Specialized Creative & Inspection Media Editors (`ZeroUI.Wpf.Media`)
* **`ZAnnotationCanvas`**: Vector-based interactive markup canvas for defect tagging and forensic inspection (Bounding Box, Ellipse, Arrow, Line, Callout, Blur/Pixelate) with severity ratings and bitmap burning.
* **`ZMeasurementRuler`**: Precision optical caliper and gauge for machine vision metrology (linear calipers, 3-point angle gauge $\theta^\circ$, sub-pixel calibration).
* **`ZWatermarkOverlay`**: Dynamic security layer with runtime token substitution (`{User}`, `{Date}`, `{Time}`, `{Machine}`), 9-point anchor, and diagonal matrix tiling.
* **`ZVideoPlayer`**: GPU-accelerated video playback workstation with SMPTE timecode (`hh:mm:ss:ff`), frame-accurate stepping, and speed ratios (0.1x–8.0x).
* **`ZAudioWaveform`**: Symmetrical audio amplitude waveform visualizer with synchronized playhead scrubbing and time-range selection.
* **`ZDocumentDeskew`**: Document straightening canvas with perspective keystone correction, alignment grid overlay, and Otsu binarization for OCR.

### 📐 Navigation, Layout & Onboarding
* **`ZTour` (`ZeroUI.Wpf.Common.ZTour`):** Interactive guided onboarding tour engine with spotlight cutout aperture (`CombinedGeometry.Exclude`), glowing animated target borders, dynamic popover card with collision-free placement, step counter, dot indicators, and keyboard navigation (`Escape`, `Enter`, arrows).
* **`ProcessMap`:** Interactive business process flowchart & workflow navigation map with customizable UserControl action triggers, orthogonal routing, and JSON serialization.
* **`WizardControl` / `ZeroWizard`:** Multi-step process workflow wizard with validation and step progress indicator.
* **`SideNavControl` & `AccordionControl`:** Collapsible vertical sidebar navigation with category groups and badges.
* **`DockManager` / `ZeroDockManager`:** Multi-region docking layout system with detachable floating windows and auto-hide tabs.
* **`SpreadsheetControl` & Reporting:** Vector spreadsheet with live formula evaluation (`=SUM`, `AVERAGE`, `MIN`, `MAX`, `IF`), interactive formula bar, in-place cell editing, and PDF/Document previewers (`PdfViewerControl`, `DocumentPreviewControl`).

### 🏭 Industrial Instrumentation & Theme Engine
* **SCADA Gauges:** `SevenSegment` (100% parity with WinForms, 7-segment digital display with custom segment styling and color profiles), `LinearGauge`, `RadialGauge`, `HeatmapControl`, `LedTowerControl`, `SignalScopeControl`.
* **Unified Theme Engine:** Obsidian Dark and Clean Light styling with dynamic resource dictionary swapping.

---

## 📦 Installation

```powershell
dotnet add package ZeroUI.Wpf --version 1.10.0
```

---

## 🚀 Quick Example (XAML)

```xml
<Window x:Class="MyApp.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:zeroui="http://schemas.zeroui.net/winfx/xaml"
        Title="ZeroUI WPF Showcase" Height="700" Width="1000">
    <Grid>
        <!-- Enterprise Virtual Data Grid -->
        <zeroui:GridControl x:Name="virtualGrid" />
    </Grid>
</Window>
```

Full documentation and source code: [github.com/kzxl/ZeroUI](https://github.com/kzxl/ZeroUI)
