using System;
using System.Windows;
using System.Windows.Media;
using ZeroUI.Core.Petrochem;
using ZeroUI.Wpf.Base;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Petrochem
{
    /// <summary>
    /// Intelligent Pipeline Inspection Gauge (PIG) tracking monitor for WPF.
    /// Visualizes long-distance pipeline transit, above-ground acoustic markers (AGM),
    /// pig speed/driving differential pressure, and MFL wall loss defect ribbons.
    /// </summary>
    public class ZPipelinePigMonitor : ZeroWpfVisualBase
    {
        private readonly PipelinePigEngine _engine = new PipelinePigEngine();
        private double _pulsePhase;

        protected override bool AutoAnimate => true;

        public ZPipelinePigMonitor()
        {
        }

        #region Public Properties

        public PipelinePigEngine Engine => _engine;

        public string PipelineTag
        {
            get => _engine.PipelineTag;
            set
            {
                _engine.PipelineTag = value ?? "PL-01";
                InvalidateVisual();
            }
        }

        public string RouteDescription
        {
            get => _engine.RouteDescription;
            set
            {
                _engine.RouteDescription = value ?? string.Empty;
                InvalidateVisual();
            }
        }

        public double PigVelocityMs
        {
            get => _engine.PigVelocityMs;
            set
            {
                _engine.PigVelocityMs = Math.Max(0.0, value);
                InvalidateVisual();
            }
        }

        #endregion

        protected override void OnAnimationTick(double delta, long frame)
        {
            _engine.AdvanceTime(delta);
            _pulsePhase = (_pulsePhase + delta * 4.0) % (Math.PI * 2.0);
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

            double headerH = 50;
            double pipeH = 170;
            var trackRect = new Rect(16, headerH, w - 32, pipeH);
            var telemetryRect = new Rect(16, trackRect.Bottom + 12, w - 32, h - trackRect.Bottom - 24);

            // 2. Longitudinal Pipeline Track
            DrawPipelineTrack(dc, trackRect);

            // 3. Telemetry KPI Grid
            DrawTelemetryGrid(dc, telemetryRect);
        }

        private void DrawHeader(DrawingContext dc, double width)
        {
            string title = $"{_engine.PipelineTag} — {_engine.RouteDescription.ToUpperInvariant()}";
            var titleText = CreateFormattedText(title, ZeroWpfTheme.BoldTypeface, 12.5, ZeroWpfTheme.TextPrimary);
            dc.DrawText(titleText, new Point(16, 14));

            string badgeText = _engine.Status.ToString().ToUpperInvariant();
            Brush badgeBrush = _engine.Status switch
            {
                PigRunStatus.RunningInPipeline => ZeroWpfTheme.SuccessAccent,
                PigRunStatus.ApproachingReceiver => new SolidColorBrush(Color.FromRgb(14, 165, 233)),
                PigRunStatus.StalledAlert => ZeroWpfTheme.DangerAccent,
                _ => ZeroWpfTheme.TextSecondary
            };

            DrawStatusBadge(dc, new Rect(width - 200, 12, 184, 26), badgeText, badgeBrush);
        }

        private void DrawPipelineTrack(DrawingContext dc, Rect rect)
        {
            DrawCardBox(dc, rect, "PIPELINE PROFILE & PIG POSITION");

            double padX = 36;
            double pipeX = rect.X + padX;
            double pipeW = rect.Width - padX * 2;
            double pipeY = rect.Y + 62;
            double pipeH = 34;

            // Pipeline Body Shell
            var pipeBrush = new SolidColorBrush(Color.FromArgb(30, 148, 163, 184));
            pipeBrush.Freeze();
            var pipePen = new Pen(ZeroWpfTheme.BorderDefault, 2.0);
            pipePen.Freeze();

            dc.DrawRectangle(pipeBrush, pipePen, new Rect(pipeX, pipeY, pipeW, pipeH));

            // Traps
            var trapBrush = new SolidColorBrush(Color.FromArgb(60, 148, 163, 184));
            trapBrush.Freeze();
            var trapPen = new Pen(ZeroWpfTheme.TextSecondary, 1.5);
            trapPen.Freeze();
            dc.DrawRectangle(trapBrush, trapPen, new Rect(pipeX - 22, pipeY - 6, 22, pipeH + 12));
            dc.DrawRectangle(trapBrush, trapPen, new Rect(pipeX + pipeW, pipeY - 6, 22, pipeH + 12));

            // Traversed Pipeline Fill
            double progress = Math.Max(0.0, Math.Min(1.0, _engine.CurrentDistanceKm / Math.Max(1.0, _engine.TotalLengthKm)));
            double filledW = pipeW * progress;
            if (filledW > 0)
            {
                var fillBrush = new SolidColorBrush(Color.FromArgb(40, 14, 165, 233));
                fillBrush.Freeze();
                dc.DrawRectangle(fillBrush, null, new Rect(pipeX, pipeY + 1, filledW, pipeH - 1));
            }

            // Draw Markers (AGM Stations)
            for (int i = 0; i < _engine.Markers.Count; i++)
            {
                var m = _engine.Markers[i];
                double mFrac = m.DistanceKm / Math.Max(1.0, _engine.TotalLengthKm);
                double mx = pipeX + (pipeW * mFrac);

                Brush mBrush = m.HasPassed ? ZeroWpfTheme.SuccessAccent : ZeroWpfTheme.TextSecondary;
                var mPen = new Pen(mBrush, 1.5);
                mPen.Freeze();

                dc.DrawLine(mPen, new Point(mx, pipeY - 14), new Point(mx, pipeY));
                dc.DrawEllipse(mBrush, null, new Point(mx, pipeY - 18), 4, 4);

                var lblText = CreateFormattedText($"KP {m.DistanceKm:F0}", ZeroWpfTheme.RegularTypeface, 8.5, ZeroWpfTheme.TextSecondary);
                dc.DrawText(lblText, new Point(mx - 14, pipeY - 32));
            }

            // Draw PIG Capsule
            double pigX = pipeX + (pipeW * progress);
            double pigW = 32;
            double pigH = 24;
            double pigY = pipeY + (pipeH - pigH) / 2.0;

            var pigBodyBrush = new SolidColorBrush(Color.FromRgb(245, 158, 11)); pigBodyBrush.Freeze();
            var cupBrush = new SolidColorBrush(Color.FromRgb(239, 68, 68)); cupBrush.Freeze();
            var pigPen = new Pen(new SolidColorBrush(Color.FromRgb(180, 83, 9)), 1.5); pigPen.Freeze();

            dc.DrawRectangle(pigBodyBrush, pigPen, new Rect(pigX - pigW / 2, pigY, pigW, pigH));
            dc.DrawRectangle(cupBrush, null, new Rect(pigX - pigW / 2 - 4, pigY - 2, 4, pigH + 4));
            dc.DrawRectangle(cupBrush, null, new Rect(pigX, pigY - 2, 4, pigH + 4));
            dc.DrawRectangle(cupBrush, null, new Rect(pigX + pigW / 2, pigY - 2, 4, pigH + 4));

            // Acoustic Ping Beacon
            double pingR = 12 + Math.Sin(_pulsePhase) * 6.0;
            var pingPen = new Pen(new SolidColorBrush(Color.FromArgb(120, 14, 165, 233)), 1.5);
            pingPen.Freeze();
            dc.DrawEllipse(null, pingPen, new Point(pigX, pigY + pigH / 2.0), pingR, pingR);

            // Wall Loss Defect Ribbon
            double defY = pipeY + pipeH + 16;
            var ribbonPen = new Pen(new SolidColorBrush(Color.FromArgb(50, 148, 163, 184)), 1.0);
            ribbonPen.Freeze();
            dc.DrawLine(ribbonPen, new Point(pipeX, defY), new Point(pipeX + pipeW, defY));

            for (int i = 0; i < _engine.Anomalies.Count; i++)
            {
                var a = _engine.Anomalies[i];
                double aFrac = a.DistanceKm / Math.Max(1.0, _engine.TotalLengthKm);
                double ax = pipeX + (pipeW * aFrac);

                Brush aBrush = a.Severity switch
                {
                    AnomalySeverity.SevereActionRequired => ZeroWpfTheme.DangerAccent,
                    AnomalySeverity.Moderate => ZeroWpfTheme.WarningAccent,
                    _ => ZeroWpfTheme.SuccessAccent
                };

                dc.DrawRectangle(aBrush, null, new Rect(ax - 3, defY - 4, 6, 8));

                if (a.Severity == AnomalySeverity.SevereActionRequired)
                {
                    var aText = CreateFormattedText($"⚠️ {a.WallLossDepthPct:F0}%", ZeroWpfTheme.BoldTypeface, 8.5, ZeroWpfTheme.DangerAccent);
                    dc.DrawText(aText, new Point(ax - 14, defY + 8));
                }
            }
        }

        private void DrawTelemetryGrid(DrawingContext dc, Rect rect)
        {
            double colW = (rect.Width - 36) / 4;

            // Card 1: Position & Progress
            var card1 = new Rect(rect.X, rect.Y, colW, rect.Height);
            DrawCardBox(dc, card1, "ODOMETER & KP");
            double y1 = card1.Y + 34;
            double rh = 22;
            DrawDataRow(dc, card1.X + 10, card1.Right - 10, y1, "Current Distance", $"{_engine.CurrentDistanceKm:F1} km");
            DrawDataRow(dc, card1.X + 10, card1.Right - 10, y1 + rh, "Total Pipeline", $"{_engine.TotalLengthKm:F1} km");
            var skyBrush = new SolidColorBrush(Color.FromRgb(56, 189, 248)); skyBrush.Freeze();
            DrawDataRow(dc, card1.X + 10, card1.Right - 10, y1 + rh * 2, "Run Progress", $"{_engine.ProgressPct:F1}%", null, skyBrush);

            // Card 2: Kinematics & Hydraulics
            var card2 = new Rect(card1.Right + 12, rect.Y, colW, rect.Height);
            DrawCardBox(dc, card2, "VELOCITY & HEAD");
            double y2 = card2.Y + 34;
            DrawDataRow(dc, card2.X + 10, card2.Right - 10, y2, "PIG Speed", $"{_engine.PigVelocityMs:F2} m/s");
            DrawDataRow(dc, card2.X + 10, card2.Right - 10, y2 + rh, "Differential Head", $"{_engine.DifferentialPressureBar:F2} bar");
            DrawDataRow(dc, card2.X + 10, card2.Right - 10, y2 + rh * 2, "Wheel Revs", $"{_engine.OdometerRevs:F0}");

            // Card 3: Tool Health
            var card3 = new Rect(card2.Right + 12, rect.Y, colW, rect.Height);
            DrawCardBox(dc, card3, "ONBOARD DIAGNOSTICS");
            double y3 = card3.Y + 34;
            DrawDataRow(dc, card3.X + 10, card3.Right - 10, y3, "Battery State", $"{_engine.BatteryPct:F0}%", null, ZeroWpfTheme.SuccessAccent);
            DrawDataRow(dc, card3.X + 10, card3.Right - 10, y3 + rh, "MFL Storage", $"{_engine.StorageMemoryPct:F0}%");
            DrawDataRow(dc, card3.X + 10, card3.Right - 10, y3 + rh * 2, "Tool Class", _engine.ToolType.ToString(), null, ZeroWpfTheme.PrimaryAccent);

            // Card 4: Operations & Anomalies
            var card4 = new Rect(card3.Right + 12, rect.Y, colW, rect.Height);
            DrawCardBox(dc, card4, "INTEGRITY & ETA");
            double y4 = card4.Y + 34;
            string etaStr = _engine.EstimatedTimeToReceiver.TotalHours < 24.0
                ? $"{_engine.EstimatedTimeToReceiver.Hours}h {_engine.EstimatedTimeToReceiver.Minutes}m"
                : "> 24h";
            DrawDataRow(dc, card4.X + 10, card4.Right - 10, y4, "ETA to Receiver", etaStr);
            Brush defBrush = _engine.Anomalies.Count > 0 ? ZeroWpfTheme.WarningAccent : ZeroWpfTheme.SuccessAccent;
            DrawDataRow(dc, card4.X + 10, card4.Right - 10, y4 + rh, "Wall Anomalies", $"{_engine.Anomalies.Count} Defects", null, defBrush);
            DrawDataRow(dc, card4.X + 10, card4.Right - 10, y4 + rh * 2, "AGM Stations", $"{_engine.Markers.Count} Markers");
        }
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZPipelinePigMonitor"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("PipelinePigMonitor is deprecated and will be removed in 5 release cycles. Please migrate to ZPipelinePigMonitor instead.")]
    public class PipelinePigMonitor : ZPipelinePigMonitor { }

    /// <summary>
    /// Legacy alias for <see cref="ZPipelinePigMonitor"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroPipelinePigMonitor is deprecated and will be removed in 5 release cycles. Please migrate to ZPipelinePigMonitor instead.")]
    public class ZeroPipelinePigMonitor : ZPipelinePigMonitor { }

    #endregion

}
