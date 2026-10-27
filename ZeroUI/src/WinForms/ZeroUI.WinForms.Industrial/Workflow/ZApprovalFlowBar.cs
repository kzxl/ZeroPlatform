using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.Core.Theme;
using ZeroUI.WinForms.Base;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Workflow
{
    /// <summary>
    /// Status of an individual stage in an enterprise document approval workflow.
    /// </summary>
    public enum ApprovalStepStatus
    {
        Draft,
        Pending,
        Approved,
        Rejected,
        ReApprove,
        Skipped
    }

    /// <summary>
    /// Represents a single stage/level in the approval chain of an enterprise document.
    /// </summary>
    public class ApprovalStep
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string ApproverName { get; set; } = string.Empty;
        public string ApproverRole { get; set; } = string.Empty;
        public DateTime? ApprovalDate { get; set; }
        public ApprovalStepStatus Status { get; set; } = ApprovalStepStatus.Pending;
        public string Comment { get; set; } = string.Empty;
        public object? Tag { get; set; }

        public ApprovalStep() { }

        public ApprovalStep(string title, string approverName = "", ApprovalStepStatus status = ApprovalStepStatus.Pending, string role = "")
        {
            Title = title;
            ApproverName = approverName;
            Status = status;
            ApproverRole = role;
        }
    }

    /// <summary>
    /// High-performance visual workflow tracker for ERP document approvals (PO, Invoices, QC, Material Requests).
    /// Renders multi-stage approval pipelines with color-coded nodes, vector status glyphs, connecting progress tracks,
    /// and rich contextual tooltips for approver, timestamp, and audit comments.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Workflow")]
    [DefaultEvent("StepClicked")]
    [Description("Interactive horizontal approval workflow bar with status glyphs and audit notes")]
    public class ZApprovalFlowBar : ControlBase
    {
        private readonly List<ApprovalStep> _steps = new List<ApprovalStep>();
        private readonly List<Rectangle> _nodeRects = new List<Rectangle>();
        private readonly ToolTip _toolTip = new ToolTip();

        private int _nodeSize = 36;
        private int _hoverIndex = -1;

        public event EventHandler<ApprovalStep>? StepClicked;
        public event EventHandler? StepsChanged;

        public ZApprovalFlowBar()
        {
            Size = new Size(600, 85);
            DoubleBuffered = true;
            _toolTip.InitialDelay = 200;
            _toolTip.ReshowDelay = 100;
            _toolTip.AutoPopDelay = 8000;

            // Default sample steps for design-time preview
            _steps.Add(new ApprovalStep("Create Request", "John Doe", ApprovalStepStatus.Approved, "Warehouse Staff") { ApprovalDate = DateTime.Now.AddHours(-3) });
            _steps.Add(new ApprovalStep("Dept Manager", "Jane Smith", ApprovalStepStatus.Approved, "Planning Lead") { ApprovalDate = DateTime.Now.AddHours(-1) });
            _steps.Add(new ApprovalStep("Chief Accountant", "Robert Johnson", ApprovalStepStatus.Pending, "Chief Accountant"));
            _steps.Add(new ApprovalStep("Executive Board", "Emily Davis", ApprovalStepStatus.Pending, "Chief Executive Officer"));
        }

        [Browsable(false)]
        public IReadOnlyList<ApprovalStep> Steps => _steps;

        [Category("Appearance")]
        [DefaultValue(36)]
        public int NodeSize
        {
            get => _nodeSize;
            set
            {
                _nodeSize = Math.Max(24, Math.Min(60, value));
                Invalidate();
            }
        }

        public void SetSteps(IEnumerable<ApprovalStep> steps)
        {
            _steps.Clear();
            if (steps != null)
            {
                _steps.AddRange(steps);
            }
            Invalidate();
            StepsChanged?.Invoke(this, EventArgs.Empty);
        }

        public void AddStep(ApprovalStep step)
        {
            if (step == null) throw new ArgumentNullException(nameof(step));
            _steps.Add(step);
            Invalidate();
            StepsChanged?.Invoke(this, EventArgs.Empty);
        }

        public void UpdateStep(int index, ApprovalStepStatus status, string? approver = null, string? comment = null, DateTime? date = null)
        {
            if (index >= 0 && index < _steps.Count)
            {
                var s = _steps[index];
                s.Status = status;
                if (!string.IsNullOrEmpty(approver)) s.ApproverName = approver!;
                if (comment != null) s.Comment = comment;
                s.ApprovalDate = date ?? (status == ApprovalStepStatus.Approved || status == ApprovalStepStatus.Rejected ? DateTime.Now : s.ApprovalDate);
                Invalidate();
                StepsChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public ApprovalStepStatus GetOverallStatus()
        {
            if (_steps.Count == 0) return ApprovalStepStatus.Draft;
            bool allApproved = true;
            foreach (var s in _steps)
            {
                if (s.Status == ApprovalStepStatus.Rejected) return ApprovalStepStatus.Rejected;
                if (s.Status == ApprovalStepStatus.ReApprove) return ApprovalStepStatus.ReApprove;
                if (s.Status != ApprovalStepStatus.Approved && s.Status != ApprovalStepStatus.Skipped)
                {
                    allApproved = false;
                }
            }
            return allApproved ? ApprovalStepStatus.Approved : ApprovalStepStatus.Pending;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var pal = CurrentPalette;
            _nodeRects.Clear();

            int count = _steps.Count;
            if (count == 0) return;

            int padX = 40;
            int availW = Math.Max(100, Width - padX * 2);
            int stepSpacing = count > 1 ? availW / (count - 1) : 0;
            int centerY = 24;

            // 1. Draw Connecting Progress Lines
            for (int i = 0; i < count - 1; i++)
            {
                int x1 = (count == 1) ? Width / 2 : padX + i * stepSpacing;
                int x2 = (count == 1) ? Width / 2 : padX + (i + 1) * stepSpacing;

                var currentStep = _steps[i];
                var nextStep = _steps[i + 1];

                Color lineColor = pal.Border;
                DashStyle dash = DashStyle.Solid;

                if (currentStep.Status == ApprovalStepStatus.Approved)
                {
                    if (nextStep.Status == ApprovalStepStatus.Approved || nextStep.Status == ApprovalStepStatus.Pending)
                    {
                        lineColor = Color.FromArgb(16, 185, 129); // Emerald green
                    }
                    else if (nextStep.Status == ApprovalStepStatus.Rejected)
                    {
                        lineColor = Color.FromArgb(239, 68, 68); // Red
                    }
                }
                else if (currentStep.Status == ApprovalStepStatus.Rejected)
                {
                    lineColor = Color.FromArgb(239, 68, 68);
                }
                else
                {
                    dash = DashStyle.Dash;
                }

                using (var pen = new Pen(lineColor, 3f) { DashStyle = dash })
                {
                    g.DrawLine(pen, x1 + _nodeSize / 2, centerY, x2 - _nodeSize / 2, centerY);
                }
            }

            // 2. Draw Nodes and Labels
            for (int i = 0; i < count; i++)
            {
                int cx = (count == 1) ? Width / 2 : padX + i * stepSpacing;
                var nodeBounds = new Rectangle(cx - _nodeSize / 2, centerY - _nodeSize / 2, _nodeSize, _nodeSize);
                _nodeRects.Add(nodeBounds);

                var step = _steps[i];
                bool isHover = (i == _hoverIndex);

                // Determine Colors based on Status
                Color fillCol;
                Color borderCol;
                Color iconCol = Color.White;

                switch (step.Status)
                {
                    case ApprovalStepStatus.Approved:
                        fillCol = Color.FromArgb(16, 185, 129);   // Emerald
                        borderCol = Color.FromArgb(5, 150, 105);
                        break;
                    case ApprovalStepStatus.Pending:
                        fillCol = Color.FromArgb(59, 130, 246);  // Blue
                        borderCol = Color.FromArgb(37, 99, 235);
                        break;
                    case ApprovalStepStatus.Rejected:
                        fillCol = Color.FromArgb(239, 68, 68);   // Red
                        borderCol = Color.FromArgb(220, 38, 38);
                        break;
                    case ApprovalStepStatus.ReApprove:
                        fillCol = Color.FromArgb(245, 158, 11);  // Amber
                        borderCol = Color.FromArgb(217, 119, 6);
                        break;
                    case ApprovalStepStatus.Skipped:
                        fillCol = Color.FromArgb(107, 114, 128); // Gray
                        borderCol = Color.FromArgb(75, 85, 99);
                        break;
                    default: // Draft
                        fillCol = pal.Surface;
                        borderCol = pal.Border;
                        iconCol = pal.TextSecondary;
                        break;
                }

                // Hover glow
                if (isHover)
                {
                    using (var glowPen = new Pen(Color.FromArgb(80, fillCol), 5f))
                    {
                        g.DrawEllipse(glowPen, nodeBounds.X - 2, nodeBounds.Y - 2, nodeBounds.Width + 4, nodeBounds.Height + 4);
                    }
                }

                // Fill & Border
                using (var brush = new SolidBrush(fillCol))
                using (var pen = new Pen(borderCol, 2f))
                {
                    g.FillEllipse(brush, nodeBounds);
                    g.DrawEllipse(pen, nodeBounds);
                }

                // Vector Icon Glyph
                DrawStatusGlyph(g, nodeBounds, step.Status, iconCol);

                // Text: Title
                using (var titleFont = new Font(Font.FontFamily, 8.5f, FontStyle.Bold))
                using (var titleBrush = new SolidBrush(pal.TextPrimary))
                {
                    var sf = new StringFormat
                    {
                        Alignment = StringAlignment.Center,
                        LineAlignment = StringAlignment.Near,
                        Trimming = StringTrimming.EllipsisCharacter
                    };
                    var textRect = new RectangleF(cx - 65, centerY + _nodeSize / 2 + 5, 130, 16);
                    g.DrawString(step.Title, titleFont, titleBrush, textRect, sf);
                }

                // Text: Approver & Time
                string subtitle = !string.IsNullOrEmpty(step.ApproverName)
                    ? (step.ApprovalDate.HasValue ? $"{step.ApproverName} ({step.ApprovalDate.Value:dd/MM HH:mm})" : step.ApproverName)
                    : (step.Status == ApprovalStepStatus.Pending ? "Pending..." : "");

                if (!string.IsNullOrEmpty(subtitle))
                {
                    using (var subFont = new Font(Font.FontFamily, 7.5f, FontStyle.Regular))
                    using (var subBrush = new SolidBrush(pal.TextSecondary))
                    {
                        var sf = new StringFormat
                        {
                            Alignment = StringAlignment.Center,
                            LineAlignment = StringAlignment.Near,
                            Trimming = StringTrimming.EllipsisCharacter
                        };
                        var subRect = new RectangleF(cx - 75, centerY + _nodeSize / 2 + 22, 150, 16);
                        g.DrawString(subtitle, subFont, subBrush, subRect, sf);
                    }
                }
            }
        }

        private void DrawStatusGlyph(Graphics g, Rectangle bounds, ApprovalStepStatus status, Color color)
        {
            int cx = bounds.X + bounds.Width / 2;
            int cy = bounds.Y + bounds.Height / 2;

            using (var pen = new Pen(color, 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                switch (status)
                {
                    case ApprovalStepStatus.Approved:
                        // Checkmark (✔)
                        g.DrawLine(pen, cx - 6, cy, cx - 2, cy + 5);
                        g.DrawLine(pen, cx - 2, cy + 5, cx + 7, cy - 5);
                        break;

                    case ApprovalStepStatus.Rejected:
                        // Cross (✖)
                        g.DrawLine(pen, cx - 5, cy - 5, cx + 5, cy + 5);
                        g.DrawLine(pen, cx + 5, cy - 5, cx - 5, cy + 5);
                        break;

                    case ApprovalStepStatus.Pending:
                        // Clock glyph (⏱)
                        g.DrawEllipse(pen, cx - 6, cy - 6, 12, 12);
                        g.DrawLine(pen, cx, cy - 4, cx, cy);
                        g.DrawLine(pen, cx, cy, cx + 3, cy);
                        break;

                    case ApprovalStepStatus.ReApprove:
                        // Return arrow (↺)
                        g.DrawArc(pen, cx - 6, cy - 6, 12, 12, 45, 270);
                        g.DrawLine(pen, cx + 4, cy - 6, cx + 7, cy - 2);
                        g.DrawLine(pen, cx + 4, cy - 6, cx + 1, cy - 2);
                        break;

                    default: // Draft / Skipped
                        using (var b = new SolidBrush(color))
                        {
                            g.FillEllipse(b, cx - 3, cy - 3, 6, 6);
                        }
                        break;
                }
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int newHover = -1;
            for (int i = 0; i < _nodeRects.Count; i++)
            {
                if (_nodeRects[i].Contains(e.Location))
                {
                    newHover = i;
                    break;
                }
            }

            if (newHover != _hoverIndex)
            {
                _hoverIndex = newHover;
                Invalidate();

                if (_hoverIndex >= 0 && _hoverIndex < _steps.Count)
                {
                    var s = _steps[_hoverIndex];
                    string tip = $"{s.Title} [{s.Status}]{Environment.NewLine}" +
                                 $"Approver: {(string.IsNullOrEmpty(s.ApproverName) ? "(Pending)" : s.ApproverName)}" +
                                 $"{(string.IsNullOrEmpty(s.ApproverRole) ? "" : $" - {s.ApproverRole}")}{Environment.NewLine}" +
                                 $"Time: {(s.ApprovalDate.HasValue ? s.ApprovalDate.Value.ToString("yyyy-MM-dd HH:mm:ss") : "(None)")}" +
                                 $"{(string.IsNullOrEmpty(s.Comment) ? "" : $"{Environment.NewLine}Comment: {s.Comment}")}";
                    _toolTip.SetToolTip(this, tip);
                    Cursor = Cursors.Hand;
                }
                else
                {
                    _toolTip.SetToolTip(this, null);
                    Cursor = Cursors.Default;
                }
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hoverIndex != -1)
            {
                _hoverIndex = -1;
                _toolTip.SetToolTip(this, null);
                Cursor = Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (_hoverIndex >= 0 && _hoverIndex < _steps.Count)
            {
                StepClicked?.Invoke(this, _steps[_hoverIndex]);
            }
        }
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZApprovalFlowBar"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ApprovalFlowBar is deprecated and will be removed in 5 release cycles. Please migrate to ZApprovalFlowBar instead.")]
    [ToolboxItem(false)]
    public class ApprovalFlowBar : ZApprovalFlowBar
    {
    }

    #endregion
}
