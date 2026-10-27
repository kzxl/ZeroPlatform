namespace ZeroComm.Core.Siemens
{
    /// <summary>
    /// Siemens S7 Memory Area Codes (ISO 1006 / S7comm).
    /// </summary>
    public enum S7Area : byte
    {
        /// <summary>System Info</summary>
        SystemInfo = 0x03,

        /// <summary>System Flags</summary>
        SystemFlags = 0x05,

        /// <summary>Analog Inputs</summary>
        AnalogInputs = 0x06,

        /// <summary>Analog Outputs</summary>
        AnalogOutputs = 0x07,

        /// <summary>Counter area (C)</summary>
        Counter = 0x1C,

        /// <summary>Timer area (T)</summary>
        Timer = 0x1D,

        /// <summary>Direct Peripheral Access</summary>
        DirectPeripheralAccess = 0x80,

        /// <summary>Process Inputs (I / PE)</summary>
        Inputs = 0x81,

        /// <summary>Process Outputs (Q / PA)</summary>
        Outputs = 0x82,

        /// <summary>Bit Memory / Merkers / Flags (M)</summary>
        Merkers = 0x83,

        /// <summary>Data Block (DB)</summary>
        DB = 0x84,

        /// <summary>Instance Data Block (DI)</summary>
        InstanceDB = 0x85,

        /// <summary>Local Data (L)</summary>
        LocalData = 0x86,

        /// <summary>Previous Local Data (V)</summary>
        PreviousLocalData = 0x87
    }

    /// <summary>
    /// Siemens S7 Transport / Word Size codes in PDU parameter.
    /// </summary>
    public enum S7WordLength : byte
    {
        /// <summary>Bit (1 bit)</summary>
        Bit = 0x01,

        /// <summary>Byte (8 bits)</summary>
        Byte = 0x02,

        /// <summary>Character (8 bits)</summary>
        Char = 0x03,

        /// <summary>Word (16 bits)</summary>
        Word = 0x04,

        /// <summary>Integer (16 bits)</summary>
        Int = 0x05,

        /// <summary>Double Word (32 bits)</summary>
        DWord = 0x06,

        /// <summary>Double Integer (32 bits)</summary>
        DInt = 0x07,

        /// <summary>Real / IEEE 754 Floating Point (32 bits)</summary>
        Real = 0x08,

        /// <summary>Date Time (8 bytes)</summary>
        DateTime = 0x0F
    }
}
