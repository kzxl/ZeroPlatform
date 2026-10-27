using System;
using System.Collections.Generic;
using Xunit;
using ZeroTwin3D.Camera;
using ZeroTwin3D.Engine;
using ZeroTwin3D.Kinematics;
using ZeroTwin3D.Lighting;
using ZeroTwin3D.Rendering;
using ZeroTwin3D.Sync;

namespace ZeroTwin3D.Tests
{
    public class TwinFeatureTests
    {
        [Fact]
        public void Camera3D_ComputesViewProjectionAndFrustumPlanes()
        {
            var camera = new Camera3D
            {
                Position = new Vec3(0f, 500f, 1000f),
                Target = new Vec3(0f, 0f, 0f),
                Up = Vec3.UnitY,
                FovYDegrees = 60f,
                AspectRatio = 16f / 9f,
                NearPlane = 10f,
                FarPlane = 5000f
            };

            var viewMat = camera.GetViewMatrix();
            var projMat = camera.GetProjectionMatrix();
            var vpMat = camera.GetViewProjectionMatrix();

            Assert.NotEqual(0f, viewMat.M11);
            Assert.NotEqual(0f, projMat.M11);
            Assert.NotEqual(0f, vpMat.M11);

            var planes = camera.GetFrustumPlanes();
            Assert.Equal(6, planes.Length);

            foreach (var plane in planes)
            {
                Assert.InRange(plane.Normal.Length(), 0.99f, 1.01f);
            }

            // Object at target (0, 0, 0) should be inside frustum
            var insideBox = new Aabb3D(new Vec3(-50, -50, -50), new Vec3(50, 50, 50));
            Assert.True(camera.IsInFrustum(insideBox), "Target center box should be inside camera view frustum.");

            // Object far behind camera (Z = 5000, while camera is at Z = 1000 looking towards Z = 0)
            var behindBox = new Aabb3D(new Vec3(0, 0, 4000), new Vec3(100, 100, 4200));
            Assert.False(camera.IsInFrustum(behindBox), "Box behind camera should be rejected by frustum culling.");

            // Object far beyond far plane
            var distantBox = new Aabb3D(new Vec3(0, 0, -10000), new Vec3(100, 100, -9000));
            Assert.False(camera.IsInFrustum(distantBox), "Box beyond far plane should be culled.");
        }

        [Fact]
        public void OrbitCameraController_ClampsPitchAndUpdatesCamera()
        {
            var controller = new OrbitCameraController(target: new Vec3(100, 200, 300), distance: 500f)
            {
                MinPitch = -89f,
                MaxPitch = 89f,
                YawDegrees = 0f,
                PitchDegrees = 0f
            };

            // Test position at 0 yaw, 0 pitch -> Target + (0, 0, distance)
            var pos = controller.CalculatePosition();
            Assert.Equal(100f, pos.X, 2);
            Assert.Equal(200f, pos.Y, 2);
            Assert.Equal(800f, pos.Z, 2); // 300 + 500

            // Pitch clamping test
            controller.Rotate(0f, 120f); // exceeds 89f max pitch
            Assert.Equal(89f, controller.PitchDegrees);

            controller.Rotate(0f, -200f); // exceeds -89f min pitch
            Assert.Equal(-89f, controller.PitchDegrees);

            // Zoom test
            controller.Zoom(100f);
            Assert.Equal(600f, controller.Distance);
            controller.Zoom(-1000f); // clamped to MinDistance
            Assert.Equal(controller.MinDistance, controller.Distance);

            // Update camera test
            var camera = new Camera3D();
            controller.Distance = 400f;
            controller.PitchDegrees = 30f;
            controller.YawDegrees = 45f;
            controller.UpdateCamera(camera);

            Assert.Equal(controller.Target.X, camera.Target.X);
            Assert.Equal(controller.Target.Y, camera.Target.Y);
            Assert.Equal(controller.Target.Z, camera.Target.Z);
            Assert.True(camera.Position.Length() > 0f);
        }

