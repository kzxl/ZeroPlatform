using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.Core.Rendering;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Base
{
    /// <summary>
    /// High-performance architectural base class for custom vector schematic and telemetry canvas controls.
    /// Provides built-in double buffering, scoped theme palette resolution, automated ZeroAnimationClock lifecycle,
    /// and standard GDI+ anti-aliased surface rendering.
    /// </summary>
    [ToolboxItem(false)]
    public abstract class VisualControlBase : ControlBase
    {
        private IDisposable? _animSub;

        /// <summary>
        /// Determines whether this control automatically subscribes to ZeroAnimationClock for dynamic 60 FPS repainting.
        /// Derived controls override this to return true if they feature continuous particle flows, spinning impellers, or tickers.
        /// </summary>
        protected virtual bool AutoAnimate => false;

        protected VisualControlBase()
        {
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (AutoAnimate && _animSub == null)
            {
                _animSub = ZeroAnimationClock.Subscribe((delta, frame) =>
                {
                    if (IsHandleCreated && !IsDisposed)
                    {
                        OnAnimationTick(delta, frame);
                        Invalidate();
                    }
                });
            }
        }

        /// <summary>
        /// Invoked on each animation clock tick when AutoAnimate is true. Override to advance kinematics or simulation state.
        /// </summary>
        protected virtual void OnAnimationTick(double delta, long frame)
        {
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            _animSub?.Dispose();
            _animSub = null;
            base.OnHandleDestroyed(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _animSub?.Dispose();
                _animSub = null;
            }
            base.Dispose(disposing);
        }

        /// <summary>
        /// Prepares the GDI+ graphics context with anti-aliasing, clears the canvas with the active theme background,
        /// and delegates rendering to OnDrawVisual.
        /// </summary>
        protected sealed override void OnPaint(PaintEventArgs e)
        {
            if (Width <= 0 || Height <= 0) return;

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var palette = CurrentPalette;

            using (var bgBrush = new SolidBrush(palette.Background))
            {
                g.FillRectangle(bgBrush, ClientRectangle);
            }

            OnDrawVisual(g, ClientRectangle, palette);
        }

        /// <summary>
        /// Renders the visual vector content onto the prepared GDI+ surface.
        /// </summary>
        /// <param name="g">Anti-aliased Graphics context.</param>
        /// <param name="bounds">Client bounds rectangle.</param>
        /// <param name="palette">Active theme color palette (supporting local skin overrides).</param>
        protected abstract void OnDrawVisual(Graphics g, Rectangle bounds, ZeroThemePalette palette);

        #region Standard Drawing Helpers

        protected void DrawCard(Graphics g, Rectangle bounds, Color backColor, Color borderColor, int cornerRadius = 4)
        {
            PaintHelper.DrawCard(g, bounds, backColor, borderColor, cornerRadius);
        }

        protected void DrawStatusBadge(Graphics g, Rectangle bounds, string text, Font font, Color statusColor, Color textColor, int cornerRadius = 3)
        {
            PaintHelper.DrawStatusBadge(g, bounds, text, font, statusColor, textColor, cornerRadius);
        }

        protected void DrawCardBox(Graphics g, Rectangle bounds, string title, Font titleFont, ZeroThemePalette palette)
        {
            PaintHelper.DrawCardBox(g, bounds, title, titleFont, palette);
        }

        protected void DrawKpiCell(Graphics g, Rectangle bounds, string label, string value, Color valueColor, Font labelFont, Font valueFont, ZeroThemePalette palette)
        {
            PaintHelper.DrawKpiCell(g, bounds, label, value, valueColor, labelFont, valueFont, palette);
        }

        protected void DrawDataRow(Graphics g, Rectangle bounds, string label, string value, Color labelColor, Color valueColor, Font labelFont, Font valueFont)
        {
            PaintHelper.DrawDataRow(g, bounds, label, value, labelColor, valueColor, labelFont, valueFont);
        }

        protected void DrawLedIndicator(Graphics g, int x, int y, Color ledColor, string label, Font font)
        {
            PaintHelper.DrawLedIndicator(g, x, y, ledColor, label, font);
        }

        #endregion
    }

    /// <summary>
    /// Obsolete alias for <see cref="VisualControlBase"/> to maintain backward compatibility.
    /// </summary>
    [Obsolete("Use VisualControlBase instead.")]
    public abstract class ZeroVisualControlBase : VisualControlBase
    {
    }
}

