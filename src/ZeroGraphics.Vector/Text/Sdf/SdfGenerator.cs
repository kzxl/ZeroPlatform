using System;
using System.Buffers;
using ZeroGraphics.Core.Native;
using ZeroGraphics.Vector.Geometry;
using ZeroGraphics.Vector.Tessellation;

namespace ZeroGraphics.Vector.Text.Sdf
{
    /// <summary>
    /// Computes Euclidean Signed Distance Fields (SDF) from 2D vector paths.
    /// Converts continuous Bézier vector glyphs into high-resolution distance textures for GPU rendering.
    /// Supports zero-copy native SIMD dispatch via ZeroGraphicsNativeBridge and 0-LOH SoA pure C# fallback.
    /// </summary>
    public static class SdfGenerator
    {
        /// <summary>
        /// Generates an 8-bit unsigned distance field buffer for the specified Path2D within the given bounds.
        /// </summary>
        /// <param name="path">Vector path to rasterize.</param>
        /// <param name="width">Width of the output distance bitmap.</param>
        /// <param name="height">Height of the output distance bitmap.</param>
        /// <param name="boundsMinX">Minimum X in vector coordinates.</param>
        /// <param name="boundsMinY">Minimum Y in vector coordinates.</param>
        /// <param name="boundsMaxX">Maximum X in vector coordinates.</param>
        /// <param name="boundsMaxY">Maximum Y in vector coordinates.</param>
        /// <param name="spread">Distance spread in vector coordinate units across which the field transitions from 0 to 255.</param>
        /// <param name="flattenTolerance">Tolerance for Bézier curve flattening.</param>
        /// <returns>A byte array of size (width * height) containing normalized SDF values (128 = boundary, >128 = inside, &lt;128 = outside).</returns>
        public static unsafe byte[] GenerateSdf(
            Path2D path,
            int width,
            int height,
            float boundsMinX,
            float boundsMinY,
            float boundsMaxX,
            float boundsMaxY,
            float spread,
            float flattenTolerance = 0.25f)
        {
            if (width <= 0 || height <= 0) return Array.Empty<byte>();

            var output = new byte[width * height];
            if (path == null || path.VerbCount == 0 || spread <= 1e-6f)
            {
                return output;
            }

            var subpaths = AdaptiveFlattening.Flatten(path, flattenTolerance);
            int maxSegments = 0;
            for (int s = 0; s < subpaths.Count; s++)
            {
                int count = subpaths[s].Count;
                if (count >= 2)
                {
                    maxSegments += count;
                }
            }

            if (maxSegments == 0)
            {
                return output;
            }

            float[]? segAx = null;
            float[]? segAy = null;
            float[]? segBx = null;
            float[]? segBy = null;
            float[]? invAbLenSq = null;

            try
            {
                segAx = ArrayPool<float>.Shared.Rent(maxSegments);
                segAy = ArrayPool<float>.Shared.Rent(maxSegments);
                segBx = ArrayPool<float>.Shared.Rent(maxSegments);
                segBy = ArrayPool<float>.Shared.Rent(maxSegments);
                invAbLenSq = ArrayPool<float>.Shared.Rent(maxSegments);

                int numSegments = 0;
                for (int s = 0; s < subpaths.Count; s++)
                {
                    var pts = subpaths[s];
                    int count = pts.Count;
                    if (count < 2) continue;

                    for (int i = 0; i < count - 1; i++)
                    {
                        float ax = pts[i].X;
                        float ay = pts[i].Y;
                        float bx = pts[i + 1].X;
                        float by = pts[i + 1].Y;
                        float dx = bx - ax;
                        float dy = by - ay;
                        float lenSq = dx * dx + dy * dy;

                        segAx[numSegments] = ax;
                        segAy[numSegments] = ay;
                        segBx[numSegments] = bx;
                        segBy[numSegments] = by;
                        invAbLenSq[numSegments] = lenSq >= 1e-8f ? (1.0f / lenSq) : 0.0f;
                        numSegments++;
                    }

                    // If closed contour, add closing segment if not already closed
                    if ((pts[count - 1] - pts[0]).LengthSquared > 1e-5f)
                    {
                        float ax = pts[count - 1].X;
                        float ay = pts[count - 1].Y;
                        float bx = pts[0].X;
                        float by = pts[0].Y;
                        float dx = bx - ax;
                        float dy = by - ay;
                        float lenSq = dx * dx + dy * dy;

                        segAx[numSegments] = ax;
                        segAy[numSegments] = ay;
                        segBx[numSegments] = bx;
                        segBy[numSegments] = by;
                        invAbLenSq[numSegments] = lenSq >= 1e-8f ? (1.0f / lenSq) : 0.0f;
                        numSegments++;
                    }
                }

                if (numSegments == 0)
                {
                    return output;
                }

                fixed (float* pAx = segAx, pAy = segAy, pBx = segBx, pBy = segBy, pInvLenSq = invAbLenSq)
                fixed (byte* pOut = output)
                {
                    if (ZeroGraphicsNativeBridge.DispatchMode != GraphicsNativeDispatchMode.ForcePureCSharp &&
                        ZeroGraphicsNativeBridge.IsAvailable &&
                        ZeroGraphicsNativeBridge.TryGenerateSdf(
                            pAx, pAy, pBx, pBy,
                            numSegments, width, height,
                            boundsMinX, boundsMinY, boundsMaxX, boundsMaxY,
                            spread, pOut))
                    {
                        return output;
                    }

                    float rangeX = Math.Max(boundsMaxX - boundsMinX, 1e-4f);
                    float rangeY = Math.Max(boundsMaxY - boundsMinY, 1e-4f);

                    float stepX = rangeX / width;
                    float stepY = rangeY / height;
                    float halfInvSpread = 0.5f / spread;

                    for (int y = 0; y < height; y++)
                    {
                        float py = boundsMinY + (y + 0.5f) * stepY;
                        int rowOffset = y * width;

                        for (int x = 0; x < width; x++)
                        {
                            float px = boundsMinX + (x + 0.5f) * stepX;

                            float minSqDist = float.MaxValue;
                            int intersections = 0;

                            for (int i = 0; i < numSegments; i++)
                            {
                                float ax = pAx[i];
                                float ay = pAy[i];
                                float bx = pBx[i];
                                float by = pBy[i];
                                float abx = bx - ax;
                                float aby = by - ay;
                                float apx = px - ax;
                                float apy = py - ay;

                                float invLenSq = pInvLenSq[i];
                                float sqDist;
                                if (invLenSq == 0.0f)
                                {
                                    sqDist = apx * apx + apy * apy;
                                }
                                else
                                {
                                    float t = (apx * abx + apy * aby) * invLenSq;
                                    if (t < 0.0f) t = 0.0f;
                                    else if (t > 1.0f) t = 1.0f;

                                    float dx = apx - t * abx;
                                    float dy = apy - t * aby;
                                    sqDist = dx * dx + dy * dy;
                                }

                                if (sqDist < minSqDist)
                                {
                                    minSqDist = sqDist;
                                }

                                // Ray-casting along +X axis with Y-bounding-box check (Even-Odd rule)
                                if ((ay <= py && by > py) || (by <= py && ay > py))
                                {
                                    float t = (py - ay) / aby;
                                    float intersectX = ax + t * abx;
                                    if (intersectX > px)
                                    {
                                        intersections++;
                                    }
                                }
                            }

                            float dist = (float)Math.Sqrt(minSqDist);
                            bool inside = (intersections & 1) != 0;
                            float signedDist = inside ? dist : -dist;

                            // Normalize to [0..1] range, centered at 0.5 (128) on the glyph boundary
                            float norm = 0.5f + (signedDist * halfInvSpread);
                            if (norm < 0.0f) norm = 0.0f;
                            else if (norm > 1.0f) norm = 1.0f;

                            pOut[rowOffset + x] = (byte)(norm * 255.0f);
                        }
                    }
                }

                return output;
            }
            finally
            {
                if (segAx != null) ArrayPool<float>.Shared.Return(segAx);
                if (segAy != null) ArrayPool<float>.Shared.Return(segAy);
                if (segBx != null) ArrayPool<float>.Shared.Return(segBx);
                if (segBy != null) ArrayPool<float>.Shared.Return(segBy);
                if (invAbLenSq != null) ArrayPool<float>.Shared.Return(invAbLenSq);
            }
        }
    }
}
