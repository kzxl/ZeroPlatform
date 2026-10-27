namespace ZeroBus.CanOpen
{
    public static class CanOpenConstants
    {
        // Standard Pre-Defined Connection Set COB-IDs (Function Code << 7 | NodeID)
        public const uint CobIdNmt = 0x000;
        public const uint CobIdSync = 0x080;
        public const uint CobIdTime = 0x100;
        public const uint CobIdEmergencyBase = 0x080;
        public const uint CobIdTpdo1Base = 0x180;
        public const uint CobIdRpdo1Base = 0x200;
        public const uint CobIdTpdo2Base = 0x280;
        public const uint CobIdRpdo2Base = 0x300;
        public const uint CobIdTpdo3Base = 0x380;
        public const uint CobIdRpdo3Base = 0x400;
        public const uint CobIdTpdo4Base = 0x480;
        public const uint CobIdRpdo4Base = 0x500;
        public const uint CobIdSdoTxBase = 0x580; // Slave -> Master
        public const uint CobIdSdoRxBase = 0x600; // Master -> Slave
        public const uint CobIdHeartbeatBase = 0x700;

        // NMT Command Specifiers
        public const byte NmtStartNode = 0x01;
        public const byte NmtStopNode = 0x02;
        public const byte NmtEnterPreOp = 0x80;
        public const byte NmtResetNode = 0x81;
        public const byte NmtResetComm = 0x82;

        // SDO Command Specifiers
        public const byte SdoInitiateDownloadExpedited = 0x23; // 4 bytes data
        public const byte SdoInitiateDownloadExpedited2B = 0x2B; // 2 bytes data
        public const byte SdoInitiateDownloadExpedited1B = 0x2F; // 1 byte data
        public const byte SdoInitiateUploadRequest = 0x40;
        public const byte SdoAbort = 0x80;

        // CiA 402 Object Dictionary Indices
        public const ushort IndexControlword = 0x6040;
        public const ushort IndexStatusword = 0x6041;
        public const ushort IndexModesOfOperation = 0x6060;
        public const ushort IndexModesOfOperationDisplay = 0x6061;
        public const ushort IndexPositionActualValue = 0x6064;
        public const ushort IndexVelocityActualValue = 0x606C;
        public const ushort IndexTargetPosition = 0x607A;
        public const ushort IndexProfileVelocity = 0x6081;
        public const ushort IndexProfileAcceleration = 0x6083;
        public const ushort IndexProfileDeceleration = 0x6084;
        public const ushort IndexTargetVelocity = 0x60FF;
    }

    public enum NmtState : byte
    {
        BootUp = 0x00,
        Stopped = 0x04,
        Operational = 0x05,
        PreOperational = 0x7F
    }

    public enum CiA402State
    {
        NotReadyToSwitchOn,
        SwitchOnDisabled,
        ReadyToSwitchOn,
        SwitchedOn,
        OperationEnabled,
        QuickStopActive,
        FaultReactionActive,
        Fault
    }

    public enum CiA402ModeOfOperation : sbyte
    {
        NoMode = 0,
        ProfilePosition = 1,
        Velocity = 2,
        ProfileVelocity = 3,
        ProfileTorque = 4,
        Homing = 6,
        InterpolatedPosition = 7,
        CyclicSynchronousPosition = 8,
        CyclicSynchronousVelocity = 9,
        CyclicSynchronousTorque = 10
    }
}
