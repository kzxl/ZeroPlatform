# ZeroUI: Comprehensive Industrial & Enterprise Control Catalog

A complete reference catalog of enterprise and industrial controls provided by ZeroUI across Windows Forms and WPF (.NET Framework 4.6.2, .NET 8.0/9.0).

> [!NOTE]
> **Canonical `Z*` Control Naming Standard:** All UI controls strictly adopt the canonical **`Z`** prefix (e.g., `ZGrid`, `ZChart`, `ZSevenSegment`, `ZLinearGauge`, `ZRadialGauge`, `ZButton`, `ZTextBox`, `ZRatingControl`, `ZTour`, `ZDockManager`). This eliminates naming collisions (`CS0104`) with native WinForms and WPF BCL controls while offering instant IDE discovery. Full backward compatibility is preserved across all namespaces via `[Obsolete]` class aliases and shims (`GridControl`, `ZeroGridControl`, etc.).

---

## 1. Data Matrix & Virtualization Subsystem (`ZeroUI.WinForms.DataGrid` / `ZeroUI.Wpf.DataGrid`)

* **`ZGrid`** *(Aliases: `GridControl`, `ZeroGridControl`, `ZeroGrid`)*:
  * High-performance virtual data grid powered by Win32 Memory DC DIBSection double-buffering (WinForms) or Direct3D 11 shared texture bridge (WPF).
  * Smoothly renders **10,000,000+ virtual rows** at 60–144 FPS with **0 bytes GC heap allocation** on hot scrolling loops.
  * Features multi-column sorting (`RowIndexMap`), frozen columns (left/right pinned), density switching (`Compact = 24px`, `Normal = 28px`, `Comfortable = 36px`), and hierarchical row grouping (`GroupedRowIndexMap`).
  * Built-in CardView and TileView presentation modes using `GridCardLayoutManager`.
* **`ZPivotGrid`** *(Aliases: `PivotGridControl`, `ZeroPivotGrid`)*:
  * High-performance multidimensional OLAP aggregation matrix.
  * Dynamically pivots tabular datasets across Row and Column dimensions, calculating summary aggregations (`Sum`, `Count`, `Average`, `Min`, `Max`), interactive collapsible drill-down tree nodes (`▶`/`▼`), automated sub-totals, and grand totals with zero-alloc matrix virtualization.
* **`ZTreeList`** *(Aliases: `TreeList`, `ZeroTreeList`)*:
  * Hierarchical virtual multi-level BOM tree with expandable nodes, tri-state cascading checkboxes (`Unchecked`, `Checked`, `Indeterminate`), and guidelines.
* **`ZFilterControl` & `FilterCriteria`** *(Aliases: `FilterControl`, `ZeroFilterControl`)*:
  * Enterprise Visual Query Builder UI rendering condition trees with boolean operator badges (`AND`, `OR`, `NOT AND`, `NOT OR`), comparison selectors (`Equals`, `GreaterThan`, `Contains`, `Between`, `IsNull`), and automated SQL `WHERE` clause generation.
* **`ZGridSearchBar`** *(Aliases: `GridSearchBar`, `ZeroGridSearchBar`)*:
  * Integrated search header with 150ms debounced input, live match counter, density switcher, and CSV export trigger.
* **`ZGridPagination`** *(Aliases: `GridPagination`, `ZeroGridPagination`)*:
  * Enterprise pagination toolbar with page size selector (`50`, `100`, `500`, `1000`, `All`), row statistics, and navigation buttons.
* **`ZGridExporter`** *(Aliases: `GridDataExporter`, `ZeroGridExporter`)*:
  * High-throughput streaming CSV and OpenXML (`.xlsx`) exporter streaming >1,100,000 rows/sec directly to disk without loading datasets into memory.

---

## 2. Analytics & Business Charts Subsystem (`ZeroUI.WinForms.Charts` / `ZeroUI.Wpf.Charts`)

All specialized analytics charts are built upon a decoupled Clean Architecture: **100% mathematical logic and layout calculation** reside in platform-agnostic `ZeroUI.Core.Analytics` engines, while thin presentation view layers render via GDI+ (`ZeroUI.WinForms.Charts`) and `DrawingContext`/`StreamGeometry` (`ZeroUI.Wpf.Charts`).

* **`ZChart`** *(Aliases: `ChartControl`, `ZeroChart`)*:
  * Universal multi-series chart engine supporting Cartesian (Column, Bar, Line, Spline, Area) and Polar (Pie, Donut) visualizations with subpixel antialiasing, automatic human-friendly $Y$-axis rounding, interactive cursor crosshairs, halo tooltips, and clickable legends.
* **`ZBarChart`** *(Aliases: `BarChart`, `ZeroBarChart`)*:
  * Specialized Column and Bar comparison chart with grouped and stacked modes (`IsHorizontal = true/false`, `IsStacked = true/false`), rounded column caps, and custom value formatting.
* **`ZLineChart`** *(Aliases: `LineChart`, `ZeroLineChart`)*:
  * Specialized Line and Area trend chart featuring smooth Catmull-Rom spline curves (`IsCurved = true`), vertical translucent area gradient fills with bottom fade, point markers, and interactive hover tooltips.
