using System;

using ZeroUI.WinForms.Icons;using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using ZeroUI.WinForms.Rendering;
using ZeroUI.WinForms.Theme;
using ZeroUI.WinForms.Warehouse.Models;

namespace ZeroUI.WinForms.Warehouse
{
    /// <summary>
    /// Industrial Smart Lot Allocation Selector for Warehouse Outward Logistics.
    /// Supports automatic FIFO (First In First Out) and FEFO (First Expired First Out) allocation,
    /// safety quarantine/expiry locks, and explicit 1-Way Data Flow (Populate / CollectSelectedLots).
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Warehouse & Logistics")]
    [Description("Industrial Lot Selector with automated FIFO / FEFO allocation logic")]
    [ToolboxBitmap(typeof(ZeroIcons), "ZeroLotSelector.bmp")]
    public class ZLotSelector : Control
    {
        private string _productCode = "ABC-001";
        private string _productName = "Cylindrical Bushing φ32";
        private decimal _requiredQuantity = 800;
        private LotAllocationStrategy _strategy = LotAllocationStrategy.FIFO;
        private readonly List<LotItemModel> _lots = new List<LotItemModel>();

        private int _hoveredRowIndex = -1;
        private const int HeaderHeight = 65;
        private const int TableHeaderHeight = 28;
        private const int RowHeight = 32;

        public event EventHandler? SelectionChanged;

        public ZLotSelector()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor, true);

            Size = new Size(540, 240);
            BackColor = Color.Transparent;
            Font = new Font("Segoe UI", 9f, FontStyle.Regular);

