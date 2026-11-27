using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using ZeroPrimitives.Buffers;

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

        private enum HashAlgorithmKind : byte
        {
            Md5 = 0,
            Sha1 = 1,
            Sha256 = 2
        }

        /// <summary>
        /// Computes MD5 hash and formats as a 32-character hexadecimal string.
        /// Zero heap allocation (excluding returned string) via stack or pooled UTF-8 encoding.
        /// </summary>
        public static string Md5Hex(string input)
            => ComputeHashHex(input, 16, HashAlgorithmKind.Md5);

        /// <summary>
        /// Computes SHA256 hash and formats as a 64-character hexadecimal string.
        /// Zero heap allocation (excluding returned string) via stack or pooled UTF-8 encoding.
        /// </summary>
        public static string Sha256Hex(string input)
            => ComputeHashHex(input, 32, HashAlgorithmKind.Sha256);

        /// <summary>
        /// Computes SHA1 hash and formats as a 40-character hexadecimal string.
        /// Zero heap allocation (excluding returned string) via stack or pooled UTF-8 encoding.
        /// </summary>
        public static string Sha1Hex(string input)
            => ComputeHashHex(input, 20, HashAlgorithmKind.Sha1);

        private static string ComputeHashHex(string input, int hashLength, HashAlgorithmKind kind)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;

            int maxBytes = Encoding.UTF8.GetMaxByteCount(input.Length);
#if NET8_0_OR_GREATER
            Span<byte> hash = stackalloc byte[hashLength];
            if (maxBytes <= 512)
            {
                Span<byte> utf8 = stackalloc byte[maxBytes];
                int written = Encoding.UTF8.GetBytes(input.AsSpan(), utf8);
                ComputeHashNet8(utf8.Slice(0, written), hash, kind);
                return ToHex(hash);
            }

            byte[] rented = ArrayPool<byte>.Shared.Rent(maxBytes);
            try
            {
                int written = Encoding.UTF8.GetBytes(input, 0, input.Length, rented, 0);
                ComputeHashNet8(rented.AsSpan(0, written), hash, kind);
                return ToHex(hash);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
#else
            byte[] rented = ArrayPool<byte>.Shared.Rent(maxBytes);
            try
            {
                int written = Encoding.UTF8.GetBytes(input, 0, input.Length, rented, 0);
                using HashAlgorithm alg = kind switch
                {
                    HashAlgorithmKind.Md5 => MD5.Create(),
                    HashAlgorithmKind.Sha1 => SHA1.Create(),
                    _ => SHA256.Create()
                };
                byte[] hashBytes = alg.ComputeHash(rented, 0, written);
                return ToHex(hashBytes);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
#endif
        }

#if NET8_0_OR_GREATER
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ComputeHashNet8(ReadOnlySpan<byte> source, Span<byte> destination, HashAlgorithmKind kind)
        {
            switch (kind)
            {
                case HashAlgorithmKind.Md5: MD5.HashData(source, destination); break;
                case HashAlgorithmKind.Sha1: SHA1.HashData(source, destination); break;
                default: SHA256.HashData(source, destination); break;
            }
        }
#endif

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe string ToHex(ReadOnlySpan<byte> hash)
        {
            Span<char> hexChars = stackalloc char[hash.Length * 2];
            FastHex.Encode(hash, hexChars, lowercase: false);
#if NET8_0_OR_GREATER
            return new string(hexChars);
#else
            fixed (char* p = hexChars)
                return new string(p, 0, hexChars.Length);
#endif
        }

        #endregion
    }
}
