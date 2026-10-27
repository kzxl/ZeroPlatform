using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace ZeroCompute.Core.DirectX
{
    /// <summary>
    /// Runtime HLSL compiler utilizing native d3dcompiler_47.dll with in-memory bytecode caching.
    /// Pure C# P/Invoke without external build tools.
    /// </summary>
    internal static class D3D11Compiler
    {
        private const string CompilerDll = "d3dcompiler_47.dll";
        private static readonly ConcurrentDictionary<string, byte[]> _bytecodeCache = new ConcurrentDictionary<string, byte[]>();

        [DllImport(CompilerDll, CallingConvention = CallingConvention.StdCall, SetLastError = true)]
        private static extern int D3DCompile(
            [MarshalAs(UnmanagedType.LPStr)] string srcData,
            UIntPtr srcDataLen,
            [MarshalAs(UnmanagedType.LPStr)] string? sourceName,
            IntPtr pDefines,
            IntPtr pInclude,
            [MarshalAs(UnmanagedType.LPStr)] string entryPoint,
            [MarshalAs(UnmanagedType.LPStr)] string target,
            uint flags1,
            uint flags2,
            out IntPtr ppCode,
            out IntPtr ppErrorMsgs);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate IntPtr GetBufferPointerDelegate(IntPtr thisPtr);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate UIntPtr GetBufferSizeDelegate(IntPtr thisPtr);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate uint ReleaseDelegate(IntPtr thisPtr);

        /// <summary>
        /// Compiles HLSL compute shader source code into Shader Model 5.0 (cs_5_0) bytecode.
        /// </summary>
        public static unsafe byte[] CompileComputeShader(string hlslCode, string entryPoint = "main")
        {
            if (string.IsNullOrEmpty(hlslCode))
                throw new ArgumentNullException(nameof(hlslCode));

            string cacheKey = $"{entryPoint}::{hlslCode.GetHashCode()}";
            if (_bytecodeCache.TryGetValue(cacheKey, out var cached))
                return cached;

            int hr = D3DCompile(
                hlslCode,
                (UIntPtr)hlslCode.Length,
                null,
                IntPtr.Zero,
                IntPtr.Zero,
                entryPoint,
                "cs_5_0",
                0,
                0,
                out IntPtr ppCode,
                out IntPtr ppError);

            if (hr < 0 || ppCode == IntPtr.Zero)
            {
                string errorMsg = "Unknown compilation error";
                if (ppError != IntPtr.Zero)
                {
                    try
                    {
                        IntPtr methodPtr = (*(IntPtr**)ppError)[3];
                        IntPtr errPtr = Marshal.GetDelegateForFunctionPointer<GetBufferPointerDelegate>(methodPtr)(ppError);
                        errorMsg = Marshal.PtrToStringAnsi(errPtr) ?? errorMsg;
                    }
                    finally
                    {
                        IntPtr relPtr = (*(IntPtr**)ppError)[2];
                        Marshal.GetDelegateForFunctionPointer<ReleaseDelegate>(relPtr)(ppError);
                    }
                }
                throw new InvalidOperationException($"HLSL Compute Shader compilation failed (0x{hr:X8}): {errorMsg}");
            }

            try
            {
                IntPtr pBufMethod = (*(IntPtr**)ppCode)[3];
                IntPtr pBuf = Marshal.GetDelegateForFunctionPointer<GetBufferPointerDelegate>(pBufMethod)(ppCode);

                IntPtr pSizeMethod = (*(IntPtr**)ppCode)[4];
                UIntPtr size = Marshal.GetDelegateForFunctionPointer<GetBufferSizeDelegate>(pSizeMethod)(ppCode);

                byte[] bytecode = new byte[(int)size];
                Marshal.Copy(pBuf, bytecode, 0, bytecode.Length);

                _bytecodeCache[cacheKey] = bytecode;
                return bytecode;
            }
            finally
            {
                IntPtr relMethod = (*(IntPtr**)ppCode)[2];
                Marshal.GetDelegateForFunctionPointer<ReleaseDelegate>(relMethod)(ppCode);
            }
        }
    }
}
