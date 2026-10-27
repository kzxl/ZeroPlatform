using System;
using System.Globalization;

namespace ZeroBus.Can
{
    /// <summary>
    /// Represents a Controller Area Network (CAN 2.0A / 2.0B / CAN FD) data frame.
    /// </summary>
    public struct CanFrame : IEquatable<CanFrame>
    {
        public uint Id;
        public bool IsExtended;
        public bool IsRtr;
        public byte Dlc;
        public byte[] Data;
        public long TimestampUs;

        public CanFrame(uint id, byte[] data, bool isExtended = false, bool isRtr = false, long timestampUs = 0)
        {
            Id = isExtended ? (id & 0x1FFFFFFF) : (id & 0x7FF);
            IsExtended = isExtended;
            IsRtr = isRtr;
            Data = data ?? Array.Empty<byte>();
            Dlc = (byte)System.Math.Min(64, Data.Length);
            TimestampUs = timestampUs;
        }

        public static CanFrame CreateStandard(uint id, byte[] data, bool isRtr = false) =>
            new CanFrame(id, data, isExtended: false, isRtr: isRtr);

        public static CanFrame CreateExtended(uint id, byte[] data, bool isRtr = false) =>
            new CanFrame(id, data, isExtended: true, isRtr: isRtr);

        public static CanFrame CreateRtr(uint id, byte dlc, bool isExtended = false)
        {
            var f = new CanFrame(id, new byte[dlc], isExtended: isExtended, isRtr: true);
            f.Dlc = dlc;
            return f;
        }

        public bool Equals(CanFrame other)
        {
            if (Id != other.Id || IsExtended != other.IsExtended || IsRtr != other.IsRtr || Dlc != other.Dlc)
                return false;

            if (Data.Length != other.Data.Length) return false;
            for (int i = 0; i < Dlc; i++)
            {
                if (Data[i] != other.Data[i]) return false;
            }
            return true;
        }

        public override bool Equals(object? obj) => obj is CanFrame other && Equals(other);
        public override int GetHashCode() => (int)Id ^ (IsExtended ? 0x5555 : 0) ^ Dlc;
        public static bool operator ==(CanFrame a, CanFrame b) => a.Equals(b);
        public static bool operator !=(CanFrame a, CanFrame b) => !a.Equals(b);

        public override string ToString()
        {
            string hexData = BitConverter.ToString(Data, 0, Dlc).Replace("-", " ");
            return string.Format(CultureInfo.InvariantCulture,
                "CAN {0:X3}{1} [{2}] {3}",
                Id, IsExtended ? " (EXT)" : "", Dlc, hexData);
        }
    }
}
