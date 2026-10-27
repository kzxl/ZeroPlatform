using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using ZeroOcr.Core.Imaging;
using ZeroOcr.Core.Interfaces;
using ZeroOcr.Core.Models;
using ZeroOcr.Inference.Abstractions;
using ZeroOcr.Inference.Classifiers;
using ZeroOcr.Inference.Detectors;
using ZeroOcr.Inference.Geometry;
using ZeroOcr.Inference.ModelHub;
using ZeroOcr.Inference.Options;
using ZeroOcr.Inference.PostProcessing;
using ZeroOcr.Inference.Recognizers;
using ZeroOcr.Inference.Vocab;

namespace ZeroOcr.Inference.Engines;

/// <summary>
/// Sovereign high-performance deep learning OCR engine for ZeroPlatform.
/// Replaces legacy Windows.Media.Ocr with cross-platform 2-stage DBNet++ and RepSVTR models.
/// Supports industrial inkjet dot-matrix fusion, full Vietnamese diacritics, and true token confidence.
/// </summary>
public sealed class ZeroDeepOcrEngine : IOcrEngine, IDisposable
{
    private readonly ITextDetector _detector;
    private readonly ITextRecognizer _recognizer;
    private readonly ITextDirectionClassifier? _classifier;
    private readonly bool _ownsComponents;
    private bool _disposed;

    public string Name => "ZeroPlatform.DeepOcr.Sovereign";

    public bool IsAvailable => _detector.IsReady && _recognizer.IsReady;

    public IReadOnlyList<string> SupportedLanguages => _recognizer.SupportedLanguages;

    public ZeroDeepOcrEngine(
        ITextDetector? detector = null,
        ITextRecognizer? recognizer = null,
        ITextDirectionClassifier? classifier = null,
        bool ownsComponents = true)
    {
        _detector = detector ?? new DbNetTextDetector();
        _recognizer = recognizer ?? new SvtrTextRecognizer();
        _classifier = classifier;
        _ownsComponents = ownsComponents;
    }

    /// <summary>
    /// Creates an engine instance by discovering or loading model files from a ModelBundle.
    /// </summary>
    public static ZeroDeepOcrEngine FromModelBundle(
        DeepOcrModelBundle? bundle = null,
        bool preferGpu = true)
    {
        bundle ??= DeepOcrModelBundle.Discover();
        var sessionOpts = DeepOcrSessionOptions.Create(preferGpu);

        var detector = new DbNetTextDetector(bundle.DetectionModelPath, sessionOpts);
        var recognizer = new SvtrTextRecognizer(
            bundle.RecognitionModelPath,
            bundle.HasVocab ? VietnameseCharacterMap.FromFile(bundle.VocabPath!) : VietnameseCharacterMap.Default,
            sessionOpts);

        TextDirectionClassifier? classifier = bundle.HasClassifierModel
            ? new TextDirectionClassifier(bundle.ClassifierModelPath, sessionOpts)
            : null;

        return new ZeroDeepOcrEngine(detector, recognizer, classifier, ownsComponents: true);
    }

