using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using ZeroUI.Core.Runtime;

namespace ZeroUI.Core.Rendering
{
    /// <summary>
    /// Listener contract for central animation frame notifications.
    /// </summary>
    public interface IAnimationFrameListener
    {
        /// <summary>
        /// Called when an animation frame advances.
        /// </summary>
        /// <param name="deltaSeconds">Elapsed time in seconds since the last frame.</param>
        /// <param name="frameCount">Total monotonic frame counter.</param>
        void OnAnimationFrame(double deltaSeconds, long frameCount);
    }

    /// <summary>
    /// High-resolution centralized animation clock.
    /// Eliminates per-control UI timers, drastically cutting CPU overhead and avoiding timer thread contention.
    /// Supports both worker thread ticks and UI SynchronizationContext synchronization.
    /// </summary>
    public static class ZeroAnimationClock
    {
        private static readonly object _lock = new object();
        private static Action<double, long>[] _actionListeners = Array.Empty<Action<double, long>>();
        private static IAnimationFrameListener[] _contractListeners = Array.Empty<IAnimationFrameListener>();

        private static Timer? _timer;
        private static readonly Stopwatch _stopwatch = new Stopwatch();
        private static double _lastElapsedSeconds;
        private static long _frameCount;
        private static bool _isRunning;
        private static int _targetFps = 60;
        private static int _baseFps = 60;
        private static int _boostCount;
        private static SynchronizationContext? _syncContext;
        private static bool _timerPeriodSet;

        private static IDisposable? _runtimeSub;
        private static double _totalElapsedTime;

        [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod", ExactSpelling = true)]
        private static extern uint TimeBeginPeriod(uint uMilliseconds);

        [DllImport("winmm.dll", EntryPoint = "timeEndPeriod", ExactSpelling = true)]
        private static extern uint TimeEndPeriod(uint uMilliseconds);

        /// <summary>
        /// Total monotonic frames rendered since clock startup.
        /// </summary>
        public static long FrameCount => _frameCount;

        /// <summary>
        /// Total elapsed time in seconds accumulated by this animation clock.
        /// </summary>
        public static double TotalElapsedTime => _totalElapsedTime;

        /// <summary>
        /// Global synchronized ISA-18.2 fast blink phase (2 Hz / 250 ms toggle).
        /// Standardized across all annunciators, LED towers, and unacknowledged alarms.
        /// </summary>
        public static bool BlinkFast => ((long)(_totalElapsedTime * 4.0) % 2) == 0;

        /// <summary>
        /// Global synchronized ISA-18.2 slow blink phase (1 Hz / 500 ms toggle).
        /// Standardized across all warning beacons, valves, and acknowledged alarm states.
        /// </summary>
        public static bool BlinkSlow => ((long)(_totalElapsedTime * 2.0) % 2) == 0;

        /// <summary>
        /// Continuous sinusoidal pulse / breath phase in range [0.0, 1.0].
        /// Ideal for glowing halos, status badge pulses, and alert rings.
        /// </summary>
        public static float PulsePhase => (float)((Math.Sin(_totalElapsedTime * Math.PI * 2.0) + 1.0) * 0.5);

        /// <summary>
        /// Continuous linear translation phase in range [0.0, 1.0).
        /// Ideal for fluid dynamics in pipes, moving conveyor belts, and marquee progress indicators.
        /// </summary>
        public static float FluidPhase => (float)(_totalElapsedTime % 1.0);

        /// <summary>
        /// Gets the total active subscriber count.
        /// </summary>
        public static int SubscriberCount => _actionListeners.Length + _contractListeners.Length;

        /// <summary>
        /// True if the central clock is actively ticking.
        /// </summary>
        public static bool IsRunning => _isRunning;

        private static int _detectedDisplayFps;

        /// <summary>
        /// Gets the detected physical display refresh rate (in Hz) on this system.
        /// </summary>
        public static int DetectedDisplayFps
        {
            get
            {
                if (_detectedDisplayFps <= 0)
                {
                    _detectedDisplayFps = ZeroUI.Core.Platform.DisplayRefreshDetector.GetPrimaryDisplayRefreshRate();
                }
                return _detectedDisplayFps;
            }
        }

        /// <summary>
        /// Automatically synchronizes TargetFps to match the user's active display refresh rate.
        /// Avoids wasted CPU on 60 Hz monitors while unlocking 120Hz/144Hz/240Hz on high-refresh monitors.
        /// </summary>
        /// <returns>The synchronized target FPS.</returns>
        public static int AutoSynchronizeWithDisplay()
        {
            int hz = DetectedDisplayFps;
            TargetFps = hz;
            _baseFps = hz;
            return hz;
        }

        /// <summary>
        /// Gets or sets the target frames per second (default is 60, up to 240).
        /// </summary>
        public static int TargetFps
        {
            get => _targetFps;
            set
            {
                if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value), "FPS must be greater than 0.");
                _targetFps = Math.Min(240, Math.Max(10, value));
                ZeroRuntime.Shared.SetCycleInterval(RuntimeCycle.Animation, TimeSpan.FromMilliseconds(Math.Max(4, 1000.0 / _targetFps)));
                if (_isRunning)
                {
                    RestartTimer();
                }
            }
        }

