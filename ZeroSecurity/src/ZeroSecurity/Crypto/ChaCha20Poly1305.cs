using System;
using System.Runtime.InteropServices;
using ZeroSecurity.Common;

namespace ZeroSecurity.Crypto;

/// <summary>
/// Pure C# RFC 8439 Authenticated Encryption with Associated Data (AEAD) using ChaCha20 and Poly1305.
/// Designed for zero-allocation quarantine file encryption and confidential ZeroWire frame payloads.
/// </summary>
public static class ChaCha20Poly1305
{
    public const int KeySize = 32;
    public const int NonceSize = 12;
    public const int TagSize = 16;

    private static readonly uint[] Sigma = { 0x61707865, 0x3320646e, 0x79622d32, 0x6b206574 }; // "expand 32-byte k"

    /// <summary>
    /// Encrypts plaintext using ChaCha20-Poly1305 AEAD.
    /// </summary>
    public static void Encrypt(
        ReadOnlySpan<byte> key,
        ReadOnlySpan<byte> nonce,
        ReadOnlySpan<byte> associatedData,
        ReadOnlySpan<byte> plaintext,
        Span<byte> ciphertext,
        Span<byte> tag)
    {
        if (key.Length != KeySize) throw new ArgumentException($"Key must be {KeySize} bytes.", nameof(key));
        if (nonce.Length != NonceSize) throw new ArgumentException($"Nonce must be {NonceSize} bytes.", nameof(nonce));
        if (ciphertext.Length < plaintext.Length) throw new ArgumentException("Ciphertext span too small.", nameof(ciphertext));
        if (tag.Length < TagSize) throw new ArgumentException($"Tag span must be at least {TagSize} bytes.", nameof(tag));

        Span<byte> polyKeyBlock = stackalloc byte[64];
        try
        {
            // 1. Generate Poly1305 key using ChaCha20 block 0
            ChaCha20Block(key, nonce, 0, polyKeyBlock);
            ReadOnlySpan<byte> polyKey = polyKeyBlock.Slice(0, 32);

            // 2. Encrypt plaintext starting with ChaCha20 block 1
            ChaCha20Process(key, nonce, 1, plaintext, ciphertext.Slice(0, plaintext.Length));

            // 3. Compute Poly1305 MAC over (AAD || pad || Ciphertext || pad || len(AAD) || len(Ciphertext))
            ComputeTag(polyKey, associatedData, ciphertext.Slice(0, plaintext.Length), tag.Slice(0, TagSize));
        }
        finally
        {
            CryptoMemory.SecureZero(polyKeyBlock);
        }
    }

    /// <summary>
    /// Decrypts ciphertext and verifies Poly1305 authentication tag.
    /// Returns true if tag is valid and plaintext was written; false if authentication failed.
    /// </summary>
    public static bool Decrypt(
        ReadOnlySpan<byte> key,
        ReadOnlySpan<byte> nonce,
        ReadOnlySpan<byte> associatedData,
        ReadOnlySpan<byte> ciphertext,
        ReadOnlySpan<byte> tag,
        Span<byte> plaintext)
    {
        if (key.Length != KeySize) throw new ArgumentException($"Key must be {KeySize} bytes.", nameof(key));
        if (nonce.Length != NonceSize) throw new ArgumentException($"Nonce must be {NonceSize} bytes.", nameof(nonce));
        if (tag.Length < TagSize) throw new ArgumentException($"Tag must be at least {TagSize} bytes.", nameof(tag));
        if (plaintext.Length < ciphertext.Length) throw new ArgumentException("Plaintext span too small.", nameof(plaintext));

        Span<byte> polyKeyBlock = stackalloc byte[64];
        Span<byte> expectedTag = stackalloc byte[TagSize];
        try
        {
            // 1. Generate Poly1305 key
            ChaCha20Block(key, nonce, 0, polyKeyBlock);
            ReadOnlySpan<byte> polyKey = polyKeyBlock.Slice(0, 32);

            // 2. Verify MAC
            ComputeTag(polyKey, associatedData, ciphertext, expectedTag);

            if (!CryptoMemory.ConstantTimeEquals(tag.Slice(0, TagSize), expectedTag))
            {
                CryptoMemory.SecureZero(plaintext.Slice(0, ciphertext.Length));
                return false;
            }

            // 3. Decrypt
            ChaCha20Process(key, nonce, 1, ciphertext, plaintext.Slice(0, ciphertext.Length));
            return true;
        }
        finally
        {
            CryptoMemory.SecureZero(polyKeyBlock);
            CryptoMemory.SecureZero(expectedTag);
        }
    }

