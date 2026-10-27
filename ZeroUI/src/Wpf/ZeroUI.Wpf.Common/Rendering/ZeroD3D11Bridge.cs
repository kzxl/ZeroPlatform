using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using ZeroGraphics.DirectX.Core;
using ZeroUI.Core.Rendering.Optimizer;

namespace ZeroUI.Wpf.Rendering
{
    /// <summary>
    /// Direct3D 11 to Direct3D 9Ex Shared Surface Bridge for WPF.
    /// Provides zero-copy GPU texture compositing directly into WPF's D3DImage (milcore pipeline),
    /// eliminating CPU roundtrips and achieving maximum framerate rendering.
    /// Consolidated with ZeroGraphics D3D11DeviceManager for shared process-wide GPU device management.
    /// </summary>
    public sealed class ZeroD3D11Bridge : IDisposable
    {
        private readonly object _syncLock = new object();

        // D3D9Ex pointers
        private IntPtr _pD3D9Ex = IntPtr.Zero;
        private IntPtr _pD3D9Device = IntPtr.Zero;
        private IntPtr _pD3D9Texture = IntPtr.Zero;
        private IntPtr _pD3D9Surface = IntPtr.Zero;

        // D3D11 pointers & typed wrappers
        private IntPtr _pD3D11Device = IntPtr.Zero;
        private IntPtr _pD3D11Context = IntPtr.Zero;
        private IntPtr _pD3D11Texture = IntPtr.Zero;
        private IntPtr _pD3D11RTV = IntPtr.Zero;
        private D3D11RenderTargetView? _gpuRtv;

        private IntPtr _sharedHandle = IntPtr.Zero;
        private int _pixelWidth;
        private int _pixelHeight;
        private bool _isDisposed;

        public bool IsInitialized => _pD3D11Device != IntPtr.Zero && _pD3D9Device != IntPtr.Zero;
        public int PixelWidth => _pixelWidth;
        public int PixelHeight => _pixelHeight;

        public IntPtr D3D11Device => _pD3D11Device;
        public IntPtr D3D11Context => _pD3D11Context;
        public IntPtr D3D11Texture => _pD3D11Texture;
        public IntPtr D3D11RenderTargetView => _pD3D11RTV;
        public IntPtr D3D9Surface => _pD3D9Surface;

        /// <summary>
        /// Gets the typed ZeroGraphics Direct3D 11 device.
        /// </summary>
        public D3D11Device GpuDevice => D3D11DeviceManager.Device;

        /// <summary>
        /// Gets the typed ZeroGraphics Direct3D 11 immediate device context.
        /// </summary>
        public D3D11DeviceContext GpuContext => D3D11DeviceManager.Context;

        /// <summary>
        /// Gets the typed ZeroGraphics Direct3D 11 Render Target View for the current back buffer.
        /// </summary>
        public D3D11RenderTargetView? GpuRenderTargetView => _gpuRtv;

        public ZeroD3D11Bridge()
        {
        }

        /// <summary>
        /// Ensures D3D9Ex and D3D11 devices are initialized.
        /// </summary>
        public void EnsureInitialized()
        {
            if (IsInitialized) return;

            lock (_syncLock)
            {
                if (IsInitialized) return;

                InitD3D9();
                InitD3D11();
            }
        }

        private void InitD3D9()
        {
            int hr = D3DNative.Direct3DCreate9Ex(D3DNative.D3D_SDK_VERSION, out _pD3D9Ex);
            if (hr != 0 || _pD3D9Ex == IntPtr.Zero)
            {
                throw new InvalidOperationException($"Direct3DCreate9Ex failed with HRESULT 0x{hr:X8}");
            }

            var pp = new D3DNative.D3DPRESENT_PARAMETERS
            {
                Windowed = 1,
                SwapEffect = D3DNative.D3DSWAPEFFECT_DISCARD,
                hDeviceWindow = D3DNative.GetDesktopWindow(),
                PresentationInterval = 0,
                BackBufferFormat = D3DNative.D3DFMT_UNKNOWN
            };

            var createDeviceEx = D3DNative.GetVTableDelegate<D3DNative.Direct3D9Ex_CreateDeviceEx>(_pD3D9Ex, 20);
            hr = createDeviceEx(
                _pD3D9Ex,
                0, // D3DADAPTER_DEFAULT
                D3DNative.D3DDEVTYPE_HAL,
                IntPtr.Zero,
                D3DNative.D3DCREATE_HARDWARE_VERTEXPROCESSING | D3DNative.D3DCREATE_MULTITHREADED | D3DNative.D3DCREATE_FPU_PRESERVE,
                ref pp,
                IntPtr.Zero,
                out _pD3D9Device);

            if (hr != 0 || _pD3D9Device == IntPtr.Zero)
            {
                // Fallback to NULLREF if HAL is constrained
                hr = createDeviceEx(
                    _pD3D9Ex,
                    0,
                    D3DNative.D3DDEVTYPE_NULLREF,
                    IntPtr.Zero,
                    D3DNative.D3DCREATE_HARDWARE_VERTEXPROCESSING | D3DNative.D3DCREATE_MULTITHREADED | D3DNative.D3DCREATE_FPU_PRESERVE,
                    ref pp,
                    IntPtr.Zero,
                    out _pD3D9Device);

                if (hr != 0 || _pD3D9Device == IntPtr.Zero)
                {
                    throw new InvalidOperationException($"CreateDeviceEx failed with HRESULT 0x{hr:X8}");
                }
            }
        }

