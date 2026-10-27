using System;
using System.IO;
using Xunit;
using ZeroGraphics.Rhi;
using ZeroGraphics.Rhi.Null;
using ZeroGraphics.Rhi.Vulkan;
using ZeroGraphics.Vector.Geometry;
using ZeroGraphics.Vector.Rendering;
using ZeroGraphics.Vector.Tessellation;
using ZeroGraphics.Vector.Text;

namespace ZeroGraphics.Tests
{
    public class TypographyAndVulkanTests
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

        #region TrueType Reader Tests

        [Fact]
        public void TrueTypeReader_ReadsBigEndianPrimitivesCorrectly()
        {
            // 0x1234 (4660), 0x8765 (-30875 as short), 0x01020304 (16909060)
            byte[] buffer = new byte[] { 0x12, 0x34, 0x87, 0x65, 0x01, 0x02, 0x03, 0x04 };
            var reader = new TrueTypeReader(buffer);

            Assert.Equal((ushort)0x1234, reader.ReadUInt16());
            Assert.Equal((short)unchecked((short)0x8765), reader.ReadInt16());
            Assert.Equal(0x01020304u, reader.ReadUInt32());
        }

        [Fact]
        public void TrueTypeReader_TagConversions_MatchFourCC()
        {
            uint cmapTag = TrueTypeReader.StringToTag("cmap");
            string str = TrueTypeReader.TagToString(cmapTag);
            Assert.Equal("cmap", str);

            uint headTag = TrueTypeReader.StringToTag("head");
            Assert.Equal("head", TrueTypeReader.TagToString(headTag));
        }

        [Fact]
        public void TrueTypeReader_F2Dot14_ConvertsAccurately()
        {
            // 1.0 in F2Dot14 is 0x4000 (16384 / 16384 = 1.0)
            byte[] buffer = new byte[] { 0x40, 0x00, 0xC0, 0x00 }; // 1.0 and -1.0
            var reader = new TrueTypeReader(buffer);

            Assert.Equal(1.0f, reader.ReadF2Dot14(), 4);
            Assert.Equal(-1.0f, reader.ReadF2Dot14(), 4);
        }

        #endregion

        #region TrueType Font Tests

        [Fact]
        public void TrueTypeFont_ParsesStandardSystemFont_MetricsAndGlyphs()
        {
            string? fontPath = GetSystemFontPath();
            if (fontPath == null) return; // Skip if fonts not on system (CI runner)

            var font = TrueTypeFont.FromFile(fontPath);
            Assert.NotNull(font);
            Assert.True(font.Metrics.UnitsPerEm > 0);
            Assert.True(font.NumGlyphs > 100);
            Assert.True(font.Metrics.Ascender > 0);
            Assert.True(font.Metrics.Descender < 0);

            // Check glyph index mapping for ASCII characters
            int idxA = font.GetGlyphIndex('A');
            int idxB = font.GetGlyphIndex('B');
            int idxZero = font.GetGlyphIndex('0');

            Assert.True(idxA > 0);
            Assert.True(idxB > 0);
            Assert.True(idxZero > 0);
            Assert.NotEqual(idxA, idxB);

            // Advance width
            float advA = font.GetGlyphAdvance(idxA);
            Assert.True(advA > 0);
        }

        [Fact]
        public void TrueTypeFont_GetGlyphPath_ProducesValidVectorPath()
        {
            string? fontPath = GetSystemFontPath();
            if (fontPath == null) return;

            var font = TrueTypeFont.FromFile(fontPath);
            var path = font.GetGlyphPath('A', 48.0f, 10.0f, 100.0f);

            Assert.NotNull(path);
            Assert.True(path.VerbCount >= 4, "Glyph 'A' outline must contain at least 4 path verbs");

            // Verify bounds
            path.GetBounds(out float minX, out float minY, out float maxX, out float maxY);
            Assert.True(maxX > minX);
            Assert.True(maxY > minY);
        }

        [Fact]
        public void TrueTypeFont_GetTextPath_CombinesMultipleGlyphsWithLayout()
        {
            string? fontPath = GetSystemFontPath();
            if (fontPath == null) return;

            var font = TrueTypeFont.FromFile(fontPath);
            string testText = "ZeroGraphics Pure C# 2026";
            var path = font.GetTextPath(testText, 24.0f, 0.0f, 50.0f);

            Assert.NotNull(path);
            Assert.True(path.VerbCount > 20);

            path.GetBounds(out float minX, out float minY, out float maxX, out float maxY);
            Assert.True((maxX - minX) > 100.0f, "Laid out text width should be wider than 100 units");
            Assert.True((maxY - minY) > 10.0f);

            // SVG export test
            string svg = SvgExporter.ExportSinglePath(path, 600, 200, null, "#000000", "#000000");
            Assert.Contains("<svg", svg);
            Assert.Contains("<path", svg);
            Assert.Contains("d=\"", svg);
        }

