using System.Threading;
using System.Threading.Tasks;
using Xunit;
using ZeroBus.Can;

namespace ZeroBus.Tests
{
    public class CanTests
    {
        [Fact]
        public void CanFrame_StandardAndExtendedFrameCreation()
        {
            var fStd = CanFrame.CreateStandard(0x123, new byte[] { 0x01, 0x02, 0x03 });
            Assert.Equal((uint)0x123, fStd.Id);
            Assert.False(fStd.IsExtended);
            Assert.Equal(3, fStd.Dlc);

            var fExt = CanFrame.CreateExtended(0x18FF1234, new byte[] { 0xAA, 0xBB });
            Assert.Equal((uint)0x18FF1234, fExt.Id);
            Assert.True(fExt.IsExtended);
            Assert.Equal(2, fExt.Dlc);
        }

        [Fact]
        public void CanFilter_AcceptsMatchingIdsAndRejectsOthers()
        {
            // Accept any frame with ID 0x200..0x207 (mask: 0x7F8, match: 0x200)
            var filter = new CanFilter(mask: 0x7F8, match: 0x200);

            var f1 = CanFrame.CreateStandard(0x201, new byte[] { 1 });
            var f2 = CanFrame.CreateStandard(0x207, new byte[] { 2 });
            var f3 = CanFrame.CreateStandard(0x208, new byte[] { 3 });

            Assert.True(filter.Accepts(f1));
            Assert.True(filter.Accepts(f2));
            Assert.False(filter.Accepts(f3));
        }

        [Fact]
        public async Task VirtualCanBus_TransfersFramesBetweenNodes()
        {
            var bus = new VirtualCanBus();
            using var nodeA = new VirtualCanTransport(bus);
            using var nodeB = new VirtualCanTransport(bus);

            await nodeA.ConnectAsync();
            await nodeB.ConnectAsync();

            CanFrame? receivedByB = null;
            using var sem = new SemaphoreSlim(0, 1);

            nodeB.OnFrameReceived += f =>
            {
                receivedByB = f;
                sem.Release();
            };

            var txFrame = CanFrame.CreateStandard(0x181, new byte[] { 0x11, 0x22, 0x33, 0x44 });
            await nodeA.SendFrameAsync(txFrame);

            bool received = await sem.WaitAsync(1000);
            Assert.True(received, "Node B should receive frame from Node A via VirtualCanBus.");
            Assert.NotNull(receivedByB);
            Assert.Equal(txFrame.Id, receivedByB!.Value.Id);
            Assert.Equal(txFrame.Data, receivedByB!.Value.Data);
        }
    }
}
