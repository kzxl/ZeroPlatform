using System;
using ZeroUI.Core.Common;
using ZeroUI.Core.Data;

namespace ZeroUI.Demo.Data
{
    /// <summary>
    /// Procedural virtual data source for extreme datasets (1,000,000 to 10,000,000+ rows).
    /// Generates cell values completely on-the-fly without keeping multi-gigabyte managed objects in RAM.
    /// Memory footprint is virtually ZERO beyond the grid's own index map.
    /// </summary>
    public sealed class ProceduralSource : IZeroVirtualSource, IZeroSortableSource
    {
        private readonly int _totalRowCount;
        private readonly char[] _scratch = new char[64];

        private static readonly string[] Categories = new[]
        {
            "STM32F407VGT6 LQFP100 Microcontroller",
            "TI TPS54302DDCR Buck Converter 28V 3A",
            "Winbond W25Q128JVS 128Mb SPI Flash",
            "ESP32-WROOM-32E Wi-Fi/BLE Module",
            "SMD 0805 10uF 25V X7R Ceramic Capacitor",
            "Precision Thin Film Resistor 0603 10kΩ 1%",
            "SMD Power Inductor 10uH 3.5A Shielded",
            "SMD 3225 16.000MHz Crystal Oscillator",
            "Schottky Rectifier Diode SS34 40V 3A SMC",
            "USB Type-C 16-Pin SMT IPX7 Connector",
            "Phoenix 5.08mm 4P Terminal Block",
            "Omron G3MB-202P 5V Solid State Relay",
            "Airtac 4V210-08 24VDC Solenoid Valve",
            "SMC MGPM25-50Z Compact Pneumatic Cylinder",
            "Keyence PZ-G41N Optical Sensor",
            "Hybrid Stepper Motor Nema 23 2.8Nm",
            "HIWIN HGH20CA Linear Guide Block",
            "TBI Motion SFU1605-600mm Ball Screw",
            "SKF 6205-2RSH/C3 Deep Groove Ball Bearing",
            "Senju M705 SAC305 Lead-Free Solder Paste"
        };

        private static readonly string[] CodePrefixes = new[]
        {
            "IC", "PWR", "MEM", "IOT", "CAP", "RES", "IND", "XTAL", "DIO", "USBC",
            "TERM", "REL", "VAL", "CYL", "SENS", "STEP", "RAIL", "SCREW", "BRG", "SOLD"
        };

        private static readonly string[] Statuses = new[]
        {
            "Passed OQC",
            "Pending Inspection",
            "Safety Stock",
            "In Quarantine",
            "Restock Required"
        };

        private static readonly uint[] StatusBgColors = new uint[]
        {
            0x00E8F8E8, // Soft Green
            0x00FFF8E7, // Soft Orange
            0x00EBF4FF, // Soft Blue
            0x00FEE8E8, // Soft Red
            0x00F3E8FF  // Soft Purple
        };

        private static readonly uint[] StatusFgColors = new uint[]
        {
            0x001B692A,
            0x00A05A00,
            0x001B5EBE,
            0x00B3261E,
            0x006B21A8
        };

        public ProceduralSource(int totalRowCount)
        {
            _totalRowCount = totalRowCount;
        }

        public int TotalRowCount => _totalRowCount;
        public int TotalColumnCount => 11;

        public void GetCellValue(int rowIndex, int columnIndex, ref CellValueBuffer buffer)
        {
            if (rowIndex < 0 || rowIndex >= _totalRowCount) return;

            int id = rowIndex + 1;
            int catIdx = rowIndex % Categories.Length;

            switch (columnIndex)
            {
                case 0: // Active (Boolean)
                    buffer.Text = (rowIndex % 3 != 0) ? "true".AsSpan() : "false".AsSpan();
                    buffer.Alignment = CellAlignment.Center;
                    break;

                case 1: // Category
                    buffer.Text = Categories[catIdx].AsSpan();
                    buffer.Alignment = CellAlignment.Left;
                    break;

                case 2: // ID
                    buffer.Text = id.ToString().AsSpan();
                    buffer.Alignment = CellAlignment.Right;
                    break;

                case 3: // Item Code
                    buffer.Text = $"{CodePrefixes[catIdx]}-{id:D7}".AsSpan();
                    buffer.Alignment = CellAlignment.Left;
                    break;

                case 4: // Item Name / Description
                    buffer.Text = Categories[(catIdx + 3) % Categories.Length].AsSpan();
                    buffer.Alignment = CellAlignment.Left;
                    break;

                case 5: // Quantity
                    int qty = 100 + ((rowIndex * 37) % 9900);
                    buffer.Text = qty.ToString().AsSpan();
                    buffer.Alignment = CellAlignment.Right;
                    break;

                case 6: // Unit Price ($)
                    double price = 5.50 + ((rowIndex * 19) % 450);
                    buffer.Text = price.ToString("N2").AsSpan();
                    buffer.Alignment = CellAlignment.Right;
                    break;

                case 7: // Total Amount ($)
                    int q = 100 + ((rowIndex * 37) % 9900);
                    double p = 5.50 + ((rowIndex * 19) % 450);
                    double total = q * p;
                    buffer.Text = total.ToString("N2").AsSpan();
                    buffer.Alignment = CellAlignment.Right;
                    break;

                case 8: // Yield %
                    float yield = (70f + ((rowIndex * 7) % 30)) / 100f;
                    buffer.DataBarPercent = yield;
                    buffer.Text = yield.ToString("P0").AsSpan();
                    buffer.Alignment = CellAlignment.Center;
                    break;

                case 9: // Lot Number
                    int lotYear = 24 + ((rowIndex / 1000) % 3);
                    int lotSeq = 1000 + (rowIndex % 9000);
                    buffer.Text = $"LOT-{lotYear}08-{lotSeq:D4}".AsSpan();
                    buffer.Alignment = CellAlignment.Center;
                    break;

                case 10: // Status
                    int statusIdx = (rowIndex % Statuses.Length);
                    buffer.Text = Statuses[statusIdx].AsSpan();
                    buffer.Alignment = CellAlignment.Center;
                    buffer.HasCustomBackground = true;
                    buffer.BackColor = StatusBgColors[statusIdx];
                    buffer.TextColor = StatusFgColors[statusIdx];
                    break;
            }
        }

        public int CompareRows(int rowA, int rowB, int columnIndex)
        {
            if (rowA == rowB) return 0;
            return columnIndex switch
            {
                0 => (rowA % 3).CompareTo(rowB % 3),
                1 => string.Compare(Categories[rowA % Categories.Length], Categories[rowB % Categories.Length], StringComparison.OrdinalIgnoreCase),
                2 => rowA.CompareTo(rowB),
                5 => ((rowA * 37) % 9900).CompareTo((rowB * 37) % 9900),
                6 => ((rowA * 19) % 450).CompareTo((rowB * 19) % 450),
                8 => ((rowA * 7) % 30).CompareTo((rowB * 7) % 30),
                _ => rowA.CompareTo(rowB)
            };
        }

        public void Sort(int columnIndex, bool ascending)
        {
            // Procedural virtual mode sort
        }
    }
}
