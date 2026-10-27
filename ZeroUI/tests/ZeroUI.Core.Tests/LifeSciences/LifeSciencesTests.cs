using System;
using System.Collections.Generic;
using Xunit;
using ZeroUI.Core.LifeSciences;

namespace ZeroUI.Core.Tests.LifeSciences
{
    public class LifeSciencesTests
    {
        [Fact]
        public void MicroplateEngine_96Well_InitializesCorrectGridDimensions()
        {
            var engine = new MicroplateEngine(PlateFormat.Wells96);
            Assert.Equal(8, engine.RowCount);
            Assert.Equal(12, engine.ColCount);
            Assert.Equal("A01", engine.Wells[0, 0].WellId);
            Assert.Equal("H12", engine.Wells[7, 11].WellId);
            Assert.True(engine.MaxValue > engine.MinValue);
        }

        [Fact]
        public void MicroplateEngine_CalculatesStatisticsAccurately()
        {
            var engine = new MicroplateEngine(PlateFormat.Wells96);
            bool success = engine.CalculateStatistics(SampleType.Blank, out double mean, out double sd, out double cvPct);

            Assert.True(success);
            Assert.True(mean > 0.0);
            Assert.True(sd >= 0.0);
            Assert.True(cvPct >= 0.0);
        }

        [Fact]
        public void CentrifugeEngine_CalculatesRcfAndRpmAccurately()
        {
            double radiusMm = 180.0;
            double rpm = 10000.0;

            // RCF = 1.118 * 10^-5 * 180 * (10000)^2 = 1.118e-5 * 180 * 100,000,000 = 20,124 g
            double rcf = CentrifugeEngine.CalculateRcf(rpm, radiusMm);
            Assert.InRange(rcf, 20100.0, 20150.0);

            // Invert back
            double calcRpm = CentrifugeEngine.CalculateRpmFromRcf(rcf, radiusMm);
            Assert.InRange(calcRpm, 9999.0, 10001.0);
        }

        [Fact]
        public void CentrifugeEngine_DetectsImbalanceAndVibrationAlarm()
        {
            var engine = new CentrifugeEngine();
            Assert.True(engine.IsRotorBalanced(1.5));

            // Force opposing imbalance
            engine.Buckets[0].ActualWeightGrams = 430.0;
            engine.Buckets[2].ActualWeightGrams = 425.0; // delta = 5.0g > 1.5g
            Assert.False(engine.IsRotorBalanced(1.5));

            // Vibration threshold
            bool alarm = CentrifugeEngine.EvaluateVibrationAlarm(3.2, out bool isTrip);
            Assert.True(alarm);
            Assert.False(isTrip);

            alarm = CentrifugeEngine.EvaluateVibrationAlarm(5.5, out isTrip);
            Assert.True(alarm);
            Assert.True(isTrip);
        }

        [Fact]
        public void ColdChainEngine_CalculatesMeanKineticTemperatureAccurately()
        {
            // If all temperatures are constant at -80.0°C, MKT must equal -80.0°C
            var constTemps = new List<double> { -80.0, -80.0, -80.0, -80.0 };
            double mkt = ColdChainEngine.CalculateMeanKineticTemperature(constTemps);
            Assert.InRange(mkt, -80.01, -79.99);

            // Variable temperatures
            var varTemps = new List<double> { -82.0, -80.0, -78.0, -81.0 };
            double mktVar = ColdChainEngine.CalculateMeanKineticTemperature(varTemps);
            Assert.InRange(mktVar, -81.0, -79.0);
        }

        [Fact]
        public void ColdChainEngine_EvaluatesExcursionProperly()
        {
            var engine = new ColdChainEngine();
            engine.Telemetry.CurrentTempC = -81.4;
            engine.Telemetry.HighAlarmLimitC = -70.0;
            engine.Telemetry.LowAlarmLimitC = -88.0;
            Assert.False(engine.IsInExcursion);

            // Warm breach
            engine.Telemetry.CurrentTempC = -65.0;
            Assert.True(engine.IsInExcursion);
        }
    }
}
