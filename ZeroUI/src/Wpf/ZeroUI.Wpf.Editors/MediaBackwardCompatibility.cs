using System;

namespace ZeroUI.Wpf.Editors
{
    #region Media Controls Backward Compatibility Shims (5-Release Deprecation Policy)

    #region Canonical Z* Shims in Editors namespace

    /// <summary>
    /// Forwarding shim for <see cref="ZeroUI.Wpf.Media.ZCompareViewer"/>.
    /// </summary>
    [Obsolete("ZCompareViewer has moved to ZeroUI.Wpf.Media. Please update your using directives.")]
    public class ZCompareViewer : ZeroUI.Wpf.Media.ZCompareViewer { }

    /// <summary>
    /// Forwarding shim for <see cref="ZeroUI.Wpf.Media.ZCropBox"/>.
    /// </summary>
    [Obsolete("ZCropBox has moved to ZeroUI.Wpf.Media. Please update your using directives.")]
    public class ZCropBox : ZeroUI.Wpf.Media.ZCropBox { }

    /// <summary>
    /// Forwarding shim for <see cref="ZeroUI.Wpf.Media.ZDocumentDeskew"/>.
    /// </summary>
    [Obsolete("ZDocumentDeskew has moved to ZeroUI.Wpf.Media. Please update your using directives.")]
    public class ZDocumentDeskew : ZeroUI.Wpf.Media.ZDocumentDeskew { }

    /// <summary>
    /// Forwarding shim for <see cref="ZeroUI.Wpf.Media.ZOcrViewer"/>.
    /// </summary>
    [Obsolete("ZOcrViewer has moved to ZeroUI.Wpf.Media. Please update your using directives.")]
    public class ZOcrViewer : ZeroUI.Wpf.Media.ZOcrViewer { }

    /// <summary>
    /// Forwarding shim for <see cref="ZeroUI.Wpf.Media.ZHistogramScope"/>.
    /// </summary>
    [Obsolete("ZHistogramScope has moved to ZeroUI.Wpf.Media. Please update your using directives.")]
    public class ZHistogramScope : ZeroUI.Wpf.Media.ZHistogramScope { }

    /// <summary>
    /// Forwarding shim for <see cref="ZeroUI.Wpf.Media.ZMiniMapNavigator"/>.
    /// </summary>
    [Obsolete("ZMiniMapNavigator has moved to ZeroUI.Wpf.Media. Please update your using directives.")]
    public class ZMiniMapNavigator : ZeroUI.Wpf.Media.ZMiniMapNavigator { }

    /// <summary>
    /// Forwarding shim for <see cref="ZeroUI.Wpf.Media.ZMaskGizmoOverlay"/>.
    /// </summary>
    [Obsolete("ZMaskGizmoOverlay has moved to ZeroUI.Wpf.Media. Please update your using directives.")]
    public class ZMaskGizmoOverlay : ZeroUI.Wpf.Media.ZMaskGizmoOverlay { }

    /// <summary>
    /// Forwarding shim for <see cref="ZeroUI.Wpf.Media.ZFilmstripScroller"/>.
    /// </summary>
    [Obsolete("ZFilmstripScroller has moved to ZeroUI.Wpf.Media. Please update your using directives.")]
    public class ZFilmstripScroller : ZeroUI.Wpf.Media.ZFilmstripScroller { }

    /// <summary>
    /// Forwarding shim for <see cref="ZeroUI.Wpf.Media.ZThumbnailGrid"/>.
    /// </summary>
    [Obsolete("ZThumbnailGrid has moved to ZeroUI.Wpf.Media. Please update your using directives.")]
    public class ZThumbnailGrid : ZeroUI.Wpf.Media.ZThumbnailGrid { }

    /// <summary>
    /// Forwarding shim for <see cref="ZeroUI.Wpf.Media.ZDominantPalette"/>.
    /// </summary>
    [Obsolete("ZDominantPalette has moved to ZeroUI.Wpf.Media. Please update your using directives.")]
    public class ZDominantPalette : ZeroUI.Wpf.Media.ZDominantPalette { }

    /// <summary>
    /// Forwarding shim for <see cref="ZeroUI.Wpf.Media.ZExifTelemetryCard"/>.
    /// </summary>
    [Obsolete("ZExifTelemetryCard has moved to ZeroUI.Wpf.Media. Please update your using directives.")]
    public class ZExifTelemetryCard : ZeroUI.Wpf.Media.ZExifTelemetryCard { }

