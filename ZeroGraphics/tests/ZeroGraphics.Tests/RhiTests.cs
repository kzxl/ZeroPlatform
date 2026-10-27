using System;
using Xunit;
using ZeroGraphics.DirectX.Rhi;
using ZeroGraphics.Rhi;
using ZeroGraphics.Rhi.Null;

namespace ZeroGraphics.Tests
{
    public class RhiTests
    {
        [Fact]
        public void NullRhiDevice_InitializesCorrectly()
        {
            using (var device = new NullRhiDevice())
            {
                Assert.Equal(RhiBackend.Null, device.Backend);
                Assert.False(device.IsDisposed);
                Assert.Contains("Null", device.DeviceName);
            }
        }

        [Fact]
        public void NullRhiDevice_BufferCreationAndUpdates_WorkAccurately()
        {
            using (var device = new NullRhiDevice())
            {
                var desc = new RhiBufferDesc(128, RhiBufferType.Vertex, RhiBufferUsage.Dynamic);
                using (var buffer = device.CreateBuffer(desc))
                {
                    Assert.Equal(128, buffer.SizeInBytes);
                    Assert.Equal(RhiBufferType.Vertex, buffer.Type);
                    Assert.Equal(RhiBufferUsage.Dynamic, buffer.Usage);

                    float[] vertices = new[] { 1.0f, 2.0f, 3.0f, 4.0f };
                    buffer.UpdateData(vertices.AsSpan(), 0);

                    // Validate underlying storage
                    var nullBuf = (NullRhiBuffer)buffer;
                    float readBack = BitConverter.ToSingle(nullBuf.Storage, 0);
                    Assert.Equal(1.0f, readBack);
                }
            }
        }

        [Fact]
        public void NullRhiDevice_TextureCreationAndAllocation_Succeeds()
        {
            using (var device = new NullRhiDevice())
            {
                var desc = new RhiTextureDesc(64, 64, RhiFormat.R8G8B8A8_UNorm, RhiTextureUsage.ShaderResource | RhiTextureUsage.RenderTarget);
                using (var texture = device.CreateTexture(desc))
                {
                    Assert.Equal(64, texture.Width);
                    Assert.Equal(64, texture.Height);
                    Assert.Equal(RhiFormat.R8G8B8A8_UNorm, texture.Format);
                    Assert.True(texture.Usage.HasFlag(RhiTextureUsage.RenderTarget));

                    var nullTex = (NullRhiTexture)texture;
                    Assert.Equal(64 * 64 * 4, nullTex.PixelData.Length);
                }
            }
        }

        [Fact]
        public void NullRhiDevice_CommandBuffer_RecordsPassLifecycle()
        {
            using (var device = new NullRhiDevice())
            using (var cmd = device.CreateCommandBuffer())
            {
                var texDesc = new RhiTextureDesc(100, 100, RhiFormat.B8G8R8A8_UNorm, RhiTextureUsage.RenderTarget);
                using var target = device.CreateTexture(texDesc);

                var bufDesc = new RhiBufferDesc(64, RhiBufferType.Vertex);
                using var vBuf = device.CreateBuffer(bufDesc);

                cmd.Begin();
                cmd.SetViewport(new RhiViewport(0, 0, 100, 100));
                cmd.SetScissorRect(new RhiRect(0, 0, 100, 100));
                cmd.BeginRenderPass(target, RhiClearFlags.Color, RhiColor.Emerald);
                cmd.SetVertexBuffer(0, vBuf, 16);
                cmd.Draw(6, 0);
                cmd.EndRenderPass();
                cmd.End();

                var nullCmd = (NullRhiCommandBuffer)cmd;
                Assert.Equal(1, nullCmd.TotalDrawCalls);
                Assert.Contains("Begin", nullCmd.RecordedCommands);
                Assert.Contains("Draw(6, 0)", nullCmd.RecordedCommands);
                Assert.Contains("End", nullCmd.RecordedCommands);
            }
        }

        [Fact]
        public void NullRhiDevice_SwapChain_ResizesSeamlessly()
        {
            using (var device = new NullRhiDevice())
            using (var swapChain = device.CreateSwapChain(IntPtr.Zero, 800, 600))
            {
                Assert.Equal(800, swapChain.Width);
                Assert.Equal(600, swapChain.Height);
                Assert.NotNull(swapChain.BackBuffer);

                swapChain.Resize(1024, 768);
                Assert.Equal(1024, swapChain.Width);
                Assert.Equal(768, swapChain.Height);
                Assert.NotNull(swapChain.BackBuffer);

                swapChain.Present(1);
            }
        }

        [Fact]
        public void D3D11RhiDevice_SharedInitialization_CreatesHardwareDevice()
        {
            using (var rhiDevice = D3D11RhiDevice.GetOrCreateShared())
            {
                Assert.Equal(RhiBackend.Direct3D11, rhiDevice.Backend);
                Assert.Contains("Direct3D 11", rhiDevice.DeviceName);

                var d3dDevice = (D3D11RhiDevice)rhiDevice;
                Assert.NotNull(d3dDevice.NativeDevice);
                Assert.True(d3dDevice.NativeDevice.IsValid);

                // Create a hardware dynamic buffer
                var bufDesc = new RhiBufferDesc(64, RhiBufferType.Vertex, RhiBufferUsage.Dynamic);
                using var buf = rhiDevice.CreateBuffer(bufDesc);
                Assert.Equal(64, buf.SizeInBytes);

                // Update data via map discard
                float[] data = new[] { 1f, 2f, 3f, 4f };
                buf.UpdateData(data.AsSpan(), 0);

                // Create a hardware 2D texture
                var texDesc = new RhiTextureDesc(32, 32, RhiFormat.B8G8R8A8_UNorm, RhiTextureUsage.ShaderResource | RhiTextureUsage.RenderTarget);
                using var tex = rhiDevice.CreateTexture(texDesc);
                Assert.Equal(32, tex.Width);
                Assert.Equal(32, tex.Height);

                // Create command buffer and execute pass
                using var cmd = rhiDevice.CreateCommandBuffer();
                cmd.Begin();
                cmd.SetViewport(new RhiViewport(0, 0, 32, 32));
                cmd.BeginRenderPass(tex, RhiClearFlags.Color, RhiColor.DarkSlate);
                cmd.EndRenderPass();
                cmd.End();
            }
        }
    }
}
