using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shell;
using ZeroUI.Wpf.Base;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Layout
{
    /// <summary>
    /// Modern unified title bar for frameless WPF windows integrating WindowChrome.
    /// Provides drag support, double-click maximize toggle, customizable content slot,
    /// and theme-reactive system window buttons (Minimize, Maximize/Restore, Close).
    /// </summary>
    public class ZTitleBar : ZeroWpfControlBase
    {
        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(nameof(Title), typeof(string), typeof(ZTitleBar), new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty IconProperty =
            DependencyProperty.Register(nameof(Icon), typeof(ImageSource), typeof(ZTitleBar), new PropertyMetadata(null));

        public static readonly DependencyProperty TitleContentProperty =
            DependencyProperty.Register(nameof(TitleContent), typeof(object), typeof(ZTitleBar), new PropertyMetadata(null));

        public static readonly DependencyProperty ShowMinimizeButtonProperty =
            DependencyProperty.Register(nameof(ShowMinimizeButton), typeof(bool), typeof(ZTitleBar), new PropertyMetadata(true));

        public static readonly DependencyProperty ShowMaximizeButtonProperty =
            DependencyProperty.Register(nameof(ShowMaximizeButton), typeof(bool), typeof(ZTitleBar), new PropertyMetadata(true));

        public static readonly DependencyProperty ShowCloseButtonProperty =
            DependencyProperty.Register(nameof(ShowCloseButton), typeof(bool), typeof(ZTitleBar), new PropertyMetadata(true));

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public ImageSource? Icon
        {
            get => (ImageSource?)GetValue(IconProperty);
            set => SetValue(IconProperty, value);
        }

        public object? TitleContent
        {
            get => GetValue(TitleContentProperty);
            set => SetValue(TitleContentProperty, value);
        }

        public bool ShowMinimizeButton
        {
            get => (bool)GetValue(ShowMinimizeButtonProperty);
            set => SetValue(ShowMinimizeButtonProperty, value);
        }

        public bool ShowMaximizeButton
        {
            get => (bool)GetValue(ShowMaximizeButtonProperty);
            set => SetValue(ShowMaximizeButtonProperty, value);
        }

        public bool ShowCloseButton
        {
            get => (bool)GetValue(ShowCloseButtonProperty);
            set => SetValue(ShowCloseButtonProperty, value);
        }

        static ZTitleBar()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ZTitleBar), new FrameworkPropertyMetadata(typeof(ZTitleBar)));
        }

        public ZTitleBar()
        {
            Height = 36;
            SetResourceReference(BackgroundProperty, "ZeroUI.BgPrimary");
            SetResourceReference(ForegroundProperty, "ZeroUI.TextPrimary");
            SetResourceReference(BorderBrushProperty, "ZeroUI.BorderDefault");
            BorderThickness = new Thickness(0, 0, 0, 1);
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            if (GetTemplateChild("PART_MinBtn") is Button minBtn)
            {
                WindowChrome.SetIsHitTestVisibleInChrome(minBtn, true);
                minBtn.Click += (s, e) =>
                {
                    var win = Window.GetWindow(this);
                    if (win != null) win.WindowState = WindowState.Minimized;
                };
            }

            if (GetTemplateChild("PART_MaxBtn") is Button maxBtn)
            {
                WindowChrome.SetIsHitTestVisibleInChrome(maxBtn, true);
                maxBtn.Click += (s, e) => ToggleMaximize();
            }

            if (GetTemplateChild("PART_CloseBtn") is Button closeBtn)
            {
                WindowChrome.SetIsHitTestVisibleInChrome(closeBtn, true);
                closeBtn.Click += (s, e) =>
                {
                    var win = Window.GetWindow(this);
                    win?.Close();
                };
            }

            if (GetTemplateChild("PART_ContentHost") is ContentPresenter contentHost)
            {
                WindowChrome.SetIsHitTestVisibleInChrome(contentHost, true);
            }

            var parentWin = Window.GetWindow(this);
            if (parentWin != null)
            {
                parentWin.StateChanged += OnWindowStateChanged;
                UpdateMaximizeIcon(parentWin.WindowState);
            }
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);

            if (e.ClickCount == 2 && ShowMaximizeButton)
            {
                ToggleMaximize();
                e.Handled = true;
                return;
            }

            var win = Window.GetWindow(this);
            if (win != null && e.ButtonState == MouseButtonState.Pressed)
            {
                win.DragMove();
            }
        }

        private void ToggleMaximize()
        {
            var win = Window.GetWindow(this);
            if (win == null) return;

            win.WindowState = win.WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        private void OnWindowStateChanged(object? sender, EventArgs e)
        {
            if (sender is Window win)
            {
                UpdateMaximizeIcon(win.WindowState);
            }
        }

        private void UpdateMaximizeIcon(WindowState state)
        {
            if (GetTemplateChild("PART_MaxGlyph") is TextBlock glyph)
            {
                glyph.Text = state == WindowState.Maximized ? "❐" : "▢";
            }
        }
    }

    /// <summary>
    /// Backward-compatibility alias for <see cref="ZTitleBar"/>.
    /// </summary>
    [Obsolete("TitleBar is deprecated and will be removed in 5 release cycles. Please migrate to ZTitleBar instead.")]
    public class TitleBar : ZTitleBar
    {
        static TitleBar()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(TitleBar), new FrameworkPropertyMetadata(typeof(ZTitleBar)));
        }
    }

    /// <summary>
    /// Legacy alias for <see cref="ZTitleBar"/>.
    /// </summary>
    [Obsolete("ZeroTitleBar is deprecated and will be removed in 5 release cycles. Please migrate to ZTitleBar instead.")]
    public class ZeroTitleBar : ZTitleBar
    {
        static ZeroTitleBar()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ZeroTitleBar), new FrameworkPropertyMetadata(typeof(ZTitleBar)));
        }
    }
}
