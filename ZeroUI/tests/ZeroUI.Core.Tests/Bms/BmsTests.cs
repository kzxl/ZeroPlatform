using System;
using Xunit;
using ZeroUI.Core.Bms;

namespace ZeroUI.Core.Tests.Bms
{
    public class BmsTests
    {
        [Fact]
        public void AhuEngine_CalculateMixedAirTemp_CalculatesProportionalMix()
        {
            // Outside = 30°C, Return = 20°C, 25% OA -> 30*0.25 + 20*0.75 = 7.5 + 15 = 22.5°C
            double mixed = AhuEngine.CalculateMixedAirTemp(30.0, 20.0, 25.0);
            Assert.Equal(22.5, mixed, 2);

            // 0% OA -> 20°C
            Assert.Equal(20.0, AhuEngine.CalculateMixedAirTemp(30.0, 20.0, 0.0), 2);

            // 100% OA -> 30°C
            Assert.Equal(30.0, AhuEngine.CalculateMixedAirTemp(30.0, 20.0, 100.0), 2);
        }

        [Fact]
        public void AhuEngine_SensibleThermalDuty_CalculatesAccurately()
        {
            // 10,000 CFM, Delta T = 20°F -> 1.08 * 10000 * 20 = 216,000 BTU/hr
            double duty = AhuEngine.CalculateSensibleThermalDutyBtu(10000.0, 20.0);
            Assert.Equal(216000.0, duty, 2);
        }

        [Fact]
        public void AhuEngine_EconomizerFeasibility_ValidatesConditions()
        {
            // OA = 16°C, RA = 24°C -> Feasible (16 < 22)
            Assert.True(AhuEngine.EvaluateEconomizerFeasible(16.0, 24.0));

            // OA = 25°C, RA = 24°C -> Not feasible (hotter than return)
            Assert.False(AhuEngine.EvaluateEconomizerFeasible(25.0, 24.0));

            // OA = 5°C (freezing risk below 10°C) -> Not feasible
            Assert.False(AhuEngine.EvaluateEconomizerFeasible(5.0, 24.0, 10.0));
        }

        [Fact]
        public void AhuFilterState_FlagsDirtyWhenExceedingDeltaPLimit()
        {
            var filter = new AhuFilterState
            {
                PreFilterDeltaP = 0.3,
                FinalFilterDeltaP = 0.6,
                MaxAllowedDeltaP = 1.0
            };
            Assert.False(filter.IsDirty);
            Assert.Equal(AhuComponentStatus.Normal, filter.Status);

            filter.FinalFilterDeltaP = 1.2;
            Assert.True(filter.IsDirty);
            Assert.Equal(AhuComponentStatus.Warning, filter.Status);
        }

        [Fact]
        public void ChillerPlantEngine_CoolingLoadAndEfficiency_ComputesCorrectly()
        {
            // 1200 GPM, Delta T = 5.556°C (10°F)
            // Tons = (1200 * 10) / 24 = 500 Tons
            double deltaTC = 10.0 / 1.8;
            double tons = ChillerPlantEngine.CalculateCoolingLoadTons(1200.0, deltaTC);
            Assert.Equal(500.0, tons, 1);

            // Power = 300 kW, Load = 500 Tons -> 0.60 kW/Ton
            double kwPerTon = ChillerPlantEngine.CalculateEfficiencyKwPerTon(300.0, tons);
            Assert.Equal(0.60, kwPerTon, 2);

            // COP = 3.51685 / 0.60 = 5.86
            double cop = ChillerPlantEngine.CalculateCop(kwPerTon);
            Assert.Equal(5.86, cop, 1);
        }

        [Fact]
        public void ChillerPlantEngine_TowerApproach_CalculatesDifference()
        {
            // Leaving water = 29.5°C, Ambient wet-bulb = 24.0°C -> Approach = 5.5°C
            double approach = ChillerPlantEngine.CalculateTowerApproach(29.5, 24.0);
            Assert.Equal(5.5, approach, 2);
        }

