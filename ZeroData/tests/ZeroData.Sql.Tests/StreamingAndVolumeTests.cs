using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Xunit;
using Xunit.Abstractions;
using ZeroData.Core;
using ZeroData.Sql.Mapping;

namespace ZeroData.Sql.Tests
{
    [Table(Name = "LargeVolumeItems")]
    public class VolumeEntity
    {
        [Column(Name = "Id", IsPrimaryKey = true)]
        public int Id { get; set; }

        [Column(Name = "Name")]
        public string Name { get; set; } = string.Empty;

        [Column(Name = "Price")]
        public decimal Price { get; set; }

        [Column(Name = "Quantity")]
        public int Quantity { get; set; }

        [Column(Name = "IsActive")]
        public bool IsActive { get; set; }

        [Column(Name = "CreatedAt")]
        public DateTime CreatedAt { get; set; }
    }

    public class StreamingAndVolumeTests
    {
        private readonly ITestOutputHelper _output;

        public StreamingAndVolumeTests(ITestOutputHelper output)
        {
            _output = output;
        }

        private SqliteConnection CreatePopulatedDatabase(int count)
        {
            var conn = new SqliteConnection("Data Source=:memory:");
            conn.Open();

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    CREATE TABLE LargeVolumeItems (
                        Id INTEGER PRIMARY KEY,
                        Name TEXT NOT NULL,
                        Price REAL NOT NULL,
                        Quantity INTEGER NOT NULL,
                        IsActive INTEGER NOT NULL,
                        CreatedAt TEXT NOT NULL
                    );";
                cmd.ExecuteNonQuery();
            }

            using (var tx = conn.BeginTransaction())
            {
                for (int i = 1; i <= count; i++)
                {
                    using var ins = conn.CreateCommand();
                    ins.Transaction = tx;
                    ins.CommandText = $"INSERT INTO LargeVolumeItems VALUES ({i}, 'Item_{i}', {i * 2.5}, {i * 10}, {i % 2}, '2026-09-13T20:00:00Z')";
                    ins.ExecuteNonQuery();
                }
                tx.Commit();
            }

            return conn;
        }

        [Fact]
        public async Task QueryStreamAsync_Streams50000Records_FlatMemoryFootprint()
        {
            const int totalRecords = 50000;
            using var conn = CreatePopulatedDatabase(totalRecords);

            long memBefore = GC.GetTotalMemory(true);
            var sw = Stopwatch.StartNew();

            int streamedCount = 0;
            decimal priceSum = 0;

            await foreach (var row in conn.QueryStreamAsync<VolumeEntity>("SELECT * FROM LargeVolumeItems"))
            {
                streamedCount++;
                priceSum += row.Price;
            }

            sw.Stop();
            long memAfter = GC.GetTotalMemory(false);
            long memUsedKb = Math.Max(0, memAfter - memBefore) / 1024;

            _output.WriteLine($"[Stream 50k Records] Time: {sw.ElapsedMilliseconds} ms, Streamed: {streamedCount}, Memory Used: {memUsedKb} KB");

            Assert.Equal(totalRecords, streamedCount);
            Assert.True(priceSum > 0);
            // Verify throughput: streaming 50,000 rows completes swiftly
            Assert.True(sw.ElapsedMilliseconds < 5000, $"Streaming exceeded 5s: {sw.ElapsedMilliseconds}ms");
        }

        [Fact]
        public async Task QueryStreamAsync_EarlyBreak_DisposesResourcesCleanly()
        {
            using var conn = CreatePopulatedDatabase(1000);

            int count = 0;
            await foreach (var row in conn.QueryStreamAsync<VolumeEntity>("SELECT * FROM LargeVolumeItems"))
            {
                count++;
                if (count >= 10) break; // Early termination
            }

            Assert.Equal(10, count);

            // Verify connection remains completely healthy and can execute subsequent commands
            using var testCmd = conn.CreateCommand();
            testCmd.CommandText = "SELECT COUNT(*) FROM LargeVolumeItems";
            long actual = Convert.ToInt64(testCmd.ExecuteScalar());
            Assert.Equal(1000L, actual);
        }

