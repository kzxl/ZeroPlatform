using System;
using Xunit;
using ZeroUI.Core.Signal;
using ZeroUI.Core.Memory;
using ZeroUI.Core.Automation;

namespace ZeroUI.Core.Tests
{
    public class BreakthroughControlsTests
    {
        [Fact]
        public void SignalRingBuffer_Write_Read_And_Trigger_Work_Correctly()
        {
            var buffer = new SignalRingBuffer(16);

            // Write 20 samples to verify wrap-around
            for (int i = 0; i < 20; i++)
            {
                buffer.Write(i);
            }

            Assert.Equal(16, buffer.Count);
            // Oldest sample should be 4, newest sample should be 19
            Assert.Equal(4, buffer[0]);
            Assert.Equal(19, buffer[15]);

            Span<float> readSpan = stackalloc float[4];
            int readCount = buffer.ReadLatest(readSpan);
            Assert.Equal(4, readCount);
            Assert.Equal(16, readSpan[0]);
            Assert.Equal(17, readSpan[1]);
            Assert.Equal(18, readSpan[2]);
            Assert.Equal(19, readSpan[3]);

            buffer.ComputeMetrics(out float min, out float max, out float p2p, out float rms);
            Assert.Equal(4, min);
            Assert.Equal(19, max);
            Assert.Equal(15, p2p);
            Assert.True(rms > 0);

            // Find rising edge trigger crossing 10
            int trigIdx = buffer.FindTriggerIndex(10.0f, risingEdge: true, maxSearchCount: 16);
            Assert.True(trigIdx >= 0);
            Assert.True(buffer[trigIdx] > 10.0f);
            Assert.True(buffer[trigIdx - 1] <= 10.0f);
        }

        [Fact]
        public void HexViewEngine_ModbusDissection_And_Crc_Work_Correctly()
        {
            // Sample Modbus TCP frame:
            // Transaction ID: 0x00, 0x01
            // Protocol ID:    0x00, 0x00
            // Length:         0x00, 0x06
            // Unit ID:        0x01
            // Function Code:  0x03 (Read Holding Registers)
            // Address:        0x00, 0x6B
            // Quantity:       0x00, 0x03
            byte[] frame = new byte[] { 0x00, 0x01, 0x00, 0x00, 0x00, 0x06, 0x01, 0x03, 0x00, 0x6B, 0x00, 0x03 };

            var engine = new HexViewEngine(frame, bytesPerRow: 16);
            Assert.Equal(12, engine.TotalBytes);
            Assert.Equal(1, engine.TotalRows);

            engine.Dissector.DissectModbusTcp(frame);
            Assert.True(engine.Dissector.Segments.Count >= 5);

            var segTx = engine.Dissector.FindSegment(0);
            Assert.NotNull(segTx);
            Assert.Equal("Transaction ID", segTx!.Title);

            var segFunc = engine.Dissector.FindSegment(7);
            Assert.NotNull(segFunc);
            Assert.Equal("Function Code", segFunc!.Title);

            // CRC calculation on RTU portion
            byte[] rtuPayload = new byte[] { 0x01, 0x03, 0x00, 0x6B, 0x00, 0x03 };
            ushort crc = ProtocolDissector.CalculateCrc16Modbus(rtuPayload);
            Assert.True(crc != 0);

            // Inspection check
            engine.InspectOffset(7, isLittleEndian: false,
                out byte u8, out short s16, out ushort u16, out int s32, out uint u32,
                out float f32, out double f64, out string bitStr);

            Assert.Equal(0x03, u8);
            Assert.Equal("00000011", bitStr);
        }

        [Fact]
        public void StateMachineEngine_Transitions_And_Pulses_Advance_Correctly()
        {
            var sm = new StateMachineEngine();

            var nodeIdle = new MachineStateNode("idle", "Idle", 50, 50, 0xFF3B82F6, duration: 2.0);
            var nodeRun = new MachineStateNode("run", "Running", 200, 50, 0xFF10B981, duration: 3.0);
            sm.Nodes.Add(nodeIdle);
            sm.Nodes.Add(nodeRun);

            var t1 = new StateTransitionEdge("t1", "idle", "run", "Start Command");
            sm.Transitions.Add(t1);

            sm.SetInitialState("idle");
            Assert.Equal("idle", sm.ActiveStateId);
            Assert.Equal(MachineStateStatus.Active, nodeIdle.Status);

            // Trigger transition to "run"
            bool ok = sm.TriggerTransition("run");
            Assert.True(ok);
            Assert.Single(sm.ActivePulses);

            // Update delta to complete pulse (speed 1.5 -> 1.0s completes it)
            sm.Update(1.0);

            Assert.Empty(sm.ActivePulses);
            Assert.Equal("run", sm.ActiveStateId);
            Assert.Equal(MachineStateStatus.Completed, nodeIdle.Status);
            Assert.Equal(MachineStateStatus.Active, nodeRun.Status);
        }
    }
}
