using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using ZeroGraphics.DirectX.Native;

namespace ZeroGraphics.DirectX.Interception
{
    /// <summary>
    /// Direct3D 11 and DXGI graphics interception engine.
    /// Intercepts SwapChain Present and DeviceContext Draw/Shader slots for telemetry,
    /// dynamic shader overriding, and frame capture without external C++ detours.
    /// </summary>
    public sealed unsafe class D3D11GraphicsInterceptor : IDisposable
    {
        // COM VTable slot constants
        private const int DXGI_SWAPCHAIN_PRESENT_SLOT = 8;
        private const int D3D11_CONTEXT_DRAWINDEXED_SLOT = 12;
        private const int D3D11_CONTEXT_DRAW_SLOT = 13;
        private const int D3D11_CONTEXT_PSSETSHADER_SLOT = 9;

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int PresentDelegate(IntPtr pSwapChain, uint syncInterval, uint flags);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void DrawDelegate(IntPtr pContext, uint vertexCount, uint startVertexLocation);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void DrawIndexedDelegate(IntPtr pContext, uint indexCount, uint startIndexLocation, int baseVertexLocation);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void PSSetShaderDelegate(IntPtr pContext, IntPtr pPixelShader, IntPtr ppClassInstances, uint numClassInstances);

        private ComVTableHook? _swapChainHook;
        private ComVTableHook? _contextHook;

        // Pinned delegate references to prevent GC collection
        private readonly PresentDelegate _presentDetour;
        private readonly DrawDelegate _drawDetour;
        private readonly DrawIndexedDelegate _drawIndexedDetour;
        private readonly PSSetShaderDelegate _psSetShaderDetour;

        // Original function pointers
        private IntPtr _origPresentPtr;
        private IntPtr _origDrawPtr;
        private IntPtr _origDrawIndexedPtr;
        private IntPtr _origPSSetShaderPtr;

        // Frame Metrics & Telemetry
        private long _lastTimestamp;
        private ulong _frameIndex;
        private int _currentDrawCalls;
        private long _currentVertexCount;
        private long _currentIndexCount;

        // Shader Overrides: Original Shader Pointer -> Replacement Shader Pointer
        private readonly ConcurrentDictionary<IntPtr, IntPtr> _shaderOverrides = new ConcurrentDictionary<IntPtr, IntPtr>();

        /// <summary>
        /// Fires when a frame is presented by the DXGI SwapChain.
        /// </summary>
        public event Action<FrameTelemetry>? FramePresented;

        /// <summary>
        /// Gets the total number of frames presented so far.
        /// </summary>
        public ulong TotalFramesPresented => _frameIndex;

        /// <summary>
        /// Gets the number of draw calls executed in the active frame.
        /// </summary>
        public int ActiveFrameDrawCalls => _currentDrawCalls;

        public D3D11GraphicsInterceptor()
        {
            _presentDetour = OnDetourPresent;
            _drawDetour = OnDetourDraw;
            _drawIndexedDetour = OnDetourDrawIndexed;
            _psSetShaderDetour = OnDetourPSSetShader;
            _lastTimestamp = Stopwatch.GetTimestamp();
        }

        /// <summary>
        /// Attaches the interceptor to an active DXGI SwapChain.
        /// Intercepts Present calls to measure frame times and FPS.
        /// </summary>
        public void AttachSwapChain(IntPtr pSwapChain)
        {
            if (pSwapChain == IntPtr.Zero) throw new ArgumentNullException(nameof(pSwapChain));
            if (_swapChainHook != null) throw new InvalidOperationException("SwapChain is already attached.");

            _swapChainHook = new ComVTableHook(pSwapChain);
            IntPtr detourPtr = Marshal.GetFunctionPointerForDelegate(_presentDetour);
            _origPresentPtr = _swapChainHook.HookMethod(DXGI_SWAPCHAIN_PRESENT_SLOT, detourPtr);
        }

