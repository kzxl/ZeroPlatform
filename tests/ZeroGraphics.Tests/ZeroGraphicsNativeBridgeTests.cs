using System;
using Xunit;
using ZeroGraphics.Core.Data;
using ZeroGraphics.Core.Native;
using ZeroGraphics.Imaging.Core;
using ZeroGraphics.Imaging.Filters;
using ZeroGraphics.Vector.Geometry;
using ZeroGraphics.Vector.Text.Sdf;
using ZeroGraphics.Vision.Edge;
using ZeroGraphics.Vision.Matching;

namespace ZeroGraphics.Tests
{
    public class ZeroGraphicsNativeBridgeTests
    {
        [Fact]
        public void NativeBridge_ProbesAndReportsCapabilitiesCleanly()
        {
            ZeroGraphicsNativeBridge.Initialize(forceReload: true);
#if NET8_0_OR_GREATER
            if (ZeroGraphicsNativeBridge.IsAvailable)
            {
                Assert.Equal(100u, ZeroGraphicsNativeBridge.GetVersion());
                Assert.True(ZeroGraphicsNativeBridge.IsSimdSupported());
            }
#else
            Assert.False(ZeroGraphicsNativeBridge.IsAvailable);
#endif
        }

        [Fact]
        public unsafe void CannyEdgeDetector_Detect_MatchesAcrossPureCSharpAndNativeModes()
        {
            int w = 128;
            int h = 128;

            using var img = ImageBuffer.CreateGray8(w, h);
            img.Clear(20);

            // Draw high-contrast rectangle [32..95, 32..95]
            for (int y = 32; y < 96; y++)
            {
                byte* row = img.GetRowPointer(y);
                for (int x = 32; x < 96; x++)
                {
                    row[x] = 220;
                }
            }

            var prevMode = ZeroGraphicsNativeBridge.DispatchMode;
            try
            {
                ZeroGraphicsNativeBridge.DispatchMode = GraphicsNativeDispatchMode.ForcePureCSharp;
                using var edgesCs = CannyEdgeDetector.Detect(img, lowThreshold: 30.0, highThreshold: 80.0, applyGaussian: true);

                ZeroGraphicsNativeBridge.DispatchMode = GraphicsNativeDispatchMode.ForceNative;
                using var edgesNative = CannyEdgeDetector.Detect(img, lowThreshold: 30.0, highThreshold: 80.0, applyGaussian: true);

                Assert.Equal(w, edgesCs.Width);
                Assert.Equal(h, edgesCs.Height);
                Assert.Equal(w, edgesNative.Width);
                Assert.Equal(h, edgesNative.Height);

                int csEdgeCount = 0;
                int nativeEdgeCount = 0;
                int matchCount = 0;

                for (int y = 0; y < h; y++)
                {
                    byte* rCs = edgesCs.GetRowPointer(y);
                    byte* rNat = edgesNative.GetRowPointer(y);
                    for (int x = 0; x < w; x++)
                    {
                        if (rCs[x] == 255) csEdgeCount++;
                        if (rNat[x] == 255) nativeEdgeCount++;
                        if (rCs[x] == rNat[x]) matchCount++;
                    }
                }

                Assert.True(csEdgeCount > 100, $"Expected >100 edge pixels in C# mode, got {csEdgeCount}");
                Assert.True(nativeEdgeCount > 100, $"Expected >100 edge pixels in Native mode, got {nativeEdgeCount}");
                Assert.Equal(csEdgeCount, nativeEdgeCount);
                Assert.Equal(w * h, matchCount);
            }
            finally
            {
                ZeroGraphicsNativeBridge.DispatchMode = prevMode;
            }
        }

        [Fact]
        public unsafe void NccTemplateMatcher_Match_FindsSubPixelCoordinatesInBothModes()
        {
            int sw = 96;
            int sh = 96;
            int tw = 20;
            int th = 20;

            using var search = ImageBuffer.CreateGray8(sw, sh);
            using var template = ImageBuffer.CreateGray8(tw, th);

            for (int y = 0; y < sh; y++)
            {
                byte* row = search.GetRowPointer(y);
                for (int x = 0; x < sw; x++)
                {
                    row[x] = (byte)((x * 3 + y * 2) % 90);
                }
            }

            template.Clear(25);
            for (int y = 0; y < th; y++)
            {
                byte* row = template.GetRowPointer(y);
                for (int x = 0; x < tw; x++)
                {
                    if (x == 10 || y == 10 || (x >= 7 && x <= 13 && y >= 7 && y <= 13))
                    {
                        row[x] = 235;
                    }
                }
            }

            int targetX = 37;
            int targetY = 42;
            for (int ty = 0; ty < th; ty++)
            {
                byte* srcRow = template.GetRowPointer(ty);
                byte* dstRow = search.GetRowPointer(targetY + ty);
                for (int tx = 0; tx < tw; tx++)
                {
                    dstRow[targetX + tx] = srcRow[tx];
                }
            }

            var prevMode = ZeroGraphicsNativeBridge.DispatchMode;
            try
            {
                ZeroGraphicsNativeBridge.DispatchMode = GraphicsNativeDispatchMode.ForcePureCSharp;
                var resCs = NccTemplateMatcher.Match(search, template, minScore: 0.85, subPixelRefinement: true);

                ZeroGraphicsNativeBridge.DispatchMode = GraphicsNativeDispatchMode.ForceNative;
                var resNative = NccTemplateMatcher.Match(search, template, minScore: 0.85, subPixelRefinement: true);

                Assert.True(resCs.IsFound);
                Assert.True(resNative.IsFound);

                Assert.InRange(resCs.X, targetX - 0.25, targetX + 0.25);
                Assert.InRange(resCs.Y, targetY - 0.25, targetY + 0.25);
                Assert.InRange(resNative.X, targetX - 0.25, targetX + 0.25);
                Assert.InRange(resNative.Y, targetY - 0.25, targetY + 0.25);

                Assert.Equal(resCs.X, resNative.X, precision: 4);
                Assert.Equal(resCs.Y, resNative.Y, precision: 4);
                Assert.Equal(resCs.Score, resNative.Score, precision: 4);
            }
            finally
            {
                ZeroGraphicsNativeBridge.DispatchMode = prevMode;
            }
        }

