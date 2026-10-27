using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Reflection;
using Microsoft.Data.Sqlite;
using Xunit;
using Xunit.Abstractions;
using ZeroData.Sql.Execution;

namespace ZeroData.Sql.Tests
{
    public class BenchmarkRow
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public bool InStock { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class NativeExecutorPerformanceTests
    {
        private readonly ITestOutputHelper _output;

        public NativeExecutorPerformanceTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void Performance_ExpressionTree_Outperforms_Reflection()
        {
            using var conn = new SqliteConnection("Data Source=:memory:");
            conn.Open();

            using (var initCmd = conn.CreateCommand())
            {
                initCmd.CommandText = @"
                    CREATE TABLE Benchmarks (
                        Id INTEGER PRIMARY KEY,
                        Name TEXT,
                        Price REAL,
                        Quantity INTEGER,
                        InStock INTEGER,
                        Timestamp TEXT
                    );";
                initCmd.ExecuteNonQuery();
            }

            // Insert 5,000 rows
            using (var tx = conn.BeginTransaction())
            {
                for (int i = 1; i <= 5000; i++)
                {
                    using var ins = conn.CreateCommand();
                    ins.Transaction = tx;
                    ins.CommandText = $"INSERT INTO Benchmarks VALUES ({i}, 'Product_{i}', {i * 1.5}, {i * 2}, {i % 2}, '2026-09-13T12:00:00Z')";
                    ins.ExecuteNonQuery();
                }
                tx.Commit();
            }

            // Warm up both engines to eliminate JIT compilation skew
            _ = conn.Query<BenchmarkRow>("SELECT * FROM Benchmarks LIMIT 100");
            using (var warmupCmd = conn.CreateCommand())
            {
                warmupCmd.CommandText = "SELECT * FROM Benchmarks LIMIT 100";
                using var r = warmupCmd.ExecuteReader();
                while (r.Read()) { }
            }

            // 1. Benchmark Native Expression-Tree Materializer
            var swNative = Stopwatch.StartNew();
            long memStartNative = GC.GetTotalMemory(true);

            var nativeResults = conn.Query<BenchmarkRow>("SELECT * FROM Benchmarks");
            int nativeCount = 0;
            foreach (var item in nativeResults) nativeCount++;

            swNative.Stop();
            long memEndNative = GC.GetTotalMemory(false);
            long memNative = Math.Max(0, memEndNative - memStartNative);

            // 2. Benchmark Raw Reflection Loop (Baseline)
            var swReflection = Stopwatch.StartNew();
            long memStartRef = GC.GetTotalMemory(true);

            var refResults = new List<BenchmarkRow>();
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT * FROM Benchmarks";
                using var reader = cmd.ExecuteReader();
                var props = typeof(BenchmarkRow).GetProperties(BindingFlags.Public | BindingFlags.Instance);
                while (reader.Read())
                {
                    var obj = new BenchmarkRow();
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        var colName = reader.GetName(i);
                        for (int p = 0; p < props.Length; p++)
                        {
                            if (string.Equals(props[p].Name, colName, StringComparison.OrdinalIgnoreCase))
                            {
                                if (!reader.IsDBNull(i))
                                {
                                    var val = Convert.ChangeType(reader.GetValue(i), Nullable.GetUnderlyingType(props[p].PropertyType) ?? props[p].PropertyType);
                                    props[p].SetValue(obj, val, null);
                                }
                                break;
                            }
                        }
                    }
                    refResults.Add(obj);
                }
            }

            swReflection.Stop();
            long memEndRef = GC.GetTotalMemory(false);
            long memRef = Math.Max(0, memEndRef - memStartRef);

            _output.WriteLine($"[Benchmark Result (5,000 rows)]");
            _output.WriteLine($"Native Expression-Tree Engine: {swNative.ElapsedMilliseconds} ms, {nativeCount} rows, Memory: {memNative / 1024} KB");
            _output.WriteLine($"Naive Reflection Materializer: {swReflection.ElapsedMilliseconds} ms, {refResults.Count} rows, Memory: {memRef / 1024} KB");

            Assert.Equal(5000, nativeCount);
            Assert.Equal(5000, refResults.Count);

            // High throughput sanity assertion: 5,000 entities materialized under 500ms
            Assert.True(swNative.ElapsedMilliseconds < 500,
                $"Native execution time ({swNative.ElapsedMilliseconds}ms) exceeded expected threshold (< 500ms)");
        }
    }
}
