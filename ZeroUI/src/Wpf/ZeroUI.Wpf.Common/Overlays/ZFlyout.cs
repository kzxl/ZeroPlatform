using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Effects;
using ZeroUI.Core.Theme;
using ZeroUI.Wpf.Base;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Overlays
{
    /// <summary>
    /// Modern lightweight anchored flyout / popover matching WinUI 3 standards.
    /// Provides auto-dismiss on outside click, header/content/footer slots,
    /// drop shadow, and dynamic theme synchronization.
    /// </summary>
    [ContentProperty(nameof(FlyoutContent))]
    public class ZFlyout : Control, IZeroSkinnable
    {
        private Popup? _popup;

        public static readonly DependencyProperty IsOpenProperty =
            DependencyProperty.Register(
                nameof(IsOpen),
                typeof(bool),
                typeof(ZFlyout),
                new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnIsOpenChanged));

        public static readonly DependencyProperty StaysOpenProperty =
            DependencyProperty.Register(nameof(StaysOpen), typeof(bool), typeof(ZFlyout), new PropertyMetadata(false));

        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(nameof(Title), typeof(string), typeof(ZFlyout), new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty TargetProperty =
            DependencyProperty.Register(nameof(Target), typeof(UIElement), typeof(ZFlyout), new PropertyMetadata(null));

        public static readonly DependencyProperty PlacementProperty =
            DependencyProperty.Register(nameof(Placement), typeof(PlacementMode), typeof(ZFlyout), new PropertyMetadata(PlacementMode.Bottom));

        public static readonly DependencyProperty FlyoutContentProperty =
            DependencyProperty.Register(nameof(FlyoutContent), typeof(object), typeof(ZFlyout), new PropertyMetadata(null));

        public static readonly DependencyProperty FooterProperty =
            DependencyProperty.Register(nameof(Footer), typeof(object), typeof(ZFlyout), new PropertyMetadata(null));

        public static readonly DependencyProperty ShowCloseButtonProperty =
            DependencyProperty.Register(nameof(ShowCloseButton), typeof(bool), typeof(ZFlyout), new PropertyMetadata(true));

        public static readonly DependencyProperty UseDefaultSkinProperty =
            DependencyProperty.Register(nameof(UseDefaultSkin), typeof(bool), typeof(ZFlyout), new PropertyMetadata(true));

        public static readonly DependencyProperty CustomSkinProperty =
            DependencyProperty.Register(nameof(CustomSkin), typeof(ZeroSkin), typeof(ZFlyout), new PropertyMetadata(null));

        public bool IsOpen
        {
            get => (bool)GetValue(IsOpenProperty);
            set => SetValue(IsOpenProperty, value);
        }

        public bool StaysOpen
        {
            get => (bool)GetValue(StaysOpenProperty);
            set => SetValue(StaysOpenProperty, value);
        }

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public UIElement? Target
        {
            get => (UIElement?)GetValue(TargetProperty);
            set => SetValue(TargetProperty, value);
        }

        public PlacementMode Placement
        {
            get => (PlacementMode)GetValue(PlacementProperty);
            set => SetValue(PlacementProperty, value);
        }

        public object? FlyoutContent
        {
            get => GetValue(FlyoutContentProperty);
            set => SetValue(FlyoutContentProperty, value);
        }

        public object? Footer
        {
            get => GetValue(FooterProperty);
            set => SetValue(FooterProperty, value);
        }

        public bool ShowCloseButton
        {
            get => (bool)GetValue(ShowCloseButtonProperty);
            set => SetValue(ShowCloseButtonProperty, value);
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

        static ZFlyout()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ZFlyout), new FrameworkPropertyMetadata(typeof(ZFlyout)));
        }

        public ZFlyout()
        {
            SetResourceReference(BackgroundProperty, "ZeroUI.BgCard");
            SetResourceReference(ForegroundProperty, "ZeroUI.TextPrimary");
            SetResourceReference(BorderBrushProperty, "ZeroUI.BorderDefault");
            BorderThickness = new Thickness(1);
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            _popup = GetTemplateChild("PART_Popup") as Popup;
            if (_popup != null)
            {
                _popup.Closed += (s, e) => IsOpen = false;
            }

            if (GetTemplateChild("PART_CloseButton") is Button closeBtn)
            {
                closeBtn.Click += (s, e) => IsOpen = false;
            }
        }

        public void ApplySkin(ZeroSkin skin)
        {
            if (skin == null) throw new ArgumentNullException(nameof(skin));
            CustomSkin = skin;
            UseDefaultSkin = false;
        }

        public void Show(UIElement? target = null)
        {
            if (target != null) Target = target;
            IsOpen = true;
        }

        public void Hide()
        {
            IsOpen = false;
        }

        private static void OnIsOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZFlyout flyout && flyout._popup != null)
            {
                flyout._popup.IsOpen = (bool)e.NewValue;
            }
        }
    }

    /// <summary>
    /// Backward-compatibility alias for <see cref="ZFlyout"/>.
    /// </summary>
    [Obsolete("FlyoutControl is deprecated and will be removed in 5 release cycles. Please migrate to ZFlyout instead.")]
    public class FlyoutControl : ZFlyout
    {
        static FlyoutControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(FlyoutControl), new FrameworkPropertyMetadata(typeof(ZFlyout)));
        }
    }

    /// <summary>
    /// Legacy alias for <see cref="ZFlyout"/>.
    /// </summary>
    [Obsolete("ZeroFlyout is deprecated and will be removed in 5 release cycles. Please migrate to ZFlyout instead.")]
    public class ZeroFlyout : ZFlyout
    {
        static ZeroFlyout()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ZeroFlyout), new FrameworkPropertyMetadata(typeof(ZFlyout)));
        }
    }
}
