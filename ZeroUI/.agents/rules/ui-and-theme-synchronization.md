# UI and Theme Synchronization Standard

## 1. Core Principle & Objective
To guarantee flawless visual consistency and prevent text or background contrast defects when users switch themes at runtime, all controls and user interfaces in **ZeroUI** (across both **WinForms** and **WPF**) must adhere to this mandatory synchronization standard.

---

## 2. Mandatory Rules

### Rule 1: Zero Hardcoded Color Literals
- **Strictly Prohibited:**
  - Hardcoded hex literals (e.g., `#181B26`, `#2E344E`, `#F1F5F9`, `#94A3B8`, `#64748B`, `#FFFFFF`, `#000000`).
  - Static brushes (e.g., `Brushes.White`, `Brushes.Black`, `Brushes.Gray`).
  - Hardcoded `Color.FromArgb(...)` for standard UI backgrounds, borders, or text elements.
- **Mandatory Usage:**
  - **WPF XAML:** Always use `{DynamicResource ZeroUI.<TokenName>}`.
  - **WPF C# Vector / OnRender:** Always use `ZeroWpfTheme.<TokenName>` (frozen brushes and pens).
  - **WinForms C# GDI+ / OnPaint:** Always use `ZeroTheme.Colors.<TokenName>`.

---

### Rule 2: Semantic Design Token Mapping Matrix

| Semantic Role | WPF XAML Resource | WPF C# (`ZeroWpfTheme`) | WinForms C# (`ZeroTheme.Colors`) | Intended Purpose |
| :--- | :--- | :--- | :--- | :--- |
| **Workspace Background** | `{DynamicResource ZeroUI.BgPrimary}` | `BgPrimary` | `Background` | Deep application canvas / Form backdrop |
| **Card / Panel Surface** | `{DynamicResource ZeroUI.BgCard}` | `BgCard` | `Surface` / `CardBackground` | Cards, popups, toolbars, node boxes |
| **Input / Slot Fill** | `{DynamicResource ZeroUI.BgInput}` | `BgInput` | `Background` | TextBoxes, ComboBoxes, rack slots |
| **Default Border** | `{DynamicResource ZeroUI.BorderDefault}` | `BorderDefault` / `BorderPen` | `Border` | Perimeter outlines, splitters, dividers |
| **Subtle Border** | `{DynamicResource ZeroUI.BorderSubtle}` | `BorderSubtle` | `Border` | Secondary grids, inner card borders |
| **Primary Text** | `{DynamicResource ZeroUI.TextPrimary}` | `TextPrimary` | `TextPrimary` | Headings, active values, card titles |
| **Secondary Text** | `{DynamicResource ZeroUI.TextSecondary}` | `TextSecondary` | `TextSecondary` | Subtitles, status captions, legends |
| **Muted Text** | `{DynamicResource ZeroUI.TextMuted}` | `TextMuted` | `TextSecondary` | Inactive hints, timestamps, units |
| **Primary Accent** | `{DynamicResource ZeroUI.PrimaryAccent}` | `PrimaryAccent` | `Primary` | Key interactive highlights, indicators |
| **Selection Text** | `{DynamicResource ZeroUI.SelectionForeground}`| `SelectionForeground` | `TextPrimary` | Button labels on accent fills |
| **Status: Success** | `{DynamicResource ZeroUI.SuccessAccent}` | `SuccessAccent` | `Success` | Healthy status, normal operations |
| **Status: Warning** | `{DynamicResource ZeroUI.WarningAccent}` | `WarningAccent` | `Warning` | Degraded state, alerts, cautions |
| **Status: Danger** | `{DynamicResource ZeroUI.DangerAccent}` | `DangerAccent` | `Danger` | Alarms, severed cables, faults |

---

### Rule 3: Mandatory Theme Lifecycle Hook
Every custom control, component, or composite view that instantiates UI elements procedurally in C# **MUST** register for central theme events and provide dynamic updates:

#### WPF Controls:
```csharp
// Constructor
ZeroWpfTheme.ThemeChanged += UpdateTheme;

private void UpdateTheme()
{
    Background = ZeroWpfTheme.BgInput;
    BorderBrush = ZeroWpfTheme.BorderDefault;
    if (_popupBorder != null)
    {
        _popupBorder.Background = ZeroWpfTheme.BgCard;
        _popupBorder.BorderBrush = ZeroWpfTheme.BorderDefault;
    }
    InvalidateVisual();
}
```

#### WinForms Controls:
```csharp
// Constructor
ZeroTheme.ThemeChanged += (s, e) =>
{
    BackColor = ZeroTheme.Colors.Surface;
    Invalidate();
};
```

---

### Rule 4: Overlay & Animation Non-Blocking Requirement
1. **No Dock-Triggered Relayout Storms:**
   - Slide-out drawers, flyouts, side sheets, and toast notifications must **NEVER** use `Dock = DockStyle.Right/Left` on composite parent containers.
   - Use **Floating Overlay** (`Dock = DockStyle.None`, `Anchor = Top | Bottom | Right`) or GPU-accelerated `TranslateTransform` to avoid triggering layout recalculations on sibling data-heavy controls.
2. **Time-Based Animation:**
   - Animations must be driven by `deltaSeconds` from `ZeroAnimationClock` with standardized duration (e.g., 200–250 ms) and smooth mathematical easing (`EaseOutCubic`). Never use crude divisor-based step jumps.

---

### Rule 5: Dual-Skin Pre-Commit Verification Checklist
Before submitting or committing any UI control, feature, or bugfix:
- [ ] **Dark Skin Verification:** Control inspected under a dark skin (e.g., `ObsidianDark`).
- [ ] **Light Skin Verification:** Control inspected under a light skin (e.g., `CleanLight` / `OfficeWhite`).
- [ ] **Zero Text Washout:** All labels, metrics, and captions remain legible with a minimum 4.5:1 contrast ratio against their immediate background.
- [ ] **Dynamic Switching:** Toggling the skin at runtime updates all child elements instantly without requiring application restart.
- [ ] **Zero Warnings, Zero Errors:** Build output is 100% warning-free across all target frameworks.
