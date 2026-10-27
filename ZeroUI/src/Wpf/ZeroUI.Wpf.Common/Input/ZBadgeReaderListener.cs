using System;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using ZeroUI.Core.Common;
using ZeroUI.Core.Security;

namespace ZeroUI.Wpf.Input
{
    /// <summary>
    /// Event arguments for RFID/NFC hardware badge scans in WPF.
    /// </summary>
    public class BadgeScannedEventArgs : EventArgs
    {
        public string BadgeId { get; }
        public bool Handled { get; set; }

        public BadgeScannedEventArgs(string badgeId)
        {
            BadgeId = badgeId ?? string.Empty;
        }
    }

    /// <summary>
    /// Global hardware badge scanner listener for WPF applications.
    /// Intercepts USB HID RFID/NFC wedge scanners and performs instant sign-in.
    /// </summary>
    public class ZBadgeReaderListener : IDisposable
    {
        private static ZBadgeReaderListener? _instance;
        private static readonly object _syncLock = new object();

        private readonly StringBuilder _buffer = new StringBuilder();
        private long _lastKeyTimeMs;
        private bool _isEnabled = true;
        private int _maxInterKeyLatencyMs = 80;
        private int _minBadgeLength = 4;
        private bool _autoSignIn = true;

        private readonly IAuthenticationProvider _authProvider;
        private readonly ISessionManager _session;

        public bool IsEnabled
        {
            get => _isEnabled;
            set => _isEnabled = value;
        }

        public int MaxInterKeyLatencyMs
        {
            get => _maxInterKeyLatencyMs;
            set => _maxInterKeyLatencyMs = value;
        }

        public int MinBadgeLength
        {
            get => _minBadgeLength;
            set => _minBadgeLength = value;
        }

        public bool AutoSignIn
        {
            get => _autoSignIn;
            set => _autoSignIn = value;
        }

        public event EventHandler<BadgeScannedEventArgs>? BadgeScanned;

        public ZBadgeReaderListener(IAuthenticationProvider? authProvider = null, ISessionManager? session = null)
        {
            _authProvider = authProvider ?? new InMemoryUserStore();
            _session = session ?? SessionContext.Current;
            InputManager.Current.PreProcessInput += OnPreProcessInput;
        }

        public static ZBadgeReaderListener StartListening(IAuthenticationProvider? authProvider = null, ISessionManager? session = null)
        {
            lock (_syncLock)
            {
                if (_instance == null)
                {
                    _instance = new ZBadgeReaderListener(authProvider, session);
                }
                return _instance;
            }
        }

        private void OnPreProcessInput(object sender, PreProcessInputEventArgs e)
        {
            if (!_isEnabled) return;

            if (e.StagingItem.Input is TextCompositionEventArgs textArgs)
            {
                string text = textArgs.Text;
                long now = MonotonicClock.ElapsedMilliseconds;

                if (_buffer.Length > 0 && (now - _lastKeyTimeMs) > _maxInterKeyLatencyMs)
                {
                    _buffer.Clear();
                }

                _lastKeyTimeMs = now;

                for (int i = 0; i < text.Length; i++)
                {
                    char c = text[i];
                    if (c == '\r' || c == '\n')
                    {
                        if (_buffer.Length >= _minBadgeLength)
                        {
                            string badgeId = _buffer.ToString().Trim();
                            _buffer.Clear();
                            _ = OnBadgeDetectedAsync(badgeId);
                            textArgs.Handled = true;
                            return;
                        }
                        _buffer.Clear();
                    }
                    else if (!char.IsControl(c))
                    {
                        _buffer.Append(c);
                    }
                }
            }
        }

        private async Task OnBadgeDetectedAsync(string badgeId)
        {
            var args = new BadgeScannedEventArgs(badgeId);
            BadgeScanned?.Invoke(this, args);

            if (_autoSignIn && !args.Handled)
            {
                var result = await _authProvider.AuthenticateBadgeAsync(badgeId);
                if (result.Succeeded && result.User != null)
                {
                    _session.SetCurrentUser(result.User);
                    _session.UnlockSession();
                }
            }
        }

        public void Dispose()
        {
            InputManager.Current.PreProcessInput -= OnPreProcessInput;
        }
    }
}
