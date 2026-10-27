using System;
using System.Collections.Generic;

namespace ZeroComm.Core.Siemens
{
    /// <summary>
    /// Item descriptor for S7 Read / Write requests.
    /// </summary>
    public struct S7VariableAddress
    {
        public S7Area Area { get; set; }
        public ushort DbNumber { get; set; }
        public int StartByte { get; set; }
        public int BitIndex { get; set; }
        public ushort Count { get; set; }
        public bool IsBit { get; set; }

        public S7VariableAddress(S7Area area, ushort dbNumber, int startByte, ushort count, bool isBit = false, int bitIndex = 0)
        {
            Area = area;
            DbNumber = dbNumber;
            StartByte = startByte;
            Count = count;
            IsBit = isBit;
            BitIndex = bitIndex;
        }

        public static S7VariableAddress ForDbBytes(ushort dbNumber, int startByte, ushort count)
            => new S7VariableAddress(S7Area.DB, dbNumber, startByte, count, isBit: false);

        public static S7VariableAddress ForDbBit(ushort dbNumber, int startByte, int bitIndex)
            => new S7VariableAddress(S7Area.DB, dbNumber, startByte, 1, isBit: true, bitIndex: bitIndex);

        public static S7VariableAddress ForMerkers(int startByte, ushort count)
            => new S7VariableAddress(S7Area.Merkers, 0, startByte, count, isBit: false);

        public static S7VariableAddress ForInputs(int startByte, ushort count)
            => new S7VariableAddress(S7Area.Inputs, 0, startByte, count, isBit: false);

        public static S7VariableAddress ForOutputs(int startByte, ushort count)
            => new S7VariableAddress(S7Area.Outputs, 0, startByte, count, isBit: false);
    }

    /// <summary>
    /// Pure C# frame encoder and decoder for Siemens S7 ISO-on-TCP (RFC 1006 / ISO 8073 COTP) protocol.
    /// Handles Connection Request, Connection Confirm, Setup Communication, Read Variable, and Write Variable.
    /// </summary>
    public static class S7Frame
    {
        public const int DefaultPort = 102;
        public const ushort DefaultPduLength = 480;

        #region COTP Connection Handshake

        /// <summary>
        /// Builds an RFC 1006 COTP Connection Request (CR) packet.
        /// </summary>
        public static byte[] BuildConnectionRequest(int rack = 0, int slot = 1, byte connectionType = 0x03)
        {
            // TPKT (4 bytes) + COTP (18 bytes) = 22 bytes total
            byte[] packet = new byte[22];

            // 1. TPKT Header
            packet[0] = 0x03; // RFC 1006 Version
            packet[1] = 0x00; // Reserved
            packet[2] = 0x00; // Length High
            packet[3] = 0x16; // Length Low = 22 bytes

            // 2. COTP Header
            packet[4] = 0x11; // COTP Header Length (17 bytes)
            packet[5] = 0xE0; // PDU Type: Connection Request (CR)
            packet[6] = 0x00; // Dst Ref High
            packet[7] = 0x00; // Dst Ref Low
            packet[8] = 0x00; // Src Ref High
            packet[9] = 0x01; // Src Ref Low
            packet[10] = 0x00; // Class 0

            // Parameter 1: Calling TSAP (Src) = 0x0100
            packet[11] = 0xC1; // Code
            packet[12] = 0x02; // Length
            packet[13] = 0x01; // Source TSAP High
            packet[14] = 0x00; // Source TSAP Low

            // Parameter 2: Called TSAP (Dst) = connectionType and (rack * 0x20 + slot)
            packet[15] = 0xC2; // Code
            packet[16] = 0x02; // Length
            packet[17] = connectionType; // 0x01 = PG, 0x02 = OP, 0x03 = Basic S7
            packet[18] = (byte)((rack * 0x20) + slot);

            // Parameter 3: TPDU Size (1024 bytes = 0x0A)
            packet[19] = 0xC0; // Code
            packet[20] = 0x01; // Length
            packet[21] = 0x0A; // 2^10 = 1024 bytes

            return packet;
        }

        /// <summary>
        /// Validates a COTP Connection Confirm (CC) response packet.
        /// </summary>
        public static bool ValidateConnectionConfirm(byte[] response)
        {
            if (response == null || response.Length < 7)
                return false;

            // Validate TPKT version 0x03
            if (response[0] != 0x03 || response[1] != 0x00)
                return false;

            // Validate COTP PDU Type 0xD0 (Connection Confirm)
            return response[5] == 0xD0;
        }

