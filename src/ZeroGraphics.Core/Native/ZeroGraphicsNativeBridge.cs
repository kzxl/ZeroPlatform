using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ZeroGraphics.Core.Native
{
    /// <summary>
    /// Execution policy for dispatching graphics and vision kernels between pure C# and native ZeroRust substrate.
    /// </summary>
    public enum GraphicsNativeDispatchMode
    {
        /// <summary>
        /// Automatically dispatches based on image/signal dimensions and FFI transition overhead.
        /// </summary>
        Auto = 0,

        /// <summary>
        /// Forces native ZeroRust execution when the native library is loaded and available.
        /// </summary>
        ForceNative = 1,

        /// <summary>
        /// Forces pure C# execution with zero P/Invoke or native library overhead.
        /// </summary>
        ForcePureCSharp = 2
    }

    /// <summary>
    /// Dynamic C-ABI bridge to ZeroRust native graphics and vision substrate (zero_graphics.dll / zero_compute.dll).
    /// Dynamically detects and resolves native AVX2/FMA vision, SDF, morphology, and LTTB kernels at runtime
    /// with graceful fallback to pure C#.
    /// </summary>
    public static unsafe class ZeroGraphicsNativeBridge
    {
        private static bool _initialized;
        private static bool _isAvailable;
        private static IntPtr _libraryHandle = IntPtr.Zero;

        /// <summary>
        /// Gets or sets the dispatch policy between pure C# and ZeroRust native execution.
        /// Defaults to <see cref="GraphicsNativeDispatchMode.Auto"/>.
        /// </summary>
        public static GraphicsNativeDispatchMode DispatchMode { get; set; } = GraphicsNativeDispatchMode.Auto;

#if NET8_0_OR_GREATER
        private static delegate* unmanaged[Cdecl]<uint> _fnVersion;
        private static delegate* unmanaged[Cdecl]<int> _fnIsSimdSupported;
        private static delegate* unmanaged[Cdecl]<byte*, nuint, byte*, nuint, nuint, nuint, int> _fnBgra32ToGray8;
        private static delegate* unmanaged[Cdecl]<byte*, nuint, byte*, nuint, nuint, nuint, int> _fnGaussian5x5U8;
        private static delegate* unmanaged[Cdecl]<byte*, nuint, byte*, nuint, nuint, nuint, float, float, int, int> _fnCannyEdgeU8;
        private static delegate* unmanaged[Cdecl]<byte*, nuint, nuint, nuint, byte*, nuint, nuint, nuint, double, int, double*, double*, double*, int> _fnNccMatchU8;
        private static delegate* unmanaged[Cdecl]<float*, float*, float*, float*, nuint, nuint, nuint, float, float, float, float, float, byte*, int> _fnSdfGenerateU8;
        private static delegate* unmanaged[Cdecl]<byte*, nuint, byte*, nuint, nuint, nuint, int, int> _fnMorphology3x3U8;
        private static delegate* unmanaged[Cdecl]<double*, double*, nuint, nuint, double*, double*, nuint*, int> _fnLttbDecimateF64;
#endif

        /// <summary>
        /// Gets whether the native ZeroRust graphics/vision engine is loaded and operational.
        /// </summary>
        public static bool IsAvailable
        {
            get
            {
                if (!_initialized)
                {
                    Initialize();
                }
                return _isAvailable;
            }
        }

        /// <summary>
        /// Determines whether an image operation of dimensions (<paramref name="width"/> x <paramref name="height"/>)
        /// should be dispatched to the native ZeroRust kernel.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ShouldDispatchImage(int width, int height, int minPixels = 4096)
        {
            if (DispatchMode == GraphicsNativeDispatchMode.ForcePureCSharp || !IsAvailable) return false;
            if (DispatchMode == GraphicsNativeDispatchMode.ForceNative) return true;
            return (long)width * height >= minPixels;
        }

        /// <summary>
        /// Initializes (or re-initializes) the native library bridge. Safe to call multiple times.
        /// </summary>
        public static void Initialize(bool forceReload = false)
        {
            if (_initialized && !forceReload) return;

            lock (typeof(ZeroGraphicsNativeBridge))
            {
                if (_initialized && !forceReload) return;

#if NET8_0_OR_GREATER
                try
                {
                    _libraryHandle = LoadAndBindNativeLibrary();
                    _isAvailable = _libraryHandle != IntPtr.Zero;
                }
                catch
                {
                    _isAvailable = false;
                }
#else
                _isAvailable = false;
#endif
                _initialized = true;
            }
        }

#if NET8_0_OR_GREATER
        private static IntPtr LoadAndBindNativeLibrary()
        {
            string[] libCandidates = new[] { "zero_graphics", "zero_compute" };
            string baseDir = AppContext.BaseDirectory;

            foreach (var libName in libCandidates)
            {
                // 1. Standard assembly/PATH probe
                if (NativeLibrary.TryLoad(libName, typeof(ZeroGraphicsNativeBridge).Assembly, null, out IntPtr handle) &&
                    TryBindExports(handle))
                {
                    return handle;
                }

                // 2. AppContext.BaseDirectory
                string localDll = Path.Combine(baseDir, $"{libName}.dll");
                if (File.Exists(localDll) && NativeLibrary.TryLoad(localDll, out handle) &&
                    TryBindExports(handle))
                {
                    return handle;
                }

                // 3. runtimes/win-x64/native
                string runtimesDll = Path.Combine(baseDir, "runtimes", "win-x64", "native", $"{libName}.dll");
                if (File.Exists(runtimesDll) && NativeLibrary.TryLoad(runtimesDll, out handle) &&
                    TryBindExports(handle))
                {
                    return handle;
                }

                // 4. Development workspace paths
                string[] probePaths = new[]
                {
                    $@"E:\15. Other\ZeroUniverse\ZeroRust\target\release\{libName}.dll",
                    $@"..\..\..\..\..\ZeroRust\target\release\{libName}.dll",
                    $@"..\..\..\..\ZeroRust\target\release\{libName}.dll",
                    Path.Combine(baseDir, $@"..\..\..\..\..\ZeroRust\target\release\{libName}.dll"),
                    Path.Combine(baseDir, $@"..\..\..\..\ZeroRust\target\release\{libName}.dll")
                };

                foreach (var path in probePaths)
                {
                    try
                    {
                        string fullPath = Path.GetFullPath(path);
                        if (File.Exists(fullPath) && NativeLibrary.TryLoad(fullPath, out handle) &&
                            TryBindExports(handle))
                        {
                            return handle;
                        }
                    }
                    catch
                    {
                        // Ignore invalid path probes
                    }
                }
            }

            return IntPtr.Zero;
        }

        private static bool TryBindExports(IntPtr handle)
        {
            if (handle == IntPtr.Zero) return false;

            if (!NativeLibrary.TryGetExport(handle, "zg_version", out IntPtr pVersion) ||
                !NativeLibrary.TryGetExport(handle, "zg_is_simd_supported", out IntPtr pSimd) ||
                !NativeLibrary.TryGetExport(handle, "zg_bgra32_to_gray8", out IntPtr pBgraToGray) ||
                !NativeLibrary.TryGetExport(handle, "zg_gaussian5x5_u8", out IntPtr pGauss) ||
                !NativeLibrary.TryGetExport(handle, "zg_canny_edge_u8", out IntPtr pCanny) ||
                !NativeLibrary.TryGetExport(handle, "zg_ncc_match_u8", out IntPtr pNcc) ||
                !NativeLibrary.TryGetExport(handle, "zg_sdf_generate_u8", out IntPtr pSdf) ||
                !NativeLibrary.TryGetExport(handle, "zg_morphology3x3_u8", out IntPtr pMorph) ||
                !NativeLibrary.TryGetExport(handle, "zg_lttb_decimate_f64", out IntPtr pLttb))
            {
                return false;
            }

            _fnVersion = (delegate* unmanaged[Cdecl]<uint>)pVersion;
            _fnIsSimdSupported = (delegate* unmanaged[Cdecl]<int>)pSimd;
            _fnBgra32ToGray8 = (delegate* unmanaged[Cdecl]<byte*, nuint, byte*, nuint, nuint, nuint, int>)pBgraToGray;
            _fnGaussian5x5U8 = (delegate* unmanaged[Cdecl]<byte*, nuint, byte*, nuint, nuint, nuint, int>)pGauss;
            _fnCannyEdgeU8 = (delegate* unmanaged[Cdecl]<byte*, nuint, byte*, nuint, nuint, nuint, float, float, int, int>)pCanny;
            _fnNccMatchU8 = (delegate* unmanaged[Cdecl]<byte*, nuint, nuint, nuint, byte*, nuint, nuint, nuint, double, int, double*, double*, double*, int>)pNcc;
            _fnSdfGenerateU8 = (delegate* unmanaged[Cdecl]<float*, float*, float*, float*, nuint, nuint, nuint, float, float, float, float, float, byte*, int>)pSdf;
            _fnMorphology3x3U8 = (delegate* unmanaged[Cdecl]<byte*, nuint, byte*, nuint, nuint, nuint, int, int>)pMorph;
            _fnLttbDecimateF64 = (delegate* unmanaged[Cdecl]<double*, double*, nuint, nuint, double*, double*, nuint*, int>)pLttb;

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint GetVersion()
        {
            if (!IsAvailable || _fnVersion == null) return 0;
            return _fnVersion();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsSimdSupported()
        {
            if (!IsAvailable || _fnIsSimdSupported == null) return false;
            return _fnIsSimdSupported() != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryBgra32ToGray8(byte* src, int srcStride, byte* dst, int dstStride, int width, int height)
        {
            if (!IsAvailable || _fnBgra32ToGray8 == null) return false;
            return _fnBgra32ToGray8(src, (nuint)srcStride, dst, (nuint)dstStride, (nuint)width, (nuint)height) == 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryGaussian5x5(byte* src, int srcStride, byte* dst, int dstStride, int width, int height)
        {
            if (!IsAvailable || _fnGaussian5x5U8 == null) return false;
            return _fnGaussian5x5U8(src, (nuint)srcStride, dst, (nuint)dstStride, (nuint)width, (nuint)height) == 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryCannyEdge(
            byte* src, int srcStride,
            byte* dst, int dstStride,
            int width, int height,
            float lowThreshold, float highThreshold,
            bool applyGaussian)
        {
            if (!IsAvailable || _fnCannyEdgeU8 == null) return false;
            return _fnCannyEdgeU8(
                src, (nuint)srcStride,
                dst, (nuint)dstStride,
                (nuint)width, (nuint)height,
                lowThreshold, highThreshold,
                applyGaussian ? 1 : 0) == 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryNccMatch(
            byte* search, int searchStride, int sw, int sh,
            byte* tpl, int tplStride, int tw, int th,
            double minScore, bool subPixel,
            out double outX, out double outY, out double outScore)
        {
            outX = -1.0;
            outY = -1.0;
            outScore = -1.0;
            if (!IsAvailable || _fnNccMatchU8 == null) return false;

            double x = -1.0, y = -1.0, score = -1.0;
            int rc = _fnNccMatchU8(
                search, (nuint)searchStride, (nuint)sw, (nuint)sh,
                tpl, (nuint)tplStride, (nuint)tw, (nuint)th,
                minScore, subPixel ? 1 : 0,
                &x, &y, &score);
            if (rc != 0) return false;

            outX = x;
            outY = y;
            outScore = score;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryGenerateSdf(
            float* segAx, float* segAy, float* segBx, float* segBy,
            int numSegments, int width, int height,
            float boundsMinX, float boundsMinY, float boundsMaxX, float boundsMaxY,
            float spread, byte* dst)
        {
            if (!IsAvailable || _fnSdfGenerateU8 == null) return false;
            return _fnSdfGenerateU8(
                segAx, segAy, segBx, segBy,
                (nuint)numSegments, (nuint)width, (nuint)height,
                boundsMinX, boundsMinY, boundsMaxX, boundsMaxY,
                spread, dst) == 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryMorphology3x3(
            byte* src, int srcStride,
            byte* dst, int dstStride,
            int width, int height,
            int mode)
        {
            if (!IsAvailable || _fnMorphology3x3U8 == null) return false;
            return _fnMorphology3x3U8(src, (nuint)srcStride, dst, (nuint)dstStride, (nuint)width, (nuint)height, mode) == 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryLttbDecimate(
            double* srcX, double* srcY,
            int srcLen, int threshold,
            double* dstX, double* dstY,
            out int outWritten)
        {
            outWritten = 0;
            if (!IsAvailable || _fnLttbDecimateF64 == null) return false;

            nuint written = 0;
            int rc = _fnLttbDecimateF64(srcX, srcY, (nuint)srcLen, (nuint)threshold, dstX, dstY, &written);
            if (rc != 0) return false;

            outWritten = (int)written;
            return true;
        }
#else
        public static uint GetVersion() => 0;
        public static bool IsSimdSupported() => false;
        public static bool TryBgra32ToGray8(byte* src, int srcStride, byte* dst, int dstStride, int width, int height) => false;
        public static bool TryGaussian5x5(byte* src, int srcStride, byte* dst, int dstStride, int width, int height) => false;
        public static bool TryCannyEdge(byte* src, int srcStride, byte* dst, int dstStride, int width, int height, float lowThreshold, float highThreshold, bool applyGaussian) => false;
        public static bool TryNccMatch(byte* search, int searchStride, int sw, int sh, byte* tpl, int tplStride, int tw, int th, double minScore, bool subPixel, out double outX, out double outY, out double outScore)
        {
            outX = -1.0;
            outY = -1.0;
            outScore = -1.0;
            return false;
        }
        public static bool TryGenerateSdf(float* segAx, float* segAy, float* segBx, float* segBy, int numSegments, int width, int height, float boundsMinX, float boundsMinY, float boundsMaxX, float boundsMaxY, float spread, byte* dst) => false;
        public static bool TryMorphology3x3(byte* src, int srcStride, byte* dst, int dstStride, int width, int height, int mode) => false;
        public static bool TryLttbDecimate(double* srcX, double* srcY, int srcLen, int threshold, double* dstX, double* dstY, out int outWritten)
        {
            outWritten = 0;
            return false;
        }
#endif
    }
}
