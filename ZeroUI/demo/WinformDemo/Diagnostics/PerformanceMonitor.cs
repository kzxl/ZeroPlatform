using System;
using System.Diagnostics;

namespace ZeroUI.Samples.WinformDemo.Diagnostics
{
    public sealed class PerformanceMonitor
    {
        private readonly Stopwatch _fpsStopwatch = new Stopwatch();
        private int _frameCount = 0;
        private double _currentFps = 0.0;
        private double _lastFrameMs = 0.0;

        public PerformanceMonitor()
        {
            _fpsStopwatch.Start();
        }

        public double CurrentFps => _currentFps;
        public double LastFrameMs => _lastFrameMs;

        public double ProcessRamMb
        {
            get
            {
                using var p = Process.GetCurrentProcess();
                return p.WorkingSet64 / (1024.0 * 1024.0);
            }
        }

        public int Gen0Count => GC.CollectionCount(0);
        public int Gen1Count => GC.CollectionCount(1);
        public int Gen2Count => GC.CollectionCount(2);

        public void RecordFrame(double frameDurationMs)
        {
            _lastFrameMs = frameDurationMs;
            _frameCount++;

            if (_fpsStopwatch.ElapsedMilliseconds >= 500)
            {
                _currentFps = (_frameCount * 1000.0) / _fpsStopwatch.ElapsedMilliseconds;
                _frameCount = 0;
                _fpsStopwatch.Restart();
            }
        }
    }
}
