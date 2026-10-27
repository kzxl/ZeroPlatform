using System;
using System.Text;
using Xunit;
using ZeroConcurrency.Ipc;

namespace ZeroConcurrency.Tests
{
    public class MmfIpcTests
    {
        [Fact]
        public void MmfRingBuffer_CreateAndWriteRead_RoundtripsSuccessfully()
        {
            string mapName = "ZeroTest_SPSC_" + Guid.NewGuid().ToString("N");
            using var buffer = ZeroMmfRingBuffer.CreateOrOpen(mapName, capacity: 8, slotSize: 256);

            Assert.Equal(8, buffer.Capacity);
            Assert.Equal(252, buffer.MaxPayloadSize);
            Assert.Equal(0, buffer.Count);

            byte[] message1 = Encoding.UTF8.GetBytes("Hello ZeroPlatform 4K Video Stream!");
            byte[] message2 = Encoding.UTF8.GetBytes("Camera Frame 001 - PTS: 10020304050");

            Assert.True(buffer.TryWrite(message1));
            Assert.True(buffer.TryWrite(message2));
            Assert.Equal(2, buffer.Count);

            Span<byte> readBuf = stackalloc byte[256];

            Assert.True(buffer.TryRead(readBuf, out int len1));
            Assert.Equal(message1.Length, len1);
            Assert.Equal("Hello ZeroPlatform 4K Video Stream!", Encoding.UTF8.GetString(readBuf.Slice(0, len1)));

            Assert.True(buffer.TryRead(readBuf, out int len2));
            Assert.Equal(message2.Length, len2);
            Assert.Equal("Camera Frame 001 - PTS: 10020304050", Encoding.UTF8.GetString(readBuf.Slice(0, len2)));

            Assert.Equal(0, buffer.Count);
            Assert.False(buffer.TryRead(readBuf, out _));
        }

        [Fact]
        public void MmfRingBuffer_Backpressure_RejectsWhenFull()
        {
            string mapName = "ZeroTest_Full_" + Guid.NewGuid().ToString("N");
            using var buffer = ZeroMmfRingBuffer.CreateOrOpen(mapName, capacity: 4, slotSize: 64);

            byte[] payload = new byte[] { 1, 2, 3, 4, 5 };

            // Fill all 4 slots
            Assert.True(buffer.TryWrite(payload));
            Assert.True(buffer.TryWrite(payload));
            Assert.True(buffer.TryWrite(payload));
            Assert.True(buffer.TryWrite(payload));
            Assert.Equal(4, buffer.Count);

            // 5th write must be rejected (backpressure)
            Assert.False(buffer.TryWrite(payload));

            // Drain 1 slot
            Span<byte> readBuf = stackalloc byte[64];
            Assert.True(buffer.TryRead(readBuf, out int readLen));
            Assert.Equal(5, readLen);
            Assert.Equal(3, buffer.Count);

            // Now write must succeed
            Assert.True(buffer.TryWrite(payload));
            Assert.Equal(4, buffer.Count);
        }

        [Fact]
        public void MmfRingBuffer_OpenExisting_EnablesInterProcessSharing()
        {
            string mapName = "ZeroTest_Shared_" + Guid.NewGuid().ToString("N");
            using var writer = ZeroMmfRingBuffer.CreateOrOpen(mapName, capacity: 8, slotSize: 128);
            using var reader = ZeroMmfRingBuffer.OpenExisting(mapName);

            byte[] data = Encoding.UTF8.GetBytes("Zero-Copy Cross Process IPC Data");
            Assert.True(writer.TryWrite(data));

            Assert.Equal(1, reader.Count);

            Span<byte> dest = stackalloc byte[128];
            Assert.True(reader.TryRead(dest, out int bytesRead));
            Assert.Equal(data.Length, bytesRead);
            Assert.Equal("Zero-Copy Cross Process IPC Data", Encoding.UTF8.GetString(dest.Slice(0, bytesRead)));
        }
    }
}
