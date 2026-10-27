using System;
using System.Runtime.InteropServices;
using ZeroGraphics.DirectX.Core;
using ZeroGraphics.DirectX.Native;

namespace ZeroGraphics.DirectX.Pipeline
{
    /// <summary>
    /// Encapsulates a DXGI SwapChain bound to a native WinForms HWND.
    /// Manages backbuffer life cycle, zero-flicker presentation, and seamless buffer resizing.
    /// </summary>
    public sealed class HwndSwapChain : IDisposable
    {
        private readonly IntPtr _hWnd;
        private DxgiSwapChain? _swapChain;
        private D3D11RenderTargetView? _renderTargetView;
        private DXGI_SWAP_EFFECT _swapEffect;
        private int _width;
        private int _height;
        private bool _disposed;

        public IntPtr HWnd => _hWnd;
        public int Width => _width;
        public int Height => _height;
        public DXGI_SWAP_EFFECT SwapEffect => _swapEffect;
        public D3D11RenderTargetView? RenderTargetView => _renderTargetView;
        public bool IsValid => _swapChain != null && _swapChain.IsValid && _renderTargetView != null && _renderTargetView.IsValid;

        public HwndSwapChain(IntPtr hWnd, int initialWidth, int initialHeight)
        {
            if (hWnd == IntPtr.Zero)
                throw new ArgumentException("Invalid window handle.", nameof(hWnd));

            _hWnd = hWnd;
            _width = Math.Max(1, initialWidth);
            _height = Math.Max(1, initialHeight);

            RecreateSwapChain();
        }

        private void RecreateSwapChain()
        {
            DisposeBuffers();
            _swapChain?.Dispose();
            _swapChain = null;

            D3D11DeviceManager.EnsureInitialized();

            DXGI_SWAP_CHAIN_DESC desc = new DXGI_SWAP_CHAIN_DESC
            {
                BufferDesc = new DXGI_MODE_DESC
                {
                    Width = (uint)_width,
                    Height = (uint)_height,
                    Format = DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM,
                    RefreshRate = new DXGI_RATIONAL(60, 1),
                    Scaling = 0,
                    ScanlineOrdering = 0
                },
                SampleDesc = new DXGI_SAMPLE_DESC(1, 0),
                BufferUsage = DXGI_USAGE.DXGI_USAGE_RENDER_TARGET_OUTPUT,
                BufferCount = 2,
                OutputWindow = _hWnd,
                Windowed = true,
                SwapEffect = DXGI_SWAP_EFFECT.DXGI_SWAP_EFFECT_FLIP_DISCARD,
                Flags = (uint)DXGI_SWAP_CHAIN_FLAG.DXGI_SWAP_CHAIN_FLAG_ALLOW_MODE_SWITCH
            };

            int hr = ComVTableHelper.CreateSwapChain(
                D3D11DeviceManager.FactoryHandle,
                D3D11DeviceManager.Device.Handle,
                ref desc,
                out IntPtr pSwapChain);

            if (hr < 0 || pSwapChain == IntPtr.Zero)
            {
                // Fallback to legacy Blt model (DXGI_SWAP_EFFECT_DISCARD) if FLIP_DISCARD is not supported on older OS/driver
                desc.SwapEffect = DXGI_SWAP_EFFECT.DXGI_SWAP_EFFECT_DISCARD;
                hr = ComVTableHelper.CreateSwapChain(
                    D3D11DeviceManager.FactoryHandle,
                    D3D11DeviceManager.Device.Handle,
                    ref desc,
                    out pSwapChain);

                if (hr < 0 || pSwapChain == IntPtr.Zero)
                {
                    throw new COMException("Failed to create DXGI SwapChain for HWND.", hr);
                }
            }

            _swapEffect = desc.SwapEffect;
            _swapChain = new DxgiSwapChain(pSwapChain);
            CreateRenderTargetView();
        }

        public DxgiSwapChain? SwapChain => _swapChain;

        /// <summary>
        /// Retrieves the underlying IDXGISurface of the backbuffer (e.g. for Direct2D interop).
        /// The caller must call ComVTableHelper.Release or wrap in an unmanaged wrapper.
        /// </summary>
        public IntPtr GetBackBufferSurface()
        {
            if (_swapChain == null || !_swapChain.IsValid)
                throw new InvalidOperationException("SwapChain is not initialized.");

            Guid surfaceIid = DirectXNative.IID_IDXGISurface;
            return _swapChain.GetBuffer(0, ref surfaceIid);
        }

        /// <summary>
        /// Retrieves the underlying ID3D11Texture2D of the backbuffer for zero-copy presentation.
        /// The caller must call ComVTableHelper.Release or wrap in an unmanaged wrapper.
        /// </summary>
        public IntPtr GetBackBufferTexture()
        {
            if (_swapChain == null || !_swapChain.IsValid)
                throw new InvalidOperationException("SwapChain is not initialized.");

            Guid texture2DIid = DirectXNative.IID_ID3D11Texture2D;
            return _swapChain.GetBuffer(0, ref texture2DIid);
        }


        private void CreateRenderTargetView()
        {
            if (_swapChain == null || !_swapChain.IsValid) return;

            Guid texture2DIid = DirectXNative.IID_ID3D11Texture2D;
            IntPtr pBackBuffer = _swapChain.GetBuffer(0, ref texture2DIid);
            try
            {
                _renderTargetView = D3D11DeviceManager.Device.CreateRenderTargetView(pBackBuffer);
            }
            finally
            {
                ComVTableHelper.Release(pBackBuffer);
            }
        }

        private void DisposeBuffers()
        {
            _renderTargetView?.Dispose();
            _renderTargetView = null;
        }

        public void Resize(int width, int height)
        {
            if (_disposed) return;
            width = Math.Max(1, width);
            height = Math.Max(1, height);

            if (_width == width && _height == height && _renderTargetView != null) return;

            _width = width;
            _height = height;

            if (_swapChain == null || !_swapChain.IsValid)
            {
                RecreateSwapChain();
                return;
            }

            // Unbind render target before releasing backbuffers to avoid pipeline reference locks
            if (D3D11DeviceManager.IsInitialized)
            {
                D3D11DeviceManager.Context.OMSetRenderTargets(null!);
            }

            // Must release all backbuffer references before calling ResizeBuffers
            DisposeBuffers();

            int hr = _swapChain.ResizeBuffers(0, (uint)_width, (uint)_height, DXGI_FORMAT.DXGI_FORMAT_UNKNOWN, 0);
            if (hr < 0)
            {
                // If resize failed due to device lost, recreate device and swapchain
                if (hr == unchecked((int)0x887A0005) || hr == unchecked((int)0x887A0007)) // DXGI_ERROR_DEVICE_REMOVED / RESET
                {
                    D3D11DeviceManager.HandleDeviceLost();
                }
                RecreateSwapChain();
                return;
            }

            CreateRenderTargetView();
        }

        public bool Present(uint syncInterval = 0)
        {
            if (_disposed || _swapChain == null || !_swapChain.IsValid) return false;

            int hr = _swapChain.Present(syncInterval, 0);
            if (hr < 0)
            {
                if (hr == unchecked((int)0x887A0005) || hr == unchecked((int)0x887A0007))
                {
                    D3D11DeviceManager.HandleDeviceLost();
                    RecreateSwapChain();
                }
                return false;
            }

            return true;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                DisposeBuffers();
                _swapChain?.Dispose();
                _swapChain = null;
                _disposed = true;
            }
        }
    }
}
