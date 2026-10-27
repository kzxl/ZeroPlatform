using System;

namespace ZeroUI.Core.LifeSciences
{
    /// <summary>
    /// Standard ANSI/SLAS microplate format dimensions.
    /// </summary>
    public enum PlateFormat
    {
        Wells96,  // 8 rows (A-H) x 12 cols (1-12)
        Wells384  // 16 rows (A-P) x 24 cols (1-24)
    }

    /// <summary>
    /// Assay sample classification for microplate wells.
    /// </summary>
    public enum SampleType
    {
        Unknown,
        Standard,
        Blank,
        PositiveControl,
        NegativeControl
    }

    /// <summary>
    /// Telemetry and assay measurement for an individual microplate well.
    /// </summary>
    public class MicroplateWell
    {
        public int RowIndex { get; }
        public int ColIndex { get; }
        public string WellId { get; }
        public SampleType SampleType { get; set; } = SampleType.Unknown;
        public double Value { get; set; } // Optical Density (OD450) or Fluorescence RFU
        public double Concentration { get; set; }
        public bool IsOutlier { get; set; }
        public bool IsSelected { get; set; }

        public MicroplateWell(int rowIndex, int colIndex)
        {
            RowIndex = rowIndex;
            ColIndex = colIndex;
            char rowChar = (char)('A' + rowIndex);
            WellId = $"{rowChar}{colIndex + 1:00}";
        }
    }

    /// <summary>
    /// Pure computation engine for microplate reader assays, statistics, and well layouts.
    /// </summary>
    public class MicroplateEngine
    {
        public PlateFormat Format { get; }
        public int RowCount { get; }
        public int ColCount { get; }
        public MicroplateWell[,] Wells { get; }
        public string AssayTitle { get; set; } = "ELISA Absorbance Assay (450 nm)";

        public MicroplateEngine(PlateFormat format = PlateFormat.Wells96)
        {
            Format = format;
            RowCount = format == PlateFormat.Wells96 ? 8 : 16;
            ColCount = format == PlateFormat.Wells96 ? 12 : 24;
            Wells = new MicroplateWell[RowCount, ColCount];

            InitializeDefaultPlate();
        }

        private void InitializeDefaultPlate()
        {
            for (int r = 0; r < RowCount; r++)
            {
                for (int c = 0; c < ColCount; c++)
                {
                    var well = new MicroplateWell(r, c);

                    // Typical assay layout:
                    // Col 0: Blank / Controls
                    // Col 1: Calibration Standards (dilution series)
                    // Col 2-11: Unknown Samples
                    if (c == 0)
                    {
                        if (r <= 1)
                        {
                            well.SampleType = SampleType.Blank;
                            well.Value = 0.045 + (r * 0.003);
                        }
                        else if (r <= 4)
                        {
                            well.SampleType = SampleType.PositiveControl;
                            well.Value = 2.150 + ((r - 2) * 0.02);
                        }
                        else
                        {
                            well.SampleType = SampleType.NegativeControl;
                            well.Value = 0.095 + ((r - 5) * 0.005);
                        }
                    }
                    else if (c == 1)
                    {
                        well.SampleType = SampleType.Standard;
                        // 8-point standard dilution series
                        well.Value = 2.80 / Math.Pow(1.8, r);
                        well.Concentration = 1000.0 / Math.Pow(2.0, r);
                    }
                    else
                    {
                        well.SampleType = SampleType.Unknown;
                        // Pseudo-random absorbance profile
                        double seed = ((r * 13 + c * 7) % 100) / 100.0;
                        well.Value = 0.15 + seed * 1.85;
                        well.Concentration = well.Value * 125.0;

                        if (r == 4 && c == 8)
                        {
                            well.IsOutlier = true;
                            well.Value = 3.45;
                        }
                    }

                    Wells[r, c] = well;
                }
            }
        }

        public double MinValue
        {
            get
            {
                double min = double.MaxValue;
                for (int r = 0; r < RowCount; r++)
                    for (int c = 0; c < ColCount; c++)
                        if (Wells[r, c].Value < min) min = Wells[r, c].Value;
                return min == double.MaxValue ? 0.0 : min;
            }
        }

        public double MaxValue
        {
            get
            {
                double max = double.MinValue;
                for (int r = 0; r < RowCount; r++)
                    for (int c = 0; c < ColCount; c++)
                        if (Wells[r, c].Value > max) max = Wells[r, c].Value;
                return max == double.MinValue ? 1.0 : max;
            }
        }

        /// <summary>
        /// Calculates Mean, Standard Deviation (SD), and Coefficient of Variation (%CV) for a group of replicate wells.
        /// </summary>
        public bool CalculateStatistics(SampleType type, out double mean, out double sd, out double cvPct)
        {
            mean = 0.0;
            sd = 0.0;
            cvPct = 0.0;

            int count = 0;
            double sum = 0.0;
            for (int r = 0; r < RowCount; r++)
            {
                for (int c = 0; c < ColCount; c++)
                {
                    if (Wells[r, c].SampleType == type && !Wells[r, c].IsOutlier)
                    {
                        sum += Wells[r, c].Value;
                        count++;
                    }
                }
            }

            if (count == 0) return false;
            mean = sum / count;

            if (count > 1)
            {
                double sumSq = 0.0;
                for (int r = 0; r < RowCount; r++)
                {
                    for (int c = 0; c < ColCount; c++)
                    {
                        if (Wells[r, c].SampleType == type && !Wells[r, c].IsOutlier)
                        {
                            double diff = Wells[r, c].Value - mean;
                            sumSq += diff * diff;
                        }
                    }
                }
                sd = Math.Sqrt(sumSq / (count - 1));
                cvPct = mean > 0.0 ? (sd / mean) * 100.0 : 0.0;
            }

            return true;
        }
    }
}
