using System;
using System.Runtime.InteropServices;
using ZeroGraphics.Rhi;
using ZeroGraphics.Vector.Geometry;
using ZeroGraphics.Vector.Tessellation;
using ZeroGraphics.Vector.Text;
using ZeroGraphics.Vector.Text.Sdf;

namespace ZeroGraphics.Vector.Rendering
{
    /// <summary>
    /// High-performance sovereign 2D vector graphics canvas and batch renderer.
    /// Batches vector paths, strokes, and filled polygons into GPU vertex and index buffers
    /// and records draw calls directly into an IRhiCommandBuffer.
    /// </summary>
    public sealed class VectorRenderer : IDisposable
    {
        private readonly IRhiDevice _device;
        private readonly VectorMesh _mesh;
        private IRhiBuffer? _vertexBuffer;
        private IRhiBuffer? _indexBuffer;
        private bool _disposed;

        public IRhiDevice Device => _device;
        public VectorMesh Mesh => _mesh;

        public VectorRenderer(IRhiDevice device, int initialVertexCapacity = 512, int initialIndexCapacity = 1024)
        {
            _device = device ?? throw new ArgumentNullException(nameof(device));
            _mesh = new VectorMesh(initialVertexCapacity, initialIndexCapacity);
        }

        public void DrawPath(Path2D path, StrokeStyle stroke, uint color, float tolerance = 0.5f)
        {
            if (path == null || path.VerbCount == 0) return;
            PathStroker.StrokePath(path, stroke, color, _mesh, tolerance);
        }

        public void FillPath(Path2D path, uint color, float tolerance = 0.5f)
        {
            if (path == null || path.VerbCount == 0) return;
            var subpaths = AdaptiveFlattening.Flatten(path, tolerance);

            for (int i = 0; i < subpaths.Count; i++)
            {
                var points = subpaths[i];
                if (points.Count >= 3)
                {
                    PolygonTriangulator.TriangulatePolygon(points, color, _mesh);
                }
            }
        }

        public void DrawLine(float x0, float y0, float x1, float y1, StrokeStyle stroke, uint color)
        {
            var p = new Path2D();
            p.MoveTo(x0, y0);
            p.LineTo(x1, y1);
            DrawPath(p, stroke, color);
        }

        public void DrawRect(float x, float y, float width, float height, StrokeStyle stroke, uint color)
        {
            var p = new Path2D();
            p.AddRect(x, y, width, height);
            DrawPath(p, stroke, color);
        }

        public void FillRect(float x, float y, float width, float height, uint color)
        {
            uint i0 = _mesh.AddVertex(x, y, color);
            uint i1 = _mesh.AddVertex(x + width, y, color);
            uint i2 = _mesh.AddVertex(x + width, y + height, color);
            uint i3 = _mesh.AddVertex(x, y + height, color);
            _mesh.AddQuad(i0, i1, i2, i3);
        }

        public void DrawCircle(float cx, float cy, float radius, StrokeStyle stroke, uint color, float tolerance = 0.5f)
        {
            var p = new Path2D();
            p.AddCircle(cx, cy, radius);
            DrawPath(p, stroke, color, tolerance);
        }

        public void FillCircle(float cx, float cy, float radius, uint color, int segments = 32)
        {
            if (segments < 3) segments = 3;
            uint center = _mesh.AddVertex(cx, cy, color);
            uint first = _mesh.AddVertex(cx + radius, cy, color);
            uint prev = first;

            for (int i = 1; i < segments; i++)
            {
                double angle = (2.0 * Math.PI * i) / segments;
                float px = cx + (float)(Math.Cos(angle) * radius);
                float py = cy + (float)(Math.Sin(angle) * radius);
                uint curr = _mesh.AddVertex(px, py, color);
                _mesh.AddTriangle(center, prev, curr);
                prev = curr;
            }

            _mesh.AddTriangle(center, prev, first);
        }

        public void DrawText(string text, TrueTypeFont font, float fontSize, float x, float y, StrokeStyle stroke, uint color, float tolerance = 0.5f)
        {
            if (string.IsNullOrEmpty(text) || font == null || fontSize <= 0.0f) return;
            var path = font.GetTextPath(text, fontSize, x, y);
            DrawPath(path, stroke, color, tolerance);
        }

