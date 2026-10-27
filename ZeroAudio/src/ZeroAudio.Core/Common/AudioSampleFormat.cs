namespace ZeroAudio.Common
{
    /// <summary>
    /// Specifies the binary data format of audio samples.
    /// </summary>
    public enum AudioSampleFormat : byte
    {
        /// <summary>8-bit unsigned integer PCM (0 to 255, center 128).</summary>
        Pcm8 = 1,

        /// <summary>16-bit signed integer linear PCM (-32,768 to 32,767).</summary>
        Pcm16 = 2,

        /// <summary>24-bit signed integer linear PCM (-8,388,608 to 8,388,607, packed 3 bytes).</summary>
        Pcm24 = 3,

        /// <summary>32-bit signed integer linear PCM.</summary>
        Pcm32 = 4,

        /// <summary>32-bit IEEE 754 single-precision floating point (normalized -1.0f to +1.0f).</summary>
        IeeeFloat32 = 5
    }
}
