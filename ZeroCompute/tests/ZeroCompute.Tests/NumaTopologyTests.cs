using System;
using Xunit;
using ZeroCompute.Core.Cpu;

namespace ZeroCompute.Tests
{
    public class NumaTopologyTests
    {
        [Fact]
        public void NumaTopology_DetectionAndNodeCount()
        {
            int nodes = NumaTopology.NodeCount;
            Assert.True(nodes >= 1, "Host must report at least 1 NUMA node.");
        }

        [Fact]
        public void NumaTopology_AllocateAndFreeBuffer()
        {
            int size = 4096;
            IntPtr buffer = NumaTopology.Allocate(size, preferredNode: 0);

            Assert.NotEqual(IntPtr.Zero, buffer);

            unsafe
            {
                byte* ptr = (byte*)buffer;
                for (int i = 0; i < size; i++)
                {
                    ptr[i] = (byte)(i % 256);
                }

                for (int i = 0; i < size; i++)
                {
                    Assert.Equal((byte)(i % 256), ptr[i]);
                }
            }

            NumaTopology.Free(buffer, size);
        }
    }
}
