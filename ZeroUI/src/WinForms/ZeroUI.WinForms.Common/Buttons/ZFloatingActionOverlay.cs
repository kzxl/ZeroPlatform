using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ZeroUI.WinForms.Buttons
{
    /// <summary>
    /// Lightweight borderless owned floating window designed to host <see cref="ZFloatingActionButton"/>.
    /// Permanently stays on top of all child controls, tabs, and data grids of the parent form
    /// without ever being obscured, losing z-order priority, or stealing focus.
    /// </summary>
    public class ZFloatingActionOverlay : Form
    {
        private readonly Form _parentForm;
        private readonly ZFloatingActionButton _button;
        private readonly int _baseRightMargin;
        private readonly int _baseBottomMargin;
        private readonly int _baseButtonSize;
        private bool _isDisposed;

        public ZFloatingActionButton Button => _button;

        public ZFloatingActionOverlay(Form parentForm, ZFloatingActionButton button, int rightMargin = 28, int bottomMargin = 28)
        {
            _parentForm = parentForm ?? throw new ArgumentNullException(nameof(parentForm));
            _button = button ?? throw new ArgumentNullException(nameof(button));
            _baseRightMargin = rightMargin;
            _baseBottomMargin = bottomMargin;
            _baseButtonSize = Math.Min(button.Width, button.Height);
            if (_baseButtonSize <= 0) _baseButtonSize = 56;

            // Prevent WinForms AutoScale from double-scaling controls
            this.AutoScaleMode = AutoScaleMode.None;
            this.FormBorderStyle = FormBorderStyle.None;
            this.ShowInTaskbar = false;
            this.StartPosition = FormStartPosition.Manual;
            this.BackColor = Color.Magenta;
            this.TransparencyKey = Color.Magenta;
            this.Owner = _parentForm;

            // Apply DPI scale immediately
            ApplyDpiScaling();

            _button.Dock = DockStyle.Fill;
            this.Controls.Add(_button);

            // Synchronize overlay position with parent form
            UpdatePosition();

            _parentForm.LocationChanged += OnParentStateChanged;
            _parentForm.SizeChanged += OnParentStateChanged;
            _parentForm.VisibleChanged += OnParentStateChanged;
            _parentForm.Activated += OnParentActivated;
            _parentForm.FormClosing += OnParentFormClosing;
        }

        #region Win32 P/Invoke & Interop

        private const int WM_GETMINMAXINFO = 0x0024;
        private const int WM_DPICHANGED = 0x02E0;

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct MINMAXINFO
        {
            public POINT ptReserved;
            public POINT ptMaxSize;
            public POINT ptMaxPosition;
            public POINT ptMinTrackSize;
            public POINT ptMaxTrackSize;
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr hWnd);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        private static extern int GetDeviceCaps(IntPtr hdc, int nIndex);

        private static readonly IntPtr HWND_TOP = IntPtr.Zero;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const int LOGPIXELSX = 88;

        /// <summary>
        /// Explicitly elevates this overlay to the absolute top of the window Z-order without stealing focus.
        /// </summary>
        public void BringToTop()
        {
            if (this.IsHandleCreated && !this.IsDisposed)
            {
                SetWindowPos(this.Handle, HWND_TOP, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
            }
        }

        protected override void WndProc(ref Message m)
        {
            // CRITICAL: Windows enforces a default minimum track width (~136px on Win10/11) on top-level forms.
            // Overriding WM_GETMINMAXINFO allows the form to retain its true circular dimensions (e.g. 56x56, 70x70) without distortion.
            if (m.Msg == WM_GETMINMAXINFO)
            {
                base.WndProc(ref m);
                var mmi = (MINMAXINFO)System.Runtime.InteropServices.Marshal.PtrToStructure(m.LParam, typeof(MINMAXINFO));
                mmi.ptMinTrackSize.x = 1;
                mmi.ptMinTrackSize.y = 1;
                System.Runtime.InteropServices.Marshal.StructureToPtr(mmi, m.LParam, true);
                return;
            }

            if (m.Msg == WM_DPICHANGED)
            {
                base.WndProc(ref m);
                ApplyDpiScaling();
                UpdatePosition();
                return;
            }

            base.WndProc(ref m);
        }

        #endregion

        /// <summary>
        /// Prevents this overlay window from stealing input focus from the parent form.
        /// </summary>
        protected override bool ShowWithoutActivation => true;

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
                return cp;
            }
        }

        /// <summary>
        /// Obtains the current display DPI scale factor (1.0 = 100%, 1.25 = 125%, 1.5 = 150%).
        /// </summary>
        public float GetDpiScale()
        {
            try
            {
                IntPtr targetHwnd = _parentForm.IsHandleCreated ? _parentForm.Handle : this.Handle;
                if (targetHwnd != IntPtr.Zero)
                {
                    try
                    {
                        uint dpi = GetDpiForWindow(targetHwnd);
                        if (dpi > 0) return dpi / 96.0f;
                    }
                    catch (EntryPointNotFoundException)
                    {
                        // Fallback for older OS versions
                    }

                    IntPtr hdc = GetDC(targetHwnd);
                    if (hdc != IntPtr.Zero)
                    {
                        try
                        {
                            int dpiX = GetDeviceCaps(hdc, LOGPIXELSX);
                            if (dpiX > 0) return dpiX / 96.0f;
                        }
                        finally
                        {
                            ReleaseDC(targetHwnd, hdc);
                        }
                    }
                }
            }
            catch { }
            return 1.0f;
        }

        /// <summary>
        /// Recalculates button diameter and window size according to the current monitor DPI.
        /// </summary>
        public void ApplyDpiScaling()
        {
            float scale = GetDpiScale();
            int targetSize = (int)Math.Round(_baseButtonSize * scale);
            if (targetSize < 24) targetSize = 24;

            if (this.Width != targetSize || this.Height != targetSize)
            {
                this.Size = new Size(targetSize, targetSize);
                _button.Size = new Size(targetSize, targetSize);
                UpdateRegion();
                _button.UpdateRegion();
            }
        }

        private void UpdateRegion()
        {
            int d = Math.Min(this.ClientSize.Width, this.ClientSize.Height);
            if (d <= 0) return;

            using (var path = new GraphicsPath())
            {
                path.AddEllipse(0, 0, d, d);
                this.Region = new Region(path);
            }
        }

        private void OnParentStateChanged(object sender, EventArgs e)
        {
            if (_isDisposed || this.IsDisposed) return;
            UpdatePosition();
            BringToTop();
        }

        private void OnParentActivated(object sender, EventArgs e)
        {
            if (_isDisposed || this.IsDisposed) return;
            UpdatePosition();
            BringToTop();
        }

        private void OnParentFormClosing(object sender, FormClosingEventArgs e)
        {
            if (!_isDisposed)
            {
                this.Close();
            }
        }

        /// <summary>
        /// Recalculates absolute screen coordinates relative to the parent form's client area and raises to top.
        /// </summary>
        public void UpdatePosition()
        {
            if (_parentForm.WindowState == FormWindowState.Minimized || !_parentForm.Visible)
            {
                if (this.Visible) this.Hide();
                return;
            }

            ApplyDpiScaling();

            if (!this.Visible) this.Show();

            float scale = GetDpiScale();
            int rightMargin = (int)Math.Round(_baseRightMargin * scale);
            int bottomMargin = (int)Math.Round(_baseBottomMargin * scale);

            var clientPt = new Point(
                Math.Max(10, _parentForm.ClientSize.Width - this.Width - rightMargin),
                Math.Max(10, _parentForm.ClientSize.Height - this.Height - bottomMargin)
            );

            this.Location = _parentForm.PointToScreen(clientPt);
            BringToTop();
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            UpdateRegion();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !_isDisposed)
            {
                _isDisposed = true;
                _parentForm.LocationChanged -= OnParentStateChanged;
                _parentForm.SizeChanged -= OnParentStateChanged;
                _parentForm.VisibleChanged -= OnParentStateChanged;
                _parentForm.Activated -= OnParentActivated;
                _parentForm.FormClosing -= OnParentFormClosing;
            }
            base.Dispose(disposing);
        }
    }
}
