using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.Core.Process;
using ZeroUI.WinForms.Base;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Process
{
    /// <summary>
    /// ANSI/ISA-88 (IEC 61512) Sequential Function Chart (SFC) batch recipe tracker for WinForms.
    /// Visualizes active procedural steps, countdown timers, holding interlocks, and
    /// 21 CFR Part 11 electronic signature checkpoints.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Process & Life Sciences")]
    [Description("ISA-88 Sequential Function Chart (SFC) procedural batch execution tracker")]
    public class ZSfcBatchTracker : VisualControlBase
    {
        private readonly SfcRecipeExecutionEngine _engine = new SfcRecipeExecutionEngine();

        protected override bool AutoAnimate => true;

        public ZSfcBatchTracker()
        {
            Size = new Size(760, 360);
        }

        #region Public Properties

        [Category("ISA-88")]
        [Description("Access to the underlying pure ISA-88 recipe execution engine")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public SfcRecipeExecutionEngine Engine => _engine;

        [Category("ISA-88")]
        [DefaultValue("Monoclonal Antibody Harvest Recipe")]
        public string RecipeName
        {
            get => _engine.RecipeName;
            set
            {
                _engine.RecipeName = value ?? "Batch Recipe";
                Invalidate();
            }
        }

        [Category("ISA-88")]
        [DefaultValue("B2609-MAB-042")]
        public string BatchId
        {
            get => _engine.BatchId;
            set
            {
                _engine.BatchId = value ?? "BATCH-01";
                Invalidate();
            }
        }

        [Category("ISA-88")]
        [DefaultValue(S88BatchState.Running)]
        public S88BatchState State
        {
            get => _engine.State;
            set
            {
                _engine.State = value;
                Invalidate();
            }
        }

        #endregion

        protected override void OnAnimationTick(double delta, long frame)
        {
            _engine.AdvanceTime(delta);
        }

        protected override void OnDrawVisual(Graphics g, Rectangle bounds, ZeroThemePalette palette)
        {
            // 1. Top Header
            DrawHeader(g, bounds, palette, out int headerH);

            int mainTop = bounds.Y + headerH;
            int mainHeight = bounds.Height - headerH - 12;

            int hudW = bounds.Width < 540 ? Math.Max(160, (int)(bounds.Width * 0.42)) : 240;
            int sfcW = bounds.Width - hudW - 32;

            if (sfcW < 80 || mainHeight < 80) return;

            Rectangle sfcRect = new Rectangle(bounds.X + 16, mainTop, sfcW, mainHeight);
            Rectangle hudRect = new Rectangle(sfcRect.Right + 12, mainTop, hudW, mainHeight);

            // 2. Sequential Function Chart (SFC) Layout
            DrawSfcDiagram(g, sfcRect, palette);

            // 3. Active Step & 21 CFR Part 11 HUD
            DrawBatchHud(g, hudRect, palette);
        }

        private void DrawHeader(Graphics g, Rectangle bounds, ZeroThemePalette palette, out int headerH)
        {
            string title = $"{_engine.RecipeName} — {_engine.BatchId}";
            using (var fontTitle = new Font("Segoe UI", 10.5f, FontStyle.Bold))
            using (var fontSub = new Font("Segoe UI", 8.5f, FontStyle.Bold))
            {
                Size titleSize = TextRenderer.MeasureText(g, title, fontTitle);

                Color badgeColor = _engine.State switch
                {
                    S88BatchState.Running => Color.FromArgb(34, 197, 94),
                    S88BatchState.Holding or S88BatchState.Held => Color.FromArgb(245, 158, 11),
                    S88BatchState.Aborting or S88BatchState.Aborted => Color.FromArgb(239, 68, 68),
                    S88BatchState.Complete => Color.FromArgb(56, 189, 248),
                    _ => Color.FromArgb(148, 163, 184)
                };

                int badgeW = 154;
                int badgeH = 24;

                if (bounds.Width - badgeW - 20 >= 20 + titleSize.Width + 16)
                {
                    TextRenderer.DrawText(g, title, fontTitle, new Point(bounds.X + 16, bounds.Y + 12), palette.TextPrimary);
                    Rectangle badgeRect = new Rectangle(bounds.Right - badgeW - 16, bounds.Y + 10, badgeW, badgeH);
                    PaintHelper.DrawStatusBadge(g, badgeRect, _engine.State.ToString().ToUpperInvariant(), fontSub, badgeColor, badgeColor, 4);
                    headerH = 46;
                }
                else
                {
                    Rectangle titleRect = new Rectangle(bounds.X + 16, bounds.Y + 8, bounds.Width - 32, 20);
                    TextRenderer.DrawText(g, title, fontTitle, titleRect, palette.TextPrimary, TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.EndEllipsis);
                    Rectangle badgeRect = new Rectangle(bounds.X + 16, bounds.Y + 32, Math.Min(bounds.Width - 32, badgeW), badgeH);
                    PaintHelper.DrawStatusBadge(g, badgeRect, _engine.State.ToString().ToUpperInvariant(), fontSub, badgeColor, badgeColor, 4);
                    headerH = 64;
                }
            }
        }

        private void DrawSfcDiagram(Graphics g, Rectangle rect, ZeroThemePalette palette)
        {
            using (var fontTag = new Font("Segoe UI", 8f, FontStyle.Bold))
            using (var fontName = new Font("Segoe UI", 7.5f))
            using (var fontTimer = new Font("Segoe UI", 7.5f, FontStyle.Bold))
            using (var linePen = new Pen(Color.FromArgb(148, 163, 184), 1.5f))
            {
                int stepCount = _engine.Steps.Count;
                if (stepCount == 0) return;

                int stepH = Math.Min(48, (rect.Height - (stepCount - 1) * 20) / stepCount);
                int stepW = Math.Min(340, rect.Width - 40);
                int stepX = rect.X + (rect.Width - stepW) / 2;

                for (int i = 0; i < stepCount; i++)
                {
                    var step = _engine.Steps[i];
                    int stepY = rect.Y + i * (stepH + 20);
                    Rectangle stepBox = new Rectangle(stepX, stepY, stepW, stepH);

                    // Connecting line & transition crossbar to next step
                    if (i < stepCount - 1)
                    {
                        int lineY1 = stepBox.Bottom;
                        int lineY2 = lineY1 + 20;
                        int midX = stepBox.X + stepBox.Width / 2;

                        g.DrawLine(linePen, midX, lineY1, midX, lineY2);

                        // Horizontal Transition Crossbar
                        int transBarW = 24;
                        g.DrawLine(linePen, midX - transBarW / 2, lineY1 + 10, midX + transBarW / 2, lineY1 + 10);
                    }

                    // Step Box Colors
                    Color borderCol = step.State switch
                    {
                        SfcStepState.Active => Color.FromArgb(34, 197, 94),
                        SfcStepState.Completed => Color.FromArgb(56, 189, 248),
                        SfcStepState.Held => Color.FromArgb(245, 158, 11),
                        _ => palette.Border
                    };

                    using (var fillBrush = new SolidBrush(step.State == SfcStepState.Active ? Color.FromArgb(35, 34, 197, 94) : Color.FromArgb(20, palette.Border)))
                    using (var borderPen = new Pen(borderCol, step.State == SfcStepState.Active ? 2f : 1f))
                    {
                        g.FillRectangle(fillBrush, stepBox);
                        g.DrawRectangle(borderPen, stepBox);

                        // Initial step double line border
                        if (step.Type == SfcStepType.Initial)
                        {
                            g.DrawRectangle(borderPen, stepBox.X + 2, stepBox.Y + 2, stepBox.Width - 4, stepBox.Height - 4);
                        }
                    }

                    // Progress track under active/completed step
                    if (step.ProgressPct > 0)
                    {
                        Rectangle progTrack = new Rectangle(stepBox.X + 2, stepBox.Bottom - 4, stepBox.Width - 4, 3);
                        int fillW = (int)(progTrack.Width * (step.ProgressPct / 100.0));
                        using (var progBrush = new SolidBrush(borderCol))
                        {
                            g.FillRectangle(progBrush, progTrack.X, progTrack.Y, fillW, progTrack.Height);
                        }
                    }

                    // Countdown / Status Timer
                    string timeStr = step.State == SfcStepState.Completed ? "DONE ✓" :
                                     step.State == SfcStepState.Active ? $"{step.RemainingDurationSec:F0}s left" :
                                     step.State == SfcStepState.Held ? "HELD !" : $"{step.AllocatedDurationSec:F0}s";
                    var sz = TextRenderer.MeasureText(g, timeStr, fontTimer);
                    TextRenderer.DrawText(g, timeStr, fontTimer, new Point(stepBox.Right - sz.Width - 8, stepBox.Y + 6), borderCol);

                    // Step Tag & Name with safe clipping bounds
                    string tagStr = $"[{step.StepTag}]";
                    var tagSize = TextRenderer.MeasureText(g, tagStr, fontTag);
                    TextRenderer.DrawText(g, tagStr, fontTag, new Point(stepBox.X + 8, stepBox.Y + 6), borderCol);

                    int nameX = stepBox.X + tagSize.Width + 12;
                    int nameMaxW = Math.Max(20, stepBox.Right - sz.Width - 16 - nameX);
                    Rectangle nameRect = new Rectangle(nameX, stepBox.Y + 6, nameMaxW, stepBox.Height - 12);
                    TextRenderer.DrawText(g, step.StepName, fontName, nameRect, palette.TextPrimary,
                        TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.EndEllipsis);
                }
            }
        }

        private void DrawBatchHud(Graphics g, Rectangle rect, ZeroThemePalette palette)
        {
            using (var fontTitle = new Font("Segoe UI", 9f, FontStyle.Bold))
            using (var fontLabel = new Font("Segoe UI", 8f))
            using (var fontValue = new Font("Segoe UI", 8.5f, FontStyle.Bold))
            {
                PaintHelper.DrawCardBox(g, rect, "BATCH STEP EXECUTION", fontTitle, palette);

                int pad = 12;
                int innerW = rect.Width - pad * 2;
                int rowY = rect.Y + 34;
                int rowH = 24;

                var step = _engine.CurrentStep;

                // Active Step Tag
                PaintHelper.DrawDataRow(g, new Rectangle(rect.X + pad, rowY, innerW, rowH),
                    "Active Step", step != null ? $"[{step.StepTag}]" : "None",
                    palette.TextSecondary, palette.TextPrimary, fontLabel, fontValue);
                rowY += rowH;

                // Step Progress %
                double prog = step?.ProgressPct ?? 0.0;
                PaintHelper.DrawDataRow(g, new Rectangle(rect.X + pad, rowY, innerW, rowH),
                    "Phase Progress", $"{prog:F1}%",
                    palette.TextSecondary, Color.FromArgb(56, 189, 248), fontLabel, fontValue);
                rowY += rowH;

                // Elapsed Time
                double elapsed = step?.ElapsedDurationSec ?? 0.0;
                double allocated = step?.AllocatedDurationSec ?? 0.0;
                PaintHelper.DrawDataRow(g, new Rectangle(rect.X + pad, rowY, innerW, rowH),
                    "Duration", $"{elapsed:F0}s / {allocated:F0}s",
                    palette.TextSecondary, palette.TextPrimary, fontLabel, fontValue);
                rowY += rowH;

                // 21 CFR Part 11 Sign-off
                bool needsSignOff = step?.RequiresSignOff ?? false;
                string? signedBy = step?.SignedOffBy;
                string signStr = !needsSignOff ? "Not Required" :
                                 !string.IsNullOrEmpty(signedBy) ? $"Signed: {signedBy}" : "PENDING SIG";
                Color signCol = !needsSignOff ? palette.TextSecondary :
                                !string.IsNullOrEmpty(signedBy) ? Color.FromArgb(34, 197, 94) : Color.FromArgb(239, 68, 68);

                PaintHelper.DrawDataRow(g, new Rectangle(rect.X + pad, rowY, innerW, rowH),
                    "Electronic Sig", signStr,
                    palette.TextSecondary, signCol, fontLabel, fontValue);
                rowY += rowH + 6;

                // Hold Reason if any
                if (!string.IsNullOrEmpty(_engine.HoldReason))
                {
                    Rectangle holdBox = new Rectangle(rect.X + pad, rowY, innerW, 42);
                    using (var holdBrush = new SolidBrush(Color.FromArgb(30, 245, 158, 11)))
                    using (var holdPen = new Pen(Color.FromArgb(245, 158, 11), 1f))
                    {
                        g.FillRectangle(holdBrush, holdBox);
                        g.DrawRectangle(holdPen, holdBox);
                    }
                    TextRenderer.DrawText(g, _engine.HoldReason, fontLabel,
                        new Rectangle(holdBox.X + 4, holdBox.Y + 4, holdBox.Width - 8, holdBox.Height - 8),
                        Color.FromArgb(245, 158, 11), TextFormatFlags.WordBreak);
                }
            }
        }
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZSfcBatchTracker"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("SfcBatchTracker is deprecated and will be removed in 5 release cycles. Please migrate to ZSfcBatchTracker instead.")]
    [ToolboxItem(false)]
    public class SfcBatchTracker : ZSfcBatchTracker
    {
    }

    #endregion
}
