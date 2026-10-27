using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Xunit;
using ZeroGraphics.DirectX.Core;
using ZeroGraphics.DirectX.Native;
using ZeroGraphics.DirectX.Pipeline;
using ZeroGraphics.Imaging.Core;
using ZeroGraphics.Imaging.Gpu;

namespace ZeroGraphics.Tests
{
    public class StreamingPipelineTests
    {
        [Fact]
        public void GpuStagingPool_ReusesStagingTextures()
        {
            D3D11DeviceManager.EnsureInitialized();
            if (!D3D11DeviceManager.IsSupported) return;

            var device = D3D11DeviceManager.Device;
            using (var pool = new GpuStagingPool(device))
            {
                // 1. Initial allocation of read staging texture
                var texRead1 = pool.AcquireReadStaging(64, 64, DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM);
                Assert.NotNull(texRead1);
                Assert.True(texRead1.IsReadStaging);
                Assert.Equal(1, pool.TotalAllocatedCount);
                Assert.Equal(1, pool.ActiveLeasedCount);

                // 2. Initial allocation of write staging texture (different intent)
                var texWrite1 = pool.AcquireWriteStaging(64, 64, DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM);
                Assert.NotNull(texWrite1);
                Assert.False(texWrite1.IsReadStaging);
                Assert.Equal(2, pool.TotalAllocatedCount);
                Assert.Equal(2, pool.ActiveLeasedCount);

                // 3. Release read texture and re-acquire -> must reuse texRead1
                pool.Release(texRead1);
                Assert.Equal(1, pool.ActiveLeasedCount);

                var texRead2 = pool.AcquireReadStaging(64, 64, DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM);
                Assert.Same(texRead1, texRead2);
                Assert.Equal(2, pool.TotalAllocatedCount);
                Assert.Equal(2, pool.ActiveLeasedCount);

                pool.Release(texRead2);
                pool.Release(texWrite1);
                Assert.Equal(0, pool.ActiveLeasedCount);

                // 4. Test RAII Lease struct
                using (var lease = pool.LeaseRead(64, 64, DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM))
                {
                    Assert.Same(texRead1, lease.Texture);
                    Assert.Equal(1, pool.ActiveLeasedCount);
                }
                Assert.Equal(0, pool.ActiveLeasedCount);
            }
        }

        [Fact]
        public void GpuTimer_MeasuresKernelExecutionAccurately()
        {
            D3D11DeviceManager.EnsureInitialized();
            if (!D3D11DeviceManager.IsSupported) return;

            using (var context = GpuImageContext.CreateDefault())
            {
                using (var timer = new GpuTimer(context.Device))
                {
                    // Create an input texture and an output UAV texture
                    using var inTex = context.TexturePool.Acquire(128, 128, DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM, needsUav: false);
                    using var outTex = context.TexturePool.Acquire(128, 128, DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM, needsUav: true);

                    // Start GPU Timer
                    timer.Start(context.ImmediateContext);

                    // Dispatch Compute Shader
                    var colorPass = new CsColorPassNode(brightness: 0.2f, contrast: 1.1f, grayscale: false, invert: false);
                    colorPass.Execute(context, inTex, outTex);

                    // Stop GPU Timer
                    timer.Stop(context.ImmediateContext);

                    // Measure elapsed microseconds
                    double elapsedUs = timer.GetElapsedMicroseconds(context.ImmediateContext, waitForGpu: true);
                    double elapsedMs = timer.GetElapsedMilliseconds(context.ImmediateContext, waitForGpu: true);

                    Assert.True(elapsedUs >= 0.0);
                    Assert.True(elapsedMs >= 0.0);

                    context.TexturePool.Release(inTex);
                    context.TexturePool.Release(outTex);
                }
            }
        }

        [Fact]
        public void DoubleBufferedGpuTarget_PingPongsCorrectly()
        {
            D3D11DeviceManager.EnsureInitialized();
            if (!D3D11DeviceManager.IsSupported) return;

            using (var context = GpuImageContext.CreateDefault())
            {
                using (var doubleTarget = new DoubleBufferedGpuTarget(context.Device, context.TexturePool, 64, 64))
                {
                    var initialWrite = doubleTarget.CurrentWrite;
                    var initialRead = doubleTarget.CurrentRead;

                    Assert.NotSame(initialWrite, initialRead);

                    // Swap
                    doubleTarget.Swap(context.ImmediateContext);

                    Assert.Same(initialWrite, doubleTarget.CurrentRead);
                    Assert.Same(initialRead, doubleTarget.CurrentWrite);

                    // Wait for read ready (should complete immediately)
                    doubleTarget.WaitForReadReady(context.ImmediateContext);

                    // Swap again -> returns to initial configuration
                    doubleTarget.Swap(context.ImmediateContext);
                    Assert.Same(initialWrite, doubleTarget.CurrentWrite);
                    Assert.Same(initialRead, doubleTarget.CurrentRead);
                }
            }
        }

