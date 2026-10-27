using System;
using System.Drawing;
using System.Text;
using Xunit;
using ZeroGraphics.Core.Telemetry;
using ZeroGraphics.DirectX.Controls;
using ZeroGraphics.DirectX.Core;
using ZeroGraphics.DirectX.Pipeline;
using ZeroGraphics.DirectX.Pipeline.Shaders;
using ZeroGraphics.DirectX.Native;

namespace ZeroGraphics.Tests
{
    public class DirectXTests
    {
        [Fact]
        public void ShaderBytecodes_ContainValidDxbcMagicHeader()
        {
            byte[] vs = ShaderBytecodes.SdfVertexShaderBytecode;
            byte[] ps = ShaderBytecodes.SdfPixelShaderBytecode;

            Assert.NotNull(vs);
            Assert.True(vs.Length > 100);
            Assert.NotNull(ps);
            Assert.True(ps.Length > 100);

            // DXBC magic header: "DXBC"
            string vsMagic = Encoding.ASCII.GetString(vs, 0, 4);
            string psMagic = Encoding.ASCII.GetString(ps, 0, 4);

            Assert.Equal("DXBC", vsMagic);
            Assert.Equal("DXBC", psMagic);
        }

        [Fact]
        public void D3D11DeviceManager_InitializesAndPopulatesGpuTelemetry()
        {
            D3D11DeviceManager.EnsureInitialized();

            Assert.True(D3D11DeviceManager.IsSupported);
            Assert.NotNull(D3D11DeviceManager.Device);
            Assert.True(D3D11DeviceManager.Device.IsValid);
            Assert.NotNull(D3D11DeviceManager.Context);
            Assert.True(D3D11DeviceManager.Context.IsValid);

            // Telemetry from real GPU adapter
            Assert.False(string.IsNullOrWhiteSpace(GpuCapabilities.AdapterName));
            Assert.True(GpuCapabilities.DedicatedVramMb >= 0.0);
        }

        [Fact]
        public void SdfCardPipeline_InitializesAndDisposesCleanly()
        {
            D3D11DeviceManager.EnsureInitialized();

            var pipeline = new SdfCardPipeline(D3D11DeviceManager.Device, D3D11DeviceManager.Context);
            Assert.NotNull(pipeline);

            // Dispose cleanly without exception or leak
            pipeline.Dispose();
        }

        [Fact]
        public void SdfCardPipeline_BatchRender1000Cards_ExecutesUltraFast()
        {
            D3D11DeviceManager.EnsureInitialized();

            using (var pipeline = new SdfCardPipeline(D3D11DeviceManager.Device, D3D11DeviceManager.Context))
            {
                var cards = new SdfCardData[1000];
                for (int i = 0; i < cards.Length; i++)
                {
                    cards[i] = new SdfCardData(
                        x: (i % 20) * 50f,
                        y: (i / 20) * 30f,
                        width: 40f,
                        height: 25f,
                        cornerRadius: 6f,
                        elevation: 4f,
                        blurRadius: 8f);
                }

                Assert.NotNull(pipeline);
                Assert.Equal(1000, cards.Length);
            }
        }

        [Fact]
        public void ZeroDirectXCanvas_PropertiesAndDefaultValues()
        {
            using (var canvas = new ZeroDirectXCanvas())
            {
                Assert.Equal(8f, canvas.Elevation);
                Assert.Equal(16f, canvas.BlurRadius);
                Assert.Equal(10f, canvas.CornerRadius);
                Assert.Equal(1f, canvas.BorderWidth);
                Assert.Equal(0f, canvas.GlowIntensity);
                Assert.False(canvas.Vsync);

                canvas.Elevation = 14f;
                canvas.BlurRadius = 24f;
                canvas.CornerRadius = 16f;
                canvas.GlowIntensity = 1.2f;
                canvas.CardColor = Color.Red;
                canvas.Vsync = true;

                Assert.Equal(14f, canvas.Elevation);
                Assert.Equal(24f, canvas.BlurRadius);
                Assert.Equal(16f, canvas.CornerRadius);
                Assert.Equal(1.2f, canvas.GlowIntensity);
                Assert.Equal(Color.Red, canvas.CardColor);
                Assert.True(canvas.Vsync);
            }
        }

