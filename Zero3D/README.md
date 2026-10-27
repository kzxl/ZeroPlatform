# Zero3D

[![ZeroPlatform Tier](https://img.shields.io/badge/ZeroPlatform-Tier%204%20(Graphics%20%26%20Spatial%203D)-ea580c.svg)](https://github.com/kzxl/ZeroPlatform)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET Multi-Targeting](https://img.shields.io/badge/.NET-8.0%20%7C%204.6.2%20%7C%20Standard%202.0-purple.svg)](https://dotnet.microsoft.com/)
[![NuGet Version](https://img.shields.io/badge/NuGet-1.2.0-blue.svg)](https://www.nuget.org/packages/Zero3D)

**Zero3D** is an enterprise-grade, pure C# 3D graphics, software rasterization, point cloud processing, and model loading engine for .NET. Built with **zero external unmanaged dependencies**, it runs seamlessly across Windows, Linux, macOS, and headless containerized environments.

Operating as a 3D geometry, rasterization & model format foundation of **Tier 4 (Graphics & Spatial 3D)** within the **[ZeroPlatform](https://github.com/kzxl/ZeroPlatform)** ecosystem.

---

## 🏛️ Ecosystem Architectural Alignment

- **Architectural Tier**: **Tier 4 (Graphics & Spatial 3D)**
- **Permitted Upstream Dependencies**: Tier 0 (`ZeroPrimitives`, `ZeroConcurrency`), Tier 1 (`ZeroTensor`, `ZeroCompute`), Tier 3 (`ZeroGeometry`)
- **Downstream Consumers**: Tier 4 (`ZeroTwin3D`), Tier 5 (`ZeroUI`, `ZeroPipeline`)
- **Core Guarantees**: Pure C# software rasterizer, 0 unmanaged dependencies, cross-platform glTF/OBJ/STL/PLY model parsing.

---

## Key Features

- **Pure CPU Software Rasterizer (`SoftwareRasterizer`)**:
  - Sub-pixel precision triangle rasterization using Barycentric coordinates.
  - 32-bit floating-point Z-Buffer (Depth Buffer) for accurate occlusion handling.
  - Flat and Gouraud normal-interpolated shading modes.
  - Bresenham 3D line drawing with depth testing.
  - Direct 32-bit BGRA byte array export (`GetPixelBytes()`) without GPU drivers.
- **3D Point Cloud & Voxel Filtering (`PointCloud3D`)**:
  - Lightweight `Point3D` structure supporting coordinates, RGBA color keys, and surface normals.
  - Spatial `VoxelGridFilter` for fast 3D spatial downsampling and decimation of high-density LiDAR/sensor scans.
- **Pure C# 3D Mathematics (`Math3D`)**:
  - `Vec3`, `Vec2`, `Mat4`, `Quat`, `Ray3D`, `Plane3D`, `Aabb3D` with SIMD-ready layouts, Euler/Quaternion conversions, and fast ray intersections.
- **Multi-Format 3D Model Loaders (IO)**:
  - **Stanford PLY (`PlyLoader`)**: Comprehensive ASCII & Binary Little-Endian parser with vertex colors, normals, and face index reconstruction.
  - **glTF 2.0 & Binary GLB (`GltfLoader`)**: Full scene hierarchy, node transformations, materials, accessors, and embedded JSON tokenizer.
  - **Wavefront OBJ (`ObjLoader`)**: Multi-group objects, vertex positions, normals, texture coordinates, and polygon fan triangulation.
  - **Stereolithography STL (`StlLoader`)**: Binary and ASCII STL parsing and binary export for CAD/CAM and 3D printing.
  - **Unified `LoadAuto`**: Single entry-point automatic format detection and loading.
- **Camera System (`Camera3D`)**:
  - Perspective and Orthographic projection, screen-point-to-ray unprojection for mouse picking, 6-plane frustum extraction.
  - `OrbitCameraController` (Arcball/CAD inspector) and `FlyCameraController` (WASD walkthrough).
- **Hierarchical Scene Graph (`Scene3D`, `SceneNode`)**:
  - Transform inheritance (`WorldTransform = LocalTransform * ParentWorld`), hierarchical AABB bounds, recursive child search, and raycast picking.
- **Multi-Targeting**: `.NET Standard 2.0`, `.NET Framework 4.6.2`, `.NET 8.0+`.

---

## Architecture

```
Zero3D/
├── Camera/       # Camera3D, OrbitCameraController, FlyCameraController
├── IO/           # GltfLoader, ObjLoader, StlLoader, PlyLoader
├── Lighting/     # Light3D, LightType
├── Materials/    # ColorRgb, Material3D, PbrMaterial
├── Math/         # Vec3, Vec2, Mat4, Quat, Ray3D, Plane3D, Aabb3D
├── Mesh/         # Vertex3D, SubMesh, Mesh3D, MeshPrimitives, PointCloud3D, VoxelGridFilter
├── Rendering/    # SoftwareRasterizer, RenderCommand, RenderPass, SceneRenderer
└── Scene/        # SceneNode, Scene3D, RaycastHit
```

---

## License

MIT License. Copyright © 2026 Phong Võ (`kzxl`).
