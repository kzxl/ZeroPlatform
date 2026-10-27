using System;
using System.Diagnostics;
using System.Linq.Expressions;
using ZeroData.Sql.Sql;
using Xunit;

namespace ZeroData.Sql.Tests
{
    [CollectionDefinition("ExpressionCache", DisableParallelization = true)]
    public class ExpressionCacheCollection
    {
    }

    [Collection("ExpressionCache")]
    public class ExpressionCacheTests
    {
        public ExpressionCacheTests()
        {
            // Clear cache before each test
            ExpressionCache.Clear();
        }

        [Fact]
        public void GetOrAdd_SingleParameter_CachesCompiledExpression()
        {
            // Arrange
            Expression<Func<int, int>> expr = x => x * 2;

            // Act
            var func1 = ExpressionCache.GetOrAdd(expr);
            var func2 = ExpressionCache.GetOrAdd(expr);

            // Assert
            Assert.Same(func1, func2); // Should return same cached instance
            Assert.Equal(4, func1(2));
            Assert.Equal(4, func2(2));
        }

        [Fact]
        public void GetOrAdd_TwoParameters_CachesCompiledExpression()
        {
            // Arrange
            Expression<Func<int, int, int>> expr = (x, y) => x + y;

            // Act
            var func1 = ExpressionCache.GetOrAdd(expr);
            var func2 = ExpressionCache.GetOrAdd(expr);

            // Assert
            Assert.Same(func1, func2);
            Assert.Equal(5, func1(2, 3));
            Assert.Equal(5, func2(2, 3));
        }

        [Fact]
        public void GetOrAdd_DifferentExpressions_CachesSeparately()
        {
            // Arrange
            Expression<Func<int, int>> expr1 = x => x * 2;
            Expression<Func<int, int>> expr2 = x => x * 3;

            // Act
            var func1 = ExpressionCache.GetOrAdd(expr1);
            var func2 = ExpressionCache.GetOrAdd(expr2);

            // Assert
            Assert.NotSame(func1, func2);
            Assert.Equal(4, func1(2));
            Assert.Equal(6, func2(2));
        }

        [Fact]
        public void GetOrAdd_SameExpressionDifferentInstances_UsesCache()
        {
            // Arrange - Create two separate expression instances with same logic
            Expression<Func<int, bool>> expr1 = x => x > 10;
            Expression<Func<int, bool>> expr2 = x => x > 10;

            // Act
            var func1 = ExpressionCache.GetOrAdd(expr1);
            var func2 = ExpressionCache.GetOrAdd(expr2);

            // Assert - Should be cached because ToString() is the same
            Assert.Same(func1, func2);
            Assert.True(func1(15));
            Assert.False(func1(5));
        }