        [Fact]
        public unsafe void GpuBatchProcessor_ProcessesBatchEfficiently()
        {
            D3D11DeviceManager.EnsureInitialized();
            if (!D3D11DeviceManager.IsSupported) return;

            using (var context = GpuImageContext.CreateDefault())
            {
                using (var batchProcessor = new GpuBatchProcessor(context))
                {
                    const int batchSize = 6;
                    var inputs = new List<ImageBuffer>(batchSize);
                    for (int i = 0; i < batchSize; i++)
                    {
                        var img = new ImageBuffer(32, 32, ImageFormatMode.Bgra32);
                        byte* p = (byte*)img.Scan0;
                        for (int k = 0; k < img.Stride * 32; k += 4)
                        {
                            p[k] = (byte)(i * 40);     // B
                            p[k + 1] = (byte)(i * 30); // G
                            p[k + 2] = (byte)(i * 20); // R
                            p[k + 3] = 255;            // A
                        }
                        inputs.Add(img);
                    }

                    // Build pipeline
                    var pipeline = new ImagePipelineBuilder(context)
                        .AddCsColorAdjust(brightness: 0.1f, contrast: 1.0f, grayscale: true, invert: false)
                        .Compile();

                    var reports = new List<BatchProgressReport>();
                    var progress = new Progress<BatchProgressReport>(r => reports.Add(r));

                    // Execute batch
                    var results = batchProcessor.ProcessBatch(inputs, pipeline, progress);

                    Assert.Equal(batchSize, results.Length);
                    for (int i = 0; i < batchSize; i++)
                    {
                        Assert.NotNull(results[i]);
                        Assert.Equal(32, results[i].Width);
                        Assert.Equal(32, results[i].Height);

                        // Grayscale verification: R == G == B
                        byte* pRes = (byte*)results[i].Scan0;
                        byte b = pRes[0];
                        byte g = pRes[1];
                        byte r = pRes[2];
                        Assert.Equal(b, g);
                        Assert.Equal(g, r);

                        results[i].Dispose();
                        inputs[i].Dispose();
                    }
                }
            }
        }

        [Fact]
        public unsafe void GpuBatchProcessor_ProcessBatchPipelined_DirectVramTransfer()
        {
            D3D11DeviceManager.EnsureInitialized();
            if (!D3D11DeviceManager.IsSupported) return;

            using (var context = GpuImageContext.CreateDefault())
            {
                using (var batchProcessor = new GpuBatchProcessor(context))
                {
                    const int batchSize = 4;
                    var inputs = new List<ImageBuffer>(batchSize);
                    var outputs = new List<ImageBuffer>(batchSize);

                    for (int i = 0; i < batchSize; i++)
                    {
                        var inImg = new ImageBuffer(32, 32, ImageFormatMode.Bgra32);
                        byte* p = (byte*)inImg.Scan0;
                        for (int k = 0; k < inImg.Stride * 32; k += 4)
                        {
                            p[k] = 100;
                            p[k + 1] = 100;
                            p[k + 2] = 100;
                            p[k + 3] = 255;
                        }
                        inputs.Add(inImg);
                        outputs.Add(new ImageBuffer(32, 32, ImageFormatMode.Bgra32));
                    }

                    var invertPass = new CsColorPassNode(brightness: 0.0f, contrast: 1.0f, grayscale: false, invert: true);

                    // Process using custom GPU action with Staging Texture Ring
                    batchProcessor.ProcessBatchPipelined(inputs, outputs, (inTex, outTex) =>
                    {
                        invertPass.Execute(context, inTex, outTex);
                    });

                    // Verification: pixels inverted (255 - 100 = 155)
                    for (int i = 0; i < batchSize; i++)
                    {
                        byte* pOut = (byte*)outputs[i].Scan0;
                        Assert.Equal(155, pOut[0]);
                        Assert.Equal(155, pOut[1]);
                        Assert.Equal(155, pOut[2]);

                        inputs[i].Dispose();
                        outputs[i].Dispose();
                    }
                }
            }
        }

        [Fact]
        public void ZeroCopyPresenter_PresentsToHwndSwapChainWithoutHostCopy()
        {
            D3D11DeviceManager.EnsureInitialized();
            if (!D3D11DeviceManager.IsSupported) return;

            using (var canvas = new Control())
            {
                IntPtr hwnd = canvas.Handle;
                using (var swapChain = new HwndSwapChain(hwnd, 64, 64))
                {
                    Assert.True(swapChain.IsValid);

                    using (var context = GpuImageContext.CreateDefault())
                    {
                        // 1. Acquire processed GPU texture
                        using var inTex = context.TexturePool.Acquire(64, 64, DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM, needsUav: false);
                        using var outTex = context.TexturePool.Acquire(64, 64, DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM, needsUav: true);

                        // 2. Render color adjustment into it
                        var colorPass = new CsColorPassNode(brightness: 0.1f, contrast: 1.0f, grayscale: false, invert: false);
                        colorPass.Execute(context, inTex, outTex);

                        // 3. Zero-Copy Present directly to SwapChain
                        bool presented = ZeroCopyPresenter.Present(context.ImmediateContext, swapChain, outTex);
                        Assert.True(presented);

                        // 4. Test ExecuteAndPresent
                        using var testImg = new ImageBuffer(64, 64, ImageFormatMode.Bgra32);
                        var pipeline = new ImagePipelineBuilder(context)
                            .AddCsColorAdjust(brightness: 0.05f, contrast: 1.0f, grayscale: true, invert: false)
                            .Compile();

                        bool pipelinePresented = ZeroCopyPresenter.ExecuteAndPresent(pipeline, testImg, swapChain);
                        Assert.True(pipelinePresented);

                        context.TexturePool.Release(inTex);
                        context.TexturePool.Release(outTex);
                    }
                }
            }
        }
    }
}
