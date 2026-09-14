using System;
using System.IO;
using System.Text;

namespace ZeroPlatform.AudioVisual.Acoustic
{
    /// <summary>
    /// Circular ring buffer optimized for real-time acoustic telemetry and vibration sensor acquisition.
    /// Provides statistical metrics calculation (RMS, Peak, Crest Factor, Kurtosis) for condition monitoring.
    /// </summary>
    public class AcousticWaveBuffer
    {
        private readonly float[] _buffer;
        private readonly int _capacity;
        private int _writeIndex = 0;
        private int _count = 0;
        private readonly object _syncLock = new object();

        public int SampleRate { get; }
        public int Capacity => _capacity;
        public int Count => _count;

        public AcousticWaveBuffer(int capacity, int sampleRate = 44100)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            _capacity = capacity;
            _buffer = new float[capacity];
            SampleRate = sampleRate;
        }

        public void Write(ReadOnlySpan<float> samples)
        {
            lock (_syncLock)
            {
                for (int i = 0; i < samples.Length; i++)
                {
                    _buffer[_writeIndex] = samples[i];
                    _writeIndex = (_writeIndex + 1) % _capacity;
                    if (_count < _capacity)
                        _count++;
                }
            }
        }

        public void Write(float[] samples, int offset, int count)
        {
            Write(new ReadOnlySpan<float>(samples, offset, count));
        }

        /// <summary>
        /// Reads the most recent N samples into the destination array.
        /// </summary>
        public int ReadLatest(Span<float> destination)
        {
            lock (_syncLock)
            {
                int toRead = Math.Min(destination.Length, _count);
                int start = (_writeIndex - toRead + _capacity) % _capacity;

                for (int i = 0; i < toRead; i++)
                {
                    destination[i] = _buffer[(start + i) % _capacity];
                }
                return toRead;
            }
        }

        /// <summary>
        /// Calculates statistical metrics for rotating machinery condition evaluation.
        /// </summary>
        public VibrationMetrics ComputeMetrics(int windowSamples = 0)
        {
            lock (_syncLock)
            {
                int samplesToEval = (windowSamples > 0 && windowSamples <= _count) ? windowSamples : _count;
                if (samplesToEval == 0) return default;

                int start = (_writeIndex - samplesToEval + _capacity) % _capacity;

                float sum = 0f;
                float sumSq = 0f;
                float peak = 0f;

                for (int i = 0; i < samplesToEval; i++)
                {
                    float val = _buffer[(start + i) % _capacity];
                    float absVal = Math.Abs(val);
                    if (absVal > peak) peak = absVal;

                    sum += val;
                    sumSq += val * val;
                }

                float mean = sum / samplesToEval;
                float rms = (float)Math.Sqrt(sumSq / samplesToEval);
                float crestFactor = (rms > 1e-6f) ? (peak / rms) : 0f;

                // Fourth moment (Kurtosis)
                float sumKurt = 0f;
                for (int i = 0; i < samplesToEval; i++)
                {
                    float diff = _buffer[(start + i) % _capacity] - mean;
                    sumKurt += diff * diff * diff * diff;
                }
                float variance = (sumSq / samplesToEval) - (mean * mean);
                float kurtosis = (variance > 1e-6f) ? (sumKurt / (samplesToEval * variance * variance)) : 3.0f;

                return new VibrationMetrics
                {
                    Rms = rms,
                    Peak = peak,
                    CrestFactor = crestFactor,
                    Kurtosis = kurtosis,
                    SampleCount = samplesToEval
                };
            }
        }
    }

    public struct VibrationMetrics
    {
        public float Rms;
        public float Peak;
        public float CrestFactor;
        public float Kurtosis;
        public int SampleCount;
    }

    /// <summary>
    /// Pure C# Standard RIFF/WAVE audio file encoder and decoder without external native DLLs.
    /// </summary>
    public static class WavCodec
    {
        public static byte[] EncodePcm16(float[] samples, int sampleRate = 44100, short channels = 1)
        {
            int subChunk2Size = samples.Length * channels * sizeof(short);
            int chunkSize = 36 + subChunk2Size;

            using (var ms = new MemoryStream(chunkSize + 8))
            using (var writer = new BinaryWriter(ms))
            {
                // RIFF chunk descriptor
                writer.Write(Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(chunkSize);
                writer.Write(Encoding.ASCII.GetBytes("WAVE"));

                // "fmt " sub-chunk
                writer.Write(Encoding.ASCII.GetBytes("fmt "));
                writer.Write(16); // Subchunk1Size for PCM
                writer.Write((short)1); // AudioFormat 1 = PCM
                writer.Write(channels);
                writer.Write(sampleRate);
                writer.Write(sampleRate * channels * sizeof(short)); // ByteRate
                writer.Write((short)(channels * sizeof(short))); // BlockAlign
                writer.Write((short)16); // BitsPerSample

                // "data" sub-chunk
                writer.Write(Encoding.ASCII.GetBytes("data"));
                writer.Write(subChunk2Size);

                for (int i = 0; i < samples.Length; i++)
                {
                    float s = Math.Max(-1.0f, Math.Min(1.0f, samples[i]));
                    short pcm = (short)(s * 32767f);
                    writer.Write(pcm);
                }

                writer.Flush();
                return ms.ToArray();
            }
        }

        public static float[] DecodePcm16(byte[] wavBytes, out int sampleRate, out short channels)
        {
            using (var ms = new MemoryStream(wavBytes))
            using (var reader = new BinaryReader(ms))
            {
                string riff = Encoding.ASCII.GetString(reader.ReadBytes(4));
                if (riff != "RIFF") throw new InvalidDataException("Not a valid RIFF file");
                reader.ReadInt32(); // chunkSize
                string wave = Encoding.ASCII.GetString(reader.ReadBytes(4));
                if (wave != "WAVE") throw new InvalidDataException("Not a valid WAVE file");

                sampleRate = 44100;
                channels = 1;
                byte[]? pcmData = null;

                while (ms.Position < ms.Length)
                {
                    string chunkId = Encoding.ASCII.GetString(reader.ReadBytes(4));
                    int chunkSize = reader.ReadInt32();

                    if (chunkId == "fmt ")
                    {
                        short format = reader.ReadInt16();
                        channels = reader.ReadInt16();
                        sampleRate = reader.ReadInt32();
                        reader.ReadInt32(); // byteRate
                        reader.ReadInt16(); // blockAlign
                        short bitsPerSample = reader.ReadInt16();

                        int remaining = chunkSize - 16;
                        if (remaining > 0) reader.ReadBytes(remaining);
                    }
                    else if (chunkId == "data")
                    {
                        pcmData = reader.ReadBytes(chunkSize);
                        break;
                    }
                    else
                    {
                        reader.ReadBytes(chunkSize);
                    }
                }

                if (pcmData == null) throw new InvalidDataException("WAV missing data chunk");

                int sampleCount = pcmData.Length / 2;
                var result = new float[sampleCount];
                for (int i = 0; i < sampleCount; i++)
                {
                    short val = BitConverter.ToInt16(pcmData, i * 2);
                    result[i] = val / 32768.0f;
                }

                return result;
            }
        }
    }
}
