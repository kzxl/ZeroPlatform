using System;

using ZeroUI.WinForms.Icons;using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Media;
using System.Windows.Forms;
using ZeroUI.WinForms.Native;
using ZeroUI.WinForms.Rendering;
using ZeroUI.WinForms.Theme;
using ZeroUI.WinForms.Warehouse.Models;

namespace ZeroUI.WinForms.Warehouse
{
    /// <summary>
    /// Industrial Smart Barcode & QR Code Workstation Control.
    /// Supports USB Keyboard Wedge scanner timing detection, continuous scanning,
    /// duplicate scan protection, audio beeps, prefix parsing, and scan history.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Warehouse & Logistics")]
    [DefaultEvent("BarcodeScanned")]
    [Description("Industrial Barcode & QR Code Workstation Scanner Control")]
    [ToolboxBitmap(typeof(ZeroIcons), "ZeroBarcodeScanControl.bmp")]
    public class ZBarcodeScan : Control
    {
        private readonly TextBox _txtBarcode;
        private readonly Button _btnScan;
        private readonly Timer _flashTimer;
        private int _flashTicks = 0;
        private bool _flashSuccess = true;

        // Hardware scanner keystroke timing detector
        private DateTime _lastKeystrokeTime = DateTime.MinValue;
        private readonly List<long> _keyIntervals = new List<long>(32);
        private const int MaxHardwareIntervalMs = 35; // Keystrokes under 35ms indicate barcode scanner hardware

        // Duplicate scan protection
        private string _lastScannedCode = "";
        private DateTime _lastScanTime = DateTime.MinValue;
        private int _duplicateTimeoutMs = 2000;
        private bool _preventDuplicates = true;

        // Features
        private bool _continuousScan = true;
        private bool _enableAudioFeedback = true;
        private string _title = "Scan Barcode / QR Code";
        private BarcodeScanResult? _lastResult = null;
        private readonly List<BarcodeScanResult> _history = new List<BarcodeScanResult>(32);
        private int _maxHistoryCount = 30;

        public event EventHandler<BarcodeScanEventArgs>? BarcodeScanned;
        public event EventHandler<string>? DuplicateDetected;

        /// <summary>
        /// Custom parsing delegate to transform raw barcodes into structured Product/Lot/Quantity.
        /// </summary>
        public Func<string, BarcodeScanResult>? CustomParser { get; set; }

        public ZBarcodeScan()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor, true);

            Size = new Size(340, 160);
            BackColor = Color.Transparent;
            Font = new Font("Segoe UI", 9f, FontStyle.Regular);

            // 1. Textbox for barcode input
            _txtBarcode = new TextBox
            {
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                Location = new Point(14, 40),
                Size = new Size(230, 24),
                BackColor = Color.FromArgb(248, 250, 252)
            };
            _txtBarcode.KeyDown += OnBarcodeKeyDown;