            LoadSampleLots();
        }

        private void LoadSampleLots()
        {
            _lots.Clear();
            _lots.Add(new LotItemModel
            {
                LotNumber = "L001",
                AvailableQuantity = 500,
                TotalQuantity = 500,
                ImportDate = DateTime.Today.AddMonths(-6),
                ExpiryDate = DateTime.Today.AddMonths(4),
                Status = LotStatus.Available,
                AllocatedQuantity = 500,
                IsSelected = true
            });
            _lots.Add(new LotItemModel
            {
                LotNumber = "L002",
                AvailableQuantity = 300,
                TotalQuantity = 300,
                ImportDate = DateTime.Today.AddMonths(-4),
                ExpiryDate = DateTime.Today.AddMonths(6),
                Status = LotStatus.Available,
                AllocatedQuantity = 300,
                IsSelected = true
            });
            _lots.Add(new LotItemModel
            {
                LotNumber = "L003",
                AvailableQuantity = 450,
                TotalQuantity = 450,
                ImportDate = DateTime.Today.AddMonths(-2),
                ExpiryDate = DateTime.Today.AddMonths(10),
                Status = LotStatus.Available,
                AllocatedQuantity = 0,
                IsSelected = false
            });
            _lots.Add(new LotItemModel
            {
                LotNumber = "L004",
                AvailableQuantity = 100,
                TotalQuantity = 100,
                ImportDate = DateTime.Today.AddMonths(-8),
                ExpiryDate = DateTime.Today.AddMonths(-1),
                Status = LotStatus.Expired,
                AllocatedQuantity = 0,
                IsSelected = false
            });
            _lots.Add(new LotItemModel
            {
                LotNumber = "L005",
                AvailableQuantity = 200,
                TotalQuantity = 200,
                ImportDate = DateTime.Today.AddMonths(-1),
                ExpiryDate = DateTime.Today.AddMonths(12),
                Status = LotStatus.Quarantined,
                AllocatedQuantity = 0,
                IsSelected = false
            });
        }

        #region Public Properties

        [Category("Data")]
        [DefaultValue("ABC-001")]
        public string ProductCode
        {
            get => _productCode;
            set { _productCode = value ?? ""; Invalidate(); }
        }

        [Category("Data")]
        [DefaultValue(800)]
        public decimal RequiredQuantity
        {
            get => _requiredQuantity;
            set { _requiredQuantity = Math.Max(0, value); Invalidate(); }
        }

        [Category("Behavior")]
        [DefaultValue(LotAllocationStrategy.FIFO)]
        public LotAllocationStrategy Strategy
        {
            get => _strategy;
            set
            {
                _strategy = value;
                if (_strategy != LotAllocationStrategy.Manual)
                {
                    AutoAllocate(_requiredQuantity, _strategy);
                }
                Invalidate();
            }
        }

        [Browsable(false)]
        public decimal TotalAllocatedQuantity => _lots.Where(l => l.IsSelected).Sum(l => l.AllocatedQuantity);

        #endregion

        #region 1-Way Data Flow API

        /// <summary>
        /// Populates the LotSelector with a product and list of available lots.
        /// Thread-safe via InvokeIfRequired marshaling.
        /// </summary>
        public void Populate(string productCode, string productName, decimal requiredQuantity, IEnumerable<LotItemModel> lots, LotAllocationStrategy strategy = LotAllocationStrategy.FIFO)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => Populate(productCode, productName, requiredQuantity, lots, strategy)));
                return;
            }

            _productCode = productCode ?? "";
            _productName = productName ?? "";
            _requiredQuantity = requiredQuantity;
            _strategy = strategy;

            _lots.Clear();
            if (lots != null)
            {
                _lots.AddRange(lots);
            }

            if (_strategy != LotAllocationStrategy.Manual)
            {
                AutoAllocate(_requiredQuantity, _strategy);
            }

            Invalidate();
        }

        /// <summary>
        /// Collects all currently selected lots and their allocated quantities.
        /// </summary>
        public List<SelectedLotModel> CollectSelectedLots()
        {
            var result = new List<SelectedLotModel>();
            for (int i = 0; i < _lots.Count; i++)
            {
                if (_lots[i].IsSelected && _lots[i].AllocatedQuantity > 0)
                {
                    result.Add(new SelectedLotModel
                    {
                        LotNumber = _lots[i].LotNumber,
                        AllocatedQuantity = _lots[i].AllocatedQuantity
                    });
                }
            }
            return result;
        }

        /// <summary>
        /// Executes automated lot allocation according to FIFO (oldest import date) or FEFO (earliest expiry).
        /// </summary>
        public void AutoAllocate(decimal requiredQty, LotAllocationStrategy strategy)
        {
            _strategy = strategy;
            _requiredQuantity = requiredQty;

            // 1. Reset all allocations
            for (int i = 0; i < _lots.Count; i++)
            {
                _lots[i].AllocatedQuantity = 0;
                _lots[i].IsSelected = false;
            }

            if (strategy == LotAllocationStrategy.Manual || requiredQty <= 0)
            {
                Invalidate();
                SelectionChanged?.Invoke(this, EventArgs.Empty);
                return;
            }

            // 2. Filter eligible lots (Available only, ignore Quarantined and Expired)
            var eligibleLots = _lots.Where(l => l.Status == LotStatus.Available && l.AvailableQuantity > 0);

            // 3. Sort according to strategy
            if (strategy == LotAllocationStrategy.FIFO)
            {
                eligibleLots = eligibleLots.OrderBy(l => l.ImportDate);
            }
            else if (strategy == LotAllocationStrategy.FEFO)
            {
                eligibleLots = eligibleLots.OrderBy(l => l.ExpiryDate);
            }

            decimal remainingToAllocate = requiredQty;

            foreach (var lot in eligibleLots)
            {
                if (remainingToAllocate <= 0) break;

                decimal take = Math.Min(lot.AvailableQuantity, remainingToAllocate);
                lot.AllocatedQuantity = take;
                lot.IsSelected = true;
                remainingToAllocate -= take;
            }

            Invalidate();
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        #endregion

        #region Interaction

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int tableStartY = HeaderHeight + TableHeaderHeight;
            if (e.Y >= tableStartY)
            {
                int rowIndex = (e.Y - tableStartY) / RowHeight;
                if (rowIndex >= 0 && rowIndex < _lots.Count)
                {
                    if (_hoveredRowIndex != rowIndex)
                    {
                        _hoveredRowIndex = rowIndex;
                        Invalidate();
                    }
                    return;
                }
            }

            if (_hoveredRowIndex != -1)
            {
                _hoveredRowIndex = -1;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hoveredRowIndex = -1;
            Invalidate();
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);

            // 1. Click on Strategy Pills
            if (e.Y >= 34 && e.Y <= 58)
            {
                if (e.X >= 12 && e.X <= 110)
                {
                    AutoAllocate(_requiredQuantity, LotAllocationStrategy.FIFO);
                    return;
                }
                if (e.X >= 115 && e.X <= 220)
                {
                    AutoAllocate(_requiredQuantity, LotAllocationStrategy.FEFO);
                    return;
                }
            }

            // 2. Click on a row
            int tableStartY = HeaderHeight + TableHeaderHeight;
            if (e.Y >= tableStartY)
            {
                int rowIndex = (e.Y - tableStartY) / RowHeight;
                if (rowIndex >= 0 && rowIndex < _lots.Count)
                {
                    var lot = _lots[rowIndex];
                    if (lot.Status == LotStatus.Available)
                    {
                        lot.IsSelected = !lot.IsSelected;
                        if (lot.IsSelected && lot.AllocatedQuantity == 0)
                        {
                            lot.AllocatedQuantity = lot.AvailableQuantity;
                        }
                        else if (!lot.IsSelected)
                        {
                            lot.AllocatedQuantity = 0;
                        }
                        Invalidate();
                        SelectionChanged?.Invoke(this, EventArgs.Empty);
                    }
                }
            }
        }

        #endregion

        #region Rendering Engine

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            int w = Width;
            int h = Height;

            // 1. Enclosure Card
            using (var cardPath = ZeroUIConfig.CreateRoundedRectangle(new Rectangle(0, 0, w - 1, h - 1), 8))
            {
                using var bgBrush = new SolidBrush(Color.White);
                g.FillPath(bgBrush, cardPath);
                using var borderPen = new Pen(Color.FromArgb(226, 232, 240), 1f);
                g.DrawPath(borderPen, cardPath);
            }

            // 2. Header Area
            var titleFont = ZeroFontCache.Get("Segoe UI", 9.5f, FontStyle.Bold);
            var boldFont = ZeroFontCache.Get("Segoe UI", 8.5f, FontStyle.Bold);
            using (var textBrush = new SolidBrush(Color.FromArgb(30, 41, 59)))
            {
                g.DrawString($"Lot Allocation: {_productCode}", titleFont, textBrush, 12, 10);

                decimal allocatedTotal = TotalAllocatedQuantity;
                bool isFulfilled = allocatedTotal >= _requiredQuantity;
                Color statusColor = isFulfilled ? Color.FromArgb(16, 185, 129) : Color.FromArgb(245, 158, 11);

                string allocSummary = $"Required: {_requiredQuantity:N0} | Allocated: {allocatedTotal:N0} / {_requiredQuantity:N0}";
                var summarySize = g.MeasureString(allocSummary, boldFont);

                using (var allocBrush = new SolidBrush(statusColor))
                {
                    g.DrawString(allocSummary, boldFont, allocBrush, w - summarySize.Width - 14, 12);
                }
            }

            // Strategy Selector Buttons
            DrawStrategyPill(g, 12, 34, 98, 22, "FIFO (Oldest In)", _strategy == LotAllocationStrategy.FIFO);
            DrawStrategyPill(g, 116, 34, 102, 22, "FEFO (Earliest Exp)", _strategy == LotAllocationStrategy.FEFO);

            // 3. Table Header
            int tableY = HeaderHeight;
            Rectangle thRect = new Rectangle(1, tableY, w - 2, TableHeaderHeight);
            using (var thBrush = new SolidBrush(Color.FromArgb(248, 250, 252)))
            {
                g.FillRectangle(thBrush, thRect);
            }
            using (var sepPen = new Pen(Color.FromArgb(226, 232, 240), 1f))
            {
                g.DrawLine(sepPen, 1, tableY, w - 2, tableY);
                g.DrawLine(sepPen, 1, tableY + TableHeaderHeight, w - 2, tableY + TableHeaderHeight);
            }

            // Column Layout
            int colLot = 14;
            int colQty = 110;
            int colExp = 210;
            int colStatus = 310;
            int colSelect = w - 90;

            var thFont = ZeroFontCache.Get("Segoe UI", 8f, FontStyle.Bold);
            using (var thBrush = new SolidBrush(Color.FromArgb(71, 85, 105)))
            {
                g.DrawString("LOT NO", thFont, thBrush, colLot, tableY + 6);
                g.DrawString("AVAILABLE QTY", thFont, thBrush, colQty, tableY + 6);
                g.DrawString("EXPIRY DATE", thFont, thBrush, colExp, tableY + 6);
                g.DrawString("STATUS", thFont, thBrush, colStatus, tableY + 6);
                g.DrawString("ALLOCATED", thFont, thBrush, colSelect, tableY + 6);
            }

            // 4. Rows
            int rowY = tableY + TableHeaderHeight;
            var cellFont = ZeroFontCache.Get("Segoe UI", 8.5f, FontStyle.Regular);
            var cellBold = ZeroFontCache.Get("Segoe UI", 8.5f, FontStyle.Bold);
            using (var textBrush = new SolidBrush(Color.FromArgb(30, 41, 59)))
            using (var subBrush = new SolidBrush(Color.FromArgb(100, 116, 139)))
            using (var rowBrush = new SolidBrush(Color.Empty))
            using (var linePen = new Pen(Color.FromArgb(241, 245, 249), 1f))
            using (var selBrush = new SolidBrush(Color.Empty))
            using (var lockBrush = new SolidBrush(Color.FromArgb(203, 213, 225)))
            {
                for (int i = 0; i < _lots.Count; i++)
                {
                    if (rowY + RowHeight > h - 4) break;

                    var lot = _lots[i];
                    bool isHover = (i == _hoveredRowIndex);
                    bool isSelected = lot.IsSelected;

                    // Row background
                    Color rowBg = isSelected
                        ? Color.FromArgb(238, 242, 255) // Light Indigo
                        : (isHover ? Color.FromArgb(248, 250, 252) : Color.White);

                    rowBrush.Color = rowBg;
                    g.FillRectangle(rowBrush, 1, rowY, w - 2, RowHeight);

                    // Bottom row line
                    g.DrawLine(linePen, 1, rowY + RowHeight, w - 2, rowY + RowHeight);

                    int textY = rowY + 7;

                    // Col 1: Lot Number
                    g.DrawString(lot.LotNumber, cellBold, textBrush, colLot, textY);

                    // Col 2: Qty
                    g.DrawString($"{lot.AvailableQuantity:N0}", cellFont, textBrush, colQty, textY);

                    // Col 3: Expiry Date
                    string expStr = lot.ExpiryDate.ToString("MM/yyyy");
                    g.DrawString(expStr, cellFont, subBrush, colExp, textY);

                    // Col 4: Status Badge
                    DrawStatusBadge(g, colStatus, textY - 1, lot.Status);

                    // Col 5: Select Checkbox / Allocated Qty
                    if (lot.Status == LotStatus.Available)
                    {
                        string selText = isSelected ? $"[✓] {lot.AllocatedQuantity:N0}" : "[   ] 0";
                        selBrush.Color = isSelected ? Color.FromArgb(79, 70, 229) : Color.FromArgb(148, 163, 184);
                        g.DrawString(selText, cellBold, selBrush, colSelect, textY);
                    }
                    else
                    {
                        g.DrawString("[Locked]", cellFont, lockBrush, colSelect, textY);
                    }

                    rowY += RowHeight;
                }
            }
        }

        private void DrawStrategyPill(Graphics g, int x, int y, int w, int h, string label, bool isActive)
        {
            Rectangle rect = new Rectangle(x, y, w, h);
            using var path = ZeroUIConfig.CreateRoundedRectangle(rect, 4);

            Color bg = isActive ? Color.FromArgb(79, 70, 229) : Color.FromArgb(241, 245, 249);
            Color text = isActive ? Color.White : Color.FromArgb(71, 85, 105);

            using (var brush = new SolidBrush(bg))
            {
                g.FillPath(brush, path);
            }
            var font = ZeroFontCache.Get("Segoe UI", 7.5f, FontStyle.Bold);
            using (var textBrush = new SolidBrush(text))
            {
                var sz = g.MeasureString(label, font);
                g.DrawString(label, font, textBrush, x + (w - sz.Width) / 2f, y + 4);
            }
        }

        private void DrawStatusBadge(Graphics g, int x, int y, LotStatus status)
        {
            string label;
            Color bg;
            Color text;

            switch (status)
            {
                case LotStatus.Available:
                    label = "● Available";
                    bg = Color.FromArgb(236, 253, 245);
                    text = Color.FromArgb(5, 150, 105);
                    break;
                case LotStatus.Quarantined:
                    label = "⚠ Quarantine";
                    bg = Color.FromArgb(254, 243, 199);
                    text = Color.FromArgb(217, 119, 6);
                    break;
                case LotStatus.Expired:
                    label = "❌ Expired";
                    bg = Color.FromArgb(254, 242, 242);
                    text = Color.FromArgb(220, 38, 38);
                    break;
                default:
                    label = "Low Stock";
                    bg = Color.FromArgb(241, 245, 249);
                    text = Color.FromArgb(100, 116, 139);
                    break;
            }

            var font = ZeroFontCache.Get("Segoe UI", 7.5f, FontStyle.Bold);
            using var brush = new SolidBrush(bg);
            using var textBrush = new SolidBrush(text);

            var sz = g.MeasureString(label, font);
            Rectangle rect = new Rectangle(x, y, (int)sz.Width + 8, 18);
            using var path = ZeroUIConfig.CreateRoundedRectangle(rect, 3);
            g.FillPath(brush, path);
            g.DrawString(label, font, textBrush, x + 4, y + 2);
        }

        #endregion
    }

    /// <summary>
    /// Legacy alias for <see cref="ZLotSelector"/>.
    /// Preserved for backward compatibility.
    /// </summary>
    [Obsolete("ZeroLotSelector is deprecated and will be removed in 5 release cycles. Please migrate to ZLotSelector instead.")]
    [ToolboxItem(false)]
    public class ZeroLotSelector : ZLotSelector
    {
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZLotSelector"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("LotSelector is deprecated and will be removed in 5 release cycles. Please migrate to ZLotSelector instead.")]
    [ToolboxItem(false)]
    public class LotSelector : ZLotSelector
    {
    }

    #endregion
}
