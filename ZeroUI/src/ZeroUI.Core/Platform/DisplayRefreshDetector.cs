using System;
using System.Runtime.InteropServices;

namespace ZeroUI.Core.Platform
{
    /// <summary>
    /// Utility for detecting the current physical display refresh rate (in Hz) on Windows.
    /// Supports automatic adaptive framerate clamping (e.g. 60Hz, 120Hz, 144Hz, 240Hz).
    /// </summary>
    public static class DisplayRefreshDetector
    {
        private const int VREFRESH = 116;
        private const int ENUM_CURRENT_SETTINGS = -1;

        [DllImport("user32.dll", ExactSpelling = true)]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll", ExactSpelling = true)]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll", ExactSpelling = true)]
        private static extern int GetDeviceCaps(IntPtr hdc, int nIndex);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct DEVMODE
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmDeviceName;
            public short dmSpecVersion;
            public short dmDriverVersion;
            public short dmSize;
            public short dmDriverExtra;
            public int dmFields;
            public int dmPositionX;
            public int dmPositionY;
            public int dmDisplayOrientation;
            public int dmDisplayFixedOutput;
            public short dmColor;
            public short dmDuplex;
            public short dmYResolution;
            public short dmTTOption;
            public short dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmFormName;
            public short dmLogPixels;
            public short dmBitsPerPel;
            public int dmPelsWidth;
            public int dmPelsHeight;
            public int dmDisplayFlags;
            public int dmDisplayFrequency;
            public int dmICMMethod;
            public int dmICMIntent;
            public int dmMediaType;
            public int dmDitherType;
            public int dmReserved1;
            public int dmReserved2;
            public int dmPanningWidth;
            public int dmPanningHeight;
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool EnumDisplaySettings(string? lpszDeviceName, int iModeNum, ref DEVMODE lpDevMode);

        /// <summary>
        /// Queries the primary display refresh rate (in Hz).
        /// Returns clamped value between 30 and 240 Hz. Defaults to 60 Hz if detection fails or on non-Windows.
        /// </summary>
        public static int GetPrimaryDisplayRefreshRate(int defaultFallbackHz = 60)
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                return defaultFallbackHz;
            }

            try
            {
                // Primary strategy: EnumDisplaySettings
                DEVMODE dm = default;
                dm.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));
                if (EnumDisplaySettings(null, ENUM_CURRENT_SETTINGS, ref dm) && dm.dmDisplayFrequency > 1)
                {
                    return ClampFrequency(dm.dmDisplayFrequency);
                }

                // Secondary strategy: GetDeviceCaps(VREFRESH)
                IntPtr hdc = GetDC(IntPtr.Zero);
                if (hdc != IntPtr.Zero)
                {
                    try
                    {
                        int rate = GetDeviceCaps(hdc, VREFRESH);
                        if (rate > 1)
                        {
                            return ClampFrequency(rate);
                        }
                    }
                    finally
                    {
                        ReleaseDC(IntPtr.Zero, hdc);
                    }
                }
            }
            catch
            {
                // Graceful fallback
            }

            return defaultFallbackHz;
        }

        private static int ClampFrequency(int hz)
        {
            // Windows commonly reports 59 Hz for 59.94 Hz NTSC displays -> treat 59 as 60
            if (hz == 59) return 60;
            if (hz == 119) return 120;
            if (hz == 143) return 144;
            if (hz == 239) return 240;

            return Math.Min(240, Math.Max(30, hz));
        }
    }
}
