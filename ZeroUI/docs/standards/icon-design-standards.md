# ZeroUI Control & Toolbox Icon Design Standard

**Standard Identifier:** `STD-UI-ICON-001`  
**Version:** 1.0.0  
**Target Platform:** `ZeroUI.WinForms`, `ZeroUI.Wpf`  
**Applies To:** All public UI controls, components, panels, dialogs, and tools  

---

## 1. Overview & Core Philosophy

In Windows Forms and enterprise desktop engineering, the **Visual Studio Toolbox** and designer surfaces represent the primary discovery point for developers. Unstyled default icons (the generic gear or blank box) degrade developer ergonomics, impair professional visual polish, and slow down rapid form construction.

ZeroUI enforces a **Rigorous, Synchronized Control Icon System** ensuring that every single control features an instantly recognizable, crisp, high-contrast 16x16 icon aligned with its functional domain.

---

## 2. Technical Specifications

| Parameter | Specification | Engineering Constraint |
| :--- | :--- | :--- |
| **Dimensions** | **$16 \times 16$ pixels** | Strict. Must match Win32 / Visual Studio Toolbox standard icon dimensions. |
| **File Format** | **Windows 32-bit Bitmap (`.bmp`)** | Compatible with `System.Drawing.ToolboxBitmapAttribute`. |
| **Color Depth** | **32-bit ARGB** or **24-bit RGB with Transparency Key** | Corner pixel $(0, 0)$ is treated as the transparent mask color (`#000000` or `#FF00FF`). |
| **Pixel Grid Alignment**| **Crisp 1px Strokes** | Anti-aliasing must be sharp and deliberate. Avoid fuzzy subpixel blur at $16 \times 16$. |
| **Safe Canvas Margin** | **1px Perimeter Padding** | Active glyph bounded within $[1, 1]$ to $[14, 14]$ (14x14 px bounding box). |
| **Visual Gravity** | **Centered Optical Weight** | Glyphs must balance optical weight evenly across horizontal and vertical axes. |
| **Storage Location** | `src/ZeroUI.WinForms/Icons/` | Embedded directly into assembly via `<EmbeddedResource Include="Icons\*.bmp" />`. |

---

## 3. Semantic Color Palette by Subsystem

To create instant visual cohesion and effortless category scanning in the IDE toolbox, icons adhere to dedicated domain color codes based on the ZeroUI Design Token System:

| Subsystem Category | Primary Fill | Stroke / Accent | Semantic Intent & Target Controls |
| :--- | :---: | :---: | :--- |
| **Data & Grids** | Sapphire Blue (`#3B82F6`) | Deep Blue (`#1D4ED8`) | Data tables, OLAP cubes, query builders (`GridControl`, `PivotGridControl`, `FilterControl`, `RangeControl`). |
| **Analytics & Charts** | Emerald Green (`#10B981`) | Forest Green (`#047857`) | Trends, series, statistical inspection (`ZeroChart`, `ZeroTrendChart`, `ZeroBoxPlotChart`, `ZeroSpcChart`, `ZeroHeatmap`). |
| **Form Editors & Inputs** | Sky Blue (`#0EA5E9`) | Slate Indigo (`#0284C7`) | Text inputs, pickers, toggles, steppers (`TextEdit`, `SpinEdit`, `DateEdit`, `ZeroLookup`, `ZeroSwitch`, `ZeroSlider`). |
| **Industrial SCADA & P&ID**| Amber Gold (`#F59E0B`) | Rust Orange (`#B45309`) | Actuators, plant mimic, gauges, alarms (`ZeroIndustrialPump`, `ZeroTank3D`, `ZeroGauge`, `ZeroPlcIoMonitor`, `ZeroAlarmGrid`). |
| **Hazard & Permissives** | Crimson Red (`#EF4444`) | Dark Wine (`#B91C1C`) | Safety interlocks, emergency stops, shutdown banners (`ZeroInterlockIndicator`, `ZeroAlertBanner`, `ZeroCommandButton`). |
| **Warehouse & Logistics** | Electric Indigo (`#6366F1`) | Deep Violet (`#4338CA`) | Storage racks, scanners, inventory cards (`ZeroWarehouseRack`, `ZeroBarcodeScanControl`, `ZeroLotSelector`). |
| **Layout & Windowing** | Obsidian Slate (`#475569`) | Cool Slate (`#94A3B8`) | Splitters, tab containers, dock panels, scrollbars (`ZeroDockManager`, `ZeroSplitContainer`, `ZeroTabControl`, `WizardControl`). |
| **Diagnostics & Systems**| Violet Purple (`#8B5CF6`) | Deep Purple (`#6D28D9`) | Runtimes, inspectors, property explorers (`ZeroPropertyGrid`, `ZeroVisualDebugger`, `ValidationProvider`). |

