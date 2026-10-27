using System;
using Xunit;
using ZeroUI.Core.Energy;

namespace ZeroUI.Core.Tests.Energy
{
    public class EnergyTests
    {
        [Fact]
        public void SldEngine_VoltageColoring_ResolvesStandardColors()
        {
            Assert.Equal("#EF4444", SldEngine.GetVoltageColorHex(VoltageLevel.UHV_500kV));
            Assert.Equal("#F97316", SldEngine.GetVoltageColorHex(VoltageLevel.HV_110kV));
            Assert.Equal("#22C55E", SldEngine.GetVoltageColorHex(VoltageLevel.MV_22kV));
            Assert.Equal("#38BDF8", SldEngine.GetVoltageColorHex(VoltageLevel.LV_400V));
        }

        [Fact]
        public void SldEngine_ApparentPowerAndCurrent_ComputesAccurately()
        {
            // P = 40 MW, Q = 30 MVAr -> S = sqrt(1600 + 900) = 50 MVA
            double s = SldEngine.CalculateApparentPowerMva(40.0, 30.0);
            Assert.Equal(50.0, s, 2);

            // S = 50 MVA at 110 kV -> I = (50 * 1000) / (sqrt(3) * 110) = 50000 / 190.525 = 262.43 Amps
            double i = SldEngine.CalculateThreePhaseCurrentAmps(s, 110.0);
            Assert.Equal(262.43, i, 1);
        }

        [Fact]
        public void SwitchgearEngine_ContactWearAndClosePermit_ValidatesInterlocks()
        {
            // 250,000 kA interrupted out of 500,000 kA max -> 50% wear
            double wear = SwitchgearEngine.CalculateEstimatedContactWearPct(250000.0, 500000.0);
            Assert.Equal(50.0, wear, 2);

            var mech = new BreakerMechanism
            {
                IsSpringCharged = true,
                LotoState = LotoLockState.Unlocked,
                VacuumBottleContactWearPct = 40.0
            };

            Assert.True(SwitchgearEngine.ValidateClosePermit(mech, out _));

            // Test LOTO lockout interlock
            mech.LotoState = LotoLockState.LockedOut;
            Assert.False(SwitchgearEngine.ValidateClosePermit(mech, out string reasonLoto));
            Assert.Contains("LOTO", reasonLoto);

            // Test uncharged spring
            mech.LotoState = LotoLockState.Unlocked;
            mech.IsSpringCharged = false;
            Assert.False(SwitchgearEngine.ValidateClosePermit(mech, out string reasonSpring));
            Assert.Contains("Spring", reasonSpring);
        }

        [Fact]
        public void BessEngine_ThermalRunawayRisk_DetectsPrecursorAlarm()
        {
            var module = new BessModule(1, "MOD-1", 16, 3.25);
            Assert.Equal(ThermalRunawayRisk.Normal, BessEngine.EvaluateRunawayRisk(module));

            // Trigger precursor condition: Rate of temp rise >= 1.0°C/min AND DeltaV >= 50mV
            module.RateOfTemperatureRiseCpm = 1.2;
            module.CellVoltages[0] = 3.20;
            module.CellVoltages[15] = 3.26; // 60mV delta
            Assert.True(module.DeltaCellMv >= 50.0);

            var risk = BessEngine.EvaluateRunawayRisk(module);
            Assert.Equal(ThermalRunawayRisk.PrecursorAlarm, risk);
        }

        [Fact]
        public void SolarPvEngine_TemperatureDerating_ComputesPowerLoss()
        {
            // 10 kW module at 45°C cell temp (deltaT = 20°C above 25°C)
            // gamma = -0.38% / °C -> loss = -7.6% -> derated = 9.24 kW
            double pDerated = SolarPvEngine.CalculateTemperatureDeratedPower(10.0, 45.0, -0.38);
            Assert.Equal(9.24, pDerated, 2);
        }

        [Fact]
        public void SolarPvEngine_StringMismatch_DetectsShadingAndFuses()
        {
            var engine = new SolarPvEngine();
            engine.Plant.Strings.Clear();

            var normalString = new PvString { StringId = 1, CurrentA = 11.5 };
            var shadedString = new PvString { StringId = 2, CurrentA = 7.0 }; // < 75% of 11.5A
            var blownString = new PvString { StringId = 3, CurrentA = 0.2 };  // < 1.0A

            engine.Plant.Strings.Add(normalString);
            engine.Plant.Strings.Add(shadedString);
            engine.Plant.Strings.Add(blownString);

            engine.EvaluateStringMismatch(expectedCurrentA: 11.5);

            Assert.Equal(PvStringStatus.Normal, normalString.Status);
            Assert.Equal(PvStringStatus.Shaded, shadedString.Status);
            Assert.Equal(PvStringStatus.BlownFuse, blownString.Status);
        }
    }
}
