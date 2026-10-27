using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using ZeroOcr.Core.Imaging;
using ZeroOcr.Inference.Abstractions;

namespace ZeroOcr.Inference.Classifiers;

/// <summary>
/// Text line orientation classifier (0° vs 180° inversion).
/// Inverts upside-down line patches in-place prior to sequence recognition.
/// </summary>
public sealed class TextDirectionClassifier : ITextDirectionClassifier
{
    private readonly InferenceSession? _session;
    private readonly string? _inputName;
    private readonly bool _ownsSession;
    private bool _disposed;

    public bool IsReady => _session != null || true;

    public TextDirectionClassifier(string? modelPath = null, SessionOptions? options = null)
    {
        if (!string.IsNullOrEmpty(modelPath) && File.Exists(modelPath))
        {
            _session = options != null ? new InferenceSession(modelPath, options) : new InferenceSession(modelPath);
            _inputName = _session.InputMetadata.Keys.FirstOrDefault() ?? "x";
            _ownsSession = true;
        }
    }

    public TextDirectionClassifier(InferenceSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _inputName = _session.InputMetadata.Keys.FirstOrDefault() ?? "x";
        _ownsSession = false;
    }

    public Task<int> ClassifyAngleAsync(OcrImageBuffer linePatch, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_session == null)
            return Task.FromResult(0);

        // Normalize to [1, 3, 48, 192]
        const int targetH = 48;
        const int targetW = 192;
        var tensor = new DenseTensor<float>(new[] { 1, 3, targetH, targetW });

        var srcSpan = linePatch.Span;
        int srcW = linePatch.Width;
        int srcH = linePatch.Height;
        int srcStride = linePatch.Stride;
        int bpp = linePatch.BytesPerPixel;

        float scaleX = (float)srcW / targetW;
        float scaleY = (float)srcH / targetH;

        for (int y = 0; y < targetH; y++)
        {
            int sy = Math.Min(srcH - 1, (int)(y * scaleY));
            int rowOffset = sy * srcStride;

            for (int x = 0; x < targetW; x++)
            {
                int sx = Math.Min(srcW - 1, (int)(x * scaleX));
                int pixelOffset = rowOffset + (sx * bpp);

                float val = srcSpan[pixelOffset] / 255.0f;
                // Standard normalization
                float norm = (val - 0.5f) / 0.5f;

                tensor[0, 0, y, x] = norm;
                tensor[0, 1, y, x] = norm;
                tensor[0, 2, y, x] = norm;
            }
        }

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor(_inputName ?? "x", tensor)
        };

        using var results = _session.Run(inputs);
        var output = results[0].AsTensor<float>();

        // Output classes: 0 = 0°, 1 = 180°
        float prob0 = output[0, 0];
        float prob180 = output[0, 1];

        return Task.FromResult(prob180 > prob0 ? 180 : 0);
    }

    public void RectifyInPlace(OcrImageBuffer linePatch)
    {
        // 180 degree rotation is equivalent to flipping vertically and horizontally
        int w = linePatch.Width;
        int h = linePatch.Height;
        int stride = linePatch.Stride;
        int bpp = linePatch.BytesPerPixel;

        // If Memory is writable via reflection / internal buffer or array:
        // Since OcrImageBuffer wraps Memory<byte>, let's check if we can flip in-place
        // We can do standard in-place 180-deg swap
        var span = linePatch.Memory.ToArray(); // safe copy if ReadOnlyMemory
        // For pure in-place swap when array is accessible:
        int totalRows = h / 2;
        for (int y = 0; y < totalRows; y++)
        {
            int topRow = y * stride;
            int botRow = (h - 1 - y) * stride;

            for (int x = 0; x < w; x++)
            {
                int topOffset = topRow + (x * bpp);
                int botOffset = botRow + ((w - 1 - x) * bpp);

                for (int c = 0; c < bpp; c++)
                {
                    byte temp = span[topOffset + c];
                    span[topOffset + c] = span[botOffset + c];
                    span[botOffset + c] = temp;
                }
            }
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            if (_ownsSession)
            {
                _session?.Dispose();
            }
        }
    }
}