        public void FillText(string text, TrueTypeFont font, float fontSize, float x, float y, uint color, float tolerance = 0.5f)
        {
            if (string.IsNullOrEmpty(text) || font == null || fontSize <= 0.0f) return;
            var path = font.GetTextPath(text, fontSize, x, y);
            FillPath(path, color, tolerance);
        }

        /// <summary>
        /// Draws pre-baked Signed Distance Field (SDF) text using textured quads (4 vertices, 2 triangles per character).
        /// Achieves extreme rendering performance compared to dynamic polygon tessellation.
        /// </summary>
        public void DrawTextSdf(string text, SdfFont font, float fontSize, float x, float y, uint color)
        {
            if (string.IsNullOrEmpty(text) || font == null || fontSize <= 0.0f) return;

            float scale = fontSize / font.GlyphPixelSize;
            float cursorX = x;
            float cursorY = y;
            float lineHeight = font.SourceFont.Metrics.LineHeight * (fontSize / font.SourceFont.Metrics.UnitsPerEm);

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '\n')
                {
                    cursorX = x;
                    cursorY += lineHeight;
                    continue;
                }

                if (!font.TryGetGlyph(c, out var glyph) || glyph == null)
                {
                    continue;
                }

                if (glyph.Width > 0 && glyph.Height > 0)
                {
                    float gx = cursorX + glyph.BearingX * scale;
                    float gy = cursorY + glyph.BearingY * scale;
                    float gw = glyph.Width * scale;
                    float gh = glyph.Height * scale;

                    _mesh.AddQuad(
                        gx, gy,
                        gx + gw, gy + gh,
                        glyph.U0, glyph.V0,
                        glyph.U1, glyph.V1,
                        color);
                }

                cursorX += glyph.AdvanceWidth * scale;
            }
        }

        /// <summary>
        /// Flushes the batched vector mesh to GPU vertex and index buffers and records the draw call into the command buffer.
        /// </summary>
        public unsafe void Flush(IRhiCommandBuffer commandBuffer)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(VectorRenderer));
            if (commandBuffer == null) throw new ArgumentNullException(nameof(commandBuffer));
            if (_mesh.VertexCount == 0 || _mesh.IndexCount == 0) return;

            int vertexBytes = _mesh.VertexCount * sizeof(VectorVertex);
            int indexBytes = _mesh.IndexCount * sizeof(uint);

            EnsureBuffers(vertexBytes, indexBytes);

            // Upload vertex data
            var vertices = _mesh.Vertices;
            var vertexArray = new VectorVertex[vertices.Count];
            for (int i = 0; i < vertices.Count; i++) vertexArray[i] = vertices[i];
            _vertexBuffer!.UpdateData<VectorVertex>(vertexArray);

            // Upload index data
            var indices = _mesh.Indices;
            var indexArray = new uint[indices.Count];
            for (int i = 0; i < indices.Count; i++) indexArray[i] = indices[i];
            _indexBuffer!.UpdateData<uint>(indexArray);

            // Record draw call into command buffer
            commandBuffer.SetVertexBuffer(0, _vertexBuffer, sizeof(VectorVertex), 0);
            commandBuffer.SetIndexBuffer(_indexBuffer, RhiIndexFormat.ThirtyTwoBit, 0);
            commandBuffer.DrawIndexed(_mesh.IndexCount, 0, 0);

            // Clear CPU mesh for next frame
            _mesh.Clear();
        }

        private void EnsureBuffers(int requiredVertexBytes, int requiredIndexBytes)
        {
            if (_vertexBuffer == null || _vertexBuffer.SizeInBytes < requiredVertexBytes)
            {
                _vertexBuffer?.Dispose();
                int newSize = Math.Max(requiredVertexBytes, 16384);
                var desc = new RhiBufferDesc(newSize, RhiBufferType.Vertex, RhiBufferUsage.Dynamic);
                _vertexBuffer = _device.CreateBuffer(desc);
            }

            if (_indexBuffer == null || _indexBuffer.SizeInBytes < requiredIndexBytes)
            {
                _indexBuffer?.Dispose();
                int newSize = Math.Max(requiredIndexBytes, 32768);
                var desc = new RhiBufferDesc(newSize, RhiBufferType.Index, RhiBufferUsage.Dynamic);
                _indexBuffer = _device.CreateBuffer(desc);
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _vertexBuffer?.Dispose();
                _indexBuffer?.Dispose();
                _disposed = true;
            }
        }
    }
}