    internal static void HChaCha20(ReadOnlySpan<byte> key, ReadOnlySpan<byte> nonce, Span<byte> subkey)
    {
        Span<uint> state = stackalloc uint[16];

        try
        {
            state[0] = Sigma[0];
            state[1] = Sigma[1];
            state[2] = Sigma[2];
            state[3] = Sigma[3];

            for (int i = 0; i < 8; i++)
            {
                state[4 + i] = ReadUInt32LittleEndian(key.Slice(i * 4, 4));
            }

            for (int i = 0; i < 4; i++)
            {
                state[12 + i] = ReadUInt32LittleEndian(nonce.Slice(i * 4, 4));
            }

            for (int i = 0; i < 10; i++)
            {
                QR(ref state[0], ref state[4], ref state[8], ref state[12]);
                QR(ref state[1], ref state[5], ref state[9], ref state[13]);
                QR(ref state[2], ref state[6], ref state[10], ref state[14]);
                QR(ref state[3], ref state[7], ref state[11], ref state[15]);

                QR(ref state[0], ref state[5], ref state[10], ref state[15]);
                QR(ref state[1], ref state[6], ref state[11], ref state[12]);
                QR(ref state[2], ref state[7], ref state[8], ref state[13]);
                QR(ref state[3], ref state[4], ref state[9], ref state[14]);
            }

            WriteUInt32LittleEndian(state[0], subkey.Slice(0, 4));
            WriteUInt32LittleEndian(state[1], subkey.Slice(4, 4));
            WriteUInt32LittleEndian(state[2], subkey.Slice(8, 4));
            WriteUInt32LittleEndian(state[3], subkey.Slice(12, 4));
            WriteUInt32LittleEndian(state[12], subkey.Slice(16, 4));
            WriteUInt32LittleEndian(state[13], subkey.Slice(20, 4));
            WriteUInt32LittleEndian(state[14], subkey.Slice(24, 4));
            WriteUInt32LittleEndian(state[15], subkey.Slice(28, 4));
        }
        finally
        {
            CryptoMemory.SecureZero(MemoryMarshal.AsBytes(state));
        }
    }

    private static void ChaCha20Process(ReadOnlySpan<byte> key, ReadOnlySpan<byte> nonce, uint initialCounter, ReadOnlySpan<byte> input, Span<byte> output)
    {
        Span<byte> keyStream = stackalloc byte[64];
        uint counter = initialCounter;
        int offset = 0;

        try
        {
            while (offset < input.Length)
            {
                ChaCha20Block(key, nonce, counter++, keyStream);
                int take = Math.Min(64, input.Length - offset);

                for (int i = 0; i < take; i++)
                {
                    output[offset + i] = (byte)(input[offset + i] ^ keyStream[i]);
                }

                offset += take;
            }
        }
        finally
        {
            CryptoMemory.SecureZero(keyStream);
        }
    }