* **`ZPieChart`** *(Aliases: `PieChart`, `ZeroPieChart`)*:
  * Categorical distribution chart supporting full Pie and Donut rings (`IsDonut = true`, `DonutHoleRatio = 0.58f`), center KPI summary metrics, radial hover slice explosion, and percentage calculations.
* **`ZHistogramChart`** *(New in v1.9.0)*:
  * Continuous statistical distribution histogram powered by `HistogramEngine`. Supports automated binning via Sturges' rule ($k = \lceil \log_2(n) + 1 \rceil$), statistical distribution moments (Mean $\mu$, StdDev $\sigma$), and parametric Gaussian normal distribution PDF bell curve overlay with hover tooltips.
* **`ZScatterChart`** *(New in v1.9.0)*:
  * Cartesian bivariate correlation and 3D Bubble plot powered by `ScatterPlotEngine`. Computes Ordinary Least Squares (OLS) linear regression trendlines ($y = mx + b$), correlation coefficient $R^2$, and magnitude bubble area scaling with alpha transparency blending.
* **`ZSunburstChart`** *(New in v1.9.0)*:
  * Multi-tiered hierarchical radial partition diagram powered by `SunburstLayoutEngine`. Partitions $360^\circ$ circular angular sectors across recursive tree depths with polar coordinate hit-testing and central overview breadcrumbs.
* **`ZLollipopChart`** *(New in v1.9.0)*:
  * High-density comparison chart powered by `LollipopEngine`. Features thin stem lines, circular dot marker heads, horizontal and vertical orientation modes, and baseline vector alignments.
* **`ZTreemapChart`** *(New in v1.9.0)*:
  * Squarified Treemap partition chart powered by `TreemapEngine` (Bruls-Huizing-van Wijk algorithm). Renders multi-category asset allocation and nested hierarchical proportional weights.
* **`ZSankeyChart`** *(New in v1.9.0)*:
  * Energy and process flow vector chart powered by `SankeyLayoutEngine`. Resolves multi-column flow stages, node heights, and smooth cubic Bézier flow ribbons with dynamic hover highlighting.
* **`ZBulletChart`** *(New in v1.9.0)*:
  * Stephen Few linear bullet graph powered by `BulletBenchmarkEngine`. Benchmarks dense executive KPIs against comparative targets and qualitative threshold ranges (`Poor`, `Satisfactory`, `Good`).
* **`ZParetoChart`** *(New in v1.9.0)*:
  * Quality root cause distribution chart powered by `ParetoEngine`. Computes Juran's 80/20 rule, sorting defect frequencies, plotting cumulative percentage curves, and delineating the "Vital Few" from the "Useful Many".
* **`ZCandlestickChart`** *(Aliases: `CandlestickChart`, `ZeroCandlestickChart`)*:
  * High-performance OHLC candlestick chart with volume histogram, moving average (MA) curve, and interactive crosshair HUD.
* **`ZBoxPlotChart`** *(Aliases: `BoxPlotChart`, `ZeroBoxPlotChart`)*:
  * Statistical Box-and-Whisker chart for industrial Six Sigma / SPC tolerance inspection with five-number statistical summaries (Min, Q1, Median, Q3, Max) and USL/LSL specification limits.
* **`ZRadarChart`** *(Aliases: `RadarChart`, `ZeroRadarChart`)*:
  * Multi-dimensional radar and spider chart with customizable concentric web rings, radial spokes, polygonal series fills, and vertex markers.
* **`ZFunnelChart` & `ZPyramidChart`** *(Aliases: `FunnelChart`, `PyramidChart`, `ZeroFunnelChart`, `ZeroPyramidChart`)*:
  * Conversion funnel and yield rate visualizer with dual presentation modes, interactive slice selection, drop-off metrics, and automated inward yield rate computation.
* **`ZWaterfallChart`** *(Aliases: `WaterfallChart`, `ZeroWaterfallChart`)*:
  * Financial and variance waterfall chart visualizing cumulative effects of sequential positive and negative values.

---

## 3. Industrial Instrumentation & SCADA Suite (`ZeroUI.WinForms.Industrial` / `ZeroUI.Wpf.Industrial`)

* **`ZSevenSegment`** *(Aliases: `SevenSegment`, `ZeroSevenSegment`)*:
  * Industrial 7-segment digital LED readout display with 100% feature parity across WinForms and WPF.
  * Backed by platform-neutral `SevenSegmentState` in `ZeroUI.Core.Industrial` with hardware bitmask decoding (`0x7F` mapping).
  * Supports custom digit counts, decimal points, colons, negative signs, italic slant angles, unlit ghost segments, and standardized color presets (`Green`, `Amber`, `Red`, `Cyan`, `Blue`, `White`).
* **`ZLinearGauge`** *(Aliases: `LinearGauge`, `ZeroLinearGauge`)*:
  * High-precision industrial vertical and horizontal thermometer gauge with multi-color threshold bands (`GaugeThresholdRange`: Normal, Warning, Critical) and magnetic needle damping.
