using System;
using System.Windows;
using System.Windows.Media;
using ZeroUI.Core.Process;
using ZeroUI.Wpf.Base;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Process
{
    /// <summary>
    /// ANSI/ISA-88 (IEC 61512) Sequential Function Chart (SFC) batch recipe tracker for WPF.
    /// Visualizes active procedural steps, countdown timers, holding interlocks, and
    /// 21 CFR Part 11 electronic signature checkpoints.
    /// </summary>
    public class ZSfcBatchTracker : ZeroWpfVisualBase
    {
        private readonly SfcRecipeExecutionEngine _engine = new SfcRecipeExecutionEngine();

        protected override bool AutoAnimate => true;

        public ZSfcBatchTracker()
        {
        }

        #region Public Properties

        public SfcRecipeExecutionEngine Engine => _engine;

        public string RecipeName
        {
            get => _engine.RecipeName;
            set
            {
                _engine.RecipeName = value ?? "Batch Recipe";
                InvalidateVisual();
            }
        }

        public string BatchId
        {
            get => _engine.BatchId;
            set
            {
                _engine.BatchId = value ?? "BATCH-01";
                InvalidateVisual();
            }
        }

        public S88BatchState State
        {
            get => _engine.State;
            set
            {
                _engine.State = value;
                InvalidateVisual();
            }
        }

        #endregion

        protected override void OnAnimationTick(double delta, long frame)
        {
            _engine.AdvanceTime(delta);
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            double w = ActualWidth;
            double h = ActualHeight;
            if (w < 120 || h < 80) return;

            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;

            // Background
            dc.DrawRectangle(ZeroWpfTheme.BgPrimary, null, new Rect(0, 0, w, h));

            // Header
            DrawHeader(dc, w, dpi);

            double headerH = 44;
            double mainTop = headerH;
            double mainHeight = h - headerH - 12;

            double hudW = 230;
            double sfcW = w - hudW - 28;

            if (sfcW < 120 || mainHeight < 80) return;

            Rect sfcRect = new Rect(14, mainTop, sfcW, mainHeight);
            Rect hudRect = new Rect(sfcRect.Right + 10, mainTop, hudW, mainHeight);

            // SFC Diagram
            DrawSfcDiagram(dc, sfcRect, dpi);

            // Active Step HUD
            DrawBatchHud(dc, hudRect, dpi);
        }

        private void DrawHeader(DrawingContext dc, double width, double dpi)
        {
            var titleFt = CreateFormattedText($"{_engine.RecipeName} — {_engine.BatchId}",
                ZeroWpfTheme.BoldTypeface, 11, ZeroWpfTheme.TextPrimary, dpi);
            dc.DrawText(titleFt, new Point(14, 12));

            Brush badgeBrush = _engine.State switch
            {
                S88BatchState.Running => Brushes.LimeGreen,
                S88BatchState.Holding or S88BatchState.Held => Brushes.Orange,
                S88BatchState.Aborting or S88BatchState.Aborted => Brushes.Crimson,
                S88BatchState.Complete => Brushes.DodgerBlue,
                _ => Brushes.Gray
            };

            Rect badgeRect = new Rect(width - 160, 10, 146, 22);
            DrawStatusBadge(dc, badgeRect, _engine.State.ToString().ToUpperInvariant(), badgeBrush, ZeroWpfTheme.BoldTypeface, 8.5);
        }

        private void DrawSfcDiagram(DrawingContext dc, Rect rect, double dpi)
        {
            int stepCount = _engine.Steps.Count;
            if (stepCount == 0) return;

            double stepH = Math.Min(44, (rect.Height - (stepCount - 1) * 18) / stepCount);
            double stepW = Math.Min(320, rect.Width - 30);
            double stepX = rect.Left + (rect.Width - stepW) * 0.5;

            Pen linePen = new Pen(new SolidColorBrush(Color.FromArgb(140, 148, 163, 184)), 1.5);
            linePen.Freeze();

            for (int i = 0; i < stepCount; i++)
            {
                var step = _engine.Steps[i];
                double stepY = rect.Top + i * (stepH + 18);
                Rect stepBox = new Rect(stepX, stepY, stepW, stepH);

                // Connecting Line & Transition Bar
                if (i < stepCount - 1)
                {
                    double lineY1 = stepBox.Bottom;
                    double lineY2 = lineY1 + 18;
                    double midX = stepBox.Left + stepBox.Width * 0.5;

                    dc.DrawLine(linePen, new Point(midX, lineY1), new Point(midX, lineY2));
                    dc.DrawLine(linePen, new Point(midX - 12, lineY1 + 9), new Point(midX + 12, lineY1 + 9));
                }

                Brush borderBrush = step.State switch
                {
                    SfcStepState.Active => Brushes.LimeGreen,
                    SfcStepState.Completed => Brushes.DodgerBlue,
                    SfcStepState.Held => Brushes.Orange,
                    _ => ZeroWpfTheme.BorderDefault
                };

                Pen borderPen = new Pen(borderBrush, step.State == SfcStepState.Active ? 1.8 : 1.0);
                borderPen.Freeze();

                Brush fillBrush = step.State == SfcStepState.Active
                    ? new SolidColorBrush(Color.FromArgb(35, 34, 197, 94))
                    : new SolidColorBrush(Color.FromArgb(20, 148, 163, 184));
                fillBrush.Freeze();

                dc.DrawRectangle(fillBrush, borderPen, stepBox);

                if (step.Type == SfcStepType.Initial)
                {
                    Rect innerBox = new Rect(stepBox.Left + 2, stepBox.Top + 2, stepBox.Width - 4, stepBox.Height - 4);
                    dc.DrawRectangle(null, borderPen, innerBox);
                }

                // Progress Bar under active step
                if (step.ProgressPct > 0)
                {
                    Rect progTrack = new Rect(stepBox.Left + 2, stepBox.Bottom - 3, stepBox.Width - 4, 2.5);
                    double fillW = progTrack.Width * (step.ProgressPct / 100.0);
                    dc.DrawRectangle(borderBrush, null, new Rect(progTrack.Left, progTrack.Top, fillW, progTrack.Height));
                }

                // Step Tag
                var tagFt = CreateFormattedText($"[{step.StepTag}]", ZeroWpfTheme.BoldTypeface, 7.5, borderBrush, dpi);
                dc.DrawText(tagFt, new Point(stepBox.Left + 6, stepBox.Top + 6));

                // Step Name
                var nameFt = CreateFormattedText(step.StepName, ZeroWpfTheme.RegularTypeface, 7.5, ZeroWpfTheme.TextPrimary, dpi);
                dc.DrawText(nameFt, new Point(stepBox.Left + 48, stepBox.Top + 6));

                // Countdown / Status Timer
                string timeStr = step.State == SfcStepState.Completed ? "DONE ✓" :
                                 step.State == SfcStepState.Active ? $"{step.RemainingDurationSec:F0}s left" :
                                 step.State == SfcStepState.Held ? "HELD !" : $"{step.AllocatedDurationSec:F0}s";
                var timeFt = CreateFormattedText(timeStr, ZeroWpfTheme.BoldTypeface, 7.5, borderBrush, dpi);
                dc.DrawText(timeFt, new Point(stepBox.Right - timeFt.Width - 6, stepBox.Top + 6));
            }
        }

        private void DrawBatchHud(DrawingContext dc, Rect rect, double dpi)
        {
            DrawCardBox(dc, rect, "BATCH STEP EXECUTION", ZeroWpfTheme.BoldTypeface, ZeroWpfTheme.TextSecondary, ZeroWpfTheme.BgCard, ZeroWpfTheme.BorderPen);

            double pad = 10;
            double left = rect.Left + pad;
            double right = rect.Right - pad;
            double rowY = rect.Top + 30;
            double rowH = 22;

            var step = _engine.CurrentStep;

            // Active Step
            DrawDataRow(dc, left, right, rowY, "Active Step", step != null ? $"[{step.StepTag}]" : "None",
                ZeroWpfTheme.RegularTypeface, ZeroWpfTheme.BoldTypeface, ZeroWpfTheme.TextSecondary, ZeroWpfTheme.TextPrimary);
            rowY += rowH;

            // Phase Progress
            double prog = step?.ProgressPct ?? 0.0;
            DrawDataRow(dc, left, right, rowY, "Phase Progress", $"{prog:F1}%",
                ZeroWpfTheme.RegularTypeface, ZeroWpfTheme.BoldTypeface, ZeroWpfTheme.TextSecondary, Brushes.DodgerBlue);
            rowY += rowH;

            // Duration
            double elapsed = step?.ElapsedDurationSec ?? 0.0;
            double allocated = step?.AllocatedDurationSec ?? 0.0;
            DrawDataRow(dc, left, right, rowY, "Duration", $"{elapsed:F0}s / {allocated:F0}s",
                ZeroWpfTheme.RegularTypeface, ZeroWpfTheme.BoldTypeface, ZeroWpfTheme.TextSecondary, ZeroWpfTheme.TextPrimary);
            rowY += rowH;

            // 21 CFR Part 11 Electronic Signature
            bool needsSignOff = step?.RequiresSignOff ?? false;
            string? signedBy = step?.SignedOffBy;
            string signStr = !needsSignOff ? "Not Required" :
                             !string.IsNullOrEmpty(signedBy) ? $"Signed: {signedBy}" : "PENDING SIG";
            Brush signBrush = !needsSignOff ? ZeroWpfTheme.TextSecondary :
                              !string.IsNullOrEmpty(signedBy) ? Brushes.LimeGreen : Brushes.Crimson;

            DrawDataRow(dc, left, right, rowY, "Electronic Sig", signStr,
                ZeroWpfTheme.RegularTypeface, ZeroWpfTheme.BoldTypeface, ZeroWpfTheme.TextSecondary, signBrush);
            rowY += rowH + 6;

            // Hold Reason if any
            if (!string.IsNullOrEmpty(_engine.HoldReason))
            {
                Rect holdRect = new Rect(left, rowY, right - left, 38);
                Brush holdBg = new SolidColorBrush(Color.FromArgb(30, 245, 158, 11));
                holdBg.Freeze();
                Pen holdPen = new Pen(Brushes.Orange, 1.0);
                holdPen.Freeze();

                dc.DrawRectangle(holdBg, holdPen, holdRect);

                var reasonFt = CreateFormattedText(_engine.HoldReason ?? string.Empty, ZeroWpfTheme.RegularTypeface, 7.5, Brushes.Orange, dpi);
                dc.DrawText(reasonFt, new Point(holdRect.Left + 4, holdRect.Top + 4));
            }
        }
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZSfcBatchTracker"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("SfcBatchTracker is deprecated and will be removed in 5 release cycles. Please migrate to ZSfcBatchTracker instead.")]
    public class SfcBatchTracker : ZSfcBatchTracker { }

    /// <summary>
    /// Legacy alias for <see cref="ZSfcBatchTracker"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroSfcBatchTracker is deprecated and will be removed in 5 release cycles. Please migrate to ZSfcBatchTracker instead.")]
    public class ZeroSfcBatchTracker : ZSfcBatchTracker { }

    #endregion

}
