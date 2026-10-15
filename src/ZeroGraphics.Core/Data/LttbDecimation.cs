using System;
using System.Buffers;
using ZeroGraphics.Core.Native;

namespace ZeroGraphics.Core.Data
{
    public readonly struct TimePoint : IEquatable<TimePoint>
    {
        public double X { get; }
        public double Y { get; }

        public TimePoint(double x, double y)
        {
            X = x;
            Y = y;
        }

        public bool Equals(TimePoint other) => X.Equals(other.X) && Y.Equals(other.Y);
        public override bool Equals(object? obj) => obj is TimePoint other && Equals(other);
        public override int GetHashCode() => unchecked((X.GetHashCode() * 397) ^ Y.GetHashCode());
        public static bool operator ==(TimePoint left, TimePoint right) => left.Equals(right);
        public static bool operator !=(TimePoint left, TimePoint right) => !left.Equals(right);
        public override string ToString() => $"({X:0.##}, {Y:0.##})";
    }

    /// <summary>
    /// Zero-allocation Largest-Triangle-Three-Buckets (LTTB) decimation algorithm.
    /// Downsamples millions of telemetry points to viewport resolution while preserving visual peaks and valleys.
    /// </summary>
    public static class LttbDecimation
    {
        /// <summary>
        /// Downsamples Structure-of-Arrays (SoA) X/Y telemetry buffers with zero-copy native SIMD dispatch when available.
        /// </summary>
        public static unsafe int Downsample(
            ReadOnlySpan<double> srcX,
            ReadOnlySpan<double> srcY,
            Span<double> dstX,
            Span<double> dstY,
            int targetCount)
        {
            int dataLength = System.Math.Min(srcX.Length, srcY.Length);
            int maxDest = System.Math.Min(dstX.Length, dstY.Length);
            if (targetCount > maxDest) targetCount = maxDest;

            if (targetCount >= dataLength || targetCount <= 2 || dataLength <= 2)
            {
                int copyCount = System.Math.Min(dataLength, maxDest);
                srcX.Slice(0, copyCount).CopyTo(dstX);
                srcY.Slice(0, copyCount).CopyTo(dstY);
                return copyCount;
            }

            if (ZeroGraphicsNativeBridge.DispatchMode != GraphicsNativeDispatchMode.ForcePureCSharp &&
                ZeroGraphicsNativeBridge.IsAvailable &&
                (ZeroGraphicsNativeBridge.DispatchMode == GraphicsNativeDispatchMode.ForceNative || dataLength >= 2048))
            {
                fixed (double* pSrcX = srcX, pSrcY = srcY, pDstX = dstX, pDstY = dstY)
                {
                    if (ZeroGraphicsNativeBridge.TryLttbDecimate(
                        pSrcX, pSrcY, dataLength, targetCount, pDstX, pDstY, out int written))
                    {
                        return written;
                    }
                }
            }

            int sampledIndex = 0;
            double every = (double)(dataLength - 2) / (targetCount - 2);

            int a = 0;
            dstX[sampledIndex] = srcX[a];
            dstY[sampledIndex] = srcY[a];
            sampledIndex++;

            for (int i = 0; i < targetCount - 2; i++)
            {
                double avgX = 0;
                double avgY = 0;
                int avgRangeStart = (int)System.Math.Floor((i + 1) * every) + 1;
                int avgRangeEnd = (int)System.Math.Floor((i + 2) * every) + 1;
                if (avgRangeEnd > dataLength) avgRangeEnd = dataLength;

                int avgRangeLength = avgRangeEnd - avgRangeStart;
                if (avgRangeLength > 0)
                {
                    for (int j = avgRangeStart; j < avgRangeEnd; j++)
                    {
                        avgX += srcX[j];
                        avgY += srcY[j];
                    }
                    double invLen = 1.0 / avgRangeLength;
                    avgX *= invLen;
                    avgY *= invLen;
                }
                else
                {
                    avgX = srcX[dataLength - 1];
                    avgY = srcY[dataLength - 1];
                }

                int rangeOffs = (int)System.Math.Floor(i * every) + 1;
                int rangeTo = (int)System.Math.Floor((i + 1) * every) + 1;
                if (rangeTo > dataLength) rangeTo = dataLength;

                double pointAx = srcX[a];
                double pointAy = srcY[a];

                double maxArea = -1;
                int maxAreaIndex = rangeOffs;

                for (int j = rangeOffs; j < rangeTo; j++)
                {
                    double area = System.Math.Abs(
                        (pointAx - avgX) * (srcY[j] - pointAy) -
                        (pointAx - srcX[j]) * (avgY - pointAy)
                    ) * 0.5;

                    if (area > maxArea)
                    {
                        maxArea = area;
                        maxAreaIndex = j;
                    }
                }

                dstX[sampledIndex] = srcX[maxAreaIndex];
                dstY[sampledIndex] = srcY[maxAreaIndex];
                sampledIndex++;
                a = maxAreaIndex;
            }

            dstX[sampledIndex] = srcX[dataLength - 1];
            dstY[sampledIndex] = srcY[dataLength - 1];
            sampledIndex++;
            return sampledIndex;
        }

