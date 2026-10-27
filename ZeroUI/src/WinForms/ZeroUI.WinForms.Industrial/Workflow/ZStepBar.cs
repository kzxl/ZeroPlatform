using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;
using ZeroUI.Core.Theme;
using ZeroUI.Core.Workflow;
using ZeroUI.WinForms.Base;
using ZeroUI.WinForms.Icons;
using ZeroUI.WinForms.Theme;
using StepStatus = ZeroUI.Core.Workflow.StepStatus;
using StepItem = ZeroUI.Core.Workflow.StepItem;

namespace ZeroUI.WinForms.Workflow
{
    public class StepClickEventArgs : EventArgs
    {
        public int Index { get; }
        public StepItem Step { get; }

        public StepClickEventArgs(int index, StepItem step)
        {
            Index = index;
            Step = step;
        }
    }

    /// <summary>
    /// High-performance, single-HWND multi-step workflow pipeline stepper control.
    /// Provides vector node geometry, connecting progress tracks, interactive step selection,
    /// and zero GC allocations during OnPaint.
    /// </summary>
    [ToolboxItem(true)]
    [ToolboxBitmap(typeof(ZeroIcons), "StepBar.bmp")]
    [Category("ZeroUI - Workflow")]
    [DefaultEvent("StepClick")]
    [Description("High-performance single-HWND workflow pipeline stepper with vector nodes and progress tracks")]
    public class ZStepBar : ControlBase
    {
        private readonly StepBarModel _model = new StepBarModel();
        private readonly List<Rectangle> _nodeBounds = new List<Rectangle>();
        private readonly List<Rectangle> _stepHitAreas = new List<Rectangle>();

        private int _nodeSize = 32;
        private int _hoveredIndex = -1;
        private int _pressedIndex = -1;
        private bool _allowClickToNavigate = true;
        private Orientation _orientation = Orientation.Horizontal;

        public event EventHandler<StepClickEventArgs>? StepClick;
        public event EventHandler<int>? CurrentIndexChanged;

        public ZStepBar()
        {
            Size = new Size(600, 72);
            Font = new Font("Segoe UI", 9f);
            Cursor = Cursors.Default;

            _model.ModelChanged += (s, e) =>
            {
                RecalculateLayout();
                Invalidate();
            };

            _model.CurrentIndexChanged += (s, idx) =>
            {
                CurrentIndexChanged?.Invoke(this, idx);
                Invalidate();
            };

            ZeroUIConfig.CornerStyleChanged += (s, e) => Invalidate();
            ZeroUIConfig.FontChanged += (s, e) =>
            {
                Font = ZeroUIConfig.DefaultFont;
                RecalculateLayout();
                Invalidate();
            };
        }

        #region Properties

        [Browsable(false)]
        public StepBarModel Model => _model;

        [Browsable(false)]
        public IReadOnlyList<StepItem> Steps => _model.Steps;

        [Category("Workflow")]
        [DefaultValue(-1)]
        [Description("The 0-based index of the currently active workflow step.")]
        public int CurrentIndex
        {
            get => _model.CurrentIndex;
            set => _model.SetCurrentStep(value);
        }

        [Category("Workflow")]
        [DefaultValue(32)]
        [Description("Diameter of the step node circles in pixels.")]
        public int NodeSize
        {
            get => _nodeSize;
            set
            {
                if (value < 16) value = 16;
                if (value > 64) value = 64;
                if (_nodeSize != value)
                {
                    _nodeSize = value;
                    RecalculateLayout();
                    Invalidate();
                }
            }
        }

        [Category("Behavior")]
        [DefaultValue(true)]
        [Description("Enables user click navigation to activate enabled steps.")]
        public bool AllowClickToNavigate
        {
            get => _allowClickToNavigate;
            set => _allowClickToNavigate = value;
        }

        [Category("Appearance")]
        [DefaultValue(Orientation.Horizontal)]
        public Orientation Orientation
        {
            get => _orientation;
            set
            {
                if (_orientation != value)
                {
                    _orientation = value;
                    RecalculateLayout();
                    Invalidate();
                }
            }
        }

        #endregion

        #region Public Methods

        public void AddStep(string key, string title, string? description = null, StepStatus status = StepStatus.Pending)
        {
            _model.AddStep(key, title, description, status);
        }

        public void SetSteps(IEnumerable<StepItem> steps)
        {
            _model.SetSteps(steps);
        }

        public void SetStepStatus(int index, StepStatus status)
        {
            _model.SetStepStatus(index, status);
        }

        public bool NextStep() => _model.NextStep();

        public bool PrevStep() => _model.PrevStep();

        public void Clear() => _model.Clear();

        #endregion

        #region Layout & Hit-Testing

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            RecalculateLayout();
        }

