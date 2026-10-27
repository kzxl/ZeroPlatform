using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using ZeroUI.Core.Energy;
using ZeroUI.WinForms.Base;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Energy
{
    /// <summary>
    /// Medium/High-Voltage Vacuum Circuit Breaker (VCB) Switchgear front faceplate visualizer.
    /// Displays operating spring charge mechanical flag, vacuum bottle contact wear gauge,
    /// trip coil continuous supervision LEDs, LOTO padlock interlock, and protection relay telemetry.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Energy & Smart Grid")]
    [Description("MV/HV Switchgear faceplate with spring charge indicator, contact wear gauge, trip coil supervision, and LOTO interlock")]
    public class ZSwitchgearFaceplate : VisualControlBase
    {
        private readonly SwitchgearEngine _engine = new SwitchgearEngine();
        private string _breakerTag = "VCB-BAY-04 (110kV Incomer)";

        protected override bool AutoAnimate => true;

        public ZSwitchgearFaceplate()
        {
            Size = new Size(680, 360);
        }

        #region Public Properties

        [Category("Switchgear")]
        [Description("Access to the underlying Switchgear computation engine")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public SwitchgearEngine Engine => _engine;

        [Category("Switchgear")]
        [DefaultValue("VCB-BAY-04 (110kV Incomer)")]
        public string BreakerTag
        {
            get => _breakerTag;
            set
            {
                _breakerTag = value;
                Invalidate();
            }
        }

        #endregion

        protected override void OnDrawVisual(Graphics g, Rectangle bounds, ZeroThemePalette theme)
        {
            // Header banner
            DrawHeader(g, theme, out int contentTop);

            int contentWidth = bounds.Width - 40;
            int contentHeight = bounds.Height - contentTop - 16;
            if (contentWidth < 120 || contentHeight < 80)
                return;

            if (contentWidth >= 660)
            {
                // Wide mode: 3 horizontal columns
                int col1W = (int)(contentWidth * 0.32);
                int col2W = (int)(contentWidth * 0.36);
                int col3W = contentWidth - col1W - col2W - 24;

                int col1X = 20;
                int col2X = col1X + col1W + 12;
                int col3X = col2X + col2W + 12;

                Rectangle col1Rect = new Rectangle(col1X, contentTop, col1W, contentHeight);
                Rectangle col2Rect = new Rectangle(col2X, contentTop, col2W, contentHeight);
                Rectangle col3Rect = new Rectangle(col3X, contentTop, col3W, contentHeight);

                DrawMechanicalPanel(g, col1Rect, theme, isStacked: false);
                DrawVacuumHealthPanel(g, col2Rect, theme, isStacked: false);
                DrawRelayInterlockPanel(g, col3Rect, theme, isStacked: false);
            }
            else
            {
                // Responsive mode: Stacked cards vertically for narrow splitters
                int cardGap = 8;
                int cardH = Math.Max(120, (contentHeight - cardGap * 2) / 3);

                Rectangle col1Rect = new Rectangle(20, contentTop, contentWidth, cardH);
                Rectangle col2Rect = new Rectangle(20, col1Rect.Bottom + cardGap, contentWidth, cardH);
                Rectangle col3Rect = new Rectangle(20, col2Rect.Bottom + cardGap, contentWidth, cardH);

                DrawMechanicalPanel(g, col1Rect, theme, isStacked: true);
                DrawVacuumHealthPanel(g, col2Rect, theme, isStacked: true);
                DrawRelayInterlockPanel(g, col3Rect, theme, isStacked: true);
            }
        }

        private void DrawHeader(Graphics g, ZeroThemePalette theme, out int contentTop)
        {
            using (var fontTitle = new Font("Segoe UI", 10.5f, FontStyle.Bold))
            using (var fontBold = new Font("Segoe UI", 8.5f, FontStyle.Bold))
            {
                bool permit = SwitchgearEngine.ValidateClosePermit(_engine.Mechanism, out string reason);
                Color permitColor = permit ? Color.FromArgb(34, 197, 94) : Color.FromArgb(239, 68, 68);

                Size titleSize = TextRenderer.MeasureText(g, _breakerTag, fontTitle);
                Size badgeSize = TextRenderer.MeasureText(g, reason, fontBold);
                int badgeW = Math.Min(260, Math.Max(180, badgeSize.Width + 24));
                int badgeH = 26;

                if (Width - badgeW - 20 >= 20 + titleSize.Width + 16)
                {
                    // Single row layout
                    TextRenderer.DrawText(g, _breakerTag, fontTitle, new Point(20, 14), theme.TextPrimary);
                    Rectangle pillRect = new Rectangle(Width - badgeW - 20, 12, badgeW, badgeH);
                    DrawStatusBadge(g, pillRect, reason, fontBold, permitColor, permitColor, 4);
                    contentTop = 52;
                }
                else
                {
                    // Stacked row layout (prevent title/badge overlap)
                    TextRenderer.DrawText(g, _breakerTag, fontTitle, new Point(20, 10), theme.TextPrimary);
                    Rectangle pillRect = new Rectangle(20, 36, Math.Min(Width - 40, badgeW), badgeH);
                    DrawStatusBadge(g, pillRect, reason, fontBold, permitColor, permitColor, 4);
                    contentTop = 72;
                }
            }
        }

        private void DrawMechanicalPanel(Graphics g, Rectangle rect, ZeroThemePalette theme, bool isStacked)
        {
            using (var fontHead = new Font("Segoe UI", 7.5f, FontStyle.Bold))
            using (var fontLabel = new Font("Segoe UI", 8.5f))
            using (var fontValue = new Font("Segoe UI", 9f, FontStyle.Bold))
            using (var fontFlag = new Font("Segoe UI", 10.5f, FontStyle.Bold))
            {
                DrawCardBox(g, rect, "MECHANISM & CHARGE", fontHead, theme);

                var mech = _engine.Mechanism;
                Color flagBg = mech.IsSpringCharged ? Color.FromArgb(245, 158, 11) : Color.FromArgb(75, 85, 99);
                string flagText = mech.IsSpringCharged ? "SPR. CHARGED" : "DISCHARGED";

                if (isStacked && rect.Width >= 280)
                {
                    int flagW = Math.Min(140, (int)(rect.Width * 0.36));
                    Rectangle flagRect = new Rectangle(rect.X + 16, rect.Y + 32, flagW, rect.Height - 44);
                    using (var flagBrush = new SolidBrush(flagBg))
                    using (var flagPen = new Pen(Color.FromArgb(200, flagBg), 2f))
                    {
                        g.FillRectangle(flagBrush, flagRect);
                        g.DrawRectangle(flagPen, flagRect);
                    }
                    TextRenderer.DrawText(g, flagText, fontFlag, flagRect, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

                    int dataX = flagRect.Right + 16;
                    int dataW = rect.Right - dataX - 16;
                    int rowH = Math.Min(24, (rect.Height - 40) / 4);
                    int rowY = rect.Y + 30;

                    string motorStatus = mech.IsMotorCharging ? "Motor Running..." : "Idle";
                    DrawDataRow(g, new Rectangle(dataX, rowY, dataW, rowH), "Charge Motor:", motorStatus, theme.TextSecondary, theme.TextPrimary, fontLabel, fontValue);
                    rowY += rowH;
                    DrawDataRow(g, new Rectangle(dataX, rowY, dataW, rowH), "Operations Count:", mech.TotalOperationsCount.ToString("N0"), theme.TextSecondary, theme.TextPrimary, fontLabel, fontValue);
                    rowY += rowH;
                    DrawDataRow(g, new Rectangle(dataX, rowY, dataW, rowH), "Mech Temp:", $"{mech.MechanismTemperatureC:F1} °C", theme.TextSecondary, theme.TextPrimary, fontLabel, fontValue);
                    rowY += rowH;
                    DrawDataRow(g, new Rectangle(dataX, rowY, dataW, rowH), "Rated Breaking:", $"{mech.RatedShortCircuitKa:F1} kA", theme.TextSecondary, theme.TextPrimary, fontLabel, fontValue);
                }
                else
                {
                    Rectangle flagRect = new Rectangle(rect.X + 16, rect.Y + 34, rect.Width - 32, 42);
                    using (var flagBrush = new SolidBrush(flagBg))
                    using (var flagPen = new Pen(Color.FromArgb(200, flagBg), 2f))
                    {
                        g.FillRectangle(flagBrush, flagRect);
                        g.DrawRectangle(flagPen, flagRect);
                    }
                    TextRenderer.DrawText(g, flagText, fontFlag, flagRect, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

                    int rowY = flagRect.Bottom + 10;
                    int rowH = 22;
                    string motorStatus = mech.IsMotorCharging ? "Motor Running..." : "Idle";
                    DrawDataRow(g, new Rectangle(rect.X + 16, rowY, rect.Width - 32, rowH), "Charge Motor:", motorStatus, theme.TextSecondary, theme.TextPrimary, fontLabel, fontValue);
                    rowY += rowH;
                    DrawDataRow(g, new Rectangle(rect.X + 16, rowY, rect.Width - 32, rowH), "Operations Count:", mech.TotalOperationsCount.ToString("N0"), theme.TextSecondary, theme.TextPrimary, fontLabel, fontValue);
                    rowY += rowH;
                    DrawDataRow(g, new Rectangle(rect.X + 16, rowY, rect.Width - 32, rowH), "Mech Temp:", $"{mech.MechanismTemperatureC:F1} °C", theme.TextSecondary, theme.TextPrimary, fontLabel, fontValue);
                    rowY += rowH;
                    DrawDataRow(g, new Rectangle(rect.X + 16, rowY, rect.Width - 32, rowH), "Rated Breaking:", $"{mech.RatedShortCircuitKa:F1} kA", theme.TextSecondary, theme.TextPrimary, fontLabel, fontValue);
                }
            }
        }

        private void DrawVacuumHealthPanel(Graphics g, Rectangle rect, ZeroThemePalette theme, bool isStacked)
        {
            using (var fontHead = new Font("Segoe UI", 7.5f, FontStyle.Bold))
            using (var fontLabel = new Font("Segoe UI", 8.5f))
            using (var fontValue = new Font("Segoe UI", 9f, FontStyle.Bold))
            using (var fontSmall = new Font("Segoe UI", 7.5f))
            {
                DrawCardBox(g, rect, "VACUUM BOTTLE HEALTH", fontHead, theme);

                var mech = _engine.Mechanism;

                int barY = rect.Y + 32;
                TextRenderer.DrawText(g, "Contact Erosion Wear:", fontLabel, new Point(rect.X + 16, barY), theme.TextSecondary);
                Size wearSize = TextRenderer.MeasureText(g, $"{mech.VacuumBottleContactWearPct:F1}%", fontValue);
                TextRenderer.DrawText(g, $"{mech.VacuumBottleContactWearPct:F1}%", fontValue, new Point(Math.Max(rect.X + 150, rect.Right - 16 - wearSize.Width), barY),
                    mech.IsContactWearCritical ? Color.FromArgb(239, 68, 68) : theme.TextPrimary);

                Rectangle barRect = new Rectangle(rect.X + 16, barY + 20, rect.Width - 32, 12);
                using (var bgBrush = new SolidBrush(Color.FromArgb(40, 128, 128, 128)))
                {
                    g.FillRectangle(bgBrush, barRect);
                }

                Color barColor = mech.VacuumBottleContactWearPct < 60 ? Color.FromArgb(34, 197, 94) :
                                 mech.VacuumBottleContactWearPct < 85 ? Color.FromArgb(245, 158, 11) :
                                 Color.FromArgb(239, 68, 68);

                int fillW = (int)((barRect.Width * Math.Min(100.0, mech.VacuumBottleContactWearPct)) / 100.0);
                if (fillW > 0)
                {
                    using (var fillBrush = new SolidBrush(barColor))
                    {
                        g.FillRectangle(fillBrush, barRect.X, barRect.Y, fillW, barRect.Height);
                    }
                }

                if (isStacked && rect.Width >= 320)
                {
                    int coilY = barRect.Bottom + 14;
                    int midX = rect.X + (rect.Width / 2);

                    TextRenderer.DrawText(g, "Trip Coil 1:", fontLabel, new Point(rect.X + 16, coilY), theme.TextSecondary);
                    DrawTripCoil(g, midX - 35, coilY + 2, mech.TripCoil1);

                    TextRenderer.DrawText(g, "Trip Coil 2:", fontLabel, new Point(midX + 10, coilY), theme.TextSecondary);
                    DrawTripCoil(g, rect.Right - 36, coilY + 2, mech.TripCoil2);

                    int chY = coilY + 24;
                    Rectangle chamberRect = new Rectangle(rect.X + 16, chY, rect.Width - 32, Math.Max(22, rect.Bottom - chY - 12));
                    using (var chBrush = new SolidBrush(Color.FromArgb(20, 59, 130, 246)))
                    using (var chPen = new Pen(Color.FromArgb(59, 130, 246), 1f))
                    {
                        g.FillRectangle(chBrush, chamberRect);
                        g.DrawRectangle(chPen, chamberRect);
                    }
                    TextRenderer.DrawText(g, "Vacuum Integrity: HIGH-INTEGRITY", fontSmall, chamberRect, Color.FromArgb(59, 130, 246), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
                else
                {
                    int coilY = barRect.Bottom + 14;
                    TextRenderer.DrawText(g, "Trip Coil 1 (TC1):", fontLabel, new Point(rect.X + 16, coilY), theme.TextSecondary);
                    DrawTripCoil(g, rect.Right - 60, coilY + 2, mech.TripCoil1);

                    coilY += 24;
                    TextRenderer.DrawText(g, "Trip Coil 2 (TC2):", fontLabel, new Point(rect.X + 16, coilY), theme.TextSecondary);
                    DrawTripCoil(g, rect.Right - 60, coilY + 2, mech.TripCoil2);

                    coilY += 26;
                    Rectangle chamberRect = new Rectangle(rect.X + 16, coilY, rect.Width - 32, Math.Max(24, rect.Bottom - coilY - 12));
                    using (var chBrush = new SolidBrush(Color.FromArgb(20, 59, 130, 246)))
                    using (var chPen = new Pen(Color.FromArgb(59, 130, 246), 1f))
                    {
                        g.FillRectangle(chBrush, chamberRect);
                        g.DrawRectangle(chPen, chamberRect);
                    }
                    TextRenderer.DrawText(g, "Vacuum Integrity: HIGH-INTEGRITY", fontSmall, chamberRect, Color.FromArgb(59, 130, 246), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
            }
        }

        private void DrawRelayInterlockPanel(Graphics g, Rectangle rect, ZeroThemePalette theme, bool isStacked)
        {
            using (var fontHead = new Font("Segoe UI", 7.5f, FontStyle.Bold))
            using (var fontLabel = new Font("Segoe UI", 8.5f))
            using (var fontValue = new Font("Segoe UI", 9f, FontStyle.Bold))
            using (var fontBold = new Font("Segoe UI", 9f, FontStyle.Bold))
            {
                DrawCardBox(g, rect, "RELAY & LOTO SAFETY", fontHead, theme);

                var relay = _engine.Relay;
                var mech = _engine.Mechanism;

                Color pickColor = relay.IsPickupActive ? Color.FromArgb(239, 68, 68) : Color.FromArgb(34, 197, 94);
                Color lotoColor = mech.LotoState == LotoLockState.LockedOut ? Color.FromArgb(239, 68, 68) :
                                  mech.LotoState == LotoLockState.PermitActive ? Color.FromArgb(245, 158, 11) :
                                  Color.FromArgb(34, 197, 94);
                string lotoText = mech.LotoState == LotoLockState.LockedOut ? "[LOCKED OUT] Padlock Applied" :
                                  mech.LotoState == LotoLockState.PermitActive ? "[PERMIT ACTIVE] Tagged" :
                                  "[UNLOCKED] No Interlock";

                if (isStacked && rect.Width >= 320)
                {
                    int leftW = (int)(rect.Width * 0.52);
                    int rowY = rect.Y + 30;
                    int rowH = 22;

                    DrawDataRow(g, new Rectangle(rect.X + 16, rowY, leftW - 24, rowH), "Relay Type:", relay.RelayTag, theme.TextSecondary, theme.TextPrimary, fontLabel, fontValue);
                    rowY += rowH;
                    DrawDataRow(g, new Rectangle(rect.X + 16, rowY, leftW - 24, rowH), "ANSI Protect:", relay.ActiveAnsiCode, theme.TextSecondary, Color.FromArgb(59, 130, 246), fontLabel, fontValue);
                    rowY += rowH;
                    DrawDataRow(g, new Rectangle(rect.X + 16, rowY, leftW - 24, rowH), "Pickup / Alarm:", relay.IsPickupActive ? "PICKUP ACTIVE" : "Normal", theme.TextSecondary, pickColor, fontLabel, fontValue);

                    int rightX = rect.X + leftW;
                    Rectangle lotoRect = new Rectangle(rightX, rect.Y + 30, rect.Right - rightX - 16, rect.Height - 42);
                    DrawStatusBadge(g, lotoRect, lotoText, fontBold, lotoColor, lotoColor, 4);
                }
                else
                {
                    int rowY = rect.Y + 30;
                    int rowH = 22;

                    DrawDataRow(g, new Rectangle(rect.X + 16, rowY, rect.Width - 32, rowH), "Relay Type:", relay.RelayTag, theme.TextSecondary, theme.TextPrimary, fontLabel, fontValue);
                    rowY += rowH;
                    DrawDataRow(g, new Rectangle(rect.X + 16, rowY, rect.Width - 32, rowH), "ANSI Protect:", relay.ActiveAnsiCode, theme.TextSecondary, Color.FromArgb(59, 130, 246), fontLabel, fontValue);
                    rowY += rowH;
                    DrawDataRow(g, new Rectangle(rect.X + 16, rowY, rect.Width - 32, rowH), "Pickup / Alarm:", relay.IsPickupActive ? "PICKUP ACTIVE" : "Normal", theme.TextSecondary, pickColor, fontLabel, fontValue);

                    rowY += 24;
                    Rectangle lotoRect = new Rectangle(rect.X + 16, rowY, rect.Width - 32, Math.Max(32, rect.Bottom - rowY - 12));
                    DrawStatusBadge(g, lotoRect, lotoText, fontBold, lotoColor, lotoColor, 4);
                }
            }
        }

        private void DrawTripCoil(Graphics g, int x, int y, TripCoilState state)
        {
            Color ledColor = state == TripCoilState.Healthy ? Color.FromArgb(34, 197, 94) :
                             state == TripCoilState.OpenCircuit ? Color.FromArgb(239, 68, 68) :
                             Color.FromArgb(245, 158, 11);
            string text = state == TripCoilState.Healthy ? "OK" : "FAULT";
            using (var font = new Font("Segoe UI", 7.5f, FontStyle.Bold))
            {
                DrawLedIndicator(g, x, y, ledColor, text, font);
            }
        }
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZSwitchgearFaceplate"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("SwitchgearFaceplate is deprecated and will be removed in 5 release cycles. Please migrate to ZSwitchgearFaceplate instead.")]
    [ToolboxItem(false)]
    public class SwitchgearFaceplate : ZSwitchgearFaceplate
    {
    }

    #endregion
}
