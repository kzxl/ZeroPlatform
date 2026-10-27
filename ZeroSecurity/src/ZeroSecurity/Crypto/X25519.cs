using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using ZeroSecurity.Common;

namespace ZeroSecurity.Crypto;

/// <summary>
/// Pure C#, zero-allocation RFC 7748 Montgomery ladder Elliptic Curve Diffie-Hellman (ECDH) over Curve25519.
/// Provides constant-time public-key derivation and sovereign peer-to-peer key agreement without external libraries.
/// </summary>
public static class X25519
{
    public const int KeySize = 32;

    private static readonly byte[] BasePoint =
    {
        9, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
    };

    /// <summary>
    /// Computes the 32-byte public key corresponding to a 32-byte private key.
    /// </summary>
    public static void GetPublicKey(ReadOnlySpan<byte> privateKey, Span<byte> publicKey)
    {
        ScalarMult(privateKey, BasePoint, publicKey);
    }

    /// <summary>
    /// Computes the 32-byte shared secret from a local private key and a peer's public key.
    /// Enforces RFC 7748 Section 6.1contributory behavior by rejecting low-order public keys that yield an all-zero shared secret.
    /// </summary>
    public static void Agree(ReadOnlySpan<byte> privateKey, ReadOnlySpan<byte> peerPublicKey, Span<byte> sharedSecret)
    {
        ScalarMult(privateKey, peerPublicKey, sharedSecret);

        Span<byte> zeros = stackalloc byte[KeySize];
        zeros.Clear();
        if (CryptoMemory.ConstantTimeEquals(sharedSecret.Slice(0, KeySize), zeros))
        {
            CryptoMemory.SecureZero(sharedSecret.Slice(0, KeySize));
            throw new CryptographicException("Peer public key produced an all-zero shared secret (low-order point).");
        }
    }

    /// <summary>
    /// RFC 7748 Section 5 X25519 scalar multiplication function: out = scalar * point.
    /// </summary>
    public static void ScalarMult(ReadOnlySpan<byte> scalar, ReadOnlySpan<byte> point, Span<byte> output)
    {
        if (scalar.Length < KeySize) throw new ArgumentException($"Scalar must be {KeySize} bytes.", nameof(scalar));
        if (point.Length < KeySize) throw new ArgumentException($"Point must be {KeySize} bytes.", nameof(point));
        if (output.Length < KeySize) throw new ArgumentException($"Output span must be at least {KeySize} bytes.", nameof(output));

        // 1. Clamp scalar
        Span<byte> k = stackalloc byte[32];
        scalar.Slice(0, 32).CopyTo(k);
        k[0] &= 248;
        k[31] &= 127;
        k[31] |= 64;

        // 2. Initialize field elements
        Span<long> x1 = stackalloc long[10];
        Span<long> x2 = stackalloc long[10];
        Span<long> z2 = stackalloc long[10];
        Span<long> x3 = stackalloc long[10];
        Span<long> z3 = stackalloc long[10];

        Span<long> a = stackalloc long[10];
        Span<long> b = stackalloc long[10];
        Span<long> c = stackalloc long[10];
        Span<long> d = stackalloc long[10];
        Span<long> e = stackalloc long[10];
        Span<long> aa = stackalloc long[10];
        Span<long> bb = stackalloc long[10];
        Span<long> da = stackalloc long[10];
        Span<long> cb = stackalloc long[10];

        try
        {
            FeFromBytes(point, x1);
            FeOne(x2);
            FeZero(z2);
            x1.CopyTo(x3);
            FeOne(z3);

            long swap = 0;

            // 3. Montgomery ladder (255 down to 0)
            for (int pos = 254; pos >= 0; pos--)
            {
                int byteIdx = pos >> 3;
                int bitIdx = pos & 7;
                long bit = (k[byteIdx] >> bitIdx) & 1;

                swap ^= bit;
                FeCSwap(swap, x2, x3);
                FeCSwap(swap, z2, z3);
                swap = bit;

                // A = x2 + z2
                FeAdd(a, x2, z2);
                // AA = A^2
                FeSquare(aa, a);
                // B = x2 - z2
                FeSub(b, x2, z2);
                // BB = B^2
                FeSquare(bb, b);
                // E = AA - BB
                FeSub(e, aa, bb);
                // C = x3 + z3
                FeAdd(c, x3, z3);
                // D = x3 - z3
                FeSub(d, x3, z3);
                // DA = D * A
                FeMul(da, d, a);
                // CB = C * B
                FeMul(cb, c, b);

                // x3 = (DA + CB)^2
                FeAdd(x3, da, cb);
                FeSquare(x3, x3);

                // z3 = x1 * (DA - CB)^2
                FeSub(z3, da, cb);
                FeSquare(z3, z3);
                FeMul(z3, x1, z3);

                // x2 = AA * BB
                FeMul(x2, aa, bb);

                // z2 = E * (AA + a24 * E)  [where a24 = 121665]
                FeMulA24(c, e);
                FeAdd(c, aa, c);
                FeMul(z2, e, c);
            }

            FeCSwap(swap, x2, x3);
            FeCSwap(swap, z2, z3);

            // 4. Output = x2 * z2^(p - 2)
            FeInvert(z2, z2);
            FeMul(x2, x2, z2);
            FeToBytes(x2, output);
        }
        finally
        {
            CryptoMemory.SecureZero(k);
            CryptoMemory.SecureZero(MemoryMarshal.AsBytes(x1));
            CryptoMemory.SecureZero(MemoryMarshal.AsBytes(x2));
            CryptoMemory.SecureZero(MemoryMarshal.AsBytes(z2));
            CryptoMemory.SecureZero(MemoryMarshal.AsBytes(x3));
            CryptoMemory.SecureZero(MemoryMarshal.AsBytes(z3));
            CryptoMemory.SecureZero(MemoryMarshal.AsBytes(a));
            CryptoMemory.SecureZero(MemoryMarshal.AsBytes(b));
            CryptoMemory.SecureZero(MemoryMarshal.AsBytes(c));
            CryptoMemory.SecureZero(MemoryMarshal.AsBytes(d));
            CryptoMemory.SecureZero(MemoryMarshal.AsBytes(e));
            CryptoMemory.SecureZero(MemoryMarshal.AsBytes(aa));
            CryptoMemory.SecureZero(MemoryMarshal.AsBytes(bb));
            CryptoMemory.SecureZero(MemoryMarshal.AsBytes(da));
            CryptoMemory.SecureZero(MemoryMarshal.AsBytes(cb));
        }
    }

