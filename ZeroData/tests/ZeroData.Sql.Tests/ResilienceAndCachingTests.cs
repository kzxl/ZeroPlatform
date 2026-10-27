using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Xunit;
using ZeroData.Sql.Caching;
using ZeroData.Sql.Mapping;
using ZeroData.Sql.Resilience;

namespace ZeroData.Sql.Tests
{
    [Table(Name = "CachedProducts")]
    public class CachedProduct
    {
        [Column(Name = "Id", IsPrimaryKey = true)]
        public int Id { get; set; }

        [Column(Name = "Name")]
        public string Name { get; set; }

        [Column(Name = "Price")]
        public decimal Price { get; set; }
    }

    public class ResilienceAndCachingTests
    {
        private SqliteConnection CreateDatabase()
        {
            var conn = new SqliteConnection("Data Source=:memory:");
            conn.Open();

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    CREATE TABLE CachedProducts (
                        Id INTEGER PRIMARY KEY,
                        Name TEXT NOT NULL,
                        Price NUMERIC NOT NULL
                    );
                ";
                cmd.ExecuteNonQuery();
            }

            return conn;
        }

        private void SeedProducts(SqlContext db)
        {
            var items = new List<CachedProduct>
            {
                new CachedProduct { Id = 1, Name = "Laptop", Price = 1200m },
                new CachedProduct { Id = 2, Name = "Mouse", Price = 30m },
                new CachedProduct { Id = 3, Name = "Keyboard", Price = 80m },
            };
            db.BulkInsert(items);
        }

        #region RetryPolicy Unit Tests

        [Fact]
        public void RetryPolicy_SuccessfulOperation_ReturnsResultDirectly()
        {
            var policy = new RetryPolicy { MaxRetryCount = 3 };
            int calls = 0;

            var result = policy.Execute(() =>
            {
                calls++;
                return 42;
            });

            Assert.Equal(42, result);
            Assert.Equal(1, calls);
        }

        [Fact]
        public void RetryPolicy_TransientException_RetriesAndSucceeds()
        {
            var policy = new RetryPolicy
            {
                MaxRetryCount = 3,
                InitialDelay = TimeSpan.FromMilliseconds(5)
            };
            int attempts = 0;

            var result = policy.Execute(() =>
            {
                attempts++;
                if (attempts < 3)
                    throw new TimeoutException("Database lock timeout");
                return "success";
            });

            Assert.Equal("success", result);
            Assert.Equal(3, attempts);
        }

        [Fact]
        public void RetryPolicy_NonTransientException_ThrowsImmediatelyWithoutRetry()
        {
            var policy = new RetryPolicy
            {
                MaxRetryCount = 3,
                InitialDelay = TimeSpan.FromMilliseconds(5)
            };
            int attempts = 0;

            Assert.Throws<ArgumentNullException>(() =>
            {
                policy.Execute<string>(() =>
                {
                    attempts++;
                    throw new ArgumentNullException("param");
                });
            });

            Assert.Equal(1, attempts);
        }

        [Fact]
        public void RetryPolicy_ExhaustsRetries_ThrowsLastTransientException()
        {
            var policy = new RetryPolicy
            {
                MaxRetryCount = 2,
                InitialDelay = TimeSpan.FromMilliseconds(5)
            };
            int attempts = 0;

            var ex = Assert.Throws<TimeoutException>(() =>
            {
                policy.Execute<int>(() =>
                {
                    attempts++;
                    throw new TimeoutException("Deadlock detected");
                });
            });

            Assert.Contains("Deadlock", ex.Message);
            Assert.Equal(3, attempts); // 1 initial + 2 retries
        }

        [Fact]
        public async Task RetryPolicy_ExecuteAsync_RetriesAndSucceeds()
        {
            var policy = new RetryPolicy
            {
                MaxRetryCount = 3,
                InitialDelay = TimeSpan.FromMilliseconds(5)
            };
            int attempts = 0;

            var result = await policy.ExecuteAsync(async ct =>
            {
                attempts++;
                await Task.Yield();
                if (attempts < 2)
                    throw new TimeoutException("Network timeout");
                return 999;
            });

            Assert.Equal(999, result);
            Assert.Equal(2, attempts);
        }

