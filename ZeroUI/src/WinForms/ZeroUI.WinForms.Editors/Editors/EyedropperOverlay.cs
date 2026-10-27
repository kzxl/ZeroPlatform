using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace ZeroUI.WinForms.Editors
{
    /// <summary>
    /// Desktop-wide screen color sampler overlay.
    /// Freezes a high-performance snapshot of the entire virtual desktop screen,
    /// displaying a real-time magnified 9x9 pixel loupe with hex preview for pixel-perfect color picking.
    /// </summary>
    internal class EyedropperOverlay : Form
    {
        private readonly Bitmap? _desktopSnapshot;
        private readonly Action<Color> _onColorSelected;
        private readonly Action _onCancelled;
        private Point _currentScreenPos;
        private Color _currentColor = Color.Black;

        public EyedropperOverlay(Action<Color> onColorSelected, Action onCancelled)
        {
            _onColorSelected = onColorSelected;
            _onCancelled = onCancelled;

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            Cursor = Cursors.Cross;
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);

            Rectangle vScreen = SystemInformation.VirtualScreen;
            Bounds = vScreen;

            try
            {
                _desktopSnapshot = new Bitmap(vScreen.Width, vScreen.Height, PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(_desktopSnapshot))
                {
                    g.CopyFromScreen(vScreen.X, vScreen.Y, 0, 0, vScreen.Size, CopyPixelOperation.SourceCopy);
                }
            }
            catch
            {
                _desktopSnapshot = null;
            }

            _currentScreenPos = Cursor.Position;
            UpdateHoverColor();
        }

        private void UpdateHoverColor()
        {
            if (_desktopSnapshot == null) return;

            Rectangle vScreen = SystemInformation.VirtualScreen;
            int localX = _currentScreenPos.X - vScreen.X;
            int localY = _currentScreenPos.Y - vScreen.Y;

            if (localX >= 0 && localX < _desktopSnapshot.Width && localY >= 0 && localY < _desktopSnapshot.Height)
            {
                _currentColor = _desktopSnapshot.GetPixel(localX, localY);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            _currentScreenPos = Cursor.Position;
            UpdateHoverColor();
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                UpdateHoverColor();
                _onColorSelected(_currentColor);
                Close();
            }
            else if (e.Button == MouseButtons.Right)
            {
                _onCancelled();
                Close();
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape)
            {
                _onCancelled();
                Close();
            }
            else if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space)
            {
                UpdateHoverColor();
                _onColorSelected(_currentColor);
                Close();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;

            // 1. Draw desktop snapshot background
            if (_desktopSnapshot != null)
            {
                g.DrawImageUnscaled(_desktopSnapshot, 0, 0);
            }

            // 2. Compute Loupe position relative to this window
            Rectangle vScreen = SystemInformation.VirtualScreen;
            int localCursorX = _currentScreenPos.X - vScreen.X;
            int localCursorY = _currentScreenPos.Y - vScreen.Y;

            int loupeSize = 136;
            int offset = 20;
            int loupeX = localCursorX + offset;
            int loupeY = localCursorY + offset;

            if (loupeX + loupeSize > ClientSize.Width)
            {
                loupeX = localCursorX - loupeSize - offset;
            }
            if (loupeY + loupeSize + 32 > ClientSize.Height)
            {
                loupeY = localCursorY - loupeSize - offset - 32;
            }

            var loupeRect = new Rectangle(loupeX, loupeY, loupeSize, loupeSize + 32);

            // 3. Draw Loupe shadow & card
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var shadowBrush = new SolidBrush(Color.FromArgb(90, 0, 0, 0)))
            {
                g.FillRectangle(shadowBrush, loupeRect.X + 4, loupeRect.Y + 4, loupeRect.Width, loupeRect.Height);
            }
            using (var bgBrush = new SolidBrush(Color.FromArgb(24, 28, 38)))
            {
                g.FillRectangle(bgBrush, loupeRect);
            }

            // 4. Magnified 9x9 Pixel Grid
            int gridDimension = 9;
            int pixelCellSize = loupeSize / gridDimension; // 15px each
            int gridStartX = loupeX + (loupeSize - gridDimension * pixelCellSize) / 2;
            int gridStartY = loupeY + 6;

            for (int dy = -4; dy <= 4; dy++)
            {
                for (int dx = -4; dx <= 4; dx++)
                {
                    int sampleX = localCursorX + dx;
                    int sampleY = localCursorY + dy;

                    Color cellColor = Color.Black;
                    if (_desktopSnapshot != null && sampleX >= 0 && sampleX < _desktopSnapshot.Width && sampleY >= 0 && sampleY < _desktopSnapshot.Height)
                    {
                        cellColor = _desktopSnapshot.GetPixel(sampleX, sampleY);
                    }

                    int cellX = gridStartX + (dx + 4) * pixelCellSize;
                    int cellY = gridStartY + (dy + 4) * pixelCellSize;

                    using (var b = new SolidBrush(cellColor))
                    {
                        g.FillRectangle(b, cellX, cellY, pixelCellSize, pixelCellSize);
                    }
                }
            }

            // Grid cell borders
            using (var gridPen = new Pen(Color.FromArgb(40, 255, 255, 255), 1f))
            {
                for (int i = 0; i <= gridDimension; i++)
                {
                    g.DrawLine(gridPen, gridStartX + i * pixelCellSize, gridStartY, gridStartX + i * pixelCellSize, gridStartY + gridDimension * pixelCellSize);
                    g.DrawLine(gridPen, gridStartX, gridStartY + i * pixelCellSize, gridStartX + gridDimension * pixelCellSize, gridStartY + i * pixelCellSize);
                }
            }

            // Highlight Center Pixel Box
            int centerCellX = gridStartX + 4 * pixelCellSize;
            int centerCellY = gridStartY + 4 * pixelCellSize;
            using (var centerPen = new Pen(Color.White, 2f))
            {
                g.DrawRectangle(centerPen, centerCellX, centerCellY, pixelCellSize, pixelCellSize);
            }

            // 5. Color Info Footer (Swatch + Hex string)
            int footerY = loupeY + loupeSize + 8;
            var swatchRect = new Rectangle(loupeX + 8, footerY, 18, 18);
            using (var sb = new SolidBrush(_currentColor))
            {
                g.FillRectangle(sb, swatchRect);
            }
            using (var sp = new Pen(Color.White, 1f))
            {
                g.DrawRectangle(sp, swatchRect);
            }

            string hexCode = $"#{_currentColor.R:X2}{_currentColor.G:X2}{_currentColor.B:X2}";
            using (var textBrush = new SolidBrush(Color.White))
            using (var font = new Font("Segoe UI", 9f, FontStyle.Bold))
            {
                g.DrawString(hexCode, font, textBrush, loupeX + 32, footerY);
            }

            // Outer border for the loupe card
            using (var borderPen = new Pen(Color.FromArgb(99, 102, 241), 1.5f))
            {
                g.DrawRectangle(borderPen, loupeRect);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _desktopSnapshot?.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// Legacy alias for <see cref="EyedropperOverlay"/>.
    /// Preserved for backward compatibility.
    /// </summary>
    [Obsolete("ZeroEyedropperOverlay is deprecated. Use EyedropperOverlay instead.")]
    internal sealed class ZeroEyedropperOverlay : EyedropperOverlay
    {
        public ZeroEyedropperOverlay(Action<Color> onColorSelected, Action onCancelled)
            : base(onColorSelected, onCancelled) { }
    }
}
