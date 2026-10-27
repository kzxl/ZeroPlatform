using System;

namespace ZeroComm.Core.Omron
{
    /// <summary>
    /// Omron PLC memory area codes for FINS protocol frames.
    /// Supports both Bit and Word units.
    /// </summary>
    public enum FinsMemoryArea : byte
    {
        CIO_Bit = 0x30,
        WR_Bit = 0x31,
        HR_Bit = 0x32,
        AR_Bit = 0x33,
        DM_Bit = 0x02,
        CIO_Word = 0xB0,
        WR_Word = 0xB1,
        HR_Word = 0xB2,
        AR_Word = 0xB3,
        DM_Word = 0x82,
        EM0_Bit = 0x20,
        EM0_Word = 0x50,
        EM1_Bit = 0x21,
        EM1_Word = 0x51
    }

    /// <summary>
    /// High-performance builder and parser for Omron FINS (Factory Interface Network Service) communication frames.
    /// Supports both raw FINS frame payloads and 16-byte FINS TCP encapsulation.
    /// </summary>
    public static class FinsFrame
    {
        public const ushort MemoryAreaReadCommand = 0x0101;
        public const ushort MemoryAreaWriteCommand = 0x0102;

        public const uint TcpCommandNodeRequest = 0x00000000;
        public const uint TcpCommandNodeResponse = 0x00000001;
        public const uint TcpCommandDataSend = 0x00000002;
        public const uint TcpCommandErrorNotification = 0x00000003;

        #region FINS TCP Encapsulation

        /// <summary>
        /// Builds a 20-byte FINS/TCP Client Node Address Allocation Request (Command 0x00000000).
        /// </summary>
        public static byte[] BuildTcpNodeAllocationRequest(byte clientNode = 0)
        {
            byte[] packet = new byte[20];
            // Magic 'FINS'
            packet[0] = 0x46;
            packet[1] = 0x49;
            packet[2] = 0x4E;
            packet[3] = 0x53;

            // Length (12 bytes follow)
            packet[4] = 0x00;
            packet[5] = 0x00;
            packet[6] = 0x00;
            packet[7] = 0x0C;

            // Command (0x00000000)
            packet[8] = 0x00;
            packet[9] = 0x00;
            packet[10] = 0x00;
            packet[11] = 0x00;

            // Error code (0x00000000)
            packet[12] = 0x00;
            packet[13] = 0x00;
            packet[14] = 0x00;
            packet[15] = 0x00;

            // Client Node request (0 = auto-allocate)
            packet[16] = 0x00;
            packet[17] = 0x00;
            packet[18] = 0x00;
            packet[19] = clientNode;

            return packet;
        }

        /// <summary>
        /// Parses a 24-byte FINS/TCP Node Address Allocation Response (Command 0x00000001).
        /// </summary>
        public static bool ParseTcpNodeAllocationResponse(
            byte[] frame,
            out byte clientNode,
            out byte serverNode)
        {
            clientNode = 0;
            serverNode = 0;

            if (frame == null || frame.Length < 24)
                return false;

            // Verify Magic 'FINS'
            if (frame[0] != 0x46 || frame[1] != 0x49 || frame[2] != 0x4E || frame[3] != 0x53)
                return false;

            // Verify Command (0x00000001)
            uint cmd = ((uint)frame[8] << 24) | ((uint)frame[9] << 16) | ((uint)frame[10] << 8) | frame[11];
            if (cmd != TcpCommandNodeResponse)
                return false;

            // Verify Error code
            uint err = ((uint)frame[12] << 24) | ((uint)frame[13] << 16) | ((uint)frame[14] << 8) | frame[15];
            if (err != 0)
                return false;

            clientNode = frame[19];
            serverNode = frame[23];
            return true;
        }

