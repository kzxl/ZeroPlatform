namespace ZeroComm.Core.Abstractions
{
    /// <summary>
    /// Supported industrial PLC vendors and fieldbus protocols.
    /// </summary>
    public enum PlcVendor
    {
        SiemensS7,
        MitsubishiMelsec,
        ModbusTcp,
        ModbusRtu,
        OmronFins,
        AllenBradleyCip
    }

    /// <summary>
    /// Connection state lifecycle for industrial PLC drivers.
    /// </summary>
    public enum PlcConnectionState
    {
        Disconnected,
        Connecting,
        Handshaking,
        Connected,
        Reconnecting,
        Faulted
    }

    /// <summary>
    /// Primitive and composite data types supported by PLC memory registers.
    /// </summary>
    public enum TagDataType
    {
        Byte,
        Bool,
        Int16,
        UInt16,
        Int32,
        UInt32,
        Int64,
        UInt64,
        Float,
        Double,
        String,
        RawBytes
    }

    /// <summary>
    /// SCADA quality codes conforming to OPC DA / ISA-88 telemetry standards.
    /// </summary>
    public enum ScadaQuality : byte
    {
        Good = 192,
        Uncertain = 64,
        Bad = 0,
        DeviceFailure = 12,
        CommFailure = 24
    }
}
