using System;
using System.Drawing;
using System.Windows.Forms;
using ZeroUI.Core.Scada;
using ZeroUI.Core.Scada.Safety;
using ZeroUI.WinForms.Base;
using ZeroUI.WinForms.Containers;
using ZeroUI.WinForms.Editors;
using ZeroUI.WinForms.Feedback;
using ZeroUI.WinForms.Industrial;
using ZeroUI.WinForms.Navigation;
using ZeroUI.WinForms.Theme;
using Card = ZeroUI.WinForms.Containers.Card;

namespace ZeroUI.Samples.WinformDemo.Forms
{
    public sealed partial class MainForm
    {
        private void InitializeScadaSafetyCluster(ZeroTabPage page)
        {
            var colors = ZeroTheme.Colors;
            page.BackColor = colors.Background;

            var mainContainer = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = colors.Background,
                Padding = new Padding(14)
            };

            // 1. Top Alert Banner
            var banner = new ZeroUI.WinForms.Feedback.AlertBanner
            {
                Dock = DockStyle.Top,
                Height = 62,
                Severity = ZeroAlertSeverity.Warning,
                Title = "🚨 PHASE 6: SIL 3 / PLe SAFETY INTERLOCKS & ISA-18.2 ALARM ANNUNCIATORS",
                Message = "Integrated machinery protection suite combining IEC 60947-5-5 Emergency Stops, Type 4 optical safety light curtains, permissive interlocks, and the central ISA-18.2 alarm annunciator matrix."
            };

            var spacerBanner = new Panel { Dock = DockStyle.Top, Height = 8, BackColor = Color.Transparent };

            // 2. Top ISA-18.2 Alarm Banner Ticker
            var alarmBanner = new ZAlarmBanner
            {
                Dock = DockStyle.Top,
                Height = 36,
                OperatorName = "Chief Safety Officer"
            };

            var spacerSplit = new Panel { Dock = DockStyle.Top, Height = 10, BackColor = Color.Transparent };

            // 3. Main Dual Split Container
            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 530,
                SplitterWidth = 8,
                BackColor = Color.Transparent
            };

            // =========================================================================
            // LEFT CARD: Machine Safety & Optical Protective Barriers (SIL 3 / PLe)
            // =========================================================================
            var cardSafety = new Card
            {
                Dock = DockStyle.Fill,
                Title = "SIL 3 / PLe Machine Safety & Optical Protective Barriers",
                Subtitle = "Dual-channel E-Stop, 16-beam Type 4 optical barrier & safety permissive interlocks",
                StepNumber = 1,
                AutoFitContent = false
            };

