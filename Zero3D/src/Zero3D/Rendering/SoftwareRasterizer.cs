using System;
using System.Collections.Generic;
using Zero3D.Camera;
using Zero3D.Lighting;
using Zero3D.Materials;
using Zero3D.Math;
using Zero3D.Mesh;
using Zero3D.Scene;

namespace Zero3D.Rendering
{
    /// <summary>
    /// Pure C# CPU-based Software Rasterizer with Z-buffer depth testing, Barycentric triangle interpolation,
    /// Bresenham 3D line drawing, and Blinn-Phong lighting.
    /// Enables headless rendering on server environments, report image generation, and UI preview without GPU hardware.
    /// </summary>
    public class SoftwareRasterizer
    {
        public int Width { get; private set; }
        public int Height { get; private set; }

        public uint[] ColorBuffer { get; private set; } = Array.Empty<uint>();
        public float[] DepthBuffer { get; private set; } = Array.Empty<float>();

        public SoftwareRasterizer(int width = 800, int height = 600)
        {
            Resize(width, height);
        }

        public void Resize(int width, int height)
        {
            if (width <= 0) width = 1;
            if (height <= 0) height = 1;

            Width = width;
            Height = height;
            int totalPixels = width * height;

            if (ColorBuffer.Length != totalPixels)
            {
                ColorBuffer = new uint[totalPixels];
                DepthBuffer = new float[totalPixels];
            }
        }

        public void Clear(ColorRgb color, float clearDepth = float.MaxValue)
        {
            uint packedColor = PackColor(color, 1.0f);
            for (int i = 0; i < ColorBuffer.Length; i++)
            {
                ColorBuffer[i] = packedColor;
                DepthBuffer[i] = clearDepth;
            }
        }

        public byte[] GetPixelBytes()
        {
            byte[] bytes = new byte[Width * Height * 4];
            Buffer.BlockCopy(ColorBuffer, 0, bytes, 0, bytes.Length);
            return bytes;
        }

        public static uint PackColor(ColorRgb c, float alpha = 1.0f)
        {
            byte a = (byte)System.Math.Max(0, System.Math.Min(255, (int)(alpha * 255f)));
            byte r = (byte)System.Math.Max(0, System.Math.Min(255, (int)(c.R * 255f)));
            byte g = (byte)System.Math.Max(0, System.Math.Min(255, (int)(c.G * 255f)));
            byte b = (byte)System.Math.Max(0, System.Math.Min(255, (int)(c.B * 255f)));
            return (uint)((a << 24) | (r << 16) | (g << 8) | b);
        }

        /// <summary>
        /// Renders an entire Scene3D to the software framebuffer.
        /// </summary>
        public void Render(Scene3D scene, int? targetWidth = null, int? targetHeight = null)
        {
            if (targetWidth.HasValue && targetHeight.HasValue)
            {
                Resize(targetWidth.Value, targetHeight.Value);
            }

            var renderer = new SceneRenderer();
            var stats = renderer.PrepareFrame(scene);

            Clear(new ColorRgb(0.12f, 0.12f, 0.14f));

            var vp = scene.Camera.GetViewProjectionMatrix();
            IReadOnlyList<Light3D> lights = scene.Lights.Count > 0 
                ? (IReadOnlyList<Light3D>)scene.Lights 
                : new[] { Light3D.CreateDirectional(new Vec3(-0.5f, -1f, -0.5f), ColorRgb.White, 1.0f) };

            // Render opaque queue
            for (int i = 0; i < renderer.OpaqueQueue.Count; i++)
            {
                DrawCommand(renderer.OpaqueQueue[i], vp, scene.Camera.Position, lights);
            }

            // Render transparent queue
            for (int i = 0; i < renderer.TransparentQueue.Count; i++)
            {
                DrawCommand(renderer.TransparentQueue[i], vp, scene.Camera.Position, lights);
            }

            // Render wireframe queue
            for (int i = 0; i < renderer.WireframeQueue.Count; i++)
            {
                DrawWireframeCommand(renderer.WireframeQueue[i], vp);
            }
        }

