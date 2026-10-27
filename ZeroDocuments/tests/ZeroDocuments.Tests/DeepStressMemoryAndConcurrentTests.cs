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
    public class DeepStressMemoryAndConcurrentTests
    {
        public class StressOrderDto
        {
            public int OrderId { get; set; }
            public string CustomerCode { get; set; } = string.Empty;
            public decimal TotalAmount { get; set; }
            public DateTime OrderDate { get; set; }
            public bool IsShipped { get; set; }
        }

        [Fact]
        public async Task ConcurrentHighThroughput_ParallelExcelAndCsvGeneration_16Threads_ShouldSucceed()
        {
            const int threadCount = 16;
            const int rowsPerThread = 1000;
            var exceptions = new ConcurrentBag<Exception>();

            var tasks = Enumerable.Range(1, threadCount).Select(async threadId =>
            {
                await Task.Yield(); // Force async dispatch to threadpool

                try
                {
                    var items = new List<StressOrderDto>(rowsPerThread);
                    for (int i = 1; i <= rowsPerThread; i++)
                    {
                        items.Add(new StressOrderDto
                        {
                            OrderId = threadId * 100000 + i,
                            CustomerCode = $"CUST-{threadId:D2}-{i:D4}",
                            TotalAmount = (decimal)(i * 1.5),
                            OrderDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMinutes(i),
                            IsShipped = (i % 2) == 0
                        });
                    }

                    // 1. Excel Generation & Readback
                    using var excelMs = new MemoryStream();
                    ExcelWriter.WriteToStream(excelMs, items, sheetName: $"Orders_T{threadId}");
                    Assert.True(excelMs.Length > 0);

                    excelMs.Position = 0;
                    var readBackOrders = ExcelReader.Read<StressOrderDto>(excelMs, sheetName: $"Orders_T{threadId}");
                    Assert.Equal(rowsPerThread, readBackOrders.Count);
                    Assert.Equal(threadId * 100000 + 1, readBackOrders[0].OrderId);
                    Assert.Equal($"CUST-{threadId:D2}-0001", readBackOrders[0].CustomerCode);

                    // 2. CSV Generation & Readback
                    using var csvMs = new MemoryStream();
                    CsvWriter.WriteToStream(csvMs, items);
                    Assert.True(csvMs.Length > 0);

                    csvMs.Position = 0;
                    var csvTable = CsvReader.ReadToDataTable(csvMs);
                    Assert.Equal(rowsPerThread, csvTable.Rows.Count);
                    Assert.Equal((threadId * 100000 + 1).ToString(), csvTable.Rows[0]["OrderId"]?.ToString());
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
        public void StreamReuse_LeaveOpenTrue_ShouldAllowMultipleSequentialReads()
        {
            var items = new List<StressOrderDto>
            {
                new StressOrderDto { OrderId = 1, CustomerCode = "C1", TotalAmount = 100m, OrderDate = DateTime.UtcNow, IsShipped = true },
                new StressOrderDto { OrderId = 2, CustomerCode = "C2", TotalAmount = 200m, OrderDate = DateTime.UtcNow, IsShipped = false }
            };

            using var ms = new MemoryStream();
            ExcelWriter.WriteToStream(ms, items, sheetName: "ReuseSheet");

            // 1. First read: StreamRows
            ms.Position = 0;
            var streamRows = ExcelReader.StreamRows(ms, sheetName: "ReuseSheet").ToList();
            Assert.Equal(3, streamRows.Count); // 1 header + 2 data rows
            Assert.True(ms.CanRead, "MemoryStream must still be open after StreamRows.");

            // 2. Second read: ReadToDataTable
            ms.Position = 0;
            var dataTable = ExcelReader.ReadToDataTable(ms, sheetName: "ReuseSheet");
            Assert.True(ms.CanRead, "MemoryStream must still be open after ReadToDataTable.");
            Assert.Equal(3, dataTable.Rows.Count);

            // 3. Third read: Read<T>
            ms.Position = 0;
            var pocoList = ExcelReader.Read<StressOrderDto>(ms, sheetName: "ReuseSheet");
            Assert.True(ms.CanRead, "MemoryStream must still be open after Read<T>.");
            Assert.Equal(2, pocoList.Count);
        }

        [Fact]
        public void LargeDataset_PartialStreamingEarlyBreak_ShouldReleaseImmediately()
        {
            const int totalRows = 25000;
            var rows = new List<IReadOnlyList<object?>>(totalRows);
            for (int i = 1; i <= totalRows; i++)
            {
                rows.Add(new object?[] { i, $"SKU-{i}", i * 10.5 });
            }

            using var ms = new MemoryStream();
            ExcelWriter.WriteRowsToStream(ms, rows, new[] { "Id", "Sku", "Price" }, sheetName: "LargeData");
            ms.Position = 0;

            int readCount = 0;
            // Early break after 100 rows
            foreach (var row in ExcelReader.StreamRows(ms, sheetName: "LargeData"))
            {
                readCount++;
                if (readCount >= 100)
                {
                    break;
                }
            }

            Assert.Equal(100, readCount);
            // Stream should still be readable and valid
            Assert.True(ms.CanRead);
        }

        [Fact]
        public void BatchPocoReflectionCache_ConcurrentDistinctTypes_ShouldNeverCorrupt()
        {
            var types = new[]
            {
                typeof(StressOrderDto),
                typeof(DeepCultureAndTypeResilienceTests.CultureTestDto),
                typeof(DeepCultureAndTypeResilienceTests.NullableTypeMatrixDto),
                typeof(DeepCsvComplianceTests.CsvProductDto),
                typeof(DeepMalformationAndCorruptedStreamTests.StubEntity)
            };

            Parallel.For(0, 50, _ =>
            {
                foreach (var t in types)
                {
                    var accessors = PropertyAccessorCache.GetAccessors(t);
                    Assert.NotEmpty(accessors);
                }
            });
        }
    }
}
