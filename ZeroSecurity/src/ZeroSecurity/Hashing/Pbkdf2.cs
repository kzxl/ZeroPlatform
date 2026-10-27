using System;
using System.Text;
using ZeroSecurity.Common;

namespace ZeroSecurity.Hashing;

/// <summary>
/// Pure C#, zero-allocation implementation of RFC 2898 (PKCS #5 v2.0 / NIST SP 800-132)
/// Password-Based Key Derivation Function 2 (PBKDF2) using HMAC-SHA256.
/// Designed for operator master password hashing, key stretching, and cryptographic vault derivation.
/// </summary>
public static class Pbkdf2
{
    public const int DefaultIterations = 100_000;

    /// <summary>
    /// Derives a cryptographic key from a password byte span, salt, and iteration count.
    /// </summary>
    public static void DeriveKey(
        ReadOnlySpan<byte> password,
        ReadOnlySpan<byte> salt,
        int iterations,
        Span<byte> destination)
    {
        if (iterations <= 0)
            throw new ArgumentOutOfRangeException(nameof(iterations), "Iterations must be positive.");

        if (destination.IsEmpty) return;

        int hashLen = HmacSha256.HashSizeInBytes; // 32 bytes
        int numBlocks = (destination.Length + hashLen - 1) / hashLen;

        Span<byte> u = stackalloc byte[hashLen];
        Span<byte> t = stackalloc byte[hashLen];
        Span<byte> blockIndex = stackalloc byte[4];
        int bytesWritten = 0;

        try
        {
            for (int i = 1; i <= numBlocks; i++)
            {
                // INT_32_BE(i)
                blockIndex[0] = (byte)(i >> 24);
                blockIndex[1] = (byte)(i >> 16);
                blockIndex[2] = (byte)(i >> 8);
                blockIndex[3] = (byte)i;

                // U_1 = PRF(Password, Salt || INT_32_BE(i))
                HmacSha256.Hash(password, salt, blockIndex, u);
                u.CopyTo(t);

                // U_2 ... U_c
                for (int j = 1; j < iterations; j++)
                {
                    HmacSha256.Hash(password, u, u);
                    for (int k = 0; k < hashLen; k++)
                    {
                        t[k] ^= u[k];
                    }
                }

                int take = Math.Min(hashLen, destination.Length - bytesWritten);
                t.Slice(0, take).CopyTo(destination.Slice(bytesWritten, take));
                bytesWritten += take;
            }
        }
        finally
        {
            CryptoMemory.SecureZero(u);
            CryptoMemory.SecureZero(t);
        }
    }

    /// <summary>
    /// Derives a cryptographic key from a UTF-8 password string.
    /// </summary>
    public static void DeriveKey(
        string password,
        ReadOnlySpan<byte> salt,
        int iterations,
        Span<byte> destination)
    {
        if (password == null) throw new ArgumentNullException(nameof(password));

        int byteCount = Encoding.UTF8.GetByteCount(password);
        if (byteCount <= 512)
        {
            Span<byte> pwBytes = stackalloc byte[byteCount];
            unsafe
            {
                fixed (char* pChars = password)
                fixed (byte* pBytes = pwBytes)
                {
                    Encoding.UTF8.GetBytes(pChars, password.Length, pBytes, byteCount);
                }
            }
            try
            {
                DeriveKey(pwBytes, salt, iterations, destination);
            }
            finally
            {
                CryptoMemory.SecureZero(pwBytes);
            }
        }
        else
        {
            byte[] pwBytes = Encoding.UTF8.GetBytes(password);
            try
            {
                DeriveKey(pwBytes, salt, iterations, destination);
            }
            finally
            {
                CryptoMemory.SecureZero(pwBytes);
            }
        }
    }
}
