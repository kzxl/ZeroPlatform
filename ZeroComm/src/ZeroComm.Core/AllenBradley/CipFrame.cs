using System;
using System.IO;
using System.Text;

namespace ZeroComm.Core.AllenBradley
{
    /// <summary>
    /// Frame encoder and decoder for EtherNet/IP encapsulation and Common Industrial Protocol (CIP) commands.
    /// Implements ANSI Extended Symbol Segment tag addressing and SendRRData messaging with zero external dependencies.
    /// </summary>
    public static class CipFrame
    {
        public const int EncapsulationHeaderLength = 24;

        #region Session Management Frames

        /// <summary>
        /// Builds a 28-byte EtherNet/IP RegisterSession encapsulation frame.
        /// </summary>
        /// <param name="senderContextLo">Sender context low 32-bit.</param>
        /// <param name="senderContextHi">Sender context high 32-bit.</param>
        public static byte[] BuildRegisterSession(uint senderContextLo = 0, uint senderContextHi = 0)
        {
            byte[] frame = new byte[28];
            // Command = 0x0065 (RegisterSession)
            frame[0] = 0x65;
            frame[1] = 0x00;
            // Length = 4 (data length following 24B header)
            frame[2] = 0x04;
            frame[3] = 0x00;
            // Session Handle = 0
            // Status = 0
            // Sender Context (8 bytes)
            frame[12] = (byte)(senderContextLo & 0xFF);
            frame[13] = (byte)((senderContextLo >> 8) & 0xFF);
            frame[14] = (byte)((senderContextLo >> 16) & 0xFF);
            frame[15] = (byte)((senderContextLo >> 24) & 0xFF);
            frame[16] = (byte)(senderContextHi & 0xFF);
            frame[17] = (byte)((senderContextHi >> 8) & 0xFF);
            frame[18] = (byte)((senderContextHi >> 16) & 0xFF);
            frame[19] = (byte)((senderContextHi >> 24) & 0xFF);
            // Options = 0
            // Protocol Version = 1 (ushort LE)
            frame[24] = 0x01;
            frame[25] = 0x00;
            // Option Flags = 0 (ushort LE)
            frame[26] = 0x00;
            frame[27] = 0x00;

            return frame;
        }

        /// <summary>
        /// Parses the RegisterSession response to extract assigned SessionHandle and status.
        /// </summary>
        public static bool ParseRegisterSessionResponse(ReadOnlySpan<byte> frame, out uint sessionHandle, out uint status)
        {
            sessionHandle = 0;
            status = 0xFFFFFFFF;

            if (frame.Length < EncapsulationHeaderLength)
                return false;

            ushort command = (ushort)(frame[0] | (frame[1] << 8));
            if (command != (ushort)CipCommand.RegisterSession)
                return false;

            sessionHandle = (uint)(frame[4] | (frame[5] << 8) | (frame[6] << 16) | (frame[7] << 24));
            status = (uint)(frame[8] | (frame[9] << 8) | (frame[10] << 16) | (frame[11] << 24));
            return status == 0;
        }

        /// <summary>
        /// Builds a 24-byte EtherNet/IP UnregisterSession encapsulation frame.
        /// </summary>
        public static byte[] BuildUnregisterSession(uint sessionHandle)
        {
            byte[] frame = new byte[EncapsulationHeaderLength];
            // Command = 0x0066 (UnregisterSession)
            frame[0] = 0x66;
            frame[1] = 0x00;
            // Length = 0
            // Session Handle
            frame[4] = (byte)(sessionHandle & 0xFF);
            frame[5] = (byte)((sessionHandle >> 8) & 0xFF);
            frame[6] = (byte)((sessionHandle >> 16) & 0xFF);
            frame[7] = (byte)((sessionHandle >> 24) & 0xFF);

            return frame;
        }

        #endregion

        #region Tag Path Encoding

