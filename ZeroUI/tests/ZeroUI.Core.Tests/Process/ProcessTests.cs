using System;
using Xunit;
using ZeroUI.Core.Process;

namespace ZeroUI.Core.Tests.Process
{
    public class ProcessTests
    {
        #region SFC Batch Execution Tests

        [Fact]
        public void SfcEngine_AdvanceTime_CompletesStepAndSatisfiesTransition()
        {
            var engine = new SfcRecipeExecutionEngine();
            // Default active step is index 1 (MEDIA: 60s allocated, 22s elapsed)
            Assert.Equal(1, engine.ActiveStepIndex);
            var activeStep = engine.CurrentStep;
            Assert.NotNull(activeStep);
            Assert.Equal("MEDIA", activeStep!.StepTag);

            // Advance time by 40s (total 62s -> exceeds 60s)
            engine.AdvanceTime(40.0);

            // Step 2 should be completed, and engine should advance to step 3 (INOC)
            Assert.Equal(SfcStepState.Completed, engine.Steps[1].State);
            Assert.True(engine.Transitions[1].IsSatisfied);
            Assert.Equal(2, engine.ActiveStepIndex);
            Assert.Equal("INOC", engine.CurrentStep?.StepTag);
            Assert.Equal(SfcStepState.Active, engine.CurrentStep?.State);
        }

        [Fact]
        public void SfcEngine_RequiresSignOff_HoldsBatchUntilSigned()
        {
            var engine = new SfcRecipeExecutionEngine();
            // Force active step to step 3 (INOC: requires sign-off, 90s allocated)
            engine.AdvanceTime(50.0); // finishes step 2
            Assert.Equal(2, engine.ActiveStepIndex);
            Assert.True(engine.CurrentStep?.RequiresSignOff);

            // Advance past allocated duration without sign-off
            engine.AdvanceTime(95.0);

            // Engine must enter Holding state waiting for electronic signature
            Assert.Equal(S88BatchState.Holding, engine.State);
            Assert.Equal(SfcStepState.Held, engine.CurrentStep?.State);
            Assert.NotNull(engine.HoldReason);

            // Attempt further advance while held -> must not progress
            engine.AdvanceTime(30.0);
            Assert.Equal(S88BatchState.Holding, engine.State);

            // Provide 21 CFR Part 11 sign-off
            bool signed = engine.SignOffStep("Dr. Sarah Chen, QA Lead");
            Assert.True(signed);
            Assert.Equal(S88BatchState.Running, engine.State);
            Assert.Equal("Dr. Sarah Chen, QA Lead", engine.Steps[2].SignedOffBy);
            Assert.NotNull(engine.Steps[2].SignOffTimestamp);
        }

        [Fact]
        public void SfcEngine_AbortBatch_TransitionsToAbortedState()
        {
            var engine = new SfcRecipeExecutionEngine();
            engine.AbortBatch("Vessel pressure burst disk relief alarm");

            Assert.Equal(S88BatchState.Aborted, engine.State);
            Assert.Equal("Vessel pressure burst disk relief alarm", engine.HoldReason);
        }

        #endregion

        #region Bioreactor Tests

        [Fact]
        public void BioreactorEngine_MassTransferKLa_CalculatesExpectedRate()
        {
            var engine = new BioreactorEngine();
            engine.AgitationRpm = 450.0;
            engine.SpargingAirLpm = 20.0;
            engine.SpargingO2Lpm = 5.0;

            double kla = engine.EstimatedKLaHr;
            // Expected ~40 to 80 hr^-1 for typical pilot single-use bioreactor
            Assert.True(kla > 30.0 && kla < 100.0, $"kLa was {kla}, expected in range 30-100 hr^-1");
        }

        [Fact]
        public void BioreactorEngine_AdvanceSimulation_RotatesImpellerKinematics()
        {
            var engine = new BioreactorEngine();
            engine.AgitationRpm = 600.0; // 10 revolutions per second = 3600 deg/sec
            engine.ImpellerAngleDeg = 0.0;

            // 0.1 seconds -> 360 degrees = full revolution (modulo 360 = 0)
            engine.AdvanceSimulation(0.1);
            Assert.True(engine.ImpellerAngleDeg >= 0.0 && engine.ImpellerAngleDeg < 360.0);

            // 0.025 seconds -> 90 degrees
            engine.ImpellerAngleDeg = 0.0;
            engine.AdvanceSimulation(0.025);
            Assert.Equal(90.0, engine.ImpellerAngleDeg, 1);
        }