        /// <summary>
        /// Temporarily boosts animation framerate to a high-refresh target (e.g. 120 FPS)
        /// while interactive transient animations (Drawer, Modal, Toast) are active.
        /// Reverts automatically to standard cadence (60 FPS) when disposed.
        /// </summary>
        public static IDisposable BoostTargetFps(int boostedFps = 120)
        {
            lock (_lock)
            {
                _boostCount++;
                if (_targetFps < boostedFps)
                {
                    TargetFps = boostedFps;
                }
            }

            return new SubscriptionToken(() =>
            {
                lock (_lock)
                {
                    _boostCount = Math.Max(0, _boostCount - 1);
                    if (_boostCount == 0 && _targetFps != _baseFps)
                    {
                        TargetFps = _baseFps;
                    }
                }
            });
        }

        /// <summary>
        /// Configures the SynchronizationContext used to marshal frame callbacks to the UI thread (if desired).
        /// </summary>
        public static void SetSynchronizationContext(SynchronizationContext? context)
        {
            _syncContext = context;
        }

        /// <summary>
        /// Starts the centralized animation clock if not already running.
        /// </summary>
        public static void Start(int targetFps = 60)
        {
            lock (_lock)
            {
                if (_isRunning) return;
                if (targetFps <= 0)
                {
                    targetFps = DetectedDisplayFps;
                }
                _targetFps = targetFps;
                _baseFps = targetFps;
                _stopwatch.Restart();
                _lastElapsedSeconds = 0;
                _isRunning = true;

                if (!_timerPeriodSet && Environment.OSVersion.Platform == PlatformID.Win32NT)
                {
                    try
                    {
                        TimeBeginPeriod(1);
                        _timerPeriodSet = true;
                    }
                    catch { }
                }

                ZeroRuntime.Shared.SetCycleInterval(RuntimeCycle.Animation, TimeSpan.FromMilliseconds(Math.Max(4, 1000.0 / _targetFps)));
                _runtimeSub ??= ZeroRuntime.Shared.Register(RuntimeCycle.Animation, (delta, frame) => TriggerFrame(delta.TotalSeconds));
                if (!ZeroRuntime.Shared.IsRunning)
                {
                    ZeroRuntime.Shared.Start();
                }
            }
        }

        /// <summary>
        /// Stops the central animation clock.
        /// </summary>
        public static void Stop()
        {
            lock (_lock)
            {
                _isRunning = false;
                _runtimeSub?.Dispose();
                _runtimeSub = null;
                _timer?.Dispose();
                _timer = null;
                _stopwatch.Stop();

                if (_timerPeriodSet && Environment.OSVersion.Platform == PlatformID.Win32NT)
                {
                    try
                    {
                        TimeEndPeriod(1);
                        _timerPeriodSet = false;
                    }
                    catch { }
                }
            }
        }

        /// <summary>
        /// Subscribes a delegate callback to the central animation frame tick.
        /// Returns an IDisposable token for easy unsubscription.
        /// </summary>
        public static IDisposable Subscribe(Action<double, long> callback)
        {
            if (callback == null) throw new ArgumentNullException(nameof(callback));

            lock (_lock)
            {
                if (Array.IndexOf(_actionListeners, callback) < 0)
                {
                    var newArray = new Action<double, long>[_actionListeners.Length + 1];
                    Array.Copy(_actionListeners, newArray, _actionListeners.Length);
                    newArray[newArray.Length - 1] = callback;
                    _actionListeners = newArray;
                }

                if (!_isRunning && (_actionListeners.Length > 0 || _contractListeners.Length > 0))
                {
                    Start(_targetFps);
                }
            }

            return new SubscriptionToken(() => Unsubscribe(callback));
        }

