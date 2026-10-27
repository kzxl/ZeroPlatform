using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.Core.Network;
using ZeroUI.Core.Rendering;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Network
{
    /// <summary>
    /// Industrial Fieldbus Line & Redundant Ring Monitor.
    /// Visualizes industrial communication lines (Profinet, EtherCAT, Modbus RTU, EtherNet/IP)
    /// with real-time cable break localization, CRC retry telemetry, and ring recovery status.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Network & Infrastructure")]
    [DefaultEvent("SelectedStationChanged")]
    [Description("Industrial Fieldbus Line Monitor with automatic cable break localization")]
    public class ZFieldbusMonitor : Control
    {
        private readonly FieldbusNetwork _network = new FieldbusNetwork();
        private FieldbusStation? _selectedStation;
        private int _hoveredStationIndex = -1;
        private IDisposable? _animSub;

        public event EventHandler? SelectedStationChanged;

        [Category("Fieldbus")]
        [Description("Access to the underlying industrial fieldbus network model")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public FieldbusNetwork Network => _network;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public FieldbusStation? SelectedStation
        {
            get => _selectedStation;
            set
            {
                if (_selectedStation != value)
                {
                    _selectedStation = value;
                    SelectedStationChanged?.Invoke(this, EventArgs.Empty);
                    Invalidate();
                }
            }
        }

        public ZFieldbusMonitor()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor, true);

            Size = new Size(680, 220);
            BackColor = Color.Transparent;
            Font = new Font("Segoe UI", 8.25f);

            _network.PopulateDemoIndustrialLine();

            ZeroTheme.ThemeChanged += OnThemeChanged;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (!DesignMode)
            {
                _animSub = ZeroAnimationClock.Subscribe((delta, frame) =>
                {
                    if (IsHandleCreated && Visible)
                    {
                        Invalidate();
                    }
                });
            }
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            _animSub?.Dispose();
            _animSub = null;
            base.OnHandleDestroyed(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
            ZeroTheme.ThemeChanged -= OnThemeChanged;
                _animSub?.Dispose();
                _animSub = null;
            }
            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var bounds = ClientRectangle;
            if (bounds.Width < 200 || bounds.Height < 120) return;

            // 1. Frame Background
            using (var bgBrush = new SolidBrush(Color.FromArgb(20, 24, 33)))
            using (var borderPen = new Pen(Color.FromArgb(45, 55, 72), 1.5f))
            {
                using var path = CreateRoundedRect(new Rectangle(0, 0, bounds.Width - 1, bounds.Height - 1), 6);
                g.FillPath(bgBrush, path);
                g.DrawPath(borderPen, path);
            }

            // 2. Header Status & Master Telemetry Strip
            int topH = 44;
            DrawHeader(g, new Rectangle(10, 8, bounds.Width - 20, topH));

            // Check for cable break
            var brokenSegment = _network.LocateCableBreak();
            int alertH = (brokenSegment != null) ? 28 : 0;
            if (brokenSegment != null)
            {
                DrawBreakAlert(g, brokenSegment, new Rectangle(10, topH + 12, bounds.Width - 20, alertH));
            }

            // 3. Fieldbus Line Stations & Bus Segments Area
            int lineTop = topH + 12 + alertH + (alertH > 0 ? 8 : 4);
            int footerH = 26;
            int lineH = bounds.Height - lineTop - footerH - 10;
            int lineW = bounds.Width - 20;

            DrawBusLine(g, new Rectangle(10, lineTop, lineW, lineH), brokenSegment);

            // 4. Telemetry Footer
            int footTop = bounds.Height - footerH - 6;
            DrawFooter(g, new Rectangle(10, footTop, bounds.Width - 20, footerH));
        }

        private void DrawHeader(Graphics g, Rectangle r)
        {
            using (var headerBrush = new SolidBrush(Color.FromArgb(28, 33, 46)))
            using (var headerPen = new Pen(Color.FromArgb(45, 55, 72), 1f))
            {
                using var path = CreateRoundedRect(r, 4);
                g.FillPath(headerBrush, path);
                g.DrawPath(headerPen, path);
            }

            // Protocol Badge
            var protoRect = new Rectangle(r.Left + 8, r.Top + 10, 95, 24);
            using (var protoBrush = new SolidBrush(Color.FromArgb(22, 78, 99)))
            using (var protoPen = new Pen(Color.FromArgb(34, 211, 238), 1.2f))
            {
                using var pPath = CreateRoundedRect(protoRect, 4);
                g.FillPath(protoBrush, pPath);
                g.DrawPath(protoPen, pPath);
            }

            using var protoFont = new Font(Font.FontFamily, 7.5f, FontStyle.Bold);
            using var protoTextBrush = new SolidBrush(Color.FromArgb(34, 211, 238));
            var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString(_network.Protocol.ToString().ToUpperInvariant(), protoFont, protoTextBrush, protoRect, sf);

            // Master and Timing
            using var titleFont = new Font(Font.FontFamily, 8.5f, FontStyle.Bold);
            using var subFont = new Font(Font.FontFamily, 7.5f, FontStyle.Regular);
            using var textBrush = new SolidBrush(Color.FromArgb(241, 245, 249));
            using var subBrush = new SolidBrush(Color.FromArgb(148, 163, 184));
            using var sfText = new StringFormat
            {
                Alignment = StringAlignment.Near,
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter,
                FormatFlags = StringFormatFlags.NoWrap
            };

            int textW = Math.Max(40, r.Right - r.Left - 116);
            g.DrawString($"Master: {_network.MasterName} ({_network.Topology})", titleFont, textBrush, new RectangleF(r.Left + 112, r.Top + 4, textW, 16), sfText);
            g.DrawString($"Bus Cycle: {_network.MasterCycleTimeMs:F1} ms | Jitter: ±{_network.JitterMicroseconds:F0} µs | Stations: {_network.Stations.Count}", subFont, subBrush, new RectangleF(r.Left + 112, r.Top + 22, textW, 16), sfText);
        }

        private void DrawBreakAlert(Graphics g, FieldbusSegment seg, Rectangle r)
        {
            bool blink = ZeroAnimationClock.BlinkFast;
            Color bg = blink ? Color.FromArgb(127, 29, 29) : Color.FromArgb(69, 10, 10);
            using (var brush = new SolidBrush(bg))
            using (var pen = new Pen(Color.FromArgb(239, 68, 68), 1.2f))
            {
                using var path = CreateRoundedRect(r, 4);
                g.FillPath(brush, path);
                g.DrawPath(pen, path);
            }

            using var font = new Font(Font.FontFamily, 7.75f, FontStyle.Bold);
            using var textBrush = new SolidBrush(Color.FromArgb(254, 202, 202));
            var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString($"⚠ PHYSICAL CABLE BREAK DETECTED: Between Station #{seg.FromStationIndex} and Station #{seg.ToStationIndex}!", font, textBrush, r, sf);
        }

        private void DrawBusLine(Graphics g, Rectangle r, FieldbusSegment? brokenSegment)
        {
            if (_network.Stations.Count == 0) return;

            int count = _network.Stations.Count;
            float slotW = (float)r.Width / count;
            float cy = r.Top + (r.Height * 0.42f);

            // Draw connecting segments
            for (int i = 0; i < count - 1; i++)
            {
                float x1 = r.Left + (i * slotW) + (slotW / 2);
                float x2 = r.Left + ((i + 1) * slotW) + (slotW / 2);

                var stA = _network.Stations[i];
                var stB = _network.Stations[i + 1];
                bool isSevered = (brokenSegment != null && 
                                  brokenSegment.FromStationIndex == stA.StationIndex && 
                                  brokenSegment.ToStationIndex == stB.StationIndex);

                if (isSevered)
                {
                    // Broken line: zig-zag red
                    using var breakPen = new Pen(Color.FromArgb(239, 68, 68), 2.5f) { DashStyle = DashStyle.Dash };
                    g.DrawLine(breakPen, x1, cy, (x1 + x2) / 2 - 4, cy - 8);
                    g.DrawLine(breakPen, (x1 + x2) / 2 - 4, cy - 8, (x1 + x2) / 2 + 4, cy + 8);
                    g.DrawLine(breakPen, (x1 + x2) / 2 + 4, cy + 8, x2, cy);

                    // Break indicator cross
                    using var xBrush = new SolidBrush(Color.FromArgb(239, 68, 68));
                    g.FillEllipse(xBrush, (x1 + x2) / 2 - 5, cy - 5, 10, 10);
                }
                else
                {
                    // Healthy communication link
                    using var linkPen = new Pen(Color.FromArgb(56, 189, 248), 2f);
                    g.DrawLine(linkPen, x1, cy, x2, cy);
                }
            }

            // Draw Stations
            for (int i = 0; i < count; i++)
            {
                var st = _network.Stations[i];
                float cx = r.Left + (i * slotW) + (slotW / 2);
                float boxW = Math.Max(28f, Math.Min(84f, slotW - 8));
                float boxH = Math.Max(40f, Math.Min(68f, r.Height - 16));
                var boxRect = new Rectangle((int)(cx - boxW / 2), (int)(cy - boxH / 2), (int)boxW, (int)boxH);

                DrawStationBox(g, st, boxRect);
            }
        }

        private void DrawStationBox(Graphics g, FieldbusStation st, Rectangle r)
        {
            bool isSelected = st == _selectedStation;
            bool isHovered = st.StationIndex == _hoveredStationIndex;

            using (var bgBrush = new SolidBrush(Color.FromArgb(26, 31, 44)))
            using (var borderPen = new Pen(isSelected ? Color.FromArgb(56, 189, 248) : (isHovered ? Color.FromArgb(96, 165, 250) : Color.FromArgb(48, 58, 78)), isSelected ? 2f : 1f))
            {
                using var path = CreateRoundedRect(r, 4);
                g.FillPath(bgBrush, path);
                g.DrawPath(borderPen, path);
            }

            // Status LED
            Color ledColor = st.Status switch
            {
                StationStatus.Normal => Color.FromArgb(34, 197, 94),
                StationStatus.Degraded => Color.FromArgb(245, 158, 11),
                _ => Color.FromArgb(239, 68, 68) // Comm lost
            };

            using (var ledBrush = new SolidBrush(ledColor))
            {
                g.FillEllipse(ledBrush, r.Left + 6, r.Top + 6, 6, 6);
            }

            // Station index label
            using var numFont = new Font(Font.FontFamily, 6.5f, FontStyle.Bold);
            using var numBrush = new SolidBrush(Color.FromArgb(148, 163, 184));
            g.DrawString($"#{st.StationIndex}", numFont, numBrush, r.Left + 16, r.Top + 4);

            // Station name & type
            using var nameFont = new Font(Font.FontFamily, 6.75f, FontStyle.Bold);
            using var subFont = new Font(Font.FontFamily, 6.25f, FontStyle.Regular);
            using var textBrush = new SolidBrush(Color.FromArgb(241, 245, 249));
            using var subBrush = new SolidBrush(Color.FromArgb(148, 163, 184));
            var sf = new StringFormat { Alignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter };

            g.DrawString(st.Name, nameFont, textBrush, new RectangleF(r.Left + 2, r.Top + 18, r.Width - 4, 14), sf);
            g.DrawString(st.DeviceType, subFont, subBrush, new RectangleF(r.Left + 2, r.Top + 32, r.Width - 4, 12), sf);
            g.DrawString(st.Address, subFont, subBrush, new RectangleF(r.Left + 2, r.Top + 46, r.Width - 4, 12), sf);

            // Termination (EOL) resistor tag
            if (st.IsTerminated)
            {
                using var eolBrush = new SolidBrush(Color.FromArgb(234, 88, 12));
                using var eolFont = new Font(Font.FontFamily, 5.75f, FontStyle.Bold);
                var eolRect = new Rectangle(r.Right - 28, r.Bottom - 12, 26, 10);
                g.FillRectangle(eolBrush, eolRect);
                var sfEol = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString("EOL", eolFont, Brushes.White, eolRect, sfEol);
            }
        }

        private void DrawFooter(Graphics g, Rectangle r)
        {
            using var font = new Font(Font.FontFamily, 7.25f, FontStyle.Regular);
            using var boldFont = new Font(Font.FontFamily, 7.25f, FontStyle.Bold);
            using var textBrush = new SolidBrush(Color.FromArgb(226, 232, 240));
            using var subBrush = new SolidBrush(Color.FromArgb(148, 163, 184));

            if (_selectedStation != null)
            {
                var st = _selectedStation;
                g.DrawString($"Selected Station #{st.StationIndex}:", boldFont, textBrush, r.Left, r.Top + 4);
                g.DrawString($"{st.Name} ({st.DeviceType}) | Address: {st.Address} | Response: {st.ResponseTimeMs:F1}ms | CRC Errors: {st.CrcErrorCount}", font, subBrush, r.Left + 140, r.Top + 4);
            }
            else
            {
                g.DrawString("Click on any field station node to view device addressing, diagnostic counters, and bus loop health.", font, subBrush, r.Left, r.Top + 4);
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            int idx = HitTestStation(e.Location);
            if (idx >= 0 && idx < _network.Stations.Count)
            {
                SelectedStation = _network.Stations[idx];
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int idx = HitTestStation(e.Location);
            if (idx != _hoveredStationIndex)
            {
                _hoveredStationIndex = idx;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hoveredStationIndex != -1)
            {
                _hoveredStationIndex = -1;
                Invalidate();
            }
        }

        private int HitTestStation(Point pt)
        {
            if (_network.Stations.Count == 0) return -1;

            int count = _network.Stations.Count;
            float slotW = (float)(Width - 20) / count;
            int topH = 44;
            int alertH = (_network.LocateCableBreak() != null) ? 28 : 0;
            int lineTop = topH + 12 + alertH + (alertH > 0 ? 8 : 4);
            int footerH = 26;
            int lineH = Height - lineTop - footerH - 10;
            float cy = lineTop + (lineH * 0.42f);

            for (int i = 0; i < count; i++)
            {
                float cx = 10 + (i * slotW) + (slotW / 2);
                float boxW = Math.Min(84f, slotW - 12);
                float boxH = Math.Min(68f, lineH - 16);
                var boxRect = new RectangleF(cx - boxW / 2, cy - boxH / 2, boxW, boxH);
                if (boxRect.Contains(pt))
                {
                    return i;
                }
            }

            return -1;
        }

        private static GraphicsPath CreateRoundedRect(Rectangle r, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    
    private void OnThemeChanged(object? sender, EventArgs e) => Invalidate();

}

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZFieldbusMonitor"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("FieldbusMonitor is deprecated and will be removed in 5 release cycles. Please migrate to ZFieldbusMonitor instead.")]
    [ToolboxItem(false)]
    public class FieldbusMonitor : ZFieldbusMonitor
    {
    }

    #endregion
}
