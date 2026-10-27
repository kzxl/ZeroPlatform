using System;
using System.IO;
using System.Text;

namespace ZeroGraphics.Vision.Codes
{
    /// <summary>
    /// ISO/IEC 18004 QR Code payload bitstream decoder.
    /// Supports Numeric, Alphanumeric, Byte (UTF-8 / ISO-8859-1), and Kanji encodation modes.
    /// </summary>
    public static class QrPayloadDecoder
    {
        private static readonly char[] AlphanumericTable = new char[]
        {
            '0', '1', '2', '3', '4', '5', '6', '7', '8', '9',
            'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J',
            'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T',
            'U', 'V', 'W', 'X', 'Y', 'Z',
            ' ', '$', '%', '*', '+', '-', '.', '/', ':'
        };

        private sealed class BitSource
        {
            private readonly byte[] _bytes;
            private int _byteOffset;
            private int _bitOffset;

            public int Available => (_bytes.Length - _byteOffset) * 8 - _bitOffset;

            public BitSource(byte[] bytes)
            {
                _bytes = bytes ?? new byte[0];
            }

            public int ReadBits(int numBits)
            {
                if (numBits < 1 || numBits > 32 || numBits > Available)
                    return 0;

                int result = 0;
                while (numBits > 0)
                {
                    if (_bitOffset > 0)
                    {
                        int bitsLeftInByte = 8 - _bitOffset;
                        int toRead = Math.Min(numBits, bitsLeftInByte);
                        int shift = bitsLeftInByte - toRead;
                        int mask = ((1 << toRead) - 1) << shift;
                        result = (result << toRead) | ((_bytes[_byteOffset] & mask) >> shift);
                        numBits -= toRead;
                        _bitOffset += toRead;
                        if (_bitOffset == 8)
                        {
                            _bitOffset = 0;
                            _byteOffset++;
                        }
                    }
                    else
                    {
                        int toRead = Math.Min(numBits, 8);
                        int shift = 8 - toRead;
                        int mask = ((1 << toRead) - 1) << shift;
                        result = (result << toRead) | ((_bytes[_byteOffset] & mask) >> shift);
                        numBits -= toRead;
                        _bitOffset += toRead;
                        if (_bitOffset == 8)
                        {
                            _bitOffset = 0;
                            _byteOffset++;
                        }
                    }
                }

                return result;
            }
        }

        public static string Decode(byte[] dataBytes, QrVersion version)
        {
            if (dataBytes == null || dataBytes.Length == 0)
                return string.Empty;

            var bits = new BitSource(dataBytes);
            var sb = new StringBuilder();

            while (bits.Available >= 4)
            {
                int modeBits = bits.ReadBits(4);
                var mode = (QrMode)modeBits;

                if (mode == QrMode.Terminator)
                    break;

                switch (mode)
                {
                    case QrMode.Numeric:
                        DecodeNumeric(bits, sb, version);
                        break;
                    case QrMode.Alphanumeric:
                        DecodeAlphanumeric(bits, sb, version);
                        break;
                    case QrMode.Byte:
                        DecodeByte(bits, sb, version);
                        break;
                    case QrMode.Kanji:
                        DecodeKanji(bits, sb, version);
                        break;
                    case QrMode.Eci:
                        // Read ECI designator and continue
                        bits.ReadBits(8);
                        break;
                    default:
                        // Unknown or unsupported mode, break
                        return sb.ToString();
                }
            }

            return sb.ToString();
        }

        private static void DecodeNumeric(BitSource bits, StringBuilder sb, QrVersion version)
        {
            int countBits = (version.VersionNumber <= 9) ? 10 : (version.VersionNumber <= 26) ? 12 : 14;
            int count = bits.ReadBits(countBits);

            while (count >= 3)
            {
                if (bits.Available < 10) break;
                int val = bits.ReadBits(10);
                sb.Append((char)('0' + (val / 100)));
                sb.Append((char)('0' + ((val / 10) % 10)));
                sb.Append((char)('0' + (val % 10)));
                count -= 3;
            }

            if (count == 2)
            {
                if (bits.Available >= 7)
                {
                    int val = bits.ReadBits(7);
                    sb.Append((char)('0' + (val / 10)));
                    sb.Append((char)('0' + (val % 10)));
                }
            }
            else if (count == 1)
            {
                if (bits.Available >= 4)
                {
                    int val = bits.ReadBits(4);
                    sb.Append((char)('0' + val));
                }
            }
        }

        private static void DecodeAlphanumeric(BitSource bits, StringBuilder sb, QrVersion version)
        {
            int countBits = (version.VersionNumber <= 9) ? 9 : (version.VersionNumber <= 26) ? 11 : 13;
            int count = bits.ReadBits(countBits);

            while (count >= 2)
            {
                if (bits.Available < 11) break;
                int val = bits.ReadBits(11);
                int c1 = val / 45;
                int c2 = val % 45;
                if (c1 < AlphanumericTable.Length) sb.Append(AlphanumericTable[c1]);
                if (c2 < AlphanumericTable.Length) sb.Append(AlphanumericTable[c2]);
                count -= 2;
            }

            if (count == 1)
            {
                if (bits.Available >= 6)
                {
                    int val = bits.ReadBits(6);
                    if (val < AlphanumericTable.Length) sb.Append(AlphanumericTable[val]);
                }
            }
        }

        private static void DecodeByte(BitSource bits, StringBuilder sb, QrVersion version)
        {
            int countBits = (version.VersionNumber <= 9) ? 8 : 16;
            int count = bits.ReadBits(countBits);

            byte[] rawBytes = new byte[count];
            for (int i = 0; i < count; i++)
            {
                if (bits.Available < 8) break;
                rawBytes[i] = (byte)bits.ReadBits(8);
            }

            // Attempt UTF-8 decoding; fallback to ISO-8859-1
            try
            {
                string text = Encoding.UTF8.GetString(rawBytes);
                sb.Append(text);
            }
            catch
            {
                string text = Encoding.GetEncoding("ISO-8859-1").GetString(rawBytes);
                sb.Append(text);
            }
        }

        private static void DecodeKanji(BitSource bits, StringBuilder sb, QrVersion version)
        {
            int countBits = (version.VersionNumber <= 9) ? 8 : (version.VersionNumber <= 26) ? 10 : 12;
            int count = bits.ReadBits(countBits);

            byte[] kanjiBytes = new byte[count * 2];
            int offset = 0;

            for (int i = 0; i < count; i++)
            {
                if (bits.Available < 13) break;
                int val = bits.ReadBits(13);
                int assembled = ((val / 0x0C0) << 8) | (val % 0x0C0);
                int adjusted = assembled < 0x01F00 ? assembled + 0x08140 : assembled + 0x0C140;

                kanjiBytes[offset++] = (byte)(adjusted >> 8);
                kanjiBytes[offset++] = (byte)(adjusted & 0xFF);
            }

            try
            {
                string text = Encoding.GetEncoding("Shift_JIS").GetString(kanjiBytes, 0, offset);
                sb.Append(text);
            }
            catch
            {
                // Fallback: append hex representation
                for (int i = 0; i < offset; i++)
                {
                    sb.Append(kanjiBytes[i].ToString("X2"));
                }
            }
        }
    }
}
