using System;
using Xunit;
using ZeroGraphics.DirectX.Core;
using ZeroGraphics.DirectX.Native;
using ZeroGraphics.Imaging.Core;
using ZeroGraphics.Imaging.Gpu;

namespace ZeroGraphics.Tests
{
    public class ComputationalVisionTests
    {
        [Fact]
        public void GpuImagePyramid_GeneratesMultiScaleLevelsAccurately()
        {
            D3D11DeviceManager.EnsureInitialized();
            if (!D3D11DeviceManager.IsSupported) return;

            using (var context = GpuImageContext.CreateDefault())
            {
                int width = 128;
                int height = 128;

                // 1. Create source image with gradient
                using (var srcBuf = new ImageBuffer(width, height, ImageFormatMode.Bgra32))
                {
                    unsafe
                    {
                        for (int y = 0; y < height; y++)
                        {
                            byte* row = srcBuf.GetRowPointer(y);
                            for (int x = 0; x < width; x++)
                            {
                                row[x * 4 + 0] = (byte)(x * 2);        // B
                                row[x * 4 + 1] = (byte)(y * 2);        // G
                                row[x * 4 + 2] = (byte)((x + y));      // R
                                row[x * 4 + 3] = 255;                  // A
                            }
                        }
                    }

                    var baseTex = context.TexturePool.Acquire(width, height, DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM, needsUav: true);
                    context.Transfer.Upload(srcBuf, baseTex.Texture);

                    // 2. Build 4-level Gaussian Pyramid (128 -> 64 -> 32 -> 16)
                    using (var pyramid = GpuImagePyramid.BuildGaussian(context, baseTex, octaves: 4))
                    {
                        Assert.Equal(4, pyramid.Count);
                        Assert.Equal(128, pyramid[0].Width);
                        Assert.Equal(128, pyramid[0].Height);
                        Assert.Equal(64, pyramid[1].Width);
                        Assert.Equal(64, pyramid[1].Height);
                        Assert.Equal(32, pyramid[2].Width);
                        Assert.Equal(32, pyramid[2].Height);
                        Assert.Equal(16, pyramid[3].Width);
                        Assert.Equal(16, pyramid[3].Height);

                        // 3. Test Upsample from Level 2 (32x32) to 64x64
                        using (var upsampled = GpuImagePyramid.Upsample(context, pyramid[2], 64, 64))
                        {
                            Assert.Equal(64, upsampled.Width);
                            Assert.Equal(64, upsampled.Height);

                            using (var dstBuf = new ImageBuffer(64, 64, ImageFormatMode.Bgra32))
                            {
                                context.Transfer.Download(upsampled.Texture, dstBuf);

                                unsafe
                                {
                                    byte* center = dstBuf.GetRowPointer(32) + 32 * 4;
                                    // Check that valid color was interpolated
                                    Assert.True(center[3] > 0);
                                }
                            }
                        }
                    }

                    context.TexturePool.Release(baseTex);
                }
            }
        }