* **`ZRadialGauge`** *(Aliases: `RadialGauge`, `ZeroRadialGauge`, `ZeroGauge`)*:
  * Industrial Bourdon tube circular dial gauge (180° / 270° sweep) with subpixel anti-aliased needle, warning arcs, and direct telemetry binding via `IScadaBindable`.
* **`ZTrendChart`** *(Aliases: `TrendChart`, `ZeroTrendChart`)*:
  * 60 FPS real-time multi-channel oscilloscope streaming high-frequency telemetry via ring buffers (`ZeroTripleBuffer`), limit lines (USL/LSL), and LTTB peak downsampling.
* **`ZAlarmGrid`** *(Aliases: `AlarmGrid`, `ZeroAlarmGrid`)*:
  * ISA-18.2 compliant real-time alarm monitoring grid with state-synchronized blinking driven by `ZeroAnimationClock`, severity badges (`Critical`, `High`, `Medium`, `Low`), and operator acknowledgment.
* **`ZPlantMimicCanvas` & P&ID Vector Primitives**:
  * Single-HWND vector synoptic canvas supporting infinite pan/zoom and hardware nodes:
    * `IndustrialPump`: Variable RPM speed, fluid flow arrows.
    * `IndustrialValve`: Proportional control, angle indication (0–100%).
    * `Tank3D`: Cylindrical fluid vessel with real-time level gradient physics and low/high alarm setpoints.
    * `IndustrialMotor`: Dynamic rotor rotation, overload trip indicators.
    * `IndustrialHeater`: Heat shimmer and temperature gradient.
    * `IndustrialFan`: Ventilation impeller blades driven by `ZeroAnimationClock`.
    * `PneumaticCylinder`: Bidirectional stroke extension (0–100%) and magnetic limit switches.
    * `ConveyorBelt`: Roller belt speed, item indexing, moving product flow.
    * `PipeFlow`: Subpixel animated liquid/gas pulse lines connecting equipment.
* **`ZLedTowerControl`** *(Aliases: `LedTowerControl`, `ZeroLedTowerControl`)*:
  * 4-tier Andon signal tower (`Red`, `Amber`, `Green`, `Blue`) with `On`, `Off`, `BlinkFast`, `BlinkSlow` states.

---

## 4. Modern Enterprise Form Editors & Controls (`ZeroUI.WinForms.Editors` / `ZeroUI.Wpf.Editors`)

All editors implement `IZeroEditor` and integrate with `ZeroDataBinder` for automated two-way binding:
* **`ZButton`**: High-performance vector flat button with state styling (`Primary`, `Secondary`, `Success`, `Danger`, `Ghost`), badge counts, and icon slots.
* **`ZTextBox` / `ZTextEdit` & `ZMemoEdit`**: Single-line and multi-line themed text editors with focus glow, clear button (✕), password reveal, character counter, and regex masking.
* **`ZButtonEdit`**: Action-embedded editor with customizable button slots (Browse, Copy, Clear, Search, Custom).
* **`ZCalcEdit`**: In-place numeric input with dropdown financial mini-calculator popup and currency formatting.
* **`ZSpinEdit` & `ZNumericBox`**: High-precision numeric stepper and spin box with mouse hold acceleration and decimal formatting.
* **`ZCheckBox` / `ZCheckEdit` & `ZSwitch` / `ZToggleSwitch`**: Sleek modern checkboxes (supporting Tri-State) and iOS-style toggle switches with custom glyphs.
* **`ZRatingControl` / `ZRating`**: Inspection severity and QA rating half-star selector supporting stars, shields, hearts, and diamonds.
* **`ZRangeControl` & `ZRangeSlider`**: Interactive dual-thumb range selector with background sparkline / distribution histogram track.
* **`ZNumericSliderEdit` & `ZSlider`**: High-precision slider + label + direct numeric input composite control with reset to default.
* **`ZGridLookupEdit`** *(Aliases: `GridLookupEdit`, `ZeroGridLookup`)*: Multi-column dropdown editor hosting an embedded virtual DataGrid with instant live search across 100k+ records.
* **`ZSearchLookUpEdit`**: Paginated high-capacity dropdown with persistent top search bar for 50,000+ item master catalogs.
* **`ZCheckedComboBoxEdit`** *(Aliases: `CheckedComboBoxEdit`, `ZeroCheckedComboBox`)*: Multi-select dropdown with checkbox items and search.
* **`ZTokenEdit`** *(Aliases: `TokenEdit`, `ZeroTokenEdit`)*: Tag & badge input with dismissible chips and keyboard navigation.
* **`ZColorPicker` / `ZColorPickEdit`** *(Aliases: `ColorPickEdit`, `ZeroColorPicker`)*: Swatch matrix and HEX color editor.
* **`ZDateEdit` & `ZDateRangePicker`**: Multi-tier zoom navigation calendar (Days &rarr; Months &rarr; Years) and dual-date range presets.
* **`ZTimeSpanEdit` & `ZTimePicker`**: Precision duration and shift time picker (Hours : Minutes : Seconds).
* **`ZIPAddressEdit` & `ZMaskedTextBox`**: Byte-validated IPv4 / IPv6 network address editor and customizable masked text inputs.
* **`ZHyperlinkEdit`**: Interactive clickable URL and command link editor with browser launch.
* **`ZBarcodeBox`**: Pure vector 1D barcode (Code 128) and 2D QR Code generator with zero pixelation at any DPI.
* **`ZFacetedFilterBar`**: Multi-dimension drill-down search and faceted filter bar featuring categorized pill chips, match count badges, active indicator, and batch reset.
* **`ZProgressBar`**: Smooth subpixel antialiased determinate and 60 FPS indeterminate marquee progress indicator.
* **`ZSearchBox`**: Instant search box with placeholder text, debounced filter events, and clear button.
* **`ZTag` & `ZBadge`**: Semantic status tags (`Active`, `Processing`, `Warning`, `Error`) and numeric notification count badges.
* **`ZSegmented`**: Pill-style segmented view and timeframe switcher (`Daily`, `Weekly`, `Monthly`).
* **`ZStatistic`**: Executive KPI dashboard card with trend direction indicators, prefixes, and suffixes.
* **`ZListView`**: High-throughput virtual log viewer smoothly handling 100k+ entries with severity color coding.
* **`ZCard` & `ZPanel`**: Elevated surface card container with customizable corner radius, border, and drop shadow.
* **`ZToast` & `ZFlyout`**: Non-blocking toast notification stack with Windows Action Center bridge integration and lightweight popup flyouts.
* **`ZErrorProvider` & `ValidationProvider`**: Declarative fluent validation engine with pulsing vector error badges, warning icons, and automatic scroll-to-error.

