using System;
using System.Collections.Generic;
using ZeroVideo.Core;

namespace ZeroVideo.Playback
{
    public enum PlaybackState
    {
        Stopped,
        Playing,
        Paused
    }

    /// <summary>
    /// Frame dropping policy when the video queue exceeds capacity.
    /// </summary>
    public enum FrameDropStrategy
    {
        /// <summary>Drops the oldest queued frame to maintain lowest display latency (ideal for live cameras).</summary>
        DropOldest,

        /// <summary>Rejects newly incoming frames when queue is full.</summary>
        DropNewest,

        /// <summary>Does not drop; keeps queuing without limit (legacy mode).</summary>
        Unbounded
    }

    /// <summary>
    /// Thread-safe video playback controller with clock synchronization, bounded buffer backpressure, and frame stepping.
    /// Ideal for both continuous camera stream display and forensic inspection review.
    /// </summary>
    public class VideoPlayer
    {
        private readonly Queue<VideoFrameBuffer> _frameQueue = new Queue<VideoFrameBuffer>();
        private readonly object _lock = new object();
        private PlaybackState _state = PlaybackState.Stopped;
        private VideoFrameBuffer? _currentFrame;
        private int _maxQueueCapacity = 30;
        private FrameDropStrategy _dropStrategy = FrameDropStrategy.DropOldest;
        private long _droppedFramesCount;

        public VideoClock Clock { get; } = new VideoClock();
        public PlaybackState State => _state;
        public VideoFrameBuffer? CurrentFrame => _currentFrame;
        public int QueueCount { get { lock (_lock) return _frameQueue.Count; } }

        /// <summary>Maximum queued frames allowed before applying the drop strategy (default 30).</summary>
        public int MaxQueueCapacity
        {
            get => _maxQueueCapacity;
            set
            {
                if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value), "MaxQueueCapacity must be positive.");
                _maxQueueCapacity = value;
            }
        }

        /// <summary>Active frame drop policy when capacity is reached.</summary>
        public FrameDropStrategy DropStrategy
        {
            get => _dropStrategy;
            set => _dropStrategy = value;
        }

        /// <summary>Total frames dropped due to queue saturation.</summary>
        public long DroppedFramesCount => _droppedFramesCount;

        public event EventHandler<VideoFrameBuffer>? FrameReady;
        public event EventHandler<PlaybackState>? StateChanged;
        public event EventHandler<VideoFrameBuffer>? FrameDropped;

        /// <summary>
        /// Enqueues a video frame. Returns true if queued, or false if dropped due to capacity backpressure.
        /// </summary>
        public bool EnqueueFrame(VideoFrameBuffer frame)
        {
            if (frame == null) throw new ArgumentNullException(nameof(frame));
            VideoFrameBuffer? dropped = null;

            lock (_lock)
            {
                if (_dropStrategy != FrameDropStrategy.Unbounded && _frameQueue.Count >= _maxQueueCapacity)
                {
                    if (_dropStrategy == FrameDropStrategy.DropNewest)
                    {
                        _droppedFramesCount++;
                        dropped = frame;
                    }
                    else if (_dropStrategy == FrameDropStrategy.DropOldest)
                    {
                        _droppedFramesCount++;
                        dropped = _frameQueue.Dequeue();
                        _frameQueue.Enqueue(frame);
                    }
                }
                else
                {
                    _frameQueue.Enqueue(frame);
                }
            }

            if (dropped != null)
            {
                FrameDropped?.Invoke(this, dropped);
                dropped.Dispose();
                return false;
            }

            return true;
        }

        public void Play()
        {
            lock (_lock)
            {
                if (_state == PlaybackState.Playing) return;

                if (_state == PlaybackState.Paused)
                {
                    Clock.Resume();
                }
                else
                {
                    Clock.Start();
                }

                _state = PlaybackState.Playing;
                StateChanged?.Invoke(this, _state);
            }
        }

        public void Pause()
        {
            lock (_lock)
            {
                if (_state != PlaybackState.Playing) return;

                Clock.Pause();
                _state = PlaybackState.Paused;
                StateChanged?.Invoke(this, _state);
            }
        }

        public void Stop()
        {
            lock (_lock)
            {
                Clock.Stop();
                while (_frameQueue.Count > 0)
                {
                    var f = _frameQueue.Dequeue();
                    f.Dispose();
                }
                _state = PlaybackState.Stopped;
                StateChanged?.Invoke(this, _state);
            }
        }

        public void Seek(TimeSpan position)
        {
            lock (_lock)
            {
                Clock.Seek(position);
            }
        }

        /// <summary>
        /// Advances playback by presenting the next queued frame immediately.
        /// </summary>
        public bool StepNextFrame()
        {
            VideoFrameBuffer? nextFrame = null;
            lock (_lock)
            {
                if (_frameQueue.Count > 0)
                {
                    nextFrame = _frameQueue.Dequeue();
                    _currentFrame = nextFrame;
                }
            }

            if (nextFrame != null)
            {
                FrameReady?.Invoke(this, nextFrame);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Synchronizes queued frames against the current master clock presentation time.
        /// Returns true if a new frame was presented.
        /// </summary>
        public bool Update()
        {
            if (_state != PlaybackState.Playing) return false;

            TimeSpan currentPts = Clock.Position;
            VideoFrameBuffer? frameToPresent = null;

            lock (_lock)
            {
                while (_frameQueue.Count > 0)
                {
                    var peek = _frameQueue.Peek();
                    if (peek.PresentationTime <= currentPts)
                    {
                        frameToPresent = _frameQueue.Dequeue();
                        _currentFrame = frameToPresent;
                    }
                    else
                    {
                        break;
                    }
                }
            }

            if (frameToPresent != null)
            {
                FrameReady?.Invoke(this, frameToPresent);
                return true;
            }

            return false;
        }
    }
}
