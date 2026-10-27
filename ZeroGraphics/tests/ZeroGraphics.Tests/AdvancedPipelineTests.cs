using System;
using System.Collections.Generic;
using Xunit;
using ZeroGraphics.DirectX.Core;
using ZeroGraphics.DirectX.Native;
using ZeroGraphics.Imaging.Core;
using ZeroGraphics.Imaging.Gpu;

namespace ZeroGraphics.Tests
{
    public class AdvancedPipelineTests
    {
        [Fact]
        public void GpuTensorPreprocessor_FormatsNCHWPlanarTensorWithImageNetNormalization()
        {
            D3D11DeviceManager.EnsureInitialized();
            if (!D3D11DeviceManager.IsSupported) return;

            using (var context = GpuImageContext.CreateDefault())
            {
                int srcW = 64;
                int srcH = 64;
                int targetW = 32;
                int targetH = 32;

                // Create a solid Red image (R=255, G=0, B=0, A=255)
                using (var srcBuf = new ImageBuffer(srcW, srcH, ImageFormatMode.Bgra32))
                {
                    unsafe
                    {
                        for (int y = 0; y < srcH; y++)
                        {
                            byte* row = srcBuf.GetRowPointer(y);
                            for (int x = 0; x < srcW; x++)
                            {
                                row[x * 4 + 0] = 0;    // B
                                row[x * 4 + 1] = 0;    // G
                                row[x * 4 + 2] = 255;  // R
                                row[x * 4 + 3] = 255;  // A
                            }
                        }
                    }

                    using (var preprocessor = new GpuTensorPreprocessor(context))
                    using (var tensor = preprocessor.Preprocess(srcBuf, targetW, targetH, TensorNormalization.ImageNet))
                    {
                        Assert.Equal(3, tensor.Channels);
                        Assert.Equal(targetH, tensor.Height);
                        Assert.Equal(targetW, tensor.Width);
                        Assert.Equal(1, tensor.Batch);
                        Assert.Equal(3 * targetW * targetH, tensor.ElementCount);

                        float[] hostData = new float[tensor.ElementCount];
                        tensor.DownloadToHost(hostData);

                        int planeSize = targetW * targetH;

                        // Verify Channel 0 (Red): (1.0 - 0.485) / 0.229 ~= 2.2489
                        float expectedR = (1.0f - 0.485f) / 0.229f;
                        float actualR = hostData[0 * planeSize + 10];
                        Assert.InRange(actualR, expectedR - 0.05f, expectedR + 0.05f);

                        // Verify Channel 1 (Green): (0.0 - 0.456) / 0.224 ~= -2.0357
                        float expectedG = (0.0f - 0.456f) / 0.224f;
                        float actualG = hostData[1 * planeSize + 10];
                        Assert.InRange(actualG, expectedG - 0.05f, expectedG + 0.05f);

                        // Verify Channel 2 (Blue): (0.0 - 0.406) / 0.225 ~= -1.8044
                        float expectedB = (0.0f - 0.406f) / 0.225f;
                        float actualB = hostData[2 * planeSize + 10];
                        Assert.InRange(actualB, expectedB - 0.05f, expectedB + 0.05f);
                    }
                }
            }
        }

        [Fact]
        public void FusedVisionPipeline_ExecutesEndToEndCvAiWorkflow()
        {
            D3D11DeviceManager.EnsureInitialized();
            if (!D3D11DeviceManager.IsSupported) return;

            using (var context = GpuImageContext.CreateDefault())
            {
                int width = 64;
                int height = 64;

                using (var srcBuf = new ImageBuffer(width, height, ImageFormatMode.Bgra32))
                {
                    unsafe
                    {
                        for (int y = 0; y < height; y++)
                        {
                            byte* row = srcBuf.GetRowPointer(y);
                            for (int x = 0; x < width; x++)
                            {
                                // Dark background with anomalous defect spot at center
                                bool isDefect = (x >= 28 && x <= 36 && y >= 28 && y <= 36);
                                byte val = isDefect ? (byte)250 : (byte)20;

                                row[x * 4 + 0] = val;
                                row[x * 4 + 1] = val;
                                row[x * 4 + 2] = val;
                                row[x * 4 + 3] = 255;
                            }
                        }
                    }

                    var inputTex = context.TexturePool.Acquire(width, height, DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM);
                    context.Transfer.Upload(srcBuf, inputTex.Texture);

                    using (var pipeline = new FusedVisionPipeline(context))
                    using (var detector = new ReferenceAnomalyDetector())
                    {
                        using (var visualOutput = pipeline.ExecuteInspection(
                            inputTex,
                            detector,
                            tensorWidth: 32,
                            tensorHeight: 32,
                            overlayAlpha: 0.8f,
                            overlayThreshold: 0.2f,
                            applyGaussianDenoise: true))
                        {
                            Assert.Equal(width, visualOutput.Width);
                            Assert.Equal(height, visualOutput.Height);

                            using (var outBuf = new ImageBuffer(width, height, ImageFormatMode.Bgra32))
                            {
                                context.Transfer.Download(visualOutput.Texture, outBuf);

                                unsafe
                                {
                                    // Verify background pixel remains dark
                                    byte* bg = outBuf.GetRowPointer(5) + 5 * 4;
                                    Assert.True(bg[2] < 50, "Background should remain dark");

                                    // Verify center defect pixel has vibrant alert response
                                    byte* defect = outBuf.GetRowPointer(32) + 32 * 4;
                                    Assert.True(defect[2] > 100, "Defect center should have active overlay");
                                }
                            }
                        }
                    }

                    context.TexturePool.Release(inputTex);
                }
            }
        }