        /// <summary>
        /// Encodes a Logix tag name into CIP ANSI Extended Symbol segments and Element segments.
        /// Supports standard tags ("Tank1"), array elements ("Data[2]"), and nested members ("Recipe.Speed").
        /// Returns total path bytes (always even/word-aligned) and word count.
        /// </summary>
        public static byte[] EncodeTagPath(string tagName, out byte pathWords)
        {
            if (string.IsNullOrEmpty(tagName))
                throw new ArgumentException("Tag name cannot be null or empty.", nameof(tagName));

            using (var ms = new MemoryStream())
            {
                // Split dot-separated members (e.g., "Parent.Child")
                string[] parts = tagName.Split('.');
                foreach (string rawPart in parts)
                {
                    string part = rawPart.Trim();
                    if (string.IsNullOrEmpty(part))
                        continue;

                    int bracketOpen = part.IndexOf('[');
                    if (bracketOpen >= 0)
                    {
                        // Array tag: Name[Index]
                        string symbol = part.Substring(0, bracketOpen);
                        int bracketClose = part.IndexOf(']', bracketOpen);
                        if (bracketClose < 0)
                            throw new FormatException($"Malformed array tag '{tagName}': missing closing bracket.");

                        string indexStr = part.Substring(bracketOpen + 1, bracketClose - bracketOpen - 1);
                        if (!uint.TryParse(indexStr, out uint arrayIndex))
                            throw new FormatException($"Malformed array index '{indexStr}' in tag '{tagName}'.");

                        // 1. Write ANSI Extended Symbol Segment (0x91)
                        WriteAnsiExtendedSymbol(ms, symbol);

                        // 2. Write Element Segment (0x28 for 8-bit, 0x29 for 16-bit, 0x2A for 32-bit)
                        WriteElementSegment(ms, arrayIndex);
                    }
                    else
                    {
                        // Plain symbol
                        WriteAnsiExtendedSymbol(ms, part);
                    }
                }

                byte[] pathBytes = ms.ToArray();
                if ((pathBytes.Length % 2) != 0)
                {
                    // Pad with an extra 0x00 to guarantee 16-bit word alignment
                    Array.Resize(ref pathBytes, pathBytes.Length + 1);
                }

                pathWords = (byte)(pathBytes.Length / 2);
                return pathBytes;
            }
        }

        private static void WriteAnsiExtendedSymbol(MemoryStream ms, string symbol)
        {
            byte[] asciiBytes = Encoding.ASCII.GetBytes(symbol);
            int len = asciiBytes.Length;

            ms.WriteByte(0x91); // ANSI Extended Symbol Segment
            ms.WriteByte((byte)len);
            ms.Write(asciiBytes, 0, len);

            if ((len % 2) != 0)
            {
                // Pad byte to ensure word boundary
                ms.WriteByte(0x00);
            }
        }

        private static void WriteElementSegment(MemoryStream ms, uint index)
        {
            if (index < 256)
            {
                // 8-bit element segment: 0x28, <index>
                ms.WriteByte(0x28);
                ms.WriteByte((byte)index);
            }
            else if (index <= 65535)
            {
                // 16-bit element segment: 0x29, 0x00, <index_lo>, <index_hi>
                ms.WriteByte(0x29);
                ms.WriteByte(0x00);
                ms.WriteByte((byte)(index & 0xFF));
                ms.WriteByte((byte)((index >> 8) & 0xFF));
            }
            else
            {
                // 32-bit element segment: 0x2A, 0x00, <index_32le>
                ms.WriteByte(0x2A);
                ms.WriteByte(0x00);
                ms.WriteByte((byte)(index & 0xFF));
                ms.WriteByte((byte)((index >> 8) & 0xFF));
                ms.WriteByte((byte)((index >> 16) & 0xFF));
                ms.WriteByte((byte)((index >> 24) & 0xFF));
            }
        }

        #endregion

        #region Read Tag Service (0x4C)

        /// <summary>
        /// Builds an EtherNet/IP SendRRData frame for CIP Read Tag Service (0x4C).
        /// </summary>
        public static byte[] BuildReadTagRequest(
            uint sessionHandle,
            string tagName,
            ushort elementsCount = 1,
            byte slot = 0,
            bool useRouting = false)
        {
            byte[] tagPath = EncodeTagPath(tagName, out byte pathWords);

            // Construct inner CIP message for Service 0x4C
            // Service(1B) + PathSize(1B) + Path(NB) + ElementsCount(2B)
            int innerCipLen = 2 + tagPath.Length + 2;
            byte[] innerCip = new byte[innerCipLen];
            innerCip[0] = (byte)CipService.ReadTag;
            innerCip[1] = pathWords;
            Buffer.BlockCopy(tagPath, 0, innerCip, 2, tagPath.Length);
            int elemOffset = 2 + tagPath.Length;
            innerCip[elemOffset] = (byte)(elementsCount & 0xFF);
            innerCip[elemOffset + 1] = (byte)((elementsCount >> 8) & 0xFF);

            return WrapSendRRData(sessionHandle, innerCip, slot, useRouting);
        }