---

## 5. Media & Creative Image Controls Suite (`ZeroUI.WinForms.Media` / `ZeroUI.Wpf.Media`)

All creative photo studio, inspection, color grading, and media controls reside cleanly in dedicated Media subsystems (`ZeroUI.WinForms.Media` and `ZeroUI.Wpf.Media`). Complete backward compatibility across 5 release cycles is guaranteed via forwarding shims in `ZeroUI.WinForms.Editors` and `ZeroUI.Wpf.Editors`.

### Core Interactive Image Viewers & Picture Editors
* **`ZImageViewer`** *(WinForms & WPF - Aliases: `ImageViewerControl`, `ZeroImageViewer`)*:
  * High-performance interactive image viewing canvas powered by Direct2D, GDI+, and WPF vector rendering.
  * Smooth sub-pixel infinite pan and cursor-anchored continuous zoom ($0.01\times$ to $64\times$).
  * **Forensic Pixel Grid**: Automatically renders aligned pixel grid overlay when zoom level reaches $\ge 800\%$ for fine sub-pixel inspection and alignment.
  * **Interactive MiniMap Navigator**: Translucent viewport box in corner overview thumbnail with drag navigation.
  * **Telemetry Color Probe HUD**: Real-time hover cursor coordinates $(X, Y)$ and exact RGBA / HEX color readout badge.
  * **Floating Loupe Magnifier (WPF)**: Dedicated magnification lens tracking cursor for instant close-up inspection.
  * **Transform Pipeline**: Real-time lossless rotation ($0^\circ, 90^\circ, 180^\circ, 270^\circ$), horizontal/vertical flip, and interpolation quality switching (`NearestNeighbor`, `Bilinear`, `HighQuality`).
  * **IZeroEditor & IZeroSkinnable**: Two-way data binding support, file-loading, and Obsidian Dark / Clean Light theme synchronization.
* **`ZPictureEdit`** *(WinForms & WPF - Aliases: `PictureEdit`, `ZeroImage`, `ZeroPictureEdit`)*:
  * Anti-aliased image and avatar control with rounded corners, circular clip, initials fallback with deterministic background palettes, presence status indicator dots, drag-and-drop file loading, clipboard sync, and click-to-zoom Lightbox inspection modal.

### Specialized Creative & Inspection Media Editors (`ZeroUI.Wpf.Media`)
* **`ZAnnotationCanvas`** *(New in v1.10.0)*: Interactive canvas for defect tagging and forensic markup. Supports vector drawing of Bounding Boxes, Ellipses, Arrows, Lines, Text Callouts, and Sensitive Info Blur/Pixelation with severity levels (`Ok`, `Warning`, `Defect`, `Critical`), resize handles, and direct bitmap burning (`BurnAnnotationsToBitmap`).
* **`ZMeasurementRuler`** *(New in v1.10.0)*: Precision optical caliper for metrology and machine vision inspection. Supports pixel-to-millimeter/micrometer calibration (`CalibrationFactor`), 2-point linear distance calipers, 3-point angle gauges ($\theta^\circ$), sub-pixel ticks, and telemetry badge overlays.
* **`ZWatermarkOverlay`** *(New in v1.10.0)*: Dynamic security stamp and watermark layer. Supports token resolution (`{User}`, `{Date}`, `{Time}`, `{Machine}`), 9-point anchor placement, full diagonal matrix tiling (`DiagonalTiled`), opacity/rotation, and direct bitmap burning (`BurnWatermarkToBitmap`).
* **`ZVideoPlayer`** *(New in v1.10.0)*: GPU-accelerated video playback workstation with SMPTE timecode readout (`hh:mm:ss:ff`), frame-accurate stepping (`StepForward`, `StepBackward`), speed ratios (0.1x–8.0x), scrub bar, volume, and MVVM playback state management.
* **`ZAudioWaveform`** *(New in v1.10.0)*: Symmetrical audio amplitude waveform visualizer and scrub bar. Supports live playhead tracking, interactive time-range selection (`SelectionStart` / `SelectionEnd`), and waveform data rendering.
* **`ZOcrViewer`** *(WinForms & WPF - Aliases: `OcrRoiBoxControl`, `ZeroOcrViewer`)*:
  * Enterprise Optical Character Recognition (OCR) visualization and interactive document inspection canvas.
  * Powered by `ZeroOcr.Core` model integration (`OcrResult`, `OcrBlock`, `OcrLine`, `OcrWord`, `OcrRect`).
  * Features bounding box overlays, confidence score color coding (Green $\ge 85\%$, Amber $70-84\%$, Crimson $< 70\%$), confidence badges, and selectable text overlays.
  * Interactive word & line selection, hit-testing, copy-to-clipboard, zoom-to-fit, center-anchored pan/zoom, and dynamic drag-and-drop Region of Interest (ROI) box filtering (`GetWordsInRoi`).