        [Fact]
        public void GetOrAdd_NullExpression_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                ExpressionCache.GetOrAdd<int, int>(null));
        }

        [Fact]
        public void GetStats_InitialState_ReturnsZeros()
        {
            // Act
            var stats = ExpressionCache.GetStats();

            // Assert
            Assert.Equal(0, stats.Hits);
            Assert.Equal(0, stats.Misses);
            Assert.Equal(0, stats.Total);
            Assert.Equal(0, stats.HitRate);
            Assert.Equal(0, stats.CacheSize);
        }

        [Fact]
        public void GetStats_AfterCacheHit_ReturnsCorrectStats()
        {
            // Arrange
            Expression<Func<int, int>> expr = x => x * 2;

            // Act
            ExpressionCache.GetOrAdd(expr); // Miss
            ExpressionCache.GetOrAdd(expr); // Hit
            ExpressionCache.GetOrAdd(expr); // Hit
            var stats = ExpressionCache.GetStats();

            // Assert
            Assert.Equal(2, stats.Hits);
            Assert.Equal(1, stats.Misses);
            Assert.Equal(3, stats.Total);
            Assert.Equal(2.0 / 3.0, stats.HitRate, 2);
            Assert.Equal(1, stats.CacheSize);
        }

        [Fact]
        public void GetStats_MultipleDifferentExpressions_TracksCorrectly()
        {
            // Arrange
            Expression<Func<int, int>> expr1 = x => x * 2;
            Expression<Func<int, int>> expr2 = x => x * 3;
            Expression<Func<int, int>> expr3 = x => x * 4;

            // Act
            ExpressionCache.GetOrAdd(expr1); // Miss
            ExpressionCache.GetOrAdd(expr1); // Hit
            ExpressionCache.GetOrAdd(expr2); // Miss
            ExpressionCache.GetOrAdd(expr3); // Miss
            ExpressionCache.GetOrAdd(expr2); // Hit
            ExpressionCache.GetOrAdd(expr1); // Hit
            var stats = ExpressionCache.GetStats();

            // Assert
            Assert.Equal(3, stats.Hits);
            Assert.Equal(3, stats.Misses);
            Assert.Equal(6, stats.Total);
            Assert.Equal(0.5, stats.HitRate);
            Assert.Equal(3, stats.CacheSize);
        }

        [Fact]
        public void Clear_RemovesAllCachedExpressions()
        {
            // Arrange
            Expression<Func<int, int>> expr = x => x * 2;
            ExpressionCache.GetOrAdd(expr);
            var statsBefore = ExpressionCache.GetStats();

            // Act
            ExpressionCache.Clear();
            var statsAfter = ExpressionCache.GetStats();

            // Assert
            Assert.Equal(1, statsBefore.CacheSize);
            Assert.Equal(0, statsAfter.CacheSize);
            Assert.Equal(0, statsAfter.Hits);
            Assert.Equal(0, statsAfter.Misses);
        }

        [Fact]
        public void GetOrAdd_ComplexExpression_CachesCorrectly()
        {
            // Arrange
            Expression<Func<Person, bool>> expr = p =>
                p.Age > 18 && p.Name.StartsWith("A") && p.IsActive;

            // Act
            var func1 = ExpressionCache.GetOrAdd(expr);
            var func2 = ExpressionCache.GetOrAdd(expr);

            var person = new Person { Age = 25, Name = "Alice", IsActive = true };

            // Assert
            Assert.Same(func1, func2);
            Assert.True(func1(person));
        }

        [Fact]
        public void GetOrAdd_WithClosureVariable_CachesBasedOnExpressionStructure()
        {
            // Arrange
            int threshold = 10;
            Expression<Func<int, bool>> expr1 = x => x > threshold;

            threshold = 20; // Change closure variable
            Expression<Func<int, bool>> expr2 = x => x > threshold;

            // Act
            var func1 = ExpressionCache.GetOrAdd(expr1);
            var func2 = ExpressionCache.GetOrAdd(expr2);

            // Assert - Both expressions have same structure, so they're cached together
            // This is expected behavior - cache key is based on expression.ToString()
            Assert.Same(func1, func2);
        }

        [Fact]
        public void Performance_CachedVsUncached_ShowsSignificantImprovement()
        {
            // Arrange
            Expression<Func<int, int>> expr = x => x * 2 + x * 3 + x * 4;
            const int iterations = 10000;

            // Warm up
            ExpressionCache.Clear();
            ExpressionCache.GetOrAdd(expr);

            // Act - Measure cached performance
            var sw1 = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                var func = ExpressionCache.GetOrAdd(expr);
                func(i);
            }
            sw1.Stop();

            // Act - Measure uncached performance (compile every time)
            var sw2 = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                var func = expr.Compile();
                func(i);
            }
            sw2.Stop();

            // Assert - Cached should be significantly faster
            var cachedMs = sw1.ElapsedMilliseconds;
            var uncachedMs = sw2.ElapsedMilliseconds;
            var improvement = (double)uncachedMs / cachedMs;

            // Cache should be at least 10x faster (typically 50-100x)
            Assert.True(improvement > 10,
                $"Expected >10x improvement, got {improvement:F2}x (cached: {cachedMs}ms, uncached: {uncachedMs}ms)");
        }

        [Fact]
        public void GetStats_ToString_FormatsCorrectly()
        {
            // Arrange
            Expression<Func<int, int>> expr = x => x * 2;
            ExpressionCache.GetOrAdd(expr); // Miss
            ExpressionCache.GetOrAdd(expr); // Hit
            ExpressionCache.GetOrAdd(expr); // Hit

            // Act
            var stats = ExpressionCache.GetStats();
            var str = stats.ToString();

            // Assert
            Assert.Contains("Hits: 2", str);
            Assert.Contains("Misses: 1", str);
            Assert.Contains("Hit Rate: 66", str); // 66.67%
            Assert.Contains("Cache Size: 1", str);
        }

        [Fact]
        public void GetOrAdd_ThreadSafe_HandlesMultipleThreads()
        {
            // Arrange
            Expression<Func<int, int>> expr = x => x * 2;
            const int threadCount = 10;
            const int iterationsPerThread = 1000;

            // Act - Multiple threads accessing cache simultaneously
            var tasks = new System.Threading.Tasks.Task[threadCount];
            for (int i = 0; i < threadCount; i++)
            {
                tasks[i] = System.Threading.Tasks.Task.Run(() =>
                {
                    for (int j = 0; j < iterationsPerThread; j++)
                    {
                        var func = ExpressionCache.GetOrAdd(expr);
                        Assert.Equal(4, func(2));
                    }
                });
            }

            System.Threading.Tasks.Task.WaitAll(tasks);
            var stats = ExpressionCache.GetStats();

            // Assert - Should have at least 1 miss and total operations match
            Assert.Equal(1, stats.CacheSize);
            Assert.True(stats.Misses >= 1, $"Expected at least 1 miss, got {stats.Misses}");
            Assert.Equal(threadCount * iterationsPerThread, stats.Hits + stats.Misses);
        }

        private class Person
        {
            public int Age { get; set; }
            public string Name { get; set; }
            public bool IsActive { get; set; }
        }
    }
}
