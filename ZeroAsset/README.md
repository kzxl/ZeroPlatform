# ZeroAsset 📦

> **High-Performance Digital Asset Management, Zero-Byte Variant Branching, and Hierarchical Curation Engine for .NET**

[![ZeroPlatform Tier](https://img.shields.io/badge/ZeroPlatform-Tier%201%20(Asset%20%26%20Storage)-1e293b.svg)](https://github.com/kzxl/ZeroPlatform)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET Multi-Targeting](https://img.shields.io/badge/.NET-8.0%20%7C%204.6.2%20%7C%20Standard%202.0-purple.svg)](https://dotnet.microsoft.com/)
[![NuGet Version](https://img.shields.io/badge/NuGet-1.0.0-blue.svg)](https://www.nuget.org/packages/ZeroAsset)
[![Zero External Dependencies](https://img.shields.io/badge/Dependencies-0%20(Pure%20C%23)-brightgreen.svg)]()
[![Unit Tests](https://img.shields.io/badge/tests-9%20passed%20(100%25)-brightgreen.svg)](#-automated-testing)

---

## 📖 Overview

**ZeroAsset** is a sovereign, zero-external-dependency asset management engine for .NET. Built in pure C#, it provides foundational algorithms and data structures for high-speed photo and video management workflows, non-destructive virtual copy branching, strict hierarchical contiguous sorting, and collision-free content-addressable cache keys.

Designed for professional photo/video workstations (like **ZVision**) and industrial media management systems, `ZeroAsset` guarantees zero heap allocations on high-frequency evaluation hot paths.

---

## 🌟 Key Capabilities

- **Zero-Byte Variant Branching (`VariantIdentifier`)**:
  - Canonical `#vc<n>` suffix parser and string representation generator.
  - Disk path resolver identifying the physical master file path from arbitrary variant identifiers.
  - Monotonic copy ID allocation ensuring deterministic unique IDs for new copies.
  - Sidecar file resolution (e.g. `.xmp`) mapped back to master assets.
- **Hierarchical Contiguous Sorting (`HierarchicalContiguousComparer`)**:
  - Implements a Strict Weak Ordering comparator guaranteeing that child variants/virtual copies always remain contiguously adjacent to their master parent in the filmstrip or grid, regardless of the active primary sort mode (Capture Date, File Name, Star Rating, File Size, or Custom).
- **Immutable Asset Curation (`AssetCuration`)**:
  - Star ratings (0–5 stars with clamping).
  - Color labels (`None`, `Red`, `Yellow`, `Green`, `Blue`, `Purple`).
  - Culling pick flags (`None`, `Pick`, `Reject`).
  - Zero-allocation immutable record updates (`WithRating`, `WithFlag`, `WithColorLabel`, `WithKeywords`).
- **Content-Addressable Cache Keys (`AssetCacheKey`)**:
  - Cryptographically isolates master assets and virtual copies to prevent thumbnail, preview, or proxy cache collision.
  - Generates deterministic, hex-encoded 64-bit cache identifiers based on path, variant copy ID, and edit recipe hash.

---

## 📦 Installation

Install via the .NET CLI:
```bash
dotnet add package ZeroAsset
```

Or via the Package Manager Console:
```powershell
Install-Package ZeroAsset
```

---

## 🚀 Quick Start

### 1. Variant Identification & Master Resolution

```csharp
using ZeroAsset.Curation;

// Parse a virtual copy path
string virtualPath = @"D:\Photos\RAW_0042.CR3#vc1";
var variant = VariantIdentifier.Parse(virtualPath);

Console.WriteLine($"Is Master: {variant.IsMaster}");     // False
Console.WriteLine($"Copy ID: {variant.CopyId}");         // 1
Console.WriteLine($"Master File: {variant.MasterPath}"); // D:\Photos\RAW_0042.CR3

// Generate the next variant ID
int nextId = VariantIdentifier.GetNextCopyId(new[] { 1, 2 }); // 3
string newVariantPath = VariantIdentifier.CreateVariantPath(variant.MasterPath, nextId);
// -> D:\Photos\RAW_0042.CR3#vc3
```

### 2. Hierarchical Contiguous Filmstrip Sorting

```csharp
using ZeroAsset.Curation;

var items = new List<AssetNode>
{
    new AssetNode("IMG_0002.CR3", copyId: 0, rating: 5),
    new AssetNode("IMG_0001.CR3", copyId: 0, rating: 2),
    new AssetNode("IMG_0001.CR3#vc1", copyId: 1, rating: 4),
};

// Sort by rating while keeping variants grouped with their parent master
var comparer = new HierarchicalContiguousComparer<AssetNode>(
    primaryComparison: (a, b) => b.Rating.CompareTo(a.Rating)
);

items.Sort(comparer);
// Result: IMG_0001.CR3#vc1 stays directly with IMG_0001.CR3!
```

### 3. Collision-Free Thumbnail & Proxy Cache Keys

```csharp
using ZeroAsset.Curation;

// Compute deterministic cache key for a variant
string cacheKey = AssetCacheKey.Compute(
    masterPath: @"D:\Photos\RAW_0042.CR3",
    copyId: 1,
    pipelineRecipeHash: 0x9A4B12C3D4E5F678
);

Console.WriteLine($"Cache Key: {cacheKey}");
```

---

## 🧪 Automated Testing

ZeroAsset includes automated unit and stress tests targeting both `.NET 8.0` and `.NET Framework 4.6.2`:

```bash
dotnet test tests/ZeroAsset.Tests/ZeroAsset.Tests.csproj -c Release
```

---

## 📄 License & Author

Distributed under the permissive **MIT License**.  
Architected and maintained by **Phong Võ** (`kzxl`) as part of the **ZeroPlatform** industrial computing ecosystem.