    #region Field Arithmetic Modulo 2^255 - 19 (10 Limbs: Radix 2^25.5)

    private static void FeZero(Span<long> h) => h.Clear();

    private static void FeOne(Span<long> h)
    {
        h.Clear();
        h[0] = 1;
    }

    private static void FeCSwap(long b, Span<long> p, Span<long> q)
    {
        long mask = -b;
        for (int i = 0; i < 10; i++)
        {
            long t = mask & (p[i] ^ q[i]);
            p[i] ^= t;
            q[i] ^= t;
        }
    }

    private static void FeAdd(Span<long> h, ReadOnlySpan<long> f, ReadOnlySpan<long> g)
    {
        for (int i = 0; i < 10; i++) h[i] = f[i] + g[i];
    }

    private static void FeSub(Span<long> h, ReadOnlySpan<long> f, ReadOnlySpan<long> g)
    {
        for (int i = 0; i < 10; i++) h[i] = f[i] - g[i];
    }

    private static void FeMulA24(Span<long> h, ReadOnlySpan<long> f)
    {
        for (int i = 0; i < 10; i++) h[i] = f[i] * 121665L;
        FeCarry(h);
    }

    private static void FeFromBytes(ReadOnlySpan<byte> s, Span<long> h)
    {
        long Load4(ReadOnlySpan<byte> b) => (long)b[0] | ((long)b[1] << 8) | ((long)b[2] << 16) | ((long)b[3] << 24);

        h[0] = Load4(s.Slice(0, 4)) & 0x3ffffff;
        h[1] = (Load4(s.Slice(3, 4)) >> 2) & 0x1ffffff;
        h[2] = (Load4(s.Slice(6, 4)) >> 3) & 0x3ffffff;
        h[3] = (Load4(s.Slice(9, 4)) >> 5) & 0x1ffffff;
        h[4] = (Load4(s.Slice(12, 4)) >> 6) & 0x3ffffff;
        h[5] = Load4(s.Slice(16, 4)) & 0x1ffffff;
        h[6] = (Load4(s.Slice(19, 4)) >> 1) & 0x3ffffff;
        h[7] = (Load4(s.Slice(22, 4)) >> 3) & 0x1ffffff;
        h[8] = (Load4(s.Slice(25, 4)) >> 4) & 0x3ffffff;
        h[9] = (Load4(s.Slice(28, 4)) >> 6) & 0x1ffffff;
    }

