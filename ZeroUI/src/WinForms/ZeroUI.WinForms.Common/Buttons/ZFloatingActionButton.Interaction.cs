using System;
using System.Drawing;
using System.Windows.Forms;

namespace ZeroUI.WinForms.Buttons
{
    public partial class ZFloatingActionButton
    {
        #region Mouse Interactions

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _isHovered = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _isHovered = false;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                _isPressed = true;
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _isPressed = false;
            Invalidate();
        }

        #endregion

        #region Helper: Attach To Parent

        /// <summary>
        /// Automatically binds this floating action button to the specified parent container,
        /// pinning it to the bottom-right corner with responsive repositioning on parent resize.
        /// </summary>
        public void AttachTo(Control parent, int rightMargin = 28, int bottomMargin = 28)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));

            this.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            this.Location = new Point(
                Math.Max(10, parent.ClientSize.Width - this.Width - rightMargin),
                Math.Max(10, parent.ClientSize.Height - this.Height - bottomMargin)
            );

            if (!parent.Controls.Contains(this))
            {
                parent.Controls.Add(this);
            }
            parent.Controls.SetChildIndex(this, 0);
            this.BringToFront();

            parent.Resize += (s, e) =>
            {
                this.Location = new Point(
                    Math.Max(10, parent.ClientSize.Width - this.Width - rightMargin),
                    Math.Max(10, parent.ClientSize.Height - this.Height - bottomMargin)
                );
                parent.Controls.SetChildIndex(this, 0);
                this.BringToFront();
            };
        }

        /// <summary>
        /// Shows this floating action button inside a dedicated top-most owned overlay window.
        /// Guarantees that the button will NEVER be obscured or covered by any child controls, tabs, or grids.
        /// </summary>
        public ZFloatingActionOverlay ShowAsOverlay(Form parentForm, int rightMargin = 28, int bottomMargin = 28)
        {
            if (parentForm == null) throw new ArgumentNullException(nameof(parentForm));
            var overlay = new ZFloatingActionOverlay(parentForm, this, rightMargin, bottomMargin);
            return overlay;
        }

        #endregion
    }
}
