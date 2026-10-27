using System;
using System.IO;
using System.Linq;
using Xunit;
using ZeroAgent.Core.Database;

namespace ZeroAgent.Tests
{
    public class ZabKnowledgeIngestionTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly string _zabPath;
        private readonly string _jsonPath;

        public ZabKnowledgeIngestionTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "zab_ingest_test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
            _zabPath = Path.Combine(_tempDir, "agent_core.zab");
            _jsonPath = Path.Combine(_tempDir, "agent_core_kb.json");

            string sampleJson = @"{
  ""Manifest"": {
    ""Name"": ""ZeroAgent-Core"",
    ""Description"": ""Core Knowledge Base for Autonomous Agent Operations"",
    ""Role"": ""Autonomous-Engineer"",
    ""SystemPrompt"": ""You are a sovereign AI agent."",
    ""RegisteredTools"": [""Search"", ""Execute"", ""Plan""]
  },
  ""Knowledge"": [
    {
      ""Key"": ""system_rule_pure_csharp"",
      ""Value"": ""All core engines must remain 100% native managed C# without unmanaged wrappers."",
      ""Category"": ""Architecture"",
      ""Author"": ""CoreTeam"",
      ""Confidence"": 1.0
    },
    {
      ""Key"": ""system_rule_zero_alloc"",
      ""Value"": ""Evaluation loops must avoid GC heap allocations using Span and SIMD intrinsics."",
      ""Category"": ""Performance"",
      ""Author"": ""CoreTeam"",
      ""Confidence"": 1.0
    },
    {
      ""Key"": ""system_rule_durability"",
      ""Value"": ""State mutations must be atomic and protected by Write-Ahead Log (WAL) and CRC32C."",
      ""Category"": ""Reliability"",
      ""Author"": ""CoreTeam"",
      ""Confidence"": 1.0
    }
  ],
  ""Reflexions"": [
    {
      ""Goal"": ""Optimize vector search"",
      ""FailureReason"": ""Full FP32 decompression created cache thrashing."",
      ""Lesson"": ""Use 1-bit POPCNT filter followed by SIMD SQ8 inner product reranking.""
    }
  ],
  ""Plans"": [
    {
      ""Goal"": ""Process autonomous reasoning"",
      ""Solution"": ""Step 1: Check Bloom Filter. Step 2: Query Hash Index. Step 3: Execute policy."",
      ""StepsCount"": 3,
      ""Confidence"": 0.99
    }
  ]
}";
            File.WriteAllText(_jsonPath, sampleJson);
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
        public void ImportKnowledgeFromJson_PopulatesDatabaseAndIndexes()
        {
            // 1. Create container and import JSON
            using (var db = ZabDatabase.CreateNew(_zabPath))
            {
                db.ImportFromJson(_jsonPath);

                Assert.Equal("ZeroAgent-Core", db.Manifest.Name);
                Assert.Equal(3, db.Knowledge.Count);
                Assert.Single(db.Reflexions);
                Assert.Single(db.Plans);

                // 2. Test specific rules
                var rule = db.FindKnowledge("system_rule_pure_csharp");
                Assert.NotNull(rule);
                Assert.Contains("100% native managed C#", rule.Value);

                var perfRule = db.FindKnowledge("system_rule_zero_alloc");
                Assert.NotNull(perfRule);
                Assert.Contains("Span and SIMD", perfRule.Value);
            }

            // 3. Reopen binary .zab and verify persistence
            using (var reopenedDb = ZabDatabase.Open(_zabPath))
            {
                Assert.Equal("ZeroAgent-Core", reopenedDb.Manifest.Name);
                Assert.Equal(3, reopenedDb.Knowledge.Count);

                // Build Bloom filter and sparse block index
                var bloom = ZabBloomFilter.Build(reopenedDb.Knowledge.Select(k => k.Key));
                Assert.True(bloom.MayContain("system_rule_pure_csharp"));
                Assert.True(bloom.MayContain("system_rule_durability"));
                Assert.False(bloom.MayContain("non_existent_random_key_12345"));

                var billionIndex = reopenedDb.BuildBillionScaleIndex(recordsPerBlock: 2);
                Assert.NotNull(billionIndex);
            }
        }
    }
}
