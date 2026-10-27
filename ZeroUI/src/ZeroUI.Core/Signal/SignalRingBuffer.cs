using System;

namespace ZeroUI.Core.Signal
{
    /// <summary>
    /// High-performance zero-allocation circular ring buffer for streaming real-time sensor
    /// and analog oscilloscope signal samples. Supports single-sample push, bulk span writes,
    /// chronological extraction, and hardware-grade edge trigger detection.
    /// </summary>
    public class SignalRingBuffer
    {
        private readonly float[] _buffer;
        private readonly int _capacity;
        private int _head = 0; // Next write position
        private int _count = 0;

        public int Capacity => _capacity;
        public int Count => _count;

        public SignalRingBuffer(int capacity = 65536)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be positive.");
            _capacity = capacity;
            _buffer = new float[capacity];
        }

        /// <summary>
        /// Writes a single sample into the ring buffer without any heap allocations.
        /// </summary>
        public void Write(float sample)
        {
            _buffer[_head] = sample;
            _head = (_head + 1) % _capacity;
            if (_count < _capacity)
            {
                _count++;
            }
        }

        /// <summary>
        /// Writes a span of samples into the ring buffer efficiently.
        /// </summary>
        public void WriteSpan(ReadOnlySpan<float> samples)
        {
            for (int i = 0; i < samples.Length; i++)
            {
                _buffer[_head] = samples[i];
                _head = (_head + 1) % _capacity;
            }
            _count = Math.Min(_capacity, _count + samples.Length);
        }

        /// <summary>
        /// Clears all stored samples and resets buffer pointers.
        /// </summary>
        public void Clear()
        {
            _head = 0;
            _count = 0;
        }

        /// <summary>
        /// Retrieves sample at index in chronological order: index 0 is oldest, index Count - 1 is newest.
        /// </summary>
        public float this[int index]
        {
            get
            {
                if (index < 0 || index >= _count)
                    throw new ArgumentOutOfRangeException(nameof(index));

                int start = (_count == _capacity) ? _head : 0;
                int actualIndex = (start + index) % _capacity;
                return _buffer[actualIndex];
            }
        }

        /// <summary>
        /// Reads the most recent samples into the destination span in chronological order.
        /// Returns the number of samples actually copied.
        /// </summary>
        public int ReadLatest(Span<float> destination)
        {
            int samplesToCopy = Math.Min(destination.Length, _count);
            if (samplesToCopy == 0) return 0;

            int startIndex = _count - samplesToCopy;
            for (int i = 0; i < samplesToCopy; i++)
            {
                destination[i] = this[startIndex + i];
            }
            return samplesToCopy;
        }

        /// <summary>
        /// Computes basic statistical measurements over the buffer without heap allocation.
        /// </summary>
        public void ComputeMetrics(out float vMin, out float vMax, out float vP2P, out float vRms)
        {
            if (_count == 0)
            {
                vMin = 0;
                vMax = 0;
                vP2P = 0;
                vRms = 0;
                return;
            }

            float min = float.MaxValue;
            float max = float.MinValue;
            double sumSq = 0.0;

            for (int i = 0; i < _count; i++)
            {
                float val = this[i];
                if (val < min) min = val;
                if (val > max) max = val;
                sumSq += (double)val * val;
            }

            vMin = min;
            vMax = max;
            vP2P = max - min;
            vRms = (float)Math.Sqrt(sumSq / _count);
        }

        /// <summary>
        /// Searches backward from the latest sample for a trigger edge crossing.
        /// Returns the chronological index in [0..Count-1], or -1 if no trigger found.
        /// </summary>
        public int FindTriggerIndex(float threshold, bool risingEdge, int maxSearchCount)
        {
            if (_count < 2) return -1;

            int searchLen = Math.Min(maxSearchCount, _count - 1);
            int lastIndex = _count - 1;

            for (int i = 0; i < searchLen; i++)
            {
                int currIdx = lastIndex - i;
                int prevIdx = currIdx - 1;

                float curr = this[currIdx];
                float prev = this[prevIdx];

                if (risingEdge)
                {
                    if (prev <= threshold && curr > threshold)
                    {
                        return currIdx;
                    }
                }
                else
                {
                    if (prev >= threshold && curr < threshold)
                    {
                        return currIdx;
                    }
                }
            }

            return -1;
        }
    }
}
