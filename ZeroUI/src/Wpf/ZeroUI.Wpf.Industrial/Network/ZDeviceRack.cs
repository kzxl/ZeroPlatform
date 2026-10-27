using System;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ZeroUI.Core.Network;
using ZeroUI.Wpf.Base;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Network
{
    /// <summary>
    /// 19-Inch 42U/24U/12U Equipment Rack & DIN-Rail Cabinet Visualizer for WPF.
    /// Renders standard EIA-310 vertical mounting rails, blade servers, switches, PDUs,
    /// with real-time thermal gradient overlays and power/weight load rollups.
    /// </summary>
    public class ZDeviceRack : ZeroWpfVisualBase
    {
        private readonly RackLayoutEngine _engine = new RackLayoutEngine();
        private bool _showThermalOverlay = false;
        private RackSlotItem? _selectedItem;

        public event EventHandler? SelectedSlotChanged;

        #region Properties

        public RackLayoutEngine Engine => _engine;

        public int TotalUnits
        {
            get => _engine.TotalUnits;
            set
            {
                if (_engine.TotalUnits != value && value > 0)
                {
                    _engine.TotalUnits = value;
                    InvalidateVisual();
                }
            }
        }

        public bool ShowThermalOverlay
        {
            get => _showThermalOverlay;
            set
            {
                if (_showThermalOverlay != value)
                {
                    _showThermalOverlay = value;
                    InvalidateVisual();
                }
            }
        }

        public RackSlotItem? SelectedSlotItem
        {
            get => _selectedItem;
            set
            {
                if (_selectedItem != value)
                {
                    _selectedItem = value;
                    SelectedSlotChanged?.Invoke(this, EventArgs.Empty);
                    InvalidateVisual();
                }
            }
        }

        #endregion

        public ZDeviceRack()
        {
            InitializeDemoRack();
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
                PowerDrawWatts = 340,
                TemperatureCelsius = 38.0,
                WeightKg = 19.5,
                Status = RackSlotStatus.Normal
            });

            _engine.AddItem(new RackSlotItem
            {
                StartUnit = 22,
                UnitHeight = 1,
                Name = "Cisco Catalyst 9300 48P",
                Model = "C9300-48P",
                Category = RackUnitCategory.Switch,
                PowerDrawWatts = 95,
                TemperatureCelsius = 41.5,
                WeightKg = 7.5,
                Status = RackSlotStatus.Normal
            });

            _engine.AddItem(new RackSlotItem
            {
                StartUnit = 24,
                UnitHeight = 1,
                Name = "Fortinet FortiGate 200F",
                Model = "FG-200F-HA",
                Category = RackUnitCategory.Router,
                PowerDrawWatts = 110,
                TemperatureCelsius = 39.0,
                WeightKg = 6.8,
                Status = RackSlotStatus.Normal
            });

            _engine.AddItem(new RackSlotItem
            {
                StartUnit = 30,
                UnitHeight = 1,
                Name = "Cat6A 24-Port Patch Panel",
                Model = "CP-24-CAT6A",
                Category = RackUnitCategory.PatchPanel,
                PowerDrawWatts = 0,
                TemperatureCelsius = 22.0,
                WeightKg = 1.8,
                Status = RackSlotStatus.Normal
            });
        }

        #if NETFRAMEWORK
        private static FormattedText CreateFormattedText(string text, Typeface typeface, double fontSize, Brush brush, double pixelsPerDip = 1.0)
        {
            return new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, fontSize, brush);
        }
        #else
        private static FormattedText CreateFormattedText(string text, Typeface typeface, double fontSize, Brush brush, double pixelsPerDip = 1.0)
        {
            return new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, fontSize, brush, pixelsPerDip);
        }
        #endif

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            double w = ActualWidth;
            double h = ActualHeight;
            if (w < 80 || h < 120) return;

            #if NETFRAMEWORK
            double dpi = 1.0;
            #else
            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            #endif

            // Outer Cabinet Frame
            var cabinetBg = new SolidColorBrush(Color.FromRgb(20, 24, 33));
            cabinetBg.Freeze();
            var cabinetBorder = new Pen(new SolidColorBrush(Color.FromRgb(45, 55, 72)), 1.5);
            cabinetBorder.Freeze();
            dc.DrawRoundedRectangle(cabinetBg, cabinetBorder, new Rect(0.5, 0.5, w - 1, h - 1), 6, 6);

            // Header HUD
            double headerH = 44;
            DrawHeaderHud(dc, new Rect(6, 6, w - 12, headerH), dpi);

            // Rack Rail Area
            double railTop = headerH + 10;
            double railBottom = h - 10;
            double railHeight = railBottom - railTop;
            double railLeft = 8;
            double thermalWidth = _showThermalOverlay ? 36 : 0;
            double railRight = w - 8 - thermalWidth;
            double railWidth = railRight - railLeft;

            if (railHeight <= 0 || railWidth <= 0) return;

            DrawRackFrameAndSlots(dc, new Rect(railLeft, railTop, railWidth, railHeight), dpi);

            // Thermal Elevation Strip
            if (_showThermalOverlay)
            {
                DrawThermalElevationStrip(dc, new Rect(railRight + 4, railTop, thermalWidth - 6, railHeight), dpi);
            }
        }

        private void DrawHeaderHud(DrawingContext dc, Rect r, double dpi)
        {
            var headerBg = ZeroWpfTheme.BgCard;
            var headerPen = ZeroWpfTheme.BorderPen;
            dc.DrawRoundedRectangle(headerBg, headerPen, r, 4, 4);

            double powerW = _engine.CalculateTotalPowerDrawWatts();
            double powerPct = _engine.CalculatePowerUtilizationPercent();
            double weightKg = _engine.CalculateTotalWeightKg();

            var titleFt = CreateFormattedText($"19\" Cabinet ({TotalUnits}U)", ZeroWpfTheme.BoldTypeface, 11.0, ZeroWpfTheme.TextPrimary, dpi);
            var subFt = CreateFormattedText($"Power: {powerW:F0} W ({powerPct:F0}%) | Weight: {weightKg:F1} kg", ZeroWpfTheme.RegularTypeface, 9.5, ZeroWpfTheme.TextSecondary, dpi);

            dc.DrawText(titleFt, new Point(r.Left + 8, r.Top + 6));
            dc.DrawText(subFt, new Point(r.Left + 8, r.Top + 24));

            // Status Pill
            string statusText = _selectedItem != null ? $"{_selectedItem.Name} ({_selectedItem.UnitHeight}U)" : "Normal";
            var pillBg = ZeroWpfTheme.BgInput;
            var pillPen = new Pen(ZeroWpfTheme.PrimaryAccent, 1.0);
            pillPen.Freeze();

            double pillW = 120;
            double pillH = 24;
            Rect pillRect = new Rect(r.Right - pillW - 8, r.Top + (r.Height - pillH) / 2.0, pillW, pillH);
            dc.DrawRoundedRectangle(pillBg, pillPen, pillRect, 4, 4);

            var pillFt = CreateFormattedText(statusText, ZeroWpfTheme.BoldTypeface, 9.0, ZeroWpfTheme.PrimaryAccent, dpi);
            dc.DrawText(pillFt, new Point(pillRect.Left + (pillRect.Width - pillFt.Width) / 2.0, pillRect.Top + (pillRect.Height - pillFt.Height) / 2.0));
        }

        private void DrawRackFrameAndSlots(DrawingContext dc, Rect r, double dpi)
        {
            double postWidth = 24;
            var postBg = ZeroWpfTheme.BgInput;
            var postPen = ZeroWpfTheme.BorderPen;

            // Left and Right EIA-310 rails
            dc.DrawRectangle(postBg, postPen, new Rect(r.Left, r.Top, postWidth, r.Height));
            dc.DrawRectangle(postBg, postPen, new Rect(r.Right - postWidth, r.Top, postWidth, r.Height));

            double bayLeft = r.Left + postWidth;
            double bayWidth = r.Width - (postWidth * 2);
            double unitHeightPx = r.Height / _engine.TotalUnits;

            var slotPen = ZeroWpfTheme.GridLinePen;

            // U Tick marks
            for (int u = 1; u <= _engine.TotalUnits; u++)
            {
                double y = r.Bottom - (u * unitHeightPx);
                dc.DrawLine(slotPen, new Point(r.Left + 2, y), new Point(r.Right - 2, y));

                if (u % 2 == 1 || unitHeightPx > 18)
                {
                    var uFt = CreateFormattedText(u.ToString(), ZeroWpfTheme.RegularTypeface, 8.0, ZeroWpfTheme.TextMuted, dpi);
                    dc.DrawText(uFt, new Point(r.Left + (postWidth - uFt.Width) / 2.0, y + (unitHeightPx - uFt.Height) / 2.0));
                    dc.DrawText(uFt, new Point(r.Right - postWidth + (postWidth - uFt.Width) / 2.0, y + (unitHeightPx - uFt.Height) / 2.0));
                }
            }

            // Render installed equipment
            for (int i = 0; i < _engine.Items.Count; i++)
            {
                var item = _engine.Items[i];
                double topY = r.Bottom - (item.EndUnit * unitHeightPx);
                double itemH = item.UnitHeight * unitHeightPx;
                var itemRect = new Rect(bayLeft + 2, topY + 1, bayWidth - 4, itemH - 2);

                DrawEquipmentItem(dc, item, itemRect, dpi);
            }
        }

        private void DrawEquipmentItem(DrawingContext dc, RackSlotItem item, Rect r, double dpi)
        {
            bool isSelected = item == _selectedItem;

            Color baseColor = ZeroWpfTheme.IsDark
                ? item.Category switch
                {
                    RackUnitCategory.Server => Color.FromRgb(30, 41, 59),
                    RackUnitCategory.Switch => Color.FromRgb(22, 47, 58),
                    RackUnitCategory.StorageArray => Color.FromRgb(40, 32, 56),
                    RackUnitCategory.Router => Color.FromRgb(58, 28, 30),
                    RackUnitCategory.Ups => Color.FromRgb(48, 42, 22),
                    RackUnitCategory.Pdu => Color.FromRgb(25, 45, 35),
                    _ => Color.FromRgb(28, 33, 44)
                }
                : item.Category switch
                {
                    RackUnitCategory.Server => Color.FromRgb(224, 231, 255),
                    RackUnitCategory.Switch => Color.FromRgb(207, 250, 254),
                    RackUnitCategory.StorageArray => Color.FromRgb(243, 232, 255),
                    RackUnitCategory.Router => Color.FromRgb(254, 226, 226),
                    RackUnitCategory.Ups => Color.FromRgb(254, 243, 199),
                    RackUnitCategory.Pdu => Color.FromRgb(220, 252, 231),
                    _ => Color.FromRgb(241, 245, 249)
                };

            var itemBg = new SolidColorBrush(baseColor);
            itemBg.Freeze();
            var itemPen = isSelected
                ? new Pen(ZeroWpfTheme.PrimaryAccent, 2.0)
                : ZeroWpfTheme.BorderPen;
            itemPen.Freeze();

            dc.DrawRoundedRectangle(itemBg, itemPen, r, 3, 3);

            // LED indicator
            Brush ledBrush = item.Status switch
            {
                RackSlotStatus.Normal => ZeroWpfTheme.SuccessAccent,
                RackSlotStatus.Warning => ZeroWpfTheme.WarningAccent,
                RackSlotStatus.Critical => ZeroWpfTheme.DangerAccent,
                _ => ZeroWpfTheme.TextMuted
            };
            dc.DrawEllipse(ledBrush, null, new Point(r.Left + 12, r.Top + r.Height / 2.0), 3.5, 3.5);

            // Category Badge
            double textX = r.Left + 22;
            if (r.Height >= 14)
            {
                var nameFt = CreateFormattedText(item.Name, ZeroWpfTheme.BoldTypeface, 9.5, ZeroWpfTheme.TextPrimary, dpi);
                dc.DrawText(nameFt, new Point(textX, r.Top + 2));

                if (r.Height >= 28)
                {
                    string info = $"{item.Model} | {item.PowerDrawWatts}W | {item.TemperatureCelsius:F1}°C";
                    var infoFt = CreateFormattedText(info, ZeroWpfTheme.RegularTypeface, 8.5, ZeroWpfTheme.TextSecondary, dpi);
                    dc.DrawText(infoFt, new Point(textX, r.Top + 14));
                }
            }
        }

        private void DrawThermalElevationStrip(DrawingContext dc, Rect r, double dpi)
        {
            var lgb = new LinearGradientBrush(
                Color.FromRgb(239, 68, 68),  // Top: Hot (Red)
                Color.FromRgb(16, 185, 129), // Bottom: Cool (Green)
                new Point(0, 0),
                new Point(0, 1));
            lgb.Freeze();

            var pen = ZeroWpfTheme.BorderPen;
            dc.DrawRoundedRectangle(lgb, pen, r, 3, 3);

            var hotFt = CreateFormattedText("45°C", ZeroWpfTheme.BoldTypeface, 7.5, Brushes.White, dpi);
            var coolFt = CreateFormattedText("18°C", ZeroWpfTheme.BoldTypeface, 7.5, Brushes.White, dpi);

            dc.DrawText(hotFt, new Point(r.Left + (r.Width - hotFt.Width) / 2.0, r.Top + 2));
            dc.DrawText(coolFt, new Point(r.Left + (r.Width - coolFt.Width) / 2.0, r.Bottom - coolFt.Height - 2));
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            Point p = e.GetPosition(this);

            double headerH = 44;
            double railTop = headerH + 10;
            double railHeight = (ActualHeight - 10) - railTop;
            double unitHeightPx = railHeight / _engine.TotalUnits;

            for (int i = 0; i < _engine.Items.Count; i++)
            {
                var item = _engine.Items[i];
                double topY = (ActualHeight - 10) - (item.EndUnit * unitHeightPx);
                double itemH = item.UnitHeight * unitHeightPx;

                if (p.Y >= topY && p.Y <= topY + itemH)
                {
                    SelectedSlotItem = item;
                    return;
                }
            }

            SelectedSlotItem = null;
        }
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZDeviceRack"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("DeviceRack is deprecated and will be removed in 5 release cycles. Please migrate to ZDeviceRack instead.")]
    public class DeviceRack : ZDeviceRack { }

    /// <summary>
    /// Legacy alias for <see cref="ZDeviceRack"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroDeviceRack is deprecated and will be removed in 5 release cycles. Please migrate to ZDeviceRack instead.")]
    public class ZeroDeviceRack : ZDeviceRack { }

    #endregion

}
