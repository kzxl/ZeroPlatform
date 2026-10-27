using System;

using ZeroUI.WinForms.Icons;using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;

using System.Windows.Forms;
using ZeroUI.WinForms.Native;
using ZeroUI.WinForms.Rendering;

namespace ZeroUI.WinForms.Data
{
    public enum LogSeverity : byte

    {
        Info = 0,
        Success = 1,
        Warning = 2,
        Error = 3
    }

    public readonly struct LogEntry
    {
        public readonly DateTime Timestamp;
        public readonly LogSeverity Severity;
        public readonly string Message;

        public LogEntry(DateTime timestamp, LogSeverity severity, string message)
        {
            Timestamp = timestamp;
            Severity = severity;
            Message = message;
        }
    }

    /// <summary>
    /// High-performance virtualized event and log viewer capable of displaying 100,000+ items smoothly.
    /// Utilizes double-buffered Win32 Memory DC for flicker-free rendering.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Overlays")]
    [DefaultEvent("ItemSelected")]
    [Description("High-throughput log viewer and virtual list view")]
    [ToolboxBitmap(typeof(ZeroIcons), "ListViewControl.bmp")]
    public class ZListView : Control
    {

        private readonly List<LogEntry> _entries = new List<LogEntry>(10000);
        private readonly MemoryDIBSection _dibSection = new MemoryDIBSection();

        private int _itemHeight = 28;
        private int _scrollY = 0;
        private int _selectedIndex = -1;
        private bool _autoScrollToBottom = true;

        private IntPtr _hFont = IntPtr.Zero;
        private IntPtr _hBoldFont = IntPtr.Zero;
        private Font? _cachedFont;

        public event EventHandler<LogEntry>? ItemSelected;

        public ZListView()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.Opaque |
                ControlStyles.ResizeRedraw |
                ControlStyles.Selectable, true);