        [Fact]
        public void RetryPolicy_CustomPredicate_RecognizesCustomTransientException()
        {
            var policy = new RetryPolicy
            {
                MaxRetryCount = 2,
                InitialDelay = TimeSpan.FromMilliseconds(5),
                TransientPredicate = ex => ex.Message.Contains("CustomTransient")
            };
            int attempts = 0;

            var result = policy.Execute(() =>
            {
                attempts++;
                if (attempts == 1)
                    throw new InvalidOperationException("CustomTransient error");
                return "ok";
            });

            Assert.Equal("ok", result);
            Assert.Equal(2, attempts);
        }

        #endregion

        #region MemoryL2QueryCache Unit Tests

        [Fact]
        public void MemoryL2QueryCache_SetAndTryGet_ReturnsCachedValue()
        {
            var cache = new MemoryL2QueryCache();
            cache.Set("test_key", new List<string> { "item1", "item2" }, TimeSpan.FromMinutes(1));

            var found = cache.TryGet<List<string>>("test_key", out var result);

            Assert.True(found);
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
        }

        [Fact]
        public async Task MemoryL2QueryCache_ExpiredEntry_ReturnsFalse()
        {
            var cache = new MemoryL2QueryCache();
            cache.Set("short_key", "value", TimeSpan.FromMilliseconds(20));

            await Task.Delay(50);

            var found = cache.TryGet<string>("short_key", out var result);

            Assert.False(found);
            Assert.Null(result);
        }

        [Fact]
        public void MemoryL2QueryCache_InvalidateByType_EvictsOnlyTaggedEntries()
        {
            var cache = new MemoryL2QueryCache();
            cache.Set("key1", "prod_val", TimeSpan.FromMinutes(1), new[] { typeof(CachedProduct) });
            cache.Set("key2", "other_val", TimeSpan.FromMinutes(1), new[] { typeof(string) });

            cache.Invalidate(typeof(CachedProduct));

            Assert.False(cache.TryGet<string>("key1", out _));
            Assert.True(cache.TryGet<string>("key2", out var val2));
            Assert.Equal("other_val", val2);
        }

        [Fact]
        public void MemoryL2QueryCache_InvalidateAll_ClearsAllEntries()
        {
            var cache = new MemoryL2QueryCache();
            cache.Set("k1", 1, TimeSpan.FromMinutes(1));
            cache.Set("k2", 2, TimeSpan.FromMinutes(1));

            cache.InvalidateAll();

            Assert.False(cache.TryGet<int>("k1", out _));
            Assert.False(cache.TryGet<int>("k2", out _));
        }

        #endregion

        #region SqlContext + Table<T> L2 Cache Integration Tests

        [Fact]
        public void SqlContext_FromCache_ServesFromCacheWithoutQueryingDatabase()
        {
            using var conn = CreateDatabase();
            using var db = new SqlContext(conn);
            db.QueryCache = new MemoryL2QueryCache();
            SeedProducts(db);

            // First query fills the cache
            var firstQuery = db.GetTable<CachedProduct>()
                .FromCache(TimeSpan.FromMinutes(5))
                .Where(p => p.Price > 50m);

            Assert.Equal(2, firstQuery.Count);

            // Directly modify the raw DB table bypassing SqlContext
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "UPDATE CachedProducts SET Price = 10.0 WHERE Id = 1";
                cmd.ExecuteNonQuery();
            }

            // Second identical query uses cache — should still see original Price > 50 result!
            var secondQuery = db.GetTable<CachedProduct>()
                .FromCache(TimeSpan.FromMinutes(5))
                .Where(p => p.Price > 50m);

            Assert.Equal(2, secondQuery.Count);

            // Now query WITHOUT cache — should see only 1 product (Keyboard)
            var liveQuery = db.GetTable<CachedProduct>()
                .Where(p => p.Price > 50m);

