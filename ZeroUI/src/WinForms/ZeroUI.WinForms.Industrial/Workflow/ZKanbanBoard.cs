using System;

using ZeroUI.WinForms.Icons;using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.WinForms.Native;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Workflow
{
    public enum KanbanPriority
    {
        Normal,
        Urgent,
        Low
    }

    public class KanbanCard
    {
        public string OrderNo { get; set; } = "";
        public string ProductName { get; set; } = "";
        public int Quantity { get; set; }
        public KanbanPriority Priority { get; set; } = KanbanPriority.Normal;
        public string OperatorName { get; set; } = "";

        internal Rectangle Bounds;
    }

    public class KanbanColumn
    {
        public string Title { get; set; }
        public int WipLimit { get; set; }
        public List<KanbanCard> Cards { get; } = new List<KanbanCard>();

        internal Rectangle Bounds;

        public KanbanColumn(string title, int wipLimit = 0)
        {
            Title = title;
            WipLimit = wipLimit;
        }
    }

    public class KanbanCardClickedEventArgs : EventArgs
    {
        public KanbanCard Card { get; }
        public int ColumnIndex { get; }

        public KanbanCardClickedEventArgs(KanbanCard card, int colIndex)
        {
            Card = card;
            ColumnIndex = colIndex;
        }
    }

    /// <summary>
    /// Electronic Shopfloor Kanban Dispatching Board for MES manufacturing workflows.
    /// Supports multi-stage columns, Work-In-Progress (WIP) limit enforcement,
    /// priority tags, and interactive card transitions.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Industrial & SCADA")]
    [DefaultEvent("CardClicked")]
    [Description("Electronic Shopfloor Kanban Dispatching Board with WIP limits")]
    [ToolboxBitmap(typeof(ZeroIcons), "ZeroKanbanBoard.bmp")]
    public class ZKanbanBoard : Control
    {
        private readonly List<KanbanColumn> _columns = new List<KanbanColumn>();
        private KanbanCard? _hoveredCard;
        private int _hoveredColIndex = -1;

        public event EventHandler<KanbanCardClickedEventArgs>? CardClicked;

        public ZKanbanBoard()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor, true);

            Size = new Size(540, 240);
            BackColor = Color.Transparent;
            Font = new Font("Segoe UI", 8.5f);

            ZeroTheme.ThemeChanged += OnThemeChanged;
            InitializeDefaultBoard();
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

        [Browsable(false)]
        public List<KanbanColumn> Columns => _columns;

        private void InitializeDefaultBoard()
        {
            var col1 = new KanbanColumn("1. Material Prep", 5);
            col1.Cards.Add(new KanbanCard { OrderNo = "WO-8101", ProductName = "STM32 Microcontroller PCB", Quantity = 200, Priority = KanbanPriority.Normal, OperatorName = "Alex Nguyen" });
            col1.Cards.Add(new KanbanCard { OrderNo = "WO-8104", ProductName = "24V Buck Converter Module", Quantity = 500, Priority = KanbanPriority.Urgent, OperatorName = "Sarah Chen" });

            var col2 = new KanbanColumn("2. SMT Assembly", 3);
            col2.Cards.Add(new KanbanCard { OrderNo = "WO-8098", ProductName = "Keyence Optical Sensor", Quantity = 150, Priority = KanbanPriority.Normal, OperatorName = "David Lee" });
            col2.Cards.Add(new KanbanCard { OrderNo = "WO-8099", ProductName = "TMC2209 Stepper Driver", Quantity = 300, Priority = KanbanPriority.Urgent, OperatorName = "Michael Ross" });

            var col3 = new KanbanColumn("3. AOI / QC Inspection", 3);
            col3.Cards.Add(new KanbanCard { OrderNo = "WO-8092", ProductName = "ESP32 Wi-Fi IoT Gateway", Quantity = 400, Priority = KanbanPriority.Normal, OperatorName = "Emily Watson" });

            var col4 = new KanbanColumn("4. Packaging & Dispatch", 0);
            col4.Cards.Add(new KanbanCard { OrderNo = "WO-8085", ProductName = "IP67 CNC Aluminum Enclosure", Quantity = 100, Priority = KanbanPriority.Low, OperatorName = "John Miller" });

            _columns.Add(col1);
            _columns.Add(col2);
            _columns.Add(col3);
            _columns.Add(col4);
        }

        public void MoveCardNext(KanbanCard card)
        {
            for (int i = 0; i < _columns.Count; i++)
            {
                if (_columns[i].Cards.Contains(card))
                {
                    _columns[i].Cards.Remove(card);
                    if (i + 1 < _columns.Count)
                    {
                        _columns[i + 1].Cards.Add(card);
                    }
                    Invalidate();
                    break;
                }
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            KanbanCard? hitCard = null;
            int hitCol = -1;

            for (int c = 0; c < _columns.Count; c++)
            {
                var col = _columns[c];
                for (int i = 0; i < col.Cards.Count; i++)
                {
                    if (col.Cards[i].Bounds.Contains(e.Location))
                    {
                        hitCard = col.Cards[i];
                        hitCol = c;
                        break;
                    }
                }
                if (hitCard != null) break;
            }

            if (_hoveredCard != hitCard)
            {
                _hoveredCard = hitCard;
                _hoveredColIndex = hitCol;
                Cursor = hitCard != null ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hoveredCard != null)
            {
                _hoveredCard = null;
                _hoveredColIndex = -1;
                Invalidate();
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left && _hoveredCard != null)
            {
                CardClicked?.Invoke(this, new KanbanCardClickedEventArgs(_hoveredCard, _hoveredColIndex));
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            int w = Width;
            int h = Height;

            // Frame Background
            using (var brush = new SolidBrush(BackColor))
            {
                g.FillRectangle(brush, 0, 0, w, h);
            }
            using (var borderPen = new Pen(Color.FromArgb(51, 65, 85), 1f))
            {
                g.DrawRectangle(borderPen, 0, 0, w - 1, h - 1);
            }

            if (_columns.Count == 0) return;

            int pad = 8;
            int gap = 8;
            int availableW = w - (pad * 2) - ((_columns.Count - 1) * gap);
            int colW = availableW / _columns.Count;

            for (int c = 0; c < _columns.Count; c++)
            {
                var col = _columns[c];
                int cx = pad + (c * (colW + gap));
                int cy = pad;
                int ch = h - (pad * 2);
                col.Bounds = new Rectangle(cx, cy, colW, ch);

                DrawColumn(g, col);
            }
        }

        private void DrawColumn(Graphics g, KanbanColumn col)
        {
            var rect = col.Bounds;
            var palette = ZeroTheme.Colors;

            // Column container background
            using (var colBrush = new SolidBrush(palette.CardBackground))
            {
                g.FillRectangle(colBrush, rect);
            }
            using (var colPen = new Pen(palette.Border, 1f))
            {
                g.DrawRectangle(colPen, rect);
            }

            // Column Header
            int headerH = 28;
            var headerRect = new Rectangle(rect.X, rect.Y, rect.Width, headerH);
            using (var hBrush = new SolidBrush(palette.HeaderBackground))
            {
                g.FillRectangle(hBrush, headerRect);
            }

            // Column Title
            using (var titleFont = new Font("Segoe UI", 8f, FontStyle.Bold))
            using (var titleBrush = new SolidBrush(palette.TextPrimary))
            {
                g.DrawString(col.Title, titleFont, titleBrush, rect.X + 6, rect.Y + 6);
            }

            // WIP Limit Badge
            bool isOverWip = (col.WipLimit > 0 && col.Cards.Count > col.WipLimit);
            string countText = col.WipLimit > 0 ? $"{col.Cards.Count}/{col.WipLimit}" : $"{col.Cards.Count}";
            Color badgeBg = isOverWip ? Color.FromArgb(239, 68, 68) : palette.Border;

            using (var badgeFont = new Font("Segoe UI", 7f, FontStyle.Bold))
            {
                var bsz = g.MeasureString(countText, badgeFont);
                int bx = rect.Right - (int)bsz.Width - 12;
                int by = rect.Y + 5;
                using var bBrush = new SolidBrush(badgeBg);
                g.FillRectangle(bBrush, bx - 2, by, (int)bsz.Width + 6, 16);
                using var tBrush = new SolidBrush(palette.TextPrimary);
                g.DrawString(countText, badgeFont, tBrush, bx + 1, by + 1);
            }

            // Render Cards inside column
            int cardY = rect.Y + headerH + 6;
            int cardH = 54;
            int cardMargin = 4;

            for (int i = 0; i < col.Cards.Count; i++)
            {
                var card = col.Cards[i];
                card.Bounds = new Rectangle(rect.X + cardMargin, cardY, rect.Width - (cardMargin * 2), cardH);

                DrawCard(g, card, _hoveredCard == card);
                cardY += cardH + 6;
            }
        }

        private void DrawCard(Graphics g, KanbanCard card, bool isHovered)
        {
            var r = card.Bounds;
            var palette = ZeroTheme.Colors;

            // Card Background
            Color bg = isHovered ? palette.Hover : palette.Surface;
            using (var brush = new SolidBrush(bg))
            {
                g.FillRectangle(brush, r);
            }

            // Border
            Color borderC = isHovered ? palette.Primary : palette.Border;
            using (var pen = new Pen(borderC, isHovered ? 1.5f : 1f))
            {
                g.DrawRectangle(pen, r);
            }

            // Priority Indicator Stripe
            Color pColor = card.Priority switch
            {
                KanbanPriority.Urgent => Color.FromArgb(239, 68, 68),
                KanbanPriority.Low => palette.TextSecondary,
                _ => Color.FromArgb(59, 130, 246)
            };
            using (var pBrush = new SolidBrush(pColor))
            {
                g.FillRectangle(pBrush, r.X, r.Y, 3, r.Height);
            }

            // Order No
            using (var orderFont = new Font("Segoe UI", 7.5f, FontStyle.Bold))
            using (var orderBrush = new SolidBrush(palette.Primary))
            {
                g.DrawString(card.OrderNo, orderFont, orderBrush, r.X + 8, r.Y + 4);
            }

            // Qty
            using (var qtyFont = new Font("Segoe UI", 7f))
            using (var qtyBrush = new SolidBrush(palette.TextSecondary))
            {
                string q = $"{card.Quantity} PCS";
                var qsz = g.MeasureString(q, qtyFont);
                g.DrawString(q, qtyFont, qtyBrush, r.Right - qsz.Width - 4, r.Y + 4);
            }

            // Product Name
            using (var nameFont = new Font("Segoe UI", 7.5f))
            using (var nameBrush = new SolidBrush(palette.TextPrimary))
            {
                g.DrawString(card.ProductName, nameFont, nameBrush, r.X + 8, r.Y + 20);
            }

            // Operator
            using (var opFont = new Font("Segoe UI", 6.5f))
            using (var opBrush = new SolidBrush(palette.TextSecondary))
            {
                g.DrawString($"👤 {card.OperatorName}", opFont, opBrush, r.X + 8, r.Y + 36);
            }
        }
    }

    /// <summary>
    /// Legacy alias for <see cref="ZKanbanBoard"/>.
    /// Preserved for backward compatibility.
    /// </summary>
    [Obsolete("ZeroKanbanBoard is deprecated and will be removed in 5 release cycles. Please migrate to ZKanbanBoard instead.")]
    [ToolboxItem(false)]
    public class ZeroKanbanBoard : ZKanbanBoard
    {
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZKanbanBoard"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("KanbanBoard is deprecated and will be removed in 5 release cycles. Please migrate to ZKanbanBoard instead.")]
    [ToolboxItem(false)]
    public class KanbanBoard : ZKanbanBoard
    {
    }

    #endregion
}