            // 2. Scan Submit Button
            _btnScan = new Button
            {
                Text = "Scan",
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Location = new Point(252, 38),
                Size = new Size(72, 28),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(79, 70, 229), // Indigo
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            _btnScan.FlatAppearance.BorderSize = 0;
            _btnScan.Click += (s, e) => ProcessInput(_txtBarcode.Text);

            Controls.Add(_txtBarcode);
            Controls.Add(_btnScan);

            // 3. Visual flash feedback timer
            _flashTimer = new Timer { Interval = 50 };
            _flashTimer.Tick += (s, e) =>
            {
                _flashTicks--;
                if (_flashTicks <= 0)
                {
                    _flashTimer.Stop();
                }
                Invalidate();
            };
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _flashTimer?.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Public Properties

        [Category("Appearance")]
        [DefaultValue("Scan Barcode / QR Code")]
        public string Title
        {
            get => _title;
            set { _title = value ?? ""; Invalidate(); }
        }

        [Category("Behavior")]
        [DefaultValue(true)]
        [Description("Automatically clear input and re-focus for high-speed continuous line scanning.")]
        public bool ContinuousScan
        {
            get => _continuousScan;
            set => _continuousScan = value;
        }

        [Category("Behavior")]
        [DefaultValue(true)]
        [Description("Prevent duplicate scans of the exact same barcode within the debounce timeout window.")]
        public bool PreventDuplicates
        {
            get => _preventDuplicates;
            set => _preventDuplicates = value;
        }

        [Category("Behavior")]
        [DefaultValue(2000)]
        [Description("Duplicate scan suppression interval in milliseconds.")]
        public int DuplicateTimeoutMs
        {
            get => _duplicateTimeoutMs;
            set => _duplicateTimeoutMs = Math.Max(0, value);
        }

        [Category("Behavior")]
        [DefaultValue(true)]
        [Description("Play pleasant audio chime on successful scan, or low alert tone on duplicate/error.")]
        public bool EnableAudioFeedback
        {
            get => _enableAudioFeedback;
            set => _enableAudioFeedback = value;
        }

        [Browsable(false)]
        public BarcodeScanResult? LastResult => _lastResult;

        [Browsable(false)]
        public IReadOnlyList<BarcodeScanResult> RecentHistory => _history.AsReadOnly();

        #endregion

        #region Public Methods

        public void FocusInput()
        {
            if (_txtBarcode.CanFocus)
            {
                _txtBarcode.Focus();
                _txtBarcode.SelectAll();
            }
        }

        public void Clear()
        {
            _txtBarcode.Clear();
            _lastResult = null;
            Invalidate();
        }

        public BarcodeScanResult? CollectLastScan() => _lastResult;

        #endregion

        #region Processing Logic

        private void OnBarcodeKeyDown(object? sender, KeyEventArgs e)
        {
            DateTime now = DateTime.Now;
            if (_lastKeystrokeTime != DateTime.MinValue)
            {
                long elapsedMs = (long)(now - _lastKeystrokeTime).TotalMilliseconds;
                _keyIntervals.Add(elapsedMs);
            }
            _lastKeystrokeTime = now;

            if (e.KeyCode == Keys.Enter)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                ProcessInput(_txtBarcode.Text);
            }
        }

        private void ProcessInput(string raw)
        {
            string code = (raw ?? "").Trim();
            if (string.IsNullOrEmpty(code)) return;

            DateTime now = DateTime.Now;

            // 1. Detect if input was hardware barcode scanner wedge vs human typing
            bool isHardware = false;
            if (_keyIntervals.Count >= 3)
            {
                long avgInterval = 0;
                for (int i = 0; i < _keyIntervals.Count; i++) avgInterval += _keyIntervals[i];
                avgInterval /= _keyIntervals.Count;
                isHardware = avgInterval <= MaxHardwareIntervalMs;
            }
            _keyIntervals.Clear();
            _lastKeystrokeTime = DateTime.MinValue;

            // 2. Duplicate Detection
            if (_preventDuplicates && string.Equals(code, _lastScannedCode, StringComparison.OrdinalIgnoreCase))
            {
                double elapsedSinceLastScan = (now - _lastScanTime).TotalMilliseconds;
                if (elapsedSinceLastScan < _duplicateTimeoutMs)
                {
                    // Trigger error flash & tone
                    TriggerFlash(false);
                    PlaySound(false);
                    DuplicateDetected?.Invoke(this, code);
                    if (_continuousScan)
                    {
                        _txtBarcode.SelectAll();
                        _txtBarcode.Focus();
                    }
                    return;
                }
            }

            _lastScannedCode = code;
            _lastScanTime = now;

            // 3. Parse Barcode (Product, Lot, Qty)
            BarcodeScanResult result = CustomParser != null ? CustomParser(code) : DefaultParseBarcode(code);
            result.RawBarcode = code;
            result.Timestamp = now;
            result.IsHardwareScanner = isHardware;

            _lastResult = result;

            // Maintain history buffer
            _history.Insert(0, result);
            if (_history.Count > _maxHistoryCount) _history.RemoveAt(_history.Count - 1);

            // Visual and audio feedback
            TriggerFlash(true);
            PlaySound(true);

            // Fire Event
            BarcodeScanned?.Invoke(this, new BarcodeScanEventArgs(result));

            if (_continuousScan)
            {
                _txtBarcode.Clear();
                _txtBarcode.Focus();
            }
            else
            {
                _txtBarcode.SelectAll();
            }

            Invalidate();
        }