        #endregion

        #region Write Tag Service (0x4D)

        /// <summary>
        /// Builds an EtherNet/IP SendRRData frame for CIP Write Tag Service (0x4D).
        /// </summary>
        public static byte[] BuildWriteTagRequest(
            uint sessionHandle,
            string tagName,
            CipDataType dataType,
            ReadOnlySpan<byte> data,
            ushort elementsCount = 1,
            byte slot = 0,
            bool useRouting = false)
        {
            byte[] tagPath = EncodeTagPath(tagName, out byte pathWords);

            // Construct inner CIP message for Service 0x4D
            // Service(1B) + PathSize(1B) + Path(NB) + DataType(2B) + ElementsCount(2B) + Data(MB)
            int innerCipLen = 2 + tagPath.Length + 2 + 2 + data.Length;
            byte[] innerCip = new byte[innerCipLen];
            innerCip[0] = (byte)CipService.WriteTag;
            innerCip[1] = pathWords;
            Buffer.BlockCopy(tagPath, 0, innerCip, 2, tagPath.Length);

            int offset = 2 + tagPath.Length;
            // Data Type (ushort LE)
            innerCip[offset] = (byte)((ushort)dataType & 0xFF);
            innerCip[offset + 1] = (byte)(((ushort)dataType >> 8) & 0xFF);
            offset += 2;

            // Elements Count (ushort LE)
            innerCip[offset] = (byte)(elementsCount & 0xFF);
            innerCip[offset + 1] = (byte)((elementsCount >> 8) & 0xFF);
            offset += 2;

            // Raw data payload
            data.CopyTo(new Span<byte>(innerCip, offset, data.Length));

            return WrapSendRRData(sessionHandle, innerCip, slot, useRouting);
        }

        #endregion

        #region SendRRData Encapsulation Wrapper

