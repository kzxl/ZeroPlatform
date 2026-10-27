using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.WinForms.Native;
using ZeroUI.WinForms.Rendering;
using ZeroUI.WinForms.Icons;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Industrial
{
    public enum BinOccupancyStatus
    {
        Empty,
        Available,
        Full,
        Quarantine
    }

    public class WarehouseBin
    {
        public int Bay { get; set; }
        public int Level { get; set; }
        public string BinCode { get; set; } = "";
        public string Sku { get; set; } = "";
        public string ItemName { get; set; } = "";
        public string LotNumber { get; set; } = "";
        public int CurrentQty { get; set; }
        public int MaxCapacity { get; set; } = 1000;
        public BinOccupancyStatus Status { get; set; } = BinOccupancyStatus.Available;

        internal Rectangle Bounds;
    }

    public class WarehouseBinClickedEventArgs : EventArgs
    {
        public WarehouseBin Bin { get; }

        public WarehouseBinClickedEventArgs(WarehouseBin bin)
        {
            Bin = bin;
        }
    }

    /// <summary>
    /// 2D Smart Warehouse Storage Rack visualization control for WMS and inventory management.
    /// Renders multi-tier rack shelves (Bay x Level x Bin) with occupancy indicators,
    /// quarantine locks, hover inspection, and click events.
    /// </summary>
    [ToolboxItem(true)]
    [ToolboxBitmap(typeof(ZeroIcons), "ZeroWarehouseRack.bmp")]
    [Category("ZeroUI - Industrial & SCADA")]
    [DefaultProperty("RackTitle")]
    [DefaultEvent("BinClicked")]
    [Description("2D Smart Warehouse Storage Rack visualizer for WMS inventory management")]
    public class ZWarehouseRack : Control
    {
        private int _bays = 5;
        private int _levels = 4;
        private WarehouseBin[,] _bins = new WarehouseBin[0, 0];
        private WarehouseBin? _hoveredBin;
        private string _rackTitle = "SMT Reel Storage Rack — Row A (Rack A-01..05)";

        public event EventHandler<WarehouseBinClickedEventArgs>? BinClicked;

        public ZWarehouseRack()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor, true);

            Size = new Size(460, 240);
            BackColor = Color.Transparent;
            Font = new Font("Segoe UI", 8.5f);

            ZeroTheme.ThemeChanged += OnThemeChanged;
            InitializeRack();
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

        [Category("Rack Configuration")]
        [DefaultValue(5)]
        public int Bays
        {
            get => _bays;
            set
            {
                _bays = Math.Max(2, Math.Min(12, value));
                InitializeRack();
                Invalidate();
            }
        }

        [Category("Rack Configuration")]
        [DefaultValue(4)]
        public int Levels
        {
            get => _levels;
            set
            {
                _levels = Math.Max(2, Math.Min(8, value));
                InitializeRack();
                Invalidate();
            }
        }

        [Category("Appearance")]
        [DefaultValue("SMT Reel Storage Rack — Row A (Rack A-01..05)")]
        public string RackTitle
        {
            get => _rackTitle;
            set { _rackTitle = value ?? ""; Invalidate(); }
        }

        private void InitializeRack()
        {
            _bins = new WarehouseBin[_levels, _bays];
            var rand = new Random(101);

            for (int lvl = 0; lvl < _levels; lvl++)
            {
                int levelNum = _levels - lvl; // Top level is highest number
                for (int bay = 0; bay < _bays; bay++)
                {
                    int bayNum = bay + 1;
                    string code = $"A-{bayNum:D2}-{levelNum:D2}";

                    int roll = rand.Next(100);
                    BinOccupancyStatus status;
                    int max = 2000;
                    int qty = 0;
                    string sku = "";
                    string name = "";
                    string lot = "";

                    if (roll < 20)
                    {
                        status = BinOccupancyStatus.Empty;
                    }
                    else if (roll < 35)
                    {
                        status = BinOccupancyStatus.Quarantine;
                        qty = 500;
                        sku = "IC-MCU-000001";
                        name = "STM32F407VGT6";
                        lot = "LOT-20260815-HOLD";
                    }
                    else if (roll < 75)
                    {
                        status = BinOccupancyStatus.Available;
                        qty = rand.Next(300, 1500);
                        sku = "SMD-CAP-0805";
                        name = "Ceramic Capacitor SMD 10uF 25V";
                        lot = $"LOT-2026090{bayNum}-SMT1";
                    }
                    else
                    {
                        status = BinOccupancyStatus.Full;
                        qty = max;
                        sku = "MEC-STEP-N23";
                        name = "Nema 23 Stepper Motor";
                        lot = $"LOT-2026082{bayNum}-IMP";
                    }

                    _bins[lvl, bay] = new WarehouseBin
                    {
                        Bay = bayNum,
                        Level = levelNum,
                        BinCode = code,
                        Sku = sku,
                        ItemName = name,
                        LotNumber = lot,
                        CurrentQty = qty,
                        MaxCapacity = max,
                        Status = status
                    };
                }
            }
        }

        public void SetBin(int level, int bay, BinOccupancyStatus status, string sku, string name, string lot, int qty)
        {
            int r = _levels - level;
            int c = bay - 1;
            if (r >= 0 && r < _levels && c >= 0 && c < _bays)
            {
                var b = _bins[r, c];
                b.Status = status;
                b.Sku = sku;
                b.ItemName = name;
                b.LotNumber = lot;
                b.CurrentQty = qty;
                Invalidate();
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            WarehouseBin? hit = null;

            for (int lvl = 0; lvl < _levels; lvl++)
            {
                for (int bay = 0; bay < _bays; bay++)
                {
                    if (_bins[lvl, bay].Bounds.Contains(e.Location))
                    {
                        hit = _bins[lvl, bay];
                        break;
                    }
                }
                if (hit != null) break;
            }

            if (_hoveredBin != hit)
            {
                _hoveredBin = hit;
                Cursor = hit != null ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hoveredBin != null)
            {
                _hoveredBin = null;
                Invalidate();
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left && _hoveredBin != null)
            {
                BinClicked?.Invoke(this, new WarehouseBinClickedEventArgs(_hoveredBin));
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

            // 1. Rack Enclosure
            using (var brush = new SolidBrush(palette.CardBackground))
            {
                g.FillRectangle(brush, 0, 0, w, h);
            }
            using (var enclosurePen = new Pen(palette.Border, 1f))
            {
                g.DrawRectangle(enclosurePen, 0, 0, w - 1, h - 1);
            }

            // 2. Title & Occupancy Summary
            var titleFont = ZeroFontCache.Get("Segoe UI", 8.5f, FontStyle.Bold);
            using (var titleBrush = new SolidBrush(palette.TextPrimary))
            {
                g.DrawString(_rackTitle, titleFont, titleBrush, 8, 6);
            }

            int emptyCount = 0, fullCount = 0, availCount = 0, quarCount = 0;
            for (int lvl = 0; lvl < _levels; lvl++)
            {
                for (int bay = 0; bay < _bays; bay++)
                {
                    switch (_bins[lvl, bay].Status)
                    {
                        case BinOccupancyStatus.Empty: emptyCount++; break;
                        case BinOccupancyStatus.Full: fullCount++; break;
                        case BinOccupancyStatus.Quarantine: quarCount++; break;
                        default: availCount++; break;
                    }
                }
            }

            string summary = $"Occupancy: {fullCount + availCount}/{_levels * _bays} | QC Hold: {quarCount}";
            var statFont = ZeroFontCache.Get("Segoe UI", 7.5f, FontStyle.Regular);
            using (var statBrush = new SolidBrush(palette.TextSecondary))
            {
                var sz = g.MeasureString(summary, statFont);
                g.DrawString(summary, statFont, statBrush, w - sz.Width - 10, 8);
            }

            // 3. Rack Geometry Layout
            int startY = 24;
            int legendH = 20;
            int marginX = 12;
            int availableW = w - (marginX * 2);
            int availableH = h - startY - legendH - 6;

            int gapX = 6;
            int gapY = 5;
            int binW = Math.Max(24, (availableW - ((_bays - 1) * gapX)) / _bays);
            int binH = Math.Max(20, (availableH - ((_levels - 1) * gapY)) / _levels);

            int startX = marginX + (availableW - ((binW * _bays) + ((_bays - 1) * gapX))) / 2;

            // Draw Upright Columns (Metallic beams)
            using (var beamPen = new Pen(palette.Border, 3f))
            {
                for (int b = 0; b <= _bays; b++)
                {
                    int bx = startX + (b * (binW + gapX)) - (gapX / 2);
                    g.DrawLine(beamPen, bx, startY - 2, bx, startY + (binH * _levels) + ((_levels - 1) * gapY) + 2);
                }
            }

            // Draw Shelf Beams (Horizontal)
            using (var shelfPen = new Pen(palette.Border, 2f))
            {
                for (int l = 0; l <= _levels; l++)
                {
                    int ly = startY + (l * (binH + gapY)) - (gapY / 2);
                    g.DrawLine(shelfPen, startX - 3, ly, startX + (_bays * (binW + gapX)), ly);
                }
            }

            // 4. Draw Individual Bins
            var binFont = ZeroFontCache.Get("Segoe UI", 7.5f, FontStyle.Bold);
            var subFont = ZeroFontCache.Get("Segoe UI", 6.5f, FontStyle.Regular);
            using var textBrush = new SolidBrush(Color.White);
            using var subBrush = new SolidBrush(Color.FromArgb(226, 232, 240));
            using var pillBrush = new SolidBrush(Color.FromArgb(70, 0, 0, 0));
            using var borderPen = new Pen(Color.Empty, 1f);
            using var hoverPen = new Pen(Color.White, 2f);

            for (int lvl = 0; lvl < _levels; lvl++)
            {
                for (int bay = 0; bay < _bays; bay++)
                {
                    var bin = _bins[lvl, bay];
                    int x = startX + (bay * (binW + gapX));
                    int y = startY + (lvl * (binH + gapY));
                    bin.Bounds = new Rectangle(x, y, binW, binH);

                    DrawBin(g, bin, _hoveredBin == bin, binFont, subFont, textBrush, subBrush, pillBrush, borderPen, hoverPen);
                }
            }

            // 5. Bottom Legend
            int legY = h - 18;
            using var legTxtBrush = new SolidBrush(palette.TextSecondary);
            DrawLegend(g, 10, legY, Color.FromArgb(51, 65, 85), "Empty", statFont, legTxtBrush);
            DrawLegend(g, 85, legY, Color.FromArgb(59, 130, 246), "Available", statFont, legTxtBrush);
            DrawLegend(g, 175, legY, Color.FromArgb(16, 185, 129), "Full", statFont, legTxtBrush);
            DrawLegend(g, 240, legY, Color.FromArgb(239, 68, 68), "QC Hold", statFont, legTxtBrush);
        }

        private void DrawBin(
            Graphics g, WarehouseBin bin, bool isHovered,
            Font font, Font subFont, SolidBrush textBrush, SolidBrush subBrush,
            SolidBrush pillBrush, Pen borderPen, Pen hoverPen)
        {
            Rectangle r = bin.Bounds;
            Color fillC;
            Color borderC;

            switch (bin.Status)
            {
                case BinOccupancyStatus.Empty:
                    fillC = Color.FromArgb(24, 32, 48);
                    borderC = Color.FromArgb(51, 65, 85);
                    break;
                case BinOccupancyStatus.Full:
                    fillC = Color.FromArgb(16, 185, 129);
                    borderC = Color.FromArgb(52, 211, 153);
                    break;
                case BinOccupancyStatus.Quarantine:
                    fillC = Color.FromArgb(239, 68, 68);
                    borderC = Color.FromArgb(248, 113, 113);
                    break;
                default:
                    fillC = Color.FromArgb(59, 130, 246);
                    borderC = Color.FromArgb(96, 165, 250);
                    break;
            }

            // Fill with subtle gradient
            using (var brush = new LinearGradientBrush(new Point(r.X, r.Y), new Point(r.X, r.Bottom), fillC, Color.FromArgb(180, fillC.R / 2, fillC.G / 2, fillC.B / 2)))
            {
                g.FillRectangle(brush, r);
            }

            // Border
            if (isHovered)
            {
                g.DrawRectangle(hoverPen, r);
            }
            else
            {
                borderPen.Color = borderC;
                g.DrawRectangle(borderPen, r);
            }

            // Measure Texts
            var sz = g.MeasureString(bin.BinCode, font);

            string sub = bin.Status == BinOccupancyStatus.Empty ? "—" : $"{bin.CurrentQty:N0}";
            var subSz = g.MeasureString(sub, subFont);

            // Adaptive layout: if height allows 2 stacked lines without overlapping, stack vertically.
            // Otherwise, place side-by-side (Bin Code on left, Qty on right) to guarantee zero overlap.
            float minStackHeight = sz.Height + subSz.Height + 2;
            if (r.Height >= minStackHeight)
            {
                float codeY = r.Y + 2;
                float subY = r.Bottom - subSz.Height - 2;

                g.DrawString(bin.BinCode, font, textBrush, r.X + (r.Width - sz.Width) / 2, codeY);

                if (bin.Status != BinOccupancyStatus.Empty)
                {
                    int pillW = (int)subSz.Width + 8;
                    int pillH = (int)subSz.Height + 1;
                    int pillX = r.X + (r.Width - pillW) / 2;
                    int pillY = (int)subY;
                    var pillRect = new Rectangle(pillX, pillY, pillW, pillH);
                    using var pillPath = ZeroUIConfig.CreateRoundedRectangle(pillRect, 3);
                    g.FillPath(pillBrush, pillPath);
                }

                g.DrawString(sub, subFont, subBrush, r.X + (r.Width - subSz.Width) / 2, subY);
            }
            else
            {
                // Side-by-side layout: Bin Code on left, Quantity on right
                float centerY = r.Y + (r.Height - sz.Height) / 2;
                float codeX = r.X + 6;
                g.DrawString(bin.BinCode, font, textBrush, codeX, centerY);

                float subY = r.Y + (r.Height - subSz.Height) / 2;
                float subX = r.Right - subSz.Width - 8;

                if (bin.Status != BinOccupancyStatus.Empty)
                {
                    int pillW = (int)subSz.Width + 8;
                    int pillH = (int)subSz.Height + 2;
                    int pillX = (int)subX - 4;
                    int pillY = (int)subY - 1;
                    var pillRect = new Rectangle(pillX, pillY, pillW, pillH);
                    using var pillPath = ZeroUIConfig.CreateRoundedRectangle(pillRect, 3);
                    g.FillPath(pillBrush, pillPath);
                }

                g.DrawString(sub, subFont, subBrush, subX, subY);
            }
        }

        private void DrawLegend(Graphics g, int x, int y, Color color, string label, Font font, SolidBrush txtBrush)
        {
            using var brush = new SolidBrush(color);
            g.FillRectangle(brush, x, y + 2, 8, 8);
            g.DrawString(label, font, txtBrush, x + 12, y);
        }
    }

    /// <summary>
    /// Legacy alias for <see cref="ZWarehouseRack"/>.
    /// Preserved for backward compatibility.
    /// </summary>
    [Obsolete("ZeroWarehouseRack is deprecated and will be removed in 5 release cycles. Please migrate to ZWarehouseRack instead.")]
    [ToolboxItem(false)]
    public class ZeroWarehouseRack : ZWarehouseRack
    {
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZWarehouseRack"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("WarehouseRack is deprecated and will be removed in 5 release cycles. Please migrate to ZWarehouseRack instead.")]
    [ToolboxItem(false)]
    public class WarehouseRack : ZWarehouseRack
    {
    }

    #endregion
}
