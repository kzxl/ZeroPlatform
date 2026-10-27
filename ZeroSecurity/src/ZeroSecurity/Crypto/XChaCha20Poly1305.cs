using System;
using ZeroSecurity.Common;

namespace ZeroSecurity.Crypto;

/// <summary>
/// Pure C# implementation of the XChaCha20-Poly1305 extended-nonce Authenticated Encryption with Associated Data (AEAD) cipher.
/// Utilizes a 192-bit (24-byte) nonce to eliminate birthday-bound nonce reuse hazards, enabling safe random-nonce generation
/// for quarantine vaults, disk persistence, and long-lived telemetry streams.
/// </summary>
public static class XChaCha20Poly1305
{
    public const int KeySize = 32;
    public const int NonceSize = 24;
    public const int TagSize = 16;

    /// <summary>
    /// Encrypts plaintext using XChaCha20-Poly1305 AEAD.
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

        Span<byte> subkey = stackalloc byte[KeySize];
        Span<byte> subNonce = stackalloc byte[ChaCha20Poly1305.NonceSize];

        try
        {
            // 1. Derive subkey via HChaCha20 over first 16 bytes of nonce
            ChaCha20Poly1305.HChaCha20(key, nonce.Slice(0, 16), subkey);

            // 2. Sub-nonce: 4 zero bytes + last 8 bytes of nonce
            subNonce.Slice(0, 4).Clear();
            nonce.Slice(16, 8).CopyTo(subNonce.Slice(4, 8));

            // 3. Encrypt via ChaCha20Poly1305
            ChaCha20Poly1305.Encrypt(subkey, subNonce, associatedData, plaintext, ciphertext, tag);
        }
        finally
        {
            CryptoMemory.SecureZero(subkey);
        }
    }

    /// <summary>
    /// Decrypts ciphertext and verifies Poly1305 authentication tag using XChaCha20-Poly1305 AEAD.
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

        Span<byte> subkey = stackalloc byte[KeySize];
        Span<byte> subNonce = stackalloc byte[ChaCha20Poly1305.NonceSize];

        try
        {
            // 1. Derive subkey via HChaCha20 over first 16 bytes of nonce
            ChaCha20Poly1305.HChaCha20(key, nonce.Slice(0, 16), subkey);

            // 2. Sub-nonce: 4 zero bytes + last 8 bytes of nonce
            subNonce.Slice(0, 4).Clear();
            nonce.Slice(16, 8).CopyTo(subNonce.Slice(4, 8));

            // 3. Decrypt via ChaCha20Poly1305
            return ChaCha20Poly1305.Decrypt(subkey, subNonce, associatedData, ciphertext, tag, plaintext);
        }
        finally
        {
            CryptoMemory.SecureZero(subkey);
        }
    }
}
