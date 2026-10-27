using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using ZeroUI.Core.Rendering;
using ZeroUI.Core.Theme;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Base
{
    /// <summary>
    /// Architectural base class for high-performance direct vector rendering controls (FrameworkElement / OnRender) in ZeroUI WPF.
    /// Manages theme lifecycle invalidation, memory-safe event subscription, localized skin scoping,
    /// automated ZeroAnimationClock subscriptions, and standard DrawingContext visual primitives.
    /// </summary>
    public abstract class WpfVisualBase : FrameworkElement, IZeroSkinnable
    {
        private IDisposable? _animSub;
        private int _isRenderPending = 0;

        public static readonly DependencyProperty UseDefaultSkinProperty =
            DependencyProperty.Register(
                nameof(UseDefaultSkin),
                typeof(bool),
                typeof(WpfVisualBase),
                new PropertyMetadata(true, OnSkinPropertyChanged));

        public static readonly DependencyProperty CustomSkinProperty =
            DependencyProperty.Register(
                nameof(CustomSkin),
                typeof(ZeroSkin),
                typeof(WpfVisualBase),
                new PropertyMetadata(null, OnSkinPropertyChanged));

        public bool UseDefaultSkin
        {
            get => (bool)GetValue(UseDefaultSkinProperty);
            set => SetValue(UseDefaultSkinProperty, value);
        }

        public ZeroSkin? CustomSkin
        {
            get => (ZeroSkin?)GetValue(CustomSkinProperty);
            set => SetValue(CustomSkinProperty, value);
        }

        public ZeroSkin EffectiveSkin => ZeroSkinManager.ResolveSkin(this);

        /// <summary>
        /// Determines whether this visualizer automatically subscribes to ZeroAnimationClock for dynamic 60 FPS repainting.
        /// </summary>
        protected virtual bool AutoAnimate => false;

        protected WpfVisualBase()
        {
            ClipToBounds = true;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            ZeroWpfTheme.ThemeChanged += OnThemeChangedInternal;
            OnThemeChanged();

            if (AutoAnimate && _animSub == null)
            {
                var dispatcher = Dispatcher;
                _animSub = ZeroAnimationClock.Subscribe((delta, frame) =>
                {
                    if (dispatcher.CheckAccess())
                    {
                        if (IsLoaded)
                        {
                            OnAnimationTick(delta, frame);
                            InvalidateVisual();
                        }
                    }
                    else
                    {
                        if (System.Threading.Interlocked.CompareExchange(ref _isRenderPending, 1, 0) == 0)
                        {
                            dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Render, new Action(() =>
                            {
                                try
                                {
                                    if (IsLoaded)
                                    {
                                        OnAnimationTick(delta, frame);
                                        InvalidateVisual();
                                    }
                                }
                                finally
                                {
                                    System.Threading.Interlocked.Exchange(ref _isRenderPending, 0);
                                }
                            }));
                        }
                    }
                });
            }

            InvalidateVisual();
        }

        /// <summary>
        /// Invoked on each animation clock tick when AutoAnimate is true. Override to advance kinematics or simulation state.
        /// </summary>
        protected virtual void OnAnimationTick(double delta, long frame)
        {
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            ZeroWpfTheme.ThemeChanged -= OnThemeChangedInternal;
            _animSub?.Dispose();
            _animSub = null;
            System.Threading.Interlocked.Exchange(ref _isRenderPending, 0);
        }

        private void OnThemeChangedInternal()
        {
            if (UseDefaultSkin)
            {
                OnThemeChanged();
                InvalidateVisual();
            }
        }

        /// <summary>
        /// Applies a localized skin directly to this visualizer and invalidates the surface.
        /// </summary>
        public virtual void ApplySkin(ZeroSkin skin)
        {
            if (skin == null) throw new ArgumentNullException(nameof(skin));
            CustomSkin = skin;
            UseDefaultSkin = false;
            OnThemeChanged();
            InvalidateVisual();
        }

        /// <summary>
        /// Invoked when the active theme is modified globally or locally. Override to update local geometry, brushes, or pens.
        /// </summary>
        protected virtual void OnThemeChanged()
        {
        }

        private static void OnSkinPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is WpfVisualBase visual)
            {
                visual.OnThemeChanged();
                visual.InvalidateVisual();
            }
        }

        #region Cross-Target Text Formatting Helpers

#if NETFRAMEWORK
        protected FormattedText CreateFormattedText(string text, Typeface typeface, double fontSize, Brush brush, double? dpi = null)
        {
            return new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, fontSize, brush);
        }
#else
        protected FormattedText CreateFormattedText(string text, Typeface typeface, double fontSize, Brush brush, double? dpi = null)
        {
            double d = dpi ?? VisualTreeHelper.GetDpi(this).PixelsPerDip;
            return new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, fontSize, brush, d);
        }
