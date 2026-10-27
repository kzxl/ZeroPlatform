using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.WinForms.Icons;
using ZeroUI.WinForms.Rendering;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Workflow
{
    using StepStatus = ZeroUI.WinForms.Workflow.StepsControlStatus;
    using StepGlyph = ZeroUI.WinForms.Workflow.StepsControlGlyph;

    /// <summary>
    /// Represents a single stage node in a ZeroWorkflowCard pipeline.
    /// </summary>
    public class WorkflowStage
    {
        public string Key { get; set; } = string.Empty;
        public string Title { get; set; } = "Stage Title";
        public int Quantity { get; set; } = 0;
        public string? UpdatedTime { get; set; } = "--";
        public StepStatus Status { get; set; } = StepStatus.Waiting;
        public StepGlyph Glyph { get; set; } = StepGlyph.Gear;

        public WorkflowStage(string key, string title, int qty = 0, string? updated = "--", StepStatus status = StepStatus.Waiting, StepGlyph glyph = StepGlyph.Gear)
        {
            Key = key;
            Title = title;
            Quantity = qty;
            UpdatedTime = updated;
            Status = status;
            Glyph = glyph;
        }
    }

    public class WorkflowStageClickedEventArgs : EventArgs
    {
        public int Index { get; }
        public WorkflowStage Stage { get; }

        public WorkflowStageClickedEventArgs(int index, WorkflowStage stage)
        {
            Index = index;
            Stage = stage;
        }
    }

    /// <summary>
    /// High-performance Process Step Card with an integrated horizontal milestone pipeline (Assembly ➔ QC ➔ Warehouse).
    /// Combines step headers, stage status node boxes, directional transition vectors, and click interactions.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Industrial & SCADA")]
    [ToolboxBitmap(typeof(ZeroIcons), "ZeroWorkflowCard.bmp")]
    public class ZWorkflowCard : Control
    {
        private readonly List<WorkflowStage> _stages = new List<WorkflowStage>();
        private readonly List<RectangleF> _stageBoxes = new List<RectangleF>();

        private int? _stepNumber = 3;
        private string? _stepText;
        private Color _badgeColor = Color.FromArgb(22, 119, 255); // Indigo/Blue
        private string _title = "Production Line Workflow Pipeline";
        private string? _subtitle = "SMT Line 01 • Manufacturing Order MO-20260901";
        private string? _statusTag = "Operating";
        private Color _statusTagColor = Color.FromArgb(16, 185, 129);

        private string? _footerText = "Click any stage to view details or transition production step";
        private Color _footerTextColor = Color.FromArgb(100, 116, 139);

        private int _hoveredIndex = -1;

        public event EventHandler<WorkflowStageClickedEventArgs>? StageClicked;

        public ZWorkflowCard()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor, true);

            BackColor = Color.Transparent;
            Font = new Font("Segoe UI", 9f, FontStyle.Regular);
            Size = new Size(820, 155);

            ZeroTheme.ThemeChanged += OnThemeChanged;
        }

        [Category("Appearance")]
        [DefaultValue(3)]
        public int? StepNumber
        {
            get => _stepNumber;
            set { _stepNumber = value; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue(null)]
        public string? StepText
        {
            get => _stepText;
            set { _stepText = value; Invalidate(); }
        }

        [Category("Appearance")]
        public Color BadgeColor
        {
            get => _badgeColor;
            set { _badgeColor = value; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue("Production Line Workflow Pipeline")]
        public string Title
        {
            get => _title;
            set { _title = value; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue("SMT Line 01 • Manufacturing Order MO-20260901")]
        public string? Subtitle
        {
            get => _subtitle;
            set { _subtitle = value; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue("Operating")]
        public string? StatusTag
        {
            get => _statusTag;
            set { _statusTag = value; Invalidate(); }
        }

        [Category("Appearance")]
        public Color StatusTagColor
        {
            get => _statusTagColor;
            set { _statusTagColor = value; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue("Click any stage to view details or transition production step")]
        public string? FooterText
        {
            get => _footerText;
            set { _footerText = value; Invalidate(); }
        }

        [Browsable(false)]
        public List<WorkflowStage> Stages => _stages;

        public WorkflowStage AddStage(string key, string title, int qty = 0, string? updated = "--", StepStatus status = StepStatus.Waiting, StepGlyph glyph = StepGlyph.Gear)
        {
            var stage = new WorkflowStage(key, title, qty, updated, status, glyph);
            _stages.Add(stage);
            Invalidate();
            return stage;
        }

        public void ClearStages()
        {
            _stages.Clear();
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            int newHover = -1;
            for (int i = 0; i < _stageBoxes.Count; i++)
            {
                if (_stageBoxes[i].Contains(e.Location))
                {
                    newHover = i;
                    break;
                }
            }

            if (_hoveredIndex != newHover)
            {
                _hoveredIndex = newHover;
                Cursor = _hoveredIndex >= 0 ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hoveredIndex != -1)
            {
                _hoveredIndex = -1;
                Cursor = Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left && _hoveredIndex >= 0 && _hoveredIndex < _stages.Count)
            {
                StageClicked?.Invoke(this, new WorkflowStageClickedEventArgs(_hoveredIndex, _stages[_hoveredIndex]));
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var palette = ZeroTheme.Colors;
            var cardRect = new Rectangle(0, 0, Width - 1, Height - 1);

            // 0. Card Outer Container
            using (var path = CreateRoundedRect(cardRect, 8))
            using (var bgBrush = new SolidBrush(palette.Surface))
            using (var borderPen = new Pen(palette.Border, 1f))
            {
                g.FillPath(bgBrush, path);
                g.DrawPath(borderPen, path);
            }

            // 1. Header Section
            int headerH = string.IsNullOrEmpty(_subtitle) ? 44 : 54;
            float curX = 14f;

            // Step Badge
            string badgeLabel = _stepText ?? (_stepNumber.HasValue ? _stepNumber.Value.ToString() : "");
            if (!string.IsNullOrEmpty(badgeLabel))
            {
                var badgeRect = new RectangleF(curX, 12f, 22f, 22f);
                using (var bPath = CreateRoundedRect(badgeRect, 5))
                using (var bBrush = new SolidBrush(_badgeColor))
                using (var numBrush = new SolidBrush(Color.White))
                using (var numFont = new Font(Font.FontFamily, 8.5f, FontStyle.Bold))
                {
                    g.FillPath(bBrush, bPath);
                    var sfNum = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString(badgeLabel, numFont, numBrush, badgeRect, sfNum);
                }
                curX += 28f;
            }

            // Title
            using (var titleBrush = new SolidBrush(palette.TextPrimary))
            {
                var titleFont = ZeroFontCache.Get(9.5f, FontStyle.Bold);
                g.DrawString(_title, titleFont, titleBrush, curX, 13f);
            }

            // Subtitle
            if (!string.IsNullOrEmpty(_subtitle))
            {
                using var subBrush = new SolidBrush(palette.TextSecondary);
                var subFont = ZeroFontCache.Get(8f, FontStyle.Regular);
                g.DrawString(_subtitle, subFont, subBrush, curX, 33f);
            }

            // Status Tag on Header Right
            if (!string.IsNullOrEmpty(_statusTag))
            {
                var tagFont = ZeroFontCache.Get(8f, FontStyle.Bold);
                var tagSize = g.MeasureString(_statusTag, tagFont);
                float tagW = tagSize.Width + 14f;
                float tagH = 20f;
                float tagX = Width - tagW - 14f;
                float tagY = 13f;

                var tagRect = new RectangleF(tagX, tagY, tagW, tagH);
                using var tPath = CreateRoundedRect(tagRect, 4);
                using var tBg = new SolidBrush(Color.FromArgb(30, _statusTagColor));
                using var tPen = new Pen(Color.FromArgb(120, _statusTagColor), 1f);
                using var tText = new SolidBrush(_statusTagColor);

                g.FillPath(tBg, tPath);
                g.DrawPath(tPen, tPath);
                g.DrawString(_statusTag, tagFont, tText, tagRect, ZeroStringFormats.Center);
            }

            // 2. Stages Workflow Pipeline
            _stageBoxes.Clear();
            if (_stages.Count == 0) return;

            int stageCount = _stages.Count;
            float padLeft = 16f;
            float padRight = 16f;
            float availW = Width - padLeft - padRight;
            float boxH = 64f;
            float boxY = headerH + 6f;

            // Fixed box width or proportional
            float boxW = Math.Max(150f, Math.Min(220f, (availW - (stageCount - 1) * 40f) / stageCount));
            float totalBoxesW = stageCount * boxW;
            float totalGapW = availW - totalBoxesW;
            float gapW = stageCount > 1 ? totalGapW / (stageCount - 1) : 0f;

            // Fallback if width too tight
            if (gapW < 24f)
            {
                boxW = (availW - (stageCount - 1) * 24f) / stageCount;
                gapW = 24f;
            }

            var tFont = ZeroFontCache.Get(8.5f, FontStyle.Bold);
            var sFont = ZeroFontCache.Get(7.5f, FontStyle.Regular);
            var arrowFont = ZeroFontCache.Get(8f, FontStyle.Bold);

            using var textPrimaryBrush = new SolidBrush(palette.TextPrimary);
            using var textSecondaryBrush = new SolidBrush(palette.TextSecondary);
            using var dotPenGreen = new Pen(Color.FromArgb(16, 185, 129), 1f) { DashStyle = DashStyle.Dot };
            using var dotPenGray = new Pen(Color.FromArgb(120, palette.TextSecondary), 1f) { DashStyle = DashStyle.Dot };
            using var arrowBrushGreen = new SolidBrush(Color.FromArgb(16, 185, 129));
            using var arrowBrushGray = new SolidBrush(Color.FromArgb(120, palette.TextSecondary));

            for (int i = 0; i < stageCount; i++)
            {
                var stage = _stages[i];
                float bX = padLeft + i * (boxW + gapW);
                var bRect = new RectangleF(bX, boxY, boxW, boxH);
                _stageBoxes.Add(bRect);

                bool isHovered = (i == _hoveredIndex);

                // Determine Theme & Colors for stage status
                Color nodeBg, nodeBorder, iconBg, iconFg;
                switch (stage.Status)
                {
                    case StepStatus.Completed:
                        nodeBg = ZeroTheme.IsDark ? Color.FromArgb(20, 30, 45) : Color.FromArgb(250, 255, 252);
                        nodeBorder = Color.FromArgb(16, 185, 129); // Emerald
                        iconBg = Color.FromArgb(30, 16, 185, 129);
                        iconFg = Color.FromArgb(16, 185, 129);
                        break;
                    case StepStatus.InProgress:
                        nodeBg = ZeroTheme.IsDark ? Color.FromArgb(25, 35, 60) : Color.FromArgb(248, 250, 255);
                        nodeBorder = Color.FromArgb(22, 119, 255); // Blue
                        iconBg = Color.FromArgb(30, 22, 119, 255);
                        iconFg = Color.FromArgb(22, 119, 255);
                        break;
                    case StepStatus.Warning:
                        nodeBg = ZeroTheme.IsDark ? Color.FromArgb(40, 35, 20) : Color.FromArgb(255, 251, 235);
                        nodeBorder = Color.FromArgb(245, 158, 11); // Amber
                        iconBg = Color.FromArgb(30, 245, 158, 11);
                        iconFg = Color.FromArgb(245, 158, 11);
                        break;
                    default: // Waiting
                        nodeBg = ZeroTheme.IsDark ? Color.FromArgb(20, 24, 33) : Color.FromArgb(255, 255, 255);
                        nodeBorder = palette.Border;
                        iconBg = ZeroTheme.IsDark ? Color.FromArgb(35, 45, 60) : Color.FromArgb(241, 245, 249);
                        iconFg = palette.TextSecondary;
                        break;
                }

                if (isHovered)
                {
                    nodeBorder = palette.Primary;
                }

                // Draw Stage Box
                using (var bPath = CreateRoundedRect(bRect, 6))
                using (var bgBrush = new SolidBrush(nodeBg))
                using (var pen = new Pen(nodeBorder, isHovered ? 1.5f : 1f))
                {
                    g.FillPath(bgBrush, bPath);
                    g.DrawPath(pen, bPath);
                }

                // Draw Icon Circle
                float iconSize = 36f;
                float iconX = bX + 10f;
                float iconY = boxY + (boxH - iconSize) / 2f;
                var iconRect = new RectangleF(iconX, iconY, iconSize, iconSize);

                using (var iBgBrush = new SolidBrush(iconBg))
                using (var iBorderPen = new Pen(Color.FromArgb(80, iconFg), 1f))
                {
                    g.FillEllipse(iBgBrush, iconRect);
                    g.DrawEllipse(iBorderPen, iconRect);
                }

                // Draw Glyph inside circle
                DrawStageGlyph(g, stage.Glyph, iconRect, iconFg);

                // Draw Texts
                float textX = iconX + iconSize + 10f;
                float textW = bX + boxW - textX - 6f;

                // Stage Title
                g.DrawString(stage.Title, tFont, textPrimaryBrush, textX, boxY + 8f);

                // Qty
                string qtyText = $"Qty: {stage.Quantity:N0}";
                g.DrawString(qtyText, sFont, textSecondaryBrush, textX, boxY + 26f);

                // Updated
                string upText = $"Updated: {stage.UpdatedTime ?? "--"}";
                g.DrawString(upText, sFont, textSecondaryBrush, textX, boxY + 42f);

                // Draw Transition Arrow to next stage
                if (i < stageCount - 1)
                {
                    float arrowStartX = bX + boxW + 4f;
                    float arrowEndX = bX + boxW + gapW - 4f;
                    float arrowY = boxY + boxH / 2f;

                    bool isCompleted = (stage.Status == StepStatus.Completed);
                    Pen dotPen = isCompleted ? dotPenGreen : dotPenGray;
                    SolidBrush arrowBrush = isCompleted ? arrowBrushGreen : arrowBrushGray;

                    g.DrawLine(dotPen, arrowStartX, arrowY, arrowEndX, arrowY);

                    // Draw arrow chevron in the middle
                    float midX = (arrowStartX + arrowEndX) / 2f;
                    g.DrawString("➔", arrowFont, arrowBrush, midX, arrowY, ZeroStringFormats.Center);
                }
            }

            // 3. Footer Section
            if (!string.IsNullOrEmpty(_footerText))
            {
                int footerY = Height - 22;
                var footFont = ZeroFontCache.Get(7.5f, FontStyle.Regular);
                using var footBrush = new SolidBrush(_footerTextColor);
                g.DrawString(_footerText, footFont, footBrush, 14, footerY);
            }
        }

        private static void DrawStageGlyph(Graphics g, StepGlyph glyph, RectangleF rect, Color color)
        {
            float cx = rect.X + rect.Width / 2f;
            float cy = rect.Y + rect.Height / 2f;

            using var pen = new Pen(color, 2f);
            using var brush = new SolidBrush(color);

            switch (glyph)
            {
                case StepGlyph.Checkmark:
                    // Checkmark
                    var p1 = new PointF(cx - 6f, cy);
                    var p2 = new PointF(cx - 2f, cy + 5f);
                    var p3 = new PointF(cx + 6f, cy - 5f);
                    g.DrawLines(pen, new[] { p1, p2, p3 });
                    break;

                case StepGlyph.Warehouse:
                    // Warehouse roof & body
                    var r1 = new PointF(cx, cy - 6f);
                    var r2 = new PointF(cx - 7f, cy - 1f);
                    var r3 = new PointF(cx + 7f, cy - 1f);
                    g.DrawPolygon(pen, new[] { r1, r2, r3 });
                    g.DrawRectangle(pen, cx - 6f, cy - 1f, 12f, 8f);
                    g.FillRectangle(brush, cx - 2f, cy + 2f, 4f, 5f);
                    break;

                case StepGlyph.Truck:
                    // Transport truck
                    g.DrawRectangle(pen, cx - 7f, cy - 4f, 9f, 8f);
                    g.DrawRectangle(pen, cx + 2f, cy - 1f, 5f, 5f);
                    g.FillEllipse(brush, cx - 5f, cy + 4f, 4f, 4f);
                    g.FillEllipse(brush, cx + 3f, cy + 4f, 4f, 4f);
                    break;

                default: // Gear / Assembly
                    g.DrawEllipse(pen, cx - 6f, cy - 6f, 12f, 12f);
                    g.FillEllipse(brush, cx - 2.5f, cy - 2.5f, 5f, 5f);
                    // 4 small teeth
                    g.DrawLine(pen, cx, cy - 7f, cx, cy - 9f);
                    g.DrawLine(pen, cx, cy + 7f, cx, cy + 9f);
                    g.DrawLine(pen, cx - 7f, cy, cx - 9f, cy);
                    g.DrawLine(pen, cx + 7f, cy, cx + 9f, cy);
                    break;
            }
        }

        private static GraphicsPath CreateRoundedRect(RectangleF rect, float radius)
        {
            var path = new GraphicsPath();
            float d = radius * 2f;
            if (rect.Width < d || rect.Height < d)
            {
                path.AddRectangle(rect);
                return path;
            }

            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    
    private void OnThemeChanged(object? sender, EventArgs e) => Invalidate();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ZeroTheme.ThemeChanged -= OnThemeChanged;
        }
        base.Dispose(disposing);
    }

}

    /// <summary>
    /// Legacy alias for <see cref="ZWorkflowCard"/>.
    /// Preserved for backward compatibility.
    /// </summary>
    [Obsolete("ZeroWorkflowCard is deprecated and will be removed in 5 release cycles. Please migrate to ZWorkflowCard instead.")]
    [ToolboxItem(false)]
    public class ZeroWorkflowCard : ZWorkflowCard
    {
        public void AddStage(string id, string title, int quantity, string timestamp, ZeroStepStatus status, ZeroStepGlyph glyph)
            => base.AddStage(id, title, quantity, timestamp, (StepStatus)status, (StepGlyph)glyph);
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZWorkflowCard"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("WorkflowCard is deprecated and will be removed in 5 release cycles. Please migrate to ZWorkflowCard instead.")]
    [ToolboxItem(false)]
    public class WorkflowCard : ZWorkflowCard
    {
    }

    #endregion
}
