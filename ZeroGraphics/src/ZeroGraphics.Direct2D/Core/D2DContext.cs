using System;
using System.Drawing;
using System.Runtime.InteropServices;
using ZeroGraphics.Direct2D.Native;

namespace ZeroGraphics.Direct2D.Core
{
    public abstract class D2DResourceWrapper : IDisposable
    {
        private IntPtr _handle;
        private bool _disposed;

        public IntPtr Handle => _handle;
        public bool IsValid => _handle != IntPtr.Zero && !_disposed;

        protected D2DResourceWrapper(IntPtr handle)
        {
            _handle = handle;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (_handle != IntPtr.Zero)
                {
                    D2DComVTable.Release(_handle);
                    _handle = IntPtr.Zero;
                }
                _disposed = true;
            }
        }

        ~D2DResourceWrapper()
        {
            Dispose(false);
        }
    }

    public sealed class D2DFactory : D2DResourceWrapper
    {
        private static readonly Lazy<D2DFactory> _instance = new Lazy<D2DFactory>(() =>
        {
            Guid iid = D2DNative.IID_ID2D1Factory;
            int hr = D2DNative.D2D1CreateFactory(D2D1_FACTORY_TYPE.D2D1_FACTORY_TYPE_SINGLE_THREADED, ref iid, IntPtr.Zero, out IntPtr ppFactory);
            if (hr < 0 || ppFactory == IntPtr.Zero)
                throw new COMException("Failed to create Direct2D Factory.", hr);

            return new D2DFactory(ppFactory);
        });

        public static D2DFactory Default => _instance.Value;

        public D2DFactory(IntPtr handle) : base(handle) { }

        public D2DHwndRenderTarget CreateHwndRenderTarget(IntPtr hwnd, int width, int height)
        {
            D2D1_RENDER_TARGET_PROPERTIES rtProps = new D2D1_RENDER_TARGET_PROPERTIES
            {
                Type = D2D1_RENDER_TARGET_TYPE.D2D1_RENDER_TARGET_TYPE_DEFAULT,
                PixelFormat = new D2D1_PIXEL_FORMAT(0, 0),
                DpiX = 96.0f,
                DpiY = 96.0f,
                Usage = D2D1_RENDER_TARGET_USAGE.D2D1_RENDER_TARGET_USAGE_NONE,
                MinLevel = D2D1_FEATURE_LEVEL.D2D1_FEATURE_LEVEL_DEFAULT
            };

            D2D1_HWND_RENDER_TARGET_PROPERTIES hwndProps = new D2D1_HWND_RENDER_TARGET_PROPERTIES
            {
                Hwnd = hwnd,
                PixelSize = new D2D1_SIZE_U((uint)Math.Max(1, width), (uint)Math.Max(1, height)),
                PresentOptions = D2D1_PRESENT_OPTIONS.D2D1_PRESENT_OPTIONS_NONE
            };

            int hr = D2DComVTable.CreateHwndRenderTarget(Handle, ref rtProps, ref hwndProps, out IntPtr ppHwndRT);
            if (hr < 0 || ppHwndRT == IntPtr.Zero)
                throw new COMException("Failed to create Direct2D HwndRenderTarget.", hr);

            return new D2DHwndRenderTarget(ppHwndRT);
        }

        public D2DDxgiRenderTarget CreateDxgiSurfaceRenderTarget(IntPtr dxgiSurface, float dpiX = 96.0f, float dpiY = 96.0f)
        {
            if (dxgiSurface == IntPtr.Zero)
                throw new ArgumentException("Invalid DXGI surface handle.", nameof(dxgiSurface));

            D2D1_RENDER_TARGET_PROPERTIES rtProps = new D2D1_RENDER_TARGET_PROPERTIES
            {
                Type = D2D1_RENDER_TARGET_TYPE.D2D1_RENDER_TARGET_TYPE_DEFAULT,
                PixelFormat = new D2D1_PIXEL_FORMAT(0, (int)D2D1_ALPHA_MODE.D2D1_ALPHA_MODE_PREMULTIPLIED),
                DpiX = dpiX,
                DpiY = dpiY,
                Usage = D2D1_RENDER_TARGET_USAGE.D2D1_RENDER_TARGET_USAGE_NONE,
                MinLevel = D2D1_FEATURE_LEVEL.D2D1_FEATURE_LEVEL_DEFAULT
            };

            int hr = D2DComVTable.CreateDxgiSurfaceRenderTarget(Handle, dxgiSurface, ref rtProps, out IntPtr ppRT);
            if (hr < 0 || ppRT == IntPtr.Zero)
                throw new COMException("Failed to create Direct2D DxgiSurfaceRenderTarget.", hr);

            return new D2DDxgiRenderTarget(ppRT);
        }
    }

    public sealed class DWriteFactory : D2DResourceWrapper
    {
        private static readonly Lazy<DWriteFactory> _instance = new Lazy<DWriteFactory>(() =>
        {
            Guid iid = D2DNative.IID_IDWriteFactory;
            int hr = D2DNative.DWriteCreateFactory(DWRITE_FACTORY_TYPE.DWRITE_FACTORY_TYPE_SHARED, ref iid, out IntPtr ppFactory);
            if (hr < 0 || ppFactory == IntPtr.Zero)
                throw new COMException("Failed to create DirectWrite Factory.", hr);

            return new DWriteFactory(ppFactory);
        });

        public static DWriteFactory Default => _instance.Value;

        public DWriteFactory(IntPtr handle) : base(handle) { }

        public DWriteTextFormat CreateTextFormat(
            string fontFamilyName,
            float fontSize,
            DWRITE_FONT_WEIGHT weight = DWRITE_FONT_WEIGHT.DWRITE_FONT_WEIGHT_NORMAL,
            DWRITE_FONT_STYLE style = DWRITE_FONT_STYLE.DWRITE_FONT_STYLE_NORMAL,
            string localeName = "en-US")
        {
            int hr = D2DComVTable.CreateTextFormat(
                Handle,
                fontFamilyName,
                weight,
                style,
                DWRITE_FONT_STRETCH.DWRITE_FONT_STRETCH_NORMAL,
                fontSize,
                localeName,
                out IntPtr ppFormat);

            if (hr < 0 || ppFormat == IntPtr.Zero)
                throw new COMException($"Failed to create DirectWrite TextFormat for '{fontFamilyName}'.", hr);

            return new DWriteTextFormat(ppFormat, fontFamilyName, fontSize);
        }
    }

    public sealed class DWriteTextFormat : D2DResourceWrapper
    {
        public string FontFamilyName { get; }
        public float FontSize { get; }

        public DWriteTextFormat(IntPtr handle, string fontFamilyName, float fontSize) : base(handle)
        {
            FontFamilyName = fontFamilyName;
            FontSize = fontSize;
        }
    }

    public sealed class D2DSolidColorBrush : D2DResourceWrapper
    {
        public D2D1_COLOR_F Color { get; }

        public D2DSolidColorBrush(IntPtr handle, D2D1_COLOR_F color) : base(handle)
        {
            Color = color;
        }
    }

    public class D2DRenderTarget : D2DResourceWrapper
    {
        public D2DRenderTarget(IntPtr handle) : base(handle) { }

        public D2DSolidColorBrush CreateSolidColorBrush(Color color)
        {
            D2D1_COLOR_F c = new D2D1_COLOR_F(color.R / 255.0f, color.G / 255.0f, color.B / 255.0f, color.A / 255.0f);
            int hr = D2DComVTable.CreateSolidColorBrush(Handle, ref c, out IntPtr ppBrush);
            if (hr < 0 || ppBrush == IntPtr.Zero)
                throw new COMException("Failed to create SolidColorBrush.", hr);

            return new D2DSolidColorBrush(ppBrush, c);
        }

        public void BeginDraw() => D2DComVTable.BeginDraw(Handle);

        public int EndDraw() => D2DComVTable.EndDraw(Handle);

        /// <summary>
        /// Sets the Dots Per Inch (DPI) of the render target for dynamic High-DPI and multi-monitor scaling.
        /// Direct2D uses these DPI metrics to map logical Device Independent Pixels (DIPs) to physical pixels.
        /// </summary>
        public void SetDpi(float dpiX, float dpiY)
        {
            if (dpiX > 0 && dpiY > 0)
            {
                D2DComVTable.SetDpi(Handle, dpiX, dpiY);
            }
        }

        /// <summary>
        /// Gets the current DPI values configured on the render target.
        /// </summary>
        public void GetDpi(out float dpiX, out float dpiY)
        {
            D2DComVTable.GetDpi(Handle, out dpiX, out dpiY);
        }


        public void Clear(Color color)
        {
            D2D1_COLOR_F c = new D2D1_COLOR_F(color.R / 255.0f, color.G / 255.0f, color.B / 255.0f, color.A / 255.0f);
            D2DComVTable.Clear(Handle, ref c);
        }

        public void DrawLine(float x0, float y0, float x1, float y1, D2DSolidColorBrush brush, float strokeWidth = 1.0f)
        {
            if (brush == null || !brush.IsValid) return;
            D2DComVTable.DrawLine(Handle, new D2D1_POINT_2F(x0, y0), new D2D1_POINT_2F(x1, y1), brush.Handle, strokeWidth);
        }

        public void DrawRectangle(float x, float y, float width, float height, D2DSolidColorBrush brush, float strokeWidth = 1.0f)
        {
            if (brush == null || !brush.IsValid) return;
            D2D1_RECT_F r = new D2D1_RECT_F(x, y, x + width, y + height);
            D2DComVTable.DrawRectangle(Handle, ref r, brush.Handle, strokeWidth);
        }

        public void FillRectangle(float x, float y, float width, float height, D2DSolidColorBrush brush)
        {
            if (brush == null || !brush.IsValid) return;
            D2D1_RECT_F r = new D2D1_RECT_F(x, y, x + width, y + height);
            D2DComVTable.FillRectangle(Handle, ref r, brush.Handle);
        }

        public void DrawRoundedRectangle(float x, float y, float width, float height, float radius, D2DSolidColorBrush brush, float strokeWidth = 1.0f)
        {
            if (brush == null || !brush.IsValid) return;
            D2D1_ROUNDED_RECT rr = new D2D1_ROUNDED_RECT(new D2D1_RECT_F(x, y, x + width, y + height), radius, radius);
            D2DComVTable.DrawRoundedRectangle(Handle, ref rr, brush.Handle, strokeWidth);
        }

        public void FillRoundedRectangle(float x, float y, float width, float height, float radius, D2DSolidColorBrush brush)
        {
            if (brush == null || !brush.IsValid) return;
            D2D1_ROUNDED_RECT rr = new D2D1_ROUNDED_RECT(new D2D1_RECT_F(x, y, x + width, y + height), radius, radius);
            D2DComVTable.FillRoundedRectangle(Handle, ref rr, brush.Handle);
        }

        public void DrawText(string text, DWriteTextFormat format, float x, float y, float width, float height, D2DSolidColorBrush brush)
        {
            if (string.IsNullOrEmpty(text) || format == null || !format.IsValid || brush == null || !brush.IsValid) return;
            D2D1_RECT_F r = new D2D1_RECT_F(x, y, x + width, y + height);
            D2DComVTable.DrawText(Handle, text, format.Handle, ref r, brush.Handle);
        }
    }

    public sealed class D2DHwndRenderTarget : D2DRenderTarget
    {
        public D2DHwndRenderTarget(IntPtr handle) : base(handle) { }

        public void Resize(int width, int height)
        {
            D2D1_SIZE_U size = new D2D1_SIZE_U((uint)Math.Max(1, width), (uint)Math.Max(1, height));
            D2DComVTable.Resize(Handle, ref size);
        }
    }

    public sealed class D2DDxgiRenderTarget : D2DRenderTarget
    {
        public D2DDxgiRenderTarget(IntPtr handle) : base(handle) { }
    }
}
