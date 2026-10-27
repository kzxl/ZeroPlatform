using System;
using Xunit;
using ZeroUI.Core.Network;

namespace ZeroUI.Core.Tests
{
    public class NetworkSuiteTests
    {
        [Fact]
        public void RackLayoutEngine_AddsValidItems_CalculatesPowerAndWeight()
        {
            var engine = new RackLayoutEngine { TotalUnits = 42, PduBreakerCapacityWatts = 10000, MaxWeightCapacityKg = 1000 };
            engine.Clear();

            engine.AddItem(new RackSlotItem
            {
                StartUnit = 1,
                UnitHeight = 2,
                Name = "Server 1",
                PowerDrawWatts = 300,
                WeightKg = 20
            });

            engine.AddItem(new RackSlotItem
            {
                StartUnit = 5,
                UnitHeight = 4,
                Name = "Storage 1",
                PowerDrawWatts = 500,
                WeightKg = 40
            });

            Assert.Equal(2, engine.Items.Count);
            Assert.Equal(800, engine.CalculateTotalPowerDrawWatts());
            Assert.Equal(60, engine.CalculateTotalWeightKg());
            Assert.Equal(8.0, engine.CalculatePowerUtilizationPercent());
            Assert.Equal(6.0, engine.CalculateWeightUtilizationPercent());
        }