    /// <summary>
    /// Forwarding shim for <see cref="ZeroUI.Wpf.Media.ZCurveEditor"/>.
    /// </summary>
    [Obsolete("ZCurveEditor has moved to ZeroUI.Wpf.Media. Please update your using directives.")]
    public class ZCurveEditor : ZeroUI.Wpf.Media.ZCurveEditor { }

    /// <summary>
    /// Forwarding shim for <see cref="ZeroUI.Wpf.Media.ZColorWheel"/>.
    /// </summary>
    [Obsolete("ZColorWheel has moved to ZeroUI.Wpf.Media. Please update your using directives.")]
    public class ZColorWheel : ZeroUI.Wpf.Media.ZColorWheel { }

    /// <summary>
    /// Forwarding shim for <see cref="ZeroUI.Wpf.Media.ZBatchTaskQueue"/>.
    /// </summary>
    [Obsolete("ZBatchTaskQueue has moved to ZeroUI.Wpf.Media. Please update your using directives.")]
    public class ZBatchTaskQueue : ZeroUI.Wpf.Media.ZBatchTaskQueue { }

    /// <summary>
    /// Forwarding shim for <see cref="ZeroUI.Wpf.Media.ZTokenPatternEditor"/>.
    /// </summary>
    [Obsolete("ZTokenPatternEditor has moved to ZeroUI.Wpf.Media. Please update your using directives.")]
    public class ZTokenPatternEditor : ZeroUI.Wpf.Media.ZTokenPatternEditor { }

    /// <summary>
    /// Forwarding shim for <see cref="ZeroUI.Wpf.Media.ZHistoryTimeline"/>.
    /// </summary>
    [Obsolete("ZHistoryTimeline has moved to ZeroUI.Wpf.Media. Please update your using directives.")]
    public class ZHistoryTimeline : ZeroUI.Wpf.Media.ZHistoryTimeline { }

    /// <summary>
    /// Forwarding shim for <see cref="ZeroUI.Wpf.Media.ZPictureEdit"/>.
    /// </summary>
    [Obsolete("ZPictureEdit has moved to ZeroUI.Wpf.Media. Please update your using directives.")]
    public class ZPictureEdit : ZeroUI.Wpf.Media.ZPictureEdit { }

    #endregion

    #region Legacy Non-Prefixed Shims

    /// <summary>
    /// Legacy alias for <see cref="ZeroUI.Wpf.Media.ZCompareViewer"/>.
    /// </summary>
    [Obsolete("CompareViewerControl has moved to ZeroUI.Wpf.Media. Please migrate to ZeroUI.Wpf.Media.ZCompareViewer instead.")]
    public class CompareViewerControl : ZeroUI.Wpf.Media.ZCompareViewer { }

    /// <summary>
    /// Legacy alias for <see cref="ZeroUI.Wpf.Media.ZCropBox"/>.
    /// </summary>
    [Obsolete("CropBoxControl has moved to ZeroUI.Wpf.Media. Please migrate to ZeroUI.Wpf.Media.ZCropBox instead.")]
    public class CropBoxControl : ZeroUI.Wpf.Media.ZCropBox { }

    /// <summary>
    /// Legacy alias for <see cref="ZeroUI.Wpf.Media.ZOcrViewer"/>.
    /// </summary>
    [Obsolete("OcrRoiBoxControl has moved to ZeroUI.Wpf.Media. Please migrate to ZeroUI.Wpf.Media.ZOcrViewer instead.")]
    public class OcrRoiBoxControl : ZeroUI.Wpf.Media.ZOcrViewer { }

    /// <summary>
    /// Legacy alias for <see cref="ZeroUI.Wpf.Media.ZOcrViewer"/>.
    /// </summary>
    [Obsolete("ZeroOcrViewer has moved to ZeroUI.Wpf.Media. Please migrate to ZeroUI.Wpf.Media.ZOcrViewer instead.")]
    public class ZeroOcrViewer : ZeroUI.Wpf.Media.ZOcrViewer { }

    /// <summary>
    /// Legacy alias for <see cref="ZeroUI.Wpf.Media.ZHistogramScope"/>.
    /// </summary>
    [Obsolete("HistogramScopeControl has moved to ZeroUI.Wpf.Media. Please migrate to ZeroUI.Wpf.Media.ZHistogramScope instead.")]
    public class HistogramScopeControl : ZeroUI.Wpf.Media.ZHistogramScope { }