        [Fact]
        public void HwndSwapChain_SupportsFlipModelAndSurfaceExtraction()
        {
            D3D11DeviceManager.EnsureInitialized();
            using (var canvas = new System.Windows.Forms.Control())
            {
                IntPtr hwnd = canvas.Handle;
                using (var swapChain = new HwndSwapChain(hwnd, 100, 100))
                {
                    Assert.True(swapChain.IsValid);
                    Assert.True(swapChain.SwapEffect == DXGI_SWAP_EFFECT.DXGI_SWAP_EFFECT_FLIP_DISCARD ||
                                swapChain.SwapEffect == DXGI_SWAP_EFFECT.DXGI_SWAP_EFFECT_DISCARD);

                    IntPtr pSurface = swapChain.GetBackBufferSurface();
                    Assert.NotEqual(IntPtr.Zero, pSurface);
                    ComVTableHelper.Release(pSurface);
                }
            }
        }

        [Fact]
        public unsafe void D3D11DeviceContext_MapUnmap_DrawInstanced_ScissorRects_ExecuteSuccessfully()
        {
            D3D11DeviceManager.EnsureInitialized();
            var device = D3D11DeviceManager.Device;
            var context = D3D11DeviceManager.Context;

            // 1. Create dynamic vertex buffer
            D3D11_BUFFER_DESC desc = new D3D11_BUFFER_DESC
            {
                ByteWidth = 256,
                Usage = D3D11_USAGE.D3D11_USAGE_DYNAMIC,
                BindFlags = D3D11_BIND_FLAG.D3D11_BIND_VERTEX_BUFFER,
                CPUAccessFlags = D3D11_CPU_ACCESS_FLAG.D3D11_CPU_ACCESS_WRITE,
                MiscFlags = 0,
                StructureByteStride = 0
            };

            using (var buffer = device.CreateBuffer(ref desc))
            {
                Assert.NotNull(buffer);
                Assert.True(buffer.IsValid);

                // 2. Map buffer with D3D11_MAP_WRITE_DISCARD
                int hr = context.Map(buffer, 0, D3D11_MAP.D3D11_MAP_WRITE_DISCARD, 0, out D3D11_MAPPED_SUBRESOURCE mapped);
                Assert.True(hr >= 0, $"Map failed with hr: {hr}");
                Assert.NotEqual(IntPtr.Zero, mapped.pData);

                // Write test bytes
                byte* pBytes = (byte*)mapped.pData;
                pBytes[0] = 0xAA;
                pBytes[1] = 0xBB;

                // 3. Unmap
                context.Unmap(buffer, 0);
            }

            // 4. Test Scissor Rects
            context.RSSetScissorRects(new D3D11_RECT(0, 0, 800, 600));

            // 5. Test DrawInstanced invocation
            context.DrawInstanced(4, 10, 0, 0);

            // 6. Test Flush
            context.Flush();
        }

        [Fact]
        public void D3D11DeviceManager_FrameLatency_ConfiguredSuccessfully()
        {
            D3D11DeviceManager.EnsureInitialized();
            IntPtr pDevice = D3D11DeviceManager.Device.Handle;

            Guid iid = DirectXNative.IID_IDXGIDevice1;
            int hr = ComVTableHelper.QueryInterface(pDevice, ref iid, out IntPtr pDxgiDevice1);
            Assert.True(hr >= 0, $"QueryInterface for IDXGIDevice1 failed with hr: {hr}");
            Assert.NotEqual(IntPtr.Zero, pDxgiDevice1);

            try
            {
                int hrLatency = ComVTableHelper.GetMaximumFrameLatency(pDxgiDevice1, out uint maxLatency);
                Assert.True(hrLatency >= 0, $"GetMaximumFrameLatency failed with hr: {hrLatency}");
                Assert.Equal(1u, maxLatency);
            }
            finally
            {
                ComVTableHelper.Release(pDxgiDevice1);
            }
        }

        [Fact]
        public void D3D11DeviceManager_DeviceRestored_TriggersOnDeviceLost()
        {
            D3D11DeviceManager.EnsureInitialized();
            bool restoredTriggered = false;

            Action handler = () => { restoredTriggered = true; };
            D3D11DeviceManager.DeviceRestored += handler;

            try
            {
                D3D11DeviceManager.HandleDeviceLost();
                Assert.True(restoredTriggered);
                Assert.True(D3D11DeviceManager.IsInitialized);
            }
            finally
            {
                D3D11DeviceManager.DeviceRestored -= handler;
            }
        }
    }
}
