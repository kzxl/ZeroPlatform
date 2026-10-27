using System;
using System.ComponentModel;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using ZeroUI.Core.Common;
using ZeroUI.Core.Security;

namespace ZeroUI.WinForms.Input
{
    /// <summary>
    /// Event arguments for RFID/NFC hardware badge scans.
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
    /// Global hardware badge scanner listener that intercepts USB HID RFID/NFC wedge scanners
    /// and triggers instantaneous operator authentication without requiring manual focus on an input box.
    /// </summary>
    [ToolboxItem(true)]
    public class ZBadgeReaderListener : Component, IMessageFilter
    {
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_CHAR = 0x0102;

        private readonly StringBuilder _buffer = new StringBuilder();
        private long _lastKeyTimeMs;
        private bool _isEnabled = true;
        private int _maxInterKeyLatencyMs = 80;
        private int _minBadgeLength = 4;
        private bool _autoSignIn = true;

        private readonly IAuthenticationProvider _authProvider;
        private readonly ISessionManager _session;

        [Category("Behavior")]
        [DefaultValue(true)]
        [Description("Enables or disables automatic badge scanning.")]
        public bool IsEnabled
        {
            get => _isEnabled;
            set => _isEnabled = value;
        }

        [Category("Behavior")]
        [DefaultValue(80)]
        [Description("Maximum millisecond latency between consecutive characters to qualify as a hardware scanner burst.")]
        public int MaxInterKeyLatencyMs
        {
            get => _maxInterKeyLatencyMs;
            set => _maxInterKeyLatencyMs = value;
        }

        [Category("Behavior")]
        [DefaultValue(4)]
        [Description("Minimum character length for a valid RFID/NFC badge identifier.")]
        public int MinBadgeLength
        {
            get => _minBadgeLength;
            set => _minBadgeLength = value;
        }

        [Category("Behavior")]
        [DefaultValue(true)]
        [Description("If true, automatically authenticates and signs in the user when a registered badge is swiped.")]
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
            Application.AddMessageFilter(this);
        }

        public ZBadgeReaderListener(IContainer container) : this()
        {
            container?.Add(this);
        }

        public bool PreFilterMessage(ref Message m)
        {
            if (!_isEnabled) return false;

            if (m.Msg == WM_CHAR)
            {
                char c = (char)m.WParam.ToInt32();
                long now = MonotonicClock.ElapsedMilliseconds;

                // Check inter-key latency
                if (_buffer.Length > 0 && (now - _lastKeyTimeMs) > _maxInterKeyLatencyMs)
                {
                    _buffer.Clear(); // Too slow, was likely manual keyboard typing
                }

                _lastKeyTimeMs = now;

                if (c == '\r' || c == '\n')
                {
                    if (_buffer.Length >= _minBadgeLength)
                    {
                        string badgeId = _buffer.ToString().Trim();
                        _buffer.Clear();
                        _ = OnBadgeDetectedAsync(badgeId);
                        return true; // Consume trailing enter
                    }
                    _buffer.Clear();
                }
                else if (!char.IsControl(c))
                {
                    _buffer.Append(c);
                }
            }

            return false;
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

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Application.RemoveMessageFilter(this);
            }
            base.Dispose(disposing);
        }
    }
}