            Assert.Single(liveQuery);
        }

        [Fact]
        public async Task SqlContext_AsCached_ServesFromCacheAsync()
        {
            using var conn = CreateDatabase();
            using var db = new SqlContext(conn);
            db.QueryCache = new MemoryL2QueryCache();
            SeedProducts(db);

            var first = await db.GetTable<CachedProduct>()
                .AsCached()
                .WhereAsync(p => p.Price > 50m);

            Assert.Equal(2, first.Count);

            // Modify database directly
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "UPDATE CachedProducts SET Price = 10.0 WHERE Id = 1";
                cmd.ExecuteNonQuery();
            }

            // Cached async call still sees cached result
            var cached = await db.GetTable<CachedProduct>()
                .AsCached()
                .WhereAsync(p => p.Price > 50m);

            Assert.Equal(2, cached.Count);
        }

        [Fact]
        public void SqlContext_SubmitChanges_AutomaticallyInvalidatesL2Cache()
        {
            using var conn = CreateDatabase();
            using var db = new SqlContext(conn);
            db.QueryCache = new MemoryL2QueryCache();
            SeedProducts(db);

            // Prime cache
            var initial = db.GetTable<CachedProduct>().AsCached().ToList();
            Assert.Equal(3, initial.Count);

            // Add product and submit changes
            db.Insert(new CachedProduct { Id = 4, Name = "Monitor", Price = 400m });
            db.SubmitChanges();

            // Cache was automatically invalidated by SubmitChanges!
            var refreshed = db.GetTable<CachedProduct>().AsCached().ToList();
            Assert.Equal(4, refreshed.Count);
            Assert.Contains(refreshed, p => p.Name == "Monitor");
        }

        [Fact]
        public void SqlContext_UpdateWhere_AutomaticallyInvalidatesL2Cache()
        {
            using var conn = CreateDatabase();
            using var db = new SqlContext(conn);
            db.QueryCache = new MemoryL2QueryCache();
            SeedProducts(db);

            // Prime cache
            var cachedBefore = db.GetTable<CachedProduct>()
                .FromCache(TimeSpan.FromMinutes(1))
                .Where(p => p.Name == "Laptop");
            Assert.Equal(1200m, cachedBefore[0].Price);

            // Update directly on server
            db.UpdateWhere<CachedProduct>(p => p.Name == "Laptop", new { Price = 1500m });

            // Cache was evicted automatically by UpdateWhere!
            var cachedAfter = db.GetTable<CachedProduct>()
                .FromCache(TimeSpan.FromMinutes(1))
                .Where(p => p.Name == "Laptop");

            Assert.Equal(1500m, cachedAfter[0].Price);
        }

        [Fact]
        public void SqlContext_DeleteWhere_AutomaticallyInvalidatesL2Cache()
        {
            using var conn = CreateDatabase();
            using var db = new SqlContext(conn);
            db.QueryCache = new MemoryL2QueryCache();
            SeedProducts(db);

            // Prime cache
            var cachedBefore = db.GetTable<CachedProduct>().AsCached().ToList();
            Assert.Equal(3, cachedBefore.Count);

            // Delete on server
            db.DeleteWhere<CachedProduct>(p => p.Id == 1);

            // Cache was evicted automatically!
            var cachedAfter = db.GetTable<CachedProduct>().AsCached().ToList();
            Assert.Equal(2, cachedAfter.Count);
        }

        [Fact]
        public void SqlContext_BulkOperations_AutomaticallyInvalidatesL2Cache()
        {
            using var conn = CreateDatabase();
            using var db = new SqlContext(conn);
            db.QueryCache = new MemoryL2QueryCache();
            SeedProducts(db);

            // Prime cache
            var initial = db.GetTable<CachedProduct>().AsCached().ToList();
            Assert.Equal(3, initial.Count);

            // Bulk Insert
            db.BulkInsert(new[] { new CachedProduct { Id = 5, Name = "Headset", Price = 60m } });

            // Cache was evicted
            var updated = db.GetTable<CachedProduct>().AsCached().ToList();
            Assert.Equal(4, updated.Count);
        }

        [Fact]
        public void SqlContext_ExecuteWithRetry_ExecutesOperationSuccessfully()
        {
            using var conn = CreateDatabase();
            using var db = new SqlContext(conn);
            db.RetryPolicy = new RetryPolicy
            {
                MaxRetryCount = 2,
                InitialDelay = TimeSpan.FromMilliseconds(5)
            };

            int calls = 0;
            var result = db.ExecuteWithRetry(() =>
            {
                calls++;
                if (calls == 1)
                    throw new TimeoutException("Lock timeout");
                return "operation_success";
            });

            Assert.Equal("operation_success", result);
            Assert.Equal(2, calls);
        }

        [Fact]
        public void RetryPolicy_WithJitterAndOnRetry_CallsOnRetryWithJitteredDelay()
        {
            var retries = new List<(Exception ex, TimeSpan delay, int attempt)>();
            var policy = new RetryPolicy
            {
                MaxRetryCount = 3,
                InitialDelay = TimeSpan.FromMilliseconds(50),
                EnableJitter = true,
                OnRetry = (ex, delay, attempt) => retries.Add((ex, delay, attempt))
            };

            int calls = 0;
            var result = policy.Execute(() =>
            {
                calls++;
                if (calls <= 2)
                    throw new TimeoutException("Database connection timeout");
                return "completed";
            });

            Assert.Equal("completed", result);
            Assert.Equal(3, calls);
            Assert.Equal(2, retries.Count);

            // Attempt 1: 50ms * [0.8..1.2] = [40ms..60ms]
            Assert.Equal(1, retries[0].attempt);
            Assert.InRange(retries[0].delay.TotalMilliseconds, 35, 65);

            // Attempt 2: 100ms * [0.8..1.2] = [80ms..120ms]
            Assert.Equal(2, retries[1].attempt);
            Assert.InRange(retries[1].delay.TotalMilliseconds, 75, 125);
        }

        [Fact]
        public void RetryPolicy_DisableJitter_DelayMatchesMultiplierExactly()
        {
            var retries = new List<(Exception ex, TimeSpan delay, int attempt)>();
            var policy = new RetryPolicy
            {
                MaxRetryCount = 2,
                InitialDelay = TimeSpan.FromMilliseconds(20),
                BackoffMultiplier = 2.0,
                EnableJitter = false,
                OnRetry = (ex, delay, attempt) => retries.Add((ex, delay, attempt))
            };

            int calls = 0;
            policy.Execute(() =>
            {
                calls++;
                if (calls <= 2)
                    throw new TimeoutException("Temporary failure");
                return true;
            });

            Assert.Equal(2, retries.Count);
            Assert.Equal(TimeSpan.FromMilliseconds(20), retries[0].delay);
            Assert.Equal(TimeSpan.FromMilliseconds(40), retries[1].delay);
        }

        [Fact]
        public void MemoryL2QueryCache_MaxCapacity_EvictsEarliestExpiringWhenFull()
        {
            var cache = new MemoryL2QueryCache { MaxCapacity = 3 };

            cache.Set("k1", 1, TimeSpan.FromMinutes(5));
            cache.Set("k2", 2, TimeSpan.FromMinutes(1)); // earliest expiry
            cache.Set("k3", 3, TimeSpan.FromMinutes(10));

            Assert.Equal(3, cache.Count);

            // Adding 4th item should evict k2 (earliest expiry)
            cache.Set("k4", 4, TimeSpan.FromMinutes(15));

            Assert.Equal(3, cache.Count);
            Assert.False(cache.TryGet<int>("k2", out _));
            Assert.True(cache.TryGet<int>("k1", out _));
            Assert.True(cache.TryGet<int>("k3", out _));
            Assert.True(cache.TryGet<int>("k4", out _));
        }

        [Fact]
        public async Task MemoryL2QueryCache_PruneExpired_PurgesOnlyExpiredEntries()
        {
            var cache = new MemoryL2QueryCache();
            cache.Set("fast_exp", 1, TimeSpan.FromMilliseconds(20));
            cache.Set("slow_exp", 2, TimeSpan.FromMinutes(5));

            Assert.Equal(2, cache.Count);
            await Task.Delay(50);

            int pruned = cache.PruneExpired();

            Assert.Equal(1, pruned);
            Assert.Equal(1, cache.Count);
            Assert.False(cache.TryGet<int>("fast_exp", out _));
            Assert.True(cache.TryGet<int>("slow_exp", out _));
        }

        #endregion
    }
}
