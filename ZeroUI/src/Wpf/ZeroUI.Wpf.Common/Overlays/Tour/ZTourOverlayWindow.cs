using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Overlays
{
    /// <summary>
    /// Specialized full-surface transparent overlay window hosting the spotlight cutout mask
    /// and floating tour guide cards for <see cref="ZTour"/>.
    /// </summary>
    internal sealed class ZTourOverlayWindow : Window
    {
        private readonly ZTour _tour;
        private readonly Canvas _rootCanvas;
        private readonly Path _maskPath;
        private readonly Border _spotlightGlowBorder;
        private readonly Border _cardBorder;
        private readonly Polygon _arrowPolygon;
        private readonly TextBlock _stepBadgeBlock;
        private readonly TextBlock _titleBlock;
        private readonly TextBlock _descBlock;
        private readonly ContentPresenter _customContentPresenter;
        private readonly StackPanel _dotsPanel;
        private readonly Button _btnSkip;
        private readonly Button _btnPrev;
        private readonly Button _btnNext;
        private readonly Button _btnClose;

        private readonly Window? _targetOwner;
        private Rect _currentTargetRect = Rect.Empty;

        public ZTourOverlayWindow(Window owner, ZTour tour)
        {
            _tour = tour ?? throw new ArgumentNullException(nameof(tour));
            _targetOwner = owner;

            if (owner != null && (owner.IsLoaded || owner.IsVisible))
            {
                try
                {
                    Owner = owner;
                }
                catch { }
            }

            WindowStartupLocation = WindowStartupLocation.Manual;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            Focusable = true;
            Topmost = true;

            if (owner != null)
            {
                SyncWithWindowBounds(owner);
                owner.LocationChanged += Owner_BoundsChanged;
                owner.SizeChanged += Owner_BoundsChanged;
                owner.StateChanged += Owner_StateChanged;
            }

            _rootCanvas = new Canvas
            {
                ClipToBounds = true,
                Focusable = false
            };
            Content = _rootCanvas;

            // 1. Dark Backdrop Mask with Spotlight Cutout (mờ dịu 50%, thấy rõ cửa sổ bên dưới)
            _maskPath = new Path
            {
                Fill = new SolidColorBrush(Color.FromArgb(135, 10, 15, 26)), // Soft Obsidian Dim
                IsHitTestVisible = true
            };
            _rootCanvas.Children.Add(_maskPath);

            // 2. Animated Spotlight Target Glow Border
            _spotlightGlowBorder = new Border
            {
                BorderBrush = ZeroWpfTheme.PrimaryAccent,
                BorderThickness = new Thickness(2),
                IsHitTestVisible = false,
                Visibility = Visibility.Collapsed,
                Effect = new DropShadowEffect
                {
                    Color = Color.FromRgb(59, 130, 246),
                    BlurRadius = 16,
                    ShadowDepth = 0,
                    Opacity = 0.8
                }
            };
            _rootCanvas.Children.Add(_spotlightGlowBorder);

            // 3. Arrow Polygon pointing to target
            _arrowPolygon = new Polygon
            {
                Fill = ZeroWpfTheme.BgCard,
                IsHitTestVisible = false,
                Visibility = Visibility.Collapsed
            };
            _rootCanvas.Children.Add(_arrowPolygon);

            // 4. Tour Guide Popover Card
            _cardBorder = new Border
            {
                Width = 360,
                Background = ZeroWpfTheme.BgCard,
                BorderBrush = ZeroWpfTheme.BorderDefault,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Effect = new DropShadowEffect
                {
                    BlurRadius = 24,
                    ShadowDepth = 6,
                    Opacity = 0.5,
                    Color = Colors.Black
                }
            };

            var cardGrid = new Grid { Margin = new Thickness(16) };
            cardGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header
            cardGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Body
            cardGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Footer

            // Header: Step Badge + Title + Close Button
            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _stepBadgeBlock = new TextBlock
            {
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            var badgeBorder = new Border
            {
                Background = ZeroWpfTheme.PrimaryAccent,
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 2, 6, 2),
                Child = _stepBadgeBlock,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            Grid.SetColumn(badgeBorder, 0);
            headerGrid.Children.Add(badgeBorder);

            _titleBlock = new TextBlock
            {
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = ZeroWpfTheme.TextPrimary,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Grid.SetColumn(_titleBlock, 1);
            headerGrid.Children.Add(_titleBlock);

            _btnClose = new Button
            {
                Content = "✕",
                Width = 24,
                Height = 24,
                Foreground = ZeroWpfTheme.TextMuted,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center
            };
            _btnClose.Click += (s, e) => _tour.Close(completed: false);
            Grid.SetColumn(_btnClose, 2);
            headerGrid.Children.Add(_btnClose);

            Grid.SetRow(headerGrid, 0);
            cardGrid.Children.Add(headerGrid);

            // Body: Description or Custom Content
            var bodyStack = new StackPanel { Margin = new Thickness(0, 10, 0, 14) };

            _descBlock = new TextBlock
            {
                FontSize = 12,
                LineHeight = 18,
                Foreground = ZeroWpfTheme.TextSecondary,
                TextWrapping = TextWrapping.Wrap
            };
            bodyStack.Children.Add(_descBlock);

            _customContentPresenter = new ContentPresenter
            {
                Visibility = Visibility.Collapsed,
                Margin = new Thickness(0, 6, 0, 0)
            };
            bodyStack.Children.Add(_customContentPresenter);

            Grid.SetRow(bodyStack, 1);
            cardGrid.Children.Add(bodyStack);

            // Footer: Indicators (left) + Action Buttons (right)
            var footerGrid = new Grid();
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            _dotsPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(_dotsPanel, 0);
            footerGrid.Children.Add(_dotsPanel);

            var buttonsStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };

            _btnSkip = new Button
            {
                Content = "Skip",
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = ZeroWpfTheme.TextMuted,
                FontSize = 11,
                Padding = new Thickness(8, 4, 8, 4),
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 6, 0)
            };
            _btnSkip.Click += (s, e) => _tour.Close(completed: false);
            buttonsStack.Children.Add(_btnSkip);

            _btnPrev = new Button
            {
                Content = "Previous",
                Background = ZeroWpfTheme.BgInput,
                BorderBrush = ZeroWpfTheme.BorderDefault,
                BorderThickness = new Thickness(1),
                Foreground = ZeroWpfTheme.TextPrimary,
                FontSize = 11,
                Padding = new Thickness(10, 4, 10, 4),
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 6, 0)
            };
            _btnPrev.Click += (s, e) => _tour.Previous();
            buttonsStack.Children.Add(_btnPrev);

            _btnNext = new Button
            {
                Content = "Next",
                Background = ZeroWpfTheme.PrimaryAccent,
                BorderThickness = new Thickness(0),
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold,
                FontSize = 11,
                Padding = new Thickness(14, 5, 14, 5),
                Cursor = Cursors.Hand
            };
            _btnNext.Click += (s, e) => _tour.Next();
            buttonsStack.Children.Add(_btnNext);

            Grid.SetColumn(buttonsStack, 1);
            footerGrid.Children.Add(buttonsStack);

            Grid.SetRow(footerGrid, 2);
            cardGrid.Children.Add(footerGrid);

            _cardBorder.Child = cardGrid;
            _rootCanvas.Children.Add(_cardBorder);

            PreviewKeyDown += OnWindowKeyDown;
        }

        public void DisplayStep(ZTourStep step, int currentIndex, int totalCount)
        {
            if (step == null) return;

            Topmost = true;
            Activate();

            // 1. Cập nhật dữ liệu hiển thị trên Card
            _stepBadgeBlock.Text = $"{currentIndex + 1}/{totalCount}";
            _titleBlock.Text = step.Title;
            _descBlock.Text = step.Description;

            if (step.CustomContent != null)
            {
                _customContentPresenter.Content = step.CustomContent;
                _customContentPresenter.Visibility = Visibility.Visible;
            }
            else
            {
                _customContentPresenter.Visibility = Visibility.Collapsed;
            }

            // 2. Navigation buttons
            _btnPrev.Visibility = currentIndex > 0 ? Visibility.Visible : Visibility.Collapsed;
            _btnPrev.Content = step.PrevButtonText ?? "Previous";

            bool isLast = (currentIndex == totalCount - 1);
            _btnNext.Content = step.NextButtonText ?? (isLast ? "Finish" : "Next");
            _btnSkip.Visibility = isLast ? Visibility.Collapsed : Visibility.Visible;

            // 3. Render Dot indicators
            RenderDots(currentIndex, totalCount);

            // 4. Tính toán Target Bounding Box
            _currentTargetRect = ResolveTargetRect(step);

            // 5. Cập nhật Spotlight Cutout Mask
            UpdateSpotlightMask(step, _currentTargetRect);

            // 6. Định vị Popover Card
            PositionCard(step, _currentTargetRect);
        }

        private void RenderDots(int currentIndex, int totalCount)
        {
            _dotsPanel.Children.Clear();
            for (int i = 0; i < totalCount; i++)
            {
                bool active = (i == currentIndex);
                var dot = new Border
                {
                    Width = active ? 14 : 6,
                    Height = 6,
                    CornerRadius = new CornerRadius(3),
                    Background = active ? ZeroWpfTheme.PrimaryAccent : ZeroWpfTheme.BorderDefault,
                    Margin = new Thickness(0, 0, 4, 0)
                };
                _dotsPanel.Children.Add(dot);
            }
        }

        private static FrameworkElement? FindChildByName(DependencyObject? parent, string name)
        {
            if (parent == null || string.IsNullOrWhiteSpace(name)) return null;
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is FrameworkElement fe && fe.Name == name)
                    return fe;
                var nested = FindChildByName(child, name);
                if (nested != null) return nested;
            }
            return null;
        }

        private Rect ResolveTargetRect(ZTourStep step)
        {
            UIElement? target = step.Target;

            if (target == null && !string.IsNullOrWhiteSpace(step.TargetName) && Owner != null)
            {
                target = (Owner.FindName(step.TargetName) as UIElement) ?? FindChildByName(Owner, step.TargetName);
            }

            if (target == null || !target.IsVisible)
                return Rect.Empty;

            try
            {
                // Chuyển đổi tọa độ từ target sang tọa độ màn hình thực tế (an toàn giữa các Visual Tree khác nhau)
                Point screenTopLeft = target.PointToScreen(new Point(0, 0));
                Point overlayTopLeft;

                var source = PresentationSource.FromVisual(this);
                if (source?.CompositionTarget != null)
                {
                    overlayTopLeft = PointFromScreen(screenTopLeft);
                }
                else
                {
                    var dpi = VisualTreeHelper.GetDpi(this);
                    overlayTopLeft = new Point(
                        (screenTopLeft.X / dpi.DpiScaleX) - Left,
                        (screenTopLeft.Y / dpi.DpiScaleY) - Top);
                }

                double renderW = target.RenderSize.Width;
                double renderH = target.RenderSize.Height;
                if ((renderW <= 0 || renderH <= 0) && target is FrameworkElement fe)
                {
                    renderW = fe.ActualWidth;
                    renderH = fe.ActualHeight;
                }

                var pad = step.TargetPadding;
                return new Rect(
                    overlayTopLeft.X - pad.Left,
                    overlayTopLeft.Y - pad.Top,
                    Math.Max(10, renderW + pad.Left + pad.Right),
                    Math.Max(10, renderH + pad.Top + pad.Bottom));
            }
            catch
            {
                return Rect.Empty;
            }
        }

        private void UpdateSpotlightMask(ZTourStep step, Rect targetRect)
        {
            double width = Math.Max(10, ActualWidth);
            double height = Math.Max(10, ActualHeight);

            if (!step.Mask)
            {
                _maskPath.Data = null;
                _spotlightGlowBorder.BeginAnimation(UIElement.OpacityProperty, null);
                _spotlightGlowBorder.Visibility = Visibility.Collapsed;
                return;
            }

            var fullWindowGeom = new RectangleGeometry(new Rect(0, 0, width, height));

            if (targetRect.IsEmpty)
            {
                // Toàn màn hình bị mờ, không có khoét lỗ (Welcome Step)
                _maskPath.Data = fullWindowGeom;
                _spotlightGlowBorder.BeginAnimation(UIElement.OpacityProperty, null);
                _spotlightGlowBorder.Visibility = Visibility.Collapsed;
            }
            else
            {
                // Khoét lỗ spotlight tại targetRect
                var holeGeom = new RectangleGeometry(targetRect, Math.Max(2, step.CornerRadius), Math.Max(2, step.CornerRadius));
                var combined = new CombinedGeometry(GeometryCombineMode.Exclude, fullWindowGeom, holeGeom);
                _maskPath.Data = combined;

                // Border phát sáng nổi bật với nhịp thở (Pulsing Halo)
                _spotlightGlowBorder.Visibility = Visibility.Visible;
                _spotlightGlowBorder.Width = targetRect.Width;
                _spotlightGlowBorder.Height = targetRect.Height;
                _spotlightGlowBorder.CornerRadius = new CornerRadius(Math.Max(2, step.CornerRadius));
                Canvas.SetLeft(_spotlightGlowBorder, targetRect.Left);
                Canvas.SetTop(_spotlightGlowBorder, targetRect.Top);

                var pulseAnimation = new DoubleAnimation
                {
                    From = 0.55,
                    To = 1.0,
                    Duration = TimeSpan.FromMilliseconds(850),
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
                };
                _spotlightGlowBorder.BeginAnimation(UIElement.OpacityProperty, pulseAnimation);
            }
        }

        private static bool TryCalculatePlacement(ZTourPlacement placement, Rect targetRect, double cardW, double cardH, double clientW, double clientH, out Rect resultRect)
        {
            const double margin = 16;
            const double spacing = 14;
            double x;
            double y;

            switch (placement)
            {
                case ZTourPlacement.Bottom:
                    x = targetRect.Left + (targetRect.Width - cardW) / 2.0;
                    y = targetRect.Bottom + spacing;
                    break;
                case ZTourPlacement.Top:
                    x = targetRect.Left + (targetRect.Width - cardW) / 2.0;
                    y = targetRect.Top - cardH - spacing;
                    break;
                case ZTourPlacement.Right:
                    x = targetRect.Right + spacing;
                    y = targetRect.Top + (targetRect.Height - cardH) / 2.0;
                    break;
                case ZTourPlacement.Left:
                    x = targetRect.Left - cardW - spacing;
                    y = targetRect.Top + (targetRect.Height - cardH) / 2.0;
                    break;
                default:
                    resultRect = new Rect((clientW - cardW) / 2.0, (clientH - cardH) / 2.0, cardW, cardH);
                    return true;
            }

            // Kẹp trong màn hình
            x = Math.Max(margin, Math.Min(clientW - cardW - margin, x));
            y = Math.Max(margin, Math.Min(clientH - cardH - margin, y));

            resultRect = new Rect(x, y, cardW, cardH);

            // Kiểm tra xem sau khi kẹp có bị đè lên targetRect không
            Rect safeTarget = new Rect(targetRect.Left - 2, targetRect.Top - 2, targetRect.Width + 4, targetRect.Height + 4);
            return !resultRect.IntersectsWith(safeTarget);
        }

        private void PositionCard(ZTourStep step, Rect targetRect)
        {
            _cardBorder.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double cardW = _cardBorder.DesiredSize.Width > 0 ? _cardBorder.DesiredSize.Width : 360;
            double cardH = _cardBorder.DesiredSize.Height > 0 ? _cardBorder.DesiredSize.Height : 160;

            double cardX;
            double cardY;
            ZTourPlacement placement = step.Placement;

            if (targetRect.IsEmpty || placement == ZTourPlacement.Center)
            {
                // Canh giữa màn hình
                cardX = (ActualWidth - cardW) / 2.0;
                cardY = (ActualHeight - cardH) / 2.0;
                _arrowPolygon.Visibility = Visibility.Collapsed;
            }
            else
            {
                ZTourPlacement[] candidates;
                if (placement == ZTourPlacement.Auto)
                {
                    double spaceBottom = ActualHeight - targetRect.Bottom;
                    double spaceTop = targetRect.Top;
                    double spaceRight = ActualWidth - targetRect.Right;
                    double spaceLeft = targetRect.Left;

                    var dirs = new List<(ZTourPlacement p, double space)>
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
                    candidates = placement switch
                    {
                        ZTourPlacement.Bottom => new[] { ZTourPlacement.Bottom, ZTourPlacement.Top, ZTourPlacement.Right, ZTourPlacement.Left },
                        ZTourPlacement.Top => new[] { ZTourPlacement.Top, ZTourPlacement.Bottom, ZTourPlacement.Right, ZTourPlacement.Left },
                        ZTourPlacement.Right => new[] { ZTourPlacement.Right, ZTourPlacement.Left, ZTourPlacement.Bottom, ZTourPlacement.Top },
                        ZTourPlacement.Left => new[] { ZTourPlacement.Left, ZTourPlacement.Right, ZTourPlacement.Bottom, ZTourPlacement.Top },
                        _ => new[] { ZTourPlacement.Bottom, ZTourPlacement.Top, ZTourPlacement.Right, ZTourPlacement.Left }
                    };
                }

                bool placed = false;
                Rect placedRect = Rect.Empty;
                ZTourPlacement resolved = placement;

                foreach (var p in candidates)
                {
                    if (TryCalculatePlacement(p, targetRect, cardW, cardH, ActualWidth, ActualHeight, out var rect))
                    {
                        placedRect = rect;
                        resolved = p;
                        placed = true;
                        break;
                    }
                }

                if (placed)
                {
                    cardX = placedRect.X;
                    cardY = placedRect.Y;
                    BuildArrow(targetRect, cardX, cardY, cardW, cardH, resolved);
                }
                else
                {
                    // Fallback nếu không hướng nào hoàn toàn né được (target quá to): đặt vào góc thoáng nhất
                    cardX = targetRect.Right + 14 + cardW <= ActualWidth ? targetRect.Right + 14 : Math.Max(16, ActualWidth - cardW - 16);
                    cardY = Math.Max(16, Math.Min(ActualHeight - cardH - 16, targetRect.Bottom + 14));
                    _arrowPolygon.Visibility = Visibility.Collapsed;
                }
            }

            Canvas.SetLeft(_cardBorder, cardX);
            Canvas.SetTop(_cardBorder, cardY);
        }

        private void BuildArrow(Rect targetRect, double cardX, double cardY, double cardW, double cardH, ZTourPlacement placement)
        {
            _arrowPolygon.Points.Clear();
            _arrowPolygon.Visibility = Visibility.Visible;

            const double arrowSize = 8;
            switch (placement)
            {
                case ZTourPlacement.Bottom:
                {
                    double targetCenterX = targetRect.Left + targetRect.Width / 2.0;
                    double arrowX = Math.Max(cardX + 20, Math.Min(cardX + cardW - 20, targetCenterX));
                    _arrowPolygon.Points.Add(new Point(arrowX, targetRect.Bottom + 2));
                    _arrowPolygon.Points.Add(new Point(arrowX - arrowSize, cardY));
                    _arrowPolygon.Points.Add(new Point(arrowX + arrowSize, cardY));
                    break;
                }

                case ZTourPlacement.Top:
                {
                    double targetCenterX = targetRect.Left + targetRect.Width / 2.0;
                    double arrowX = Math.Max(cardX + 20, Math.Min(cardX + cardW - 20, targetCenterX));
                    _arrowPolygon.Points.Add(new Point(arrowX, targetRect.Top - 2));
                    _arrowPolygon.Points.Add(new Point(arrowX - arrowSize, cardY + cardH));
                    _arrowPolygon.Points.Add(new Point(arrowX + arrowSize, cardY + cardH));
                    break;
                }

                case ZTourPlacement.Right:
                {
                    double targetCenterY = targetRect.Top + targetRect.Height / 2.0;
                    double arrowY = Math.Max(cardY + 16, Math.Min(cardY + cardH - 16, targetCenterY));
                    _arrowPolygon.Points.Add(new Point(targetRect.Right + 2, arrowY));
                    _arrowPolygon.Points.Add(new Point(cardX, arrowY - arrowSize));
                    _arrowPolygon.Points.Add(new Point(cardX, arrowY + arrowSize));
                    break;
                }

                case ZTourPlacement.Left:
                {
                    double targetCenterY = targetRect.Top + targetRect.Height / 2.0;
                    double arrowY = Math.Max(cardY + 16, Math.Min(cardY + cardH - 16, targetCenterY));
                    _arrowPolygon.Points.Add(new Point(targetRect.Left - 2, arrowY));
                    _arrowPolygon.Points.Add(new Point(cardX + cardW, arrowY - arrowSize));
                    _arrowPolygon.Points.Add(new Point(cardX + cardW, arrowY + arrowSize));
                    break;
                }

                default:
                    _arrowPolygon.Visibility = Visibility.Collapsed;
                    break;
            }
        }

        private void SyncWithWindowBounds(Window owner)
        {
            if (owner == null) return;

            if (owner.WindowState == WindowState.Maximized)
            {
                WindowState = WindowState.Maximized;
            }
            else
            {
                WindowState = WindowState.Normal;
                Left = owner.Left;
                Top = owner.Top;
                Width = Math.Max(100, owner.ActualWidth);
                Height = Math.Max(100, owner.ActualHeight);
            }
        }

        private void Owner_BoundsChanged(object? sender, EventArgs e)
        {
            if (Owner != null)
            {
                SyncWithWindowBounds(Owner);
                if (_tour.CurrentStep != null)
                {
                    DisplayStep(_tour.CurrentStep, _tour.CurrentIndex, _tour.Steps.Count);
                }
            }
        }

        private void Owner_StateChanged(object? sender, EventArgs e)
        {
            if (Owner != null)
            {
                SyncWithWindowBounds(Owner);
            }
        }

        private void OnWindowKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                _tour.Close(completed: false);
                e.Handled = true;
            }
            else if (e.Key == Key.Right || e.Key == Key.Enter)
            {
                _tour.Next();
                e.Handled = true;
            }
            else if (e.Key == Key.Left)
            {
                _tour.Previous();
                e.Handled = true;
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            if (_targetOwner != null)
            {
                _targetOwner.LocationChanged -= Owner_BoundsChanged;
                _targetOwner.SizeChanged -= Owner_BoundsChanged;
                _targetOwner.StateChanged -= Owner_StateChanged;
            }
            base.OnClosed(e);
        }
    }
}
