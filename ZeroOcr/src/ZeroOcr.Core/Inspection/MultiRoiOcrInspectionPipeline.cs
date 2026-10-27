using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ZeroOcr.Core.Imaging;
using ZeroOcr.Core.Interfaces;
using ZeroOcr.Core.Models;

namespace ZeroOcr.Core.Inspection;

/// <summary>
/// Preprocessing strategy customized for specific industrial text styles in an ROI.
/// </summary>
public enum RoiPreprocessingMode
{
    Standard = 0,
    DotMatrixInkjet = 1,      // Grayscale -> Otsu -> Morphological Closing -> Deskew
    LaserEtchedFoil = 2,      // High contrast -> Adaptive/Otsu -> Deskew
    InvertedHighContrast = 3  // Invert -> Otsu -> Deskew
}

/// <summary>
/// Specification describing how a single inspection zone is located, preprocessed, OCR-recognized, and evaluated.
/// </summary>
public sealed class RoiInspectionSpec
{
    public string ZoneName { get; }
    public OcrRect Region { get; }
    public RoiPreprocessingMode PreprocessingMode { get; }
    public Func<OcrResult, OcrInspectionVerdict> EvaluationRule { get; }
    public OcrOptions Options { get; }

    public RoiInspectionSpec(
        string zoneName,
        OcrRect region,
        RoiPreprocessingMode preprocessingMode,
        Func<OcrResult, OcrInspectionVerdict> evaluationRule,
        OcrOptions? options = null)
    {
        ZoneName = zoneName ?? throw new ArgumentNullException(nameof(zoneName));
        Region = region;
        PreprocessingMode = preprocessingMode;
        EvaluationRule = evaluationRule ?? throw new ArgumentNullException(nameof(evaluationRule));
        Options = options ?? new OcrOptions();
    }
}

/// <summary>
/// Comprehensive outcome of a multi-ROI high-resolution image inspection.
/// </summary>
public sealed class MultiRoiInspectionOutcome
{
    public bool IsAllPassed { get; }
    public TimeSpan TotalElapsed { get; }
    public IReadOnlyList<ZoneInspectionResult> ZoneResults { get; }

    public MultiRoiInspectionOutcome(bool isAllPassed, TimeSpan totalElapsed, IReadOnlyList<ZoneInspectionResult> zoneResults)
    {
        IsAllPassed = isAllPassed;
        TotalElapsed = totalElapsed;
        ZoneResults = zoneResults;
    }
}

public sealed class ZoneInspectionResult
{
    public string ZoneName { get; }
    public OcrRect ZoneBounds { get; }
    public OcrInspectionVerdict Verdict { get; }
    public OcrResult LocalOcrResult { get; }
    public IReadOnlyList<OcrLine> RemappedLines { get; }
    public TimeSpan ExecutionTime { get; }

    public ZoneInspectionResult(
        string zoneName,
        OcrRect zoneBounds,
        OcrInspectionVerdict verdict,
        OcrResult localOcrResult,
        IReadOnlyList<OcrLine> remappedLines,
        TimeSpan executionTime)
    {
        ZoneName = zoneName;
        ZoneBounds = zoneBounds;
        Verdict = verdict;
        LocalOcrResult = localOcrResult;
        RemappedLines = remappedLines;
        ExecutionTime = executionTime;
    }
}

/// <summary>
/// Enterprise parallel OCR inspection pipeline coordinating concurrent multi-zone inspection on large sensor images.
/// </summary>
public sealed class MultiRoiOcrInspectionPipeline
{
    private readonly ConcurrentBag<IOcrEngine> _enginePool;
    private readonly int _maxDegreeOfParallelism;

    public MultiRoiOcrInspectionPipeline(IEnumerable<IOcrEngine> engineInstances, int? maxDegreeOfParallelism = null)
    {
        if (engineInstances == null) throw new ArgumentNullException(nameof(engineInstances));
        _enginePool = new ConcurrentBag<IOcrEngine>(engineInstances);
        if (_enginePool.IsEmpty)
            throw new ArgumentException("Engine pool must contain at least one IOcrEngine instance.", nameof(engineInstances));

        _maxDegreeOfParallelism = maxDegreeOfParallelism ?? Math.Max(1, Math.Min(Environment.ProcessorCount, _enginePool.Count));
    }