* **`ZDocumentDeskew`** *(WinForms & WPF - Aliases: `DocumentDeskew`, `ZeroDocumentDeskew`)*: Document straightening, perspective keystone deskew, alignment grid guides, and high-performance pixel binarization (fixed / Otsu thresholding) for OCR and scanned document cleanup.
* **`ZCompareViewer`** *(Aliases: `CompareViewerControl`, `ZeroCompareViewer`)*: Dual-image comparison inspection viewer featuring Split-Curtain (with draggable divider line), Side-by-Side (horizontal split), and Fade-Blend modes with synchronized panning and zoom.
* **`ZCropBox`** *(Aliases: `CropBoxControl`, `ZeroCropBox`)*: Non-destructive image cropping canvas with 8-point bounding box handles, aspect ratio locking, and dynamic rule overlays (Rule of Thirds, Golden Ratio, Diagonals, Grid, None).
* **`ZHistogramScope`** *(Aliases: `HistogramScopeControl`, `ZeroHistogramScope`)*: Real-time histogram and waveform scope supporting RGB Parade, Luminance curve, 5-zone parametric tonal range dragging (Blacks, Shadows, Midtones, Highlights, Whites), and clipping warnings.
* **`ZCurveEditor` & `CurveMath`** *(Aliases: `CurveEditor`, `ZeroCurveEditor`)*: Monotone cubic spline curve editor supporting RGB and per-channel curves (Red, Green, Blue), 4x4 coordinate grid, interactive control point manipulation, delete, and evaluate.
* **`ZColorWheel`** *(Aliases: `ColorWheelEdit`, `ColorWheel`, `ZeroColorWheel`)*: 360° Hue/Saturation color grading wheel with pre-calculated antialiased gamut bitmap, smooth dragging crosshairs, and numeric degree/distance updates.
* **`ZMiniMapNavigator`** *(Aliases: `MiniMapNavigator`, `ZeroMiniMapNavigator`)*: Floating / docking thumbnail canvas overview with an interactive translucent viewport box for fluid 2D pan/zoom navigation.
* **`ZMaskGizmoOverlay`** *(Aliases: `MaskGizmoOverlay`, `ZeroMaskGizmoOverlay`)*: Non-destructive linear / radial gradient and brush mask gizmo with interactive origin pin, cardinal handles, feather boundary ellipses, and rotation arcs.
* **`ZHistoryTimeline`** *(Aliases: `HistoryTimelineControl`, `ZeroHistoryTimeline`)*: Visual undo/redo history and snapshot tree timeline with step indexing, relative timestamps, state jumping, and snapshot curation.
* **`ZFilmstripScroller`** *(Aliases: `FilmstripScrollerControl`, `ZeroFilmstripScroller`)*: Horizontal smooth-scrolling thumbnail filmstrip with rating stars, color label flags, active badges, and multi-selection support.
* **`ZThumbnailGrid`** *(Aliases: `ThumbnailGridControl`, `ZeroThumbnailGrid`)*: Lightroom-style light table photo browser grid with responsive wrap-panel card layout, Ctrl/Shift multi-selection, rating stars, color label badges, pick flags, and overlay badges.
* **`ZDominantPalette`** *(Aliases: `DominantPaletteControl`, `ZeroDominantPalette`)*: Dominant color palette inspector with extracted swatches, percentage labels, click-to-copy hex codes, color theory harmonies, and contrast evaluation.
* **`ZExifTelemetryCard`** *(Aliases: `ExifTelemetryCard`, `ZeroExifTelemetryCard`)*: Camera & exposure HUD metrics deck (Shutter, Aperture, ISO, Focal Length), device banner, GPS location link, and detailed collapsible EXIF table.
* **`ZBatchTaskQueue`** *(Aliases: `BatchTaskQueueControl`)*: Production-grade background batch task queue controller with concurrency selection (1..8 workers), pause/resume orchestration, retry/remove item actions, and live visual progress bars.
* **`ZTokenPatternEditor`** *(Aliases: `TokenPatternEditor`, `ZeroTokenPatternEditor`)*: Filename and dynamic text template pattern editor with interactive token insertion chips, caret placement, and live evaluated preview.

