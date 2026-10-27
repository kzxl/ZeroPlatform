# ZeroGeometry

[![ZeroPlatform Tier](https://img.shields.io/badge/ZeroPlatform-Tier%203%20(Perception%20%26%20AI)-7c3aed.svg)](https://github.com/kzxl/ZeroPlatform)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET Multi-Targeting](https://img.shields.io/badge/.NET-8.0%20%7C%204.6.2%20%7C%20Standard%202.0-purple.svg)](https://dotnet.microsoft.com/)
[![Point Cloud & Spatial](https://img.shields.io/badge/Geometry-ICP%20%7C%20KdTree%20%7C%20Delaunay-blue.svg)]()
[![Zero External Dependencies](https://img.shields.io/badge/Dependencies-0%20(Pure%20C%23)-brightgreen.svg)]()
[![NuGet Version](https://img.shields.io/badge/NuGet-1.2.0-blue.svg)](https://www.nuget.org/packages/ZeroGeometry.Core/1.2.0)

**ZeroGeometry** is a high-performance 2D/3D computational geometry, spatial indexing, projective geometry, and point cloud processing engine for .NET with **zero external dependencies**. Implemented from scratch in pure C#, it provides industrial metrology, 3D laser scan registration (Iterative Closest Point via SVD), spatial nearest-neighbor lookups (KdTree/RTree), polygon boolean clipping, offsetting, Delaunay triangulation, and 2D Homography / projective rectification without PCL, CGAL, or OpenCV dependencies.

---

## 🌟 Key Capabilities

- **2D Projective Geometry & Homography (`ZeroGeometry.Core.Projective`)**:
  - **Homography2D Estimation**: Direct Linear Transform (DLT) solving the $3\times3$ planar homography matrix from 4 or more corresponding points using SVD / eigenanalysis.
  - **RANSAC Robust Fitting**: Outlier rejection for noisy feature correspondences in perspective rectification and image alignment.
  - **Perspective Warping & Inversion**: High-speed mapping of 2D points across projective planes with matrix inversion.
- **3D Point Cloud Processing (`ZeroGeometry.Core.PointCloud`)**:
  - **ICP Registration**: Arun's SVD 3D rigid cloud alignment finding optimal rotation $R$ and translation $T$.
  - **Surface Normal Estimation**: Local covariance eigenanalysis (Jacobi $3\times3$ rotations) computing curvature and viewpoint-oriented normals.
  - **Voxel Grid Downsampling**: Uniform voxel filter aggregating points to centroid.
  - **RANSAC Plane Fitting**: Robust plane estimation rejecting outliers.
- **Spatial Indexing Structures (`ZeroGeometry.Core.Spatial`)**:
  - **Balanced 3D KdTree**: Median-split spatial partitioning tree for $O(\log N)$ nearest-neighbor and radius search.
  - **2D R-Tree**: Bounding-box hierarchical index for fast rectangle range queries.
- **2D Polygon Boolean Ops (`ZeroGeometry.Core.Polygons`)**:
  - **Sutherland-Hodgman Polygon Clipping**: Convex polygon clipping with exact vertex interpolation.
  - **Polygon Offsetting / Buffering**: Minkowski dilation/erosion for tolerance boundaries.
  - **Delaunay Triangulation**: Bowyer-Watson incremental 2D triangulation with Voronoi dual generation.
- **Zero External Dependencies**: Standard .NET runtime only.

---

## 📦 Installation

Install via the .NET CLI:
```bash
dotnet add package ZeroGeometry.Core
```

---

## 🚀 Quick Start

### 1. 3D Nearest Neighbor Search via KdTree
```csharp
using ZeroGeometry.Core.Spatial;

var cloud = new List<Point3D>
{
    new Point3D(0, 0, 0),
    new Point3D(10, 20, 30),
    new Point3D(12, 22, 31),
    new Point3D(100, 200, 300)
};

// Build spatial KdTree
var tree = new KdTree3D(cloud);

// Find nearest neighbor to query point
var nearest = tree.FindNearest(new Point3D(11, 21, 30), out double distSq);

Console.WriteLine($"Nearest: ({nearest.X}, {nearest.Y}, {nearest.Z}), Distance: {Math.Sqrt(distSq):F2}");
```

### 2. Point Cloud Rigid Alignment (ICP)
```csharp
using ZeroGeometry.Core.PointCloud;

var sourceCloud = LoadPointCloud("scan_current.xyz");
var targetCloud = LoadPointCloud("cad_reference.xyz");

// Align scan to reference CAD model
var icp = new IcpRegistration(maxIterations: 30, tolerance: 1e-4);
var result = icp.Align(sourceCloud, targetCloud);

Console.WriteLine($"Fitness RMSE: {result.Rmse:F4} mm, Converged: {result.Converged}");
```

### 3. 2D Homography & Perspective Rectification
```csharp
using ZeroGeometry.Core.Projective;
using ZeroGeometry.Core.Spatial;

// Define 4 corner correspondences (source quad to target rectangle)
var src = new[]
{
    new Point2D(100, 150), new Point2D(800, 120),
    new Point2D(850, 600), new Point2D(120, 620)
};
var dst = new[]
{
    new Point2D(0, 0), new Point2D(1000, 0),
    new Point2D(1000, 600), new Point2D(0, 600)
};

// Estimate homography via Direct Linear Transform (DLT)
if (Homography2D.Estimate(src, dst, out var H))
{
    var mapped = H.Transform(new Point2D(450, 360));
    Console.WriteLine($"Rectified Point: ({mapped.X:F1}, {mapped.Y:F1})");
}
```

---

## 📊 Benchmark & Performance

Tested on Intel Core i7-13700K (Release x64):

| Operation | Dataset Size | Execution Time | Memory Overhead |
| :--- | :--- | :--- | :--- |
| **KdTree3D Build** | $100\text{k points}$ | **$24.5 \text{ ms}$** | Contiguous node array |
| **KdTree Nearest Query** | $10\text{k queries}$ | **$1.82 \text{ ms}$** | $O(\log N)$ stack walk |
| **ICP 3D Alignment** | $50\text{k points}$ (20 iters) | **$18.6 \text{ ms}$** | SVD closed form |
| **2D Delaunay Triangulation** | $5000$ points | **$6.40 \text{ ms}$** | Bowyer-Watson |

---

## 📄 License

MIT License © 2026 Phong Võ. Part of the **ZeroPlatform** project.
