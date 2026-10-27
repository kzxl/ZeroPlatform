using System;
using System.Runtime.InteropServices;

namespace ZeroUI.WinForms.Native
{
    public static unsafe class NativeMethods
    {
        public const int BI_RGB = 0;
        public const int DIB_RGB_COLORS = 0;
        public const int SRCCOPY = 0x00CC0020;
        public const int TRANSPARENT = 1;
        public const int OPAQUE = 2;

        public const uint ETO_OPAQUE = 0x0002;
        public const uint ETO_CLIPPED = 0x0004;

        public const int SB_HORZ = 0;
        public const int SB_VERT = 1;

        public const uint SIF_RANGE = 0x0001;
        public const uint SIF_PAGE = 0x0002;
        public const uint SIF_POS = 0x0004;
        public const uint SIF_TRACKPOS = 0x0010;
        public const uint SIF_ALL = SIF_RANGE | SIF_PAGE | SIF_POS | SIF_TRACKPOS;

        public const int SB_LINEUP = 0;
        public const int SB_LINEDOWN = 1;
        public const int SB_PAGEUP = 2;
        public const int SB_PAGEDOWN = 3;
        public const int SB_THUMBPOSITION = 4;
        public const int SB_THUMBTRACK = 5;
        public const int SB_TOP = 6;
        public const int SB_BOTTOM = 7;
        public const int SB_ENDSCROLL = 8;

        public const int WS_VSCROLL = 0x00200000;
        public const int WS_HSCROLL = 0x00100000;

        public const int WM_SIZE = 0x0005;
        public const int WM_ERASEBKGND = 0x0014;
        public const int WM_PAINT = 0x000F;
        public const int WM_HSCROLL = 0x0114;
        public const int WM_VSCROLL = 0x0115;
        public const int WM_MOUSEWHEEL = 0x020A;
        public const int WM_SETCURSOR = 0x0020;


        [DllImport("gdi32.dll", SetLastError = true)]
        public static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll", SetLastError = true)]
        public static extern IntPtr CreateDIBSection(
            IntPtr hdc,
            ref BITMAPINFO pbmi,
            uint iUsage,
            out IntPtr ppvBits,
            IntPtr hSection,
            uint dwOffset);

        [DllImport("gdi32.dll", ExactSpelling = true, SetLastError = true)]
        public static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

        [DllImport("gdi32.dll", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DeleteObject(IntPtr hObject);

        [DllImport("gdi32.dll", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool BitBlt(
            IntPtr hdcDest,
            int nXDest,
            int nYDest,
            int nWidth,
            int nHeight,
            IntPtr hdcSrc,
            int nXSrc,
            int nYSrc,
            int dwRop);

        [DllImport("gdi32.dll", EntryPoint = "ExtTextOutW", SetLastError = true, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ExtTextOut(
            IntPtr hdc,
            int X,
            int Y,
            uint fuOptions,
            ref RECT lprc,
            char* lpString,
            uint cbCount,
            int* lpDx);

        [DllImport("gdi32.dll", EntryPoint = "GetTextExtentPoint32W", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetTextExtentPoint32(
            IntPtr hdc,
            char* lpString,
            int c,
            out SIZE lpSize);


        [DllImport("gdi32.dll", ExactSpelling = true)]
        public static extern int SetBkMode(IntPtr hdc, int iBkMode);

        [DllImport("gdi32.dll", ExactSpelling = true)]
        public static extern uint SetTextColor(IntPtr hdc, uint crColor);

        [DllImport("gdi32.dll", ExactSpelling = true)]
        public static extern uint SetBkColor(IntPtr hdc, uint crColor);

        [DllImport("gdi32.dll", ExactSpelling = true)]
        public static extern IntPtr CreateSolidBrush(uint crColor);

        [DllImport("user32.dll", ExactSpelling = true)]
        public static extern int FillRect(IntPtr hdc, ref RECT lprc, IntPtr hbr);

        [DllImport("user32.dll", ExactSpelling = true)]
        public static extern int SetScrollInfo(IntPtr hwnd, int fnBar, ref SCROLLINFO lpsi, [MarshalAs(UnmanagedType.Bool)] bool fRedraw);

        [DllImport("user32.dll", ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetScrollInfo(IntPtr hwnd, int fnBar, ref SCROLLINFO lpsi);

        [DllImport("user32.dll", ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ShowScrollBar(IntPtr hWnd, int wBar, [MarshalAs(UnmanagedType.Bool)] bool bShow);

        public const int VREFRESH = 116;

        [DllImport("gdi32.dll", ExactSpelling = true)]
        public static extern int GetDeviceCaps(IntPtr hdc, int nIndex);

        [DllImport("user32.dll", ExactSpelling = true)]
        public static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll", ExactSpelling = true)]
        public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("user32.dll", ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool InvalidateRect(IntPtr hWnd, ref RECT lpRect, [MarshalAs(UnmanagedType.Bool)] bool bErase);
    }
}
