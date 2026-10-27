using System;

namespace ZeroGraphics.Core.Data
{
    /// <summary>
    /// Zero-allocation MinMax (Peak-Preserving) decimation algorithm.
    /// Downsamples high-frequency telemetry and oscilloscope data by dividing into equal buckets
    /// and retaining the minimum and maximum values in chronological order within each bucket.
    /// Guarantees that narrow transients, anomalies, and peaks are never omitted.
    /// </summary>
    public static class MinMaxDecimation
    {
        /// <summary>
        /// Downsamples the source time-series points to a target count while preserving peaks and valleys.
        /// </summary>
        /// <param name="data">Source points ordered chronologically by X.</param>
        /// <param name="destination">Destination span to receive downsampled points.</param>
        /// <param name="targetCount">Target point count (must be at least 2).</param>
        /// <returns>Number of points written into destination.</returns>
        public static int Downsample(ReadOnlySpan<TimePoint> data, Span<TimePoint> destination, int targetCount)
        {
            int dataLength = data.Length;
            if (targetCount >= dataLength || targetCount <= 2 || dataLength <= 2)
            {
                int copyCount = System.Math.Min(dataLength, destination.Length);
                data.Slice(0, copyCount).CopyTo(destination);
                return copyCount;
            }

            if (destination.Length < targetCount)
            {
                targetCount = destination.Length;
                if (targetCount <= 2)
                {
                    destination[0] = data[0];
                    destination[1] = data[dataLength - 1];
                    return 2;
                }
            }

            int sampledIndex = 0;
            destination[sampledIndex++] = data[0];

            int innerBuckets = (targetCount - 2) / 2;
            if (innerBuckets <= 0)
            {
                destination[sampledIndex++] = data[dataLength - 1];
                return sampledIndex;
            }

            double bucketSize = (double)(dataLength - 2) / innerBuckets;

            for (int b = 0; b < innerBuckets; b++)
            {
                int start = 1 + (int)System.Math.Floor(b * bucketSize);
                int end = 1 + (int)System.Math.Floor((b + 1) * bucketSize);
                if (end > dataLength - 1) end = dataLength - 1;

                if (start >= end)
                {
                    if (start < dataLength - 1)
                    {
                        destination[sampledIndex++] = data[start];
                    }
                    continue;
                }

                int minIdx = start;
                int maxIdx = start;
                double minY = data[start].Y;
                double maxY = data[start].Y;

                for (int j = start + 1; j < end; j++)
                {
                    double y = data[j].Y;
                    if (y < minY)
                    {
                        minY = y;
                        minIdx = j;
                    }
                    if (y > maxY)
                    {
                        maxY = y;
                        maxIdx = j;
                    }
                }

                // Preserve chronological time order between min and max
                if (minIdx < maxIdx)
                {
                    destination[sampledIndex++] = data[minIdx];
                    destination[sampledIndex++] = data[maxIdx];
                }
                else if (minIdx > maxIdx)
                {
                    destination[sampledIndex++] = data[maxIdx];
                    destination[sampledIndex++] = data[minIdx];
                }
                else
                {
                    destination[sampledIndex++] = data[minIdx];
                }
            }

            destination[sampledIndex++] = data[dataLength - 1];
            return sampledIndex;
        }
    }
}
