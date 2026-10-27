using System;
using Xunit;
using ZeroUI.Core.Logistics;

namespace ZeroUI.Core.Tests.Logistics
{
    public class LogisticsTests
    {
        [Fact]
        public void AsrsEngine_EstimateMoveDuration_ComputesKinematicTime()
        {
            var config = new AsrsAisleConfiguration
            {
                BayWidthMm = 1500, // 1.5m per bay
                TierHeightMm = 1500, // 1.5m per tier
                MaxTravelSpeedMps = 3.0, // 3 m/s
                MaxHoistSpeedMps = 1.5 // 1.5 m/s
            };

            // Move 10 bays (15m -> 5s) and 4 tiers (6m -> 4s)
            // Kinematic Max(5s, 4s) = 5s + 7s fork cycle = 12s
            double duration = AsrsEngine.EstimateMoveDurationSeconds(1, 1, 11, 5, config, forkCycleSec: 7.0);
            Assert.Equal(12.0, duration, 2);
        }

        [Fact]
        public void AsrsEngine_CoordinateValidation_ValidatesBoundaries()
        {
            var config = new AsrsAisleConfiguration { TotalBays = 20, TotalTiers = 8 };

            Assert.True(AsrsEngine.IsCoordinateValid(1, 1, config));
            Assert.True(AsrsEngine.IsCoordinateValid(20, 8, config));
            Assert.False(AsrsEngine.IsCoordinateValid(0, 5, config));
            Assert.False(AsrsEngine.IsCoordinateValid(21, 5, config));
            Assert.False(AsrsEngine.IsCoordinateValid(10, 9, config));
        }

        [Fact]
        public void AsrsPayload_OverweightDetection_FlagsExcessWeight()
        {
            var payload = new AsrsPayloadState { WeightKg = 1200 };
            Assert.False(payload.IsOverweight(1500));

            payload.WeightKg = 1650;
            Assert.True(payload.IsOverweight(1500));
        }

        [Fact]
        public void AsrsEngine_GetNormalizedPosition_CalculatesRatio()
        {
            var engine = new AsrsEngine();
            engine.Config.TotalBays = 11;
            engine.Config.TotalTiers = 6;
            engine.Position.CurrentBay = 6; // Halfway (bay 1 to 11 -> 5 / 10 = 0.5)
            engine.Position.CurrentTier = 6; // Top (tier 1 to 6 -> 5 / 5 = 1.0)

            engine.GetNormalizedPosition(out double xRatio, out double yRatio);
            Assert.Equal(0.5, xRatio, 2);
            Assert.Equal(1.0, yRatio, 2);
        }

        [Fact]
        public void AgvFleetEngine_CalculateDistance_ComputesEuclidean()
        {
            double dist = AgvFleetEngine.CalculateDistance(0, 0, 30, 40);
            Assert.Equal(50.0, dist, 2);
        }

        [Fact]
        public void AgvFleetEngine_DetectProximityWarnings_IdentifiesCloseRobots()
        {
            var engine = new AgvFleetEngine();
            engine.Vehicles.Clear();

            var v1 = new AgvVehicle { Id = 1, Name = "V1", X = 100, Y = 100 };
            var v2 = new AgvVehicle { Id = 2, Name = "V2", X = 120, Y = 100 }; // 20m away
            var v3 = new AgvVehicle { Id = 3, Name = "V3", X = 300, Y = 300 }; // Far away

            engine.Vehicles.Add(v1);
            engine.Vehicles.Add(v2);
            engine.Vehicles.Add(v3);

            // Warning radius = 30m -> V1 & V2 should trigger warning
            var warnings = engine.DetectProximityWarnings(warningRadius: 30.0);
            Assert.Single(warnings);
            Assert.Equal(20.0, warnings[0].Distance, 2);
        }

        [Fact]
        public void AgvFleetEngine_AverageBatterySoc_RollsUpAverage()
        {
            var engine = new AgvFleetEngine();
            engine.Vehicles.Clear();
            engine.Vehicles.Add(new AgvVehicle { BatterySocPct = 80 });
            engine.Vehicles.Add(new AgvVehicle { BatterySocPct = 60 });

            Assert.Equal(70.0, engine.AverageBatterySocPct, 2);
        }

        [Fact]
        public void ConveyorMergeEngine_AdvancesParcelsAndTriggersDivert()
        {
            var engine = new ConveyorMergeEngine();
            engine.Parcels.Clear();
            engine.Chutes.Clear();

            var chute = new DivertChute { ChuteId = 1, PositionRatio = 0.5 };
            engine.Chutes.Add(chute);

            var parcel = new ParcelItem("PKG-1", 0.48, 1);
            engine.Parcels.Add(parcel);

            // Advance by 1 second (Speed 90 m/min = 1.5 m/s, LineLength = 30m -> deltaRatio = 1.5/30 = 0.05)
            engine.AdvanceParcels(deltaSeconds: 1.0);

            // Position moves from 0.48 to ~0.53, intersecting chute at 0.50 -> Diverted
            Assert.True(parcel.IsDiverted);
            Assert.Equal(1, chute.DivertedCount);
            Assert.True(chute.IsDiverting);
        }

        [Fact]
        public void ConveyorMergeEngine_PhotoEyeOcclusionDetection()
        {
            var engine = new ConveyorMergeEngine();
            engine.Parcels.Clear();
            engine.Sensors.Clear();

            var pe = new PhotoEyeSensor { SensorId = 1, PositionRatio = 0.30 };
            engine.Sensors.Add(pe);

            var parcel = new ParcelItem("PKG-1", 0.30, 1);
            engine.Parcels.Add(parcel);

            engine.AdvanceParcels(deltaSeconds: 0.01);
            Assert.True(pe.IsBlocked);
        }

        [Fact]
        public void ConveyorMergeEngine_CalculatePph_CalculatesHourlyRate()
        {
            // 100 parcels in 60 seconds -> 6000 PPH
            double pph = ConveyorMergeEngine.CalculatePph(100, 60.0);
            Assert.Equal(6000.0, pph, 2);
        }
    }
}
