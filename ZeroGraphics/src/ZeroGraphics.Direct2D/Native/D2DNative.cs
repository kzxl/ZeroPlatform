using System;
using System.Runtime.InteropServices;

namespace ZeroGraphics.Direct2D.Native
{
    public static class D2DNative
    {
        public static readonly Guid IID_ID2D1Factory = new Guid("06152247-6f50-465a-9245-118bfd3b6007");
        public static readonly Guid IID_IDWriteFactory = new Guid("b859ee5a-d838-4b5b-a2e8-1adc7d93db48");

        [DllImport("d2d1.dll", CallingConvention = CallingConvention.StdCall, SetLastError = false)]
        public static extern int D2D1CreateFactory(
            D2D1_FACTORY_TYPE factoryType,
            [In] ref Guid riid,
            IntPtr pFactoryOptions,
            out IntPtr ppIFactory);

        [DllImport("dwrite.dll", CallingConvention = CallingConvention.StdCall, SetLastError = false)]
        public static extern int DWriteCreateFactory(
            DWRITE_FACTORY_TYPE factoryType,
            [In] ref Guid iid,
            out IntPtr ppFactory);
    }

    public static unsafe class D2DComVTable
    {
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate uint ReleaseDelegate(IntPtr thisPtr);

        public static uint Release(IntPtr comPtr)
        {
            if (comPtr == IntPtr.Zero) return 0;
            IntPtr methodPtr = (*(IntPtr**)comPtr)[2];
            return Marshal.GetDelegateForFunctionPointer<ReleaseDelegate>(methodPtr)(comPtr);
        }

        // =========================================================================
        // ID2D1Factory
        // =========================================================================

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate int CreateHwndRenderTargetDelegate(
            IntPtr thisPtr,
            ref D2D1_RENDER_TARGET_PROPERTIES renderTargetProperties,
            ref D2D1_HWND_RENDER_TARGET_PROPERTIES hwndRenderTargetProperties,
            out IntPtr hwndRenderTarget);

