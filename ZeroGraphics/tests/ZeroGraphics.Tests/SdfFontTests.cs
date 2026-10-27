using System;
using System.IO;
using Xunit;
using ZeroGraphics.Rhi;
using ZeroGraphics.Rhi.Null;
using ZeroGraphics.Vector.Geometry;
using ZeroGraphics.Vector.Rendering;
using ZeroGraphics.Vector.Text;
using ZeroGraphics.Vector.Text.Sdf;

namespace ZeroGraphics.Tests
{
    public class SdfFontTests
    {
        private static string? GetSystemFontPath()
        {
            string[] candidates = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "consola.ttf"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "segoeui.ttf"),
                "C:\\Windows\\Fonts\\arial.ttf"
            };

            foreach (var path in candidates)
            {
                if (File.Exists(path)) return path;
            }
            return null;
        }

        [Fact]
        public void SdfGenerator_DistanceField_ProducesGradientAcrossOutline()
        {
            // Create a 20x20 square vector path located from (10, 10) to (30, 30)
            var path = new Path2D();
            path.MoveTo(10, 10);
            path.LineTo(30, 10);
            path.LineTo(30, 30);
            path.LineTo(10, 30);
            path.Close();

            int w = 40, h = 40;
            float spread = 4.0f;
            byte[] sdf = SdfGenerator.GenerateSdf(path, w, h, 0, 0, 40, 40, spread);

            Assert.Equal(w * h, sdf.Length);

            // Center (20, 20) is deeply inside the square: signed distance is positive -> value > 128
            byte centerVal = sdf[20 * w + 20];
            Assert.True(centerVal > 180, $"Center should be inside (>180), actual: {centerVal}");

            // Far outside (2, 2): signed distance is negative -> value < 128
            byte outsideVal = sdf[2 * w + 2];
            Assert.True(outsideVal < 70, $"Outside point should be far outside (<70), actual: {outsideVal}");

            // Near boundary (10, 20): signed distance should be close to 0 -> value near 128
            byte edgeVal = sdf[20 * w + 10];
            Assert.InRange(edgeVal, 100, 155);
        }

        [Fact]
        public void FontAtlasPacker_PacksMultipleGlyphs_WithoutOverlap()
        {
            var packer = new FontAtlasPacker(width: 128, height: 128, padding: 2);
            int glyphW = 16;
            int glyphH = 16;
            byte[] mockGlyph = new byte[glyphW * glyphH];
            Array.Fill(mockGlyph, (byte)200);

            var packedRects = new System.Collections.Generic.List<(int X, int Y)>();

            for (int i = 0; i < 16; i++)
            {
                bool success = packer.TryPack(mockGlyph, glyphW, glyphH, out int px, out int py);
                Assert.True(success, $"Failed to pack glyph {i}");

                // Ensure within atlas bounds
                Assert.True(px >= 0 && px + glyphW <= packer.Width);
                Assert.True(py >= 0 && py + glyphH <= packer.Height);

                // Ensure non-overlapping with previously packed glyphs
                foreach (var other in packedRects)
                {
                    bool overlap = !(px + glyphW <= other.X || px >= other.X + glyphW ||
                                     py + glyphH <= other.Y || py >= other.Y + glyphH);
                    Assert.False(overlap, $"Glyph {i} at ({px},{py}) overlaps with ({other.X},{other.Y})");
                }

                packedRects.Add((px, py));
            }

            Assert.Equal(16, packedRects.Count);
        }

        [Fact]
        public void SdfFont_CreatesAtlasAndRhiTexture_FromTrueTypeFont()
        {
            string? fontPath = GetSystemFontPath();
            if (fontPath == null) return;

            var ttf = TrueTypeFont.FromFile(fontPath);
            var sdfFont = SdfFont.Create(
                ttf,
                characterSet: "ZeroGraphics 2026",
                glyphPixelSize: 32,
                spread: 4,
                atlasWidth: 256,
                atlasHeight: 256);

            Assert.NotNull(sdfFont);
            Assert.True(sdfFont.GlyphCount >= 10);
            Assert.Equal(256, sdfFont.AtlasWidth);
            Assert.Equal(256, sdfFont.AtlasHeight);

            // Test glyph lookup
            Assert.True(sdfFont.TryGetGlyph('Z', out var glyphZ));
            Assert.NotNull(glyphZ);
            Assert.True(glyphZ!.AdvanceWidth > 0);
            Assert.True(glyphZ.Width > 0);
            Assert.True(glyphZ.Height > 0);
            Assert.True(glyphZ.U1 > glyphZ.U0);
            Assert.True(glyphZ.V1 > glyphZ.V0);

            // Test space glyph
            Assert.True(sdfFont.TryGetGlyph(' ', out var glyphSpace));
            Assert.NotNull(glyphSpace);
            Assert.True(glyphSpace!.AdvanceWidth > 0);
            Assert.Equal(0, glyphSpace.Width);

            // Test RHI Texture Creation on Null Device
            using var nullDevice = new NullRhiDevice();
            using var texture = sdfFont.CreateRhiTexture(nullDevice);

            Assert.NotNull(texture);
            Assert.Equal(256, texture.Width);
            Assert.Equal(256, texture.Height);
            Assert.Equal(RhiFormat.R8_UNorm, texture.Format);
        }

        [Fact]
        public void VectorRenderer_DrawTextSdf_EmitsTexturedQuads()
        {
            string? fontPath = GetSystemFontPath();
            if (fontPath == null) return;

            var ttf = TrueTypeFont.FromFile(fontPath);
            var sdfFont = SdfFont.Create(
                ttf,
                characterSet: "Zero",
                glyphPixelSize: 32,
                spread: 4,
                atlasWidth: 256,
                atlasHeight: 256);

            using var nullDevice = new NullRhiDevice();
            using var renderer = new VectorRenderer(nullDevice);

            renderer.DrawTextSdf("Zero", sdfFont, fontSize: 24.0f, x: 10.0f, y: 30.0f, color: 0xFF00FF00);

            // "Zero" consists of 4 visible characters -> exactly 4 quads = 16 vertices, 24 indices, 8 triangles
            Assert.Equal(16, renderer.Mesh.VertexCount);
            Assert.Equal(24, renderer.Mesh.IndexCount);
            Assert.Equal(8, renderer.Mesh.TriangleCount);

            // Verify UVs are non-zero
            var v0 = renderer.Mesh.Vertices[0];
            Assert.True(v0.U >= 0.0f && v0.U <= 1.0f);
            Assert.True(v0.V >= 0.0f && v0.V <= 1.0f);

            // Flush into command buffer
            using var cmdBuffer = nullDevice.CreateCommandBuffer();
            cmdBuffer.Begin();
            renderer.Flush(cmdBuffer);
            cmdBuffer.End();

            Assert.Equal(0, renderer.Mesh.VertexCount);
            Assert.Equal(0, renderer.Mesh.IndexCount);
        }
    }
}