        private static BarcodeScanResult DefaultParseBarcode(string code)
        {
            var res = new BarcodeScanResult { RawBarcode = code, IsValid = true };

            // Supported format: "P:ABC01|L:LOT260901|Q:100" or "ABC-001|L001|100" or "PROD-LOT"
            if (code.Contains("|"))
            {
                string[] parts = code.Split('|');
                for (int i = 0; i < parts.Length; i++)
                {
                    string p = parts[i].Trim();
                    if (p.StartsWith("P:", StringComparison.OrdinalIgnoreCase)) res.ProductCode = p.Substring(2);
                    else if (p.StartsWith("L:", StringComparison.OrdinalIgnoreCase)) res.LotNumber = p.Substring(2);
                    else if (p.StartsWith("Q:", StringComparison.OrdinalIgnoreCase) && decimal.TryParse(p.Substring(2), out decimal q)) res.Quantity = q;
                    else if (i == 0 && string.IsNullOrEmpty(res.ProductCode)) res.ProductCode = p;
                    else if (i == 1 && string.IsNullOrEmpty(res.LotNumber)) res.LotNumber = p;
                    else if (i == 2 && decimal.TryParse(p, out decimal q2)) res.Quantity = q2;
                }
            }
            else if (code.Contains("-"))
            {
                string[] parts = code.Split('-');
                if (parts.Length >= 2)
                {
                    res.ProductCode = parts[0];
                    res.LotNumber = parts[1];
                    if (parts.Length >= 3 && decimal.TryParse(parts[2], out decimal q)) res.Quantity = q;
                }
                else
                {
                    res.ProductCode = code;
                }
            }
            else
            {
                res.ProductCode = code;
            }

            return res;
        }

        private void TriggerFlash(bool success)
        {
            _flashSuccess = success;
            _flashTicks = 6;
            _flashTimer.Start();
            Invalidate();
        }

        private void PlaySound(bool success)
        {
            if (!_enableAudioFeedback) return;
            try
            {
                if (success)
                {
                    SystemSounds.Asterisk.Play();
                }
                else
                {
                    SystemSounds.Hand.Play();
                }
            }
            catch { }
        }

        #endregion

        #region Rendering Engine

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (_txtBarcode == null || _btnScan == null) return;
            _txtBarcode.Location = new Point(14, 38);
            _btnScan.Location = new Point(Width - 84, 34);
            _btnScan.Size = new Size(70, 32);
            _txtBarcode.Width = Math.Max(80, Width - 106);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            int w = Width;
            int h = Height;

            // 1. Container Background with Card Bezel
            using (var bgBrush = new SolidBrush(Color.White))
            {
                using var cardPath = ZeroUIConfig.CreateRoundedRectangle(new Rectangle(0, 0, w - 1, h - 1), 8);
                g.FillPath(bgBrush, cardPath);

                Color borderColor = Color.FromArgb(226, 232, 240);
                if (_flashTicks > 0)
                {
                    borderColor = _flashSuccess ? Color.FromArgb(16, 185, 129) : Color.FromArgb(239, 68, 68);
                }
                using var borderPen = new Pen(borderColor, _flashTicks > 0 ? 2f : 1f);
                g.DrawPath(borderPen, cardPath);
            }

            // 2. Header: Title + Live Status Badge
            var titleFont = ZeroFontCache.Get("Segoe UI", 9.5f, FontStyle.Bold);
            using (var titleBrush = new SolidBrush(Color.FromArgb(30, 41, 59)))
            {
                g.DrawString(_title, titleFont, titleBrush, 14, 10);
            }

            // Status Indicator Pill
            string statusText = _flashTicks > 0
                ? (_flashSuccess ? "✓ Accepted" : "⚠ Duplicate")
                : "● Ready";
            Color statusColor = _flashTicks > 0
                ? (_flashSuccess ? Color.FromArgb(16, 185, 129) : Color.FromArgb(239, 68, 68))
                : Color.FromArgb(59, 130, 246);

            var pillFont = ZeroFontCache.Get("Segoe UI", 7.5f, FontStyle.Bold);
            using (var pillBrush = new SolidBrush(statusColor))
            {
                g.DrawString(statusText, pillFont, pillBrush, w - 85, 12);
            }