---

## 6. Layout, Docking, Reporting & Workflows

* **`ZDockManager`** *(Aliases: `DockManager`, `ZeroDockManager`)*: Multi-zone docking system (Left/Right/Top/Bottom/Document) with splitters, auto-hide tabs, and `FloatingWindow` for detached multi-monitor workspaces.
* **`ZWorkspaceSerializer`** *(Aliases: `WorkspaceSerializer`, `ZeroWorkspaceSerializer`)*: Pure zero-dependency JSON layout persistence engine capturing and restoring DockPanel and DataGrid column configurations.
* **`ZWizardControl`** *(Aliases: `WizardControl`, `ZeroWizard`)*: Multi-step process workflow wizard with validation and step progress indicator.
* **`ZProcessMap`** *(Aliases: `ProcessMap`)*: Interactive business process flowchart & workflow navigation map with customizable UserControl action triggers, orthogonal routing, and JSON serialization.
* **`ZSpreadsheetControl`** *(Aliases: `SpreadsheetControl`)*: High-performance vector spreadsheet engine with formula evaluation (`SUM`, `AVERAGE`, `MIN`, `MAX`, `IF`), interactive formula bar, and in-place editing.
* **`ZPdfViewerControl`** *(Aliases: `PdfViewerControl`)*: Vector CAD schematic and technical PDF document viewer with continuous vertical scrolling, zoom (Fit Width, 25%–400%), and full-text search highlights.
* **`ZAiChatBox`** *(WinForms & WPF - Aliases: `AiChatBox`, `ZeroAiChatBox`, `ZCopilotBox`)*:
  * Enterprise AI chat box and copilot workstation control with streaming token display, speech-bubble styling, and dark theme synchronization.
  * Powered by `ZeroUI.Core.AiMl` models (`ChatMessage`, `ChatRole`, `ChatStreamingState`, `ChatPromptAction`).
  * Features header deck with Model/Assistant banner, real-time generation state indicator (`Thinking...`, `Generating response...`, `Ready`), message history scroller, one-click clipboard text copy, quick prompt suggestion chips, auto-expanding multi-line prompt editor, and instant Stop Generation controls.
* **`ZTour`** *(WPF & WinForms - New in v1.10.0, Aliases: `ZeroTour`)*:
  * Enterprise interactive onboarding tour and product walkthrough engine matching Ant Design `Tour` standards.
  * Features spotlight target highlighting with dark backdrop cutout masking (`CombinedGeometry.Exclude` / GDI+ `Region.Exclude`), glowing animated bounding box, dynamic popover card with collision-free adaptive positioning (`TopMost = true`), directional arrows, step progress badges (`1/4`), dot indicators, and full keyboard navigation (`Escape`, `Right`, `Left`, `Enter`).
* **`ZToolbarControl` & `ZSideNavControl`** *(Aliases: `ToolbarControl`, `SideNavControl`)*: Anti-aliased action toolbar with collision guard and collapsible vertical navigation bar.
* **`ZTabControl` & `ZSplitContainer`** *(Aliases: `ZeroTabControl`, `ZeroSplitContainer`)*: Modern themed tab control and split container with custom splitter grip.

---

## 7. Smart Warehouse & Logistics Subsystem (`ZeroUI.WinForms.Warehouse`)

* **`ZBarcodeScanControl`** *(Aliases: `BarcodeScanControl`, `ZeroBarcodeScanControl`)*: Industrial USB wedge barcode/QR scanner workstation control with hardware timing delta auto-detection (<35ms), duplicate scan suppression, and parsed metadata cards.
* **`ZWarehouseRack`** *(Aliases: `WarehouseRack`, `ZeroWarehouseRack`)*: 2D interactive storage rack visualizer (Bay x Level x Bin) with occupancy color coding and lot inspection.
* **`ZInventoryCard`** *(Aliases: `InventoryCard`, `ZeroInventoryCard`)*: Industrial stock telemetry card featuring Available, Waiting, and Reserved segment distributions.
* **`ZLotSelector`** *(Aliases: `LotSelector`, `ZeroLotSelector`)*: Automated FIFO/FEFO lot allocation selector with quarantine and expiry locks.

---

## 8. Enterprise Extended Controls Suite (`Z*` - v1.10.1)

A specialized suite of 21 cross-platform enterprise controls spanning Modern Inputs, Industrial Workflows, Visual Schedulers, Tree Views, Markdown/Code/JSON Viewers, and Audit Trails:

### Modern Inputs & Search Overlays
* **`ZAutoComplete`** *(WinForms: `ZeroUI.WinForms.Editors` / WPF: `ZeroUI.Wpf.Editors`)*:
  * Async debounce filtering (default 200ms) with cancellation token support, customizable suggestion items, keyboard navigation (Up/Down/Enter/Escape), and theme-aware popup dropdown.
