# ZeroTwin3D: Pure C# 3D Digital Twin & Robot Kinematics for .NET

[![ZeroPlatform Tier](https://img.shields.io/badge/ZeroPlatform-Tier%204%20(Graphics%20%26%20Spatial%203D)-ea580c.svg)](https://github.com/kzxl/ZeroPlatform)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET Multi-Targeting](https://img.shields.io/badge/.NET-8.0%20%7C%204.6.2%20%7C%20Standard%202.0-purple.svg)](https://dotnet.microsoft.com/)
[![NuGet Version](https://img.shields.io/badge/NuGet-1.5.0-blue.svg)](https://www.nuget.org/packages/ZeroTwin3D)

**ZeroTwin3D** is a sovereign, lightweight, pure C# 3D digital twin engine, robotic kinematics solver, collision detection pipeline, and spatial safety boundary supervisor for .NET.

Operating as a spatial simulation & kinematics member of **Tier 4 (Graphics & Spatial 3D)** within the **[ZeroPlatform](https://github.com/kzxl/ZeroPlatform)** ecosystem.

---

## 🏛️ Ecosystem Architectural Alignment

- **Architectural Tier**: **Tier 4 (Graphics & Spatial 3D)**
- **Permitted Upstream Dependencies**: Tier 0 (`ZeroPrimitives`), Tier 2 (`ZeroData`), Tier 4 (`Zero3D`, `ZeroGraphics`)
- **Downstream Consumers**: Tier 5 (`ZeroUI`, `ZeroPipeline`)
- **Core Guarantees**: Pure C# execution, zero unmanaged CAD DLLs, analytical kinematics, OBB SAT collision in microseconds.

---

## Key Features

- **ROS URDF Robot Model Loader (`UrdfLoader`)**:
  - Full XML parser for Unified Robot Description Format (URDF) models.
  - Automatic parsing of robot links (Box, Cylinder, Sphere, CAD mesh), visuals, colors, materials, and joint types (revolute, continuous, prismatic, fixed) with joint limits.
  - Automatically reconstructs hierarchical `TwinNode` Scene Graph and directly configures analytical `RobotKinematics`.
- **OBB Separating Axis Theorem Collision Detection (`Obb3D`)**:
  - Full 15-axis Separating Axis Theorem (SAT) collision pipeline for Oriented Bounding Boxes (OBB vs OBB, OBB vs AABB).
  - Fast oriented bounding box construction from point sets or extents/quaternion orientation.
- **Robotic Kinematics Engine (`RobotKinematics`)**:
  - Analytical Denavit-Hartenberg (DH) forward kinematics solver supporting 6-DOF articulated arms and 4-DOF SCARA robots.
- **Inverse Kinematics (CCD-IK) (`NumericalInverseKinematics`)**:
  - Cyclic Coordinate Descent solver for $N$-DOF articulated robots and SCARA manipulators with joint limit constraints.
- **Multi-Joint Trajectory Planner (`TrajectoryPlanner`)**:
  - Smooth kinematic path generation supporting Cubic Hermite Spline ($C^1$ continuity), smooth S-curve Trapezoidal, and Linear velocity profiles.
- **3D Spatial Camera & View Frustum Culling (`Camera3D`)**:
  - Perspective & Orthographic projection, 6-plane frustum extraction, and high-performance AABB view-frustum culling.
- **Orbit / Arcball Camera Controller (`OrbitCameraController`)**:
  - Intuitive CAD model inspection with spherical yaw/pitch orbiting, boundary clamping, distance zooming, and screen-aligned panning.
- **CAD Mesh Parsers (`StlParser`, `ObjParser`)**:
  - Pure C# streaming parsers for binary/ASCII STL and Wavefront OBJ meshes.
- **Spatial Safety Boundaries (`TwinScene`, `TwinTelemetryBridge`)**:
  - Real-time PLC telemetry synchronization and automated workcell exclusion zone breach detection.
- **Multi-Targeting**: `.NET Standard 2.0`, `.NET Framework 4.6.2`, `.NET 8.0+`.

---

## Multi-Targeting

- `.NET 8.0+`
- `.NET Framework 4.6.2+`
- `.NET Standard 2.0`

---

## License

MIT License. Copyright © 2026 Phong Võ (`kzxl`).
