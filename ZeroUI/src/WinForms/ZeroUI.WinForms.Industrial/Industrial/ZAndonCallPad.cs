using System;

using ZeroUI.WinForms.Icons;using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.WinForms.Native;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Industrial
{
    public enum AndonCallType
    {
        Material,
        Maintenance,
        Quality,
        Supervisor
    }

    public class AndonCallEventArgs : EventArgs
    {
        public AndonCallType CallType { get; }
        public bool IsActive { get; }

        public AndonCallEventArgs(AndonCallType type, bool active)
        {
            CallType = type;
            IsActive = active;
        }
    }

    /// <summary>
    /// Touchscreen-optimized Shopfloor Andon Call Pad for factory operator workstations.
    /// Features 4 quick-action dispatch tiles (Material, Maintenance, Quality, Supervisor)
    /// with live SLA response time counters and flashing alert feedback.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Industrial & SCADA")]
    [DefaultEvent("CallTriggered")]
    [Description("Shopfloor Touchscreen Andon Call Pad with live SLA response timers")]
    [ToolboxBitmap(typeof(ZeroIcons), "ZeroAndonCallPad.bmp")]
    public class ZAndonCallPad : Control
    {
        private class CallTile
        {
            public AndonCallType Type;
            public string Title = "";
            public string Icon = "";
            public Color Color = Color.Blue;
            public bool IsActive;
            public int ElapsedSeconds;
            public Rectangle Bounds;
        }


        private readonly CallTile[] _tiles;
        private readonly Timer _slaTimer;
        private bool _blinkPhase = false;
        private int _hoveredIndex = -1;

        public event EventHandler<AndonCallEventArgs>? CallTriggered;

        public ZAndonCallPad()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor, true);


            Size = new Size(360, 90);
            BackColor = Color.Transparent;
            Font = new Font("Segoe UI", 9f);

            _tiles = new CallTile[]
            {
                new CallTile { Type = AndonCallType.Material, Title = "Material", Icon = "📦", Color = Color.FromArgb(59, 130, 246) },
                new CallTile { Type = AndonCallType.Maintenance, Title = "Maintenance", Icon = "🔧", Color = Color.FromArgb(239, 68, 68) },
                new CallTile { Type = AndonCallType.Quality, Title = "Quality", Icon = "🔍", Color = Color.FromArgb(245, 158, 11) },
                new CallTile { Type = AndonCallType.Supervisor, Title = "Supervisor", Icon = "👤", Color = Color.FromArgb(139, 92, 246) }
            };

            _slaTimer = new Timer { Interval = 1000 };
            _slaTimer.Tick += (s, e) =>
            {
                bool anyActive = false;
                for (int i = 0; i < _tiles.Length; i++)
                {
                    if (_tiles[i].IsActive)
                    {
                        _tiles[i].ElapsedSeconds++;
                        anyActive = true;
                    }
                }
                if (anyActive)
                {
                    _blinkPhase = !_blinkPhase;
                    Invalidate();
                }
            };

            if (!ZeroDesignHelper.IsInDesignMode(this))
            {
                _slaTimer.Start();
            }

            ZeroTheme.ThemeChanged += OnThemeChanged;
        }

        private void OnThemeChanged(object? sender, EventArgs e) => Invalidate();

        public void TriggerCall(AndonCallType type, bool active)
        {
            for (int i = 0; i < _tiles.Length; i++)
            {
                if (_tiles[i].Type == type)
                {
                    _tiles[i].IsActive = active;
                    if (!active) _tiles[i].ElapsedSeconds = 0;
                    CallTriggered?.Invoke(this, new AndonCallEventArgs(type, active));
                    Invalidate();
                    break;
                }
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int hit = -1;
            for (int i = 0; i < _tiles.Length; i++)
            {
                if (_tiles[i].Bounds.Contains(e.Location))
                {
                    hit = i;
                    break;
                }
            }
            if (_hoveredIndex != hit)
            {
                _hoveredIndex = hit;
                Cursor = hit >= 0 ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hoveredIndex != -1)
            {
                _hoveredIndex = -1;
                Invalidate();
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left && _hoveredIndex >= 0)
            {
                var tile = _tiles[_hoveredIndex];
                tile.IsActive = !tile.IsActive;
                if (!tile.IsActive) tile.ElapsedSeconds = 0;
                CallTriggered?.Invoke(this, new AndonCallEventArgs(tile.Type, tile.IsActive));
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            int w = Width;
            int h = Height;
            int count = _tiles.Length;
            int gap = 6;
            int tileW = (w - (gap * (count - 1))) / count;

            for (int i = 0; i < count; i++)
            {
                var tile = _tiles[i];
                int tx = i * (tileW + gap);
                tile.Bounds = new Rectangle(tx, 0, tileW, h);

                DrawTile(g, tile, _hoveredIndex == i);
            }
        }

        private void DrawTile(Graphics g, CallTile tile, bool isHovered)
        {
            var rect = tile.Bounds;
            var palette = ZeroTheme.Colors;

            Color baseBg = tile.IsActive
                ? (_blinkPhase ? tile.Color : Color.FromArgb(180, tile.Color))
                : (isHovered ? palette.Hover : palette.CardBackground);

            Color textColor = tile.IsActive ? Color.White : palette.TextPrimary;
            Color subColor = tile.IsActive ? Color.FromArgb(241, 245, 249) : palette.TextSecondary;

            // Rounded Tile Background
            using (var path = CreateRoundedRectangle(rect, 6))
            {
                using (var brush = new SolidBrush(baseBg))
                {
                    g.FillPath(brush, path);
                }

                Color borderC = tile.IsActive ? tile.Color : (isHovered ? tile.Color : palette.Border);
                using var pen = new Pen(borderC, isHovered || tile.IsActive ? 2f : 1f);
                g.DrawPath(pen, path);
            }

            // Tile Icon
            using (var iconFont = new Font("Segoe UI Emoji", 14f))
            using (var iconBrush = new SolidBrush(textColor))
            {
                var sz = g.MeasureString(tile.Icon, iconFont);
                g.DrawString(tile.Icon, iconFont, iconBrush, rect.X + (rect.Width - sz.Width) / 2, rect.Y + 8);
            }

            // Tile Title
            using (var titleFont = new Font("Segoe UI", 8.5f, FontStyle.Bold))
            using (var titleBrush = new SolidBrush(textColor))
            {
                var sz = g.MeasureString(tile.Title, titleFont);
                g.DrawString(tile.Title, titleFont, titleBrush, rect.X + (rect.Width - sz.Width) / 2, rect.Y + 38);
            }

            // SLA Counter or Status
            string subText = tile.IsActive
                ? $"⏱ {tile.ElapsedSeconds / 60:D2}:{tile.ElapsedSeconds % 60:D2}"
                : "Ready";

            using (var subFont = new Font("Segoe UI", 8f))
            using (var subBrush = new SolidBrush(subColor))
            {
                var sz = g.MeasureString(subText, subFont);
                g.DrawString(subText, subFont, subBrush, rect.X + (rect.Width - sz.Width) / 2, rect.Y + 58);
            }
        }

        private static GraphicsPath CreateRoundedRectangle(Rectangle rect, int radius) =>
            ZeroUIConfig.CreateRoundedRectangle(rect, radius);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ZeroTheme.ThemeChanged -= OnThemeChanged;
                _slaTimer.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// Legacy alias for <see cref="ZAndonCallPad"/>.
    /// Preserved for backward compatibility.
    /// </summary>
    [Obsolete("ZeroAndonCallPad is deprecated and will be removed in 5 release cycles. Please migrate to ZAndonCallPad instead.")]
    [ToolboxItem(false)]
    public class ZeroAndonCallPad : ZAndonCallPad
    {
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZAndonCallPad"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("AndonCallPad is deprecated and will be removed in 5 release cycles. Please migrate to ZAndonCallPad instead.")]
    [ToolboxItem(false)]
    public class AndonCallPad : ZAndonCallPad
    {
    }

    #endregion
}
