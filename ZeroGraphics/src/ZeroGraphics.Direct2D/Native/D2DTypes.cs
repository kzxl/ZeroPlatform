using System;
using System.Runtime.InteropServices;

namespace ZeroGraphics.Direct2D.Native
{
    public enum D2D1_FACTORY_TYPE : int
    {
        D2D1_FACTORY_TYPE_SINGLE_THREADED = 0,
        D2D1_FACTORY_TYPE_MULTI_THREADED = 1
    }

    public enum D2D1_RENDER_TARGET_TYPE : int
    {
        D2D1_RENDER_TARGET_TYPE_DEFAULT = 0,
        D2D1_RENDER_TARGET_TYPE_SOFTWARE = 1,
        D2D1_RENDER_TARGET_TYPE_HARDWARE = 2
    }

    [Flags]
    public enum D2D1_RENDER_TARGET_USAGE : int
    {
        D2D1_RENDER_TARGET_USAGE_NONE = 0,
        D2D1_RENDER_TARGET_USAGE_FORCE_BITMAP_REMOTING = 1,
        D2D1_RENDER_TARGET_USAGE_GDI_COMPATIBLE = 2
    }

    public enum D2D1_FEATURE_LEVEL : int
    {
        D2D1_FEATURE_LEVEL_DEFAULT = 0,
        D2D1_FEATURE_LEVEL_9 = 0x9100,
        D2D1_FEATURE_LEVEL_10 = 0xa000
    }

    [Flags]
    public enum D2D1_PRESENT_OPTIONS : int
    {
        D2D1_PRESENT_OPTIONS_NONE = 0,
        D2D1_PRESENT_OPTIONS_RETAIN_CONTENTS = 1,
        D2D1_PRESENT_OPTIONS_IMMEDIATELY = 2
    }

    public enum D2D1_ANTIALIAS_MODE : int
    {
        D2D1_ANTIALIAS_MODE_PER_PRIMITIVE = 0,
        D2D1_ANTIALIAS_MODE_ALIASED = 1
    }

    public enum D2D1_TEXT_ANTIALIAS_MODE : int
    {
        D2D1_TEXT_ANTIALIAS_MODE_DEFAULT = 0,
        D2D1_TEXT_ANTIALIAS_MODE_CLEARTYPE = 1,
        D2D1_TEXT_ANTIALIAS_MODE_GRAYSCALE = 2,
        D2D1_TEXT_ANTIALIAS_MODE_ALIASED = 3
    }

    [Flags]
    public enum D2D1_DRAW_TEXT_OPTIONS : int
    {
        D2D1_DRAW_TEXT_OPTIONS_NO_SNAP = 1,
        D2D1_DRAW_TEXT_OPTIONS_CLIP = 2,
        D2D1_DRAW_TEXT_OPTIONS_NONE = 0,
        D2D1_DRAW_TEXT_OPTIONS_ENABLE_COLOR_FONT = 4
    }

    public enum D2D1_ALPHA_MODE : int
    {
        D2D1_ALPHA_MODE_UNKNOWN = 0,
        D2D1_ALPHA_MODE_PREMULTIPLIED = 1,
        D2D1_ALPHA_MODE_STRAIGHT = 2,
        D2D1_ALPHA_MODE_IGNORE = 3
    }

    public enum DWRITE_FACTORY_TYPE : int
    {
        DWRITE_FACTORY_TYPE_SHARED = 0,
        DWRITE_FACTORY_TYPE_ISOLATED = 1
    }

    public enum DWRITE_FONT_WEIGHT : int
    {
        DWRITE_FONT_WEIGHT_THIN = 100,
        DWRITE_FONT_WEIGHT_NORMAL = 400,
        DWRITE_FONT_WEIGHT_MEDIUM = 500,
        DWRITE_FONT_WEIGHT_SEMI_BOLD = 600,
        DWRITE_FONT_WEIGHT_BOLD = 700,
        DWRITE_FONT_WEIGHT_BLACK = 900
    }

    public enum DWRITE_FONT_STYLE : int
    {
        DWRITE_FONT_STYLE_NORMAL = 0,
        DWRITE_FONT_STYLE_OBLIQUE = 1,
        DWRITE_FONT_STYLE_ITALIC = 2
    }

    public enum DWRITE_FONT_STRETCH : int
    {
        DWRITE_FONT_STRETCH_UNDEFINED = 0,
        DWRITE_FONT_STRETCH_NORMAL = 5
    }

    public enum DWRITE_MEASURING_MODE : int
    {
        DWRITE_MEASURING_MODE_NATURAL = 0,
        DWRITE_MEASURING_MODE_GDI_CLASSIC = 1,
        DWRITE_MEASURING_MODE_GDI_NATURAL = 2
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct D2D1_COLOR_F
    {
        public float R, G, B, A;

        public D2D1_COLOR_F(float r, float g, float b, float a = 1.0f)
        {
            R = r; G = g; B = b; A = a;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct D2D1_POINT_2F
    {
        public float X, Y;
        public D2D1_POINT_2F(float x, float y) { X = x; Y = y; }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct D2D1_RECT_F
    {
        public float Left, Top, Right, Bottom;
        public D2D1_RECT_F(float left, float top, float right, float bottom)
        {
            Left = left; Top = top; Right = right; Bottom = bottom;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct D2D1_ROUNDED_RECT
    {
        public D2D1_RECT_F Rect;
        public float RadiusX;
        public float RadiusY;

        public D2D1_ROUNDED_RECT(D2D1_RECT_F rect, float radiusX, float radiusY)
        {
            Rect = rect;
            RadiusX = radiusX;
            RadiusY = radiusY;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct D2D1_SIZE_U
    {
        public uint Width, Height;
        public D2D1_SIZE_U(uint width, uint height) { Width = width; Height = height; }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct D2D1_PIXEL_FORMAT
    {
        public uint Format;
        public int AlphaMode;

        public D2D1_PIXEL_FORMAT(uint format = 0, int alphaMode = 0)
        {
            Format = format;
            AlphaMode = alphaMode;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct D2D1_RENDER_TARGET_PROPERTIES
    {
        public D2D1_RENDER_TARGET_TYPE Type;
        public D2D1_PIXEL_FORMAT PixelFormat;
        public float DpiX;
        public float DpiY;
        public D2D1_RENDER_TARGET_USAGE Usage;
        public D2D1_FEATURE_LEVEL MinLevel;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct D2D1_HWND_RENDER_TARGET_PROPERTIES
    {
        public IntPtr Hwnd;
        public D2D1_SIZE_U PixelSize;
        public D2D1_PRESENT_OPTIONS PresentOptions;
    }
}
