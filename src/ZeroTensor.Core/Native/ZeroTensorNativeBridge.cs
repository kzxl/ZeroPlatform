using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ZeroTensor.Core.Native
{
    /// <summary>
    /// Execution policy for dispatching tensor compute between pure C# and native ZeroRust substrate.
    /// </summary>
    public enum NativeDispatchMode
    {
        /// <summary>
        /// Automatically and adaptively dispatches based on matrix dimensions, arithmetic intensity, and latency characteristics.
        /// </summary>
        Auto = 0,

        /// <summary>
        /// Forces native ZeroRust execution when the library is loaded and available.
        /// </summary>
        ForceNative = 1,

        /// <summary>
        /// Forces pure C# SIMD execution with zero P/Invoke or native library overhead.
        /// </summary>
        ForcePureCSharp = 2
    }

    /// <summary>
    /// Dynamic C-ABI bridge to ZeroRust native compute substrate (zero_tensor.dll / libzero_tensor.so).
    /// Dynamically detects and resolves native AVX2/FMA kernels at runtime with graceful fallback to pure C#.
    /// </summary>
    public static unsafe class ZeroTensorNativeBridge
    {
        private static bool _initialized = false;
        private static bool _isAvailable = false;
        private static IntPtr _libraryHandle = IntPtr.Zero;

        /// <summary>
        /// Gets or sets the dispatch policy between pure C# and ZeroRust native execution.
        /// Defaults to <see cref="NativeDispatchMode.Auto"/>.
        /// </summary>
        public static NativeDispatchMode DispatchMode { get; set; } = NativeDispatchMode.Auto;

        /// <summary>
        /// Determines whether GEMM should be dispatched to ZeroRust based on payload size and execution mode.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ShouldDispatchGemm(int m, int n, int k, bool isBatched = false)
        {
            if (DispatchMode == NativeDispatchMode.ForcePureCSharp || !IsAvailable) return false;
            if (DispatchMode == NativeDispatchMode.ForceNative) return true;

            // Batched MatMul has high arithmetic intensity and benefits immensely from zero-copy native microkernels
            if (isBatched) return true;

            // Small matrices (M * N * K < 32768) are faster inlined in RyuJIT without FFI transition cost
            long volume = (long)m * n * k;
            return volume >= 32768;
        }

        /// <summary>
        /// Determines whether FlashAttention should be dispatched to ZeroRust based on token count and context size.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ShouldDispatchFlashAttention(int batchCount, int sQ, int sK, int d)
        {
            if (DispatchMode == NativeDispatchMode.ForcePureCSharp || !IsAvailable) return false;
            if (DispatchMode == NativeDispatchMode.ForceNative) return true;

            // Single token decode step with short context
            if (batchCount == 1 && sQ == 1 && sK < 64) return false;

            // Otherwise dispatch to ZeroRust AVX2/FMA block-tiled online softmax
            long totalTokens = (long)batchCount * sQ * sK;
            return totalTokens >= 2048;
        }

        /// <summary>
        /// Determines whether Quantized GEMV should be dispatched to ZeroRust based on weight matrix dimensions.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ShouldDispatchGemv(int nRows, int kCols)
        {
            if (DispatchMode == NativeDispatchMode.ForcePureCSharp || !IsAvailable) return false;
            if (DispatchMode == NativeDispatchMode.ForceNative) return true;

            long totalWeights = (long)nRows * kCols;
            return totalWeights >= 32768;
        }

#if NET8_0_OR_GREATER
        private static delegate* unmanaged[Cdecl]<uint> _fnVersion;
        private static delegate* unmanaged[Cdecl]<int> _fnIsSimdSupported;
        private static delegate* unmanaged[Cdecl]<nuint, nuint, nuint, float, float*, nint, nint, float*, nint, nint, float, float*, nint, nint, int, int> _fnGemmF32;
        private static delegate* unmanaged[Cdecl]<nuint, nuint, nuint, nuint, nuint, float, int, nuint, nuint, nuint, float*, float*, float*, float*, float*, int> _fnFlashAttentionF32;
        private static delegate* unmanaged[Cdecl]<byte*, float*, float*, nuint, nuint, int> _fnGemvQ4_0;
        private static delegate* unmanaged[Cdecl]<byte*, float*, float*, nuint, nuint, int> _fnGemvQ8_0;
        private static delegate* unmanaged[Cdecl]<nuint, float*, float*, float*, float, int> _fnRmsNormF32;
        private static delegate* unmanaged[Cdecl]<nuint, float*, float*, int> _fnSiluF32;
        private static delegate* unmanaged[Cdecl]<nuint, float*, float*, float*, int> _fnSwigluF32;
        private static delegate* unmanaged[Cdecl]<byte*, float, float*, float*, nuint, nuint, int> _fnGemvFp8;
        private static delegate* unmanaged[Cdecl]<byte*, float*, float*, nuint, nuint, int> _fnGemvQ4_K;
        private static delegate* unmanaged[Cdecl]<nuint, nuint, nuint, nuint, float, float*, int> _fnRoPEF32;
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
                        IntPtr pVersion, pSimd, pGemm, pFlash, pQ4, pQ8;
                        bool hasVersion = NativeLibrary.TryGetExport(_libraryHandle, "zc_version", out pVersion) ||
                                          NativeLibrary.TryGetExport(_libraryHandle, "zerorust_version", out pVersion);
                        bool hasSimd = NativeLibrary.TryGetExport(_libraryHandle, "zc_is_simd_supported", out pSimd) ||
                                       NativeLibrary.TryGetExport(_libraryHandle, "zerorust_is_simd_supported", out pSimd);
                        bool hasGemm = NativeLibrary.TryGetExport(_libraryHandle, "zc_gemm_f32", out pGemm) ||
                                       NativeLibrary.TryGetExport(_libraryHandle, "zerorust_gemm_f32", out pGemm);
                        bool hasFlash = NativeLibrary.TryGetExport(_libraryHandle, "zc_flash_attention_f32", out pFlash) ||
                                        NativeLibrary.TryGetExport(_libraryHandle, "zerorust_flash_attention_f32", out pFlash);
                        bool hasQ4 = NativeLibrary.TryGetExport(_libraryHandle, "zc_gemv_q4_0", out pQ4) ||
                                     NativeLibrary.TryGetExport(_libraryHandle, "zerorust_gemv_q4_0", out pQ4);
                        bool hasQ8 = NativeLibrary.TryGetExport(_libraryHandle, "zc_gemv_q8_0", out pQ8) ||
                                     NativeLibrary.TryGetExport(_libraryHandle, "zerorust_gemv_q8_0", out pQ8);

                        if (hasVersion && hasSimd && hasGemm && hasFlash && hasQ4 && hasQ8)
                        {
                            _fnVersion = (delegate* unmanaged[Cdecl]<uint>)pVersion;
                            _fnIsSimdSupported = (delegate* unmanaged[Cdecl]<int>)pSimd;
                            _fnGemmF32 = (delegate* unmanaged[Cdecl]<nuint, nuint, nuint, float, float*, nint, nint, float*, nint, nint, float, float*, nint, nint, int, int>)pGemm;
                            _fnFlashAttentionF32 = (delegate* unmanaged[Cdecl]<nuint, nuint, nuint, nuint, nuint, float, int, nuint, nuint, nuint, float*, float*, float*, float*, float*, int>)pFlash;
                            _fnGemvQ4_0 = (delegate* unmanaged[Cdecl]<byte*, float*, float*, nuint, nuint, int>)pQ4;
                            _fnGemvQ8_0 = (delegate* unmanaged[Cdecl]<byte*, float*, float*, nuint, nuint, int>)pQ8;

                            if (NativeLibrary.TryGetExport(_libraryHandle, "zc_rmsnorm_f32", out IntPtr pRms))
                                _fnRmsNormF32 = (delegate* unmanaged[Cdecl]<nuint, float*, float*, float*, float, int>)pRms;
                            if (NativeLibrary.TryGetExport(_libraryHandle, "zc_silu_f32", out IntPtr pSilu))
                                _fnSiluF32 = (delegate* unmanaged[Cdecl]<nuint, float*, float*, int>)pSilu;
                            if (NativeLibrary.TryGetExport(_libraryHandle, "zc_swiglu_f32", out IntPtr pSwiglu))
                                _fnSwigluF32 = (delegate* unmanaged[Cdecl]<nuint, float*, float*, float*, int>)pSwiglu;
                            if (NativeLibrary.TryGetExport(_libraryHandle, "zc_gemv_fp8", out IntPtr pFp8))
                                _fnGemvFp8 = (delegate* unmanaged[Cdecl]<byte*, float, float*, float*, nuint, nuint, int>)pFp8;
                            if (NativeLibrary.TryGetExport(_libraryHandle, "zc_gemv_q4_k", out IntPtr pQ4K))
                                _fnGemvQ4_K = (delegate* unmanaged[Cdecl]<byte*, float*, float*, nuint, nuint, int>)pQ4K;
                            if (NativeLibrary.TryGetExport(_libraryHandle, "zc_rope_f32", out IntPtr pRope))
                                _fnRoPEF32 = (delegate* unmanaged[Cdecl]<nuint, nuint, nuint, nuint, float, float*, int>)pRope;

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
            string[] libCandidates = new[] { "zero_compute", "zero_tensor" };
            string baseDir = AppContext.BaseDirectory;

            foreach (var libName in libCandidates)
            {
                // 1. Try standard probe (PATH, AppDomain, etc.)
                if (NativeLibrary.TryLoad(libName, typeof(ZeroTensorNativeBridge).Assembly, null, out IntPtr handle))
                {
                    return handle;
                }

                // 2. Try AppContext.BaseDirectory
                string localDll = Path.Combine(baseDir, $"{libName}.dll");
                if (File.Exists(localDll) && NativeLibrary.TryLoad(localDll, out handle))
                {
                    return handle;
                }

                // 3. Try runtimes/win-x64/native
                string runtimesDll = Path.Combine(baseDir, "runtimes", "win-x64", "native", $"{libName}.dll");
                if (File.Exists(runtimesDll) && NativeLibrary.TryLoad(runtimesDll, out handle))
                {
                    return handle;
                }

                // 4. Try development workspace build (ZeroRust/target/release)
                string[] probePaths = new[]
                {
                    $@"E:\15. Other\ZeroUniverse\ZeroRust\target\release\{libName}.dll",
                    $@"..\..\..\..\ZeroRust\target\release\{libName}.dll",
                    $@"..\..\ZeroRust\target\release\{libName}.dll"
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
            }

            return IntPtr.Zero;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int RmsNorm(int n, float* x, float* weight, float* dst, float eps = 1e-5f)
        {
            if (!_isAvailable || _fnRmsNormF32 == null) return -1;
            return _fnRmsNormF32((nuint)n, x, weight, dst, eps);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int SiLU(int n, float* src, float* dst)
        {
            if (!_isAvailable || _fnSiluF32 == null) return -1;
            return _fnSiluF32((nuint)n, src, dst);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int SwiGLU(int n, float* gate, float* up, float* dst)
        {
            if (!_isAvailable || _fnSwigluF32 == null) return -1;
            return _fnSwigluF32((nuint)n, gate, up, dst);
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GemvFp8(byte* ptrWeight, float scale, float* pAct, float* pDst, int nRows, int kCols)
        {
            if (!_isAvailable || _fnGemvFp8 == null) return -1;
            return _fnGemvFp8(ptrWeight, scale, pAct, pDst, (nuint)nRows, (nuint)kCols);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GemvQ4_K(byte* ptrWeight, float* pAct, float* pDst, int nRows, int kCols)
        {
            if (!_isAvailable || _fnGemvQ4_K == null) return -1;
            return _fnGemvQ4_K(ptrWeight, pAct, pDst, (nuint)nRows, (nuint)kCols);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int RoPE(int numHeads, int seqLen, int headDim, int posOffset, float thetaBase, float* x)
        {
            if (!_isAvailable || _fnRoPEF32 == null) return -1;
            return _fnRoPEF32((nuint)numHeads, (nuint)seqLen, (nuint)headDim, (nuint)posOffset, thetaBase, x);
        }
#else
        public static int RmsNorm(int n, float* x, float* weight, float* dst, float eps = 1e-5f) => -1;
        public static int SiLU(int n, float* src, float* dst) => -1;
        public static int SwiGLU(int n, float* gate, float* up, float* dst) => -1;
        public static int Gemm(int m, int n, int k, float alpha, float* a, int rsa, int csa, float* b, int rsb, int csb, float beta, float* c, int rsc, int csc, bool parallel = true) => -1;
        public static int FlashAttention(int batchCount, int sq, int sk, int d, int dv, float scaleFactor, bool isGqa, int hq, int hkv, int gqaGroup, float* q, float* k, float* v, float* mask, float* output) => -1;
        public static int GemvQ4_0(byte* ptrWeight, float* pAct, float* pDst, int nRows, int kCols) => -1;
        public static int GemvQ8_0(byte* ptrWeight, float* pAct, float* pDst, int nRows, int kCols) => -1;
        public static int GemvFp8(byte* ptrWeight, float scale, float* pAct, float* pDst, int nRows, int kCols) => -1;
        public static int GemvQ4_K(byte* ptrWeight, float* pAct, float* pDst, int nRows, int kCols) => -1;
        public static int RoPE(int numHeads, int seqLen, int headDim, int posOffset, float thetaBase, float* x) => -1;
#endif
    }
}
