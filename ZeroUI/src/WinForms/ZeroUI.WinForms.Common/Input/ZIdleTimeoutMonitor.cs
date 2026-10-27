using System;
using System.ComponentModel;
using System.Windows.Forms;
using ZeroUI.Core.Security;

namespace ZeroUI.WinForms.Input
{
    /// <summary>
    /// Application-wide input activity listener that automatically resets
    /// the <see cref="SessionContext"/> idle countdown whenever any keyboard,
    /// mouse, or touch interaction occurs.
    /// </summary>
    [ToolboxItem(true)]
    public class ZIdleTimeoutMonitor : Component, IMessageFilter
    {
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;
        private const int WM_MOUSEMOVE = 0x0200;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_RBUTTONDOWN = 0x0204;
        private const int WM_MBUTTONDOWN = 0x0207;
        private const int WM_MOUSEWHEEL = 0x020A;
        private const int WM_TOUCH = 0x0240;
        private const int WM_POINTERDOWN = 0x0246;

        private static bool _isHooked;
        private static readonly object _hookLock = new object();
        private bool _isEnabled = true;
        private int _lastMouseX;
        private int _lastMouseY;

        [Category("Behavior")]
        [DefaultValue(true)]
        [Description("Enables or disables idle activity tracking.")]
        public bool IsEnabled
        {
            get => _isEnabled;
            set => _isEnabled = value;
        }

        public ZIdleTimeoutMonitor()
        {
            StartGlobalMonitoring();
        }

        public ZIdleTimeoutMonitor(IContainer container)
        {
            container?.Add(this);
            StartGlobalMonitoring();
        }

        /// <summary>
        /// Installs the global WinForms message filter across the entire application domain.
        /// </summary>
        public static void StartGlobalMonitoring()
        {
            lock (_hookLock)
            {
                if (!_isHooked)
                {
                    Application.AddMessageFilter(new ZIdleTimeoutMonitor(null!));
                    _isHooked = true;
                }
            }
        }

        public bool PreFilterMessage(ref Message m)
        {
            if (!_isEnabled) return false;

            int msg = m.Msg;

            // Keyboard or click activity immediately resets
            if (msg == WM_KEYDOWN || msg == WM_KEYUP ||
                msg == WM_SYSKEYDOWN || msg == WM_SYSKEYUP ||
                msg == WM_LBUTTONDOWN || msg == WM_RBUTTONDOWN || msg == WM_MBUTTONDOWN ||
                msg == WM_MOUSEWHEEL || msg == WM_TOUCH || msg == WM_POINTERDOWN)
            {
                SessionContext.Current.NotifyActivity();
                return false;
            }

            // Mouse move: filter jitter to prevent spurious wakes
            if (msg == WM_MOUSEMOVE)
            {
                int x = (short)(m.LParam.ToInt32() & 0xFFFF);
                int y = (short)((m.LParam.ToInt32() >> 16) & 0xFFFF);

                if (Math.Abs(x - _lastMouseX) > 2 || Math.Abs(y - _lastMouseY) > 2)
                {
                    _lastMouseX = x;
                    _lastMouseY = y;
                    SessionContext.Current.NotifyActivity();
                }
            }

            return false;
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
