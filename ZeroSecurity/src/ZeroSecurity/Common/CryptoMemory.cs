using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace ZeroSecurity.Common;

/// <summary>
/// Sovereign memory hygiene utilities and constant-time security operations.
/// Prevents compiler dead-store elimination and side-channel timing attacks.
/// </summary>
public static class CryptoMemory
{
    /// <summary>
    /// Cryptographically zeroes out memory in the given span, ensuring the compiler/JIT
    /// will not optimize away or eliminate the write as a dead store.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    public static void SecureZero(Span<byte> buffer)
    {
        if (buffer.IsEmpty) return;

        unsafe
        {
            fixed (byte* ptr = buffer)
            {
                byte* p = ptr;
                int len = buffer.Length;
                while (len-- > 0)
                {
                    Volatile.Write(ref *p, (byte)0);
                    p++;
                }
            }
        }
    }

    /// <summary>
    /// Cryptographically zeroes out memory in the given char span, ensuring the compiler/JIT
    /// will not optimize away or eliminate the write as a dead store.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    public static void SecureZero(Span<char> buffer)
    {
        if (buffer.IsEmpty) return;

        unsafe
        {
            fixed (char* ptr = buffer)
            {
                ushort* p = (ushort*)ptr;
                int len = buffer.Length;
                while (len-- > 0)
                {
                    Volatile.Write(ref *p, (ushort)0);
                    p++;
                }
            }
        }
    }

    /// <summary>
    /// Compares two byte spans for equality in constant time to prevent timing side-channel attacks.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    public static bool ConstantTimeEquals(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b)
    {
        if (a.Length != b.Length) return false;

        int diff = 0;
        for (int i = 0; i < a.Length; i++)
        {
            diff |= a[i] ^ b[i];
        }

        return diff == 0;
    }

    /// <summary>
    /// Compares two char spans (e.g. passwords, tokens, API keys) for equality in constant time
    /// to prevent timing side-channel attacks.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    public static bool ConstantTimeEquals(ReadOnlySpan<char> a, ReadOnlySpan<char> b)
    {
        if (a.Length != b.Length) return false;

        int diff = 0;
        for (int i = 0; i < a.Length; i++)
        {
            diff |= a[i] ^ b[i];
        }

        return diff == 0;
    }

    /// <summary>
    /// Compares two strings in constant time to prevent timing side-channel attacks.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    public static bool ConstantTimeEquals(string? a, string? b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a == null || b == null) return false;

        return ConstantTimeEquals(a.AsSpan(), b.AsSpan());
    }
}
