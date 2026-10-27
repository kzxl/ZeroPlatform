using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Overlays
{
    /// <summary>
    /// Modern enterprise modal dialog for ZeroUI WPF.
    /// Provides semi-transparent backdrop dimming, centered rounded card,
    /// and built-in dialog helpers (Information, Warning, Error, Confirm).
    /// </summary>
    public class ZModalDialog : Window
    {
        public bool? DialogResultValue { get; private set; }

        public ZModalDialog(
            Window owner,
            string title,
            UIElement contentElement,
            string okText = "OK",
            string cancelText = "Cancel",
            bool showCancel = true,
            double cardWidth = 500,
            double cardHeight = 320)
        {
            Owner = owner;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = new SolidColorBrush(Color.FromArgb(140, 0, 0, 0)); // Dimmed backdrop
            ShowInTaskbar = false;

            if (owner != null && owner.IsLoaded)
            {
                Left = owner.Left;
                Top = owner.Top;
                Width = owner.ActualWidth;
                Height = owner.ActualHeight;
            }
            else
            {
                WindowState = WindowState.Maximized;
            }

            // Centered Modal Card
            var cardBorder = new Border
            {
                Width = cardWidth,
                MinHeight = cardHeight,
                Background = ZeroWpfTheme.BgCard,
                BorderBrush = ZeroWpfTheme.BorderDefault,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    BlurRadius = 24,
                    Opacity = 0.4,
                    ShadowDepth = 6
                }
            };

            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(48, GridUnitType.Pixel) }); // Header
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });   // Body
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(56, GridUnitType.Pixel) }); // Footer

            // 1. Header
            var headerBorder = new Border
            {
                Background = ZeroWpfTheme.BgInput,
                BorderBrush = ZeroWpfTheme.BorderDefault,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(16, 0, 12, 0),
                CornerRadius = new CornerRadius(8, 8, 0, 0)
            };
            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32, GridUnitType.Pixel) });

            var titleBlock = new TextBlock
            {
                Text = title,
                FontSize = 14.0,
                FontWeight = FontWeights.Bold,
                Foreground = ZeroWpfTheme.TextPrimary,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(titleBlock, 0);
            headerGrid.Children.Add(titleBlock);

            var closeBtn = new Button
            {
                Content = "✕",
                Width = 28,
                Height = 28,
                Foreground = ZeroWpfTheme.TextSecondary,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center
            };
            closeBtn.Click += (s, e) => { DialogResultValue = false; Close(); };
            Grid.SetColumn(closeBtn, 1);
            headerGrid.Children.Add(closeBtn);

            headerBorder.Child = headerGrid;
            Grid.SetRow(headerBorder, 0);
            mainGrid.Children.Add(headerBorder);

            // 2. Body
            var bodyPresenter = new ContentPresenter
            {
                Content = contentElement,
                Margin = new Thickness(16)
            };
            Grid.SetRow(bodyPresenter, 1);
            mainGrid.Children.Add(bodyPresenter);

            // 3. Footer
            var footerBorder = new Border
            {
                Background = ZeroWpfTheme.BgInput,
                BorderBrush = ZeroWpfTheme.BorderDefault,
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(16, 0, 16, 0),
                CornerRadius = new CornerRadius(0, 0, 8, 8)
            };
            var buttonsStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };

            if (showCancel)
            {
                var btnCancel = new Button
                {
                    Content = cancelText,
                    Width = 84,
                    Height = 32,
                    Margin = new Thickness(0, 0, 10, 0)
                };
                btnCancel.Click += (s, e) => { DialogResultValue = false; Close(); };
                buttonsStack.Children.Add(btnCancel);
            }

            var btnOk = new Button
            {
                Content = okText,
                Width = 84,
                Height = 32,
                Background = ZeroWpfTheme.PrimaryAccent,
                Foreground = Brushes.White
            };
            btnOk.Click += (s, e) => { DialogResultValue = true; Close(); };
            buttonsStack.Children.Add(btnOk);

            footerBorder.Child = buttonsStack;
            Grid.SetRow(footerBorder, 2);
            mainGrid.Children.Add(footerBorder);

            cardBorder.Child = mainGrid;
            Content = cardBorder;

            Loaded += (s, e) =>
            {
                var anim = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(150));
                BeginAnimation(OpacityProperty, anim);
            };
        }

        public static bool Confirm(Window owner, string title, string message, string okText = "Yes", string cancelText = "No")
        {
            var textBlock = new TextBlock
            {
                Text = message,
                FontSize = 13.0,
                Foreground = ZeroWpfTheme.TextPrimary,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center
            };
            var modal = new ZModalDialog(owner, title, textBlock, okText, cancelText, true, 420, 200);
            modal.ShowDialog();
            return modal.DialogResultValue == true;
        }

        public static void Information(Window owner, string title, string message)
        {
            var textBlock = new TextBlock
            {
                Text = message,
                FontSize = 13.0,
                Foreground = ZeroWpfTheme.TextPrimary,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center
            };
            var modal = new ZModalDialog(owner, title, textBlock, "OK", "", false, 400, 190);
            modal.ShowDialog();
        }

        public static void Warning(Window owner, string title, string message)
        {
            var textBlock = new TextBlock
            {
                Text = $"⚠  {message}",
                FontSize = 13.0,
                Foreground = ZeroWpfTheme.WarningAccent,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center
            };
            var modal = new ZModalDialog(owner, title, textBlock, "OK", "", false, 420, 190);
            modal.ShowDialog();
        }

        public static void Error(Window owner, string title, string message)
        {
            var textBlock = new TextBlock
            {
                Text = $"✕  {message}",
                FontSize = 13.0,
                Foreground = ZeroWpfTheme.DangerAccent,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center
            };
            var modal = new ZModalDialog(owner, title, textBlock, "OK", "", false, 420, 190);
            modal.ShowDialog();
        }
    }

    /// <summary>
    /// Backward-compatibility alias for <see cref="ZModalDialog"/>.
    /// </summary>
    [Obsolete("ModalDialog is deprecated and will be removed in 5 release cycles. Please migrate to ZModalDialog instead.")]
    public class ModalDialog : ZModalDialog
    {
        public ModalDialog(
            Window owner,
            string title,
            UIElement contentElement,
            string okText = "OK",
            string cancelText = "Cancel",
            bool showCancel = true,
            double cardWidth = 500,
            double cardHeight = 320)
            : base(owner, title, contentElement, okText, cancelText, showCancel, cardWidth, cardHeight)
        {
        }
    }

    /// <summary>
    /// Legacy alias for <see cref="ZModalDialog"/>.
    /// </summary>
    [Obsolete("ZeroModal is deprecated and will be removed in 5 release cycles. Please migrate to ZModalDialog instead.")]
    public class ZeroModal : ZModalDialog
    {
        public ZeroModal(
            Window owner,
            string title,
            UIElement contentElement,
            string okText = "OK",
            string cancelText = "Cancel",
            bool showCancel = true,
            double cardWidth = 500,
            double cardHeight = 320)
            : base(owner, title, contentElement, okText, cancelText, showCancel, cardWidth, cardHeight)
        {
        }
    }
}
