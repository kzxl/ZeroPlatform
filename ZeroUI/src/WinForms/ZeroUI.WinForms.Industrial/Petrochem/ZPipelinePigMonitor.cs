using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.Core.Petrochem;
using ZeroUI.WinForms.Base;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Petrochem
{
    /// <summary>
    /// Intelligent Pipeline Inspection Gauge (PIG) tracking monitor for WinForms.
    /// Visualizes long-distance pipeline transit, above-ground acoustic markers (AGM),
    /// pig speed/driving differential pressure, and MFL wall loss defect ribbons.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Oil & Gas")]
    [Description("Intelligent pipeline PIG tracking monitor with AGM markers and wall anomaly diagnostics")]
    public class ZPipelinePigMonitor : VisualControlBase
    {
        private readonly PipelinePigEngine _engine = new PipelinePigEngine();
        private double _pulsePhase;

        protected override bool AutoAnimate => true;

        public ZPipelinePigMonitor()
        {
            Size = new Size(840, 420);
        }

        #region Public Properties

        [Category("Pipeline")]
        [Description("Access to the underlying pure pipeline PIG tracking engine")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public PipelinePigEngine Engine => _engine;

        [Category("Pipeline")]
        [DefaultValue("PL-24-TRANS")]
        public string PipelineTag
        {
            get => _engine.PipelineTag;
            set
            {
                _engine.PipelineTag = value ?? "PL-01";
                Invalidate();
            }
        }

        [Category("Pipeline")]
        [DefaultValue("Offshore Platform Alpha -> Coastal Refinery Terminal B")]
        public string RouteDescription
        {
            get => _engine.RouteDescription;
            set
            {
                _engine.RouteDescription = value ?? string.Empty;
                Invalidate();
            }
        }

        [Category("Pipeline")]
        [DefaultValue(2.45)]
        public double PigVelocityMs
        {
            get => _engine.PigVelocityMs;
            set
            {
                _engine.PigVelocityMs = Math.Max(0.0, value);
                Invalidate();
            }
        }

        #endregion

        protected override void OnAnimationTick(double delta, long frame)
        {
            _engine.AdvanceTime(delta);
            _pulsePhase = (_pulsePhase + delta * 4.0) % (Math.PI * 2.0);
        }

        protected override void OnDrawVisual(Graphics g, Rectangle bounds, ZeroThemePalette palette)
        {
            // 1. Header
            DrawHeader(g, bounds, palette, out int headerH);

            int pipeH = Math.Min(170, Math.Max(120, (bounds.Height - headerH - 36) / 2));
            var trackRect = new Rectangle(bounds.X + 16, bounds.Y + headerH, bounds.Width - 32, pipeH);
            var telemetryRect = new Rectangle(bounds.X + 16, trackRect.Bottom + 10, bounds.Width - 32, Math.Max(80, bounds.Height - trackRect.Bottom - 18));

            // 2. Longitudinal Pipeline & Pig Track
            DrawPipelineTrack(g, trackRect, palette);

            // 3. Telemetry KPI Grid
            DrawTelemetryGrid(g, telemetryRect, palette);
        }

        private void DrawHeader(Graphics g, Rectangle bounds, ZeroThemePalette palette, out int headerH)
        {
            string title = $"{_engine.PipelineTag} — {_engine.RouteDescription.ToUpperInvariant()}";
            using (var font = new Font("Segoe UI", 10.5f, FontStyle.Bold))
            using (var subFont = new Font("Segoe UI", 8.5f, FontStyle.Bold))
            {
                Size titleSize = TextRenderer.MeasureText(g, title, font);

                // Status Badge
                string badgeText = _engine.Status.ToString().ToUpperInvariant();
                Color badgeBg, badgeFg;
                switch (_engine.Status)
                {
                    case PigRunStatus.RunningInPipeline:
                        badgeBg = palette.Success;
                        badgeFg = Color.White;
                        break;
                    case PigRunStatus.ApproachingReceiver:
                        badgeBg = Color.FromArgb(14, 165, 233);
                        badgeFg = Color.White;
                        break;
                    case PigRunStatus.StalledAlert:
                        badgeBg = palette.Danger;
                        badgeFg = Color.White;
                        break;
                    default:
                        badgeBg = palette.Border;
                        badgeFg = palette.TextSecondary;
                        break;
                }

                int badgeW = 184;
                int badgeH = 26;

                if (bounds.Width - badgeW - 20 >= 20 + titleSize.Width + 16)
                {
                    TextRenderer.DrawText(g, title, font, new Point(bounds.X + 16, bounds.Y + 14), palette.TextPrimary);
                    PaintHelper.DrawStatusBadge(g, new Rectangle(bounds.Right - badgeW - 16, bounds.Y + 12, badgeW, badgeH), badgeText, subFont, badgeBg, badgeFg);
                    headerH = 48;
                }
                else
                {
                    Rectangle titleRect = new Rectangle(bounds.X + 16, bounds.Y + 10, bounds.Width - 32, 20);
                    TextRenderer.DrawText(g, title, font, titleRect, palette.TextPrimary, TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.EndEllipsis);
                    PaintHelper.DrawStatusBadge(g, new Rectangle(bounds.X + 16, bounds.Y + 34, Math.Min(bounds.Width - 32, badgeW), badgeH), badgeText, subFont, badgeBg, badgeFg);
                    headerH = 68;
                }
            }
        }

        private void DrawPipelineTrack(Graphics g, Rectangle rect, ZeroThemePalette palette)
        {
            using (var titleFont = new Font("Segoe UI", 9f, FontStyle.Bold))
            {
                PaintHelper.DrawCardBox(g, rect, "PIPELINE PROFILE & PIG POSITION", titleFont, palette);
            }

            int padX = 36;
            int pipeX = rect.X + padX;
            int pipeW = rect.Width - padX * 2;
            int pipeY = rect.Y + 62;
            int pipeH = 34;

            // Pipeline Body Shell
            using (var pipeBrush = new SolidBrush(Color.FromArgb(30, palette.Border)))
            using (var pipePen = new Pen(palette.Border, 2f))
            {
                g.FillRectangle(pipeBrush, pipeX, pipeY, pipeW, pipeH);
                g.DrawLine(pipePen, pipeX, pipeY, pipeX + pipeW, pipeY);
                g.DrawLine(pipePen, pipeX, pipeY + pipeH, pipeX + pipeW, pipeY + pipeH);
            }

            // Launcher Trap (Left)
            using (var trapBrush = new SolidBrush(Color.FromArgb(60, palette.Border)))
            using (var trapPen = new Pen(palette.TextSecondary, 1.5f))
            {
                g.FillRectangle(trapBrush, pipeX - 22, pipeY - 6, 22, pipeH + 12);
                g.DrawRectangle(trapPen, pipeX - 22, pipeY - 6, 22, pipeH + 12);
                // Receiver Trap (Right)
                g.FillRectangle(trapBrush, pipeX + pipeW, pipeY - 6, 22, pipeH + 12);
                g.DrawRectangle(trapPen, pipeX + pipeW, pipeY - 6, 22, pipeH + 12);
            }

            // Traversed Pipeline fill
            double progress = Math.Max(0.0, Math.Min(1.0, _engine.CurrentDistanceKm / Math.Max(1.0, _engine.TotalLengthKm)));
            int filledW = (int)(pipeW * progress);
            if (filledW > 0)
            {
                using (var fillBrush = new SolidBrush(Color.FromArgb(40, 14, 165, 233)))
                {
                    g.FillRectangle(fillBrush, pipeX, pipeY + 1, filledW, pipeH - 1);
                }
            }

            // Draw Markers (AGM Stations)
            using (var markerFont = new Font("Segoe UI", 6.5f, FontStyle.Regular))
            {
                for (int i = 0; i < _engine.Markers.Count; i++)
                {
                    var m = _engine.Markers[i];
                    double mFrac = m.DistanceKm / Math.Max(1.0, _engine.TotalLengthKm);
                    float mx = pipeX + (float)(pipeW * mFrac);

                    Color mColor = m.HasPassed ? palette.Success : palette.TextSecondary;
                    using (var pen = new Pen(mColor, 1.5f))
                    using (var brush = new SolidBrush(mColor))
                    {
                        // Indicator pin above pipe
                        g.DrawLine(pen, mx, pipeY - 14, mx, pipeY);
                        g.FillEllipse(brush, mx - 4, pipeY - 20, 8, 8);
                    }

                    using (var textBrush = new SolidBrush(palette.TextSecondary))
                    {
                        string lbl = $"KP {m.DistanceKm:F0}";
                        g.DrawString(lbl, markerFont, textBrush, mx - 14, pipeY - 32);
                    }
                }
            }

            // Draw PIG Capsule
            float pigX = pipeX + (float)(pipeW * progress);
            int pigW = 32;
            int pigH = 24;
            float pigY = pipeY + (pipeH - pigH) / 2f;

            using (var pigBodyBrush = new SolidBrush(Color.FromArgb(245, 158, 11)))
            using (var cupBrush = new SolidBrush(Color.FromArgb(239, 68, 68)))
            using (var pigPen = new Pen(Color.FromArgb(180, 83, 9), 1.5f))
            {
                // Body
                g.FillRectangle(pigBodyBrush, pigX - pigW / 2, pigY, pigW, pigH);
                g.DrawRectangle(pigPen, pigX - pigW / 2, pigY, pigW, pigH);

                // Driving Polyurethane Sealing Cups
                g.FillRectangle(cupBrush, pigX - pigW / 2 - 4, pigY - 2, 4, pigH + 4);
                g.FillRectangle(cupBrush, pigX, pigY - 2, 4, pigH + 4);
                g.FillRectangle(cupBrush, pigX + pigW / 2, pigY - 2, 4, pigH + 4);

                // Transmitter Ping Aura
                int pingR = 12 + (int)(Math.Sin(_pulsePhase) * 6.0);
                using (var pingPen = new Pen(Color.FromArgb(120, 14, 165, 233), 1.5f))
                {
                    g.DrawEllipse(pingPen, pigX - pingR, pigY + pigH / 2 - pingR, pingR * 2, pingR * 2);
                }
            }

            // Wall Loss Defect Ribbon underneath pipe
            int defY = pipeY + pipeH + 16;
            using (var ribbonPen = new Pen(Color.FromArgb(50, palette.Border), 1f))
            using (var defFont = new Font("Segoe UI", 6.5f, FontStyle.Regular))
            {
                g.DrawLine(ribbonPen, pipeX, defY, pipeX + pipeW, defY);

                for (int i = 0; i < _engine.Anomalies.Count; i++)
                {
                    var a = _engine.Anomalies[i];
                    double aFrac = a.DistanceKm / Math.Max(1.0, _engine.TotalLengthKm);
                    float ax = pipeX + (float)(pipeW * aFrac);

                    Color aColor = a.Severity == AnomalySeverity.SevereActionRequired
                        ? palette.Danger
                        : (a.Severity == AnomalySeverity.Moderate ? palette.Warning : palette.Success);

                    using (var b = new SolidBrush(aColor))
                    {
                        g.FillRectangle(b, ax - 3, defY - 4, 6, 8);
                    }

                    if (a.Severity == AnomalySeverity.SevereActionRequired)
                    {
                        using (var textB = new SolidBrush(palette.Danger))
                        {
                            g.DrawString($"⚠️ {a.WallLossDepthPct:F0}%", defFont, textB, ax - 14, defY + 8);
                        }
                    }
                }
            }
        }

        private void DrawTelemetryGrid(Graphics g, Rectangle rect, ZeroThemePalette palette)
        {
            using (var titleFont = new Font("Segoe UI", 8.5f, FontStyle.Bold))
            using (var labelFont = new Font("Segoe UI", 8f))
            using (var valFont = new Font("Segoe UI", 8.5f, FontStyle.Bold))
            {
                Rectangle card1, card2, card3, card4;
                int rowH = 21;

                if (rect.Width >= 580)
                {
                    int colW = (rect.Width - 36) / 4;
                    card1 = new Rectangle(rect.X, rect.Y, colW, rect.Height);
                    card2 = new Rectangle(card1.Right + 12, rect.Y, colW, rect.Height);
                    card3 = new Rectangle(card2.Right + 12, rect.Y, colW, rect.Height);
                    card4 = new Rectangle(card3.Right + 12, rect.Y, rect.Right - card3.Right - 12, rect.Height);
                }
                else
                {
                    int colW = (rect.Width - 10) / 2;
                    int rowHCard = Math.Max(95, (rect.Height - 8) / 2);
                    card1 = new Rectangle(rect.X, rect.Y, colW, rowHCard);
                    card2 = new Rectangle(card1.Right + 10, rect.Y, rect.Right - card1.Right - 10, rowHCard);
                    card3 = new Rectangle(rect.X, card1.Bottom + 8, colW, rect.Bottom - card1.Bottom - 8);
                    card4 = new Rectangle(card3.Right + 10, card1.Bottom + 8, rect.Right - card3.Right - 10, rect.Bottom - card1.Bottom - 8);
                }

                // Card 1: Position & Progress
                PaintHelper.DrawCardBox(g, card1, "ODOMETER & KP", titleFont, palette);
                int y1 = card1.Y + 30;
                PaintHelper.DrawDataRow(g, new Rectangle(card1.X + 10, y1, card1.Width - 20, rowH), "Distance", $"{_engine.CurrentDistanceKm:F1} km", palette.TextSecondary, palette.TextPrimary, labelFont, valFont);
                PaintHelper.DrawDataRow(g, new Rectangle(card1.X + 10, y1 + rowH, card1.Width - 20, rowH), "Total Pipe", $"{_engine.TotalLengthKm:F1} km", palette.TextSecondary, palette.TextPrimary, labelFont, valFont);
                PaintHelper.DrawDataRow(g, new Rectangle(card1.X + 10, y1 + rowH * 2, card1.Width - 20, rowH), "Progress", $"{_engine.ProgressPct:F1}%", palette.TextSecondary, Color.FromArgb(56, 189, 248), labelFont, valFont);

                // Card 2: Kinematics & Hydraulics
                PaintHelper.DrawCardBox(g, card2, "VELOCITY & HEAD", titleFont, palette);
                int y2 = card2.Y + 30;
                PaintHelper.DrawDataRow(g, new Rectangle(card2.X + 10, y2, card2.Width - 20, rowH), "PIG Speed", $"{_engine.PigVelocityMs:F2} m/s", palette.TextSecondary, palette.TextPrimary, labelFont, valFont);
                PaintHelper.DrawDataRow(g, new Rectangle(card2.X + 10, y2 + rowH, card2.Width - 20, rowH), "Diff Head", $"{_engine.DifferentialPressureBar:F2} bar", palette.TextSecondary, palette.TextPrimary, labelFont, valFont);
                PaintHelper.DrawDataRow(g, new Rectangle(card2.X + 10, y2 + rowH * 2, card2.Width - 20, rowH), "Revs", $"{_engine.OdometerRevs:F0}", palette.TextSecondary, palette.TextPrimary, labelFont, valFont);

                // Card 3: Tool Health
                PaintHelper.DrawCardBox(g, card3, "ONBOARD DIAGNOSTICS", titleFont, palette);
                int y3 = card3.Y + 30;
                PaintHelper.DrawDataRow(g, new Rectangle(card3.X + 10, y3, card3.Width - 20, rowH), "Battery", $"{_engine.BatteryPct:F0}%", palette.TextSecondary, palette.Success, labelFont, valFont);
                PaintHelper.DrawDataRow(g, new Rectangle(card3.X + 10, y3 + rowH, card3.Width - 20, rowH), "MFL Storage", $"{_engine.StorageMemoryPct:F0}%", palette.TextSecondary, palette.TextPrimary, labelFont, valFont);
                PaintHelper.DrawDataRow(g, new Rectangle(card3.X + 10, y3 + rowH * 2, card3.Width - 20, rowH), "Tool Class", _engine.ToolType.ToString(), palette.TextSecondary, palette.Primary, labelFont, valFont);

                // Card 4: Operations & Anomalies
                PaintHelper.DrawCardBox(g, card4, "INTEGRITY & ETA", titleFont, palette);
                int y4 = card4.Y + 30;
                string etaStr = _engine.EstimatedTimeToReceiver.TotalHours < 24.0
                    ? $"{_engine.EstimatedTimeToReceiver.Hours}h {_engine.EstimatedTimeToReceiver.Minutes}m"
                    : "> 24h";
                PaintHelper.DrawDataRow(g, new Rectangle(card4.X + 10, y4, card4.Width - 20, rowH), "ETA Receiver", etaStr, palette.TextSecondary, palette.TextPrimary, labelFont, valFont);
                Color defColor = _engine.Anomalies.Count > 0 ? palette.Warning : palette.Success;
                PaintHelper.DrawDataRow(g, new Rectangle(card4.X + 10, y4 + rowH, card4.Width - 20, rowH), "Anomalies", $"{_engine.Anomalies.Count} Defects", palette.TextSecondary, defColor, labelFont, valFont);
                PaintHelper.DrawDataRow(g, new Rectangle(card4.X + 10, y4 + rowH * 2, card4.Width - 20, rowH), "AGM Stations", $"{_engine.Markers.Count} Markers", palette.TextSecondary, palette.TextPrimary, labelFont, valFont);
            }
        }
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZPipelinePigMonitor"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("PipelinePigMonitor is deprecated and will be removed in 5 release cycles. Please migrate to ZPipelinePigMonitor instead.")]
    [ToolboxItem(false)]
    public class PipelinePigMonitor : ZPipelinePigMonitor
    {
    }

    #endregion
}