        [Fact]
        public void SdfGenerator_GenerateSdf_MatchesAcrossPureCSharpAndNativeModes()
        {
            var path = new Path2D();
            path.MoveTo(12, 12);
            path.LineTo(52, 12);
            path.LineTo(52, 52);
            path.LineTo(12, 52);
            path.Close();

            int w = 64, h = 64;
            float spread = 6.0f;

            var prevMode = ZeroGraphicsNativeBridge.DispatchMode;
            try
            {
                ZeroGraphicsNativeBridge.DispatchMode = GraphicsNativeDispatchMode.ForcePureCSharp;
                byte[] sdfCs = SdfGenerator.GenerateSdf(path, w, h, 0, 0, 64, 64, spread);

                ZeroGraphicsNativeBridge.DispatchMode = GraphicsNativeDispatchMode.ForceNative;
                byte[] sdfNative = SdfGenerator.GenerateSdf(path, w, h, 0, 0, 64, 64, spread);

                Assert.Equal(w * h, sdfCs.Length);
                Assert.Equal(w * h, sdfNative.Length);

                // Inside center (32, 32) > 180
                Assert.True(sdfCs[32 * w + 32] > 180);
                Assert.True(sdfNative[32 * w + 32] > 180);

                // Outside corner (2, 2) < 70
                Assert.True(sdfCs[2 * w + 2] < 70);
                Assert.True(sdfNative[2 * w + 2] < 70);

                for (int i = 0; i < sdfCs.Length; i++)
                {
                    Assert.InRange(Math.Abs(sdfCs[i] - sdfNative[i]), 0, 1);
                }
            }
            finally
            {
                ZeroGraphicsNativeBridge.DispatchMode = prevMode;
            }
        }

        [Fact]
        public unsafe void MorphologyAndLttb_OperateAccuratelyInBothModes()
        {
            int w = 64, h = 64;
            using var src = ImageBuffer.CreateGray8(w, h);
            using var dilatedCs = ImageBuffer.CreateGray8(w, h);
            using var dilatedNat = ImageBuffer.CreateGray8(w, h);
            using var erodedCs = ImageBuffer.CreateGray8(w, h);
            using var erodedNat = ImageBuffer.CreateGray8(w, h);

            src.Clear(0);
            src.GetRowPointer(32)[32] = 255;

            var prevMode = ZeroGraphicsNativeBridge.DispatchMode;
            try
            {
                ZeroGraphicsNativeBridge.DispatchMode = GraphicsNativeDispatchMode.ForcePureCSharp;
                Morphology.Dilate(src, dilatedCs, radius: 1);
                Morphology.Erode(dilatedCs, erodedCs, radius: 1);

                ZeroGraphicsNativeBridge.DispatchMode = GraphicsNativeDispatchMode.ForceNative;
                Morphology.Dilate(src, dilatedNat, radius: 1);
                Morphology.Erode(dilatedNat, erodedNat, radius: 1);

                for (int y = 0; y < h; y++)
                {
                    byte* dCs = dilatedCs.GetRowPointer(y);
                    byte* dNat = dilatedNat.GetRowPointer(y);
                    byte* eCs = erodedCs.GetRowPointer(y);
                    byte* eNat = erodedNat.GetRowPointer(y);
                    for (int x = 0; x < w; x++)
                    {
                        Assert.Equal(dCs[x], dNat[x]);
                        Assert.Equal(eCs[x], eNat[x]);
                    }
                }

                // LTTB SoA test
                int n = 4096;
                int target = 64;
                double[] sx = new double[n];
                double[] sy = new double[n];
                for (int i = 0; i < n; i++)
                {
                    sx[i] = i;
                    sy[i] = Math.Sin(i * 0.05) * 100.0;
                }

                double[] dxCs = new double[target];
                double[] dyCs = new double[target];
                double[] dxNat = new double[target];
                double[] dyNat = new double[target];

                ZeroGraphicsNativeBridge.DispatchMode = GraphicsNativeDispatchMode.ForcePureCSharp;
                int wCs = LttbDecimation.Downsample(sx, sy, dxCs, dyCs, target);

                ZeroGraphicsNativeBridge.DispatchMode = GraphicsNativeDispatchMode.ForceNative;
                int wNat = LttbDecimation.Downsample(sx, sy, dxNat, dyNat, target);

                Assert.Equal(target, wCs);
                Assert.Equal(target, wNat);
                for (int i = 0; i < target; i++)
                {
                    Assert.Equal(dxCs[i], dxNat[i], precision: 6);
                    Assert.Equal(dyCs[i], dyNat[i], precision: 6);
                }
            }
            finally
            {
                ZeroGraphicsNativeBridge.DispatchMode = prevMode;
            }
        }
    }
}