            var pnlSafetyContent = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                Padding = new Padding(12),
                BackColor = Color.Transparent
            };

            // Section 1: Emergency Stop Actuator
            var lblEStopHeader = new Label
            {
                Text = "EMERGENCY STOP ACTUATOR (IEC 60947-5-5 / ISO 13850)",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = colors.Danger,
                AutoSize = true,
                Location = new Point(14, 12)
            };

            var eStop = new ZEmergencyStop
            {
                Location = new Point(14, 38),
                Size = new Size(130, 130),
                ResetMode = EStopResetMode.TwistToReset
            };

            var lblEStopStatus = new Label
            {
                Text = "Status: READY (Dual Safety Contacts Closed)",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = colors.Success,
                AutoSize = true,
                Location = new Point(155, 40)
            };

            var interlock = new ZInterlockIndicator
            {
                Location = new Point(155, 70),
                Size = new Size(140, 42),
                TagLabel = "PERMISSIVE"
            };

            var btnTripEStop = new SimpleButton
            {
                Text = "🚨 Trip E-Stop",
                ButtonStyle = ZeroButtonStyle.Danger,
                Location = new Point(155, 124),
                Size = new Size(115, 32),
                Cursor = Cursors.Hand
            };

            var btnResetEStop = new SimpleButton
            {
                Text = "🔄 Reset E-Stop",
                ButtonStyle = ZeroButtonStyle.Success,
                Location = new Point(278, 124),
                Size = new Size(115, 32),
                Cursor = Cursors.Hand
            };

            var lblEStopHint = new Label
            {
                Text = "Tip: Click mushroom directly to trip, drag clockwise or click Reset to release.",
                Font = new Font("Segoe UI", 8.25f, FontStyle.Regular),
                ForeColor = colors.TextSecondary,
                AutoSize = true,
                Location = new Point(155, 164)
            };

            var pnlDiv1 = new Panel
            {
                Location = new Point(14, 194),
                Size = new Size(490, 1),
                BackColor = colors.Border
            };

            // Section 2: Type 4 Optical Safety Light Curtain
            var lblCurtainHeader = new Label
            {
                Text = "TYPE 4 SAFETY OPTICAL BARRIER (IEC 61496 16-BEAM ARRAY)",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(234, 179, 8),
                AutoSize = true,
                Location = new Point(14, 208)
            };

            var lightCurtain = new ZLightCurtainBar
            {
                Location = new Point(14, 234),
                Size = new Size(160, 240),
                BeamCount = 16,
                InteractiveSimulation = true
            };

            var lblCurtainStatus = new Label
            {
                Text = "Safety Field: CLEAR (Dual OSSD Outputs Active)",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = colors.Success,
                AutoSize = true,
                Location = new Point(185, 242)
            };

            var btnBreakBeam = new SimpleButton
            {
                Text = "✋ Break Beam",
                ButtonStyle = ZeroButtonStyle.Danger,
                Location = new Point(185, 278),
                Size = new Size(115, 32),
                Cursor = Cursors.Hand
            };

            var btnClearBeam = new SimpleButton
            {
                Text = "✔ Clear Field",
                ButtonStyle = ZeroButtonStyle.Secondary,
                Location = new Point(308, 278),
                Size = new Size(115, 32),
                Cursor = Cursors.Hand
            };

            var lblCurtainHint = new Label
            {
                Text = "Hover or drag mouse over optical beams to simulate intrusion into danger zone. Dual OSSD channels automatically disengage machine permissive.",
                Font = new Font("Segoe UI", 8.25f, FontStyle.Regular),
                ForeColor = colors.TextSecondary,
                Location = new Point(185, 322),
                Size = new Size(295, 45)
            };

            pnlSafetyContent.Controls.Add(lblEStopHeader);
            pnlSafetyContent.Controls.Add(eStop);
            pnlSafetyContent.Controls.Add(lblEStopStatus);
            pnlSafetyContent.Controls.Add(interlock);
            pnlSafetyContent.Controls.Add(btnTripEStop);
            pnlSafetyContent.Controls.Add(btnResetEStop);
            pnlSafetyContent.Controls.Add(lblEStopHint);
            pnlSafetyContent.Controls.Add(pnlDiv1);
            pnlSafetyContent.Controls.Add(lblCurtainHeader);
            pnlSafetyContent.Controls.Add(lightCurtain);
            pnlSafetyContent.Controls.Add(lblCurtainStatus);
            pnlSafetyContent.Controls.Add(btnBreakBeam);
            pnlSafetyContent.Controls.Add(btnClearBeam);
            pnlSafetyContent.Controls.Add(lblCurtainHint);

            cardSafety.ContentPanel.Controls.Add(pnlSafetyContent);
            split.Panel1.Controls.Add(cardSafety);

            // =========================================================================
            // RIGHT CARD: ISA-18.2 Plant Alarm Annunciator & SCADA Telemetry
            // =========================================================================
            var cardAnnunciator = new Card
            {
                Dock = DockStyle.Fill,
                Title = "Plant Alarm Annunciator Panel — ISA-18.2 Standard (12-Tile Matrix)",
                Subtitle = "Synchronized sequence state machine, multi-state pilot LEDs & hydraulic telemetry",
                StepNumber = 2,
                AutoFitContent = false
            };

            var pnlAnnuncContent = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                Padding = new Padding(12),
                BackColor = Color.Transparent
            };

            // Section 1: Central 12-Tile Annunciator Matrix
            var annunciator = new ZAnnunciatorGrid
            {
                Location = new Point(12, 10),
                Size = new Size(480, 210),
                Rows = 3,
                Columns = 4
            };

            annunciator.ClearTiles();
            annunciator.AddAlarm("Line1.Alarm.EmergencyStop", "EMERGENCY STOP (E-STOP)", IsaAlarmSeverity.Critical);
            annunciator.AddAlarm("Line1.Alarm.LightCurtain", "OPTICAL LIGHT CURTAIN (OSSD)", IsaAlarmSeverity.Critical);
            annunciator.AddAlarm("Line1.Alarm.Interlock", "SAFETY GUARD INTERLOCK", IsaAlarmSeverity.High);
            annunciator.AddAlarm("Line1.Alarm.HydraulicPress", "HYDRAULIC PRESSURE FAULT", IsaAlarmSeverity.Critical);
            annunciator.AddAlarm("Line1.Alarm.SafetyRelay", "SIL 3 SAFETY RELAY FAULT", IsaAlarmSeverity.High);
            annunciator.AddAlarm("Line1.Alarm.OshaLoto", "OSHA LOTO ACTIVE LOCKOUT", IsaAlarmSeverity.Medium);
            annunciator.AddAlarm("Line1.Alarm.SpindleTemp", "SPINDLE BEARING OVER-TEMP", IsaAlarmSeverity.High);
            annunciator.AddAlarm("Line1.Alarm.PumpTrip", "COOLANT PUMP OVERLOAD", IsaAlarmSeverity.High);
            annunciator.AddAlarm("Line1.Alarm.LowLevel", "LUBRICANT RESERVOIR LOW", IsaAlarmSeverity.Medium);
            annunciator.AddAlarm("Line1.Alarm.GasLeak", "CH4 GAS DETECTOR WARN", IsaAlarmSeverity.Critical);
            annunciator.AddAlarm("Line1.Alarm.UpsPower", "UPS ON BATTERY BACKUP", IsaAlarmSeverity.Medium);
            annunciator.AddAlarm("Line1.Alarm.PlcWatchdog", "SAFETY PLC WATCHDOG", IsaAlarmSeverity.High);

            // Annunciator Simulation Quick Toolbar
            var btnSimHighPress = new SimpleButton
            {
                Text = "+ High Press",
                ButtonStyle = ZeroButtonStyle.Danger,
                Location = new Point(12, 228),
                Size = new Size(100, 28),
                Cursor = Cursors.Hand
            };

            var btnSimPumpTrip = new SimpleButton
            {
                Text = "+ Pump Trip",
                ButtonStyle = ZeroButtonStyle.Danger,
                Location = new Point(118, 228),
                Size = new Size(100, 28),
                Cursor = Cursors.Hand
            };

            var btnSimLowLevel = new SimpleButton
            {
                Text = "+ Low Level",
                ButtonStyle = ZeroButtonStyle.Secondary,
                Location = new Point(224, 228),
                Size = new Size(95, 28),
                Cursor = Cursors.Hand
            };

            var btnSimClearFaults = new SimpleButton
            {
                Text = "✔ Clear Faults",
                ButtonStyle = ZeroButtonStyle.Success,
                Location = new Point(325, 228),
                Size = new Size(105, 28),
                Cursor = Cursors.Hand
            };

            var pnlDiv2 = new Panel
            {
                Location = new Point(12, 268),
                Size = new Size(480, 1),
                BackColor = colors.Border
            };

            // Section 2: SCADA Multi-State Pilot Indicators & Hydraulic Telemetry
            var lblPilotHeader = new Label
            {
                Text = "SCADA PILOT LIGHTS (ISA-101) & HYDRAULIC PRESSURE (DIN 43700)",
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = colors.TextSecondary,
                AutoSize = true,
                Location = new Point(12, 280)
            };

            var ledMaster = new ZMultiStateLed { Location = new Point(14, 304), Size = new Size(60, 72), State = MultiStateLedState.Normal, Label = "SYSTEM" };
            var ledEStop = new ZMultiStateLed { Location = new Point(80, 304), Size = new Size(60, 72), State = MultiStateLedState.Normal, Label = "E-STOP" };
            var ledCurtain = new ZMultiStateLed { Location = new Point(146, 304), Size = new Size(60, 72), State = MultiStateLedState.Normal, Label = "CURTAIN" };
            var ledInterlock = new ZMultiStateLed { Location = new Point(212, 304), Size = new Size(60, 72), State = MultiStateLedState.Normal, Label = "INTERLOCK" };

            var btnLampTest = new SimpleButton
            {
                Text = "💡 Lamp Test",
                ButtonStyle = ZeroButtonStyle.Ghost,
                Location = new Point(285, 322),
                Size = new Size(100, 32),
                Cursor = Cursors.Hand
            };

            var meterHydraulic = new ZEdgewiseMeter
            {
                Location = new Point(12, 388),
                Size = new Size(270, 68),
                Orientation = EdgewiseOrientation.Horizontal,
                Title = "HYDRAULIC PRESSURE",
                Unit = "bar",
                Value = 65.0,
                Minimum = 0,
                Maximum = 100,
                HighAlarmThreshold = 85,
                LowAlarmThreshold = 20
            };

            var btnSpikePress = new SimpleButton
            {
                Text = "📈 Spike 92 bar",
                ButtonStyle = ZeroButtonStyle.Danger,
                Location = new Point(292, 392),
                Size = new Size(115, 28),
                Cursor = Cursors.Hand
            };

            var btnNormPress = new SimpleButton
            {
                Text = "📉 Norm 65 bar",
                ButtonStyle = ZeroButtonStyle.Secondary,
                Location = new Point(292, 426),
                Size = new Size(115, 28),
                Cursor = Cursors.Hand
            };

            pnlAnnuncContent.Controls.Add(annunciator);
            pnlAnnuncContent.Controls.Add(btnSimHighPress);
            pnlAnnuncContent.Controls.Add(btnSimPumpTrip);
            pnlAnnuncContent.Controls.Add(btnSimLowLevel);
            pnlAnnuncContent.Controls.Add(btnSimClearFaults);
            pnlAnnuncContent.Controls.Add(pnlDiv2);
            pnlAnnuncContent.Controls.Add(lblPilotHeader);
            pnlAnnuncContent.Controls.Add(ledMaster);
            pnlAnnuncContent.Controls.Add(ledEStop);
            pnlAnnuncContent.Controls.Add(ledCurtain);
            pnlAnnuncContent.Controls.Add(ledInterlock);
            pnlAnnuncContent.Controls.Add(btnLampTest);
            pnlAnnuncContent.Controls.Add(meterHydraulic);
            pnlAnnuncContent.Controls.Add(btnSpikePress);
            pnlAnnuncContent.Controls.Add(btnNormPress);

            cardAnnunciator.ContentPanel.Controls.Add(pnlAnnuncContent);
            split.Panel2.Controls.Add(cardAnnunciator);

            // =========================================================================
            // REVENUE & SAFETY REACTIVE ORCHESTRATION ENGINE
            // =========================================================================
            void UpdateSystemSafetyLed()
            {
                bool isEStop = eStop.IsDepressed;
                bool isCurtain = lightCurtain.CurtainState == LightCurtainState.Tripped;
                bool isHydr = meterHydraulic.Value >= 85.0 || meterHydraulic.Value <= 20.0;

                if (isEStop)
                {
                    ledMaster.State = MultiStateLedState.Alarm;
                    ledMaster.BlinkRate = MultiStateLedBlink.Fast;
                    ledInterlock.State = MultiStateLedState.Alarm;
                    ledInterlock.BlinkRate = MultiStateLedBlink.Fast;
                }
                else if (isCurtain || isHydr)
                {
                    ledMaster.State = MultiStateLedState.Warning;
                    ledMaster.BlinkRate = MultiStateLedBlink.Fast;
                    ledInterlock.State = MultiStateLedState.Warning;
                    ledInterlock.BlinkRate = MultiStateLedBlink.Slow;
                }
                else
                {
                    ledMaster.State = MultiStateLedState.Normal;
                    ledMaster.BlinkRate = MultiStateLedBlink.None;
                    ledInterlock.State = MultiStateLedState.Normal;
                    ledInterlock.BlinkRate = MultiStateLedBlink.None;
                }
            }

            // E-Stop Event Wiring
            eStop.Tripped += (s, e) =>
            {
                lblEStopStatus.Text = "Status: TRIPPED (Interlock Open)";
                lblEStopStatus.ForeColor = ZeroTheme.Colors.Danger;
                interlock.SetInterlockCondition("Emergency Stop Depressed", true);
                annunciator.TriggerAlarm("Line1.Alarm.EmergencyStop", true);
                annunciator.TriggerAlarm("Line1.Alarm.Interlock", true);
                ScadaAlarmEngine.RaiseAlarm("ESTOP_01", "Machine.Safety.EStop", "CRITICAL: Emergency Stop Depressed on Assembly Line 1", ScadaAlarmSeverity.Critical);
                ledEStop.State = MultiStateLedState.Alarm;
                ledEStop.BlinkRate = MultiStateLedBlink.Fast;
                UpdateSystemSafetyLed();
            };

            eStop.ResetCompleted += (s, e) =>
            {
                lblEStopStatus.Text = "Status: READY (Dual Safety Contacts Closed)";
                lblEStopStatus.ForeColor = ZeroTheme.Colors.Success;
                interlock.SetInterlockCondition("Emergency Stop Depressed", false);
                annunciator.TriggerAlarm("Line1.Alarm.EmergencyStop", false);
                if (lightCurtain.CurtainState == LightCurtainState.Clear)
                {
                    annunciator.TriggerAlarm("Line1.Alarm.Interlock", false);
                }
                ScadaAlarmEngine.Acknowledge("ESTOP_01", "Chief Safety Officer");
                ScadaAlarmEngine.ClearAlarm("ESTOP_01");
                ledEStop.State = MultiStateLedState.Normal;
                ledEStop.BlinkRate = MultiStateLedBlink.None;
                UpdateSystemSafetyLed();
            };

            btnTripEStop.Click += (s, e) => eStop.Trip();
            btnResetEStop.Click += (s, e) => eStop.Reset();

            // Light Curtain Event Wiring
            lightCurtain.Tripped += (s, e) =>
            {
                lblCurtainStatus.Text = $"Safety Field: INTRUSION (Beam #{e.BeamIndex + 1} Breached)";
                lblCurtainStatus.ForeColor = ZeroTheme.Colors.Danger;
                interlock.SetInterlockCondition("Optical Curtain Intrusion", true);
                annunciator.TriggerAlarm("Line1.Alarm.LightCurtain", true);
                annunciator.TriggerAlarm("Line1.Alarm.Interlock", true);
                ScadaAlarmEngine.RaiseAlarm("CURTAIN_01", "Safety.Curtain.Trip", $"HIGH: Optical Curtain Intrusion at Beam #{e.BeamIndex + 1}", ScadaAlarmSeverity.High);
                ledCurtain.State = MultiStateLedState.Warning;
                ledCurtain.BlinkRate = MultiStateLedBlink.Fast;
                UpdateSystemSafetyLed();
            };

            lightCurtain.Cleared += (s, e) =>
            {
                lblCurtainStatus.Text = "Safety Field: CLEAR (Dual OSSD Outputs Active)";
                lblCurtainStatus.ForeColor = ZeroTheme.Colors.Success;
                interlock.SetInterlockCondition("Optical Curtain Intrusion", false);
                annunciator.TriggerAlarm("Line1.Alarm.LightCurtain", false);
                if (!eStop.IsDepressed)
                {
                    annunciator.TriggerAlarm("Line1.Alarm.Interlock", false);
                }
                ScadaAlarmEngine.ClearAlarm("CURTAIN_01");
                ledCurtain.State = MultiStateLedState.Normal;
                ledCurtain.BlinkRate = MultiStateLedBlink.None;
                UpdateSystemSafetyLed();
            };

            btnBreakBeam.Click += (s, e) => lightCurtain.SimulateObstacle(7, 3);
            btnClearBeam.Click += (s, e) => lightCurtain.ClearObstacle();

            // Annunciator Simulation Buttons
            btnSimHighPress.Click += (s, e) =>
            {
                meterHydraulic.Value = 92.4;
                annunciator.TriggerAlarm("Line1.Alarm.HydraulicPress", true);
                ScadaAlarmEngine.RaiseAlarm("HYDR_HIGH", "Safety.Hydraulic.Pressure", "CRITICAL: Hydraulic Brake Pressure 92.4 bar exceeds 85 bar threshold", ScadaAlarmSeverity.Critical);
                UpdateSystemSafetyLed();
            };

            btnSimPumpTrip.Click += (s, e) =>
            {
                annunciator.TriggerAlarm("Line1.Alarm.PumpTrip", true);
                ScadaAlarmEngine.RaiseAlarm("PUMP_TRIP", "Line1.CoolantPump", "HIGH: Coolant Circulation Pump Overload Trip", ScadaAlarmSeverity.High);
            };

            btnSimLowLevel.Click += (s, e) =>
            {
                annunciator.TriggerAlarm("Line1.Alarm.LowLevel", true);
                ScadaAlarmEngine.RaiseAlarm("LUBE_LOW", "Line1.Lubrication", "WARNING: Lubrication Reservoir Level Low (<20%)", ScadaAlarmSeverity.Medium);
            };

            btnSimClearFaults.Click += (s, e) =>
            {
                annunciator.TriggerAlarm("Line1.Alarm.HydraulicPress", false);
                annunciator.TriggerAlarm("Line1.Alarm.PumpTrip", false);
                annunciator.TriggerAlarm("Line1.Alarm.LowLevel", false);
                meterHydraulic.Value = 65.0;
                ScadaAlarmEngine.ClearAlarm("HYDR_HIGH");
                ScadaAlarmEngine.ClearAlarm("PUMP_TRIP");
                ScadaAlarmEngine.ClearAlarm("LUBE_LOW");
                UpdateSystemSafetyLed();
                ZeroUI.WinForms.Overlays.ZeroToast.Info(this, "Cleared non-safety active fault triggers. Press RESET on panel to clear annunciator tiles.");
            };

            // Lamp Test Sequence
            int lampTestCycle = 0;
            btnLampTest.Click += (s, e) =>
            {
                lampTestCycle = (lampTestCycle + 1) % 4;
                var testState = lampTestCycle == 1 ? MultiStateLedState.Warning
                              : lampTestCycle == 2 ? MultiStateLedState.Alarm
                              : lampTestCycle == 3 ? MultiStateLedState.Maintenance
                              : MultiStateLedState.Normal;
                var blink = lampTestCycle == 2 ? MultiStateLedBlink.Fast : MultiStateLedBlink.None;

                ledMaster.State = testState; ledMaster.BlinkRate = blink;
                ledEStop.State = testState; ledEStop.BlinkRate = blink;
                ledCurtain.State = testState; ledCurtain.BlinkRate = blink;
                ledInterlock.State = testState; ledInterlock.BlinkRate = blink;
            };

            // Hydraulic Pressure Buttons
            btnSpikePress.Click += (s, e) =>
            {
                meterHydraulic.Value = 92.4;
                annunciator.TriggerAlarm("Line1.Alarm.HydraulicPress", true);
                ScadaAlarmEngine.RaiseAlarm("HYDR_HIGH", "Safety.Hydraulic.Pressure", "CRITICAL: Hydraulic Brake Pressure 92.4 bar exceeds threshold", ScadaAlarmSeverity.Critical);
                UpdateSystemSafetyLed();
            };

            btnNormPress.Click += (s, e) =>
            {
                meterHydraulic.Value = 65.0;
                annunciator.TriggerAlarm("Line1.Alarm.HydraulicPress", false);
                ScadaAlarmEngine.ClearAlarm("HYDR_HIGH");
                UpdateSystemSafetyLed();
            };

            // Central Annunciator ACK and RESET integration
            annunciator.AlarmAcknowledged += (s, e) =>
            {
                ScadaAlarmEngine.AcknowledgeAll("Chief Safety Officer");
                alarmBanner.Invalidate();
            };

            annunciator.AlarmReset += (s, e) =>
            {
                if (!eStop.IsDepressed) ScadaAlarmEngine.ClearAlarm("ESTOP_01");
                if (lightCurtain.CurtainState == LightCurtainState.Clear) ScadaAlarmEngine.ClearAlarm("CURTAIN_01");
                if (meterHydraulic.Value < 85.0 && meterHydraulic.Value > 20.0) ScadaAlarmEngine.ClearAlarm("HYDR_HIGH");
                UpdateSystemSafetyLed();
            };

            // Theme Changed Sync
            ZeroTheme.ThemeChanged += (s, e) =>
            {
                if (page.IsDisposed || !page.IsHandleCreated) return;
                mainContainer.BackColor = ZeroTheme.Colors.Background;
                pnlDiv1.BackColor = ZeroTheme.Colors.Border;
                pnlDiv2.BackColor = ZeroTheme.Colors.Border;
                lblPilotHeader.ForeColor = ZeroTheme.Colors.TextSecondary;
                lblEStopHint.ForeColor = ZeroTheme.Colors.TextSecondary;
                lblCurtainHint.ForeColor = ZeroTheme.Colors.TextSecondary;
            };

            // Assemble main container with proper dock ordering
            mainContainer.Controls.Add(split);
            mainContainer.Controls.Add(spacerSplit);
            mainContainer.Controls.Add(alarmBanner);
            mainContainer.Controls.Add(spacerBanner);
            mainContainer.Controls.Add(banner);

            banner.BringToFront();
            spacerBanner.BringToFront();
            alarmBanner.BringToFront();
            spacerSplit.BringToFront();
            split.BringToFront();

            page.Controls.Add(mainContainer);
        }
    }
}
