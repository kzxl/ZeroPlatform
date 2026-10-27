using System;
using System.Diagnostics;
using System.Windows.Media;
using ZeroUI.Core.Rendering.Optimizer;

namespace ZeroUI.Wpf.Rendering.Optimizer
{
    /// <summary>
    /// Ambient frame-budget provider for WPF applications.
    /// Hooks into WPF's <see cref="CompositionTarget.Rendering"/> pipeline and drives
    /// the underlying <see cref="ZeroAdaptiveRenderMonitor"/> to throttle or promote rendering fidelity.
    /// </summary>
    public sealed class ZeroWpfRenderMonitor : IDisposable
    {
        private static readonly Lazy<ZeroWpfRenderMonitor> _instance =
            new Lazy<ZeroWpfRenderMonitor>(() => new ZeroWpfRenderMonitor());

        public static ZeroWpfRenderMonitor Instance => _instance.Value;

        private readonly ZeroAdaptiveRenderMonitor _coreMonitor;
        private long _lastTimestamp;
        private bool _isHooked;
        private bool _isDisposed;

        public ZeroAdaptiveRenderMonitor CoreMonitor => _coreMonitor;

        public double CurrentFps => _coreMonitor.CurrentFps;
        public double RollingAverageFrameTimeMs => _coreMonitor.RollingAverageFrameTimeMs;
        public RenderFidelityTier CurrentFidelity => _coreMonitor.CurrentFidelity;

        private ZeroWpfRenderMonitor()
        {
            _coreMonitor = new ZeroAdaptiveRenderMonitor(targetFps: 60.0);
            Start();
        }

        public void Start()
        {
            if (_isHooked) return;
            _lastTimestamp = Stopwatch.GetTimestamp();
            CompositionTarget.Rendering += OnRendering;
            _isHooked = true;
        }

        public void Stop()
        {
            if (!_isHooked) return;
            CompositionTarget.Rendering -= OnRendering;
            _isHooked = false;
        }

        private void OnRendering(object? sender, EventArgs e)
        {
            long now = Stopwatch.GetTimestamp();
            long elapsedTicks = now - _lastTimestamp;
            _lastTimestamp = now;

            double frameTimeMs = (elapsedTicks * 1000.0) / Stopwatch.Frequency;
            if (frameTimeMs > 0.01 && frameTimeMs < 500.0)
            {
                _coreMonitor.RecordFrameTime(frameTimeMs);
            }
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            Stop();
        }
    }
}
