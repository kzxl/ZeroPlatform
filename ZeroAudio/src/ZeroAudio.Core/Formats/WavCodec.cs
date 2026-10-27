using System;
using System.IO;

namespace ZeroAudio.Formats
{
    /// <summary>
    /// Pure C# high-level WAV audio file codec helpers.
    /// Provides convenient 1-line encode and decode methods backed by WavWriter and WavReader.
    /// </summary>
    public static class WavCodec
    {
        /// <summary>
        /// Encodes float samples into a 16-bit PCM RIFF/WAVE byte array.
        /// </summary>
        public static byte[] EncodePcm16(float[] samples, int sampleRate = 44100, short channels = 1)
        {
            if (samples == null) throw new ArgumentNullException(nameof(samples));
            var format = AudioFormat.Pcm16(sampleRate, channels);
            using (var ms = new MemoryStream())
            {
                using (var writer = new WavWriter(ms, format, leaveOpen: true))
                {
                    writer.WriteSamples(samples);
                }
                return ms.ToArray();
            }
        }

        /// <summary>
        /// Decodes a 16-bit PCM RIFF/WAVE byte array into normalized float samples.
        /// </summary>
        public static float[] DecodePcm16(byte[] wavBytes, out int sampleRate, out short channels)
        {
            if (wavBytes == null) throw new ArgumentNullException(nameof(wavBytes));
            using (var ms = new MemoryStream(wavBytes))
            using (var reader = new WavReader(ms))
            {
                sampleRate = reader.Format.SampleRate;
                channels = (short)reader.Format.Channels;
                int totalSamples = (int)(reader.TotalFrames * channels);
                float[] result = new float[totalSamples];
                reader.ReadSamples(result);
                return result;
            }
        }
    }
}
