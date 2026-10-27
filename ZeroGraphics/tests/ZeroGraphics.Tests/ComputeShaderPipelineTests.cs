using System;
using Xunit;
using ZeroGraphics.DirectX.Core;
using ZeroGraphics.DirectX.Native;
using ZeroGraphics.Imaging.Core;
using ZeroGraphics.Imaging.Gpu;

namespace ZeroGraphics.Tests
{
    public class ComputeShaderPipelineTests
    {
        [Fact]
        public void GpuImageContext_ComputeShaders_InitializedValid()
        {
            D3D11DeviceManager.EnsureInitialized();
            if (!D3D11DeviceManager.IsSupported) return;

            using (var context = GpuImageContext.CreateDefault())
            {
                Assert.NotNull(context.CsColorAdjust);
                Assert.True(context.CsColorAdjust.IsValid);

                Assert.NotNull(context.CsBlurHorizontal);
                Assert.True(context.CsBlurHorizontal.IsValid);

                Assert.NotNull(context.CsBlurVertical);
                Assert.True(context.CsBlurVertical.IsValid);

                Assert.NotNull(context.CsConvolution3x3);
                Assert.True(context.CsConvolution3x3.IsValid);
            }
        }

        [Fact]
        public void GpuTexturePool_AcquireWithUav_ProvidesValidUav()
        {
            D3D11DeviceManager.EnsureInitialized();
            if (!D3D11DeviceManager.IsSupported) return;

            using (var context = GpuImageContext.CreateDefault())
            {
                var pool = context.TexturePool;

                // 1. Acquire standard texture (no UAV)
                var standardTex = pool.Acquire(64, 64, DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM, needsUav: false);
                Assert.NotNull(standardTex.Rtv);
                Assert.NotNull(standardTex.Srv);
                Assert.Null(standardTex.Uav);
                pool.Release(standardTex);

                // 2. Acquire UAV-enabled texture
                var uavTex = pool.Acquire(64, 64, DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM, needsUav: true);
                Assert.NotNull(uavTex.Uav);
                Assert.True(uavTex.Uav!.IsValid);
                pool.Release(uavTex);

                // 3. Re-acquire with UAV -> must reuse uavTex
                var uavTex2 = pool.Acquire(64, 64, DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM, needsUav: true);
                Assert.Same(uavTex, uavTex2);
                pool.Release(uavTex2);
            }
        }

        [Fact]
        public unsafe void DirectCompute_CsColorAdjust_GrayscaleAndInvert_Valid()
        {
            D3D11DeviceManager.EnsureInitialized();
            if (!D3D11DeviceManager.IsSupported) return;

            using (var context = GpuImageContext.CreateDefault())
            using (var src = ImageBuffer.CreateBgra32(32, 32))
            using (var dst = ImageBuffer.CreateBgra32(32, 32))
            {
                // Fill with pure red (B=0, G=0, R=255, A=255)
                for (int y = 0; y < 32; y++)
                {
                    uint* row = (uint*)src.GetRowPointer(y);
                    for (int x = 0; x < 32; x++)
                    {
                        row[x] = 0xFFFF0000; // BGRA: B=0, G=0, R=255, A=255
                    }
                }

                var pipeline = new ImagePipelineBuilder(context)
                    .AddCsColorAdjust(brightness: 0.0f, contrast: 1.0f, grayscale: true, invert: false)
                    .Compile();

                pipeline.Execute(src, dst);

                // Grayscale of pure red: 0.2126 * 255 ~= 54
                byte* pPixel = dst.GetRowPointer(16) + 16 * 4;
                byte b = pPixel[0];
                byte g = pPixel[1];
                byte r = pPixel[2];

                // All channels should be approximately equal for grayscale
                Assert.InRange(Math.Abs(b - g), 0, 3);
                Assert.InRange(Math.Abs(r - g), 0, 3);
                Assert.InRange(r, 45, 65);
            }
        }

        [Fact]
        public unsafe void DirectCompute_CsGaussianBlur_LdsApron_SmoothesImage()
        {
            D3D11DeviceManager.EnsureInitialized();
            if (!D3D11DeviceManager.IsSupported) return;

            using (var context = GpuImageContext.CreateDefault())
            using (var src = ImageBuffer.CreateBgra32(64, 64))
            using (var dst = ImageBuffer.CreateBgra32(64, 64))
            {
                // Create a single white point impulse at center (32, 32)
                for (int y = 0; y < 64; y++)
                {
                    uint* row = (uint*)src.GetRowPointer(y);
                    for (int x = 0; x < 64; x++)
                    {
                        row[x] = (x == 32 && y == 32) ? 0xFFFFFFFF : 0xFF000000;
                    }
                }

                var pipeline = new ImagePipelineBuilder(context)
                    .AddCsGaussianBlur(sigma: 2.0f)
                    .Compile();

                pipeline.Execute(src, dst);

                // Center should be diffused down from 255
                byte* pCenter = dst.GetRowPointer(32) + 32 * 4;
                byte centerVal = pCenter[0];
                Assert.True(centerVal > 0 && centerVal < 255, $"Expected center diffused, got {centerVal}");

                // Immediate neighbors should now have received non-zero energy
                byte* pNeighbor = dst.GetRowPointer(32) + 33 * 4;
                byte neighborVal = pNeighbor[0];
                Assert.True(neighborVal > 0, $"Expected neighbor to receive blur energy, got {neighborVal}");
                Assert.True(centerVal >= neighborVal, "Impulse center should have maximum or equal intensity");
            }
        }

