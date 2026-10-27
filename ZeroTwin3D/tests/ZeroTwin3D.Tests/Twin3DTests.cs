using System;
using System.IO;
using System.Text;
using Xunit;
using ZeroTwin3D.Engine;
using ZeroTwin3D.Kinematics;
using ZeroTwin3D.Sync;

namespace ZeroTwin3D.Tests
{
    public class Twin3DTests
    {
        [Fact]
        public void Math3D_ComputesVectorsAndMatricesCorrectly()
        {
            var v1 = new Vec3(1, 0, 0);
            var v2 = new Vec3(0, 1, 0);

            var cross = Vec3.Cross(v1, v2);
            Assert.Equal(0, cross.X);
            Assert.Equal(0, cross.Y);
            Assert.Equal(1, cross.Z);

            Assert.Equal(0, Vec3.Dot(v1, v2));
            Assert.Equal(1, v1.Length());
            Assert.Equal((float)Math.Sqrt(2), Vec3.Distance(v1, v2), 4);

            // Matrix Transform
            var t = Mat4.CreateTranslation(10, 20, 30);
            var p = t.TransformPoint(new Vec3(5, 5, 5));
            Assert.Equal(15, p.X);
            Assert.Equal(25, p.Y);
            Assert.Equal(35, p.Z);

            // AABB
            var box1 = new Aabb3D(new Vec3(0, 0, 0), new Vec3(10, 10, 10));
            var box2 = new Aabb3D(new Vec3(5, 5, 5), new Vec3(15, 15, 15));
            var box3 = new Aabb3D(new Vec3(20, 20, 20), new Vec3(30, 30, 30));

            Assert.True(box1.Intersects(box2));
            Assert.False(box1.Intersects(box3));
            Assert.True(box1.Contains(new Vec3(5, 5, 5)));
            Assert.False(box1.Contains(new Vec3(15, 5, 5)));
        }

        [Fact]
        public void MeshLoader_ParsesObjCorrectly()
        {
            string objData =
@"# Wavefront OBJ test cube face
v 0.0 0.0 0.0
v 10.0 0.0 0.0
v 10.0 10.0 0.0
v 0.0 10.0 0.0
vn 0.0 0.0 1.0
f 1//1 2//1 3//1
f 1//1 3//1 4//1
";
            var mesh = Mesh3D.ParseObj(objData);
            Assert.Equal(6, mesh.Indices.Count); // 2 triangles = 6 indices
            Assert.Equal(6, mesh.Vertices.Count);
            Assert.Equal(0f, mesh.BoundingBox.Min.X);
            Assert.Equal(10f, mesh.BoundingBox.Max.X);
            Assert.Equal(0f, mesh.BoundingBox.Min.Y);
            Assert.Equal(10f, mesh.BoundingBox.Max.Y);
        }

        [Fact]
        public void Kinematics_Standard6AxisRobotFk()
        {
            // Standard industrial 6-DOF robot
            var robot = RobotKinematics.CreateStandard6AxisRobot(d1: 400f, a1: 150f, a2: 450f, d4: 400f, d6: 100f);

            // Zero position: all joints 0 deg
            var zeroResult = robot.ComputeForwardKinematics(new float[] { 0, 0, 0, 0, 0, 0 });
            Assert.Equal(7, zeroResult.JointPositions.Count); // Base + 6 joints

            var ee = zeroResult.EndEffectorPosition;
            // In zero position, arm reaches outward into workspace
            Assert.True(ee.Length() > 500f, $"End effector reach should be significant. Actual length: {ee.Length()}");
        }

        [Fact]
        public void Kinematics_ScaraIkReachesTargetFk()
        {
            float l1 = 300f;
            float l2 = 250f;
            var target = new Vec3(350f, 150f, -60f);

            bool solved = RobotKinematics.SolveScaraIk(l1, l2, target, out float t1, out float t2, out float z);
            Assert.True(solved, "SCARA IK should find solution within workspace.");

            var robot = RobotKinematics.CreateStandardScaraRobot(l1, l2, baseHeight: 0f);
            var fkResult = robot.ComputeForwardKinematics(new float[] { t1, t2, z, 0f });

            var actualPos = fkResult.EndEffectorPosition;

            // Verify IK precision matches FK output within 0.1 mm
            Assert.InRange(actualPos.X, target.X - 0.1f, target.X + 0.1f);
            Assert.InRange(actualPos.Y, target.Y - 0.1f, target.Y + 0.1f);
            Assert.InRange(actualPos.Z, target.Z - 0.1f, target.Z + 0.1f);
        }

        [Fact]
        public void TwinScene_DetectsSafetyBreachOnTelemetrySync()
        {
            var scene = new TwinScene();

            // Operator Exclusion Danger Zone
            var dangerZone = new Aabb3D(new Vec3(300, 100, -100), new Vec3(500, 300, 100));
            scene.SafetyZones.Add(dangerZone);

            var robotNode = new TwinNode { Name = "IndustrialRobot_6DOF" };
            scene.Root.AddChild(robotNode);

            var bridge = new TwinTelemetryBridge(scene)
            {
                Robot = RobotKinematics.CreateStandardScaraRobot(300f, 250f, baseHeight: 0f),
                RobotEndEffectorNode = robotNode
            };

            bool breachDetected = false;
            string? breachedNodeName = null;
            bridge.OnSafetyBreach += (node, zone) =>
            {
                breachDetected = true;
                breachedNodeName = node;
            };

            // 1. Safe position: Arm retracted
            bridge.ApplyRobotJointTelemetry(new float[] { 0, -90f, 0, 0 });
            Assert.False(breachDetected);

            // 2. Dangerous position: Arm enters dangerZone at (X ~ 350, Y ~ 150, Z = 0)
            RobotKinematics.SolveScaraIk(300f, 250f, new Vec3(350f, 150f, 0f), out float t1, out float t2, out _);
            bridge.ApplyRobotJointTelemetry(new float[] { t1, t2, 0f, 0f });

            Assert.True(breachDetected);
            Assert.Equal("IndustrialRobot_6DOF", breachedNodeName);
        }
    }
}
