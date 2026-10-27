using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ZeroUI.Core.Security;
using ZeroUI.Wpf.Base;

namespace ZeroUI.Wpf.Feedback
{
    /// <summary>
    /// Statusbar or header badge displaying active user identity, role,
    /// live idle timeout countdown, and quick-switch / logout actions in WPF.
    /// </summary>
    public class ZUserStatusBadge : WpfControlBase
    {
        public static readonly DependencyProperty ShowCountdownProperty =
            DependencyProperty.Register(
                nameof(ShowCountdown),
                typeof(bool),
                typeof(ZUserStatusBadge),
                new PropertyMetadata(true));

        public static readonly DependencyProperty ShowAvatarProperty =
            DependencyProperty.Register(
                nameof(ShowAvatar),
                typeof(bool),
                typeof(ZUserStatusBadge),
                new PropertyMetadata(true));

        public bool ShowCountdown
        {
            get => (bool)GetValue(ShowCountdownProperty);
            set => SetValue(ShowCountdownProperty, value);
        }

        public bool ShowAvatar
        {
            get => (bool)GetValue(ShowAvatarProperty);
            set => SetValue(ShowAvatarProperty, value);
        }

        private ISessionManager? _session;

        public ISessionManager Session
        {
            get => _session ?? SessionContext.Current;
            set
            {
                if (_session != value)
                {
                    if (_session != null) _session.UserChanged -= OnUserChanged;
                    _session = value;
                    if (_session != null) _session.UserChanged += OnUserChanged;
                    UpdateDisplay();
                }
            }
        }

        private void OnUserChanged(object? sender, UserChangedEventArgs e)
        {
            Dispatcher.Invoke(UpdateDisplay);
        }

        public event EventHandler? SwitchUserClicked;
        public event EventHandler? LogoutClicked;

        private readonly DispatcherTimer _timer;
        private readonly Border _rootBorder = new Border();
        private readonly TextBlock _txtName = new TextBlock();
        private readonly TextBlock _txtRole = new TextBlock();
        private readonly TextBlock _txtCountdown = new TextBlock();
        private readonly TextBlock _txtAvatar = new TextBlock();

        static ZUserStatusBadge()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(ZUserStatusBadge),
                new FrameworkPropertyMetadata(typeof(ZUserStatusBadge)));
        }

        public ZUserStatusBadge()
        {
            Width = 240;
            Height = 38;

            BuildUI();

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += (s, e) => UpdateCountdown();
            _timer.Start();

            Session.UserChanged += OnUserChanged;
            UpdateDisplay();

            AddVisualChild(_rootBorder);
            AddLogicalChild(_rootBorder);
        }

        protected override int VisualChildrenCount => 1;
        protected override Visual GetVisualChild(int index) => _rootBorder;

        protected override Size MeasureOverride(Size constraint)
        {
            _rootBorder.Measure(constraint);
            return _rootBorder.DesiredSize;
        }

        protected override Size ArrangeOverride(Size arrangeBounds)
        {
            _rootBorder.Arrange(new Rect(arrangeBounds));
            return arrangeBounds;
        }

        private void BuildUI()
        {
            _rootBorder.CornerRadius = new CornerRadius(6);
            _rootBorder.Background = new SolidColorBrush(Color.FromRgb(26, 32, 44));
            _rootBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(51, 65, 85));
            _rootBorder.BorderThickness = new Thickness(1);
            _rootBorder.Padding = new Thickness(6, 2, 6, 2);

            var dock = new DockPanel();

            // Avatar
            var avBorder = new Border
            {
                Width = 26,
                Height = 26,
                CornerRadius = new CornerRadius(13),
                Background = new SolidColorBrush(Color.FromRgb(14, 165, 233)),
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(avBorder, Dock.Left);

            _txtAvatar.Text = "U";
            _txtAvatar.FontSize = 10;
            _txtAvatar.FontWeight = FontWeights.Bold;
            _txtAvatar.Foreground = Brushes.White;
            _txtAvatar.HorizontalAlignment = HorizontalAlignment.Center;
            _txtAvatar.VerticalAlignment = VerticalAlignment.Center;
            avBorder.Child = _txtAvatar;
            dock.Children.Add(avBorder);

            // Right action buttons
            var btnLogout = new Button
            {
                Content = "✕",
                Width = 18,
                Height = 18,
                FontSize = 9,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                Background = new SolidColorBrush(Color.FromRgb(71, 85, 105)),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0, 0, 0)
            };
            btnLogout.Click += (s, e) =>
            {
                LogoutClicked?.Invoke(this, EventArgs.Empty);
                SessionContext.Current.Logout();
            };
            DockPanel.SetDock(btnLogout, Dock.Right);
            dock.Children.Add(btnLogout);

            var btnSwitch = new Button
            {
                Content = "⇄",
                Width = 18,
                Height = 18,
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                Background = new SolidColorBrush(Color.FromRgb(71, 85, 105)),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0, 0, 0)
            };
            btnSwitch.Click += (s, e) => SwitchUserClicked?.Invoke(this, EventArgs.Empty);
            DockPanel.SetDock(btnSwitch, Dock.Right);
            dock.Children.Add(btnSwitch);

            _txtCountdown.FontSize = 10;
            _txtCountdown.Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184));
            _txtCountdown.VerticalAlignment = VerticalAlignment.Center;
            _txtCountdown.Margin = new Thickness(4, 0, 0, 0);
            DockPanel.SetDock(_txtCountdown, Dock.Right);
            dock.Children.Add(_txtCountdown);

            // Center details
            var centerStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            _txtName.FontSize = 11;
            _txtName.FontWeight = FontWeights.Bold;
            _txtName.Foreground = Brushes.White;

            _txtRole.FontSize = 9;
            _txtRole.Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184));

            centerStack.Children.Add(_txtName);
            centerStack.Children.Add(_txtRole);
            dock.Children.Add(centerStack);

            _rootBorder.Child = dock;
        }

        private void UpdateDisplay()
        {
            var session = Session;
            var user = session.CurrentUser;
            bool auth = session.IsAuthenticated;

            if (auth && user != null)
            {
                _txtName.Text = user.DisplayName;
                _txtRole.Text = (user.Roles.Count > 0) ? user.Roles[0] : "Operator";
                _txtAvatar.Text = (user.DisplayName.Length > 0) ? user.DisplayName.Substring(0, Math.Min(2, user.DisplayName.Length)).ToUpperInvariant() : "U";
            }
            else
            {
                _txtName.Text = "Not Signed In";
                _txtRole.Text = "Guest";
                _txtAvatar.Text = "?";
                _txtCountdown.Text = "";
            }
            UpdateCountdown();
        }

        private void UpdateCountdown()
        {
            var session = Session;
            if (!ShowCountdown || !session.IsAuthenticated || session.IdleTimeout <= TimeSpan.Zero)
            {
                _txtCountdown.Text = "";
                return;
            }

            var remain = session.IdleTimeout - session.ElapsedIdleTime;
            if (remain < TimeSpan.Zero) remain = TimeSpan.Zero;
            _txtCountdown.Text = $"{(int)remain.TotalMinutes}:{remain.Seconds:D2}";
        }
    }
}
