using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using ZeroUI.Core.Notification;

namespace ZeroUI.Wpf.Overlays
{
    /// <summary>
    /// Native Windows OS Notification Bridge for ZeroUI WPF.
    /// Utilizes Win32 Shell_NotifyIcon to trigger Windows 10/11 Action Center and System Tray notifications.
    /// Operates with zero third-party dependencies and sub-millisecond execution.
    /// </summary>
    public static class WindowsNotificationBridge
    {
        private static readonly object _syncLock = new object();
        private static Window? _registeredWindow;
        private static HwndSource? _hwndSource;
        private static IntPtr _hWnd = IntPtr.Zero;
        private static bool _isTrayAdded = false;
        private static Action? _pendingClickAction;

        private const int WM_USER = 0x0400;
        private const int WM_TRAYICON = WM_USER + 201;

        private const int NIM_ADD = 0x00000000;
        private const int NIM_MODIFY = 0x00000001;
        private const int NIM_DELETE = 0x00000002;
        private const int NIM_SETVERSION = 0x00000004;

        private const int NOTIFYICON_VERSION_4 = 4;

        private const int NIF_MESSAGE = 0x00000001;
        private const int NIF_ICON = 0x00000002;
        private const int NIF_TIP = 0x00000004;
        private const int NIF_INFO = 0x00000010;

        private const int NIIF_NONE = 0x00000000;
        private const int NIIF_INFO = 0x00000001;
        private const int NIIF_WARNING = 0x00000002;
        private const int NIIF_ERROR = 0x00000003;
        private const int NIIF_LARGE_ICON = 0x00000020;

        private const int NIN_BALLOONUSERCLICK = 0x0405;
        private const int WM_LBUTTONDBLCLK = 0x0203;

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern bool Shell_NotifyIcon(int dwMessage, ref NOTIFYICONDATA lpdata);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr LoadIcon(IntPtr hInstance, IntPtr lpIconName);

        private const int SW_RESTORE = 9;
        private static readonly IntPtr IDI_APPLICATION = new IntPtr(32512);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct NOTIFYICONDATA
        {
            public int cbSize;
            public IntPtr hWnd;
            public int uID;
            public int uFlags;
            public int uCallbackMessage;
            public IntPtr hIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szTip;
            public int dwState;
            public int dwStateMask;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string szInfo;
            public int uTimeoutOrVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public string szInfoTitle;
            public int dwInfoFlags;
            public Guid guidItem;
            public IntPtr hBalloonIcon;
        }

        /// <summary>
        /// Registers the main application window for auto-focus/restoration when notifications are clicked.
        /// </summary>
        public static void RegisterMainWindow(Window window)
        {
            _registeredWindow = window;
            if (window.IsLoaded)
            {
                AttachHwnd(window);
            }
            else
            {
                window.Loaded += (s, e) => AttachHwnd(window);
            }
        }

        private static void AttachHwnd(Window window)
        {
            var helper = new WindowInteropHelper(window);
            _hWnd = helper.Handle;
            if (_hWnd != IntPtr.Zero && _hwndSource == null)
            {
                _hwndSource = HwndSource.FromHwnd(_hWnd);
                _hwndSource?.AddHook(WndProc);
            }
        }

        private static IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_TRAYICON)
            {
                int eventId = lParam.ToInt32() & 0xFFFF;
                if (eventId == NIN_BALLOONUSERCLICK || eventId == WM_LBUTTONDBLCLK)
                {
                    OnNotificationActivated();
                    handled = true;
                }
            }
            return IntPtr.Zero;
        }

        private static void OnNotificationActivated()
        {
            try
            {
                if (_registeredWindow != null)
                {
                    _registeredWindow.Dispatcher.Invoke(() =>
                    {
                        if (_registeredWindow.WindowState == WindowState.Minimized)
                        {
                            _registeredWindow.WindowState = WindowState.Normal;
                        }
                        _registeredWindow.Activate();
                        _registeredWindow.Focus();
                        if (_hWnd != IntPtr.Zero)
                        {
                            ShowWindow(_hWnd, SW_RESTORE);
                            SetForegroundWindow(_hWnd);
                        }
                    });
                }

                var action = _pendingClickAction;
                _pendingClickAction = null;
                action?.Invoke();
            }
            catch
            {
                // Non-critical activation
            }
        }

        /// <summary>
        /// Dispatches a native Windows OS notification banner.
        /// </summary>
        public static void ShowNotification(
            string title,
            string message,
            ToastType type = ToastType.Info,
            int timeoutMs = 4000,
            Action? onClick = null)
        {
            lock (_syncLock)
            {
                try
                {
                    if (_hWnd == IntPtr.Zero && _registeredWindow != null)
                    {
                        var helper = new WindowInteropHelper(_registeredWindow);
                        _hWnd = helper.Handle;
                    }

                    _pendingClickAction = onClick;

                    int infoFlag = type switch
                    {
                        ToastType.Success => NIIF_INFO,
                        ToastType.Info => NIIF_INFO,
                        ToastType.Warning => NIIF_WARNING,
                        ToastType.Error => NIIF_ERROR,
                        ToastType.Alarm => NIIF_ERROR,
                        _ => NIIF_INFO
                    };

                    NOTIFYICONDATA nid = new NOTIFYICONDATA();
                    nid.cbSize = Marshal.SizeOf(typeof(NOTIFYICONDATA));
                    nid.hWnd = _hWnd;
                    nid.uID = 1001;
                    nid.uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP | NIF_INFO;
                    nid.uCallbackMessage = WM_TRAYICON;
                    nid.hIcon = LoadIcon(IntPtr.Zero, IDI_APPLICATION);
                    nid.szTip = "ZeroUI Notification";
                    string safeTitle = string.IsNullOrEmpty(title) ? "ZeroUI Notification" : title;
                    if (safeTitle.Length >= 64)
                    {
                        safeTitle = safeTitle.Substring(0, 60) + "...";
                    }
                    string safeMsg = message ?? string.Empty;
                    if (safeMsg.Length >= 256)
                    {
                        safeMsg = safeMsg.Substring(0, 252) + "...";
                    }

                    nid.szInfoTitle = safeTitle;
                    nid.szInfo = safeMsg;
                    nid.dwInfoFlags = infoFlag | NIIF_LARGE_ICON;
                    nid.uTimeoutOrVersion = timeoutMs;

                    if (!_isTrayAdded)
                    {
                        Shell_NotifyIcon(NIM_ADD, ref nid);
                        _isTrayAdded = true;
                    }
                    else
                    {
                        Shell_NotifyIcon(NIM_MODIFY, ref nid);
                    }
                }
                catch
                {
                    // Graceful degradation
                }
            }
        }

        /// <summary>
        /// Removes the tray icon and releases OS hooks.
        /// </summary>
        public static void Shutdown()
        {
            lock (_syncLock)
            {
                if (_isTrayAdded && _hWnd != IntPtr.Zero)
                {
                    NOTIFYICONDATA nid = new NOTIFYICONDATA();
                    nid.cbSize = Marshal.SizeOf(typeof(NOTIFYICONDATA));
                    nid.hWnd = _hWnd;
                    nid.uID = 1001;
                    Shell_NotifyIcon(NIM_DELETE, ref nid);
                    _isTrayAdded = false;
                }
                _hwndSource?.RemoveHook(WndProc);
                _hwndSource = null;
            }
        }
    }
}
