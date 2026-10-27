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
    /// <summary>
    /// Multi-modal enterprise industrial login dialog in WPF supporting Username/Password,
    /// touch PIN Numpad, and hardware RFID badge authentication with dynamic theme synchronization.
    /// </summary>
    public partial class ZLoginDialog : Window, IDisposable
    {
        public enum StatusState
        {
            Ready,
            Authenticating,
            Error
        }

        private readonly IAuthenticationProvider _authProvider;
        private readonly ISessionManager _session;

        private Border _rootBorder = null!;
        private TextBlock _lblTitle = null!;
        private TextBlock _lblSubtitle = null!;
        private Button _btnClose = null!;

        private Button _tabPass = null!;
        private Button _tabPin = null!;
        private Button _tabBadge = null!;
        private int _currentTabIndex;

        private StackPanel _panelPassword = null!;
        private TextBlock _lblUsername = null!;
        private TextBlock _lblUser = null!;
        private TextBox _txtUsername = null!;
        private TextBlock _lblPassword = null!;
        private TextBlock _lblPass = null!;
        private PasswordBox _txtPassword = null!;
        private Button _btnSignIn = null!;
        private Button _btnSubmit = null!;
        private Button _btnCancel = null!;

        private StackPanel _panelPin = null!;
        private PasswordBox _txtPin = null!;
        private ZVirtualKeyboard _numpad = null!;

        private StackPanel _panelBadge = null!;
        private TextBlock _lblBadgePrompt = null!;
        private TextBlock _lblBadgeIcon = null!;

        private TextBlock _lblStatus = null!;
        private StatusState _currentStatusState = StatusState.Ready;
        private Popup? _activePopup;

        public IUser? AuthenticatedUser { get; private set; }
        public Button CloseButton => _btnClose;
        public StatusState CurrentStatusState => _currentStatusState;

        public ZLoginDialog(IAuthenticationProvider? authProvider = null, ISessionManager? session = null)
        {
            _authProvider = authProvider ?? new InMemoryUserStore();
            _session = session ?? SessionContext.Current;

            WindowStyle = WindowStyle.None;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ResizeMode = ResizeMode.NoResize;
            Width = 460;
            Height = 520;
            Background = ZeroWpfTheme.BgPrimary;

            PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    try { DialogResult = false; } catch (InvalidOperationException) { }
                    Close();
                }
            };

            BuildUI();
            ApplyTheme();

            ZeroWpfTheme.ThemeChanged += ApplyTheme;
            Closed += OnDialogClosed;
        }

        private void OnDialogClosed(object? sender, EventArgs e)
        {
            ZeroWpfTheme.ThemeChanged -= ApplyTheme;
        }

        public void Dispose()
        {
            ZeroWpfTheme.ThemeChanged -= ApplyTheme;
            Closed -= OnDialogClosed;
            GC.SuppressFinalize(this);
        }
    }
}
