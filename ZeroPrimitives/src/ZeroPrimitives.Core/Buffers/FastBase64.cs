using System;
using System.Runtime.CompilerServices;

namespace ZeroPrimitives.Buffers
{
    /// <summary>
    /// Zero-allocation RFC 4648 Base64 and Base64Url encoder/decoder operating across <see cref="char"/> and UTF-8 <see cref="byte"/> spans.
    /// Provides full Base64Url parity on <c>.NET Framework 4.6.2</c> and <c>.NET Standard 2.0</c>.
    /// </summary>
    public static class FastBase64
    {
        private const string StandardAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";
        private const string UrlSafeAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_";

        private static readonly sbyte[] StandardDecodeMap = CreateDecodeMap(urlSafe: false);
        private static readonly sbyte[] UrlSafeDecodeMap = CreateDecodeMap(urlSafe: true);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetEncodedLength(int byteCount, bool includePadding = true)
        {
            if (byteCount < 0)
                throw new ArgumentOutOfRangeException(nameof(byteCount));
            if (byteCount == 0) return 0;
            if (includePadding)
                return ((byteCount + 2) / 3) * 4;

            int groups = byteCount / 3;
            int rem = byteCount - (groups * 3);
            return (groups * 4) + (rem == 0 ? 0 : rem + 1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetMaxDecodedLength(int encodedLength)
        {
            if (encodedLength <= 0) return 0;
            int groups = encodedLength / 4;
            int rem = encodedLength - (groups * 4);
            return (groups * 3) + (rem > 1 ? rem - 1 : 0);
        }

        public static bool TryEncode(
            ReadOnlySpan<byte> source,
            Span<char> destination,
            out int charsWritten,
            bool urlSafe = false,
            bool includePadding = true)
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

            string alphabet = urlSafe ? UrlSafeAlphabet : StandardAlphabet;
            int srcIdx = 0;
            int dstIdx = 0;
            int fullEnd = source.Length - (source.Length % 3);

            while (srcIdx < fullEnd)
            {
                int b0 = source[srcIdx++];
                int b1 = source[srcIdx++];
                int b2 = source[srcIdx++];
                int triple = (b0 << 16) | (b1 << 8) | b2;

                destination[dstIdx++] = alphabet[(triple >> 18) & 0x3F];
                destination[dstIdx++] = alphabet[(triple >> 12) & 0x3F];
                destination[dstIdx++] = alphabet[(triple >> 6) & 0x3F];
                destination[dstIdx++] = alphabet[triple & 0x3F];
            }

            int rem = source.Length - fullEnd;
            if (rem == 1)
            {
                int b0 = source[srcIdx];
                destination[dstIdx++] = alphabet[b0 >> 2];
                destination[dstIdx++] = alphabet[(b0 & 0x03) << 4];
                if (includePadding)
                {
                    destination[dstIdx++] = '=';
                    destination[dstIdx++] = '=';
                }
            }
            else if (rem == 2)
            {
                int b0 = source[srcIdx++];
                int b1 = source[srcIdx];
                destination[dstIdx++] = alphabet[b0 >> 2];
                destination[dstIdx++] = alphabet[((b0 & 0x03) << 4) | (b1 >> 4)];
                destination[dstIdx++] = alphabet[(b1 & 0x0F) << 2];
                if (includePadding)
                {
                    destination[dstIdx++] = '=';
                }
            }

            charsWritten = dstIdx;
            return true;
        }

        public static bool TryEncodeUtf8(
            ReadOnlySpan<byte> source,
            Span<byte> destination,
            out int bytesWritten,
            bool urlSafe = false,
            bool includePadding = true)
        {
            int required = GetEncodedLength(source.Length, includePadding);
            if (destination.Length < required)
            {
                bytesWritten = 0;
                return false;
            }
            if (source.IsEmpty)
            {
                bytesWritten = 0;
                return true;
            }

            string alphabet = urlSafe ? UrlSafeAlphabet : StandardAlphabet;
            int srcIdx = 0;
            int dstIdx = 0;
            int fullEnd = source.Length - (source.Length % 3);

            while (srcIdx < fullEnd)
            {
                int b0 = source[srcIdx++];
                int b1 = source[srcIdx++];
                int b2 = source[srcIdx++];
                int triple = (b0 << 16) | (b1 << 8) | b2;

                destination[dstIdx++] = (byte)alphabet[(triple >> 18) & 0x3F];
                destination[dstIdx++] = (byte)alphabet[(triple >> 12) & 0x3F];
                destination[dstIdx++] = (byte)alphabet[(triple >> 6) & 0x3F];
                destination[dstIdx++] = (byte)alphabet[triple & 0x3F];
            }

            int rem = source.Length - fullEnd;
            if (rem == 1)
            {
                int b0 = source[srcIdx];
                destination[dstIdx++] = (byte)alphabet[b0 >> 2];
                destination[dstIdx++] = (byte)alphabet[(b0 & 0x03) << 4];
                if (includePadding)
                {
                    destination[dstIdx++] = (byte)'=';
                    destination[dstIdx++] = (byte)'=';
                }
            }
            else if (rem == 2)
            {
                int b0 = source[srcIdx++];
                int b1 = source[srcIdx];
                destination[dstIdx++] = (byte)alphabet[b0 >> 2];
                destination[dstIdx++] = (byte)alphabet[((b0 & 0x03) << 4) | (b1 >> 4)];
                destination[dstIdx++] = (byte)alphabet[(b1 & 0x0F) << 2];
                if (includePadding)
                {
                    destination[dstIdx++] = (byte)'=';
                }
            }

            bytesWritten = dstIdx;
            return true;
        }

        public static bool TryDecode(
            ReadOnlySpan<char> encoded,
            Span<byte> destination,
            out int bytesWritten,
            bool urlSafe = false)
        {
            bytesWritten = 0;
            encoded = TrimAscii(encoded);
            if (encoded.IsEmpty) return true;

            int padding = 0;
            if (encoded[encoded.Length - 1] == '=')
            {
                padding++;
                if (encoded.Length >= 2 && encoded[encoded.Length - 2] == '=')
                    padding++;
            }

            int unpaddedLen = encoded.Length - padding;
            if ((unpaddedLen & 3) == 1) return false;
            if (padding > 0 && (encoded.Length & 3) != 0) return false;

            int expectedBytes = ((unpaddedLen / 4) * 3) + ((unpaddedLen & 3) == 2 ? 1 : ((unpaddedLen & 3) == 3 ? 2 : 0));
            if (destination.Length < expectedBytes) return false;

            sbyte[] map = urlSafe ? UrlSafeDecodeMap : StandardDecodeMap;
            int srcIdx = 0;
            int dstIdx = 0;
            int fullGroupsEnd = unpaddedLen - (unpaddedLen & 3);

            while (srcIdx < fullGroupsEnd)
            {
                int v0 = LookupChar(map, encoded[srcIdx++]);
                int v1 = LookupChar(map, encoded[srcIdx++]);
                int v2 = LookupChar(map, encoded[srcIdx++]);
                int v3 = LookupChar(map, encoded[srcIdx++]);
                if ((v0 | v1 | v2 | v3) < 0) { bytesWritten = 0; return false; }

                int triple = (v0 << 18) | (v1 << 12) | (v2 << 6) | v3;
                destination[dstIdx++] = (byte)(triple >> 16);
                destination[dstIdx++] = (byte)(triple >> 8);
                destination[dstIdx++] = (byte)triple;
            }

            int rem = unpaddedLen - fullGroupsEnd;
            if (rem == 2)
            {
                int v0 = LookupChar(map, encoded[srcIdx++]);
                int v1 = LookupChar(map, encoded[srcIdx]);
                if ((v0 | v1) < 0) { bytesWritten = 0; return false; }
                destination[dstIdx++] = (byte)((v0 << 2) | (v1 >> 4));
            }
            else if (rem == 3)
            {
                int v0 = LookupChar(map, encoded[srcIdx++]);
                int v1 = LookupChar(map, encoded[srcIdx++]);
                int v2 = LookupChar(map, encoded[srcIdx]);
                if ((v0 | v1 | v2) < 0) { bytesWritten = 0; return false; }
                destination[dstIdx++] = (byte)((v0 << 2) | (v1 >> 4));
                destination[dstIdx++] = (byte)((v1 << 4) | (v2 >> 2));
            }

            bytesWritten = dstIdx;
            return true;
        }

        public static bool TryDecode(
            ReadOnlySpan<byte> encodedUtf8,
            Span<byte> destination,
            out int bytesWritten,
            bool urlSafe = false)
        {
            bytesWritten = 0;
            encodedUtf8 = TrimAsciiBytes(encodedUtf8);
            if (encodedUtf8.IsEmpty) return true;

            int padding = 0;
            if (encodedUtf8[encodedUtf8.Length - 1] == (byte)'=')
            {
                padding++;
                if (encodedUtf8.Length >= 2 && encodedUtf8[encodedUtf8.Length - 2] == (byte)'=')
                    padding++;
            }

            int unpaddedLen = encodedUtf8.Length - padding;
            if ((unpaddedLen & 3) == 1) return false;
            if (padding > 0 && (encodedUtf8.Length & 3) != 0) return false;

            int expectedBytes = ((unpaddedLen / 4) * 3) + ((unpaddedLen & 3) == 2 ? 1 : ((unpaddedLen & 3) == 3 ? 2 : 0));
            if (destination.Length < expectedBytes) return false;

            sbyte[] map = urlSafe ? UrlSafeDecodeMap : StandardDecodeMap;
            int srcIdx = 0;
            int dstIdx = 0;
            int fullGroupsEnd = unpaddedLen - (unpaddedLen & 3);

            while (srcIdx < fullGroupsEnd)
            {
                int v0 = map[encodedUtf8[srcIdx++]];
                int v1 = map[encodedUtf8[srcIdx++]];
                int v2 = map[encodedUtf8[srcIdx++]];
                int v3 = map[encodedUtf8[srcIdx++]];
                if ((v0 | v1 | v2 | v3) < 0) { bytesWritten = 0; return false; }

                int triple = (v0 << 18) | (v1 << 12) | (v2 << 6) | v3;
                destination[dstIdx++] = (byte)(triple >> 16);
                destination[dstIdx++] = (byte)(triple >> 8);
                destination[dstIdx++] = (byte)triple;
            }

            int rem = unpaddedLen - fullGroupsEnd;
            if (rem == 2)
            {
                int v0 = map[encodedUtf8[srcIdx++]];
                int v1 = map[encodedUtf8[srcIdx]];
                if ((v0 | v1) < 0) { bytesWritten = 0; return false; }
                destination[dstIdx++] = (byte)((v0 << 2) | (v1 >> 4));
            }
            else if (rem == 3)
            {
                int v0 = map[encodedUtf8[srcIdx++]];
                int v1 = map[encodedUtf8[srcIdx++]];
                int v2 = map[encodedUtf8[srcIdx]];
                if ((v0 | v1 | v2) < 0) { bytesWritten = 0; return false; }
                destination[dstIdx++] = (byte)((v0 << 2) | (v1 >> 4));
                destination[dstIdx++] = (byte)((v1 << 4) | (v2 >> 2));
            }

            bytesWritten = dstIdx;
            return true;
        }

        public static unsafe string ToString(ReadOnlySpan<byte> source, bool urlSafe = false, bool includePadding = true)
        {
            if (source.IsEmpty) return string.Empty;
            int len = GetEncodedLength(source.Length, includePadding);
            string result = new string('\0', len);
            fixed (char* p = result)
            {
                TryEncode(source, new Span<char>(p, len), out _, urlSafe, includePadding);
            }
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int LookupChar(sbyte[] map, char c)
            => (uint)c < 256U ? map[c] : -1;

        private static ReadOnlySpan<char> TrimAscii(ReadOnlySpan<char> span)
        {
            int s = 0, e = span.Length - 1;
            while (s <= e && (span[s] == ' ' || span[s] == '\t' || span[s] == '\r' || span[s] == '\n')) s++;
            while (e >= s && (span[e] == ' ' || span[e] == '\t' || span[e] == '\r' || span[e] == '\n')) e--;
            return s <= e ? span.Slice(s, e - s + 1) : ReadOnlySpan<char>.Empty;
        }

        private static ReadOnlySpan<byte> TrimAsciiBytes(ReadOnlySpan<byte> span)
        {
            int s = 0, e = span.Length - 1;
            while (s <= e && (span[s] == ' ' || span[s] == '\t' || span[s] == '\r' || span[s] == '\n')) s++;
            while (e >= s && (span[e] == ' ' || span[e] == '\t' || span[e] == '\r' || span[e] == '\n')) e--;
            return s <= e ? span.Slice(s, e - s + 1) : ReadOnlySpan<byte>.Empty;
        }

        private static sbyte[] CreateDecodeMap(bool urlSafe)
        {
            sbyte[] map = new sbyte[256];
            for (int i = 0; i < 256; i++) map[i] = -1;

            string alphabet = urlSafe ? UrlSafeAlphabet : StandardAlphabet;
            for (int i = 0; i < alphabet.Length; i++)
            {
                map[alphabet[i]] = (sbyte)i;
            }
            return map;
        }
    }
}