    /// <summary>
    /// Legacy alias for <see cref="ZeroUI.Wpf.Media.ZMiniMapNavigator"/>.
    /// </summary>
    [Obsolete("MiniMapNavigator has moved to ZeroUI.Wpf.Media. Please migrate to ZeroUI.Wpf.Media.ZMiniMapNavigator instead.")]
    public class MiniMapNavigator : ZeroUI.Wpf.Media.ZMiniMapNavigator { }

    /// <summary>
    /// Legacy alias for <see cref="ZeroUI.Wpf.Media.ZMaskGizmoOverlay"/>.
    /// </summary>
    [Obsolete("MaskGizmoOverlay has moved to ZeroUI.Wpf.Media. Please migrate to ZeroUI.Wpf.Media.ZMaskGizmoOverlay instead.")]
    public class MaskGizmoOverlay : ZeroUI.Wpf.Media.ZMaskGizmoOverlay { }

    /// <summary>
    /// Legacy alias for <see cref="ZeroUI.Wpf.Media.ZFilmstripScroller"/>.
    /// </summary>
    [Obsolete("FilmstripScrollerControl has moved to ZeroUI.Wpf.Media. Please migrate to ZeroUI.Wpf.Media.ZFilmstripScroller instead.")]
    public class FilmstripScrollerControl : ZeroUI.Wpf.Media.ZFilmstripScroller { }

    /// <summary>
    /// Legacy alias for <see cref="ZeroUI.Wpf.Media.ZThumbnailGrid"/>.
    /// </summary>
    [Obsolete("ThumbnailGridControl has moved to ZeroUI.Wpf.Media. Please migrate to ZeroUI.Wpf.Media.ZThumbnailGrid instead.")]
    public class ThumbnailGridControl : ZeroUI.Wpf.Media.ZThumbnailGrid { }

    /// <summary>
    /// Legacy alias for <see cref="ZeroUI.Wpf.Media.ZDominantPalette"/>.
    /// </summary>
    [Obsolete("DominantPaletteControl has moved to ZeroUI.Wpf.Media. Please migrate to ZeroUI.Wpf.Media.ZDominantPalette instead.")]
    public class DominantPaletteControl : ZeroUI.Wpf.Media.ZDominantPalette { }

    /// <summary>
    /// Legacy alias for <see cref="ZeroUI.Wpf.Media.ZExifTelemetryCard"/>.
    /// </summary>
    [Obsolete("ExifTelemetryCard has moved to ZeroUI.Wpf.Media. Please migrate to ZeroUI.Wpf.Media.ZExifTelemetryCard instead.")]
    public class ExifTelemetryCard : ZeroUI.Wpf.Media.ZExifTelemetryCard { }

    /// <summary>
    /// Legacy alias for <see cref="ZeroUI.Wpf.Media.ZCurveEditor"/>.
    /// </summary>
    [Obsolete("CurveEditor has moved to ZeroUI.Wpf.Media. Please migrate to ZeroUI.Wpf.Media.ZCurveEditor instead.")]
    public class CurveEditor : ZeroUI.Wpf.Media.ZCurveEditor { }

    /// <summary>
    /// Legacy alias for <see cref="ZeroUI.Wpf.Media.ZColorWheel"/>.
    /// </summary>
    [Obsolete("ColorWheelEdit has moved to ZeroUI.Wpf.Media. Please migrate to ZeroUI.Wpf.Media.ZColorWheel instead.")]
    public class ColorWheelEdit : ZeroUI.Wpf.Media.ZColorWheel { }

    /// <summary>
    /// Legacy alias for <see cref="ZeroUI.Wpf.Media.ZColorWheel"/>.
    /// </summary>
    [Obsolete("ColorWheel has moved to ZeroUI.Wpf.Media. Please migrate to ZeroUI.Wpf.Media.ZColorWheel instead.")]
    public class ColorWheel : ZeroUI.Wpf.Media.ZColorWheel { }

    /// <summary>
    /// Legacy alias for <see cref="ZeroUI.Wpf.Media.ZBatchTaskQueue"/>.
    /// </summary>
    [Obsolete("BatchTaskQueueControl has moved to ZeroUI.Wpf.Media. Please migrate to ZeroUI.Wpf.Media.ZBatchTaskQueue instead.")]
    public class BatchTaskQueueControl : ZeroUI.Wpf.Media.ZBatchTaskQueue { }