        [Fact]
        public void NumericalIk_Solves6AxisRobotWithinTolerance()
        {
            var robot = RobotKinematics.CreateStandard6AxisRobot(d1: 400f, a1: 150f, a2: 450f, d4: 400f, d6: 100f);

            // Test with a target generated directly from known reachable FK joint angles
            var knownJoints = new float[] { 30f, 25f, -35f, 15f, 20f, -10f };
            var fkKnown = robot.ComputeForwardKinematics(knownJoints);
            var target = fkKnown.EndEffectorPosition;

            var ikResult = NumericalInverseKinematics.SolveCcd(
                robot,
                target,
                seedJoints: new float[] { 0f, 0f, 0f, 0f, 0f, 0f },
                maxIterations: 300,
                toleranceMm: 5.0f,
                damping: 0.6f
            );

            Assert.True(ikResult.Success, $"CCD-IK should converge to reachable target. Final error: {ikResult.FinalErrorMm:F2} mm (Iter: {ikResult.IterationsUsed})");
            Assert.InRange(ikResult.FinalErrorMm, 0f, 5.0f);

            // Verify with Forward Kinematics
            var fkVerification = robot.ComputeForwardKinematics(ikResult.JointAngles);
            float actualDist = Vec3.Distance(fkVerification.EndEffectorPosition, target);
            Assert.InRange(actualDist, 0f, 5.0f);

            // Verify joint limits are respected
            for (int i = 0; i < robot.Links.Count; i++)
            {
                Assert.InRange(ikResult.JointAngles[i], robot.Links[i].MinLimit, robot.Links[i].MaxLimit);
            }
        }

        [Fact]
        public void NumericalIk_SolvesScaraRobotAccurately()
        {
            var robot = RobotKinematics.CreateStandardScaraRobot(l1: 300f, l2: 250f, baseHeight: 200f);
            var target = new Vec3(350f, 150f, 100f);

            var ikResult = NumericalInverseKinematics.SolveCcd(
                robot,
                target,
                seedJoints: new float[] { 0f, 0f, 0f, 0f },
                maxIterations: 100,
                toleranceMm: 1.0f,
                damping: 0.8f
            );

            Assert.True(ikResult.Success, $"SCARA CCD-IK should converge. Error: {ikResult.FinalErrorMm:F2} mm");
            Assert.InRange(ikResult.FinalErrorMm, 0f, 1.0f);

            var fk = robot.ComputeForwardKinematics(ikResult.JointAngles);
            Assert.InRange(Vec3.Distance(fk.EndEffectorPosition, target), 0f, 1.0f);
        }

        [Fact]
        public void TrajectoryPlanner_SmoothsWaypointsContinuously()
        {
            var planner = new TrajectoryPlanner { Profile = TrajectoryProfile.CubicSpline };

            // Waypoint 0: t=0, joints: [0, 0, 0]
            // Waypoint 1: t=1, joints: [45, -30, 90]
            // Waypoint 2: t=2, joints: [90, 0, 180]
            planner.AddWaypoint(0.0, new float[] { 0f, 0f, 0f });
            planner.AddWaypoint(1.0, new float[] { 45f, -30f, 90f });
            planner.AddWaypoint(2.0, new float[] { 90f, 0f, 180f });

            Assert.Equal(3, planner.WaypointCount);
            Assert.Equal(2.0, planner.TotalDuration);

            // Evaluate at t=0
            var s0 = planner.Evaluate(0.0);
            Assert.Equal(0f, s0.Positions[0], 2);
            Assert.Equal(0f, s0.Velocities[0], 2);

            // Evaluate at t=1.0 (exact waypoint)
            var s1 = planner.Evaluate(1.0);
            Assert.Equal(45f, s1.Positions[0], 2);
            Assert.Equal(-30f, s1.Positions[1], 2);
            Assert.Equal(90f, s1.Positions[2], 2);

            // Evaluate at midpoint t=0.5
            var sMid = planner.Evaluate(0.5);
            Assert.True(sMid.Positions[0] > 0f && sMid.Positions[0] < 45f);
            Assert.True(sMid.Velocities[0] > 0f); // Moving forward

            // Evaluate at end t=2.0
            var sEnd = planner.Evaluate(2.0);
            Assert.Equal(90f, sEnd.Positions[0], 2);
            Assert.Equal(0f, sEnd.Velocities[0], 2); // Stops at endpoint
        }

