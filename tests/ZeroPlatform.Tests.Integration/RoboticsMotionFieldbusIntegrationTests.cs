using System;
using System.Threading.Tasks;
using Xunit;
using ZeroBus.Can;
using ZeroBus.CanOpen;
using ZeroMotion.Trajectory;

namespace ZeroPlatform.Tests.Integration
{
    public class RoboticsMotionFieldbusIntegrationTests
    {
        [Fact]
        public async Task EndToEnd_MultiAxisSCurveTrajectory_StreamsThroughMotionBusCoordinator()
        {
            // 1. Setup Virtual Fieldbus with Master and 2 Servo Drives
            var bus = new VirtualCanBus();
            using var masterTransport = new VirtualCanTransport(bus);
            using var drive1Transport = new VirtualCanTransport(bus);
            using var drive2Transport = new VirtualCanTransport(bus);

            await masterTransport.ConnectAsync();
            await drive1Transport.ConnectAsync();
            await drive2Transport.ConnectAsync();

            var sdo = new SdoClient(masterTransport);
            var drive1 = new CiA402Drive(nodeId: 1, masterTransport, sdo);
            var drive2 = new CiA402Drive(nodeId: 2, masterTransport, sdo);

            var sync = new SyncProducer(masterTransport);
            var coordinator = new MotionBusCoordinator(sync);
            coordinator.AddAxis(drive1, maxAllowedFollowingError: 500);
            coordinator.AddAxis(drive2, maxAllowedFollowingError: 500);

            // Simulate drive feedback loop: when drive receives RPDO1 (0x201 / 0x202), it echoes TPDO1 (0x181 / 0x182)
            drive1Transport.OnFrameReceived += f =>
            {
                if (f.Id == 0x201 && f.Data.Length >= 6)
                {
                    int target = BitConverter.ToInt32(f.Data, 2);
                    // Echo simulated actual position with negligible 2-count encoder tracking lag
                    int actual = System.Math.Max(0, target - 2);
                    byte[] tpdo = new byte[6];
                    tpdo[0] = 0x27; tpdo[1] = 0x00; // Operation Enabled
                    Array.Copy(BitConverter.GetBytes(actual), 0, tpdo, 2, 4);
                    _ = drive1Transport.SendFrameAsync(CanFrame.CreateStandard(0x181, tpdo));
                }
            };

            drive2Transport.OnFrameReceived += f =>
            {
                if (f.Id == 0x202 && f.Data.Length >= 6)
                {
                    int target = BitConverter.ToInt32(f.Data, 2);
                    int actual = System.Math.Max(0, target - 1);
                    byte[] tpdo = new byte[6];
                    tpdo[0] = 0x27; tpdo[1] = 0x00;
                    Array.Copy(BitConverter.GetBytes(actual), 0, tpdo, 2, 4);
                    _ = drive2Transport.SendFrameAsync(CanFrame.CreateStandard(0x182, tpdo));
                }
            };

            // 2. Plan 2-Axis Synchronized S-Curve Trajectory (ZeroMotion Tier 3)
            // Axis 1 moves 50,000 counts, Axis 2 moves 20,000 counts
            var start = new double[] { 0, 0 };
            var target = new double[] { 50_000, 20_000 };
            var maxVel = new double[] { 25_000, 25_000 };
            var maxAcc = new double[] { 50_000, 50_000 };
            var maxJerk = new double[] { 200_000, 200_000 };

            var planner = new MultiAxisSCurvePlanner(start, target, maxVel, maxAcc, maxJerk);
            Assert.True(planner.SynchronizedDuration > 0.0);

            // 3. Stream trajectory in 20 cyclic ticks through MotionBusCoordinator (ZeroBus Tier 2)
            int ticks = 20;
            double dt = planner.SynchronizedDuration / ticks;

            for (int step = 1; step <= ticks; step++)
            {
                double t = step * dt;
                var (positions, _, _) = planner.Evaluate(t);

                int[] targetCounts = new int[]
                {
                    (int)System.Math.Round(positions[0]),
                    (int)System.Math.Round(positions[1])
                };

                await coordinator.SyncTickAsync(targetCounts);
                await Task.Delay(5); // allow async transport message delivery
            }

            // 4. Assertions: Both axes completed motion without tripping following error
            Assert.False(coordinator.TrippedFault, "Motion coordinator should not trip during normal tracking.");
            Assert.InRange(drive1.ActualPosition, 49_900, 50_000);
            Assert.InRange(drive2.ActualPosition, 19_900, 20_000);
        }
    }
}