    /// <summary>
    /// Legacy alias for <see cref="ZeroUI.Wpf.Media.ZTokenPatternEditor"/>.
    /// </summary>
    [Obsolete("TokenPatternEditor has moved to ZeroUI.Wpf.Media. Please migrate to ZeroUI.Wpf.Media.ZTokenPatternEditor instead.")]
    public class TokenPatternEditor : ZeroUI.Wpf.Media.ZTokenPatternEditor { }

    /// <summary>
    /// Legacy alias for <see cref="ZeroUI.Wpf.Media.ZHistoryTimeline"/>.
    /// </summary>
    [Obsolete("HistoryTimelineControl has moved to ZeroUI.Wpf.Media. Please migrate to ZeroUI.Wpf.Media.ZHistoryTimeline instead.")]
    public class HistoryTimelineControl : ZeroUI.Wpf.Media.ZHistoryTimeline { }

    /// <summary>
    /// Legacy alias for <see cref="ZeroUI.Wpf.Media.ZPictureEdit"/>.
    /// </summary>
    [Obsolete("PictureEdit has moved to ZeroUI.Wpf.Media. Please migrate to ZeroUI.Wpf.Media.ZPictureEdit instead.")]
    public class PictureEdit : ZeroUI.Wpf.Media.ZPictureEdit { }

    #endregion

    #region Legacy Zero* Prefixed Shims

    /// <summary>
    /// Legacy alias for <see cref="ZeroUI.Wpf.Media.ZCompareViewer"/>.
    /// </summary>
    [Obsolete("ZeroCompareViewer has moved to ZeroUI.Wpf.Media. Please migrate to ZeroUI.Wpf.Media.ZCompareViewer instead.")]
    public class ZeroCompareViewer : ZeroUI.Wpf.Media.ZCompareViewer { }

    /// <summary>
    /// Legacy alias for <see cref="ZeroUI.Wpf.Media.ZCropBox"/>.
    /// </summary>
    [Obsolete("ZeroCropBox has moved to ZeroUI.Wpf.Media. Please migrate to ZeroUI.Wpf.Media.ZCropBox instead.")]
    public class ZeroCropBox : ZeroUI.Wpf.Media.ZCropBox { }

    /// <summary>
    /// Legacy alias for <see cref="ZeroUI.Wpf.Media.ZHistogramScope"/>.
    /// </summary>
    [Obsolete("ZeroHistogramScope has moved to ZeroUI.Wpf.Media. Please migrate to ZeroUI.Wpf.Media.ZHistogramScope instead.")]
    public class ZeroHistogramScope : ZeroUI.Wpf.Media.ZHistogramScope { }

    /// <summary>
    /// Legacy alias for <see cref="ZeroUI.Wpf.Media.ZMiniMapNavigator"/>.
    /// </summary>
    [Obsolete("ZeroMiniMapNavigator has moved to ZeroUI.Wpf.Media. Please migrate to ZeroUI.Wpf.Media.ZMiniMapNavigator instead.")]
    public class ZeroMiniMapNavigator : ZeroUI.Wpf.Media.ZMiniMapNavigator { }

    /// <summary>
    /// Legacy alias for <see cref="ZeroUI.Wpf.Media.ZMaskGizmoOverlay"/>.
    /// </summary>
    [Obsolete("ZeroMaskGizmoOverlay has moved to ZeroUI.Wpf.Media. Please migrate to ZeroUI.Wpf.Media.ZMaskGizmoOverlay instead.")]
    public class ZeroMaskGizmoOverlay : ZeroUI.Wpf.Media.ZMaskGizmoOverlay { }

    /// <summary>
    /// Legacy alias for <see cref="ZeroUI.Wpf.Media.ZFilmstripScroller"/>.
    /// </summary>
    [Obsolete("ZeroFilmstripScroller has moved to ZeroUI.Wpf.Media. Please migrate to ZeroUI.Wpf.Media.ZFilmstripScroller instead.")]
    public class ZeroFilmstripScroller : ZeroUI.Wpf.Media.ZFilmstripScroller { }

    /// <summary>
    /// Legacy alias for <see cref="ZeroUI.Wpf.Media.ZThumbnailGrid"/>.
    /// </summary>
    [Obsolete("ZeroThumbnailGrid has moved to ZeroUI.Wpf.Media. Please migrate to ZeroUI.Wpf.Media.ZThumbnailGrid instead.")]
    public class ZeroThumbnailGrid : ZeroUI.Wpf.Media.ZThumbnailGrid { }

