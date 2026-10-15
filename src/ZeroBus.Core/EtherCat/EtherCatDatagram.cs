using System;

namespace ZeroBus.EtherCat
{
    public static class EtherCatConstants
    {
        public const ushort EtherType = 0x88A4;

        // EtherCAT Command Types
        public const byte CmdNop = 0x00;
        public const byte CmdAprd = 0x01; // Auto Increment Read
        public const byte CmdApwr = 0x02; // Auto Increment Write
        public const byte CmdAprw = 0x03; // Auto Increment Read Write
        public const byte CmdFprd = 0x04; // Configured Address Read
        public const byte CmdFpwr = 0x05; // Configured Address Write
        public const byte CmdFprw = 0x06; // Configured Address Read Write
        public const byte CmdBrd = 0x07;  // Broadcast Read
        public const byte CmdBwr = 0x08;  // Broadcast Write
        public const byte CmdBrw = 0x09;  // Broadcast Read Write
        public const byte CmdLrd = 0x0A;  // Logical Read
        public const byte CmdLwr = 0x0B;  // Logical Write
        public const byte CmdLrw = 0x0C;  // Logical Read Write
        public const byte CmdArmw = 0x0D; // Auto Increment Multiple Read Write
        public const byte CmdFrmw = 0x0E; // Configured Address Multiple Read Write

        // Standard Registers
        public const ushort RegType = 0x0000;
        public const ushort RegRevision = 0x0001;
        public const ushort RegBuild = 0x0002;
        public const ushort RegFmmusSupported = 0x0004;
        public const ushort RegSyncManagersSupported = 0x0005;
        public const ushort RegConfiguredStationAddress = 0x0010;
        public const ushort RegConfiguredStationAlias = 0x0012;
        public const ushort RegAlControl = 0x0120;
        public const ushort RegAlStatus = 0x0130;
        public const ushort RegAlStatusCode = 0x0134;

        // ESM States
        public const byte EsmInit = 0x01;
        public const byte EsmPreOp = 0x02;
        public const byte EsmBoot = 0x03;
        public const byte EsmSafeOp = 0x04;
        public const byte EsmOp = 0x08;
        public const byte EsmAck = 0x10;
    }

    public enum EtherCatState : byte
    {
        None = 0x00,
        Init = 0x01,
        PreOp = 0x02,
        Boot = 0x03,
        SafeOp = 0x04,
        Op = 0x08
    }

    /// <summary>
    /// Represents an individual EtherCAT datagram within an EtherCAT frame.
    /// </summary>
    public class EtherCatDatagram
    {
        public byte Command { get; set; }
        public byte Index { get; set; }
        public uint Address { get; set; }
        public byte[] Data { get; set; } = Array.Empty<byte>();
        public ushort WorkingCounter { get; set; }
        public bool MoreDatagrams { get; set; }

        public EtherCatDatagram() { }

        public EtherCatDatagram(byte command, byte index, uint address, byte[] data)
        {
            Command = command;
            Index = index;
            Address = address;
            Data = data ?? Array.Empty<byte>();
        }

        public int Serialize(Span<byte> buffer)
        {
            int len = Data.Length;
            if (buffer.Length < 10 + len + 2)
            {
                throw new ArgumentException("Buffer too small for EtherCAT datagram.");
            }

            buffer[0] = Command;
            buffer[1] = Index;
            buffer[2] = (byte)(Address & 0xFF);
            buffer[3] = (byte)((Address >> 8) & 0xFF);
            buffer[4] = (byte)((Address >> 16) & 0xFF);
            buffer[5] = (byte)((Address >> 24) & 0xFF);

            // Length: 11 bits, Reserved: 3 bits, Circulating: 1 bit, More: 1 bit
            ushort lenField = (ushort)(len & 0x07FF);
            if (MoreDatagrams) lenField |= 0x8000;

            buffer[6] = (byte)(lenField & 0xFF);
            buffer[7] = (byte)((lenField >> 8) & 0xFF);

            // Interrupt: 2 bytes
            buffer[8] = 0;
            buffer[9] = 0;

            // Payload
            Data.CopyTo(buffer.Slice(10, len));

            // Working counter (2 bytes, filled by slaves)
            int wkcOffset = 10 + len;
            buffer[wkcOffset] = (byte)(WorkingCounter & 0xFF);
            buffer[wkcOffset + 1] = (byte)((WorkingCounter >> 8) & 0xFF);

            return 10 + len + 2;
        }

        public static EtherCatDatagram Deserialize(ReadOnlySpan<byte> buffer, out int bytesConsumed)
        {
            if (buffer.Length < 12) throw new ArgumentException("Buffer too small to deserialize EtherCAT datagram.");

            byte cmd = buffer[0];
            byte idx = buffer[1];
            uint addr = BitConverter.ToUInt32(buffer.Slice(2, 4).ToArray(), 0);

            ushort lenField = BitConverter.ToUInt16(buffer.Slice(6, 2).ToArray(), 0);
            int len = lenField & 0x07FF;
            bool more = (lenField & 0x8000) != 0;

            if (buffer.Length < 10 + len + 2)
            {
                throw new ArgumentException("Buffer truncated during EtherCAT datagram payload read.");
            }

            byte[] data = buffer.Slice(10, len).ToArray();
            ushort wkc = BitConverter.ToUInt16(buffer.Slice(10 + len, 2).ToArray(), 0);

            bytesConsumed = 10 + len + 2;
            return new EtherCatDatagram(cmd, idx, addr, data)
            {
                WorkingCounter = wkc,
                MoreDatagrams = more
            };
        }
    }
}
