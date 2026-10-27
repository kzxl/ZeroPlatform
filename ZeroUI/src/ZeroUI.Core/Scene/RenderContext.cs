using System;

namespace ZeroUI.Core.Scene
{
    /// <summary>
    /// Contextual rendering parameters passed to scene nodes during paint passes.
    /// </summary>
    public readonly struct RenderContext
    {
        public readonly SceneRect ViewportBounds;
        public readonly float ZoomFactor;
        public readonly bool IsDarkTheme;
        public readonly long FrameTimeMs;
        public readonly bool HighQuality;

        public RenderContext(
            in SceneRect viewportBounds,
            float zoomFactor,
            bool isDarkTheme,
            long frameTimeMs = 0,
            bool highQuality = true)
        {
            ViewportBounds = viewportBounds;
            ZoomFactor = zoomFactor;
            IsDarkTheme = isDarkTheme;
            FrameTimeMs = frameTimeMs;
            HighQuality = highQuality;
        }
    }
}
