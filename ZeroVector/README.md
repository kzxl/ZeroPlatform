# 📐 ZeroVector: Sovereign Embedded Vector Database & SIMD Similarity Engine

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET Multi-Targeting](https://img.shields.io/badge/.NET-8.0%20%7C%204.6.2%20%7C%20Standard%202.0-purple.svg)](https://dotnet.microsoft.com/)
[![Zero External Dependencies](https://img.shields.io/badge/Dependencies-0%20(Pure%20C%23)-brightgreen.svg)]()

**ZeroVector** is an ultra-high-throughput, zero-allocation embedded Vector Database and SIMD similarity metric engine engineered in 100% pure C# for .NET. It resides in **Tier 2 (Transport & Storage)** of the [ZeroPlatform](https://github.com/kzxl/ZeroPlatform) ecosystem.

---

## ⚡ Key Capabilities

- **Hardware-Accelerated SIMD Metrics (AVX2 / FMA)**:
  - `DotProduct`: Simultaneous 8-wide float multiply-add evaluation.
  - `CosineSimilarity`: Single-pass combined dot product and norm calculation ($\le 1.0$).
  - `EuclideanDistance` & `EuclideanDistanceSquared`: L2 metric for spatial embeddings.
  - `ManhattanDistance`: L1 metric for sparse features.
  - `HammingDistance`: 64-bit hardware POPCNT for binary bitstrings (ORB/BRIEF).
- **Vector Indices**:
  - `FlatVectorIndex`: Contiguous memory, exact brute-force Top-K search with multi-core parallel partitioning for up to millions of embeddings.
  - `HnswVectorIndex`: Hierarchical Navigable Small World (HNSW) graph index providing sub-millisecond $O(\log N)$ approximate nearest neighbor (ANN) search with configurable $M$, $efConstruction$, and $efSearch$.
- **Storage & Persistence**:
  - Compact zero-overhead binary index serialization and deserialization (`VectorIndexFile`).
- **Zero External Dependencies & Multi-Targeting**:
  - Pure C# implementation compatible with `.NET 8.0+`, `.NET Framework 4.6.2+`, and `.NET Standard 2.0`.

---

## 🚀 Quick Start

### 1. Flat Contiguous Index (Exact Search)

```csharp
using ZeroVector.Core.Indices;
using ZeroVector.Core.Metrics;

// Initialize index for 384-dimensional embeddings
var index = new FlatVectorIndex(dimension: 384, initialCapacity: 1024);

// Add embeddings
ReadOnlySpan<float> embedding = stackalloc float[384];
// ... populate embedding ...
index.Add(id: 1, embedding);

// Query Top-K nearest neighbors via Cosine Similarity
ReadOnlySpan<float> query = stackalloc float[384];
VectorSearchResult[] results = index.SearchTopK(query, k: 5, VectorMetricType.Cosine);

foreach (var r in results)
{
    Console.WriteLine($"Matched ID: {r.Id}, Similarity Score: {r.Score:F4}");
}
```

### 2. HNSW Graph Index (Fast Approximate Search)

```csharp
using ZeroVector.Core.Indices;
using ZeroVector.Core.Metrics;

// Initialize HNSW index
var hnsw = new HnswVectorIndex(
    dimension: 128,
    m: 16,
    efConstruction: 100,
    efSearch: 50,
    metric: VectorMetricType.Cosine
);

// Populate index
for (int i = 0; i < 10000; i++)
{
    float[] vec = GenerateVector(128);
    hnsw.Add(id: i, vec);
}

// Sub-millisecond ANN search
VectorSearchResult[] matches = hnsw.SearchTopK(queryVec, k: 10);
```

---

## 🏛 Architectural Placement in ZeroPlatform

ZeroVector resides strictly within **Tier 2 (Transport & Storage)**:
- **Upstream consumers**: `ZeroInference` (L3), `ZeroGraphics` (L4), `ZeroAgent` (L5), and `ZeroPipeline` (L5).
- **Downstream dependencies**: `ZeroPrimitives` (L0) and pure .NET primitives.

---

## 📄 License

Architected and developed by **Phong Võ** (`kzxl`). Released under the **MIT License**.
