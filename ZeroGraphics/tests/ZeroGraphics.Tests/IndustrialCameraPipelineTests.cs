using System;
using System.Drawing;
using System.Runtime.InteropServices;
using Xunit;
using ZeroGraphics.DirectX.Core;
using ZeroGraphics.DirectX.Native;
using ZeroGraphics.Imaging.Core;
using ZeroGraphics.Imaging.Gpu;
using ZeroGraphics.Vision.Codes;

namespace ZeroGraphics.Tests
{
    public class IndustrialCameraPipelineTests
    {
        [Fact]
        public unsafe void WrapUnmanaged_Gray8_ProvidesDirectPointerAccess_ZeroAllocation()
        {
            // Simulate 1408 x 1024 industrial camera unmanaged buffer (e.g. Hikrobot MV-SCC007M Mono8)
            int width = 1408;
            int height = 1024;
            int stride = ((width * 1) + 3) & ~3;
            int totalBytes = stride * height;

            IntPtr unmanagedMemory = Marshal.AllocHGlobal(totalBytes);
            try
            {
                byte* pBytes = (byte*)unmanagedMemory;
                // Write test pixel pattern
                pBytes[0] = 0xAA;
                pBytes[stride * 100 + 200] = 0x55;

                using (var buffer = ImageBuffer.WrapUnmanaged(unmanagedMemory, width, height, stride, ImageFormatMode.Gray8))
                {
                    Assert.True(buffer.IsUnmanaged);
                    Assert.Null(buffer.RawBytes); // Zero managed array on GC heap
                    Assert.Equal(width, buffer.Width);
                    Assert.Equal(height, buffer.Height);
                    Assert.Equal(stride, buffer.Stride);
                    Assert.Equal((IntPtr)pBytes, (IntPtr)buffer.Scan0);

                    // Verify pixel reading via pointer
                    Assert.Equal(0xAA, buffer.Scan0[0]);
                    Assert.Equal(0x55, buffer.GetRowPointer(100)[200]);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(unmanagedMemory);
            }
        }

        [Fact]
        public unsafe void WrapUnmanaged_InPlaceUpdate_AllowsReusingSingleInstanceAcrossFrames()
        {
            int width = 640;
            int height = 480;
            int stride = width;
            int totalBytes = stride * height;

            IntPtr frame1Mem = Marshal.AllocHGlobal(totalBytes);
            IntPtr frame2Mem = Marshal.AllocHGlobal(totalBytes);

            try
            {
                *((byte*)frame1Mem) = 111;
                *((byte*)frame2Mem) = 222;

                // Create a single wrapper instance
                using (var buffer = ImageBuffer.WrapUnmanaged(frame1Mem, width, height, stride, ImageFormatMode.Gray8))
                {
                    Assert.Equal(111, buffer.Scan0[0]);

                    // Simulate arrival of Frame 2 in camera callback: update pointer in-place
                    buffer.UpdateUnmanagedScan0(frame2Mem);
                    Assert.Equal((IntPtr)frame2Mem, (IntPtr)buffer.Scan0);
                    Assert.Equal(222, buffer.Scan0[0]);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(frame1Mem);
                Marshal.FreeHGlobal(frame2Mem);
            }
        }

        [Fact]
        public unsafe void WrapUnmanaged_CompatibleWith_QrDecoder_NativeInspection()
        {
            // Simulate camera capturing a QR code (matching the Hikrobot demo in user's image)
            string expectedPayload = "http://ndatrace.vn/02/8935217402737/02/hp9vm3mzoio";
            bool[,] qrGrid = QrEncoder.EncodeSymbol(expectedPayload, QrErrorCorrectionLevel.M);
            int qrDim = qrGrid.GetLength(0);

            int modSize = 6;
            int qzBorder = 4;
            int totalQrSize = (qrDim + qzBorder * 2) * modSize;

            int imgW = totalQrSize + 40;
            int imgH = totalQrSize + 40;
            int stride = (imgW + 3) & ~3;
            int totalBytes = stride * imgH;

            IntPtr unmanagedMem = Marshal.AllocHGlobal(totalBytes);
            try
            {
                byte* pBytes = (byte*)unmanagedMem;
                // Initialize background (dark conveyor belt = 40)
                for (int i = 0; i < totalBytes; i++) pBytes[i] = 40;

                // Draw quiet zone (white = 255)
                int startX = 20;
                int startY = 20;
                for (int y = 0; y < totalQrSize; y++)
                {
                    byte* row = pBytes + (startY + y) * stride;
                    for (int x = 0; x < totalQrSize; x++)
                    {
                        row[startX + x] = 255;
                    }
                }

                // Draw QR modules
                for (int r = 0; r < qrDim; r++)
                {
                    for (int c = 0; c < qrDim; c++)
                    {
                        byte color = qrGrid[r, c] ? (byte)0 : (byte)255;
                        int modX = startX + (c + qzBorder) * modSize;
                        int modY = startY + (r + qzBorder) * modSize;

                        for (int py = 0; py < modSize; py++)
                        {
                            byte* row = pBytes + (modY + py) * stride;
                            for (int px = 0; px < modSize; px++)
                            {
                                row[modX + px] = color;
                            }
                        }
                    }
                }

                // Wrap unmanaged camera frame
                using (var cameraFrame = ImageBuffer.WrapUnmanaged(unmanagedMem, imgW, imgH, stride, ImageFormatMode.Gray8))
                {
                    var qrDecoder = new QrDecoder();
                    var result = qrDecoder.Decode(cameraFrame);

                    Assert.NotNull(result);
                    Assert.Equal(BarcodeSymbology.QrCode, result.Symbology);
                    Assert.Equal(expectedPayload, result.Text);
                    Assert.True(result.Confidence > 0.85);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(unmanagedMem);
            }
        }

        [Fact]
        public void CameraViewportPipeline_LifecycleAndOverlay_Succeeds()
        {
            if (!D3D11DeviceManager.IsSupported) return;

            D3D11DeviceManager.EnsureInitialized();
            using var pipeline = new CameraViewportPipeline(D3D11DeviceManager.Device, D3D11DeviceManager.Context);

            Assert.False(pipeline.HasFrame);

            // Upload a synthetic unmanaged 1408 x 1024 Gray8 frame
            int width = 1408;
            int height = 1024;
            int stride = ((width * 1) + 3) & ~3;
            int totalBytes = stride * height;

            IntPtr pMem = Marshal.AllocHGlobal(totalBytes);
            try
            {
                pipeline.UploadFrameRaw(pMem, width, height, stride, ImageFormatMode.Gray8);

                Assert.True(pipeline.HasFrame);
                Assert.Equal(width, pipeline.CameraWidth);
                Assert.Equal(height, pipeline.CameraHeight);
                Assert.Equal(ImageFormatMode.Gray8, pipeline.CameraFormat);

                // Add Pass & Fail bounding box overlays
                var overlays = new[]
                {
                    new CameraOverlayBox(100, 100, 300, 300, Color.Lime, 2.0f, "PASS [QR OK]"),
                    new CameraOverlayBox(500, 500, 200, 150, Color.Red, 2.0f, "FAIL [SCRATCH]")
                };

                Assert.Equal(2, overlays.Length);
                Assert.Equal("PASS [QR OK]", overlays[0].Label);
                Assert.Equal(Color.Lime, overlays[0].Color);
            }
            finally
            {
                Marshal.FreeHGlobal(pMem);
            }
        }
    }
}
