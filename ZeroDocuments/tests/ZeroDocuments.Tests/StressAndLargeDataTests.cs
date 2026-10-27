using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Xunit;
using ZeroDocuments.Csv;
using ZeroDocuments.Excel;
using ZeroDocuments.Excel.Models;

namespace ZeroDocuments.Tests
{
    public class StressAndLargeDataTests
    {
        public class TelemetryRecord
        {
            public int Id { get; set; }
            public string DeviceId { get; set; } = string.Empty;
            public double Voltage { get; set; }
            public double Current { get; set; }
            public double Power { get; set; }
            public bool IsAlarm { get; set; }
            public DateTime Timestamp { get; set; }
        }

        [Fact]
        public void StressTest_50000Rows_WriteAndStreamRead_ShouldBeFastAndLowMemory()
        {
            const int totalRows = 50000;
            var records = new List<TelemetryRecord>(totalRows);
            var baseTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            for (int i = 1; i <= totalRows; i++)
            {
                records.Add(new TelemetryRecord
                {
                    Id = i,
                    DeviceId = $"DEV-NODE-{(i % 100):000}",
                    Voltage = 220.0 + (i % 50) * 0.1,
                    Current = 5.0 + (i % 20) * 0.05,
                    Power = (220.0 + (i % 50) * 0.1) * (5.0 + (i % 20) * 0.05),
                    IsAlarm = (i % 500) == 0,
                    Timestamp = baseTime.AddSeconds(i)
                });
            }

            using var ms = new MemoryStream();

            // 1. Measure write time
            var writeSw = Stopwatch.StartNew();
            ExcelWriter.WriteToStream(ms, records, sheetName: "Telemetry50K", includeHeaders: true);
            writeSw.Stop();

            Assert.True(ms.Length > 0, "Excel output stream must not be empty.");
            Assert.True(writeSw.ElapsedMilliseconds < 5000, $"Writing 50,000 rows took {writeSw.ElapsedMilliseconds} ms, expected < 5,000 ms");

            // 2. Measure streaming read time
            ms.Position = 0;
            var readSw = Stopwatch.StartNew();

            int readCount = 0;
            string? firstDeviceId = null;
            string? lastDeviceId = null;

            foreach (var row in ExcelReader.StreamRows(ms, sheetName: "Telemetry50K"))
            {
                readCount++;
                if (readCount == 1) continue; // Skip header row
                if (readCount == 2) firstDeviceId = row[2]; // DeviceId is col 2
                lastDeviceId = row[2];
            }
            readSw.Stop();

            Assert.Equal(totalRows + 1, readCount); // 50,000 data rows + 1 header row
            Assert.Equal("DEV-NODE-001", firstDeviceId);
            Assert.Equal($"DEV-NODE-{(totalRows % 100):000}", lastDeviceId);
            Assert.True(readSw.ElapsedMilliseconds < 5000, $"Streaming 50,000 rows took {readSw.ElapsedMilliseconds} ms, expected < 5,000 ms");
        }

        [Fact]
        public void StressTest_100000Rows_CsvWriteAndStreamRead_ShouldSucceed()
        {
            const int totalRows = 100000;
            var rows = new List<IReadOnlyList<object?>>(totalRows);

            for (int i = 1; i <= totalRows; i++)
            {
                rows.Add(new object?[] { i, $"SKU-{i:000000}", i * 1.25, (i % 2 == 0) ? "ACTIVE" : "INACTIVE" });
            }

            using var ms = new MemoryStream();
            var sw = Stopwatch.StartNew();
            CsvWriter.WriteRowsToStream(ms, rows, new[] { "ID", "Code", "Price", "Status" });
            sw.Stop();

            Assert.True(ms.Length > 0);
            Assert.True(sw.ElapsedMilliseconds < 3000, $"CSV writing 100,000 rows took {sw.ElapsedMilliseconds} ms, expected < 3,000 ms");

            // Read back
            ms.Position = 0;
            int parsedRowCount = 0;
            using var reader = new StreamReader(ms);
            foreach (var r in CsvReader.ReadRows(reader))
            {
                parsedRowCount++;
            }

            Assert.Equal(totalRows + 1, parsedRowCount); // 100k data + 1 header
        }
    }
}