    private static void ChaCha20Block(ReadOnlySpan<byte> key, ReadOnlySpan<byte> nonce, uint counter, Span<byte> output)
    {
        Span<uint> state = stackalloc uint[16];
        Span<uint> working = stackalloc uint[16];

        try
        {
            state[0] = Sigma[0];
            state[1] = Sigma[1];
            state[2] = Sigma[2];
            state[3] = Sigma[3];

            for (int i = 0; i < 8; i++)
            {
                state[4 + i] = ReadUInt32LittleEndian(key.Slice(i * 4, 4));
            }

            state[12] = counter;

            for (int i = 0; i < 3; i++)
            {
                state[13 + i] = ReadUInt32LittleEndian(nonce.Slice(i * 4, 4));
            }

            state.CopyTo(working);

            for (int i = 0; i < 10; i++)
            {
                QR(ref working[0], ref working[4], ref working[8], ref working[12]);
                QR(ref working[1], ref working[5], ref working[9], ref working[13]);
                QR(ref working[2], ref working[6], ref working[10], ref working[14]);
                QR(ref working[3], ref working[7], ref working[11], ref working[15]);

                QR(ref working[0], ref working[5], ref working[10], ref working[15]);
                QR(ref working[1], ref working[6], ref working[11], ref working[12]);
                QR(ref working[2], ref working[7], ref working[8], ref working[13]);
                QR(ref working[3], ref working[4], ref working[9], ref working[14]);
            }

            for (int i = 0; i < 16; i++)
            {
                WriteUInt32LittleEndian(working[i] + state[i], output.Slice(i * 4, 4));
            }
        }
        finally
        {
            CryptoMemory.SecureZero(MemoryMarshal.AsBytes(state));
            CryptoMemory.SecureZero(MemoryMarshal.AsBytes(working));
        }
    }

    private static void QR(ref uint a, ref uint b, ref uint c, ref uint d)
    {
        a += b; d = RotateLeft(d ^ a, 16);
        c += d; b = RotateLeft(b ^ c, 12);
        a += b; d = RotateLeft(d ^ a, 8);
        c += d; b = RotateLeft(b ^ c, 7);
    }

    private static uint RotateLeft(uint v, int c) => (v << c) | (v >> (32 - c));