        private void DrawCommand(RenderCommand cmd, Mat4 viewProj, Vec3 viewPos, IReadOnlyList<Light3D> lights)
        {
            var mesh = cmd.Mesh;
            if (mesh == null || mesh.Vertices.Count < 3) return;

            var world = cmd.WorldTransform;
            var wvp = world * viewProj;
            var mat = cmd.Material;

            bool hasIndices = mesh.Indices.Count >= 3;
            int count = hasIndices ? mesh.Indices.Count : mesh.Vertices.Count;

            for (int i = 0; i < count; i += 3)
            {
                Vertex3D v0 = hasIndices ? mesh.Vertices[mesh.Indices[i]] : mesh.Vertices[i];
                Vertex3D v1 = hasIndices ? mesh.Vertices[mesh.Indices[i + 1]] : mesh.Vertices[i + 1];
                Vertex3D v2 = hasIndices ? mesh.Vertices[mesh.Indices[i + 2]] : mesh.Vertices[i + 2];

                // World positions
                Vec3 w0 = world.TransformPoint(v0.Position);
                Vec3 w1 = world.TransformPoint(v1.Position);
                Vec3 w2 = world.TransformPoint(v2.Position);

                // Clip space coordinates
                Vec4 c0 = TransformToClipSpace(v0.Position, wvp);
                Vec4 c1 = TransformToClipSpace(v1.Position, wvp);
                Vec4 c2 = TransformToClipSpace(v2.Position, wvp);

                // Near plane frustum clipping (simple discard if behind camera)
                if (c0.W <= 0.001f && c1.W <= 0.001f && c2.W <= 0.001f) continue;
                if (c0.W <= 0.001f || c1.W <= 0.001f || c2.W <= 0.001f) continue;

                // Screen coordinates
                Vec3 s0 = ClipToScreen(c0);
                Vec3 s1 = ClipToScreen(c1);
                Vec3 s2 = ClipToScreen(c2);

                // Backface culling in screen space (cross product Z)
                float crossZ = (s1.X - s0.X) * (s2.Y - s0.Y) - (s1.Y - s0.Y) * (s2.X - s0.X);
                if (!mat.DoubleSided && crossZ <= 0) continue;

                // Calculate lighting (Flat shading via face center & normal)
                Vec3 faceNormal = Vec3.Cross(w1 - w0, w2 - w0).Normalize();
                Vec3 faceCenter = (w0 + w1 + w2) * (1f / 3f);
                ColorRgb shadedColor = SceneRenderer.ComputeBlinnPhongShading(faceCenter, faceNormal, viewPos, mat, lights);

                RasterizeTriangle(s0, s1, s2, shadedColor, mat.Alpha);
            }
        }

        private void DrawWireframeCommand(RenderCommand cmd, Mat4 viewProj)
        {
            var mesh = cmd.Mesh;
            if (mesh == null || mesh.Vertices.Count < 3) return;

            var world = cmd.WorldTransform;
            var wvp = world * viewProj;
            uint color = PackColor(cmd.Material.Albedo, cmd.Material.Alpha);

            bool hasIndices = mesh.Indices.Count >= 3;
            int count = hasIndices ? mesh.Indices.Count : mesh.Vertices.Count;

            for (int i = 0; i < count; i += 3)
            {
                Vertex3D v0 = hasIndices ? mesh.Vertices[mesh.Indices[i]] : mesh.Vertices[i];
                Vertex3D v1 = hasIndices ? mesh.Vertices[mesh.Indices[i + 1]] : mesh.Vertices[i + 1];
                Vertex3D v2 = hasIndices ? mesh.Vertices[mesh.Indices[i + 2]] : mesh.Vertices[i + 2];

                Vec4 c0 = TransformToClipSpace(v0.Position, wvp);
                Vec4 c1 = TransformToClipSpace(v1.Position, wvp);
                Vec4 c2 = TransformToClipSpace(v2.Position, wvp);

                if (c0.W <= 0.001f || c1.W <= 0.001f || c2.W <= 0.001f) continue;

                Vec3 s0 = ClipToScreen(c0);
                Vec3 s1 = ClipToScreen(c1);
                Vec3 s2 = ClipToScreen(c2);

                DrawLine(s0, s1, color);
                DrawLine(s1, s2, color);
                DrawLine(s2, s0, color);
            }
        }

