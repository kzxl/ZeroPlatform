using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using ZeroUI.Core.Editors;
using ZeroUI.Core.Theme;
using ZeroUI.WinForms.Base;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Feedback
{
    [ToolboxItem(true)]
    [Category("ZeroUI - Feedback")]
    [Description("Notification center panel with header and scrollable list of notifications")]
    public class ZNotificationCenter : ControlBase
    {
        private readonly List<ZNotification> _notifications = new List<ZNotification>();
        private VScrollBar _scrollBar;
        private bool _showTimestamp = true;
        private bool _groupByDate = true;
        private int _maxVisible = 50;

        private const int HeaderHeight = 40;
        private const int ItemHeight = 72;
        private int _scrollOffset = 0;

        private int _hoveredIndex = -1;
        private bool _hoveredAction = false;
        private Rectangle _clearRect;
        private Rectangle _markAllRect;

        private bool _hoveredClear = false;
        private bool _hoveredMarkAll = false;

        public event EventHandler<NotificationClickedEventArgs>? NotificationClicked;
        public event EventHandler<NotificationActionEventArgs>? ActionClicked;
        public event EventHandler? UnreadCountChanged;

        [Category("Data")]
        [Browsable(false)]
        public IReadOnlyList<ZNotification> Notifications => _notifications.AsReadOnly();

        [Category("Data")]
        [Browsable(false)]
        public int UnreadCount => _notifications.Count(n => !n.IsRead);

        [Category("Appearance")]
        [DefaultValue(true)]
        public bool ShowTimestamp
        {
            get => _showTimestamp;
            set
            {
                _showTimestamp = value;
                Invalidate();
            }
        }

        [Category("Behavior")]
        [DefaultValue(true)]
        public bool GroupByDate
        {
            get => _groupByDate;
            set
            {
                _groupByDate = value;
                Invalidate();
            }
        }

        [Category("Behavior")]
        [DefaultValue(50)]
        public int MaxVisible
        {
            get => _maxVisible;
            set
            {
                _maxVisible = value;
                UpdateScrollBounds();
            }
        }

        public ZNotificationCenter()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Size = new Size(320, 400);

            _scrollBar = new VScrollBar
            {
                Visible = false,
                SmallChange = ItemHeight,
                LargeChange = ItemHeight * 3
            };
            _scrollBar.Scroll += (s, e) => { _scrollOffset = _scrollBar.Value; Invalidate(); };
            Controls.Add(_scrollBar);
        }

        public void Push(ZNotification notification)
        {
            if (notification == null) return;
            _notifications.Insert(0, notification);
            if (_notifications.Count > MaxVisible)
            {
                _notifications.RemoveAt(_notifications.Count - 1);
            }
            UpdateScrollBounds();
            NotifyUnreadChanged();
        }

        public void MarkAsRead(string id)
        {
            var notif = _notifications.FirstOrDefault(n => n.Id == id);
            if (notif != null && !notif.IsRead)
            {
                notif.IsRead = true;
                Invalidate();
                NotifyUnreadChanged();
            }
        }

        public void MarkAllAsRead()
        {
            bool changed = false;
            foreach (var notif in _notifications)
            {
                if (!notif.IsRead)
                {
                    notif.IsRead = true;
                    changed = true;
                }
            }
            if (changed)
            {
                Invalidate();
                NotifyUnreadChanged();
            }
        }

        public void Remove(string id)
        {
            int removed = _notifications.RemoveAll(n => n.Id == id);
            if (removed > 0)
            {
                UpdateScrollBounds();
                NotifyUnreadChanged();
            }
        }

        public void Clear()
        {
            if (_notifications.Count > 0)
            {
                _notifications.Clear();
                UpdateScrollBounds();
                NotifyUnreadChanged();
            }
        }

        private void NotifyUnreadChanged()
        {
            UnreadCountChanged?.Invoke(this, EventArgs.Empty);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateScrollBounds();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            UpdateScrollBounds();
        }

        private void UpdateScrollBounds()
        {
            if (_scrollBar == null || !IsHandleCreated) return;
            if (InvokeRequired)
            {
                try { BeginInvoke(new Action(UpdateScrollBounds)); } catch { }
                return;
            }
            int totalHeight = _notifications.Count * ItemHeight;
            int viewHeight = Height - HeaderHeight;

            if (totalHeight > viewHeight)
            {
                _scrollBar.Visible = true;
                _scrollBar.Maximum = totalHeight - viewHeight + _scrollBar.LargeChange - 1;
                _scrollBar.Bounds = new Rectangle(Width - _scrollBar.Width, HeaderHeight, _scrollBar.Width, viewHeight);
            }
            else
            {
                _scrollBar.Visible = false;
                _scrollOffset = 0;
            }
            Invalidate();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (_scrollBar.Visible)
            {
                int newValue = _scrollBar.Value - (e.Delta / 120) * ItemHeight;
                newValue = Math.Max(0, Math.Min(newValue, _scrollBar.Maximum - _scrollBar.LargeChange + 1));
                _scrollBar.Value = newValue;
                _scrollOffset = newValue;
                Invalidate();
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            int oldHover = _hoveredIndex;
            bool oldAction = _hoveredAction;
            bool oldClear = _hoveredClear;
            bool oldMarkAll = _hoveredMarkAll;

            _hoveredIndex = -1;
            _hoveredAction = false;
            _hoveredClear = false;
            _hoveredMarkAll = false;

            if (e.Y < HeaderHeight)
            {
                if (_clearRect.Contains(e.Location)) _hoveredClear = true;
                if (_markAllRect.Contains(e.Location)) _hoveredMarkAll = true;
            }
            else
            {
                int yOff = e.Y - HeaderHeight + _scrollOffset;
                int index = yOff / ItemHeight;

                if (index >= 0 && index < _notifications.Count)
                {
                    _hoveredIndex = index;
                    var n = _notifications[index];
                    if (!string.IsNullOrEmpty(n.ActionText))
                    {
                        Rectangle actionRect = GetActionRect(index);
                        if (actionRect.Contains(e.Location))
                        {
                            _hoveredAction = true;
                        }
                    }
                }
            }

            if (oldHover != _hoveredIndex || oldAction != _hoveredAction || oldClear != _hoveredClear || oldMarkAll != _hoveredMarkAll)
            {
                Cursor = (_hoveredAction || _hoveredClear || _hoveredMarkAll) ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hoveredIndex = -1;
            _hoveredAction = false;
            _hoveredClear = false;
            _hoveredMarkAll = false;
            Cursor = Cursors.Default;
            Invalidate();
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);

            if (e.Button == MouseButtons.Left)
            {
                if (_hoveredClear)
                {
                    Clear();
                }
                else if (_hoveredMarkAll)
                {
                    MarkAllAsRead();
                }
                else if (_hoveredIndex >= 0 && _hoveredIndex < _notifications.Count)
                {
                    var n = _notifications[_hoveredIndex];
                    if (_hoveredAction && !string.IsNullOrEmpty(n.ActionText))
                    {
                        ActionClicked?.Invoke(this, new NotificationActionEventArgs(n, n.ActionText));
                    }
                    else
                    {
                        if (!n.IsRead)
                        {
                            n.IsRead = true;
                            NotifyUnreadChanged();
                            Invalidate();
                        }
                        NotificationClicked?.Invoke(this, new NotificationClickedEventArgs(n));
                    }
                }
            }
        }

        private Rectangle GetActionRect(int index)
        {
            int y = HeaderHeight + (index * ItemHeight) - _scrollOffset;
            int rightEdge = _scrollBar.Visible ? Width - _scrollBar.Width : Width;
            return new Rectangle(rightEdge - 70, y + 40, 60, 24);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var palette = CurrentPalette;
            int rightEdge = _scrollBar.Visible ? Width - _scrollBar.Width : Width;

            // Draw Header
            using (var headerBrush = new SolidBrush(palette.HeaderBackground))
            {
                g.FillRectangle(headerBrush, 0, 0, Width, HeaderHeight);
            }
            using (var linePen = new Pen(palette.Border))
            {
                g.DrawLine(linePen, 0, HeaderHeight - 1, Width, HeaderHeight - 1);
            }

            using (var titleFont = new Font("Segoe UI", 10f, FontStyle.Bold))
            {
                TextRenderer.DrawText(g, "Notifications", titleFont, new Point(12, 10), palette.TextPrimary);
            }

            // Draw unread badge
            int unread = UnreadCount;
            if (unread > 0)
            {
                string countText = unread.ToString();
                using (var badgeFont = new Font("Segoe UI", 8f, FontStyle.Bold))
                using (var badgeBrush = new SolidBrush(palette.Primary))
                using (var badgeTextBrush = new SolidBrush(Color.White))
                {
                    var size = TextRenderer.MeasureText(countText, badgeFont);
                    int badgeWidth = Math.Max(20, size.Width + 8);
                    var badgeRect = new Rectangle(110, 10, badgeWidth, 20);
                    using (var path = PaintHelper.CreateRoundedRectangle(badgeRect, 10))
                    {
                        g.FillPath(badgeBrush, path);
                    }
                    TextRenderer.DrawText(g, countText, badgeFont, badgeRect, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
            }

            // Header Buttons
            using (var linkFont = new Font("Segoe UI", 8.5f, FontStyle.Regular))
            {
                string clearText = "Clear";
                var clearSize = TextRenderer.MeasureText(clearText, linkFont);
                _clearRect = new Rectangle(rightEdge - clearSize.Width - 12, 10, clearSize.Width, 20);
                TextRenderer.DrawText(g, clearText, linkFont, _clearRect, _hoveredClear ? palette.Primary : palette.TextSecondary, TextFormatFlags.VerticalCenter);

                string markAllText = "Mark All Read";
                var markAllSize = TextRenderer.MeasureText(markAllText, linkFont);
                _markAllRect = new Rectangle(_clearRect.Left - markAllSize.Width - 16, 10, markAllSize.Width, 20);
                TextRenderer.DrawText(g, markAllText, linkFont, _markAllRect, _hoveredMarkAll ? palette.Primary : palette.TextSecondary, TextFormatFlags.VerticalCenter);
            }

            // Draw Items
            var clipRect = new Rectangle(0, HeaderHeight, rightEdge, Height - HeaderHeight);
            g.SetClip(clipRect);

            using (var titleFont = new Font("Segoe UI", 9.5f, FontStyle.Bold))
            using (var msgFont = new Font("Segoe UI", 8.5f, FontStyle.Regular))
            using (var timeFont = new Font("Segoe UI", 8f, FontStyle.Regular))
            using (var unreadBrush = new SolidBrush(Color.FromArgb(20, palette.Primary)))
            using (var hoverBrush = new SolidBrush(palette.Hover))
            {
                for (int i = 0; i < _notifications.Count; i++)
                {
                    var n = _notifications[i];
                    int y = HeaderHeight + (i * ItemHeight) - _scrollOffset;

                    if (y + ItemHeight < HeaderHeight || y > Height) continue;

                    var itemRect = new Rectangle(0, y, rightEdge, ItemHeight);

                    // Background
                    if (i == _hoveredIndex)
                    {
                        g.FillRectangle(hoverBrush, itemRect);
                    }
                    else if (!n.IsRead)
                    {
                        g.FillRectangle(unreadBrush, itemRect);
                    }
                    else
                    {
                        using (var bgBrush = new SolidBrush(palette.CardBackground))
                            g.FillRectangle(bgBrush, itemRect);
                    }

                    // Severity Bar
                    Color sevColor = palette.Info;
                    if (n.Severity == NotificationSeverity.Success) sevColor = palette.Success;
                    else if (n.Severity == NotificationSeverity.Warning) sevColor = palette.Warning;
                    else if (n.Severity == NotificationSeverity.Error) sevColor = palette.Danger;

                    using (var sevBrush = new SolidBrush(sevColor))
                    {
                        g.FillRectangle(sevBrush, 0, y, 4, ItemHeight);
                    }

                    // Title
                    var titleRect = new Rectangle(16, y + 8, rightEdge - 80, 20);
                    TextRenderer.DrawText(g, n.Title, titleFont, titleRect, palette.TextPrimary, TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter);

                    // Time
                    if (_showTimestamp)
                    {
                        var timeSpan = DateTime.Now - n.Timestamp;
                        string timeStr = timeSpan.TotalMinutes < 1 ? "Just now" :
                                         timeSpan.TotalHours < 1 ? $"{(int)timeSpan.TotalMinutes}m ago" :
                                         timeSpan.TotalDays < 1 ? $"{(int)timeSpan.TotalHours}h ago" :
                                         n.Timestamp.ToString("MMM dd");
                        var timeRect = new Rectangle(rightEdge - 70, y + 8, 60, 20);
                        TextRenderer.DrawText(g, timeStr, timeFont, timeRect, palette.TextSecondary, TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
                    }

                    // Message
                    var msgRect = new Rectangle(16, y + 28, rightEdge - 32, 34);
                    if (!string.IsNullOrEmpty(n.ActionText))
                    {
                        msgRect.Width -= 70;
                    }
                    TextRenderer.DrawText(g, n.Message, msgFont, msgRect, palette.TextSecondary, TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis);

                    // Action Button
                    if (!string.IsNullOrEmpty(n.ActionText))
                    {
                        var actionRect = GetActionRect(i);
                        bool isHoveredAction = (_hoveredIndex == i && _hoveredAction);
                        
                        using (var actionPath = PaintHelper.CreateRoundedRectangle(actionRect, 4))
                        {
                            using (var bg = new SolidBrush(isHoveredAction ? palette.PrimaryHover : palette.Primary))
                            {
                                g.FillPath(bg, actionPath);
                            }
                        }
                        TextRenderer.DrawText(g, n.ActionText, timeFont, actionRect, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                    }

                    // Divider
                    using (var divPen = new Pen(palette.Border))
                    {
                        g.DrawLine(divPen, 16, y + ItemHeight - 1, rightEdge - 16, y + ItemHeight - 1);
                    }
                }
            }

            g.ResetClip();

            using (var borderPen = new Pen(palette.Border))
            {
                g.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);
            }
        }
    }

    [ToolboxItem(true)]
    [Category("ZeroUI - Feedback")]
    [Description("Notification bell button companion")]
    public class ZNotificationBell : ControlBase
    {
        private ZNotificationCenter? _center;
        private bool _isHovered;

        [Category("Behavior")]
        [DefaultValue(null)]
        public ZNotificationCenter? NotificationCenter
        {
            get => _center;
            set
            {
                if (_center != null)
                {
                    _center.UnreadCountChanged -= Center_UnreadCountChanged;
                }
                _center = value;
                if (_center != null)
                {
                    _center.UnreadCountChanged += Center_UnreadCountChanged;
                }
                Invalidate();
            }
        }

        public ZNotificationBell()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            Size = new Size(40, 40);
        }

        private void Center_UnreadCountChanged(object? sender, EventArgs e)
        {
            Invalidate();
        }

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

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (e.Button == MouseButtons.Left && _center != null)
            {
                _center.Visible = !_center.Visible;
                if (_center.Visible)
                {
                    _center.BringToFront();
                    _center.Focus();
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var palette = CurrentPalette;

            if (_isHovered)
            {
                using (var hoverBrush = new SolidBrush(palette.Hover))
                {
                    using (var path = PaintHelper.CreateRoundedRectangle(ClientRectangle, 6))
                    {
                        g.FillPath(hoverBrush, path);
                    }
                }
            }

            // Draw Bell Icon (approximated with shapes)
            int cx = Width / 2;
            int cy = Height / 2;

            using (var bellPen = new Pen(palette.TextPrimary, 2f))
            {
                bellPen.LineJoin = LineJoin.Round;
                
                var p = new GraphicsPath();
                p.AddArc(cx - 6, cy - 10, 12, 12, 180, 180); // top dome
                p.AddLine(cx + 6, cy - 4, cx + 6, cy + 4);
                p.AddLine(cx + 6, cy + 4, cx + 8, cy + 8);
                p.AddLine(cx + 8, cy + 8, cx - 8, cy + 8);
                p.AddLine(cx - 8, cy + 8, cx - 6, cy + 4);
                p.AddLine(cx - 6, cy + 4, cx - 6, cy - 4);
                g.DrawPath(bellPen, p);

                g.DrawArc(bellPen, cx - 3, cy + 8, 6, 6, 0, 180); // clapper
            }

            int unread = _center?.UnreadCount ?? 0;
            if (unread > 0)
            {
                string txt = unread > 99 ? "99+" : unread.ToString();
                using (var badgeFont = new Font("Segoe UI", 7f, FontStyle.Bold))
                using (var badgeBrush = new SolidBrush(palette.Danger))
                using (var textBrush = new SolidBrush(Color.White))
                {
                    var size = TextRenderer.MeasureText(txt, badgeFont);
                    int bw = Math.Max(16, size.Width + 4);
                    var badgeRect = new Rectangle(Width - bw - 4, 4, bw, 16);
                    using (var path = PaintHelper.CreateRoundedRectangle(badgeRect, 8))
                    {
                        g.FillPath(badgeBrush, path);
                    }
                    TextRenderer.DrawText(g, txt, badgeFont, badgeRect, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_center != null)
                {
                    _center.UnreadCountChanged -= Center_UnreadCountChanged;
                }
            }
            base.Dispose(disposing);
        }
    }

    [Obsolete("Use ZNotificationCenter instead.")]
    [ToolboxItem(false)]
    public class NotificationCenterControl : ZNotificationCenter { }

    [Obsolete("Use ZNotificationCenter instead.")]
    [ToolboxItem(false)]
    public class ZeroNotificationCenter : ZNotificationCenter { }
}
