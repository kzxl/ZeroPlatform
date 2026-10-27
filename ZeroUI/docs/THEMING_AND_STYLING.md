# ZeroUI: Theming, Styling & Rendering Architecture

ZeroUI provides an enterprise theme engine, high-density industrial color palette, and zero-allocation text rasterization infrastructure.

---

## 1. Unified Theme Engine: Obsidian Dark & Clean Light

ZeroUI includes two meticulously designed industrial themes with instantaneous reactive switching across all controls:

| Theme | Background | Surface / Cards | Primary Accent | Purpose |
| :--- | :--- | :--- | :--- | :--- |
| **Obsidian Dark** | `#12151C` | `#1A1E28` | `#00E5FF` (Cyan) | High-contrast 24/7 control rooms, low eye fatigue |
| **Clean Light** | `#F8FAFC` | `#FFFFFF` | `#0284C7` (Sky Blue) | Clean-room labs, daylight production offices |

### Reactive Theme Switching
Theme changes propagate instantaneously across all instantiated forms and controls without requiring application restarts:

```csharp
using ZeroUI.WinForms.Theme;

// Switch application to Obsidian Dark Theme
ZeroThemeManager.SetTheme(ZeroThemeMode.ObsidianDark);

// Switch application to Clean Light Theme
ZeroThemeManager.SetTheme(ZeroThemeMode.CleanLight);
```

---

## 2. Zero-Allocation Rendering Engine

Standard WinForms controls suffer from GDI handle churn caused by repeatedly recreating `Font`, `Pen`, and `Brush` instances inside `OnPaint`. ZeroUI eliminates this completely:

* **`ZeroFontCache`**: Thread-safe binary-keyed font memoization. Fonts are leased and reused indefinitely across paint cycles.
* **`ZeroStringFormats`**: Immutable singleton instances for standard alignments (Center, Left, Right, Ellipsis) eliminating GDI string format handle allocations.
* **Win32 Memory DC DIBSection Engine**: Offscreen unmanaged bitmap buffer with zero-copy `BitBlt` presentation. Completely flicker-free and 100% resilient across Remote Desktop (RDP), Citrix, and virtual machine sessions.

---

## 3. Single-HWND Architecture

WinForms applications that nest dozens of panels, groupboxes, and buttons quickly exhaust the Win32 OS handle limit (10,000 handles), resulting in catastrophic application crashes.

Composite controls in ZeroUI (`GridControl`, `StepsControl`, `ToolbarControl`, `TimelineControl`, `SideNavControl`):
- Maintain **exactly 1 top-level Win32 window handle (`HWND`)**.
- All internal child elements, buttons, tabs, and headers are virtual vector nodes rendered inside the single HWND.
- Eliminates Win32 handle leaks, window clipping glitches, and child-window paint synchronization lag.

---

## 4. Centralized 60 FPS Animation Clock (`ZeroAnimationClock`)

Instead of each animated control running its own independent `System.Windows.Forms.Timer` (which causes thread timer starvation and jitter), ZeroUI uses a single global animation clock:

- **Single Global Ticker**: Runs at a steady 60 FPS with drift compensation.
- **Copy-On-Write Registration**: Animated nodes register and unregister via lock-free arrays.
- **Synchronized ISA-18.2 Phases**: Generates synchronized industrial blink signals (`BlinkFast` 2Hz, `BlinkSlow` 1Hz, `PulsePhase`, `FluidPhase`), ensuring all alarm annunciators and pipe flow animations in the entire plant pulse in perfect unison.

---

## 5. Per-Monitor V2 Dynamic High-DPI Scaling

- Supports mixed-DPI multi-monitor industrial setups (e.g. 4K operator dashboard alongside standard 1080p touch panels).
- Dynamically recalculates padding, row heights, and vector font sizes upon `WM_DPICHANGED` without blurry DWM bitmap stretching.
