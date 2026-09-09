using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using ZeroPipeline.Core.Execution;
using ZeroPipeline.Core.Graph;
using ZeroPipeline.Core.Nodes;
using ZeroPipeline.Core.Ports;

namespace ZeroPipeline.Tests
{
    public class PipelineGraphTests
    {
        private class TestSource : SourceNode<int>
        {
            public TestSource(string name) : base(name) { }
            protected override Task OnExecuteAsync(PipelineContext context, CancellationToken cancellationToken)
            {
                Output.Emit(42);
                return Task.CompletedTask;
            }
        }

        private class TestTransform : TransformNode<int, int>
        {
            public TestTransform(string name) : base(name) { }
            protected override Task<int> ProcessAsync(int input, PipelineContext context, CancellationToken cancellationToken)
            {
                return Task.FromResult(input * 2);
            }
        }

        private class TestSink : SinkNode<int>
        {
            public int LastReceived { get; private set; }
            public TestSink(string name) : base(name) { }
            protected override Task ConsumeAsync(int input, PipelineContext context, CancellationToken cancellationToken)
            {
                LastReceived = input;
                return Task.CompletedTask;
            }
        }

        [Fact]
        public void LinearTopologicalOrder_ResolvesDependenciesInOrder()
        {
            var graph = new PipelineGraph();
            var src = new TestSource("Source");
            var trans = new TestTransform("Transform");
            var sink = new TestSink("Sink");

            graph.Connect(src.Output, trans.Input);
            graph.Connect(trans.Output, sink.Input);

            var order = graph.GetTopologicalOrder();

            Assert.Equal(3, order.Count);
            Assert.Same(src, order[0]);
            Assert.Same(trans, order[1]);
            Assert.Same(sink, order[2]);
        }

        [Fact]
        public void DiamondTopologicalOrder_ResolvesDependenciesProperly()
        {
            // Topology:
            //       -> B (Transform) -
            //      /                  \
            // A (Source)               -> D (Sink with 2 inputs or 2 connections)
            //      \                  /
            //       -> C (Transform) -
            var graph = new PipelineGraph();
            var a = new TestSource("A");
            var b = new TestTransform("B");
            var c = new TestTransform("C");
            var d = new TestSink("D");

            graph.Connect(a.Output, b.Input);
            graph.Connect(a.Output, c.Input);
            graph.Connect(b.Output, d.Input);
            graph.Connect(c.Output, d.Input);

            var order = graph.GetTopologicalOrder();

            Assert.Equal(4, order.Count);
            Assert.Same(a, order[0]); // A must be first

            int idxB = -1, idxC = -1, idxD = -1;
            for (int i = 0; i < order.Count; i++)
            {
                if (order[i] == b) idxB = i;
                if (order[i] == c) idxC = i;
                if (order[i] == d) idxD = i;
            }

            Assert.True(idxB > 0 && idxB < idxD);
            Assert.True(idxC > 0 && idxC < idxD);
            Assert.Equal(3, idxD); // D must be last
        }

        [Fact]
        public void CycleDetection_ThrowsCycleDetectedException()
        {
            var graph = new PipelineGraph();
            var nodeA = new TestTransform("NodeA");
            var nodeB = new TestTransform("NodeB");
            var nodeC = new TestTransform("NodeC");

            // A -> B -> C -> A
            graph.Connect(nodeA.Output, nodeB.Input);
            graph.Connect(nodeB.Output, nodeC.Input);
            graph.Connect(nodeC.Output, nodeA.Input);

            var ex = Assert.Throws<CycleDetectedException>(() => graph.GetTopologicalOrder());
            Assert.Contains("Circular dependency loop detected", ex.Message);
        }

        [Fact]
        public void SelfReferentialCycle_ThrowsCycleDetectedException()
        {
            var graph = new PipelineGraph();
            var nodeA = new TestTransform("NodeA");

            // A -> A
            var ex = Assert.Throws<CycleDetectedException>(() => graph.Connect(nodeA.Output, nodeA.Input));
            Assert.Contains("Self-referential", ex.Message);
        }

        [Fact]
        public void TypeSafety_WeaklyTypedConnect_ThrowsTypeMismatchException()
        {
            var graph = new PipelineGraph();
            var src = new TestSource("Source"); // Output: int
            var stringSink = new ActionSinkNode<string>(_ => { }, "StringSink"); // Input: string

            var ex = Assert.Throws<TypeMismatchException>(() =>
            {
                graph.Connect(src.Output, stringSink.Input);
            });

            Assert.Contains("Cannot connect Output port", ex.Message);
        }
    }
}
