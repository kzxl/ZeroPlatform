using System;

namespace ZeroComm.Core.Mitsubishi
{
    /// <summary>
    /// Exception thrown when a Mitsubishi PLC returns a non-zero EndCode in an MC Protocol 3E frame.
    /// </summary>
    public class McProtocolException : Exception
    {
        /// <summary>
        /// Gets the PLC error EndCode (e.g., 0xC050, 0xC051, etc.).
        /// </summary>
        public ushort EndCode { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="McProtocolException"/> class.
        /// </summary>
        public McProtocolException(ushort endCode)
            : base($"Mitsubishi MC Protocol PLC reported an error EndCode 0x{endCode:X4}.")
        {
            EndCode = endCode;
        }
    }
}
