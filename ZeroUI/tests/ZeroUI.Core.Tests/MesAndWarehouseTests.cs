using System;
using System.Collections.Generic;
using Xunit;
using ZeroUI.Core.Mes;
using ZeroUI.Core.Runtime;
using ZeroUI.Core.Warehouse;

namespace ZeroUI.Core.Tests
{
    public class MesAndWarehouseTests
    {
        [Fact]
        public void PackMlStateMachine_StandardProductionCycle_TransitionsCorrectly()
        {
            var sm = new PackMlStateMachine("CNC_01", PackMlState.Stopped);
            Assert.Equal(PackMlState.Stopped, sm.CurrentState);

            // Reset -> Resetting -> Complete -> Idle
            Assert.True(sm.CanExecuteCommand(PackMlCommand.Reset));
            sm.ExecuteCommand(PackMlCommand.Reset);
            Assert.Equal(PackMlState.Resetting, sm.CurrentState);

            sm.ExecuteCommand(PackMlCommand.Complete);
            Assert.Equal(PackMlState.Idle, sm.CurrentState);

            // Start -> Starting -> Complete -> Execute
            Assert.True(sm.CanExecuteCommand(PackMlCommand.Start));
            sm.ExecuteCommand(PackMlCommand.Start);
            Assert.Equal(PackMlState.Starting, sm.CurrentState);

            sm.ExecuteCommand(PackMlCommand.Complete);
            Assert.Equal(PackMlState.Execute, sm.CurrentState);

            // Execute -> Hold -> Holding -> Complete -> Held
            Assert.True(sm.CanExecuteCommand(PackMlCommand.Hold));
            sm.ExecuteCommand(PackMlCommand.Hold);
            Assert.Equal(PackMlState.Holding, sm.CurrentState);

            sm.ExecuteCommand(PackMlCommand.Complete);
            Assert.Equal(PackMlState.Held, sm.CurrentState);

            // Held -> Unhold -> Unholding -> Complete -> Execute
            Assert.True(sm.CanExecuteCommand(PackMlCommand.Unhold));
            sm.ExecuteCommand(PackMlCommand.Unhold);
            Assert.Equal(PackMlState.Unholding, sm.CurrentState);

            sm.ExecuteCommand(PackMlCommand.Complete);
            Assert.Equal(PackMlState.Execute, sm.CurrentState);

            // Abort override from Execute
            Assert.True(sm.CanExecuteCommand(PackMlCommand.Abort));
            sm.ExecuteCommand(PackMlCommand.Abort);
            Assert.Equal(PackMlState.Aborting, sm.CurrentState);

            sm.ExecuteCommand(PackMlCommand.Complete);
            Assert.Equal(PackMlState.Aborted, sm.CurrentState);

            // Verify StateStore synchronization
            Assert.Equal("Aborted", StateStore.Default.GetState<string>("Machine.CNC_01.State"));
        }

        [Fact]
        public void OeeEngine_MathematicalCalculation_ComputesExactValues()
        {
            // Scenario:
            // Planned time: 480 minutes (8 hours) = 28,800 seconds
            // Downtime: 48 minutes = 2,880 seconds
            // Operating time: 432 minutes = 25,920 seconds
            // Availability: 432 / 480 = 0.90 (90%)
            //
            // Ideal cycle time: 10 seconds / piece
            // Total units: 2,400 pieces
            // Expected operating time: 2,400 * 10 = 24,000 seconds
            // Performance: 24,000 / 25,920 = 0.925925 (92.59%)
            //
            // Good units: 2,280 pieces
            // Defect units: 120 pieces
            // Quality: 2,280 / 2,400 = 0.95 (95%)
            //
            // OEE = 0.90 * 0.925925 * 0.95 = 0.791666 (79.17%)

            var oeeEngine = new OeeEngine("PRESS_01", idealCycleTimeSeconds: 10.0, TimeSpan.FromHours(8));

            oeeEngine.RecordDowntime(DowntimeCategory.MachineBreakdown, "Hydraulic seal leak", TimeSpan.FromMinutes(48));
            oeeEngine.RecordProduction(goodCount: 2280, defectCount: 120);

            var snapshot = oeeEngine.CalculateSnapshot();

            Assert.Equal(0.90, snapshot.Availability, precision: 4);
            Assert.Equal(0.9259, snapshot.Performance, precision: 4);
            Assert.Equal(0.95, snapshot.Quality, precision: 4);
            Assert.Equal(0.7917, snapshot.OverallOee, precision: 4);

            Assert.Equal(2400, snapshot.TotalUnits);
            Assert.Equal(2280, snapshot.GoodUnits);
            Assert.Equal(120, snapshot.DefectUnits);
        }

