namespace ZeroComm.Core.Modbus
{
    /// <summary>
    /// Standard Modbus protocol function codes.
    /// </summary>
    public enum ModbusFunctionCode : byte
    {
        ReadCoils = 0x01,
        ReadDiscreteInputs = 0x02,
        ReadHoldingRegisters = 0x03,
        ReadInputRegisters = 0x04,
        WriteSingleCoil = 0x05,
        WriteSingleRegister = 0x06,
        WriteMultipleCoils = 0x0F,
        WriteMultipleRegisters = 0x10,
        MaskWriteRegister = 0x16,
        ReadWriteMultipleRegisters = 0x17
    }

    /// <summary>
    /// Standard Modbus protocol exception codes.
    /// </summary>
    public enum ModbusExceptionCode : byte
    {
        None = 0x00,
        IllegalFunction = 0x01,
        IllegalDataAddress = 0x02,
        IllegalDataValue = 0x03,
        SlaveDeviceFailure = 0x04,
        Acknowledge = 0x05,
        SlaveDeviceBusy = 0x06,
        NegativeAcknowledge = 0x07,
        MemoryParityError = 0x08,
        GatewayPathUnavailable = 0x0A,
        GatewayTargetDeviceFailedToRespond = 0x0B
    }
}
