using System;
using System.Collections.Generic;
using ZeroGraphics.Imaging.Controls;
using ZeroGraphics.Imaging.Gpu;

namespace ZeroGraphics.Vision.Pipeline
{
    /// <summary>
    /// Seamless integration extensions between ZeroCameraCanvas and InspectionPipelineHost.
    /// </summary>
    public static class CameraCanvasPipelineExtensions
    {
        /// <summary>
        /// Binds a ZeroCameraCanvas control to the asynchronous InspectionPipelineHost.
        /// Automatically receives the latest camera frame and Pass/Fail visual overlays at smooth 60 FPS
        /// completely decoupled from the industrial camera inspection workers.
        /// </summary>
        public static void BindPipeline(this ZeroCameraCanvas canvas, InspectionPipelineHost pipeline)
        {
            if (canvas == null) throw new ArgumentNullException(nameof(canvas));
            if (pipeline == null) throw new ArgumentNullException(nameof(pipeline));

            pipeline.OnDisplayFrameReady += (pScan0, w, h, fmt, stride, overlays) =>
            {
                if (canvas.IsDisposed || !canvas.IsHandleCreated) return;

                // Deliver frame to DirectX 11 SwapChain
                canvas.SetFrame(pScan0, w, h, fmt, stride);

                // Set bounding box overlays
                if (overlays != null && overlays.Count > 0)
                {
                    canvas.SetOverlays(overlays);
                }
                else
                {
                    canvas.ClearOverlays();
                }
            };
        }
    }
}
