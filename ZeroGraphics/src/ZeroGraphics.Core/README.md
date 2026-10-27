# ZeroGraphics.Core ⚡

Zero-dependency, high-performance mathematical, decimation, spatial indexing, and statistical quality control (SPC) algorithms for .NET (`netstandard2.0`, `net462`, `net8.0`).

---

## 🌟 Key Features

* **Peak-Preserving Decimation (`MinMaxDecimation`, `LttbDecimation`):** Downsamples 1,000,000+ points in <7ms with zero GC allocations while guaranteeing that narrow transient spikes and valleys are never lost.
* **2D Spatial Indexing (`QuadTree<T>`, `SpatialGrid<T>`):** $O(\log N)$ viewport frustum culling, indexing 50,000+ warehouse storage locations or CAD primitives with sub-millisecond query latency.
* **Statistical Process Control (`SpcAnalysis`, `SpcRuleEngine`):** Automated computation of Mean, $\sigma$, UCL, LCL, $Cp$, $Cpk$, Gaussian distribution curves, and Western Electric / Nelson out-of-control rules.
* **Spectral Analysis (`FftRadix2`, `WindowFunctions`):** 1D Cooley-Tukey Radix-2 Fast Fourier Transform with Hann, Hamming, and Blackman windowing for real-time vibration and audio telemetry.
* **Part of the [ZeroPlatform](https://github.com/kzxl/ZeroPlatform) Ecosystem.**
