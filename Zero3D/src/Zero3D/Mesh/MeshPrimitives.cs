using System;
using Zero3D.Math;
using Zero3D.Materials;

namespace Zero3D.Mesh
{
    /// <summary>
    /// Procedural 3D mesh primitive generator for rapid prototyping, CAD placeholding,
    /// and visual debugging (Cube, Sphere, Cylinder, Plane, Torus, Triad).
    /// </summary>
    public static class MeshPrimitives
    {
        /// <summary>
        /// Creates an axis-aligned 3D Box / Cube centered at (0, 0, 0).
        /// </summary>
        public static Mesh3D CreateBox(float width, float height, float depth)
        {
            var mesh = new Mesh3D { Name = $"Box_{width}x{height}x{depth}" };
            float hx = width * 0.5f;
            float hy = height * 0.5f;
            float hz = depth * 0.5f;

            // 6 faces * 4 vertices = 24 vertices (distinct normals per face)
            void AddFace(Vec3 n, Vec3 v0, Vec3 v1, Vec3 v2, Vec3 v3)
            {
                int start = mesh.Vertices.Count;
                mesh.Vertices.Add(new Vertex3D(v0, n, new Vec2(0, 1)));
                mesh.Vertices.Add(new Vertex3D(v1, n, new Vec2(1, 1)));
                mesh.Vertices.Add(new Vertex3D(v2, n, new Vec2(1, 0)));
                mesh.Vertices.Add(new Vertex3D(v3, n, new Vec2(0, 0)));

                mesh.Indices.Add(start);
                mesh.Indices.Add(start + 1);
                mesh.Indices.Add(start + 2);

                mesh.Indices.Add(start);
                mesh.Indices.Add(start + 2);
                mesh.Indices.Add(start + 3);
            }

            // Front (+Z)
            AddFace(Vec3.UnitZ, new Vec3(-hx, -hy, hz), new Vec3(hx, -hy, hz), new Vec3(hx, hy, hz), new Vec3(-hx, hy, hz));
            // Back (-Z)
            AddFace(new Vec3(0, 0, -1), new Vec3(hx, -hy, -hz), new Vec3(-hx, -hy, -hz), new Vec3(-hx, hy, -hz), new Vec3(hx, hy, -hz));
            // Top (+Y)
            AddFace(Vec3.UnitY, new Vec3(-hx, hy, hz), new Vec3(hx, hy, hz), new Vec3(hx, hy, -hz), new Vec3(-hx, hy, -hz));
            // Bottom (-Y)
            AddFace(new Vec3(0, -1, 0), new Vec3(-hx, -hy, -hz), new Vec3(hx, -hy, -hz), new Vec3(hx, -hy, hz), new Vec3(-hx, -hy, hz));
            // Right (+X)
            AddFace(Vec3.UnitX, new Vec3(hx, -hy, hz), new Vec3(hx, -hy, -hz), new Vec3(hx, hy, -hz), new Vec3(hx, hy, hz));
            // Left (-X)
            AddFace(new Vec3(-1, 0, 0), new Vec3(-hx, -hy, -hz), new Vec3(-hx, -hy, hz), new Vec3(-hx, hy, hz), new Vec3(-hx, hy, -hz));

            mesh.ComputeBounds();
            return mesh;
        }

        public static Mesh3D CreateCube(float size) => CreateBox(size, size, size);

