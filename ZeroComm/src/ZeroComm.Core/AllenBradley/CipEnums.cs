namespace ZeroComm.Core.AllenBradley
{
    /// <summary>
    /// EtherNet/IP encapsulation commands.
    /// </summary>
    public enum CipCommand : ushort
    {
        Nop = 0x0000,
        ListServices = 0x0004,
        ListIdentity = 0x0063,
        ListInterfaces = 0x0064,
        RegisterSession = 0x0065,
        UnregisterSession = 0x0066,
        SendRRData = 0x006F,
        SendUnitData = 0x0070
    }

    /// <summary>
    /// Common Industrial Protocol (CIP) service codes for Logix PLCs.
    /// </summary>
    public enum CipService : byte
    {
        GetAttributeAll = 0x01,
        SetAttributeAll = 0x02,
        GetAttributeList = 0x03,
        SetAttributeList = 0x04,
        Reset = 0x05,
        Start = 0x06,
        Stop = 0x07,
        Create = 0x08,
        Delete = 0x09,
        MultipleServicePacket = 0x0A,
        ApplyAttributes = 0x0D,
        GetAttributeSingle = 0x0E,
        SetAttributeSingle = 0x10,
        FindNextObject = 0x11,
        ReadTag = 0x4C,
        WriteTag = 0x4D,
        ReadTagFragmented = 0x52,
        WriteTagFragmented = 0x53,
        ForwardClose = 0x4E,
        UnconnectedSend = 0x52,
        ForwardOpen = 0x54,
        LargeForwardOpen = 0x5B
    }

    /// <summary>
    /// Common Industrial Protocol (CIP) elemental and structural data types.
    /// </summary>
    public enum CipDataType : ushort
    {
        Bool = 0x00C1,
        SByte = 0x00C2,     // SINT (8-bit signed integer)
        Int16 = 0x00C3,     // INT (16-bit signed integer)
        Int32 = 0x00C4,     // DINT (32-bit signed integer)
        Int64 = 0x00C5,     // LINT (64-bit signed integer)
        Byte = 0x00C6,      // USINT (8-bit unsigned integer)
        UInt16 = 0x00C7,    // UINT (16-bit unsigned integer)
        UInt32 = 0x00C8,    // UDINT (32-bit unsigned integer)
        UInt64 = 0x00C9,    // ULINT (64-bit unsigned integer)
        Single = 0x00CA,    // REAL (32-bit IEEE 754 float)
        Double = 0x00CB,    // LREAL (64-bit IEEE 754 double)
        Struct = 0x02A0,    // General structure
        String = 0x0FCE     // Standard Logix STRING structure
    }

    /// <summary>
    /// Common Industrial Protocol (CIP) General Status Codes.
    /// </summary>
    public enum CipGeneralStatus : byte
    {
        Success = 0x00,
        ConnectionFailure = 0x01,
        ResourceUnavailable = 0x02,
        InvalidParameterValue = 0x03,
        PathSegmentError = 0x04,
        PathDestinationUnknown = 0x05,
        PartialTransfer = 0x06,
        ConnectionLost = 0x07,
        ServiceNotSupported = 0x08,
        InvalidAttributeValue = 0x09,
        AttributeListError = 0x0A,
        AlreadyInRequestedMode = 0x0B,
        ObjectStateConflict = 0x0C,
        ObjectAlreadyExists = 0x0D,
        AttributeNotSettable = 0x0E,
        PrivilegeViolation = 0x0F,
        DeviceStateConflict = 0x10,
        ReplyDataTooLarge = 0x11,
        FragmentationPrerequisite = 0x12,
        NotEnoughData = 0x13,
        AttributeNotSupported = 0x14,
        TooMuchData = 0x15,
        KeyFailure = 0x1E,
        PathSizeInvalid = 0x26
    }
}
