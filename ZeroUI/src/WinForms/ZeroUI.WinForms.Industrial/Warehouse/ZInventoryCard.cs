using System;

using ZeroUI.WinForms.Icons;using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.WinForms.Rendering;
using ZeroUI.WinForms.Theme;
using ZeroUI.WinForms.Warehouse.Models;

namespace ZeroUI.WinForms.Warehouse
{
    /// <summary>
    /// Industrial Inventory Stock Metric Card for Warehouse & MES.
    /// Displays immediate stock telemetry: Available, Waiting, Reserved quantities,
    /// warehouse location, unit of measure, and visual allocation segment distribution.
    /// Follows the 1-Way Data Flow standard (Populate / CollectData).
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Warehouse & Logistics")]
    [DefaultProperty("ProductCode")]
    [Description("Industrial Inventory Stock Metric Card with allocation distribution")]
    [ToolboxBitmap(typeof(ZeroIcons), "ZeroInventoryCard.bmp")]
    public class ZInventoryCard : Control
    {
        private InventoryStockModel _data = new InventoryStockModel
        {
            ProductCode = "ABC-001",
            ProductName = "Cylindrical Bushing φ32 mm",
            AvailableQuantity = 1250,
            WaitingQuantity = 120,
            ReservedQuantity = 300,
            WarehouseCode = "WH01",
            WarehouseName = "Main Central Warehouse",
            LocationBin = "Zone A - Rack 03",
            UnitOfMeasure = "Pcs"
        };

        public ZInventoryCard()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor, true);

            Size = new Size(280, 190);
            BackColor = Color.Transparent;
            Font = new Font("Segoe UI", 9f, FontStyle.Regular);

            ZeroTheme.ThemeChanged += OnThemeChanged;
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

        #region Public Properties

        [Category("Data")]
        [DefaultValue("ABC-001")]
        public string ProductCode
        {
            get => _data.ProductCode;
            set { _data.ProductCode = value ?? ""; Invalidate(); }
        }

        [Category("Data")]
        [DefaultValue("Cylindrical Bushing φ32 mm")]
        public new string ProductName
        {
            get => _data.ProductName;
            set { _data.ProductName = value ?? ""; Invalidate(); }
        }

        [Category("Data")]
        [DefaultValue(1250)]
        public decimal AvailableQuantity
        {
            get => _data.AvailableQuantity;
            set { _data.AvailableQuantity = value; Invalidate(); }
        }

        [Category("Data")]
        [DefaultValue(120)]
        public decimal WaitingQuantity
        {
            get => _data.WaitingQuantity;
            set { _data.WaitingQuantity = value; Invalidate(); }
        }

        [Category("Data")]
        [DefaultValue(300)]
        public decimal ReservedQuantity
        {
            get => _data.ReservedQuantity;
            set { _data.ReservedQuantity = value; Invalidate(); }
        }

        [Category("Data")]
        [DefaultValue("WH01")]
        public string WarehouseCode
        {
            get => _data.WarehouseCode;
            set { _data.WarehouseCode = value ?? ""; Invalidate(); }
        }

        [Category("Data")]
        [DefaultValue("Pcs")]
        public string UnitOfMeasure
        {
            get => _data.UnitOfMeasure;
            set { _data.UnitOfMeasure = value ?? ""; Invalidate(); }
        }

        #endregion

        #region 1-Way Data Flow API (Enterprise Standard)

        /// <summary>
        /// Explicit 1-way inward data flow. Populates the control with new stock metrics.
        /// Thread-safe via InvokeIfRequired marshaling.
        /// </summary>
        public void Populate(InventoryStockModel data)
        {
            if (data == null) return;

            if (InvokeRequired)
            {
                Invoke(new Action(() => Populate(data)));
                return;
            }

            _data = data;
            Invalidate();
        }

        /// <summary>
        /// Explicit 1-way outward data flow. Collects current stock snapshot.
        /// </summary>
        public InventoryStockModel CollectData() => _data;

