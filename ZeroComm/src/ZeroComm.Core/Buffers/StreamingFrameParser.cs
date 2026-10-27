using System;
using ZeroComm.Core.Modbus;

namespace ZeroComm.Core.Buffers
{
    /// <summary>
    /// Fast zero-copy streaming frame extractor operating directly on CircularRingBuffer.
    /// Solves TCP stream fragmentation and packet coalescing without garbage collection pressure.
    /// </summary>
    public static class StreamingFrameParser
    {
        /// <summary>
        /// Attempts to extract a complete Modbus TCP frame from the ring buffer.
        /// Inspects the MBAP Length field to determine exact packet boundary.
        /// </summary>
        public static bool TryExtractModbusTcpFrame(CircularRingBuffer ring, out byte[] frame)
        {
            frame = Array.Empty<byte>();
            if (ring == null || ring.Count < ModbusTcpFrame.MbapHeaderLength)
                return false;

            // Header: TransactionId(2B) + ProtocolId(2B) + Length(2B) + UnitId(1B)
            byte lenHigh = ring.PeekByte(4);
            byte lenLow = ring.PeekByte(5);
            ushort lengthField = (ushort)((lenHigh << 8) | lenLow);

            // Total frame size = MBAP Header(6 bytes before length payload) + LengthField
            int totalExpectedBytes = 6 + lengthField;

            if (ring.Count < totalExpectedBytes)
                return false; // Still waiting for more stream data

            frame = new byte[totalExpectedBytes];
            ring.Read(frame, 0, totalExpectedBytes);
            return true;
        }

        /// <summary>
        /// Attempts to extract a complete Mitsubishi MELSEC MC Protocol 3E Binary response frame.
        /// Inspects the subheader (0xD0, 0x00) and 2-byte response data length field (offset 7).
        /// </summary>
        public static bool TryExtractMcProtocolFrame(CircularRingBuffer ring, out byte[] frame)
        {
            frame = Array.Empty<byte>();
            if (ring == null || ring.Count < 9)
                return false;

            // Validate subheader (0xD0, 0x00)
            if (ring.PeekByte(0) != 0xD0 || ring.PeekByte(1) != 0x00)
                return false;

            byte lenLow = ring.PeekByte(7);
            byte lenHigh = ring.PeekByte(8);
            ushort dataLength = (ushort)(lenLow | (lenHigh << 8));
            int totalExpectedBytes = 9 + dataLength;

            if (ring.Count < totalExpectedBytes)
                return false;

            frame = new byte[totalExpectedBytes];
            ring.Read(frame, 0, totalExpectedBytes);
            return true;
        }

        /// <summary>
        /// Attempts to extract a complete Siemens S7 (ISO-on-TCP / RFC 1006) frame from the ring buffer.
        /// Inspects the 4-byte TPKT header (Version 0x03, Reserved 0x00, Length [2B big-endian]).
        /// </summary>
        public static bool TryExtractS7Frame(CircularRingBuffer ring, out byte[] frame)
        {
            frame = Array.Empty<byte>();
            if (ring == null || ring.Count < 4)
                return false;

            // Check TPKT Version (0x03) and Reserved (0x00)
            if (ring.PeekByte(0) != 0x03 || ring.PeekByte(1) != 0x00)
            {
                // Unaligned to TPKT header, advance 1 byte to seek synchronization
                ring.Advance(1);
                return false;
            }

            ushort totalLength = (ushort)((ring.PeekByte(2) << 8) | ring.PeekByte(3));
            if (totalLength < 4)
            {
                // Invalid length, discard 1 byte
                ring.Advance(1);
                return false;
            }

            if (ring.Count < totalLength)
                return false; // Still waiting for stream data

            frame = new byte[totalLength];
            ring.Read(frame, 0, totalLength);
            return true;
        }

        /// <summary>
        /// Attempts to extract a complete Modbus RTU response frame from the ring buffer.
        /// Inspects function code, byte count, and verifies CRC16 before extraction.
        /// </summary>
        public static bool TryExtractModbusRtuFrame(CircularRingBuffer ring, out byte[] frame)
        {
            frame = Array.Empty<byte>();
            if (ring == null || ring.Count < 5)
                return false;

            byte fc = ring.PeekByte(1);
            int expectedLen = 0;

            if ((fc & 0x80) != 0)
            {
                // Exception response: UnitId(1) + FC(1) + ExcCode(1) + CRC(2) = 5
                expectedLen = 5;
            }
            else if (fc == 0x01 || fc == 0x02 || fc == 0x03 || fc == 0x04)
            {
                // Read response: UnitId(1) + FC(1) + ByteCount(1) + Bytes(N) + CRC(2) = 5 + ByteCount
                if (ring.Count < 3) return false;
                byte byteCount = ring.PeekByte(2);
                expectedLen = 5 + byteCount;
            }
            else if (fc == 0x05 || fc == 0x06 || fc == 0x0F || fc == 0x10)
            {
                // Write response: UnitId(1) + FC(1) + Addr(2) + Value/Qty(2) + CRC(2) = 8
                expectedLen = 8;
            }
            else
            {
                // Unknown function code: not aligned to frame start, advance 1 byte
                ring.Advance(1);
                return false;
            }

            if (ring.Count < expectedLen)
                return false; // Waiting for remaining frame bytes

            var candidate = new byte[expectedLen];
            // Read candidate in one batch without consuming to check CRC
            ring.Peek(candidate, 0, expectedLen);

            if (ModbusRtuFrame.ValidateCrc(candidate, 0, expectedLen))
            {
                // CRC valid! Consume frame from ring
                frame = new byte[expectedLen];
                ring.Read(frame, 0, expectedLen);
                return true;
            }
            else
            {
                // CRC failed: false header alignment, skip 1 byte
                ring.Advance(1);
                return false;
            }
        }