        #endregion

        #region S7 Communication Setup

        /// <summary>
        /// Builds S7 Communication Setup packet to negotiate maximum PDU length.
        /// </summary>
        public static byte[] BuildSetupCommunication(ushort sequenceNumber, ushort maxPduLength = DefaultPduLength)
        {
            // TPKT (4) + COTP DT (3) + S7 Header (10) + S7 Parameter (8) = 25 bytes
            byte[] packet = new byte[25];

            // 1. TPKT Header
            packet[0] = 0x03;
            packet[1] = 0x00;
            packet[2] = 0x00;
            packet[3] = 0x19; // 25 bytes total

            // 2. COTP DT Header
            packet[4] = 0x02; // Length
            packet[5] = 0xF0; // PDU Type: Data Transfer (DT)
            packet[6] = 0x80; // EOT & TPDU number 0

            // 3. S7 PDU Header (10 bytes)
            packet[7] = 0x32; // Protocol ID
            packet[8] = 0x01; // ROSCTR: Job
            packet[9] = 0x00; // Redundancy High
            packet[10] = 0x00; // Redundancy Low
            packet[11] = (byte)(sequenceNumber >> 8);
            packet[12] = (byte)(sequenceNumber & 0xFF);
            packet[13] = 0x00; // Parameter Length High
            packet[14] = 0x08; // Parameter Length Low = 8 bytes
            packet[15] = 0x00; // Data Length High
            packet[16] = 0x00; // Data Length Low = 0 bytes

            // 4. S7 Setup Parameter (8 bytes)
            packet[17] = 0xF0; // Function: Setup Communication
            packet[18] = 0x00; // Reserved
            packet[19] = 0x00; // Max AMQ Calling High
            packet[20] = 0x01; // Max AMQ Calling Low (1)
            packet[21] = 0x00; // Max AMQ Called High
            packet[22] = 0x01; // Max AMQ Called Low (1)
            packet[23] = (byte)(maxPduLength >> 8);
            packet[24] = (byte)(maxPduLength & 0xFF);

            return packet;
        }

        /// <summary>
        /// Parses the S7 Communication Setup response and returns the negotiated PDU length.
        /// </summary>
        public static ushort ParseSetupCommunicationResponse(byte[] response)
        {
            ValidateS7ResponseHeader(response, expectedFunction: 0xF0);

            // S7 Parameter starts at offset 19 (TPKT 4 + COTP 3 + S7 Header 12)
            // Negotiated PDU length is at parameter offset 6-7 (overall packet offset 25-26)
            if (response.Length < 27)
                throw new S7Exception("S7 Setup Communication response is truncated.");

            ushort negotiatedPdu = (ushort)((response[25] << 8) | response[26]);
            return negotiatedPdu > 0 ? negotiatedPdu : DefaultPduLength;
        }

        #endregion

        #region S7 Read Variable

