using System;
using System.Windows;
using System.Windows.Controls;
using ZeroUI.Core.Theme;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Base
{
    /// <summary>
    /// Architectural base class for templated and compound controls in ZeroUI WPF.
    /// Provides dynamic theme resource linkage, memory-safe theme lifecycle dispatch,
    /// and local theme override capabilities.
    /// </summary>
    public abstract class WpfControlBase : Control, IZeroSkinnable
    {
        public static readonly DependencyProperty UseDefaultSkinProperty =
            DependencyProperty.Register(
                nameof(UseDefaultSkin),
                typeof(bool),
                typeof(WpfControlBase),
                new PropertyMetadata(true, OnSkinPropertyChanged));

        public static readonly DependencyProperty CustomSkinProperty =
            DependencyProperty.Register(
                nameof(CustomSkin),
                typeof(ZeroSkin),
                typeof(WpfControlBase),
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

        protected WpfControlBase()
        {
            SetResourceReference(BackgroundProperty, "ZeroUI.BgCard");
            SetResourceReference(ForegroundProperty, "ZeroUI.TextPrimary");
            SetResourceReference(BorderBrushProperty, "ZeroUI.BorderDefault");

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

        /// <summary>
        /// Applies a localized skin directly to this control and unlinks from the global skin.
        /// </summary>
        public virtual void ApplySkin(ZeroSkin skin)
        {
            if (skin == null) throw new ArgumentNullException(nameof(skin));
            CustomSkin = skin;
            UseDefaultSkin = false;
            OnThemeChanged();
        }

        /// <summary>
        /// Invoked when the global or localized theme changes. Override in derived controls to update internal elements.
        /// </summary>
        protected virtual void OnThemeChanged()
        {
        }

        private static void OnSkinPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is WpfControlBase ctrl)
            {
                ctrl.OnThemeChanged();
            }
        }
    }

    /// <summary>
    /// Obsolete alias for <see cref="WpfControlBase"/> to maintain backward compatibility.
    /// </summary>
    [Obsolete("Use WpfControlBase instead.")]
    public abstract class ZeroWpfControlBase : WpfControlBase
    {
    }
}
