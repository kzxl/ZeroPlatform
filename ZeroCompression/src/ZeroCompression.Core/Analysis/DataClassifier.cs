using System;
using System.Buffers.Binary;
using System.IO;

namespace ZeroCompression.Core.Analysis
{
    /// <summary>
    /// Categories of data payloads identified by the heuristic classifier.
    /// </summary>
    public enum DetectedDataType
    {
        /// <summary>Numerical time-series, IoT/SCADA sensor feeds (timestamp + float64 pairs).</summary>
        TelemetryTimeSeries,
        /// <summary>Structured text with high JSON syntax density (JSON Lines, JSON objects/arrays).</summary>
        StructuredJson,
        /// <summary>Comma or tab-delimited tabular text (CSV, TSV).</summary>
        DelimitedText,
        /// <summary>Plain natural language or source code text (UTF-8/ASCII).</summary>
        PlainText,
        /// <summary>Native machine code executable or library (PE, ELF, DLL, EXE).</summary>
        ExecutableBinary,
        /// <summary>High-entropy payload that is already compressed, media, or encrypted.</summary>
        AlreadyCompressed,
        /// <summary>Generic or heterogeneous binary payload.</summary>
        GenericBinary
    }

    /// <summary>
    /// Detailed classification result and recommended compression configuration.
    /// </summary>
    public sealed class DataClassificationResult
    {
        public required DetectedDataType DetectedType { get; init; }
        public required CompressionMethod RecommendedMethod { get; init; }
        public required int RecommendedLevel { get; init; }
        public bool LongDistanceMatching { get; init; }
        public int WindowLog { get; init; }
        public double ShannonEntropy { get; init; }
        public double PrintableRatio { get; init; }
        public required string Reason { get; init; }

        public CompressionOptions CreateOptions(CompressionProfile baseProfile = CompressionProfile.Ultra)
        {
            if (RecommendedMethod == CompressionMethod.Store)
            {
                return new CompressionOptions
                {
                    Method = CompressionMethod.Store,
                    Level = 0,
                    Workers = 0
                };
            }

            var opt = CompressionOptions.FromProfile(baseProfile, RecommendedMethod);
            if (RecommendedLevel > 0)
            {
                opt.Level = RecommendedLevel;
            }
            opt.LongDistanceMatching = LongDistanceMatching;
            if (WindowLog > 0)
            {
                opt.WindowLog = WindowLog;
            }
            return opt;
        }

        public override string ToString() =>
            $"[{DetectedType}] -> {RecommendedMethod} (Level {RecommendedLevel}, LDM: {LongDistanceMatching}) - {Reason}";
    }

    /// <summary>
    /// Sub-millisecond heuristic analyzer and adaptive compression router.
    /// Analyzes magic headers, Shannon entropy, ASCII/JSON grammar, and numerical time-series patterns.
    /// </summary>
    public static class DataClassifier
    {
        public const int DefaultProbeSize = 65536; // 64 KiB
        public const double HighEntropyThreshold = 7.92; // Max is 8.0 bits/byte