        public static unsafe int Downsample(ReadOnlySpan<TimePoint> data, Span<TimePoint> destination, int targetCount)
        {
            int dataLength = data.Length;
            if (targetCount > destination.Length) targetCount = destination.Length;
            if (targetCount >= dataLength || targetCount <= 2 || dataLength <= 2)
            {
                int copyCount = System.Math.Min(dataLength, destination.Length);
                data.Slice(0, copyCount).CopyTo(destination);
                return copyCount;
            }

            if (ZeroGraphicsNativeBridge.DispatchMode == GraphicsNativeDispatchMode.ForceNative &&
                ZeroGraphicsNativeBridge.IsAvailable)
            {
                double[]? sx = null;
                double[]? sy = null;
                double[]? dx = null;
                double[]? dy = null;
                try
                {
                    sx = ArrayPool<double>.Shared.Rent(dataLength);
                    sy = ArrayPool<double>.Shared.Rent(dataLength);
                    dx = ArrayPool<double>.Shared.Rent(targetCount);
                    dy = ArrayPool<double>.Shared.Rent(targetCount);

                    for (int i = 0; i < dataLength; i++)
                    {
                        sx[i] = data[i].X;
                        sy[i] = data[i].Y;
                    }

                    fixed (double* pSx = sx, pSy = sy, pDx = dx, pDy = dy)
                    {
                        if (ZeroGraphicsNativeBridge.TryLttbDecimate(
                            pSx, pSy, dataLength, targetCount, pDx, pDy, out int written))
                        {
                            for (int i = 0; i < written; i++)
                            {
                                destination[i] = new TimePoint(dx[i], dy[i]);
                            }
                            return written;
                        }
                    }
                }
                finally
                {
                    if (sx != null) ArrayPool<double>.Shared.Return(sx);
                    if (sy != null) ArrayPool<double>.Shared.Return(sy);
                    if (dx != null) ArrayPool<double>.Shared.Return(dx);
                    if (dy != null) ArrayPool<double>.Shared.Return(dy);
                }
            }

            int sampledIndex = 0;
            double every = (double)(dataLength - 2) / (targetCount - 2);

            int a = 0;
            destination[sampledIndex++] = data[a];

            for (int i = 0; i < targetCount - 2; i++)
            {
                double avgX = 0;
                double avgY = 0;
                int avgRangeStart = (int)System.Math.Floor((i + 1) * every) + 1;
                int avgRangeEnd = (int)System.Math.Floor((i + 2) * every) + 1;
                if (avgRangeEnd > dataLength) avgRangeEnd = dataLength;

                int avgRangeLength = avgRangeEnd - avgRangeStart;
                if (avgRangeLength > 0)
                {
                    for (int j = avgRangeStart; j < avgRangeEnd; j++)
                    {
                        ref readonly var pt = ref data[j];
                        avgX += pt.X;
                        avgY += pt.Y;
                    }
                    avgX /= avgRangeLength;
                    avgY /= avgRangeLength;
                }
                else
                {
                    ref readonly var last = ref data[dataLength - 1];
                    avgX = last.X;
                    avgY = last.Y;
                }

                int rangeOffs = (int)System.Math.Floor(i * every) + 1;
                int rangeTo = (int)System.Math.Floor((i + 1) * every) + 1;
                if (rangeTo > dataLength) rangeTo = dataLength;

                ref readonly var pointA = ref data[a];
                double pointAx = pointA.X;
                double pointAy = pointA.Y;

                double maxArea = -1;
                int maxAreaIndex = rangeOffs;

                for (int j = rangeOffs; j < rangeTo; j++)
                {
                    ref readonly var pt = ref data[j];
                    double area = System.Math.Abs(
                        (pointAx - avgX) * (pt.Y - pointAy) -
                        (pointAx - pt.X) * (avgY - pointAy)
                    ) * 0.5;

                    if (area > maxArea)
                    {
                        maxArea = area;
                        maxAreaIndex = j;
                    }
                }

                destination[sampledIndex++] = data[maxAreaIndex];
                a = maxAreaIndex;
            }

            destination[sampledIndex++] = data[dataLength - 1];
            return sampledIndex;
        }
    }
}