        private void RecalculateLayout()
        {
            _nodeBounds.Clear();
            _stepHitAreas.Clear();

            int count = _model.Count;
            if (count == 0) return;

            int w = ClientRectangle.Width;
            int h = ClientRectangle.Height;

            if (_orientation == Orientation.Horizontal)
            {
                int padX = Math.Max(40, _nodeSize + 10);
                int usableWidth = Math.Max(10, w - (padX * 2));
                int stepSpacing = count > 1 ? usableWidth / (count - 1) : 0;
                int nodeY = 10;

                for (int i = 0; i < count; i++)
                {
                    int centerX = count == 1 ? w / 2 : padX + (i * stepSpacing);
                    int nodeX = centerX - (_nodeSize / 2);
                    var nodeRect = new Rectangle(nodeX, nodeY, _nodeSize, _nodeSize);
                    _nodeBounds.Add(nodeRect);

                    int hitLeft = Math.Max(0, centerX - (stepSpacing > 0 ? stepSpacing / 2 : padX));
                    int hitRight = Math.Min(w, centerX + (stepSpacing > 0 ? stepSpacing / 2 : padX));
                    _stepHitAreas.Add(new Rectangle(hitLeft, 0, hitRight - hitLeft, h));
                }
            }
            else
            {
                int padY = Math.Max(30, _nodeSize + 6);
                int usableHeight = Math.Max(10, h - (padY * 2));
                int stepSpacing = count > 1 ? usableHeight / (count - 1) : 0;
                int nodeX = 14;

                for (int i = 0; i < count; i++)
                {
                    int centerY = count == 1 ? h / 2 : padY + (i * stepSpacing);
                    int nodeY = centerY - (_nodeSize / 2);
                    var nodeRect = new Rectangle(nodeX, nodeY, _nodeSize, _nodeSize);
                    _nodeBounds.Add(nodeRect);
                    _stepHitAreas.Add(new Rectangle(0, Math.Max(0, centerY - (stepSpacing / 2)), w, stepSpacing > 0 ? stepSpacing : padY * 2));
                }
            }
        }

        private int HitTest(Point pt)
        {
            for (int i = 0; i < _stepHitAreas.Count; i++)
            {
                if (_stepHitAreas[i].Contains(pt))
                {
                    return i;
                }
            }
            return -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int hit = HitTest(e.Location);
            if (hit != _hoveredIndex)
            {
                _hoveredIndex = hit;
                Cursor = (_hoveredIndex >= 0 && _allowClickToNavigate && _model.Steps[_hoveredIndex].Enabled) 
                    ? Cursors.Hand 
                    : Cursors.Default;
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
            if (e.Button == MouseButtons.Left)
            {
                _pressedIndex = HitTest(e.Location);
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Left)
            {
                int hit = HitTest(e.Location);
                if (hit >= 0 && hit == _pressedIndex)
                {
                    var step = _model.Steps[hit];
                    if (step.Enabled)
                    {
                        StepClick?.Invoke(this, new StepClickEventArgs(hit, step));
                        if (_allowClickToNavigate)
                        {
                            _model.SetCurrentStep(hit);
                        }
                    }
                }
                _pressedIndex = -1;
                Invalidate();
            }
        }

        #endregion

        #region Rendering

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            var p = CurrentPalette;

            // Background
            using (var bgBrush = new SolidBrush(p.Surface))
            {
                g.FillRectangle(bgBrush, ClientRectangle);
            }

            int count = _model.Count;
            if (count == 0 || _nodeBounds.Count != count) return;

            // 1. Draw connecting progress tracks between nodes
            for (int i = 0; i < count - 1; i++)
            {
                var n1 = _nodeBounds[i];
                var n2 = _nodeBounds[i + 1];

                bool isCompletedTrack = (i < _model.CurrentIndex);

                Color trackColor = isCompletedTrack ? p.PrimaryAccent : p.Border;
                float trackWidth = isCompletedTrack ? 3f : 2f;

                using (var pen = new Pen(trackColor, trackWidth))
                {
                    if (_orientation == Orientation.Horizontal)
                    {
                        int y = n1.Y + (n1.Height / 2);
                        int startX = n1.Right + 4;
                        int endX = n2.Left - 4;
                        if (endX > startX)
                        {
                            g.DrawLine(pen, startX, y, endX, y);
                        }
                    }
                    else
                    {
                        int x = n1.X + (n1.Width / 2);
                        int startY = n1.Bottom + 4;
                        int endY = n2.Top - 4;
                        if (endY > startY)
                        {
                            g.DrawLine(pen, x, startY, x, endY);
                        }
                    }
                }
            }

            // 2. Draw nodes and text
            using var fontBold = new Font(Font, FontStyle.Bold);
            using var fontSmall = new Font(Font.FontFamily, Math.Max(7.5f, Font.Size - 1.5f), FontStyle.Regular);

            for (int i = 0; i < count; i++)
            {
                var step = _model.Steps[i];
                var nodeRect = _nodeBounds[i];
                bool isHovered = (i == _hoveredIndex && step.Enabled);
                bool isPressed = (i == _pressedIndex);

                DrawNode(g, step, nodeRect, i + 1, isHovered, isPressed, p, fontBold);
                DrawText(g, step, nodeRect, p, fontBold, fontSmall);
            }
        }

