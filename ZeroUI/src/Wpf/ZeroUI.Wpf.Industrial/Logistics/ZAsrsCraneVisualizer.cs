using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using ZeroUI.Core.Logistics;
using ZeroUI.Core.Rendering;
using ZeroUI.Wpf.Base;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Logistics
{
    /// <summary>
    /// Automated Storage and Retrieval System (ASRS) Stacker Crane Visualizer for WPF.
    /// Renders high-bay racking matrix, traveling crane mast, hoist carriage,
    /// telescopic fork extension, pallet payload status, and live PPH throughput metrics.
    /// </summary>
    public class ZAsrsCraneVisualizer : ZeroWpfVisualBase
    {
        private readonly AsrsEngine _engine = new AsrsEngine();
        private string _aisleTag = "AISLE-04";

        protected override bool AutoAnimate => true;

        #region Properties

        public AsrsEngine Engine => _engine;

        public string AisleTag
        {
            get => _aisleTag;
            set
            {
                _aisleTag = value;
                InvalidateVisual();
            }
        }

        public double CurrentBay
        {
            get => _engine.Position.CurrentBay;
            set
            {
                _engine.Position.CurrentBay = Math.Max(1.0, Math.Min(_engine.Config.TotalBays, value));
                InvalidateVisual();
            }
        }

        public double CurrentTier
        {
            get => _engine.Position.CurrentTier;
            set
            {
                _engine.Position.CurrentTier = Math.Max(1.0, Math.Min(_engine.Config.TotalTiers, value));
                InvalidateVisual();
            }
        }

        public double ForkExtensionPct
        {
            get => _engine.Position.ForkExtensionPct;
            set
            {
                _engine.Position.ForkExtensionPct = Math.Max(0.0, Math.Min(100.0, value));
                InvalidateVisual();
            }
        }

        public AsrsForkDirection ForkDirection
        {
            get => _engine.Position.Direction;
            set
            {
                _engine.Position.Direction = value;
                InvalidateVisual();
            }
        }

        public bool HasPallet
        {
            get => _engine.Payload.HasPallet;
            set
            {
                _engine.Payload.HasPallet = value;
                InvalidateVisual();
            }
        }

        #endregion



        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            double w = ActualWidth;
            double h = ActualHeight;
            if (w < 120 || h < 80)
                return;

            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;

            // Background
            dc.DrawRectangle(ZeroWpfTheme.BgPrimary, null, new Rect(0, 0, w, h));

            // Top Header & HUD
            DrawHeader(dc, w, dpi);

            // Rack Area
            double rackLeft = 20;
            double rackTop = 50;
            double rackW = w - 40;
            double rackH = h - rackTop - 20;

            if (rackW < 150 || rackH < 60)
                return;

            Rect rackRect = new Rect(rackLeft, rackTop, rackW, rackH);

            // Draw High-Bay Rack Grid
            DrawRackGrid(dc, rackRect);

            // Rails
            DrawRails(dc, rackRect);

            // Crane Assembly
            DrawCraneAssembly(dc, rackRect);
        }

        private void DrawHeader(DrawingContext dc, double w, double dpi)
        {
            var titleFt = CreateFormattedText($"{_aisleTag} — ASRS Stacker Crane", ZeroWpfTheme.BoldTypeface, 11.5, ZeroWpfTheme.TextPrimary, dpi);
            dc.DrawText(titleFt, new Point(16, 12));

            double hudX = w - 420;
            if (hudX > 200)
            {
                string posStr = $"Bay: {_engine.Position.CurrentBay:F1} | Tier: {_engine.Position.CurrentTier:F1}";
                var posFt = CreateFormattedText(posStr, ZeroWpfTheme.RegularTypeface, 9.5, ZeroWpfTheme.TextSecondary, dpi);
                dc.DrawText(posFt, new Point(hudX, 14));

                string loadStr = _engine.Payload.HasPallet ? $"Pallet: {_engine.Payload.PalletBarcode} ({_engine.Payload.WeightKg:N0}kg)" : "EMPTY";
                var loadFt = CreateFormattedText(loadStr, ZeroWpfTheme.BoldTypeface, 9.5, Brushes.SkyBlue, dpi);
                dc.DrawText(loadFt, new Point(hudX + 150, 14));

                // PPH Badge
                Rect badgeRect = new Rect(w - 90, 10, 74, 24);
                dc.DrawRectangle(ZeroWpfTheme.BgCard, new Pen(Brushes.MediumSeaGreen, 1.0), badgeRect);
                var pphFt = CreateFormattedText($"{_engine.Stats.HourlyThroughputPph:F0} PPH", ZeroWpfTheme.BoldTypeface, 9.0, Brushes.MediumSeaGreen, dpi);
                dc.DrawText(pphFt, new Point(badgeRect.Left + 10, badgeRect.Top + 4));
            }
        }

        private void DrawRackGrid(DrawingContext dc, Rect rect)
        {
            dc.DrawRectangle(ZeroWpfTheme.BgCard, ZeroWpfTheme.BorderPen, rect);

            int bays = Math.Max(1, _engine.Config.TotalBays);
            int tiers = Math.Max(1, _engine.Config.TotalTiers);

            double cellW = rect.Width / bays;
            double cellH = rect.Height / tiers;

            Pen gridPen = ZeroWpfTheme.BorderPen;

            for (int b = 1; b < bays; b++)
            {
                double x = rect.Left + b * cellW;
                dc.DrawLine(gridPen, new Point(x, rect.Top), new Point(x, rect.Bottom));
            }

            for (int t = 1; t < tiers; t++)
            {
                double y = rect.Top + t * cellH;
                dc.DrawLine(gridPen, new Point(rect.Left, y), new Point(rect.Right, y));
            }
        }

        private static void DrawRails(DrawingContext dc, Rect rack)
        {
            Pen railPen = new Pen(Brushes.SlateGray, 2.5);
            railPen.Freeze();

            dc.DrawLine(railPen, new Point(rack.Left - 6, rack.Top - 2), new Point(rack.Right + 6, rack.Top - 2));
            dc.DrawLine(railPen, new Point(rack.Left - 6, rack.Bottom + 2), new Point(rack.Right + 6, rack.Bottom + 2));
        }

        private void DrawCraneAssembly(DrawingContext dc, Rect rack)
        {
            int bays = Math.Max(1, _engine.Config.TotalBays);
            int tiers = Math.Max(1, _engine.Config.TotalTiers);

            double cellW = rack.Width / bays;
            double cellH = rack.Height / tiers;

            double craneX = rack.Left + (_engine.Position.CurrentBay - 0.5) * cellW;
            double carriageY = rack.Bottom - (_engine.Position.CurrentTier - 0.5) * cellH;

            // 1. Mast (Amber/Orange)
            double mastW = Math.Max(10.0, cellW * 0.45);
            Rect mastRect = new Rect(craneX - mastW / 2, rack.Top - 3, mastW, rack.Height + 6);

            Brush mastBrush = Brushes.Orange;
            Pen mastPen = new Pen(Brushes.DarkOrange, 1.2);
            mastPen.Freeze();

            dc.DrawRectangle(mastBrush, mastPen, mastRect);

            // Wheel truck
            dc.DrawRectangle(Brushes.DarkSlateGray, null, new Rect(craneX - mastW, rack.Bottom, mastW * 2, 6));

            // 2. Hoist Carriage
            double carrW = mastW * 1.6;
            double carrH = Math.Max(14.0, cellH * 0.7);
            Rect carrRect = new Rect(craneX - carrW / 2, carriageY - carrH / 2, carrW, carrH);

            dc.DrawRectangle(ZeroWpfTheme.BgHover, new Pen(Brushes.SkyBlue, 1.5), carrRect);

            // 3. Forks & Pallet
            double stroke = (_engine.Position.ForkExtensionPct / 100.0) * cellW * 0.85;
            double forkX = craneX - carrW / 2;

            if (_engine.Position.Direction == AsrsForkDirection.Right)
                forkX += stroke;
            else if (_engine.Position.Direction == AsrsForkDirection.Left)
                forkX -= stroke;

            Pen tinePen = new Pen(Brushes.LightGray, 2.0);
            tinePen.Freeze();
            dc.DrawLine(tinePen, new Point(forkX - 6, carriageY + carrH * 0.3), new Point(forkX + carrW + 6, carriageY + carrH * 0.3));

            if (_engine.Payload.HasPallet)
            {
                double pW = carrW * 0.9;
                double pH = carrH * 0.6;
                Rect pRect = new Rect(forkX + (carrW - pW) / 2, carriageY - carrH * 0.2 - pH, pW, pH);
                dc.DrawRectangle(Brushes.SkyBlue, new Pen(Brushes.DodgerBlue, 1.0), pRect);
            }
        }
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZAsrsCraneVisualizer"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("AsrsCraneVisualizer is deprecated and will be removed in 5 release cycles. Please migrate to ZAsrsCraneVisualizer instead.")]
    public class AsrsCraneVisualizer : ZAsrsCraneVisualizer { }

    /// <summary>
    /// Legacy alias for <see cref="ZAsrsCraneVisualizer"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroAsrsCraneVisualizer is deprecated and will be removed in 5 release cycles. Please migrate to ZAsrsCraneVisualizer instead.")]
    public class ZeroAsrsCraneVisualizer : ZAsrsCraneVisualizer { }

    #endregion

}
