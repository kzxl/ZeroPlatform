using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using ZeroGraphics.DirectX.Core;
using ZeroGraphics.Waveform.Pipeline;

namespace ZeroUI.Wpf.Rendering
{
    /// <summary>
    /// High-performance GPU Canvas control for WPF, powered by ZeroD3D11Bridge and ZeroGraphics WaveformPipeline.
    /// Hosts a D3DImage back buffer connected via shared Direct3D 11 texture handles.
    /// Supports streaming up to 10 million points with real-time MinMax/LTTB decimation at 60/120/144 FPS.
    /// Automatically manages high-DPI scaling, front-buffer loss recovery, and zero-allocation updates.
    /// </summary>
    public class ZD3DCanvas : Image, IDisposable
    {
        private readonly D3DImage _d3dImage;
        private readonly ZeroD3D11Bridge _bridge;
        private readonly Stopwatch _fpsStopwatch = new Stopwatch();
        private WaveformPipeline? _waveformPipeline;
        private float[]? _dataPoints;
        private float[]? _demoBuffer;

        private int _frameCount;
        private double _animPhase = 0.0;
        private double _mouseX;
        private double _mouseY;
        private bool _isMouseOver;
        private bool _isDisposed;

        public static readonly DependencyProperty IsRenderingActiveProperty =
            DependencyProperty.Register(
                nameof(IsRenderingActive),
                typeof(bool),
                typeof(ZD3DCanvas),
                new PropertyMetadata(true, OnIsRenderingActiveChanged));

        public static readonly DependencyProperty EnableWaveformDemoProperty =
            DependencyProperty.Register(
                nameof(EnableWaveformDemo),
                typeof(bool),
                typeof(ZD3DCanvas),
                new PropertyMetadata(true));

        public static readonly DependencyProperty TraceColorProperty =
            DependencyProperty.Register(
                nameof(TraceColor),
                typeof(Color),
                typeof(ZD3DCanvas),
                new PropertyMetadata(Color.FromRgb(0x00, 0xE5, 0xFF))); // High-visibility neon cyan

        public static readonly DependencyProperty MinYProperty =
            DependencyProperty.Register(
                nameof(MinY),
                typeof(float),
                typeof(ZD3DCanvas),
                new PropertyMetadata(-1.0f));

        public static readonly DependencyProperty MaxYProperty =
            DependencyProperty.Register(
                nameof(MaxY),
                typeof(float),
                typeof(ZD3DCanvas),
                new PropertyMetadata(1.0f));

        public static readonly DependencyProperty AutoScaleProperty =
            DependencyProperty.Register(
                nameof(AutoScale),
                typeof(bool),
                typeof(ZD3DCanvas),
                new PropertyMetadata(false));

        public static readonly DependencyProperty DecimationModeProperty =
            DependencyProperty.Register(
                nameof(DecimationMode),
                typeof(WaveformDecimationMode),
                typeof(ZD3DCanvas),
                new PropertyMetadata(WaveformDecimationMode.MinMax));

        public bool IsRenderingActive
        {
            get => (bool)GetValue(IsRenderingActiveProperty);
            set => SetValue(IsRenderingActiveProperty, value);
        }

        public bool EnableWaveformDemo
        {
            get => (bool)GetValue(EnableWaveformDemoProperty);
            set => SetValue(EnableWaveformDemoProperty, value);
        }

        public Color TraceColor
        {
            get => (Color)GetValue(TraceColorProperty);
            set => SetValue(TraceColorProperty, value);
        }

        public float MinY
        {
            get => (float)GetValue(MinYProperty);
            set => SetValue(MinYProperty, value);
        }

        public float MaxY
        {
            get => (float)GetValue(MaxYProperty);
            set => SetValue(MaxYProperty, value);
        }

        public bool AutoScale
        {
            get => (bool)GetValue(AutoScaleProperty);
            set => SetValue(AutoScaleProperty, value);
        }

        public WaveformDecimationMode DecimationMode
        {
            get => (WaveformDecimationMode)GetValue(DecimationModeProperty);
            set => SetValue(DecimationModeProperty, value);
        }

        public double CurrentFps { get; private set; }

        /// <summary>
        /// Hook for custom Direct3D 11 GPU rendering pass.
        /// Receives the active bridge with D3D11Device, D3D11Context, RTV, pixel width, and pixel height.
        /// </summary>
        public event Action<ZeroD3D11Bridge, int, int>? RenderFrame;

