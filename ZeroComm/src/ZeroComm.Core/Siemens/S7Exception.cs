using System;

namespace ZeroComm.Core.Siemens
{
    /// <summary>
    /// Exception thrown when a Siemens S7 protocol communication error or CPU rejection occurs.
    /// </summary>
    public class S7Exception : Exception
    {
        /// <summary>
        /// Gets the S7 error code returned by the PLC (0 = success).
        /// </summary>
        public ushort ErrorCode { get; }

        /// <summary>
        /// Gets the S7 error class.
        /// </summary>
        public byte ErrorClass { get; }

        public S7Exception(string message) : base(message)
        {
        }

        public S7Exception(string message, byte errorClass, ushort errorCode)
            : base($"{message} (ErrorClass: 0x{errorClass:X2}, ErrorCode: 0x{errorCode:X4})")
        {
            ErrorClass = errorClass;
            ErrorCode = errorCode;
        }

        public S7Exception(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
