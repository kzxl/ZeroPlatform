using System;
using System.Collections.Generic;
using Xunit;
using ZeroUI.Core.Charts;

namespace ZeroUI.Core.Tests
{
    public class ChartStreamingBufferTests
    {
        [Fact]
        public void Push_UnderCapacity_RetainsAllPoints()
        {
            var buffer = new ChartStreamingBuffer(64);
            for (int i = 0; i < 20; i++)
            {
                buffer.Push(i * 1.5, $"P{i}");
            }

            Assert.Equal(20, buffer.Count);
            var snapshot = buffer.Snapshot();
            Assert.Equal(20, snapshot.Length);
            Assert.Equal(0.0, snapshot[0].Value);
            Assert.Equal("P0", snapshot[0].Label);
            Assert.Equal(19 * 1.5, snapshot[19].Value);
        }

        [Fact]
        public void Push_ExceedsCapacity_MaintainsRollingWindow()
        {
            // Capacity rounds up to 32
            var buffer = new ChartStreamingBuffer(32);
            int capacity = buffer.Capacity;
            Assert.Equal(32, capacity);

            for (int i = 0; i < capacity + 10; i++)
            {
                buffer.Push(i, $"P{i}");
            }

            Assert.Equal(capacity, buffer.Count);
            var snapshot = buffer.Snapshot();
            Assert.Equal(capacity, snapshot.Length);
            // Oldest points 0..9 should be evicted, first point is 10
            Assert.Equal(10.0, snapshot[0].Value);
            Assert.Equal(capacity + 9, snapshot[capacity - 1].Value);
        }

        [Fact]
        public void SnapshotValues_ReturnsOnlyNumericalValues()
        {
            var buffer = new ChartStreamingBuffer(16);
            buffer.Push(10.5);
            buffer.Push(20.5);
            buffer.Push(30.5);

            var values = buffer.SnapshotValues();
            Assert.Equal(3, values.Length);
            Assert.Equal(10.5, values[0]);
            Assert.Equal(20.5, values[1]);
            Assert.Equal(30.5, values[2]);
        }

        [Fact]
        public void CopyTo_FillsTargetList()
        {
            var buffer = new ChartStreamingBuffer(16);
            buffer.Push(1.0);
            buffer.Push(2.0);

            var list = new List<ChartPoint>();
            buffer.CopyTo(list);

            Assert.Equal(2, list.Count);
            Assert.Equal(1.0, list[0].Value);
            Assert.Equal(2.0, list[1].Value);
        }

        [Fact]
        public void Clear_EmptiesBuffer()
        {
            var buffer = new ChartStreamingBuffer(16);
            buffer.Push(1.0);
            buffer.Push(2.0);
            Assert.Equal(2, buffer.Count);

            buffer.Clear();
            Assert.Equal(0, buffer.Count);
            Assert.Empty(buffer.Snapshot());
        }
    }
}
