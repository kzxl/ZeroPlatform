using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using ZeroUI.Core.Editors;
using ZeroUI.Wpf.Base;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Feedback
{
    public class ZNotificationCenter : WpfControlBase
    {
        private Grid _rootGrid;
        private StackPanel _listPanel;
        private ScrollViewer _scrollViewer;
        private TextBlock _badgeText;
        private Border _badgeBorder;

        private readonly List<ZNotification> _notifications = new List<ZNotification>();

        public event EventHandler<NotificationClickedEventArgs>? NotificationClicked;
        public event EventHandler<NotificationActionEventArgs>? ActionClicked;
        public event EventHandler? UnreadCountChanged;

        public static readonly DependencyProperty ShowTimestampProperty =
            DependencyProperty.Register(nameof(ShowTimestamp), typeof(bool), typeof(ZNotificationCenter), new PropertyMetadata(true, OnRenderPropertyChanged));

        public static readonly DependencyProperty GroupByDateProperty =
            DependencyProperty.Register(nameof(GroupByDate), typeof(bool), typeof(ZNotificationCenter), new PropertyMetadata(true, OnRenderPropertyChanged));

        public static readonly DependencyProperty MaxVisibleProperty =
            DependencyProperty.Register(nameof(MaxVisible), typeof(int), typeof(ZNotificationCenter), new PropertyMetadata(50));

        public bool ShowTimestamp
        {
            get => (bool)GetValue(ShowTimestampProperty);
            set => SetValue(ShowTimestampProperty, value);
        }

        public bool GroupByDate
        {
            get => (bool)GetValue(GroupByDateProperty);
            set => SetValue(GroupByDateProperty, value);
        }

        public int MaxVisible
        {
            get => (int)GetValue(MaxVisibleProperty);
            set => SetValue(MaxVisibleProperty, value);
        }

        public IReadOnlyList<ZNotification> Notifications => _notifications.AsReadOnly();
        public int UnreadCount => _notifications.Count(n => !n.IsRead);

        public ZNotificationCenter()
        {
            Width = 320;
            Height = 400;

            _rootGrid = new Grid();
            _rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(40) });
            _rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // Header
            var headerGrid = new Grid { Background = ZeroWpfTheme.BgInput };
            
            var title = new TextBlock
            {
                Text = "Notifications",
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 0, 0),
                Foreground = ZeroWpfTheme.TextPrimary
            };
            headerGrid.Children.Add(title);

            _badgeBorder = new Border
            {
                Background = ZeroWpfTheme.PrimaryAccent,
                CornerRadius = new CornerRadius(10),
                Height = 20,
                MinWidth = 20,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(110, 0, 0, 0),
                Visibility = Visibility.Collapsed
            };
            
            _badgeText = new TextBlock
            {
                Foreground = Brushes.White,
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 6, 0)
            };
            _badgeBorder.Child = _badgeText;
            headerGrid.Children.Add(_badgeBorder);

            var actionsPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) };
            
            var markAllBtn = new TextBlock { Text = "Mark All Read", FontSize = 12, Foreground = ZeroWpfTheme.TextSecondary, Cursor = Cursors.Hand, Margin = new Thickness(0, 0, 16, 0) };
            markAllBtn.MouseDown += (s, e) => MarkAllAsRead();
            
            var clearBtn = new TextBlock { Text = "Clear", FontSize = 12, Foreground = ZeroWpfTheme.TextSecondary, Cursor = Cursors.Hand };
            clearBtn.MouseDown += (s, e) => Clear();

            actionsPanel.Children.Add(markAllBtn);
            actionsPanel.Children.Add(clearBtn);
            headerGrid.Children.Add(actionsPanel);

            var divider = new Border { BorderThickness = new Thickness(0, 0, 0, 1), BorderBrush = ZeroWpfTheme.BorderDefault, VerticalAlignment = VerticalAlignment.Bottom };
            headerGrid.Children.Add(divider);

            Grid.SetRow(headerGrid, 0);
            _rootGrid.Children.Add(headerGrid);

            // List
            _listPanel = new StackPanel();
            _scrollViewer = new ScrollViewer
            {
                Content = _listPanel,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };
            
            Grid.SetRow(_scrollViewer, 1);
            _rootGrid.Children.Add(_scrollViewer);

            _rootBorder = new Border
            {
                BorderBrush = ZeroWpfTheme.BorderDefault,
                BorderThickness = new Thickness(1),
                Background = ZeroWpfTheme.BgCard,
                Child = _rootGrid
            };

            AddVisualChild(_rootBorder);
            AddLogicalChild(_rootBorder);
        }

        private Border _rootBorder;

        protected override int VisualChildrenCount => 1;
        protected override Visual GetVisualChild(int index) => _rootBorder;

        protected override Size MeasureOverride(Size constraint)
        {
            var child = (UIElement)GetVisualChild(0);
            child.Measure(constraint);
            return child.DesiredSize;
        }

        protected override Size ArrangeOverride(Size arrangeBounds)
        {
            var child = (UIElement)GetVisualChild(0);
            child.Arrange(new Rect(arrangeBounds));
            return arrangeBounds;
        }

        private static void OnRenderPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZNotificationCenter c) c.RenderList();
        }

        public void Push(ZNotification notification)
        {
            if (notification == null) return;
            _notifications.Insert(0, notification);
            if (_notifications.Count > MaxVisible)
            {
                _notifications.RemoveAt(_notifications.Count - 1);
            }
            RenderList();
            NotifyUnreadChanged();
        }

        public void MarkAsRead(string id)
        {
            var notif = _notifications.FirstOrDefault(n => n.Id == id);
            if (notif != null && !notif.IsRead)
            {
                notif.IsRead = true;
                RenderList();
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
                RenderList();
                NotifyUnreadChanged();
            }
        }

        public void Remove(string id)
        {
            if (_notifications.RemoveAll(n => n.Id == id) > 0)
            {
                RenderList();
                NotifyUnreadChanged();
            }
        }

        public void Clear()
        {
            if (_notifications.Count > 0)
            {
                _notifications.Clear();
                RenderList();
                NotifyUnreadChanged();
            }
        }

        private void NotifyUnreadChanged()
        {
            int unread = UnreadCount;
            if (unread > 0)
            {
                _badgeText.Text = unread.ToString();
                _badgeBorder.Visibility = Visibility.Visible;
            }
            else
            {
                _badgeBorder.Visibility = Visibility.Collapsed;
            }
            UnreadCountChanged?.Invoke(this, EventArgs.Empty);
        }

        private void RenderList()
        {
            _listPanel.Children.Clear();

            foreach (var n in _notifications)
            {
                var card = new Border
                {
                    Height = 72,
                    Background = n.IsRead ? ZeroWpfTheme.BgCard : new SolidColorBrush(Color.FromArgb(20, ((SolidColorBrush)ZeroWpfTheme.PrimaryAccent).Color.R, ((SolidColorBrush)ZeroWpfTheme.PrimaryAccent).Color.G, ((SolidColorBrush)ZeroWpfTheme.PrimaryAccent).Color.B)),
                    BorderBrush = ZeroWpfTheme.BorderDefault,
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    Cursor = Cursors.Hand
                };

                card.MouseEnter += (s, e) => card.Background = ZeroWpfTheme.BgHover;
                card.MouseLeave += (s, e) => card.Background = n.IsRead ? ZeroWpfTheme.BgCard : new SolidColorBrush(Color.FromArgb(20, ((SolidColorBrush)ZeroWpfTheme.PrimaryAccent).Color.R, ((SolidColorBrush)ZeroWpfTheme.PrimaryAccent).Color.G, ((SolidColorBrush)ZeroWpfTheme.PrimaryAccent).Color.B));

                var grid = new Grid();
                
                Brush sevBrush = ZeroWpfTheme.InfoAccent;
                if (n.Severity == NotificationSeverity.Success) sevBrush = ZeroWpfTheme.SuccessAccent;
                else if (n.Severity == NotificationSeverity.Warning) sevBrush = ZeroWpfTheme.WarningAccent;
                else if (n.Severity == NotificationSeverity.Error) sevBrush = ZeroWpfTheme.DangerAccent;

                grid.Children.Add(new Border { Background = sevBrush, Width = 4, HorizontalAlignment = HorizontalAlignment.Left });

                var title = new TextBlock
                {
                    Text = n.Title,
                    FontWeight = FontWeights.Bold,
                    Foreground = ZeroWpfTheme.TextPrimary,
                    Margin = new Thickness(16, 8, 80, 0),
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                grid.Children.Add(title);

                if (ShowTimestamp)
                {
                    var timeSpan = DateTime.Now - n.Timestamp;
                    string timeStr = timeSpan.TotalMinutes < 1 ? "Just now" :
                                     timeSpan.TotalHours < 1 ? $"{(int)timeSpan.TotalMinutes}m ago" :
                                     timeSpan.TotalDays < 1 ? $"{(int)timeSpan.TotalHours}h ago" :
                                     n.Timestamp.ToString("MMM dd");

                    grid.Children.Add(new TextBlock
                    {
                        Text = timeStr,
                        FontSize = 11,
                        Foreground = ZeroWpfTheme.TextSecondary,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Margin = new Thickness(0, 10, 12, 0)
                    });
                }

                var msg = new TextBlock
                {
                    Text = n.Message,
                    FontSize = 11,
                    Foreground = ZeroWpfTheme.TextSecondary,
                    Margin = new Thickness(16, 28, string.IsNullOrEmpty(n.ActionText) ? 16 : 80, 0),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    TextWrapping = TextWrapping.WrapWithOverflow,
                    MaxHeight = 34
                };
                grid.Children.Add(msg);

                if (!string.IsNullOrEmpty(n.ActionText))
                {
                    var actionBtn = new Border
                    {
                        Background = ZeroWpfTheme.PrimaryAccent,
                        CornerRadius = new CornerRadius(4),
                        Width = 60,
                        Height = 24,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        VerticalAlignment = VerticalAlignment.Bottom,
                        Margin = new Thickness(0, 0, 12, 12),
                        Cursor = Cursors.Hand
                    };
                    actionBtn.Child = new TextBlock
                    {
                        Text = n.ActionText,
                        Foreground = Brushes.White,
                        FontSize = 11,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        TextTrimming = TextTrimming.CharacterEllipsis
                    };
                    actionBtn.MouseDown += (s, e) =>
                    {
                        e.Handled = true;
                        ActionClicked?.Invoke(this, new NotificationActionEventArgs(n, n.ActionText));
                    };
                    grid.Children.Add(actionBtn);
                }

                card.Child = grid;

                card.MouseDown += (s, e) =>
                {
                    if (!n.IsRead)
                    {
                        n.IsRead = true;
                        card.Background = ZeroWpfTheme.BgCard;
                        NotifyUnreadChanged();
                    }
                    NotificationClicked?.Invoke(this, new NotificationClickedEventArgs(n));
                };

                _listPanel.Children.Add(card);
            }
        }

        protected override void OnThemeChanged()
        {
            base.OnThemeChanged();
            RenderList();
        }
    }

    public class ZNotificationBell : WpfControlBase
    {
        private ZNotificationCenter? _center;
        private Border _root;
        private Border _badge;
        private TextBlock _badgeText;
        private Path _bellPath;

        public static readonly DependencyProperty NotificationCenterProperty =
            DependencyProperty.Register(nameof(NotificationCenter), typeof(ZNotificationCenter), typeof(ZNotificationBell), new PropertyMetadata(null, OnCenterChanged));

        public ZNotificationCenter? NotificationCenter
        {
            get => (ZNotificationCenter?)GetValue(NotificationCenterProperty);
            set => SetValue(NotificationCenterProperty, value);
        }

        public ZNotificationBell()
        {
            Width = 40;
            Height = 40;

            _root = new Border
            {
                Background = Brushes.Transparent,
                CornerRadius = new CornerRadius(6),
                Cursor = Cursors.Hand
            };

            var grid = new Grid();

            _bellPath = new Path
            {
                Stroke = ZeroWpfTheme.TextPrimary,
                StrokeThickness = 2,
                StrokeLineJoin = PenLineJoin.Round,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Data = Geometry.Parse("M -6,-10 A 12,12 0 0 1 6,-10 L 6,-4 L 8,8 L -8,8 L -6,-4 Z M -3,8 A 6,6 0 0 0 3,8")
            };
            
            // Adjust the parse string to match bell better, standard way:
            var geomGroup = new GeometryGroup();
            geomGroup.Children.Add(new PathGeometry(new PathFigure[] {
                new PathFigure(new Point(-6, 0), new PathSegment[] {
                    new ArcSegment(new Point(6, 0), new Size(6, 6), 0, false, SweepDirection.Clockwise, true),
                    new LineSegment(new Point(6, 4), true),
                    new LineSegment(new Point(8, 8), true),
                    new LineSegment(new Point(-8, 8), true),
                    new LineSegment(new Point(-6, 4), true),
                    new LineSegment(new Point(-6, 0), true)
                }, true)
            }));
            geomGroup.Children.Add(new PathGeometry(new PathFigure[] {
                new PathFigure(new Point(-3, 8), new PathSegment[] {
                    new ArcSegment(new Point(3, 8), new Size(3, 3), 0, false, SweepDirection.Counterclockwise, true)
                }, false)
            }));
            
            _bellPath.Data = geomGroup;
            // Center geometry inside a specific transform
            _bellPath.RenderTransform = new TranslateTransform(20, 16);

            grid.Children.Add(_bellPath);

            _badgeText = new TextBlock
            {
                Foreground = Brushes.White,
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0, 4, 0)
            };

            _badge = new Border
            {
                Background = ZeroWpfTheme.DangerAccent,
                CornerRadius = new CornerRadius(8),
                Height = 16,
                MinWidth = 16,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 4, 4, 0),
                Visibility = Visibility.Collapsed,
                Child = _badgeText
            };
            grid.Children.Add(_badge);

            _root.Child = grid;

            _root.MouseEnter += (s, e) => _root.Background = ZeroWpfTheme.BgHover;
            _root.MouseLeave += (s, e) => _root.Background = Brushes.Transparent;
            _root.MouseDown += (s, e) =>
            {
                if (_center != null)
                {
                    _center.Visibility = _center.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
                }
            };

            AddVisualChild(_root);
            AddLogicalChild(_root);
        }

        protected override int VisualChildrenCount => 1;
        protected override Visual GetVisualChild(int index) => _root;

        protected override Size MeasureOverride(Size constraint)
        {
            _root.Measure(constraint);
            return _root.DesiredSize;
        }

        protected override Size ArrangeOverride(Size arrangeBounds)
        {
            _root.Arrange(new Rect(arrangeBounds));
            return arrangeBounds;
        }

        private static void OnCenterChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZNotificationBell bell)
            {
                if (e.OldValue is ZNotificationCenter oldCenter)
                    oldCenter.UnreadCountChanged -= bell.Center_UnreadCountChanged;
                
                if (e.NewValue is ZNotificationCenter newCenter)
                {
                    newCenter.UnreadCountChanged += bell.Center_UnreadCountChanged;
                    bell.UpdateBadge();
                }
            }
        }

        private void Center_UnreadCountChanged(object? sender, EventArgs e)
        {
            UpdateBadge();
        }

        private void UpdateBadge()
        {
            int unread = _center?.UnreadCount ?? 0;
            if (unread > 0)
            {
                _badgeText.Text = unread > 99 ? "99+" : unread.ToString();
                _badge.Visibility = Visibility.Visible;
            }
            else
            {
                _badge.Visibility = Visibility.Collapsed;
            }
        }

        protected override void OnThemeChanged()
        {
            base.OnThemeChanged();
            _bellPath.Stroke = ZeroWpfTheme.TextPrimary;
            _badge.Background = ZeroWpfTheme.DangerAccent;
        }
    }

    [Obsolete("Use ZNotificationCenter instead.")]
    public class NotificationCenterControl : ZNotificationCenter { }

    [Obsolete("Use ZNotificationCenter instead.")]
    public class ZeroNotificationCenter : ZNotificationCenter { }
}
