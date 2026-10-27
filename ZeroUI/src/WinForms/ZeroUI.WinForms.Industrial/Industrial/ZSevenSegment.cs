using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using ZeroUI.Core.Industrial;
using ZeroUI.WinForms.Base;
using ZeroUI.WinForms.Icons;
using ZeroUI.WinForms.Native;

namespace ZeroUI.WinForms.Industrial
{
    // Shims forwarding to Core for binary & source compatibility
    public enum SevenSegmentColorPreset
    {
        Custom = ZeroUI.Core.Industrial.SevenSegmentColorPreset.Custom,
        NeonEmerald = ZeroUI.Core.Industrial.SevenSegmentColorPreset.NeonEmerald,
        NeonCyan = ZeroUI.Core.Industrial.SevenSegmentColorPreset.NeonCyan,
        NeonAmber = ZeroUI.Core.Industrial.SevenSegmentColorPreset.NeonAmber,
        NeonRed = ZeroUI.Core.Industrial.SevenSegmentColorPreset.NeonRed,
        CrispWhite = ZeroUI.Core.Industrial.SevenSegmentColorPreset.CrispWhite,
        UltraViolet = ZeroUI.Core.Industrial.SevenSegmentColorPreset.UltraViolet
    }

    public enum LeadingZeroDisplayMode
    {
        Blank = ZeroUI.Core.Industrial.LeadingZeroDisplayMode.Blank,
        DimmedGhost = ZeroUI.Core.Industrial.LeadingZeroDisplayMode.DimmedGhost,
        LitZero = ZeroUI.Core.Industrial.LeadingZeroDisplayMode.LitZero
    }

    public enum SevenSegmentFrameStyle
    {
        RecessedBezel = ZeroUI.Core.Industrial.SevenSegmentFrameStyle.RecessedBezel,
        AcrylicGlass = ZeroUI.Core.Industrial.SevenSegmentFrameStyle.AcrylicGlass,
        Borderless = ZeroUI.Core.Industrial.SevenSegmentFrameStyle.Borderless
    }

    /// <summary>
    /// Industrial 7-Segment Digital LED Display for SCADA & MES telemetry.
    /// Features authentic beveled polygon segment geometry, configurable slant (italic) angle,
    /// smart colon separators, decimal point integration, multi-stage neon glow, acrylic reflection,
    /// dynamic High-DPI Per-Monitor V2 scaling, and comprehensive alphanumeric status messaging support.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Industrial & SCADA")]
    [DefaultProperty("Value")]
    [Description("Industrial 7-Segment Digital LED Display for SCADA & MES telemetry")]
    [ToolboxBitmap(typeof(ZeroIcons), "ZeroSevenSegment.bmp")]
    [Designer("ZeroUI.WinForms.Design.Industrial.ZSevenSegmentDesigner, ZeroUI.WinForms.Design")]
    public partial class ZSevenSegment : ControlBase
    {
        private string _value = "1420";
        private Color _segmentColor = Color.FromArgb(52, 211, 153); // Neon Emerald
        private Color _dimColor = Color.FromArgb(20, 45, 35);
        private SevenSegmentColorPreset _colorPreset = SevenSegmentColorPreset.NeonEmerald;
        private int _digitCount = 6;
        private float _slantAngle = 7f;
        private float _segmentGap = 1.5f;
        private int _segmentThickness = 0;
        private LeadingZeroDisplayMode _leadingZeroMode = LeadingZeroDisplayMode.Blank;
        private HorizontalAlignment _textAlignment = HorizontalAlignment.Right;
        private SevenSegmentFrameStyle _frameStyle = SevenSegmentFrameStyle.RecessedBezel;
        private bool _showGhostSegments = true;
        private bool _showGlow = true;
        private bool _showGlassReflection = true;
        private bool _blinkColon = false;
        private bool _blink = false;
        private int _blinkInterval = 500;
        private string _unit = "";
        private Color _unitColor = Color.Empty;

        private readonly Timer? _blinkTimer;
        private bool _blinkPhase = true;

