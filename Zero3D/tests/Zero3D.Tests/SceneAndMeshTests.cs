using System;
using System.IO;
using System.Text;
using Xunit;
using Zero3D.Camera;
using Zero3D.IO;
using Zero3D.Lighting;
using Zero3D.Materials;
using Zero3D.Math;
using Zero3D.Mesh;
using Zero3D.Rendering;
using Zero3D.Scene;

namespace Zero3D.Tests
{
    public class SceneAndMeshTests
    {
        [Fact]
        public void Math3D_Transform_And_Aabb_Tests()
        {
            var p = new Vec3(10f, 20f, 30f);
            var t = Mat4.CreateTranslation(5f, -10f, 15f);
            var res = t.TransformPoint(p);

            Assert.Equal(15f, res.X, 3);
            Assert.Equal(10f, res.Y, 3);
            Assert.Equal(45f, res.Z, 3);

            var aabb1 = new Aabb3D(new Vec3(0, 0, 0), new Vec3(10, 10, 10));
            var aabb2 = new Aabb3D(new Vec3(5, 5, 5), new Vec3(15, 15, 15));
            var aabb3 = new Aabb3D(new Vec3(20, 20, 20), new Vec3(30, 30, 30));

            Assert.True(aabb1.Intersects(aabb2));
            Assert.False(aabb1.Intersects(aabb3));
            Assert.True(aabb1.Contains(new Vec3(5, 5, 5)));

            var ray = new Ray3D(new Vec3(5, 5, -10), Vec3.UnitZ);
            Assert.True(aabb1.Intersects(ray, out float hitDist));
            Assert.Equal(10f, hitDist, 2);
        }

        [Fact]
        public void Camera_Projection_And_Unproject_Tests()
        {
            var cam = new Camera3D
            {
                Position = new Vec3(0f, 0f, 100f),
                Target = Vec3.Zero,
                Up = Vec3.UnitY,
                FovYDegrees = 60f,
                AspectRatio = 1.0f,
                NearPlane = 1f,
                FarPlane = 1000f
            };

            var viewMat = cam.GetViewMatrix();
            var projMat = cam.GetProjectionMatrix();
            var vp = cam.GetViewProjectionMatrix();

            Assert.False(float.IsNaN(vp.M11));

            // Screen center unproject should point straight forward (0, 0, -1)
            var centerRay = cam.ScreenPointToRay(400, 300, 800, 600);
            Assert.Equal(0f, centerRay.Direction.X, 2);
            Assert.Equal(0f, centerRay.Direction.Y, 2);
            Assert.Equal(-1f, centerRay.Direction.Z, 2);

            // Frustum test
            var boxInside = new Aabb3D(new Vec3(-10, -10, -50), new Vec3(10, 10, -40));
            Assert.True(cam.IsInFrustum(boxInside));

            var boxFarBehind = new Aabb3D(new Vec3(-10, -10, 200), new Vec3(10, 10, 250));
            Assert.False(cam.IsInFrustum(boxFarBehind));
        }

        [Fact]
        public void Orbit_And_Fly_CameraControllers_Test()
        {
            var orbit = new OrbitCameraController(Vec3.Zero, 100f, 0f, 0f);
            var cam = new Camera3D();
            orbit.UpdateCamera(cam);

            Assert.Equal(0f, cam.Position.X, 1);
            Assert.Equal(0f, cam.Position.Y, 1);
            Assert.Equal(100f, cam.Position.Z, 1);

            orbit.Rotate(90f, 0f);
            orbit.Zoom(-20f);
            orbit.UpdateCamera(cam);

            Assert.Equal(80f, cam.Position.X, 1);
            Assert.Equal(80f, orbit.Distance, 1);

            var fly = new FlyCameraController(new Vec3(0, 50, 0), 0f, 0f);
            fly.Move(1f, 0f, 0f, 0.1f); // Move forward 30 units
            fly.UpdateCamera(cam);

            Assert.True(cam.Position.Z < 0f);
        }

        [Fact]
        public void MeshPrimitives_Procedural_Generators_Test()
        {
            var cube = MeshPrimitives.CreateBox(10f, 20f, 30f);
            Assert.Equal(24, cube.Vertices.Count);
            Assert.Equal(36, cube.Indices.Count);
            Assert.Equal(12, cube.TriangleCount);
            Assert.Equal(10f, cube.BoundingBox.Size.X, 2);
            Assert.Equal(20f, cube.BoundingBox.Size.Y, 2);
            Assert.Equal(30f, cube.BoundingBox.Size.Z, 2);

            var sphere = MeshPrimitives.CreateSphere(5f, 16, 12);
            Assert.True(sphere.Vertices.Count > 0);
            Assert.True(sphere.Indices.Count > 0);
            Assert.True(sphere.BoundingBox.Size.X <= 10.1f);

            var cylinder = MeshPrimitives.CreateCylinder(4f, 15f, 16);
            Assert.True(cylinder.TriangleCount > 0);

            var plane = MeshPrimitives.CreatePlane(100f, 100f, 4, 4);
            Assert.Equal(16 * 2, plane.TriangleCount);

            var torus = MeshPrimitives.CreateTorus(20f, 5f, 16, 8);
            Assert.True(torus.TriangleCount > 0);
        }

