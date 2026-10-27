using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using ZeroGraphics.Direct2D.Native;
using ZeroGraphics.DirectX.Core;
using ZeroGraphics.DirectX.Native;

namespace ZeroGraphics.Direct2D.Core
{
    /// <summary>
    /// Headless Direct2D Offscreen Render Target.
    /// Enables ultra-fast hardware-accelerated 2D vector rendering, diagram generation, and reporting
    /// in background services, WebAPI endpoints, or console daemons without requiring a Windows Form (HWND).
    /// </summary>
    public sealed class D2DOffscreenTarget : IDisposable
    {
        private readonly int _width;
        private readonly int _height;
        private readonly float _dpiX;
        private readonly float _dpiY;

        private D3D11Texture2D? _renderTexture;
        private D3D11Texture2D? _stagingTexture;
        private D2DDxgiRenderTarget? _renderTarget;
        private bool _disposed;

        public int Width => _width;
        public int Height => _height;
        public float DpiX => _dpiX;
        public float DpiY => _dpiY;
        public D2DRenderTarget RenderTarget => _renderTarget ?? throw new ObjectDisposedException(nameof(D2DOffscreenTarget));

        public D2DOffscreenTarget(int width, int height, float dpiX = 96.0f, float dpiY = 96.0f)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width), "Width must be positive.");
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height), "Height must be positive.");

            _width = width;
            _height = height;
            _dpiX = dpiX;
            _dpiY = dpiY;

            InitializeResources();
        }

        private void InitializeResources()
        {
            var device = D3D11DeviceManager.Device;

            // 1. Create Render Target Texture2D
            D3D11_TEXTURE2D_DESC renderDesc = new D3D11_TEXTURE2D_DESC
            {
                Width = (uint)_width,
                Height = (uint)_height,
                MipLevels = 1,
                ArraySize = 1,
                Format = DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM,
                SampleDesc = new DXGI_SAMPLE_DESC(1, 0),
                Usage = D3D11_USAGE.D3D11_USAGE_DEFAULT,
                BindFlags = D3D11_BIND_FLAG.D3D11_BIND_RENDER_TARGET,
                CPUAccessFlags = 0,
                MiscFlags = 0
            };
            _renderTexture = device.CreateTexture2D(ref renderDesc);

            // 2. Create Staging Texture2D for CPU Readback
            D3D11_TEXTURE2D_DESC stagingDesc = new D3D11_TEXTURE2D_DESC
            {
                Width = (uint)_width,
                Height = (uint)_height,
                MipLevels = 1,
                ArraySize = 1,
                Format = DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM,
                SampleDesc = new DXGI_SAMPLE_DESC(1, 0),
                Usage = D3D11_USAGE.D3D11_USAGE_STAGING,
                BindFlags = 0,
                CPUAccessFlags = D3D11_CPU_ACCESS_FLAG.D3D11_CPU_ACCESS_READ,
                MiscFlags = 0
            };
            _stagingTexture = device.CreateTexture2D(ref stagingDesc);

            // 3. Query IDXGISurface from Render Texture
            Guid iidSurface = DirectXNative.IID_IDXGISurface;
            int hr = ComVTableHelper.QueryInterface(_renderTexture.Handle, ref iidSurface, out IntPtr pSurface);
            if (hr < 0 || pSurface == IntPtr.Zero)
                throw new COMException("Failed to query IDXGISurface from D3D11Texture2D.", hr);

            try
            {
                // 4. Create Direct2D DXGI Render Target
                _renderTarget = D2DFactory.Default.CreateDxgiSurfaceRenderTarget(pSurface, _dpiX, _dpiY);
            }
            finally
            {
                // D2D internally AddRefs the surface, so release our local query reference
                ComVTableHelper.Release(pSurface);
            }
        }

        /// <summary>
        /// Executes a rendering delegate within BeginDraw / EndDraw brackets.
        /// </summary>
        public void Render(Action<D2DRenderTarget> renderAction)
        {
            if (_disposed || _renderTarget == null) throw new ObjectDisposedException(nameof(D2DOffscreenTarget));
            if (renderAction == null) throw new ArgumentNullException(nameof(renderAction));

            _renderTarget.BeginDraw();
            try
            {
                renderAction(_renderTarget);
            }
            finally
            {
                _renderTarget.EndDraw();
            }
        }

        /// <summary>
        /// Reads back rendered pixels from the GPU into a System.Drawing.Bitmap.
        /// </summary>
        public unsafe Bitmap ToBitmap()
        {
            if (_disposed || _renderTexture == null || _stagingTexture == null)
                throw new ObjectDisposedException(nameof(D2DOffscreenTarget));

            var context = D3D11DeviceManager.Context;

            // Copy render target to staging texture
            context.CopyResource(_stagingTexture, _renderTexture);

            // Map staging texture for CPU read
            int hr = ComVTableHelper.Map(
                context.Handle,
                _stagingTexture.Handle,
                0,
                D3D11_MAP.D3D11_MAP_READ,
                0,
                out D3D11_MAPPED_SUBRESOURCE mapped);

            if (hr < 0 || mapped.pData == IntPtr.Zero)
                throw new COMException("Failed to map staging texture for offscreen readback.", hr);

            try
            {
                Bitmap bmp = new Bitmap(_width, _height, PixelFormat.Format32bppPArgb);
                BitmapData bmpData = bmp.LockBits(
                    new Rectangle(0, 0, _width, _height),
                    ImageLockMode.WriteOnly,
                    PixelFormat.Format32bppPArgb);

                try
                {
                    byte* src = (byte*)mapped.pData;
                    byte* dst = (byte*)bmpData.Scan0;
                    int rowBytes = _width * 4;

                    for (int y = 0; y < _height; y++)
                    {
                        byte* srcRow = src + y * mapped.RowPitch;
                        byte* dstRow = dst + y * bmpData.Stride;
                        Buffer.MemoryCopy(srcRow, dstRow, rowBytes, rowBytes);
                    }
                }
                finally
                {
                    bmp.UnlockBits(bmpData);
                }

                return bmp;
            }
            finally
            {
                ComVTableHelper.Unmap(context.Handle, _stagingTexture.Handle, 0);
            }
        }

        /// <summary>
        /// Reads back pixels and returns the raw BGRA32 byte buffer.
        /// </summary>
        public unsafe byte[] ToBgraBytes()
        {
            if (_disposed || _renderTexture == null || _stagingTexture == null)
                throw new ObjectDisposedException(nameof(D2DOffscreenTarget));

            var context = D3D11DeviceManager.Context;
            context.CopyResource(_stagingTexture, _renderTexture);

            int hr = ComVTableHelper.Map(
                context.Handle,
                _stagingTexture.Handle,
                0,
                D3D11_MAP.D3D11_MAP_READ,
                0,
                out D3D11_MAPPED_SUBRESOURCE mapped);

            if (hr < 0 || mapped.pData == IntPtr.Zero)
                throw new COMException("Failed to map staging texture.", hr);

            try
            {
                byte[] bytes = new byte[_width * _height * 4];
                byte* src = (byte*)mapped.pData;
                int rowBytes = _width * 4;

                fixed (byte* dst = bytes)
                {
                    for (int y = 0; y < _height; y++)
                    {
                        byte* srcRow = src + y * mapped.RowPitch;
                        byte* dstRow = dst + y * rowBytes;
                        Buffer.MemoryCopy(srcRow, dstRow, rowBytes, rowBytes);
                    }
                }

                return bytes;
            }
            finally
            {
                ComVTableHelper.Unmap(context.Handle, _stagingTexture.Handle, 0);
            }
        }

        /// <summary>
        /// Saves rendered graphic to a PNG file on disk.
        /// </summary>
        public void SaveToPng(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) throw new ArgumentNullException(nameof(filePath));
            using (var bmp = ToBitmap())
            {
                bmp.Save(filePath, ImageFormat.Png);
            }
        }

        /// <summary>
        /// Saves rendered graphic to an output stream as PNG.
        /// </summary>
        public void SaveToPng(Stream stream)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            using (var bmp = ToBitmap())
            {
                bmp.Save(stream, ImageFormat.Png);
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _renderTarget?.Dispose();
                _renderTarget = null;

                _stagingTexture?.Dispose();
                _stagingTexture = null;

                _renderTexture?.Dispose();
                _renderTexture = null;

                _disposed = true;
            }
            GC.SuppressFinalize(this);
        }

        ~D2DOffscreenTarget()
        {
            Dispose();
        }
    }
}
