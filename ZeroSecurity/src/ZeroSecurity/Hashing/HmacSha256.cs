using System;
using ZeroSecurity.Common;

namespace ZeroSecurity.Hashing;

/// <summary>
/// Pure C#, zero-allocation RFC 2104 Keyed-Hashing for Message Authentication (HMAC) using SHA-256.
/// Operates directly over ReadOnlySpan with stack-allocated states and zero GC heap pressure.
/// </summary>
public static class HmacSha256
{
    public const int HashSizeInBytes = 32;
    public const int BlockSizeInBytes = 64;

    private const byte IPAD = 0x36;
    private const byte OPAD = 0x5C;

    /// <summary>
    /// Computes the HMAC-SHA256 of the given data using the specified key.
    /// </summary>
    public static void Hash(ReadOnlySpan<byte> key, ReadOnlySpan<byte> data, Span<byte> destination)
    {
        Hash(key, data, ReadOnlySpan<byte>.Empty, ReadOnlySpan<byte>.Empty, destination);
    }

    /// <summary>
    /// Computes HMAC-SHA256 over two concatenated data spans with zero allocation.
    /// </summary>
    public static void Hash(ReadOnlySpan<byte> key, ReadOnlySpan<byte> data1, ReadOnlySpan<byte> data2, Span<byte> destination)
    {
        Hash(key, data1, data2, ReadOnlySpan<byte>.Empty, destination);
    }

    /// <summary>
    /// Computes HMAC-SHA256 over three concatenated data spans with zero allocation.
    /// </summary>
    public static void Hash(
        ReadOnlySpan<byte> key,
        ReadOnlySpan<byte> data1,
        ReadOnlySpan<byte> data2,
        ReadOnlySpan<byte> data3,
        Span<byte> destination)
    {
        if (destination.Length < HashSizeInBytes)
            throw new ArgumentException($"Destination span must be at least {HashSizeInBytes} bytes.", nameof(destination));

        Span<byte> k = stackalloc byte[BlockSizeInBytes];
        Span<byte> kIpad = stackalloc byte[BlockSizeInBytes];
        Span<byte> kOpad = stackalloc byte[BlockSizeInBytes];
        Span<byte> innerHash = stackalloc byte[HashSizeInBytes];

        try
        {
            if (key.Length > BlockSizeInBytes)
            {
                FastSha256.Hash(key, k.Slice(0, HashSizeInBytes));
                k.Slice(HashSizeInBytes).Clear();
            }
            else
            {
                key.CopyTo(k);
                k.Slice(key.Length).Clear();
            }

            for (int i = 0; i < BlockSizeInBytes; i++)
            {
                kIpad[i] = (byte)(k[i] ^ IPAD);
                kOpad[i] = (byte)(k[i] ^ OPAD);
            }

            // 1. Inner hash: H( (K ^ ipad) || data1 || data2 || data3 )
            FastSha256.Sha256Incremental inner = default;
            inner.Init();
            inner.Update(kIpad);
            if (!data1.IsEmpty) inner.Update(data1);
            if (!data2.IsEmpty) inner.Update(data2);
            if (!data3.IsEmpty) inner.Update(data3);
            inner.Final(innerHash);

            // 2. Outer hash: H( (K ^ opad) || innerHash )
            FastSha256.Sha256Incremental outer = default;
            outer.Init();
            outer.Update(kOpad);
            outer.Update(innerHash);
            outer.Final(destination);
        }
        finally
        {
            CryptoMemory.SecureZero(k);
            CryptoMemory.SecureZero(kIpad);
            CryptoMemory.SecureZero(kOpad);
            CryptoMemory.SecureZero(innerHash);
        }
    }

    /// <summary>
    /// Computes HMAC-SHA256 and returns the lowercase 64-character hex string.
    /// </summary>
    public static string HashHex(ReadOnlySpan<byte> key, ReadOnlySpan<byte> data)
    {
        Span<byte> output = stackalloc byte[HashSizeInBytes];
        Hash(key, data, output);
        return FastHex.ToHex(output);
    }
}
