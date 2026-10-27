using System;
using Xunit;
using ZeroUI.Core.Water;

namespace ZeroUI.Core.Tests.Water
{
    public class WaterTests
    {
        #region Clarifier Tests

        [Fact]
        public void ClarifierEngine_SurfaceOverflowRate_CalculatesExpectedValue()
        {
            var engine = new ClarifierEngine();
            engine.Hydraulics.DiameterM = 28.0; // Area = pi * 14^2 ≈ 615.75 m2
            engine.Hydraulics.InfluentFlowM3H = 850.0; // Daily = 850 * 24 = 20,400 m3/day

            double sor = engine.SurfaceOverflowRateM3M2Day;
            // Expected: 20400 / 615.752 ≈ 33.13 m3/(m2*d)
            Assert.True(sor > 32.5 && sor < 33.5, $"SOR was {sor}, expected ~33.13");
        }

        [Fact]
        public void ClarifierEngine_HydraulicRetentionTime_CalculatesExpectedHours()
        {
            var engine = new ClarifierEngine();
            engine.Hydraulics.DiameterM = 28.0;
            engine.Hydraulics.SideWaterDepthM = 4.0;
            engine.Hydraulics.InfluentFlowM3H = 850.0;

            double hrt = engine.HydraulicRetentionTimeHours;
            // Area ≈ 615.75 m2, Volume = 615.75 * 4 = 2463.0 m3
            // HRT = 2463.0 / 850 ≈ 2.898 hours
            Assert.True(hrt > 2.8 && hrt < 3.0, $"HRT was {hrt}, expected ~2.90 hrs");
        }

        [Fact]
        public void ClarifierEngine_SolidsLoadingRate_IncludesRasAndMlss()
        {
            var engine = new ClarifierEngine();
            engine.Hydraulics.DiameterM = 28.0;
            engine.Hydraulics.InfluentFlowM3H = 800.0;
            engine.Hydraulics.ReturnActivatedSludgeFlowM3H = 200.0;
            engine.Blanket.TssMgL = 3000.0; // 3.0 kg/m3

            // Total flow = 1000 m3/h = 24,000 m3/d
            // Total solids = 24,000 * 3.0 = 72,000 kg/d
            // Area = pi * 14^2 ≈ 615.75 m2
            // SLR = 72,000 / 615.75 ≈ 116.93 kg/(m2*d)
            double slr = engine.SolidsLoadingRateKgM2Day;
            Assert.True(slr > 115.0 && slr < 118.0, $"SLR was {slr}, expected ~116.93");
        }

        [Fact]
        public void ClarifierEngine_AdvanceRotation_AdvancesAngleAndStopsOnTrip()
        {
            var engine = new ClarifierEngine();
            engine.Scraper.IsRunning = true;
            engine.Scraper.RotationSpeedRpm = 0.05; // 0.05 RPM = 0.3 deg/sec
            engine.Scraper.CurrentAngleDeg = 0.0;
            engine.Scraper.TorquePct = 40.0;

            engine.AdvanceRotation(10.0); // 10s * 0.3 deg/s = 3.0 deg
            Assert.Equal(3.0, engine.Scraper.CurrentAngleDeg, 2);

            // Simulate OverTorque Trip (> 85%)
            engine.Scraper.TorquePct = 90.0;
            Assert.Equal(ScraperTorqueStatus.OverTorqueTrip, engine.Scraper.TorqueStatus);

            // Further rotation must freeze
            engine.AdvanceRotation(10.0);
            Assert.Equal(3.0, engine.Scraper.CurrentAngleDeg, 2);
        }

        [Fact]
        public void ClarifierEngine_IsOperatingNormally_EvaluatesLimits()
        {
            var engine = new ClarifierEngine();
            Assert.True(engine.IsOperatingNormally);

            // Blanket too high
            engine.Blanket.BlanketDepthM = 3.0; // max is 2.2
            Assert.False(engine.IsOperatingNormally);
            engine.Blanket.BlanketDepthM = 1.0;

            // Effluent turbidity too high
            engine.Blanket.EffluentTurbidityNtu = 8.0; // max safe is 5.0
            Assert.False(engine.IsOperatingNormally);
        }

        #endregion

        #region Chemical Dosing Tests

        [Fact]
        public void ChemicalDosingEngine_CalculateRequiredDosingRate_MatchesStoichiometry()
        {
            var engine = new ChemicalDosingEngine();
            engine.WaterFlowM3H = 1000.0; // 1,000 m3/h
            engine.TargetDoseMgL = 3.0; // 3.0 mg/L -> 3,000 g/h neat chemical
            engine.Tank.SolutionConcentrationPct = 12.0; // 12% active
            engine.Tank.SpecificGravity = 1.25; // 1.25 kg/L

            // Grams active per liter = 0.12 * 1.25 * 1000 = 150 g/L
            // Required L/h = 3,000 / 150 = 20.0 L/h
            double requiredLh = engine.CalculateRequiredDosingRateLh();
            Assert.Equal(20.0, requiredLh, 2);
        }

