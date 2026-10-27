# ZeroGraphics.Direct2D ⚡

Ultra-fast, hardware-accelerated Direct2D and DirectWrite vector graphics and subpixel typography engine for Windows Forms (`net462`, `net8.0-windows`).

---

## 🌟 Key Features

* **DirectWrite Subpixel ClearType Typography (`DWriteFactory`):** Eliminates GDI/GDI+ blurry text scaling on 2K/4K high-DPI displays.
* **Pure COM VTable Dispatch (`D2DComVTable`):** Zero external NuGet dependencies, lightweight footprint (< 120 KB), with 100% native runtime compatibility.
* **Zero-Allocation Hot Path (`D2DHwndRenderTarget`):** Renders vectors, lines, rounded rectangles, and text without GC heap allocations.
* **Flicker-Free WinForms Integration (`ZeroDirect2DCanvas`):** Direct HWND swap presentation suppressing `WM_ERASEBKGND`.