        /// <summary>
        /// Creates a UV Sphere centered at (0, 0, 0).
        /// </summary>
        public static Mesh3D CreateSphere(float radius, int segments = 24, int rings = 16)
        {
            var mesh = new Mesh3D { Name = $"Sphere_R{radius}" };

            for (int ring = 0; ring <= rings; ring++)
            {
                float v = (float)ring / rings;
                float phi = v * (float)System.Math.PI;

                for (int seg = 0; seg <= segments; seg++)
                {
                    float u = (float)seg / segments;
                    float theta = u * (float)(System.Math.PI * 2.0);

                    float x = (float)(System.Math.Sin(phi) * System.Math.Cos(theta));
                    float y = (float)System.Math.Cos(phi);
                    float z = (float)(System.Math.Sin(phi) * System.Math.Sin(theta));

                    var normal = new Vec3(x, y, z);
                    var pos = normal * radius;

                    mesh.Vertices.Add(new Vertex3D(pos, normal, new Vec2(u, v)));
                }
            }

            int stride = segments + 1;
            for (int ring = 0; ring < rings; ring++)
            {
                for (int seg = 0; seg < segments; seg++)
                {
                    int i0 = ring * stride + seg;
                    int i1 = (ring + 1) * stride + seg;
                    int i2 = (ring + 1) * stride + (seg + 1);
                    int i3 = ring * stride + (seg + 1);

                    mesh.Indices.Add(i0);
                    mesh.Indices.Add(i1);
                    mesh.Indices.Add(i2);

                    mesh.Indices.Add(i0);
                    mesh.Indices.Add(i2);
                    mesh.Indices.Add(i3);
                }
            }

            mesh.ComputeBounds();
            return mesh;
        }

        /// <summary>
        /// Creates a Cylinder along the Y axis centered at (0, 0, 0).
        /// </summary>
        public static Mesh3D CreateCylinder(float radius, float height, int segments = 24)
        {
            var mesh = new Mesh3D { Name = $"Cylinder_R{radius}_H{height}" };
            float halfH = height * 0.5f;

            // Side vertices
            for (int seg = 0; seg <= segments; seg++)
            {
                float u = (float)seg / segments;
                float theta = u * (float)(System.Math.PI * 2.0);

                float cos = (float)System.Math.Cos(theta);
                float sin = (float)System.Math.Sin(theta);

                var normal = new Vec3(cos, 0, sin);
                var posTop = new Vec3(cos * radius, halfH, sin * radius);
                var posBottom = new Vec3(cos * radius, -halfH, sin * radius);

                mesh.Vertices.Add(new Vertex3D(posTop, normal, new Vec2(u, 0)));
                mesh.Vertices.Add(new Vertex3D(posBottom, normal, new Vec2(u, 1)));
            }

            for (int seg = 0; seg < segments; seg++)
            {
                int t0 = seg * 2;
                int b0 = seg * 2 + 1;
                int t1 = (seg + 1) * 2;
                int b1 = (seg + 1) * 2 + 1;

                mesh.Indices.Add(t0);
                mesh.Indices.Add(b0);
                mesh.Indices.Add(b1);

                mesh.Indices.Add(t0);
                mesh.Indices.Add(b1);
                mesh.Indices.Add(t1);
            }

            // Top Cap (+Y)
            int topCenterIdx = mesh.Vertices.Count;
            mesh.Vertices.Add(new Vertex3D(new Vec3(0, halfH, 0), Vec3.UnitY, new Vec2(0.5f, 0.5f)));

            for (int seg = 0; seg <= segments; seg++)
            {
                float theta = (float)seg / segments * (float)(System.Math.PI * 2.0);
                float cos = (float)System.Math.Cos(theta);
                float sin = (float)System.Math.Sin(theta);
                mesh.Vertices.Add(new Vertex3D(new Vec3(cos * radius, halfH, sin * radius), Vec3.UnitY, new Vec2(cos * 0.5f + 0.5f, sin * 0.5f + 0.5f)));
            }

            for (int seg = 0; seg < segments; seg++)
            {
                mesh.Indices.Add(topCenterIdx);
                mesh.Indices.Add(topCenterIdx + 1 + seg);
                mesh.Indices.Add(topCenterIdx + 1 + seg + 1);
            }

            // Bottom Cap (-Y)
            int botCenterIdx = mesh.Vertices.Count;
            mesh.Vertices.Add(new Vertex3D(new Vec3(0, -halfH, 0), new Vec3(0, -1, 0), new Vec2(0.5f, 0.5f)));

            for (int seg = 0; seg <= segments; seg++)
            {
                float theta = (float)seg / segments * (float)(System.Math.PI * 2.0);
                float cos = (float)System.Math.Cos(theta);
                float sin = (float)System.Math.Sin(theta);
                mesh.Vertices.Add(new Vertex3D(new Vec3(cos * radius, -halfH, sin * radius), new Vec3(0, -1, 0), new Vec2(cos * 0.5f + 0.5f, sin * 0.5f + 0.5f)));
            }

            for (int seg = 0; seg < segments; seg++)
            {
                mesh.Indices.Add(botCenterIdx);
                mesh.Indices.Add(botCenterIdx + 1 + seg + 1);
                mesh.Indices.Add(botCenterIdx + 1 + seg);
            }

            mesh.ComputeBounds();
            return mesh;
        }