        public ZSevenSegment()
        {
            Size = new Size(180, 52);
            BackColor = Color.FromArgb(12, 18, 32); // Industrial dark acrylic enclosure

            _blinkTimer = new Timer { Interval = _blinkInterval };
            _blinkTimer.Tick += (s, e) =>
            {
                if (_blinkColon || _blink)
                {
                    _blinkPhase = !_blinkPhase;
                    Invalidate();
                }
            };

            if (!ZeroDesignHelper.IsInDesignMode(this))
            {
                _blinkTimer.Start();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _blinkTimer?.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Public Properties

        [Category("Appearance")]
        [DefaultValue("1420")]
        [Description("The text or numeric value to display on the 7-segment LED.")]
        public string Value
        {
            get => _value;
            set { _value = value ?? ""; Invalidate(); }
        }

        [Category("Appearance")]
        [Description("The primary illuminated color of the LED segments.")]
        public Color SegmentColor
        {
            get => _segmentColor;
            set
            {
                _segmentColor = value;
                _dimColor = Color.FromArgb(28, Math.Max(8, value.R / 5), Math.Max(8, value.G / 5), Math.Max(8, value.B / 5));
                _colorPreset = SevenSegmentColorPreset.Custom;
                Invalidate();
            }
        }

        [Category("Appearance")]
        [Description("The color of unlit/ghost segments when ShowGhostSegments is enabled.")]
        public Color DimColor
        {
            get => _dimColor;
            set { _dimColor = value; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue(SevenSegmentColorPreset.NeonEmerald)]
        [Description("Quick color presets tailored for industrial SCADA and MES monitoring.")]
        public SevenSegmentColorPreset ColorPreset
        {
            get => _colorPreset;
            set
            {
                _colorPreset = value;
                switch (value)
                {
                    case SevenSegmentColorPreset.NeonEmerald:
                        _segmentColor = Color.FromArgb(52, 211, 153);
                        _dimColor = Color.FromArgb(20, 45, 35);
                        break;
                    case SevenSegmentColorPreset.NeonCyan:
                        _segmentColor = Color.FromArgb(56, 189, 248);
                        _dimColor = Color.FromArgb(16, 38, 54);
                        break;
                    case SevenSegmentColorPreset.NeonAmber:
                        _segmentColor = Color.FromArgb(245, 158, 11);
                        _dimColor = Color.FromArgb(48, 32, 14);
                        break;
                    case SevenSegmentColorPreset.NeonRed:
                        _segmentColor = Color.FromArgb(239, 68, 68);
                        _dimColor = Color.FromArgb(48, 18, 18);
                        break;
                    case SevenSegmentColorPreset.CrispWhite:
                        _segmentColor = Color.FromArgb(248, 250, 252);
                        _dimColor = Color.FromArgb(35, 40, 48);
                        break;
                    case SevenSegmentColorPreset.UltraViolet:
                        _segmentColor = Color.FromArgb(192, 132, 252);
                        _dimColor = Color.FromArgb(38, 24, 52);
                        break;
                    case SevenSegmentColorPreset.Custom:
                        break;
                }
                Invalidate();
            }
        }

        [Category("Appearance")]
        [DefaultValue(6)]
        [Description("Minimum number of digit slots allocated on the display.")]
        public int DigitCount
        {
            get => _digitCount;
            set { _digitCount = Math.Max(1, Math.Min(16, value)); Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue(7f)]
        [Description("Italic slant angle in degrees (0 - 15 deg) for authentic industrial meters.")]
        public float SlantAngle
        {
            get => _slantAngle;
            set
            {
                _slantAngle = Math.Max(0f, Math.Min(15f, value));
                Invalidate();
            }
        }

        [Category("Appearance")]
        [DefaultValue(1.5f)]
        [Description("Physical isolation gap between adjacent beveled segments.")]
        public float SegmentGap
        {
            get => _segmentGap;
            set
            {
                _segmentGap = Math.Max(0.5f, Math.Min(5f, value));
                Invalidate();
            }
        }

        [Category("Appearance")]
        [DefaultValue(0)]
        [Description("Fixed segment thickness in pixels (0 = auto-calculated proportional to height).")]
        public int SegmentThickness
        {
            get => _segmentThickness;
            set
            {
                _segmentThickness = Math.Max(0, value);
                Invalidate();
            }
        }

        [Category("Appearance")]
        [DefaultValue(LeadingZeroDisplayMode.Blank)]
        [Description("How leading zeros are presented (Blank, DimmedGhost, or fully LitZero).")]
        public LeadingZeroDisplayMode LeadingZeroMode
        {
            get => _leadingZeroMode;
            set { _leadingZeroMode = value; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue(false)]
        [Browsable(false)]
        [Description("Backward-compatible wrapper: true for LitZero, false for Blank.")]
        public bool ShowLeadingZeros
        {
            get => _leadingZeroMode == LeadingZeroDisplayMode.LitZero;
            set
            {
                _leadingZeroMode = value ? LeadingZeroDisplayMode.LitZero : LeadingZeroDisplayMode.Blank;
                Invalidate();
            }
        }

        [Category("Appearance")]
        [DefaultValue(HorizontalAlignment.Right)]
        [Description("Text alignment within the display area.")]
        public HorizontalAlignment TextAlignment
        {
            get => _textAlignment;
            set { _textAlignment = value; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue(SevenSegmentFrameStyle.RecessedBezel)]
        [Description("Aesthetic frame and casing style.")]
        public SevenSegmentFrameStyle FrameStyle
        {
            get => _frameStyle;
            set { _frameStyle = value; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue(true)]
        [Description("Display unlit ghost segments for authentic digital LED physics.")]
        public bool ShowGhostSegments
        {
            get => _showGhostSegments;
            set { _showGhostSegments = value; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue(true)]
        [Description("Enable realistic multi-stage neon glow bloom around lit segments.")]
        public bool ShowGlow
        {
            get => _showGlow;
            set { _showGlow = value; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue(true)]
        [Description("Enable subtle acrylic glass sheen reflection across the upper display.")]
        public bool ShowGlassReflection
        {
            get => _showGlassReflection;
            set { _showGlassReflection = value; Invalidate(); }
        }

        [Category("Behavior")]
        [DefaultValue(false)]
        [Description("Blink the colon separator at a 1Hz rate (ideal for Takt time and clocks).")]
        public bool BlinkColon
        {
            get => _blinkColon;
            set { _blinkColon = value; Invalidate(); }
        }

        [Category("Behavior")]
        [DefaultValue(false)]
        [Description("Flash the entire readout for alarm or critical threshold conditions.")]
        public bool Blink
        {
            get => _blink;
            set { _blink = value; Invalidate(); }
        }

        [Category("Behavior")]
        [DefaultValue(500)]
        [Description("Blink rate interval in milliseconds.")]
        public int BlinkInterval
        {
            get => _blinkInterval;
            set
            {
                _blinkInterval = Math.Max(50, value);
                if (_blinkTimer != null) _blinkTimer.Interval = _blinkInterval;
            }
        }

        [Category("Appearance")]
        [DefaultValue("")]
        [Description("Measurement unit badge displayed on the right edge (e.g., 's', 'pcs', '°C', 'BAR').")]
        public string Unit
        {
            get => _unit;
            set { _unit = value ?? ""; Invalidate(); }
        }

        [Category("Appearance")]
        [Description("Custom color for the measurement unit badge.")]
        public Color UnitColor
        {
            get => _unitColor;
            set { _unitColor = value; Invalidate(); }
        }

        #endregion
    }

    /// <summary>
    /// Legacy alias for <see cref="ZSevenSegment"/>.
    /// Preserved for backward compatibility.
    /// </summary>
    [Obsolete("ZeroSevenSegment is deprecated and will be removed in 5 release cycles. Please migrate to ZSevenSegment instead.")]
    [ToolboxItem(false)]
    public class ZeroSevenSegment : ZSevenSegment
    {
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZSevenSegment"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("SevenSegment is deprecated and will be removed in 5 release cycles. Please migrate to ZSevenSegment instead.")]
    [ToolboxItem(false)]
    public class SevenSegment : ZSevenSegment
    {
    }

    #endregion
}
