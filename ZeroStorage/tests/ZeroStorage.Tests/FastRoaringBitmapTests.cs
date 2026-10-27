using System;
using Xunit;
using ZeroStorage.Core.Indexing;

namespace ZeroStorage.Tests
{
    public class FastRoaringBitmapTests
    {
        [Fact]
        public void FastRoaringBitmap_BasicAddAndContains_WorksAccurately()
        {
            var bitmap = new FastRoaringBitmap();
            Assert.True(bitmap.IsEmpty);

            int[] values = { 1, 5, 100, 1000, 65535, 65536, 100000, 2000000 };
            foreach (var v in values)
            {
                bitmap.Add(v);
            }

            Assert.False(bitmap.IsEmpty);
            foreach (var v in values)
            {
                Assert.True(bitmap.Contains(v));
            }

            Assert.False(bitmap.Contains(0));
            Assert.False(bitmap.Contains(2));
            Assert.False(bitmap.Contains(65537));

            var result = bitmap.ToArray();
            Assert.Equal(values.Length, result.Length);
            for (int i = 0; i < values.Length; i++)
            {
                Assert.Equal(values[i], result[i]);
            }
        }

        [Fact]
        public void FastRoaringBitmap_SetOperations_And_Or()
        {
            var bm1 = new FastRoaringBitmap();
            var bm2 = new FastRoaringBitmap();

            // BM1 has 1, 2, 3, 10, 20, 30
            bm1.Add(1); bm1.Add(2); bm1.Add(3);
            bm1.Add(10); bm1.Add(20); bm1.Add(30);

            // BM2 has 2, 4, 10, 25, 30
            bm2.Add(2); bm2.Add(4);
            bm2.Add(10); bm2.Add(25); bm2.Add(30);

            // Intersection: 2, 10, 30
            var andResult = bm1.And(bm2);
            int[] andArray = andResult.ToArray();
            Assert.Equal(new int[] { 2, 10, 30 }, andArray);

            // Union: 1, 2, 3, 4, 10, 20, 25, 30
            var orResult = bm1.Or(bm2);
            int[] orArray = orResult.ToArray();
            Assert.Equal(new int[] { 1, 2, 3, 4, 10, 20, 25, 30 }, orArray);
        }

        [Fact]
        public void FastRoaringBitmap_ContainerPromotion_ToBitmapContainer()
        {
            var bitmap = new FastRoaringBitmap();

            // Add 5,000 items in the same 16-bit chunk (>= 4096 threshold)
            for (int i = 0; i < 5000; i++)
            {
                bitmap.Add(i * 2);
            }

            for (int i = 0; i < 5000; i++)
            {
                Assert.True(bitmap.Contains(i * 2));
                Assert.False(bitmap.Contains(i * 2 + 1));
            }

            var arr = bitmap.ToArray();
            Assert.Equal(5000, arr.Length);
        }
    }
}
