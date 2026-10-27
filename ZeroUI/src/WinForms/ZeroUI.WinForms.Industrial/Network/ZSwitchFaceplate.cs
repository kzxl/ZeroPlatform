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
    /// High-Density Physical Port Matrix & Patch Panel Control.
    /// Emulates hardware switch front-panels (RJ45 + SFP+ cages) with synchronized
    /// Link/Speed and Activity LEDs, STP states, and live cable diagnostics.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Network & Infrastructure")]
    [DefaultEvent("SelectedPortChanged")]
    [Description("High-density Network Switch & Patch Panel Faceplate with animated LEDs")]
    public class ZSwitchFaceplate : Control
    {
        private readonly SwitchPortLayout _layout;
        private SwitchPort? _selectedPort;
        private int _hoveredPortIndex = -1;
        private IDisposable? _animSub;

        public event EventHandler? SelectedPortChanged;

        [Category("Switch")]
        [Description("Access to the underlying switch port layout model")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public SwitchPortLayout PortLayout => _layout;

        [Category("Switch")]
        public string ModelName { get; set; } = "ZeroSwitch 24G-4X";

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public SwitchPort? SelectedPort
        {
            get => _selectedPort;
            set
            {
                if (_selectedPort != value)
                {
                    _selectedPort = value;
                    SelectedPortChanged?.Invoke(this, EventArgs.Empty);
                    Invalidate();
                }
            }
        }

        public ZSwitchFaceplate()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor, true);

            _layout = new SwitchPortLayout(24, 4);
            Size = new Size(620, 150);
            BackColor = Color.Transparent;
            Font = new Font("Segoe UI", 8.25f);

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
            if (bounds.Width < 200 || bounds.Height < 80) return;

            // 1. Brushed Aluminum Chassis (1U rackmount faceplate)
            using (var chassisBrush = new SolidBrush(Color.FromArgb(24, 28, 38)))
            using (var chassisPen = new Pen(Color.FromArgb(48, 56, 74), 1.5f))
            {
                using var path = CreateRoundedRect(new Rectangle(0, 0, bounds.Width - 1, bounds.Height - 1), 6);
                g.FillPath(chassisBrush, path);
                g.DrawPath(chassisPen, path);
            }

            // Rack mounting ears (left & right flanges)
            int earWidth = 24;
            DrawRackEar(g, new Rectangle(0, 0, earWidth, bounds.Height), isLeft: true);
            DrawRackEar(g, new Rectangle(bounds.Width - earWidth, 0, earWidth, bounds.Height), isLeft: false);

            int panelLeft = earWidth + 6;
            int panelRight = bounds.Width - earWidth - 6;
            int panelWidth = panelRight - panelLeft;

            // 2. Chassis Branding & Master System LEDs (PWR, SYS, PoE)
            int topBarHeight = 26;
            DrawBrandingBar(g, new Rectangle(panelLeft, 6, panelWidth, topBarHeight));

            // 3. Port Matrix Area (RJ45 + SFP+ uplink cages)
            int portAreaTop = topBarHeight + 10;
            int portAreaHeight = Math.Max(40, bounds.Height - portAreaTop - 34);
            DrawPortMatrix(g, new Rectangle(panelLeft, portAreaTop, panelWidth, portAreaHeight));

            // 4. Diagnostic Inspection Strip (Bottom)
            int hudTop = bounds.Height - 28;
            DrawInspectionHud(g, new Rectangle(panelLeft, hudTop, panelWidth, 24));
        }

        private void DrawRackEar(Graphics g, Rectangle r, bool isLeft)
        {
            using var earBrush = new SolidBrush(Color.FromArgb(18, 21, 29));
            using var earPen = new Pen(Color.FromArgb(40, 48, 64), 1f);
            g.FillRectangle(earBrush, r);
            g.DrawRectangle(earPen, r);

            // Mounting screw oval hole
            using var holeBrush = new SolidBrush(Color.FromArgb(10, 12, 16));
            using var holePen = new Pen(Color.FromArgb(64, 75, 96), 1f);
            int holeW = 8;
            int holeH = 14;
            int holeX = isLeft ? r.Left + 8 : r.Right - 16;
            int holeY = r.Top + (r.Height / 2) - (holeH / 2);
            var holeRect = new Rectangle(holeX, holeY, holeW, holeH);
            g.FillEllipse(holeBrush, holeRect);
            g.DrawEllipse(holePen, holeRect);
        }

        private void DrawBrandingBar(Graphics g, Rectangle r)
        {
            using var font = new Font(Font.FontFamily, 8f, FontStyle.Bold);
            using var textBrush = new SolidBrush(Color.FromArgb(226, 232, 240));
            g.DrawString(ModelName, font, textBrush, r.Left + 4, r.Top + 4);

            // Master LEDs: PWR (Green), SYS (Green), PoE (Amber)
            int ledX = r.Left + 160;
            DrawSystemLed(g, "PWR", Color.FromArgb(34, 197, 94), ref ledX, r.Top + 5);
            DrawSystemLed(g, "SYS", Color.FromArgb(34, 197, 94), ref ledX, r.Top + 5);
            DrawSystemLed(g, "PoE", Color.FromArgb(245, 158, 11), ref ledX, r.Top + 5);

            // Right side active port summary
            int active = _layout.CountActiveLinks();
            double poeW = _layout.CalculateTotalPoeWatts();
            using var subFont = new Font(Font.FontFamily, 7.5f, FontStyle.Regular);
            using var subBrush = new SolidBrush(Color.FromArgb(148, 163, 184));
            string summary = $"Active: {active}/{_layout.Ports.Count} | PoE: {poeW:F1}W/{_layout.MaxPoeBudgetWatts:F0}W";
            var sf = new StringFormat { Alignment = StringAlignment.Far };
            g.DrawString(summary, subFont, subBrush, new RectangleF(r.Left, r.Top + 4, r.Width - 6, r.Height), sf);
        }

        private void DrawSystemLed(Graphics g, string label, Color ledColor, ref int x, int y)
        {
            using (var brush = new SolidBrush(ledColor))
            {
                g.FillEllipse(brush, x, y + 2, 6, 6);
            }
            using var font = new Font(Font.FontFamily, 6.75f, FontStyle.Regular);
            using var textBrush = new SolidBrush(Color.FromArgb(148, 163, 184));
            g.DrawString(label, font, textBrush, x + 9, y);
            x += 42;
        }

        private void DrawPortMatrix(Graphics g, Rectangle r)
        {
            int rj45Cols = (_layout.Rj45Count + 1) / 2;
            int sfpCols = (_layout.SfpCount + 1) / 2;
            int totalCols = rj45Cols + sfpCols;

            float slotWidth = Math.Min(36f, (float)r.Width / (totalCols + 0.5f));
            float slotHeight = r.Height / 2.1f;

            // Draw RJ45 ports (interleaved dual rows: top = odd, bottom = even)
            for (int p = 1; p <= _layout.Rj45Count; p++)
            {
                SwitchPortLayout.GetPortGridCoordinates(p, _layout.Rj45Count, out int row, out int col, out _);
                float x = r.Left + (col * slotWidth) + 4;
                float y = r.Top + (row * slotHeight) + 2;
                var portRect = new Rectangle((int)x, (int)y, (int)slotWidth - 4, (int)slotHeight - 4);

                var port = _layout.FindPort(p);
                if (port != null)
                {
                    DrawPortReceptacle(g, port, portRect, isSfp: false);
                }
            }

            // Draw SFP cages divider line and SFP ports
            float sfpStartX = r.Left + (rj45Cols * slotWidth) + 12;
            using (var divPen = new Pen(Color.FromArgb(50, 60, 80), 1f))
            {
                g.DrawLine(divPen, sfpStartX - 6, r.Top + 2, sfpStartX - 6, r.Bottom - 2);
            }

            for (int s = 1; s <= _layout.SfpCount; s++)
            {
                int pIndex = _layout.Rj45Count + s;
                SwitchPortLayout.GetPortGridCoordinates(pIndex, _layout.Rj45Count, out int row, out int col, out _);
                float x = sfpStartX + (col * slotWidth);
                float y = r.Top + (row * slotHeight) + 2;
                var portRect = new Rectangle((int)x, (int)y, (int)slotWidth - 4, (int)slotHeight - 4);

                var port = _layout.FindPort(pIndex);
                if (port != null)
                {
                    DrawPortReceptacle(g, port, portRect, isSfp: true);
                }
            }
        }

        private void DrawPortReceptacle(Graphics g, SwitchPort port, Rectangle r, bool isSfp)
        {
            bool isSelected = port == _selectedPort;
            bool isHovered = port.PortIndex == _hoveredPortIndex;

            // Metallic jack outer enclosure
            using (var jackBrush = new SolidBrush(isSfp ? Color.FromArgb(32, 40, 56) : Color.FromArgb(20, 24, 34)))
            using (var jackPen = new Pen(isSelected ? Color.FromArgb(56, 189, 248) : (isHovered ? Color.FromArgb(96, 165, 250) : Color.FromArgb(52, 62, 82)), isSelected ? 1.5f : 1f))
            {
                using var path = CreateRoundedRect(r, 3);
                g.FillPath(jackBrush, path);
                g.DrawPath(jackPen, path);
            }

            // Port number label
            using var numFont = new Font(Font.FontFamily, 6.25f, FontStyle.Bold);
            using var numBrush = new SolidBrush(Color.FromArgb(148, 163, 184));
            var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString(port.PortIndex.ToString(), numFont, numBrush, new RectangleF(r.Left, r.Top + 2, r.Width, 10), sf);

            // Dual LEDs: Link/Speed (Left) and Activity (Right)
            int ledY = r.Bottom - 8;
            int ledL = r.Left + 4;
            int ledR = r.Right - 8;

            // Link / Speed LED
            Color linkColor = Color.FromArgb(30, 35, 48); // off
            if (port.IsLinkUp)
            {
                linkColor = port.Speed switch
                {
                    PortSpeed.Speed10G or PortSpeed.Speed25G => Color.FromArgb(56, 189, 248), // Cyan for 10G
                    PortSpeed.Speed1G => Color.FromArgb(34, 197, 94),                        // Green for 1G
                    PortSpeed.Speed100M => Color.FromArgb(245, 158, 11),                     // Amber for 100M
                    _ => Color.FromArgb(100, 116, 139)
                };
            }

            // Activity LED (blinks when traffic present)
            Color actColor = Color.FromArgb(30, 35, 48);
            if (port.IsLinkUp && port.HasTraffic)
            {
                bool blink = ZeroAnimationClock.BlinkFast;
                actColor = blink ? Color.FromArgb(34, 197, 94) : Color.FromArgb(16, 85, 40);
            }

            using (var linkBrush = new SolidBrush(linkColor))
            using (var actBrush = new SolidBrush(actColor))
            {
                g.FillRectangle(linkBrush, ledL, ledY, 4, 3);
                g.FillRectangle(actBrush, ledR, ledY, 4, 3);
            }

            // Port connector clip shape
            int clipW = r.Width - 12;
            int clipH = Math.Max(6, r.Height - 20);
            var clipRect = new Rectangle(r.Left + 6, r.Top + 12, clipW, clipH);
            using var clipBrush = new SolidBrush(Color.FromArgb(12, 14, 20));
            using var clipPen = new Pen(Color.FromArgb(40, 48, 64), 1f);
            g.FillRectangle(clipBrush, clipRect);
            g.DrawRectangle(clipPen, clipRect);
        }

        private void DrawInspectionHud(Graphics g, Rectangle r)
        {
            using (var hudBrush = new SolidBrush(Color.FromArgb(18, 22, 30)))
            using (var hudPen = new Pen(Color.FromArgb(40, 48, 64), 1f))
            {
                using var path = CreateRoundedRect(r, 4);
                g.FillPath(hudBrush, path);
                g.DrawPath(hudPen, path);
            }

            using var font = new Font(Font.FontFamily, 7.5f, FontStyle.Regular);
            using var boldFont = new Font(Font.FontFamily, 7.5f, FontStyle.Bold);
            using var textBrush = new SolidBrush(Color.FromArgb(226, 232, 240));
            using var subBrush = new SolidBrush(Color.FromArgb(148, 163, 184));

            if (_selectedPort != null)
            {
                var p = _selectedPort;
                string typeStr = p.PortType == SwitchPortType.RJ45 ? "10/100/1000Base-T" : "10GBase-SR SFP+";
                string statusStr = p.IsLinkUp ? $"LINK UP ({p.Speed})" : "LINK DOWN";

                g.DrawString($"Port {p.PortIndex}:", boldFont, textBrush, r.Left + 8, r.Top + 4);
                g.DrawString($"{typeStr} | {statusStr} | VLAN {p.VlanId}{(p.IsTagged ? " (Tagged)" : "")} | PoE: {p.PoeWatts:F1}W | TDR: {p.CableLengthMeters:F1}m (OK)", font, subBrush, r.Left + 62, r.Top + 4);
            }
            else
            {
                g.DrawString("Click on any port to inspect VLAN, PoE power consumption, link rate and cable TDR status.", font, subBrush, r.Left + 8, r.Top + 4);
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            int port = HitTestPort(e.Location);
            if (port > 0)
            {
                SelectedPort = _layout.FindPort(port);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int port = HitTestPort(e.Location);
            if (port != _hoveredPortIndex)
            {
                _hoveredPortIndex = port;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hoveredPortIndex != -1)
            {
                _hoveredPortIndex = -1;
                Invalidate();
            }
        }

        private int HitTestPort(Point pt)
        {
            int earWidth = 24;
            int panelLeft = earWidth + 6;
            int topBarHeight = 26;
            int portAreaTop = topBarHeight + 10;
            int portAreaHeight = Math.Max(40, Height - portAreaTop - 34);

            int rj45Cols = (_layout.Rj45Count + 1) / 2;
            int sfpCols = (_layout.SfpCount + 1) / 2;
            int totalCols = rj45Cols + sfpCols;
            float slotWidth = Math.Min(36f, (float)(Width - (earWidth * 2) - 12) / (totalCols + 0.5f));
            float slotHeight = portAreaHeight / 2.1f;

            for (int p = 1; p <= _layout.Ports.Count; p++)
            {
                SwitchPortLayout.GetPortGridCoordinates(p, _layout.Rj45Count, out int row, out int col, out bool isSfp);
                float x = isSfp ? (panelLeft + (rj45Cols * slotWidth) + 12 + (col * slotWidth)) : (panelLeft + (col * slotWidth) + 4);
                float y = portAreaTop + (row * slotHeight) + 2;
                var rect = new RectangleF(x, y, slotWidth - 4, slotHeight - 4);
                if (rect.Contains(pt))
                {
                    return p;
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
    
    private void OnThemeChanged(object sender, EventArgs e) => Invalidate();

}

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZSwitchFaceplate"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("SwitchFaceplate is deprecated and will be removed in 5 release cycles. Please migrate to ZSwitchFaceplate instead.")]
    [ToolboxItem(false)]
    public class SwitchFaceplate : ZSwitchFaceplate
    {
    }

    #endregion
}
