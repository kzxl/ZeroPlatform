using System;

namespace ZeroCompression.Core
{
    /// <summary>
    /// Tunable compression options for all ZeroUniverse compression codecs.
    /// </summary>
    public class CompressionOptions
    {
        public const int MaxSafeWindowLog = 27;
        public const int MaxLongWindowLog = 31;
        public const int DefaultBufferSize = 1 << 20; // 1 MiB

        /// <summary>Codec applied to the payload.</summary>
        public CompressionMethod Method { get; set; } = CompressionMethod.Zstd;

        /// <summary>Compression level (1..22 for Zstd, 1..9 for Deflate/GZip).</summary>
        public int Level { get; set; } = 19;

        /// <summary>Worker thread count. 0 = single-threaded. -1 = auto (Environment.ProcessorCount).</summary>
        public int Workers { get; set; } = -1;

        /// <summary>Enable long-distance matching (great for large/redundant data in Zstd).</summary>
        public bool LongDistanceMatching { get; set; }

        /// <summary>History window log (2^windowLog bytes). 0 = default.</summary>
        public int WindowLog { get; set; }

        /// <summary>Append an internal content checksum to the codec frame.</summary>
        public bool ContentChecksum { get; set; } = true;

        /// <summary>Optional password for authenticated AES-256-GCM encryption.</summary>
        public string? Password { get; set; }

        /// <summary>Buffer size used for stream copying and pooling.</summary>
        public int BufferSize { get; set; } = DefaultBufferSize;

        /// <summary>When true, enables precomp deep repack mode for pre-compressed streams.</summary>
        public bool UsePrecomp { get; set; }

        /// <summary>Explicit path to precomp.exe; null = auto-detect.</summary>
        public string? PrecompPath { get; set; }

        /// <summary>Extra arguments passed to precomp tool.</summary>
        public string? PrecompExtraArgs { get; set; }

        public bool IsEncrypted => !string.IsNullOrEmpty(Password);

        public int ResolvedWorkers => Workers < 0 ? Environment.ProcessorCount : Workers;
        public int ResolvedWindowLog => WindowLog <= 0 ? 0 : Math.Min(WindowLog, MaxLongWindowLog);

        public static CompressionOptions FromProfile(CompressionProfile profile, CompressionMethod method = CompressionMethod.Zstd)
        {
            var opt = profile switch
            {
                CompressionProfile.Fast => new CompressionOptions
                {
                    Level = 3,
                    Workers = 0,
                    LongDistanceMatching = false,
                    WindowLog = 0
                },
                CompressionProfile.Normal => new CompressionOptions
                {
                    Level = 12,
                    Workers = -1,
                    LongDistanceMatching = false,
                    WindowLog = 0
                },
                CompressionProfile.Ultra => new CompressionOptions
                {
                    Level = 22,
                    Workers = -1,
                    LongDistanceMatching = true,
                    WindowLog = MaxSafeWindowLog
                },
                _ => new CompressionOptions()
            };
            opt.Method = method;
            return opt;
        }

        /// <summary>
        /// Probes a disk path or file, automatically detects the data type, and configures the optimal compression options.
        /// </summary>
        public static CompressionOptions AutoDetect(string path, CompressionProfile profile = CompressionProfile.Ultra)
        {
            var result = ZeroCompression.Core.Analysis.DataClassifier.ClassifyPath(path);
            return result.CreateOptions(profile);
        }

        /// <summary>
        /// Probes a byte sample and configures the optimal compression options.
        /// </summary>
        public static CompressionOptions AutoDetect(ReadOnlySpan<byte> sample, string? fileNameOrExtension = null, CompressionProfile profile = CompressionProfile.Ultra)
        {
            var result = ZeroCompression.Core.Analysis.DataClassifier.Classify(sample, fileNameOrExtension);
            return result.CreateOptions(profile);
        }
    }
}