        /// <summary>
        /// Builds an S7 Read Variable request for one or more memory areas.
        /// </summary>
        public static byte[] BuildReadRequest(ushort sequenceNumber, IReadOnlyList<S7VariableAddress> variables)
        {
            if (variables == null || variables.Count == 0)
                throw new ArgumentException("At least one variable must be specified.", nameof(variables));

            int itemCount = variables.Count;
            int paramLength = 2 + (12 * itemCount);
            int totalLength = 4 + 3 + 10 + paramLength; // TPKT(4) + COTP(3) + S7Header(10) + Param

            byte[] packet = new byte[totalLength];

            // 1. TPKT Header
            packet[0] = 0x03;
            packet[1] = 0x00;
            packet[2] = (byte)(totalLength >> 8);
            packet[3] = (byte)(totalLength & 0xFF);

            // 2. COTP DT Header
            packet[4] = 0x02;
            packet[5] = 0xF0;
            packet[6] = 0x80;

            // 3. S7 Header
            packet[7] = 0x32; // Protocol ID
            packet[8] = 0x01; // ROSCTR: Job
            packet[9] = 0x00;
            packet[10] = 0x00;
            packet[11] = (byte)(sequenceNumber >> 8);
            packet[12] = (byte)(sequenceNumber & 0xFF);
            packet[13] = (byte)(paramLength >> 8);
            packet[14] = (byte)(paramLength & 0xFF);
            packet[15] = 0x00; // Data Length High
            packet[16] = 0x00; // Data Length Low

            // 4. S7 Parameter: Function 0x04 (Read Var)
            packet[17] = 0x04;
            packet[18] = (byte)itemCount;

            int offset = 19;
            for (int i = 0; i < itemCount; i++)
            {
                var v = variables[i];
                packet[offset++] = 0x12; // Variable Spec
                packet[offset++] = 0x0A; // Length of address spec (10 bytes)
                packet[offset++] = 0x10; // Syntax ID: S7Any

                if (v.IsBit)
                {
                    packet[offset++] = (byte)S7WordLength.Bit;
                    packet[offset++] = 0x00;
                    packet[offset++] = 0x01; // 1 bit
                }
                else
                {
                    packet[offset++] = (byte)S7WordLength.Byte;
                    packet[offset++] = (byte)(v.Count >> 8);
                    packet[offset++] = (byte)(v.Count & 0xFF);
                }

                // DB Number
                packet[offset++] = (byte)(v.DbNumber >> 8);
                packet[offset++] = (byte)(v.DbNumber & 0xFF);

                // Area
                packet[offset++] = (byte)v.Area;

                // 24-bit bit address: (startByte * 8) + bitIndex
                int bitAddress = (v.StartByte * 8) + (v.IsBit ? v.BitIndex : 0);
                packet[offset++] = (byte)((bitAddress >> 16) & 0xFF);
                packet[offset++] = (byte)((bitAddress >> 8) & 0xFF);
                packet[offset++] = (byte)(bitAddress & 0xFF);
            }

            return packet;
        }

        /// <summary>
        /// Parses S7 Read Variable response and extracts byte payload for each requested item.
        /// </summary>
        public static List<byte[]> ParseReadResponse(byte[] response, int expectedItems = 1)
        {
            ValidateS7ResponseHeader(response, expectedFunction: 0x04);

            // S7 Header is 12 bytes for AckData (offset 7 to 18).
            // Parameter starts at offset 19: Function(1) + ItemCount(1) = 2 bytes.
            // Data section starts at offset 21.
            int offset = 21;
            var resultList = new List<byte[]>(expectedItems);

            for (int i = 0; i < expectedItems; i++)
            {
                if (offset + 4 > response.Length)
                    throw new S7Exception("S7 Read response is truncated in data header.");

                byte returnCode = response[offset++];
                byte transportSize = response[offset++];
                ushort rawBitLength = (ushort)((response[offset++] << 8) | response[offset++]);

                if (returnCode != 0xFF)
                {
                    throw new S7Exception($"S7 read failed for item {i}", 0x81, returnCode);
                }

                int byteCount = (transportSize == 0x03 || transportSize == 0x04)
                    ? (rawBitLength + 7) / 8 // Length in bits, convert to bytes
                    : rawBitLength;

                if (offset + byteCount > response.Length)
                    throw new S7Exception("S7 Read response truncated in payload.");

                byte[] itemData = new byte[byteCount];
                Array.Copy(response, offset, itemData, 0, byteCount);
                resultList.Add(itemData);

                offset += byteCount;
                // If odd length in multi-item response, align to 2 bytes
                if (byteCount % 2 != 0 && offset < response.Length && i < expectedItems - 1)
                {
                    offset++;
                }
            }

            return resultList;
        }

        #endregion

        #region S7 Write Variable