        public void Clear()
        {
            _data = new InventoryStockModel();
            Invalidate();
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
            var palette = ZeroTheme.Colors;

            // 1. Outer Container Card
            Rectangle cardRect = new Rectangle(0, 0, w - 1, h - 1);
            using (var cardPath = ZeroUIConfig.CreateRoundedRectangle(cardRect, 8))
            {
                using var bgBrush = new SolidBrush(palette.CardBackground);
                g.FillPath(bgBrush, cardPath);

                using var borderPen = new Pen(palette.Border, 1f);
                g.DrawPath(borderPen, cardPath);
            }

            // 2. Product Header Area
            var codeFont = ZeroFontCache.Get("Segoe UI", 11f, FontStyle.Bold);
            var nameFont = ZeroFontCache.Get("Segoe UI", 8.5f, FontStyle.Regular);
            using (var codeBrush = new SolidBrush(palette.TextPrimary))
            using (var nameBrush = new SolidBrush(palette.TextSecondary))
            {
                g.DrawString(_data.ProductCode, codeFont, codeBrush, 14, 10);
                g.DrawString(_data.ProductName, nameFont, nameBrush, 14, 32);
            }

            // Top-right Warehouse Pill
            using (var whPillBrush = new SolidBrush(palette.Hover))
            using (var whTextBrush = new SolidBrush(palette.TextPrimary))
            {
                var whFont = ZeroFontCache.Get("Segoe UI", 8f, FontStyle.Bold);
                string whLabel = $"WH: {_data.WarehouseCode}";
                var whSize = g.MeasureString(whLabel, whFont);
                int pillW = (int)whSize.Width + 12;
                Rectangle pillRect = new Rectangle(w - pillW - 12, 10, pillW, 20);
                using var pillPath = ZeroUIConfig.CreateRoundedRectangle(pillRect, 4);
                g.FillPath(whPillBrush, pillPath);
                g.DrawString(whLabel, whFont, whTextBrush, pillRect.X + 6, pillRect.Y + 3);
            }

            // Separator Line
            using (var sepPen = new Pen(palette.Border, 1f))
            {
                g.DrawLine(sepPen, 12, 54, w - 12, 54);
            }

            // 3. Three Golden Stock Metrics
            int metricStartY = 62;
            var labelFont = ZeroFontCache.Get("Segoe UI", 9f, FontStyle.Regular);
            var valFont = ZeroFontCache.Get("Segoe UI", 9.5f, FontStyle.Bold);
            using (var lblBrush = new SolidBrush(palette.TextSecondary))
            {
                // Metric 1: Available - Emerald Green
                using var greenBrush = new SolidBrush(Color.FromArgb(16, 185, 129));
                g.DrawString("Available :", labelFont, lblBrush, 14, metricStartY);
                g.DrawString($"{_data.AvailableQuantity:N0} {_data.UnitOfMeasure}", valFont, greenBrush, w - 110, metricStartY);

                // Metric 2: Waiting - Amber
                using var amberBrush = new SolidBrush(Color.FromArgb(217, 119, 6));
                g.DrawString("Waiting    :", labelFont, lblBrush, 14, metricStartY + 24);
                g.DrawString($"{_data.WaitingQuantity:N0} {_data.UnitOfMeasure}", valFont, amberBrush, w - 110, metricStartY + 24);

                // Metric 3: Reserved - Indigo
                using var indigoBrush = new SolidBrush(Color.FromArgb(99, 102, 241));
                g.DrawString("Reserved  :", labelFont, lblBrush, 14, metricStartY + 48);
                g.DrawString($"{_data.ReservedQuantity:N0} {_data.UnitOfMeasure}", valFont, indigoBrush, w - 110, metricStartY + 48);
            }

            // 4. Stock Allocation Segment Bar
            int barY = metricStartY + 76;
            int barW = w - 28;
            int barH = 8;
            decimal total = _data.TotalQuantity;

            using (var barBgBrush = new SolidBrush(palette.Border))
            {
                g.FillRectangle(barBgBrush, 14, barY, barW, barH);
            }

            if (total > 0)
            {
                float pctAvail = (float)(_data.AvailableQuantity / total);
                float pctWait = (float)(_data.WaitingQuantity / total);
                float pctRes = (float)(_data.ReservedQuantity / total);

                int wAvail = (int)(barW * pctAvail);
                int wWait = (int)(barW * pctWait);
                int wRes = barW - wAvail - wWait;

                int curX = 14;
                if (wAvail > 0)
                {
                    using var bAvail = new SolidBrush(Color.FromArgb(16, 185, 129));
                    g.FillRectangle(bAvail, curX, barY, wAvail, barH);
                    curX += wAvail;
                }
                if (wWait > 0)
                {
                    using var bWait = new SolidBrush(Color.FromArgb(245, 158, 11));
                    g.FillRectangle(bWait, curX, barY, wWait, barH);
                    curX += wWait;
                }
                if (wRes > 0)
                {
                    using var bRes = new SolidBrush(Color.FromArgb(99, 102, 241));
                    g.FillRectangle(bRes, curX, barY, wRes, barH);
                }
            }

            // 5. Footer: Location & Total Summary
            int footerY = barY + 14;
            var footFont = ZeroFontCache.Get("Segoe UI", 8f, FontStyle.Regular);
            var footBoldFont = ZeroFontCache.Get("Segoe UI", 8f, FontStyle.Bold);
            using (var footBrush = new SolidBrush(palette.TextSecondary))
            using (var totalBrush = new SolidBrush(palette.TextPrimary))
            {
                string totalStr = $"Total: {total:N0} {_data.UnitOfMeasure}";
                var totSize = g.MeasureString(totalStr, footBoldFont);
                g.DrawString(totalStr, footBoldFont, totalBrush, w - totSize.Width - 14, footerY);

                int maxLocW = (int)(w - totSize.Width - 32);
                string loc = string.IsNullOrEmpty(_data.LocationBin) ? _data.WarehouseName : $"{_data.WarehouseName} - {_data.LocationBin}";
                RectangleF locRect = new RectangleF(14, footerY, maxLocW, 16);
                g.DrawString(loc, footFont, footBrush, locRect, ZeroStringFormats.EllipsisNearCenter);
            }
        }

        #endregion
    }

    /// <summary>
    /// Legacy alias for <see cref="ZInventoryCard"/>.
    /// Preserved for backward compatibility.
    /// </summary>
    [Obsolete("ZeroInventoryCard is deprecated and will be removed in 5 release cycles. Please migrate to ZInventoryCard instead.")]
    [ToolboxItem(false)]
    public class ZeroInventoryCard : ZInventoryCard
    {
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZInventoryCard"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("InventoryCard is deprecated and will be removed in 5 release cycles. Please migrate to ZInventoryCard instead.")]
    [ToolboxItem(false)]
    public class InventoryCard : ZInventoryCard
    {
    }

    #endregion
}
