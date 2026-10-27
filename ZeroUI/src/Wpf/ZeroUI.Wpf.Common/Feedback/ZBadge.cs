using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using ZeroUI.Core.Theme;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Feedback
{
    public enum BadgeStatus
    {
        None,
        Online,
        Offline,
        Busy,
        Idle,
        Warning,
        Accent
    }

    public enum BadgePlacement
    {
        TopRight,
        TopLeft,
        BottomRight,
        BottomLeft
    }

    /// <summary>
    /// Versatile numeric counter, status indicator pill, and notification badge for ZeroUI WPF.
    /// Supports wrapping host content (adorner mode) or standalone rendering, with optional
    /// hardware-accelerated continuous status pulse animation.
    /// </summary>
    public class ZBadge : ContentControl, IZeroSkinnable
    {
        public static readonly DependencyProperty CountProperty =
            DependencyProperty.Register(nameof(Count), typeof(int?), typeof(ZBadge), new PropertyMetadata(null, OnBadgeVisualChanged));

        public static readonly DependencyProperty MaxCountProperty =
            DependencyProperty.Register(nameof(MaxCount), typeof(int), typeof(ZBadge), new PropertyMetadata(99, OnBadgeVisualChanged));

        public static readonly DependencyProperty BadgeTextProperty =
            DependencyProperty.Register(nameof(BadgeText), typeof(string), typeof(ZBadge), new PropertyMetadata(null, OnBadgeVisualChanged));

        public static readonly DependencyProperty IsDotProperty =
            DependencyProperty.Register(nameof(IsDot), typeof(bool), typeof(ZBadge), new PropertyMetadata(false, OnBadgeVisualChanged));

        public static readonly DependencyProperty StatusProperty =
            DependencyProperty.Register(nameof(Status), typeof(BadgeStatus), typeof(ZBadge), new PropertyMetadata(BadgeStatus.None, OnBadgeVisualChanged));

        public static readonly DependencyProperty IsPulseProperty =
            DependencyProperty.Register(nameof(IsPulse), typeof(bool), typeof(ZBadge), new PropertyMetadata(false, OnPulseChanged));

        public static readonly DependencyProperty PlacementProperty =
            DependencyProperty.Register(nameof(Placement), typeof(BadgePlacement), typeof(ZBadge), new PropertyMetadata(BadgePlacement.TopRight));

        public static readonly DependencyProperty DisplayTextProperty =
            DependencyProperty.Register(nameof(DisplayText), typeof(string), typeof(ZBadge), new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty BadgeBrushProperty =
            DependencyProperty.Register(nameof(BadgeBrush), typeof(Brush), typeof(ZBadge), new PropertyMetadata(null));

        public static readonly DependencyProperty UseDefaultSkinProperty =
            DependencyProperty.Register(nameof(UseDefaultSkin), typeof(bool), typeof(ZBadge), new PropertyMetadata(true));

        public static readonly DependencyProperty CustomSkinProperty =
            DependencyProperty.Register(nameof(CustomSkin), typeof(ZeroSkin), typeof(ZBadge), new PropertyMetadata(null));

        public int? Count
        {
            get => (int?)GetValue(CountProperty);
            set => SetValue(CountProperty, value);
        }

        public int MaxCount
        {
            get => (int)GetValue(MaxCountProperty);
            set => SetValue(MaxCountProperty, value);
        }

        public string? BadgeText
        {
            get => (string?)GetValue(BadgeTextProperty);
            set => SetValue(BadgeTextProperty, value);
        }

        public bool IsDot
        {
            get => (bool)GetValue(IsDotProperty);
            set => SetValue(IsDotProperty, value);
        }

        public BadgeStatus Status
        {
            get => (BadgeStatus)GetValue(StatusProperty);
            set => SetValue(StatusProperty, value);
        }

        public bool IsPulse
        {
            get => (bool)GetValue(IsPulseProperty);
            set => SetValue(IsPulseProperty, value);
        }

        public BadgePlacement Placement
        {
            get => (BadgePlacement)GetValue(PlacementProperty);
            set => SetValue(PlacementProperty, value);
        }

        public string DisplayText
        {
            get => (string)GetValue(DisplayTextProperty);
            private set => SetValue(DisplayTextProperty, value);
        }

        public Brush? BadgeBrush
        {
            get => (Brush?)GetValue(BadgeBrushProperty);
            set => SetValue(BadgeBrushProperty, value);
        }

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

        static ZBadge()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ZBadge), new FrameworkPropertyMetadata(typeof(ZBadge)));
        }

        public ZBadge()
        {
            Loaded += (s, e) =>
            {
                ZeroWpfTheme.ThemeChanged += UpdateThemeColors;
                UpdateThemeColors();
                UpdateBadgeVisuals();
            };
            Unloaded += (s, e) =>
            {
                ZeroWpfTheme.ThemeChanged -= UpdateThemeColors;
            };
        }

        public void ApplySkin(ZeroSkin skin)
        {
            if (skin == null) throw new ArgumentNullException(nameof(skin));
            CustomSkin = skin;
            UseDefaultSkin = false;
            UpdateThemeColors();
        }

        private void UpdateThemeColors()
        {
            Brush brush = Status switch
            {
                BadgeStatus.Online => ZeroWpfTheme.SuccessAccent,
                BadgeStatus.Busy => ZeroWpfTheme.DangerAccent,
                BadgeStatus.Idle => ZeroWpfTheme.WarningAccent,
                BadgeStatus.Warning => ZeroWpfTheme.WarningAccent,
                BadgeStatus.Offline => ZeroWpfTheme.TextMuted,
                BadgeStatus.Accent => ZeroWpfTheme.PrimaryAccent,
                _ => ZeroWpfTheme.PrimaryAccent
            };

            BadgeBrush = brush;
        }

        private void UpdateBadgeVisuals()
        {
            if (IsDot)
            {
                DisplayText = string.Empty;
            }
            else if (!string.IsNullOrEmpty(BadgeText))
            {
                DisplayText = BadgeText!;
            }
            else if (Count.HasValue)
            {
                DisplayText = Count.Value > MaxCount ? $"{MaxCount}+" : Count.Value.ToString();
            }
            else
            {
                DisplayText = string.Empty;
            }

            UpdateThemeColors();
        }

        private static void OnBadgeVisualChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZBadge badge)
            {
                badge.UpdateBadgeVisuals();
            }
        }

        private static void OnPulseChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            // Pulse triggers handle in template
        }
    }

    /// <summary>
    /// Backward-compatibility alias for <see cref="ZBadge"/>.
    /// </summary>
    [Obsolete("Badge is deprecated and will be removed in 5 release cycles. Please migrate to ZBadge instead.")]
    public class Badge : ZBadge
    {
        static Badge()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(Badge), new FrameworkPropertyMetadata(typeof(ZBadge)));
        }
    }

    /// <summary>
    /// Legacy alias for <see cref="ZBadge"/>.
    /// </summary>
    [Obsolete("ZeroBadge is deprecated and will be removed in 5 release cycles. Please migrate to ZBadge instead.")]
    public class ZeroBadge : ZBadge
    {
        static ZeroBadge()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ZeroBadge), new FrameworkPropertyMetadata(typeof(ZBadge)));
        }
    }
}
