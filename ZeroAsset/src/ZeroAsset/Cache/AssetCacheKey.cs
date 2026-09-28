using System;
using System.Security.Cryptography;
using System.Text;

namespace ZeroAsset.Cache
{
    /// <summary>
    /// Content-addressable hash generator creating isolated, deterministic cache keys
    /// for master assets and virtual variants.
    /// </summary>
    public static class AssetCacheKey
    {
        /// <summary>
        /// Computes a hexadecimal SHA-1 cache key for the given logical path.
        /// </summary>
        public static string ComputeKey(string? logicalPath)
        {
            if (string.IsNullOrWhiteSpace(logicalPath)) return string.Empty;

            using var sha = SHA1.Create();
            byte[] bytes = Encoding.UTF8.GetBytes(logicalPath!.ToLowerInvariant());
            byte[] hash = sha.ComputeHash(bytes);

            var sb = new StringBuilder(hash.Length * 2);
            foreach (byte b in hash)
            {
                sb.Append(b.ToString("x2"));
            }
            return sb.ToString();
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