        /// <summary>
        /// Creates a horizontal Grid Plane (XZ) with subdivisions.
        /// </summary>
        public static Mesh3D CreatePlane(float width, float depth, int subdivX = 1, int subdivZ = 1)
        {
            var mesh = new Mesh3D { Name = $"Plane_{width}x{depth}" };
            float hx = width * 0.5f;
            float hz = depth * 0.5f;

            for (int z = 0; z <= subdivZ; z++)
            {
                float fz = (float)z / subdivZ;
                float pz = -hz + fz * depth;

                for (int x = 0; x <= subdivX; x++)
                {
                    float fx = (float)x / subdivX;
                    float px = -hx + fx * width;

                    mesh.Vertices.Add(new Vertex3D(new Vec3(px, 0, pz), Vec3.UnitY, new Vec2(fx, fz)));
                }
            }

            int stride = subdivX + 1;
            for (int z = 0; z < subdivZ; z++)
            {
                for (int x = 0; x < subdivX; x++)
                {
                    int i0 = z * stride + x;
                    int i1 = (z + 1) * stride + x;
                    int i2 = (z + 1) * stride + (x + 1);
                    int i3 = z * stride + (x + 1);

                    mesh.Indices.Add(i0);
                    mesh.Indices.Add(i1);
                    mesh.Indices.Add(i2);

                    mesh.Indices.Add(i0);
                    mesh.Indices.Add(i2);
                    mesh.Indices.Add(i3);
                }
            }

            mesh.ComputeBounds();
            return mesh;
        }

        /// <summary>
        /// Creates a Torus (doughnut) shape.
        /// </summary>
        public static Mesh3D CreateTorus(float majorRadius, float minorRadius, int majorSegments = 32, int minorSegments = 16)
        {
            var mesh = new Mesh3D { Name = $"Torus_R{majorRadius}_r{minorRadius}" };

            for (int i = 0; i <= majorSegments; i++)
            {
                float u = (float)i / majorSegments;
                float theta = u * (float)(System.Math.PI * 2.0);
                float cosTheta = (float)System.Math.Cos(theta);
                float sinTheta = (float)System.Math.Sin(theta);

                for (int j = 0; j <= minorSegments; j++)
                {
                    float v = (float)j / minorSegments;
                    float phi = v * (float)(System.Math.PI * 2.0);
                    float cosPhi = (float)System.Math.Cos(phi);
                    float sinPhi = (float)System.Math.Sin(phi);

                    float x = (majorRadius + minorRadius * cosPhi) * cosTheta;
                    float y = minorRadius * sinPhi;
                    float z = (majorRadius + minorRadius * cosPhi) * sinTheta;

                    var normal = new Vec3(cosPhi * cosTheta, sinPhi, cosPhi * sinTheta).Normalize();
                    mesh.Vertices.Add(new Vertex3D(new Vec3(x, y, z), normal, new Vec2(u, v)));
                }
            }

            int stride = minorSegments + 1;
            for (int i = 0; i < majorSegments; i++)
            {
                for (int j = 0; j < minorSegments; j++)
                {
                    int i0 = i * stride + j;
                    int i1 = (i + 1) * stride + j;
                    int i2 = (i + 1) * stride + (j + 1);
                    int i3 = i * stride + (j + 1);

                    mesh.Indices.Add(i0);
                    mesh.Indices.Add(i1);
                    mesh.Indices.Add(i2);

                    mesh.Indices.Add(i0);
                    mesh.Indices.Add(i2);
                    mesh.Indices.Add(i3);
                }
            }

            mesh.ComputeBounds();
            return mesh;
        }
    }
}
