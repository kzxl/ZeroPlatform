using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ZeroUI.Core.Barcode;
using ZeroUI.Core.Editors;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Editors
{
    /// <summary>
    /// Pure vector industrial barcode and QR code generator and viewer for WPF.
    /// Supports Code 128, Code 39, and QR Code with Reed-Solomon error correction.
    /// Implements <see cref="IZeroEditor"/> for direct form data binding.
    /// </summary>
    public class ZBarcodeBox : FrameworkElement, IZeroEditor
    {
        private bool _isModified = false;

        #region Dependency Properties

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register(nameof(Text), typeof(string), typeof(ZBarcodeBox),
                new FrameworkPropertyMetadata("LOT-123456", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsRender, OnTextChanged));

        public static readonly DependencyProperty SymbologyProperty =
            DependencyProperty.Register(nameof(Symbology), typeof(BarcodeSymbology), typeof(ZBarcodeBox),
                new FrameworkPropertyMetadata(BarcodeSymbology.Code128, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ShowTextProperty =
            DependencyProperty.Register(nameof(ShowText), typeof(bool), typeof(ZBarcodeBox),
                new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty BarColorProperty =
            DependencyProperty.Register(nameof(BarColor), typeof(Color), typeof(ZBarcodeBox),
                new FrameworkPropertyMetadata(Colors.Black, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty QuietZoneProperty =
            DependencyProperty.Register(nameof(QuietZone), typeof(int), typeof(ZBarcodeBox),
                new FrameworkPropertyMetadata(8, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ReadOnlyProperty =
            DependencyProperty.Register(nameof(ReadOnly), typeof(bool), typeof(ZBarcodeBox),
                new PropertyMetadata(false));

        #endregion

        #region Properties & Events

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value ?? string.Empty);
        }

        public BarcodeSymbology Symbology
        {
            get => (BarcodeSymbology)GetValue(SymbologyProperty);
            set => SetValue(SymbologyProperty, value);
        }

        public bool ShowText
        {
            get => (bool)GetValue(ShowTextProperty);
            set => SetValue(ShowTextProperty, value);
        }

        public Color BarColor
        {
            get => (Color)GetValue(BarColorProperty);
            set => SetValue(BarColorProperty, value);
        }

        public int QuietZone
        {
            get => (int)GetValue(QuietZoneProperty);
            set => SetValue(QuietZoneProperty, Math.Max(0, value));
        }

        public bool ReadOnly
        {
            get => (bool)GetValue(ReadOnlyProperty);
            set => SetValue(ReadOnlyProperty, value);
        }

        public event EventHandler? EditValueChanged;

        #endregion

        #region IZeroEditor Implementation

        public object? EditValue
        {
            get => Text;
            set
            {
                Text = value?.ToString() ?? string.Empty;
            }
        }

        public bool IsModified
        {
            get => _isModified;
            set => _isModified = value;
        }

        public void Reset()
        {
            Text = "LOT-123456";
            _isModified = false;
        }

        public void Clear()
        {
            Text = string.Empty;
            _isModified = false;
        }

        #endregion

        public ZBarcodeBox()
        {
            Width = 200;
            Height = 90;
            SnapsToDevicePixels = true;
            UseLayoutRounding = true;

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZBarcodeBox box)
            {
                box.EditValueChanged?.Invoke(box, EventArgs.Empty);
            }
        }

        #region Image Export

        /// <summary>
        /// Renders the barcode onto an image and saves it to disk (e.g. for label printing).
        /// </summary>
        public void SaveAsImage(string filePath, int targetWidth = 0, int targetHeight = 0)
        {
            int w = targetWidth > 0 ? targetWidth : (int)Math.Max(1, ActualWidth);
            int h = targetHeight > 0 ? targetHeight : (int)Math.Max(1, ActualHeight);

            DrawingVisual visual = new DrawingVisual();
            using (DrawingContext dc = visual.RenderOpen())
            {
                RenderBarcode(dc, w, h);
            }

            RenderTargetBitmap rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(visual);

            PngBitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(rtb));

            using (FileStream fs = File.OpenWrite(filePath))
            {
                encoder.Save(fs);
            }
        }

        #endregion

        #region Rendering

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            RenderBarcode(dc, ActualWidth, ActualHeight);
        }

        private void RenderBarcode(DrawingContext dc, double width, double height)
        {
            if (width <= 0 || height <= 0) return;

            bool isDark = ZeroWpfTheme.IsDark;
            Brush bgBrush = isDark ? ZeroWpfTheme.BgCard : Brushes.White;
            Color barColor = isDark ? Colors.White : BarColor;
            Brush barBrush = new SolidColorBrush(barColor);
            barBrush.Freeze();

            // Background
            dc.DrawRectangle(bgBrush, null, new Rect(0, 0, width, height));

            if (string.IsNullOrEmpty(Text)) return;

            int quiet = QuietZone;

            if (Symbology == BarcodeSymbology.QrCode)
            {
                // Render 2D QR Code Matrix
                bool[,] matrix = BarcodeEngine.EncodeQr(Text);
                int moduleCount = matrix.GetLength(0);

                double availableW = width - (quiet * 2);
                double availableH = height - (quiet * 2);
                int modulePixelSize = Math.Max(1, (int)Math.Min(availableW / moduleCount, availableH / moduleCount));

                double qrPixelSize = moduleCount * modulePixelSize;
                double startX = Math.Round((width - qrPixelSize) / 2.0);
                double startY = Math.Round((height - qrPixelSize) / 2.0);

                for (int r = 0; r < moduleCount; r++)
                {
                    for (int c = 0; c < moduleCount; c++)
                    {
                        if (matrix[r, c])
                        {
                            dc.DrawRectangle(barBrush, null,
                                new Rect(startX + (c * modulePixelSize), startY + (r * modulePixelSize), modulePixelSize, modulePixelSize));
                        }
                    }
                }
            }
            else
            {
                // Render 1D Barcode (Code 128 / Code 39)
                bool[] bits = BarcodeEngine.Encode1D(Text, Symbology);
                if (bits.Length == 0) return;

                double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
                double textH = ShowText ? 16.0 : 0.0;
                double barHeight = Math.Max(16.0, height - (quiet * 2) - textH);

                double availableW = width - (quiet * 2);
                double moduleW = availableW / bits.Length;
                if (moduleW < 1.0) moduleW = 1.0;

                double totalBarcodeW = bits.Length * moduleW;
                double startX = Math.Max(quiet, (width - totalBarcodeW) / 2.0);
                double startY = quiet;

                for (int i = 0; i < bits.Length; i++)
                {
                    if (bits[i])
                    {
                        double x1 = Math.Round(startX + (i * moduleW));
                        double x2 = Math.Round(startX + ((i + 1) * moduleW));
                        double w = Math.Max(1.0, x2 - x1);
                        dc.DrawRectangle(barBrush, null, new Rect(x1, startY, w, barHeight));
                    }
                }

                if (ShowText)
                {
                    var ft = new FormattedText(
                        Text,
                        CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        new Typeface(new FontFamily("Consolas"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
                        11.0,
                        barBrush,
                        dpi);

                    double textX = Math.Round((width - ft.Width) / 2.0);
                    double textY = Math.Round(startY + barHeight + 2.0);
                    dc.DrawText(ft, new Point(textX, textY));
                }
            }
        }

        #endregion
    
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

    /// <summary>
    /// Alias for <see cref="BarcodeBox"/> matching the BarcodeEdit naming convention.
    /// </summary>
    public class BarcodeEdit : BarcodeBox
    {
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZBarcodeBox"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("BarcodeBox is deprecated and will be removed in 5 release cycles. Please migrate to ZBarcodeBox instead.")]
    public class BarcodeBox : ZBarcodeBox { }

    /// <summary>
    /// Legacy alias for <see cref="ZBarcodeBox"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroBarcodeBox is deprecated and will be removed in 5 release cycles. Please migrate to ZBarcodeBox instead.")]
    public class ZeroBarcodeBox : ZBarcodeBox { }

    #endregion

}
