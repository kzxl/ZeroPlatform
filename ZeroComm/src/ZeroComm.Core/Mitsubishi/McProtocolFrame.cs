using System;

namespace ZeroComm.Core.Mitsubishi
{
    /// <summary>
    /// Mitsubishi MELSEC device codes for MC Protocol 3E binary frames.
    /// </summary>
    public enum McDeviceCode : byte
    {
        D = 0xA8,  // Data Register
        W = 0xB4,  // Link Register
        M = 0x90,  // Internal Relay
        X = 0x9C,  // Input Relay
        Y = 0x9D,  // Output Relay
        L = 0x92,  // Latch Relay
        F = 0x93,  // Annunciator
        V = 0x94,  // Edge Relay
        B = 0xA0,  // Link Relay
        ZR = 0xB0, // File Register (Consecutive)
        R = 0xAF   // File Register
    }

    /// <summary>
    /// High-performance builder and parser for Mitsubishi MC Protocol 3E Binary communication frames.
    /// Zero external dependencies, supports Q/L/iQ-R and FX5U series PLCs.
    /// </summary>
    public static class McProtocolFrame
    {
        public const ushort BatchReadCommand = 0x0401;
        public const ushort BatchWriteCommand = 0x1401;

        /// <summary>
        /// Builds a 3E Binary frame for Batch Read of Word devices (e.g., D100 to D110).
        /// </summary>
        public static byte[] BuildBatchReadWordsRequest(
            McDeviceCode deviceCode,
            int headDeviceNumber,
            ushort devicePoints,
            byte networkNo = 0x00,
            byte pcNo = 0xFF,
            ushort destModuleIo = 0x03FF,
            byte destModuleStation = 0x00,
            ushort monitoringTimer = 0x0010)
        {
            return BuildBatchReadRequest(deviceCode, headDeviceNumber, devicePoints, isBitUnit: false,
                networkNo, pcNo, destModuleIo, destModuleStation, monitoringTimer);
        }

        /// <summary>
        /// Builds a 3E Binary frame for Batch Read of Bit devices (e.g., M100 to M116).
        /// </summary>
        public static byte[] BuildBatchReadBitsRequest(
            McDeviceCode deviceCode,
            int headDeviceNumber,
            ushort devicePoints,
            byte networkNo = 0x00,
            byte pcNo = 0xFF,
            ushort destModuleIo = 0x03FF,
            byte destModuleStation = 0x00,
            ushort monitoringTimer = 0x0010)
        {
            return BuildBatchReadRequest(deviceCode, headDeviceNumber, devicePoints, isBitUnit: true,
                networkNo, pcNo, destModuleIo, destModuleStation, monitoringTimer);
        }

        private static byte[] BuildBatchReadRequest(
            McDeviceCode deviceCode,
            int headDeviceNumber,
            ushort devicePoints,
            bool isBitUnit,
            byte networkNo,
            byte pcNo,
            ushort destModuleIo,
            byte destModuleStation,
            ushort monitoringTimer)
        {
            byte[] frame = new byte[21];

            // Subheader: 0x50, 0x00
            frame[0] = 0x50;
            frame[1] = 0x00;

            // Route
            frame[2] = networkNo;
            frame[3] = pcNo;
            frame[4] = (byte)(destModuleIo & 0xFF);
            frame[5] = (byte)((destModuleIo >> 8) & 0xFF);
            frame[6] = destModuleStation;

            // Request Data Length = 12 bytes (Timer 2B + Command 2B + Subcommand 2B + Head 3B + Code 1B + Points 2B)
            ushort reqDataLength = 12;
            frame[7] = (byte)(reqDataLength & 0xFF);
            frame[8] = (byte)((reqDataLength >> 8) & 0xFF);

            // Monitoring Timer (250ms units)
            frame[9] = (byte)(monitoringTimer & 0xFF);
            frame[10] = (byte)((monitoringTimer >> 8) & 0xFF);

            // Command = 0x0401 (Batch Read)
            frame[11] = 0x01;
            frame[12] = 0x04;

            // Subcommand: 0x0000 (Word), 0x0001 (Bit)
            frame[13] = (byte)(isBitUnit ? 0x01 : 0x00);
            frame[14] = 0x00;

            // Head Device Number (3 bytes little-endian)
            frame[15] = (byte)(headDeviceNumber & 0xFF);
            frame[16] = (byte)((headDeviceNumber >> 8) & 0xFF);
            frame[17] = (byte)((headDeviceNumber >> 16) & 0xFF);

            // Device Code
            frame[18] = (byte)deviceCode;

            // Device Points (2 bytes little-endian)
            frame[19] = (byte)(devicePoints & 0xFF);
            frame[20] = (byte)((devicePoints >> 8) & 0xFF);

            return frame;
        }

