using System;
using System.Runtime.InteropServices;
using System.Threading;
using ZeroGraphics.Imaging.Core;

namespace ZeroGraphics.Vision.Pipeline
{
    /// <summary>
    /// Represents a pre-allocated unmanaged memory slot in the ring buffer.
    /// Thread-safe transitions managed via lock-free atomic CAS operations.
    /// </summary>
    public unsafe struct FrameSlot
    {
        public int SlotIndex;
        public byte* Scan0;
        public long FrameId;
        public int Width;
        public int Height;
        public int Stride;
        public ImageFormatMode Format;
        public long TimestampTicks;

        /// <summary>
        /// Atomic slot lifecycle state:
        /// 0: Free (available for ingestion)
        /// 1: Writing (being filled by camera acquisition thread)
        /// 2: Ready (populated, waiting for inspection worker)
        /// 3: Processing (claimed by an inspection worker thread)
        /// </summary>
        public int State;
    }

    /// <summary>
    /// High-throughput, lock-free circular ring buffer backed by pre-allocated unmanaged memory.
    /// Eliminates garbage collection (GC) overhead during continuous high-speed industrial camera acquisition (100-200 FPS).
    /// </summary>
    public sealed unsafe class UnmanagedFrameRingBuffer : IDisposable
    {
        public const int StateFree = 0;
        public const int StateWriting = 1;
        public const int StateReady = 2;
        public const int StateProcessing = 3;

        private readonly int _slotCount;
        private readonly int _slotCapacityBytes;
        private readonly FrameSlot[] _slots;
        private readonly IntPtr[] _nativeBuffers;

        private long _pushedCount;
        private long _droppedCount;
        private long _processedCount;
        private long _writeCursor;

        private bool _disposed;

        public int SlotCount => _slotCount;
        public int SlotCapacityBytes => _slotCapacityBytes;
        public long PushedCount => Interlocked.Read(ref _pushedCount);
        public long DroppedCount => Interlocked.Read(ref _droppedCount);
        public long ProcessedCount => Interlocked.Read(ref _processedCount);

        public UnmanagedFrameRingBuffer(
            int slotCount = 16,
            int maxFrameWidth = 2592,
            int maxFrameHeight = 2048,
            int maxBytesPerPixel = 4)
        {
            if (slotCount <= 0 || (slotCount & (slotCount - 1)) != 0)
                throw new ArgumentException("SlotCount must be a positive power of two (e.g. 8, 16, 32).", nameof(slotCount));

            _slotCount = slotCount;

            int maxStride = ((maxFrameWidth * maxBytesPerPixel) + 3) & ~3;
            _slotCapacityBytes = maxStride * maxFrameHeight;

            _slots = new FrameSlot[_slotCount];
            _nativeBuffers = new IntPtr[_slotCount];

            for (int i = 0; i < _slotCount; i++)
            {
                IntPtr pMem = Marshal.AllocHGlobal(_slotCapacityBytes);
                _nativeBuffers[i] = pMem;

                _slots[i] = new FrameSlot
                {
                    SlotIndex = i,
                    Scan0 = (byte*)pMem,
                    State = StateFree
                };
            }
        }

        /// <summary>
        /// Attempts to acquire an available slot for writing directly from camera SDK callback.
        /// Non-blocking: returns false if all slots are currently saturated (backpressure).
        /// </summary>
        public bool TryAcquireWritingSlot(out int slotIndex)
        {
            slotIndex = -1;
            if (_disposed) return false;

            long startSeq = Interlocked.Increment(ref _writeCursor);

            for (int attempt = 0; attempt < _slotCount; attempt++)
            {
                int candidate = (int)((startSeq + attempt) & (_slotCount - 1));
                ref FrameSlot slot = ref _slots[candidate];

                if (Interlocked.CompareExchange(ref slot.State, StateWriting, StateFree) == StateFree)
                {
                    slotIndex = candidate;
                    return true;
                }
            }

            // All slots currently saturated by workers
            Interlocked.Increment(ref _droppedCount);
            return false;
        }

        /// <summary>
        /// Commits a populated slot and marks it as ready for parallel inspection workers.
        /// </summary>
        public void CommitWritingSlot(
            int slotIndex,
            long frameId,
            int width,
            int height,
            ImageFormatMode format,
            int stride)
        {
            if ((uint)slotIndex >= (uint)_slotCount) throw new ArgumentOutOfRangeException(nameof(slotIndex));

            ref FrameSlot slot = ref _slots[slotIndex];
            slot.FrameId = frameId;
            slot.Width = width;
            slot.Height = height;
            slot.Format = format;
            slot.Stride = stride;
            slot.TimestampTicks = DateTime.UtcNow.Ticks;

            Interlocked.Increment(ref _pushedCount);

            // Publish slot to workers atomically
            Volatile.Write(ref slot.State, StateReady);
        }

        /// <summary>
        /// Attempts to claim the next ready frame for inspection by a worker thread.
        /// Uses atomic CAS: exactly one worker will successfully claim each frame.
        /// </summary>
        public bool TryAcquireReadySlot(out int slotIndex)
        {
            slotIndex = -1;
            if (_disposed) return false;

            for (int i = 0; i < _slotCount; i++)
            {
                ref FrameSlot slot = ref _slots[i];

                if (Volatile.Read(ref slot.State) == StateReady)
                {
                    if (Interlocked.CompareExchange(ref slot.State, StateProcessing, StateReady) == StateReady)
                    {
                        slotIndex = i;
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Releases a processed slot back to the Free state.
        /// </summary>
        public void ReleaseSlot(int slotIndex)
        {
            if ((uint)slotIndex >= (uint)_slotCount) return;

            ref FrameSlot slot = ref _slots[slotIndex];
            Interlocked.Increment(ref _processedCount);

            // Release back to pool
            Volatile.Write(ref slot.State, StateFree);
        }

        /// <summary>
        /// Gets a reference to a slot by index for zero-copy inspection or blitting.
        /// </summary>
        public ref FrameSlot GetSlot(int slotIndex)
        {
            if ((uint)slotIndex >= (uint)_slotCount) throw new ArgumentOutOfRangeException(nameof(slotIndex));
            return ref _slots[slotIndex];
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            for (int i = 0; i < _slotCount; i++)
            {
                if (_nativeBuffers[i] != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(_nativeBuffers[i]);
                    _nativeBuffers[i] = IntPtr.Zero;
                    _slots[i].Scan0 = null;
                }
            }
        }
    }
}