        /// <summary>
        /// Builds S7 Write Variable request.
        /// </summary>
        public static byte[] BuildWriteRequest(ushort sequenceNumber, S7VariableAddress variable, byte[] data)
        {
            if (data == null || data.Length == 0)
                throw new ArgumentException("Data to write cannot be null or empty.", nameof(data));

            int paramLength = 2 + 12; // 1 item = 14 bytes
            int dataLength = 4 + data.Length; // ReturnCode(1) + Size(1) + Len(2) + Payload
            int totalLength = 4 + 3 + 10 + paramLength + dataLength;

            byte[] packet = new byte[totalLength];

            // 1. TPKT Header
            packet[0] = 0x03;
            packet[1] = 0x00;
            packet[2] = (byte)(totalLength >> 8);
            packet[3] = (byte)(totalLength & 0xFF);

            // 2. COTP DT
            packet[4] = 0x02;
            packet[5] = 0xF0;
            packet[6] = 0x80;

            // 3. S7 Header
            packet[7] = 0x32;
            packet[8] = 0x01; // Job
            packet[9] = 0x00;
            packet[10] = 0x00;
            packet[11] = (byte)(sequenceNumber >> 8);
            packet[12] = (byte)(sequenceNumber & 0xFF);
            packet[13] = (byte)(paramLength >> 8);
            packet[14] = (byte)(paramLength & 0xFF);
            packet[15] = (byte)(dataLength >> 8);
            packet[16] = (byte)(dataLength & 0xFF);

            // 4. S7 Parameter: Function 0x05 (Write Var)
            packet[17] = 0x05;
            packet[18] = 0x01; // 1 item

            // Address Spec
            packet[19] = 0x12;
            packet[20] = 0x0A;
            packet[21] = 0x10; // S7Any

            if (variable.IsBit)
            {
                packet[22] = (byte)S7WordLength.Bit;
                packet[23] = 0x00;
                packet[24] = 0x01;
            }
            else
            {
                packet[22] = (byte)S7WordLength.Byte;
                packet[23] = (byte)(data.Length >> 8);
                packet[24] = (byte)(data.Length & 0xFF);
            }

            packet[25] = (byte)(variable.DbNumber >> 8);
            packet[26] = (byte)(variable.DbNumber & 0xFF);
            packet[27] = (byte)variable.Area;

            int bitAddress = (variable.StartByte * 8) + (variable.IsBit ? variable.BitIndex : 0);
            packet[28] = (byte)((bitAddress >> 16) & 0xFF);
            packet[29] = (byte)((bitAddress >> 8) & 0xFF);
            packet[30] = (byte)(bitAddress & 0xFF);

            // 5. S7 Data section
            packet[31] = 0x00; // Reserved
            packet[32] = variable.IsBit ? (byte)0x03 : (byte)0x04; // 0x03=BIT, 0x04=BYTE

            ushort bitLen = variable.IsBit ? (ushort)1 : (ushort)(data.Length * 8);
            packet[33] = (byte)(bitLen >> 8);
            packet[34] = (byte)(bitLen & 0xFF);

            Array.Copy(data, 0, packet, 35, data.Length);

            return packet;
        }

        /// <summary>
        /// Parses S7 Write Variable response and verifies return code.
        /// </summary>
        public static void ParseWriteResponse(byte[] response)
        {
            ValidateS7ResponseHeader(response, expectedFunction: 0x05);

            // Data section starts at offset 21
            if (response.Length < 22)
                throw new S7Exception("S7 Write response is truncated.");

            byte returnCode = response[21];
            if (returnCode != 0xFF)
            {
                throw new S7Exception("S7 Write failed", 0x82, returnCode);
            }
        }

        #endregion

        #region Private Validation Helpers

        private static void ValidateS7ResponseHeader(byte[] response, byte expectedFunction)
        {
            if (response == null || response.Length < 21)
                throw new S7Exception("Response packet is too small to be a valid S7 frame.");

            // TPKT Version
            if (response[0] != 0x03)
                throw new S7Exception("Invalid TPKT Version.");

            // COTP PDU Type 0xF0 (Data Transfer)
            if (response[5] != 0xF0)
                throw new S7Exception($"Expected COTP DT (0xF0), but received 0x{response[5]:X2}.");

            // S7 Protocol ID 0x32
            if (response[7] != 0x32)
                throw new S7Exception($"Expected S7 Protocol ID 0x32, but received 0x{response[7]:X2}.");

            // S7 ROSCTR: 0x03 (AckData) or 0x02 (Ack)
            byte rosctr = response[8];
            if (rosctr != 0x03 && rosctr != 0x02)
                throw new S7Exception($"Unexpected S7 ROSCTR type: 0x{rosctr:X2}.");

            // S7 Error Class and Error Code at offsets 17-18
            byte errorClass = response[17];
            ushort errorCode = (ushort)((response[17] << 8) | response[18]);
            if (errorClass != 0x00 || response[18] != 0x00)
            {
                throw new S7Exception("S7 CPU returned an error", errorClass, errorCode);
            }

            // Function code verification at offset 19
            if (response[19] != expectedFunction)
            {
                throw new S7Exception($"Expected function 0x{expectedFunction:X2}, but received 0x{response[19]:X2}.");
            }
        }

        #endregion
    }
}
