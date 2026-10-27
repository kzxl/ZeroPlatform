using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using ZeroOcr.Core.Imaging;
using ZeroOcr.Core.Interfaces;
using ZeroOcr.Inference.Engines;
using ZeroOcr.Inference.ModelHub;
using ZeroOcr.Inference.Options;
using ZeroOcr.Windows;

namespace ZeroOcr.Tests;

public class VietnameseImageRecognitionTests
{
    private readonly ITestOutputHelper _output;

    public VietnameseImageRecognitionTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task Benchmark_VietnameseRealImages_CompareWindowsOcrAndDeepOcr()
    {
        string repoRoot = FindRepoRoot();
        string samplesDir = Path.Combine(repoRoot, "tests", "samples", "sample");
        if (!Directory.Exists(samplesDir))
        {
            _output.WriteLine($"Sample directory not found: {samplesDir}. Skipping benchmark.");
            return;
        }

        // Pick diverse Vietnamese real-world samples
        string[] testFiles = new[]
        {
            "2019_10_04_tran_quang_dung_201910041118369_29.jpg",
            "deskewed-2019_08_28_tran_thi_thanh_2019082813444611_9.jpg",
            "deskewed-2019_09_28_051998_201909281423187_11.jpg",
            "deskewed-2019_12_17_dinh_ngoc_anh_201912171124175_6.jpg",
            "030068003051.jpeg"
        };

        // Initialize Engines
        var winEngine = new WindowsOcrEngine();
        
        string modelDir = Path.Combine(repoRoot, "models", "ocr");
        var bundle = new DeepOcrModelBundle
        {
            DetectionModelPath = Path.Combine(modelDir, "dbnet_det.onnx"),
            RecognitionModelPath = Path.Combine(modelDir, "svtr_rec.onnx"),
            VocabPath = Path.Combine(modelDir, "dict.txt")
        };

        using var deepEngine = ZeroDeepOcrEngine.FromModelBundle(bundle, preferGpu: true);

        _output.WriteLine("================================================================================");
        _output.WriteLine("REAL VIETNAMESE IMAGE OCR BENCHMARK: Windows.Media.Ocr vs Sovereign Deep OCR");
        _output.WriteLine("================================================================================");

        foreach (var file in testFiles)
        {
            string fullPath = Path.Combine(samplesDir, file);
            if (!File.Exists(fullPath)) continue;

            using var imageBuffer = LoadBufferFromImageFile(fullPath);

            _output.WriteLine($"\n[IMAGE]: {file} ({imageBuffer.Width}x{imageBuffer.Height})");

            // 1. Run Windows.Media.Ocr
            if (winEngine.IsAvailable)
            {
                var winOptions = new OcrOptions { LanguageTag = "vi-VN" };
                var winResult = await winEngine.RecognizeAsync(imageBuffer, winOptions);

                _output.WriteLine($"  --- Microsoft Windows.Media.Ocr ({winResult.Elapsed.TotalMilliseconds:F1}ms) ---");
                _output.WriteLine($"  Success: {winResult.Success}, Lines: {winResult.Lines.Count}, Conf: {winResult.MeanConfidence:P0}");
                foreach (var line in winResult.Lines)
                {
                    _output.WriteLine($"    [WinRT]: \"{line.Text}\"");
                }
            }

            // 2. Run Sovereign Deep OCR
            var deepOptions = ZeroDeepOcrOptions.ForVietnameseDocument(minConfidence: 0.30f);
            var deepResult = await deepEngine.RecognizeAsync(imageBuffer, deepOptions);

            _output.WriteLine($"  --- Sovereign ZeroDeepOcr ({deepResult.Elapsed.TotalMilliseconds:F1}ms) ---");
            _output.WriteLine($"  Success: {deepResult.Success}, Lines: {deepResult.Lines.Count}, Real Conf: {deepResult.MeanConfidence:P1}");
            foreach (var line in deepResult.Lines)
            {
                _output.WriteLine($"    [DeepOcr]: \"{line.Text}\" (Conf: {line.Confidence:P1})");
            }
        }
    }

    private static string FindRepoRoot()
    {
        string? current = AppDomain.CurrentDomain.BaseDirectory;
        while (!string.IsNullOrEmpty(current))
        {
            if (File.Exists(Path.Combine(current, "ZeroOcr.slnx")) ||
                File.Exists(Path.Combine(current, "Directory.Build.props")) ||
                Directory.Exists(Path.Combine(current, ".git")))
            {
                return current;
            }
            current = Directory.GetParent(current)?.FullName;
        }
        return AppDomain.CurrentDomain.BaseDirectory;
    }

    private static OcrImageBuffer LoadBufferFromImageFile(string filePath)
    {
        using var bmp = new Bitmap(filePath);
        int w = bmp.Width;
        int h = bmp.Height;

        var rect = new Rectangle(0, 0, w, h);
        var data = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

        try
        {
            int stride = data.Stride;
            byte[] bytes = new byte[stride * h];
            Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);

            return OcrImageBuffer.FromBgra32(w, h, bytes, stride);
        }
        finally
        {
            bmp.UnlockBits(data);
        }
    }
}
