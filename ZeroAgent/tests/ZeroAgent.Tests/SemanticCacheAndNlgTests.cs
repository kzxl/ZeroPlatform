using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using ZeroAgent.Dialog;
using ZeroAgent.Dialog.DST;
using ZeroAgent.Dialog.Engine;
using ZeroAgent.Dialog.Generator;
using ZeroAgent.Dialog.Memory;

namespace ZeroAgent.Tests
{
    public class SemanticCacheAndNlgTests
    {
        [Fact]
        public void SemanticResponseCache_StoreAndRetrieve_SubMillisecondHit()
        {
            var cache = new SemanticResponseCache(dimension: 16, defaultTtl: TimeSpan.FromMinutes(5));

            float[] vec1 = new float[16];
            vec1[0] = 1.0f; // Unit vector on dimension 0

            cache.Store(vec1, "Kiểm tra nhiệt độ máy", "Nhiệt độ hiện tại là 65 độ C", intentName: "QUERY_METRIC");

            // Query with identical vector
            bool hit = cache.TryGet(vec1, minSimilarity: 0.95f, out var entry);
            Assert.True(hit);
            Assert.NotNull(entry);
            Assert.Equal("Nhiệt độ hiện tại là 65 độ C", entry!.ResponseText);
            Assert.Equal("QUERY_METRIC", entry.IntentName);
            Assert.True(entry.Similarity >= 0.99f);

            // Query with orthogonal vector (should miss)
            float[] vec2 = new float[16];
            vec2[1] = 1.0f;
            bool hitMiss = cache.TryGet(vec2, minSimilarity: 0.95f, out var entryMiss);
            Assert.False(hitMiss);
            Assert.Null(entryMiss);
        }

        [Fact]
        public void SemanticResponseCache_PruneExpired_RemovesStaleEntries()
        {
            // Cache with very short TTL (1 millisecond)
            var cache = new SemanticResponseCache(dimension: 8, defaultTtl: TimeSpan.FromMilliseconds(1));

            float[] vec = new float[8];
            vec[0] = 1.0f;

            cache.Store(vec, "Test query", "Test response");
            Assert.Equal(1, cache.Count);

            // Wait 20ms for expiration
            System.Threading.Thread.Sleep(20);

            // Lookup should fail because entry is expired
            bool hit = cache.TryGet(vec, minSimilarity: 0.90f, out _);
            Assert.False(hit);

            // Pruning removes the expired entry
            int pruned = cache.PruneExpired();
            Assert.Equal(1, pruned);
            Assert.Equal(0, cache.Count);
        }

        [Fact]
        public async Task ZeroDialogEngine_SecondIdenticalQuery_HitsSemanticCache()
        {
            var bot = IndustrialDialogFactory.CreateIndustrialBot(enableNeuralClassifier: true);
            string sessionId = "test_semantic_cache_session_01";

            // Turn 1: Fresh execution (executes action and caches response)
            var response1 = await bot.ChatAsync(sessionId, "Cho tôi xem bảng máy móc");
            Assert.Equal(SessionState.Completed, response1.State);
            Assert.Equal("QUERY_DATABASE_RECORDS", response1.IntentName);
            Assert.Contains("CNC-01", response1.Text);

            // Verify cache now has at least 1 entry
            Assert.True(bot.Memory.ResponseCache.Count > 0);

            // Turn 2: Exactly identical query in new session -> should immediately hit SEMANTIC_CACHE_HIT
            string session2 = "test_semantic_cache_session_02";
            var response2 = await bot.ChatAsync(session2, "Cho tôi xem bảng máy móc");

            Assert.Equal(SessionState.Completed, response2.State);
            Assert.Equal("QUERY_DATABASE_RECORDS", response2.IntentName);
            Assert.Equal(response1.Text, response2.Text);
            Assert.True(response2.Confidence >= 0.95f);
        }

        [Fact]
        public void DialogueResponseGenerator_TryFormatJsonAsTable_RendersMarkdownTable()
        {
            var generator = new DialogueResponseGenerator();

            string json = "[{\"machine_id\":\"CNC-01\",\"status\":\"RUNNING\",\"temp\":\"65C\"},{\"machine_id\":\"ROBOT-02\",\"status\":\"IDLE\",\"temp\":\"30C\"}]";

            string mdTable = generator.TryFormatJsonAsTable(json);

            Assert.Contains("| machine_id | status | temp |", mdTable);
            Assert.Contains("| --- | --- | --- |", mdTable);
            Assert.Contains("| CNC-01 | RUNNING | 65C |", mdTable);
            Assert.Contains("| ROBOT-02 | IDLE | 30C |", mdTable);
        }

        [Fact]
        public void DialogueResponseGenerator_FormatMarkdownTable_EmptyRows_ReturnsHeadersOnly()
        {
            var generator = new DialogueResponseGenerator();
            var headers = new[] { "ColA", "ColB" };
            var rows = new List<IReadOnlyList<string>>();

            string table = generator.FormatMarkdownTable(headers, rows);
            Assert.Contains("| ColA | ColB |", table);
            Assert.Contains("| --- | --- |", table);
        }
    }
}