        /// <summary>
        /// Classifies a memory slice of sample data and returns the optimal compression profile.
        /// </summary>
        public static DataClassificationResult Classify(ReadOnlySpan<byte> sample, string? fileNameOrExtension = null)
        {
            if (sample.IsEmpty)
            {
                return new DataClassificationResult
                {
                    DetectedType = DetectedDataType.GenericBinary,
                    RecommendedMethod = CompressionMethod.Zstd,
                    RecommendedLevel = 3,
                    Reason = "Payload is empty; falling back to default Zstandard codec."
                };
            }

            string ext = ExtractExtension(fileNameOrExtension);

            // 1. Magic Header Inspection
            if (IsKnownPrecompressedMagic(sample, ext, out string mediaReason))
            {
                return new DataClassificationResult
                {
                    DetectedType = DetectedDataType.AlreadyCompressed,
                    RecommendedMethod = CompressionMethod.Store,
                    RecommendedLevel = 0,
                    ShannonEntropy = ComputeShannonEntropy(sample),
                    Reason = mediaReason
                };
            }

            if (IsExecutableMagic(sample, ext, out string exeReason))
            {
                return new DataClassificationResult
                {
                    DetectedType = DetectedDataType.ExecutableBinary,
                    RecommendedMethod = CompressionMethod.Lzma,
                    RecommendedLevel = 9,
                    Reason = exeReason
                };
            }

            // 2. Shannon Entropy Computation
            double entropy = ComputeShannonEntropy(sample);
            if (entropy >= HighEntropyThreshold)
            {
                return new DataClassificationResult
                {
                    DetectedType = DetectedDataType.AlreadyCompressed,
                    RecommendedMethod = CompressionMethod.Store,
                    RecommendedLevel = 0,
                    ShannonEntropy = entropy,
                    Reason = $"High Shannon entropy ({entropy:F2}/8.0 bits/byte); payload is already compressed or encrypted. Selecting Store to bypass CPU overhead."
                };
            }

            // 3. Printable Text & JSON Grammar Analysis
            double printableRatio = ComputePrintableRatio(sample);
            if (printableRatio >= 0.85)
            {
                if (IsStructuredJson(sample, ext, out double jsonRatio))
                {
                    return new DataClassificationResult
                    {
                        DetectedType = DetectedDataType.StructuredJson,
                        RecommendedMethod = CompressionMethod.Zstd,
                        RecommendedLevel = 19,
                        LongDistanceMatching = true,
                        WindowLog = 27,
                        ShannonEntropy = entropy,
                        PrintableRatio = printableRatio,
                        Reason = $"Structured JSON text detected ({jsonRatio:P1} JSON syntax density). Activating Zstandard Ultra (Level 19) with Long-Distance Matching (LDM) to eliminate schema duplication."
                    };
                }

                if (IsDelimitedText(sample, ext))
                {
                    return new DataClassificationResult
                    {
                        DetectedType = DetectedDataType.DelimitedText,
                        RecommendedMethod = CompressionMethod.Zstd,
                        RecommendedLevel = 19,
                        LongDistanceMatching = true,
                        ShannonEntropy = entropy,
                        PrintableRatio = printableRatio,
                        Reason = "Delimited tabular data detected (CSV/TSV). Activating Zstandard Ultra with Long-Distance Matching."
                    };
                }

                return new DataClassificationResult
                {
                    DetectedType = DetectedDataType.PlainText,
                    RecommendedMethod = CompressionMethod.Brotli,
                    RecommendedLevel = 11,
                    ShannonEntropy = entropy,
                    PrintableRatio = printableRatio,
                    Reason = "Natural plain text or source code detected. Activating Brotli High Level for optimal text compression."
                };
            }

            // 4. Time-Series Telemetry Stride Analysis (16-byte pairs: long timestamp, double value)
            if (IsTelemetryTimeSeries(sample, ext, out string telemetryReason))
            {
                return new DataClassificationResult
                {
                    DetectedType = DetectedDataType.TelemetryTimeSeries,
                    RecommendedMethod = CompressionMethod.ZeroTelemetry,
                    RecommendedLevel = 0,
                    ShannonEntropy = entropy,
                    PrintableRatio = printableRatio,
                    Reason = telemetryReason
                };
            }

            // 5. Default Generic Binary
            return new DataClassificationResult
            {
                DetectedType = DetectedDataType.GenericBinary,
                RecommendedMethod = CompressionMethod.Zstd,
                RecommendedLevel = 12,
                LongDistanceMatching = false,
                ShannonEntropy = entropy,
                PrintableRatio = printableRatio,
                Reason = $"General binary payload (Entropy: {entropy:F2}). Applying balanced Zstandard Normal (Level 12)."
            };
        }

        /// <summary>
        /// Probes the beginning of a stream and classifies it without rewinding beyond the sample.
        /// </summary>
        public static DataClassificationResult Classify(Stream stream, string? fileNameOrExtension = null, int probeBytes = DefaultProbeSize)
        {
            ArgumentNullException.ThrowIfNull(stream);

            byte[] buffer = new byte[probeBytes];
            int read = 0;

            if (stream.CanSeek)
            {
                long originalPos = stream.Position;
                read = stream.Read(buffer, 0, buffer.Length);
                stream.Position = originalPos; // restore
            }
            else
            {
                read = stream.Read(buffer, 0, buffer.Length);
            }

            return Classify(buffer.AsSpan(0, read), fileNameOrExtension);
        }

        /// <summary>
        /// Probes a file on disk or the first file in a directory and classifies the payload.
        /// </summary>
        public static DataClassificationResult ClassifyPath(string path, int probeBytes = DefaultProbeSize)
        {
            if (File.Exists(path))
            {
                byte[] buffer = new byte[probeBytes];
                int read = 0;
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    read = fs.Read(buffer, 0, buffer.Length);
                }
                return Classify(buffer.AsSpan(0, read), Path.GetFileName(path));
            }

