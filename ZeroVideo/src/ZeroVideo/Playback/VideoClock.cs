using System;
using System.Diagnostics;

namespace ZeroVideo.Playback
{
    /// <summary>
    /// Master timing clock for audio-video playback synchronization and PTS alignment.
    /// Provides continuous drift-free time tracking with variable speed playback and seeking.
    /// </summary>
    public class VideoClock
    {
        private readonly Stopwatch _stopwatch = new Stopwatch();
        private TimeSpan _baseOffset = TimeSpan.Zero;
        private double _playbackRate = 1.0;
        private readonly object _lock = new object();

        public bool IsRunning => _stopwatch.IsRunning;

        public double PlaybackRate
        {
            get => _playbackRate;
            set
            {
                lock (_lock)
                {
                    if (value <= 0.0) throw new ArgumentOutOfRangeException(nameof(value), "Playback rate must be positive.");
                    // Snapshot current accumulated time before rate change
                    _baseOffset = Position;
                    _stopwatch.Restart();
                    _playbackRate = value;
                }
            }
        }

        public TimeSpan Position
        {
            get
            {
                lock (_lock)
                {
                    if (!_stopwatch.IsRunning) return _baseOffset;
                    double elapsedTicks = _stopwatch.Elapsed.Ticks * _playbackRate;
                    return _baseOffset + TimeSpan.FromTicks((long)elapsedTicks);
                }
            }
        }

        public void Start()
        {
            lock (_lock)
            {
                _baseOffset = TimeSpan.Zero;
                _stopwatch.Restart();
            }
        }

        public void Pause()
        {
            lock (_lock)
            {
                if (_stopwatch.IsRunning)
                {
                    _baseOffset = Position;
                    _stopwatch.Stop();
                }
            }
        }

        public void Resume()
        {
            lock (_lock)
            {
                if (!_stopwatch.IsRunning)
                {
                    _stopwatch.Restart();
                }
            }
        }

        public void Stop()
        {
            lock (_lock)
            {
                _stopwatch.Reset();
                _baseOffset = TimeSpan.Zero;
            }
        }

        public void Seek(TimeSpan targetPosition)
        {
            lock (_lock)
            {
                _baseOffset = targetPosition < TimeSpan.Zero ? TimeSpan.Zero : targetPosition;
                if (_stopwatch.IsRunning)
                {
                    _stopwatch.Restart();
                }
            }
        }
    }
}