            DoubleBuffered = false;
            TabStop = true;
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            BackColor = Color.White;
        }

        public IReadOnlyList<LogEntry> Entries => _entries;

        public bool AutoScrollToBottom
        {
            get => _autoScrollToBottom;
            set => _autoScrollToBottom = value;
        }

        public void AddLog(LogSeverity severity, string message)
        {
            AddLog(DateTime.Now, severity, message);
        }

        public void AddLog(DateTime timestamp, LogSeverity severity, string message)
        {
            _entries.Add(new LogEntry(timestamp, severity, message));
            UpdateScrollBars();

            if (_autoScrollToBottom)
            {
                ScrollToBottom();
            }
            Invalidate();
        }


        public void AddLogs(IEnumerable<LogEntry> batch)
        {
            _entries.AddRange(batch);
            UpdateScrollBars();
            if (_autoScrollToBottom)
            {
                ScrollToBottom();
            }
            Invalidate();
        }

        public void Clear()
        {
            _entries.Clear();
            _scrollY = 0;
            _selectedIndex = -1;
            UpdateScrollBars();
            Invalidate();
        }

        public void ScrollToBottom()
        {
            int totalH = _entries.Count * _itemHeight;
            int maxScroll = Math.Max(0, totalH - ClientSize.Height);
            _scrollY = maxScroll;
            UpdateScrollBars();
            Invalidate();
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.Style |= NativeMethods.WS_VSCROLL;
                return cp;
            }
        }

        private void UpdateScrollBars()
        {
            if (!IsHandleCreated) return;
            int totalH = _entries.Count * _itemHeight;
            int clientH = ClientSize.Height;

            SCROLLINFO si = new SCROLLINFO
            {
                cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(SCROLLINFO)),
                fMask = NativeMethods.SIF_RANGE | NativeMethods.SIF_PAGE | NativeMethods.SIF_POS,
                nMin = 0,
                nMax = Math.Max(0, totalH),
                nPage = (uint)Math.Max(0, clientH),
                nPos = _scrollY
            };
            NativeMethods.SetScrollInfo(Handle, NativeMethods.SB_VERT, ref si, true);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == NativeMethods.WM_VSCROLL)
            {
                HandleVScroll(m.WParam);
                m.Result = IntPtr.Zero;
                return;
            }
            else if (m.Msg == NativeMethods.WM_MOUSEWHEEL)
            {
                int delta = (short)((m.WParam.ToInt64() >> 16) & 0xFFFF);
                int scrollLines = 3;
                int scrollAmount = (delta / 120) * scrollLines * _itemHeight;
                int maxScroll = Math.Max(0, (_entries.Count * _itemHeight) - ClientSize.Height);
                _scrollY = Math.Max(0, Math.Min(maxScroll, _scrollY - scrollAmount));
                UpdateScrollBars();
                Invalidate();
                m.Result = IntPtr.Zero;
                return;
            }
            base.WndProc(ref m);
        }

        private void HandleVScroll(IntPtr wParam)
        {
            int action = (int)(wParam.ToInt64() & 0xFFFF);
            int maxScroll = Math.Max(0, (_entries.Count * _itemHeight) - ClientSize.Height);

            switch (action)
            {
                case NativeMethods.SB_LINEUP: _scrollY = Math.Max(0, _scrollY - _itemHeight); break;
                case NativeMethods.SB_LINEDOWN: _scrollY = Math.Min(maxScroll, _scrollY + _itemHeight); break;
                case NativeMethods.SB_PAGEUP: _scrollY = Math.Max(0, _scrollY - ClientSize.Height); break;
                case NativeMethods.SB_PAGEDOWN: _scrollY = Math.Min(maxScroll, _scrollY + ClientSize.Height); break;
                case NativeMethods.SB_THUMBTRACK:
                case NativeMethods.SB_THUMBPOSITION:
                    SCROLLINFO si = new SCROLLINFO
                    {
                        cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(SCROLLINFO)),
                        fMask = NativeMethods.SIF_TRACKPOS
                    };
                    if (NativeMethods.GetScrollInfo(Handle, NativeMethods.SB_VERT, ref si))
                    {
                        _scrollY = Math.Max(0, Math.Min(maxScroll, si.nTrackPos));
                    }
                    break;
            }
            UpdateScrollBars();
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            int clickedIdx = (_scrollY + e.Y) / _itemHeight;
            if (clickedIdx >= 0 && clickedIdx < _entries.Count)
            {
                _selectedIndex = clickedIdx;
                ItemSelected?.Invoke(this, _entries[clickedIdx]);
                Invalidate();
            }
        }

        private void EnsureFonts()
        {
            if (_cachedFont != Font || _hFont == IntPtr.Zero)
            {
                if (_hFont != IntPtr.Zero) NativeMethods.DeleteObject(_hFont);
                if (_hBoldFont != IntPtr.Zero) NativeMethods.DeleteObject(_hBoldFont);

                _cachedFont = Font;
                _hFont = Font.ToHfont();
                using var bold = new Font(Font, FontStyle.Bold);
                _hBoldFont = bold.ToHfont();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            int width = ClientSize.Width;
            int height = ClientSize.Height;
            if (width <= 0 || height <= 0) return;

            IntPtr hdc = e.Graphics.GetHdc();
            try
            {
                EnsureFonts();
                _dibSection.EnsureSize(width, height, hdc);
                _dibSection.Clear(0x00FFFFFF);

                int firstRow = Math.Max(0, _scrollY / _itemHeight);
                int lastRow = Math.Min(_entries.Count - 1, (_scrollY + height) / _itemHeight);

                for (int i = firstRow; i <= lastRow; i++)
                {
                    int rowY = (i * _itemHeight) - _scrollY;
                    bool isSelected = (i == _selectedIndex);
                    uint rowBg = isSelected ? 0x00E0D0B0u : ((i % 2 == 1) ? 0x00FAFAFAu : 0x00FFFFFFu);


                    // Row Background
                    _dibSection.FillRectangle(0, rowY, width, _itemHeight, rowBg);

                    var entry = _entries[i];

                    // 1. Timestamp (e.g. 14:20:05.123)
                    string timeStr = entry.Timestamp.ToString("HH:mm:ss.fff");
                    RECT timeRect = new RECT(10, rowY, 110, rowY + _itemHeight);
                    _dibSection.SelectFont(_hFont);
                    _dibSection.DrawText(timeStr.AsSpan(), ref timeRect, 0x00888888, Core.Common.CellAlignment.Left, Font.Height);

                    // 2. Severity Badge
                    var (badgeText, badgeBg, badgeFg) = GetSeverityColors(entry.Severity);
                    RECT badgeRect = new RECT(116, rowY + 4, 180, rowY + _itemHeight - 4);
                    _dibSection.FillRectangle(badgeRect.Left, badgeRect.Top, badgeRect.Width, badgeRect.Height, badgeBg);
                    _dibSection.SelectFont(_hBoldFont);
                    _dibSection.DrawText(badgeText.AsSpan(), ref badgeRect, badgeFg, Core.Common.CellAlignment.Center, Font.Height);

                    // 3. Message Text
                    RECT msgRect = new RECT(190, rowY, width - 10, rowY + _itemHeight);
                    _dibSection.SelectFont(_hFont);
                    _dibSection.DrawText(entry.Message.AsSpan(), ref msgRect, 0x001A1A1A, Core.Common.CellAlignment.Left, Font.Height);

                    // Row divider
                    _dibSection.FillRectangle(0, rowY + _itemHeight - 1, width, 1, 0x00EEEEEE);
                }

                _dibSection.BitBltTo(hdc, 0, 0, width, height);
            }
            finally
            {
                e.Graphics.ReleaseHdc(hdc);
            }
        }

        private static (string text, uint bg, uint fg) GetSeverityColors(LogSeverity severity) => severity switch
        {
            LogSeverity.Success => ("SUCCESS", 0x00E0F8E0, 0x001B691B), // Soft Green
            LogSeverity.Warning => ("WARNING", 0x00D0F0FF, 0x001A6DB2), // Soft Orange
            LogSeverity.Error => ("ERROR", 0x00E0E0FF, 0x001A1AB2),     // Soft Red
            _ => ("INFO", 0x00F5F0E8, 0x0078491A)                      // Soft Blue
        };

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _dibSection.Dispose();
                if (_hFont != IntPtr.Zero) NativeMethods.DeleteObject(_hFont);
                if (_hBoldFont != IntPtr.Zero) NativeMethods.DeleteObject(_hBoldFont);
            }
            base.Dispose(disposing);
        }
    }

    [Obsolete("Use ListViewControl instead.")]
    [ToolboxItem(false)]
    public class ZeroListView : ListViewControl
    {
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZListView"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ListViewControl is deprecated and will be removed in 5 release cycles. Please migrate to ZListView instead.")]
    [ToolboxItem(false)]
    public class ListViewControl : ZListView
    {
    }

    #endregion
}
