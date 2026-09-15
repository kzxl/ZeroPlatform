namespace ZeroRfid.Core.Models;

/// <summary>
/// Specifies the standardized EPCglobal Gen2 memory bank.
/// </summary>
public enum MemoryBank
{
    /// <summary>Bank 00: Reserved memory containing Kill and Access passwords (32 bits each).</summary>
    Reserved = 0,

    /// <summary>Bank 01: Electronic Product Code (EPC) memory, CRC, and protocol control (PC) bits.</summary>
    Epc = 1,

    /// <summary>Bank 10: Tag Identifier (TID) memory containing factory-programmed silicon manufacturer and chip IDs.</summary>
    Tid = 2,

    /// <summary>Bank 11: User memory containing user-defined data payload.</summary>
    User = 3
}
