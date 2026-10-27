using System;
using System.Drawing;
using System.Runtime.InteropServices;
using Xunit;
using ZeroGraphics.DirectX.Core;
using ZeroGraphics.DirectX.Interception;
using ZeroGraphics.DirectX.Native;
using ZeroGraphics.DirectX.Rhi;
using ZeroGraphics.Imaging.Core;
using ZeroGraphics.Imaging.Filters;
using ZeroGraphics.Imaging.Gpu;
using ZeroGraphics.Rhi;
using ZeroGraphics.Rhi.Null;
using ZeroGraphics.Vision.Matching;

namespace ZeroGraphics.Tests
{
    public class PerformanceOptimizationsTests
    {
        [Fact]
        public unsafe void NccTemplateMatcher_ZeroLOH_MatchesPatternAccurately()
        {
            // Create 128x128 search image with a cross pattern
            using var search = ImageBuffer.CreateGray8(128, 128);
            for (int y = 0; y < 128; y++)
            {
                byte* p = search.GetRowPointer(y);
                for (int x = 0; x < 128; x++)
                {
                    p[x] = (byte)(50 + (x + y) % 30);
                }
            }

            // Draw a high contrast cross at (40, 50)
            int targetX = 40;
            int targetY = 50;
            for (int dx = 0; dx < 16; dx++)
            {
                search.GetRowPointer(targetY + 8)[targetX + dx] = 240;
                search.GetRowPointer(targetY + dx)[targetX + 8] = 240;
            }

            // Create 16x16 template
            using var template = ImageBuffer.CreateGray8(16, 16);
            for (int y = 0; y < 16; y++)
            {
                byte* p = template.GetRowPointer(y);
                for (int x = 0; x < 16; x++)
                {
                    p[x] = 50;
                }
            }
            for (int dx = 0; dx < 16; dx++)
            {
                template.GetRowPointer(8)[dx] = 240;
                template.GetRowPointer(dx)[8] = 240;
            }

            // Match using zero-LOH pooled matcher
            var result = NccTemplateMatcher.Match(search, template, minScore: 0.70, subPixelRefinement: true);

            Assert.True(result.IsFound);
            Assert.True(result.Score > 0.85);
            Assert.InRange(result.X, targetX - 0.5, targetX + 0.5);
            Assert.InRange(result.Y, targetY - 0.5, targetY + 0.5);
        }

        [Fact]
        public unsafe void Thresholding_SimdBranchless_MatchesScalarPixelByPixel()
        {
            const int width = 137; // Non-multiple of 16/32 to test vector + scalar tail
            const int height = 64;

            using var src = ImageBuffer.CreateGray8(width, height);
            using var dstSimd = ImageBuffer.CreateGray8(width, height);
            using var dstReference = ImageBuffer.CreateGray8(width, height);

            // Populate with gradient and noise
            var rnd = new Random(42);
            for (int y = 0; y < height; y++)
            {
                byte* p = src.GetRowPointer(y);
                for (int x = 0; x < width; x++)
                {
                    p[x] = (byte)rnd.Next(0, 256);
                }
            }

            byte threshold = 127;

            // Compute Reference Scalar
            for (int y = 0; y < height; y++)
            {
                byte* s = src.GetRowPointer(y);
                byte* d = dstReference.GetRowPointer(y);
                for (int x = 0; x < width; x++)
                {
                    d[x] = s[x] >= threshold ? (byte)255 : (byte)0;
                }
            }

            // Compute Optimized (SIMD + branchless)
            Thresholding.ApplyBinaryThreshold(src, dstSimd, threshold, invert: false);

            // Verify bit-exact equality across entire frame
            for (int y = 0; y < height; y++)
            {
                byte* expected = dstReference.GetRowPointer(y);
                byte* actual = dstSimd.GetRowPointer(y);
                for (int x = 0; x < width; x++)
                {
                    Assert.Equal(expected[x], actual[x]);
                }
            }
        }

        [Fact]
        public unsafe void AdaptiveThresholdBradley_ZeroLOH_ProducesBinaryOutput()
        {
            using var src = ImageBuffer.CreateGray8(64, 64);
            using var dst = ImageBuffer.CreateGray8(64, 64);

            for (int y = 0; y < 64; y++)
            {
                byte* p = src.GetRowPointer(y);
                for (int x = 0; x < 64; x++)
                {
                    p[x] = (byte)((x < 32) ? 80 : 200);
                }
            }

            // Draw a black mark in both dark and bright sides
            src.GetRowPointer(30)[15] = 10;
            src.GetRowPointer(30)[45] = 100;

            Thresholding.AdaptiveThresholdBradley(src, dst, windowRatio: 0.2f, percentage: 0.15f);

            // Verify both marks are binarized as foreground (0)
            Assert.Equal(0, dst.GetRowPointer(30)[15]);
            Assert.Equal(0, dst.GetRowPointer(30)[45]);

            // And background is white (255)
            Assert.Equal(255, dst.GetRowPointer(10)[10]);
            Assert.Equal(255, dst.GetRowPointer(10)[50]);
        }

