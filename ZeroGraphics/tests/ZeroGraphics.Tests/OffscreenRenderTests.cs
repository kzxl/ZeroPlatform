using System;
using System.Drawing;
using Xunit;
using ZeroGraphics.Direct2D.Core;
using ZeroGraphics.DirectX.Core;

namespace ZeroGraphics.Tests
{
    public class OffscreenRenderTests
    {
        [Fact]
        public void D2DOffscreenTarget_RendersAndExportsBitmap_WithoutHwnd()
        {
            // Only run if DirectX 11 hardware/software is available
            if (!D3D11DeviceManager.IsSupported) return;

            int width = 200;
            int height = 150;

            using (var offscreen = new D2DOffscreenTarget(width, height))
            {
                Color bgColor = Color.FromArgb(255, 20, 24, 33);
                Color rectColor = Color.FromArgb(255, 0, 200, 115);

                offscreen.Render(rt =>
                {
                    rt.Clear(bgColor);

                    using (var brush = rt.CreateSolidColorBrush(rectColor))
                    {
                        rt.FillRectangle(20, 20, 100, 60, brush);
                    }
                });

                // 1. Verify ToBitmap
                using (Bitmap bmp = offscreen.ToBitmap())
                {
                    Assert.NotNull(bmp);
                    Assert.Equal(width, bmp.Width);
                    Assert.Equal(height, bmp.Height);

                    // Sample pixel inside the filled rectangle (e.g. x=50, y=50)
                    Color sampleInside = bmp.GetPixel(50, 50);
                    Assert.True(sampleInside.G > 150, $"Pixel inside rect should be green, got G={sampleInside.G}");

                    // Sample pixel outside (e.g. x=180, y=120)
                    Color sampleOutside = bmp.GetPixel(180, 120);
                    Assert.True(sampleOutside.R < 50 && sampleOutside.G < 50, $"Pixel outside rect should be dark background, got R={sampleOutside.R}, G={sampleOutside.G}");
                }

                // 2. Verify ToBgraBytes
                byte[] bgra = offscreen.ToBgraBytes();
                Assert.NotNull(bgra);
                Assert.Equal(width * height * 4, bgra.Length);
            }
        }
    }
}