        private static byte[] WrapSendRRData(uint sessionHandle, byte[] cipMessage, byte slot, bool useRouting)
        {
            byte[] effectiveCip;

            if (useRouting || slot > 0)
            {
                // Wrap in Unconnected Send (Service 0x52) directed to Connection Manager (Class 0x06, Instance 0x01)
                // Connection Manager Request Path: 0x20, 0x06, 0x24, 0x01 (4 bytes = 2 words)
                // Route Path: Port 1 (Backplane), Slot (e.g., [0x01, slot] = 2 bytes = 1 word)
                int padByte = (cipMessage.Length % 2 != 0) ? 1 : 0;
                int routeLen = 2; // Port 1, slot
                int cmPayloadLen = 1 + 1 + 2 + cipMessage.Length + padByte + 1 + 1 + routeLen;
                int cmMessageLen = 1 + 1 + 4 + cmPayloadLen;

                effectiveCip = new byte[cmMessageLen];
                effectiveCip[0] = 0x52; // Unconnected Send Service
                effectiveCip[1] = 0x02; // Request Path Size in words (Class 0x06, Inst 0x01 = 4B = 2 words)
                effectiveCip[2] = 0x20; // Class segment (8-bit)
                effectiveCip[3] = 0x06; // Connection Manager
                effectiveCip[4] = 0x24; // Instance segment (8-bit)
                effectiveCip[5] = 0x01; // Instance 1

                effectiveCip[6] = 0x0A; // Priority / Time_Tick (10ms)
                effectiveCip[7] = 0x0E; // Timeout_Ticks (14 * 10ms = 140ms timeout multiplier)

                // Message Request Size (ushort LE)
                effectiveCip[8] = (byte)(cipMessage.Length & 0xFF);
                effectiveCip[9] = (byte)((cipMessage.Length >> 8) & 0xFF);

                // Copy inner CIP message
                Buffer.BlockCopy(cipMessage, 0, effectiveCip, 10, cipMessage.Length);
                int routeIdx = 10 + cipMessage.Length;
                if (padByte > 0)
                {
                    effectiveCip[routeIdx++] = 0x00; // Pad byte
                }

                // Route Path Size in words
                effectiveCip[routeIdx++] = (byte)(routeLen / 2);
                effectiveCip[routeIdx++] = 0x00; // Reserved
                // Route Path: Port 1 (Backplane), Slot
                effectiveCip[routeIdx++] = 0x01;
                effectiveCip[routeIdx++] = slot;
            }
            else
            {
                // Direct message to controller message router
                effectiveCip = cipMessage;
            }

            // Total Encapsulation Payload Length = Interface Handle (4B) + Timeout (2B) + Item Count (2B)
            // + Null Address Item (4B) + Data Item Header (4B) + CIP Message Length
            ushort encapPayloadLen = (ushort)(4 + 2 + 2 + 4 + 4 + effectiveCip.Length);
            byte[] frame = new byte[EncapsulationHeaderLength + encapPayloadLen];

            // 1. Encapsulation Header (24 bytes)
            frame[0] = 0x6F; // SendRRData command (0x006F)
            frame[1] = 0x00;
            frame[2] = (byte)(encapPayloadLen & 0xFF);
            frame[3] = (byte)((encapPayloadLen >> 8) & 0xFF);
            frame[4] = (byte)(sessionHandle & 0xFF);
            frame[5] = (byte)((sessionHandle >> 8) & 0xFF);
            frame[6] = (byte)((sessionHandle >> 16) & 0xFF);
            frame[7] = (byte)((sessionHandle >> 24) & 0xFF);
            // Status = 0
            // Sender Context = 0
            // Options = 0

            // 2. Encapsulation Data
            int off = EncapsulationHeaderLength;
            // Interface Handle (4B) = 0x00000000 (CIP)
            off += 4;
            // Timeout (2B LE) = 10 seconds
            frame[off++] = 0x0A;
            frame[off++] = 0x00;
            // Item Count (2B LE) = 2
            frame[off++] = 0x02;
            frame[off++] = 0x00;

            // Item 1: Null Address Item (Type 0x0000, Length 0x0000)
            off += 4;

            // Item 2: Unconnected Data Item (Type 0x00B2, Length)
            frame[off++] = 0xB2;
            frame[off++] = 0x00;
            frame[off++] = (byte)(effectiveCip.Length & 0xFF);
            frame[off++] = (byte)((effectiveCip.Length >> 8) & 0xFF);

            // Copy CIP Payload
            Buffer.BlockCopy(effectiveCip, 0, frame, off, effectiveCip.Length);

            return frame;
        }

        #endregion

        #region Response Extraction