        [Fact]
        public void TwinSceneRenderer_ExecutesCullingAndQueueSorting()
        {
            var scene = new TwinScene();
            scene.Camera.Position = new Vec3(0f, 0f, 1000f);
            scene.Camera.Target = Vec3.Zero;

            // Simple cube mesh
            var meshOpaqueNear = new Mesh3D();
            meshOpaqueNear.Vertices.Add(new Vertex3D(new Vec3(-10, -10, 0), Vec3.UnitZ, Vec2.Zero));
            meshOpaqueNear.Vertices.Add(new Vertex3D(new Vec3(10, -10, 0), Vec3.UnitZ, Vec2.Zero));
            meshOpaqueNear.Vertices.Add(new Vertex3D(new Vec3(0, 10, 0), Vec3.UnitZ, Vec2.Zero));
            meshOpaqueNear.ComputeBounds();

            var nodeOpaqueNear = new TwinNode
            {
                Name = "OpaqueNear",
                Mesh = meshOpaqueNear,
                Material = Material3D.IndustrialSteel,
                LocalTransform = Mat4.CreateTranslation(0, 0, 500) // Near camera
            };
            scene.Root.AddChild(nodeOpaqueNear);

            var nodeOpaqueFar = new TwinNode
            {
                Name = "OpaqueFar",
                Mesh = meshOpaqueNear,
                Material = Material3D.RobotOrange,
                LocalTransform = Mat4.CreateTranslation(0, 0, 100) // Further from camera
            };
            scene.Root.AddChild(nodeOpaqueFar);

            var nodeTransparent = new TwinNode
            {
                Name = "TransparentCover",
                Mesh = meshOpaqueNear,
                Material = Material3D.Glass,
                LocalTransform = Mat4.CreateTranslation(0, 0, 200)
            };
            scene.Root.AddChild(nodeTransparent);

            var nodeCulled = new TwinNode
            {
                Name = "BehindCameraCulled",
                Mesh = meshOpaqueNear,
                Material = Material3D.SafetyYellow,
                LocalTransform = Mat4.CreateTranslation(0, 0, 5000) // Behind camera
            };
            scene.Root.AddChild(nodeCulled);

            var renderer = new TwinSceneRenderer();
            var stats = renderer.PrepareFrame(scene);

            Assert.Equal(2, stats.OpaqueCommands);
            Assert.Equal(1, stats.TransparentCommands);
            Assert.Equal(1, stats.CulledMeshes);

            // Verify opaque queue is sorted front-to-back (Near to Far)
            Assert.True(renderer.OpaqueQueue[0].DistanceToCamera < renderer.OpaqueQueue[1].DistanceToCamera);
            Assert.Equal("OpaqueNear", renderer.OpaqueQueue[0].NodeName);
        }

        [Fact]
        public void Lighting_BlinnPhongShadesSurfaceCorrectly()
        {
            var surface = Vec3.Zero;
            var normal = Vec3.UnitY; // Facing UP
            var viewPos = new Vec3(0, 100, 100);
            var material = Material3D.IndustrialSteel;

            // 1. Directional light shining directly down onto surface
            var topLight = Light3D.CreateDirectional(new Vec3(0, -1, 0), ColorRgb.White, intensity: 1.0f);
            var ambientLight = Light3D.CreateAmbient(ColorRgb.White, intensity: 0.1f);

            var illuminated = TwinSceneRenderer.ComputeBlinnPhongShading(surface, normal, viewPos, material, new[] { topLight, ambientLight });

            Assert.True(illuminated.R > 0.3f);
            Assert.True(illuminated.G > 0.3f);
            Assert.True(illuminated.B > 0.3f);

            // 2. Light shining away from surface (from below)
            var bottomLight = Light3D.CreateDirectional(new Vec3(0, 1, 0), ColorRgb.White, intensity: 1.0f);
            var dark = TwinSceneRenderer.ComputeBlinnPhongShading(surface, normal, viewPos, material, new[] { bottomLight, ambientLight });

            // Surface only receives ambient contribution
            Assert.True(dark.R < 0.1f);
            Assert.True(dark.G < 0.1f);
            Assert.True(dark.B < 0.1f);
        }
    }
}
