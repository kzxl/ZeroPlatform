using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Base
{
    /// <summary>
    /// Universal base class for all popup and drop-down editors (ComboBox, LookUp, DateEdit, ColorPickEdit).
    /// Standardizes DropDownHost lifecycle, automatic DropUp/DropDown boundary clamping,
    /// chevron arrow glyph rendering, and keyboard dropdown shortcuts (Alt+Down, F4, Escape).
    /// </summary>
    /// <typeparam name="TValue">The primary strongly-typed value managed by the editor.</typeparam>
    /// <typeparam name="TPopupContent">The specialized WinForms control hosted within the dropdown popup.</typeparam>
    [ToolboxItem(false)]
    public abstract class PopupEditorBase<TValue, TPopupContent> : EditorBase<TValue>
        where TPopupContent : Control, new()
    {
        private readonly DropDownHost _dropDownHost;
        private readonly TPopupContent _popupContent;
        private Rectangle _chevronRect;
        private bool _hoverOnChevron;
        private int _popupWidth = 240;
        private int _popupHeight = 280;

        #region Events

        public event EventHandler? DropDownOpened;
        public event EventHandler? DropDownClosed;

        #endregion

        #region Properties

        [Browsable(false)]
        public TPopupContent PopupContent => _popupContent;

        [Browsable(false)]
        public bool IsDroppedDown => _dropDownHost.Visible;

        [Category("Appearance")]
        [DefaultValue(240)]
        [Description("The width of the dropdown popup window in pixels.")]
        public int PopupWidth
        {
            get => _popupWidth;
            set => _popupWidth = Math.Max(50, value);
        }

        [Category("Appearance")]
        [DefaultValue(280)]
        [Description("The height of the dropdown popup window in pixels.")]
        public int PopupHeight
        {
            get => _popupHeight;
            set => _popupHeight = Math.Max(50, value);
        }

        #endregion

        protected PopupEditorBase()
        {
            Cursor = Cursors.Hand;

            _popupContent = new TPopupContent();
            _dropDownHost = new DropDownHost
            {
                Content = _popupContent
            };

            _dropDownHost.Opened += (s, e) =>
            {
                OnDropDownOpened();
                Invalidate();
            };

            _dropDownHost.Closed += (s, e) =>
            {
                OnDropDownClosed();
                Invalidate();
            };
        }

        #region Dropdown Open / Close Actions

        public virtual void ShowDropDown()
        {
            if (ReadOnly || !Enabled || IsDroppedDown)
            {
                return;
            }

            OnBeforeDropDownOpen();
            int effectiveWidth = Math.Max(Width, _popupWidth);
            _dropDownHost.ShowDropDown(this, effectiveWidth, _popupHeight);
        }

        public virtual void CloseDropDown()
        {
            if (IsDroppedDown)
            {
                _dropDownHost.Close();
            }
        }

        public virtual void ToggleDropDown()
        {
            if (IsDroppedDown)
            {
                CloseDropDown();
            }
            else
            {
                ShowDropDown();
            }
        }

        protected virtual void OnBeforeDropDownOpen()
        {
        }

        protected virtual void OnDropDownOpened()
        {
            DropDownOpened?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void OnDropDownClosed()
        {
            DropDownClosed?.Invoke(this, EventArgs.Empty);
        }

        #endregion

        #region Mouse & Keyboard Interaction

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left && !ReadOnly && Enabled)
            {
                // If clicked clear button, clear value instead of opening dropdown
                if (ShowClearButton && ClearButtonRect.Contains(e.Location))
                {
                    Clear();
                    return;
                }

                ToggleDropDown();
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            bool prevChevronHover = _hoverOnChevron;
            bool prevClearHover = HoverOnClear;

            _hoverOnChevron = _chevronRect.Contains(e.Location);
            HoverOnClear = ShowClearButton && ClearButtonRect.Contains(e.Location);

            if (prevChevronHover != _hoverOnChevron || prevClearHover != HoverOnClear)
            {
                Invalidate();
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (!ReadOnly && Enabled)
            {
                if (keyData == (Keys.Alt | Keys.Down) || keyData == Keys.F4)
                {
                    ToggleDropDown();
                    return true;
                }
                else if (keyData == Keys.Escape && IsDroppedDown)
                {
                    CloseDropDown();
                    return true;
                }
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        #endregion

        #region Drawing

        /// <summary>
        /// Renders the standard dropdown chevron glyph on the right side of the editor.
        /// </summary>
        protected virtual void DrawChevron(Graphics g, Rectangle bounds, ZeroThemePalette palette)
        {
            int chevronWidth = 24;
            _chevronRect = new Rectangle(bounds.Right - chevronWidth, bounds.Y, chevronWidth, bounds.Height);

            int cx = _chevronRect.X + _chevronRect.Width / 2;
            int cy = _chevronRect.Y + _chevronRect.Height / 2;

            Color arrowColor = IsDroppedDown || _hoverOnChevron ? palette.Primary : palette.TextSecondary;
            using (var pen = new Pen(arrowColor, 1.6f))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;

                if (IsDroppedDown)
                {
                    // Up arrow
                    g.DrawLine(pen, cx - 4, cy + 2, cx, cy - 2);
                    g.DrawLine(pen, cx, cy - 2, cx + 4, cy + 2);
                }
                else
                {
                    // Down arrow
                    g.DrawLine(pen, cx - 4, cy - 2, cx, cy + 2);
                    g.DrawLine(pen, cx, cy + 2, cx + 4, cy - 2);
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _dropDownHost.Dispose();
            }
            base.Dispose(disposing);
        }

        #endregion
    }

    /// <summary>
    /// Obsolete alias for <see cref="PopupEditorBase{TValue, TPopupContent}"/> to maintain backward compatibility.
    /// </summary>
    [Obsolete("Use PopupEditorBase<TValue, TPopupContent> instead.")]
    public abstract class ZeroPopupEditorBase<TValue, TPopupContent> : PopupEditorBase<TValue, TPopupContent>
        where TPopupContent : Control, new()
    {
    }
}

