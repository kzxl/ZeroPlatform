using System;
using System.Collections.Generic;
using Xunit;
using ZeroUI.Core.Communication;
using ZeroUI.Core.Scada;

namespace ZeroUI.Core.Tests
{
    public class S7AddressPlannerTests
    {
        [Theory]
        [InlineData("DB1.DBD0", 1, 0, 0)]
        [InlineData("DB5.DBW10", 5, 10, 0)]
        [InlineData("DB2.DBX4.3", 2, 4, 3)]
        [InlineData("DB100.DBB25", 100, 25, 0)]
        public void TryParseAddress_ValidPatterns_ParsedCorrectly(string addr, int expectedDb, int expectedOffset, int expectedBit)
        {
            bool ok = S7AddressPlanner.TryParseAddress(addr, out int db, out int offset, out int bit);
            Assert.True(ok);
            Assert.Equal(expectedDb, db);
            Assert.Equal(expectedOffset, offset);
            Assert.Equal(expectedBit, bit);
        }

        [Fact]
        public void PlanReadBlocks_AdjacentTags_CoalescesIntoSingleBlock()
        {
            var tags = new List<AdapterTagDefinition>
            {
                new AdapterTagDefinition("Temp", "DB1.DBD0", TagDataType.Float32),
                new AdapterTagDefinition("Pressure", "DB1.DBD4", TagDataType.Float32),
                new AdapterTagDefinition("Flow", "DB1.DBD8", TagDataType.Float32),
                new AdapterTagDefinition("Level", "DB1.DBD12", TagDataType.Float32),
            };

            var blocks = S7AddressPlanner.PlanReadBlocks(tags);

            Assert.Single(blocks);
            var block = blocks[0];
            Assert.Equal(1, block.DbNumber);
            Assert.Equal(0, block.StartByteOffset);
            Assert.Equal(16, block.ByteCount); // 4 floats * 4 bytes = 16 bytes
            Assert.Equal(4, block.TagMappings.Count);
            Assert.Equal(0, block.TagMappings[0].RelativeByteOffset);
            Assert.Equal(4, block.TagMappings[1].RelativeByteOffset);
            Assert.Equal(8, block.TagMappings[2].RelativeByteOffset);
            Assert.Equal(12, block.TagMappings[3].RelativeByteOffset);
        }

        [Fact]
        public void PlanReadBlocks_MultipleDBs_SeparatesByDbNumber()
        {
            var tags = new List<AdapterTagDefinition>
            {
                new AdapterTagDefinition("DB1_T1", "DB1.DBD0", TagDataType.Float32),
                new AdapterTagDefinition("DB1_T2", "DB1.DBD4", TagDataType.Float32),
                new AdapterTagDefinition("DB2_T1", "DB2.DBD0", TagDataType.Float32),
                new AdapterTagDefinition("DB2_T2", "DB2.DBD4", TagDataType.Float32),
            };

            var blocks = S7AddressPlanner.PlanReadBlocks(tags);

            Assert.Equal(2, blocks.Count);
            Assert.Equal(1, blocks[0].DbNumber);
            Assert.Equal(2, blocks[1].DbNumber);
        }

        [Fact]
        public void PlanReadBlocks_LargeGap_SplitsIntoSeparateBlocks()
        {
            var tags = new List<AdapterTagDefinition>
            {
                new AdapterTagDefinition("T1", "DB1.DBD0", TagDataType.Float32),
                new AdapterTagDefinition("T2", "DB1.DBD100", TagDataType.Float32), // Gap = 96 bytes > maxByteGap (10)
            };

            var blocks = S7AddressPlanner.PlanReadBlocks(tags, maxByteGap: 10);

            Assert.Equal(2, blocks.Count);
            Assert.Equal(0, blocks[0].StartByteOffset);
            Assert.Equal(4, blocks[0].ByteCount);
            Assert.Equal(100, blocks[1].StartByteOffset);
            Assert.Equal(4, blocks[1].ByteCount);
        }

        [Fact]
        public void PlanReadBlocks_DenseSet_ReducesRequestsByOver90Percent()
        {
            var tags = new List<AdapterTagDefinition>();
            for (int i = 0; i < 100; i++)
            {
                // 100 contiguous float tags: DB1.DBD0, DB1.DBD4, ... DB1.DBD396 (400 bytes total)
                tags.Add(new AdapterTagDefinition($"Tag_{i}", $"DB1.DBD{i * 4}", TagDataType.Float32));
            }

            // Max payload 222 bytes per block -> 400 bytes will fit into 2 blocks!
            var blocks = S7AddressPlanner.PlanReadBlocks(tags, maxBlockBytes: 222);

            Assert.Equal(2, blocks.Count);
            double reduction = (1.0 - (double)blocks.Count / tags.Count) * 100.0;
            Assert.True(reduction >= 95.0, $"Expected >= 95% request reduction, got {reduction:F1}%");
        }

        [Fact]
        public void SiemensS7Adapter_RegisterTag_CompilesBlocksAutomatically()
        {
            using (var adapter = new SiemensS7Adapter("s7_test", "127.0.0.1", port: 102))
            {
                adapter.RegisterTag(new AdapterTagDefinition("Tag1", "DB1.DBD0", TagDataType.Float32));
                adapter.RegisterTag(new AdapterTagDefinition("Tag2", "DB1.DBD4", TagDataType.Float32));

                var blocks = adapter.CompiledBlocks;
                Assert.Single(blocks);
                Assert.Equal(8, blocks[0].ByteCount);
            }
        }
    }
}
