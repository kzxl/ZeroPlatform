using System;
using System.Security.Cryptography;
using System.Text;
using ZeroPrimitives.Text;

namespace ZeroPrimitives.Cryptography
{
    /// <summary>
    /// High-performance cryptographic and non-cryptographic hashing utilities.
    /// </summary>
    public static class FastHash
    {
        #region Non-Cryptographic (FNV-1a)

        private const uint Fnv1a32Offset = 2166136261;
        private const uint Fnv1a32Prime = 16777619;
        private const ulong Fnv1a64Offset = 14695981039346656037;
        private const ulong Fnv1a64Prime = 1099511628211;

        /// <summary>
        /// Computes 32-bit FNV-1a hash over bytes (ultra-fast for hash tables and lookups).
        /// </summary>
        public static uint Fnv1a32(ReadOnlySpan<byte> data)
        {
            uint hash = Fnv1a32Offset;
            for (int i = 0; i < data.Length; i++)
            {
                hash ^= data[i];
                hash *= Fnv1a32Prime;
            }
            return hash;
        }

        /// <summary>
        /// Computes 64-bit FNV-1a hash over bytes.
        /// </summary>
        public static ulong Fnv1a64(ReadOnlySpan<byte> data)
        {
            ulong hash = Fnv1a64Offset;
            for (int i = 0; i < data.Length; i++)
            {
                hash ^= data[i];
                hash *= Fnv1a64Prime;
            }
            return hash;
        }

        /// <summary>
        /// Computes 64-bit FNV-1a hash over characters.
        /// </summary>
        public static ulong Fnv1a64(ReadOnlySpan<char> chars)
        {
            ulong hash = Fnv1a64Offset;
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                hash ^= (byte)(c & 0xFF);
                hash *= Fnv1a64Prime;
                hash ^= (byte)(c >> 8);
                hash *= Fnv1a64Prime;
            }
            return hash;
        }

        #endregion

        #region Cryptographic (MD5, SHA1, SHA256)

        /// <summary>
        /// Computes MD5 hash and formats as a 32-character hexadecimal string.
        /// </summary>
        public static string Md5Hex(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            byte[] bytes = Encoding.UTF8.GetBytes(input);
            using var md5 = MD5.Create();
            byte[] hash = md5.ComputeHash(bytes);
            return ToHex(hash);
        }

        /// <summary>
        /// Computes SHA256 hash and formats as a 64-character hexadecimal string.
        /// </summary>
        public static string Sha256Hex(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            byte[] bytes = Encoding.UTF8.GetBytes(input);
            using var sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(bytes);
            return ToHex(hash);
        }

        /// <summary>
        /// Computes SHA1 hash and formats as a 40-character hexadecimal string.
        /// </summary>
        public static string Sha1Hex(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            byte[] bytes = Encoding.UTF8.GetBytes(input);
            using var sha = SHA1.Create();
            byte[] hash = sha.ComputeHash(bytes);
            return ToHex(hash);
        }

        private static string ToHex(byte[] hash)
        {
            Span<char> hexChars = stackalloc char[hash.Length * 2];
            SpanTextOps.BytesToHex(hash.AsSpan(), hexChars, lowerCase: false);
            return hexChars.ToString();
        }

        #endregion
    }
}
