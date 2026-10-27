using System;
using Xunit;
using ZeroUI.Core.Memory;

namespace ZeroUI.Core.Tests
{
    public class ZeroBufferPoolTests
    {
        [Fact]
        public void RentAndReturn_ByteArray_MaintainsPoolIntegrity()
        {
            byte[] buffer = ZeroBufferPool.RentByteArray(256);
            Assert.NotNull(buffer);
            Assert.True(buffer.Length >= 256);

            ZeroBufferPool.ReturnByteArray(buffer);
        }

        [Fact]
        public void RentAndReturn_GenericArray_MaintainsPoolIntegrity()
        {
            int[] buffer = ZeroBufferPool.Rent<int>(500);
            Assert.NotNull(buffer);
            Assert.True(buffer.Length >= 500);

            ZeroBufferPool.Return(buffer);
        }

        [Fact]
        public unsafe void RentNative_AllocatesAndFreesNativeMemory()
        {
            nuint size = 1024;
            using (var lease = ZeroBufferPool.RentNative(size, zeroMemory: true))
            {
                Assert.True(lease.IsValid);
                Assert.Equal(size, lease.ByteCount);

                Span<byte> span = lease.AsSpan();
                Assert.Equal(1024, span.Length);
                for (int i = 0; i < span.Length; i++)
                {
                    Assert.Equal(0, span[i]);
                }

                span[0] = 0xAA;
                span[1023] = 0xFF;
                Assert.Equal(0xAA, span[0]);
                Assert.Equal(0xFF, span[1023]);
            }
        }
    }
}