            if (Directory.Exists(path))
            {
                // Find first non-empty file in directory
                foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                {
                    try
                    {
                        var info = new FileInfo(file);
                        if (info.Length > 0)
                        {
                            return ClassifyPath(file, probeBytes);
                        }
                    }
                    catch { /* skip inaccessible */ }
                }

                return new DataClassificationResult
                {
                    DetectedType = DetectedDataType.GenericBinary,
                    RecommendedMethod = CompressionMethod.Zstd,
                    RecommendedLevel = 3,
                    Reason = "Source directory is empty or contains only empty subdirectories."
                };
            }

            throw new FileNotFoundException("Source path not found for analysis.", path);
        }

        #region Heuristic Sub-Engines

        public static double ComputeShannonEntropy(ReadOnlySpan<byte> data)
        {
            if (data.IsEmpty) return 0.0;

            Span<int> counts = stackalloc int[256];
            for (int i = 0; i < data.Length; i++)
            {
                counts[data[i]]++;
            }

            double entropy = 0.0;
            double invLength = 1.0 / data.Length;

            for (int i = 0; i < 256; i++)
            {
                int count = counts[i];
                if (count > 0)
                {
                    double p = count * invLength;
                    entropy -= p * Math.Log2(p);
                }
            }

            return entropy;
        }

        public static double ComputePrintableRatio(ReadOnlySpan<byte> data)
        {
            if (data.IsEmpty) return 0.0;

            int printable = 0;
            for (int i = 0; i < data.Length; i++)
            {
                byte b = data[i];
                // ASCII printable (0x20..0x7E) + common whitespace (\r, \n, \t)
                if ((b >= 0x20 && b <= 0x7E) || b == 0x0A || b == 0x0D || b == 0x09)
                {
                    printable++;
                }
            }

            return (double)printable / data.Length;
        }

        private static bool IsStructuredJson(ReadOnlySpan<byte> data, string ext, out double jsonRatio)
        {
            jsonRatio = 0.0;
            if (ext is ".csv" or ".tsv") return false;
            if (ext is ".json" or ".jsonl" or ".ndjson")
            {
                jsonRatio = 1.0;
                return true;
            }

            int structuralChars = 0;
            for (int i = 0; i < data.Length; i++)
            {
                byte b = data[i];
                if (b is (byte)'{' or (byte)'}' or (byte)'[' or (byte)']' or (byte)'"' or (byte)':')
                {
                    structuralChars++;
                }
            }

            jsonRatio = (double)structuralChars / data.Length;

            // Trim leading whitespace
            int start = 0;
            while (start < data.Length && (data[start] == ' ' || data[start] == '\r' || data[start] == '\n' || data[start] == '\t'))
                start++;

            bool startsWithJson = start < data.Length && (data[start] == '{' || data[start] == '[');

            return (startsWithJson && jsonRatio >= 0.02) || jsonRatio >= 0.04;
        }

        private static bool IsDelimitedText(ReadOnlySpan<byte> data, string ext)
        {
            if (ext is ".csv" or ".tsv") return true;

            int commas = 0;
            int tabs = 0;
            int newlines = 0;

            for (int i = 0; i < data.Length; i++)
            {
                byte b = data[i];
                if (b == (byte)',') commas++;
                else if (b == (byte)'\t') tabs++;
                else if (b == (byte)'\n') newlines++;
            }

            if (newlines < 2) return false;
            double commaFreq = (double)commas / newlines;
            double tabFreq = (double)tabs / newlines;

            return commaFreq >= 2.0 || tabFreq >= 2.0;
        }

        private static bool IsTelemetryTimeSeries(ReadOnlySpan<byte> data, string ext, out string reason)
        {
            reason = "";
            if (ext is ".ztel" or ".telemetry" or ".scada")
            {
                reason = "File extension indicates dedicated telemetry data.";
                return true;
            }

            // Stride: 16 bytes per sample (8-byte timestamp + 8-byte float64)
            int sampleCount = data.Length / 16;
            if (sampleCount < 4) return false;

            int validSequences = 0;
            long prevTime = 0;

            for (int i = 0; i < sampleCount; i++)
            {
                int offset = i * 16;
                long timestamp = BinaryPrimitives.ReadInt64LittleEndian(data.Slice(offset, 8));
                ulong valBits = BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(offset + 8, 8));
                double val = BitConverter.UInt64BitsToDouble(valBits);

                // Verify timestamp plausibility (epoch between year 2000 and 2100: 946684800000ms to 4102444800000ms)
                bool plausibleEpoch = (timestamp > 946_684_800_000L && timestamp < 4_102_444_800_000L) ||
                                      (i > 0 && timestamp > prevTime && (timestamp - prevTime) < 86_400_000L);

                // Verify finite normal float value
                bool validFloat = !double.IsNaN(val) && !double.IsInfinity(val);

                if (i > 0)
                {
                    long delta = timestamp - prevTime;
                    if (delta > 0 && delta <= 600_000L && validFloat) // delta between 1ms and 10 minutes
                    {
                        validSequences++;
                    }
                }

                prevTime = timestamp;
            }