        [Fact]
        public void SceneGraph_Hierarchy_And_Raycast_Test()
        {
            var scene = new Scene3D();
            var parentNode = new SceneNode
            {
                Name = "Parent",
                LocalTransform = Mat4.CreateTranslation(100f, 0f, 0f)
            };

            var childNode = new SceneNode
            {
                Name = "Child",
                LocalTransform = Mat4.CreateTranslation(0f, 50f, 0f),
                Mesh = MeshPrimitives.CreateCube(10f)
            };

            parentNode.AddChild(childNode);
            scene.Root.AddChild(parentNode);
            scene.Update();

            // Child world transform: translation = (100, 50, 0)
            Assert.Equal(100f, childNode.WorldTransform.Translation.X, 2);
            Assert.Equal(50f, childNode.WorldTransform.Translation.Y, 2);
            Assert.Equal(0f, childNode.WorldTransform.Translation.Z, 2);

            Assert.Equal(new Vec3(100f, 50f, 0f), childNode.WorldBounds.Center);

            // Raycast targeting the child cube
            var ray = new Ray3D(new Vec3(100f, 50f, -50f), Vec3.UnitZ);
            var hit = scene.Raycast(ray);

            Assert.True(hit.HasHit);
            Assert.Equal("Child", hit.Node?.Name);
            Assert.Equal(45f, hit.Distance, 1);
        }

        [Fact]
        public void ObjLoader_And_StlLoader_Tests()
        {
            string objText = @"
# Sample OBJ
v 0.0 0.0 0.0
v 1.0 0.0 0.0
v 0.0 1.0 0.0
vn 0.0 0.0 1.0
f 1//1 2//1 3//1
";
            var objMesh = ObjLoader.LoadMesh(objText);
            Assert.Equal(3, objMesh.Vertices.Count);
            Assert.Equal(3, objMesh.Indices.Count);
            Assert.Equal(1, objMesh.TriangleCount);

            // STL Binary round-trip
            var box = MeshPrimitives.CreateCube(20f);
            byte[] stlBytes = StlLoader.SaveToBinary(box);
            Assert.True(stlBytes.Length >= 84);

            var loadedStl = StlLoader.LoadBinary(stlBytes);
            Assert.Equal(12, loadedStl.TriangleCount);
            Assert.Equal(20f, loadedStl.BoundingBox.Size.X, 2);

            // STL ASCII parser
            string asciiStl = @"solid test
facet normal 0 0 1
  outer loop
    vertex 0 0 0
    vertex 10 0 0
    vertex 0 10 0
  endloop
endfacet
endsolid test";
            var loadedAscii = StlLoader.LoadAscii(asciiStl);
            Assert.Equal(1, loadedAscii.TriangleCount);
            Assert.Equal(10f, loadedAscii.BoundingBox.Max.X, 2);
        }

        [Fact]
        public void SceneRenderer_Pipeline_And_Shading_Test()
        {
            var scene = new Scene3D();
            scene.Camera.Position = new Vec3(0f, 0f, 200f);
            scene.Camera.Target = Vec3.Zero;

            var opaqueNode = new SceneNode
            {
                Name = "Opaque",
                Mesh = MeshPrimitives.CreateCube(10f),
                Material = new Material3D { Alpha = 1.0f }
            };

            var transparentNode = new SceneNode
            {
                Name = "Glass",
                Mesh = MeshPrimitives.CreateSphere(5f),
                Material = new Material3D { Alpha = 0.5f }
            };

            scene.Root.AddChild(opaqueNode);
            scene.Root.AddChild(transparentNode);

            var renderer = new SceneRenderer();
            var stats = renderer.PrepareFrame(scene);

            Assert.Equal(1, stats.OpaqueCommands);
            Assert.Equal(1, stats.TransparentCommands);
            Assert.True(stats.TotalTriangles > 0);

            // Blinn-Phong shading calculation
            var lights = new[]
            {
                Light3D.CreateAmbient(new ColorRgb(0.2f, 0.2f, 0.2f)),
                Light3D.CreateDirectional(new Vec3(0, 0, -1), ColorRgb.White)
            };

            var shaded = SceneRenderer.ComputeBlinnPhongShading(
                new Vec3(0, 0, 0),
                new Vec3(0, 0, 1),
                new Vec3(0, 0, 10),
                Material3D.Default,
                lights
            );

            Assert.True(shaded.R > 0f);
            Assert.True(shaded.G > 0f);
            Assert.True(shaded.B > 0f);
        }
    }
}