* **`ZCommandPalette`** *(WinForms: `ZeroUI.WinForms.Common` / WPF: `ZeroUI.Wpf.Common`)*:
  * Global command launcher modal overlay (invoked via `Ctrl+K`), fuzzy match scoring, categorized command groups, keyboard execution triggers, and translucent backdrop dimming.
* **`ZPasswordBox`** *(WinForms: `ZeroUI.WinForms.Editors` / WPF: `ZeroUI.Wpf.Editors`)*:
  * Masked secure text input with interactive password reveal eye toggle button, real-time entropy calculation bar (Weak, Medium, Strong), and focus glow borders.
* **`ZMultiSelect`** *(WinForms: `ZeroUI.WinForms.Editors` / WPF: `ZeroUI.Wpf.Editors`)*:
  * Modern multi-value tag box with dismissible chips, inline text search filter, maximum selection limits, clear-all button, and theme synchronization.
* **`ZSignaturePad`** *(WinForms: `ZeroUI.WinForms.Editors` / WPF: `ZeroUI.Wpf.Editors`)*:
  * High-fidelity vector handwriting ink stroke capture, Bezier curve smoothing, custom stroke width/color, clear canvas trigger, and lossless PNG export.

### Feedback, Notifications & Status
* **`ZNotificationCenter` & `ZNotificationBell`** *(WinForms: `ZeroUI.WinForms.Common` / WPF: `ZeroUI.Wpf.Common`)*:
  * Slide-out notification drawer with severity grouping (Info, Success, Warning, Error), unread badge counts, timestamp formatting, action links, mark-as-read, and companion header bell icon (`ZNotificationBell`).
* **`ZEmptyState`** *(WinForms: `ZeroUI.WinForms.Common` / WPF: `ZeroUI.Wpf.Common`)*:
  * Zero-data placeholder container featuring vector icon glyphs, title, description, and primary/secondary call-to-action buttons.
* **`ZAvatar`** *(WinForms: `ZeroUI.WinForms.Common` / WPF: `ZeroUI.Wpf.Common`)*:
  * Circular or rounded user avatar with image caching, initials fallback with deterministic background color generation, and real-time presence status indicator dots (Online, Busy, Away, Offline).

### Industrial SCADA, Planning & Workflow
* **`ZAlarmSummary`** *(WinForms: `ZeroUI.WinForms.Industrial` / WPF: `ZeroUI.Wpf.Industrial`)*:
  * ISA-18.2 compliant industrial alarm management console featuring state badges (Active, Acked, Cleared), operator acknowledgment action, audio snooze toggles, and severity indicators.
* **`ZScheduler`** *(WinForms: `ZeroUI.WinForms.Industrial` / WPF: `ZeroUI.Wpf.Industrial`)*:
  * Comprehensive scheduling calendar control offering Day, Week, Month, and Agenda views, interactive date navigation headers, and appointment binding.
* **`ZCronEditor`** *(WinForms: `ZeroUI.WinForms.Editors` / WPF: `ZeroUI.Wpf.Editors`)*:
  * Visual cron expression builder (Minutes, Hours, Days, Months, Weekdays), real-time human-readable cadence summary ("At 09:30 on day 1 of every month"), and validation.
* **`ZTimeline`** *(WinForms: `ZeroUI.WinForms.Industrial` / WPF: `ZeroUI.Wpf.Industrial`)*:
  * Vertical manufacturing audit trail and batch journal control with status indicator nodes (Completed, InProgress, Error, Pending), connecting line tracks, timestamps, and detail cards.

### Data Visualization & Virtualization
* **`ZGaugeChart`** *(WinForms: `ZeroUI.WinForms.Charts` / WPF: `ZeroUI.Wpf.Charts`)*:
  * High-precision circular dial gauge with configurable threshold arcs (Normal, Warning, Danger), animated needle pointer, and digital readout display.
* **`ZBubbleChart`** *(WinForms: `ZeroUI.WinForms.Charts` / WPF: `ZeroUI.Wpf.Charts`)*:
  * Multi-variable 3D bubble chart scaling X, Y, Radius, and Color with hover crosshair hit-testing and dynamic legend integration.
* **`ZVirtualGrid`** *(WinForms: `ZeroUI.WinForms.Data` / WPF: `ZeroUI.Wpf.Data`)*:
  * Minimal-footprint, ultra-fast virtualized data table designed for rapid embedding and high-frequency telemetry feeds.

### Documents, Code & Tree Navigation
* **`ZRichTextEditor`** *(WinForms: `ZeroUI.WinForms.Editors` / WPF: `ZeroUI.Wpf.Editors`)*:
  * Full-featured WYSIWYG document editor with formatting toolbar (Bold, Italic, Underline, Bullet lists, Colors, Font sizes), and RTF/HTML round-trip persistence.
* **`ZMarkdownViewer`** *(WinForms: `ZeroUI.WinForms.Documents` / WPF: `ZeroUI.Wpf.Documents`)*:
  * Live Markdown document previewer with headings ($H_1-H_6$), code blocks, blockquotes, horizontal rules, bullet lists, and a Raw/Preview view toggle button.