    /// <summary>
    /// Executes parallel inspection of all configured ROIs on a single high-resolution master frame.
    /// </summary>
    public async Task<MultiRoiInspectionOutcome> InspectAsync(
        OcrImageBuffer masterImage,
        IReadOnlyList<RoiInspectionSpec> specs,
        CancellationToken cancellationToken = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var results = new ConcurrentBag<ZoneInspectionResult>();

        var tasks = new List<Task>();
        var throttler = new SemaphoreSlim(_maxDegreeOfParallelism, _maxDegreeOfParallelism);

        foreach (var spec in specs)
        {
            await throttler.WaitAsync(cancellationToken);

            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var zoneSw = System.Diagnostics.Stopwatch.StartNew();

                    // 1. Crop sub-buffer from master frame
                    var cropped = masterImage.Crop(spec.Region);

                    // 2. Specialized preprocessing per ROI
                    var preprocessed = PreprocessZone(cropped, spec.PreprocessingMode);

                    // 3. Acquire OCR Engine from pool
                    var engine = RentEngine();
                    OcrResult ocrResult;
                    try
                    {
                        var zoneOptions = spec.Options;
                        zoneOptions.CustomProperties ??= new Dictionary<string, object>();
                        zoneOptions.CustomProperties["Zone"] = spec.ZoneName;
                        ocrResult = await engine.RecognizeAsync(preprocessed, zoneOptions, cancellationToken);
                    }
                    finally
                    {
                        ReturnEngine(engine);
                    }

                    // 4. Remap coordinates to master frame
                    var remappedLines = RemapToMaster(ocrResult.Lines, spec.Region);

                    // 5. Evaluate domain rules (Expiry date, Lot regex, presence check)
                    var verdict = spec.EvaluationRule(ocrResult);

                    zoneSw.Stop();
                    results.Add(new ZoneInspectionResult(spec.ZoneName, spec.Region, verdict, ocrResult, remappedLines, zoneSw.Elapsed));
                }
                finally
                {
                    throttler.Release();
                }
            }, cancellationToken));
        }

        await Task.WhenAll(tasks);
        sw.Stop();

        var zoneList = results.OrderBy(r => r.ZoneName).ToList();
        bool allPassed = zoneList.All(z => z.Verdict.IsPassed);

        return new MultiRoiInspectionOutcome(allPassed, sw.Elapsed, zoneList);
    }

    private OcrImageBuffer PreprocessZone(OcrImageBuffer input, RoiPreprocessingMode mode)
    {
        switch (mode)
        {
            case RoiPreprocessingMode.DotMatrixInkjet:
            {
                var gray = OcrPreprocessor.ToGrayscale(input);
                var binary = OcrPreprocessor.BinarizeOtsu(gray);
                var closed = OcrMorphology.Close(binary, StructuringElement.Rectangle(3, 3));
                var skew = ProjectionProfileDeskewer.DetectSkewAngle(closed, -10, 10, 1.0, 0.2);
                return ProjectionProfileDeskewer.RotateDeskew(closed, skew.AngleDegrees);
            }

            case RoiPreprocessingMode.LaserEtchedFoil:
            {
                var gray = OcrPreprocessor.ToGrayscale(input);
                var binary = OcrPreprocessor.BinarizeOtsu(gray);
                var skew = ProjectionProfileDeskewer.DetectSkewAngle(binary, -15, 15, 1.0, 0.2);
                return ProjectionProfileDeskewer.RotateDeskew(binary, skew.AngleDegrees);
            }

            case RoiPreprocessingMode.InvertedHighContrast:
            {
                var gray = OcrPreprocessor.ToGrayscale(input);
                var inverted = OcrPreprocessor.Invert(gray);
                var binary = OcrPreprocessor.BinarizeOtsu(inverted);
                return binary;
            }

            case RoiPreprocessingMode.Standard:
            default:
            {
                var gray = OcrPreprocessor.ToGrayscale(input);
                return OcrPreprocessor.BinarizeOtsu(gray);
            }
        }
    }

    private static IReadOnlyList<OcrLine> RemapToMaster(IReadOnlyList<OcrLine> localLines, OcrRect roi)
    {
        var list = new List<OcrLine>(localLines.Count);
        foreach (var line in localLines)
        {
            var remappedWords = new List<OcrWord>(line.Words.Count);
            foreach (var word in line.Words)
            {
                var wBox = word.BoundingBox;
                var masterBox = new OcrRect(wBox.X + roi.X, wBox.Y + roi.Y, wBox.Width, wBox.Height);
                remappedWords.Add(new OcrWord(word.Text, masterBox, word.Confidence));
            }

            var lBox = line.BoundingBox;
            var masterLineBox = new OcrRect(lBox.X + roi.X, lBox.Y + roi.Y, lBox.Width, lBox.Height);
            list.Add(new OcrLine(line.Text, remappedWords, masterLineBox, line.Confidence, line.AngleDegrees));
        }

        return list;
    }

    private IOcrEngine RentEngine()
    {
        if (_enginePool.TryTake(out var engine))
            return engine;

        throw new InvalidOperationException("OCR Engine pool exhausted.");
    }

    private void ReturnEngine(IOcrEngine engine)
    {
        _enginePool.Add(engine);
    }
}