    public bool IsLanguageSupported(string languageTag)
    {
        if (string.IsNullOrWhiteSpace(languageTag)) return true;
        foreach (var lang in SupportedLanguages)
        {
            if (string.Equals(lang, languageTag, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    public async Task<OcrResult> RecognizeAsync(
        OcrImageBuffer image,
        OcrOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (image == null) throw new ArgumentNullException(nameof(image));
        var sw = Stopwatch.StartNew();

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!IsAvailable)
                return OcrResult.Failed("Deep OCR Engine is not initialized or ready.", sw.Elapsed);

            var deepOptions = options as ZeroDeepOcrOptions;

            // 1. Region of Interest (ROI) Cropping
            OcrImageBuffer workingBuffer = image;
            bool isRentedCropped = false;

            if (options?.RegionOfInterest is { } roi && !roi.IsEmpty)
            {
                workingBuffer = image.Crop(roi);
                isRentedCropped = true;
            }

            // 2. Pre-processing: Morphological Dot-Matrix Bridging for Industrial Packaging
            bool applyDotMatrix = deepOptions?.ApplyDotMatrixFusion ??
                                 (deepOptions?.Preset == DeepOcrPreset.IndustrialDotMatrix);

            OcrImageBuffer? morphedBuffer = null;
            if (applyDotMatrix && workingBuffer.Format == OcrPixelFormat.Gray8)
            {
                morphedBuffer = OcrMorphology.Close(workingBuffer, StructuringElement.Cross3x3());
            }

            OcrImageBuffer detectionSource = morphedBuffer ?? workingBuffer;

            // 3. Stage 1: Text Detection (DBNet++) -> Oriented Quadrilaterals
            float detThreshold = deepOptions?.DetectionThreshold ?? 0.3f;
            if (_detector is DbNetTextDetector dbNet && deepOptions != null)
            {
                dbNet.UnclipRatio = deepOptions.UnclipRatio;
            }
            var quads = await _detector.DetectQuadsAsync(detectionSource, detThreshold, cancellationToken);

            if (quads.Count == 0)
            {
                morphedBuffer?.Dispose();
                if (isRentedCropped) workingBuffer.Dispose();
                return OcrResult.Create(Array.Empty<OcrLine>(), sw.Elapsed, options?.LanguageTag);
            }

            // 4. Stage 2: Perspective Rectification & Sequence Recognition
            var lines = new List<OcrLine>(quads.Count);
            float minConfidence = options?.MinConfidence ?? 0.0f;

            if (_recognizer is SvtrTextRecognizer svtr)
            {
                svtr.BlankGapThreshold = deepOptions?.BlankGapThreshold ?? 2;
            }

            foreach (var quad in quads)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Extract & rectify angled text region to a straight horizontal strip
                using var linePatch = QuadPerspectiveTransformer.RectifyQuad(workingBuffer, quad, targetHeight: 48);

                // Optional 180° Direction Rectification
                if (_classifier != null && (deepOptions == null || deepOptions.EnableDirectionClassifier))
                {
                    _classifier.RectifyInPlace(linePatch);
                }

                // Sequence Recognition (RepSVTR)
                var line = await _recognizer.RecognizeLineAsync(linePatch, quad, cancellationToken);

                if (line != null && line.Confidence >= minConfidence)
                {
                    if (deepOptions?.Preset == DeepOcrPreset.IndustrialDotMatrix)
                    {
                        string disambiguated = IndustrialLexiconMatcher.DisambiguateDateCode(line.Text);
                        if (!string.Equals(disambiguated, line.Text, StringComparison.Ordinal))
                        {
                            line = new OcrLine(disambiguated, line.Words, line.BoundingBox, line.Confidence, line.AngleDegrees);
                        }
                    }

                    lines.Add(line);
                }
            }

            morphedBuffer?.Dispose();
            if (isRentedCropped) workingBuffer.Dispose();

            // 5. Stage 3: Baseline Aggregation & Reading-Order Sorting
            IReadOnlyList<OcrLine> finalLines = lines;
            if (deepOptions == null || deepOptions.MergeHorizontalLines)
            {
                float baselineOffset = deepOptions?.MaxBaselineOffsetRatio ?? 0.45f;
                float horizGap = deepOptions?.MaxHorizontalGapRatio ?? 3.0f;
                finalLines = OcrLineMerger.MergeHorizontalLines(lines, baselineOffset, horizGap);
            }
            else
            {
                finalLines = OcrLineMerger.SortReadingOrder(lines);
            }

            sw.Stop();
            return OcrResult.Create(finalLines, sw.Elapsed, options?.LanguageTag);
        }
        catch (OperationCanceledException)
        {
            return OcrResult.Failed("OCR operation was canceled.", sw.Elapsed);
        }
        catch (Exception ex)
        {
            return OcrResult.Failed($"Deep OCR processing failed: {ex.Message}", sw.Elapsed);
        }
    }

    public Task<OcrResult> RecognizeAsync(
        ReadOnlyMemory<byte> encodedImageBytes,
        OcrOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        // For encoded images, caller can wrap bytes via OcrImageBuffer or dedicated codec
        return Task.FromResult(OcrResult.Failed(
            "Direct encoded byte recognition requires decoding into OcrImageBuffer before calling ZeroDeepOcrEngine.",
            TimeSpan.Zero));
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            if (_ownsComponents)
            {
                _detector.Dispose();
                _recognizer.Dispose();
                _classifier?.Dispose();
            }
        }
    }
}
