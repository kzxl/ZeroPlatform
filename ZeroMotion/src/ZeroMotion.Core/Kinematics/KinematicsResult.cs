using System.Collections.Generic;
using ZeroMotion.Math;

namespace ZeroMotion.Kinematics
{
    /// <summary>
    /// Result of a Forward Kinematics computation containing intermediate joint coordinate frames and end-effector pose.
    /// </summary>
    public class KinematicsResult
    {
        public List<Mat4> JointTransforms { get; } = new List<Mat4>();
        public List<Vec3> JointPositions { get; } = new List<Vec3>();
        public Mat4 EndEffectorTransform { get; set; } = Mat4.Identity;
        public Vec3 EndEffectorPosition => EndEffectorTransform.Translation;
    }
}