        private void InitD3D11()
        {
            D3D11DeviceManager.EnsureInitialized();
            _pD3D11Device = D3D11DeviceManager.Device.Handle;
            _pD3D11Context = D3D11DeviceManager.Context.Handle;

            D3D11DeviceManager.DeviceRestored -= OnDeviceRestored;
            D3D11DeviceManager.DeviceRestored += OnDeviceRestored;

            SyncGpuCapabilities();
        }

        private void OnDeviceRestored()
        {
            lock (_syncLock)
            {
                _pD3D11Device = D3D11DeviceManager.Device.Handle;
                _pD3D11Context = D3D11DeviceManager.Context.Handle;
                ReleaseSurfaceResources();
            }
        }

        private void SyncGpuCapabilities()
        {
            try
            {
                ZeroGpuCapabilities.Configure(
                    ZeroGraphics.Core.Telemetry.GpuCapabilities.AdapterName,
                    (ZeroUI.Core.Rendering.Optimizer.HardwareGpuTier)ZeroGraphics.Core.Telemetry.GpuCapabilities.CurrentTier,
                    ZeroGraphics.Core.Telemetry.GpuCapabilities.DedicatedVramMb,
                    ZeroGraphics.Core.Telemetry.GpuCapabilities.SharedSystemMemoryMb,
                    ZeroGraphics.Core.Telemetry.GpuCapabilities.VendorId);
            }
            catch
            {
                // Graceful fallback
            }
        }

        /// <summary>
        /// Allocates or resizes the shared texture pair between D3D11 and D3D9Ex.
        /// </summary>
        public void EnsureSurfaceSize(int width, int height)
        {
            if (width <= 0 || height <= 0) return;
            if (_pixelWidth == width && _pixelHeight == height && _pD3D9Surface != IntPtr.Zero) return;

            lock (_syncLock)
            {
                EnsureInitialized();
                ReleaseSurfaceResources();

                _pixelWidth = Math.Max(1, width);
                _pixelHeight = Math.Max(1, height);

                // 1. Create D3D11 Shared Texture
                var desc = new D3DNative.D3D11_TEXTURE2D_DESC
                {
                    Width = (uint)_pixelWidth,
                    Height = (uint)_pixelHeight,
                    MipLevels = 1,
                    ArraySize = 1,
                    Format = D3DNative.DXGI_FORMAT_B8G8R8A8_UNORM,
                    SampleDesc = new D3DNative.DXGI_SAMPLE_DESC { Count = 1, Quality = 0 },
                    Usage = D3DNative.D3D11_USAGE_DEFAULT,
                    BindFlags = D3DNative.D3D11_BIND_RENDER_TARGET | D3DNative.D3D11_BIND_SHADER_RESOURCE,
                    CPUAccessFlags = 0,
                    MiscFlags = D3DNative.D3D11_RESOURCE_MISC_SHARED
                };

                var createTexture2D = D3DNative.GetVTableDelegate<D3DNative.D3D11Device_CreateTexture2D>(_pD3D11Device, 5);
                int hr = createTexture2D(_pD3D11Device, ref desc, IntPtr.Zero, out _pD3D11Texture);
                if (hr != 0 || _pD3D11Texture == IntPtr.Zero)
                {
                    throw new InvalidOperationException($"D3D11 CreateTexture2D failed with HRESULT 0x{hr:X8}");
                }

                // 2. Query IDXGIResource to get Shared Handle
                var queryInterface = D3DNative.GetVTableDelegate<D3DNative.IUnknown_QueryInterface>(_pD3D11Texture, 0);
                Guid iidDxgi = D3DNative.IID_IDXGIResource;
                hr = queryInterface(_pD3D11Texture, ref iidDxgi, out IntPtr pDxgiResource);
                if (hr != 0 || pDxgiResource == IntPtr.Zero)
                {
                    throw new InvalidOperationException($"QueryInterface for IDXGIResource failed with HRESULT 0x{hr:X8}");
                }

                try
                {
                    var getSharedHandle = D3DNative.GetVTableDelegate<D3DNative.DXGIResource_GetSharedHandle>(pDxgiResource, 9);
                    hr = getSharedHandle(pDxgiResource, out _sharedHandle);
                    if (hr != 0 || _sharedHandle == IntPtr.Zero)
                    {
                        throw new InvalidOperationException($"GetSharedHandle failed with HRESULT 0x{hr:X8}");
                    }
                }
                finally
                {
                    D3DNative.SafeRelease(ref pDxgiResource);
                }

                // 3. Open Shared Handle in D3D9Ex
                var createTexture9 = D3DNative.GetVTableDelegate<D3DNative.Direct3DDevice9_CreateTexture>(_pD3D9Device, 23);
                IntPtr pShared = _sharedHandle;
                hr = createTexture9(
                    _pD3D9Device,
                    (uint)_pixelWidth,
                    (uint)_pixelHeight,
                    1,
                    D3DNative.D3DUSAGE_RENDERTARGET,
                    D3DNative.D3DFMT_A8R8G8B8,
                    D3DNative.D3DPOOL_DEFAULT,
                    out _pD3D9Texture,
                    ref pShared);

                if (hr != 0 || _pD3D9Texture == IntPtr.Zero)
                {
                    throw new InvalidOperationException($"D3D9 CreateTexture from shared handle failed with HRESULT 0x{hr:X8}");
                }

                // 4. Get Surface Level 0 from D3D9 Texture
                var getSurfaceLevel = D3DNative.GetVTableDelegate<D3DNative.Direct3DTexture9_GetSurfaceLevel>(_pD3D9Texture, 18);
                hr = getSurfaceLevel(_pD3D9Texture, 0, out _pD3D9Surface);
                if (hr != 0 || _pD3D9Surface == IntPtr.Zero)
                {
                    throw new InvalidOperationException($"D3D9 GetSurfaceLevel failed with HRESULT 0x{hr:X8}");
                }

                // 5. Create D3D11 Render Target View for direct GPU drawing via ZeroGraphics
                _gpuRtv = D3D11DeviceManager.Device.CreateRenderTargetView(_pD3D11Texture);
                _pD3D11RTV = _gpuRtv.Handle;
            }
        }