        [Fact]
        public void RhiResourceBarrier_TracksStateTransitions()
        {
            using var device = new NullRhiDevice();
            var texDesc = new RhiTextureDesc(64, 64, RhiFormat.B8G8R8A8_UNorm, RhiTextureUsage.RenderTarget | RhiTextureUsage.ShaderResource);
            using var texture = device.CreateTexture(texDesc);

            using var cmd = device.CreateCommandBuffer();
            cmd.Begin();

            var barrier1 = new RhiBarrier(texture, RhiResourceState.RenderTarget, RhiResourceState.ShaderResource);
            cmd.ResourceBarrier(barrier1);

            var barrier2 = new RhiBarrier(texture, RhiResourceState.ShaderResource, RhiResourceState.CopyDest);
            cmd.ResourceBarriers(new[] { barrier2 });

            cmd.End();

            var nullCmd = (NullRhiCommandBuffer)cmd;
            Assert.Contains(nullCmd.RecordedCommands, s => s.Contains("Barrier") && s.Contains("RenderTarget -> ShaderResource"));
            Assert.Contains(nullCmd.RecordedCommands, s => s.Contains("Barrier") && s.Contains("ShaderResource -> CopyDest"));
        }

        [Fact]
        public unsafe void AsyncStagingRingBuffer_CanBeInstantiatedAndOperated()
        {
            using var rhiDevice = D3D11RhiDevice.GetOrCreateShared();
            var d3dDevice = (D3D11RhiDevice)rhiDevice;
            var device = d3dDevice.NativeDevice;
            var context = d3dDevice.NativeContext;

            using var transfer = new GpuTextureTransfer(device, context);
            using var ring = transfer.CreateAsyncStagingRingBuffer(64, 64, DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM, ringSize: 3);

            Assert.Equal(3, ring.Capacity);
            Assert.Equal(64, ring.Width);
            Assert.Equal(64, ring.Height);

            // Create a test texture
            var texDesc = new D3D11_TEXTURE2D_DESC
            {
                Width = 64,
                Height = 64,
                MipLevels = 1,
                ArraySize = 1,
                Format = DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM,
                SampleDesc = new DXGI_SAMPLE_DESC(1, 0),
                Usage = D3D11_USAGE.D3D11_USAGE_DEFAULT,
                BindFlags = D3D11_BIND_FLAG.D3D11_BIND_RENDER_TARGET | D3D11_BIND_FLAG.D3D11_BIND_SHADER_RESOURCE,
                CPUAccessFlags = 0,
                MiscFlags = 0
            };
            using var gpuTex = device.CreateTexture2D(ref texDesc);

            int ticket = ring.EnqueueCopy(gpuTex);
            Assert.True(ticket >= 0 && ticket < 3);

            using var readbackBuf = ImageBuffer.CreateBgra32(64, 64);
            bool success = ring.TryReadback(ticket, readbackBuf, waitForGpu: true);
            Assert.True(success);
        }

        [Fact]
        public unsafe void GaussianBlur_ZeroLOH_ProducesSmoothOutput()
        {
            using var src = ImageBuffer.CreateGray8(64, 64);
            using var dst = ImageBuffer.CreateGray8(64, 64);

            // Put a sharp impulse at center (32, 32)
            src.GetRowPointer(32)[32] = 255;

            // Apply Gaussian blur (pooled intermediate memory)
            ConvolutionFilters.GaussianBlur(src, dst, sigma: 2.0f);

            // Center should be diffused to neighbor pixels
            byte centerVal = dst.GetRowPointer(32)[32];
            byte neighborVal = dst.GetRowPointer(32)[33];

            Assert.True(centerVal > 0 && centerVal < 255);
            Assert.True(neighborVal > 0);
            Assert.True(centerVal >= neighborVal);
        }

