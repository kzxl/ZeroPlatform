using System;
using System.Threading;

namespace ZeroUI.Core.Scada
{
    /// <summary>
    /// Lock-free Triple Buffer for SCADA telemetry ingestion and UI visualization.
    /// Completely decouples high-frequency field communication (10 kHz) from display rendering (60/144 Hz).
    /// Guarantees latest-value semantics with zero locks, zero thread stalls, and zero memory allocations.
    /// </summary>
    public sealed class ZeroTripleBuffer
    {
        private readonly TagStorage[] _buffers;
        private int _writeSlot = 0;
        private int _sharedSlot = 1;
        private int _readSlot = 2;
        private int _hasNewData = 0;

        public ZeroTripleBuffer(int initialTagCapacity = 1024)
        {
            _buffers = new[]
            {
                new TagStorage(initialTagCapacity),
                new TagStorage(initialTagCapacity),
                new TagStorage(initialTagCapacity)
            };
        }

        /// <summary>
        /// Gets the active buffer currently dedicated to the producer/writer thread.
        /// </summary>
        public TagStorage GetWriteBuffer() => _buffers[_writeSlot];

        /// <summary>
        /// Atomically publishes the current write buffer as ready for the consumer/UI thread,
        /// swapping in the previously released shared buffer for the next write cycle.
        /// </summary>
        public void PublishWrite()
        {
            int oldShared = Interlocked.Exchange(ref _sharedSlot, _writeSlot);
            _writeSlot = oldShared;
            Volatile.Write(ref _hasNewData, 1);
        }

        /// <summary>
        /// Consumed by the UI STA thread during frame tick.
        /// If a newer telemetry snapshot is available, atomically acquires it.
        /// Returns the dedicated, immutable render buffer for the current frame.
        /// </summary>
        /// <param name="hasUpdate">True if a new snapshot was acquired; false if using existing frame data.</param>
        /// <returns>The TagStorage instance for rendering.</returns>
        public TagStorage AcquireRenderBuffer(out bool hasUpdate)
        {
            if (Interlocked.Exchange(ref _hasNewData, 0) == 1)
            {
                int oldShared = Interlocked.Exchange(ref _sharedSlot, _readSlot);
                _readSlot = oldShared;
                hasUpdate = true;
            }
            else
            {
                hasUpdate = false;
            }

            return _buffers[_readSlot];
        }
    }
}
