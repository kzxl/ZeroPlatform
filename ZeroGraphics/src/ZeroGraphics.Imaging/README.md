# ZeroGraphics.Imaging ⚡

Hardware-accelerated GPU Image Processing, Render Graph optimizer, transient texture pooling, and industrial computer vision filters for .NET (`net462`, `net8.0-windows`).

---

## 🌟 Key Features

* **GPU Render Graph & Kernel Fusion (`ImagePipelineBuilder`):** Assembles multi-pass processing pipelines and automatically fuses consecutive passes into a single hardware HLSL kernel, slashing VRAM bandwidth by up to 75%.
* **Zero-Allocation GPU Texture Pool (`GpuTexturePool`):** Recycles intermediate RenderTargetViews and ShaderResourceViews across continuous camera acquisition loops with zero runtime heap allocation.
* **13 Precompiled HLSL Pixel Shaders:** High-speed GPU kernels (Resize, ColorAdjust, GaussianBlur, Sobel, Sharpen, Threshold, Fused, Dilate, Erode, AffineTransform, CannyNms, Gamma) embedded as precompiled bytecode.
* **Zero-Copy Host <-> GPU DMA Transfer (`GpuTextureTransfer`):** Pinned unmanaged buffer mapping with Direct3D 11 staging textures.
* **Color Science & Metrology (`CieLabColor`, `ColorDifference`):** CIE L\*a\*b\*, BT.709 Grayscale, sRGB/Linear conversions, and full **CIEDE2000 ($\Delta E_{00}$)** color difference metrology.
* **Mathematical Morphology:** Dilation, Erosion, Opening, Closing, and unmanaged pixel kernels.
* **Part of the [ZeroPlatform](https://github.com/kzxl/ZeroPlatform) Ecosystem.**
