using System;
using Xunit;
using ZeroLlm.Core.Memory;

namespace ZeroLlm.Tests
{
    public class PagedKvCacheTests
    {
        [Fact]
        public void PagedKvCache_Should_Allocate_Append_And_Free_Blocks()
        {
            const int totalBlocks = 16;
            const int blockSize = 4;
            const int layers = 2;
            const int kvHeads = 2;
            const int headDim = 8;

            using (var cache = new PagedKvCache(totalBlocks, blockSize, layers, kvHeads, headDim))
            {
                Assert.Equal(totalBlocks, cache.FreeBlockCount);

                int seqId = 101;
                float[] keyData = new float[kvHeads * headDim];
                float[] valData = new float[kvHeads * headDim];

                // Append 10 tokens (requires 3 blocks since ceil(10/4) = 3)
                for (int t = 0; t < 10; t++)
                {
                    keyData[0] = t * 1.5f;
                    valData[0] = t * 2.5f;

                    cache.AppendToken(seqId, 0, keyData, valData);
                    cache.AppendToken(seqId, 1, keyData, valData);
                }

                Assert.Equal(10, cache.GetSequenceLength(seqId));
                Assert.Equal(totalBlocks - 3, cache.FreeBlockCount);

                // Verify values at token 7
                var retrievedKey = cache.GetKey(seqId, 0, 7);
                var retrievedVal = cache.GetValue(seqId, 1, 7);

                Assert.Equal(7 * 1.5f, retrievedKey[0]);
                Assert.Equal(7 * 2.5f, retrievedVal[0]);

                // Free sequence
                cache.FreeSequence(seqId);
                Assert.Equal(0, cache.GetSequenceLength(seqId));
                Assert.Equal(totalBlocks, cache.FreeBlockCount);
            }
        }
    }
}