        /// <summary>
        /// Builds a 3E Binary frame for Batch Write of Word devices (e.g., D100).
        /// </summary>
        public static byte[] BuildBatchWriteWordsRequest(
            McDeviceCode deviceCode,
            int headDeviceNumber,
            ushort[] values,
            byte networkNo = 0x00,
            byte pcNo = 0xFF,
            ushort destModuleIo = 0x03FF,
            byte destModuleStation = 0x00,
            ushort monitoringTimer = 0x0010)
        {
            if (values == null || values.Length == 0)
                throw new ArgumentException("Values cannot be empty.", nameof(values));

            ushort devicePoints = (ushort)values.Length;
            int payloadLength = 12 + values.Length * 2;
            byte[] frame = new byte[9 + payloadLength];

            // Subheader: 0x50, 0x00
            frame[0] = 0x50;
            frame[1] = 0x00;

            // Route
            frame[2] = networkNo;
            frame[3] = pcNo;
            frame[4] = (byte)(destModuleIo & 0xFF);
            frame[5] = (byte)((destModuleIo >> 8) & 0xFF);
            frame[6] = destModuleStation;

            // Request Data Length
            ushort reqDataLength = (ushort)payloadLength;
            frame[7] = (byte)(reqDataLength & 0xFF);
            frame[8] = (byte)((reqDataLength >> 8) & 0xFF);

            // Monitoring Timer
            frame[9] = (byte)(monitoringTimer & 0xFF);
            frame[10] = (byte)((monitoringTimer >> 8) & 0xFF);

            // Command = 0x1401 (Batch Write)
            frame[11] = 0x01;
            frame[12] = 0x14;

            // Subcommand: 0x0000 (Word)
            frame[13] = 0x00;
            frame[14] = 0x00;

            // Head Device Number
            frame[15] = (byte)(headDeviceNumber & 0xFF);
            frame[16] = (byte)((headDeviceNumber >> 8) & 0xFF);
            frame[17] = (byte)((headDeviceNumber >> 16) & 0xFF);

            // Device Code
            frame[18] = (byte)deviceCode;

            // Device Points
            frame[19] = (byte)(devicePoints & 0xFF);
            frame[20] = (byte)((devicePoints >> 8) & 0xFF);

            // Data words
            for (int i = 0; i < values.Length; i++)
            {
                frame[21 + i * 2] = (byte)(values[i] & 0xFF);
                frame[22 + i * 2] = (byte)((values[i] >> 8) & 0xFF);
            }

            return frame;
        }

        /// <summary>
        /// Parses a 3E Binary response frame. Returns true if valid and EndCode == 0.
        /// </summary>
        public static bool ParseResponse(
            byte[] frame,
            int offset,
            int count,
            out ushort endCode,
            out byte[] data)
        {
            endCode = 0xFFFF;
            data = Array.Empty<byte>();

            // Minimum 3E response length: 11 bytes (Subheader 2 + Route 5 + Length 2 + EndCode 2)
            if (frame == null || count < 11 || offset + count > frame.Length)
                return false;

            // Check subheader: 0xD0, 0x00
            if (frame[offset] != 0xD0 || frame[offset + 1] != 0x00)
                return false;

            ushort respLength = (ushort)(frame[offset + 7] | (frame[offset + 8] << 8));
            endCode = (ushort)(frame[offset + 9] | (frame[offset + 10] << 8));

            if (count < 9 + respLength)
                return false;

            int dataLength = respLength - 2; // Subtract 2 bytes of EndCode
            if (dataLength > 0)
            {
                data = new byte[dataLength];
                Array.Copy(frame, offset + 11, data, 0, dataLength);
            }

            return endCode == 0x0000;
        }

        /// <summary>
        /// Converts raw little-endian data bytes from a Word response into an array of ushort.
        /// </summary>
        public static ushort[] ToWords(byte[] data)
        {
            if (data == null || data.Length < 2) return Array.Empty<ushort>();
            int wordCount = data.Length / 2;
            ushort[] words = new ushort[wordCount];
            for (int i = 0; i < wordCount; i++)
            {
                words[i] = (ushort)(data[i * 2] | (data[i * 2 + 1] << 8));
            }
            return words;
        }
    }
}
