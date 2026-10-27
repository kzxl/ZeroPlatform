# Subsystem Modularization & Cross-Platform Control Parity Standard

## 1. Objective and Architecture Topology
To keep `ZeroUI` maintainable, fast to compile, cleanly segmented, and easily consumable in targeted environments, desktop components are partitioned into 6 granular subsystem clusters under `src/WinForms/` and `src/Wpf/`:

```
src/
├── WinForms/
│   ├── ZeroUI.WinForms.Common/       # Foundations, Base, Buttons, Containers, Layout, Navigation, Feedback, Overlays, Theme, Icons
│   ├── ZeroUI.WinForms.Editors/      # Standalone Value Editors (Text, Date, Check, Color, Token, Spin, Media)
│   ├── ZeroUI.WinForms.Data/         # High-Performance Virtual GridControl, TreeList, PropertyGrid, PivotGrid, Lookup Editors
│   ├── ZeroUI.WinForms.Charts/       # ChartControl, Series Engines, Specialized Charts
│   ├── ZeroUI.WinForms.Industrial/   # SCADA Gauges, DigitalIndicator, Mimic Canvas, Pipeline Flow, BMS, Energy, Process
│   ├── ZeroUI.WinForms.Documents/    # PdfViewerControl, SpreadsheetControl, DocumentPreviewControl
│   └── ZeroUI.WinForms/              # Umbrella Metapackage referencing all 6 clusters (100% backward compatible)
└── Wpf/
    ├── ZeroUI.Wpf.Common/            # Foundations, Base, Buttons, Containers, Layout, Navigation, Feedback, Overlays, Theme
    ├── ZeroUI.Wpf.Editors/           # Standalone Value Editors, Creative & Media Editors
    ├── ZeroUI.Wpf.Data/              # Single-Visual Virtual GridControl, TreeList, PropertyGrid, PivotGrid, Lookup Editors
    ├── ZeroUI.Wpf.Charts/            # ChartControl, Series Engines, Direct-Rendered Telemetry & Financial Charts
    ├── ZeroUI.Wpf.Industrial/        # SCADA Gauges, DigitalIndicator, CircularGauge, Mimic Canvas, BMS, Energy, Process
    ├── ZeroUI.Wpf.Documents/         # PdfViewerControl, SpreadsheetControl, DocumentPreviewControl
    └── ZeroUI.Wpf/                   # Umbrella Metapackage referencing all 6 clusters (100% backward compatible)
```

---

## 2. Dependency Rules & Decoupling Invariants
1. **Layered Hierarchy:**
   - `Common` depends only on `ZeroUI.Core`.
   - `Editors` depends on `Common` and `ZeroUI.Core`. It MUST NOT depend on `Data` or `Industrial`.
   - `Data` depends on `Editors`, `Common`, and `ZeroUI.Core`.
   - `Charts` depends on `Common`, `ZeroUI.Core`, and sovereign math packages (`ZeroData.Core`, `ZeroTensor.Core`, etc.).
   - `Industrial` depends on `Common`, `ZeroUI.Core.Scada`, and `ZeroUI.Core`.
   - `Documents` depends on `Common` and `ZeroUI.Core`.
   - `Metapackages` (`ZeroUI.WinForms`, `ZeroUI.Wpf`) reference all 6 clusters and contain only metapackage targets and assembly attributes.
2. **Button Layering:**
   - Buttons (`SimpleButton`, `SplitButton`, `ButtonGroup`) must be physically placed in `Common/Buttons/` while retaining the `ZeroUI.WinForms.Editors` and `ZeroUI.Wpf.Editors` namespaces. This prevents circular dependencies when `Common` containers and dialogs embed buttons.
3. **Lookup Editor Decoupling:**
   - Lookup editors with embedded Grids (`GridLookupEdit`, `SearchLookUpEdit`, `TreeListLookUpEdit`) must be physically placed in `Data/LookupEditors/` while retaining namespaces `ZeroUI.WinForms.Editors` and `ZeroUI.Wpf.Editors`.
4. **Dynamic XAML Style Resolution:**
   - `ZeroWpfStyles` in `ZeroUI.Wpf.Common` must resolve editor control styles dynamically via resource keys (`GetStyleByName`) to prevent compile-time circular references between `Common` and `Editors`.
5. **Unified XAML Namespace:**
   - All WPF subsystem assemblies must register `[assembly: XmlnsDefinition("http://schemas.zeroui.com/wpf", "<Namespace>")]` so XAML consumers only need one xmlns declaration.

---

## 3. Cross-Platform Control Parity Standard
1. **Symmetric Feature Delivery:**
   - Any new control, feature, or telemetry model introduced into WinForms or WPF must be mirrored with an equivalent control or shim in the companion framework.
2. **Standardized Control Names:**
   - WinForms and WPF share the same canonical enterprise names (`GridControl`, `ChartControl`, `DigitalIndicator`, `CircularGauge`, `LinearGauge`, `RadialGauge`, `Badge`, `InfoBar`, `AlertBanner`, `SimpleButton`, `TextEdit`, etc.).
   - When historical naming diverged, provide bi-directional class aliases (e.g. `SideNav` & `SideNavControl`, `InfoBar` & `AlertBanner`).
3. **Automated Parity Verification:**
   - Automated parity tests in `tests/ZeroUI.Desktop.Tests/ControlParityInspectionTests.cs` must be executed before each release to inspect and report property and control drift across all 6 subsystem clusters.