---

## 4. Visual Metaphors & Pixel Archetypes

Every icon represents a distilled, high-contrast mechanical or data archetype:

```
[ Data Table ]        [ Gauge / Meter ]     [ Switchgear / Actuator ]
  . . . . . . . .       . . . # # . . .       . . # # # # # # . .
  . # # # # # # .       . # # . . # # .       . . . . # # . . . .
  . # . # . # # .       # # . . \ . # #       . # # # # # # # # .
  . # # # # # # .       # # . . . \ # #       . # . . # # . . # .
  . # . # . # # .       . # # . . # # .       . # # # # # # # # .
  . # # # # # # .       . . . # # . . .       . . . . # # . . . .
  . . . . . . . .       . . . . . . . .       . . # # # # # # . .
```

* **Grid/Data:** 1px boundary box with shaded header band and vertical/horizontal cell dividers.
* **Charts/Analytics:** Multi-height vertical bars or rising diagonal spline with marker vertices.
* **Inputs/Pickers:** Rounded pill outline with centered cursor, toggle thumb, or dropdown chevron.
* **Actuators/Pumps:** Rotating circular volute with tangential nozzle pipe.
* **Gauges:** Outer 270° arc with needle extending from center pivot point.
* **Warehouse/Racks:** Multi-tier horizontal beams with vertical upright posts.

---

## 5. Code Decoration Standard

Every public WinForms control, container, or component must be decorated with both `ToolboxItem` and `ToolboxBitmap` attributes:

```csharp
using System.ComponentModel;
using System.Drawing;
using ZeroUI.WinForms.Icons;

namespace ZeroUI.WinForms.Industrial
{
    [ToolboxItem(true)]
    [ToolboxBitmap(typeof(ZeroIcons), "ZeroWarehouseRack.bmp")]
    [Category("ZeroUI - Industrial & SCADA")]
    [Description("2D Smart Warehouse Storage Rack visualizer for WMS inventory management")]
    public class ZeroWarehouseRack : Control
    {
        // Control implementation...
    }
}
```

### Key Rules:
1. **Marker Type:** Always use `typeof(ZeroIcons)` as the type anchor so that the WinForms designer resolves the embedded resource from the `ZeroUI.WinForms.Icons` namespace.
2. **File Naming:** The bitmap file name MUST match the control class name: `"<ControlName>.bmp"`. For semantic alias pairs (e.g. `GridControl` and `ZeroGridControl`), provide matching `.bmp` resources or aliases.
3. **Embedded Resource:** In `ZeroUI.WinForms.csproj`, the glob `<EmbeddedResource Include="Icons\*.bmp" />` ensures automatic inclusion on compilation.

---

## 6. Verification & Automated Icon Audit Protocol

To maintain 100% icon coverage across the lifecycle of ZeroUI, an automated audit script validates:
1. **Presence:** 100% of public classes inheriting from `Control`, `Component`, `UserControl`, or `Form` (excluding internal helper classes) must have `[ToolboxBitmap(typeof(ZeroIcons), "...bmp")]`.
2. **File Existence:** Every referenced `.bmp` file must exist physically in `src/ZeroUI.WinForms/Icons/`.
3. **Format Rigor:** Every `.bmp` file must be exactly $16 \times 16$ pixels and non-corrupt.
