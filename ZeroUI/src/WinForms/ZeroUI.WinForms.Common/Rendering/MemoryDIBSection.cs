using System;
using System.Runtime.InteropServices;
using ZeroUI.Core.Common;
using ZeroUI.WinForms.Native;

namespace ZeroUI.WinForms.Rendering
{
    /// <summary>
    /// Encapsulates a Win32 Memory DC backed by an unmanaged 32bpp DIB Section.
    /// Provides zero-copy BitBlt and hardware ClearType text rasterization.
    /// </summary>
    public sealed unsafe class MemoryDIBSection : IDisposable
    {
        private IntPtr _hMemDC;
        private IntPtr _hBitmap;
        private IntPtr _hOldBmp;
        private void* _pBits;
        private int _width;
        private int _height;
        private bool _disposed;
        private uint _lastTextColor = 0xFFFFFFFF;

        public int Width => _width;
        public int Height => _height;
        public IntPtr Handle => _hMemDC;
        public void* PixelPointer => _pBits;

        public void EnsureSize(int width, int height, IntPtr hScreenDC)
        {
            if (width <= 0 || height <= 0) return;
            if (_hMemDC != IntPtr.Zero && _width >= width && _height >= height) return;

            ReleaseResources();

            _width = Math.Max(width, 100);
            _height = Math.Max(height, 100);

            _hMemDC = NativeMethods.CreateCompatibleDC(hScreenDC);

            BITMAPINFO bmi = new BITMAPINFO();
            bmi.bmiHeader.biSize = (uint)sizeof(BITMAPINFOHEADER);
            bmi.bmiHeader.biWidth = _width;
            bmi.bmiHeader.biHeight = -_height; // Top-down DIB
            bmi.bmiHeader.biPlanes = 1;
            bmi.bmiHeader.biBitCount = 32;
            bmi.bmiHeader.biCompression = (uint)NativeMethods.BI_RGB;

            _hBitmap = NativeMethods.CreateDIBSection(
                _hMemDC,
                ref bmi,
                (uint)NativeMethods.DIB_RGB_COLORS,
                out IntPtr pBits,
                IntPtr.Zero,
                0);

            _pBits = (void*)pBits;
            _hOldBmp = NativeMethods.SelectObject(_hMemDC, _hBitmap);

            NativeMethods.SetBkMode(_hMemDC, NativeMethods.TRANSPARENT);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static uint ColorRefToDIBPixel(uint colorRef)
        {
            // Win32 COLORREF is 0x00BBGGRR
            // Top-down 32bpp DIB Section in memory (x86 little endian) is [B, G, R, 0] = 0x00RRGGBB
            uint r = colorRef & 0xFF;
            uint g = (colorRef >> 8) & 0xFF;
            uint b = (colorRef >> 16) & 0xFF;
            return (r << 16) | (g << 8) | b;
        }

        public void Clear(uint colorRef)
        {
            if (_pBits == null || _width <= 0 || _height <= 0) return;

            uint pixel = ColorRefToDIBPixel(colorRef);
            int totalPixels = _width * _height;
            new Span<uint>(_pBits, totalPixels).Fill(pixel);
        }

        private IntPtr _hOldFont;

        public void SelectFont(IntPtr hFont)
        {
            if (_hMemDC == IntPtr.Zero || hFont == IntPtr.Zero) return;
            IntPtr old = NativeMethods.SelectObject(_hMemDC, hFont);
            if (_hOldFont == IntPtr.Zero)
            {
                _hOldFont = old;
            }
        }

        public void FillRectangle(int x, int y, int width, int height, uint colorRef)
        {
            if (_pBits == null || width <= 0 || height <= 0 || _width <= 0 || _height <= 0) return;

            int right = Math.Min(_width, x + width);
            int bottom = Math.Min(_height, y + height);
            int startX = Math.Max(0, x);
            int startY = Math.Max(0, y);
            int fillW = right - startX;
            if (fillW <= 0 || startY >= bottom) return;

            uint pixel = ColorRefToDIBPixel(colorRef);
            uint* basePtr = (uint*)_pBits;

            for (int line = startY; line < bottom; line++)
            {
                uint* linePtr = basePtr + (line * _width) + startX;
                new Span<uint>(linePtr, fillW).Fill(pixel);
            }
        }


        public void DrawText(string? text, ref RECT rect, uint textColor, CellAlignment alignment, int textHeight)
        {
            if (!string.IsNullOrEmpty(text))
            {
                DrawText(text.AsSpan(), ref rect, textColor, alignment, textHeight);
            }
        }

        public void DrawText(ReadOnlySpan<char> text, ref RECT rect, uint textColor, CellAlignment alignment, int textHeight)
        {
            if (_hMemDC == IntPtr.Zero || text.IsEmpty || rect.Width <= 0 || rect.Height <= 0) return;

            if (_lastTextColor != textColor)
            {
                NativeMethods.SetTextColor(_hMemDC, textColor);
                _lastTextColor = textColor;
            }

            // Compute Y position vertically centered
            int y = rect.Top + Math.Max(0, (rect.Height - textHeight) / 2);
            int x = rect.Left + 8; // 8px standard left padding

            fixed (char* pText = text)
            {
                if (alignment == CellAlignment.Right)
                {
                    if (NativeMethods.GetTextExtentPoint32(_hMemDC, pText, text.Length, out SIZE sz))
                    {
                        x = Math.Max(rect.Left + 4, rect.Right - sz.cx - 8);
                    }
                }
                else if (alignment == CellAlignment.Center)
                {
                    if (NativeMethods.GetTextExtentPoint32(_hMemDC, pText, text.Length, out SIZE sz))
                    {
                        x = Math.Max(rect.Left + 4, rect.Left + (rect.Width - sz.cx) / 2);
                    }
                }

                NativeMethods.ExtTextOut(
                    _hMemDC,
                    x,
                    y,
                    NativeMethods.ETO_CLIPPED,
                    ref rect,
                    pText,
                    (uint)text.Length,
                    null);
            }
        }

        public void BitBltTo(IntPtr hTargetDC, int destX, int destY, int width, int height)
        {
            if (_hMemDC == IntPtr.Zero) return;

            NativeMethods.BitBlt(
                hTargetDC,
                destX,
                destY,
                width,
                height,
                _hMemDC,
                0,
                0,
                NativeMethods.SRCCOPY);
        }

        private void ReleaseResources()
        {
            if (_hMemDC != IntPtr.Zero)
            {
                if (_hOldFont != IntPtr.Zero)
                {
                    NativeMethods.SelectObject(_hMemDC, _hOldFont);
                    _hOldFont = IntPtr.Zero;
                }
                if (_hOldBmp != IntPtr.Zero)
                {
                    NativeMethods.SelectObject(_hMemDC, _hOldBmp);
                    _hOldBmp = IntPtr.Zero;
                }
                if (_hBitmap != IntPtr.Zero)
                {
                    NativeMethods.DeleteObject(_hBitmap);
                    _hBitmap = IntPtr.Zero;
                }
                NativeMethods.DeleteDC(_hMemDC);
                _hMemDC = IntPtr.Zero;
                _pBits = null;
                _lastTextColor = 0xFFFFFFFF;
            }
        }


        public void Dispose()
        {
            if (!_disposed)
            {
                ReleaseResources();
                _disposed = true;
                GC.SuppressFinalize(this);
            }
        }

        ~MemoryDIBSection()
        {
            ReleaseResources();
        }
    }
}
