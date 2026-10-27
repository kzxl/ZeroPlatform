using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.Core.Icons;
using ZeroUI.WinForms.Base;

namespace ZeroUI.WinForms.Buttons
{
    public enum ZFloatingActionIcon
    {
        AiSparkles,
        Chat,
        Plus,
        Vector,
        Custom
    }

    /// <summary>
    /// Modern theme-aware Floating Action Button (FAB) adhering to the canonical Z-prefix standard.
    /// Provides 100% transparent anti-aliased circular geometry without rectangular clipping artifacts,
    /// dynamic gradient backgrounds, vector AI/action glyphs, notification badge overlays, and smooth hover feedback.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Buttons")]
    [DefaultEvent("Click")]
    [Description("Theme-aware circular floating action button with 100% transparent corners, vector iconography, and badge support")]
    public partial class ZFloatingActionButton : ControlBase
    {
        #region Fields

        private ZFloatingActionIcon _iconType = ZFloatingActionIcon.AiSparkles;
        private IconKey? _vectorIconKey;
        private Image? _customIcon;
        private Color? _gradientStart;
        private Color? _gradientEnd;
        private Color _iconColor = Color.White;
        private bool _useGradient = true;
        private string? _badgeText;
        private Color _badgeColor = Color.FromArgb(239, 68, 68); // Red-500
        private ToolTip? _internalToolTip;
        private string? _toolTipText;

        private bool _isHovered = false;
        private bool _isPressed = false;

        #endregion

        public ZFloatingActionButton()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor, true);

            Size = new Size(56, 56);
            Cursor = Cursors.Hand;
            BackColor = Color.Transparent;
            UpdateRegion();
        }

        #region Properties

        [Category("Appearance")]
        [DefaultValue(ZFloatingActionIcon.AiSparkles)]
        [Description("The icon type rendered inside the circular button.")]
        public ZFloatingActionIcon IconType
        {
            get => _iconType;
            set
            {
                if (_iconType != value)
                {
                    _iconType = value;
                    Invalidate();
                }
            }
        }

        [Category("Appearance")]
        [DefaultValue(null)]
        [Description("Custom image icon when IconType is set to Custom.")]
        public Image? CustomIcon
        {
            get => _customIcon;
            set
            {
                _customIcon = value;
                if (_iconType == ZFloatingActionIcon.Custom)
                    Invalidate();
            }
        }

        [Category("Appearance")]
        [DefaultValue(null)]
        [Description("Vector icon key from ZeroUI standard icon set when IconType is set to Vector.")]
        public IconKey? VectorIconKey
        {
            get => _vectorIconKey;
            set
            {
                _vectorIconKey = value;
                if (_iconType == ZFloatingActionIcon.Vector)
                    Invalidate();
            }
        }

        [Category("Appearance")]
        [DefaultValue(typeof(Color), "White")]
        [Description("Color used to tint the vector glyph or sparkle.")]
        public Color IconColor
        {
            get => _iconColor;
            set
            {
                _iconColor = value;
                Invalidate();
            }
        }

        [Category("Appearance")]
        [DefaultValue(true)]
        [Description("Determines whether to render a modern gradient background.")]
        public bool UseGradient
        {
            get => _useGradient;
            set
            {
                _useGradient = value;
                Invalidate();
            }
        }

        [Category("Appearance")]
        [Description("Start color of the background gradient. If null, adopts theme Primary.")]
        public Color? GradientStartColor
        {
            get => _gradientStart;
            set
            {
                _gradientStart = value;
                Invalidate();
            }
        }

        [Category("Appearance")]
        [Description("End color of the background gradient. If null, adopts theme PrimaryDark.")]
        public Color? GradientEndColor
        {
            get => _gradientEnd;
            set
            {
                _gradientEnd = value;
                Invalidate();
            }
        }

        [Category("Appearance")]
        [DefaultValue(null)]
        [Description("Notification badge text or count displayed at the top-right corner.")]
        public string? BadgeText
        {
            get => _badgeText;
            set
            {
                _badgeText = value;
                Invalidate();
            }
        }

        [Category("Appearance")]
        [Description("Background color of the notification badge.")]
        public Color BadgeColor
        {
            get => _badgeColor;
            set
            {
                _badgeColor = value;
                Invalidate();
            }
        }

        [Category("Behavior")]
        [DefaultValue(null)]
        [Description("Tooltip text displayed when hovering over the button.")]
        public string? ToolTipText
        {
            get => _toolTipText;
            set
            {
                _toolTipText = value;
                if (!string.IsNullOrEmpty(_toolTipText))
                {
                    _internalToolTip ??= new ToolTip();
                    _internalToolTip.SetToolTip(this, _toolTipText);
                }
                else
                {
                    _internalToolTip?.SetToolTip(this, null);
                }
            }
        }

        #endregion

        #region Region & Transparency (Zero Rectangular Artifacts)

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            UpdateRegion();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            UpdateRegion();
        }

        /// <summary>
        /// Clips the window geometry to a pure circle, permanently eliminating rectangular 4-corner artifacts.
        /// </summary>
        public void UpdateRegion()
        {
            int d = Math.Min(Width, Height);
            if (d <= 0) return;

            using (var path = new GraphicsPath())
            {
                path.AddEllipse(0, 0, d, d);
                this.Region = new Region(path);
            }
        }

        #endregion

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _internalToolTip?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
