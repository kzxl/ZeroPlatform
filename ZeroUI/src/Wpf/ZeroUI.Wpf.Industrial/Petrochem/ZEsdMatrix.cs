using System;
using System.Windows;
using System.Windows.Media;
using ZeroUI.Core.Petrochem;
using ZeroUI.Wpf.Base;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Petrochem
{
    /// <summary>
    /// Safety Instrumented System (SIS) Cause &amp; Effect Matrix visualizer for WPF per IEC 61508 / IEC 61511.
    /// Visualizes process trip initiators (Causes), final safety elements (Effects), SIL ratings,
    /// Maintenance Override Switch (MOS) bypass states, and live trip interlock propagation.
    /// </summary>
    public class ZEsdMatrix : ZeroWpfVisualBase
    {
        private readonly EsdEngine _engine = new EsdEngine();
        private double _pulsePhase;

        protected override bool AutoAnimate => true;

        public ZEsdMatrix()
        {
        }

        #region Public Properties

        public EsdEngine Engine => _engine;

        public string SisTag
        {
            get => _engine.SisTag;
            set
            {
                _engine.SisTag = value ?? "SIS-01";
                InvalidateVisual();
            }
        }

        public string AreaDescription
        {
            get => _engine.AreaDescription;
            set
            {
                _engine.AreaDescription = value ?? string.Empty;
                InvalidateVisual();
            }
        }

        #endregion

        protected override void OnAnimationTick(double delta, long frame)
        {
            _pulsePhase = (_pulsePhase + delta * 3.5) % (Math.PI * 2.0);
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

            // 1. Safety Header
            DrawHeader(dc, w);

            double headerH = 50;
            var matrixRect = new Rect(16, headerH, w - 32, h - headerH - 16);

            // 2. Cause & Effect Grid
            DrawMatrixGrid(dc, matrixRect);
        }

        private void DrawHeader(DrawingContext dc, double width)
        {
            string title = $"{_engine.SisTag} — {_engine.AreaDescription.ToUpperInvariant()}";
            var titleText = CreateFormattedText(title, ZeroWpfTheme.BoldTypeface, 12.5, ZeroWpfTheme.TextPrimary);
            dc.DrawText(titleText, new Point(16, 14));

            string statusText;
            Brush statusBrush;

            if (_engine.ActiveTripCount > 0)
            {
                statusText = $"TRIP ACTIVE ({_engine.ActiveTripCount})";
                statusBrush = ZeroWpfTheme.DangerAccent;
            }
            else if (_engine.ActiveBypassCount > 0)
            {
                statusText = $"DEGRADED (MOS BYPASS: {_engine.ActiveBypassCount})";
                statusBrush = ZeroWpfTheme.WarningAccent;
            }
            else
            {
                statusText = "ARMED & SAFE (IEC 61511)";
                statusBrush = ZeroWpfTheme.SuccessAccent;
            }

            DrawStatusBadge(dc, new Rect(width - 220, 12, 204, 26), statusText, statusBrush);
        }

        private void DrawMatrixGrid(DrawingContext dc, Rect rect)
        {
            DrawCardBox(dc, rect, "CAUSE & EFFECT MATRIX (IEC 61508 / 61511)");

            int causeCount = _engine.Causes.Count;
            int effectCount = _engine.Effects.Count;
            if (causeCount == 0 || effectCount == 0) return;

            double leftHeaderW = Math.Min(320, rect.Width / 3 + 40);
            double topHeaderH = 68;
            double gridW = rect.Width - leftHeaderW - 16;
            double gridH = rect.Height - topHeaderH - 16;

            double cellW = gridW / effectCount;
            double cellH = gridH / causeCount;

            double gridX = rect.X + leftHeaderW;
            double gridY = rect.Y + topHeaderH;

            // 1. Column Headers (Effects)
            var borderPen = new Pen(ZeroWpfTheme.BorderDefault, 1.0);
            borderPen.Freeze();

            for (int e = 0; e < effectCount; e++)
            {
                var effect = _engine.Effects[e];
                double colX = gridX + (e * cellW);
                var colRect = new Rect(colX, rect.Y + 28, cellW, topHeaderH - 30);

                Brush effBg = effect.Status == EffectStatus.DeEnergized
                    ? new SolidColorBrush(Color.FromArgb(60, 239, 68, 68))
                    : new SolidColorBrush(Color.FromArgb(20, 148, 163, 184));
                effBg.Freeze();

                dc.DrawRectangle(effBg, borderPen, colRect);

                Brush tagCol = effect.Status == EffectStatus.DeEnergized ? ZeroWpfTheme.DangerAccent : ZeroWpfTheme.TextPrimary;
                var tText = CreateFormattedText(effect.Tag, ZeroWpfTheme.BoldTypeface, 9.5, tagCol);
                dc.DrawText(tText, new Point(colX + 4, colRect.Y + 4));

                DrawMiniSilBadge(dc, colX + 4, colRect.Y + 20, effect.SilRating);

                string stStr = effect.Status == EffectStatus.DeEnergized ? "TRIPPED" : "ENERGIZED";
                Brush stCol = effect.Status == EffectStatus.DeEnergized ? ZeroWpfTheme.DangerAccent : ZeroWpfTheme.SuccessAccent;
                var sText = CreateFormattedText(stStr, ZeroWpfTheme.RegularTypeface, 8.5, stCol);
                dc.DrawText(sText, new Point(colX + 4, colRect.Y + 34));
            }

            // 2. Row Headers (Causes) & Grid Cells
            var gridPen = new Pen(new SolidColorBrush(Color.FromArgb(40, 148, 163, 184)), 1.0);
            gridPen.Freeze();

            for (int c = 0; c < causeCount; c++)
            {
                var cause = _engine.Causes[c];
                double rowY = gridY + (c * cellH);
                var rowRect = new Rect(rect.X + 8, rowY, leftHeaderW - 12, cellH - 2);

                if (cause.IsTripped)
                {
                    var tripBg = new SolidColorBrush(Color.FromArgb(50, 239, 68, 68));
                    tripBg.Freeze();
                    dc.DrawRectangle(tripBg, null, rowRect);
                }
                else if (cause.IsBypassed)
                {
                    var bypBg = new SolidColorBrush(Color.FromArgb(40, 245, 158, 11));
                    bypBg.Freeze();
                    dc.DrawRectangle(bypBg, null, rowRect);
                }

                // Status Dot
                Brush dotBrush = cause.IsTripped
                    ? ZeroWpfTheme.DangerAccent
                    : (cause.IsBypassed ? ZeroWpfTheme.WarningAccent : ZeroWpfTheme.SuccessAccent);
                dc.DrawEllipse(dotBrush, null, new Point(rowRect.X + 8, rowRect.Y + (cellH - 2) / 2), 4, 4);

                // Cause Tag & SIL
                var cTag = CreateFormattedText(cause.Tag, ZeroWpfTheme.BoldTypeface, 10, ZeroWpfTheme.TextPrimary);
                dc.DrawText(cTag, new Point(rowRect.X + 18, rowRect.Y + 2));
                DrawMiniSilBadge(dc, rowRect.X + 105, rowRect.Y + 2, cause.SilRating);

                // Description
                string valStr = $"{cause.ProcessValue:F1} / {cause.TripSetpoint:F1} {cause.Unit}";
                if (cause.IsBypassed) valStr += " [MOS]";
                var dText = CreateFormattedText(cause.Description, ZeroWpfTheme.RegularTypeface, 8.5, ZeroWpfTheme.TextSecondary);
                dc.DrawText(dText, new Point(rowRect.X + 18, rowRect.Y + 18));
                var vText = CreateFormattedText(valStr, ZeroWpfTheme.RegularTypeface, 8.5, ZeroWpfTheme.TextSecondary);
                dc.DrawText(vText, new Point(rowRect.Right - vText.Width - 6, rowRect.Y + 2));

                // Grid Cells
                for (int e = 0; e < effectCount; e++)
                {
                    double colX = gridX + (e * cellW);
                    var cellRect = new Rect(colX, rowY, cellW, cellH);
                    dc.DrawRectangle(null, gridPen, cellRect);

                    var link = FindInterlock(c, e);
                    if (link != null)
                    {
                        DrawInterlockNode(dc, cellRect, link, cause);
                    }
                }
            }
        }

        private EsdInterlock? FindInterlock(int causeIdx, int effectIdx)
        {
            for (int i = 0; i < _engine.Interlocks.Count; i++)
            {
                if (_engine.Interlocks[i].CauseIndex == causeIdx && _engine.Interlocks[i].EffectIndex == effectIdx)
                {
                    return _engine.Interlocks[i];
                }
            }
            return null;
        }

        private void DrawInterlockNode(DrawingContext dc, Rect cell, EsdInterlock link, EsdCause cause)
        {
            Point center = new Point(cell.X + cell.Width / 2.0, cell.Y + cell.Height / 2.0);

            if (link.IsActive)
            {
                var aura = new SolidColorBrush(Color.FromArgb(80, 239, 68, 68));
                aura.Freeze();
                dc.DrawEllipse(aura, null, center, 9, 9);
                dc.DrawEllipse(ZeroWpfTheme.DangerAccent, null, center, 6, 6);

                var whitePen = new Pen(Brushes.White, 1.5);
                whitePen.Freeze();
                dc.DrawLine(whitePen, new Point(center.X - 2.5, center.Y), new Point(center.X + 2.5, center.Y));
            }
            else if (cause.IsBypassed)
            {
                // Bypassed diamond
                var geo = new StreamGeometry();
                using (var ctx = geo.Open())
                {
                    ctx.BeginFigure(new Point(center.X, center.Y - 5), true, true);
                    ctx.LineTo(new Point(center.X + 5, center.Y), true, false);
                    ctx.LineTo(new Point(center.X, center.Y + 5), true, false);
                    ctx.LineTo(new Point(center.X - 5, center.Y), true, false);
                }
                geo.Freeze();
                dc.DrawGeometry(ZeroWpfTheme.WarningAccent, null, geo);
            }
            else
            {
                // Armed ring
                var greenPen = new Pen(ZeroWpfTheme.SuccessAccent, 1.2);
                greenPen.Freeze();
                dc.DrawEllipse(null, greenPen, center, 4, 4);
                var dot = new SolidColorBrush(Color.FromArgb(60, 34, 197, 94));
                dot.Freeze();
                dc.DrawEllipse(dot, null, center, 2, 2);
            }
        }

        private void DrawMiniSilBadge(DrawingContext dc, double x, double y, SafetyIntegrityLevel sil)
        {
            Color silColor = sil switch
            {
                SafetyIntegrityLevel.SIL4 => Color.FromRgb(220, 38, 38),
                SafetyIntegrityLevel.SIL3 => Color.FromRgb(234, 88, 12),
                SafetyIntegrityLevel.SIL2 => Color.FromRgb(202, 138, 4),
                SafetyIntegrityLevel.SIL1 => Color.FromRgb(13, 148, 136),
                _ => Color.FromRgb(100, 116, 139)
            };

            var b = new SolidColorBrush(silColor);
            b.Freeze();
            dc.DrawRectangle(b, null, new Rect(x, y, 30, 13));

            var text = CreateFormattedText(sil.ToString(), ZeroWpfTheme.BoldTypeface, 8, Brushes.White);
            dc.DrawText(text, new Point(x + 2, y + 0.5));
        }
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZEsdMatrix"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("EsdMatrix is deprecated and will be removed in 5 release cycles. Please migrate to ZEsdMatrix instead.")]
    public class EsdMatrix : ZEsdMatrix { }

    /// <summary>
    /// Legacy alias for <see cref="ZEsdMatrix"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroEsdMatrix is deprecated and will be removed in 5 release cycles. Please migrate to ZEsdMatrix instead.")]
    public class ZeroEsdMatrix : ZEsdMatrix { }

    #endregion

}