    private static void FeToBytes(Span<long> h, Span<byte> s)
    {
        Span<long> t = stackalloc long[10];
        h.CopyTo(t);
        FeFreeze(t);

        s[0] = (byte)t[0];
        s[1] = (byte)(t[0] >> 8);
        s[2] = (byte)(t[0] >> 16);
        s[3] = (byte)((t[0] >> 24) | (t[1] << 2));
        s[4] = (byte)(t[1] >> 6);
        s[5] = (byte)(t[1] >> 14);
        s[6] = (byte)((t[1] >> 22) | (t[2] << 3));
        s[7] = (byte)(t[2] >> 5);
        s[8] = (byte)(t[2] >> 13);
        s[9] = (byte)((t[2] >> 21) | (t[3] << 5));
        s[10] = (byte)(t[3] >> 3);
        s[11] = (byte)(t[3] >> 11);
        s[12] = (byte)((t[3] >> 19) | (t[4] << 6));
        s[13] = (byte)(t[4] >> 2);
        s[14] = (byte)(t[4] >> 10);
        s[15] = (byte)(t[4] >> 18);
        s[16] = (byte)t[5];
        s[17] = (byte)(t[5] >> 8);
        s[18] = (byte)(t[5] >> 16);
        s[19] = (byte)((t[5] >> 24) | (t[6] << 1));
        s[20] = (byte)(t[6] >> 7);
        s[21] = (byte)(t[6] >> 15);
        s[22] = (byte)((t[6] >> 23) | (t[7] << 3));
        s[23] = (byte)(t[7] >> 5);
        s[24] = (byte)(t[7] >> 13);
        s[25] = (byte)((t[7] >> 21) | (t[8] << 4));
        s[26] = (byte)(t[8] >> 4);
        s[27] = (byte)(t[8] >> 12);
        s[28] = (byte)((t[8] >> 20) | (t[9] << 6));
        s[29] = (byte)(t[9] >> 2);
        s[30] = (byte)(t[9] >> 10);
        s[31] = (byte)(t[9] >> 18);
    }

    private static void FeCarry(Span<long> h)
    {
        for (int i = 0; i < 10; i++)
        {
            long c = h[i] >> (i % 2 == 0 ? 26 : 25);
            h[i] -= c << (i % 2 == 0 ? 26 : 25);
            if (i < 9) h[i + 1] += c;
            else h[0] += c * 19;
        }
    }

    private static void FeFreeze(Span<long> h)
    {
        FeCarry(h);
        FeCarry(h);

        // Check if h >= 2^255 - 19
        Span<long> q = stackalloc long[10];
        q[0] = h[0] + 19;
        for (int i = 0; i < 9; i++)
        {
            long c = q[i] >> (i % 2 == 0 ? 26 : 25);
            q[i] -= c << (i % 2 == 0 ? 26 : 25);
            q[i + 1] = h[i + 1] + c;
        }

        long carry = q[9] >> 25;
        q[9] -= carry << 25;

        FeCSwap(carry, h, q);
    }