            // 3. Input Border Enclosure
            Rectangle inputRect = new Rectangle(10, 34, w - 98, 32);
            using (var inputPath = ZeroUIConfig.CreateRoundedRectangle(inputRect, 6))
            {
                using var fillBrush = new SolidBrush(Color.FromArgb(248, 250, 252));
                g.FillPath(fillBrush, inputPath);

                Color boxBorder = _flashTicks > 0
                    ? (_flashSuccess ? Color.FromArgb(16, 185, 129) : Color.FromArgb(239, 68, 68))
                    : Color.FromArgb(203, 213, 225);
                using var borderPen = new Pen(boxBorder, 1.2f);
                g.DrawPath(borderPen, inputPath);
            }

            // 4. Metadata Display Area (Parsed Info Card)
            int metaY = 74;
            Rectangle metaRect = new Rectangle(10, metaY, w - 20, h - metaY - 10);
            using (var metaPath = ZeroUIConfig.CreateRoundedRectangle(metaRect, 6))
            {
                using var metaFill = new SolidBrush(Color.FromArgb(241, 245, 249));
                g.FillPath(metaFill, metaPath);
                using var metaPen = new Pen(Color.FromArgb(226, 232, 240), 1f);
                g.DrawPath(metaPen, metaPath);
            }

            if (_lastResult != null)
            {
                var labelFont = ZeroFontCache.Get("Segoe UI", 8.5f, FontStyle.Bold);
                var valueFont = ZeroFontCache.Get("Segoe UI", 8.5f, FontStyle.Regular);
                using var greenBrush = new SolidBrush(Color.FromArgb(16, 185, 129));
                using var textBrush = new SolidBrush(Color.FromArgb(30, 41, 59));
                using var subBrush = new SolidBrush(Color.FromArgb(100, 116, 139));

                int row1Y = metaY + 6;
                int row2Y = metaY + 26;
                int row3Y = metaY + 46;

                // Row 1: Product Code
                g.DrawString("✓", labelFont, greenBrush, 16, row1Y);
                g.DrawString("Product :", labelFont, subBrush, 32, row1Y);
                g.DrawString(string.IsNullOrEmpty(_lastResult.ProductCode) ? _lastResult.RawBarcode : _lastResult.ProductCode, labelFont, textBrush, 100, row1Y);

                // Row 2: Lot Number
                g.DrawString("✓", labelFont, greenBrush, 16, row2Y);
                g.DrawString("Lot No   :", labelFont, subBrush, 32, row2Y);
                g.DrawString(string.IsNullOrEmpty(_lastResult.LotNumber) ? "(Default)" : _lastResult.LotNumber, valueFont, textBrush, 100, row2Y);

                // Row 3: Quantity & Hardware Tag
                g.DrawString("✓", labelFont, greenBrush, 16, row3Y);
                g.DrawString("Quantity :", labelFont, subBrush, 32, row3Y);
                g.DrawString($"{_lastResult.Quantity:N0} pcs", labelFont, textBrush, 100, row3Y);

                if (_lastResult.IsHardwareScanner)
                {
                    using var badgeBrush = new SolidBrush(Color.FromArgb(224, 231, 255));
                    using var badgeTextBrush = new SolidBrush(Color.FromArgb(67, 56, 202));
                    var badgeFont = ZeroFontCache.Get("Segoe UI", 7f, FontStyle.Bold);
                    g.FillRectangle(badgeBrush, w - 85, row1Y, 65, 16);
                    g.DrawString("USB WEDGE", badgeFont, badgeTextBrush, w - 83, row1Y + 2);
                }
            }
            else
            {
                var guideFont = ZeroFontCache.Get("Segoe UI", 8.5f, FontStyle.Italic);
                using var guideBrush = new SolidBrush(Color.FromArgb(148, 163, 184));
                g.DrawString("No barcode scanned. Ready for hardware scanner or manual input...", guideFont, guideBrush, 18, metaY + 24);
            }
        }

        #endregion
    }

    /// <summary>
    /// Legacy alias for <see cref="ZBarcodeScan"/>.
    /// Preserved for backward compatibility.
    /// </summary>
    [Obsolete("ZeroBarcodeScanControl is deprecated and will be removed in 5 release cycles. Please migrate to ZBarcodeScan instead.")]
    [ToolboxItem(false)]
    public class ZeroBarcodeScanControl : ZBarcodeScan
    {
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZBarcodeScan"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("BarcodeScanControl is deprecated and will be removed in 5 release cycles. Please migrate to ZBarcodeScan instead.")]
    [ToolboxItem(false)]
    public class BarcodeScanControl : ZBarcodeScan
    {
    }

    #endregion
}