        /// <summary>
        /// Wraps a standard FINS frame into a 16-byte FINS/TCP Data Send packet (Command 0x00000002).
        /// </summary>
        public static byte[] WrapInTcpHeader(byte[] finsPayload)
        {
            if (finsPayload == null) throw new ArgumentNullException(nameof(finsPayload));

            uint lengthFollowing = (uint)(8 + finsPayload.Length);
            byte[] packet = new byte[16 + finsPayload.Length];

            // Magic 'FINS'
            packet[0] = 0x46;
            packet[1] = 0x49;
            packet[2] = 0x4E;
            packet[3] = 0x53;

            // Length
            packet[4] = (byte)(lengthFollowing >> 24);
            packet[5] = (byte)(lengthFollowing >> 16);
            packet[6] = (byte)(lengthFollowing >> 8);
            packet[7] = (byte)(lengthFollowing & 0xFF);

            // Command: 0x00000002 (Data Send)
            packet[8] = 0x00;
            packet[9] = 0x00;
            packet[10] = 0x00;
            packet[11] = 0x02;

            // Error code: 0x00000000
            packet[12] = 0x00;
            packet[13] = 0x00;
            packet[14] = 0x00;
            packet[15] = 0x00;

            // FINS Payload
            Array.Copy(finsPayload, 0, packet, 16, finsPayload.Length);
            return packet;
        }

        /// <summary>
        /// Strips the 16-byte FINS/TCP header and returns the inner FINS frame payload.
        /// </summary>
        public static bool UnwrapTcpHeader(byte[] tcpPacket, out byte[] finsPayload)
        {
            finsPayload = Array.Empty<byte>();
            if (tcpPacket == null || tcpPacket.Length < 16)
                return false;

            if (tcpPacket[0] != 0x46 || tcpPacket[1] != 0x49 || tcpPacket[2] != 0x4E || tcpPacket[3] != 0x53)
                return false;

            uint err = ((uint)tcpPacket[12] << 24) | ((uint)tcpPacket[13] << 16) | ((uint)tcpPacket[14] << 8) | tcpPacket[15];
            if (err != 0)
                return false;

            int payloadLen = tcpPacket.Length - 16;
            finsPayload = new byte[payloadLen];
            Array.Copy(tcpPacket, 16, finsPayload, 0, payloadLen);
            return true;
        }

        #endregion

        #region FINS Memory Read / Write Frame Builders

        /// <summary>
        /// Builds a FINS frame for Memory Area Read (Word or Bit units).
        /// </summary>
        public static byte[] BuildMemoryAreaReadRequest(
            FinsMemoryArea memoryArea,
            ushort address,
            ushort numberOfItems,
            byte bitOffset = 0,
            byte destNode = 0x01,
            byte srcNode = 0xEF,
            byte serviceId = 0x01)
        {
            byte[] frame = new byte[18];

            // FINS Header (10 bytes)
            frame[0] = 0x80; // ICF: Command requiring response
            frame[1] = 0x00; // RSV
            frame[2] = 0x02; // GCT: Max gateway count
            frame[3] = 0x00; // DNA: Dest Network
            frame[4] = destNode; // DA1: Dest Node
            frame[5] = 0x00; // DA2: Dest Unit (CPU)
            frame[6] = 0x00; // SNA: Src Network
            frame[7] = srcNode;  // SA1: Src Node
            frame[8] = 0x00; // SA2: Src Unit
            frame[9] = serviceId; // SID

            // Command: 0x0101 (Memory Area Read)
            frame[10] = 0x01; // MRC
            frame[11] = 0x01; // SRC

            // Parameters
            frame[12] = (byte)memoryArea;
            frame[13] = (byte)(address >> 8);   // Big-endian address
            frame[14] = (byte)(address & 0xFF);
            frame[15] = bitOffset;              // Sub-address (bit offset 0..15)
            frame[16] = (byte)(numberOfItems >> 8);
            frame[17] = (byte)(numberOfItems & 0xFF);

            return frame;
        }

