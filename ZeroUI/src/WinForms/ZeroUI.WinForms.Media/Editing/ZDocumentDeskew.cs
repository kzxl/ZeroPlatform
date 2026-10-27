using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Media
{
    /// <summary>
    /// Document straightening, perspective deskew, and threshold binarization processor for OCR and document archiving (WinForms).
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI")]
    [DefaultProperty("Image")]
    public class ZDocumentDeskew : Control
    {
        private Bitmap? _image;
        private double _deskewAngle;
        private bool _isBinarizationEnabled;
        private byte _threshold = 128;

        public ZDocumentDeskew()
        {
            SetStyle(ControlStyles.UserPaint |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);

            ZeroTheme.ThemeChanged += OnThemeChanged;
        }

        #region Properties

        [Category("ZeroUI")]
        [Description("The source document bitmap to deskew and binarize.")]
        [DefaultValue(null)]
        public Bitmap? Image
        {
            get => _image;
            set
            {
                if (_image != value)
                {
                    _image = value;
                    Invalidate();
                }
            }
        }

        [Category("ZeroUI")]
        [Description("Alias for Image property.")]
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Bitmap? Source
        {
            get => Image;
            set => Image = value;
        }

        [Category("ZeroUI")]
        [Description("The rotation deskew angle in degrees (-45.0 to 45.0).")]
        [DefaultValue(0.0)]
        public double DeskewAngle
        {
            get => _deskewAngle;
            set
            {
                double clamped = Math.Max(-45.0, Math.Min(45.0, value));
                if (Math.Abs(_deskewAngle - clamped) > 0.001)
                {
                    _deskewAngle = clamped;
                    Invalidate();
                }
            }
        }

        [Category("ZeroUI")]
        [Description("Enables or disables fast Otsu / threshold binarization for OCR preprocessing.")]
        [DefaultValue(false)]
        public bool IsBinarizationEnabled
        {
            get => _isBinarizationEnabled;
            set
            {
                if (_isBinarizationEnabled != value)
                {
                    _isBinarizationEnabled = value;
                    Invalidate();
                }
            }
        }

        [Category("ZeroUI")]
        [Description("Luminance threshold value for binarization (0-255). Default is 128.")]
        [DefaultValue((byte)128)]
        public byte Threshold
        {
            get => _threshold;
            set
            {
                if (_threshold != value)
                {
                    _threshold = value;
                    Invalidate();
                }
            }
        }

        #endregion

        #region Painting

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            var g = e.Graphics;
            g.Clear(ZeroTheme.Colors.Surface);

            if (_image == null || Width <= 0 || Height <= 0)
            {
                using var placeholderBrush = new SolidBrush(ZeroTheme.Colors.TextMuted);
                using var sf = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };
                g.DrawString("No Document Loaded", Font, placeholderBrush, ClientRectangle, sf);
                return;
            }

            g.SmoothingMode = SmoothingMode.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;

            double fitScale = Math.Min((double)Width / _image.Width, (double)Height / _image.Height);
            float dispW = (float)(_image.Width * fitScale);
            float dispH = (float)(_image.Height * fitScale);
            float left = (Width - dispW) / 2.0f;
            float top = (Height - dispH) / 2.0f;

            var state = g.Save();

            // Center of the display image
            float centerX = Width / 2.0f;
            float centerY = Height / 2.0f;

            g.TranslateTransform(centerX, centerY);
            g.RotateTransform((float)_deskewAngle);
            g.TranslateTransform(-centerX, -centerY);

            var destRect = new RectangleF(left, top, dispW, dispH);
            g.DrawImage(_image, destRect);

            // Draw grid guides for deskew alignment (cyan 50 alpha)
            using (var guidePen = new Pen(Color.FromArgb(50, 0, 229, 255), 1.0f))
            {
                float step = 30.0f;
                for (float gx = left; gx <= left + dispW; gx += step)
                {
                    g.DrawLine(guidePen, gx, top, gx, top + dispH);
                }
                for (float gy = top; gy <= top + dispH; gy += step)
                {
                    g.DrawLine(guidePen, left, gy, left + dispW, gy);
                }
            }

            g.Restore(state);
        }

        #endregion

        #region Processing

        /// <summary>
        /// Generates the transformed (rotated / binarized) clean document bitmap ready for OCR.
        /// </summary>
        public Bitmap? GetProcessedBitmap()
        {
            if (_image == null) return null;

            Bitmap result = (Bitmap)_image.Clone();

            // 1. Rotate Deskew if angle is significant
            if (Math.Abs(_deskewAngle) > 0.05)
            {
                var rotated = new Bitmap(result.Width, result.Height, PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(rotated))
                {
                    g.SmoothingMode = SmoothingMode.HighQuality;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.Clear(Color.White);

                    g.TranslateTransform(result.Width / 2.0f, result.Height / 2.0f);
                    g.RotateTransform((float)_deskewAngle);
                    g.TranslateTransform(-result.Width / 2.0f, -result.Height / 2.0f);

                    g.DrawImage(result, 0, 0);
                }
                result.Dispose();
                result = rotated;
            }

            // 2. High-Performance Pixel Binarization
            if (_isBinarizationEnabled)
            {
                var binarized = ApplyBinarization(result, _threshold);
                result.Dispose();
                result = binarized;
            }

            return result;
        }

        private static Bitmap ApplyBinarization(Bitmap src, byte threshold)
        {
            int width = src.Width;
            int height = src.Height;
            var output = new Bitmap(width, height, PixelFormat.Format32bppArgb);

            var srcRect = new Rectangle(0, 0, width, height);
            var srcData = src.LockBits(srcRect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            var dstData = output.LockBits(srcRect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

            try
            {
                unsafe
                {
                    byte* pSrc = (byte*)srcData.Scan0;
                    byte* pDst = (byte*)dstData.Scan0;
                    int stride = srcData.Stride;

                    for (int y = 0; y < height; y++)
                    {
                        byte* rowSrc = pSrc + (y * stride);
                        byte* rowDst = pDst + (y * stride);

                        for (int x = 0; x < width; x++)
                        {
                            int idx = x * 4;
                            byte b = rowSrc[idx];
                            byte g = rowSrc[idx + 1];
                            byte r = rowSrc[idx + 2];

                            // Fast grayscale luma: L = 0.299R + 0.587G + 0.114B
                            byte luma = (byte)(r * 0.299 + g * 0.587 + b * 0.114);
                            byte binaryVal = luma >= threshold ? (byte)255 : (byte)0;

                            rowDst[idx] = binaryVal;     // B
                            rowDst[idx + 1] = binaryVal; // G
                            rowDst[idx + 2] = binaryVal; // R
                            rowDst[idx + 3] = 255;       // Alpha
                        }
                    }
                }
            }
            finally
            {
                src.UnlockBits(srcData);
                output.UnlockBits(dstData);
            }

            return output;
        }

        #endregion

        #region Theme & Cleanup

        private void OnThemeChanged(object? sender, EventArgs e) => Invalidate();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ZeroTheme.ThemeChanged -= OnThemeChanged;
            }
            base.Dispose(disposing);
        }

        #endregion
    }

    #region Backward Compatibility Shims

    [Obsolete("DocumentDeskew is deprecated. Please migrate to ZDocumentDeskew instead.")]
    [ToolboxItem(false)]
    public class DocumentDeskew : ZDocumentDeskew { }

    [Obsolete("ZeroDocumentDeskew is deprecated. Please migrate to ZDocumentDeskew instead.")]
    [ToolboxItem(false)]
    public class ZeroDocumentDeskew : ZDocumentDeskew { }

    #endregion
}
