using System;
using System.Security.Cryptography;
using System.Text;
using ZeroPrimitives.Text;

namespace ZeroAsset.Cache
{
    /// <summary>
    /// Content-addressable hash generator creating isolated, deterministic cache keys
    /// for master assets and virtual variants with zero GC heap allocation.
    /// </summary>
    public static class AssetCacheKey
    {
        /// <summary>
        /// Computes a hexadecimal SHA-1 cache key for the given logical path.
        /// Zero heap allocation on modern runtimes.
        /// </summary>
        public static string ComputeKey(string? logicalPath)
        {
            if (string.IsNullOrWhiteSpace(logicalPath)) return string.Empty;

            string normalized = logicalPath!.ToLowerInvariant();

#if NET8_0_OR_GREATER
            int maxBytes = Encoding.UTF8.GetMaxByteCount(normalized.Length);
            if (maxBytes <= 512)
            {
                Span<byte> utf8 = stackalloc byte[maxBytes];
                int written = Encoding.UTF8.GetBytes(normalized.AsSpan(), utf8);
                Span<byte> hash = stackalloc byte[20];
                SHA1.HashData(utf8.Slice(0, written), hash);

                Span<char> hexChars = stackalloc char[40];
                SpanTextOps.BytesToHex(hash, hexChars, lowerCase: true);
                return hexChars.ToString();
            }
#endif
            byte[] bytes = Encoding.UTF8.GetBytes(normalized);
            using var sha = SHA1.Create();
            byte[] hashBytes = sha.ComputeHash(bytes);
            Span<char> hex = stackalloc char[hashBytes.Length * 2];
            SpanTextOps.BytesToHex(hashBytes, hex, lowerCase: true);
            return hex.ToString();
        }

        /// <summary>
        /// Computes a compound cache key incorporating logical path, last modified timestamp, and length.
        /// </summary>
        public static string ComputeCompoundKey(string? logicalPath, long lastModifiedTicks, long fileLength)
        {
            string baseKey = ComputeKey(logicalPath);
            return $"{baseKey}_{lastModifiedTicks}_{fileLength}";
        }
    }
}
