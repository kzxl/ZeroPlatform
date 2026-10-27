using System;
using System.Threading;
using System.Threading.Tasks;
using ZeroGraphics.Imaging.Core;
using ZeroOcr.Core.Engines;
using ZeroOcr.Core.Imaging;
using ZeroOcr.Core.Inspection;
using ZeroOcr.Core.Interfaces;
using ZeroOcr.Core.Models;
using ZeroPipeline.Core.Execution;
using ZeroPipeline.Core.Nodes;

namespace ZeroPipeline.Nodes.Inspection;

/// <summary>
/// Operational verification mode for automated OCR inspection.
/// </summary>
public enum OcrInspectionMode
{
    Substring = 0,
    Regex = 1,
    ExpiryDate = 2
}

/// <summary>
/// Industrial pipeline node that performs automated Optical Character Recognition (OCR)
/// and evaluates inspection criteria (Part number, LOT code, Expiration date).
/// </summary>
public sealed class OcrInspectionNode : TransformNode<ImageBuffer, InspectionResult>
{
    public string InspectionName { get; set; }
    public string ExpectedPattern { get; set; }
    public OcrInspectionMode Mode { get; set; }
    public string? LanguageTag { get; set; }
    public string? EngineName { get; set; }
    public float? RoiX { get; set; }
    public float? RoiY { get; set; }
    public float? RoiWidth { get; set; }
    public float? RoiHeight { get; set; }

    public OcrInspectionNode(
        string inspectionName = "OcrInspection",
        string expectedPattern = "",
        OcrInspectionMode mode = OcrInspectionMode.Substring,
        string? name = null)
        : base(name ?? $"Ocr<{inspectionName}>")
    {
        InspectionName = inspectionName ?? throw new ArgumentNullException(nameof(inspectionName));
        ExpectedPattern = expectedPattern;
        Mode = mode;
    }

    protected override async Task<InspectionResult> ProcessAsync(
        ImageBuffer input,
        PipelineContext context,
        CancellationToken cancellationToken)
    {
        if (input == null)
        {
            return InspectionResult.Fail(InspectionName, 0, 0, 0, "status", reason: "Input ImageBuffer is null.");
        }

        var engine = OcrEngineRegistry.Default.GetEngine(EngineName);
        if (engine == null || !engine.IsAvailable)
        {
            return InspectionResult.Fail(
                InspectionName,
                0, 0, 0, "status",
                reason: $"OCR Engine '{(EngineName ?? "Default")}' is unavailable.");
        }

        // Convert ZeroGraphics ImageBuffer to ZeroOcr OcrImageBuffer
        OcrImageBuffer ocrBuffer;
        if (input.RawBytes != null)
        {
            var format = input.Format == ImageFormatMode.Bgra32 ? OcrPixelFormat.Bgra32 : OcrPixelFormat.Gray8;
            ocrBuffer = new OcrImageBuffer(input.Width, input.Height, input.Stride, format, input.RawBytes);
        }
        else
        {
            // Unmanaged buffer: copy into pooled OcrImageBuffer
            int bpp = input.BytesPerPixel;
            int totalBytes = input.Stride * input.Height;
            byte[] bytes = new byte[totalBytes];
            unsafe
            {
                fixed (byte* pDst = bytes)
                {
                    Buffer.MemoryCopy(input.Scan0, pDst, totalBytes, totalBytes);
                }
            }
            var format = input.Format == ImageFormatMode.Bgra32 ? OcrPixelFormat.Bgra32 : OcrPixelFormat.Gray8;
            ocrBuffer = new OcrImageBuffer(input.Width, input.Height, input.Stride, format, bytes);
        }

        var options = new OcrOptions
        {
            LanguageTag = LanguageTag
        };

        if (RoiX.HasValue && RoiY.HasValue && RoiWidth.HasValue && RoiHeight.HasValue &&
            RoiWidth.Value > 0 && RoiHeight.Value > 0)
        {
            options.RegionOfInterest = new OcrRect(RoiX.Value, RoiY.Value, RoiWidth.Value, RoiHeight.Value);
        }

        var ocrResult = await engine.RecognizeAsync(ocrBuffer, options, cancellationToken);
        if (!ocrResult.Success)
        {
            return InspectionResult.Fail(InspectionName, 0, 0, 0, "status", reason: ocrResult.ErrorMessage ?? "OCR recognition failed.");
        }

        OcrInspectionVerdict verdict = Mode switch
        {
            OcrInspectionMode.Regex => OcrInspectionJudge.JudgeRegex(ocrResult, ExpectedPattern, InspectionName),
            OcrInspectionMode.ExpiryDate => OcrInspectionJudge.JudgeExpiryDate(ocrResult, DateTime.UtcNow.Date, InspectionName),
            _ => OcrInspectionJudge.JudgeSubstring(ocrResult, ExpectedPattern, InspectionName)
        };

        return verdict.IsPassed
            ? InspectionResult.Pass(InspectionName, ocrResult.MeanConfidence, 0.8, 1.0, "score", lotNumber: verdict.Actual)
            : InspectionResult.Fail(InspectionName, ocrResult.MeanConfidence, 0.8, 1.0, "score", reason: verdict.Message);
    }
}
