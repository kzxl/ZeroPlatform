using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using ZeroDocuments.Common;
using ZeroDocuments.Csv;
using ZeroDocuments.Excel;

namespace ZeroDocuments.Tests
{
    public class ConcurrencyAndThreadSafetyTests
    {
        public class SamplePoco
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public decimal Amount { get; set; }
            public DateTime CreatedAt { get; set; }
        }

        public class SecondaryPoco
        {
            public Guid Uid { get; set; }
            public string Code { get; set; } = string.Empty;
            public bool IsActive { get; set; }
        }

        [Fact]
        public void PropertyAccessorCache_ConcurrentAccessAcrossThreads_ShouldBeThreadSafe()
        {
            const int threadCount = 16;
            const int iterationsPerThread = 500;
            var exceptions = new ConcurrentBag<Exception>();

            Parallel.For(0, threadCount, _ =>
            {
                try
                {
                    for (int i = 0; i < iterationsPerThread; i++)
                    {
                        var accessors1 = PropertyAccessorCache.GetAccessors(typeof(SamplePoco));
                        Assert.Equal(4, accessors1.Length);

                        var accessors2 = PropertyAccessorCache.GetAccessors(typeof(SecondaryPoco));
                        Assert.Equal(3, accessors2.Length);

                        var poco = new SamplePoco();
                        var idAcc = accessors1.First(a => a.Name == "Id");
                        idAcc.Setter!(poco, i);
                        Assert.Equal(i, poco.Id);
                        Assert.Equal(i, (int)idAcc.Getter(poco)!);
                    }
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            });

            Assert.Empty(exceptions);
        }

        [Fact]
        public async Task ExcelWriterAndReader_ConcurrentParallelGeneration_ShouldProduceValidWorkbooks()
        {
            const int concurrentTasks = 10;
            const int rowsPerTask = 500;

            var tasks = Enumerable.Range(1, concurrentTasks).Select(async taskId =>
            {
                var records = Enumerable.Range(1, rowsPerTask).Select(i => new SamplePoco
                {
                    Id = taskId * 10000 + i,
                    Name = $"Task_{taskId}_Item_{i}",
                    Amount = taskId * 10.5m + i,
                    CreatedAt = DateTime.UtcNow
                }).ToList();

                using var ms = new MemoryStream();
                ExcelWriter.WriteToStream(ms, records, sheetName: $"Sheet_{taskId}");

                Assert.True(ms.Length > 0);

                ms.Position = 0;
                var readBack = ExcelReader.Read<SamplePoco>(ms, sheetName: $"Sheet_{taskId}");
                Assert.Equal(rowsPerTask, readBack.Count);
                Assert.Equal(taskId * 10000 + 1, readBack[0].Id);
                Assert.Equal($"Task_{taskId}_Item_1", readBack[0].Name);

                await Task.Yield();
            });

            await Task.WhenAll(tasks);
        }

        [Fact]
        public async Task CsvWriterAndReader_ConcurrentParallelStreams_ShouldSucceedWithoutDataCorruption()
        {
            const int concurrentTasks = 8;
            const int rowsPerTask = 1000;

            var tasks = Enumerable.Range(1, concurrentTasks).Select(async taskId =>
            {
                var rows = Enumerable.Range(1, rowsPerTask).Select(i => (IReadOnlyList<object?>)new object?[]
                {
                    taskId * 10000 + i,
                    $"CSV_Task_{taskId}_Row_{i}",
                    i * 0.99
                }).ToList();

                using var ms = new MemoryStream();
                CsvWriter.WriteRowsToStream(ms, rows, new[] { "Id", "Title", "Score" });

                Assert.True(ms.Length > 0);

                ms.Position = 0;
                var table = CsvReader.ReadToDataTable(ms);
                Assert.Equal(rowsPerTask, table.Rows.Count);
                Assert.Equal($"CSV_Task_{taskId}_Row_1", table.Rows[0]["Title"]);

                await Task.Yield();
            });

            await Task.WhenAll(tasks);
        }
    }
}
