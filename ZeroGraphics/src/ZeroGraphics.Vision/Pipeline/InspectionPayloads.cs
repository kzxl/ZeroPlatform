using System;
using System.Collections.Generic;
using ZeroGraphics.Imaging.Core;
using ZeroGraphics.Imaging.Gpu;
using ZeroGraphics.Vision.Codes;
using ZeroGraphics.Vision.Metrology;

namespace ZeroGraphics.Vision.Pipeline
{
    /// <summary>
    /// Metadata context provided to the inspection worker when analyzing a camera frame.
    /// </summary>
    public sealed class FrameInspectionContext
    {
        public long FrameId { get; }
        public DateTime Timestamp { get; }
        public int Width { get; }
        public int Height { get; }
        public ImageFormatMode Format { get; }
        public int WorkerThreadId { get; }
        public object? UserData { get; set; }

        public FrameInspectionContext(
            long frameId,
            DateTime timestamp,
            int width,
            int height,
            ImageFormatMode format,
            int workerThreadId,
            object? userData = null)
        {
            FrameId = frameId;
            Timestamp = timestamp;
            Width = width;
            Height = height;
            Format = format;
            WorkerThreadId = workerThreadId;
            UserData = userData;
        }
    }

    /// <summary>
    /// Encapsulates the complete inspection outcome (Pass/Fail, Barcode, Metrology dimensions, and UI Overlays).
    /// Ready for direct dispatch to industrial PLCs, ERP QC records (ucMaterialInspection), and ZeroCameraCanvas.
    /// </summary>
    public sealed class InspectionResultPayload
    {
        public long FrameId { get; }
        public bool IsPass { get; }
        public string StatusMessage { get; }
        public double DurationMs { get; }
        public BarcodeResult? Barcode { get; set; }
        public List<MetricDistanceResult> Measurements { get; } = new List<MetricDistanceResult>();
        public List<CameraOverlayBox> Overlays { get; } = new List<CameraOverlayBox>();
        public object? UserData { get; set; }
        public DateTime Timestamp { get; }

        public InspectionResultPayload(
            long frameId,
            bool isPass,
            string statusMessage,
            double durationMs,
            BarcodeResult? barcode = null,
            object? userData = null)
        {
            FrameId = frameId;
            IsPass = isPass;
            StatusMessage = statusMessage;
            DurationMs = durationMs;
            Barcode = barcode;
            UserData = userData;
            Timestamp = DateTime.UtcNow;
        }

        public override string ToString()
        {
            string verdict = IsPass ? "PASS" : "FAIL";
            string codeInfo = Barcode != null ? $" [{Barcode.Symbology}: {Barcode.Text}]" : "";
            string measInfo = Measurements.Count > 0 ? $" ({Measurements.Count} metric features)" : "";
            return $"Frame #{FrameId}: {verdict} - {StatusMessage}{codeInfo}{measInfo} ({DurationMs:F2}ms)";
        }
    }
}