        /// <summary>
        /// Builds a FINS frame for Memory Area Write (Word units).
        /// </summary>
        public static byte[] BuildMemoryAreaWriteRequest(
            FinsMemoryArea memoryArea,
            ushort address,
            ushort[] values,
            byte destNode = 0x01,
            byte srcNode = 0xEF,
            byte serviceId = 0x01)
        {
            if (values == null || values.Length == 0)
                throw new ArgumentException("Values cannot be empty.", nameof(values));

            ushort numberOfItems = (ushort)values.Length;
            int frameLength = 18 + numberOfItems * 2;
            byte[] frame = new byte[frameLength];

            // FINS Header
            frame[0] = 0x80;
            frame[1] = 0x00;
            frame[2] = 0x02;
            frame[3] = 0x00;
            frame[4] = destNode;
            frame[5] = 0x00;
            frame[6] = 0x00;
            frame[7] = srcNode;
            frame[8] = 0x00;
            frame[9] = serviceId;

            // Command: 0x0102 (Memory Area Write)
            frame[10] = 0x01;
            frame[11] = 0x02;

            // Parameters
            frame[12] = (byte)memoryArea;
            frame[13] = (byte)(address >> 8);
            frame[14] = (byte)(address & 0xFF);
            frame[15] = 0x00; // Bit offset 0 for word write
            frame[16] = (byte)(numberOfItems >> 8);
            frame[17] = (byte)(numberOfItems & 0xFF);

            // Data words (big-endian)
            for (int i = 0; i < values.Length; i++)
            {
                frame[18 + i * 2] = (byte)(values[i] >> 8);
                frame[19 + i * 2] = (byte)(values[i] & 0xFF);
            }

            return frame;
        }

        /// <summary>
        /// Builds a FINS frame for Memory Area Write (Single Bit).
        /// </summary>
        public static byte[] BuildMemoryAreaWriteBitRequest(
            FinsMemoryArea memoryArea,
            ushort address,
            byte bitOffset,
            bool value,
            byte destNode = 0x01,
            byte srcNode = 0xEF,
            byte serviceId = 0x01)
        {
            byte[] frame = new byte[19];

            // FINS Header
            frame[0] = 0x80;
            frame[1] = 0x00;
            frame[2] = 0x02;
            frame[3] = 0x00;
            frame[4] = destNode;
            frame[5] = 0x00;
            frame[6] = 0x00;
            frame[7] = srcNode;
            frame[8] = 0x00;
            frame[9] = serviceId;

            // Command: 0x0102 (Memory Area Write)
            frame[10] = 0x01;
            frame[11] = 0x02;

            // Parameters
            frame[12] = (byte)memoryArea;
            frame[13] = (byte)(address >> 8);
            frame[14] = (byte)(address & 0xFF);
            frame[15] = bitOffset;
            frame[16] = 0x00;
            frame[17] = 0x01; // 1 bit

            // Value byte (0x01 for true, 0x00 for false)
            frame[18] = (byte)(value ? 1 : 0);

            return frame;
        }

        /// <summary>
        /// Parses a FINS response frame. Returns true if valid and EndCode == 0x0000.
        /// </summary>
        public static bool ParseResponse(
            byte[] frame,
            int offset,
            int count,
            out byte serviceId,
            out ushort endCode,
            out byte[] data)
        {
            serviceId = 0;
            endCode = 0xFFFF;
            data = Array.Empty<byte>();

            // Minimum response: Header 10B + Command 2B + EndCode 2B = 14 bytes
            if (frame == null || count < 14 || offset + count > frame.Length)
                return false;

            serviceId = frame[offset + 9];
            byte mrc = frame[offset + 10];
            byte src = frame[offset + 11];

            // EndCode: Main response code & Sub response code
            byte mainEndCode = frame[offset + 12];
            byte subEndCode = frame[offset + 13];
            endCode = (ushort)((mainEndCode << 8) | subEndCode);

            int dataLength = count - 14;
            if (dataLength > 0)
            {
                data = new byte[dataLength];
                Array.Copy(frame, offset + 14, data, 0, dataLength);
            }

            return endCode == 0x0000;
        }

        /// <summary>
        /// Converts raw big-endian data bytes from a Word response into an array of ushort.
        /// </summary>
        public static ushort[] ToWords(byte[] data)
        {
            if (data == null || data.Length < 2) return Array.Empty<ushort>();
            int wordCount = data.Length / 2;
            ushort[] words = new ushort[wordCount];
            for (int i = 0; i < wordCount; i++)
            {
                words[i] = (ushort)((data[i * 2] << 8) | data[i * 2 + 1]);
            }
            return words;
        }

        #endregion
    }
}