        [Fact]
        public unsafe void ColorTransform_Invert_SimdMatchesScalar()
        {
            const int width = 137; // Non-multiple of 8/16/32
            const int height = 48;

            // 1. Test Gray8 Invert
            using var graySrc = ImageBuffer.CreateGray8(width, height);
            using var grayDst = ImageBuffer.CreateGray8(width, height);
            var rnd = new Random(123);
            for (int y = 0; y < height; y++)
            {
                byte* p = graySrc.GetRowPointer(y);
                for (int x = 0; x < width; x++) p[x] = (byte)rnd.Next(0, 256);
            }

            ColorTransform.Invert(graySrc, grayDst);

            for (int y = 0; y < height; y++)
            {
                byte* s = graySrc.GetRowPointer(y);
                byte* d = grayDst.GetRowPointer(y);
                for (int x = 0; x < width; x++)
                {
                    Assert.Equal((byte)(255 - s[x]), d[x]);
                }
            }

            // 2. Test Bgra32 Invert (SIMD vectorization with Alpha preservation)
            using var bgraSrc = ImageBuffer.CreateBgra32(width, height);
            using var bgraDst = ImageBuffer.CreateBgra32(width, height);
            for (int y = 0; y < height; y++)
            {
                byte* p = bgraSrc.GetRowPointer(y);
                for (int x = 0; x < width * 4; x++) p[x] = (byte)rnd.Next(0, 256);
            }

            ColorTransform.Invert(bgraSrc, bgraDst);

            for (int y = 0; y < height; y++)
            {
                byte* s = bgraSrc.GetRowPointer(y);
                byte* d = bgraDst.GetRowPointer(y);
                for (int x = 0; x < width; x++)
                {
                    int o = x * 4;
                    Assert.Equal((byte)(255 - s[o]), d[o]);         // B
                    Assert.Equal((byte)(255 - s[o + 1]), d[o + 1]); // G
                    Assert.Equal((byte)(255 - s[o + 2]), d[o + 2]); // R
                    Assert.Equal(s[o + 3], d[o + 3]);               // Alpha preserved
                }
            }
        }

        [Fact]
        public unsafe void ColorTransform_ToGrayscale_MatchesReferenceFormula()
        {
            const int width = 137;
            const int height = 48;

            using var src = ImageBuffer.CreateBgra32(width, height);
            using var dst = ImageBuffer.CreateGray8(width, height);

            var rnd = new Random(456);
            for (int y = 0; y < height; y++)
            {
                byte* p = src.GetRowPointer(y);
                for (int x = 0; x < width * 4; x++) p[x] = (byte)rnd.Next(0, 256);
            }

            ColorTransform.ToGrayscale(src, dst);

            for (int y = 0; y < height; y++)
            {
                byte* s = src.GetRowPointer(y);
                byte* d = dst.GetRowPointer(y);
                for (int x = 0; x < width; x++)
                {
                    int o = x * 4;
                    byte expected = (byte)((54 * s[o + 2] + 183 * s[o + 1] + 19 * s[o]) >> 8);
                    Assert.Equal(expected, d[x]);
                }
            }
        }

