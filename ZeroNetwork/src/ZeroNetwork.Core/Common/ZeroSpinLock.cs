using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace ZeroNetwork.Common
{
    /// <summary>
    /// Ultra-low latency, cache-line padded TTAS (Test-and-Test-and-Set) spinlock with exponential backoff.
    /// Replaces BCL <see cref="System.Threading.Monitor"/> (<c>lock (obj)</c>) on sub-microsecond critical sections,
    /// avoiding OS kernel transitions and thread descheduling while eliminating False Sharing with 64-byte padding.
    /// </summary>
    public sealed class ZeroSpinLock
    {
        private const int Unlocked = 0;
        private const int Locked = 1;

        [StructLayout(LayoutKind.Explicit, Size = 128)]
        private struct PaddedState
        {
            [FieldOffset(64)]
            public int Value;
        }

        private PaddedState _state;

        /// <summary>
        /// Gets whether the lock is currently held.
        /// </summary>
        public bool IsLocked => Volatile.Read(ref _state.Value) == Locked;

        /// <summary>
        /// Acquires the spinlock, spinning with exponential backoff and PAUSE instructions until obtained.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Enter()
        {
            if (Interlocked.CompareExchange(ref _state.Value, Locked, Unlocked) == Unlocked)
            {
                return;
            }

            EnterSlowPath();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void EnterSlowPath()
        {
            var spinner = new SpinWait();
            while (true)
            {
                // Test phase: spin locally on cache line without issuing bus-locking atomic writes
                while (Volatile.Read(ref _state.Value) == Locked)
                {
                    spinner.SpinOnce();
                }

                // Test-and-Set phase: attempt atomic transition
                if (Interlocked.CompareExchange(ref _state.Value, Locked, Unlocked) == Unlocked)
                {
                    return;
                }
            }
        }

        /// <summary>
        /// Attempts to acquire the spinlock without blocking.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryEnter()
        {
            return Interlocked.CompareExchange(ref _state.Value, Locked, Unlocked) == Unlocked;
        }

        /// <summary>
        /// Releases the spinlock with release memory barrier semantics.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Exit()
        {
            Volatile.Write(ref _state.Value, Unlocked);
        }

        /// <summary>
        /// Acquires the lock and returns a zero-allocation disposable scope.
        /// Usage: <c>using (lock.Acquire()) { ... }</c>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LockScope Acquire()
        {
            Enter();
            return new LockScope(this);
        }

        /// <summary>
        /// Zero-allocation struct scope for RAII lock release.
        /// </summary>
        public readonly struct LockScope : IDisposable
        {
            private readonly ZeroSpinLock _lock;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public LockScope(ZeroSpinLock spinLock)
            {
                _lock = spinLock;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Dispose()
            {
                _lock?.Exit();
            }
        }
    }
}
