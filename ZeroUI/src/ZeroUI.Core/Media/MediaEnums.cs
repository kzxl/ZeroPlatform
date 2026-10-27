using System;

namespace ZeroUI.Core.Media
{
    /// <summary>
    /// Sizing and framing modes for image viewports.
    /// </summary>
    public enum ImageViewMode : byte
    {
        /// <summary>Scale image proportionally to fit entirely inside viewport.</summary>
        FitToWindow = 0,

        /// <summary>Scale image proportionally so width matches viewport width.</summary>
        FitWidth = 1,

        /// <summary>Scale image proportionally so height matches viewport height.</summary>
        FitHeight = 2,

        /// <summary>Display image at 1:1 physical pixel scale (100%).</summary>
        OriginalSize = 3,

        /// <summary>Custom zoom factor controlled by user interaction.</summary>
        Custom = 4
    }

    /// <summary>
    /// Sampling and interpolation quality for image scaling.
    /// </summary>
    public enum ImageInterpolationQuality : byte
    {
        /// <summary>Point sampling for crisp pixel-art and forensic inspection.</summary>
        NearestNeighbor = 0,

        /// <summary>Standard bilinear filtering for real-time responsiveness.</summary>
        Bilinear = 1,

        /// <summary>High-quality bicubic filtering for photographic rendering.</summary>
        Bicubic = 2,

        /// <summary>Lanczos or high-quality cubic filtering for pristine presentation.</summary>
        HighQuality = 3
    }

    /// <summary>
    /// 90-degree orthogonal rotation angles.
    /// </summary>
    public enum ImageRotationAngle : short
    {
        Rotate0 = 0,
        Rotate90 = 90,
        Rotate180 = 180,
        Rotate270 = 270
    }

    /// <summary>
    /// Mirror reflection axes for viewport rendering.
    /// </summary>
    [Flags]
    public enum ImageFlipMode : byte
    {
        None = 0,
        Horizontal = 1,
        Vertical = 2,
        Both = Horizontal | Vertical
    }

    /// <summary>
    /// Overlays and HUD indicators on top of the image canvas.
    /// </summary>
    [Flags]
    public enum ImageViewerOverlays : byte
    {
        None = 0,
        Toolbar = 1 << 0,
        StatusBar = 1 << 1,
        MiniMap = 1 << 2,
        PixelGrid = 1 << 3,
        Loupe = 1 << 4,
        ColorProbe = 1 << 5,
        All = Toolbar | StatusBar | MiniMap | PixelGrid | Loupe | ColorProbe
    }

    /// <summary>
    /// Supported visual annotation primitive types for inspection &amp; QC tagging.
    /// </summary>
    public enum AnnotationShapeType : byte
    {
        BoundingBox = 0,
        Arrow = 1,
        Line = 2,
        Ellipse = 3,
        TextCallout = 4,
        Highlighter = 5,
        BlurPixelate = 6
    }

    /// <summary>
    /// Quality inspection severity levels for defect tagging.
    /// </summary>
    public enum AnnotationSeverity : byte
    {
        Info = 0,
        Ok = 1,
        Warning = 2,
        Defect = 3,
        Critical = 4
    }

    /// <summary>
    /// Measurement physical calibration units.
    /// </summary>
    public enum MeasurementUnit : byte
    {
        Pixel = 0,
        Millimeter = 1,
        Micrometer = 2,
        Centimeter = 3,
        Inch = 4
    }

    /// <summary>
    /// Measurement modes for interactive precision calipers.
    /// </summary>
    public enum MeasurementMode : byte
    {
        LinearDistance = 0,
        ThreePointAngle = 1,
        PolygonArea = 2,
        CaliperGauge = 3
    }

    /// <summary>
    /// Watermark anchor placement presets.
    /// </summary>
    public enum WatermarkPlacement : byte
    {
        TopLeft = 0,
        TopCenter = 1,
        TopRight = 2,
        MiddleLeft = 3,
        Center = 4,
        MiddleRight = 5,
        BottomLeft = 6,
        BottomCenter = 7,
        BottomRight = 8,
        DiagonalTiled = 9
    }

    /// <summary>
    /// Media playback operational states.
    /// </summary>
    public enum MediaPlaybackState : byte
    {
        Stopped = 0,
        Playing = 1,
        Paused = 2
    }
}