            double consistencyRatio = (double)validSequences / (sampleCount - 1);
            if (consistencyRatio >= 0.70)
            {
                reason = $"Detected telemetry / time-series pattern ({consistencyRatio:P0} timestamp regularity). Activating ZeroTelemetry (Gorilla DoD + XOR) for high compression.";
                return true;
            }

            return false;
        }

        private static bool IsKnownPrecompressedMagic(ReadOnlySpan<byte> data, string ext, out string reason)
        {
            reason = "";

            // Extension fast check
            if (ext is ".zip" or ".7z" or ".rar" or ".gz" or ".tgz" or ".bz2" or ".xz" or ".zst" or ".szip"
                    or ".mp4" or ".mkv" or ".avi" or ".mov" or ".mp3" or ".aac" or ".flac"
                    or ".jpg" or ".jpeg" or ".png" or ".webp" or ".gif"
                    or ".pdf" or ".docx" or ".xlsx" or ".pptx")
            {
                reason = $"File format '{ext}' is already compressed. Using Store mode to preserve CPU resources.";
                return true;
            }

            if (data.Length >= 4)
            {
                // ZIP: PK\x03\x04 or PK\x05\x06
                if (data[0] == 0x50 && data[1] == 0x4B && (data[2] == 0x03 || data[2] == 0x05))
                {
                    reason = "Detected pre-compressed ZIP container signature.";
                    return true;
                }
                // 7-Zip: 7z\xBC\xAF\x27\x1C
                if (data.Length >= 6 && data[0] == 0x37 && data[1] == 0x7A && data[2] == 0xBC && data[3] == 0xAF && data[4] == 0x27 && data[5] == 0x1C)
                {
                    reason = "Detected 7-Zip archive signature.";
                    return true;
                }
                // GZip: \x1F\x8B
                if (data[0] == 0x1F && data[1] == 0x8B)
                {
                    reason = "Detected GZip stream signature.";
                    return true;
                }
                // Zstandard frame: 0x28, 0xB5, 0x2F, 0xFD
                if (data[0] == 0x28 && data[1] == 0xB5 && data[2] == 0x2F && data[3] == 0xFD)
                {
                    reason = "Detected Zstandard frame signature.";
                    return true;
                }
                // JPEG: \xFF\xD8\xFF
                if (data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF)
                {
                    reason = "Detected pre-compressed JPEG image signature.";
                    return true;
                }
                // PNG: \x89PNG
                if (data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47)
                {
                    reason = "Detected pre-compressed PNG image signature.";
                    return true;
                }
            }

            return false;
        }

        private static bool IsExecutableMagic(ReadOnlySpan<byte> data, string ext, out string reason)
        {
            reason = "";
            if (ext is ".exe" or ".dll" or ".sys" or ".so" or ".dylib" or ".ocx")
            {
                reason = $"File extension '{ext}' indicates an executable binary. Applying LZMA for machine code.";
                return true;
            }

            if (data.Length >= 2 && data[0] == 0x4D && data[1] == 0x5A) // 'MZ'
            {
                reason = "Detected Portable Executable (Windows MZ) header. Applying LZMA for machine code optimization.";
                return true;
            }

            if (data.Length >= 4 && data[0] == 0x7F && data[1] == 0x45 && data[2] == 0x4C && data[3] == 0x46) // '\x7FELF'
            {
                reason = "Detected Linux ELF executable header. Applying LZMA.";
                return true;
            }

            return false;
        }

        private static string ExtractExtension(string? fileNameOrPath)
        {
            if (string.IsNullOrWhiteSpace(fileNameOrPath)) return "";
            try
            {
                return Path.GetExtension(fileNameOrPath).ToLowerInvariant();
            }
            catch
            {
                return "";
            }
        }

        #endregion
    }
}
