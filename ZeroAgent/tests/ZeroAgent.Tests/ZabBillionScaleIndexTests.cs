using System;
using System.Collections.Generic;
using System.IO;
using Xunit;
using ZeroAgent.Core.Database;
using ZeroPrimitives.Cryptography;

namespace ZeroAgent.Tests
{
    public class ZabBillionScaleIndexTests : IDisposable
    {
        private readonly string _tempDir;

        public ZabBillionScaleIndexTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "ZabBillionTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_tempDir))
                {
                    Directory.Delete(_tempDir, true);
                }
            }
            catch { }
        }

        [Fact]
        public void ZabBloomFilter_ZeroFalseNegatives_AndLowFalsePositiveRate()
        {
            const int count = 5000;
            var filter = new ZabBloomFilter(count, falsePositiveRate: 0.01);
            var insertedHashes = new ulong[count];

            for (int i = 0; i < count; i++)
            {
                ulong hash = FastHash.Fnv1a64($"key_entry_{i}".AsSpan());
                insertedHashes[i] = hash;
                filter.Add(hash);
            }

            // 1. Zero False Negatives Guarantee
            for (int i = 0; i < count; i++)
            {
                Assert.True(filter.MayContain(insertedHashes[i]), $"False negative detected for hash at index {i}!");
            }

            // 2. Low False Positive Test
            int falsePositives = 0;
            const int testQueries = 10000;
            for (int i = 0; i < testQueries; i++)
            {
                ulong nonExistent = FastHash.Fnv1a64($"non_existent_key_{i + 1000000}".AsSpan());
                if (filter.MayContain(nonExistent))
                {
                    falsePositives++;
                }
            }

            double fpRate = (double)falsePositives / testQueries;
            Assert.True(fpRate < 0.03, $"False positive rate too high: {fpRate:P2}");
        }

        [Fact]
        public void ZabBillionScaleIndex_PointLookup_FindsAllRecordsDirectly()
        {
            const int recordCount = 1000;
            const int recordsPerBlock = 32;

            var slots = new List<ZabIndexSlot>(recordCount);
            var keyList = new string[recordCount];

            for (int i = 0; i < recordCount; i++)
            {
                string key = $"DOMAIN_RULE_{i:D5}";
                keyList[i] = key;
                slots.Add(new ZabIndexSlot
                {
                    KeyHash = FastHash.Fnv1a64(key.AsSpan()),
                    FileOffset = 10000 + i * 128,
                    Length = 128,
                    Checksum = (uint)i
                });
            }

            var index = ZabBillionScaleIndex.Build(slots, recordsPerBlock);

            Assert.Equal(recordCount, index.TotalRecords);
            Assert.True(index.BlockCount >= (recordCount / recordsPerBlock));

            // Test 100% Hit Rate on all keys
            for (int i = 0; i < recordCount; i++)
            {
                bool found = index.TryLookup(keyList[i].AsSpan(), out long offset, out int len);
                Assert.True(found, $"Key {keyList[i]} was not found in billion-scale index!");
                Assert.Equal(10000 + i * 128, offset);
                Assert.Equal(128, len);
            }

            // Negative Lookups
            for (int i = 0; i < 100; i++)
            {
                string missingKey = $"MISSING_KEY_{i}";
                bool found = index.TryLookup(missingKey.AsSpan(), out _, out _);
                Assert.False(found);
            }
        }

        [Fact]
        public void ZabBillionScaleIndex_SerializationRoundTrip_PreservesAllBlocks()
        {
            var slots = new List<ZabIndexSlot>();
            for (int i = 0; i < 200; i++)
            {
                string key = $"KEY_ROUNDTRIP_{i}";
                slots.Add(new ZabIndexSlot
                {
                    KeyHash = FastHash.Fnv1a64(key.AsSpan()),
                    FileOffset = 500 + i * 64,
                    Length = 64,
                    Checksum = (uint)i
                });
            }

            var originalIndex = ZabBillionScaleIndex.Build(slots, recordsPerBlock: 16);

            byte[] serialized;
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                originalIndex.WriteTo(writer);
                serialized = ms.ToArray();
            }

            ZabBillionScaleIndex restoredIndex;
            using (var ms = new MemoryStream(serialized))
            using (var reader = new BinaryReader(ms))
            {
                restoredIndex = ZabBillionScaleIndex.ReadFrom(reader);
            }

            Assert.Equal(originalIndex.TotalRecords, restoredIndex.TotalRecords);
            Assert.Equal(originalIndex.BlockCount, restoredIndex.BlockCount);

            // Verify lookup on restored index
            for (int i = 0; i < 200; i++)
            {
                string key = $"KEY_ROUNDTRIP_{i}";
                bool found = restoredIndex.TryLookup(key.AsSpan(), out long offset, out int len);
                Assert.True(found);
                Assert.Equal(500 + i * 64, offset);
                Assert.Equal(64, len);
            }
        }

        [Fact]
        public void ZabDatabase_Integration_BillionScalePointLookupViaMMap()
        {
            string dbPath = Path.Combine(_tempDir, "billion_scale_test.zab");

            using (var db = ZabDatabase.CreateNew(dbPath))
            {
                // Insert 128 knowledge items
                for (int i = 0; i < 128; i++)
                {
                    db.AddKnowledge($"SYS_PARAM_{i:D4}", $"OptimizedValue_{i}", category: "ScaleConfig");
                }

                // Build Billion Scale Index over written records
                var index = db.BuildBillionScaleIndex(recordsPerBlock: 16);

                // Use ZabMMapReader to point-lookup records without full table scan
                using (var mmapReader = db.CreateMMapReader())
                {
                    var rec50 = mmapReader.ReadKnowledgeByKey("SYS_PARAM_0050", index);
                    Assert.NotNull(rec50);
                    Assert.Equal("SYS_PARAM_0050", rec50!.Key);
                    Assert.Equal("OptimizedValue_50", rec50.Value);
                    Assert.Equal("ScaleConfig", rec50.Category);

                    var rec100 = mmapReader.ReadKnowledgeByKey("SYS_PARAM_0100", index);
                    Assert.NotNull(rec100);
                    Assert.Equal("SYS_PARAM_0100", rec100!.Key);
                    Assert.Equal("OptimizedValue_100", rec100.Value);

                    // Missing key returns null immediately
                    var missing = mmapReader.ReadKnowledgeByKey("NON_EXISTENT_KEY", index);
                    Assert.Null(missing);
                }
            }
        }
    }
}