        /// <summary>
        /// Parses an EtherNet/IP SendRRData response frame and extracts the CIP payload or throws a <see cref="CipException"/>.
        /// </summary>
        public static ReadOnlyMemory<byte> ParseCipResponse(
            ReadOnlyMemory<byte> frameMemory,
            CipService expectedService,
            out CipDataType detectedType)
        {
            detectedType = CipDataType.Struct;
            ReadOnlySpan<byte> frame = frameMemory.Span;

            if (frame.Length < EncapsulationHeaderLength)
                throw new CipException("Received EtherNet/IP frame is too short.");

            // Verify Encapsulation Status
            uint encapStatus = (uint)(frame[8] | (frame[9] << 8) | (frame[10] << 16) | (frame[11] << 24));
            if (encapStatus != 0)
                throw new CipException(encapStatus);

            ushort command = (ushort)(frame[0] | (frame[1] << 8));
            if (command != (ushort)CipCommand.SendRRData && command != (ushort)CipCommand.SendUnitData)
                throw new CipException($"Unexpected EtherNet/IP command in response: 0x{command:X4}.");

            // Offset 30: Item Count (ushort LE)
            if (frame.Length < 32)
                throw new CipException("EtherNet/IP response payload truncated before Common Packet Format items.");

            ushort itemCount = (ushort)(frame[30] | (frame[31] << 8));
            int curr = 32;

            int cipDataOffset = -1;
            int cipDataLength = 0;

            for (int i = 0; i < itemCount; i++)
            {
                if (curr + 4 > frame.Length)
                    throw new CipException("Malformed Common Packet Format item header in response.");

                ushort itemTypeId = (ushort)(frame[curr] | (frame[curr + 1] << 8));
                ushort itemLength = (ushort)(frame[curr + 2] | (frame[curr + 3] << 8));
                curr += 4;

                if (curr + itemLength > frame.Length)
                    throw new CipException("Common Packet Format item length extends beyond frame bounds.");

                if (itemTypeId == 0x00B2 || itemTypeId == 0x00B1) // Unconnected or Connected Data Item
                {
                    cipDataOffset = curr;
                    cipDataLength = itemLength;
                    break;
                }

                curr += itemLength;
            }

            if (cipDataOffset < 0 || cipDataLength < 4)
                throw new CipException("No CIP Data Item found in EtherNet/IP response.");

            ReadOnlySpan<byte> cipSpan = frame.Slice(cipDataOffset, cipDataLength);

            // Handle Unconnected Send wrapper response (Service 0xD2)
            if (cipSpan[0] == 0xD2)
            {
                byte cmGeneralStatus = cipSpan[2];
                if (cmGeneralStatus != 0)
                {
                    byte cmAddCount = cipSpan[3];
                    ushort[] cmAddStatus = ExtractAdditionalStatus(cipSpan, 4, cmAddCount);
                    throw new CipException(cmGeneralStatus, cmAddStatus, "Unconnected Send routing failed");
                }

                // Inner response starts after CM status words
                byte cmAddWords = cipSpan[3];
                int innerOffset = 4 + (cmAddWords * 2);
                if (innerOffset >= cipDataLength)
                    throw new CipException("Unconnected Send response does not contain encapsulated inner CIP message.");

                cipSpan = cipSpan.Slice(innerOffset);
                cipDataOffset += innerOffset;
                cipDataLength -= innerOffset;
            }

            // Inspect service response code: should be ExpectedService | 0x80
            byte expectedRespCode = (byte)((byte)expectedService | 0x80);
            byte actualRespCode = cipSpan[0];
            if (actualRespCode != expectedRespCode)
            {
                throw new CipException($"CIP response service mismatch: expected 0x{expectedRespCode:X2}, received 0x{actualRespCode:X2}.");
            }

            // Byte 2: General Status
            byte generalStatus = cipSpan[2];
            if (generalStatus != 0)
            {
                byte addCount = cipSpan[3];
                ushort[] addStatus = ExtractAdditionalStatus(cipSpan, 4, addCount);
                throw new CipException(generalStatus, addStatus, $"Service: {expectedService}");
            }

            // Service Succeeded
            byte additionalWords = cipSpan[3];
            int payloadStart = 4 + (additionalWords * 2);

            if (expectedService == CipService.ReadTag)
            {
                if (payloadStart + 2 > cipSpan.Length)
                    throw new CipException("ReadTag response does not contain data type specifier.");

                // Read Tag returns 2-byte DataType, then raw values
                ushort typeCode = (ushort)(cipSpan[payloadStart] | (cipSpan[payloadStart + 1] << 8));
                detectedType = (CipDataType)typeCode;

                int actualPayloadOffset = cipDataOffset + payloadStart + 2;
                int actualPayloadLength = cipDataLength - (payloadStart + 2);

                return frameMemory.Slice(actualPayloadOffset, actualPayloadLength);
            }

            // For WriteTag or other services, payload is empty or auxiliary
            return ReadOnlyMemory<byte>.Empty;
        }

        private static ushort[] ExtractAdditionalStatus(ReadOnlySpan<byte> span, int offset, byte wordCount)
        {
            if (wordCount == 0 || offset + (wordCount * 2) > span.Length)
                return Array.Empty<ushort>();

            ushort[] result = new ushort[wordCount];
            for (int i = 0; i < wordCount; i++)
            {
                int pos = offset + (i * 2);
                result[i] = (ushort)(span[pos] | (span[pos + 1] << 8));
            }
            return result;
        }

        #endregion
    }
}