        [Fact]
        public void ChillerPlantEngine_PlantAggregates_SummarizesTotalCapacityAndPower()
        {
            var engine = new ChillerPlantEngine();
            Assert.Equal(1000.0, engine.TotalRatedTons);
            Assert.True(engine.TotalPowerKw > 0);
            Assert.True(engine.TotalActualTons > 0);
            Assert.True(engine.PlantAverageCop > 0);
        }

        [Fact]
        public void ZoneSchedulerEngine_ResolvesEffectiveModeAndSetpoints()
        {
            var schedule = ZoneSchedulerEngine.CreateDefaultCommercialSchedule("Z-01", "Office");

            // Wednesday 10:00 AM -> Occupied (Cooling 23.0°C, Heating 20.5°C)
            var wednesdayWorkTime = new DateTime(2026, 9, 9, 10, 0, 0); // Sep 9, 2026 is Wednesday
            Assert.Equal(DayOfWeek.Wednesday, wednesdayWorkTime.DayOfWeek);

            var mode = ZoneSchedulerEngine.GetEffectiveMode(schedule, wednesdayWorkTime);
            Assert.Equal(ZoneOccupancyMode.Occupied, mode);

            ZoneSchedulerEngine.GetActiveSetpoints(schedule, wednesdayWorkTime, out double cool, out double heat);
            Assert.Equal(23.0, cool);
            Assert.Equal(20.5, heat);

            // Wednesday 19:30 PM -> Standby
            var wednesdayEvening = new DateTime(2026, 9, 9, 19, 30, 0);
            Assert.Equal(ZoneOccupancyMode.Standby, ZoneSchedulerEngine.GetEffectiveMode(schedule, wednesdayEvening));

            // Sunday 14:00 PM -> Unoccupied default
            var sundayAfternoon = new DateTime(2026, 9, 13, 14, 0, 0);
            Assert.Equal(ZoneOccupancyMode.Unoccupied, ZoneSchedulerEngine.GetEffectiveMode(schedule, sundayAfternoon));
        }

        [Fact]
        public void ZoneSchedulerEngine_HolidayOverridesRegularSchedule()
        {
            var schedule = ZoneSchedulerEngine.CreateDefaultCommercialSchedule("Z-01", "Office");
            var holidayDate = new DateTime(2026, 9, 9); // Wednesday
            schedule.Holidays.Add(holidayDate.Date);

            var checkTime = new DateTime(2026, 9, 9, 10, 0, 0);
            Assert.Equal(ZoneOccupancyMode.HolidayOverride, ZoneSchedulerEngine.GetEffectiveMode(schedule, checkTime));
        }

        [Fact]
        public void ZoneSchedulerEngine_ValidateDeadband_DetectsTightMargins()
        {
            // 23°C - 20°C = 3°C >= 2°C -> Valid
            Assert.True(ZoneSchedulerEngine.ValidateDeadband(23.0, 20.0, 2.0));

            // 21°C - 20°C = 1°C < 2°C -> Invalid
            Assert.False(ZoneSchedulerEngine.ValidateDeadband(21.0, 20.0, 2.0));
        }

        [Fact]
        public void ZoneSchedulerEngine_DetectTimeBlockOverlaps_DetectsCollisions()
        {
            var schedule = new ZoneSchedule("Z-01", "Test");
            schedule.Blocks.Add(new ScheduleTimeBlock
            {
                Day = DayOfWeek.Monday,
                StartTime = new TimeSpan(8, 0, 0),
                EndTime = new TimeSpan(12, 0, 0)
            });
            schedule.Blocks.Add(new ScheduleTimeBlock
            {
                Day = DayOfWeek.Monday,
                StartTime = new TimeSpan(13, 0, 0),
                EndTime = new TimeSpan(17, 0, 0)
            });

            Assert.False(ZoneSchedulerEngine.DetectTimeBlockOverlaps(schedule));

            // Add overlapping block on Monday 11:00 - 14:00
            schedule.Blocks.Add(new ScheduleTimeBlock
            {
                Day = DayOfWeek.Monday,
                StartTime = new TimeSpan(11, 0, 0),
                EndTime = new TimeSpan(14, 0, 0)
            });

            Assert.True(ZoneSchedulerEngine.DetectTimeBlockOverlaps(schedule));
        }
    }
}