        [Fact]
        public void BioreactorEngine_AlarmStatus_DetectsCriticalOutOfSpec()
        {
            var engine = new BioreactorEngine();
            Assert.Equal(BioreactorAlarmStatus.Normal, engine.AlarmStatus);

            // Temperature excursion: 37°C -> 39°C (+2.0°C)
            engine.VesselTempC = 39.0;
            Assert.Equal(BioreactorAlarmStatus.OutOfSpecAlarm, engine.AlarmStatus);
            engine.VesselTempC = 37.0;

            // Critical hypoxia DO drop: 45% -> 15%
            engine.DissolvedOxygenPct = 15.0;
            Assert.Equal(BioreactorAlarmStatus.OutOfSpecAlarm, engine.AlarmStatus);
            engine.DissolvedOxygenPct = 45.0;

            // Minor warning: Foam detected
            engine.FoamDetected = true;
            Assert.Equal(BioreactorAlarmStatus.Warning, engine.AlarmStatus);
        }

        #endregion

        #region CIP / SIP Validation Tests

        [Fact]
        public void CipValidationEngine_CalculateLethalityRate_ComputesExponentialRate()
        {
            // At reference temperature 121.1°C, L = 1.0 min/min
            double rateRef = CipValidationEngine.CalculateLethalityRate(121.1);
            Assert.Equal(1.0, rateRef, 3);

            // At 131.1°C (+10°C / one z-value), L = 10.0 min/min
            double rate131 = CipValidationEngine.CalculateLethalityRate(131.1);
            Assert.Equal(10.0, rate131, 2);

            // At 111.1°C (-10°C / one z-value), L = 0.1 min/min
            double rate111 = CipValidationEngine.CalculateLethalityRate(111.1);
            Assert.Equal(0.1, rate111, 2);

            // Below 100°C, lethality is zero
            Assert.Equal(0.0, CipValidationEngine.CalculateLethalityRate(95.0));
        }

        [Fact]
        public void CipValidationEngine_AdvanceTime_AccumulatesF0DuringSipSterilization()
        {
            var engine = new CipValidationEngine();
            engine.CurrentPhase = CipCyclePhase.SipSterilization;
            engine.Tact.ReturnTempC = 121.1; // Rate = 1.0 min/min
            engine.AccumulatedF0Minutes = 0.0;

            // 15 minutes = 900 seconds hold at 121.1°C
            engine.AdvanceTime(900.0);

            Assert.Equal(15.0, engine.AccumulatedF0Minutes, 1);
            Assert.True(engine.IsSterilizationValidated);
        }

        [Fact]
        public void CipValidationEngine_TactCriteria_ValidatesAllParameters()
        {
            var engine = new CipValidationEngine();
            engine.CurrentPhase = CipCyclePhase.CausticWash;
            engine.Tact.TargetDurationSec = 600.0;
            engine.Tact.ElapsedSec = 650.0;
            engine.Tact.FlowVelocityMS = 1.8; // > 1.5
            engine.Tact.ConductivityMsCm = 45.0; // > 42.0
            engine.Tact.ReturnTempC = 82.0; // > 80.0

            Assert.True(engine.Tact.AreAllTactCriteriaMet);

            // Drop temperature
            engine.Tact.ReturnTempC = 75.0;
            Assert.False(engine.Tact.IsTemperatureValid);
            Assert.False(engine.Tact.AreAllTactCriteriaMet);
        }

        #endregion

        #region Cleanroom Tests

        [Fact]
        public void CleanroomEngine_ValidateCascadeGradient_DetectsPressureReversal()
        {
            var engine = new CleanroomEngine();
            // Default cascade: PAL(15) < GOWN(30) < PREP(45) < CORE(60) -> strictly increasing
            bool valid = engine.ValidateCascadeGradient(out string? breach);
            Assert.True(valid);
            Assert.Null(breach);

            // Simulate door open breach: Gowning room pressure collapses from 30 Pa to 10 Pa
            engine.Zones[1].DifferentialPressurePa = 10.0;

            bool breachDetected = engine.ValidateCascadeGradient(out string? breachReason);
            Assert.False(breachDetected);
            Assert.NotNull(breachReason);
            Assert.Contains("GOWN-102", breachReason);
        }

        [Fact]
        public void CleanroomZone_EvaluatesIsoParticulateLimits()
        {
            var iso5Zone = new CleanroomZone
            {
                ZoneTag = "CORE-01",
                IsoClass = IsoCleanroomClass.IsoClass5,
                ParticleCount05Um = 1500.0, // Limit 3,520
                ParticleCount50Um = 10.0    // Limit 29
            };
            Assert.True(iso5Zone.AreParticlesOk);

            // Exceed ISO 5 limit
            iso5Zone.ParticleCount05Um = 4000.0;
            Assert.False(iso5Zone.AreParticlesOk);
            Assert.False(iso5Zone.IsCompliant);
        }

        #endregion
    }
}
