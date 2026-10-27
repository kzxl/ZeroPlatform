using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Overlays
{
    internal sealed class ZTourOverlayForm : Form
    {
        private readonly ZTour _tour;
        private readonly Form _ownerForm;
        private Rectangle _targetRect = Rectangle.Empty;
        private ZTourPlacement _resolvedPlacement = ZTourPlacement.Center;

        // Card Layout
        private Rectangle _cardRect = Rectangle.Empty;
        private Rectangle _btnCloseRect = Rectangle.Empty;
        private Rectangle _btnSkipRect = Rectangle.Empty;
        private Rectangle _btnPrevRect = Rectangle.Empty;
        private Rectangle _btnNextRect = Rectangle.Empty;

        private bool _isNextHover;
        private bool _isPrevHover;
        private bool _isSkipHover;
        private bool _isCloseHover;

        // Pulsing Halo Beacon
        private readonly Timer _pulseTimer;
        private float _pulseAngle = 0f;
        private float _pulseFactor = 1.0f;

        public ZTourOverlayForm(Form owner, ZTour tour)
        {
            _ownerForm = owner ?? throw new ArgumentNullException(nameof(owner));
            _tour = tour ?? throw new ArgumentNullException(nameof(tour));

            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            KeyPreview = true;
            BackColor = Color.FromArgb(10, 15, 27);
            TransparencyKey = Color.FromArgb(10, 15, 27);

            SyncBounds();

            _ownerForm.LocationChanged += (s, e) => { SyncBounds(); Invalidate(); };
            _ownerForm.SizeChanged += (s, e) => { SyncBounds(); Invalidate(); };

            _pulseTimer = new Timer { Interval = 40 };
            _pulseTimer.Tick += (s, e) =>
            {
                if (_targetRect.IsEmpty || _tour.CurrentStep == null || !_tour.CurrentStep.Mask) return;
                _pulseAngle += 0.12f;
                if (_pulseAngle > (float)(Math.PI * 2)) _pulseAngle -= (float)(Math.PI * 2);
                _pulseFactor = 0.55f + 0.45f * (float)Math.Sin(_pulseAngle);

                var invalidRect = Rectangle.Inflate(_targetRect, 20, 20);
                Invalidate(invalidRect);
            };
            _pulseTimer.Start();
        }

        public void DisplayStep(ZTourStep step, int currentIndex, int totalCount)
        {
            _targetRect = ResolveTargetRect(step);
            RecalculateCardLayout(step, currentIndex, totalCount);
            BringToFront();
            Invalidate();
        }

        public void RenderOverlay(Graphics g)
        {
            if (g == null) return;
            OnPaint(new PaintEventArgs(g, ClientRectangle));
        }

        private void SyncBounds()
        {
            if (_ownerForm != null && !_ownerForm.IsDisposed)
            {
                var screenPt = _ownerForm.PointToScreen(Point.Empty);
                Location = screenPt;
                Size = _ownerForm.ClientSize;
            }
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

        private static bool TryCalculatePlacement(
            ZTourPlacement placement,
            Rectangle targetRect,
            int cardW,
            int cardH,
            int clientW,
            int clientH,
            out Rectangle resultRect)
        {
            int x;
            int y;
            const int gap = 14;
            const int margin = 12;

            switch (placement)
            {
                case ZTourPlacement.Bottom:
                    x = targetRect.Left + (targetRect.Width - cardW) / 2;
                    y = targetRect.Bottom + gap;
                    break;
                case ZTourPlacement.Top:
                    x = targetRect.Left + (targetRect.Width - cardW) / 2;
                    y = targetRect.Top - cardH - gap;
                    break;
                case ZTourPlacement.Left:
                    x = targetRect.Left - cardW - gap;
                    y = targetRect.Top + (targetRect.Height - cardH) / 2;
                    break;
                case ZTourPlacement.Right:
                    x = targetRect.Right + gap;
                    y = targetRect.Top + (targetRect.Height - cardH) / 2;
                    break;
                default:
                    resultRect = Rectangle.Empty;
                    return false;
            }

            // Kẹp trong màn hình
            x = Math.Max(margin, Math.Min(clientW - cardW - margin, x));
            y = Math.Max(margin, Math.Min(clientH - cardH - margin, y));

            resultRect = new Rectangle(x, y, cardW, cardH);

            // Kiểm tra xem sau khi kẹp có bị đè lên targetRect không
            var safeTarget = Rectangle.Inflate(targetRect, 2, 2);
            return !resultRect.IntersectsWith(safeTarget);
        }

        private void RecalculateCardLayout(ZTourStep step, int currentIndex, int totalCount)
        {
            int cardW = step.CardWidth ?? _tour.CardWidth;
            if (cardW < 320) cardW = 320;
            int textAvailableWidth = cardW - 28;

            int measuredDescHeight = 60;
            using (var font = new Font("Segoe UI", 9.25f))
            {
                var size = TextRenderer.MeasureText(step.Description ?? string.Empty, font, new Size(textAvailableWidth, 0), TextFormatFlags.WordBreak);
                measuredDescHeight = Math.Max(50, size.Height + 8);
            }

            int cardH = step.CardHeight ?? (44 + measuredDescHeight + 16 + 38);

            ZTourPlacement desired = step.Placement;
            if (_targetRect.IsEmpty || desired == ZTourPlacement.Center)
            {
                _resolvedPlacement = ZTourPlacement.Center;
                int cx = (ClientSize.Width - cardW) / 2;
                int cy = (ClientSize.Height - cardH) / 2;
                _cardRect = new Rectangle(cx, cy, cardW, cardH);
            }
            else
            {
                // Danh sách các hướng ưu tiên kiểm tra để tránh đè lên target control
                ZTourPlacement[] candidates;
                if (desired == ZTourPlacement.Auto)
                {
                    int spaceBottom = ClientSize.Height - _targetRect.Bottom;
                    int spaceTop = _targetRect.Top;
                    int spaceRight = ClientSize.Width - _targetRect.Right;
                    int spaceLeft = _targetRect.Left;

                    var dirs = new System.Collections.Generic.List<(ZTourPlacement p, int space)>
                    {
                        (ZTourPlacement.Bottom, spaceBottom),
                        (ZTourPlacement.Top, spaceTop),
                        (ZTourPlacement.Right, spaceRight),
                        (ZTourPlacement.Left, spaceLeft)
                    };
                    dirs.Sort((a, b) => b.space.CompareTo(a.space));
                    candidates = new[] { dirs[0].p, dirs[1].p, dirs[2].p, dirs[3].p };
                }
                else
                {
                    candidates = desired switch
                    {
                        ZTourPlacement.Bottom => new[] { ZTourPlacement.Bottom, ZTourPlacement.Top, ZTourPlacement.Right, ZTourPlacement.Left },
                        ZTourPlacement.Top => new[] { ZTourPlacement.Top, ZTourPlacement.Bottom, ZTourPlacement.Right, ZTourPlacement.Left },
                        ZTourPlacement.Right => new[] { ZTourPlacement.Right, ZTourPlacement.Left, ZTourPlacement.Bottom, ZTourPlacement.Top },
                        ZTourPlacement.Left => new[] { ZTourPlacement.Left, ZTourPlacement.Right, ZTourPlacement.Bottom, ZTourPlacement.Top },
                        _ => new[] { ZTourPlacement.Bottom, ZTourPlacement.Top, ZTourPlacement.Right, ZTourPlacement.Left }
                    };
                }

                bool placed = false;
                foreach (var p in candidates)
                {
                    if (TryCalculatePlacement(p, _targetRect, cardW, cardH, ClientSize.Width, ClientSize.Height, out var rect))
                    {
                        _cardRect = rect;
                        _resolvedPlacement = p;
                        placed = true;
                        break;
                    }
                }

                if (!placed)
                {
                    // Fallback nếu không hướng nào hoàn toàn né được (target quá to): đặt vào góc thoáng nhất
                    int fx = _targetRect.Right + 14 + cardW <= ClientSize.Width ? _targetRect.Right + 14 : Math.Max(12, ClientSize.Width - cardW - 12);
                    int fy = Math.Max(12, Math.Min(ClientSize.Height - cardH - 12, _targetRect.Bottom + 14));
                    _cardRect = new Rectangle(fx, fy, cardW, cardH);
                    _resolvedPlacement = ZTourPlacement.Center;
                }
            }

            _btnCloseRect = new Rectangle(_cardRect.Right - 28, _cardRect.Top + 10, 20, 20);

            int btnY = _cardRect.Bottom - 38;
            _btnNextRect = new Rectangle(_cardRect.Right - 92, btnY, 78, 26);
            _btnPrevRect = new Rectangle(_btnNextRect.Left - 82, btnY, 74, 26);
            _btnSkipRect = new Rectangle(_btnPrevRect.Left - 70, btnY, 62, 26);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var currentStep = _tour.CurrentStep;
            if (currentStep == null) return;

            // 1. Nền trong suốt 100% để hiển thị lớp kính mờ tối (BackdropForm) và form chính bên dưới
            g.Clear(Color.FromArgb(10, 15, 27));

            // 2. Vẽ Animated Glowing Spotlight Border xung quanh vùng khoanh
            if (!_targetRect.IsEmpty && currentStep.Mask)
            {
                // Vòng hào quang ngoài 1 (Soft Glow)
                int halo1Alpha = (int)(45 * _pulseFactor);
                var halo1Rect = Rectangle.Inflate(_targetRect, 3, 3);
                using (var halo1Pen = new Pen(Color.FromArgb(halo1Alpha, 59, 130, 246), 4))
                using (var halo1Path = CreateRoundedRectPath(halo1Rect, Math.Max(4, currentStep.CornerRadius + 2)))
                {
                    g.DrawPath(halo1Pen, halo1Path);
                }

                // Vòng hào quang ngoài 2 (Inner Glow)
                int halo2Alpha = (int)(95 * _pulseFactor);
                var halo2Rect = Rectangle.Inflate(_targetRect, 1, 1);
                using (var halo2Pen = new Pen(Color.FromArgb(halo2Alpha, 59, 130, 246), 2))
                using (var halo2Path = CreateRoundedRectPath(halo2Rect, Math.Max(3, currentStep.CornerRadius + 1)))
                {
                    g.DrawPath(halo2Pen, halo2Path);
                }

                // Viền phát sáng chính (Primary Accent Border)
                int borderAlpha = (int)(255 * _pulseFactor);
                using (var borderPen = new Pen(Color.FromArgb(borderAlpha, 59, 130, 246), 2))
                using (var borderPath = CreateRoundedRectPath(_targetRect, Math.Max(2, currentStep.CornerRadius)))
                {
                    g.DrawPath(borderPen, borderPath);
                }
            }

            // 3. Mũi tên chỉ từ Card đến Target
            if (!_targetRect.IsEmpty && _resolvedPlacement != ZTourPlacement.Center)
            {
                Point[]? arrowPoints = CalculateArrowPoints(_resolvedPlacement, _targetRect, _cardRect);
                if (arrowPoints != null)
                {
                    using (var arrowBrush = new SolidBrush(Color.FromArgb(30, 41, 59)))
                    using (var arrowPen = new Pen(Color.FromArgb(51, 65, 85), 1))
                    {
                        g.FillPolygon(arrowBrush, arrowPoints);
                        g.DrawPolygon(arrowPen, arrowPoints);
                    }
                }
            }

            // 2. Vẽ Popover Card
            using (var cardPath = CreateRoundedRectPath(_cardRect, 10))
            using (var cardBgBrush = new SolidBrush(Color.FromArgb(30, 41, 59)))
            using (var cardBorderPen = new Pen(Color.FromArgb(51, 65, 85), 1))
            {
                g.FillPath(cardBgBrush, cardPath);
                g.DrawPath(cardBorderPen, cardPath);
            }

            // 3. Header: Step Badge & Title
            string badgeText = $"{_tour.CurrentIndex + 1}/{_tour.Steps.Count}";
            var badgeRect = new Rectangle(_cardRect.Left + 14, _cardRect.Top + 14, 34, 18);
            using (var badgePath = CreateRoundedRectPath(badgeRect, 4))
            using (var badgeBrush = new SolidBrush(Color.FromArgb(59, 130, 246)))
            {
                g.FillPath(badgeBrush, badgePath);
            }
            using (var badgeFont = new Font("Segoe UI", 8f, FontStyle.Bold))
            {
                TextRenderer.DrawText(g, badgeText, badgeFont, badgeRect, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }

            var titleRect = new Rectangle(badgeRect.Right + 8, _cardRect.Top + 12, _cardRect.Width - 75, 24);
            using (var titleFont = new Font("Segoe UI", 10f, FontStyle.Bold))
            {
                TextRenderer.DrawText(g, currentStep.Title, titleFont, titleRect, Color.FromArgb(248, 250, 252), TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }

            // Nút đóng (X)
            using (var closeFont = new Font("Segoe UI", 9f))
            {
                var closeColor = _isCloseHover ? Color.FromArgb(239, 68, 68) : Color.FromArgb(148, 163, 184);
                TextRenderer.DrawText(g, "✕", closeFont, _btnCloseRect, closeColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }

            // 4. Body: Description (Dynamic height filling the card body)
            int descAreaHeight = Math.Max(50, _cardRect.Height - 44 - 46);
            var descRect = new Rectangle(_cardRect.Left + 14, _cardRect.Top + 44, _cardRect.Width - 28, descAreaHeight);
            using (var descFont = new Font("Segoe UI", 9.25f))
            {
                TextRenderer.DrawText(g, currentStep.Description, descFont, descRect, Color.FromArgb(226, 232, 240), TextFormatFlags.Left | TextFormatFlags.WordBreak);
            }

            // 5. Footer: Dot Indicators
            int dotX = _cardRect.Left + 16;
            int dotY = _cardRect.Bottom - 26;
            for (int i = 0; i < _tour.Steps.Count; i++)
            {
                bool active = (i == _tour.CurrentIndex);
                int w = active ? 14 : 6;
                using (var dotBrush = new SolidBrush(active ? Color.FromArgb(59, 130, 246) : Color.FromArgb(71, 85, 105)))
                {
                    g.FillEllipse(dotBrush, dotX, dotY, w, 6);
                }
                dotX += w + 4;
            }

            // 6. Action Buttons
            bool isLast = (_tour.CurrentIndex == _tour.Steps.Count - 1);

            // Nút Bỏ qua
            if (!isLast)
            {
                using (var skipFont = new Font("Segoe UI", 8.5f))
                {
                    var skipColor = _isSkipHover ? Color.White : Color.FromArgb(148, 163, 184);
                    TextRenderer.DrawText(g, "Skip", skipFont, _btnSkipRect, skipColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
            }

            // Previous button
            if (_tour.CurrentIndex > 0)
            {
                using (var btnPath = CreateRoundedRectPath(_btnPrevRect, 4))
                using (var btnBg = new SolidBrush(_isPrevHover ? Color.FromArgb(51, 65, 85) : Color.FromArgb(30, 41, 59)))
                using (var btnPen = new Pen(Color.FromArgb(71, 85, 105), 1))
                {
                    g.FillPath(btnBg, btnPath);
                    g.DrawPath(btnPen, btnPath);
                }
                using (var prevFont = new Font("Segoe UI", 8.5f))
                {
                    TextRenderer.DrawText(g, currentStep.PrevButtonText ?? "Previous", prevFont, _btnPrevRect, Color.FromArgb(241, 245, 249), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
            }

            // Next / Finish button
            using (var btnPath = CreateRoundedRectPath(_btnNextRect, 4))
            using (var btnBg = new SolidBrush(_isNextHover ? Color.FromArgb(37, 99, 235) : Color.FromArgb(59, 130, 246)))
            {
                g.FillPath(btnBg, btnPath);
            }
            using (var nextFont = new Font("Segoe UI", 8.5f, FontStyle.Bold))
            {
                string nextText = currentStep.NextButtonText ?? (isLast ? "Finish" : "Next");
                TextRenderer.DrawText(g, nextText, nextFont, _btnNextRect, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            bool prevNext = _isNextHover;
            bool prevPrev = _isPrevHover;
            bool prevSkip = _isSkipHover;
            bool prevClose = _isCloseHover;

            _isNextHover = _btnNextRect.Contains(e.Location);
            _isPrevHover = _btnPrevRect.Contains(e.Location);
            _isSkipHover = _btnSkipRect.Contains(e.Location);
            _isCloseHover = _btnCloseRect.Contains(e.Location);

            Cursor = (_isNextHover || _isPrevHover || _isSkipHover || _isCloseHover) ? Cursors.Hand : Cursors.Default;

            if (prevNext != _isNextHover || prevPrev != _isPrevHover || prevSkip != _isSkipHover || prevClose != _isCloseHover)
            {
                Invalidate();
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            if (_btnCloseRect.Contains(e.Location))
            {
                _tour.Close(completed: false);
            }
            else if (_btnSkipRect.Contains(e.Location))
            {
                _tour.Close(completed: false);
            }
            else if (_btnPrevRect.Contains(e.Location) && _tour.CurrentIndex > 0)
            {
                _tour.Previous();
            }
            else if (_btnNextRect.Contains(e.Location))
            {
                _tour.Next();
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape)
            {
                _tour.Close(completed: false);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Right || e.KeyCode == Keys.Enter)
            {
                _tour.Next();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Left)
            {
                _tour.Previous();
                e.Handled = true;
            }
        }

        private static Point[]? CalculateArrowPoints(ZTourPlacement placement, Rectangle targetRect, Rectangle cardRect)
        {
            const int size = 8;
            switch (placement)
            {
                case ZTourPlacement.Bottom:
                {
                    int midX = Math.Max(cardRect.Left + 16, Math.Min(cardRect.Right - 16, targetRect.Left + targetRect.Width / 2));
                    return new[]
                    {
                        new Point(midX, targetRect.Bottom + 2),
                        new Point(midX - size, cardRect.Top),
                        new Point(midX + size, cardRect.Top)
                    };
                }
                case ZTourPlacement.Top:
                {
                    int midX = Math.Max(cardRect.Left + 16, Math.Min(cardRect.Right - 16, targetRect.Left + targetRect.Width / 2));
                    return new[]
                    {
                        new Point(midX, targetRect.Top - 2),
                        new Point(midX - size, cardRect.Bottom),
                        new Point(midX + size, cardRect.Bottom)
                    };
                }
                case ZTourPlacement.Right:
                {
                    int midY = Math.Max(cardRect.Top + 16, Math.Min(cardRect.Bottom - 16, targetRect.Top + targetRect.Height / 2));
                    return new[]
                    {
                        new Point(targetRect.Right + 2, midY),
                        new Point(cardRect.Left, midY - size),
                        new Point(cardRect.Left, midY + size)
                    };
                }
                case ZTourPlacement.Left:
                {
                    int midY = Math.Max(cardRect.Top + 16, Math.Min(cardRect.Bottom - 16, targetRect.Top + targetRect.Height / 2));
                    return new[]
                    {
                        new Point(targetRect.Left - 2, midY),
                        new Point(cardRect.Right, midY - size),
                        new Point(cardRect.Right, midY + size)
                    };
                }
                default:
                    return null;
            }
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

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _pulseTimer?.Stop();
                _pulseTimer?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
