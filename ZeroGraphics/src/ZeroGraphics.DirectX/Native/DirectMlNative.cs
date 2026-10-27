using System;
using System.Runtime.InteropServices;

namespace ZeroGraphics.DirectX.Native
{
    [Flags]
    public enum DML_CREATE_DEVICE_FLAGS : uint
    {
        DML_CREATE_DEVICE_FLAG_NONE = 0x0,
        DML_CREATE_DEVICE_FLAG_DEBUG = 0x1
    }

    public enum DML_FEATURE_LEVEL : uint
    {
        DML_FEATURE_LEVEL_1_0 = 0x1000,
        DML_FEATURE_LEVEL_2_0 = 0x2000,
        DML_FEATURE_LEVEL_2_1 = 0x2100,
        DML_FEATURE_LEVEL_3_0 = 0x3000,
        DML_FEATURE_LEVEL_4_0 = 0x4000,
        DML_FEATURE_LEVEL_5_0 = 0x5000
    }

    /// <summary>
    /// Pure C# COM VTable P/Invoke for DirectML (directml.dll).
    /// Provides zero-dependency hardware-accelerated neural tensor computing on Windows.
    /// </summary>
    public static class DirectMlNative
    {
        public static readonly Guid IID_IDMLDevice = new Guid("aab29789-9150-45c0-a7d3-acf4988c558c");

        private static bool? _isAvailable;

        /// <summary>
        /// Gets whether directml.dll is available on the current Windows installation.
        /// </summary>
        public static bool IsAvailable
        {
            get
            {
                if (!_isAvailable.HasValue)
                {
                    try
                    {
                        IntPtr hModule = LoadLibrary("directml.dll");
                        if (hModule != IntPtr.Zero)
                        {
                            FreeLibrary(hModule);
                            _isAvailable = true;
                        }
                        else
                        {
                            _isAvailable = false;
                        }
                    }
                    catch
                    {
                        _isAvailable = false;
                    }
                }
                return _isAvailable.Value;
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool FreeLibrary(IntPtr hModule);

        [DllImport("directml.dll", EntryPoint = "DMLCreateDevice1", CallingConvention = CallingConvention.StdCall)]
        public static extern int DMLCreateDevice1(
            IntPtr d3d11Device,
            DML_CREATE_DEVICE_FLAGS flags,
            DML_FEATURE_LEVEL minFeatureLevel,
            ref Guid riid,
            out IntPtr ppDmlDevice);
    }
}
