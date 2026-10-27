using System;
using ZeroSecurity.Common;

namespace ZeroSecurity.Hashing;

/// <summary>
/// Pure C#, zero-allocation implementation of RFC 5869 HMAC-based Extract-and-Expand Key Derivation Function (HKDF) using SHA-256.
/// Provides cryptographically strong key derivation from shared secrets, master keys, or high-entropy data.
/// </summary>
public static class Hkdf
{
    public const int HashSizeInBytes = 32;

    /// <summary>
    /// Performs HKDF-Extract to extract a pseudorandom key (PRK) of 32 bytes from input keying material (IKM).
    /// </summary>
    public static void Extract(ReadOnlySpan<byte> salt, ReadOnlySpan<byte> ikm, Span<byte> prk)
    {
        if (prk.Length < HashSizeInBytes)
            throw new ArgumentException($"PRK span must be at least {HashSizeInBytes} bytes.", nameof(prk));

        if (salt.IsEmpty)
        {
            Span<byte> zeroSalt = stackalloc byte[HashSizeInBytes];
            zeroSalt.Clear();
            HmacSha256.Hash(zeroSalt, ikm, prk.Slice(0, HashSizeInBytes));
        }
        else
        {
            HmacSha256.Hash(salt, ikm, prk.Slice(0, HashSizeInBytes));
        }
    }

    /// <summary>
    /// Performs HKDF-Expand to expand a pseudorandom key (PRK) into output keying material (OKM).
    /// </summary>
    public static void Expand(ReadOnlySpan<byte> prk, ReadOnlySpan<byte> info, Span<byte> okm)
    {
        if (prk.Length < HashSizeInBytes)
            throw new ArgumentException($"PRK must be at least {HashSizeInBytes} bytes.", nameof(prk));

        if (okm.IsEmpty) return;

        int numBlocks = (okm.Length + HashSizeInBytes - 1) / HashSizeInBytes;
        if (numBlocks > 255)
            throw new ArgumentOutOfRangeException(nameof(okm), "Requested OKM length exceeds RFC 5869 limit (255 * HashLen).");

        Span<byte> t = stackalloc byte[HashSizeInBytes];
        Span<byte> counterSpan = stackalloc byte[1];
        int bytesWritten = 0;

        try
        {
            for (byte i = 1; i <= numBlocks; i++)
            {
                counterSpan[0] = i;

                if (i == 1)
                {
                    HmacSha256.Hash(prk, info, counterSpan, t);
                }
                else
                {
                    HmacSha256.Hash(prk, t, info, counterSpan, t);
                }

                int take = Math.Min(HashSizeInBytes, okm.Length - bytesWritten);
                t.Slice(0, take).CopyTo(okm.Slice(bytesWritten, take));
                bytesWritten += take;
            }
        }
        finally
        {
            CryptoMemory.SecureZero(t);
        }
    }

    /// <summary>
    /// Convenient one-step key derivation combining HKDF-Extract and HKDF-Expand.
    /// </summary>
    public static void DeriveKey(
        ReadOnlySpan<byte> salt,
        ReadOnlySpan<byte> ikm,
        ReadOnlySpan<byte> info,
        Span<byte> okm)
    {
        Span<byte> prk = stackalloc byte[HashSizeInBytes];
        try
        {
            Extract(salt, ikm, prk);
            Expand(prk, info, okm);
        }
        finally
        {
            CryptoMemory.SecureZero(prk);
        }
    }
}
