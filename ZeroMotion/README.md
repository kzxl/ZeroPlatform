# ZeroMotion

[![ZeroPlatform Tier](https://img.shields.io/badge/ZeroPlatform-Tier%203%20(Perception%20%26%20Intelligence)-701a75.svg)](https://github.com/kzxl/ZeroPlatform)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET Multi-Targeting](https://img.shields.io/badge/.NET-8.0%20%7C%204.6.2%20%7C%20Standard%202.0-purple.svg)](https://dotnet.microsoft.com/)
[![Pure C#](https://img.shields.io/badge/Dependencies-0%20(Pure%20C%23)-brightgreen.svg)]()

**ZeroMotion** is an industrial robotics kinematics and motion planning substrate for .NET with **zero external dependencies**. It provides:
1. **Robotic Kinematics Engine**: Forward kinematics, geometric Jacobians, Denavit-Hartenberg (DH) parameterization, Cyclic Coordinate Descent (CCD) numerical inverse kinematics, and sub-microsecond analytical closed-form inverse kinematics for SCARA and spherical wrist manipulators.
2. **Trajectory Planning Engine**: Linear, Cubic Hermite Spline, Quintic Spline, and 7-Phase Jerk-Limited S-Curve motion profiles eliminating mechanical vibration and gearbox resonance.
3. **ROS URDF Robot Model Parser**: Pure C# XML parsing of standard ROS Unified Robot Description Format files.

---

## 🏛️ Architecture & Governance

ZeroMotion strictly adheres to **SPEC-ARCH-001** as a **Tier 3 (Perception & Intelligence / Motion & Robotics)** substrate. It decouples motion mathematics, kinematics, and trajectory generation from presentation/visualization layers (Tier 4 `ZeroTwin3D`), allowing headless execution in real-time PLC gateways, edge IPCs, and robot controllers.

```
       Tier 1: ZeroTensor / ZeroCompute (Level-3 BLAS & Fast Matrix Inversion)
                         ↓
       Tier 3: ZeroMotion (Kinematics, Jacobians, S-Curve Trajectories, URDF)
            ┌────────────┴────────────┐
            ↓                         ↓
   Tier 4: ZeroTwin3D       Tier 5: ZeroPipeline
 (3D Spatial Digital Twin)    (Robot Motion Nodes)
```

---

## 📦 Developer API Examples

### 1. Forward & Analytical Inverse Kinematics (SCARA)

```csharp
using ZeroMotion.Kinematics;
using ZeroMotion.Math;

// 1. Instantiate 4-Axis SCARA Manipulator
var scara = RobotKinematics.CreateStandardScaraRobot(l1: 300f, l2: 250f, baseHeight: 200f);

// 2. Solve closed-form Analytical Inverse Kinematics (< 1 us)
var target = new Vec3(350f, 150f, 100f);
var ik = AnalyticalInverseKinematics.SolveScara(300f, 250f, 200f, target);

// 3. Forward Kinematics verification
var fk = scara.ComputeForwardKinematics(new float[] { ik.Theta1Deg, ik.Theta2Deg, ik.ZPrismatic, ik.Theta4Deg });
Console.WriteLine($"End-Effector: {fk.EndEffectorPosition}");
```

### 2. 7-Phase Jerk-Limited S-Curve Motion Profile

```csharp
using ZeroMotion.Trajectory;

// Configure S-Curve with max velocity, acceleration, and jerk limits
var scurve = new SCurvePlanner(
    startPos: 0.0,
    targetPos: 500.0, // 500 mm stroke
    maxVel: 250.0,    // 250 mm/s
    maxAcc: 1000.0,   // 1000 mm/s^2
    maxJerk: 5000.0   // 5000 mm/s^3 (finite jerk prevents mechanical resonance)
);

// Sample position, velocity, and acceleration along profile
for (double t = 0.0; t <= scurve.TotalDuration; t += 0.001)
{
    var (pos, vel, acc) = scurve.Evaluate(t);
}
```

### 3. Multi-Axis Synchronized S-Curve Trajectory

```csharp
using ZeroMotion.Trajectory;

// Synchronizes N axes so all axes start and arrive simultaneously at T_sync
var multi = new MultiAxisSCurvePlanner(
    startPositions:    new double[] { 0, 0, 0 },
    targetPositions:   new double[] { 300, 100, 50 },
    maxVelocities:     new double[] { 150, 150, 150 },
    maxAccelerations:  new double[] { 400, 400, 400 },
    maxJerks:          new double[] { 2000, 2000, 2000 }
);

var (positions, velocities, accelerations) = multi.Evaluate(t);
```

### 4. Cartesian Straight-Line (MoveL) & Quaternion SLERP

```csharp
using ZeroMotion.Trajectory;
using ZeroMotion.Math;

var planner = new CartesianTrajectoryPlanner(
    startPos: new Vec3(100f, 200f, 300f),
    startRot: Quat.Identity,
    targetPos: new Vec3(400f, 200f, 600f),
    targetRot: Quat.FromEuler(0f, 1.57f, 0f),
    maxLinearVel: 250.0, maxLinearAcc: 1000.0, maxLinearJerk: 5000.0
);

var waypoint = planner.Evaluate(t);
// waypoint.Position, waypoint.Orientation (SLERP), waypoint.LinearVelocity
```

### 5. Yoshikawa Manipulability & Damped Least Squares (DLS) IK

```csharp
// Evaluate distance from kinematic singularities
float w = robot.ComputeManipulability(jointAngles);
if (w < 0.01f) Console.WriteLine("Warning: Manipulator near singularity!");

// Singularity-robust differential inverse kinematics
float[] deltaQ = robot.SolveDampedLeastSquaresIK(jointAngles, deltaLinear, deltaAngular, dampingLambda: 0.05f);
```

---

## 📄 License

MIT License © 2026 Phong Võ. Part of the **ZeroPlatform** project.
