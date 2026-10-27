using System;
using Xunit;
using ZeroGraphics.Direct2D.Controls;
using ZeroGraphics.Direct2D.Core;
using ZeroGraphics.Direct2D.Native;

namespace ZeroGraphics.Tests
{
    public class Direct2DTests
    {
        [Fact]
        public void D2DFactory_InitializesSingletonSuccessfully()
        {
            var factory = D2DFactory.Default;
            Assert.NotNull(factory);
            Assert.True(factory.IsValid);
        }

        [Fact]
        public void DWriteFactory_CreatesTextFormatSuccessfully()
        {
            var factory = DWriteFactory.Default;
            Assert.NotNull(factory);
            Assert.True(factory.IsValid);

            using (var format = factory.CreateTextFormat("Segoe UI", 16f, DWRITE_FONT_WEIGHT.DWRITE_FONT_WEIGHT_BOLD))
            {
                Assert.NotNull(format);
                Assert.True(format.IsValid);
                Assert.Equal("Segoe UI", format.FontFamilyName);
                Assert.Equal(16f, format.FontSize);
            }
        }

        [Fact]
        public void ZeroDirect2DCanvas_DefaultPropertiesAndConfiguration()
        {
            using (var canvas = new ZeroDirect2DCanvas())
            {
                Assert.Equal("Segoe UI", canvas.TextFontFamily);
                Assert.Equal(14f, canvas.TextSize);
                Assert.Contains("DirectWrite", canvas.SampleText);

                canvas.TextFontFamily = "Consolas";
                canvas.TextSize = 20f;
                canvas.SampleText = "High-DPI Benchmark";

                Assert.Equal("Consolas", canvas.TextFontFamily);
                Assert.Equal(20f, canvas.TextSize);
                Assert.Equal("High-DPI Benchmark", canvas.SampleText);
            }
        }

        [Fact]
        public void D2DFactory_CreateDxgiSurfaceRenderTarget_CreatesAndDrawsOnSurface()
        {
            ZeroGraphics.DirectX.Core.D3D11DeviceManager.EnsureInitialized();
            using (var ctrl = new System.Windows.Forms.Control())
            {
                IntPtr hwnd = ctrl.Handle;
                using (var swapChain = new ZeroGraphics.DirectX.Pipeline.HwndSwapChain(hwnd, 120, 120))
                {
                    IntPtr pSurface = swapChain.GetBackBufferSurface();
                    try
                    {
                        using (var dxgiRT = D2DFactory.Default.CreateDxgiSurfaceRenderTarget(pSurface))
                        {
                            Assert.NotNull(dxgiRT);
                            Assert.True(dxgiRT.IsValid);

                            dxgiRT.BeginDraw();
                            dxgiRT.Clear(System.Drawing.Color.MidnightBlue);
                            int hr = dxgiRT.EndDraw();
                            Assert.True(hr >= 0, $"Direct2D EndDraw on DXGI Surface failed with hr: {hr}");
                        }
                    }
                    finally
                    {
                        ZeroGraphics.DirectX.Native.ComVTableHelper.Release(pSurface);
                    }
                }
            }
        }

        [Fact]
        public void D2DRenderTarget_SetDpi_And_GetDpi_Successfully()
        {
            ZeroGraphics.DirectX.Core.D3D11DeviceManager.EnsureInitialized();
            using (var ctrl = new System.Windows.Forms.Control())
            {
                IntPtr hwnd = ctrl.Handle;
                using (var swapChain = new ZeroGraphics.DirectX.Pipeline.HwndSwapChain(hwnd, 100, 100))
                {
                    IntPtr pSurface = swapChain.GetBackBufferSurface();
                    try
                    {
                        using (var dxgiRT = D2DFactory.Default.CreateDxgiSurfaceRenderTarget(pSurface))
                        {
                            Assert.NotNull(dxgiRT);

                            // Test dynamic DPI update (e.g. 150% scaling = 144 DPI, 200% scaling = 192 DPI)
                            dxgiRT.SetDpi(144.0f, 144.0f);
                            dxgiRT.GetDpi(out float dpiX, out float dpiY);
                            Assert.Equal(144.0f, dpiX);
                            Assert.Equal(144.0f, dpiY);

                            dxgiRT.SetDpi(192.0f, 192.0f);
                            dxgiRT.GetDpi(out dpiX, out dpiY);
                            Assert.Equal(192.0f, dpiX);
                            Assert.Equal(192.0f, dpiY);
                        }
                    }
                    finally
                    {
                        ZeroGraphics.DirectX.Native.ComVTableHelper.Release(pSurface);
                    }
                }
            }
        }
    }
}
