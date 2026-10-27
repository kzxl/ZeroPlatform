# ZeroUI Theme & UI Synchronization Standards

## 1. Architectural Overview

ZeroUI enforces a decoupled, cross-platform theme engine where **`ZeroUI.Core`** acts as the single source of truth for visual tokens, while platform-specific renderers (**`ZeroUI.Wpf`** and **`ZeroUI.WinForms`**) map tokens to high-performance platform graphics primitives.

```
┌─────────────────────────────────────────────────────────────┐
│ ZeroUI.Core.Theme                                           │
│  - ZeroSkinManager (Active Skin, Central Registry)         │
│  - ZeroPaletteTokens (Raw Hex Codes, Mode, Semantic Roles)  │
│  - ZeroSkinDefaults (Obsidian Dark, Clean Light, etc.)      │
└──────────────────────────────┬──────────────────────────────┘
                               │
            ┌──────────────────┴──────────────────┐
            ▼                                     ▼
┌──────────────────────────────┐ ┌──────────────────────────────┐
│ ZeroUI.Wpf.Theme             │ │ ZeroUI.WinForms.Theme        │
│  - ZeroWpfTheme              │ │  - ZeroTheme                 │
│    • Frozen SolidColorBrushes│ │    • Static Color Tokens     │
│    • Frozen Pens             │ │    • ZeroThemePalette (Light)│
│  - ZeroWpfStyles.xaml        │ │    • ZeroThemePalette (Dark) │
│    • DynamicResource Dict    │ │    • DoubleBuffered GDI+     │
│    • Implicit TextBlock Style│ │    • TextRenderer Hinting    │
└──────────────────────────────┘ └──────────────────────────────┘
```

---

## 2. Standard Design Tokens

Every control in ZeroUI must consume one of the standardized semantic tokens below. Under no circumstances should arbitrary hex colors or static system brushes be introduced.

### 2.1 Color Tokens Specification

| Token Identifier | Semantic Meaning | Recommended Dark Hex | Recommended Light Hex | WPF Accessor | WinForms Accessor |
| :--- | :--- | :--- | :--- | :--- | :--- |
| `BgPrimary` | Primary application canvas | `#11131F` | `#F8F9FC` | `ZeroWpfTheme.BgPrimary` | `ZeroTheme.Colors.Background` |
| `BgCard` | Panel, card, dropdown surface | `#181A28` | `#FFFFFF` | `ZeroWpfTheme.BgCard` | `ZeroTheme.Colors.Surface` |
| `BgInput` | Textbox, editor, slot background | `#141724` | `#F1F3F9` | `ZeroWpfTheme.BgInput` | `ZeroTheme.Colors.Background` |
| `BgHover` | Element hover state | `#262A44` | `#E8EDF8` | `ZeroWpfTheme.BgHover` | `ZeroTheme.Colors.Hover` |
| `BgActive` | Selected row, active tab | `#2E3354` | `#DFE5F4` | `ZeroWpfTheme.BgActive` | `ZeroTheme.Colors.Hover` |
| `BorderDefault` | Perimeter outline, structural rule | `#2E344E` | `#DCE1EE` | `ZeroWpfTheme.BorderDefault` | `ZeroTheme.Colors.Border` |
| `BorderSubtle` | Inner cell line, subtle separator | `#22273D` | `#EDF0F7` | `ZeroWpfTheme.BorderSubtle` | `ZeroTheme.Colors.Border` |
| `TextPrimary` | High-contrast headline / title | `#F1F5F9` | `#0F172A` | `ZeroWpfTheme.TextPrimary` | `ZeroTheme.Colors.TextPrimary` |
| `TextSecondary` | Subtitles, captions, legends | `#94A3B8` | `#475569` | `ZeroWpfTheme.TextSecondary` | `ZeroTheme.Colors.TextSecondary` |
| `TextMuted` | Disabled labels, micro units | `#64748B` | `#94A3B8` | `ZeroWpfTheme.TextMuted` | `ZeroTheme.Colors.TextSecondary` |
| `PrimaryAccent` | Brand color, primary actions | `#818CF8` | `#4F46E5` | `ZeroWpfTheme.PrimaryAccent` | `ZeroTheme.Colors.Primary` |
| `Success` | Normal status, healthy state | `#A6E3A1` | `#16A34A` | `ZeroWpfTheme.SuccessAccent` | `ZeroTheme.Colors.Success` |
| `Warning` | Alert, caution, degraded state | `#F9E2AF` | `#D97706` | `ZeroWpfTheme.WarningAccent` | `ZeroTheme.Colors.Warning` |
| `Danger` | Critical alarm, cable break | `#F38BA8` | `#DC2626` | `ZeroWpfTheme.DangerAccent` | `ZeroTheme.Colors.Danger` |