        [Fact]
        public void RackLayoutEngine_RejectsOverlappingSlots()
        {
            var engine = new RackLayoutEngine { TotalUnits = 42 };
            engine.Clear();

            engine.AddItem(new RackSlotItem { StartUnit = 10, UnitHeight = 3, Name = "Switch 1" });

            // Unit 10..12 occupied; trying to place item at unit 11..12
            bool canPlace = engine.CanPlaceItem(11, 2, out string reason);
            Assert.False(canPlace);
            Assert.Contains("occupied", reason, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void RackLayoutEngine_RejectsOutOfBounds()
        {
            var engine = new RackLayoutEngine { TotalUnits = 24 };
            engine.Clear();

            bool canPlaceExceed = engine.CanPlaceItem(23, 3, out _); // 23..25 exceeds 24
            bool canPlaceZero = engine.CanPlaceItem(0, 1, out _);

            Assert.False(canPlaceExceed);
            Assert.False(canPlaceZero);
        }

        [Fact]
        public void RackLayoutEngine_ThermalGradient_RisesTowardTop()
        {
            var engine = new RackLayoutEngine { TotalUnits = 42 };
            engine.Clear();

            double bottomTemp = engine.CalculateThermalAtUnit(1, ambientTempCelsius: 20.0);
            double topTemp = engine.CalculateThermalAtUnit(42, ambientTempCelsius: 20.0);

            Assert.True(topTemp > bottomTemp, "Thermal gradient should rise toward top of rack due to convection.");
        }

        [Fact]
        public void SwitchPortLayout_InitializesDefaultPorts_AndMapsGridCoordinates()
        {
            var layout = new SwitchPortLayout(24, 4);

            Assert.Equal(28, layout.Ports.Count);
            Assert.NotNull(layout.FindPort(1));
            Assert.NotNull(layout.FindPort(24));
            Assert.NotNull(layout.FindPort(25)); // SFP+ 1

            // Port 1 (odd) -> row 0, col 0
            SwitchPortLayout.GetPortGridCoordinates(1, 24, out int r1, out int c1, out bool sfp1);
            Assert.Equal(0, r1);
            Assert.Equal(0, c1);
            Assert.False(sfp1);

            // Port 2 (even) -> row 1, col 0
            SwitchPortLayout.GetPortGridCoordinates(2, 24, out int r2, out int c2, out bool sfp2);
            Assert.Equal(1, r2);
            Assert.Equal(0, c2);
            Assert.False(sfp2);

            // Port 25 (SFP 1) -> row 0, col 0, isSfp true
            SwitchPortLayout.GetPortGridCoordinates(25, 24, out int rSfp, out int cSfp, out bool isSfp);
            Assert.Equal(0, rSfp);
            Assert.Equal(0, cSfp);
            Assert.True(isSfp);
        }

        [Fact]
        public void SwitchPortLayout_CountsActiveLinks_AndPoeBudget()
        {
            var layout = new SwitchPortLayout(8, 2);
            layout.InitializeDefaultPorts();

            var p1 = layout.FindPort(1)!;
            p1.PoeWatts = 15.4;
            var p2 = layout.FindPort(2)!;
            p2.PoeWatts = 30.0;

            Assert.True(layout.CalculateTotalPoeWatts() >= 45.4);
            Assert.True(layout.CountActiveLinks() > 0);
        }

        [Fact]
        public void TopologyGraphEngine_NodeLinkManagement_AndPulseInterpolation()
        {
            var graph = new TopologyGraphEngine();
            var n1 = new TopologyNode { Id = "N1", X = 0, Y = 0, Width = 100, Height = 50 };
            var n2 = new TopologyNode { Id = "N2", X = 200, Y = 100, Width = 100, Height = 50 };
            var link = new TopologyLink { Id = "L1", SourceNodeId = "N1", TargetNodeId = "N2" };

            graph.AddNode(n1);
            graph.AddNode(n2);
            graph.AddLink(link);

            Assert.Equal(2, graph.Nodes.Count);
            Assert.Single(graph.Links);

            // Hit test
            Assert.Equal(n1, graph.HitTestNode(50, 25));
            Assert.Null(graph.HitTestNode(500, 500));

            // Pulse at phase 0.5 should be midpoint between centers
            // n1 center = (50, 25), n2 center = (250, 125)
            // midpoint = (150, 75)
            bool ok = graph.ComputePulseCoordinate(link, 0.5f, out float px, out float py);
            Assert.True(ok);
            Assert.Equal(150f, px, 1);
            Assert.Equal(75f, py, 1);
        }

        [Fact]
        public void TopologyGraphEngine_LayoutSolvers_DoNotCrash()
        {
            var graph = new TopologyGraphEngine();
            for (int i = 0; i < 6; i++)
            {
                graph.AddNode(new TopologyNode { Id = $"Node_{i}", DeviceType = (NetworkDeviceType)(i % 5) });
            }

            graph.ApplyHierarchicalLayout(800, 600);
            graph.CalculateBounds(out float minX, out float minY, out float maxX, out float maxY);
            Assert.True(maxX > minX);

            graph.ApplyCircularLayout(400, 300, 200);
            graph.CalculateBounds(out minX, out minY, out maxX, out maxY);
            Assert.True(maxX > minX);
        }

        [Fact]
        public void ChassisHealthProfile_DualPsuAndOverallHealthEvaluation()
        {
            var chassis = new ChassisHealthProfile();
            Assert.True(chassis.IsPowerRedundant);
            Assert.Equal(ChassisOverallHealth.Healthy, chassis.EvaluateOverallHealth());

            // Simulate loss of PSU 2 power
            chassis.Psu2.IsPowered = false;
            Assert.False(chassis.IsPowerRedundant);
            Assert.Equal(ChassisOverallHealth.Degraded, chassis.EvaluateOverallHealth());

            // Simulate PSU 1 fault -> Critical
            chassis.Psu1.Status = PsuHealthStatus.Fault;
            Assert.Equal(ChassisOverallHealth.Critical, chassis.EvaluateOverallHealth());
        }

        [Fact]
        public void IpSubnetEngine_GridMappingAndHostEntries()
        {
            var engine = new IpSubnetEngine("10.0.10");
            Assert.Equal(256, engine.TotalHosts);

            // Host octet 0 -> row 0, col 0
            IpSubnetEngine.OctetToGrid(0, out int r0, out int c0);
            Assert.Equal(0, r0);
            Assert.Equal(0, c0);
            Assert.Equal(0, IpSubnetEngine.GridToOctet(r0, c0));

            // Host octet 27 -> 27 / 16 = 1, 27 % 16 = 11
            IpSubnetEngine.OctetToGrid(27, out int r27, out int c27);
            Assert.Equal(1, r27);
            Assert.Equal(11, c27);
            Assert.Equal(27, IpSubnetEngine.GridToOctet(r27, c27));

            // Ping sample history
            var host = engine.GetHost(10);
            host.AddPingSample(1.5f);
            host.AddPingSample(2.0f);
            Assert.Equal(2, host.PingHistory.Count);
            Assert.Equal(2.0f, host.PingRttMs);
        }

        [Fact]
        public void FieldbusNetwork_CableBreakLocalization()
        {
            var net = new FieldbusNetwork();
            net.PopulateDemoIndustrialLine();

            Assert.Equal(6, net.Stations.Count);
            Assert.Null(net.LocateCableBreak()); // All normal

            // Simulate broken line between Station 3 and Station 4
            // Station 1, 2, 3 are Normal; Station 4, 5, 6 are CommunicationLost
            net.Stations[3].Status = StationStatus.CommunicationLost;
            net.Stations[4].Status = StationStatus.CommunicationLost;
            net.Stations[5].Status = StationStatus.CommunicationLost;

            var breakSeg = net.LocateCableBreak();
            Assert.NotNull(breakSeg);
            Assert.Equal(net.Stations[2].StationIndex, breakSeg!.FromStationIndex);
            Assert.Equal(net.Stations[3].StationIndex, breakSeg!.ToStationIndex);
            Assert.Equal(SegmentStatus.SeveredBreak, breakSeg.Status);
        }
    }
}
