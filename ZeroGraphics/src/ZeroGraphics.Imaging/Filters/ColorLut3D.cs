using System;
using System.Runtime.CompilerServices;

namespace ZeroGraphics.Imaging.Filters
{
    public enum Lut3DInterpolation
    {
        Tetrahedral = 0,
        Trilinear = 1
    }

    /// <summary>
    /// High-performance 3D Color Look-Up Table (LUT) with Tetrahedral and Trilinear lattice interpolation.
    /// Decomposes cubic lattice cells into 6 tetrahedra for 50% memory bandwidth savings and neutral gray preservation.
    /// </summary>
    public sealed class ColorLut3D
    {
        public int Size { get; }
        public float[] Table { get; } // Size^3 * 3 (RGB layout)
        public Lut3DInterpolation DefaultInterpolation { get; set; } = Lut3DInterpolation.Tetrahedral;

        public ColorLut3D(int size, float[] table)
        {
            if (size < 2) throw new ArgumentOutOfRangeException(nameof(size), "Size must be >= 2");
            if (table == null) throw new ArgumentNullException(nameof(table));
            if (table.Length != size * size * size * 3)
                throw new ArgumentException($"Table size must be {size * size * size * 3}, got {table.Length}");

            Size = size;
            Table = table;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int Idx(int size, int r, int g, int b) => ((b * size + g) * size + r) * 3;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);

        /// <summary>
        /// Evaluates the 3D LUT at continuous coordinates (r, g, b) in [0..1].
        /// </summary>
        public void Sample(float r, float g, float b, out float outR, out float outG, out float outB)
        {
            if (DefaultInterpolation == Lut3DInterpolation.Tetrahedral)
                SampleTetrahedral(Table, Size, r, g, b, out outR, out outG, out outB);
            else
                SampleTrilinear(Table, Size, r, g, b, out outR, out outG, out outB);
        }

        /// <summary>
        /// High-precision Tetrahedral 3D interpolation. Splits each cube into 6 tetrahedra,
        /// evaluating only 4 corners per point and guaranteeing smooth neutral diagonal tracking.
        /// </summary>
        public static void SampleTetrahedral(float[] lut, int size, float r, float g, float b,
            out float or, out float og, out float ob)
        {
            float fr = Clamp01(r) * (size - 1);
            float fg = Clamp01(g) * (size - 1);
            float fb = Clamp01(b) * (size - 1);
            int r0 = (int)fr, g0 = (int)fg, b0 = (int)fb;
            int r1 = r0 + 1 < size ? r0 + 1 : size - 1;
            int g1 = g0 + 1 < size ? g0 + 1 : size - 1;
            int b1 = b0 + 1 < size ? b0 + 1 : size - 1;
            float dr = fr - r0, dg = fg - g0, db = fb - b0;

            int i000 = Idx(size, r0, g0, b0);
            int i111 = Idx(size, r1, g1, b1);
            int iA, iB;
            float w0, wA, wB, w1;

            if (dr >= dg)
            {
                if (dg >= db)
                {
                    iA = Idx(size, r1, g0, b0);
                    iB = Idx(size, r1, g1, b0);
                    w0 = 1f - dr; wA = dr - dg; wB = dg - db; w1 = db;
                }
                else if (dr >= db)
                {
                    iA = Idx(size, r1, g0, b0);
                    iB = Idx(size, r1, g0, b1);
                    w0 = 1f - dr; wA = dr - db; wB = db - dg; w1 = dg;
                }
                else
                {
                    iA = Idx(size, r0, g0, b1);
                    iB = Idx(size, r1, g0, b1);
                    w0 = 1f - db; wA = db - dr; wB = dr - dg; w1 = dg;
                }
            }
            else
            {
                if (db > dg)
                {
                    iA = Idx(size, r0, g0, b1);
                    iB = Idx(size, r0, g1, b1);
                    w0 = 1f - db; wA = db - dg; wB = dg - dr; w1 = dr;
                }
                else if (db > dr)
                {
                    iA = Idx(size, r0, g1, b0);
                    iB = Idx(size, r0, g1, b1);
                    w0 = 1f - dg; wA = dg - db; wB = db - dr; w1 = dr;
                }
                else
                {
                    iA = Idx(size, r0, g1, b0);
                    iB = Idx(size, r1, g1, b0);
                    w0 = 1f - dg; wA = dg - dr; wB = dr - db; w1 = db;
                }
            }

            or = w0 * lut[i000]     + wA * lut[iA]     + wB * lut[iB]     + w1 * lut[i111];
            og = w0 * lut[i000 + 1] + wA * lut[iA + 1] + wB * lut[iB + 1] + w1 * lut[i111 + 1];
            ob = w0 * lut[i000 + 2] + wA * lut[iA + 2] + wB * lut[iB + 2] + w1 * lut[i111 + 2];
        }

        /// <summary>
        /// Standard Trilinear 3D lattice interpolation evaluating all 8 vertices.
        /// </summary>
        public static void SampleTrilinear(float[] lut, int size, float r, float g, float b,
            out float or, out float og, out float ob)
        {
            float fr = Clamp01(r) * (size - 1);
            float fg = Clamp01(g) * (size - 1);
            float fb = Clamp01(b) * (size - 1);
            int r0 = (int)fr, g0 = (int)fg, b0 = (int)fb;
            int r1 = r0 + 1 < size ? r0 + 1 : size - 1;
            int g1 = g0 + 1 < size ? g0 + 1 : size - 1;
            int b1 = b0 + 1 < size ? b0 + 1 : size - 1;
            float dr = fr - r0, dg = fg - g0, db = fb - b0;

            or = og = ob = 0f;
            for (int c = 0; c < 3; c++)
            {
                float c000 = lut[Idx(size, r0, g0, b0) + c];
                float c100 = lut[Idx(size, r1, g0, b0) + c];
                float c010 = lut[Idx(size, r0, g1, b0) + c];
                float c110 = lut[Idx(size, r1, g1, b0) + c];
                float c001 = lut[Idx(size, r0, g0, b1) + c];
                float c101 = lut[Idx(size, r1, g0, b1) + c];
                float c011 = lut[Idx(size, r0, g1, b1) + c];
                float c111 = lut[Idx(size, r1, g1, b1) + c];
                float c00 = c000 + (c100 - c000) * dr;
                float c10 = c010 + (c110 - c010) * dr;
                float c01 = c001 + (c101 - c001) * dr;
                float c11 = c011 + (c111 - c011) * dr;
                float c0 = c00 + (c10 - c00) * dg;
                float c1 = c01 + (c11 - c01) * dg;
                float val = c0 + (c1 - c0) * db;
                if (c == 0) or = val; else if (c == 1) og = val; else ob = val;
            }
        }
    }
}
