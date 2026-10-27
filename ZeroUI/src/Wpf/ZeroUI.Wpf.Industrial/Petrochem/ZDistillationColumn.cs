using System;
using System.Windows;
using System.Windows.Media;
using ZeroUI.Core.Petrochem;
using ZeroUI.Wpf.Base;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Petrochem
{
    /// <summary>
    /// Multi-tray fractional distillation column visualizer for WPF.
    /// Visualizes vertical pressure vessel shell, tray-by-tray temperature/pressure gradients,
    /// overhead condenser &amp; reflux drum balance, thermosiphon reboiler loop, and vapor flooding/weeping risk.
    /// </summary>
    public class ZDistillationColumn : ZeroWpfVisualBase
    {
        private readonly DistillationEngine _engine = new DistillationEngine();
        private double _animationPhase;

        protected override bool AutoAnimate => true;

        public ZDistillationColumn()
        {
        }

        #region Public Properties

        public DistillationEngine Engine => _engine;

        public string ColumnTag
        {
            get => _engine.ColumnTag;
            set
            {
                _engine.ColumnTag = value ?? "T-101";
                InvalidateVisual();
            }
        }

        public double RefluxRatio
        {
            get => _engine.RefluxRatio;
            set
            {
                _engine.RefluxRatio = Math.Max(0.1, value);
                InvalidateVisual();
            }
        }

        public double BottomsTempC
        {
            get => _engine.BottomsTempC;
            set
            {
                _engine.BottomsTempC = value;
                _engine.RecomputeTrays();
                InvalidateVisual();
            }
        }

        public double OverheadTempC
        {
            get => _engine.OverheadTempC;
            set
            {
                _engine.OverheadTempC = value;
                _engine.RecomputeTrays();
                InvalidateVisual();
            }
        }

        public double VaporVelocityMs
        {
            get => _engine.ActualVaporVelocityMs;
            set
            {
                _engine.ActualVaporVelocityMs = Math.Max(0.0, value);
                _engine.RecomputeTrays();
                InvalidateVisual();
            }
        }

        #endregion

        protected override void OnAnimationTick(double delta, long frame)
        {
            _animationPhase = (_animationPhase + delta * 2.5) % (Math.PI * 2.0);
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            double w = ActualWidth;
            double h = ActualHeight;
            if (w < 100 || h < 100) return;

            // Background
            dc.DrawRectangle(ZeroWpfTheme.BgPrimary, null, new Rect(0, 0, w, h));

            // 1. Header
            DrawHeader(dc, w);

            double headerH = 48;
            double mainY = headerH;
            double mainH = h - headerH - 12;

            double colW = Math.Min(280, (w - 32) / 2);
            var colRect = new Rect(16, mainY, colW, mainH);
            var hudRect = new Rect(colRect.Right + 16, mainY, w - colRect.Right - 32, mainH);

            // 2. Column Schematic
            DrawColumnSchematic(dc, colRect);

            // 3. Telemetry HUD
            DrawProcessTelemetry(dc, hudRect);
        }

        private void DrawHeader(DrawingContext dc, double width)
        {
            string title = $"{_engine.ColumnTag} — {_engine.ServiceDescription.ToUpperInvariant()}";
            var titleText = CreateFormattedText(title, ZeroWpfTheme.BoldTypeface, 12.5, ZeroWpfTheme.TextPrimary);
            dc.DrawText(titleText, new Point(16, 14));

            string badgeText = _engine.OperatingState.ToString().ToUpperInvariant();
            Brush badgeBrush = _engine.OperatingState switch
            {
                ColumnOperatingState.Normal => ZeroWpfTheme.SuccessAccent,
                ColumnOperatingState.FloodingRisk => ZeroWpfTheme.DangerAccent,
                ColumnOperatingState.WeepingRisk => ZeroWpfTheme.WarningAccent,
                _ => ZeroWpfTheme.TextSecondary
            };

            DrawStatusBadge(dc, new Rect(width - 156, 12, 140, 24), badgeText, badgeBrush);
        }

        private void DrawColumnSchematic(DrawingContext dc, Rect rect)
        {
            DrawCardBox(dc, rect, "COLUMN PROFILE");

            double towerW = Math.Max(70, rect.Width / 3);
            double towerX = rect.X + 40;
            double towerY = rect.Y + 36;
            double towerH = rect.Height - 72;

            var shellPen = new Pen(ZeroWpfTheme.TextSecondary, 2.0);
            shellPen.Freeze();
            var shellBrush = new SolidColorBrush(Color.FromArgb(28, 148, 163, 184));
            shellBrush.Freeze();

            // Cylindrical Body
            dc.DrawRectangle(shellBrush, shellPen, new Rect(towerX, towerY, towerW, towerH));

            // Sump Liquid Level
            double sumpPct = Math.Max(0.0, Math.Min(100.0, _engine.SumpLevelPct));
            double sumpH = (towerH * 0.15) * (sumpPct / 100.0);
            double sumpY = towerY + towerH - sumpH;
            var sumpBrush = new SolidColorBrush(Color.FromArgb(140, 180, 75, 20));
            sumpBrush.Freeze();
            dc.DrawRectangle(sumpBrush, null, new Rect(towerX + 1, sumpY, towerW - 2, sumpH));

            // Trays
            int trayCount = _engine.Trays.Count;
            if (trayCount > 0)
            {
                double traySpacing = (towerH - 24) / (trayCount + 1);
                for (int i = 0; i < trayCount; i++)
                {
                    var tray = _engine.Trays[i];
                    double y = towerY + towerH - 16 - (tray.TrayIndex * traySpacing);

                    double factor = (double)(tray.TrayIndex - 1) / Math.Max(1, trayCount - 1);
                    Color trayCol = LerpRefineryColor(factor);
                    var trayPen = new Pen(new SolidColorBrush(trayCol), 1.5);
                    trayPen.Freeze();

                    dc.DrawLine(trayPen, new Point(towerX + 4, y), new Point(towerX + towerW - 4, y));

                    if (tray.IsFeedTray)
                    {
                        var feedPen = new Pen(ZeroWpfTheme.PrimaryAccent, 2.0);
                        feedPen.Freeze();
                        dc.DrawLine(feedPen, new Point(rect.X + 8, y), new Point(towerX, y));
                    }
                }
            }

            // Overhead System (Condenser & Reflux Line)
            double pipeX = towerX + towerW / 2;
            double pipeTopY = towerY - 20;
            double condX = towerX + towerW + 28;
            double condY = towerY - 10;
            var pipePen = new Pen(ZeroWpfTheme.BorderDefault, 1.5);
            pipePen.Freeze();

            dc.DrawLine(pipePen, new Point(pipeX, towerY), new Point(pipeX, pipeTopY));
            dc.DrawLine(pipePen, new Point(pipeX, pipeTopY), new Point(condX + 18, pipeTopY));
            dc.DrawLine(pipePen, new Point(condX + 18, pipeTopY), new Point(condX + 18, condY));

            var finBrush = new SolidColorBrush(Color.FromArgb(40, 56, 189, 248));
            finBrush.Freeze();
            var finPen = new Pen(new SolidColorBrush(Color.FromRgb(56, 189, 248)), 1.5);
            finPen.Freeze();
            dc.DrawRectangle(finBrush, finPen, new Rect(condX, condY, 36, 20));

            // Reflux Line
            dc.DrawLine(pipePen, new Point(condX + 18, condY + 20), new Point(condX + 18, towerY + 8));
            dc.DrawLine(pipePen, new Point(condX + 18, towerY + 8), new Point(towerX + towerW, towerY + 8));

            // Reboiler Loop
            double rebX = towerX + towerW + 20;
            double rebY = towerY + towerH - 42;
            dc.DrawLine(pipePen, new Point(towerX + towerW / 2, towerY + towerH), new Point(towerX + towerW / 2, towerY + towerH + 20));
            dc.DrawLine(pipePen, new Point(towerX + towerW / 2, towerY + towerH + 20), new Point(rebX + 16, towerY + towerH + 20));
            dc.DrawLine(pipePen, new Point(rebX + 16, towerY + towerH + 20), new Point(rebX + 16, rebY + 26));

            var rebBrush = new SolidColorBrush(Color.FromArgb(45, 239, 68, 68));
            rebBrush.Freeze();
            var rebPen = new Pen(new SolidColorBrush(Color.FromRgb(239, 68, 68)), 1.5);
            rebPen.Freeze();
            dc.DrawRectangle(rebBrush, rebPen, new Rect(rebX, rebY, 32, 26));

            dc.DrawLine(pipePen, new Point(rebX + 16, rebY), new Point(rebX + 16, towerY + towerH - 16));
            dc.DrawLine(pipePen, new Point(rebX + 16, towerY + towerH - 16), new Point(towerX + towerW, towerY + towerH - 16));

            // Animated Rising Bubbles
            var bubBrush = new SolidColorBrush(Color.FromArgb(160, 255, 255, 255));
            bubBrush.Freeze();
            for (int b = 0; b < 6; b++)
            {
                double bPhase = (_animationPhase + b * 1.05) % (Math.PI * 2.0);
                double by = towerY + towerH - 20 - (bPhase / (Math.PI * 2.0)) * (towerH - 36);
                double bx = towerX + 10 + (Math.Sin(bPhase * 2.0 + b) * 14.0) + (b * 6) % (towerW - 20);
                dc.DrawEllipse(bubBrush, null, new Point(bx, by), 2, 2);
            }
        }

        private static Color LerpRefineryColor(double factor)
        {
            if (factor <= 0.5)
            {
                double t = factor * 2.0;
                return Color.FromArgb(255,
                    (byte)(239 - t * (239 - 245)),
                    (byte)(68 + t * (158 - 68)),
                    (byte)(68 - t * (68 - 11)));
            }
            else
            {
                double t = (factor - 0.5) * 2.0;
                return Color.FromArgb(255,
                    (byte)(245 - t * (245 - 56)),
                    (byte)(158 + t * (189 - 158)),
                    (byte)(11 + t * (248 - 11)));
            }
        }

        private void DrawProcessTelemetry(DrawingContext dc, Rect rect)
        {
            double cardW = (rect.Width - 12) / 2;
            double cardH = (rect.Height - 12) / 2;

            // Card 1: Pressure & Hydraulics
            var card1 = new Rect(rect.X, rect.Y, cardW, cardH);
            DrawCardBox(dc, card1, "HYDRAULIC GRADIENT");
            double y1 = card1.Y + 34;
            double rh = 22;
            DrawDataRow(dc, card1.X + 12, card1.Right - 12, y1, "Differential Pressure (ΔP)", $"{_engine.DifferentialPressureKpa:F1} kPa");
            DrawDataRow(dc, card1.X + 12, card1.Right - 12, y1 + rh, "Top Pressure (Pt)", $"{_engine.TopPressureKpa:F1} kPa");
            DrawDataRow(dc, card1.X + 12, card1.Right - 12, y1 + rh * 2, "Bottom Pressure (Pb)", $"{_engine.BottomPressureKpa:F1} kPa");
            Brush floodBrush = _engine.FloodMarginPct >= 85.0 ? ZeroWpfTheme.DangerAccent : ZeroWpfTheme.SuccessAccent;
            DrawDataRow(dc, card1.X + 12, card1.Right - 12, y1 + rh * 3, "Flood Margin Index", $"{_engine.FloodMarginPct:F1}%", null, floodBrush);

            // Card 2: Thermal Duty
            var card2 = new Rect(card1.Right + 12, rect.Y, cardW, cardH);
            DrawCardBox(dc, card2, "THERMAL BALANCE");
            double y2 = card2.Y + 34;
            var coolBrush = new SolidColorBrush(Color.FromRgb(56, 189, 248)); coolBrush.Freeze();
            var hotBrush = new SolidColorBrush(Color.FromRgb(239, 68, 68)); hotBrush.Freeze();
            DrawDataRow(dc, card2.X + 12, card2.Right - 12, y2, "Overhead Temp (To)", $"{_engine.OverheadTempC:F1} °C", null, coolBrush);
            DrawDataRow(dc, card2.X + 12, card2.Right - 12, y2 + rh, "Bottoms Temp (Tb)", $"{_engine.BottomsTempC:F1} °C", null, hotBrush);
            DrawDataRow(dc, card2.X + 12, card2.Right - 12, y2 + rh * 2, "Reboiler Duty (Qr)", $"{_engine.ReboilerDutyKw:F0} kW");
            DrawDataRow(dc, card2.X + 12, card2.Right - 12, y2 + rh * 3, "Condenser Duty (Qc)", $"{_engine.CondenserDutyKw:F0} kW");

            // Card 3: Fractionation Flows
            var card3 = new Rect(rect.X, card1.Bottom + 12, cardW, cardH);
            DrawCardBox(dc, card3, "FRACTIONATION FLOWS");
            double y3 = card3.Y + 34;
            DrawDataRow(dc, card3.X + 12, card3.Right - 12, y3, "Reflux Ratio (L/D)", $"{_engine.RefluxRatio:F2}", null, ZeroWpfTheme.PrimaryAccent);
            DrawDataRow(dc, card3.X + 12, card3.Right - 12, y3 + rh, "Reflux Flow Rate", $"{_engine.RefluxFlowRateM3H:F1} m³/h");
            DrawDataRow(dc, card3.X + 12, card3.Right - 12, y3 + rh * 2, "Distillate Draw", $"{_engine.DistillateFlowRateM3H:F1} m³/h");
            DrawDataRow(dc, card3.X + 12, card3.Right - 12, y3 + rh * 3, "Feed Flow Rate", $"{_engine.FeedFlowRateKgH:F0} kg/h");

            // Card 4: Inventory Telemetry
            var card4 = new Rect(card3.Right + 12, card1.Bottom + 12, cardW, cardH);
            DrawCardBox(dc, card4, "VESSEL INVENTORY");
            double y4 = card4.Y + 34;
            DrawDataRow(dc, card4.X + 12, card4.Right - 12, y4, "Sump Liquid Level", $"{_engine.SumpLevelPct:F1}%");
            DrawDataRow(dc, card4.X + 12, card4.Right - 12, y4 + rh, "Reflux Drum Level", $"{_engine.RefluxDrumLevelPct:F1}%");
            DrawDataRow(dc, card4.X + 12, card4.Right - 12, y4 + rh * 2, "Bottoms Takeoff", $"{_engine.BottomsFlowRateM3H:F1} m³/h");
            DrawDataRow(dc, card4.X + 12, card4.Right - 12, y4 + rh * 3, "Active Tray Count", $"{_engine.TrayCount} Trays");
        }
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZDistillationColumn"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("DistillationColumn is deprecated and will be removed in 5 release cycles. Please migrate to ZDistillationColumn instead.")]
    public class DistillationColumn : ZDistillationColumn { }

    /// <summary>
    /// Legacy alias for <see cref="ZDistillationColumn"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroDistillationColumn is deprecated and will be removed in 5 release cycles. Please migrate to ZDistillationColumn instead.")]
    public class ZeroDistillationColumn : ZDistillationColumn { }

    #endregion

}
