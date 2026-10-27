using System;
using Xunit;
using ZeroUI.Core.Petrochem;

namespace ZeroUI.Core.Tests.Petrochem
{
    public class PetrochemTests
    {
        #region Distillation Column Tests

        [Fact]
        public void DistillationEngine_DefaultState_ComputesDifferentialPressureAndHeatBalance()
        {
            var engine = new DistillationEngine();

            Assert.Equal("T-101", engine.ColumnTag);
            Assert.Equal(65.0, engine.DifferentialPressureKpa, 1);
            Assert.Equal(650.0, engine.NetHeatDutyKw, 1);
            Assert.Equal(ColumnOperatingState.Normal, engine.OperatingState);
            Assert.True(engine.FloodMarginPct > 70.0 && engine.FloodMarginPct < 90.0);
        }

        [Fact]
        public void DistillationEngine_HighVaporVelocity_DetectsFloodingRisk()
        {
            var engine = new DistillationEngine
            {
                ActualVaporVelocityMs = 1.95 // Exceeds FloodVelocityMs (1.80)
            };

            Assert.True(engine.FloodMarginPct > 100.0);
            Assert.Equal(ColumnOperatingState.FloodingRisk, engine.OperatingState);
        }

        [Fact]
        public void DistillationEngine_LowVaporVelocity_DetectsWeepingRisk()
        {
            var engine = new DistillationEngine
            {
                ActualVaporVelocityMs = 0.25
            };

            Assert.True(engine.FloodMarginPct < 25.0);
            Assert.Equal(ColumnOperatingState.WeepingRisk, engine.OperatingState);
        }

        [Fact]
        public void DistillationEngine_TrayGradient_MaintainsDecreasingTemperatureProfile()
        {
            var engine = new DistillationEngine();
            Assert.Equal(24, engine.Trays.Count);

            // Bottom tray (Tray 1) should be hot (~BottomsTempC)
            // Top tray (Tray 24) should be cool (~OverheadTempC)
            Assert.True(engine.Trays[0].TemperatureC > engine.Trays[engine.Trays.Count - 1].TemperatureC);
            Assert.True(engine.Trays[0].PressureKpa > engine.Trays[engine.Trays.Count - 1].PressureKpa);

            // Monotonic decrease from bottom to top
            for (int i = 0; i < engine.Trays.Count - 1; i++)
            {
                Assert.True(engine.Trays[i].TemperatureC >= engine.Trays[i + 1].TemperatureC);
                Assert.True(engine.Trays[i].PressureKpa >= engine.Trays[i + 1].PressureKpa);
            }
        }

        [Fact]
        public void DistillationEngine_ZeroDutyAndFeed_EntersShutdown()
        {
            var engine = new DistillationEngine
            {
                ReboilerDutyKw = 0.0,
                FeedFlowRateKgH = 0.0
            };

            Assert.Equal(ColumnOperatingState.Shutdown, engine.OperatingState);
        }

        #endregion

        #region ESD Safety Instrumented System Tests

        [Fact]
        public void EsdEngine_HealthyState_AllEffectsEnergizedAndSafe()
        {
            var engine = new EsdEngine();

            Assert.Equal(0, engine.ActiveTripCount);
            Assert.Equal(0, engine.ActiveBypassCount);
            Assert.True(engine.SystemSafeState);

            for (int i = 0; i < engine.Effects.Count; i++)
            {
                Assert.Equal(EffectStatus.Energized, engine.Effects[i].Status);
            }
        }

        [Fact]
        public void EsdEngine_ManualTrip_ActivatesInterlockAndDeEnergizesEffects()
        {
            var engine = new EsdEngine();

            // Cause 7 is Manual Emergency Shutdown Button (ESD-PB-01)
            engine.TriggerTrip(7);

            Assert.Equal(1, engine.ActiveTripCount);
            Assert.False(engine.SystemSafeState);

            // Manual PB trips all final safety elements
            for (int i = 0; i < engine.Effects.Count; i++)
            {
                Assert.Equal(EffectStatus.DeEnergized, engine.Effects[i].Status);
            }

            // Reset trips
            engine.ResetAllTrips();
            Assert.Equal(0, engine.ActiveTripCount);
            Assert.True(engine.SystemSafeState);
            for (int i = 0; i < engine.Effects.Count; i++)
            {
                Assert.Equal(EffectStatus.Energized, engine.Effects[i].Status);
            }
        }

