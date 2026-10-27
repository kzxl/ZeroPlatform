using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using ZeroUI.Core.Input;
using ZeroUI.Core.Theme;
using ZeroUI.Wpf.Base;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Input
{
    /// <summary>
    /// WPF touch-screen virtual keyboard supporting QWERTY and Numpad modes
    /// with large touch-friendly keys for industrial panel PCs.
    /// Fully synchronized with ZeroWpfTheme tokens and dynamic theme switching.
    /// </summary>
    public partial class ZVirtualKeyboard : WpfControlBase
    {
        public static readonly DependencyProperty LayoutModeProperty =
            DependencyProperty.Register(
                nameof(LayoutMode),
                typeof(VirtualKeyboardLayout),
                typeof(ZVirtualKeyboard),
                new PropertyMetadata(VirtualKeyboardLayout.AlphaNumeric, OnLayoutChanged));

        public static readonly DependencyProperty TargetElementProperty =
            DependencyProperty.Register(
                nameof(TargetElement),
                typeof(UIElement),
                typeof(ZVirtualKeyboard),
                new PropertyMetadata(null));

        public static readonly DependencyProperty KeyHeightProperty =
            DependencyProperty.Register(
                nameof(KeyHeight),
                typeof(double),
                typeof(ZVirtualKeyboard),
                new PropertyMetadata(46.0, OnLayoutChanged));

        public static readonly DependencyProperty KeySpacingProperty =
            DependencyProperty.Register(
                nameof(KeySpacing),
                typeof(double),
                typeof(ZVirtualKeyboard),
                new PropertyMetadata(4.0, OnLayoutChanged));

        public VirtualKeyboardLayout LayoutMode
        {
            get => (VirtualKeyboardLayout)GetValue(LayoutModeProperty);
            set => SetValue(LayoutModeProperty, value);
        }

        public UIElement? TargetElement
        {
            get => (UIElement?)GetValue(TargetElementProperty);
            set => SetValue(TargetElementProperty, value);
        }

        public double KeyHeight
        {
            get => (double)GetValue(KeyHeightProperty);
            set => SetValue(KeyHeightProperty, value);
        }

        public double KeySpacing
        {
            get => (double)GetValue(KeySpacingProperty);
            set => SetValue(KeySpacingProperty, value);
        }

        public event EventHandler<VirtualKeyEventArgs>? KeyPressed;
        public event EventHandler? EnterPressed;
        public event EventHandler? EscapePressed;

        private bool _shiftActive;
        private bool _capsActive;
        private readonly Grid _mainGrid = new Grid();

        static ZVirtualKeyboard()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(ZVirtualKeyboard),
                new FrameworkPropertyMetadata(typeof(ZVirtualKeyboard)));
        }

        public ZVirtualKeyboard()
        {
            HorizontalAlignment = HorizontalAlignment.Stretch;
            VerticalAlignment = VerticalAlignment.Stretch;
            Width = double.NaN;
            Height = double.NaN;
            Background = ZeroWpfTheme.BgCard ?? Brushes.Transparent;

            AddVisualChild(_mainGrid);
            AddLogicalChild(_mainGrid);
            RebuildLayout();

            Loaded += (s, e) =>
            {
                ZeroWpfTheme.ThemeChanged += ApplyTheme;
                ApplyTheme();
            };
            Unloaded += (s, e) =>
            {
                ZeroWpfTheme.ThemeChanged -= ApplyTheme;
            };
        }

        protected override int VisualChildrenCount => 1;
        protected override Visual GetVisualChild(int index) => _mainGrid;

        protected override Size MeasureOverride(Size constraint)
        {
            _mainGrid.Measure(constraint);
            return _mainGrid.DesiredSize;
        }

        protected override Size ArrangeOverride(Size arrangeBounds)
        {
            _mainGrid.Arrange(new Rect(arrangeBounds));
            return arrangeBounds;
        }

        private static void OnLayoutChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZVirtualKeyboard kb)
            {
                kb.RebuildLayout();
            }
        }
    }
}