        /// <summary>
        /// Clears the D3D11 Render Target View with specified RGBA color.
        /// </summary>
        public void Clear(float r, float g, float b, float a = 1.0f)
        {
            if (_gpuRtv != null && _gpuRtv.IsValid)
            {
                D3D11DeviceManager.Context.ClearRenderTargetView(_gpuRtv, new[] { r, g, b, a });
            }
            else if (_pD3D11Context != IntPtr.Zero && _pD3D11RTV != IntPtr.Zero)
            {
                var clearRTV = D3DNative.GetVTableDelegate<D3DNative.D3D11DeviceContext_ClearRenderTargetView>(_pD3D11Context, 33);
                clearRTV(_pD3D11Context, _pD3D11RTV, new float[] { r, g, b, a });
            }
        }

        /// <summary>
        /// Flushes GPU commands on Direct3D 11 device context.
        /// </summary>
        public void Flush()
        {
            if (D3D11DeviceManager.IsInitialized)
            {
                D3D11DeviceManager.Context.Flush();
            }
            else if (_pD3D11Context != IntPtr.Zero)
            {
                var flush = D3DNative.GetVTableDelegate<D3DNative.D3D11DeviceContext_Flush>(_pD3D11Context, 111);
                flush(_pD3D11Context);
            }
        }

        /// <summary>
        /// Binds the D3D9 shared surface as the back buffer of a WPF D3DImage and invalidates dirty region.
        /// </summary>
        public void PresentTo(D3DImage d3dImage)
        {
            if (d3dImage == null || _pD3D9Surface == IntPtr.Zero) return;

            if (!d3dImage.IsFrontBufferAvailable) return;

            d3dImage.Lock();
            try
            {
                d3dImage.SetBackBuffer(D3DResourceType.IDirect3DSurface9, _pD3D9Surface);
                d3dImage.AddDirtyRect(new Int32Rect(0, 0, _pixelWidth, _pixelHeight));
            }
            finally
            {
                d3dImage.Unlock();
            }
        }

        private void ReleaseSurfaceResources()
        {
            if (_gpuRtv != null)
            {
                _gpuRtv.Dispose();
                _gpuRtv = null;
            }
            _pD3D11RTV = IntPtr.Zero;
            D3DNative.SafeRelease(ref _pD3D11Texture);
            D3DNative.SafeRelease(ref _pD3D9Surface);
            D3DNative.SafeRelease(ref _pD3D9Texture);
            _sharedHandle = IntPtr.Zero;
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            lock (_syncLock)
            {
                D3D11DeviceManager.DeviceRestored -= OnDeviceRestored;
                ReleaseSurfaceResources();

                _pD3D11RTV = IntPtr.Zero;
                _pD3D11Context = IntPtr.Zero;
                _pD3D11Device = IntPtr.Zero;

                D3DNative.SafeRelease(ref _pD3D9Surface);
                D3DNative.SafeRelease(ref _pD3D9Texture);
                D3DNative.SafeRelease(ref _pD3D9Device);
                D3DNative.SafeRelease(ref _pD3D9Ex);
            }
        }
    }
}
