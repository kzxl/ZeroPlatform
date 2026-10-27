// <auto-refactored> Partial class file - BuildUI </auto-refactored>
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using ZeroUI.Core.Input;
using ZeroUI.Core.Security;
using ZeroUI.Core.Theme;
using ZeroUI.Wpf.Input;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Overlays
{
    public partial class ZLoginDialog
    {
        private void BuildUI()
        {
            _rootBorder = new Border
            {
                BorderBrush = ZeroWpfTheme.BorderDefault,
                BorderThickness = new Thickness(1.5),
                Padding = new Thickness(24)
            };

            var stack = new StackPanel();

            // Header (DockPanel: Title stack on Left + Close button docked Right)
            var headerPanel = new DockPanel
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 0, 0, 16),
                LastChildFill = true
            };

            var titleStack = new StackPanel();
            _lblTitle = new TextBlock
            {
                Text = "🔐 ZeroUI Operator Sign-In",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = ZeroWpfTheme.TextPrimary,
                Margin = new Thickness(0, 0, 0, 4)
            };

            _lblSubtitle = new TextBlock
            {
                Text = "Role-Based Industrial Access Control",
                FontSize = 12,
                Foreground = ZeroWpfTheme.TextSecondary
            };
            titleStack.Children.Add(_lblTitle);
            titleStack.Children.Add(_lblSubtitle);

            _btnClose = new Button
            {
                Content = "✕",
                Width = 32,
                Height = 32,
                Cursor = Cursors.Hand,
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                Foreground = ZeroWpfTheme.TextSecondary,
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Right,
                ToolTip = "Close",
                Margin = new Thickness(8, -4, -4, 0),
                IsCancel = true,
                Focusable = false
            };

            var closeTemplate = new ControlTemplate(typeof(Button));
            var borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.Name = "bd";
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
            borderFactory.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding(nameof(Button.Background)) { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });

            var textFactory = new FrameworkElementFactory(typeof(TextBlock));
            textFactory.SetValue(TextBlock.TextProperty, "✕");
            textFactory.SetValue(TextBlock.FontFamilyProperty, new FontFamily("Segoe UI, Arial, sans-serif"));
            textFactory.SetValue(TextBlock.FontSizeProperty, 14.0);
            textFactory.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
            textFactory.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            textFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            textFactory.SetBinding(TextBlock.ForegroundProperty, new System.Windows.Data.Binding(nameof(Button.Foreground)) { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });

            borderFactory.AppendChild(textFactory);
            closeTemplate.VisualTree = borderFactory;
            _btnClose.Template = closeTemplate;

            _btnClose.Click += (s, e) =>
            {
                try { DialogResult = false; } catch (InvalidOperationException) { }
                Close();
            };

            _btnClose.MouseEnter += (s, e) =>
            {
                _btnClose.Background = ZeroWpfTheme.BgHover;
                _btnClose.Foreground = ZeroWpfTheme.TextPrimary;
            };

            _btnClose.MouseLeave += (s, e) =>
            {
                _btnClose.Background = Brushes.Transparent;
                _btnClose.Foreground = ZeroWpfTheme.TextSecondary;
            };

            DockPanel.SetDock(_btnClose, Dock.Right);
            headerPanel.Children.Add(_btnClose);
            headerPanel.Children.Add(titleStack);

            // Tabs
            var tabsGrid = new UniformGrid { Rows = 1, Columns = 3, Margin = new Thickness(0, 0, 0, 16) };
            _tabPass = CreateTabButton("Password", true, () => SwitchTab(0));
            _tabPin = CreateTabButton("PIN Code", false, () => SwitchTab(1));
            _tabBadge = CreateTabButton("RFID Badge", false, () => SwitchTab(2));
            tabsGrid.Children.Add(_tabPass);
            tabsGrid.Children.Add(_tabPin);
            tabsGrid.Children.Add(_tabBadge);

            // Container for panels
            var contentContainer = new Grid { Height = 300 };

            // Panel 1: Password
            _panelPassword = new StackPanel();
            _lblUser = _lblUsername = new TextBlock
            {
                Text = "Operator ID / Username",
                Foreground = ZeroWpfTheme.TextSecondary,
                Margin = new Thickness(0, 8, 0, 4)
            };
            _panelPassword.Children.Add(_lblUsername);

            _txtUsername = new TextBox
            {
                Text = "admin",
                Height = 36,
                FontSize = 14,
                Background = ZeroWpfTheme.BgInput,
                Foreground = ZeroWpfTheme.TextPrimary,
                BorderBrush = ZeroWpfTheme.BorderDefault,
                CaretBrush = ZeroWpfTheme.TextPrimary,
                VerticalContentAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 12)
            };
            _txtUsername.GotFocus += (s, e) => ShowInputKeyboard(_txtUsername);
            _panelPassword.Children.Add(_txtUsername);

            _lblPass = _lblPassword = new TextBlock
            {
                Text = "Password",
                Foreground = ZeroWpfTheme.TextSecondary,
                Margin = new Thickness(0, 0, 0, 4)
            };
            _panelPassword.Children.Add(_lblPassword);

            _txtPassword = new PasswordBox
            {
                Password = "admin",
                Height = 36,
                FontSize = 16,
                Background = ZeroWpfTheme.BgInput,
                Foreground = ZeroWpfTheme.TextPrimary,
                BorderBrush = ZeroWpfTheme.BorderDefault,
                CaretBrush = ZeroWpfTheme.TextPrimary,
                VerticalContentAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 20)
            };
            _txtPassword.GotFocus += (s, e) => ShowInputKeyboard(_txtPassword);
            _txtPassword.KeyDown += (s, e) => { if (e.Key == Key.Enter) _ = ExecuteLoginAsync(0); };
            _panelPassword.Children.Add(_txtPassword);

            _btnSignIn = _btnSubmit = new Button
            {
                Content = "Sign In",
                Height = 42,
                Background = ZeroWpfTheme.PrimaryAccent,
                Foreground = GetAccentTextBrush(),
                FontWeight = FontWeights.Bold,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 0, 8)
            };
            _btnSignIn.Click += (s, e) => _ = ExecuteLoginAsync(0);
            _panelPassword.Children.Add(_btnSignIn);

            _btnCancel = new Button
            {
                Content = "Cancel",
                Height = 36,
                Background = ZeroWpfTheme.BgInput,
                Foreground = ZeroWpfTheme.TextPrimary,
                BorderBrush = ZeroWpfTheme.BorderDefault,
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand
            };
            _btnCancel.Click += (s, e) =>
            {
                try { DialogResult = false; } catch (InvalidOperationException) { }
                Close();
            };
            _panelPassword.Children.Add(_btnCancel);

            // Panel 2: PIN
            _panelPin = new StackPanel { Visibility = Visibility.Collapsed };
            _txtPin = new PasswordBox
            {
                Height = 38,
                FontSize = 20,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                Background = ZeroWpfTheme.BgInput,
                Foreground = ZeroWpfTheme.TextPrimary,
                BorderBrush = ZeroWpfTheme.BorderDefault,
                CaretBrush = ZeroWpfTheme.TextPrimary,
                Margin = new Thickness(0, 0, 0, 8)
            };
            _numpad = new ZVirtualKeyboard
            {
                LayoutMode = VirtualKeyboardLayout.Numpad,
                TargetElement = _txtPin,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Width = double.NaN,
                Height = 236
            };
            _numpad.EnterPressed += (s, e) => _ = ExecuteLoginAsync(1);
            _panelPin.Children.Add(_txtPin);
            _panelPin.Children.Add(_numpad);

            // Panel 3: Badge
            _panelBadge = new StackPanel { Visibility = Visibility.Collapsed, VerticalAlignment = VerticalAlignment.Center };
            _lblBadgeIcon = _lblBadgePrompt = new TextBlock
            {
                Text = "💳\n\nPresent RFID / NFC Badge\nOr Insert Hardware Dongle",
                FontSize = 16,
                Foreground = ZeroWpfTheme.PrimaryAccent,
                TextAlignment = TextAlignment.Center
            };
            _panelBadge.Children.Add(_lblBadgePrompt);

            contentContainer.Children.Add(_panelPassword);
            contentContainer.Children.Add(_panelPin);
            contentContainer.Children.Add(_panelBadge);

            // Status label
            _lblStatus = new TextBlock
            {
                Text = "Ready",
                FontSize = 12,
                Foreground = ZeroWpfTheme.TextSecondary,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 16, 0, 0)
            };

            stack.Children.Add(headerPanel);
            stack.Children.Add(tabsGrid);
            stack.Children.Add(contentContainer);
            stack.Children.Add(_lblStatus);

            _rootBorder.Child = stack;
            Content = _rootBorder;
        }

        private Button CreateTabButton(string label, bool isActive, Action onClick)
        {
            var btn = new Button
            {
                Content = label,
                Height = 32,
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand
            };
            UpdateTabButton(btn, isActive);
            btn.Click += (s, e) => onClick();
            return btn;
        }

        /// <summary>
        /// Shows a floating AlphaNumeric virtual keyboard popup anchored below the target input element.
        /// Ensures only one popup is active at a time. Only activates when the dialog is visible (not during tests).
        /// </summary>
        private void ShowInputKeyboard(UIElement target)
        {
            if (!IsLoaded || !IsVisible)
                return;

            if (_activePopup != null && _activePopup.IsOpen)
            {
                _activePopup.IsOpen = false;
            }

            _activePopup = ZVirtualKeyboard.ShowFloatingPopup(target, VirtualKeyboardLayout.AlphaNumeric);
        }
    }
}