        public ZeroD3D11Bridge Bridge => _bridge;

        public ZD3DCanvas()
        {
            _d3dImage = new D3DImage();
            Source = _d3dImage;
            Stretch = Stretch.Fill;

            _bridge = new ZeroD3D11Bridge();

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            SizeChanged += OnSizeChanged;
            _d3dImage.IsFrontBufferAvailableChanged += OnIsFrontBufferAvailableChanged;

            D3D11DeviceManager.DeviceRestored += OnDeviceRestored;

            MouseMove += (s, e) => { var p = e.GetPosition(this); _mouseX = p.X; _mouseY = p.Y; };
            MouseEnter += (s, e) => _isMouseOver = true;
            MouseLeave += (s, e) => _isMouseOver = false;
        }

        private static void OnIsRenderingActiveChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZD3DCanvas canvas && canvas.IsLoaded)
            {
                canvas.UpdateRenderingSubscription();
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            UpdateRenderingSubscription();
            RecreateSurface();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            CompositionTarget.Rendering -= OnRendering;
        }

        private void OnDeviceRestored()
        {
            _waveformPipeline?.Dispose();
            _waveformPipeline = null;
        }

        private void UpdateRenderingSubscription()
        {
            CompositionTarget.Rendering -= OnRendering;
            if (IsRenderingActive && IsLoaded)
            {
                _fpsStopwatch.Restart();
                _frameCount = 0;
                CompositionTarget.Rendering += OnRendering;
            }
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            RecreateSurface();
        }

        private void OnIsFrontBufferAvailableChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (_d3dImage.IsFrontBufferAvailable)
            {
                RecreateSurface();
            }
        }

        private void RecreateSurface()
        {
            if (!IsLoaded || ActualWidth <= 0 || ActualHeight <= 0) return;

            var dpi = VisualTreeHelper.GetDpi(this);
            int pixelWidth = Math.Max(1, (int)Math.Ceiling(ActualWidth * dpi.DpiScaleX));
            int pixelHeight = Math.Max(1, (int)Math.Ceiling(ActualHeight * dpi.DpiScaleY));

            try
            {
                _bridge.EnsureSurfaceSize(pixelWidth, pixelHeight);
                RenderCurrentFrame();
            }
            catch
            {
                // Graceful degradation
            }
        }

        private void OnRendering(object? sender, EventArgs e)
        {
            if (!_d3dImage.IsFrontBufferAvailable || ActualWidth <= 0 || ActualHeight <= 0) return;

            RenderCurrentFrame();

            // FPS Measurement
            _frameCount++;
            if (_fpsStopwatch.ElapsedMilliseconds >= 500)
            {
                CurrentFps = _frameCount * 1000.0 / _fpsStopwatch.ElapsedMilliseconds;
                _frameCount = 0;
                _fpsStopwatch.Restart();
            }
        }

        /// <summary>
        /// Sets the telemetry or waveform data points array to be rendered on the GPU.
        /// </summary>
        public void SetData(float[]? data)
        {
            _dataPoints = data;
            if (!IsRenderingActive)
            {
                RenderCurrentFrame();
            }
        }

        /// <summary>
        /// Sets data points from a read-only span with minimal allocation.
        /// </summary>
        public void SetData(ReadOnlySpan<float> data)
        {
            if (_dataPoints == null || _dataPoints.Length != data.Length)
            {
                _dataPoints = new float[data.Length];
            }
            data.CopyTo(_dataPoints);
            if (!IsRenderingActive)
            {
                RenderCurrentFrame();
            }
        }

        /// <summary>
        /// Executes a single GPU frame render pass and presents it to WPF.
        /// </summary>
        public void RenderCurrentFrame()
        {
            if (!_bridge.IsInitialized) return;

            int w = _bridge.PixelWidth;
            int h = _bridge.PixelHeight;
            if (w <= 0 || h <= 0) return;

            if (RenderFrame != null)
            {
                RenderFrame.Invoke(_bridge, w, h);
            }
            else
            {
                var rtv = _bridge.GpuRenderTargetView;
                if (rtv != null && rtv.IsValid)
                {
                    _waveformPipeline ??= new WaveformPipeline(_bridge.GpuDevice, _bridge.GpuContext);

                    // Industrial dark navy viewport background
                    _bridge.Clear(0.04f, 0.05f, 0.09f, 1.0f);

                    var traceColor = TraceColor;
                    var sysDrawingColor = System.Drawing.Color.FromArgb(traceColor.A, traceColor.R, traceColor.G, traceColor.B);

                    if (_dataPoints != null && _dataPoints.Length > 1)
                    {
                        float minY = MinY;
                        float maxY = MaxY;
                        if (AutoScale)
                        {
                            CalculateBounds(_dataPoints, out minY, out maxY);
                        }

                        _waveformPipeline.RenderWaveform(
                            rtv,
                            w,
                            h,
                            _dataPoints,
                            minY,
                            maxY,
                            sysDrawingColor,
                            DecimationMode);
                    }
                    else if (EnableWaveformDemo)
                    {
                        RenderDemoWaveform(rtv, w, h, sysDrawingColor);
                    }
                }
                else
                {
                    _bridge.Clear(0.04f, 0.05f, 0.09f, 1.0f);
                }
            }

            _bridge.Flush();
            _bridge.PresentTo(_d3dImage);
        }

        private void RenderDemoWaveform(D3D11RenderTargetView rtv, int w, int h, System.Drawing.Color traceColor)
        {
            if (_waveformPipeline == null) return;

            _animPhase += 0.04;
            float mx = _isMouseOver && ActualWidth > 0 ? (float)(_mouseX / ActualWidth) : 0.5f;
            float my = _isMouseOver && ActualHeight > 0 ? (float)(_mouseY / ActualHeight) : 0.5f;

            if (_demoBuffer == null || _demoBuffer.Length != 2048)
            {
                _demoBuffer = new float[2048];
            }

            float freq1 = 2.0f + mx * 5.0f;
            float freq2 = 7.0f + (1.0f - my) * 8.0f;
            float phase = (float)_animPhase;

            for (int i = 0; i < _demoBuffer.Length; i++)
            {
                float t = (float)i / (_demoBuffer.Length - 1);
                float s1 = (float)Math.Sin(t * Math.PI * 2.0 * freq1 + phase);
                float s2 = (float)(0.38 * Math.Sin(t * Math.PI * 2.0 * freq2 + phase * 1.6));
                float gaussian = (float)(0.25 * Math.Sin(t * Math.PI * 24.0 + phase * 3.0) * Math.Exp(-Math.Pow((t - 0.5) * 6.0, 2.0)));
                _demoBuffer[i] = s1 + s2 + gaussian;
            }

            _waveformPipeline.RenderWaveform(
                rtv,
                w,
                h,
                _demoBuffer,
                -1.8f,
                1.8f,
                traceColor,
                DecimationMode);
        }

        private static void CalculateBounds(float[] data, out float minY, out float maxY)
        {
            minY = float.MaxValue;
            maxY = float.MinValue;
            for (int i = 0; i < data.Length; i++)
            {
                float val = data[i];
                if (val < minY) minY = val;
                if (val > maxY) maxY = val;
            }

            if (minY >= maxY)
            {
                minY -= 1.0f;
                maxY += 1.0f;
            }
            else
            {
                float margin = (maxY - minY) * 0.05f;
                minY -= margin;
                maxY += margin;
            }
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            CompositionTarget.Rendering -= OnRendering;
            _d3dImage.IsFrontBufferAvailableChanged -= OnIsFrontBufferAvailableChanged;
            D3D11DeviceManager.DeviceRestored -= OnDeviceRestored;

            _waveformPipeline?.Dispose();
            _waveformPipeline = null;
            _bridge.Dispose();
        }
    }

    /// <summary>
    /// Backward-compatibility alias for <see cref="ZD3DCanvas"/>.
    /// </summary>
    [Obsolete("D3DCanvas is deprecated and will be removed in 5 release cycles. Please migrate to ZD3DCanvas instead.")]
    public class D3DCanvas : ZD3DCanvas
    {
    }

    /// <summary>
    /// Legacy alias for <see cref="ZD3DCanvas"/>.
    /// </summary>
    [Obsolete("ZeroD3DCanvas is deprecated and will be removed in 5 release cycles. Please migrate to ZD3DCanvas instead.")]
    public class ZeroD3DCanvas : ZD3DCanvas
    {
    }
}

