using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Xunit;
using ZeroData.Sql.Execution;

namespace ZeroData.Sql.Tests
{
    public class ConcurrencyPocoA
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public decimal Amount { get; set; }
    }

    public class ConcurrencyPocoB
    {
        public int Id { get; set; }
        public string Description { get; set; }
        public bool IsVerified { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class NativeExecutorConcurrencyTests
    {
        [Fact]
        public async Task ConcurrentMaterialization_100Tasks_NoRaceConditionOrDeadlock()
        {
            // Clear delegate cache to force concurrent compilation
            EntityMaterializer.ClearCache();

            const int concurrency = 100;
            var exceptions = new ConcurrentBag<Exception>();

            var tasks = Enumerable.Range(0, concurrency).Select(async i =>
            {
                try
                {
                    // Each task uses its own in-memory DB connection
                    using var conn = new SqliteConnection($"Data Source=file:memdb_{i}?mode=memory&cache=shared");
                    await conn.OpenAsync();

                    using (var initCmd = conn.CreateCommand())
                    {
                        initCmd.CommandText = @"
                            CREATE TABLE ItemsA (Id INTEGER PRIMARY KEY, Title TEXT, Amount REAL);
                            CREATE TABLE ItemsB (Id INTEGER PRIMARY KEY, Description TEXT, IsVerified INTEGER, Timestamp TEXT);
                            INSERT INTO ItemsA VALUES (1, 'Item 1', 12.50);
                            INSERT INTO ItemsB VALUES (1, 'Desc 1', 1, '2026-09-13T12:00:00Z');
                        ";
                        await initCmd.ExecuteNonQueryAsync();
                    }

                    // Interleave different types and partial schemas across concurrent tasks
                    if (i % 3 == 0)
                    {
                        var items = (await conn.QueryAsync<ConcurrencyPocoA>("SELECT * FROM ItemsA")).ToList();
                        Assert.Single(items);
                        Assert.Equal("Item 1", items[0].Title);
                    }
                    else if (i % 3 == 1)
                    {
                        var items = (await conn.QueryAsync<ConcurrencyPocoB>("SELECT * FROM ItemsB")).ToList();
                        Assert.Single(items);
                        Assert.True(items[0].IsVerified);
                    }
                    else
                    {
                        // Query partial schema of PocoA (Id, Title only)
                        var items = (await conn.QueryAsync<ConcurrencyPocoA>("SELECT Id, Title FROM ItemsA")).ToList();
                        Assert.Single(items);
                        Assert.Equal("Item 1", items[0].Title);
                        Assert.Equal(0m, items[0].Amount);
                    }
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            });

            await Task.WhenAll(tasks);

            Assert.Empty(exceptions);
        }

        [Fact]
        public void ConcurrentParameterBinding_CachedPropertyReflection_ThreadSafe()
        {
            const int concurrency = 50;
            var exceptions = new ConcurrentBag<Exception>();

            Parallel.For(0, concurrency, i =>
            {
                try
                {
                    using var conn = new SqliteConnection("Data Source=:memory:");
                    conn.Open();

                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "SELECT 1";

                    var paramObj = new
                    {
                        TaskIndex = i,
                        CreatedAt = DateTime.UtcNow,
                        Flag = true,
                        Price = 99.99m
                    };

                    FastParameterBinder.Bind(cmd, paramObj);

                    Assert.Equal(4, cmd.Parameters.Count);
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            });

            Assert.Empty(exceptions);
        }
    }
}
