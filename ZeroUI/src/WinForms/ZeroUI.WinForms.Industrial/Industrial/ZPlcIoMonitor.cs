using System;

using System.ComponentModel;
using ZeroUI.WinForms.Base;
using ZeroUI.WinForms.Icons;
using ZeroUI.WinForms.Rendering;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Industrial
{
    public class PlcCoilChangedEventArgs : EventArgs
    {
        public int BitIndex { get; }
        public bool NewState { get; }

        public PlcCoilChangedEventArgs(int bitIndex, bool newState)
        {
            BitIndex = bitIndex;
            NewState = newState;
        }
    }

    /// <summary>
    /// Industrial PLC Digital I/O Bit Monitor for SCADA and automation engineering.
    /// Visualizes 16-bit input bank (DI 00..15) and 16-bit output bank (DO 00..15) with hex register readouts,
    /// LED bit status indicators, and interactive coil toggling.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Industrial & SCADA")]
    [DefaultEvent("OutputCoilChanged")]
    [Description("PLC Digital I/O 16-Bit Monitor with live LED bit registers")]
    [ToolboxBitmap(typeof(ZeroIcons), "ZeroPlcIoMonitor.bmp")]
    public class ZPlcIoMonitor : ControlBase
    {
        private ushort _digitalInputs = 0x0055;  // Default sample bits
        private ushort _digitalOutputs = 0x0007; // Default sample bits
        private bool _allowSimulationClick = true;

        private readonly Rectangle[] _diRects = new Rectangle[16];
        private readonly Rectangle[] _doRects = new Rectangle[16];

        public event EventHandler<PlcCoilChangedEventArgs>? OutputCoilChanged;

        public ZPlcIoMonitor()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor, true);

            Size = new Size(340, 110);
            BackColor = Color.Transparent;
            Font = new Font("Segoe UI", 8f);


        }



        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {

            }
            base.Dispose(disposing);
        }

        [Category("PLC Registers")]
        [DefaultValue((ushort)0x0055)]
        public ushort DigitalInputs
        {
            get => _digitalInputs;
            set { _digitalInputs = value; Invalidate(); }
        }

        [Category("PLC Registers")]
        [DefaultValue((ushort)0x0007)]
        public ushort DigitalOutputs
        {
            get => _digitalOutputs;
            set { _digitalOutputs = value; Invalidate(); }
        }

        [Category("Behavior")]
        [DefaultValue(true)]
        public bool AllowSimulationClick
        {
            get => _allowSimulationClick;
            set => _allowSimulationClick = value;
        }

        public void SetInputBit(int bitIndex, bool state)
        {
            if (bitIndex < 0 || bitIndex > 15) return;
            if (state) _digitalInputs |= (ushort)(1 << bitIndex);
            else _digitalInputs &= (ushort)~(1 << bitIndex);
            Invalidate();
        }

        public void SetOutputBit(int bitIndex, bool state)
        {
            if (bitIndex < 0 || bitIndex > 15) return;
            if (state) _digitalOutputs |= (ushort)(1 << bitIndex);
            else _digitalOutputs &= (ushort)~(1 << bitIndex);
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (!_allowSimulationClick) return;

            // Check click on Digital Outputs
            for (int i = 0; i < 16; i++)
            {
                if (_doRects[i].Contains(e.Location))
                {
                    bool cur = (_digitalOutputs & (1 << i)) != 0;
                    SetOutputBit(i, !cur);
                    OutputCoilChanged?.Invoke(this, new PlcCoilChangedEventArgs(i, !cur));
                    break;
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            int w = Width;
            int h = Height;
            var palette = ZeroTheme.Colors;

            // 1. Enclosure Frame
            using (var brush = new SolidBrush(palette.CardBackground))
            {
                g.FillRectangle(brush, 0, 0, w, h);
            }
            using (var borderPen = new Pen(palette.Border, 1f))
            {
                g.DrawRectangle(borderPen, 0, 0, w - 1, h - 1);
            }

            // 2. Title & Status Bar
            var titleFont = ZeroFontCache.Get("Segoe UI", 8.5f, FontStyle.Bold);
            using (var titleBrush = new SolidBrush(palette.TextPrimary))
            {
                g.DrawString("PLC I/O Bit Matrix (16-DI / 16-DO)", titleFont, titleBrush, 8, 6);
            }

            // 3. Render DI Row (Bank 0: Digital Inputs)
            int rowStartY = 28;
            DrawBitBank(g, 8, rowStartY, "DI:", _digitalInputs, _diRects, Color.FromArgb(52, 211, 153), false);

            // 4. Render DO Row (Bank 1: Digital Outputs)
            DrawBitBank(g, 8, rowStartY + 38, "DO:", _digitalOutputs, _doRects, Color.FromArgb(251, 191, 36), true);
        }

        private void DrawBitBank(Graphics g, int x, int y, string label, ushort register, Rectangle[] rects, Color onColor, bool isOutput)
        {
            int w = Width;
            var palette = ZeroTheme.Colors;
            // Bank Label
            var lblFont = ZeroFontCache.Get("Segoe UI", 8f, FontStyle.Bold);
            using (var lblBrush = new SolidBrush(palette.TextSecondary))
            {
                g.DrawString(label, lblFont, lblBrush, x, y + 4);
            }

            // Hex Word Readout
            string hexText = $"0x{register:X4}";
            var hexFont = ZeroFontCache.Get("Consolas", 8.5f, FontStyle.Bold);
            using (var hexBrush = new SolidBrush(palette.TextPrimary))
            {
                var sz = g.MeasureString(hexText, hexFont);
                g.DrawString(hexText, hexFont, hexBrush, w - sz.Width - 10, y + 4);
            }

            // 16 Bits from 15 down to 0
            int startBitX = x + 34;
            int bitW = 14;
            int bitH = 14;
            int spacing = 3;

            var numFont = ZeroFontCache.Get("Segoe UI", 6f, FontStyle.Regular);
            using var numBrush = new SolidBrush(Color.FromArgb(100, 116, 139));

            for (int i = 15; i >= 0; i--)
            {
                int bitIndex = i;
                bool isBitOn = (register & (1 << bitIndex)) != 0;

                int bx = startBitX + ((15 - i) * (bitW + spacing));
                // Add extra separator between bytes
                if (i < 8) bx += 4;

                rects[bitIndex] = new Rectangle(bx, y + 8, bitW, bitH);

                // LED Ring
                Color c = isBitOn ? onColor : Color.FromArgb(30, 41, 59);
                using (var brush = new SolidBrush(c))
                {
                    g.FillEllipse(brush, rects[bitIndex]);
                }

                // Halo glow when on
                if (isBitOn)
                {
                    using var glowPen = new Pen(Color.FromArgb(80, onColor), 2f);
                    g.DrawEllipse(glowPen, rects[bitIndex].X - 1, rects[bitIndex].Y - 1, bitW + 2, bitH + 2);
                }

                // Bit border
                using (var pen = new Pen(Color.FromArgb(51, 65, 85), 1f))
                {
                    g.DrawEllipse(pen, rects[bitIndex]);
                }

                // Bit number (0..15) above
                if (i % 2 == 0 || i == 15)
                {
                    string num = $"{i}";
                    var nsz = g.MeasureString(num, numFont);
                    g.DrawString(num, numFont, numBrush, bx + (bitW - nsz.Width) / 2, y - 4);
                }
            }
        }
    }

    /// <summary>
    /// Legacy alias for <see cref="ZPlcIoMonitor"/>.
    /// Preserved for backward compatibility.
    /// </summary>
    [Obsolete("ZeroPlcIoMonitor is deprecated and will be removed in 5 release cycles. Please migrate to ZPlcIoMonitor instead.")]
    [ToolboxItem(false)]
    public class ZeroPlcIoMonitor : ZPlcIoMonitor
    {
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZPlcIoMonitor"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("PlcIoMonitor is deprecated and will be removed in 5 release cycles. Please migrate to ZPlcIoMonitor instead.")]
    [ToolboxItem(false)]
    public class PlcIoMonitor : ZPlcIoMonitor
    {
    }

    #endregion
}
