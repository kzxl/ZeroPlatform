using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using ZeroPipeline.Core.Execution;
using ZeroPipeline.Core.Nodes;
using ZeroPipeline.Core.Ports;

namespace ZeroPipeline.Tests
{
    public class PortBackpressureTests
    {
        private class DummyNode : PipelineNode
        {
            public DummyNode(string name) : base(name) { }
            protected override Task OnExecuteAsync(PipelineContext context, CancellationToken cancellationToken) => Task.CompletedTask;
        }

        [Fact]
        public void DropOldest_DiscardsOldestWhenFull()
        {
            var node = new DummyNode("Node1");
            var input = new InputPort<int>(node, "In", capacity: 2, policy: BackpressurePolicy.DropOldest);

            input.Deliver(new DataPacket<int>(10, 1));
            input.Deliver(new DataPacket<int>(20, 2));
            input.Deliver(new DataPacket<int>(30, 3)); // 10 should be dropped

            Assert.Equal(2, input.Count);
            Assert.True(input.TryReceive(out var p1));
            Assert.Equal(20, p1.Payload);
            Assert.True(input.TryReceive(out var p2));
            Assert.Equal(30, p2.Payload);
            Assert.False(input.TryReceive(out _));
        }

        [Fact]
        public void DropNewest_DiscardsIncomingWhenFull()
        {
            var node = new DummyNode("Node1");
            var input = new InputPort<int>(node, "In", capacity: 2, policy: BackpressurePolicy.DropNewest);

            input.Deliver(new DataPacket<int>(10, 1));
            input.Deliver(new DataPacket<int>(20, 2));
            input.Deliver(new DataPacket<int>(30, 3)); // 30 should be dropped

            Assert.Equal(2, input.Count);
            Assert.True(input.TryReceive(out var p1));
            Assert.Equal(10, p1.Payload);
            Assert.True(input.TryReceive(out var p2));
            Assert.Equal(20, p2.Payload);
            Assert.False(input.TryReceive(out _));
        }

        [Fact]
        public void ThrowException_ThrowsWhenFull()
        {
            var node = new DummyNode("Node1");
            var input = new InputPort<int>(node, "In", capacity: 2, policy: BackpressurePolicy.ThrowException);

            input.Deliver(new DataPacket<int>(10, 1));
            input.Deliver(new DataPacket<int>(20, 2));

            Assert.Throws<InvalidOperationException>(() =>
            {
                input.Deliver(new DataPacket<int>(30, 3));
            });
        }

        [Fact]
        public void Block_BlocksProducerUntilConsumerReads()
        {
            var node = new DummyNode("Node1");
            var input = new InputPort<int>(node, "In", capacity: 2, policy: BackpressurePolicy.Block);

            input.Deliver(new DataPacket<int>(10, 1));
            input.Deliver(new DataPacket<int>(20, 2));

            var producerFinished = false;
            var producerThread = new Thread(() =>
            {
                input.Deliver(new DataPacket<int>(30, 3));
                producerFinished = true;
            });

            producerThread.Start();
            Thread.Sleep(50); // Give thread time to attempt delivery and block

            // Should be blocked because capacity is 2
            Assert.False(producerFinished);
            Assert.Equal(2, input.Count);

            // Now consume one item
            Assert.True(input.TryReceive(out var p1));
            Assert.Equal(10, p1.Payload);

            // Wait for producer to unblock and finish
            producerThread.Join(500);
            Assert.True(producerFinished);
            Assert.Equal(2, input.Count);

            Assert.True(input.TryReceive(out var p2));
            Assert.Equal(20, p2.Payload);
            Assert.True(input.TryReceive(out var p3));
            Assert.Equal(30, p3.Payload);
        }

        [Fact]
        public void TryReceiveWait_ReturnsDataWhenArrives()
        {
            var node = new DummyNode("Node1");
            var input = new InputPort<string>(node, "In", capacity: 5);

            Task.Run(async () =>
            {
                await Task.Delay(30);
                input.Deliver(new DataPacket<string>("hello", 1));
            });

            var received = input.TryReceiveWait(500, out var packet);
            Assert.True(received);
            Assert.Equal("hello", packet.Payload);
        }
    }
}