        /// <summary>
        /// Subscribes an IAnimationFrameListener to the central animation tick.
        /// </summary>
        public static void Subscribe(IAnimationFrameListener listener)
        {
            if (listener == null) return;
            lock (_lock)
            {
                if (Array.IndexOf(_contractListeners, listener) < 0)
                {
                    var newArray = new IAnimationFrameListener[_contractListeners.Length + 1];
                    Array.Copy(_contractListeners, newArray, _contractListeners.Length);
                    newArray[newArray.Length - 1] = listener;
                    _contractListeners = newArray;
                }

                if (!_isRunning && (_actionListeners.Length > 0 || _contractListeners.Length > 0))
                {
                    Start(_targetFps);
                }
            }
        }

        /// <summary>
        /// Unsubscribes a delegate callback.
        /// </summary>
        public static void Unsubscribe(Action<double, long> callback)
        {
            if (callback == null) return;
            lock (_lock)
            {
                int idx = Array.IndexOf(_actionListeners, callback);
                if (idx >= 0)
                {
                    var newArray = new Action<double, long>[_actionListeners.Length - 1];
                    if (idx > 0)
                        Array.Copy(_actionListeners, 0, newArray, 0, idx);
                    if (idx < _actionListeners.Length - 1)
                        Array.Copy(_actionListeners, idx + 1, newArray, idx, _actionListeners.Length - idx - 1);
                    _actionListeners = newArray;
                }

                if (_actionListeners.Length == 0 && _contractListeners.Length == 0)
                {
                    Stop();
                }
            }
        }

        /// <summary>
        /// Unsubscribes an IAnimationFrameListener.
        /// </summary>
        public static void Unsubscribe(IAnimationFrameListener listener)
        {
            if (listener == null) return;
            lock (_lock)
            {
                int idx = Array.IndexOf(_contractListeners, listener);
                if (idx >= 0)
                {
                    var newArray = new IAnimationFrameListener[_contractListeners.Length - 1];
                    if (idx > 0)
                        Array.Copy(_contractListeners, 0, newArray, 0, idx);
                    if (idx < _contractListeners.Length - 1)
                        Array.Copy(_contractListeners, idx + 1, newArray, idx, _contractListeners.Length - idx - 1);
                    _contractListeners = newArray;
                }

                if (_actionListeners.Length == 0 && _contractListeners.Length == 0)
                {
                    Stop();
                }
            }
        }

        /// <summary>
        /// Manually pumps a frame tick on the caller thread. Useful when hosting in custom message loops.
        /// </summary>
        public static void ManualTick(double deltaSeconds)
        {
            TriggerFrame(deltaSeconds);
        }

        private static void RestartTimer()
        {
            _timer?.Dispose();
            int intervalMs = Math.Max(8, 1000 / _targetFps);
            _timer = new Timer(OnTimerTick, null, intervalMs, intervalMs);
        }

        private static void OnTimerTick(object? state)
        {
            if (!_isRunning) return;

            double currentElapsed = _stopwatch.Elapsed.TotalSeconds;
            double delta = currentElapsed - _lastElapsedSeconds;
            _lastElapsedSeconds = currentElapsed;

            // Clamp delta to prevent time-spiral jumps if app pauses
            if (delta <= 0) delta = 1.0 / _targetFps;
            else if (delta > 0.5) delta = 0.5;

            var ctx = _syncContext;
            if (ctx != null)
            {
                ctx.Post(_ => TriggerFrame(delta), null);
            }
            else
            {
                TriggerFrame(delta);
            }
        }

        private static void TriggerFrame(double delta)
        {
            long currentFrame = Interlocked.Increment(ref _frameCount);
            _totalElapsedTime += delta;

            // Automatically flush queued dirty UI actions on the animation frame boundary
            try
            {
                UiDispatcher.FlushPending();
            }
            catch
            {
                // Guard against individual UI flush exceptions
            }

            var actions = _actionListeners;
            for (int i = 0; i < actions.Length; i++)
            {
                try { actions[i](delta, currentFrame); }
                catch { /* Swallow exception to prevent one broken control from halting the clock */ }
            }

            var contracts = _contractListeners;
            for (int i = 0; i < contracts.Length; i++)
            {
                try { contracts[i].OnAnimationFrame(delta, currentFrame); }
                catch { /* Swallow exception */ }
            }
        }

        private sealed class SubscriptionToken : IDisposable
        {
            private readonly Action _unsubscribe;
            private bool _disposed;

            public SubscriptionToken(Action unsubscribe)
            {
                _unsubscribe = unsubscribe;
            }

            public void Dispose()
            {
                if (!_disposed)
                {
                    _disposed = true;
                    _unsubscribe();
                }
            }
        }
    }
}
