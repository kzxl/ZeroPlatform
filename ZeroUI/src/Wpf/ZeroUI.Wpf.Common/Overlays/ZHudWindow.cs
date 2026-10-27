using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace ZeroUI.Wpf.Overlays;

/// <summary>
/// A non-activating floating HUD window with frosted glass aesthetic, perfect for subtitles, real-time metrics, and telemetry overlays.
/// Avoids stealing user typing focus using WS_EX_NOACTIVATE.
/// </summary>
public class ZHudWindow : Window
{
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int GWL_EXSTYLE = -20;

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    public ZHudWindow()
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = new SolidColorBrush(Color.FromArgb(200, 24, 24, 27)); // Deep acrylic dark
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = false;

        MouseLeftButtonDown += OnMouseLeftButtonDown;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var helper = new WindowInteropHelper(this);
        if (helper.Handle != IntPtr.Zero)
        {
            var exStyle = GetWindowLongPtr(helper.Handle, GWL_EXSTYLE).ToInt64();
            exStyle |= WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW;
            SetWindowLongPtr(helper.Handle, GWL_EXSTYLE, new IntPtr(exStyle));
        }
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            try
            {
                DragMove();
            }
            catch
            {
                // Ignored if drag released unexpectedly
            }
        }
    }
}

/// <summary>
/// Backward-compatibility alias for <see cref="ZHudWindow"/>.
/// </summary>
[Obsolete("HudWindow is deprecated and will be removed in 5 release cycles. Please migrate to ZHudWindow instead.")]
public class HudWindow : ZHudWindow
{
}
