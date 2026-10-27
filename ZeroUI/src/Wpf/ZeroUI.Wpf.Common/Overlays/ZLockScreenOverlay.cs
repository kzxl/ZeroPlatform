using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ZeroUI.Core.Input;
using ZeroUI.Core.Security;
using ZeroUI.Wpf.Input;

namespace ZeroUI.Wpf.Overlays
{
    /// <summary>
    /// Touch-friendly WPF Lock Screen overlay with PIN pad, password verification,
    /// and RFID badge unlock for industrial workstations.
    /// </summary>
    public class ZLockScreenOverlay : Window
    {
        private static ZLockScreenOverlay? _currentInstance;
        private readonly IAuthenticationProvider _authProvider;
        private readonly ISessionManager _session;

        private PasswordBox _passwordBox = null!;
        private TextBlock _lblStatus = null!;
        private ZVirtualKeyboard _numpad = null!;

        public ZLockScreenOverlay(IAuthenticationProvider? authProvider = null, ISessionManager? session = null)
        {
            _authProvider = authProvider ?? new InMemoryUserStore();
            _session = session ?? SessionContext.Current;

            WindowStyle = WindowStyle.None;
            WindowState = WindowState.Maximized;
            AllowsTransparency = true;
            Background = new SolidColorBrush(Color.FromArgb(210, 10, 14, 22));
            ShowInTaskbar = false;
            Topmost = true;

            InitializeLayout();
        }

        private void InitializeLayout()
        {
            var user = _session.CurrentUser;
            string displayName = user?.DisplayName ?? "Workstation Locked";
            string roleText = (user != null && user.Roles.Count > 0) ? $"Role: {string.Join(", ", user.Roles)}" : "Operator Station";

            var rootGrid = new Grid();

            var card = new Border
            {
                Width = 380,
                Height = 520,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                CornerRadius = new CornerRadius(8),
                Background = new SolidColorBrush(Color.FromRgb(26, 32, 44)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(51, 65, 85)),
                BorderThickness = new Thickness(1.5),
                Padding = new Thickness(20)
            };

            var stack = new StackPanel();

            var titleBlock = new TextBlock
            {
                Text = "🔒 " + displayName,
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 10, 0, 4)
            };

            var subBlock = new TextBlock
            {
                Text = roleText,
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 16)
            };

            _passwordBox = new PasswordBox
            {
                FontSize = 20,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                Height = 40,
                Background = new SolidColorBrush(Color.FromRgb(20, 25, 36)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(51, 65, 85)),
                Margin = new Thickness(10, 0, 10, 6)
            };
            _passwordBox.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    _ = AttemptUnlockAsync(_passwordBox.Password);
                }
            };

            _lblStatus = new TextBlock
            {
                Text = "Enter PIN or Password to Unlock",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 10)
            };

            _numpad = new ZVirtualKeyboard
            {
                LayoutMode = VirtualKeyboardLayout.Numpad,
                TargetElement = _passwordBox,
                Height = 280,
                Margin = new Thickness(0, 0, 0, 0)
            };
            _numpad.EnterPressed += (s, e) => _ = AttemptUnlockAsync(_passwordBox.Password);

            stack.Children.Add(titleBlock);
            stack.Children.Add(subBlock);
            stack.Children.Add(_passwordBox);
            stack.Children.Add(_lblStatus);
            stack.Children.Add(_numpad);

            card.Child = stack;
            rootGrid.Children.Add(card);
            Content = rootGrid;

            Loaded += (s, e) => _passwordBox.Focus();
        }

        public async Task<bool> AttemptUnlockAsync(string secret)
        {
            if (string.IsNullOrWhiteSpace(secret))
            {
                SetStatus("Please enter your PIN or password", isError: true);
                return false;
            }

            SetStatus("Verifying...", isError: false);

            var user = _session.CurrentUser;
            string targetUser = user?.Username ?? "admin";

            bool isAllDigits = true;
            for (int i = 0; i < secret.Length; i++)
            {
                if (!char.IsDigit(secret[i])) { isAllDigits = false; break; }
            }

            AuthResult result;
            if (isAllDigits)
            {
                result = await _authProvider.AuthenticatePinAsync(targetUser, secret);
            }
            else
            {
                result = await _authProvider.AuthenticatePasswordAsync(targetUser, secret);
            }

            if (result.Succeeded)
            {
                _session.UnlockSession();
                Close();
                return true;
            }
            else
            {
                SetStatus(result.ErrorMessage ?? "Invalid credential", isError: true);
                _passwordBox.Password = string.Empty;
                _passwordBox.Focus();
                return false;
            }
        }

        public async Task<bool> UnlockWithBadgeAsync(string badgeId)
        {
            SetStatus("Reading Badge...", isError: false);
            var result = await _authProvider.AuthenticateBadgeAsync(badgeId);
            if (result.Succeeded)
            {
                _session.UnlockSession();
                Close();
                return true;
            }
            else
            {
                SetStatus(result.ErrorMessage ?? "Badge not recognized", isError: true);
                return false;
            }
        }

        private void SetStatus(string message, bool isError)
        {
            _lblStatus.Text = message;
            _lblStatus.Foreground = isError
                ? new SolidColorBrush(Color.FromRgb(239, 68, 68))
                : new SolidColorBrush(Color.FromRgb(56, 189, 248));
        }

        /// <summary>
        /// Displays the global lock screen overlay.
        /// </summary>
        public static ZLockScreenOverlay ShowLock(IAuthenticationProvider? authProvider = null, ISessionManager? session = null)
        {
            if (_currentInstance != null && _currentInstance.IsLoaded)
            {
                _currentInstance.Activate();
                return _currentInstance;
            }

            var lockScreen = new ZLockScreenOverlay(authProvider, session);
            _currentInstance = lockScreen;
            lockScreen.Closed += (s, e) => _currentInstance = null;
            lockScreen.Show();
            return lockScreen;
        }
    }
}