---

## 3. Implementation Patterns by Technology

### 3.1 WPF Implementation Guidelines

1. **Declarative XAML (Templates and UserControls):**
   - Always bind visual properties via `{DynamicResource ZeroUI.<TokenName>}`.
   - Example:
     ```xml
     <Border Background="{DynamicResource ZeroUI.BgCard}"
             BorderBrush="{DynamicResource ZeroUI.BorderDefault}"
             BorderThickness="1" CornerRadius="4">
         <TextBlock Text="{Binding Title}"
                    Foreground="{DynamicResource ZeroUI.TextPrimary}"
                    FontWeight="SemiBold" />
     </Border>
     ```

2. **Procedural Vector Drawing (`OnRender` / `DrawingContext`):**
   - Consume frozen brushes and pens directly from `ZeroWpfTheme`.
   - Never create new `SolidColorBrush` or `Pen` instances inside `OnRender`.
   - Subscribe to `ZeroWpfTheme.ThemeChanged` in the constructor and trigger `InvalidateVisual()`.
   - Example:
     ```csharp
     public class CustomVisualizer : FrameworkElement
     {
         public CustomVisualizer()
         {
             ZeroWpfTheme.ThemeChanged += () => InvalidateVisual();
         }

         protected override void OnRender(DrawingContext dc)
         {
             dc.DrawRectangle(ZeroWpfTheme.BgCard, ZeroWpfTheme.BorderPen, new Rect(0, 0, ActualWidth, ActualHeight));
             // FormattedText using ZeroWpfTheme.TextPrimary
         }
     }
     ```

3. **Procedural Control Construction (Compound Controls):**
   - Controls that create borders, textblocks, or popups in C# code must retain references to those elements and update their brushes inside a dedicated `UpdateTheme()` method hooked to `ZeroWpfTheme.ThemeChanged`.

---

### 3.2 WinForms Implementation Guidelines

1. **GDI+ Paint Loop Optimization:**
   - Consume colors from `ZeroTheme.Colors`.
   - Always wrap pen/brush creations in `using` statements, or use cached/shared brushes:
     ```csharp
     protected override void OnPaint(PaintEventArgs e)
     {
         var g = e.Graphics;
         var theme = ZeroTheme.Colors;

         using (var bgBrush = new SolidBrush(theme.Surface))
         {
             g.FillRectangle(bgBrush, ClientRectangle);
         }

         using (var borderPen = new Pen(theme.Border, 1f))
         {
             g.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);
         }

         TextRenderer.DrawText(g, Text, Font, ClientRectangle, theme.TextPrimary, TextFormatFlags.VerticalCenter);
     }
     ```

2. **Event Hooking & Cleanup:**
   - In constructor: `ZeroTheme.ThemeChanged += OnThemeChanged;`
   - In `Dispose(bool disposing)`: `ZeroTheme.ThemeChanged -= OnThemeChanged;` to prevent memory leaks.

---

### 3.3 ZeroUI Skin Framework Base Classes & Contracts

To eliminate boilerplate and ensure uniform, zero-leak theme lifecycle management, all controls should inherit from the platform-specific architectural base classes:

#### 1. Core Contract (`IZeroSkinnable`)
Defined in `ZeroUI.Core.Theme`:
```csharp
public interface IZeroSkinnable
{
    bool UseDefaultSkin { get; set; }
    ZeroSkin? CustomSkin { get; set; }
    ZeroSkin EffectiveSkin { get; }
    void ApplySkin(ZeroSkin skin);
}
```

