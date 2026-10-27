using System;

using ZeroUI.WinForms.Icons;using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Industrial
{
    public enum DefectStatus
    {
        Pass,
        Fail,
        Warning,
        Untested
    }

    public class DefectSlot
    {
        public int Row { get; set; }
        public int Column { get; set; }
        public string Code { get; set; } = "";
        public DefectStatus Status { get; set; } = DefectStatus.Pass;
        public string DefectDetail { get; set; } = "";

        internal Rectangle Bounds;
    }

    public class DefectSlotClickedEventArgs : EventArgs
    {
        public DefectSlot Slot { get; }

        public DefectSlotClickedEventArgs(DefectSlot slot)
        {
            Slot = slot;
        }
    }

    /// <summary>
    /// 2D Panel & Wafer Defect Inspection Matrix for AOI, SMT, and QC workstations.
    /// Visualizes multi-unit PCB panels or carrier trays with color-coded inspection results,
    /// interactive hover tooltips, and drill-down inspection events.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Industrial & SCADA")]
    [DefaultEvent("SlotClicked")]
    [Description("2D Defect Inspection Matrix for PCB panels, wafers, and QC carrier trays")]
    [ToolboxBitmap(typeof(ZeroIcons), "ZeroDefectMatrix.bmp")]
    public class ZDefectMatrix : Control
    {
        private int _rows = 3;
        private int _columns = 6;
        private DefectSlot[,] _slots = new DefectSlot[0, 0];
        private DefectSlot? _hoveredSlot;

        private string _title = "SMT Panel AOI Inspection (Array 3x6)";

        public event EventHandler<DefectSlotClickedEventArgs>? SlotClicked;

        public ZDefectMatrix()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor, true);

            Size = new Size(360, 190);
            BackColor = Color.Transparent;
            Font = new Font("Segoe UI", 8f);

            ZeroTheme.ThemeChanged += OnThemeChanged;
            InitializeSlots();
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

        [Category("Matrix Layout")]
        [DefaultValue(3)]
        public int Rows
        {
            get => _rows;
            set
            {
                _rows = Math.Max(1, Math.Min(12, value));
                InitializeSlots();
                Invalidate();
            }
        }

        [Category("Matrix Layout")]
        [DefaultValue(6)]
        public int Columns
        {
            get => _columns;
            set
            {
                _columns = Math.Max(1, Math.Min(16, value));
                InitializeSlots();
                Invalidate();
            }
        }

        [Category("Appearance")]
        [DefaultValue("SMT Panel AOI Inspection (Array 3x6)")]
        public string Title
        {
            get => _title;
            set { _title = value ?? ""; Invalidate(); }
        }

        private void InitializeSlots()
        {
            _slots = new DefectSlot[_rows, _columns];
            int count = 1;
            for (int r = 0; r < _rows; r++)
            {
                for (int c = 0; c < _columns; c++)
                {
                    _slots[r, c] = new DefectSlot
                    {
                        Row = r,
                        Column = c,
                        Code = $"U{count:D2}",
                        Status = DefectStatus.Pass,
                        DefectDetail = "OK"
                    };
                    count++;
                }
            }

            // Mock 2 defect slots for demo realism
            if (_rows >= 3 && _columns >= 6)
            {
                _slots[0, 2].Status = DefectStatus.Fail;
                _slots[0, 2].DefectDetail = "Solder Bridge (IC03)";

                _slots[1, 4].Status = DefectStatus.Warning;
                _slots[1, 4].DefectDetail = "Shifted Resistor (R12)";
            }
        }

        public void SetSlotStatus(int row, int column, DefectStatus status, string detail = "")
        {
            if (row >= 0 && row < _rows && column >= 0 && column < _columns)
            {
                _slots[row, column].Status = status;
                _slots[row, column].DefectDetail = detail;
                Invalidate();
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            DefectSlot? hit = null;

            for (int r = 0; r < _rows; r++)
            {
                for (int c = 0; c < _columns; c++)
                {
                    if (_slots[r, c].Bounds.Contains(e.Location))
                    {
                        hit = _slots[r, c];
                        break;
                    }
                }
                if (hit != null) break;
            }

            if (_hoveredSlot != hit)
            {
                _hoveredSlot = hit;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hoveredSlot != null)
            {
                _hoveredSlot = null;
                Invalidate();
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left && _hoveredSlot != null)
            {
                SlotClicked?.Invoke(this, new DefectSlotClickedEventArgs(_hoveredSlot));
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            int w = Width;
            int h = Height;
            var palette = ZeroTheme.Colors;

            // 1. Frame Background
            using (var brush = new SolidBrush(palette.CardBackground))
            {
                g.FillRectangle(brush, 0, 0, w, h);
            }
            using (var borderPen = new Pen(palette.Border, 1f))
            {
                g.DrawRectangle(borderPen, 0, 0, w - 1, h - 1);
            }

            // 2. Header Title & Counts
            int passCount = 0, failCount = 0, warnCount = 0;
            for (int r = 0; r < _rows; r++)
            {
                for (int c = 0; c < _columns; c++)
                {
                    if (_slots[r, c].Status == DefectStatus.Pass) passCount++;
                    else if (_slots[r, c].Status == DefectStatus.Fail) failCount++;
                    else if (_slots[r, c].Status == DefectStatus.Warning) warnCount++;
                }
            }

            using (var titleFont = new Font("Segoe UI", 8.5f, FontStyle.Bold))
            using (var titleBrush = new SolidBrush(palette.TextPrimary))
            {
                g.DrawString(_title, titleFont, titleBrush, 8, 6);
            }

            // Stats badge
            string stats = $"Pass: {passCount} | Fail: {failCount} | Warn: {warnCount}";
            using (var statFont = new Font("Segoe UI", 7.5f))
            using (var statBrush = new SolidBrush(palette.TextSecondary))
            {
                var sz = g.MeasureString(stats, statFont);
                g.DrawString(stats, statFont, statBrush, w - sz.Width - 8, 8);
            }

            // 3. Grid Cells Layout
            int startY = 28;
            int legendH = 22;
            int availableW = w - 16;
            int availableH = h - startY - legendH - 6;

            int cellW = Math.Max(20, (availableW - ((_columns - 1) * 6)) / _columns);
            int cellH = Math.Max(20, (availableH - ((_rows - 1) * 6)) / _rows);

            int startX = (w - ((cellW * _columns) + ((_columns - 1) * 6))) / 2;

            for (int r = 0; r < _rows; r++)
            {
                for (int c = 0; c < _columns; c++)
                {
                    var slot = _slots[r, c];
                    int x = startX + (c * (cellW + 6));
                    int y = startY + (r * (cellH + 6));
                    slot.Bounds = new Rectangle(x, y, cellW, cellH);

                    DrawCell(g, slot, _hoveredSlot == slot);
                }
            }

            // 4. Bottom Legend
            int legY = h - 20;
            DrawLegendItem(g, 10, legY, Color.FromArgb(16, 185, 129), "Pass (OK)");
            DrawLegendItem(g, 90, legY, Color.FromArgb(239, 68, 68), "Fail (Defect)");
            DrawLegendItem(g, 180, legY, Color.FromArgb(245, 158, 11), "Warning");
            DrawLegendItem(g, 260, legY, Color.FromArgb(100, 116, 139), "Untested");
        }

        private void DrawCell(Graphics g, DefectSlot slot, bool isHovered)
        {
            Rectangle rect = slot.Bounds;
            Color baseColor;
            Color borderColor;

            switch (slot.Status)
            {
                case DefectStatus.Fail:
                    baseColor = Color.FromArgb(239, 68, 68);
                    borderColor = Color.FromArgb(248, 113, 113);
                    break;
                case DefectStatus.Warning:
                    baseColor = Color.FromArgb(245, 158, 11);
                    borderColor = Color.FromArgb(251, 191, 36);
                    break;
                case DefectStatus.Untested:
                    baseColor = Color.FromArgb(51, 65, 85);
                    borderColor = Color.FromArgb(71, 85, 105);
                    break;
                default:
                    baseColor = Color.FromArgb(16, 185, 129);
                    borderColor = Color.FromArgb(52, 211, 153);
                    break;
            }

            // Cell Fill
            using (var brush = new LinearGradientBrush(new Point(rect.X, rect.Y), new Point(rect.X, rect.Bottom), baseColor, Color.FromArgb(200, baseColor.R / 2, baseColor.G / 2, baseColor.B / 2)))
            {
                g.FillRectangle(brush, rect);
            }

            // Border (highlighted when hovered)
            using (var pen = new Pen(isHovered ? Color.White : borderColor, isHovered ? 2f : 1f))
            {
                g.DrawRectangle(pen, rect);
            }

            // Slot Code Text
            using var font = new Font("Segoe UI", 8f, FontStyle.Bold);
            using var textBrush = new SolidBrush(Color.White);
            var sz = g.MeasureString(slot.Code, font);
            g.DrawString(slot.Code, font, textBrush, rect.X + (rect.Width - sz.Width) / 2, rect.Y + (rect.Height - sz.Height) / 2);
        }

        private void DrawLegendItem(Graphics g, int x, int y, Color color, string label)
        {
            using var brush = new SolidBrush(color);
            g.FillRectangle(brush, x, y + 2, 8, 8);
            using var font = new Font("Segoe UI", 7.5f);
            using var txtBrush = new SolidBrush(ZeroTheme.Colors.TextSecondary);
            g.DrawString(label, font, txtBrush, x + 12, y);
        }
    }

    /// <summary>
    /// Legacy alias for <see cref="ZDefectMatrix"/>.
    /// Preserved for backward compatibility.
    /// </summary>
    [Obsolete("ZeroDefectMatrix is deprecated and will be removed in 5 release cycles. Please migrate to ZDefectMatrix instead.")]
    [ToolboxItem(false)]
    public class ZeroDefectMatrix : ZDefectMatrix
    {
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZDefectMatrix"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("DefectMatrix is deprecated and will be removed in 5 release cycles. Please migrate to ZDefectMatrix instead.")]
    [ToolboxItem(false)]
    public class DefectMatrix : ZDefectMatrix
    {
    }

    #endregion
}
