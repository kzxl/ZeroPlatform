using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using ZeroSecurity.Common;
using ZeroSecurity.Hashing;

namespace ZeroSecurity.Tokens
{
    /// <summary>
    /// Supported hashing algorithms for TOTP / HOTP calculation.
    /// </summary>
    public enum TotpHashAlgorithm
    {
        Sha1,
        Sha256
    }

    /// <summary>
    /// Ultra-fast, zero-allocation implementation of RFC 6238 (TOTP - Time-Based One-Time Password)
    /// and RFC 4226 (HOTP - HMAC-Based One-Time Password) two-factor authentication.
    /// Supports 6-digit and 8-digit codes directly formatted into Span<char> with constant-time verification.
    /// </summary>
    public static class Totp
    {
        private static readonly int[] DigitsModulos = { 1, 10, 100, 1000, 10000, 100000, 1000000, 10000000, 100000000 };

#if !NET8_0_OR_GREATER
        [ThreadStatic]
        private static HMACSHA1? _threadSha1;
#endif

        /// <summary>
        /// Generates a time-based one-time password (TOTP) into the destination character span.
        /// </summary>
        /// <param name="secretKey">Secret key bytes.</param>
        /// <param name="destination">Destination span (must have length at least equal to digits).</param>
        /// <param name="charsWritten">The number of characters written.</param>
        /// <param name="unixTimeSeconds">Optional Unix timestamp in seconds (defaults to current UTC time if &lt; 0).</param>
        /// <param name="digits">Number of digits (typically 6 or 8, default 6).</param>
        /// <param name="stepSeconds">Time step in seconds (default 30 per RFC 6238).</param>
        /// <param name="algorithm">Hash algorithm (Sha1 default for Google/Microsoft Authenticator, or Sha256).</param>
        public static bool TryGenerateTotp(
            ReadOnlySpan<byte> secretKey,
            Span<char> destination,
            out int charsWritten,
            long unixTimeSeconds = -1,
            int digits = 6,
            int stepSeconds = 30,
            TotpHashAlgorithm algorithm = TotpHashAlgorithm.Sha1)
        {
            if (unixTimeSeconds < 0)
            {
                unixTimeSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            }

            long counter = unixTimeSeconds / stepSeconds;
            return TryGenerateHotp(secretKey, counter, destination, out charsWritten, digits, algorithm);
        }

        /// <summary>
        /// Generates a TOTP string.
        /// </summary>
        public static string GenerateTotp(
            ReadOnlySpan<byte> secretKey,
            long unixTimeSeconds = -1,
            int digits = 6,
            int stepSeconds = 30,
            TotpHashAlgorithm algorithm = TotpHashAlgorithm.Sha1)
        {
            Span<char> buffer = stackalloc char[digits];
            if (!TryGenerateTotp(secretKey, buffer, out int written, unixTimeSeconds, digits, stepSeconds, algorithm))
            {
                throw new ArgumentException("Failed to generate TOTP code.");
            }

#if NET8_0_OR_GREATER
            return new string(buffer.Slice(0, written));
#else
            return buffer.Slice(0, written).ToString();
#endif
        }

        /// <summary>
        /// Generates an HMAC-based one-time password (HOTP) for a specific counter into the destination span.
        /// Zero managed allocation.
        /// </summary>
        public static bool TryGenerateHotp(
            ReadOnlySpan<byte> secretKey,
            long counter,
            Span<char> destination,
            out int charsWritten,
            int digits = 6,
            TotpHashAlgorithm algorithm = TotpHashAlgorithm.Sha1)
        {
            if (digits < 6 || digits > 8)
                throw new ArgumentOutOfRangeException(nameof(digits), "Digits must be between 6 and 8.");

            if (destination.Length < digits)
            {
                charsWritten = 0;
                return false;
            }

            Span<byte> counterBytes = stackalloc byte[8];
            BinaryPrimitives.WriteInt64BigEndian(counterBytes, counter);

            Span<byte> hash = stackalloc byte[32];
            int hashLength;

            if (algorithm == TotpHashAlgorithm.Sha256)
            {
                HmacSha256.Hash(secretKey, counterBytes, hash);
                hashLength = 32;
            }
            else
            {
                hashLength = ComputeHmacSha1(secretKey, counterBytes, hash.Slice(0, 20));
            }

            // Dynamic Truncation (RFC 4226 Section 5.4)
            int offset = hash[hashLength - 1] & 0x0F;
            int binaryCode = ((hash[offset] & 0x7F) << 24)
                           | ((hash[offset + 1] & 0xFF) << 16)
                           | ((hash[offset + 2] & 0xFF) << 8)
                           | (hash[offset + 3] & 0xFF);

            int modulo = DigitsModulos[digits];
            int otp = binaryCode % modulo;

            // Format integer with leading zeros
            for (int i = digits - 1; i >= 0; i--)
            {
                destination[i] = (char)('0' + (otp % 10));
                otp /= 10;
            }

            charsWritten = digits;
            return true;
        }

