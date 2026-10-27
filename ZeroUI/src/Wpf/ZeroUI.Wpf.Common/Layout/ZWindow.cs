using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shell;
using ZeroUI.Core.Theme;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Layout
{
    /// <summary>
    /// Modern frameless enterprise window utilizing WindowChrome with customizable title bar,
    /// dynamic dark/light skin binding, and zero-distortion client area.
    /// </summary>
    public class ZWindow : Window, IZeroSkinnable
    {
        public static readonly DependencyProperty UseDefaultSkinProperty =
            DependencyProperty.Register(
                nameof(UseDefaultSkin),
                typeof(bool),
                typeof(ZWindow),
                new PropertyMetadata(true, OnSkinPropertyChanged));

        public static readonly DependencyProperty CustomSkinProperty =
            DependencyProperty.Register(
                nameof(CustomSkin),
                typeof(ZeroSkin),
                typeof(ZWindow),
                new PropertyMetadata(null, OnSkinPropertyChanged));

        public static readonly DependencyProperty TitleBarProperty =
            DependencyProperty.Register(
                nameof(TitleBar),
                typeof(ZTitleBar),
                typeof(ZWindow),
                new PropertyMetadata(null));

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

        public ZTitleBar? TitleBar
        {
            get => (ZTitleBar?)GetValue(TitleBarProperty);
            set => SetValue(TitleBarProperty, value);
        }

        static ZWindow()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ZWindow), new FrameworkPropertyMetadata(typeof(ZWindow)));
        }

        public ZWindow()
        {
            SetResourceReference(BackgroundProperty, "ZeroUI.BgPrimary");
            SetResourceReference(ForegroundProperty, "ZeroUI.TextPrimary");
            SetResourceReference(BorderBrushProperty, "ZeroUI.BorderDefault");
            BorderThickness = new Thickness(1);
            WindowStyle = WindowStyle.None;

            var chrome = new WindowChrome
            {
                CaptionHeight = 36,
                CornerRadius = new CornerRadius(0),
                GlassFrameThickness = new Thickness(0),
                ResizeBorderThickness = new Thickness(6),
                UseAeroCaptionButtons = false
            };
            WindowChrome.SetWindowChrome(this, chrome);

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            ZeroWpfTheme.ThemeChanged += OnThemeChangedInternal;
            OnThemeChanged();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            ZeroWpfTheme.ThemeChanged -= OnThemeChangedInternal;
        }

        private void OnThemeChangedInternal()
        {
            if (UseDefaultSkin)
            {
                OnThemeChanged();
            }
        }

        public virtual void ApplySkin(ZeroSkin skin)
        {
            if (skin == null) throw new ArgumentNullException(nameof(skin));
            CustomSkin = skin;
            UseDefaultSkin = false;
            OnThemeChanged();
        }

        protected virtual void OnThemeChanged()
        {
            // Dynamic resources update automatically
        }

        private static void OnSkinPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZWindow win)
            {
                win.OnThemeChanged();
            }
        }
    }

    /// <summary>
    /// Backward-compatibility alias for <see cref="ZWindow"/>.
    /// </summary>
    [Obsolete("ChromeWindow is deprecated and will be removed in 5 release cycles. Please migrate to ZWindow instead.")]
    public class ChromeWindow : ZWindow
    {
        static ChromeWindow()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ChromeWindow), new FrameworkPropertyMetadata(typeof(ZWindow)));
        }
    }

    /// <summary>
    /// Legacy alias for <see cref="ZWindow"/>.
    /// </summary>
    [Obsolete("ZeroWindow is deprecated and will be removed in 5 release cycles. Please migrate to ZWindow instead.")]
    public class ZeroWindow : ZWindow
    {
        static ZeroWindow()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ZeroWindow), new FrameworkPropertyMetadata(typeof(ZWindow)));
        }
    }
}
