using Xunit;
using ZeroMotion.Urdf;

namespace ZeroMotion.Tests
{
    public class UrdfTests
    {
        [Fact]
        public void UrdfParser_ParsesValidRobotXmlAndBuildsKinematics()
        {
            string xml = @"<?xml version=""1.0""?>
<robot name=""IndustrialArm"">
  <link name=""base_link"">
    <inertial>
      <mass value=""15.0""/>
      <origin xyz=""0 0 0.1""/>
    </inertial>
  </link>
  <link name=""link_1""/>
  <link name=""link_2""/>

  <joint name=""joint_1"" type=""revolute"">
    <parent link=""base_link""/>
    <child link=""link_1""/>
    <origin xyz=""0 0 0.3"" rpy=""0 0 0""/>
    <axis xyz=""0 0 1""/>
    <limit lower=""-3.14159"" upper=""3.14159"" velocity=""2.0"" effort=""100""/>
  </joint>

  <joint name=""joint_2"" type=""revolute"">
    <parent link=""link_1""/>
    <child link=""link_2""/>
    <origin xyz=""0.4 0 0"" rpy=""0 1.57079 0""/>
    <axis xyz=""0 1 0""/>
    <limit lower=""-1.57"" upper=""1.57"" velocity=""1.5"" effort=""80""/>
  </joint>
</robot>";

            var robot = UrdfParser.Parse(xml);
            Assert.Equal("IndustrialArm", robot.Name);
            Assert.Equal("base_link", robot.RootLinkName);
            Assert.Equal(3, robot.Links.Count);
            Assert.Equal(2, robot.Joints.Count);

            var kine = robot.ToRobotKinematics();
            Assert.Equal(2, kine.Links.Count);

            var fk = kine.ComputeForwardKinematics(new float[] { 0f, 0f });
            Assert.True(fk.EndEffectorPosition.Length() > 0.1f);
        }
    }
}
