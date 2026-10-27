namespace ZeroComm.Core.Abstractions
{
    /// <summary>
    /// Describes protocol-specific operational limits and capabilities for a PLC driver.
    /// </summary>
    public sealed class PlcDriverCapabilities
    {
        public PlcVendor Vendor { get; set; }
        public int MaxPduBytes { get; set; } = 240;
        public int MaxBatchReadItems { get; set; } = 20;
        public int MaxContiguousReadBytes { get; set; } = 1000;
        public bool SupportsBitAddressing { get; set; } = true;
        public bool SupportsRandomBatchRead { get; set; } = false;
        public bool SupportsTagNames { get; set; } = false;

        public static PlcDriverCapabilities SiemensS7 => new PlcDriverCapabilities
        {
            Vendor = PlcVendor.SiemensS7,
            MaxPduBytes = 240,
            MaxBatchReadItems = 20,
            MaxContiguousReadBytes = 222,
            SupportsBitAddressing = true,
            SupportsRandomBatchRead = true,
            SupportsTagNames = false
        };

        public static PlcDriverCapabilities MitsubishiMelsec => new PlcDriverCapabilities
        {
            Vendor = PlcVendor.MitsubishiMelsec,
            MaxPduBytes = 960,
            MaxBatchReadItems = 192,
            MaxContiguousReadBytes = 960,
            SupportsBitAddressing = true,
            SupportsRandomBatchRead = true,
            SupportsTagNames = false
        };

        public static PlcDriverCapabilities OmronFins => new PlcDriverCapabilities
        {
            Vendor = PlcVendor.OmronFins,
            MaxPduBytes = 2000,
            MaxBatchReadItems = 1,
            MaxContiguousReadBytes = 1990,
            SupportsBitAddressing = true,
            SupportsRandomBatchRead = false,
            SupportsTagNames = false
        };

        public static PlcDriverCapabilities AllenBradleyCip => new PlcDriverCapabilities
        {
            Vendor = PlcVendor.AllenBradleyCip,
            MaxPduBytes = 4000,
            MaxBatchReadItems = 50,
            MaxContiguousReadBytes = 4000,
            SupportsBitAddressing = true,
            SupportsRandomBatchRead = true,
            SupportsTagNames = true
        };

        public static PlcDriverCapabilities ModbusTcp => new PlcDriverCapabilities
        {
            Vendor = PlcVendor.ModbusTcp,
            MaxPduBytes = 250,
            MaxBatchReadItems = 1,
            MaxContiguousReadBytes = 250,
            SupportsBitAddressing = true,
            SupportsRandomBatchRead = false,
            SupportsTagNames = false
        };
    }
}