        [Fact]
        public void WarehouseLocation_ParsingAndManhattanDistance_CalculatesAccurately()
        {
            var locA = WarehouseLocation.Parse("WH1-ZN1-A01-R02-S01-B05");
            Assert.Equal("WH1", locA.WarehouseId);
            Assert.Equal("ZN1", locA.ZoneId);
            Assert.Equal(1, locA.Aisle);
            Assert.Equal(2, locA.Rack);
            Assert.Equal(1, locA.Shelf);
            Assert.Equal(5, locA.Bin);

            var locB = WarehouseLocation.Parse("WH1-ZN1-A04-R06-S03-B01");

            // locA: x = 1*3=3, y = 2*1.5=3, z = 1*1.2=1.2
            // locB: x = 4*3=12, y = 6*1.5=9, z = 3*1.2=3.6
            // dx = |3 - 12| = 9
            // dy = |3 - 9| = 6
            // dz = |1.2 - 3.6| = 2.4
            // dist = 9 + 6 + 2.4 = 17.4
            double dist = locA.CalculateManhattanDistance(locB);
            Assert.Equal(17.4, dist, precision: 2);

            // Capacity check
            locA.MaxCapacityKg = 500;
            locA.CurrentWeightKg = 400;
            Assert.True(locA.CanAccommodate(50));
            Assert.False(locA.CanAccommodate(150));
        }

        [Fact]
        public void GuidedPickingEngine_FefoStrategy_PrioritizesEarliestExpiry()
        {
            var loc1 = WarehouseLocation.Parse("WH1-ZN1-A01-R01-S01-B01");
            var loc2 = WarehouseLocation.Parse("WH1-ZN1-A01-R02-S01-B01");
            var loc3 = WarehouseLocation.Parse("WH1-ZN1-A01-R03-S01-B01");

            var now = DateTime.UtcNow;

            var items = new List<PickTaskItem>
            {
                new PickTaskItem("T1", "SKU_MILK", "LOT_C", loc1, 10, now, now.AddDays(30)),
                new PickTaskItem("T2", "SKU_MILK", "LOT_A", loc2, 10, now, now.AddDays(5)),  // Earliest expiry
                new PickTaskItem("T3", "SKU_MILK", "LOT_B", loc3, 10, now, now.AddDays(15))
            };

            var optimized = GuidedPickingEngine.OptimizePickSequence(items, strategy: PickingStrategy.Fefo);

            Assert.Equal(3, optimized.Count);
            Assert.Equal("LOT_A", optimized[0].LotNumber);
            Assert.Equal("LOT_B", optimized[1].LotNumber);
            Assert.Equal("LOT_C", optimized[2].LotNumber);

            // Barcode verification test
            Assert.True(GuidedPickingEngine.VerifyScan(optimized[0], "SKU_MILK", out _));
            Assert.True(GuidedPickingEngine.VerifyScan(optimized[0], "LOT_A", out _));
            Assert.True(GuidedPickingEngine.VerifyScan(optimized[0], "SKU_MILK|LOT_A", out _));
            Assert.False(GuidedPickingEngine.VerifyScan(optimized[0], "SKU_WRONG", out string? err));
            Assert.NotNull(err);

            // Confirm pick
            Assert.True(GuidedPickingEngine.ConfirmPick(optimized[0], 10, out _));
            Assert.True(optimized[0].IsCompleted);
        }
    }
}
