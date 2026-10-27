using System;
using System.Threading.Tasks;
using Xunit;
using ZeroPrimitives.Memory;

namespace ZeroPrimitives.Tests
{
    public class StructArenaPoolTests
    {
        private struct Point3D
        {
            public float X, Y, Z;
        }

        private struct TelemetryReading
        {
            public long Timestamp;
            public double Value;
            public int ChannelId;
            public byte Quality;
        }

        [Fact]
        public void StructArenaPool_RentAndMutate_ZeroAllocation()
        {
            using var pool = new StructArenaPool(slotsPerBlock: 32);

            using (var lease = pool.Rent<Point3D>())
            {
                ref var pt = ref lease.Value;
                pt.X = 10.5f;
                pt.Y = 20.25f;
                pt.Z = -30.0f;

                Assert.Equal(10.5f, lease.Value.X);
                Assert.Equal(20.25f, lease.Value.Y);
                Assert.Equal(-30.0f, lease.Value.Z);

                var span = lease.AsBytes();
                Assert.Equal(12, span.Length); // 3 * 4 bytes
            }
        }

        [Fact]
        public void StructArenaPool_PooledStructRef_CollectionStorage()
        {
            using var pool = new StructArenaPool(slotsPerBlock: 16);
            var refs = new PooledStructRef<TelemetryReading>[10];

            for (int i = 0; i < refs.Length; i++)
            {
                refs[i] = pool.RentRef<TelemetryReading>();
                refs[i].Value.Timestamp = 1000 + i;
                refs[i].Value.Value = (i + 1) * 3.14159;
                refs[i].Value.ChannelId = i;
                refs[i].Value.Quality = 1;
            }

            for (int i = 0; i < refs.Length; i++)
            {
                Assert.Equal(1000 + i, refs[i].Value.Timestamp);
                Assert.Equal((i + 1) * 3.14159, refs[i].Value.Value, precision: 4);
                Assert.Equal(i, refs[i].Value.ChannelId);
                refs[i].Dispose();
            }
        }

        [Fact]
        public void StructArenaPool_PolymorphicEnvelope_DifferentStructsWithoutBoxing()
        {
            using var pool = new StructArenaPool(slotsPerBlock: 32);

            // Envelope 1: Point3D (TypeId = 101)
            using (var env1 = pool.RentEnvelope(typeId: 101, byteSize: sizeof(float) * 3))
            {
                Assert.Equal(101, env1.TypeId);
                ref var p = ref env1.As<Point3D>();
                p.X = 1f; p.Y = 2f; p.Z = 3f;

                Assert.Equal(1f, env1.As<Point3D>().X);
                Assert.Equal(2f, env1.As<Point3D>().Y);
                Assert.Equal(3f, env1.As<Point3D>().Z);
            }

            // Envelope 2: TelemetryReading (TypeId = 202)
            using (var env2 = pool.RentEnvelope(typeId: 202, byteSize: sizeof(long) + sizeof(double) + sizeof(int) + sizeof(byte) + 7))
            {
                Assert.Equal(202, env2.TypeId);
                ref var t = ref env2.As<TelemetryReading>();
                t.ChannelId = 42;
                t.Value = 999.9;

                Assert.Equal(42, env2.As<TelemetryReading>().ChannelId);
                Assert.Equal(999.9, env2.As<TelemetryReading>().Value, precision: 2);
            }
        }

        [Fact]
        public void StructArenaPool_ConcurrentRentReturn_NoDeadlocks()
        {
            using var pool = new StructArenaPool(slotsPerBlock: 64);

            Parallel.For(0, 1000, i =>
            {
                using var lease = pool.Rent<Point3D>();
                lease.Value.X = i;
                lease.Value.Y = i * 2;
                lease.Value.Z = i * 3;

                Assert.Equal(i, lease.Value.X);
                Assert.Equal(i * 2, lease.Value.Y);
                Assert.Equal(i * 3, lease.Value.Z);
            });
        }
    }
}
