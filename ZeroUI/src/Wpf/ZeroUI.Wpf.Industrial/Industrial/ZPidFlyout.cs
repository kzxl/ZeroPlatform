using System;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ZeroUI.Core.Rendering;
using ZeroUI.Core.Scada;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Industrial
{
    /// <summary>
    /// High-performance non-modal industrial PID Loop Tuning Faceplate Flyout for ZeroUI WPF.
    /// Features simultaneous PV/SP/MV comparative bargraphs, interactive loop mode switching (Auto/Man/Cas),
    /// a 60 FPS real-time 3-pen micro-trend chart, and interactive PID parameter tuning inputs (Kp, Ti, Td).
    /// </summary>
    public class ZPidFlyout : Window
    {
        private string _loopTag = "PIC-101";
        private string _loopDescription = "Boiler Steam Header Pressure";
        public string EngineeringUnit { get; set; } = "PSI";
        private static readonly Random _random = new Random();

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

        private DispatcherTimer? _simTimer;
        private readonly PidVisualHost _visualHost;

        public string LoopTag
        {
            get => _loopTag;
            set { _loopTag = value ?? ""; _visualHost.InvalidateVisual(); }
        }

        public string LoopDescription
        {
            get => _loopDescription;
            set { _loopDescription = value ?? ""; _visualHost.InvalidateVisual(); }
        }

        public double ProcessVariable
        {
            get => _processVariable;
            set { _processVariable = value; _visualHost.InvalidateVisual(); }
        }

        public double SetPoint
        {
            get => _setPoint;
            set { _setPoint = Math.Max(0, Math.Min(100, value)); _visualHost.InvalidateVisual(); }
        }

        public double ManipulatedVariable
        {
            get => _manipulatedVariable;
            set { _manipulatedVariable = Math.Max(0, Math.Min(100, value)); _visualHost.InvalidateVisual(); }
        }

        public double Kp
        {
            get => _kp;
            set { _kp = Math.Max(0.01, Math.Min(50.0, value)); _visualHost.InvalidateVisual(); }
        }

        public double Ti
        {
            get => _ti;
            set { _ti = Math.Max(0.1, Math.Min(300.0, value)); _visualHost.InvalidateVisual(); }
        }

        public double Td
        {
            get => _td;
            set { _td = Math.Max(0.0, Math.Min(60.0, value)); _visualHost.InvalidateVisual(); }
        }

        public event EventHandler? ParametersChanged;

        public ZPidFlyout()
        {
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Width = 380;
            Height = 490;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            Topmost = true;

            // Pre-fill circular trend buffers with steady baseline
            for (int i = 0; i < TrendCapacity; i++)
            {
                _trendPv[i] = (float)_processVariable;
                _trendSp[i] = (float)_setPoint;
                _trendMv[i] = (float)_manipulatedVariable;
            }

            _visualHost = new PidVisualHost(this);
            Content = _visualHost;

            Loaded += (s, e) => StartSimulation();
            Closed += (s, e) => StopSimulation();
        }

        private void StartSimulation()
        {
            _simTimer = new DispatcherTimer(DispatcherPriority.Render)
            {
                Interval = TimeSpan.FromMilliseconds(50) // 20 Hz sample rate
            };
            _simTimer.Tick += OnSimulationTick;
            _simTimer.Start();
        }

        private void StopSimulation()
        {
            _simTimer?.Stop();
            _simTimer = null;
        }

        private void OnSimulationTick(object? sender, EventArgs e)
        {
            // Process dynamic response calculation (First-order plus dead time approximation)
            if (_mode == ZeroPidMode.Auto)
            {
                // Error = SP - PV
                double error = _setPoint - _processVariable;
                // Proportional action
                double pAction = _kp * error;
                // Integral approximation
                double iAction = (_kp / Math.Max(0.1, _ti)) * error * 0.05;
                // Manipulated variable update
                _manipulatedVariable = Math.Max(0, Math.Min(100, _manipulatedVariable + (pAction * 0.05) + iAction));
            }

            // System physics simulation (PV tracks MV with lag + sensor noise)
            double targetPv = _manipulatedVariable * 0.95 + 2.5;
            double noise = (_random.NextDouble() - 0.5) * 0.4;
            _processVariable += (targetPv - _processVariable) * 0.08 + noise;

            // Push into rolling buffers
            _trendPv[_trendHead] = (float)_processVariable;
            _trendSp[_trendHead] = (float)_setPoint;
            _trendMv[_trendHead] = (float)_manipulatedVariable;
            _trendHead = (_trendHead + 1) % TrendCapacity;

            _visualHost.InvalidateVisual();
        }

        public void ShowNear(Point screenPoint)
        {
            Left = Math.Max(10, screenPoint.X - Width / 2);
            Top = Math.Max(10, screenPoint.Y - Height - 10);
            Show();
        }

        public static PidFlyout ShowFlyout(Window owner, Point screenLocation, string tag = "PIC-101", string description = "Boiler Steam Header Pressure")
        {
            var flyout = new PidFlyout
            {
                Owner = owner,
                LoopTag = tag,
                LoopDescription = description,
                Left = screenLocation.X,
                Top = screenLocation.Y
            };
            flyout.Show();
            return flyout;
        }

        private sealed class PidVisualHost : FrameworkElement
        {
            private readonly ZPidFlyout _parent;
            private Rect _headerRect;
            private Rect _closeBtnRect;
            private Rect _btnAutoRect;
            private Rect _btnManRect;
            private Rect _btnCasRect;
            private Rect _btnSpMinusRect;
            private Rect _btnSpPlusRect;
            private Rect _btnKpMinusRect;
            private Rect _btnKpPlusRect;
            private Rect _btnTiMinusRect;
            private Rect _btnTiPlusRect;
            private Rect _btnTdMinusRect;
            private Rect _btnTdPlusRect;

            public PidVisualHost(ZPidFlyout parent)
            {
                _parent = parent;
                Cursor = Cursors.Arrow;
            }

            protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
            {
                base.OnMouseLeftButtonDown(e);
                Point p = e.GetPosition(this);

                if (_closeBtnRect.Contains(p))
                {
                    _parent.Close();
                    return;
                }

                if (_headerRect.Contains(p))
                {
                    _parent.DragMove();
                    return;
                }

                if (_btnAutoRect.Contains(p)) { _parent._mode = ZeroPidMode.Auto; InvalidateVisual(); return; }
                if (_btnManRect.Contains(p)) { _parent._mode = ZeroPidMode.Manual; InvalidateVisual(); return; }
                if (_btnCasRect.Contains(p)) { _parent._mode = ZeroPidMode.Cascade; InvalidateVisual(); return; }

                if (_btnSpMinusRect.Contains(p)) { _parent.SetPoint -= 1.0; _parent.ParametersChanged?.Invoke(_parent, EventArgs.Empty); return; }
                if (_btnSpPlusRect.Contains(p)) { _parent.SetPoint += 1.0; _parent.ParametersChanged?.Invoke(_parent, EventArgs.Empty); return; }

                if (_btnKpMinusRect.Contains(p)) { _parent.Kp = Math.Max(0.1, _parent.Kp - 0.1); _parent.ParametersChanged?.Invoke(_parent, EventArgs.Empty); return; }
                if (_btnKpPlusRect.Contains(p)) { _parent.Kp += 0.1; _parent.ParametersChanged?.Invoke(_parent, EventArgs.Empty); return; }

                if (_btnTiMinusRect.Contains(p)) { _parent.Ti = Math.Max(0.5, _parent.Ti - 1.0); _parent.ParametersChanged?.Invoke(_parent, EventArgs.Empty); return; }
                if (_btnTiPlusRect.Contains(p)) { _parent.Ti += 1.0; _parent.ParametersChanged?.Invoke(_parent, EventArgs.Empty); return; }

                if (_btnTdMinusRect.Contains(p)) { _parent.Td = Math.Max(0.0, _parent.Td - 0.2); _parent.ParametersChanged?.Invoke(_parent, EventArgs.Empty); return; }
                if (_btnTdPlusRect.Contains(p)) { _parent.Td += 0.2; _parent.ParametersChanged?.Invoke(_parent, EventArgs.Empty); return; }
            }

            protected override void OnRender(DrawingContext dc)
            {
                base.OnRender(dc);
                bool isDark = ZeroWpfTheme.IsDark;

                double w = ActualWidth > 0 ? ActualWidth : 380;
                double h = ActualHeight > 0 ? ActualHeight : 490;
                double dpi = 1.0;

                // Outer Shell Card
                var bgBrush = new SolidColorBrush(isDark ? Color.FromRgb(17, 24, 39) : Color.FromRgb(255, 255, 255));
                var borderPen = new Pen(new SolidColorBrush(isDark ? Color.FromRgb(55, 65, 81) : Color.FromRgb(226, 232, 240)), 1.5);
                bgBrush.Freeze();
                borderPen.Freeze();

                dc.DrawRoundedRectangle(bgBrush, borderPen, new Rect(0, 0, w, h), 8, 8);

                // 1. Header Bar
                _headerRect = new Rect(0, 0, w, 44);
                var headerBg = new SolidColorBrush(isDark ? Color.FromRgb(30, 41, 59) : Color.FromRgb(241, 245, 249));
                headerBg.Freeze();
                dc.DrawRoundedRectangle(headerBg, null, _headerRect, 8, 8);
                dc.DrawRectangle(headerBg, null, new Rect(0, 36, w, 8)); // Square off bottom curve

                var tagText = new FormattedText(
                    $"PID TUNING: {_parent.LoopTag}",
                    CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                    13.0,
                    new SolidColorBrush(isDark ? Color.FromRgb(241, 245, 249) : Color.FromRgb(15, 23, 42)),
                    dpi);
                dc.DrawText(tagText, new Point(14, 13));

                // Close button [x]
                _closeBtnRect = new Rect(w - 34, 10, 24, 24);
                var closeBrush = new SolidColorBrush(isDark ? Color.FromRgb(148, 163, 184) : Color.FromRgb(100, 116, 139));
                closeBrush.Freeze();
                dc.DrawText(new FormattedText("✕", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, ZeroWpfTheme.BoldTypeface, 12, closeBrush, dpi), new Point(w - 27, 14));

                // Mode switch selector buttons (AUTO / MAN / CAS)
                _btnAutoRect = new Rect(14, 52, 60, 26);
                _btnManRect = new Rect(78, 52, 60, 26);
                _btnCasRect = new Rect(142, 52, 60, 26);

                DrawModeButton(dc, _btnAutoRect, "AUTO", _parent._mode == ZeroPidMode.Auto, Color.FromRgb(16, 185, 129), isDark, dpi);
                DrawModeButton(dc, _btnManRect, "MAN", _parent._mode == ZeroPidMode.Manual, Color.FromRgb(245, 158, 11), isDark, dpi);
                DrawModeButton(dc, _btnCasRect, "CAS", _parent._mode == ZeroPidMode.Cascade, Color.FromRgb(59, 130, 246), isDark, dpi);

                // Live Bargraphs (PV & SP vertical comparative bars)
                double barX = 14;
                double barY = 88;
                double barW = 165;
                double barH = 140;

                var panelBg = new SolidColorBrush(isDark ? Color.FromRgb(24, 32, 47) : Color.FromRgb(248, 250, 252));
                var panelBorder = new Pen(new SolidColorBrush(isDark ? Color.FromRgb(51, 65, 85) : Color.FromRgb(226, 232, 240)), 1);
                panelBg.Freeze();
                panelBorder.Freeze();

                dc.DrawRoundedRectangle(panelBg, panelBorder, new Rect(barX, barY, barW, barH), 6, 6);

                // PV vertical bar
                double pvRatio = Math.Max(0, Math.Min(1, _parent._processVariable / 100.0));
                double spRatio = Math.Max(0, Math.Min(1, _parent._setPoint / 100.0));

                double colW = 28;
                double maxColH = 100;
                double colBottom = barY + barH - 24;

                // PV fill (Cyan)
                dc.DrawRectangle(new SolidColorBrush(isDark ? Color.FromRgb(30, 41, 59) : Color.FromRgb(226, 232, 240)), null, new Rect(barX + 24, colBottom - maxColH, colW, maxColH));
                dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(6, 182, 212)), null, new Rect(barX + 24, colBottom - (maxColH * pvRatio), colW, maxColH * pvRatio));

                // SP fill (Emerald)
                dc.DrawRectangle(new SolidColorBrush(isDark ? Color.FromRgb(30, 41, 59) : Color.FromRgb(226, 232, 240)), null, new Rect(barX + 64, colBottom - maxColH, colW, maxColH));
                dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(16, 185, 129)), null, new Rect(barX + 64, colBottom - (maxColH * spRatio), colW, maxColH * spRatio));

                // Labels
                var pvLbl = new FormattedText($"PV: {_parent._processVariable:F1}", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, ZeroWpfTheme.BoldTypeface, 9.5, new SolidColorBrush(Color.FromRgb(6, 182, 212)), dpi);
                dc.DrawText(pvLbl, new Point(barX + 16, colBottom + 4));

                var spLbl = new FormattedText($"SP: {_parent._setPoint:F1}", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, ZeroWpfTheme.BoldTypeface, 9.5, new SolidColorBrush(Color.FromRgb(16, 185, 129)), dpi);
                dc.DrawText(spLbl, new Point(barX + 60, colBottom + 4));

                // SP +/- buttons
                _btnSpMinusRect = new Rect(barX + 110, barY + 30, 36, 26);
                _btnSpPlusRect = new Rect(barX + 110, barY + 70, 36, 26);
                DrawToolButton(dc, _btnSpMinusRect, "▼", isDark, dpi);
                DrawToolButton(dc, _btnSpPlusRect, "▲", isDark, dpi);

                // Right Panel: Tuning Parameters (Kp, Ti, Td)
                double paramX = barX + barW + 10;
                double paramW = w - paramX - 14;
                dc.DrawRoundedRectangle(panelBg, panelBorder, new Rect(paramX, barY, paramW, barH), 6, 6);

                var pHead = new FormattedText("PID PARAMETERS", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, ZeroWpfTheme.BoldTypeface, 10, new SolidColorBrush(isDark ? Color.FromRgb(148, 163, 184) : Color.FromRgb(100, 116, 139)), dpi);
                dc.DrawText(pHead, new Point(paramX + 10, barY + 8));

                // Kp row
                double row1Y = barY + 28;
                dc.DrawText(new FormattedText($"Kp: {_parent.Kp:F2}", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, ZeroWpfTheme.BoldTypeface, 10.5, new SolidColorBrush(isDark ? Colors.White : Colors.Black), dpi), new Point(paramX + 10, row1Y + 3));
                _btnKpMinusRect = new Rect(paramX + paramW - 54, row1Y, 22, 20);
                _btnKpPlusRect = new Rect(paramX + paramW - 28, row1Y, 22, 20);
                DrawToolButton(dc, _btnKpMinusRect, "-", isDark, dpi);
                DrawToolButton(dc, _btnKpPlusRect, "+", isDark, dpi);

                // Ti row
                double row2Y = row1Y + 34;
                dc.DrawText(new FormattedText($"Ti: {_parent.Ti:F1}s", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, ZeroWpfTheme.BoldTypeface, 10.5, new SolidColorBrush(isDark ? Colors.White : Colors.Black), dpi), new Point(paramX + 10, row2Y + 3));
                _btnTiMinusRect = new Rect(paramX + paramW - 54, row2Y, 22, 20);
                _btnTiPlusRect = new Rect(paramX + paramW - 28, row2Y, 22, 20);
                DrawToolButton(dc, _btnTiMinusRect, "-", isDark, dpi);
                DrawToolButton(dc, _btnTiPlusRect, "+", isDark, dpi);

                // Td row
                double row3Y = row2Y + 34;
                dc.DrawText(new FormattedText($"Td: {_parent.Td:F2}s", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, ZeroWpfTheme.BoldTypeface, 10.5, new SolidColorBrush(isDark ? Colors.White : Colors.Black), dpi), new Point(paramX + 10, row3Y + 3));
                _btnTdMinusRect = new Rect(paramX + paramW - 54, row3Y, 22, 20);
                _btnTdPlusRect = new Rect(paramX + paramW - 28, row3Y, 22, 20);
                DrawToolButton(dc, _btnTdMinusRect, "-", isDark, dpi);
                DrawToolButton(dc, _btnTdPlusRect, "+", isDark, dpi);

                // MV Output horizontal progress gauge (0-100%)
                double mvY = barY + barH + 10;
                double mvH = 28;
                dc.DrawRoundedRectangle(panelBg, panelBorder, new Rect(14, mvY, w - 28, mvH), 4, 4);

                double mvFillW = (w - 30) * Math.Max(0, Math.Min(1, _parent._manipulatedVariable / 100.0));
                var mvBrush = new SolidColorBrush(Color.FromRgb(245, 158, 11));
                mvBrush.Freeze();
                dc.DrawRoundedRectangle(mvBrush, null, new Rect(15, mvY + 1, mvFillW, mvH - 2), 3, 3);

                var mvText = new FormattedText($"CONTROL OUTPUT (MV): {_parent._manipulatedVariable:F1}%", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, ZeroWpfTheme.BoldTypeface, 10.5, new SolidColorBrush(isDark ? Colors.White : Color.FromRgb(15, 23, 42)), dpi);
                dc.DrawText(mvText, new Point(22, mvY + 6));

                // 60 FPS Real-time 3-pen micro-trend chart
                double trendY = mvY + mvH + 10;
                double trendH = h - trendY - 14;
                var trendRect = new Rect(14, trendY, w - 28, trendH);
                dc.DrawRoundedRectangle(new SolidColorBrush(isDark ? Color.FromRgb(15, 23, 42) : Color.FromRgb(241, 245, 249)), panelBorder, trendRect, 6, 6);

                // Gridlines
                var gridPen = new Pen(new SolidColorBrush(isDark ? Color.FromRgb(30, 41, 59) : Color.FromRgb(226, 232, 240)), 1);
                gridPen.Freeze();
                dc.DrawLine(gridPen, new Point(14, trendY + trendH * 0.25), new Point(w - 14, trendY + trendH * 0.25));
                dc.DrawLine(gridPen, new Point(14, trendY + trendH * 0.50), new Point(w - 14, trendY + trendH * 0.50));
                dc.DrawLine(gridPen, new Point(14, trendY + trendH * 0.75), new Point(w - 14, trendY + trendH * 0.75));

                // Draw curves from circular buffers
                DrawTrendPen(dc, trendRect, _parent._trendSp, _parent._trendHead, Color.FromRgb(16, 185, 129), 1.5, true);
                DrawTrendPen(dc, trendRect, _parent._trendPv, _parent._trendHead, Color.FromRgb(6, 182, 212), 2.0, false);
                DrawTrendPen(dc, trendRect, _parent._trendMv, _parent._trendHead, Color.FromArgb(180, 245, 158, 11), 1.2, false);

                // Trend Legend
                dc.DrawText(new FormattedText("● PV (Cyan)   ● SP (Green)   ● MV (Amber)", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, ZeroWpfTheme.BoldTypeface, 9, new SolidColorBrush(isDark ? Color.FromRgb(148, 163, 184) : Color.FromRgb(100, 116, 139)), dpi), new Point(22, trendY + 6));
            }

            private static void DrawModeButton(DrawingContext dc, Rect r, string text, bool isActive, Color activeColor, bool isDark, double dpi)
            {
                var fill = isActive ? new SolidColorBrush(activeColor) : new SolidColorBrush(isDark ? Color.FromRgb(31, 41, 55) : Color.FromRgb(241, 245, 249));
                var border = new Pen(new SolidColorBrush(isActive ? activeColor : (isDark ? Color.FromRgb(55, 65, 81) : Color.FromRgb(203, 213, 225))), 1);
                fill.Freeze();
                border.Freeze();

                dc.DrawRoundedRectangle(fill, border, r, 4, 4);
                var fText = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, ZeroWpfTheme.BoldTypeface, 10, new SolidColorBrush(isActive ? Colors.White : (isDark ? Color.FromRgb(156, 163, 175) : Color.FromRgb(71, 85, 105))), dpi);
                dc.DrawText(fText, new Point(r.Left + (r.Width - fText.Width) / 2, r.Top + (r.Height - fText.Height) / 2));
            }

            private static void DrawToolButton(DrawingContext dc, Rect r, string symbol, bool isDark, double dpi)
            {
                var fill = new SolidColorBrush(isDark ? Color.FromRgb(31, 41, 55) : Color.FromRgb(241, 245, 249));
                var border = new Pen(new SolidColorBrush(isDark ? Color.FromRgb(75, 85, 99) : Color.FromRgb(203, 213, 225)), 1);
                fill.Freeze();
                border.Freeze();

                dc.DrawRoundedRectangle(fill, border, r, 3, 3);
                var fText = new FormattedText(symbol, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, ZeroWpfTheme.BoldTypeface, 10.5, new SolidColorBrush(isDark ? Colors.White : Colors.Black), dpi);
                dc.DrawText(fText, new Point(r.Left + (r.Width - fText.Width) / 2, r.Top + (r.Height - fText.Height) / 2));
            }

            private static void DrawTrendPen(DrawingContext dc, Rect r, float[] buffer, int head, Color color, double thickness, bool isDashed)
            {
                int len = buffer.Length;
                if (len < 2) return;

                StreamGeometry geom = new StreamGeometry();
                using (var ctx = geom.Open())
                {
                    double stepX = r.Width / (len - 1);
                    double minVal = 0.0;
                    double maxVal = 100.0;

                    for (int i = 0; i < len; i++)
                    {
                        int idx = (head + i) % len;
                        double norm = (buffer[idx] - minVal) / (maxVal - minVal);
                        norm = Math.Max(0, Math.Min(1, norm));
                        double px = r.Left + (i * stepX);
                        double py = r.Bottom - 4 - (norm * (r.Height - 8));

                        if (i == 0)
                        {
                            ctx.BeginFigure(new Point(px, py), false, false);
                        }
                        else
                        {
                            ctx.LineTo(new Point(px, py), true, false);
                        }
                    }
                }
                geom.Freeze();

                var pen = new Pen(new SolidColorBrush(color), thickness);
                if (isDashed)
                {
                    pen.DashStyle = DashStyles.Dash;
                }
                pen.Freeze();
                dc.DrawGeometry(null, pen, geom);
            }
        }
    }
    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZPidFlyout"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("PidFlyout is deprecated and will be removed in 5 release cycles. Please migrate to ZPidFlyout instead.")]
    public class PidFlyout : ZPidFlyout { }

    /// <summary>
    /// Legacy alias for <see cref="ZPidFlyout"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroPidFlyout is deprecated and will be removed in 5 release cycles. Please migrate to ZPidFlyout instead.")]
    public class ZeroPidFlyout : ZPidFlyout { }

    #endregion

}
