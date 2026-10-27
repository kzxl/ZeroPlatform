using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ZeroBus.Can;

namespace ZeroBus.CanOpen
{
    public class CoordinatedAxisEntry
    {
        public CiA402Drive Drive { get; }
        public int MaxAllowedFollowingError { get; set; }
        public int LastTargetPosition { get; set; }
        public int LastFollowingError { get; set; }
        public bool HasSentInitialTarget { get; set; }

        public CoordinatedAxisEntry(CiA402Drive drive, int maxAllowedFollowingError)
        {
            Drive = drive ?? throw new ArgumentNullException(nameof(drive));
            MaxAllowedFollowingError = maxAllowedFollowingError;
        }
    }

    /// <summary>
    /// Multi-Axis Motion Fieldbus Coordinator.
    /// Orchestrates multiple CiA 402 servo drives in lockstep cyclic synchronous position (CSP) mode.
    /// Features hardware-level synchronization, following-error fault containment, and emergency quick-stop guards.
    /// </summary>
    public class MotionBusCoordinator
    {
        private readonly List<CoordinatedAxisEntry> _axes = new List<CoordinatedAxisEntry>();
        private readonly SyncProducer? _syncProducer;

        public IReadOnlyList<CoordinatedAxisEntry> Axes => _axes;
        public int AxisCount => _axes.Count;
        public bool TrippedFault { get; private set; }

        public event Action<byte, int, int>? OnFollowingErrorTripped;

        public MotionBusCoordinator(SyncProducer? syncProducer = null)
        {
            _syncProducer = syncProducer;
        }

        public MotionBusCoordinator AddAxis(CiA402Drive drive, int maxAllowedFollowingError = 1000)
        {
            if (drive == null) throw new ArgumentNullException(nameof(drive));
            _axes.Add(new CoordinatedAxisEntry(drive, maxAllowedFollowingError));
            return this;
        }

        public async Task StartupAllAsync(CancellationToken ct = default)
        {
            TrippedFault = false;
            foreach (var axis in _axes)
            {
                axis.HasSentInitialTarget = false;
                await axis.Drive.StartupServoAsync(ct).ConfigureAwait(false);
            }
        }

        public async Task SetModeAllAsync(CiA402ModeOfOperation mode, CancellationToken ct = default)
        {
            foreach (var axis in _axes)
            {
                await axis.Drive.SetModeOfOperationAsync(mode, ct).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Executes a single cyclic synchronous tick (e.g. at 250Hz - 1kHz).
        /// Evaluates feedback against the previous cycle's target, transmits the new targets, and triggers SYNC.
        /// </summary>
        public async Task SyncTickAsync(int[] targetPositions, ushort controlword = 0x000F, CancellationToken ct = default)
        {
            if (targetPositions == null) throw new ArgumentNullException(nameof(targetPositions));
            if (targetPositions.Length != _axes.Count)
            {
                throw new ArgumentException($"Target positions count ({targetPositions.Length}) does not match axis count ({_axes.Count}).");
            }

            if (TrippedFault)
            {
                throw new InvalidOperationException("Cannot execute motion: Coordinator is in a tripped fault state. Call ResetFaultAllAsync() first.");
            }

            // 1. Evaluate following error on feedback from previous cycle
            for (int i = 0; i < _axes.Count; i++)
            {
                var entry = _axes[i];
                if (entry.HasSentInitialTarget)
                {
                    int followingError = System.Math.Abs(entry.Drive.ActualPosition - entry.LastTargetPosition);
                    entry.LastFollowingError = followingError;

                    if (followingError > entry.MaxAllowedFollowingError)
                    {
                        TrippedFault = true;
                        OnFollowingErrorTripped?.Invoke(entry.Drive.NodeId, followingError, entry.MaxAllowedFollowingError);
                        await QuickStopAllAsync(ct).ConfigureAwait(false);
                        return;
                    }
                }
            }

            // 2. Transmit new CSP commands to all drives
            for (int i = 0; i < _axes.Count; i++)
            {
                var entry = _axes[i];
                entry.LastTargetPosition = targetPositions[i];
                entry.HasSentInitialTarget = true;
                await entry.Drive.SendCspPdoAsync(controlword, targetPositions[i], ct).ConfigureAwait(false);
            }

            // 3. Broadcast SYNC if configured
            if (_syncProducer != null)
            {
                await _syncProducer.BroadcastSyncAsync(ct).ConfigureAwait(false);
            }
        }

        public async Task QuickStopAllAsync(CancellationToken ct = default)
        {
            foreach (var axis in _axes)
            {
                await axis.Drive.QuickStopPdoAsync(ct).ConfigureAwait(false);
            }
        }

        public async Task ResetFaultAllAsync(CancellationToken ct = default)
        {
            TrippedFault = false;
            foreach (var axis in _axes)
            {
                axis.HasSentInitialTarget = false;
                await axis.Drive.ResetFaultAsync(ct).ConfigureAwait(false);
            }
        }
    }
}
