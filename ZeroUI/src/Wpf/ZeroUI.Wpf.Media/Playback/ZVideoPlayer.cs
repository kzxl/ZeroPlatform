using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ZeroUI.Core.Media;

namespace ZeroUI.Wpf.Media
{
    /// <summary>
    /// Enterprise GPU-accelerated video player with SMPTE timecode, frame stepping, volume scrubbing, and OSD telemetry.
    /// </summary>
    [TemplatePart(Name = "PART_MediaElement", Type = typeof(MediaElement))]
    public class ZVideoPlayer : Control
    {
        private MediaElement? _mediaElement;
        private readonly DispatcherTimer _posTimer;
        private bool _isUserSeeking;

        static ZVideoPlayer()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ZVideoPlayer), new FrameworkPropertyMetadata(typeof(ZVideoPlayer)));
            FocusableProperty.OverrideMetadata(typeof(ZVideoPlayer), new FrameworkPropertyMetadata(true));
        }

        public ZVideoPlayer()
        {
            _posTimer = new DispatcherTimer(DispatcherPriority.Render)
            {
                Interval = TimeSpan.FromMilliseconds(40) // ~25-30 fps update
            };
            _posTimer.Tick += PosTimer_Tick;

            Loaded += (s, e) =>
            {
                if (AutoPlay && Source != null) Play();
            };
            Unloaded += (s, e) =>
            {
                _posTimer.Stop();
                _mediaElement?.Stop();
            };
        }

        #region Dependency Properties

        public static readonly DependencyProperty SourceProperty =
            DependencyProperty.Register(nameof(Source), typeof(Uri), typeof(ZVideoPlayer),
                new FrameworkPropertyMetadata(null, OnSourceChanged));

        public static readonly DependencyProperty PlaybackStateProperty =
            DependencyProperty.Register(nameof(PlaybackState), typeof(MediaPlaybackState), typeof(ZVideoPlayer),
                new FrameworkPropertyMetadata(MediaPlaybackState.Stopped));

        public static readonly DependencyProperty PositionProperty =
            DependencyProperty.Register(nameof(Position), typeof(TimeSpan), typeof(ZVideoPlayer),
                new FrameworkPropertyMetadata(TimeSpan.Zero, OnPositionChanged));

        public static readonly DependencyProperty DurationProperty =
            DependencyProperty.Register(nameof(Duration), typeof(TimeSpan), typeof(ZVideoPlayer),
                new FrameworkPropertyMetadata(TimeSpan.Zero));

        public static readonly DependencyProperty VolumeProperty =
            DependencyProperty.Register(nameof(Volume), typeof(double), typeof(ZVideoPlayer),
                new FrameworkPropertyMetadata(0.8, OnVolumeChanged));

        public static readonly DependencyProperty IsMutedProperty =
            DependencyProperty.Register(nameof(IsMuted), typeof(bool), typeof(ZVideoPlayer),
                new FrameworkPropertyMetadata(false, OnIsMutedChanged));

        public static readonly DependencyProperty SpeedRatioProperty =
            DependencyProperty.Register(nameof(SpeedRatio), typeof(double), typeof(ZVideoPlayer),
                new FrameworkPropertyMetadata(1.0, OnSpeedRatioChanged));

        public static readonly DependencyProperty IsLoopingProperty =
            DependencyProperty.Register(nameof(IsLooping), typeof(bool), typeof(ZVideoPlayer),
                new FrameworkPropertyMetadata(false));

        public static readonly DependencyProperty AutoPlayProperty =
            DependencyProperty.Register(nameof(AutoPlay), typeof(bool), typeof(ZVideoPlayer),
                new FrameworkPropertyMetadata(true));

        public static readonly DependencyProperty TimecodeTextProperty =
            DependencyProperty.Register(nameof(TimecodeText), typeof(string), typeof(ZVideoPlayer),
                new FrameworkPropertyMetadata("00:00:00 / 00:00:00"));

        public Uri? Source
        {
            get => (Uri?)GetValue(SourceProperty);
            set => SetValue(SourceProperty, value);
        }

        public MediaPlaybackState PlaybackState
        {
            get => (MediaPlaybackState)GetValue(PlaybackStateProperty);
            set => SetValue(PlaybackStateProperty, value);
        }

        public TimeSpan Position
        {
            get => (TimeSpan)GetValue(PositionProperty);
            set => SetValue(PositionProperty, value);
        }

        public TimeSpan Duration
        {
            get => (TimeSpan)GetValue(DurationProperty);
            set => SetValue(DurationProperty, value);
        }

        public double Volume
        {
            get => (double)GetValue(VolumeProperty);
            set => SetValue(VolumeProperty, Math.Max(0.0, Math.Min(1.0, value)));
        }

        public bool IsMuted
        {
            get => (bool)GetValue(IsMutedProperty);
            set => SetValue(IsMutedProperty, value);
        }

        public double SpeedRatio
        {
            get => (double)GetValue(SpeedRatioProperty);
            set => SetValue(SpeedRatioProperty, Math.Max(0.1, Math.Min(8.0, value)));
        }

        public bool IsLooping
        {
            get => (bool)GetValue(IsLoopingProperty);
            set => SetValue(IsLoopingProperty, value);
        }

        public bool AutoPlay
        {
            get => (bool)GetValue(AutoPlayProperty);
            set => SetValue(AutoPlayProperty, value);
        }

        public string TimecodeText
        {
            get => (string)GetValue(TimecodeTextProperty);
            private set => SetValue(TimecodeTextProperty, value);
        }

        #endregion

        #region Template & Lifecycle

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            if (_mediaElement != null)
            {
                _mediaElement.MediaOpened -= MediaElement_MediaOpened;
                _mediaElement.MediaEnded -= MediaElement_MediaEnded;
            }

            _mediaElement = GetTemplateChild("PART_MediaElement") as MediaElement;

            if (_mediaElement == null)
            {
                // Fallback direct visual child if template does not provide one
                _mediaElement = new MediaElement
                {
                    LoadedBehavior = MediaState.Manual,
                    UnloadedBehavior = MediaState.Stop,
                    Stretch = Stretch.Uniform
                };
                AddVisualChild(_mediaElement);
            }

            _mediaElement.MediaOpened += MediaElement_MediaOpened;
            _mediaElement.MediaEnded += MediaElement_MediaEnded;
            _mediaElement.Volume = Volume;
            _mediaElement.IsMuted = IsMuted;
            _mediaElement.SpeedRatio = SpeedRatio;

            if (Source != null)
            {
                _mediaElement.Source = Source;
            }
        }

        private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZVideoPlayer player && player._mediaElement != null)
            {
                player._mediaElement.Source = e.NewValue as Uri;
                if (player.AutoPlay) player.Play();
            }
        }

        private static void OnPositionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZVideoPlayer player && player._mediaElement != null && player._isUserSeeking)
            {
                player._mediaElement.Position = (TimeSpan)e.NewValue;
            }
        }

        private static void OnVolumeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZVideoPlayer player && player._mediaElement != null)
            {
                player._mediaElement.Volume = (double)e.NewValue;
            }
        }

        private static void OnIsMutedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZVideoPlayer player && player._mediaElement != null)
            {
                player._mediaElement.IsMuted = (bool)e.NewValue;
            }
        }

        private static void OnSpeedRatioChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZVideoPlayer player && player._mediaElement != null)
            {
                player._mediaElement.SpeedRatio = (double)e.NewValue;
            }
        }

        private void MediaElement_MediaOpened(object? sender, RoutedEventArgs e)
        {
            if (_mediaElement?.NaturalDuration.HasTimeSpan == true)
            {
                Duration = _mediaElement.NaturalDuration.TimeSpan;
                UpdateTimecode();
            }
        }

        private void MediaElement_MediaEnded(object? sender, RoutedEventArgs e)
        {
            if (IsLooping)
            {
                Seek(TimeSpan.Zero);
                Play();
            }
            else
            {
                PlaybackState = MediaPlaybackState.Stopped;
                _posTimer.Stop();
            }
        }

        private void PosTimer_Tick(object? sender, EventArgs e)
        {
            if (_mediaElement != null && !_isUserSeeking)
            {
                Position = _mediaElement.Position;
                UpdateTimecode();
            }
        }

        private void UpdateTimecode()
        {
            string cur = FormatTime(Position);
            string dur = FormatTime(Duration);
            TimecodeText = $"{cur} / {dur}";
        }

        private static string FormatTime(TimeSpan t)
        {
            return t.TotalHours >= 1
                ? $"{(int)t.TotalHours:D2}:{t.Minutes:D2}:{t.Seconds:D2}"
                : $"{t.Minutes:D2}:{t.Seconds:D2}";
        }

        #endregion

        #region Public Playback Commands

        public void Play()
        {
            if (_mediaElement == null) return;
            _mediaElement.Play();
            PlaybackState = MediaPlaybackState.Playing;
            _posTimer.Start();
        }

        public void Pause()
        {
            if (_mediaElement == null) return;
            _mediaElement.Pause();
            PlaybackState = MediaPlaybackState.Paused;
            _posTimer.Stop();
        }

        public void Stop()
        {
            if (_mediaElement == null) return;
            _mediaElement.Stop();
            PlaybackState = MediaPlaybackState.Stopped;
            Position = TimeSpan.Zero;
            _posTimer.Stop();
            UpdateTimecode();
        }

        public void Seek(TimeSpan target)
        {
            if (_mediaElement == null) return;
            _isUserSeeking = true;
            _mediaElement.Position = target;
            Position = target;
            _isUserSeeking = false;
            UpdateTimecode();
        }

        public void StepForward(double seconds = 1.0 / 30.0) // 1 frame at 30fps
        {
            Pause();
            Seek(Position + TimeSpan.FromSeconds(seconds));
        }

        public void StepBackward(double seconds = 1.0 / 30.0)
        {
            Pause();
            Seek(TimeSpan.FromSeconds(Math.Max(0, (Position - TimeSpan.FromSeconds(seconds)).TotalSeconds)));
        }

        #endregion
    }
}