* **`ZCodeEditor`** *(WinForms: `ZeroUI.WinForms.Documents` / WPF: `ZeroUI.Wpf.Documents`)*:
  * Monospace script and formula editor with gutter line numbering, configurable tab indentation, and dark/light theme integration.
* **`ZJsonEditor`** *(WinForms: `ZeroUI.WinForms.Editors` / WPF: `ZeroUI.Wpf.Editors`)*:
  * Zero-dependency JSON editor with pretty-formatting (`FormatJson`), minification (`MinifyJson`), real-time token syntax validation (`ValidateJson`), gutter line numbers, and status indicators.
* **`ZTreeView`** *(WinForms: `ZeroUI.WinForms.Common` / WPF: `ZeroUI.Wpf.Common`)*:
  * High-performance hierarchical tree view control supporting chevron expand/collapse toggles, optional node checkboxes, selection highlights, and connector lines.
* **`ZTransferList`** *(WinForms: `ZeroUI.WinForms.Common` / WPF: `ZeroUI.Wpf.Common`)*:
  * Dual-box item transfer control with transfer actions ($>, >>, <, <<$), dual search boxes, item count badges, and disabled state handling.

---

## 9. Industrial Security & Identity Management Suite (`Z*` - v1.11.0)

A decoupled, enterprise-grade authentication, role-based authorization (RBAC), touch HMI, and compliance audit suite engineered specifically for 24/7 industrial runtime environments and multi-operator manufacturing stations:

### Multi-Modal Authentication & Session Controls
* **`ZLoginDialog`** *(WinForms: `ZeroUI.WinForms.Common` / WPF: `ZeroUI.Wpf.Common`)*:
  * Multi-modal industrial login dialog supporting Username/Password, Operator quick-PIN, and RFID/NFC hardware badge dongles.
  * Completely decoupled via `IAuthenticationProvider`, `IUserStore`, and `ISessionManager`.
* **`ZUserStatusBadge`** *(WinForms: `ZeroUI.WinForms.Common` / WPF: `ZeroUI.Wpf.Common`)*:
  * Real-time HMI operator status bar badge displaying current user avatar, operator name, active role badge (`Operator`, `Technician`, `Engineer`, `Administrator`), idle elapsed timer, and quick sign-out / lock triggers.
* **`ZBadgeReaderListener`** *(WinForms: `ZeroUI.WinForms.Common` / WPF: `ZeroUI.Wpf.Common`)*:
  * Hardware RFID/NFC badge card scanner listener supporting USB HID keyboard-wedge scanners, serial readers, configurable preamble/postamble characters, and timing debounce.
* **`ZIdleTimeoutMonitor` & `ZLockScreenOverlay`** *(WinForms: `ZeroUI.WinForms.Common` / WPF: `ZeroUI.Wpf.Common`)*:
  * High-performance, zero-allocation activity heartbeat monitor powered by 64-bit monotonic clock (`MonotonicClock`).
  * Triggers configurable warning countdown and automatically locks the HMI or signs out the operator upon inactivity timeout.
  * Translucent lock screen overlay (`ZLockScreenOverlay`) requiring quick-PIN or badge re-authentication to resume runtime operation.

### Touch HMI Virtual Keyboard
* **`ZVirtualKeyboard` & `ZTouchKeyboardProvider`** *(WinForms: `ZeroUI.WinForms.Common` / WPF: `ZeroUI.Wpf.Common`)*:
  * Dedicated on-screen touch virtual keyboard offering QWERTY alphanumeric and 10-key numeric keypad modes with large hit targets (48px+) optimized for gloved industrial operators.
  * `ZTouchKeyboardProvider` automatically detects clicks/focus on text inputs and pops up the appropriate keyboard layout adjacent to the target control.

### Role-Based Access Control (RBAC) & Audit Trails
* **`ZRoleMatrix`** *(WinForms: `ZeroUI.WinForms.Data` / WPF: `ZeroUI.Wpf.Data`)*:
  * Visual matrix table mapping roles against permissions (`Alarms.Acknowledge`, `Parameters.Write`, `Recipes.Edit`, `Audit.Export`).
  * Features interactive tri-state checkbox toggles, batch updates, and asynchronous persistence via `IRoleStore`.
* **`ZUserManager`** *(WinForms: `ZeroUI.WinForms.Data` / WPF: `ZeroUI.Wpf.Data`)*:
  * Industrial operator administration console for creating, editing, locking, assigning RFID badge dongles, and managing passwords/PINs.
* **`ZAuthorizeExtender` & `ZAuthorize`** *(WinForms: `ZeroUI.WinForms.Common` / WPF: `ZeroUI.Wpf.Common`)*:
  * Declarative, zero-boilerplate RBAC extender (WinForms) and attached properties (WPF) that dynamically enable, disable, or hide controls based on active operator permissions.
* **`ZOperationLogViewer`** *(WinForms: `ZeroUI.WinForms.Data` / WPF: `ZeroUI.Wpf.Data`)*:
  * 21 CFR Part 11 compliant operator action and electronic signature audit viewer with severity badges, actor filtering, and streaming CSV export.