        [Fact]
        public void GpuFocusStacker_BlendsFocalSlicesIntoSingleInFocusImage()
        {
            D3D11DeviceManager.EnsureInitialized();
            if (!D3D11DeviceManager.IsSupported) return;

            using (var context = GpuImageContext.CreateDefault())
            {
                int width = 64;
                int height = 64;

                // Slice 1: Left half has high-frequency stripes (sharp), Right half is flat gray (blurry)
                using (var slice1 = new ImageBuffer(width, height, ImageFormatMode.Bgra32))
                // Slice 2: Left half is flat gray (blurry), Right half has high-frequency stripes (sharp)
                using (var slice2 = new ImageBuffer(width, height, ImageFormatMode.Bgra32))
                {
                    unsafe
                    {
                        for (int y = 0; y < height; y++)
                        {
                            byte* row1 = slice1.GetRowPointer(y);
                            byte* row2 = slice2.GetRowPointer(y);

                            for (int x = 0; x < width; x++)
                            {
                                byte stripe = (byte)(x % 2 == 0 ? 250 : 10);
                                byte flat = 128;

                                if (x < 32)
                                {
                                    // Slice 1 is sharp on left
                                    row1[x * 4 + 0] = stripe;
                                    row1[x * 4 + 1] = stripe;
                                    row1[x * 4 + 2] = stripe;
                                    row1[x * 4 + 3] = 255;

                                    row2[x * 4 + 0] = flat;
                                    row2[x * 4 + 1] = flat;
                                    row2[x * 4 + 2] = flat;
                                    row2[x * 4 + 3] = 255;
                                }
                                else
                                {
                                    // Slice 2 is sharp on right
                                    row1[x * 4 + 0] = flat;
                                    row1[x * 4 + 1] = flat;
                                    row1[x * 4 + 2] = flat;
                                    row1[x * 4 + 3] = 255;

                                    row2[x * 4 + 0] = stripe;
                                    row2[x * 4 + 1] = stripe;
                                    row2[x * 4 + 2] = stripe;
                                    row2[x * 4 + 3] = 255;
                                }
                            }
                        }
                    }

                    using (var stacker = new GpuFocusStacker(context))
                    {
                        // Test A: GPU PooledGpuTexture pipeline
                        var tex1 = context.TexturePool.Acquire(width, height, DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM);
                        var tex2 = context.TexturePool.Acquire(width, height, DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM);
                        context.Transfer.Upload(slice1, tex1.Texture);
                        context.Transfer.Upload(slice2, tex2.Texture);

                        using (var composite = stacker.Stack(new[] { tex1, tex2 }))
                        {
                            Assert.Equal(width, composite.Width);
                            Assert.Equal(height, composite.Height);

                            using (var compBuf = new ImageBuffer(width, height, ImageFormatMode.Bgra32))
                            {
                                context.Transfer.Download(composite.Texture, compBuf);

                                unsafe
                                {
                                    // Verify Left side preserved high contrast (sharpness from Slice 1)
                                    byte* leftRow = compBuf.GetRowPointer(16);
                                    int leftPixel0 = leftRow[10 * 4];
                                    int leftPixel1 = leftRow[11 * 4];
                                    int leftDelta = Math.Abs(leftPixel0 - leftPixel1);
                                    Assert.True(leftDelta > 100, $"Expected left side sharp contrast, got delta={leftDelta}");

                                    // Verify Right side preserved high contrast (sharpness from Slice 2)
                                    byte* rightRow = compBuf.GetRowPointer(16);
                                    int rightPixel0 = rightRow[40 * 4];
                                    int rightPixel1 = rightRow[41 * 4];
                                    int rightDelta = Math.Abs(rightPixel0 - rightPixel1);
                                    Assert.True(rightDelta > 100, $"Expected right side sharp contrast, got delta={rightDelta}");
                                }
                            }
                        }

                        context.TexturePool.Release(tex1);
                        context.TexturePool.Release(tex2);

                        // Test B: CPU ImageBuffer overload
                        using (var cpuComposite = stacker.Stack(new[] { slice1, slice2 }))
                        {
                            Assert.Equal(width, cpuComposite.Width);
                            Assert.Equal(height, cpuComposite.Height);

                            unsafe
                            {
                                byte* row = cpuComposite.GetRowPointer(16);
                                int leftDelta = Math.Abs(row[10 * 4] - row[11 * 4]);
                                int rightDelta = Math.Abs(row[40 * 4] - row[41 * 4]);
                                Assert.True(leftDelta > 100, $"CPU Stack left delta failed: {leftDelta}");
                                Assert.True(rightDelta > 100, $"CPU Stack right delta failed: {rightDelta}");
                            }
                        }
                    }
                }
            }
        }

        [Fact]
        public void GpuHdrToneMapper_AppliesAcesAndReinhardToneMapping()
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
                                byte val = (byte)(x * 4);
                                row[x * 4 + 0] = val;
                                row[x * 4 + 1] = val;
                                row[x * 4 + 2] = val;
                                row[x * 4 + 3] = 255;
                            }
                        }
                    }

                    var inputTex = context.TexturePool.Acquire(width, height, DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM);
                    context.Transfer.Upload(srcBuf, inputTex.Texture);

                    using (var toneMapper = new GpuHdrToneMapper(context))
                    {
                        // 1. ACES Filmic Tone Mapping
                        using (var acesOut = toneMapper.ToneMap(inputTex, ToneMappingOperator.AcesFilmic, exposure: 1.5f, gamma: 2.2f))
                        {
                            Assert.Equal(width, acesOut.Width);
                            Assert.Equal(height, acesOut.Height);

                            using (var dstBuf = new ImageBuffer(width, height, ImageFormatMode.Bgra32))
                            {
                                context.Transfer.Download(acesOut.Texture, dstBuf);
                                unsafe
                                {
                                    byte* p = dstBuf.GetRowPointer(32) + 32 * 4;
                                    Assert.True(p[3] > 0); // Alpha preserved
                                }
                            }
                        }

                        // 2. Reinhard Tone Mapping
                        using (var reinhardOut = toneMapper.ToneMap(inputTex, ToneMappingOperator.Reinhard, exposure: 2.0f, gamma: 2.2f))
                        {
                            Assert.Equal(width, reinhardOut.Width);
                            Assert.Equal(height, reinhardOut.Height);
                        }

                        // 3. Linear Clamp Tone Mapping
                        using (var linearOut = toneMapper.ToneMap(inputTex, ToneMappingOperator.LinearClamp, exposure: 0.8f, gamma: 1.0f))
                        {
                            Assert.Equal(width, linearOut.Width);
                            Assert.Equal(height, linearOut.Height);
                        }
                    }

                    context.TexturePool.Release(inputTex);
                }
            }
        }
    }
}