        /// <summary>
        /// Attaches the interceptor to an active ID3D11DeviceContext.
        /// Intercepts Draw, DrawIndexed, and PSSetShader calls for metrics and runtime modification.
        /// </summary>
        public void AttachDeviceContext(IntPtr pContext)
        {
            if (pContext == IntPtr.Zero) throw new ArgumentNullException(nameof(pContext));
            if (_contextHook != null) throw new InvalidOperationException("DeviceContext is already attached.");

            _contextHook = new ComVTableHook(pContext);

            IntPtr drawDetourPtr = Marshal.GetFunctionPointerForDelegate(_drawDetour);
            _origDrawPtr = _contextHook.HookMethod(D3D11_CONTEXT_DRAW_SLOT, drawDetourPtr);

            IntPtr drawIndexedDetourPtr = Marshal.GetFunctionPointerForDelegate(_drawIndexedDetour);
            _origDrawIndexedPtr = _contextHook.HookMethod(D3D11_CONTEXT_DRAWINDEXED_SLOT, drawIndexedDetourPtr);

            IntPtr psSetShaderDetourPtr = Marshal.GetFunctionPointerForDelegate(_psSetShaderDetour);
            _origPSSetShaderPtr = _contextHook.HookMethod(D3D11_CONTEXT_PSSETSHADER_SLOT, psSetShaderDetourPtr);
        }

        /// <summary>
        /// Registers a runtime shader replacement.
        /// When the application binds originalPixelShader, replacementPixelShader will be bound instead.
        /// </summary>
        public void RegisterShaderOverride(IntPtr originalPixelShader, IntPtr replacementPixelShader)
        {
            if (originalPixelShader == IntPtr.Zero) throw new ArgumentNullException(nameof(originalPixelShader));
            _shaderOverrides[originalPixelShader] = replacementPixelShader;
        }

        /// <summary>
        /// Removes a registered shader replacement.
        /// </summary>
        public bool RemoveShaderOverride(IntPtr originalPixelShader)
        {
            return _shaderOverrides.TryRemove(originalPixelShader, out _);
        }

        private int OnDetourPresent(IntPtr pSwapChain, uint syncInterval, uint flags)
        {
            long now = Stopwatch.GetTimestamp();
            double deltaMs = (now - _lastTimestamp) * 1000.0 / Stopwatch.Frequency;
            _lastTimestamp = now;

            double fps = deltaMs > 0 ? 1000.0 / deltaMs : 0.0;
            ulong frame = _frameIndex++;

            int draws = System.Threading.Interlocked.Exchange(ref _currentDrawCalls, 0);
            long verts = System.Threading.Interlocked.Exchange(ref _currentVertexCount, 0);
            long indices = System.Threading.Interlocked.Exchange(ref _currentIndexCount, 0);

            var telemetry = new FrameTelemetry(frame, deltaMs, fps, draws, verts, indices);
            FramePresented?.Invoke(telemetry);

            // Call original Present via unmanaged function pointer
            return ((delegate* unmanaged[Stdcall]<IntPtr, uint, uint, int>)_origPresentPtr)(pSwapChain, syncInterval, flags);
        }

        private void OnDetourDraw(IntPtr pContext, uint vertexCount, uint startVertexLocation)
        {
            System.Threading.Interlocked.Increment(ref _currentDrawCalls);
            System.Threading.Interlocked.Add(ref _currentVertexCount, vertexCount);

            ((delegate* unmanaged[Stdcall]<IntPtr, uint, uint, void>)_origDrawPtr)(pContext, vertexCount, startVertexLocation);
        }

        private void OnDetourDrawIndexed(IntPtr pContext, uint indexCount, uint startIndexLocation, int baseVertexLocation)
        {
            System.Threading.Interlocked.Increment(ref _currentDrawCalls);
            System.Threading.Interlocked.Add(ref _currentIndexCount, indexCount);

            ((delegate* unmanaged[Stdcall]<IntPtr, uint, uint, int, void>)_origDrawIndexedPtr)(pContext, indexCount, startIndexLocation, baseVertexLocation);
        }

        private void OnDetourPSSetShader(IntPtr pContext, IntPtr pPixelShader, IntPtr ppClassInstances, uint numClassInstances)
        {
            // Runtime shader replacement check
            if (_shaderOverrides.TryGetValue(pPixelShader, out var replacement))
            {
                pPixelShader = replacement;
            }

            ((delegate* unmanaged[Stdcall]<IntPtr, IntPtr, IntPtr, uint, void>)_origPSSetShaderPtr)(pContext, pPixelShader, ppClassInstances, numClassInstances);
        }

        public void Dispose()
        {
            _swapChainHook?.Dispose();
            _swapChainHook = null;

            _contextHook?.Dispose();
            _contextHook = null;

            _shaderOverrides.Clear();
        }
    }
}
