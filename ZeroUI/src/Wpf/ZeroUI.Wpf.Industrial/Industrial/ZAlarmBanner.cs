using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ZeroUI.Core.Rendering;
using ZeroUI.Core.Scada;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Industrial
{
    /// <summary>
    /// ISA-18.2 compliant industrial top/bottom alarm banner ticker for ZeroUI WPF.
    /// Shows real-time highest-priority alarm, severity counts, and quick operator actions.
    /// </summary>
    public class ZAlarmBanner : FrameworkElement
    {
        public static readonly DependencyProperty OperatorNameProperty =
            DependencyProperty.Register(nameof(OperatorName), typeof(string), typeof(ZAlarmBanner),
                new FrameworkPropertyMetadata("Operator"));

        public static readonly DependencyProperty IsSilencedProperty =
            DependencyProperty.Register(nameof(IsSilenced), typeof(bool), typeof(ZAlarmBanner),
                new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender, OnSilenceChanged));

        public string OperatorName
        {
            get => (string)GetValue(OperatorNameProperty);
            set => SetValue(OperatorNameProperty, value ?? "Operator");
        }

        public bool IsSilenced
        {
            get => (bool)GetValue(IsSilencedProperty);
            set => SetValue(IsSilencedProperty, value);
        }

        public event EventHandler<ScadaAlarmRecord>? AlarmDetailsRequested;
        public event EventHandler<bool>? SilenceStateChanged;

        private static void OnSilenceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZAlarmBanner banner)
            {
                banner.SilenceStateChanged?.Invoke(banner, (bool)e.NewValue);
            }
        }

        private bool _blinkState;
        private float _blinkTimer;
        private IDisposable? _clockToken;

        private Rect _btnAckRect;
        private Rect _btnAckAllRect;
        private Rect _btnSilenceRect;
        private Rect _contentRect;

        private static readonly Typeface SegoeBold = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
        private static readonly Typeface SegoeNormal = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

        private static readonly Brush DarkDefaultBg = Freeze(new SolidColorBrush(Color.FromRgb(15, 23, 42)));
        private static readonly Brush LightDefaultBg = Freeze(new SolidColorBrush(Color.FromRgb(241, 245, 249)));
        private static readonly Brush DarkText = Freeze(new SolidColorBrush(Color.FromRgb(248, 250, 252)));
        private static readonly Brush LightText = Freeze(new SolidColorBrush(Color.FromRgb(15, 23, 42)));
        private static readonly Brush OkGreen = Freeze(new SolidColorBrush(Color.FromRgb(34, 197, 94)));

        private static readonly Brush CritBrush = Freeze(new SolidColorBrush(Color.FromRgb(239, 68, 68)));
        private static readonly Brush HighBrush = Freeze(new SolidColorBrush(Color.FromRgb(249, 115, 22)));
        private static readonly Brush MedBrush = Freeze(new SolidColorBrush(Color.FromRgb(245, 158, 11)));
        private static readonly Brush LowBrush = Freeze(new SolidColorBrush(Color.FromRgb(59, 130, 246)));

        private static T Freeze<T>(T freezable) where T : Freezable
        {
            if (freezable.CanFreeze) freezable.Freeze();
            return freezable;
        }

        public ZAlarmBanner()
        {
            ClipToBounds = true;
            Height = 36;

            Loaded += (s, e) =>
            {
                _clockToken = ZeroAnimationClock.Subscribe(OnAnimationFrame);
                ScadaAlarmEngine.AlarmStateChanged += OnAlarmEngineChanged;
            };
            Unloaded += (s, e) =>
            {
                _clockToken?.Dispose();
                _clockToken = null;
                ScadaAlarmEngine.AlarmStateChanged -= OnAlarmEngineChanged;
            };
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnAnimationFrame(double deltaSeconds, long frameCount)
        {
            _blinkTimer += (float)deltaSeconds;
            if (_blinkTimer >= 0.5f)
            {
                _blinkTimer = 0f;
                _blinkState = !_blinkState;
                InvalidateVisual();
            }
        }

        private void OnAlarmEngineChanged(ScadaAlarmRecord record)
        {
            Dispatcher.InvokeAsync(InvalidateVisual);
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);
            Point pt = e.GetPosition(this);
            var highest = ScadaAlarmEngine.GetActiveAlarms().FirstOrDefault();

            if (_btnAckRect.Contains(pt))
            {
                if (highest != null && highest.NeedsAck)
                {
                    ScadaAlarmEngine.Acknowledge(highest.Id, OperatorName);
                    InvalidateVisual();
                }
            }
            else if (_btnAckAllRect.Contains(pt))
            {
                ScadaAlarmEngine.AcknowledgeAll(OperatorName);
                InvalidateVisual();
            }
            else if (_btnSilenceRect.Contains(pt))
            {
                IsSilenced = !IsSilenced;
            }
            else if (_contentRect.Contains(pt))
            {
                if (highest != null)
                {
                    AlarmDetailsRequested?.Invoke(this, highest);
                }
            }
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            return new Size(availableSize.Width > 0 ? availableSize.Width : 600, 36);
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            double w = ActualWidth;
            double h = ActualHeight;
            if (w <= 0 || h <= 0) return;

            bool isDark = ZeroWpfTheme.IsDark;
            var highest = ScadaAlarmEngine.GetActiveAlarms().FirstOrDefault();
            var counts = ScadaAlarmEngine.GetAlarmSummary();

            #if !NETFRAMEWORK
            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            #endif

            // Layout
            double btnW = 72;
            double btnH = 24;
            double btnY = (h - btnH) * 0.5;

            _btnSilenceRect = new Rect(w - btnW - 8, btnY, btnW, btnH);
            _btnAckAllRect = new Rect(_btnSilenceRect.Left - btnW - 6, btnY, btnW, btnH);
            _btnAckRect = new Rect(_btnAckAllRect.Left - btnW - 6, btnY, btnW, btnH);

            double contentLeft = 240;
            double contentW = Math.Max(50, _btnAckRect.Left - contentLeft - 10);
            _contentRect = new Rect(contentLeft, 0, contentW, h);

            // Background & Border
            Brush bgBrush = isDark ? DarkDefaultBg : LightDefaultBg;
            Brush borderBrush = Freeze(new SolidColorBrush(Color.FromRgb(71, 85, 105)));

            if (highest != null && highest.NeedsAck && _blinkState)
            {
                bgBrush = highest.Severity switch
                {
                    ScadaAlarmSeverity.Critical => Freeze(new SolidColorBrush(Color.FromRgb(127, 29, 29))),
                    ScadaAlarmSeverity.High => Freeze(new SolidColorBrush(Color.FromRgb(124, 45, 18))),
                    ScadaAlarmSeverity.Medium => Freeze(new SolidColorBrush(Color.FromRgb(113, 63, 18))),
                    _ => Freeze(new SolidColorBrush(Color.FromRgb(30, 41, 59)))
                };
            }

            dc.DrawRectangle(bgBrush, new Pen(borderBrush, 1.0), new Rect(0, 0, w, h));

            // 1. Severity Badges
            double bx = 8;
            double by = (h - 20) * 0.5;
            DrawBadge(dc, ref bx, by, "CRIT", counts.Critical, CritBrush
            #if !NETFRAMEWORK
            , dpi
            #endif
            );
            DrawBadge(dc, ref bx, by, "HIGH", counts.High, HighBrush
            #if !NETFRAMEWORK
            , dpi
            #endif
            );
            DrawBadge(dc, ref bx, by, "MED", counts.Medium, MedBrush
            #if !NETFRAMEWORK
            , dpi
            #endif
            );
            DrawBadge(dc, ref bx, by, "LOW", counts.Low, LowBrush
            #if !NETFRAMEWORK
            , dpi
            #endif
            );

            // 2. Highest Priority Alarm Ticker
            Brush textBrush = (highest != null && highest.NeedsAck && _blinkState) ? Brushes.White : (isDark ? DarkText : LightText);

            if (highest != null)
            {
                string timeStr = highest.ActiveTimestamp.ToLocalTime().ToString("HH:mm:ss");
                string msg = $"[{timeStr}] {highest.TagPath} - {highest.Description} (Val: {highest.TriggerValue ?? "N/A"}) [{highest.State}]";
                var msgText = new FormattedText(msg, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, SegoeBold, 11, textBrush
                #if !NETFRAMEWORK
                , dpi
                #endif
                )
                {
                    MaxTextWidth = contentW,
                    Trimming = TextTrimming.CharacterEllipsis
                };
                dc.DrawText(msgText, new Point(contentLeft, (h - msgText.Height) * 0.5));
            }
            else
            {
                var okText = new FormattedText("✓ ALL SYSTEMS NORMAL - NO ACTIVE ALARMS", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, SegoeBold, 11, OkGreen
                #if !NETFRAMEWORK
                , dpi
                #endif
                );
                dc.DrawText(okText, new Point(contentLeft, (h - okText.Height) * 0.5));
            }

            // 3. Action Buttons
            DrawButton(dc, _btnAckRect, "ACK", highest != null && highest.NeedsAck, CritBrush
            #if !NETFRAMEWORK
            , dpi
            #endif
            );
            DrawButton(dc, _btnAckAllRect, "ACK ALL", counts.TotalActive > 0, LowBrush
            #if !NETFRAMEWORK
            , dpi
            #endif
            );
            DrawButton(dc, _btnSilenceRect, IsSilenced ? "UNSILENCE" : "SILENCE", true, IsSilenced ? MedBrush : borderBrush
            #if !NETFRAMEWORK
            , dpi
            #endif
            );
        }

        private void DrawBadge(DrawingContext dc, ref double x, double y, string label, int count, Brush color
        #if !NETFRAMEWORK
        , double dpi
        #endif
        )
        {
            string text = $"{label}: {count}";
            var ft = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, SegoeBold, 10, count > 0 ? Brushes.White : Brushes.Gray
            #if !NETFRAMEWORK
            , dpi
            #endif
            );

            double bw = ft.Width + 10;
            double bh = 20;
            var r = new Rect(x, y, bw, bh);

            Brush fill = count > 0 ? color : Freeze(new SolidColorBrush(Color.FromArgb(40, 100, 116, 139)));
            dc.DrawRoundedRectangle(fill, new Pen(color, 1.0), r, 3, 3);
            dc.DrawText(ft, new Point(x + 5, y + (bh - ft.Height) * 0.5));

            x += bw + 6;
        }

        private void DrawButton(DrawingContext dc, Rect r, string text, bool enabled, Brush accent
        #if !NETFRAMEWORK
        , double dpi
        #endif
        )
        {
            Brush bg = enabled ? Freeze(new SolidColorBrush(Color.FromRgb(45, 55, 72))) : Freeze(new SolidColorBrush(Color.FromRgb(30, 41, 59)));
            Brush txt = enabled ? Brushes.White : Freeze(new SolidColorBrush(Color.FromRgb(100, 116, 139)));

            dc.DrawRoundedRectangle(bg, new Pen(enabled ? accent : Freeze(new SolidColorBrush(Color.FromRgb(51, 65, 85))), 1.2), r, 3, 3);

            var ft = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, SegoeBold, 10.5, txt
            #if !NETFRAMEWORK
            , dpi
            #endif
            );
            dc.DrawText(ft, new Point(r.X + (r.Width - ft.Width) * 0.5, r.Y + (r.Height - ft.Height) * 0.5));
        }
    
    private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        ZeroWpfTheme.ThemeChanged += OnThemeChanged;
    }

    private void OnUnloaded(object sender, System.Windows.RoutedEventArgs e)
    {
        ZeroWpfTheme.ThemeChanged -= OnThemeChanged;
    }
    private void OnThemeChanged() => InvalidateVisual();
}

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Convenience alias for <see cref="ZAlarmBanner"/>.
    /// </summary>
    public class ZAlarmBannerControl : ZAlarmBanner { }

    /// <summary>
    /// Convenience alias for <see cref="ZAlarmBanner"/>.
    /// </summary>
    public class ZeroAlarmBanner : ZAlarmBanner { }

    /// <summary>
    /// Legacy alias for <see cref="ZAlarmBanner"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("AlarmBannerControl is deprecated and will be removed in 5 release cycles. Please migrate to ZAlarmBanner instead.")]
    public class AlarmBannerControl : ZAlarmBanner { }

    /// <summary>
    /// Legacy alias for <see cref="ZAlarmBanner"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroAlarmBannerControl is deprecated and will be removed in 5 release cycles. Please migrate to ZAlarmBanner instead.")]
    public class ZeroAlarmBannerControl : ZAlarmBanner { }

    #endregion

}