        public static int CreateHwndRenderTarget(
            IntPtr factory,
            ref D2D1_RENDER_TARGET_PROPERTIES rtProps,
            ref D2D1_HWND_RENDER_TARGET_PROPERTIES hwndProps,
            out IntPtr ppHwndRT)
        {
            IntPtr methodPtr = (*(IntPtr**)factory)[14];
            return Marshal.GetDelegateForFunctionPointer<CreateHwndRenderTargetDelegate>(methodPtr)(
                factory, ref rtProps, ref hwndProps, out ppHwndRT);
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate int CreateDxgiSurfaceRenderTargetDelegate(
            IntPtr thisPtr,
            IntPtr dxgiSurface,
            ref D2D1_RENDER_TARGET_PROPERTIES renderTargetProperties,
            out IntPtr renderTarget);

        public static int CreateDxgiSurfaceRenderTarget(
            IntPtr factory,
            IntPtr dxgiSurface,
            ref D2D1_RENDER_TARGET_PROPERTIES rtProps,
            out IntPtr ppRT)
        {
            IntPtr methodPtr = (*(IntPtr**)factory)[15];
            return Marshal.GetDelegateForFunctionPointer<CreateDxgiSurfaceRenderTargetDelegate>(methodPtr)(
                factory, dxgiSurface, ref rtProps, out ppRT);
        }

        // =========================================================================
        // IDWriteFactory
        // =========================================================================

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        public delegate int CreateTextFormatDelegate(
            IntPtr thisPtr,
            [MarshalAs(UnmanagedType.LPWStr)] string fontFamilyName,
            IntPtr fontCollection,
            DWRITE_FONT_WEIGHT fontWeight,
            DWRITE_FONT_STYLE fontStyle,
            DWRITE_FONT_STRETCH fontStretch,
            float fontSize,
            [MarshalAs(UnmanagedType.LPWStr)] string localeName,
            out IntPtr textFormat);

        public static int CreateTextFormat(
            IntPtr dwriteFactory,
            string fontFamilyName,
            DWRITE_FONT_WEIGHT fontWeight,
            DWRITE_FONT_STYLE fontStyle,
            DWRITE_FONT_STRETCH fontStretch,
            float fontSize,
            string localeName,
            out IntPtr ppTextFormat)
        {
            IntPtr methodPtr = (*(IntPtr**)dwriteFactory)[15];
            return Marshal.GetDelegateForFunctionPointer<CreateTextFormatDelegate>(methodPtr)(
                dwriteFactory, fontFamilyName, IntPtr.Zero, fontWeight, fontStyle, fontStretch, fontSize, localeName, out ppTextFormat);
        }

        // =========================================================================
        // ID2D1RenderTarget / ID2D1HwndRenderTarget
        // =========================================================================

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate int CreateSolidColorBrushDelegate(
            IntPtr thisPtr,
            ref D2D1_COLOR_F color,
            IntPtr brushProperties,
            out IntPtr solidColorBrush);

        public static int CreateSolidColorBrush(IntPtr rt, ref D2D1_COLOR_F color, out IntPtr ppBrush)
        {
            IntPtr methodPtr = (*(IntPtr**)rt)[8];
            return Marshal.GetDelegateForFunctionPointer<CreateSolidColorBrushDelegate>(methodPtr)(
                rt, ref color, IntPtr.Zero, out ppBrush);
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void DrawLineDelegate(
            IntPtr thisPtr,
            D2D1_POINT_2F point0,
            D2D1_POINT_2F point1,
            IntPtr brush,
            float strokeWidth,
            IntPtr strokeStyle);

        public static void DrawLine(IntPtr rt, D2D1_POINT_2F p0, D2D1_POINT_2F p1, IntPtr brush, float strokeWidth)
        {
            IntPtr methodPtr = (*(IntPtr**)rt)[15];
            Marshal.GetDelegateForFunctionPointer<DrawLineDelegate>(methodPtr)(rt, p0, p1, brush, strokeWidth, IntPtr.Zero);
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void DrawRectangleDelegate(
            IntPtr thisPtr,
            ref D2D1_RECT_F rect,
            IntPtr brush,
            float strokeWidth,
            IntPtr strokeStyle);

        public static void DrawRectangle(IntPtr rt, ref D2D1_RECT_F rect, IntPtr brush, float strokeWidth)
        {
            IntPtr methodPtr = (*(IntPtr**)rt)[16];
            Marshal.GetDelegateForFunctionPointer<DrawRectangleDelegate>(methodPtr)(rt, ref rect, brush, strokeWidth, IntPtr.Zero);
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void FillRectangleDelegate(
            IntPtr thisPtr,
            ref D2D1_RECT_F rect,
            IntPtr brush);

        public static void FillRectangle(IntPtr rt, ref D2D1_RECT_F rect, IntPtr brush)
        {
            IntPtr methodPtr = (*(IntPtr**)rt)[17];
            Marshal.GetDelegateForFunctionPointer<FillRectangleDelegate>(methodPtr)(rt, ref rect, brush);
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void DrawRoundedRectangleDelegate(
            IntPtr thisPtr,
            ref D2D1_ROUNDED_RECT roundedRect,
            IntPtr brush,
            float strokeWidth,
            IntPtr strokeStyle);

        public static void DrawRoundedRectangle(IntPtr rt, ref D2D1_ROUNDED_RECT roundedRect, IntPtr brush, float strokeWidth)
        {
            IntPtr methodPtr = (*(IntPtr**)rt)[18];
            Marshal.GetDelegateForFunctionPointer<DrawRoundedRectangleDelegate>(methodPtr)(rt, ref roundedRect, brush, strokeWidth, IntPtr.Zero);
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void FillRoundedRectangleDelegate(
            IntPtr thisPtr,
            ref D2D1_ROUNDED_RECT roundedRect,
            IntPtr brush);

        public static void FillRoundedRectangle(IntPtr rt, ref D2D1_ROUNDED_RECT roundedRect, IntPtr brush)
        {
            IntPtr methodPtr = (*(IntPtr**)rt)[19];
            Marshal.GetDelegateForFunctionPointer<FillRoundedRectangleDelegate>(methodPtr)(rt, ref roundedRect, brush);
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        public delegate void DrawTextDelegate(
            IntPtr thisPtr,
            char* stringPtr,
            uint stringLength,
            IntPtr textFormat,
            ref D2D1_RECT_F layoutRect,
            IntPtr defaultFillBrush,
            D2D1_DRAW_TEXT_OPTIONS options,
            DWRITE_MEASURING_MODE measuringMode);

        public static void DrawText(
            IntPtr rt,
            string text,
            IntPtr textFormat,
            ref D2D1_RECT_F layoutRect,
            IntPtr defaultFillBrush,
            D2D1_DRAW_TEXT_OPTIONS options = D2D1_DRAW_TEXT_OPTIONS.D2D1_DRAW_TEXT_OPTIONS_NONE)
        {
            if (string.IsNullOrEmpty(text)) return;
            fixed (char* pText = text)
            {
                IntPtr methodPtr = (*(IntPtr**)rt)[27];
                Marshal.GetDelegateForFunctionPointer<DrawTextDelegate>(methodPtr)(
                    rt, pText, (uint)text.Length, textFormat, ref layoutRect, defaultFillBrush, options, DWRITE_MEASURING_MODE.DWRITE_MEASURING_MODE_NATURAL);
            }
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void ClearDelegate(IntPtr thisPtr, ref D2D1_COLOR_F clearColor);

        public static void Clear(IntPtr rt, ref D2D1_COLOR_F clearColor)
        {
            IntPtr methodPtr = (*(IntPtr**)rt)[47];
            Marshal.GetDelegateForFunctionPointer<ClearDelegate>(methodPtr)(rt, ref clearColor);
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void BeginDrawDelegate(IntPtr thisPtr);

        public static void BeginDraw(IntPtr rt)
        {
            IntPtr methodPtr = (*(IntPtr**)rt)[48];
            Marshal.GetDelegateForFunctionPointer<BeginDrawDelegate>(methodPtr)(rt);
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate int EndDrawDelegate(IntPtr thisPtr, out ulong tag1, out ulong tag2);

        public static int EndDraw(IntPtr rt)
        {
            IntPtr methodPtr = (*(IntPtr**)rt)[49];
            return Marshal.GetDelegateForFunctionPointer<EndDrawDelegate>(methodPtr)(rt, out _, out _);
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void SetDpiDelegate(IntPtr thisPtr, float dpiX, float dpiY);

        public static void SetDpi(IntPtr rt, float dpiX, float dpiY)
        {
            if (rt == IntPtr.Zero) return;
            IntPtr methodPtr = (*(IntPtr**)rt)[51];
            Marshal.GetDelegateForFunctionPointer<SetDpiDelegate>(methodPtr)(rt, dpiX, dpiY);
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void GetDpiDelegate(IntPtr thisPtr, out float dpiX, out float dpiY);

        public static void GetDpi(IntPtr rt, out float dpiX, out float dpiY)
        {
            if (rt == IntPtr.Zero)
            {
                dpiX = 96.0f;
                dpiY = 96.0f;
                return;
            }
            IntPtr methodPtr = (*(IntPtr**)rt)[52];
            Marshal.GetDelegateForFunctionPointer<GetDpiDelegate>(methodPtr)(rt, out dpiX, out dpiY);
        }


        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate int ResizeDelegate(IntPtr thisPtr, ref D2D1_SIZE_U pixelSize);

        public static int Resize(IntPtr hwndRt, ref D2D1_SIZE_U pixelSize)
        {
            IntPtr methodPtr = (*(IntPtr**)hwndRt)[58];
            return Marshal.GetDelegateForFunctionPointer<ResizeDelegate>(methodPtr)(hwndRt, ref pixelSize);
        }
    }
}
