using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ZeroTensor.Core.Native
{
    /// <summary>
    /// Dynamic C-ABI bridge to ZeroRust native compute substrate (zero_tensor.dll / libzero_tensor.so).
    /// Dynamically detects and resolves native AVX2/FMA kernels at runtime with graceful fallback to pure C#.
    /// </summary>
    public static unsafe class ZeroTensorNativeBridge
    {
        private static bool _initialized = false;
        private static bool _isAvailable = false;
        private static IntPtr _libraryHandle = IntPtr.Zero;

#if NET8_0_OR_GREATER
        private static delegate* unmanaged[Cdecl]<uint> _fnVersion;
        private static delegate* unmanaged[Cdecl]<int> _fnIsSimdSupported;
        private static delegate* unmanaged[Cdecl]<nuint, nuint, nuint, float, float*, nint, nint, float*, nint, nint, float, float*, nint, nint, int, int> _fnGemmF32;
        private static delegate* unmanaged[Cdecl]<nuint, nuint, nuint, nuint, nuint, float, int, nuint, nuint, nuint, float*, float*, float*, float*, float*, int> _fnFlashAttentionF32;
        private static delegate* unmanaged[Cdecl]<byte*, float*, float*, nuint, nuint, int> _fnGemvQ4_0;
        private static delegate* unmanaged[Cdecl]<byte*, float*, float*, nuint, nuint, int> _fnGemvQ8_0;
#endif

        /// <summary>
        /// Gets whether the native ZeroRust compute engine is loaded and operational.
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
        /// Initializes the native library bridge. Safe to call multiple times.
        /// </summary>
        public static void Initialize()
        {
            if (_initialized) return;

            lock (typeof(ZeroTensorNativeBridge))
            {
                if (_initialized) return;

#if NET8_0_OR_GREATER
                try
                {
                    _libraryHandle = LoadNativeLibrary();
                    if (_libraryHandle != IntPtr.Zero)
                    {
                        if (NativeLibrary.TryGetExport(_libraryHandle, "zerorust_version", out IntPtr pVersion) &&
                            NativeLibrary.TryGetExport(_libraryHandle, "zerorust_is_simd_supported", out IntPtr pSimd) &&
                            NativeLibrary.TryGetExport(_libraryHandle, "zerorust_gemm_f32", out IntPtr pGemm) &&
                            NativeLibrary.TryGetExport(_libraryHandle, "zerorust_flash_attention_f32", out IntPtr pFlash) &&
                            NativeLibrary.TryGetExport(_libraryHandle, "zerorust_gemv_q4_0", out IntPtr pQ4) &&
                            NativeLibrary.TryGetExport(_libraryHandle, "zerorust_gemv_q8_0", out IntPtr pQ8))
                        {
                            _fnVersion = (delegate* unmanaged[Cdecl]<uint>)pVersion;
                            _fnIsSimdSupported = (delegate* unmanaged[Cdecl]<int>)pSimd;
                            _fnGemmF32 = (delegate* unmanaged[Cdecl]<nuint, nuint, nuint, float, float*, nint, nint, float*, nint, nint, float, float*, nint, nint, int, int>)pGemm;
                            _fnFlashAttentionF32 = (delegate* unmanaged[Cdecl]<nuint, nuint, nuint, nuint, nuint, float, int, nuint, nuint, nuint, float*, float*, float*, float*, float*, int>)pFlash;
                            _fnGemvQ4_0 = (delegate* unmanaged[Cdecl]<byte*, float*, float*, nuint, nuint, int>)pQ4;
                            _fnGemvQ8_0 = (delegate* unmanaged[Cdecl]<byte*, float*, float*, nuint, nuint, int>)pQ8;

                            _isAvailable = true;
                        }
                    }
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
        private static IntPtr LoadNativeLibrary()
        {
            string libName = "zero_tensor";

            // 1. Try standard probe (PATH, AppDomain, etc.)
            if (NativeLibrary.TryLoad(libName, typeof(ZeroTensorNativeBridge).Assembly, null, out IntPtr handle))
            {
                return handle;
            }

            // 2. Try AppContext.BaseDirectory
            string baseDir = AppContext.BaseDirectory;
            string localDll = Path.Combine(baseDir, "zero_tensor.dll");
            if (File.Exists(localDll) && NativeLibrary.TryLoad(localDll, out handle))
            {
                return handle;
            }

            // 3. Try runtimes/win-x64/native
            string runtimesDll = Path.Combine(baseDir, "runtimes", "win-x64", "native", "zero_tensor.dll");
            if (File.Exists(runtimesDll) && NativeLibrary.TryLoad(runtimesDll, out handle))
            {
                return handle;
            }

            // 4. Try development workspace build (ZeroRust/target/release)
            string[] probePaths = new[]
            {
                @"E:\15. Other\ZeroUniverse\ZeroRust\target\release\zero_tensor.dll",
                @"..\..\..\..\ZeroRust\target\release\zero_tensor.dll",
                @"..\..\ZeroRust\target\release\zero_tensor.dll"
            };

            foreach (var path in probePaths)
            {
                try
                {
                    if (File.Exists(path) && NativeLibrary.TryLoad(Path.GetFullPath(path), out handle))
                    {
                        return handle;
                    }
                }
                catch { }
            }

            return IntPtr.Zero;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Gemm(
            int m, int n, int k,
            float alpha,
            float* a, int rsa, int csa,
            float* b, int rsb, int csb,
            float beta,
            float* c, int rsc, int csc,
            bool parallel = true)
        {
            if (!_isAvailable || _fnGemmF32 == null) return -1;
            return _fnGemmF32((nuint)m, (nuint)n, (nuint)k, alpha, a, rsa, csa, b, rsb, csb, beta, c, rsc, csc, parallel ? 1 : 0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int FlashAttention(
            int batchCount, int sq, int sk, int d, int dv,
            float scaleFactor,
            bool isGqa, int hq, int hkv, int gqaGroup,
            float* q, float* k, float* v, float* mask, float* output)
        {
            if (!_isAvailable || _fnFlashAttentionF32 == null) return -1;
            return _fnFlashAttentionF32(
                (nuint)batchCount, (nuint)sq, (nuint)sk, (nuint)d, (nuint)dv,
                scaleFactor, isGqa ? 1 : 0,
                (nuint)hq, (nuint)hkv, (nuint)gqaGroup,
                q, k, v, mask, output);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GemvQ4_0(byte* ptrWeight, float* pAct, float* pDst, int nRows, int kCols)
        {
            if (!_isAvailable || _fnGemvQ4_0 == null) return -1;
            return _fnGemvQ4_0(ptrWeight, pAct, pDst, (nuint)nRows, (nuint)kCols);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GemvQ8_0(byte* ptrWeight, float* pAct, float* pDst, int nRows, int kCols)
        {
            if (!_isAvailable || _fnGemvQ8_0 == null) return -1;
            return _fnGemvQ8_0(ptrWeight, pAct, pDst, (nuint)nRows, (nuint)kCols);
        }
#endif
    }
}