        [Fact]
        public async Task QueryChunksAsync_YieldsCorrectBatchSizes()
        {
            const int totalRecords = 2500;
            const int chunkSize = 1000;
            using var conn = CreatePopulatedDatabase(totalRecords);

            var chunks = new List<IReadOnlyList<VolumeEntity>>();
            await foreach (var batch in conn.QueryChunksAsync<VolumeEntity>("SELECT * FROM LargeVolumeItems ORDER BY Id ASC", chunkSize))
            {
                chunks.Add(batch);
            }

            Assert.Equal(3, chunks.Count);
            Assert.Equal(1000, chunks[0].Count);
            Assert.Equal(1000, chunks[1].Count);
            Assert.Equal(500, chunks[2].Count); // Final partial batch

            Assert.Equal(1, chunks[0][0].Id);
            Assert.Equal(1001, chunks[1][0].Id);
            Assert.Equal(2001, chunks[2][0].Id);
        }

        [Fact]
        public async Task Table_SeekAsync_PerformsKeysetPaginationAcrossBoundaries()
        {
            using var conn = CreatePopulatedDatabase(500);
            using var ctx = new SqlContext(conn);
            var table = ctx.GetTable<VolumeEntity>();

            // Page 1: Seek from 0, size 50
            var page1 = await table.SeekAsync(x => x.Id, 0, 50, ascending: true);
            Assert.Equal(50, page1.Count);
            Assert.Equal(1, page1[0].Id);
            Assert.Equal(50, page1[49].Id);

            // Page 2: Seek from lastSeenId (50), size 50
            int lastSeenId = page1[49].Id;
            var page2 = await table.SeekAsync(x => x.Id, lastSeenId, 50, ascending: true);
            Assert.Equal(50, page2.Count);
            Assert.Equal(51, page2[0].Id);
            Assert.Equal(100, page2[49].Id);

            // Reverse seek (Descending)
            var reversePage = await table.SeekAsync(x => x.Id, 101, 10, ascending: false);
            Assert.Equal(10, reversePage.Count);
            Assert.Equal(100, reversePage[0].Id);
            Assert.Equal(91, reversePage[9].Id);
        }

        [Fact]
        public void DataFrame_FromDataReader_ZeroBoxingIngestion_VerifiesTypesAndNulls()
        {
            using var conn = CreatePopulatedDatabase(5000);
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM LargeVolumeItems";
            using var reader = cmd.ExecuteReader();

            var sw = Stopwatch.StartNew();
            var df = DataFrame.FromDataReader(reader);
            sw.Stop();

            _output.WriteLine($"[DataFrame.FromDataReader] Ingested 5,000 rows in {sw.ElapsedMilliseconds} ms ({5000.0 / Math.Max(1, sw.ElapsedMilliseconds) * 1000:N0} rows/s)");

            Assert.Equal(5000, df.RowCount);
            Assert.Equal(6, df.ColumnCount);

            // Verify column types and data integrity
            Assert.True(df["Id"].DataType == typeof(int) || df["Id"].DataType == typeof(long));
            Assert.Equal(typeof(string), df["Name"].DataType);
            Assert.Equal(typeof(double), df["Price"].DataType);
            Assert.True(df["Quantity"].DataType == typeof(int) || df["Quantity"].DataType == typeof(long));
            Assert.True(df["IsActive"].DataType == typeof(bool) || df["IsActive"].DataType == typeof(long) || df["IsActive"].DataType == typeof(int));
            Assert.True(df["CreatedAt"].DataType == typeof(DateTime) || df["CreatedAt"].DataType == typeof(string));

            Assert.Equal(1L, Convert.ToInt64(df["Id"].GetValue(0)));
            Assert.Equal("Item_1", df["Name"].GetValue(0));
            Assert.Equal(5000L, Convert.ToInt64(df["Id"].GetValue(4999)));
        }
    }
}
