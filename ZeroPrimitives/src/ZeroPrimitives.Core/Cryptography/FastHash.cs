using System;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using ZeroPrimitives.Text;

namespace ZeroPrimitives.Cryptography
{
    /// <summary>
    /// High-performance cryptographic and non-cryptographic hashing utilities.
    /// Incorporates loop unrolling and zero-allocation span hashing.
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
        /// Features 4-way loop unrolling for maximum instruction throughput.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe uint Fnv1a32(ReadOnlySpan<byte> data)
        {
            uint hash = Fnv1a32Offset;
            fixed (byte* p = data)
            {
                byte* ptr = p;
                byte* end = p + data.Length;

                while (ptr + 4 <= end)
                {
                    hash = (hash ^ ptr[0]) * Fnv1a32Prime;
                    hash = (hash ^ ptr[1]) * Fnv1a32Prime;
                    hash = (hash ^ ptr[2]) * Fnv1a32Prime;
                    hash = (hash ^ ptr[3]) * Fnv1a32Prime;
                    ptr += 4;
                }

                while (ptr < end)
                {
                    hash = (hash ^ *ptr++) * Fnv1a32Prime;
                }
            }
            return hash;
        }

        /// <summary>
        /// Computes 64-bit FNV-1a hash over bytes with 4-way loop unrolling.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe ulong Fnv1a64(ReadOnlySpan<byte> data)
        {
            ulong hash = Fnv1a64Offset;
            fixed (byte* p = data)
            {
                byte* ptr = p;
                byte* end = p + data.Length;

                while (ptr + 4 <= end)
                {
                    hash = (hash ^ ptr[0]) * Fnv1a64Prime;
                    hash = (hash ^ ptr[1]) * Fnv1a64Prime;
                    hash = (hash ^ ptr[2]) * Fnv1a64Prime;
                    hash = (hash ^ ptr[3]) * Fnv1a64Prime;
                    ptr += 4;
                }

                while (ptr < end)
                {
                    hash = (hash ^ *ptr++) * Fnv1a64Prime;
                }
            }
            return hash;
        }

        /// <summary>
        /// Computes 64-bit FNV-1a hash over characters.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe ulong Fnv1a64(ReadOnlySpan<char> chars)
        {
            ulong hash = Fnv1a64Offset;
            fixed (char* p = chars)
            {
                char* ptr = p;
                char* end = p + chars.Length;

                while (ptr < end)
                {
                    char c = *ptr++;
                    hash = (hash ^ (byte)(c & 0xFF)) * Fnv1a64Prime;
                    hash = (hash ^ (byte)(c >> 8)) * Fnv1a64Prime;
                }
            }
            return hash;
        }

        #endregion

        #region Cryptographic (MD5, SHA1, SHA256)

        /// <summary>
        /// Computes MD5 hash and formats as a 32-character hexadecimal string.
        /// Zero heap allocation on .NET 8+ for inputs up to 512 bytes.
        /// </summary>
        public static string Md5Hex(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;

#if NET8_0_OR_GREATER
            int maxBytes = Encoding.UTF8.GetMaxByteCount(input.Length);
            if (maxBytes <= 512)
            {
                Span<byte> utf8 = stackalloc byte[maxBytes];
                int written = Encoding.UTF8.GetBytes(input.AsSpan(), utf8);
                Span<byte> hash = stackalloc byte[16];
                MD5.HashData(utf8.Slice(0, written), hash);
                return ToHex(hash);
            }
#endif

            byte[] bytes = Encoding.UTF8.GetBytes(input);
            using var md5 = MD5.Create();
            byte[] hashBytes = md5.ComputeHash(bytes);
            return ToHex(hashBytes);
        }

        /// <summary>
        /// Computes SHA256 hash and formats as a 64-character hexadecimal string.
        /// Zero heap allocation on .NET 8+ for inputs up to 512 bytes.
        /// </summary>
        public static string Sha256Hex(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;

#if NET8_0_OR_GREATER
            int maxBytes = Encoding.UTF8.GetMaxByteCount(input.Length);
            if (maxBytes <= 512)
            {
                Span<byte> utf8 = stackalloc byte[maxBytes];
                int written = Encoding.UTF8.GetBytes(input.AsSpan(), utf8);
                Span<byte> hash = stackalloc byte[32];
                SHA256.HashData(utf8.Slice(0, written), hash);
                return ToHex(hash);
            }
#endif

            byte[] bytes = Encoding.UTF8.GetBytes(input);
            using var sha = SHA256.Create();
            byte[] hashBytes = sha.ComputeHash(bytes);
            return ToHex(hashBytes);
        }

        /// <summary>
        /// Computes SHA1 hash and formats as a 40-character hexadecimal string.
        /// Zero heap allocation on .NET 8+ for inputs up to 512 bytes.
        /// </summary>
        public static string Sha1Hex(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;

#if NET8_0_OR_GREATER
            int maxBytes = Encoding.UTF8.GetMaxByteCount(input.Length);
            if (maxBytes <= 512)
            {
                Span<byte> utf8 = stackalloc byte[maxBytes];
                int written = Encoding.UTF8.GetBytes(input.AsSpan(), utf8);
                Span<byte> hash = stackalloc byte[20];
                SHA1.HashData(utf8.Slice(0, written), hash);
                return ToHex(hash);
            }
#endif

            byte[] bytes = Encoding.UTF8.GetBytes(input);
            using var sha = SHA1.Create();
            byte[] hashBytes = sha.ComputeHash(bytes);
            return ToHex(hashBytes);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string ToHex(ReadOnlySpan<byte> hash)
        {
            Span<char> hexChars = stackalloc char[hash.Length * 2];
            SpanTextOps.BytesToHex(hash, hexChars, lowerCase: false);
            return hexChars.ToString();
        }

        #endregion
    }
}