#### 2. WinForms Architecture (`ZeroControlBase` & `PaintHelper`)
- **`ZeroControlBase`**:
  - Automatically configures high-speed double-buffering (`UserPaint`, `AllPaintingInWmPaint`, `OptimizedDoubleBuffer`, `ResizeRedraw`).
  - Subscribes to `ZeroSkinManager.SkinChanged` and `ZeroTheme.ThemeChanged`, safely marshaling to UI thread via `BeginInvoke` and unhooking on `Dispose(bool disposing)`.
  - Exposes `protected ZeroThemePalette CurrentPalette` resolving to either global `ZeroTheme.Colors` or scoped `EffectiveSkin`.
  - Override `protected virtual void OnThemeChanged(ZeroSkin skin)` to update child controls or internal GDI+ caches.
- **`PaintHelper` (`ZeroPaintHelper`)**:
  - High-performance zero-alloc GDI+ routines: `DrawCard()`, `DrawStatusBadge()`, `DrawFocusRing()`, `CreateRoundedRectangle()`.

#### 3. WPF Architecture (`ZeroWpfControlBase` & `ZeroWpfVisualBase`)
- **`ZeroWpfControlBase`**:
  - Architectural base for composite/templated WPF controls (`Control`).
  - Dynamically binds default `Background`, `Foreground`, and `BorderBrush` to theme dynamic resources.
  - Automatically binds and unbinds to `ZeroWpfTheme.ThemeChanged` via `Loaded` / `Unloaded` to prevent memory leaks.
  - Exposes `EffectiveSkin`, `UseDefaultSkin`, and `CustomSkin` dependency properties.
- **`ZeroWpfVisualBase`**:
  - Architectural base for direct vector visualizers (`FrameworkElement` / `OnRender`).
  - Automatically invalidates visual surface on theme changes.
- **`ZeroSkinProps.SkinName`**:
  - Attached property allowing localized theme scoping on any container:
    ```xml
    <Border theme:ZeroSkinProps.SkinName="ObsidianDark">
        <!-- Child controls inside this container render in Obsidian Dark regardless of window theme -->
    </Border>
    ```

---

## 4. Layout Isolation & Animation Standards

1. **Prohibit Sibling-Degrading Docking for Transient Overlays:**
   - Side drawers, flyouts, and modal sidebars must **never** use `Dock = DockStyle.Right` or `Dock = DockStyle.Left` when sibling controls host heavy computational datasets (e.g., virtualized grids, high-frequency charts).
   - Transient panels must use **Floating Overlay** mode (`Dock = None`, `Anchor = Top | Bottom | Right`) in WinForms or GPU transforms (`TranslateTransform.X`) in WPF so that sibling controls undergo zero layout passes and remain locked at 60–120 FPS.
   - Use `DrawerControl` / `ZeroDrawer` (available in both `ZeroUI.WinForms.Overlays` and `ZeroUI.Wpf.Overlays`) which implements this standard out-of-the-box.

2. **Time-Based Animation Interpolation:**
   - All interactive sliding or fading motions must consume `deltaSeconds` from `ZeroAnimationClock` (WinForms) or `CubicEase` (WPF).
   - Never compute step deltas using crude distance division (e.g. `(target - current) / 3`) which produces step stuttering across varying screen refresh rates.
   - Recommended duration: `200ms - 250ms` using `EaseOutCubic`:
     $$\text{Progress}(t) = 1 - (1 - t)^3 \quad \text{where } t \in [0, 1]$$

---

## 5. Verification Checklist for Pull Requests

Before committing any UI or control modifications:
- [ ] No hardcoded hex strings (`#181B26`, `#F1F5F9`, etc.) exist in modified files.
- [ ] No static brushes (`Brushes.White`, `Brushes.Black`, etc.) exist for standard UI elements.
- [ ] Control has been visually inspected under both **Dark** and **Light** skins.
- [ ] Dynamic skin change verified at runtime without UI artifacts or retained stale colors.
- [ ] All target frameworks (`net462`, `net8.0-windows`, `netstandard2.0`) compile with **0 Warnings, 0 Errors**.
