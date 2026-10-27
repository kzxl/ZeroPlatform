using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Industrial
{
    /// <summary>
    /// Modern Fluent Design Card container with header, subtitle, and elevation styling.
    /// </summary>
    public class ZGridCard : ContentControl
    {
        public static readonly DependencyProperty HeaderTextProperty =
            DependencyProperty.Register(nameof(HeaderText), typeof(string), typeof(ZGridCard), new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty SubtitleTextProperty =
            DependencyProperty.Register(nameof(SubtitleText), typeof(string), typeof(ZGridCard), new PropertyMetadata(string.Empty));

        public string HeaderText
        {
            get => (string)GetValue(HeaderTextProperty);
            set => SetValue(HeaderTextProperty, value);
        }

        public string SubtitleText
        {
            get => (string)GetValue(SubtitleTextProperty);
            set => SetValue(SubtitleTextProperty, value);
        }

        static ZGridCard()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ZGridCard), new FrameworkPropertyMetadata(typeof(ZGridCard)));
        }

        public ZGridCard()
        {
            SetResourceReference(BackgroundProperty, "ZeroUI.BgCard");
            SetResourceReference(BorderBrushProperty, "ZeroUI.BorderDefault");
            BorderThickness = new Thickness(1);
            Padding = new Thickness(16);
        }
    }

    /// <summary>
    /// Backward-compatibility alias for <see cref="GridCard"/>.
    /// </summary>
    [Obsolete("ZeroCard is deprecated. Use GridCard instead.")]
    public class ZeroCard : GridCard
    {
        static ZeroCard()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ZeroCard), new FrameworkPropertyMetadata(typeof(ZGridCard)));
        }
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZGridCard"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("GridCard is deprecated and will be removed in 5 release cycles. Please migrate to ZGridCard instead.")]
    public class GridCard : ZGridCard { }

    /// <summary>
    /// Legacy alias for <see cref="ZGridCard"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroGridCard is deprecated and will be removed in 5 release cycles. Please migrate to ZGridCard instead.")]
    public class ZeroGridCard : ZGridCard { }

    #endregion

}