        /// <summary>
        /// Verifies a TOTP code against the secret key using constant-time comparison across a drift window.
        /// </summary>
        /// <param name="secretKey">Secret key bytes.</param>
        /// <param name="code">The code provided by the user.</param>
        /// <param name="window">Tolerance window for clock drift (e.g. 1 means [-30s, 0, +30s]). Default 1.</param>
        /// <param name="unixTimeSeconds">Optional Unix timestamp in seconds (defaults to UTC now).</param>
        /// <param name="digits">Number of digits (default 6).</param>
        /// <param name="stepSeconds">Time step in seconds (default 30).</param>
        /// <param name="algorithm">Hash algorithm (default Sha1).</param>
        public static bool VerifyTotp(
            ReadOnlySpan<byte> secretKey,
            ReadOnlySpan<char> code,
            int window = 1,
            long unixTimeSeconds = -1,
            int digits = 6,
            int stepSeconds = 30,
            TotpHashAlgorithm algorithm = TotpHashAlgorithm.Sha1)
        {
            if (code.Length != digits) return false;

            if (unixTimeSeconds < 0)
            {
                unixTimeSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            }

            long currentStep = unixTimeSeconds / stepSeconds;
            Span<char> candidate = stackalloc char[digits];

            bool matched = false;
            for (int i = -window; i <= window; i++)
            {
                long step = currentStep + i;
                if (TryGenerateHotp(secretKey, step, candidate, out int written, digits, algorithm))
                {
                    if (CryptoMemory.ConstantTimeEquals(candidate.Slice(0, written), code))
                    {
                        matched = true;
                    }
                }
            }

            return matched;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ComputeHmacSha1(ReadOnlySpan<byte> key, ReadOnlySpan<byte> data, Span<byte> destination)
        {
#if NET8_0_OR_GREATER
            return HMACSHA1.HashData(key, data, destination);
#else
            var hmac = _threadSha1;
            if (hmac == null)
            {
                hmac = new HMACSHA1(key.ToArray());
                _threadSha1 = hmac;
            }
            else
            {
                hmac.Key = key.ToArray();
            }

            byte[] result = hmac.ComputeHash(data.ToArray());
            result.AsSpan().CopyTo(destination);
            return 20;
#endif
        }
    }

    /// <summary>
    /// RFC 4648 Base32 decoder for Authenticator secret keys (e.g. Google Authenticator).
    /// </summary>
    public static class Base32
    {
        private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        private static readonly byte[] Lookup = CreateLookup();

        /// <summary>
        /// Attempts to decode an RFC 4648 Base32 string/span into the destination byte span.
        /// Ignores whitespace, dashes, and padding ('=').
        /// </summary>
        public static bool TryDecode(ReadOnlySpan<char> input, Span<byte> destination, out int bytesWritten)
        {
            int buffer = 0;
            int bitsLeft = 0;
            int count = 0;

            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];
                if (c == ' ' || c == '-' || c == '=') continue;

                int val = c < 128 ? Lookup[c] : 0xFF;
                if (val == 0xFF)
                {
                    bytesWritten = 0;
                    return false;
                }

                buffer = (buffer << 5) | val;
                bitsLeft += 5;

                if (bitsLeft >= 8)
                {
                    if (count >= destination.Length)
                    {
                        bytesWritten = 0;
                        return false;
                    }

                    destination[count++] = (byte)(buffer >> (bitsLeft - 8));
                    bitsLeft -= 8;
                }
            }

            bytesWritten = count;
            return true;
        }

        public static byte[] Decode(ReadOnlySpan<char> input)
        {
            int maxBytes = (input.Length * 5 + 7) / 8;
            byte[] buffer = new byte[maxBytes];
            if (!TryDecode(input, buffer, out int written))
            {
                throw new FormatException("Input is not a valid Base32 encoded string.");
            }

            if (written == maxBytes) return buffer;

            byte[] result = new byte[written];
            Array.Copy(buffer, result, written);
            return result;
        }

        private static byte[] CreateLookup()
        {
            byte[] table = new byte[128];
            for (int i = 0; i < table.Length; i++) table[i] = 0xFF;

            for (byte i = 0; i < Alphabet.Length; i++)
            {
                char c = Alphabet[i];
                table[c] = i;
                table[char.ToLowerInvariant(c)] = i;
            }

            return table;
        }
    }
}
