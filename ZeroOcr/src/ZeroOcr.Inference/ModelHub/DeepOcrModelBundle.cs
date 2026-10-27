using System;
using System.IO;

namespace ZeroOcr.Inference.ModelHub;

/// <summary>
/// Manages the resolution and lifetime of sovereign OCR neural model files and vocabulary tables.
/// Provides automatic directory scanning across local, application, and ecosystem model stores.
/// </summary>
public sealed class DeepOcrModelBundle
{
    public string? DetectionModelPath { get; set; }
    public string? RecognitionModelPath { get; set; }
    public string? ClassifierModelPath { get; set; }
    public string? VocabPath { get; set; }

    public bool HasDetectionModel => !string.IsNullOrEmpty(DetectionModelPath) && File.Exists(DetectionModelPath);
    public bool HasRecognitionModel => !string.IsNullOrEmpty(RecognitionModelPath) && File.Exists(RecognitionModelPath);
    public bool HasClassifierModel => !string.IsNullOrEmpty(ClassifierModelPath) && File.Exists(ClassifierModelPath);
    public bool HasVocab => !string.IsNullOrEmpty(VocabPath) && File.Exists(VocabPath);

    /// <summary>
    /// Scans standard directory hierarchies to discover available ONNX OCR model weights automatically.
    /// </summary>
    /// <param name="customDirectory">Optional custom directory to check first.</param>
    public static DeepOcrModelBundle Discover(string? customDirectory = null)
    {
        var bundle = new DeepOcrModelBundle();

        string[] candidateDirs = GetCandidateDirectories(customDirectory);

        foreach (var dir in candidateDirs)
        {
            if (!Directory.Exists(dir)) continue;

            if (bundle.DetectionModelPath == null)
            {
                bundle.DetectionModelPath = FindFile(dir, "dbnet", "det", "*.onnx");
            }

            if (bundle.RecognitionModelPath == null)
            {
                bundle.RecognitionModelPath = FindFile(dir, "svtr", "rec", "*.onnx");
            }

            if (bundle.ClassifierModelPath == null)
            {
                bundle.ClassifierModelPath = FindFile(dir, "cls", "direction", "*.onnx");
            }

            if (bundle.VocabPath == null)
            {
                bundle.VocabPath = FindFile(dir, "dict", "keys", "*.txt");
            }

            if (bundle.HasDetectionModel && bundle.HasRecognitionModel)
                break;
        }

        return bundle;
    }

    private static string[] GetCandidateDirectories(string? customDirectory)
    {
        var list = new System.Collections.Generic.List<string>();

        if (!string.IsNullOrEmpty(customDirectory))
            list.Add(customDirectory!);

        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        list.Add(Path.Combine(baseDir, "models", "ocr"));
        list.Add(Path.Combine(baseDir, "models"));
        list.Add(Path.Combine(baseDir, "assets", "models"));

        string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrEmpty(appData))
        {
            list.Add(Path.Combine(appData, "ZeroPlatform", "models", "ocr"));
        }

        return list.ToArray();
    }

    private static string? FindFile(string dir, string primaryKeyword, string secondaryKeyword, string pattern)
    {
        try
        {
            var files = Directory.GetFiles(dir, pattern, SearchOption.TopDirectoryOnly);
            foreach (var f in files)
            {
                string name = Path.GetFileNameWithoutExtension(f).ToLowerInvariant();
                if (name.Contains(primaryKeyword) || name.Contains(secondaryKeyword))
                    return f;
            }

            if (files.Length == 1 && pattern == "*.onnx")
                return files[0];
        }
        catch
        {
            // Suppress IO discovery exceptions
        }

        return null;
    }
}
