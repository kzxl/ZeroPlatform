using System;

namespace ZeroRfid.Core.Codecs;

/// <summary>
/// GS1 EPC Tag Data Standard (TDS) scheme identification.
/// </summary>
public enum Gs1Scheme
{
    Unknown = 0,

    /// <summary>Serialized Global Trade Item Number (96-bit). Used for retail and product item serialization.</summary>
    Sgtin96 = 0x30,

    /// <summary>Serial Shipping Container Code (96-bit). Used for logistics cartons and shipping pallets.</summary>
    Sscc96 = 0x31,

    /// <summary>Global Location Number (96-bit). Used for factory docks, warehouse zones, and physical locations.</summary>
    Sgln96 = 0x32,

    /// <summary>Global Returnable Asset Identifier (96-bit). Used for reusable totes, crates, and containers.</summary>
    Grai96 = 0x33,

    /// <summary>Global Individual Asset Identifier (96-bit). Used for fixed capital assets and equipment.</summary>
    Giai96 = 0x34
}

/// <summary>
/// Parsed GS1 TDS metadata container.
/// </summary>
public sealed class Gs1TagInfo
{
    public Gs1Scheme Scheme { get; }
    public byte FilterValue { get; }
    public byte Partition { get; }
    public ulong CompanyPrefix { get; }
    public ulong ItemReference { get; }
    public string SerialNumber { get; }

    public Gs1TagInfo(Gs1Scheme scheme, byte filterValue, byte partition, ulong companyPrefix, ulong itemReference, string serialNumber)
    {
        Scheme = scheme;
        FilterValue = filterValue;
        Partition = partition;
        CompanyPrefix = companyPrefix;
        ItemReference = itemReference;
        SerialNumber = serialNumber;
    }

    public override string ToString() => $"[GS1] Scheme={Scheme}, Company={CompanyPrefix}, Item={ItemReference}, Serial={SerialNumber}";
}

/// <summary>
/// Lightweight pure C# decoder for standard GS1 EPC Tag Data Standard (TDS) bitstreams.
/// </summary>
public static class Gs1TdsCodec
{
    /// <summary>
    /// Identifies the GS1 EPC scheme from a raw hexadecimal EPC string.
    /// </summary>
    public static Gs1Scheme IdentifyScheme(string rawHex)
    {
        if (string.IsNullOrEmpty(rawHex) || rawHex.Length < 2)
            return Gs1Scheme.Unknown;

        byte[] bytes = EpcHexCodec.ToBytes(rawHex.AsSpan(0, 2));
        if (bytes.Length == 0)
            return Gs1Scheme.Unknown;

        byte header = bytes[0];
        return header switch
        {
            0x30 => Gs1Scheme.Sgtin96,
            0x31 => Gs1Scheme.Sscc96,
            0x32 => Gs1Scheme.Sgln96,
            0x33 => Gs1Scheme.Grai96,
            0x34 => Gs1Scheme.Giai96,
            _ => Gs1Scheme.Unknown
        };
    }

    /// <summary>
    /// Attempts to parse a 96-bit SGTIN-96 EPC string into structured components.
    /// </summary>
    public static bool TryParseSgtin96(string rawHex, out Gs1TagInfo? info)
    {
        info = null;
        if (string.IsNullOrEmpty(rawHex)) return false;

        string norm = EpcHexCodec.Normalize(rawHex);
        if (norm.Length != 24) return false; // 96 bits = 12 bytes = 24 hex characters

        byte[] b = EpcHexCodec.ToBytes(norm);
        if (b[0] != 0x30) return false; // Header check

        // Bit extraction:
        // Header: 8 bits [0..7] = b[0]
        // Filter: 3 bits [8..10]
        // Partition: 3 bits [11..13]
        byte filter = (byte)((b[1] >> 5) & 0x07);
        byte partition = (byte)((b[1] >> 2) & 0x07);

        // Partition determines company prefix bits (L_C) vs item reference bits (L_I)
        int compBits = partition switch
        {
            0 => 40,
            1 => 37,
            2 => 34,
            3 => 30,
            4 => 27,
            5 => 24,
            6 => 20,
            _ => 24
        };
        int itemBits = 44 - compBits;

        // Serial number is the last 38 bits [58..95]
        ulong serialNum = ReadBitsUlong(b, 58, 38);

        // Simplified extraction for company and item references
        ulong company = ReadBitsUlong(b, 14, compBits);
        ulong item = ReadBitsUlong(b, 14 + compBits, itemBits);

        info = new Gs1TagInfo(Gs1Scheme.Sgtin96, filter, partition, company, item, serialNum.ToString());
        return true;
    }

    private static ulong ReadBitsUlong(byte[] data, int startBit, int bitCount)
    {
        ulong result = 0;
        for (int i = 0; i < bitCount; i++)
        {
            int bitPos = startBit + i;
            int byteIndex = bitPos / 8;
            int bitOffset = 7 - (bitPos % 8);
            int bit = (data[byteIndex] >> bitOffset) & 1;
            result = (result << 1) | (ulong)(uint)bit;
        }
        return result;
    }
}
