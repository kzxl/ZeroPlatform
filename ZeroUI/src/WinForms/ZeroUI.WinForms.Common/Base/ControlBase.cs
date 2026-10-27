using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using ZeroUI.Core.Theme;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Base
{
    /// <summary>
    /// High-performance architectural base class for all ZeroUI WinForms controls.
    /// Provides built-in double buffering, zero-flicker GDI+ rendering, and automatic
    /// skin lifecycle synchronization with local override capabilities.
    /// </summary>
    [ToolboxItem(false)]
    public abstract class ControlBase : Control, IZeroSkinnable, IZeroDpiScalable
    {
        private bool _useDefaultSkin = true;
        private ZeroSkin? _customSkin;
        private readonly Action<ZeroSkin> _skinChangedHandler;
        private readonly EventHandler _themeChangedHandler;
        private bool _isDisposed;
        private float _currentDpiScale = 1.0f;

        #region Properties & Skinnable Contract

        [Category("Skin")]
        [DefaultValue(true)]
        [Description("Determines whether this control consumes the global ZeroSkinManager theme or a localized CustomSkin.")]
        public bool UseDefaultSkin
        {
            get => _useDefaultSkin;
            set
            {
                if (_useDefaultSkin != value)
                {
                    _useDefaultSkin = value;
                    OnThemeChanged(EffectiveSkin);
                    Invalidate();
                }
            }
        }

        [Category("Skin")]
        [DefaultValue(null)]
        [Description("Localized custom skin applied when UseDefaultSkin is false.")]
        public ZeroSkin? CustomSkin
        {
            get => _customSkin;
            set
            {
                if (_customSkin != value)
                {
                    _customSkin = value;
                    if (!_useDefaultSkin)
                    {
                        OnThemeChanged(EffectiveSkin);
                        Invalidate();
                    }
                }
            }
        }

        [Browsable(false)]
        public ZeroSkin EffectiveSkin => ZeroSkinManager.ResolveSkin(this);

        /// <summary>
        /// Gets the current palette tokens corresponding to the effective skin.
        /// </summary>
        [Browsable(false)]
        protected ZeroPaletteTokens Palette => EffectiveSkin.Tokens;

        private ZeroThemePalette? _localPalette;

        /// <summary>
        /// Gets the active GDI+ color palette for this control, resolving to either the global ZeroTheme.Colors or localized CustomSkin.
        /// </summary>
        [Browsable(false)]
        protected ZeroThemePalette CurrentPalette
        {
            get
            {
                if (UseDefaultSkin) return ZeroTheme.Colors;
                if (_localPalette == null)
                {
                    _localPalette = ZeroTheme.CreatePaletteFromSkin(EffectiveSkin);
                }
                return _localPalette;
            }
        }

        /// <summary>
        /// Gets the active DPI scale factor for this control relative to 96 DPI baseline (e.g. 1.0 = 100%, 1.5 = 150%, 2.0 = 200%).
        /// </summary>
        [Browsable(false)]
        public float DpiScale => _currentDpiScale;

        /// <summary>
        /// Scales an integer measurement based on the control's effective DPI scale factor.
        /// </summary>
        public int ScaleDpi(int value) => ZeroDpi.Scale(value, _currentDpiScale);

        /// <summary>
        /// Scales a Size structure based on the control's effective DPI scale factor.
        /// </summary>
        public Size ScaleDpi(Size size) => ZeroDpi.Scale(size, _currentDpiScale);

        /// <summary>
        /// Scales a Padding structure based on the control's effective DPI scale factor.
        /// </summary>
        public Padding ScaleDpi(Padding padding) => ZeroDpi.Scale(padding, _currentDpiScale);

        /// <summary>
        /// Applies High-DPI Per-Monitor V2 scaling to this control and invalidates the surface.
        /// </summary>
        public virtual void ApplyDpiScaling(float scaleFactor)
        {
            if (scaleFactor <= 0f) scaleFactor = 1.0f;
            float ratio = scaleFactor / _currentDpiScale;
            _currentDpiScale = scaleFactor;

            OnApplyDpiScaling(scaleFactor, ratio);
            Invalidate();
        }

        /// <summary>
        /// Invoked when the control's DPI scale factor is applied or modified.
        /// Override in derived controls to rescale internal dimension metrics (e.g., margins, paddings, item heights).
        /// </summary>
        protected virtual void OnApplyDpiScaling(float scaleFactor, float factorRatio)
        {
        }

        #endregion

        protected ControlBase()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor, true);

            DoubleBuffered = true;

            _skinChangedHandler = skin =>
            {
                if (_useDefaultSkin && !_isDisposed && !IsDisposed && !Disposing)
                {
                    if (IsHandleCreated && InvokeRequired)
                    {
                        try { BeginInvoke(new Action(() => { OnThemeChanged(skin); Invalidate(); })); }
                        catch (ObjectDisposedException) { }
                    }
                    else
                    {
                        try
                        {
                            OnThemeChanged(skin);
                            Invalidate();
                        }
                        catch (ObjectDisposedException) { }
                    }
                }
            };

            _themeChangedHandler = (s, e) =>
            {
                if (_useDefaultSkin && !_isDisposed && !IsDisposed && !Disposing)
                {
                    if (IsHandleCreated && InvokeRequired)
                    {
                        try { BeginInvoke(new Action(() => { OnThemeChanged(EffectiveSkin); Invalidate(); })); }
                        catch (ObjectDisposedException) { }
                    }
                    else
                    {
                        try
                        {
                            OnThemeChanged(EffectiveSkin);
                            Invalidate();
                        }
                        catch (ObjectDisposedException) { }
                    }
                }
            };

            ZeroSkinManager.SkinChanged += _skinChangedHandler;
            ZeroTheme.ThemeChanged += _themeChangedHandler;

            OnThemeChanged(EffectiveSkin);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            float factor = ZeroDpi.GetScaleFactor(this);
            if (Math.Abs(factor - _currentDpiScale) > 0.001f)
            {
                ApplyDpiScaling(factor);
            }
            OnThemeChanged(EffectiveSkin);
            Invalidate();
        }

        /// <summary>
        /// Applies the specified skin to this control and invalidates the surface.
        /// </summary>
        public virtual void ApplySkin(ZeroSkin skin)
        {
            if (skin == null) throw new ArgumentNullException(nameof(skin));
            _customSkin = skin;
            _useDefaultSkin = false;
            OnThemeChanged(skin);
            Invalidate();
        }

        /// <summary>
        /// Invoked when the active theme is changed globally or locally.
        /// Override in derived controls to update internal GDI+ pens, brushes, or child controls.
        /// </summary>
        /// <param name="skin">The newly effective skin.</param>
        protected virtual void OnThemeChanged(ZeroSkin skin)
        {
            if (skin == null) return;
            _localPalette = !UseDefaultSkin ? ZeroTheme.CreatePaletteFromSkin(skin) : null;
            BackColor = ColorTranslator.FromHtml(skin.Tokens.BgCard);
            ForeColor = ColorTranslator.FromHtml(skin.Tokens.TextPrimary);
        }

        protected override void ScaleControl(SizeF factor, BoundsSpecified specified)
        {
            base.ScaleControl(factor, specified);
            float effectiveFactor = factor.Height > 0f ? factor.Height : factor.Width;
            if (effectiveFactor > 0f && Math.Abs(effectiveFactor - 1.0f) > 0.001f)
            {
                ApplyDpiScaling(_currentDpiScale * effectiveFactor);
            }
            OnDpiScaleChanged(factor.Width);
        }

        /// <summary>
        /// Invoked when the control's DPI scale factor changes.
        /// </summary>
        protected virtual void OnDpiScaleChanged(float scaleFactor)
        {
            Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !_isDisposed)
            {
                _isDisposed = true;
                ZeroSkinManager.SkinChanged -= _skinChangedHandler;
                ZeroTheme.ThemeChanged -= _themeChangedHandler;
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// Obsolete alias for <see cref="ControlBase"/> to maintain backward compatibility.
    /// </summary>
    [Obsolete("Use ControlBase instead.")]
    public abstract class ZeroControlBase : ControlBase
    {
    }
}

