using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Xunit;
using ZeroSystem.Memory;

namespace ZeroSystem.Tests;

public class OffHeapMemoryStoreTests
{
    [StructLayout(LayoutKind.Sequential)]
    private struct SampleMetric
    {
        public long Timestamp;
        public double CpuLoad;
        public ulong FreeRam;
    }

    [Fact]
    public void OffHeapMemoryStore_BasicAllocate_Write_Read_Free()
    {
        using var store = new OffHeapMemoryStore(slotSize: 256, capacity: 16);
        Assert.Equal(16, store.Capacity);
        Assert.Equal(0, store.AllocatedCount);
        Assert.Equal(16, store.AvailableCount);

        // Allocate slot 1
        var h1 = store.Allocate();
        Assert.True(h1.IsValid);
        Assert.Equal(1, store.AllocatedCount);
        Assert.Equal(15, store.AvailableCount);

        // Write raw bytes
        byte[] payload = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];
        store.Write(h1, payload);

        // Read raw bytes
        byte[] readBack = new byte[10];
        int readBytes = store.Read(h1, readBack);
        Assert.Equal(10, readBytes);
        Assert.Equal(payload, readBack);

        // Direct Span access
        var span = store.GetSpan(h1);
        Assert.Equal(1, span[0]);
        Assert.Equal(10, span[9]);
        span[0] = 99;
        Assert.Equal(99, store.GetSpan(h1)[0]);

        // Ref access
        ref var metric = ref store.GetRef<SampleMetric>(h1);
        metric.Timestamp = 123456789L;
        metric.CpuLoad = 85.5;
        metric.FreeRam = 1024 * 1024 * 500UL;

        ref var readMetric = ref store.GetRef<SampleMetric>(h1);
        Assert.Equal(123456789L, readMetric.Timestamp);
        Assert.Equal(85.5, readMetric.CpuLoad);
        Assert.Equal(1024 * 1024 * 500UL, readMetric.FreeRam);

        // Free
        Assert.True(store.Free(h1));
        Assert.Equal(0, store.AllocatedCount);
        Assert.Equal(16, store.AvailableCount);
    }

    [Fact]
    public void OffHeapMemoryStore_UseAfterFree_ThrowsException()
    {
        using var store = new OffHeapMemoryStore(slotSize: 64, capacity: 4);
        var handle = store.Allocate();
        Assert.True(handle.IsValid);

        // Valid write
        store.Write(handle, [1, 2, 3]);

        // Free handle
        Assert.True(store.Free(handle));

        // Re-use must throw InvalidOperationException
        Assert.Throws<InvalidOperationException>(() =>
        {
            store.GetSpan(handle);
        });

        // Double free must return false
        Assert.False(store.Free(handle));
    }

    [Fact]
    public void OffHeapMemoryStore_CapacityExhaustion()
    {
        using var store = new OffHeapMemoryStore(slotSize: 64, capacity: 2);
        var h1 = store.Allocate();
        var h2 = store.Allocate();
        Assert.True(h1.IsValid);
        Assert.True(h2.IsValid);
        Assert.Equal(0, store.AvailableCount);

        var h3 = store.Allocate();
        Assert.False(h3.IsValid);
        Assert.Equal(OffHeapHandle.Invalid, h3);

        store.Free(h1);
        Assert.Equal(1, store.AvailableCount);

        var h4 = store.Allocate();
        Assert.True(h4.IsValid);
    }

    [Fact]
    public void OffHeapCircularTelemetryBuffer_BasicWrite_And_ReadLatest()
    {
        using var ring = new OffHeapCircularTelemetryBuffer<SampleMetric>(capacity: 16);
        Assert.Equal(16, ring.Capacity);
        Assert.Equal(0, ring.TotalWrittenCount);

        Assert.False(ring.TryReadLatest(out _));

        // Write 5 metrics
        for (int i = 1; i <= 5; i++)
        {
            ring.Write(new SampleMetric { Timestamp = i, CpuLoad = i * 10.0, FreeRam = (ulong)i });
        }

        Assert.Equal(5, ring.TotalWrittenCount);
        Assert.True(ring.TryReadLatest(out var latest));
        Assert.Equal(5, latest.Timestamp);
        Assert.Equal(50.0, latest.CpuLoad);

        // Copy recent 3 items
        Span<SampleMetric> recent = new SampleMetric[3];
        int count = ring.CopyRecent(recent);
        Assert.Equal(3, count);
        Assert.Equal(3, recent[0].Timestamp);
        Assert.Equal(4, recent[1].Timestamp);
        Assert.Equal(5, recent[2].Timestamp);
    }

    [Fact]
    public void OffHeapCircularTelemetryBuffer_WraparoundBehavior()
    {
        // Capacity = 4
        using var ring = new OffHeapCircularTelemetryBuffer<long>(capacity: 4);

        // Write 10 items (0..9)
        for (long i = 0; i < 10; i++)
        {
            ring.Write(i);
        }

        Assert.Equal(10, ring.TotalWrittenCount);
        Assert.True(ring.TryReadLatest(out long latest));
        Assert.Equal(9, latest);

        // Should return 4 most recent items in chronological order: [6, 7, 8, 9]
        Span<long> buffer = new long[4];
        int count = ring.CopyRecent(buffer);
        Assert.Equal(4, count);
        Assert.Equal(6, buffer[0]);
        Assert.Equal(7, buffer[1]);
        Assert.Equal(8, buffer[2]);
        Assert.Equal(9, buffer[3]);
    }

    [Fact]
    public void OffHeapCircularTelemetryBuffer_ConcurrentWrites()
    {
        using var ring = new OffHeapCircularTelemetryBuffer<long>(capacity: 1024);

        Parallel.For(0, 1000, i =>
        {
            ring.Write(i);
        });

        Assert.Equal(1000, ring.TotalWrittenCount);
        Assert.True(ring.TryReadLatest(out _));
    }

    [Fact]
    public void OffHeapMemoryStore_SteadyStateAllocateFree_ZeroGcAllocation()
    {
        using var store = new OffHeapMemoryStore(slotSize: 64, capacity: 32);

        // Warm-up
        var warm = store.Allocate();
        Assert.True(store.Free(warm));

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++)
        {
            OffHeapHandle handle = store.Allocate();
            ref long val = ref store.GetRef<long>(handle);
            val = i;
            store.Free(handle);
        }
        long after = GC.GetAllocatedBytesForCurrentThread();

        Assert.Equal(0L, after - before);
        Assert.Equal(0, store.AllocatedCount);
        Assert.Equal(32, store.AvailableCount);
    }
}
