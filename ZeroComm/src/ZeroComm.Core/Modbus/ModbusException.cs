using System;

namespace ZeroComm.Core.Modbus
{
    /// <summary>
    /// Exception thrown when a Modbus slave returns an error response frame.
    /// </summary>
    public class ModbusException : Exception
    {
        /// <summary>
        /// Gets the function code of the failed request.
        /// </summary>
        public byte FunctionCode { get; }

        /// <summary>
        /// Gets the standard Modbus exception code reported by the slave.
        /// </summary>
        public ModbusExceptionCode ExceptionCode { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ModbusException"/> class.
        /// </summary>
        public ModbusException(byte functionCode, ModbusExceptionCode exceptionCode)
            : base($"Modbus slave returned exception code {exceptionCode} (0x{(byte)exceptionCode:X2}) for function code 0x{functionCode:X2}.")
        {
            FunctionCode = functionCode;
            ExceptionCode = exceptionCode;
        }
    }
}