    private static void FeMul(Span<long> h, ReadOnlySpan<long> f, ReadOnlySpan<long> g)
    {
        long f0 = f[0], f1 = f[1], f2 = f[2], f3 = f[3], f4 = f[4],
             f5 = f[5], f6 = f[6], f7 = f[7], f8 = f[8], f9 = f[9];
        long g0 = g[0], g1 = g[1], g2 = g[2], g3 = g[3], g4 = g[4],
             g5 = g[5], g6 = g[6], g7 = g[7], g8 = g[8], g9 = g[9];

        long g1_19 = g1 * 19, g2_19 = g2 * 19, g3_19 = g3 * 19, g4_19 = g4 * 19,
             g5_19 = g5 * 19, g6_19 = g6 * 19, g7_19 = g7 * 19, g8_19 = g8 * 19, g9_19 = g9 * 19;

        long f1_2 = f1 * 2, f3_2 = f3 * 2, f5_2 = f5 * 2, f7_2 = f7 * 2, f9_2 = f9 * 2;

        long h0 = f0 * g0 + f1_2 * g9_19 + f2 * g8_19 + f3_2 * g7_19 + f4 * g6_19 + f5_2 * g5_19 + f6 * g4_19 + f7_2 * g3_19 + f8 * g2_19 + f9_2 * g1_19;
        long h1 = f0 * g1 + f1 * g0 + f2 * g9_19 + f3 * g8_19 + f4 * g7_19 + f5 * g6_19 + f6 * g5_19 + f7 * g4_19 + f8 * g3_19 + f9 * g2_19;
        long h2 = f0 * g2 + f1_2 * g1 + f2 * g0 + f3_2 * g9_19 + f4 * g8_19 + f5_2 * g7_19 + f6 * g6_19 + f7_2 * g5_19 + f8 * g4_19 + f9_2 * g3_19;
        long h3 = f0 * g3 + f1 * g2 + f2 * g1 + f3 * g0 + f4 * g9_19 + f5 * g8_19 + f6 * g7_19 + f7 * g6_19 + f8 * g5_19 + f9 * g4_19;
        long h4 = f0 * g4 + f1_2 * g3 + f2 * g2 + f3_2 * g1 + f4 * g0 + f5_2 * g9_19 + f6 * g8_19 + f7_2 * g7_19 + f8 * g6_19 + f9_2 * g5_19;
        long h5 = f0 * g5 + f1 * g4 + f2 * g3 + f3 * g2 + f4 * g1 + f5 * g0 + f6 * g9_19 + f7 * g8_19 + f8 * g7_19 + f9 * g6_19;
        long h6 = f0 * g6 + f1_2 * g5 + f2 * g4 + f3_2 * g3 + f4 * g2 + f5_2 * g1 + f6 * g0 + f7_2 * g9_19 + f8 * g8_19 + f9_2 * g7_19;
        long h7 = f0 * g7 + f1 * g6 + f2 * g5 + f3 * g4 + f4 * g3 + f5 * g2 + f6 * g1 + f7 * g0 + f8 * g9_19 + f9 * g8_19;
        long h8 = f0 * g8 + f1_2 * g7 + f2 * g6 + f3_2 * g5 + f4 * g4 + f5_2 * g3 + f6 * g2 + f7_2 * g1 + f8 * g0 + f9_2 * g9_19;
        long h9 = f0 * g9 + f1 * g8 + f2 * g7 + f3 * g6 + f4 * g5 + f5 * g4 + f6 * g3 + f7 * g2 + f8 * g1 + f9 * g0;

        h[0] = h0; h[1] = h1; h[2] = h2; h[3] = h3; h[4] = h4;
        h[5] = h5; h[6] = h6; h[7] = h7; h[8] = h8; h[9] = h9;

        FeCarry(h);
        FeCarry(h);
    }

    private static void FeSquare(Span<long> h, ReadOnlySpan<long> f) => FeMul(h, f, f);

    private static void FeInvert(Span<long> outVal, ReadOnlySpan<long> z)
    {
        Span<long> t0 = stackalloc long[10];
        Span<long> t1 = stackalloc long[10];
        Span<long> t2 = stackalloc long[10];
        Span<long> t3 = stackalloc long[10];

        // Addition chain for 2^255 - 21
        FeSquare(t0, z); // 2
        FeSquare(t1, t0); // 4
        FeSquare(t1, t1); // 8
        FeMul(t1, z, t1); // 9
        FeMul(t0, t0, t1); // 11
        FeSquare(t2, t0); // 22
        FeMul(t1, t1, t2); // 33

        FeSquare(t2, t1); // 66
        for (int i = 1; i < 5; i++) FeSquare(t2, t2);
        FeMul(t1, t2, t1); // 2^10 - 1

        FeSquare(t2, t1); // 2^20 - 2^10
        for (int i = 1; i < 10; i++) FeSquare(t2, t2);
        FeMul(t2, t2, t1); // 2^20 - 1

        FeSquare(t3, t2); // 2^40 - 2^20
        for (int i = 1; i < 20; i++) FeSquare(t3, t3);
        FeMul(t2, t3, t2); // 2^40 - 1

        FeSquare(t2, t2); // 2^50 - 2^10
        for (int i = 1; i < 10; i++) FeSquare(t2, t2);
        FeMul(t1, t2, t1); // 2^50 - 1

        FeSquare(t2, t1); // 2^100 - 2^50
        for (int i = 1; i < 50; i++) FeSquare(t2, t2);
        FeMul(t2, t2, t1); // 2^100 - 1

        FeSquare(t3, t2); // 2^200 - 2^100
        for (int i = 1; i < 100; i++) FeSquare(t3, t3);
        FeMul(t2, t3, t2); // 2^200 - 1

        FeSquare(t2, t2); // 2^250 - 2^50
        for (int i = 1; i < 50; i++) FeSquare(t2, t2);
        FeMul(t1, t2, t1); // 2^250 - 1

        FeSquare(t1, t1); // 2^251 - 2
        FeSquare(t1, t1); // 2^252 - 4
        FeSquare(t1, t1); // 2^253 - 8
        FeSquare(t1, t1); // 2^254 - 16
        FeSquare(t1, t1); // 2^255 - 32
        FeMul(outVal, t1, t0); // 2^255 - 21
    }

    #endregion
}