        #endregion

        #region VectorRenderer Text Integration Tests

        [Fact]
        public void VectorRenderer_DrawTextAndFillText_EmitsVerticesIntoMesh()
        {
            string? fontPath = GetSystemFontPath();
            if (fontPath == null) return;

            var font = TrueTypeFont.FromFile(fontPath);
            using var nullDevice = new NullRhiDevice();
            using var renderer = new VectorRenderer(nullDevice);

            // 1. DrawText (Stroked vector glyphs)
            var stroke = new StrokeStyle(2.0f, LineCap.Round, LineJoin.Round);
            renderer.DrawText("Zero", font, 36.0f, 10.0f, 50.0f, stroke, 0xFF00FF00);

            Assert.True(renderer.Mesh.VertexCount > 0);
            Assert.True(renderer.Mesh.IndexCount > 0);

            int vertexCountBefore = renderer.Mesh.VertexCount;

            // 2. FillText (Filled triangulated vector glyphs)
            renderer.FillText("GPU", font, 36.0f, 100.0f, 50.0f, 0xFFFFCC00);

            Assert.True(renderer.Mesh.VertexCount > vertexCountBefore);

            // 3. Flush into RHI CommandBuffer
            using var cmdBuffer = nullDevice.CreateCommandBuffer();
            cmdBuffer.Begin();
            renderer.Flush(cmdBuffer);
            cmdBuffer.End();

            // Mesh cleared after flush
            Assert.Equal(0, renderer.Mesh.VertexCount);
            Assert.Equal(0, renderer.Mesh.IndexCount);
        }

        #endregion

        #region Vulkan Native & RHI Device Tests

        [Fact]
        public void VulkanNative_MakeApiVersion_CalculatesCorrectBits()
        {
            uint v10 = VulkanNative.MakeApiVersion(0, 1, 0, 0);
            Assert.Equal((1u << 22), v10);

            uint v13 = VulkanNative.MakeApiVersion(0, 1, 3, 0);
            Assert.Equal((1u << 22) | (3u << 12), v13);
        }

        [Fact]
        public void VulkanRhiDevice_TryCreate_ExecutesWithoutException()
        {
            // TryCreate must safely return either a valid VulkanRhiDevice (if Vulkan driver is present)
            // or null (if driver/loader absent), never throwing an unhandled exception.
            using var vkDevice = VulkanRhiDevice.TryCreate();

            if (vkDevice != null)
            {
                Assert.Equal(RhiBackend.Vulkan, vkDevice.Backend);
                Assert.False(string.IsNullOrEmpty(vkDevice.DeviceName));
                Assert.NotEqual(IntPtr.Zero, vkDevice.NativeInstance);
                Assert.NotEqual(IntPtr.Zero, vkDevice.NativeDevice);

                // Test Buffer Creation & Upload
                var desc = new RhiBufferDesc(1024, RhiBufferType.Vertex, RhiBufferUsage.Dynamic);
                using var buffer = vkDevice.CreateBuffer(desc);
                Assert.NotNull(buffer);
                Assert.Equal(1024, buffer.SizeInBytes);

                var testFloats = new float[] { 1.0f, 2.0f, 3.0f, 4.0f };
                buffer.UpdateData<float>(testFloats);

                // Test Command Buffer Recording
                using var cmd = vkDevice.CreateCommandBuffer();
                cmd.Begin();
                cmd.SetVertexBuffer(0, buffer, sizeof(float) * 4);
                cmd.End();

                // Test Fence
                using var fence = vkDevice.CreateFence(10);
                Assert.Equal(10ul, fence.CompletedValue);
                fence.Signal(20);
                Assert.Equal(20ul, fence.CompletedValue);
                Assert.True(fence.Wait(20, 100));
            }
        }

