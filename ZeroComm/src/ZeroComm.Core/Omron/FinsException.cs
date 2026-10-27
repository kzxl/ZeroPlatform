using System;

namespace ZeroComm.Core.Omron
{
    /// <summary>
    /// Exception thrown when an Omron FINS communication or command execution error occurs.
    /// </summary>
    public class FinsException : Exception
    {
        /// <summary>
        /// Gets the 16-bit FINS response EndCode (Main Response Code [MRES] and Sub Response Code [SRES]).
        /// </summary>
        public ushort EndCode { get; }

        public byte MainResponseCode => (byte)(EndCode >> 8);
        public byte SubResponseCode => (byte)(EndCode & 0xFF);

        public FinsException(ushort endCode)
            : base(FormatMessage(endCode))
        {
            EndCode = endCode;
        }

        public FinsException(string message) : base(message)
        {
            EndCode = 0xFFFF;
        }

        public FinsException(string message, Exception innerException) : base(message, innerException)
        {
            EndCode = 0xFFFF;
        }

        private static string FormatMessage(ushort endCode)
        {
            byte mres = (byte)(endCode >> 8);
            byte sres = (byte)(endCode & 0xFF);

            string description = (mres, sres) switch
            {
                (0x00, 0x00) => "Normal completion",
                (0x01, 0x01) => "Local node not in network",
                (0x02, 0x01) => "Destination node not in network",
                (0x03, 0x01) => "CPU Unit error",
                (0x04, 0x01) => "Unsupported service / command code not supported",
                (0x10, 0x01) => "Command too long",
                (0x10, 0x02) => "Command too short",
                (0x11, 0x01) => "Variable area code error / unsupported memory area",
                (0x11, 0x03) => "Address range exceeded",
                (0x11, 0x04) => "Address format error",
                (0x21, 0x01) => "Read-only memory area (write protected)",
                _ => $"FINS Error MRES:0x{mres:X2}, SRES:0x{sres:X2}"
            };

            return $"Omron FINS error (EndCode: 0x{endCode:X4}): {description}.";
        }
    }
}
