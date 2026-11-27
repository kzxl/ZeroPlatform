using System;
using System.Runtime.CompilerServices;

namespace ZeroPrimitives.Buffers
{
    /// <summary>
    /// Zero-allocation Crockford Base32 (human-friendly voucher/tracking codes) and RFC 4648 Base32 encoder/decoder.
    /// </summary>
    public static class FastBase32
    {
        private const string CrockfordAlphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
        private const string Rfc4648Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

        private static readonly sbyte[] CrockfordDecodeMap = CreateCrockfordDecodeMap();
        private static readonly sbyte[] Rfc4648DecodeMap = CreateRfc4648DecodeMap();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetEncodedLength(int byteCount, bool includePadding = false)
        {
            if (byteCount < 0)
                throw new ArgumentOutOfRangeException(nameof(byteCount));
            if (byteCount == 0) return 0;
            if (includePadding)
                return ((byteCount + 4) / 5) * 8;
            return ((byteCount * 8) + 4) / 5;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetMaxDecodedLength(int encodedCharCount)
        {
            if (encodedCharCount <= 0) return 0;
            return (encodedCharCount * 5) / 8;
        }

        public static bool TryEncodeCrockford(ReadOnlySpan<byte> source, Span<char> destination, out int charsWritten)
            => TryEncodeCore(source, destination, out charsWritten, CrockfordAlphabet, includePadding: false);

        public static bool TryEncodeRfc4648(
            ReadOnlySpan<byte> source,
            Span<char> destination,
            out int charsWritten,
            bool includePadding = false)
            => TryEncodeCore(source, destination, out charsWritten, Rfc4648Alphabet, includePadding);

        public static bool TryDecodeCrockford(ReadOnlySpan<char> encoded, Span<byte> destination, out int bytesWritten)
            => TryDecodeCore(encoded, destination, out bytesWritten, CrockfordDecodeMap, ignoreHyphens: true);

        public static bool TryDecodeRfc4648(ReadOnlySpan<char> encoded, Span<byte> destination, out int bytesWritten)
            => TryDecodeCore(encoded, destination, out bytesWritten, Rfc4648DecodeMap, ignoreHyphens: false);

        public static unsafe string ToCrockfordString(ReadOnlySpan<byte> source)
        {
            if (source.IsEmpty) return string.Empty;
            int len = GetEncodedLength(source.Length, includePadding: false);
            string result = new string('\0', len);
            fixed (char* p = result)
            {
                TryEncodeCrockford(source, new Span<char>(p, len), out _);
            }
            return result;
        }

        public static unsafe string ToRfc4648String(ReadOnlySpan<byte> source, bool includePadding = false)
        {
            if (source.IsEmpty) return string.Empty;
            int len = GetEncodedLength(source.Length, includePadding);
            string result = new string('\0', len);
            fixed (char* p = result)
            {
                TryEncodeRfc4648(source, new Span<char>(p, len), out _, includePadding);
            }
            return result;
        }

        private static bool TryEncodeCore(
            ReadOnlySpan<byte> source,
            Span<char> destination,
            out int charsWritten,
            string alphabet,
            bool includePadding)
        {
            int required = GetEncodedLength(source.Length, includePadding);
            if (destination.Length < required)
            {
                charsWritten = 0;
                return false;
            }
            if (source.IsEmpty)
            {
                charsWritten = 0;
                return true;
            }

            int srcIdx = 0;
            int dstIdx = 0;
            int buffer = 0;
            int bitsLeft = 0;

            while (srcIdx < source.Length)
            {
                buffer = (buffer << 8) | source[srcIdx++];
                bitsLeft += 8;
                while (bitsLeft >= 5)
                {
                    bitsLeft -= 5;
                    destination[dstIdx++] = alphabet[(buffer >> bitsLeft) & 0x1F];
                }
            }

            if (bitsLeft > 0)
            {
                destination[dstIdx++] = alphabet[(buffer << (5 - bitsLeft)) & 0x1F];
            }

            if (includePadding)
            {
                while (dstIdx < required)
                {
                    destination[dstIdx++] = '=';
                }
            }

            charsWritten = dstIdx;
            return true;
        }

        private static bool TryDecodeCore(
            ReadOnlySpan<char> encoded,
            Span<byte> destination,
            out int bytesWritten,
            sbyte[] map,
            bool ignoreHyphens)
        {
            bytesWritten = 0;
            if (encoded.IsEmpty) return true;

            int buffer = 0;
            int bitsLeft = 0;
            int outIdx = 0;
            int charGroupCount = 0;
            bool seenPadding = false;

            for (int i = 0; i < encoded.Length; i++)
            {
                char c = encoded[i];
                if (c == ' ' || c == '\t' || c == '\r' || c == '\n') continue;
                if (ignoreHyphens && c == '-') continue;

                if (c == '=')
                {
                    seenPadding = true;
                    continue;
                }

                if (seenPadding || (uint)c >= 128U)
                {
                    bytesWritten = 0;
                    return false;
                }

                sbyte val = map[c];
                if (val < 0)
                {
                    bytesWritten = 0;
                    return false;
                }

                buffer = (buffer << 5) | (byte)val;
                bitsLeft += 5;
                charGroupCount++;

                if (bitsLeft >= 8)
                {
                    if (outIdx >= destination.Length)
                    {
                        bytesWritten = 0;
                        return false;
                    }
                    bitsLeft -= 8;
                    destination[outIdx++] = (byte)((buffer >> bitsLeft) & 0xFF);
                }
            }

            int remChars = charGroupCount & 7;
            if (remChars == 1 || remChars == 3 || remChars == 6)
            {
                bytesWritten = 0;
                return false;
            }

            bytesWritten = outIdx;
            return true;
        }

        private static sbyte[] CreateCrockfordDecodeMap()
        {
            sbyte[] map = new sbyte[128];
            for (int i = 0; i < 128; i++) map[i] = -1;

            for (int i = 0; i < CrockfordAlphabet.Length; i++)
            {
                char c = CrockfordAlphabet[i];
                map[c] = (sbyte)i;
                if (c >= 'A' && c <= 'Z')
                    map[c + 32] = (sbyte)i;
            }

            map['O'] = 0; map['o'] = 0;
            map['I'] = 1; map['i'] = 1;
            map['L'] = 1; map['l'] = 1;
            return map;
        }

        private static sbyte[] CreateRfc4648DecodeMap()
        {
            sbyte[] map = new sbyte[128];
            for (int i = 0; i < 128; i++) map[i] = -1;

            for (int i = 0; i < Rfc4648Alphabet.Length; i++)
            {
                char c = Rfc4648Alphabet[i];
                map[c] = (sbyte)i;
                if (c >= 'A' && c <= 'Z')
                    map[c + 32] = (sbyte)i;
            }
            return map;
        }
    }
}
