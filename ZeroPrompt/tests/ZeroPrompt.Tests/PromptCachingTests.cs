using System.Collections.Generic;
using Xunit;
using ZeroPrompt.Core.Caching;

namespace ZeroPrompt.Tests
{
    public class PromptCachingTests
    {
        [Fact]
        public void PromptLayoutOptimizer_OrdersSectionsFromStaticToDynamic()
        {
            var optimizer = new PromptLayoutOptimizer();

            // Add sections in mixed / disordered order
            optimizer.AddUserQuery("Báo cáo trạng thái máy CNC")
                     .AddFewShot("Q: Chào -> A: Xin chào!")
                     .AddSystem("You are an industrial supervisory agent.")
                     .AddHistory("User: Hi -> Bot: Hello")
                     .AddTools("db_query_table(name, filter)");

            var layout = optimizer.Optimize();

            // The static prefix must contain System, Tools, and FewShot in sorted order
            Assert.Contains("You are an industrial supervisory agent.", layout.StaticPrefix);
            Assert.Contains("db_query_table", layout.StaticPrefix);
            Assert.Contains("Q: Chào", layout.StaticPrefix);

            // Dynamic Suffix must contain History and UserQuery
            Assert.Contains("User: Hi -> Bot: Hello", layout.DynamicSuffix);
            Assert.Contains("Báo cáo trạng thái máy CNC", layout.DynamicSuffix);

            // Verify prefix hash is deterministic and non-zero
            Assert.NotEqual(0UL, layout.PrefixHash);
            Assert.Equal(3, layout.StaticSectionCount);

            // Optimization should produce the same hash on identical static content
            var optimizer2 = new PromptLayoutOptimizer();
            optimizer2.AddTools("db_query_table(name, filter)")
                      .AddSystem("You are an industrial supervisory agent.")
                      .AddFewShot("Q: Chào -> A: Xin chào!")
                      .AddUserQuery("Câu hỏi khác hoàn toàn");

            var layout2 = optimizer2.Optimize();
            Assert.Equal(layout.PrefixHash, layout2.PrefixHash);
            Assert.Equal(layout.StaticPrefix, layout2.StaticPrefix);
        }

        [Fact]
        public void PrefixRadixCache_MatchesLongestPrefixAndPayload()
        {
            var cache = new PrefixRadixCache<string>();

            // System prompt tokens: [101, 102, 103]
            var systemPrefix = new[] { 101, 102, 103 };
            cache.Insert(systemPrefix, "KV_CACHE_SYSTEM");

            // System + Tools: [101, 102, 103, 201, 202]
            var systemPlusTools = new[] { 101, 102, 103, 201, 202 };
            cache.Insert(systemPlusTools, "KV_CACHE_SYSTEM_TOOLS");

            Assert.Equal(2, cache.Count);

            // Test 1: Query with only system tokens
            var (matched1, payload1) = cache.MatchLongestPrefix(new[] { 101, 102, 103, 999 });
            Assert.Equal(3, matched1);
            Assert.Equal("KV_CACHE_SYSTEM", payload1);

            // Test 2: Query with system + tools + dynamic user tokens
            var (matched2, payload2) = cache.MatchLongestPrefix(new[] { 101, 102, 103, 201, 202, 888, 777 });
            Assert.Equal(5, matched2);
            Assert.Equal("KV_CACHE_SYSTEM_TOOLS", payload2);

            // Test 3: Query with completely unrelated tokens
            var (matched3, payload3) = cache.MatchLongestPrefix(new[] { 999, 888 });
            Assert.Equal(0, matched3);
            Assert.Null(payload3);
        }
    }
}