        /// <summary>
        /// Attempts to extract a frame of exact fixed length.
        /// </summary>
        public static bool TryExtractFixedLengthFrame(CircularRingBuffer ring, int frameLength, out byte[] frame)
        {
            frame = Array.Empty<byte>();
            if (ring == null || ring.Count < frameLength)
                return false;

            frame = new byte[frameLength];
            ring.Read(frame, 0, frameLength);
            return true;
        }

        /// <summary>
        /// Attempts to extract a delimiter-terminated frame (e.g. "\r\n" or STX/ETX).
        /// Delimiter is stripped or retained based on keepDelimiter flag.
        /// </summary>
        public static bool TryExtractDelimitedFrame(
            CircularRingBuffer ring,
            byte[] delimiter,
            out byte[] frame,
            bool keepDelimiter = false)
        {
            frame = Array.Empty<byte>();
            if (ring == null || delimiter == null || delimiter.Length == 0)
                return false;

            int index = ring.IndexOfSequence(delimiter);
            if (index < 0)
                return false;

            int payloadLen = keepDelimiter ? (index + delimiter.Length) : index;
            frame = new byte[payloadLen];
            ring.Read(frame, 0, payloadLen);

            if (!keepDelimiter)
            {
                // Discard delimiter bytes
                ring.Advance(delimiter.Length);
            }

            return true;
        }

        /// <summary>
        /// Attempts to extract a complete Omron FINS TCP frame from the ring buffer.
        /// Inspects the 16-byte FINS TCP encapsulation header: ASCII 'FINS' and 4-byte big-endian length.
        /// </summary>
        public static bool TryExtractFinsTcpFrame(CircularRingBuffer ring, out byte[] frame)
        {
            frame = Array.Empty<byte>();
            if (ring == null || ring.Count < 16)
                return false;

            // Check ASCII 'FINS' (0x46, 0x49, 0x4E, 0x53)
            if (ring.PeekByte(0) != 0x46 || ring.PeekByte(1) != 0x49 || ring.PeekByte(2) != 0x4E || ring.PeekByte(3) != 0x53)
            {
                // Not aligned with 'FINS' magic header, advance 1 byte to seek synchronization
                ring.Advance(1);
                return false;
            }

            // Length at offset 4..7: represents length of subsequent bytes (Command 4B + Error 4B + payload)
            uint lengthField = ((uint)ring.PeekByte(4) << 24) |
                               ((uint)ring.PeekByte(5) << 16) |
                               ((uint)ring.PeekByte(6) << 8) |
                               (uint)ring.PeekByte(7);

            if (lengthField < 8 || lengthField > 65535)
            {
                // Invalid length field, advance 1 byte
                ring.Advance(1);
                return false;
            }

            int totalExpectedBytes = (int)(8 + lengthField);
            if (ring.Count < totalExpectedBytes)
                return false;

            frame = new byte[totalExpectedBytes];
            ring.Read(frame, 0, totalExpectedBytes);
            return true;
        }

        /// <summary>
        /// Attempts to extract a complete EtherNet/IP (CIP) encapsulation frame from the ring buffer.
        /// Inspects the 24-byte header: Command(2B), Length(2B little-endian), SessionHandle(4B), Status(4B), SenderContext(8B), Options(4B).
        /// </summary>
        public static bool TryExtractEtherNetIpFrame(CircularRingBuffer ring, out byte[] frame)
        {
            frame = Array.Empty<byte>();
            if (ring == null || ring.Count < 24)
                return false;

            // Length at offset 2..3 (UINT16 little-endian): length of payload following 24-byte header
            byte lenLow = ring.PeekByte(2);
            byte lenHigh = ring.PeekByte(3);
            ushort payloadLength = (ushort)(lenLow | (lenHigh << 8));

            int totalExpectedBytes = 24 + payloadLength;
            if (ring.Count < totalExpectedBytes)
                return false;

            frame = new byte[totalExpectedBytes];
            ring.Read(frame, 0, totalExpectedBytes);
            return true;
        }
    }
}

