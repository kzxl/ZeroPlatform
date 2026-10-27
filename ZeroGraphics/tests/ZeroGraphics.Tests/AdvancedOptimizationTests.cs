using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using ZeroGraphics.DirectX.Core;
using ZeroGraphics.DirectX.Native;
using ZeroGraphics.Imaging.Core;
using ZeroGraphics.Imaging.Gpu;

namespace ZeroGraphics.Tests
{
    public class AdvancedOptimizationTests
    {
        [Fact]
        public async Task D3D11DeferredContext_RecordsAndExecutesCommandListConcurrently()
        {
            D3D11DeviceManager.EnsureInitialized();
            if (!D3D11DeviceManager.IsSupported) return;

            using (var context = GpuImageContext.CreateDefault())
            {
                int width = 64;
                int height = 64;

                var srcTex = context.TexturePool.Acquire(width, height, DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM);
                var dstTex = context.TexturePool.Acquire(width, height, DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM, needsUav: true);

                try
                {
                    // 1. Create a Deferred Context on a separate task/thread
                    D3D11CommandList recordedCmdList = null!;

                    var recordTask = Task.Run(() =>
                    {
                        using (var deferredCtx = context.Device.CreateDeferredContext())
                        {
                            Assert.True(deferredCtx.IsDeferred);

                            // Record compute dispatch into deferred context
                            deferredCtx.CSSetShader(context.CsColorAdjust);
                            deferredCtx.CSSetConstantBuffers(0, context.ConstantBuffer);
                            deferredCtx.CSSetShaderResources(0, srcTex.Srv);
                            deferredCtx.CSSetUnorderedAccessViews(0, dstTex.Uav);

                            deferredCtx.Dispatch(4, 4, 1);

                            deferredCtx.CSSetShaderResources(0, (D3D11ShaderResourceView?)null);
                            deferredCtx.CSSetUnorderedAccessViews(0, (D3D11UnorderedAccessView?)null);

                            // Finish recording command list
                            recordedCmdList = deferredCtx.FinishCommandList(restoreDeferredContextState: false);
                        }
                    });

                    await recordTask;

                    Assert.NotNull(recordedCmdList);
                    Assert.True(recordedCmdList.IsValid);

                    // 2. Replay recorded command list on the Immediate Context
                    using (recordedCmdList)
                    {
                        context.ImmediateContext.ExecuteCommandList(recordedCmdList, restoreContextState: false);
                    }
                }
                finally
                {
                    context.TexturePool.Release(srcTex);
                    context.TexturePool.Release(dstTex);
                }
            }
        }

        [Fact]
        public void GigapixelTileScheduler_StreamsFromMemoryMappedFileDirectlyToDisk()
        {
            D3D11DeviceManager.EnsureInitialized();
            if (!D3D11DeviceManager.IsSupported) return;

            string tempSrcFile = Path.Combine(Path.GetTempPath(), $"zero_src_{Guid.NewGuid():N}.raw");
            string tempDstFile = Path.Combine(Path.GetTempPath(), $"zero_dst_{Guid.NewGuid():N}.raw");

            try
            {
                int width = 128;
                int height = 128;

                // 1. Initialize source memory-mapped image file
                using (var srcMm = MemoryMappedImageBuffer.CreateNew(tempSrcFile, width, height, ImageFormatMode.Bgra32))
                using (var dstMm = MemoryMappedImageBuffer.CreateNew(tempDstFile, width, height, ImageFormatMode.Bgra32))
                {
                    unsafe
                    {
                        for (int y = 0; y < height; y++)
                        {
                            byte* row = srcMm.GetRowPointer(y);
                            for (int x = 0; x < width; x++)
                            {
                                row[x * 4 + 0] = (byte)x;
                                row[x * 4 + 1] = (byte)y;
                                row[x * 4 + 2] = 200;
                                row[x * 4 + 3] = 255;
                            }
                        }
                    }
                    srcMm.Flush();

                    using (var context = GpuImageContext.CreateDefault())
                    using (var scheduler = new GigapixelTileScheduler(context, tileSize: 64, apronSize: 8))
                    {
                        // 2. Stream tiles directly from disk file -> GPU -> destination disk file
                        var colorPass = new CsColorPassNode(invert: true);
                        scheduler.ProcessTiled(srcMm, dstMm, tile =>
                        {
                            var inverted = context.TexturePool.Acquire(tile.Width, tile.Height, tile.Format, needsUav: true);
                            colorPass.Execute(context, tile, inverted);
                            return inverted;
                        });

                        // 3. Verify destination values written directly to file
                        unsafe
                        {
                            byte* p10 = dstMm.GetRowPointer(10) + 10 * 4;
                            // Invert: 255 - 200 = 55
                            Assert.Equal(55, p10[2]);

                            byte* p64 = dstMm.GetRowPointer(64) + 64 * 4;
                            Assert.Equal(55, p64[2]);
                        }
                    }
                }
            }
            finally
            {
                if (File.Exists(tempSrcFile)) File.Delete(tempSrcFile);
                if (File.Exists(tempDstFile)) File.Delete(tempDstFile);
            }
        }

        [Fact]
        public void DirectMlInferenceEngine_DetectsHardwareAndExecutesOrGracefullyFallsBack()
        {
            D3D11DeviceManager.EnsureInitialized();
            if (!D3D11DeviceManager.IsSupported) return;

            using (var context = GpuImageContext.CreateDefault())
            {
                using (var dmlEngine = new DirectMlInferenceEngine(context.Device))
                {
                    Assert.NotNull(dmlEngine);

                    // Create test input tensor (3 channels x 32 x 32)
                    using (var tensor = new GpuTensorBuffer(context.Device, context.ImmediateContext, 3, 32, 32))
                    {
                        using (var mask = dmlEngine.Infer(context, tensor))
                        {
                            Assert.NotNull(mask);
                            Assert.Equal(32, mask.Width);
                            Assert.Equal(32, mask.Height);
                        }
                    }
                }
            }
        }
    }
}