        [Fact]
        public void EsdEngine_BypassedCause_PreventsTripAndIncrementsBypassCount()
        {
            var engine = new EsdEngine();

            // Cause 3 is Reboiler Tube Temp (TSHH-204)
            engine.SetCauseBypass(3, true, "J. Doe - Shift Supv");
            Assert.Equal(1, engine.ActiveBypassCount);

            // Trigger trip on cause 3
            engine.TriggerTrip(3);

            // Because cause 3 is bypassed, it must NOT register as an active trip
            Assert.Equal(0, engine.ActiveTripCount);
            Assert.True(engine.SystemSafeState);

            // Interlocked effect XV-103 should remain Energized
            Assert.Equal(EffectStatus.Energized, engine.Effects[2].Status);
        }

        [Fact]
        public void EsdEngine_TwoOutOfThreeVoting_TripsOnlyWhenMajorityExceeded()
        {
            var engine = new EsdEngine();

            // PSHH-101A, B, C are causes 0, 1, 2 in voting group "2oo3-P101"
            // Trip 1 transmitter out of 3:
            engine.TriggerTrip(0);
            Assert.Equal(EffectStatus.Energized, engine.Effects[0].Status); // Feed inlet XV-101 remains Energized
            Assert.Equal(EffectStatus.Energized, engine.Effects[1].Status); // BDV-102 remains Energized

            // Trip 2nd transmitter (majority reached: 2oo3)
            engine.TriggerTrip(1);
            Assert.Equal(EffectStatus.DeEnergized, engine.Effects[0].Status); // XV-101 shuts down
            Assert.Equal(EffectStatus.DeEnergized, engine.Effects[1].Status); // BDV-102 trips open
        }

        #endregion

        #region Pipeline PIG Tracking Tests

        [Fact]
        public void PipelinePigEngine_AdvanceTime_IncrementsDistanceAndTriggersMarkers()
        {
            var engine = new PipelinePigEngine();
            // Default distance = 28.4 km. Marker 2 (River Crossing) is at 38.2 km (HasPassed == false).
            Assert.False(engine.Markers[2].HasPassed);

            // Advance by 4500 seconds at 2.45 m/s = +11.025 km -> new distance = 39.425 km
            engine.AdvanceTime(4500.0);

            Assert.True(engine.CurrentDistanceKm > 38.2);
            Assert.True(engine.Markers[2].HasPassed);
            Assert.NotNull(engine.Markers[2].PassageTimestamp);
            Assert.True(engine.ProgressPct > 45.0);
        }

        [Fact]
        public void PipelinePigEngine_LowVelocityHighDeltaP_TriggersStalledAlert()
        {
            var engine = new PipelinePigEngine
            {
                PigVelocityMs = 0.04,
                DifferentialPressureBar = 5.2
            };

            engine.AdvanceTime(1.0);

            Assert.Equal(PigRunStatus.StalledAlert, engine.Status);
        }

        [Fact]
        public void PipelinePigEngine_ArrivalAtReceiver_SetsStatusInReceiver()
        {
            var engine = new PipelinePigEngine
            {
                CurrentDistanceKm = 84.8,
                TotalLengthKm = 85.0,
                PigVelocityMs = 2.0
            };

            // Advance past 85.0 km
            engine.AdvanceTime(200.0);

            Assert.Equal(85.0, engine.CurrentDistanceKm);
            Assert.Equal(PigRunStatus.InReceiver, engine.Status);
            Assert.Equal(0.0, engine.PigVelocityMs);
            Assert.Equal(TimeSpan.Zero, engine.EstimatedTimeToReceiver);
        }

        #endregion
    }
}