        private void RasterizeTriangle(Vec3 s0, Vec3 s1, Vec3 s2, ColorRgb color, float alpha)
        {
            // Bounding box of triangle in screen space
            int minX = System.Math.Max(0, (int)System.Math.Floor(System.Math.Min(s0.X, System.Math.Min(s1.X, s2.X))));
            int maxX = System.Math.Min(Width - 1, (int)System.Math.Ceiling(System.Math.Max(s0.X, System.Math.Max(s1.X, s2.X))));
            int minY = System.Math.Max(0, (int)System.Math.Floor(System.Math.Min(s0.Y, System.Math.Min(s1.Y, s2.Y))));
            int maxY = System.Math.Min(Height - 1, (int)System.Math.Ceiling(System.Math.Max(s0.Y, System.Math.Max(s1.Y, s2.Y))));

            if (minX > maxX || minY > maxY) return;

            float denom = (s1.Y - s2.Y) * (s0.X - s2.X) + (s2.X - s1.X) * (s0.Y - s2.Y);
            if (System.Math.Abs(denom) < 1e-7f) return;
            float invDenom = 1f / denom;

            uint packed = PackColor(color, alpha);

            for (int y = minY; y <= maxY; y++)
            {
                int rowOffset = y * Width;
                float py = y + 0.5f;

                for (int x = minX; x <= maxX; x++)
                {
                    float px = x + 0.5f;

                    // Barycentric weights
                    float w0 = ((s1.Y - s2.Y) * (px - s2.X) + (s2.X - s1.X) * (py - s2.Y)) * invDenom;
                    float w1 = ((s2.Y - s0.Y) * (px - s2.X) + (s0.X - s2.X) * (py - s2.Y)) * invDenom;
                    float w2 = 1.0f - w0 - w1;

                    if (w0 >= 0f && w1 >= 0f && w2 >= 0f)
                    {
                        float z = w0 * s0.Z + w1 * s1.Z + w2 * s2.Z;
                        int idx = rowOffset + x;

                        if (z < DepthBuffer[idx])
                        {
                            DepthBuffer[idx] = z;
                            ColorBuffer[idx] = packed;
                        }
                    }
                }
            }
        }

        public void DrawLine(Vec3 p0, Vec3 p1, uint color)
        {
            int x0 = (int)p0.X;
            int y0 = (int)p0.Y;
            int x1 = (int)p1.X;
            int y1 = (int)p1.Y;

            int dx = System.Math.Abs(x1 - x0);
            int dy = System.Math.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int err = dx - dy;

            float dist = (float)System.Math.Sqrt(dx * dx + dy * dy);
            float step = dist > 0 ? 1f / dist : 1f;
            float t = 0f;

            while (true)
            {
                if (x0 >= 0 && x0 < Width && y0 >= 0 && y0 < Height)
                {
                    float z = p0.Z + (p1.Z - p0.Z) * t;
                    int idx = y0 * Width + x0;
                    if (z <= DepthBuffer[idx])
                    {
                        DepthBuffer[idx] = z;
                        ColorBuffer[idx] = color;
                    }
                }

                if (x0 == x1 && y0 == y1) break;

                int e2 = 2 * err;
                if (e2 > -dy)
                {
                    err -= dy;
                    x0 += sx;
                }
                if (e2 < dx)
                {
                    err += dx;
                    y0 += sy;
                }
                t += step;
            }
        }

        private struct Vec4
        {
            public float X, Y, Z, W;
            public Vec4(float x, float y, float z, float w) { X = x; Y = y; Z = z; W = w; }
        }

        private static Vec4 TransformToClipSpace(Vec3 p, Mat4 m)
        {
            return new Vec4(
                p.X * m.M11 + p.Y * m.M21 + p.Z * m.M31 + m.M41,
                p.X * m.M12 + p.Y * m.M22 + p.Z * m.M32 + m.M42,
                p.X * m.M13 + p.Y * m.M23 + p.Z * m.M33 + m.M43,
                p.X * m.M14 + p.Y * m.M24 + p.Z * m.M34 + m.M44
            );
        }

        private Vec3 ClipToScreen(Vec4 c)
        {
            float invW = 1f / c.W;
            float ndcX = c.X * invW;
            float ndcY = c.Y * invW;
            float ndcZ = c.Z * invW;

            // NDC [-1, 1] to screen [0, Width] x [0, Height]
            float screenX = (ndcX + 1f) * 0.5f * Width;
            float screenY = (1f - (ndcY + 1f) * 0.5f) * Height; // Invert Y for screen top-left

            return new Vec3(screenX, screenY, ndcZ);
        }
    }
}