    private static void ComputeTag(ReadOnlySpan<byte> polyKey, ReadOnlySpan<byte> aad, ReadOnlySpan<byte> ciphertext, Span<byte> tag)
    {
        // 1. Clamp r
        Span<byte> c = stackalloc byte[16];
        Span<byte> padBlock = stackalloc byte[16];
        try
        {
            polyKey.Slice(0, 16).CopyTo(c);
            c[3] &= 15;
            c[7] &= 15;
            c[11] &= 15;
            c[15] &= 15;
            c[4] &= 252;
            c[8] &= 252;
            c[12] &= 252;

            uint r0 = ((uint)c[0] | ((uint)c[1] << 8) | ((uint)c[2] << 16) | ((uint)c[3] << 24)) & 0x03ffffff;
            uint r1 = (((uint)c[3] >> 2) | ((uint)c[4] << 6) | ((uint)c[5] << 14) | ((uint)c[6] << 22)) & 0x03ffffff;
            uint r2 = (((uint)c[6] >> 4) | ((uint)c[7] << 4) | ((uint)c[8] << 12) | ((uint)c[9] << 20)) & 0x03ffffff;
            uint r3 = (((uint)c[9] >> 6) | ((uint)c[10] << 2) | ((uint)c[11] << 10) | ((uint)c[12] << 18)) & 0x03ffffff;
            uint r4 = (((uint)c[13]) | ((uint)c[14] << 8) | ((uint)c[15] << 16)) & 0x03ffffff;

            uint s1 = r1 * 5;
            uint s2 = r2 * 5;
            uint s3 = r3 * 5;
            uint s4 = r4 * 5;

            // s = polyKey[16..31]
            uint s_0 = ReadUInt32LittleEndian(polyKey.Slice(16, 4));
            uint s_1 = ReadUInt32LittleEndian(polyKey.Slice(20, 4));
            uint s_2 = ReadUInt32LittleEndian(polyKey.Slice(24, 4));
            uint s_3 = ReadUInt32LittleEndian(polyKey.Slice(28, 4));

            uint h0 = 0, h1 = 0, h2 = 0, h3 = 0, h4 = 0;

            // Process AAD (zero-padded to 16 bytes)
            Poly1305ProcessChunks(aad, padBlock, ref h0, ref h1, ref h2, ref h3, ref h4, r0, r1, r2, r3, r4, s1, s2, s3, s4);

            // Process Ciphertext (zero-padded to 16 bytes)
            Poly1305ProcessChunks(ciphertext, padBlock, ref h0, ref h1, ref h2, ref h3, ref h4, r0, r1, r2, r3, r4, s1, s2, s3, s4);

            // Process Lengths (8 bytes aad.Length, 8 bytes ciphertext.Length)
            padBlock.Clear();
            WriteUInt64LittleEndian((ulong)aad.Length, padBlock.Slice(0, 8));
            WriteUInt64LittleEndian((ulong)ciphertext.Length, padBlock.Slice(8, 8));
            Poly1305Block(padBlock, ref h0, ref h1, ref h2, ref h3, ref h4, r0, r1, r2, r3, r4, s1, s2, s3, s4);

            // Full carry propagation
            ulong carry = h1 >> 26; h1 &= 0x03ffffff; h2 += (uint)carry;
            carry = h2 >> 26; h2 &= 0x03ffffff; h3 += (uint)carry;
            carry = h3 >> 26; h3 &= 0x03ffffff; h4 += (uint)carry;
            carry = h4 >> 26; h4 &= 0x03ffffff; h0 += (uint)(carry * 5);
            carry = h0 >> 26; h0 &= 0x03ffffff; h1 += (uint)carry;

            // Compute h + 5 to see if h >= 2^130 - 5
            uint g0 = h0 + 5;
            carry = g0 >> 26; g0 &= 0x03ffffff;
            uint g1 = h1 + (uint)carry;
            carry = g1 >> 26; g1 &= 0x03ffffff;
            uint g2 = h2 + (uint)carry;
            carry = g2 >> 26; g2 &= 0x03ffffff;
            uint g3 = h3 + (uint)carry;
            carry = g3 >> 26; g3 &= 0x03ffffff;
            uint g4 = h4 + (uint)carry - (1u << 26);

            // If g4 >= 0 (bit 31 is 0), h >= 2^130 - 5, so select g; otherwise select h.
            uint mask = (uint)-(int)((g4 >> 31) ^ 1);
            h0 = (h0 & ~mask) | (g0 & mask);
            h1 = (h1 & ~mask) | (g1 & mask);
            h2 = (h2 & ~mask) | (g2 & mask);
            h3 = (h3 & ~mask) | (g3 & mask);
            h4 = (h4 & ~mask) | (g4 & mask);

            // Reconstruct 128-bit integer from 26-bit limbs
            uint f0 = h0 | (h1 << 26);
            uint f1 = (h1 >> 6) | (h2 << 20);
            uint f2 = (h2 >> 12) | (h3 << 14);
            uint f3 = (h3 >> 18) | (h4 << 8);

            // Add s (modulo 2^128)
            ulong sum = (ulong)f0 + s_0;
            WriteUInt32LittleEndian((uint)sum, tag.Slice(0, 4));

            sum = (ulong)f1 + s_1 + (sum >> 32);
            WriteUInt32LittleEndian((uint)sum, tag.Slice(4, 4));

            sum = (ulong)f2 + s_2 + (sum >> 32);
            WriteUInt32LittleEndian((uint)sum, tag.Slice(8, 4));

            sum = (ulong)f3 + s_3 + (sum >> 32);
            WriteUInt32LittleEndian((uint)sum, tag.Slice(12, 4));
        }
        finally
        {
            CryptoMemory.SecureZero(c);
            CryptoMemory.SecureZero(padBlock);
        }
    }

    private static void Poly1305ProcessChunks(
        ReadOnlySpan<byte> data,
        Span<byte> padBlock,
        ref uint h0, ref uint h1, ref uint h2, ref uint h3, ref uint h4,
        uint r0, uint r1, uint r2, uint r3, uint r4,
        uint s1, uint s2, uint s3, uint s4)
    {
        int offset = 0;
        while (offset + 16 <= data.Length)
        {
            Poly1305Block(data.Slice(offset, 16), ref h0, ref h1, ref h2, ref h3, ref h4,
                r0, r1, r2, r3, r4, s1, s2, s3, s4);
            offset += 16;
        }

        int remaining = data.Length - offset;
        if (remaining > 0)
        {
            padBlock.Clear();
            data.Slice(offset, remaining).CopyTo(padBlock);
            Poly1305Block(padBlock, ref h0, ref h1, ref h2, ref h3, ref h4,
                r0, r1, r2, r3, r4, s1, s2, s3, s4);
        }
    }

