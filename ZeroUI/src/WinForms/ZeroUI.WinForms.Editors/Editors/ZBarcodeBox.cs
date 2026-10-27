using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;
using ZeroUI.Core.Barcode;
using ZeroUI.Core.Editors;
using ZeroUI.Core.Theme;
using ZeroUI.WinForms.Base;
using ZeroUI.WinForms.Icons;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Editors
{
    /// <summary>
    /// Pure vector industrial barcode and QR code generator and viewer.
    /// Supports Code 128, Code 39, and QR Code with Reed-Solomon error correction.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Editors")]
    [DefaultProperty("Text")]
    [DefaultEvent("TextChanged")]
    [Description("Pure vector 1D barcode and 2D QR code generator and viewer")]
    [ToolboxBitmap(typeof(ZeroIcons), "BarcodeBox.bmp")]
    public class ZBarcodeBox : ControlBase, IZeroEditor
    {
        private string _text = "LOT-123456";
        private BarcodeSymbology _symbology = BarcodeSymbology.Code128;
        private bool _showText = true;
        private Color _barColor = Color.Black;
        private int _quietZone = 8;
        private bool _isReadOnly = false;
        private bool _isModified = false;

        public event EventHandler? EditValueChanged;

        public ZBarcodeBox()
        {
            Size = new Size(200, 90);
            Font = new Font("Consolas", 9f);
            BackColor = Color.White;
        }

        protected override void OnThemeChanged(ZeroSkin skin)
        {
            base.OnThemeChanged(skin);
            Invalidate();
        }

        #region Properties

        [Category("ZeroUI")]
        [Description("Data text encoded in the machine-readable barcode.")]
        [DefaultValue("LOT-123456")]
#if NET8_0_OR_GREATER
        [System.Diagnostics.CodeAnalysis.AllowNull]
#endif
        public override string Text
        {
            get => _text;
            set
            {
                var val = value ?? string.Empty;
                if (_text != val)
                {
                    _text = val;
                    Invalidate();
                    OnTextChanged(EventArgs.Empty);
                    EditValueChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        [Category("ZeroUI")]
        [Description("Barcode symbology standard (Code 128, Code 39, or QR Code).")]
        [DefaultValue(BarcodeSymbology.Code128)]
        public BarcodeSymbology Symbology
        {
            get => _symbology;
            set
            {
                if (_symbology != value)
                {
                    _symbology = value;
                    Invalidate();
                }
            }
        }

        [Category("ZeroUI")]
        [Description("Renders human-readable text below 1D barcode stripes.")]
        [DefaultValue(true)]
        public bool ShowText
        {
            get => _showText;
            set
            {
                _showText = value;
                Invalidate();
            }
        }

        [Category("ZeroUI")]
        [Description("Color of the dark bars or QR modules.")]
        public Color BarColor
        {
            get => _barColor;
            set
            {
                _barColor = value;
                Invalidate();
            }
        }

        [Category("ZeroUI")]
        [Description("Padding margin in pixels surrounding the barcode symbol.")]
        [DefaultValue(8)]
        public int QuietZone
        {
            get => _quietZone;
            set
            {
                _quietZone = Math.Max(0, value);
                Invalidate();
            }
        }

        #endregion

        #region IZeroEditor Implementation

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public object? EditValue
        {
            get => Text;
            set
            {
                Text = value?.ToString() ?? string.Empty;
            }
        }

        [Category("ZeroUI")]
        [Description("Tracks whether user or code modified the editor value.")]
        [DefaultValue(false)]
        public bool IsModified
        {
            get => _isModified;
            set => _isModified = value;
        }

        [Category("ZeroUI")]
        [Description("Prevents editing when in read-only mode.")]
        [DefaultValue(false)]
        public bool ReadOnly
        {
            get => _isReadOnly;
            set
            {
                _isReadOnly = value;
                Invalidate();
            }
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

        #region Image Export

        /// <summary>
        /// Renders the barcode onto a standalone bitmap and saves it to disk (e.g. for label printing).
        /// </summary>
        public void SaveAsImage(string filePath, int targetWidth = 0, int targetHeight = 0)
        {
            int w = targetWidth > 0 ? targetWidth : Width;
            int h = targetHeight > 0 ? targetHeight : Height;

            using var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.White);
                RenderBarcode(g, w, h);
            }
            bmp.Save(filePath, ImageFormat.Png);
        }

        #endregion

        #region Painting

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            RenderBarcode(g, Width, Height);
        }

        private void RenderBarcode(Graphics g, int width, int height)
        {
            g.SmoothingMode = SmoothingMode.None; // Crisp pixel alignment
            g.PixelOffsetMode = PixelOffsetMode.Half;

            Color bg = EffectiveSkin.IsDark ? Color.FromArgb(15, 23, 42) : BackColor;
            Color barCol = EffectiveSkin.IsDark ? Color.FromArgb(241, 245, 249) : _barColor;
            using var bgBrush = new SolidBrush(bg);
            using var barBrush = new SolidBrush(barCol);

            g.FillRectangle(bgBrush, 0, 0, width, height);

            if (string.IsNullOrEmpty(_text)) return;

            if (_symbology == BarcodeSymbology.QrCode)
            {
                // Render 2D QR Matrix
                bool[,] matrix = BarcodeEngine.EncodeQr(_text);
                int moduleCount = matrix.GetLength(0);

                int availableW = width - (_quietZone * 2);
                int availableH = height - (_quietZone * 2);
                int modulePixelSize = Math.Max(1, Math.Min(availableW / moduleCount, availableH / moduleCount));

                int qrPixelSize = moduleCount * modulePixelSize;
                int startX = (width - qrPixelSize) / 2;
                int startY = (height - qrPixelSize) / 2;

                for (int r = 0; r < moduleCount; r++)
                {
                    for (int c = 0; c < moduleCount; c++)
                    {
                        if (matrix[r, c])
                        {
                            g.FillRectangle(barBrush, startX + (c * modulePixelSize), startY + (r * modulePixelSize), modulePixelSize, modulePixelSize);
                        }
                    }
                }
            }
            else
            {
                // Render 1D Barcode (Code 128 / Code 39)
                bool[] bits = BarcodeEngine.Encode1D(_text, _symbology);
                if (bits.Length == 0) return;

                int textH = _showText ? (Font.Height + 4) : 0;
                int barHeight = Math.Max(16, height - (_quietZone * 2) - textH);

                int availableW = width - (_quietZone * 2);
                float moduleW = (float)availableW / bits.Length;
                if (moduleW < 1.0f) moduleW = 1.0f;

                int startX = (int)Math.Max(_quietZone, (width - (bits.Length * moduleW)) / 2f);
                int startY = _quietZone;

                for (int i = 0; i < bits.Length; i++)
                {
                    if (bits[i])
                    {
                        int x1 = (int)Math.Round(startX + (i * moduleW));
                        int x2 = (int)Math.Round(startX + ((i + 1) * moduleW));
                        int w = Math.Max(1, x2 - x1);
                        g.FillRectangle(barBrush, x1, startY, w, barHeight);
                    }
                }

                if (_showText)
                {
                    var textRect = new Rectangle(0, startY + barHeight + 2, width, textH);
                    TextRenderer.DrawText(g, _text, Font, textRect, barCol,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
            }
        }

        #endregion
    }

    /// <summary>
    /// Alias for BarcodeBox matching the BarcodeEdit convention.
    /// </summary>
    [ToolboxItem(false)]
    public class BarcodeEdit : ZBarcodeBox
    {
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZBarcodeBox"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("BarcodeBox is deprecated and will be removed in 5 release cycles. Please migrate to ZBarcodeBox instead.")]
    [ToolboxItem(false)]
    public class BarcodeBox : ZBarcodeBox
    {
    }

    #endregion
}
