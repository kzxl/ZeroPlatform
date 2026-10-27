using System;

namespace ZeroUI.Core.Rendering.Optimizer
{
    /// <summary>
    /// Real-time frame budget monitor and adaptive fidelity controller.
    /// Tracks rendering frame time against the 16.6ms (60 FPS) or 6.9ms (144 FPS) budget,
    /// automatically throttling shader fidelity to maintain stutter-free responsiveness.
    /// </summary>
    public sealed class ZeroAdaptiveRenderMonitor
    {
        private const int HistoryBufferSize = 60;
        private readonly double[] _frameTimeHistory = new double[HistoryBufferSize];
        private int _historyIndex;
        private int _historyCount;
        private double _historySum;

        private int _consecutiveViolations;
        private int _consecutiveHeadroom;

        private RenderFidelityTier _currentFidelity = RenderFidelityTier.Ultra;
        private double _targetFrameBudgetMs = 16.67; // Default 60 FPS target

        public double TargetFrameBudgetMs
        {
            get => _targetFrameBudgetMs;
            set => _targetFrameBudgetMs = Math.Max(1.0, value);
        }

        public RenderFidelityTier CurrentFidelity => _currentFidelity;

        public double RollingAverageFrameTimeMs { get; private set; } = 8.0;

        public double CurrentFps => RollingAverageFrameTimeMs > 0.001
            ? Math.Min(240.0, 1000.0 / RollingAverageFrameTimeMs)
            : 60.0;

        public long TotalFramesRecorded { get; private set; }
        public long TotalViolations { get; private set; }

        public double ViolationRatePercentage => TotalFramesRecorded > 0
            ? (double)TotalViolations * 100.0 / TotalFramesRecorded
            : 0.0;

        /// <summary>
        /// Fires when the rendering fidelity tier dynamically changes.
        /// </summary>
        public event Action<RenderFidelityTier>? FidelityTierChanged;

        public ZeroAdaptiveRenderMonitor(double targetFps = 60.0)
        {
            TargetFrameBudgetMs = 1000.0 / Math.Max(10.0, targetFps);
        }

        /// <summary>
        /// Records the duration of a rendered frame (in milliseconds) and updates adaptive state.
        /// Zero heap allocation per call.
        /// </summary>
        public void RecordFrameTime(double frameTimeMs)
        {
            TotalFramesRecorded++;

            // Ring buffer rolling sum
            if (_historyCount < HistoryBufferSize)
            {
                _frameTimeHistory[_historyIndex] = frameTimeMs;
                _historySum += frameTimeMs;
                _historyCount++;
            }
            else
            {
                _historySum -= _frameTimeHistory[_historyIndex];
                _frameTimeHistory[_historyIndex] = frameTimeMs;
                _historySum += frameTimeMs;
            }

            _historyIndex = (_historyIndex + 1) % HistoryBufferSize;
            RollingAverageFrameTimeMs = _historySum / _historyCount;

            // Evaluate budget violation with VSync jitter tolerance (+15%)
            bool isViolating = frameTimeMs > (_targetFrameBudgetMs * 1.15);
            if (isViolating)
            {
                TotalViolations++;
                _consecutiveViolations++;
                _consecutiveHeadroom = 0;

                // Degrade tier if 8 consecutive frames violate target budget
                if (_consecutiveViolations >= 8)
                {
                    DegradeFidelity();
                    _consecutiveViolations = 0;
                }
            }
            else
            {
                _consecutiveViolations = 0;
                // Headroom condition: Frame time is less than 85% of budget
                if (frameTimeMs < _targetFrameBudgetMs * 0.85)
                {
                    _consecutiveHeadroom++;
                    // Promote tier if 45 consecutive frames maintain healthy headroom
                    if (_consecutiveHeadroom >= 45)
                    {
                        PromoteFidelity();
                        _consecutiveHeadroom = 0;
                    }
                }
                else
                {
                    _consecutiveHeadroom = 0;
                }
            }
        }

        /// <summary>
        /// Manually forces a specific fidelity tier (useful for benchmarks and testing).
        /// </summary>
        public void SetFidelity(RenderFidelityTier tier)
        {
            if (_currentFidelity != tier)
            {
                _currentFidelity = tier;
                FidelityTierChanged?.Invoke(_currentFidelity);
            }
        }

        private void DegradeFidelity()
        {
            if (_currentFidelity == RenderFidelityTier.Ultra)
            {
                _currentFidelity = RenderFidelityTier.Balanced;
                FidelityTierChanged?.Invoke(_currentFidelity);
            }
            else if (_currentFidelity == RenderFidelityTier.Balanced)
            {
                _currentFidelity = RenderFidelityTier.PowerSaver;
                FidelityTierChanged?.Invoke(_currentFidelity);
            }
        }

        private void PromoteFidelity()
        {
            if (_currentFidelity == RenderFidelityTier.PowerSaver)
            {
                _currentFidelity = RenderFidelityTier.Balanced;
                FidelityTierChanged?.Invoke(_currentFidelity);
            }
            else if (_currentFidelity == RenderFidelityTier.Balanced)
            {
                _currentFidelity = RenderFidelityTier.Ultra;
                FidelityTierChanged?.Invoke(_currentFidelity);
            }
        }

        /// <summary>
        /// Resets statistics and returns to Ultra fidelity.
        /// </summary>
        public void Reset()
        {
            _historyCount = 0;
            _historyIndex = 0;
            _historySum = 0;
            _consecutiveViolations = 0;
            _consecutiveHeadroom = 0;
            TotalFramesRecorded = 0;
            TotalViolations = 0;
            _currentFidelity = RenderFidelityTier.Ultra;
            RollingAverageFrameTimeMs = 8.0;
        }
    }
}