    private static void Poly1305Block(
        ReadOnlySpan<byte> block,
        ref uint h0, ref uint h1, ref uint h2, ref uint h3, ref uint h4,
        uint r0, uint r1, uint r2, uint r3, uint r4,
        uint s1, uint s2, uint s3, uint s4)
    {
        uint t0 = ReadUInt32LittleEndian(block.Slice(0, 4));
        uint t1 = ReadUInt32LittleEndian(block.Slice(4, 4));
        uint t2 = ReadUInt32LittleEndian(block.Slice(8, 4));
        uint t3 = ReadUInt32LittleEndian(block.Slice(12, 4));

        uint m0 = t0 & 0x03ffffff;
        uint m1 = ((t0 >> 26) | (t1 << 6)) & 0x03ffffff;
        uint m2 = ((t1 >> 20) | (t2 << 12)) & 0x03ffffff;
        uint m3 = ((t2 >> 14) | (t3 << 18)) & 0x03ffffff;
        uint m4 = (t3 >> 8) | (1u << 24); // Append 1 bit (0x01 byte) at index 16

        h0 += m0;
        h1 += m1;
        h2 += m2;
        h3 += m3;
        h4 += m4;

        ulong d0 = (ulong)h0 * r0 + (ulong)h1 * s4 + (ulong)h2 * s3 + (ulong)h3 * s2 + (ulong)h4 * s1;
        ulong d1 = (ulong)h0 * r1 + (ulong)h1 * r0 + (ulong)h2 * s4 + (ulong)h3 * s3 + (ulong)h4 * s2;
        ulong d2 = (ulong)h0 * r2 + (ulong)h1 * r1 + (ulong)h2 * r0 + (ulong)h3 * s4 + (ulong)h4 * s3;
        ulong d3 = (ulong)h0 * r3 + (ulong)h1 * r2 + (ulong)h2 * r1 + (ulong)h3 * r0 + (ulong)h4 * s4;
        ulong d4 = (ulong)h0 * r4 + (ulong)h1 * r3 + (ulong)h2 * r2 + (ulong)h3 * r1 + (ulong)h4 * r0;

        ulong c = d0 >> 26; h0 = (uint)d0 & 0x03ffffff; d1 += c;
        c = d1 >> 26; h1 = (uint)d1 & 0x03ffffff; d2 += c;
        c = d2 >> 26; h2 = (uint)d2 & 0x03ffffff; d3 += c;
        c = d3 >> 26; h3 = (uint)d3 & 0x03ffffff; d4 += c;
        c = d4 >> 26; h4 = (uint)d4 & 0x03ffffff; h0 += (uint)(c * 5);
        c = h0 >> 26; h0 &= 0x03ffffff; h1 += (uint)c;
    }

    private static uint ReadUInt32LittleEndian(ReadOnlySpan<byte> s) =>
        (uint)s[0] | ((uint)s[1] << 8) | ((uint)s[2] << 16) | ((uint)s[3] << 24);

    private static void WriteUInt32LittleEndian(uint val, Span<byte> dest)
    {
        dest[0] = (byte)val;
        dest[1] = (byte)(val >> 8);
        dest[2] = (byte)(val >> 16);
        dest[3] = (byte)(val >> 24);
    }

    private static void WriteUInt64LittleEndian(ulong val, Span<byte> dest)
    {
        dest[0] = (byte)val;
        dest[1] = (byte)(val >> 8);
        dest[2] = (byte)(val >> 16);
        dest[3] = (byte)(val >> 24);
        dest[4] = (byte)(val >> 32);
        dest[5] = (byte)(val >> 40);
        dest[6] = (byte)(val >> 48);
        dest[7] = (byte)(val >> 56);
    }
}