    /// <summary>
    /// Legacy alias for <see cref="ZeroUI.Wpf.Media.ZDominantPalette"/>.
    /// </summary>
    [Obsolete("ZeroDominantPalette has moved to ZeroUI.Wpf.Media. Please migrate to ZeroUI.Wpf.Media.ZDominantPalette instead.")]
    public class ZeroDominantPalette : ZeroUI.Wpf.Media.ZDominantPalette { }

    /// <summary>
    /// Legacy alias for <see cref="ZeroUI.Wpf.Media.ZExifTelemetryCard"/>.
    /// </summary>
    [Obsolete("ZeroExifTelemetryCard has moved to ZeroUI.Wpf.Media. Please migrate to ZeroUI.Wpf.Media.ZExifTelemetryCard instead.")]
    public class ZeroExifTelemetryCard : ZeroUI.Wpf.Media.ZExifTelemetryCard { }

    /// <summary>
    /// Legacy alias for <see cref="ZeroUI.Wpf.Media.ZCurveEditor"/>.
    /// </summary>
    [Obsolete("ZeroCurveEditor has moved to ZeroUI.Wpf.Media. Please migrate to ZeroUI.Wpf.Media.ZCurveEditor instead.")]
    public class ZeroCurveEditor : ZeroUI.Wpf.Media.ZCurveEditor { }

    /// <summary>
    /// Legacy alias for <see cref="ZeroUI.Wpf.Media.ZColorWheel"/>.
    /// </summary>
    [Obsolete("ZeroColorWheel has moved to ZeroUI.Wpf.Media. Please migrate to ZeroUI.Wpf.Media.ZColorWheel instead.")]
    public class ZeroColorWheel : ZeroUI.Wpf.Media.ZColorWheel { }

    /// <summary>
    /// Legacy alias for <see cref="ZeroUI.Wpf.Media.ZBatchTaskQueue"/>.
    /// </summary>
    [Obsolete("ZeroBatchTaskQueue has moved to ZeroUI.Wpf.Media. Please migrate to ZeroUI.Wpf.Media.ZBatchTaskQueue instead.")]
    public class ZeroBatchTaskQueue : ZeroUI.Wpf.Media.ZBatchTaskQueue { }

    /// <summary>
    /// Legacy alias for <see cref="ZeroUI.Wpf.Media.ZTokenPatternEditor"/>.
    /// </summary>
    [Obsolete("ZeroTokenPatternEditor has moved to ZeroUI.Wpf.Media. Please migrate to ZeroUI.Wpf.Media.ZTokenPatternEditor instead.")]
    public class ZeroTokenPatternEditor : ZeroUI.Wpf.Media.ZTokenPatternEditor { }

    /// <summary>
    /// Legacy alias for <see cref="ZeroUI.Wpf.Media.ZHistoryTimeline"/>.
    /// </summary>
    [Obsolete("ZeroHistoryTimeline has moved to ZeroUI.Wpf.Media. Please migrate to ZeroUI.Wpf.Media.ZHistoryTimeline instead.")]
    public class ZeroHistoryTimeline : ZeroUI.Wpf.Media.ZHistoryTimeline { }

    /// <summary>
    /// Legacy alias for <see cref="ZeroUI.Wpf.Media.ZPictureEdit"/>.
    /// </summary>
    [Obsolete("ZeroPictureEdit has moved to ZeroUI.Wpf.Media. Please migrate to ZeroUI.Wpf.Media.ZPictureEdit instead.")]
    public class ZeroPictureEdit : ZeroUI.Wpf.Media.ZPictureEdit { }

    #endregion

    #region Associated Models & Event Shims

    /// <summary>
    /// Forwarding shim for <see cref="ZeroUI.Wpf.Media.BatchTaskItemModel"/>.
    /// </summary>
    [Obsolete("BatchTaskItemModel has moved to ZeroUI.Wpf.Media. Please update your using directives.")]
    public class BatchTaskItemModel : ZeroUI.Wpf.Media.BatchTaskItemModel { }

    /// <summary>
    /// Forwarding shim for <see cref="ZeroUI.Wpf.Media.FilmstripItemModel"/>.
    /// </summary>
    [Obsolete("FilmstripItemModel has moved to ZeroUI.Wpf.Media. Please update your using directives.")]
    public class FilmstripItemModel : ZeroUI.Wpf.Media.FilmstripItemModel { }

