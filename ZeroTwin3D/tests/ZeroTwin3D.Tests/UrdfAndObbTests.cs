using System;
using System.Collections.Generic;
using Xunit;
using ZeroTwin3D.Engine;
using ZeroTwin3D.IO;
using ZeroTwin3D.Kinematics;

namespace ZeroTwin3D.Tests
{
    public class UrdfAndObbTests
    {
        [Fact]
        public void Obb3D_SAT_Intersection_Tests()
        {
            // OBB at origin with extents 10, 10, 10
            var obb1 = new Obb3D(Vec3.Zero, new Vec3(10f, 10f, 10f), Vec3.UnitX, Vec3.UnitY, Vec3.UnitZ);

            // OBB overlapping
            var obb2 = new Obb3D(new Vec3(15f, 0f, 0f), new Vec3(10f, 10f, 10f), Vec3.UnitX, Vec3.UnitY, Vec3.UnitZ);
            Assert.True(obb1.Intersects(obb2));

            // OBB separated
            var obb3 = new Obb3D(new Vec3(30f, 0f, 0f), new Vec3(5f, 5f, 5f), Vec3.UnitX, Vec3.UnitY, Vec3.UnitZ);
            Assert.False(obb1.Intersects(obb3));

            // OBB rotated 45 degrees around Z axis
            float rad45 = 45f * (float)System.Math.PI / 180f;
            float cos45 = (float)System.Math.Cos(rad45);
            float sin45 = (float)System.Math.Sin(rad45);

            var rotX = new Vec3(cos45, sin45, 0f);
            var rotY = new Vec3(-sin45, cos45, 0f);
            var obbRotated = new Obb3D(new Vec3(15f, 0f, 0f), new Vec3(5f, 5f, 5f), rotX, rotY, Vec3.UnitZ);

            // With rotation, corner extends along X: 15 - 5 * sqrt(2) ≈ 15 - 7.07 = 7.93 <= 10.0 -> overlaps with 10.0
            Assert.True(obb1.Intersects(obbRotated));

            // Test intersection with Aabb3D
            var aabb = new Aabb3D(new Vec3(5f, 5f, 5f), new Vec3(20f, 20f, 20f));
            Assert.True(obb1.Intersects(aabb));
        }

        [Fact]
        public void UrdfLoader_ParsesRobotArm_BuildsSceneAndKinematics()
        {
            string urdfXml = @"<?xml version='1.0'?>
<robot name='test_robot'>
  <link name='base_link'>
    <visual>
      <geometry>
        <cylinder length='0.4' radius='0.1'/>
      </geometry>
    </visual>
  </link>
  <link name='arm_link'>
    <visual>
      <geometry>
        <box size='0.5 0.1 0.1'/>
      </geometry>
    </visual>
  </link>
  <joint name='shoulder_joint' type='revolute'>
    <parent link='base_link'/>
    <child link='arm_link'/>
    <origin xyz='0 0 0.4' rpy='0 0 0'/>
    <axis xyz='0 1 0'/>
    <limit lower='-1.57' upper='1.57'/>
  </joint>
</robot>";

            var robot = UrdfLoader.Load(urdfXml);

            Assert.NotNull(robot);
            Assert.Equal("test_robot", robot.Name);
            Assert.Equal(2, robot.Links.Count);
            Assert.Equal(1, robot.Joints.Count);

            // Root node must be base_link
            Assert.Equal("base_link", robot.RootNode.Name);
            Assert.Equal(1, robot.RootNode.Children.Count);
            Assert.Equal("arm_link", robot.RootNode.Children[0].Name);

            // Visual meshes attached
            Assert.NotNull(robot.RootNode.Mesh);
            Assert.NotNull(robot.RootNode.Children[0].Mesh);

            // Kinematics linkage mapped
            Assert.Equal(1, robot.Kinematics.Links.Count);
            var fk = robot.Kinematics.ComputeForwardKinematics(new[] { 0f });
            Assert.NotNull(fk);
        }
    }
}
