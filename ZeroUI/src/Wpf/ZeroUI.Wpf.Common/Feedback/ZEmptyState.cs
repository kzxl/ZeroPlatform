using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Feedback
{
    /// <summary>
    /// Clean, themed empty state placeholder for WPF lists, grids, and search views,
    /// complete with vector/emoji glyph, title, description, and call-to-action button.
    /// </summary>
    public class ZEmptyState : Control
    {
        private string _glyph = "📭";
        private string _title = "No Data Available";
        private string _description = "There are no records to display at this time.";
        private string _actionText = "Refresh";

        private Rect _actionButtonRect = Rect.Empty;
        private bool _isActionHovered = false;

        public event EventHandler? ActionClicked;

        public string Glyph
        {
            get => _glyph;
            set { _glyph = value ?? string.Empty; InvalidateVisual(); }
        }

        public string Title
        {
            get => _title;
            set { _title = value ?? string.Empty; InvalidateVisual(); }
        }

        public string Description
        {
            get => _description;
            set { _description = value ?? string.Empty; InvalidateVisual(); }
        }

        public string ActionText
        {
            get => _actionText;
            set { _actionText = value ?? string.Empty; InvalidateVisual(); }
        }

        public bool ShowAction => !string.IsNullOrWhiteSpace(_actionText);

        static ZEmptyState()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ZEmptyState), new FrameworkPropertyMetadata(typeof(ZEmptyState)));
        }

        public ZEmptyState()
        {
            Width = 320;
            Height = 220;
            Focusable = true;

            Loaded += (s, e) => ZeroWpfTheme.ThemeChanged += OnThemeChanged;
            Unloaded += (s, e) => ZeroWpfTheme.ThemeChanged -= OnThemeChanged;
        }

        private void OnThemeChanged()
        {
            if (!Dispatcher.CheckAccess())
            {
                if (!Dispatcher.HasShutdownStarted && !Dispatcher.HasShutdownFinished)
                    Dispatcher.BeginInvoke((Action)OnThemeChanged);
                return;
            }
            InvalidateVisual();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            var pos = e.GetPosition(this);
            bool wasHovered = _isActionHovered;
            _isActionHovered = ShowAction && _actionButtonRect.Contains(pos);
            Cursor = _isActionHovered ? Cursors.Hand : Cursors.Arrow;

            if (wasHovered != _isActionHovered)
            {
                InvalidateVisual();
            }
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            if (_isActionHovered)
            {
                _isActionHovered = false;
                Cursor = Cursors.Arrow;
                InvalidateVisual();
            }
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            var pos = e.GetPosition(this);
            if (ShowAction && _actionButtonRect.Contains(pos))
            {
                ActionClicked?.Invoke(this, EventArgs.Empty);
                e.Handled = true;
            }
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            var bounds = new Rect(0, 0, ActualWidth, ActualHeight);
            if (bounds.Width <= 0 || bounds.Height <= 0) return;

            double centerX = bounds.Width / 2;
            double totalContentH = 48 + 24 + 32 + (ShowAction ? 36 : 0);
            double startY = Math.Max(10, (bounds.Height - totalContentH) / 2);
            var typeface = new Typeface(FontFamily, FontStyle, FontWeight, FontStretch);
            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;

            // 1. Glyph
            if (!string.IsNullOrEmpty(_glyph))
            {
                var glyphFormatted = new FormattedText(
                    _glyph,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    typeface,
                    28.0,
                    ZeroWpfTheme.TextMuted,
                    dpi);

                dc.DrawText(glyphFormatted, new Point(centerX - (glyphFormatted.Width / 2), startY));
                startY += 48;
            }

            // 2. Title
            if (!string.IsNullOrEmpty(_title))
            {
                var boldTypeface = new Typeface(FontFamily, FontStyle, FontWeights.Bold, FontStretch);
                var titleFormatted = new FormattedText(
                    _title,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    boldTypeface,
                    14.0,
                    ZeroWpfTheme.TextPrimary,
                    dpi);

                dc.DrawText(titleFormatted, new Point(centerX - (titleFormatted.Width / 2), startY));
                startY += 26;
            }

            // 3. Description
            if (!string.IsNullOrEmpty(_description))
            {
                var descFormatted = new FormattedText(
                    _description,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    typeface,
                    11.5,
                    ZeroWpfTheme.TextMuted,
                    dpi);

                dc.DrawText(descFormatted, new Point(centerX - (descFormatted.Width / 2), startY));
                startY += 38;
            }

            // 4. Action Button
            if (ShowAction)
            {
                var boldTypeface = new Typeface(FontFamily, FontStyle, FontWeights.SemiBold, FontStretch);
                var btnText = new FormattedText(
                    _actionText,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    boldTypeface,
                    12.0,
                    Brushes.White,
                    dpi);

                double btnW = Math.Max(88, btnText.Width + 28);
                double btnH = 28;
                _actionButtonRect = new Rect(centerX - (btnW / 2), startY + 4, btnW, btnH);

                var btnBrush = _isActionHovered ? ZeroWpfTheme.SecondaryAccent : ZeroWpfTheme.PrimaryAccent;
                dc.DrawRoundedRectangle(btnBrush, null, _actionButtonRect, 4, 4);
                dc.DrawText(btnText, new Point(_actionButtonRect.X + (btnW - btnText.Width) / 2, _actionButtonRect.Y + (btnH - btnText.Height) / 2));
            }
            else
            {
                _actionButtonRect = Rect.Empty;
            }
        }
    }

    [Obsolete("ZeroEmptyState is deprecated and will be removed in 5 release cycles. Please migrate to ZEmptyState instead.")]
    public class ZeroEmptyState : ZEmptyState { }
}
