using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.Core.Network;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Network
{
    /// <summary>
    /// 19-Inch 42U/24U/12U Equipment Rack & DIN-Rail Cabinet Visualizer.
    /// Renders standard EIA-310 vertical mounting rails, blade servers, switches, PDUs,
    /// with real-time thermal gradient overlays and power/weight load rollups.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Network & Infrastructure")]
    [DefaultEvent("SelectedSlotChanged")]
    [Description("19-inch Equipment Rack & DIN-rail cabinet visualizer with thermal and power overlays")]
    public class ZDeviceRack : Control
    {
        private readonly RackLayoutEngine _engine = new RackLayoutEngine();
        private bool _showThermalOverlay;
        private RackSlotItem? _selectedItem;
        private int _hoveredUnit = -1;

        public event EventHandler? SelectedSlotChanged;

        [Category("Rack")]
        [Description("Access to the underlying pure rack layout computation engine")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public RackLayoutEngine Engine => _engine;

        [Category("Rack")]
        [DefaultValue(42)]
        public int TotalUnits
        {
            get => _engine.TotalUnits;
            set
            {
                if (_engine.TotalUnits != value && value > 0)
                {
                    _engine.TotalUnits = value;
                    Invalidate();
                }
            }
        }

        [Category("Rack")]
        [DefaultValue(false)]
        public bool ShowThermalOverlay
        {
            get => _showThermalOverlay;
            set
            {
                if (_showThermalOverlay != value)
                {
                    _showThermalOverlay = value;
                    Invalidate();
                }
            }
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public RackSlotItem? SelectedSlotItem
        {
            get => _selectedItem;
            set
            {
                if (_selectedItem != value)
                {
                    _selectedItem = value;
                    SelectedSlotChanged?.Invoke(this, EventArgs.Empty);
                    Invalidate();
                }
            }
        }

        public ZDeviceRack()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor, true);

            Size = new Size(320, 680);
            BackColor = Color.Transparent;
            Font = new Font("Segoe UI", 8.25f);

            InitializeDemoRack();
            ZeroTheme.ThemeChanged += OnThemeChanged;
        }

        private void InitializeDemoRack()
        {
            _engine.Clear();
            _engine.TotalUnits = 42;

            _engine.AddItem(new RackSlotItem
            {
                StartUnit = 1,
                UnitHeight = 3,
                Name = "APC Smart-UPS RT 3000",
                Model = "SURT3000XLI",
                Category = RackUnitCategory.Ups,
                PowerDrawWatts = 180,
                TemperatureCelsius = 25.0,
                WeightKg = 55.0,
                Status = RackSlotStatus.Normal
            });

            _engine.AddItem(new RackSlotItem
            {
                StartUnit = 5,
                UnitHeight = 4,
                Name = "Dell EMC PowerVault ME5",
                Model = "ME5024 Storage",
                Category = RackUnitCategory.StorageArray,
                PowerDrawWatts = 420,
                TemperatureCelsius = 32.5,
                WeightKg = 32.0,
                Status = RackSlotStatus.Normal
            });

            _engine.AddItem(new RackSlotItem
            {
                StartUnit = 12,
                UnitHeight = 2,
                Name = "HPE ProLiant DL380 Gen10",
                Model = "DL380-G10-01",
                Category = RackUnitCategory.Server,
                PowerDrawWatts = 350,
                TemperatureCelsius = 36.0,
                WeightKg = 19.5,
                Status = RackSlotStatus.Normal
            });

            _engine.AddItem(new RackSlotItem
            {
                StartUnit = 15,
                UnitHeight = 2,
                Name = "HPE ProLiant DL380 Gen10",
                Model = "DL380-G10-02",
                Category = RackUnitCategory.Server,
                PowerDrawWatts = 370,
                TemperatureCelsius = 38.5,
                WeightKg = 19.5,
                Status = RackSlotStatus.Warning
            });

            _engine.AddItem(new RackSlotItem
            {
                StartUnit = 22,
                UnitHeight = 1,
                Name = "ZeroSwitch 48G Enterprise",
                Model = "ZS-4804X",
                Category = RackUnitCategory.Switch,
                PowerDrawWatts = 120,
                TemperatureCelsius = 31.0,
                WeightKg = 5.2,
                Status = RackSlotStatus.Normal
            });

            _engine.AddItem(new RackSlotItem
            {
                StartUnit = 24,
                UnitHeight = 1,
                Name = "Cat6A 24-Port Patch Panel",
                Model = "PP-24-CAT6A",
                Category = RackUnitCategory.PatchPanel,
                PowerDrawWatts = 0,
                TemperatureCelsius = 24.0,
                WeightKg = 1.8,
                Status = RackSlotStatus.Normal
            });
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var palette = ZeroTheme.Colors;
            var bounds = ClientRectangle;
            if (bounds.Width < 80 || bounds.Height < 120) return;

            // 1. Frame Background & Outer Cabinet
            using (var bgBrush = new SolidBrush(Color.FromArgb(20, 24, 33)))
            using (var borderPen = new Pen(Color.FromArgb(45, 55, 72), 1.5f))
            {
                using var path = CreateRoundedRect(new Rectangle(0, 0, bounds.Width - 1, bounds.Height - 1), 6);
                g.FillPath(bgBrush, path);
                g.DrawPath(borderPen, path);
            }

            // 2. Header HUD (Power, Weight, Thermal summary)
            int headerHeight = 44;
            DrawHeaderHud(g, new Rectangle(6, 6, bounds.Width - 12, headerHeight));

            // 3. Rack Rail Area (Mounting frame)
            int railTop = headerHeight + 10;
            int railBottom = bounds.Height - 10;
            int railHeight = railBottom - railTop;
            int railLeft = 8;
            int thermalWidth = _showThermalOverlay ? 36 : 0;
            int railRight = bounds.Width - 8 - thermalWidth;
            int railWidth = railRight - railLeft;

            if (railHeight <= 0 || railWidth <= 0) return;

            DrawRackFrameAndSlots(g, new Rectangle(railLeft, railTop, railWidth, railHeight));

            // 4. Optional Thermal Elevation Strip
            if (_showThermalOverlay)
            {
                DrawThermalElevationStrip(g, new Rectangle(railRight + 4, railTop, thermalWidth - 6, railHeight));
            }
        }

        private void DrawHeaderHud(Graphics g, Rectangle r)
        {
            using (var headerBrush = new SolidBrush(Color.FromArgb(28, 33, 46)))
            using (var headerPen = new Pen(Color.FromArgb(45, 55, 72), 1f))
            {
                using var path = CreateRoundedRect(r, 4);
                g.FillPath(headerBrush, path);
                g.DrawPath(headerPen, path);
            }

            using var titleFont = new Font(Font.FontFamily, 8f, FontStyle.Bold);
            using var subFont = new Font(Font.FontFamily, 7.5f, FontStyle.Regular);
            using var textBrush = new SolidBrush(Color.FromArgb(226, 232, 240));
            using var subBrush = new SolidBrush(Color.FromArgb(148, 163, 184));

            double powerW = _engine.CalculateTotalPowerDrawWatts();
            double powerPct = _engine.CalculatePowerUtilizationPercent();
            double weightKg = _engine.CalculateTotalWeightKg();

            g.DrawString($"19\" Cabinet ({TotalUnits}U)", titleFont, textBrush, r.Left + 8, r.Top + 5);
            g.DrawString($"Power: {powerW:F0} W ({powerPct:F0}%) | Weight: {weightKg:F1} kg", subFont, subBrush, r.Left + 8, r.Top + 22);

            // Quick status pill
            string statusText = _selectedItem != null ? $"{_selectedItem.Name} ({_selectedItem.UnitHeight}U)" : "All Systems Normal";
            using var pillBrush = new SolidBrush(Color.FromArgb(30, 41, 59));
            using var pillPen = new Pen(Color.FromArgb(56, 189, 248), 1f);
            var pillRect = new Rectangle(r.Right - 120, r.Top + 8, 112, 24);
            using var pillPath = CreateRoundedRect(pillRect, 4);
            g.FillPath(pillBrush, pillPath);
            g.DrawPath(pillPen, pillPath);

            using var pillFont = new Font(Font.FontFamily, 7f, FontStyle.Bold);
            using var pillTextBrush = new SolidBrush(Color.FromArgb(56, 189, 248));
            var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter };
            g.DrawString(statusText, pillFont, pillTextBrush, pillRect, sf);
        }

        private void DrawRackFrameAndSlots(Graphics g, Rectangle r)
        {
            // Vertical mounting rails (left & right)
            int postWidth = 26;
            using (var postBrush = new SolidBrush(Color.FromArgb(15, 18, 26)))
            using (var postPen = new Pen(Color.FromArgb(38, 46, 62), 1f))
            {
                g.FillRectangle(postBrush, r.Left, r.Top, postWidth, r.Height);
                g.DrawRectangle(postPen, r.Left, r.Top, postWidth, r.Height);

                g.FillRectangle(postBrush, r.Right - postWidth, r.Top, postWidth, r.Height);
                g.DrawRectangle(postPen, r.Right - postWidth, r.Top, postWidth, r.Height);
            }

            int bayLeft = r.Left + postWidth;
            int bayWidth = r.Width - (postWidth * 2);
            float unitHeightPx = (float)r.Height / _engine.TotalUnits;

            using var unitFont = new Font(Font.FontFamily, 6.75f, FontStyle.Regular);
            using var unitBrush = new SolidBrush(Color.FromArgb(100, 116, 139));
            using var slotPen = new Pen(Color.FromArgb(30, 36, 49), 1f);
            var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

            // Draw unit slots from bottom (1U) to top (TotalUnits)
            for (int u = 1; u <= _engine.TotalUnits; u++)
            {
                float y = r.Bottom - (u * unitHeightPx);

                // Tick line and U number
                g.DrawLine(slotPen, r.Left + 2, y, r.Right - 2, y);
                if (u % 2 == 1 || unitHeightPx > 18)
                {
                    var numRectL = new RectangleF(r.Left, y, postWidth, unitHeightPx);
                    var numRectR = new RectangleF(r.Right - postWidth, y, postWidth, unitHeightPx);
                    g.DrawString(u.ToString(), unitFont, unitBrush, numRectL, sf);
                    g.DrawString(u.ToString(), unitFont, unitBrush, numRectR, sf);
                }
            }

            // Render installed equipment items
            for (int i = 0; i < _engine.Items.Count; i++)
            {
                var item = _engine.Items[i];
                float topY = r.Bottom - (item.EndUnit * unitHeightPx);
                float itemH = item.UnitHeight * unitHeightPx;
                var itemRect = new Rectangle((int)bayLeft + 2, (int)topY + 1, (int)bayWidth - 4, (int)itemH - 2);

                DrawEquipmentItem(g, item, itemRect);
            }
        }

        private void DrawEquipmentItem(Graphics g, RackSlotItem item, Rectangle r)
        {
            bool isSelected = item == _selectedItem;

            Color baseColor = item.Category switch
            {
                RackUnitCategory.Server => Color.FromArgb(30, 41, 59),
                RackUnitCategory.Switch => Color.FromArgb(22, 47, 58),
                RackUnitCategory.StorageArray => Color.FromArgb(40, 36, 61),
                RackUnitCategory.Ups => Color.FromArgb(43, 37, 24),
                RackUnitCategory.PatchPanel => Color.FromArgb(28, 35, 45),
                _ => Color.FromArgb(33, 38, 48)
            };

            using (var fillBrush = new SolidBrush(baseColor))
            using (var borderPen = new Pen(isSelected ? Color.FromArgb(56, 189, 248) : Color.FromArgb(60, 70, 88), isSelected ? 2f : 1f))
            {
                using var path = CreateRoundedRect(r, 3);
                g.FillPath(fillBrush, path);
                g.DrawPath(borderPen, path);
            }

            // Latch handles on left and right
            using (var handleBrush = new SolidBrush(Color.FromArgb(71, 85, 105)))
            {
                g.FillRectangle(handleBrush, r.Left + 2, r.Top + 4, 3, Math.Max(4, r.Height - 8));
                g.FillRectangle(handleBrush, r.Right - 5, r.Top + 4, 3, Math.Max(4, r.Height - 8));
            }

            // Status LED
            Color ledColor = item.Status switch
            {
                RackSlotStatus.Normal => Color.FromArgb(34, 197, 94),
                RackSlotStatus.Warning => Color.FromArgb(245, 158, 11),
                RackSlotStatus.Critical => Color.FromArgb(239, 68, 68),
                _ => Color.FromArgb(100, 116, 139)
            };

            using (var ledBrush = new SolidBrush(ledColor))
            {
                g.FillEllipse(ledBrush, r.Left + 10, r.Top + (r.Height / 2) - 3, 6, 6);
            }

            // Labels
            if (r.Height >= 14)
            {
                using var nameFont = new Font(Font.FontFamily, r.Height >= 26 ? 8f : 7f, FontStyle.Bold);
                using var infoFont = new Font(Font.FontFamily, 6.75f, FontStyle.Regular);
                using var textBrush = new SolidBrush(Color.FromArgb(241, 245, 249));
                using var subBrush = new SolidBrush(Color.FromArgb(148, 163, 184));

                float textX = r.Left + 22;
                if (r.Height >= 28)
                {
                    g.DrawString(item.Name, nameFont, textBrush, textX, r.Top + 3);
                    g.DrawString($"{item.Model} | {item.PowerDrawWatts:F0}W | {item.TemperatureCelsius:F1}°C", infoFont, subBrush, textX, r.Top + 16);
                }
                else
                {
                    g.DrawString($"{item.Name} ({item.UnitHeight}U)", nameFont, textBrush, textX, r.Top + 2);
                }
            }
        }

        private void DrawThermalElevationStrip(Graphics g, Rectangle r)
        {
            using (var stripBrush = new SolidBrush(Color.FromArgb(18, 22, 30)))
            using (var stripPen = new Pen(Color.FromArgb(38, 46, 62), 1f))
            {
                using var path = CreateRoundedRect(r, 4);
                g.FillPath(stripBrush, path);
                g.DrawPath(stripPen, path);
            }

            // Thermal gradient bar
            var barRect = new Rectangle(r.Left + 4, r.Top + 6, 10, r.Height - 12);
            using (var lgb = new LinearGradientBrush(barRect, Color.FromArgb(239, 68, 68), Color.FromArgb(56, 189, 248), 90f))
            {
                g.FillRectangle(lgb, barRect);
            }

            // Labels: top heat (~42°C), mid (~30°C), bottom inlet (~20°C)
            using var font = new Font(Font.FontFamily, 6.5f, FontStyle.Regular);
            using var brush = new SolidBrush(Color.FromArgb(203, 213, 225));
            g.DrawString("45°C", font, brush, r.Left + 16, r.Top + 4);
            g.DrawString("30°C", font, brush, r.Left + 16, r.Top + (r.Height / 2) - 4);
            g.DrawString("18°C", font, brush, r.Left + 16, r.Bottom - 16);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            int unit = HitTestUnit(e.Location);
            if (unit > 0)
            {
                SelectedSlotItem = _engine.FindItemAtUnit(unit);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int unit = HitTestUnit(e.Location);
            if (unit != _hoveredUnit)
            {
                _hoveredUnit = unit;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hoveredUnit != -1)
            {
                _hoveredUnit = -1;
                Invalidate();
            }
        }

        private int HitTestUnit(Point pt)
        {
            int headerHeight = 44;
            int railTop = headerHeight + 10;
            int railBottom = Height - 10;
            if (pt.Y < railTop || pt.Y > railBottom) return -1;

            float unitHeightPx = (float)(railBottom - railTop) / _engine.TotalUnits;
            int unitFromBottom = (int)((railBottom - pt.Y) / unitHeightPx) + 1;
            if (unitFromBottom >= 1 && unitFromBottom <= _engine.TotalUnits)
                return unitFromBottom;

            return -1;
        }

        private static GraphicsPath CreateRoundedRect(Rectangle r, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
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

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZDeviceRack"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("DeviceRack is deprecated and will be removed in 5 release cycles. Please migrate to ZDeviceRack instead.")]
    [ToolboxItem(false)]
    public class DeviceRack : ZDeviceRack
    {
    }

    #endregion
}