        [Fact]
        public void GigapixelTileScheduler_ProcessesGigapixelImageWithoutVramOverflowOrSeams()
        {
            D3D11DeviceManager.EnsureInitialized();
            if (!D3D11DeviceManager.IsSupported) return;

            using (var context = GpuImageContext.CreateDefault())
            {
                int width = 160;
                int height = 160;

                using (var srcBuf = new ImageBuffer(width, height, ImageFormatMode.Bgra32))
                using (var dstBuf = new ImageBuffer(width, height, ImageFormatMode.Bgra32))
                {
                    unsafe
                    {
                        for (int y = 0; y < height; y++)
                        {
                            byte* row = srcBuf.GetRowPointer(y);
                            for (int x = 0; x < width; x++)
                            {
                                row[x * 4 + 0] = (byte)x;
                                row[x * 4 + 1] = (byte)y;
                                row[x * 4 + 2] = (byte)((x + y) / 2);
                                row[x * 4 + 3] = 255;
                            }
                        }
                    }

                    using (var scheduler = new GigapixelTileScheduler(context, tileSize: 64, apronSize: 8))
                    {
                        var reports = new List<TileProgressReport>();
                        var progress = new Progress<TileProgressReport>(r => reports.Add(r));

                        // Simple pass-through or color invert tile processor
                        var colorPass = new CsColorPassNode(invert: true);
                        scheduler.ProcessTiled(srcBuf, dstBuf, tile =>
                        {
                            var inverted = context.TexturePool.Acquire(tile.Width, tile.Height, tile.Format, needsUav: true);
                            colorPass.Execute(context, tile, inverted);
                            return inverted;
                        }, progress);

                        // Verify stitching: Inverted Red channel at (10, 10) = 255 - (10 + 10)/2 = 245
                        unsafe
                        {
                            byte* p10 = dstBuf.GetRowPointer(10) + 10 * 4;
                            int expectedR = 255 - 10;
                            Assert.Equal(expectedR, p10[2]);

                            // Verify cross-tile stitching at boundary (e.g. x=64, y=64)
                            byte* p64 = dstBuf.GetRowPointer(64) + 64 * 4;
                            int expectedR64 = 255 - 64;
                            Assert.Equal(expectedR64, p64[2]);
                        }
                    }
                }
            }
        }

        [Fact]
        public void HeterogeneousScheduler_DispatchesConcurrentBatchesAcrossAvailableAdapters()
        {
            D3D11DeviceManager.EnsureInitialized();
            if (!D3D11DeviceManager.IsSupported) return;

            using (var scheduler = new HeterogeneousScheduler())
            {
                Assert.True(scheduler.NodeCount >= 1, "At least one compute node must be detected");

                int batchSize = 4;
                var inputs = new List<ImageBuffer>();
                for (int i = 0; i < batchSize; i++)
                {
                    var buf = new ImageBuffer(32, 32, ImageFormatMode.Bgra32);
                    inputs.Add(buf);
                }

                try
                {
                    var results = scheduler.DispatchBatch(inputs, (ctx, input) =>
                    {
                        var output = new ImageBuffer(input.Width, input.Height, input.Format);
                        // Process copy via host buffer
                        unsafe
                        {
                            Buffer.MemoryCopy((void*)input.Scan0, (void*)output.Scan0, input.Height * input.Stride, input.Height * input.Stride);
                        }
                        return output;
                    });

                    Assert.Equal(batchSize, results.Length);
                    for (int i = 0; i < batchSize; i++)
                    {
                        Assert.NotNull(results[i]);
                        Assert.Equal(32, results[i].Width);
                        Assert.Equal(32, results[i].Height);
                        results[i].Dispose();
                    }
                }
                finally
                {
                    foreach (var buf in inputs) buf.Dispose();
                }
            }
        }
    }
}