    /// <summary>
    /// Forwarding shim for <see cref="ZeroUI.Wpf.Media.FilmstripItemClickEventArgs"/>.
    /// </summary>
    [Obsolete("FilmstripItemClickEventArgs has moved to ZeroUI.Wpf.Media. Please update your using directives.")]
    public class FilmstripItemClickEventArgs : ZeroUI.Wpf.Media.FilmstripItemClickEventArgs
    {
        public FilmstripItemClickEventArgs(ZeroUI.Wpf.Media.FilmstripItemModel item, bool ctrl, bool shift) : base(item, ctrl, shift) { }
    }

    /// <summary>
    /// Forwarding shim for <see cref="ZeroUI.Wpf.Media.ThumbnailGridItemModel"/>.
    /// </summary>
    [Obsolete("ThumbnailGridItemModel has moved to ZeroUI.Wpf.Media. Please update your using directives.")]
    public class ThumbnailGridItemModel : ZeroUI.Wpf.Media.ThumbnailGridItemModel { }

    /// <summary>
    /// Forwarding shim for <see cref="ZeroUI.Wpf.Media.ThumbnailGridItemClickEventArgs"/>.
    /// </summary>
    [Obsolete("ThumbnailGridItemClickEventArgs has moved to ZeroUI.Wpf.Media. Please update your using directives.")]
    public class ThumbnailGridItemClickEventArgs : ZeroUI.Wpf.Media.ThumbnailGridItemClickEventArgs
    {
        public ThumbnailGridItemClickEventArgs(object item, bool ctrl, bool shift) : base(item, ctrl, shift) { }
    }

    /// <summary>
    /// Forwarding shim for <see cref="ZeroUI.Wpf.Media.PaletteSwatchItem"/>.
    /// </summary>
    [Obsolete("PaletteSwatchItem has moved to ZeroUI.Wpf.Media. Please update your using directives.")]
    public class PaletteSwatchItem : ZeroUI.Wpf.Media.PaletteSwatchItem { }

    /// <summary>
    /// Forwarding shim for <see cref="ZeroUI.Wpf.Media.PaletteSwatchClickEventArgs"/>.
    /// </summary>
    [Obsolete("PaletteSwatchClickEventArgs has moved to ZeroUI.Wpf.Media. Please update your using directives.")]
    public class PaletteSwatchClickEventArgs : ZeroUI.Wpf.Media.PaletteSwatchClickEventArgs
    {
        public PaletteSwatchClickEventArgs(ZeroUI.Wpf.Media.PaletteSwatchItem item) : base(item) { }
    }

    /// <summary>
    /// Forwarding shim for <see cref="ZeroUI.Wpf.Media.ExifTelemetryItem"/>.
    /// </summary>
    [Obsolete("ExifTelemetryItem has moved to ZeroUI.Wpf.Media. Please update your using directives.")]
    public class ExifTelemetryItem : ZeroUI.Wpf.Media.ExifTelemetryItem { }

    /// <summary>
    /// Forwarding shim for <see cref="ZeroUI.Wpf.Media.GpsMapRequestedEventArgs"/>.
    /// </summary>
    [Obsolete("GpsMapRequestedEventArgs has moved to ZeroUI.Wpf.Media. Please update your using directives.")]
    public class GpsMapRequestedEventArgs : ZeroUI.Wpf.Media.GpsMapRequestedEventArgs
    {
        public GpsMapRequestedEventArgs(double lat, double lon) : base(lat, lon) { }
    }

    /// <summary>
    /// Forwarding shim for <see cref="ZeroUI.Wpf.Media.TokenChipItem"/>.
    /// </summary>
    [Obsolete("TokenChipItem has moved to ZeroUI.Wpf.Media. Please update your using directives.")]
    public class TokenChipItem : ZeroUI.Wpf.Media.TokenChipItem { }

    /// <summary>
    /// Forwarding shim for <see cref="ZeroUI.Wpf.Media.HistoryTimelineItemModel"/>.
    /// </summary>
    [Obsolete("HistoryTimelineItemModel has moved to ZeroUI.Wpf.Media. Please update your using directives.")]
    public class HistoryTimelineItemModel : ZeroUI.Wpf.Media.HistoryTimelineItemModel { }

    /// <summary>
    /// Forwarding shim for <see cref="ZeroUI.Wpf.Media.HistorySnapshotItemModel"/>.
    /// </summary>
    [Obsolete("HistorySnapshotItemModel has moved to ZeroUI.Wpf.Media. Please update your using directives.")]
    public class HistorySnapshotItemModel : ZeroUI.Wpf.Media.HistorySnapshotItemModel { }

    #endregion

    #endregion
}
