# Zero3D

Enterprise Pure C# 3D Graphics, Scene Graph, Mesh Primitives, glTF/OBJ/STL Loaders & Simulation Engine for .NET.

Part of the **ZeroUniverse / ZeroPlatform** ecosystem.

## Key Features

- **Pure C# 3D Mathematics**: `Vec3`, `Vec2`, `Mat4`, `Quat`, `Ray3D`, `Plane3D`, `Aabb3D` with SIMD-ready layouts, Euler/Quat conversions, frustum matrix setup, and fast ray intersections.
- **Camera System**: Perspective & Orthographic projection, screen-point-to-ray unprojection for mouse picking, 6-plane frustum extraction, `OrbitCameraController` (Arcball/CAD inspector), and `FlyCameraController` (WASD walkthrough).
- **Procedural Mesh Primitives**: High-quality geometric generators for Box/Cube, UV Sphere, Cylinder, Plane, and Torus.
- **Multi-Format Model Loaders (IO)**:
  - **glTF 2.0 & Binary GLB**: Full scene graph hierarchy, transforms, meshes, primitives, accessors, bufferViews with Zero-dependency embedded JSON tokenizer.
  - **Wavefront OBJ**: Multi-group objects, vertex positions, normals, UV texture coordinates, polygon fan triangulation.
  - **Stereolithography STL**: Binary STL & ASCII STL format parsing and Binary STL export for 3D Printing and CAD/CAM.
  - **Unified `LoadAuto`**: Single entry-point automatic file format detection and loading.
- **Hierarchical Scene Graph**: Transform inheritance (`WorldTransform = LocalTransform * ParentWorld`), hierarchical AABB bounds, recursive child search, and fast bounding-box raycast picking.
- **Rendering Pipeline**: Frustum culling, automatic state sorting (opaque front-to-back, transparent back-to-front, wireframe), Blinn-Phong lighting evaluation with Directional, Point, Spot, and Ambient lights.
- **Multi-Targeting**: `.NET Standard 2.0`, `.NET Framework 4.6.2`, `.NET 8.0`. Zero external unmanaged C++ dependencies.

## Architecture

```
Zero3D/
├── Camera/       # Camera3D, OrbitCameraController, FlyCameraController
├── IO/           # GltfLoader, ObjLoader, StlLoader
├── Lighting/     # Light3D, LightType
├── Materials/    # ColorRgb, Material3D, PbrMaterial
├── Math/         # Vec3, Vec2, Mat4, Quat, Ray3D, Plane3D, Aabb3D
├── Mesh/         # Vertex3D, SubMesh, Mesh3D, MeshPrimitives
├── Rendering/    # RenderCommand, RenderPass, RenderStatistics, SceneRenderer
└── Scene/        # SceneNode, Scene3D, RaycastHit
```
