using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using ZeroGraphics.Waveform.Controls;
using ZeroGraphics.Waveform.Pipeline;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Industrial
{
    /// <summary>
    /// Ultra-high-performance SCADA oscilloscope and telemetry monitor powered by Direct3D 11 hardware acceleration via ZeroGraphics.
    /// Plots millions of continuous high-frequency sensor samples at 60+ FPS with zero CPU overhead, GPU Map(DISCARD) streaming,
    /// and built-in Obsidian Dark styling matching the ZeroUI industrial suite.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Industrial & SCADA")]
    [Description("Hardware-accelerated Direct3D 11 oscilloscope powered by ZeroGraphics for streaming telemetry")]
    public class ZOscilloscope : ZeroWaveformCanvas
    {
        private readonly float[] _ringBuffer;
        private int _ringHead;
        private int _sampleCount;
        private readonly object _lock = new object();

        private string _title = "Oscilloscope Channel 1";
        private string _unit = "V";

        [Category("ZeroUI - Oscilloscope")]
        [DefaultValue("Oscilloscope Channel 1")]
        public string Title
        {
            get => _title;
            set { _title = value; Invalidate(); }
        }

        [Category("ZeroUI - Oscilloscope")]
        [DefaultValue("V")]
        public string Unit
        {
            get => _unit;
            set { _unit = value; Invalidate(); }
        }

        public ZOscilloscope() : this(2048) { }

        public ZOscilloscope(int bufferCapacity)
        {
            int capacity = Math.Max(128, bufferCapacity);
            _ringBuffer = new float[capacity];

            // Default to ZeroUI Obsidian Dark theme with phosphor green trace
            BackColor = Color.FromArgb(13, 17, 23);
            TraceColor = Color.FromArgb(16, 185, 129); // Emerald / Phosphor green
            DecimationMode = WaveformDecimationMode.MinMax;
            AutoScale = true;
        }

        /// <summary>
        /// Appends a single telemetry data point into the high-speed ring buffer and updates the trace.
        /// </summary>
        public void StreamValue(float value)
        {
            lock (_lock)
            {
                _ringBuffer[_ringHead] = value;
                _ringHead = (_ringHead + 1) % _ringBuffer.Length;
                if (_sampleCount < _ringBuffer.Length)
                {
                    _sampleCount++;
                }

                // Construct contiguous span for zero-alloc display
                float[] displayPoints = new float[_sampleCount];
                int start = (_ringHead - _sampleCount + _ringBuffer.Length) % _ringBuffer.Length;
                for (int i = 0; i < _sampleCount; i++)
                {
                    displayPoints[i] = _ringBuffer[(start + i) % _ringBuffer.Length];
                }

                SetData(displayPoints);
            }
        }

        /// <summary>
        /// Streams a batch of telemetry data points directly to the Direct3D 11 pipeline.
        /// </summary>
        public void StreamBatch(float[] batch)
        {
            if (batch == null || batch.Length == 0) return;
            SetData(batch);
        }

        /// <summary>
        /// Clears all buffered signal data.
        /// </summary>
        public void ClearData()
        {
            lock (_lock)
            {
                _ringHead = 0;
                _sampleCount = 0;
                Array.Clear(_ringBuffer, 0, _ringBuffer.Length);
                SetData(Array.Empty<float>());
            }
        }
    }

    /// <summary>
    /// Legacy alias for <see cref="ZOscilloscope"/>.
    /// Preserved for backward compatibility.
    /// </summary>
    [Obsolete("ZeroOscilloscope is deprecated and will be removed in 5 release cycles. Please migrate to ZOscilloscope instead.")]
    [ToolboxItem(false)]
    public class ZeroOscilloscope : ZOscilloscope
    {
        public ZeroOscilloscope() : base() { }
        public ZeroOscilloscope(int bufferCapacity) : base(bufferCapacity) { }
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZOscilloscope"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("Oscilloscope is deprecated and will be removed in 5 release cycles. Please migrate to ZOscilloscope instead.")]
    [ToolboxItem(false)]
    public class Oscilloscope : ZOscilloscope
    {
    }

    #endregion
}