#endif

        /// <summary>
        /// Parses a hex color string (#RRGGBB) to a WPF Color.
        /// </summary>
        protected static Color HexToColor(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return Colors.White;
            hex = hex.TrimStart('#');
            if (hex.Length == 6)
            {
                byte r = byte.Parse(hex.Substring(0, 2), NumberStyles.HexNumber);
                byte g = byte.Parse(hex.Substring(2, 2), NumberStyles.HexNumber);
                byte b = byte.Parse(hex.Substring(4, 2), NumberStyles.HexNumber);
                return Color.FromRgb(r, g, b);
            }
            return Colors.White;
        }

        #endregion

        #region Shared Drawing Primitives

        /// <summary>
        /// Draws a container card box with subtle theme-aware background, border, and a header title.
        /// </summary>
        protected void DrawCardBox(DrawingContext dc, Rect bounds, string title, double? dpi = null)
        {
            DrawCardBox(dc, bounds, title, ZeroWpfTheme.BoldTypeface, ZeroWpfTheme.TextSecondary, ZeroWpfTheme.BgCard, ZeroWpfTheme.BorderPen, dpi);
        }

        /// <summary>
        /// Draws a container card box with subtle theme-aware background, border, and a header title with custom styling.
        /// </summary>
        protected void DrawCardBox(DrawingContext dc, Rect bounds, string title, Typeface titleTypeface, Brush textSecondary, Brush bgCard, Pen borderPen, double? dpi = null)
        {
            dc.DrawRoundedRectangle(bgCard, borderPen, bounds, 6, 6);
            if (!string.IsNullOrEmpty(title) && titleTypeface != null)
            {
                var titleText = CreateFormattedText(title, titleTypeface, 9.5, textSecondary, dpi);
                dc.DrawText(titleText, new Point(bounds.X + 14, bounds.Y + 10));
            }
        }

        /// <summary>
        /// Draws a standardized industrial status pill badge (e.g. NORMAL, FAULT, ACTIVE, ENERGIZED).
        /// </summary>
        protected void DrawStatusBadge(DrawingContext dc, Rect bounds, string text, Brush statusBrush, double fontSize = 10, double? dpi = null)
        {
            DrawStatusBadge(dc, bounds, text, statusBrush, ZeroWpfTheme.BoldTypeface, fontSize, dpi);
        }

        /// <summary>
        /// Draws a standardized industrial status pill badge with custom typeface and size.
        /// </summary>
        protected void DrawStatusBadge(DrawingContext dc, Rect bounds, string text, Brush statusBrush, Typeface typeface, double fontSize = 10, double? dpi = null)
        {
            Color baseCol = statusBrush is SolidColorBrush scb ? scb.Color : Color.FromRgb(34, 197, 94);
            Brush pillBg = new SolidColorBrush(Color.FromArgb(25, baseCol.R, baseCol.G, baseCol.B));
            Pen pillBorder = new Pen(statusBrush, 1);
            dc.DrawRoundedRectangle(pillBg, pillBorder, bounds, 4, 4);

            if (!string.IsNullOrEmpty(text) && typeface != null)
            {
                var formatted = CreateFormattedText(text, typeface, fontSize, statusBrush, dpi);
                dc.DrawText(formatted, new Point(bounds.X + (bounds.Width - formatted.Width) / 2, bounds.Y + (bounds.Height - formatted.Height) / 2));
            }
        }

        /// <summary>
        /// Draws a KPI summary cell with a secondary small label and a prominent value.
        /// </summary>
        protected void DrawKpiCell(DrawingContext dc, Rect bounds, string label, string value, Brush valueBrush, double? dpi = null)
        {
            DrawKpiCell(dc, bounds, label, value, valueBrush, ZeroWpfTheme.RegularTypeface, ZeroWpfTheme.BoldTypeface, ZeroWpfTheme.TextSecondary, dpi);
        }

        /// <summary>
        /// Draws a KPI summary cell with a secondary small label and a prominent value with custom typography.
        /// </summary>
        protected void DrawKpiCell(DrawingContext dc, Rect bounds, string label, string value, Brush valueBrush, Typeface labelTypeface, Typeface valueTypeface, Brush textSecondary, double? dpi = null)
        {
            if (!string.IsNullOrEmpty(label) && labelTypeface != null)
            {
                var lText = CreateFormattedText(label, labelTypeface, 9.5, textSecondary, dpi);
                dc.DrawText(lText, new Point(bounds.X + 12, bounds.Y + 6));
            }
            if (!string.IsNullOrEmpty(value) && valueTypeface != null)
            {
                var vText = CreateFormattedText(value, valueTypeface, 12.5, valueBrush, dpi);
                dc.DrawText(vText, new Point(bounds.X + 12, bounds.Y + 22));
            }
        }

        /// <summary>
        /// Draws a two-column key-value data row aligned across a card panel.
        /// </summary>
        protected void DrawDataRow(DrawingContext dc, double left, double right, double y, string label, string value, Brush? labelBrush = null, Brush? valBrush = null, double? dpi = null)
        {
            DrawDataRow(dc, left, right, y, label, value, ZeroWpfTheme.RegularTypeface, ZeroWpfTheme.BoldTypeface, labelBrush ?? ZeroWpfTheme.TextSecondary, valBrush ?? ZeroWpfTheme.TextPrimary, dpi);
        }

        /// <summary>
        /// Draws a two-column key-value data row aligned across a card panel with custom typography.
        /// </summary>
        protected void DrawDataRow(DrawingContext dc, double left, double right, double y, string label, string value, Typeface regTypeface, Typeface boldTypeface, Brush labelBrush, Brush valBrush, double? dpi = null)
        {
            if (!string.IsNullOrEmpty(label) && regTypeface != null)
            {
                var lText = CreateFormattedText(label, regTypeface, 10, labelBrush, dpi);
                dc.DrawText(lText, new Point(left, y));
            }
            if (!string.IsNullOrEmpty(value) && boldTypeface != null)
            {
                var vText = CreateFormattedText(value, boldTypeface, 10.5, valBrush, dpi);
                dc.DrawText(vText, new Point(right - vText.Width, y));
            }
        }

        #endregion
    }

    /// <summary>
    /// Obsolete alias for <see cref="WpfVisualBase"/> to maintain backward compatibility.
    /// </summary>
    [Obsolete("Use WpfVisualBase instead.")]
    public abstract class ZeroWpfVisualBase : WpfVisualBase
    {
    }
}