        private void DrawNode(Graphics g, StepItem step, Rectangle rect, int stepNumber, bool isHovered, bool isPressed, ZeroThemePalette p, Font fontBold)
        {
            Color nodeBg;
            Color nodeBorder;
            Color glyphColor;
            string glyphText = stepNumber.ToString();

            switch (step.Status)
            {
                case StepStatus.Completed:
                    nodeBg = p.Success;
                    nodeBorder = p.Success;
                    glyphColor = Color.White;
                    glyphText = "✔";
                    break;

                case StepStatus.Current:
                    nodeBg = p.PrimaryAccent;
                    nodeBorder = p.PrimaryAccent;
                    glyphColor = Color.White;
                    break;

                case StepStatus.Error:
                    nodeBg = p.Danger;
                    nodeBorder = p.Danger;
                    glyphColor = Color.White;
                    glyphText = "✖";
                    break;

                case StepStatus.Disabled:
                    nodeBg = p.Background;
                    nodeBorder = p.Border;
                    glyphColor = p.TextSecondary;
                    break;

                case StepStatus.Pending:
                default:
                    nodeBg = p.Surface;
                    nodeBorder = isHovered ? p.PrimaryAccent : p.Border;
                    glyphColor = isHovered ? p.PrimaryAccent : p.TextSecondary;
                    break;
            }

            // Hover / Pressed animation shift
            if (isPressed)
            {
                rect.Inflate(-1, -1);
            }
            else if (isHovered && step.Status == StepStatus.Current)
            {
                // Glow ring
                using var glowPen = new Pen(Color.FromArgb(60, p.PrimaryAccent), 4f);
                var glowRect = rect;
                glowRect.Inflate(2, 2);
                g.DrawEllipse(glowPen, glowRect);
            }

            // Fill circle
            using (var brush = new SolidBrush(nodeBg))
            {
                g.FillEllipse(brush, rect);
            }

            // Draw circle border
            using (var pen = new Pen(nodeBorder, 2f))
            {
                g.DrawEllipse(pen, rect);
            }

            // Center glyph
            TextRenderer.DrawText(g, glyphText, fontBold, rect, glyphColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        private void DrawText(Graphics g, StepItem step, Rectangle nodeRect, ZeroThemePalette p, Font fontBold, Font fontSmall)
        {
            if (_orientation == Orientation.Horizontal)
            {
                int textTop = nodeRect.Bottom + 6;
                int textWidth = Math.Max(120, _nodeSize * 3);
                int textLeft = (nodeRect.Left + (nodeRect.Width / 2)) - (textWidth / 2);

                var titleRect = new Rectangle(textLeft, textTop, textWidth, 18);
                Color titleColor = step.Status switch
                {
                    StepStatus.Current => p.PrimaryAccent,
                    StepStatus.Completed => p.TextPrimary,
                    StepStatus.Error => p.Danger,
                    StepStatus.Disabled => p.TextSecondary,
                    _ => p.TextSecondary
                };

                TextRenderer.DrawText(g, step.Title, fontBold, titleRect, titleColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.WordEllipsis | TextFormatFlags.NoPadding);

                if (!string.IsNullOrEmpty(step.Description))
                {
                    var descRect = new Rectangle(textLeft, titleRect.Bottom + 2, textWidth, 16);
                    TextRenderer.DrawText(g, step.Description, fontSmall, descRect, p.TextSecondary,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.WordEllipsis | TextFormatFlags.NoPadding);
                }
            }
            else
            {
                int textLeft = nodeRect.Right + 12;
                int textTop = nodeRect.Top + 2;
                int textWidth = Math.Max(50, ClientRectangle.Width - textLeft - 10);

                var titleRect = new Rectangle(textLeft, textTop, textWidth, 18);
                Color titleColor = step.Status == StepStatus.Current ? p.PrimaryAccent : p.TextPrimary;

                TextRenderer.DrawText(g, step.Title, fontBold, titleRect, titleColor,
                    TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordEllipsis | TextFormatFlags.NoPadding);

                if (!string.IsNullOrEmpty(step.Description))
                {
                    var descRect = new Rectangle(textLeft, titleRect.Bottom + 2, textWidth, 16);
                    TextRenderer.DrawText(g, step.Description, fontSmall, descRect, p.TextSecondary,
                        TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordEllipsis | TextFormatFlags.NoPadding);
                }
            }
        }

        #endregion
    }

    /// <summary>
    /// Backward-compatibility alias for <see cref="ZStepBar"/>.
    /// </summary>
    [Obsolete("ZeroStepBar is deprecated and will be removed in 5 release cycles. Please migrate to ZStepBar instead.")]
    [ToolboxItem(false)]
    public class ZeroStepBar : ZStepBar
    {
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZStepBar"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("StepBar is deprecated and will be removed in 5 release cycles. Please migrate to ZStepBar instead.")]
    [ToolboxItem(false)]
    public class StepBar : ZStepBar
    {
    }

    #endregion
}