        [Fact]
        public void VulkanRhiFence_TimelineSynchronization_WorksReliably()
        {
            // Test Vulkan fence timeline logic independently
            using var fence = new VulkanRhiFence(IntPtr.Zero, IntPtr.Zero, 100);
            Assert.Equal(100ul, fence.CompletedValue);

            Assert.True(fence.Wait(50, 10)); // Past value
            Assert.True(fence.Wait(100, 10)); // Current value

            fence.Signal(250);
            Assert.Equal(250ul, fence.CompletedValue);
            Assert.True(fence.Wait(200, 10));
            Assert.True(fence.Wait(250, 10));

            // Future value with timeout
            Assert.False(fence.Wait(500, 50));
        }

        #endregion

        #region Hardening & Edge Cases Tests

        [Fact]
        public void TrueTypeFont_GetGlyphAdvance_NegativeAndOutOfBoundsIndex_ReturnsFallback()
        {
            string? fontPath = GetSystemFontPath();
            if (fontPath == null) return;

            var font = TrueTypeFont.FromFile(fontPath);
            // Negative index should not throw IndexOutOfRangeException
            float advNeg = font.GetGlyphAdvance(-1);
            Assert.True(advNeg > 0);

            // Far out-of-bounds index should safely return fallback
            float advOverflow = font.GetGlyphAdvance(1_000_000);
            Assert.True(advOverflow > 0);
        }

        [Fact]
        public void PolygonTriangulator_CollinearPoints_ExitsWithoutGeneratingDegenerateGeometry()
        {
            var collinearPoints = new[]
            {
                new VectorPoint(0, 0),
                new VectorPoint(10, 0),
                new VectorPoint(20, 0),
                new VectorPoint(30, 0)
            };

            var mesh = new VectorMesh();
            PolygonTriangulator.TriangulatePolygon(collinearPoints, 0xFFFFFFFF, mesh);

            // Collinear polygon has zero area, should exit early without hanging or adding triangles
            Assert.Equal(0, mesh.IndexCount);
        }

        [Fact]
        public void PathStroker_ConsecutiveDuplicatePoints_StrokesWithoutDegenerateQuads()
        {
            var path = new Path2D();
            path.MoveTo(0, 0);
            path.LineTo(0, 0); // Duplicate point
            path.LineTo(10, 10);
            path.LineTo(10, 10); // Duplicate point
            path.LineTo(20, 0);

            var mesh = new VectorMesh();
            var stroke = new StrokeStyle(2.0f, LineCap.Round, LineJoin.Round);
            PathStroker.StrokePath(path, stroke, 0xFF00FF00, mesh);

            Assert.True(mesh.VertexCount > 0);
            Assert.True(mesh.IndexCount > 0);
        }

        [Fact]
        public void VectorRenderer_DrawAndFillText_ZeroOrNegativeFontSize_IgnoredSafely()
        {
            string? fontPath = GetSystemFontPath();
            if (fontPath == null) return;

            var font = TrueTypeFont.FromFile(fontPath);
            using var nullDevice = new NullRhiDevice();
            using var renderer = new VectorRenderer(nullDevice);

            var stroke = new StrokeStyle(1.0f);
            renderer.DrawText("IgnoreMe", font, 0.0f, 0, 0, stroke, 0xFFFFFFFF);
            renderer.DrawText("IgnoreMe", font, -12.0f, 0, 0, stroke, 0xFFFFFFFF);
            renderer.FillText("IgnoreMe", font, 0.0f, 0, 0, 0xFFFFFFFF);
            renderer.FillText("IgnoreMe", font, -24.0f, 0, 0, 0xFFFFFFFF);

            Assert.Equal(0, renderer.Mesh.VertexCount);
            Assert.Equal(0, renderer.Mesh.IndexCount);
        }

        [Fact]
        public void VectorRenderer_Flush_ThrowsObjectDisposedException_WhenDisposed()
        {
            using var nullDevice = new NullRhiDevice();
            var renderer = new VectorRenderer(nullDevice);
            using var cmdBuffer = nullDevice.CreateCommandBuffer();

            renderer.Dispose();

            Assert.Throws<ObjectDisposedException>(() => renderer.Flush(cmdBuffer));
        }

        [Fact]
        public void VulkanRhiCommandBuffer_Dispose_IsSafeAndIdempotent()
        {
            using var vkDevice = VulkanRhiDevice.TryCreate();
            if (vkDevice != null)
            {
                var cmd = vkDevice.CreateCommandBuffer();
                cmd.Dispose();
                // Second call should not throw
                cmd.Dispose();
            }
        }

        #endregion
    }
}
