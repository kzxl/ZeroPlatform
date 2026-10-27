using System;

namespace ZeroBus.EtherCat
{
    public static class EtherCatCoeConstants
    {
        public const byte MailboxTypeCoe = 0x03; // CANopen over EtherCAT

        // CoE Services
        public const ushort CoeServiceEmergency = 0x01;
        public const ushort CoeServiceSdoRequest = 0x02;
        public const ushort CoeServiceSdoResponse = 0x03;
        public const ushort CoeServiceTxPdo = 0x04;
        public const ushort CoeServiceRxPdo = 0x05;

        // SDO Command Specifiers (CS)
        public const byte SdoCsDownloadExpedited4B = 0x23; // Write 4 bytes
        public const byte SdoCsDownloadExpedited2B = 0x2B; // Write 2 bytes
        public const byte SdoCsDownloadExpedited1B = 0x2F; // Write 1 byte
        public const byte SdoCsUploadRequest = 0x40;       // Read request
        public const byte SdoCsDownloadResponse = 0x60;    // Write ACK
        public const byte SdoCsAbort = 0x80;               // Error
    }

    /// <summary>
    /// CANopen-over-EtherCAT (CoE) Mailbox Frame Builder and Parser.
    /// Encapsulates CiA 402 drive parameter configuration over high-speed EtherCAT mailbox transfers.
    /// </summary>
    public static class EtherCatCoeMailbox
    {
        /// <summary>
        /// Builds an EtherCAT CoE SDO Download (Write) frame.
        /// </summary>
        public static byte[] BuildSdoDownload(ushort index, byte subIndex, byte[] value, ushort slaveAddress = 0, byte mailboxCounter = 0)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            if (value.Length > 4) throw new ArgumentException("Expedited SDO download supports up to 4 bytes payload.");

            byte cs = value.Length switch
            {
                1 => EtherCatCoeConstants.SdoCsDownloadExpedited1B,
                2 => EtherCatCoeConstants.SdoCsDownloadExpedited2B,
                _ => EtherCatCoeConstants.SdoCsDownloadExpedited4B
            };

            int coeDataLen = 2 + 8; // 2 bytes CoE header + 8 bytes SDO frame
            byte[] frame = new byte[6 + coeDataLen];

            // 1. Mailbox Header (6 bytes)
            frame[0] = (byte)(coeDataLen & 0xFF);
            frame[1] = (byte)((coeDataLen >> 8) & 0xFF);
            frame[2] = (byte)(slaveAddress & 0xFF);
            frame[3] = (byte)((slaveAddress >> 8) & 0xFF);
            frame[4] = 0x00; // Channel 0, Priority 0
            frame[5] = (byte)((EtherCatCoeConstants.MailboxTypeCoe & 0x0F) | ((mailboxCounter & 0x07) << 4));

            // 2. CoE Header (2 bytes: service = 0x02 for SDO Request)
            ushort coeHeader = (ushort)(EtherCatCoeConstants.CoeServiceSdoRequest << 12);
            frame[6] = (byte)(coeHeader & 0xFF);
            frame[7] = (byte)((coeHeader >> 8) & 0xFF);

            // 3. SDO Frame (8 bytes)
            frame[8] = cs;
            frame[9] = (byte)(index & 0xFF);
            frame[10] = (byte)((index >> 8) & 0xFF);
            frame[11] = subIndex;
            for (int i = 0; i < value.Length; i++)
            {
                frame[12 + i] = value[i];
            }

            return frame;
        }

        /// <summary>
        /// Builds an EtherCAT CoE SDO Upload (Read) frame.
        /// </summary>
        public static byte[] BuildSdoUpload(ushort index, byte subIndex, ushort slaveAddress = 0, byte mailboxCounter = 0)
        {
            int coeDataLen = 2 + 8;
            byte[] frame = new byte[6 + coeDataLen];

            // 1. Mailbox Header
            frame[0] = (byte)(coeDataLen & 0xFF);
            frame[1] = (byte)((coeDataLen >> 8) & 0xFF);
            frame[2] = (byte)(slaveAddress & 0xFF);
            frame[3] = (byte)((slaveAddress >> 8) & 0xFF);
            frame[4] = 0x00;
            frame[5] = (byte)((EtherCatCoeConstants.MailboxTypeCoe & 0x0F) | ((mailboxCounter & 0x07) << 4));

            // 2. CoE Header
            ushort coeHeader = (ushort)(EtherCatCoeConstants.CoeServiceSdoRequest << 12);
            frame[6] = (byte)(coeHeader & 0xFF);
            frame[7] = (byte)((coeHeader >> 8) & 0xFF);

            // 3. SDO Upload Request
            frame[8] = EtherCatCoeConstants.SdoCsUploadRequest;
            frame[9] = (byte)(index & 0xFF);
            frame[10] = (byte)((index >> 8) & 0xFF);
            frame[11] = subIndex;

            return frame;
        }

        /// <summary>
        /// Parses an EtherCAT CoE SDO Response frame.
        /// </summary>
        public static bool TryParseSdoResponse(
            byte[] frame,
            out byte sdoCommand,
            out ushort index,
            out byte subIndex,
            out byte[] payload)
        {
            sdoCommand = 0;
            index = 0;
            subIndex = 0;
            payload = Array.Empty<byte>();

            if (frame == null || frame.Length < 16) return false;

            byte mboxType = (byte)(frame[5] & 0x0F);
            if (mboxType != EtherCatCoeConstants.MailboxTypeCoe) return false;

            ushort coeHeader = BitConverter.ToUInt16(frame, 6);
            ushort service = (ushort)((coeHeader >> 12) & 0x0F);
            if (service != EtherCatCoeConstants.CoeServiceSdoResponse) return false;

            sdoCommand = frame[8];
            index = BitConverter.ToUInt16(frame, 9);
            subIndex = frame[11];

            payload = new byte[4];
            Array.Copy(frame, 12, payload, 0, 4);
            return true;
        }
    }
}
