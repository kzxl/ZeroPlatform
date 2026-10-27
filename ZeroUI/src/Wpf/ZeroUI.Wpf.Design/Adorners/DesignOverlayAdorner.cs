using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace ZeroUI.Wpf.Design.Adorners
{
    /// <summary>
    /// Visual Adorner rendered on top of ZeroUI controls inside WPF Visual Studio Designer.
    /// Draws a subtle design-time indicator border and watermark tag.
    /// </summary>
    public class DesignOverlayAdorner : Adorner
    {
        private readonly string _controlTag;
        private readonly Pen _borderPen;
        private readonly Brush _tagBackground;
        private readonly Typeface _typeface;

        public DesignOverlayAdorner(UIElement adornedElement, string controlTag) : base(adornedElement)
        {
            _controlTag = controlTag;
            _borderPen = new Pen(new SolidColorBrush(Color.FromArgb(120, 99, 102, 241)), 1.5)
            {
                DashStyle = DashStyles.Dash
            };
            _borderPen.Freeze();

            _tagBackground = new SolidColorBrush(Color.FromArgb(200, 99, 102, 241));
            _tagBackground.Freeze();

            _typeface = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
            IsHitTestVisible = false;
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            var bounds = new Rect(0, 0, AdornedElement.RenderSize.Width, AdornedElement.RenderSize.Height);
            if (bounds.Width < 10 || bounds.Height < 10) return;

            // Draw bounding guide outline
            dc.DrawRectangle(null, _borderPen, bounds);

            // Draw small badge
            var tagText = new FormattedText(
                _controlTag,
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                _typeface,
                10.0,
                Brushes.White
#if NET8_0_OR_GREATER
                , 96.0
#endif
            );

            var tagRect = new Rect(bounds.Right - tagText.Width - 8, bounds.Bottom - tagText.Height - 4, tagText.Width + 6, tagText.Height + 2);
            if (tagRect.Left > 0 && tagRect.Top > 0)
            {
                dc.DrawRoundedRectangle(_tagBackground, null, tagRect, 3, 3);
                dc.DrawText(tagText, new Point(tagRect.Left + 3, tagRect.Top + 1));
            }
        }
    }
}