        [Fact]
        public void RhiFence_NullDevice_SignalsAndWaits()
        {
            using var device = new NullRhiDevice();
            using var fence = device.CreateFence(0);

            Assert.Equal(0UL, fence.CompletedValue);

            fence.Signal(42);
            Assert.Equal(42UL, fence.CompletedValue);

            bool waitOk = fence.Wait(42, timeoutMilliseconds: 50);
            Assert.True(waitOk);

            bool waitTimeout = fence.Wait(100, timeoutMilliseconds: 10);
            Assert.False(waitTimeout);
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int MathOpDelegate(int a, int b);

        private static int OriginalAdd(int a, int b) => a + b;
        private static int DetourAdd(int a, int b) => (a + b) * 10;

        [Fact]
        public unsafe void ComVTableHook_InterceptsAndUnhooksCorrectly()
        {
            MathOpDelegate origDel = OriginalAdd;
            IntPtr origFuncPtr = System.Runtime.InteropServices.Marshal.GetFunctionPointerForDelegate(origDel);

            IntPtr* vtable = (IntPtr*)System.Runtime.InteropServices.Marshal.AllocHGlobal(sizeof(IntPtr) * 4);
            vtable[0] = IntPtr.Zero;
            vtable[1] = origFuncPtr;

            IntPtr* comObj = (IntPtr*)System.Runtime.InteropServices.Marshal.AllocHGlobal(sizeof(IntPtr));
            *comObj = (IntPtr)vtable;

            try
            {
                using var hook = new ComVTableHook((IntPtr)comObj);

                MathOpDelegate detourDel = DetourAdd;
                IntPtr detourFuncPtr = System.Runtime.InteropServices.Marshal.GetFunctionPointerForDelegate(detourDel);

                // Before hook
                var fnBefore = (delegate* unmanaged[Stdcall]<int, int, int>)vtable[1];
                Assert.Equal(7, fnBefore(3, 4));

                // Hook slot 1
                IntPtr orig = hook.HookMethod(1, detourFuncPtr);
                Assert.Equal(origFuncPtr, orig);

                // After hook: Detour multiplies by 10
                var fnAfter = (delegate* unmanaged[Stdcall]<int, int, int>)vtable[1];
                Assert.Equal(70, fnAfter(3, 4));

                // Unhook slot 1
                bool unhooked = hook.UnhookMethod(1);
                Assert.True(unhooked);

                // Restored
                var fnRestored = (delegate* unmanaged[Stdcall]<int, int, int>)vtable[1];
                Assert.Equal(7, fnRestored(3, 4));
            }
            finally
            {
                System.Runtime.InteropServices.Marshal.FreeHGlobal((IntPtr)vtable);
                System.Runtime.InteropServices.Marshal.FreeHGlobal((IntPtr)comObj);
                GC.KeepAlive(origDel);
            }
        }

        [Fact]
        public void D3D11GraphicsInterceptor_ManagesShaderOverrides()
        {
            using var interceptor = new D3D11GraphicsInterceptor();

            IntPtr dummyShaderOrig = new IntPtr(0x1000);
            IntPtr dummyShaderRepl = new IntPtr(0x2000);

            interceptor.RegisterShaderOverride(dummyShaderOrig, dummyShaderRepl);
            bool removed = interceptor.RemoveShaderOverride(dummyShaderOrig);
            Assert.True(removed);
        }

        [Fact]
        public void D3D12RhiDevice_InitializesOrFailsGracefully_AndHandlesBarriers()
        {
            using var d3d12 = D3D12RhiDevice.TryCreate();
            if (d3d12 == null)
            {
                // Direct3D 12 runtime or compatible hardware not available in this environment
                return;
            }

            Assert.Equal(RhiBackend.Direct3D12, d3d12.Backend);
            Assert.Contains("Direct3D 12", d3d12.DeviceName);

            // Test Buffer Creation
            var bufDesc = new RhiBufferDesc(256, RhiBufferType.Vertex, RhiBufferUsage.Dynamic);
            using var buffer = d3d12.CreateBuffer(bufDesc);
            Assert.Equal(256, buffer.SizeInBytes);
            Assert.Equal(RhiBufferType.Vertex, buffer.Type);

            // Test Texture Creation
            var texDesc = new RhiTextureDesc(64, 64, RhiFormat.B8G8R8A8_UNorm, RhiTextureUsage.RenderTarget);
            using var texture = d3d12.CreateTexture(texDesc);
            Assert.Equal(64, texture.Width);
            Assert.Equal(64, texture.Height);

            // Test Fence
            using var fence = d3d12.CreateFence(0);
            Assert.Equal(0UL, fence.CompletedValue);
            fence.Signal(10);
            Assert.True(fence.Wait(10, timeoutMilliseconds: 50));

            // Test Command Buffer & Resource Barrier
            using var cmd = d3d12.CreateCommandBuffer();
            cmd.Begin();

            var barrier = new RhiBarrier(texture, RhiResourceState.RenderTarget, RhiResourceState.ShaderResource);
            cmd.ResourceBarrier(in barrier);

            var d3dTex = (D3D12RhiTexture)texture;
            Assert.True((d3dTex.CurrentState & D3D12_RESOURCE_STATES.PIXEL_SHADER_RESOURCE) != 0);

            cmd.Draw(3, 0);
            cmd.End();
        }

        [Fact]
        public unsafe void ColorTransform_ToGrayscale_SimdMatchesScalarOnArbitrarySizes()
        {
            // Test non-aligned dimension (143 x 57)
            const int width = 143;
            const int height = 57;

            using var src = ImageBuffer.CreateBgra32(width, height);
            using var dst = ImageBuffer.CreateGray8(width, height);

            var rnd = new Random(789);
            for (int y = 0; y < height; y++)
            {
                byte* p = src.GetRowPointer(y);
                for (int x = 0; x < width * 4; x++) p[x] = (byte)rnd.Next(0, 256);
            }

            ColorTransform.ToGrayscale(src, dst);

            for (int y = 0; y < height; y++)
            {
                byte* s = src.GetRowPointer(y);
                byte* d = dst.GetRowPointer(y);
                for (int x = 0; x < width; x++)
                {
                    int o = x * 4;
                    byte expected = (byte)((54 * s[o + 2] + 183 * s[o + 1] + 19 * s[o]) >> 8);
                    Assert.Equal(expected, d[x]);
                }
            }
        }
    }
}
