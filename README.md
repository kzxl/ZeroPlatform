# ZeroScan3D

[![Build & Test](https://github.com/kzxl/ZeroScan3D/actions/workflows/publish-packages.yml/badge.svg)](https://github.com/kzxl/ZeroScan3D/actions)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![ZeroPlatform Tier](https://img.shields.io/badge/ZeroPlatform-Tier%203%20Perception-purple.svg)](https://github.com/kzxl/ZeroPlatform)

**ZeroScan3D** is a sovereign, high-performance, pure C# 3D spatial scanning, Visual SLAM (Simultaneous Localization and Mapping), and continuous surface reconstruction library for modern .NET environments.

Part of the **ZeroPlatform** sovereign ecosystem (**Tier 3: Perception & Intelligence**), `ZeroScan3D` enables cameras (Intel RealSense, Microsoft Azure Kinect, Orbbec, synthetic sensors) to scan physical environments in real time, estimate 6-DOF camera trajectories, incrementally fuse 3D point clouds into sparse voxel grids, and reconstruct continuous triangle meshes via Truncated Signed Distance Fields (TSDF) and Marching Cubes.

---

## Key Features

- **Sovereign Pure C#**: Zero native C++ binaries, zero P/Invoke dependencies, zero wrappers. Runs cross-platform on Windows, Linux, macOS, and embedded runtimes.
- **Pinhole Camera Intrinsics**: Configurable focal lengths ($f_x, f_y$), principal points ($c_x, c_y$), and depth scaling with bidirectional 2D $\leftrightarrow$ 3D projection. Presets for RealSense D435 and Azure Kinect.
- **Fast Depth Unprojector**: Parallel unprojection of raw 16-bit or floating-point depth maps into 3D Euclidean points with optional synchronized RGB color channels.
- **6-DOF Visual Odometry (VO)**: Scan-to-scan rigid pose tracking using Iterative Closest Point (ICP) registration backed by `ZeroGeometry` spatial Kd-Trees.
- **Sparse Voxel Grid Mapping**: Spatial hash grid fusing incoming 3D observations into running-average centroid positions and RGB color values with statistical noise filtering.
- **TSDF Volumetric Field**: Projective Truncated Signed Distance Field grid representing continuous surfaces with sub-voxel precision.
- **Marching Cubes Surface Meshing**: Pure C# implementation of Lorensen & Cline (1987) with 256-case edge tables, producing indexed triangular meshes with vertex normals.
- **Wavefront OBJ & Stanford PLY Exporters**: Export reconstructed 3D point clouds and meshes directly to standard formats for Blender, MeshLab, CloudCompare, or Three.js.
- **Multi-Targeting**: Targets `netstandard2.0`, `net462`, and `net8.0`.

---

## Architecture (Tier 3 Placement)

```
+-------------------------------------------------------------------+
|                        ZeroScan3D.Core                            |
+--------------------+---------------------+------------------------+
| Camera & Projection| Tracking & SLAM     | Mapping & Meshing      |
| - CameraIntrinsics | - CameraPose (6-DOF)| - SpatialVoxel         |
| - RgbdFrame        | - VisualOdometry    | - VoxelGridMap         |
| - DepthUnprojector | - ICP Registration  | - TsdfVolume           |
| - RgbColor         | - OdometryTelemetry | - MarchingCubes        |
| - ColoredPoint3D   |                     | - Mesh3D (OBJ/PLY)     |
+--------------------+---------------------+------------------------+
| Orchestration: SpatialScanner (Frame -> Track -> Fuse -> Reconstruct)|
+-------------------------------------------------------------------+
            |                                   |
            v                                   v
+-----------------------+           +-----------------------+
|  ZeroGeometry.Core    |           |  ZeroPrimitives.Core  |
|  (Tier 2: Math/Geo)   |           |  (Tier 0: Primitives) |
+-----------------------+           +-----------------------+
```

---

## Quick Start

### 1. Ingest Depth Frame & Unproject to 3D Point Cloud

```csharp
using ZeroScan3D.Core.Camera;

// Initialize camera intrinsics (e.g. RealSense D435 640x480)
var intrinsics = CameraIntrinsics.RealSenseD435_640x480();

// Load or stream depth buffer (in millimeters or meters)
float[] rawDepth = GetSensorDepthBuffer();
byte[] rgbBytes = GetSensorRgbBuffer();

var frame = new RgbdFrame(640, 480, rawDepth, rgbBytes);

// Fast unprojection to 3D point cloud
var pointCloud = DepthUnprojector.Unproject(frame, intrinsics, minDepthMeters: 0.2, maxDepthMeters: 4.0);
Console.WriteLine($"Extracted {pointCloud.Count} 3D points from camera frame.");
```

### 2. Real-Time Spatial Scanner Pipeline

```csharp
using ZeroScan3D.Core.Camera;
using ZeroScan3D.Core.Reconstruction;

var intrinsics = CameraIntrinsics.RealSenseD435_640x480();
var options = new ScannerOptions
{
    VoxelSize = 0.02, // 2cm map resolution
    TrackingVoxelLeaf = 0.04, // 4cm VO downsampling
    EnableTsdf = true, // Continuous surface reconstruction
    TsdfDimX = 128,
    TsdfDimY = 128,
    TsdfDimZ = 128,
    TsdfVoxelSize = 0.02
};

var scanner = new SpatialScanner(intrinsics, options);

// Ingest consecutive camera frames from camera stream
while (cameraStream.ReadNext(out var frame))
{
    var result = scanner.ProcessFrame(frame);
    if (result.Success)
    {
        Console.WriteLine($"Pose: {result.Pose.Translation} | Voxels: {scanner.VoxelMap.VoxelCount}");
    }
}

// Export fused map as point cloud or 3D mesh
var mapCloud = scanner.GetMapPointCloud(minVoxelWeight: 2);
var mesh = scanner.ExtractMesh(isoLevel: 0.0f);

mesh.ExportObj("scanned_room.obj");
mesh.ExportPly("scanned_room.ply");
```

---

## Verification & Tests

`ZeroScan3D` comes with a comprehensive test suite (`ZeroScan3D.Tests`) covering:
- Pinhole camera inversion cycles ($2D \leftrightarrow 3D$).
- 6-DOF camera pose compositions and rigid body algebra.
- Scan-to-scan visual odometry translation estimation.
- Incremental sparse voxel grid integration and centroid running averages.
- Continuous TSDF volume projective integration.
- Analytical sphere Marching Cubes isosurface extraction with normal generation.
- Wavefront OBJ and Stanford PLY export file integrity.

Run tests:
```bash
dotnet test ZeroScan3D.slnx -c Release
```

---

## License

MIT License. Copyright (c) 2026 Phong Võ & ZeroPlatform Contributors.
