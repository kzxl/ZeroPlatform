using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ZeroUI.Core.Common;
using ZeroUI.Core.Data;

namespace ZeroUI.Wpf.Design.Mock
{
    /// <summary>
    /// Design-time sample data provider for ZeroUI WPF controls.
    /// Used by Visual Studio XAML Designer and Blend to render rich preview states when explicitly configured.
    /// </summary>
    public static class DesignDataFactory
    {
        public class DesignTelemetryRecord
        {
            public int Id { get; set; }
            public string TagName { get; set; } = string.Empty;
            public double Value { get; set; }
            public string Unit { get; set; } = string.Empty;
            public string Status { get; set; } = "OK";
            public DateTime Timestamp { get; set; }
        }

        /// <summary>
        /// Creates a collection of sample SCADA telemetry records for design-time data binding.
        /// </summary>
        public static ObservableCollection<DesignTelemetryRecord> CreateSampleTelemetryData(int count = 10)
        {
            var list = new ObservableCollection<DesignTelemetryRecord>();
            var units = new[] { "bar", "°C", "rpm", "m³/h", "kW", "kPa" };
            var tags = new[] { "PT-101", "TT-204", "PUMP-01", "FT-302", "MOTOR-03", "LT-401" };
            var statuses = new[] { "OK", "Warning", "Normal", "Critical" };
            var random = new Random(42);

            for (int i = 1; i <= count; i++)
            {
                list.Add(new DesignTelemetryRecord
                {
                    Id = i,
                    TagName = tags[(i - 1) % tags.Length],
                    Value = Math.Round(20.0 + random.NextDouble() * 80.0, 2),
                    Unit = units[(i - 1) % units.Length],
                    Status = statuses[(i - 1) % statuses.Length],
                    Timestamp = DateTime.Now.AddMinutes(-i * 5)
                });
            }

            return list;
        }

        /// <summary>
        /// Creates a high-performance virtual data source implementing <see cref="IZeroVirtualSource"/> for ZeroGridControl preview.
        /// </summary>
        public static IZeroVirtualSource CreateSampleVirtualSource(int count = 15)
        {
            var list = CreateSampleTelemetryData(count);
            var arr = new DesignTelemetryRecord[list.Count];
            list.CopyTo(arr, 0);
            return new DesignTelemetrySource(arr);
        }

        public sealed class DesignTelemetrySource : IZeroVirtualSource
        {
            private readonly DesignTelemetryRecord[] _records;

            public DesignTelemetrySource(DesignTelemetryRecord[] records)
            {
                _records = records;
            }

            public int TotalRowCount => _records.Length;
            public int TotalColumnCount => 6;

            public void GetCellValue(int rowIndex, int columnIndex, ref CellValueBuffer buffer)
            {
                if (rowIndex < 0 || rowIndex >= _records.Length) return;
                var rec = _records[rowIndex];
                switch (columnIndex)
                {
                    case 0:
                        buffer.Text = rec.Id.ToString().AsSpan();
                        buffer.Alignment = CellAlignment.Right;
                        break;
                    case 1:
                        buffer.Text = rec.TagName.AsSpan();
                        buffer.Alignment = CellAlignment.Left;
                        break;
                    case 2:
                        buffer.Text = rec.Value.ToString("F2").AsSpan();
                        buffer.Alignment = CellAlignment.Right;
                        break;
                    case 3:
                        buffer.Text = rec.Unit.AsSpan();
                        buffer.Alignment = CellAlignment.Center;
                        break;
                    case 4:
                        buffer.Text = rec.Status.AsSpan();
                        buffer.Alignment = CellAlignment.Center;
                        break;
                    case 5:
                        buffer.Text = rec.Timestamp.ToString("HH:mm:ss").AsSpan();
                        buffer.Alignment = CellAlignment.Left;
                        break;
                }
            }
        }
    }
}