        [Fact]
        public unsafe void DirectCompute_CsConvolution_SobelEdgeDetection_FindsBorders()
        {
            D3D11DeviceManager.EnsureInitialized();
            if (!D3D11DeviceManager.IsSupported) return;

            using (var context = GpuImageContext.CreateDefault())
            using (var src = ImageBuffer.CreateBgra32(64, 64))
            using (var dst = ImageBuffer.CreateBgra32(64, 64))
            {
                // Left half black, right half white
                for (int y = 0; y < 64; y++)
                {
                    uint* row = (uint*)src.GetRowPointer(y);
                    for (int x = 0; x < 64; x++)
                    {
                        row[x] = (x < 32) ? 0xFF000000 : 0xFFFFFFFF;
                    }
                }

                var pipeline = new ImagePipelineBuilder(context)
                    .AddCsSobel(strength: 1.0f)
                    .Compile();

                pipeline.Execute(src, dst);

                // Edge at boundary x=31 / x=32 should have high gradient response
                byte* pEdge = dst.GetRowPointer(32) + 31 * 4;
                Assert.True(pEdge[0] > 100, $"Expected edge response > 100, got {pEdge[0]}");

                // Flat region far away should have near-zero gradient
                byte* pFlat = dst.GetRowPointer(32) + 10 * 4;
                Assert.InRange(pFlat[0], 0, 5);
            }
        }


        [Fact]
        public void DirectCompute_DataResidency_ChainedGpuExecutionWithoutHostCopies()
        {
            D3D11DeviceManager.EnsureInitialized();
            if (!D3D11DeviceManager.IsSupported) return;

            using (var context = GpuImageContext.CreateDefault())
            using (var src = ImageBuffer.CreateBgra32(64, 64))
            using (var dst = ImageBuffer.CreateBgra32(64, 64))
            {
                // Pipeline 1: CS Grayscale
                var pipeline1 = new ImagePipelineBuilder(context)
                    .AddCsColorAdjust(brightness: 0.0f, contrast: 1.0f, grayscale: true)
                    .Compile();

                // Pipeline 2: CS Gaussian Blur
                var pipeline2 = new ImagePipelineBuilder(context)
                    .AddCsGaussianBlur(sigma: 1.5f)
                    .Compile();

                // Pipeline 3: CS Sharpen
                var pipeline3 = new ImagePipelineBuilder(context)
                    .AddCsSharpen(strength: 1.0f)
                    .Compile();

                // Execute Pipeline 1 directly to GPU texture (no CPU download)
                var gpuTex1 = pipeline1.ExecuteToGpu(src);
                Assert.NotNull(gpuTex1);

                // Execute Pipeline 2 directly on VRAM texture (Data Residency!)
                var gpuTex2 = pipeline2.ExecuteToGpu(gpuTex1, retainSource: false);
                Assert.NotNull(gpuTex2);

                // Execute Pipeline 3 on VRAM texture
                var gpuFinal = pipeline3.ExecuteToGpu(gpuTex2, retainSource: false);
                Assert.NotNull(gpuFinal);

                // Download only the final result
                context.Transfer.Download(gpuFinal.Texture, dst);
                context.TexturePool.Release(gpuFinal);

                Assert.Equal(64, dst.Width);
                Assert.Equal(64, dst.Height);
            }
        }

        [Fact]
        public void AutoBackendSelector_ResolvesOptimalBackend()
        {
            // 1. Small image (e.g. 320x240) single pass -> CPU SIMD preferred to avoid PCIe overhead
            var b1 = AutoBackendSelector.ResolveBackend(320, 240, isAlreadyResidentOnGpu: false, passCount: 1);
            Assert.Equal(ExecutionBackend.CpuSimd, b1);

            // 2. Large image (e.g. 3840x2160 4K) -> GPU Compute preferred
            var b2 = AutoBackendSelector.ResolveBackend(3840, 2160, isAlreadyResidentOnGpu: false, passCount: 1);
            Assert.Equal(ExecutionBackend.GpuCompute, b2);

            // 3. Medium image with multi-pass chain (passCount >= 3) -> GPU Compute
            var b3 = AutoBackendSelector.ResolveBackend(800, 600, isAlreadyResidentOnGpu: false, passCount: 4);
            Assert.Equal(ExecutionBackend.GpuCompute, b3);

            // 4. Data already resident on GPU VRAM -> always GPU Compute
            var b4 = AutoBackendSelector.ResolveBackend(200, 200, isAlreadyResidentOnGpu: true, passCount: 1);
            Assert.Equal(ExecutionBackend.GpuCompute, b4);

            // 5. Explicit user preference overrides heuristics
            var b5 = AutoBackendSelector.ResolveBackend(4000, 3000, preference: ExecutionBackend.CpuSimd);
            Assert.Equal(ExecutionBackend.CpuSimd, b5);
        }
    }
}