        [Fact]
        public void ChemicalDosingEngine_AutoFailover_PromotesStandbyWhenDutyTrips()
        {
            var engine = new ChemicalDosingEngine();
            engine.PumpA.Mode = DosingPumpMode.Duty;
            engine.PumpA.IsRunning = true;
            engine.PumpA.DiaphragmIntact = true;

            engine.PumpB.Mode = DosingPumpMode.Standby;
            engine.PumpB.IsRunning = false;
            engine.PumpB.DiaphragmIntact = true;

            // Trigger diaphragm rupture on Pump A
            engine.PumpA.DiaphragmIntact = false;

            bool switched = engine.CheckAndApplyAutoFailover();
            Assert.True(switched);
            Assert.Equal(DosingPumpMode.Fault, engine.PumpA.Mode);
            Assert.False(engine.PumpA.IsRunning);

            Assert.Equal(DosingPumpMode.Duty, engine.PumpB.Mode);
            Assert.True(engine.PumpB.IsRunning);
        }

        [Fact]
        public void ChemicalDosingEngine_AdvanceConsumption_DeductsTankVolume()
        {
            var engine = new ChemicalDosingEngine();
            engine.Tank.CapacityL = 2000.0;
            engine.Tank.CurrentLevelL = 1000.0;
            engine.PumpA.IsRunning = true;
            engine.PumpA.MaxCapacityLh = 100.0;
            engine.PumpA.StrokeRateSpm = 180.0;
            engine.PumpA.StrokeLengthPct = 100.0; // Delivers 100 L/h

            // 1 hour = 3600 seconds -> 100 L used -> level should be 900 L
            engine.AdvanceConsumption(3600.0);
            Assert.Equal(900.0, engine.Tank.CurrentLevelL, 2);

            double autonomy = engine.EstimatedRunHoursRemaining;
            // 900 L / 100 L/h = 9.0 hours
            Assert.Equal(9.0, autonomy, 2);
        }

        [Fact]
        public void ChemicalTank_LowLevelAlarm_TriggersBelowThreshold()
        {
            var tank = new ChemicalTank
            {
                CapacityL = 2000.0,
                CurrentLevelL = 400.0, // 20%
                LowLevelThresholdPct = 15.0
            };
            Assert.False(tank.IsLowLevel);

            tank.CurrentLevelL = 200.0; // 10%
            Assert.True(tank.IsLowLevel);
        }

        #endregion

        #region Hydraulic Gradient Tests

        [Fact]
        public void HydraulicGradientEngine_CalculateProfile_ComputesFrictionAndPumpBoost()
        {
            var engine = new HydraulicGradientEngine();
            engine.StartingHeadM = 60.0;

            var profile = engine.CalculateProfile();
            Assert.True(profile.Count >= 2);

            // Initial point at station 0 must start at StartingHeadM
            Assert.Equal(0, profile[0].StationM);
            Assert.Equal(60.0, profile[0].HglElevationM, 2);

            // Second station at 800m has booster pump (+38m)
            var st1 = profile[1];
            Assert.Equal(800, st1.StationM);
            // With friction loss (e.g. ~1-2m) and +38m pump, Hgl should be ~96m
            Assert.True(st1.HglElevationM > 90.0 && st1.HglElevationM < 100.0);

            // EGL must always be greater than or equal to HGL (EGL = HGL + v^2/2g)
            for (int i = 0; i < profile.Count; i++)
            {
                Assert.True(profile[i].EglElevationM >= profile[i].HglElevationM);
            }
        }

        [Fact]
        public void HydraulicGradientEngine_HasNegativePressureAnomaly_DetectsCavitationRisk()
        {
            var engine = new HydraulicGradientEngine();
            // Default profile has booster pump, so pressure is positive
            bool defaultCav = engine.HasNegativePressureAnomaly(out _, out double minHeadDefault);
            Assert.False(defaultCav);
            Assert.True(minHeadDefault > 0.0);

            // Now lower starting head dramatically and remove pump boost to force negative pressure
            engine.StartingHeadM = 30.0;
            engine.Stations[1].PumpHeadBoostM = 0.0; // Remove pump

            bool detectedCav = engine.HasNegativePressureAnomaly(out double worstSt, out double minHead);
            Assert.True(detectedCav, "Should detect negative pressure when head is below pipe ridge");
            Assert.True(minHead < 0.0);
            // Ridge is at 1600m
            Assert.Equal(1600, worstSt);
        }

        #endregion
    }
}
