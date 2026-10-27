using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using ZeroUI.Core.Rendering;
using ZeroUI.Core.Scada;
using ZeroUI.WinForms.Native;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Industrial
{
    /// <summary>
    /// High-performance non-modal industrial PID Loop Tuning Faceplate Flyout.
    /// Features simultaneous PV/SP/MV comparative bargraphs, interactive loop mode switching (Auto/Man/Cas),
    /// a 60 FPS real-time 3-pen micro-trend chart, and interactive PID parameter tuning inputs (Kp, Ti, Td).
    /// </summary>
    public class ZPidFlyout : Form, IScadaBindable, IAnimationFrameListener
    {
        private string _loopTag = "PIC-101";
        private string _loopDescription = "Boiler Steam Header Pressure";
        private string _engineeringUnit = "PSI";

        // Process variables
        private double _processVariable = 48.2;
        private double _setPoint = 50.0;
        private double _manipulatedVariable = 62.0; // 0-100%
        private ZeroPidMode _mode = ZeroPidMode.Auto;

        // Tuning parameters
        private double _kp = 1.25;
        private double _ti = 18.0;
        private double _td = 2.5;

        // Rolling 120-sample micro-trend circular buffers
        private const int TrendCapacity = 120;
        private readonly float[] _trendPv = new float[TrendCapacity];
        private readonly float[] _trendSp = new float[TrendCapacity];
        private readonly float[] _trendMv = new float[TrendCapacity];
        private int _trendHead = 0;

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HTCAPTION = 0x2;

        // Interaction hit boxes
        private Rectangle _headerRect;
        private Rectangle _closeBtnRect;
        private Rectangle _btnAutoRect;
        private Rectangle _btnManRect;
        private Rectangle _btnCasRect;
        private Rectangle _btnSpMinusRect;
        private Rectangle _btnSpPlusRect;
        private Rectangle _btnKpMinusRect;
        private Rectangle _btnKpPlusRect;
        private Rectangle _btnTiMinusRect;
        private Rectangle _btnTiPlusRect;
        private Rectangle _btnTdMinusRect;
        private Rectangle _btnTdPlusRect;

        public string? BoundTagPath { get; set; }

        public string LoopTag
        {
            get => _loopTag;
            set { _loopTag = value ?? ""; Invalidate(); }
        }

        public string LoopDescription
        {
            get => _loopDescription;
            set { _loopDescription = value ?? ""; Invalidate(); }
        }

        public double ProcessVariable
        {
            get => _processVariable;
            set { _processVariable = value; Invalidate(); }
        }

        public double SetPoint
        {
            get => _setPoint;
            set { _setPoint = Math.Max(0, Math.Min(100, value)); Invalidate(); }
        }

        public double ManipulatedVariable
        {
            get => _manipulatedVariable;
            set { _manipulatedVariable = Math.Max(0, Math.Min(100, value)); Invalidate(); }
        }

        public double Kp
        {
            get => _kp;
            set { _kp = Math.Max(0.01, Math.Min(50.0, value)); Invalidate(); }
        }

        public double Ti
        {
            get => _ti;
            set { _ti = Math.Max(0.1, Math.Min(300.0, value)); Invalidate(); }
        }

        public double Td
        {
            get => _td;
            set { _td = Math.Max(0.0, Math.Min(60.0, value)); Invalidate(); }
        }

        public event EventHandler? ParametersChanged;

        public ZPidFlyout()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            DoubleBuffered = true;
            Size = new Size(580, 350);
            BackColor = Color.FromArgb(15, 23, 42); // Dark slate

            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw, true);

            // Pre-fill trend buffer
            for (int i = 0; i < TrendCapacity; i++)
            {
                _trendPv[i] = (float)_processVariable;
                _trendSp[i] = (float)_setPoint;
                _trendMv[i] = (float)_manipulatedVariable;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ZeroAnimationClock.Subscribe(this);
            ZeroTagEngine.RegisterBindable(this);
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            base.OnHandleDestroyed(e);
            ZeroAnimationClock.Unsubscribe(this);
            ZeroTagEngine.UnregisterBindable(this);
        }


        public void OnAnimationFrame(double deltaSeconds, long frameCount)
        {
            if (!Visible || IsDisposed) return;

            // Simulated control loop dynamics when in Auto mode
            if (_mode == ZeroPidMode.Auto)
            {
                double error = _setPoint - _processVariable;
                double pTerm = _kp * error;
                double dMv = (pTerm * deltaSeconds * 2.0);
                _manipulatedVariable = Math.Max(0, Math.Min(100, _manipulatedVariable + dMv));
                _processVariable += (_manipulatedVariable - 50.0) * deltaSeconds * 0.8;
            }

            // Push sample into micro-trend buffer
            _trendPv[_trendHead] = (float)_processVariable;
            _trendSp[_trendHead] = (float)_setPoint;
            _trendMv[_trendHead] = (float)_manipulatedVariable;
            _trendHead = (_trendHead + 1) % TrendCapacity;

            Invalidate();
        }

        public void OnTagValueChanged(IScadaTag tag)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => OnTagValueChanged(tag)));
                return;
            }

            if (tag.TagPath.EndsWith(".PV", StringComparison.OrdinalIgnoreCase) && tag.Value is double pv)
            {
                ProcessVariable = pv;
            }
            else if (tag.TagPath.EndsWith(".SP", StringComparison.OrdinalIgnoreCase) && tag.Value is double sp)
            {
                SetPoint = sp;
            }
            else if (tag.TagPath.EndsWith(".MV", StringComparison.OrdinalIgnoreCase) && tag.Value is double mv)
            {
                ManipulatedVariable = mv;
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            if (_closeBtnRect.Contains(e.Location))
            {
                Hide();
                return;
            }

            // Header dragging
            if (_headerRect.Contains(e.Location) && e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, (IntPtr)HTCAPTION, IntPtr.Zero);
                return;
            }


            // Mode switching
            if (_btnAutoRect.Contains(e.Location)) { _mode = ZeroPidMode.Auto; Invalidate(); }
            else if (_btnManRect.Contains(e.Location)) { _mode = ZeroPidMode.Manual; Invalidate(); }
            else if (_btnCasRect.Contains(e.Location)) { _mode = ZeroPidMode.Cascade; Invalidate(); }

            // SP Adjustments
            else if (_btnSpMinusRect.Contains(e.Location)) { SetPoint = Math.Max(0, _setPoint - 1.0); }
            else if (_btnSpPlusRect.Contains(e.Location)) { SetPoint = Math.Min(100, _setPoint + 1.0); }

            // Kp tuning
            else if (_btnKpMinusRect.Contains(e.Location)) { Kp -= 0.05; ParametersChanged?.Invoke(this, EventArgs.Empty); }
            else if (_btnKpPlusRect.Contains(e.Location)) { Kp += 0.05; ParametersChanged?.Invoke(this, EventArgs.Empty); }

            // Ti tuning
            else if (_btnTiMinusRect.Contains(e.Location)) { Ti -= 0.5; ParametersChanged?.Invoke(this, EventArgs.Empty); }
            else if (_btnTiPlusRect.Contains(e.Location)) { Ti += 0.5; ParametersChanged?.Invoke(this, EventArgs.Empty); }

            // Td tuning
            else if (_btnTdMinusRect.Contains(e.Location)) { Td -= 0.1; ParametersChanged?.Invoke(this, EventArgs.Empty); }
            else if (_btnTdPlusRect.Contains(e.Location)) { Td += 0.1; ParametersChanged?.Invoke(this, EventArgs.Empty); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var palette = ZeroTheme.Colors;
            bool isDark = ZeroTheme.IsDark;

            // 1. Bezel Outer Border & Drop Shadow
            var borderRect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var penBorder = new Pen(palette.Primary, 1.5f))
            {
                g.DrawRectangle(penBorder, borderRect);
            }

            // 2. Header Bar
            _headerRect = new Rectangle(0, 0, Width, 36);
            using (var brushHeader = new SolidBrush(Color.FromArgb(30, 41, 59)))
            {
                g.FillRectangle(brushHeader, _headerRect);
            }
            using (var penHeader = new Pen(palette.Border, 1f))
            {
                g.DrawLine(penHeader, 0, 36, Width, 36);
            }

            using var fontTag = new Font("Segoe UI", 10f, FontStyle.Bold);
            using var fontSub = new Font("Segoe UI", 7.5f, FontStyle.Regular);
            using var brushWhite = new SolidBrush(Color.White);
            using var brushMuted = new SolidBrush(Color.FromArgb(148, 163, 184));

            g.DrawString($"PID FACEPLATE — {_loopTag}", fontTag, brushWhite, 12, 3);
            g.DrawString($"{_loopDescription} ({_engineeringUnit})", fontSub, brushMuted, 12, 20);

            // Close button [X]
            _closeBtnRect = new Rectangle(Width - 32, 6, 24, 24);
            using (var penClose = new Pen(Color.FromArgb(203, 213, 225), 1.8f))
            {
                g.DrawLine(penClose, _closeBtnRect.X + 6, _closeBtnRect.Y + 6, _closeBtnRect.Right - 6, _closeBtnRect.Bottom - 6);
                g.DrawLine(penClose, _closeBtnRect.Right - 6, _closeBtnRect.Y + 6, _closeBtnRect.X + 6, _closeBtnRect.Bottom - 6);
            }

            // 3. Left Section: Mode buttons + Dual Bargraph (PV / SP)
            int leftW = 250;
            int modeY = 46;
            _btnAutoRect = new Rectangle(12, modeY, 48, 22);
            _btnManRect = new Rectangle(64, modeY, 48, 22);
            _btnCasRect = new Rectangle(116, modeY, 48, 22);

            DrawModeBtn(g, _btnAutoRect, "AUTO", _mode == ZeroPidMode.Auto, palette);
            DrawModeBtn(g, _btnManRect, "MAN", _mode == ZeroPidMode.Manual, palette);
            DrawModeBtn(g, _btnCasRect, "CAS", _mode == ZeroPidMode.Cascade, palette);

            _btnSpMinusRect = new Rectangle(190, modeY, 22, 22);
            _btnSpPlusRect = new Rectangle(216, modeY, 22, 22);
            DrawStepBtn(g, _btnSpMinusRect, "-");
            DrawStepBtn(g, _btnSpPlusRect, "+");

            // Dual Vertical Bargraph
            int barY = 78;
            int barH = 180;
            int barW = 32;
            int pvX = 42;
            int spX = 110;

            DrawBargraph(g, pvX, barY, barW, barH, _processVariable, 0, 100, Color.FromArgb(6, 182, 212), "PV", palette);
            DrawBargraph(g, spX, barY, barW, barH, _setPoint, 0, 100, Color.FromArgb(245, 158, 11), "SP", palette);

            // Right-of-bars quick info
            int statsX = 160;
            double dev = _processVariable - _setPoint;
            Color devCol = Math.Abs(dev) > 3.0 ? palette.Danger : palette.Success;
            using var fontSmall = new Font("Segoe UI", 7.5f, FontStyle.Regular);
            g.DrawString($"DEV: {dev:+0.0;-0.0;0.0}", fontSmall, new SolidBrush(devCol), statsX, barY + 10);
            g.DrawString($"OUT: {_manipulatedVariable:0.0}%", fontSmall, brushWhite, statsX, barY + 30);

            // Manipulated Variable (MV) Horizontal Gauge at bottom of left pane
            int mvY = barY + barH + 42;
            var mvRect = new Rectangle(12, mvY, leftW - 24, 16);
            using (var brushTrack = new SolidBrush(Color.FromArgb(30, 41, 59)))
            using (var penTrack = new Pen(palette.Border, 1f))
            {
                g.FillRectangle(brushTrack, mvRect);
                g.DrawRectangle(penTrack, mvRect);
            }
            int mvFillW = (int)((mvRect.Width - 2) * (_manipulatedVariable / 100.0));
            if (mvFillW > 0)
            {
                using var brushMv = new SolidBrush(Color.FromArgb(236, 72, 153)); // Magenta
                g.FillRectangle(brushMv, mvRect.X + 1, mvRect.Y + 1, mvFillW, mvRect.Height - 1);
            }
            using var fontMv = new Font("Segoe UI", 6.5f, FontStyle.Bold);
            var sfCenter = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString($"MV: {_manipulatedVariable:0.0}%", fontMv, brushWhite, mvRect, sfCenter);

            // Vertical divider between left and right panes
            using (var penDiv = new Pen(Color.FromArgb(47, 63, 90), 1f))
            {
                g.DrawLine(penDiv, leftW, 36, leftW, Height);
            }

            // 4. Right Section: Real-Time 3-Pen Micro-Trend Chart (60 FPS)
            int chartX = leftW + 12;
            int chartY = 46;
            int chartW = Width - chartX - 14;
            int chartH = 175;

            var chartRect = new Rectangle(chartX, chartY, chartW, chartH);
            using (var brushChartBg = new SolidBrush(Color.FromArgb(10, 15, 30)))
            using (var penChartBorder = new Pen(Color.FromArgb(51, 65, 85), 1f))
            {
                g.FillRectangle(brushChartBg, chartRect);
                g.DrawRectangle(penChartBorder, chartRect);
            }

            // Grid lines
            using (var penGrid = new Pen(Color.FromArgb(25, 35, 55), 1f) { DashStyle = DashStyle.Dot })
            {
                for (int yTick = 1; yTick <= 3; yTick++)
                {
                    float gy = chartRect.Top + (chartRect.Height * yTick / 4f);
                    g.DrawLine(penGrid, chartRect.Left, gy, chartRect.Right, gy);
                }
            }

            // Render 3 pens (PV = Cyan, SP = Yellow dashed, MV = Magenta)
            RenderTrendPen(g, chartRect, _trendPv, Color.FromArgb(6, 182, 212), DashStyle.Solid, 1.8f);
            RenderTrendPen(g, chartRect, _trendSp, Color.FromArgb(245, 158, 11), DashStyle.Dash, 1.4f);
            RenderTrendPen(g, chartRect, _trendMv, Color.FromArgb(236, 72, 153), DashStyle.Solid, 1.2f);

            // Chart Legend Strip
            using var fontLeg = new Font("Segoe UI", 7f, FontStyle.Bold);
            DrawPenLegend(g, chartX + 6, chartY + 6, "PV (Cyan)", Color.FromArgb(6, 182, 212), fontLeg);
            DrawPenLegend(g, chartX + 76, chartY + 6, "SP (Amber)", Color.FromArgb(245, 158, 11), fontLeg);
            DrawPenLegend(g, chartX + 152, chartY + 6, "MV (Magenta)", Color.FromArgb(236, 72, 153), fontLeg);

            // 5. PID Loop Tuning Inputs (Kp, Ti, Td)
            int tuneY = chartY + chartH + 12;
            int rowH = 26;

            DrawTuningRow(g, chartX, tuneY, "Kp (Gain)", $"{_kp:0.00}", ref _btnKpMinusRect, ref _btnKpPlusRect, brushWhite, palette);
            DrawTuningRow(g, chartX, tuneY + rowH, "Ti (Reset)", $"{_ti:0.0} s", ref _btnTiMinusRect, ref _btnTiPlusRect, brushWhite, palette);
            DrawTuningRow(g, chartX, tuneY + (rowH * 2), "Td (Rate)", $"{_td:0.0} s", ref _btnTdMinusRect, ref _btnTdPlusRect, brushWhite, palette);
        }

        private void RenderTrendPen(Graphics g, Rectangle r, float[] buffer, Color color, DashStyle style, float strokeWidth)
        {
            var pts = new PointF[TrendCapacity];
            float stepX = (float)r.Width / (TrendCapacity - 1);

            for (int i = 0; i < TrendCapacity; i++)
            {
                int bufferIdx = (_trendHead + i) % TrendCapacity;
                float val = buffer[bufferIdx];
                float x = r.Left + (i * stepX);
                float y = r.Bottom - (float)(Math.Max(0, Math.Min(100, val)) / 100.0 * r.Height);
                pts[i] = new PointF(x, y);
            }

            using var pen = new Pen(color, strokeWidth) { DashStyle = style };
            g.DrawLines(pen, pts);
        }

        private static void DrawPenLegend(Graphics g, int x, int y, string text, Color color, Font font)
        {
            using var brushCol = new SolidBrush(color);
            using var textBrush = new SolidBrush(Color.White);
            g.FillEllipse(brushCol, x, y + 2, 6, 6);
            g.DrawString(text, font, textBrush, x + 10, y);
        }

        private static void DrawBargraph(Graphics g, int x, int y, int w, int h, double val, double min, double max, Color col, string label, ZeroThemePalette palette)
        {
            var track = new Rectangle(x, y, w, h);
            using (var brushTrack = new SolidBrush(Color.FromArgb(30, 41, 59)))
            using (var penTrack = new Pen(palette.Border, 1f))
            {
                g.FillRectangle(brushTrack, track);
                g.DrawRectangle(penTrack, track);
            }

            double ratio = (Math.Max(min, Math.Min(max, val)) - min) / (max - min);
            int fillH = (int)(h * ratio);
            if (fillH > 0)
            {
                int fillY = y + h - fillH;
                using var brushFill = new SolidBrush(col);
                g.FillRectangle(brushFill, x + 1, fillY, w - 1, fillH);
            }

            using var fontTag = new Font("Segoe UI", 7.5f, FontStyle.Bold);
            using var fontVal = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            using var brushText = new SolidBrush(Color.White);
            var sf = new StringFormat { Alignment = StringAlignment.Center };

            g.DrawString(label, fontTag, new SolidBrush(col), x + w / 2, y + h + 4, sf);
            g.DrawString($"{val:0.0}", fontVal, brushText, x + w / 2, y + h + 18, sf);
        }

        private static void DrawTuningRow(Graphics g, int x, int y, string name, string valStr, ref Rectangle btnMinus, ref Rectangle btnPlus, Brush textBrush, ZeroThemePalette palette)
        {
            using var fontLbl = new Font("Segoe UI", 7.5f, FontStyle.Regular);
            using var fontVal = new Font("Segoe UI", 8f, FontStyle.Bold);

            g.DrawString(name, fontLbl, textBrush, x, y + 3);

            btnMinus = new Rectangle(x + 110, y, 22, 20);
            btnPlus = new Rectangle(x + 220, y, 22, 20);
            var valBox = new Rectangle(x + 136, y, 80, 20);

            DrawStepBtn(g, btnMinus, "-");
            DrawStepBtn(g, btnPlus, "+");

            using (var bgBrush = new SolidBrush(Color.FromArgb(30, 41, 59)))
            using (var penBox = new Pen(palette.Border, 1f))
            {
                g.FillRectangle(bgBrush, valBox);
                g.DrawRectangle(penBox, valBox);
            }

            var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString(valStr, fontVal, textBrush, valBox, sf);
        }

        private static void DrawModeBtn(Graphics g, Rectangle r, string text, bool sel, ZeroThemePalette pal)
        {
            Color bg = sel ? pal.Primary : Color.FromArgb(30, 41, 59);
            using var brush = new SolidBrush(bg);
            using var pen = new Pen(sel ? pal.Primary : pal.Border, 1f);
            using var font = new Font("Segoe UI", 7f, FontStyle.Bold);
            using var textBrush = new SolidBrush(Color.White);
            var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

            g.FillRectangle(brush, r);
            g.DrawRectangle(pen, r);
            g.DrawString(text, font, textBrush, r, sf);
        }

        private static void DrawStepBtn(Graphics g, Rectangle r, string text)
        {
            using var brush = new SolidBrush(Color.FromArgb(51, 65, 85));
            using var pen = new Pen(Color.FromArgb(71, 85, 105), 1f);
            using var font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            using var textBrush = new SolidBrush(Color.White);
            var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

            g.FillRectangle(brush, r);
            g.DrawRectangle(pen, r);
            g.DrawString(text, font, textBrush, r, sf);
        }

        /// <summary>
        /// Displays the PID faceplate flyout adjacent to the target control.
        /// </summary>
        public static ZPidFlyout ShowFlyout(
            Control owner,
            string loopTag,
            string description = "",
            double initialSp = 50.0,
            double initialPv = 48.0,
            double initialMv = 60.0)
        {
            var flyout = new ZPidFlyout
            {
                LoopTag = loopTag,
                LoopDescription = description,
                SetPoint = initialSp,
                ProcessVariable = initialPv,
                ManipulatedVariable = initialMv
            };

            if (owner != null && owner.IsHandleCreated)
            {
                Point screenPt = owner.PointToScreen(new Point(owner.Width + 8, 0));
                flyout.Location = screenPt;
            }

            flyout.Show();
            return flyout;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ZeroAnimationClock.Unsubscribe(this);
                ZeroTagEngine.UnregisterBindable(this);
            }
            base.Dispose(disposing);
        }

    }

    /// <summary>
    /// Legacy alias for <see cref="ZPidFlyout"/>.
    /// Preserved for backward compatibility.
    /// </summary>
    [Obsolete("ZeroPidFlyout is deprecated and will be removed in 5 release cycles. Please migrate to ZPidFlyout instead.")]
    [ToolboxItem(false)]
    public class ZeroPidFlyout : ZPidFlyout
    {
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZPidFlyout"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("PidFlyout is deprecated and will be removed in 5 release cycles. Please migrate to ZPidFlyout instead.")]
    [ToolboxItem(false)]
    public class PidFlyout : ZPidFlyout
    {
    }

    #endregion
}
