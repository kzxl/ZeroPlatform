namespace ZeroAudio.Formats
{
    /// <summary>
    /// Constants and chunk identifiers for standard RIFF/WAVE files.
    /// </summary>
    internal static class WavConstants
    {
        public const uint RiffMagic = 0x46464952; // 'RIFF' in little-endian
        public const uint WaveMagic = 0x45564157; // 'WAVE' in little-endian
        public const uint FmtMagic  = 0x20746D66; // 'fmt ' in little-endian
        public const uint DataMagic = 0x61746164; // 'data' in little-endian
        public const uint FactMagic = 0x74636166; // 'fact' in little-endian

        public const ushort WaveFormatPcm = 1;
        public const ushort WaveFormatIeeeFloat = 3;
        public const ushort WaveFormatAlaw = 6;
        public const ushort WaveFormatMulaw = 7;
        public const ushort WaveFormatExtensible = 0xFFFE;
    }
}
