using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ZeroUI.WinForms.Overlays
{
    /// <summary>
    /// Semi-transparent backdrop window providing dimmed glass overlay with spotlight cutout.
    /// Eliminates solid black backdrop while preserving full visibility of the underlying form.
    /// </summary>
    internal sealed class ZTourBackdropForm : Form
    {
        private readonly Form _ownerForm;
        private readonly ZTour _tour;

        public ZTourBackdropForm(Form owner, ZTour tour)
        {
            _ownerForm = owner ?? throw new ArgumentNullException(nameof(owner));
            _tour = tour ?? throw new ArgumentNullException(nameof(tour));

            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            KeyPreview = true;
            BackColor = Color.FromArgb(10, 15, 26);
            Opacity = 0.55; // Mờ 55% dịu mắt, thấy rõ ràng mọi chi tiết của form bên dưới

            SyncBounds();

            _ownerForm.LocationChanged += Owner_BoundsChanged;
            _ownerForm.SizeChanged += Owner_BoundsChanged;
        }

        public void SyncBounds()
        {
            if (_ownerForm != null && !_ownerForm.IsDisposed)
            {
                Location = _ownerForm.PointToScreen(Point.Empty);
                Size = _ownerForm.ClientSize;
            }
        }

        private void Owner_BoundsChanged(object? sender, EventArgs e)
        {
            SyncBounds();
            if (_tour.CurrentStep != null)
            {
                UpdateHole(_tour.CurrentStep);
            }
        }

        public void UpdateHole(ZTourStep? step)
        {
            if (ClientSize.Width <= 0 || ClientSize.Height <= 0) return;

            if (step != null && step.Mask)
            {
                var targetRect = ResolveTargetRect(step);
                if (!targetRect.IsEmpty)
                {
                    using var fullPath = new GraphicsPath();
                    using var holePath = CreateRoundedRectPath(targetRect, Math.Max(2, step.CornerRadius));
                    fullPath.AddRectangle(new Rectangle(0, 0, ClientSize.Width, ClientSize.Height));
                    var region = new Region(fullPath);
                    region.Exclude(holePath);
                    Region = region;
                    return;
                }
            }

            Region = null;
        }

        private Rectangle ResolveTargetRect(ZTourStep step)
        {
            Control? target = step.Target;
            if (target == null && !string.IsNullOrWhiteSpace(step.TargetName))
            {
                var matches = _ownerForm.Controls.Find(step.TargetName, true);
                if (matches.Length > 0) target = matches[0];
            }

            if (target == null || !target.Visible)
                return Rectangle.Empty;

            var screenPt = target.PointToScreen(Point.Empty);
            var localPt = PointToClient(screenPt);
            var pad = step.TargetPadding;

            return new Rectangle(
                localPt.X - pad.Left,
                localPt.Y - pad.Top,
                Math.Max(10, target.Width + pad.Left + pad.Right),
                Math.Max(10, target.Height + pad.Top + pad.Bottom));
        }

        private static GraphicsPath CreateRoundedRectPath(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            int diameter = radius * 2;
            if (rect.Width <= 0 || rect.Height <= 0) return path;

            path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
            path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
            path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            // Click ra ngoài vùng backdrop mờ
            _tour.Close(completed: false);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _ownerForm != null)
            {
                _ownerForm.LocationChanged -= Owner_BoundsChanged;
                _ownerForm.SizeChanged -= Owner_BoundsChanged;
            }
            base.Dispose(disposing);
        }
    }
}
