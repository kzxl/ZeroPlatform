using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using ZeroUI.Core.Overlays;
using ZeroUI.Wpf.Theme;
using ZeroUI.Core.Notification;

namespace ZeroUI.Wpf.Overlays
{
    /// <summary>
    /// Categorizes toast notifications by operational priority and severity.
    /// </summary>
    public enum ToastType
    {
        Info,
        Success,
        Warning,
        Error,
        Alarm
    }

    [Obsolete("WpfToastType is deprecated. Use ToastType instead.")]
    public enum WpfToastType
    {
        Info = ToastType.Info,
        Success = ToastType.Success,
        Warning = ToastType.Warning,
        Error = ToastType.Error,
        Alarm = ToastType.Alarm
    }

    /// <summary>
    /// Modern lightweight non-blocking floating Toast notification for ZeroUI WPF.
    /// Slides in from anchor corner, displays state glyph, supports title + message, close button, pause on hover, and smooth stacking animations.
    /// </summary>
    public class ZToastNotification : Window
    {
        private readonly DispatcherTimer _stayTimer;
        private bool _isFadingOut = false;
        private readonly Action? _onClick;

        public new string Title { get; }
        public string Message { get; }
        public ToastType Type { get; }
        public ToastStackPosition Position { get; set; } = ToastStackPosition.TopRight;

        public ZToastNotification(
            Window? owner,
            string message,
            string title = "",
            ToastType type = ToastType.Info,
            int durationMs = 3000,
            Action? onClick = null,
            ToastStackPosition position = ToastStackPosition.TopRight)
        {
            Message = message ?? string.Empty;
            Title = title ?? string.Empty;
            Type = type;
            _onClick = onClick;
            Position = position;

            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            Topmost = true;
            SizeToContent = SizeToContent.WidthAndHeight;

            Color accent = type switch
            {
                ToastType.Success => Color.FromRgb(34, 197, 94),
                ToastType.Warning => Color.FromRgb(234, 179, 8),
                ToastType.Error => Color.FromRgb(239, 68, 68),
                ToastType.Alarm => Color.FromRgb(220, 38, 38),
                _ => Color.FromRgb(79, 70, 229)
            };

            string icon = type switch
            {
                ToastType.Success => "✔",
                ToastType.Warning => "⚠",
                ToastType.Error => "✕",
                ToastType.Alarm => "⚡",
                _ => "ℹ"
            };

            var border = new Border
            {
                Background = ZeroWpfTheme.BgCard,
                BorderBrush = new SolidColorBrush(accent),
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(14, 10, 14, 10),
                MinWidth = 280,
                MaxWidth = 420,
                Cursor = System.Windows.Input.Cursors.Hand,
                Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 14,
                Color = Colors.Black,
                Opacity = 0.28,
                ShadowDepth = 3
            }
            };

            var mainGrid = new Grid();
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // 1. Icon block
            var iconBlock = new TextBlock
            {
                Text = icon,
                FontWeight = FontWeights.Bold,
                FontSize = 15.0,
                Foreground = new SolidColorBrush(accent),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };
            Grid.SetColumn(iconBlock, 0);
            mainGrid.Children.Add(iconBlock);

            // 2. Text Content (Title + Message or Message alone)
            var textStack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };

            if (!string.IsNullOrEmpty(Title))
            {
                var titleBlock = new TextBlock
                {
                    Text = Title,
                    FontWeight = FontWeights.Bold,
                    FontSize = 12.5,
                    Foreground = ZeroWpfTheme.TextPrimary,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    Margin = new Thickness(0, 0, 0, 2)
                };
                textStack.Children.Add(titleBlock);

                var msgBlock = new TextBlock
                {
                    Text = Message,
                    FontSize = 11.5,
                    Foreground = ZeroWpfTheme.TextSecondary,
                    TextWrapping = TextWrapping.Wrap
                };
                textStack.Children.Add(msgBlock);
            }
            else
            {
                var msgBlock = new TextBlock
                {
                    Text = Message,
                    FontSize = 12.0,
                    Foreground = ZeroWpfTheme.TextPrimary,
                    TextWrapping = TextWrapping.Wrap
                };
                textStack.Children.Add(msgBlock);
            }
            Grid.SetColumn(textStack, 1);
            mainGrid.Children.Add(textStack);

            // 3. Close Button (✕)
            var closeBlock = new TextBlock
            {
                Text = "✕",
                FontWeight = FontWeights.Bold,
                FontSize = 11.0,
                Foreground = ZeroWpfTheme.TextMuted,
                VerticalAlignment = VerticalAlignment.Top,
                Cursor = System.Windows.Input.Cursors.Hand,
                Margin = new Thickness(4, 0, 0, 0)
            };
            closeBlock.MouseEnter += (s, e) => closeBlock.Foreground = ZeroWpfTheme.TextPrimary;
            closeBlock.MouseLeave += (s, e) => closeBlock.Foreground = ZeroWpfTheme.TextMuted;
            closeBlock.MouseDown += (s, e) =>
            {
                e.Handled = true;
                Dismiss();
            };
            Grid.SetColumn(closeBlock, 2);
            mainGrid.Children.Add(closeBlock);

            border.Child = mainGrid;
            Content = border;

            _stayTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(Math.Max(1000, durationMs)) };
            _stayTimer.Tick += (s, e) =>
            {
                _stayTimer.Stop();
                Dismiss();
            };
            _stayTimer.Start();

            // Animate In
            Loaded += (s, e) =>
            {
                var anim = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(180));
                BeginAnimation(OpacityProperty, anim);
            };

            // Pause countdown on hover
            MouseEnter += (s, e) => _stayTimer.Stop();
            MouseLeave += (s, e) =>
            {
                if (!_isFadingOut)
                {
                    _stayTimer.Start();
                }
            };

            MouseDown += (s, e) =>
            {
                _onClick?.Invoke();
                Dismiss();
            };
        }

        public ZToastNotification(Window? owner, string message, ToastType type = ToastType.Info, int durationMs = 3000)
            : this(owner, message, string.Empty, type, durationMs)
        {
        }

        public void AnimateTo(double targetLeft, double targetTop)
        {
            var animTop = new DoubleAnimation(Top, targetTop, TimeSpan.FromMilliseconds(180))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            BeginAnimation(TopProperty, animTop);

            var animLeft = new DoubleAnimation(Left, targetLeft, TimeSpan.FromMilliseconds(180))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            BeginAnimation(LeftProperty, animLeft);
        }

        public void Dismiss()
        {
            if (_isFadingOut) return;
            _isFadingOut = true;
            _stayTimer.Stop();

            var fadeOut = new DoubleAnimation(Opacity, 0.0, TimeSpan.FromMilliseconds(180));
            fadeOut.Completed += (s, e) =>
            {
                Close();
            };
            BeginAnimation(OpacityProperty, fadeOut);
        }

        #region Static Convenience APIs (Delegates to ToastStackManager)

        public static void Show(
            Window? owner,
            string message,
            string title = "",
            ToastType type = ToastType.Info,
            int durationMs = 3000,
            Action? onClick = null,
            ToastStackPosition? position = null)
        {
            ToastStackManager.Show(owner, message, title, type, durationMs, onClick, position);
        }

        public static void Show(Window? owner, string message, ToastType type = ToastType.Info, int durationMs = 3000) =>
            ToastStackManager.Show(owner, message, string.Empty, type, durationMs);

        public static void Success(Window? owner, string message, string title = "", int durationMs = 3000) =>
            ToastStackManager.Show(owner, message, title, ToastType.Success, durationMs);

        public static void Success(Window? owner, string message, int durationMs) =>
            ToastStackManager.Show(owner, message, string.Empty, ToastType.Success, durationMs);

        public static void Info(Window? owner, string message, string title = "", int durationMs = 3000) =>
            ToastStackManager.Show(owner, message, title, ToastType.Info, durationMs);

        public static void Info(Window? owner, string message, int durationMs) =>
            ToastStackManager.Show(owner, message, string.Empty, ToastType.Info, durationMs);

        public static void Warning(Window? owner, string message, string title = "", int durationMs = 3500) =>
            ToastStackManager.Show(owner, message, title, ToastType.Warning, durationMs);

        public static void Warning(Window? owner, string message, int durationMs) =>
            ToastStackManager.Show(owner, message, string.Empty, ToastType.Warning, durationMs);

        public static void Error(Window? owner, string message, string title = "", int durationMs = 4000) =>
            ToastStackManager.Show(owner, message, title, ToastType.Error, durationMs);

        public static void Error(Window? owner, string message, int durationMs) =>
            ToastStackManager.Show(owner, message, string.Empty, ToastType.Error, durationMs);

        public static void Alarm(Window? owner, string message, string title = "", int durationMs = 4000) =>
            ToastStackManager.Show(owner, message, title, ToastType.Alarm, durationMs);

        public static void Alarm(Window? owner, string message, int durationMs) =>
            ToastStackManager.Show(owner, message, string.Empty, ToastType.Alarm, durationMs);

        #endregion
    }

    /// <summary>
    /// Thread-safe central stacking manager for ZeroUI WPF toast notifications.
    /// Manages active toast slot allocations, multi-corner anchors, overflow queueing, and smooth slide repositioning.
    /// </summary>
    public static class ToastStackManager
    {
        private class PendingToastInfo
        {
            public Window? Owner { get; set; }
            public string Message { get; set; } = string.Empty;
            public string Title { get; set; } = string.Empty;
            public ToastType Type { get; set; }
            public int DurationMs { get; set; }
            public Action? OnClick { get; set; }
            public ToastStackPosition Position { get; set; }
        }

        private static readonly List<ZToastNotification> _activeToasts = new List<ZToastNotification>();
        private static readonly Queue<PendingToastInfo> _pendingQueue = new Queue<PendingToastInfo>();
        private static readonly object _syncLock = new object();

        public static int MaxVisibleToasts { get; set; } = 5;
        public static int MarginX { get; set; } = 24;
        public static int MarginY { get; set; } = 24;
        public static int Gap { get; set; } = 10;
        public static ToastStackPosition DefaultPosition { get; set; } = ToastStackPosition.TopRight;
        public static ZeroNotificationDeliveryMode DeliveryMode { get; set; } = ZeroNotificationDeliveryMode.Auto;
        public static bool RouteAlarmsToSystem { get; set; } = true;

        public static IReadOnlyList<ZToastNotification> ActiveToasts
        {
            get
            {
                lock (_syncLock)
                {
                    return _activeToasts.ToArray();
                }
            }
        }

        public static int PendingCount
        {
            get
            {
                lock (_syncLock)
                {
                    return _pendingQueue.Count;
                }
            }
        }

        public static void Show(
            Window? owner,
            string message,
            string title = "",
            ToastType type = ToastType.Info,
            int durationMs = 3000,
            Action? onClick = null,
            ToastStackPosition? position = null)
        {
            var targetOwner = owner ?? Application.Current?.MainWindow;
            var dispatcher = targetOwner?.Dispatcher ?? Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;

            if (!dispatcher.CheckAccess())
            {
                dispatcher.BeginInvoke(new Action(() => Show(targetOwner, message, title, type, durationMs, onClick, position)));
                return;
            }

            if (targetOwner != null)
            {
                WindowsNotificationBridge.RegisterMainWindow(targetOwner);
            }

            bool isAppForeground = targetOwner != null && targetOwner.IsActive && targetOwner.WindowState != WindowState.Minimized;
            bool shouldShowInApp = DeliveryMode == ZeroNotificationDeliveryMode.InAppOnly ||
                                   DeliveryMode == ZeroNotificationDeliveryMode.Dual ||
                                   (DeliveryMode == ZeroNotificationDeliveryMode.Auto && isAppForeground);

            bool shouldShowSystem = DeliveryMode == ZeroNotificationDeliveryMode.SystemOnly ||
                                    DeliveryMode == ZeroNotificationDeliveryMode.Dual ||
                                    (DeliveryMode == ZeroNotificationDeliveryMode.Auto && !isAppForeground) ||
                                    (RouteAlarmsToSystem && type == ToastType.Alarm);

            if (shouldShowSystem)
            {
                WindowsNotificationBridge.ShowNotification(title, message, type, durationMs, onClick);
            }

            if (!shouldShowInApp) return;

            var pos = position ?? DefaultPosition;

            lock (_syncLock)
            {
                if (_activeToasts.Count < MaxVisibleToasts)
                {
                    SpawnToast(targetOwner, message, title, type, durationMs, onClick, pos);
                }
                else
                {
                    _pendingQueue.Enqueue(new PendingToastInfo
                    {
                        Owner = targetOwner,
                        Message = message,
                        Title = title,
                        Type = type,
                        DurationMs = durationMs,
                        OnClick = onClick,
                        Position = pos
                    });
                }
            }
        }

        public static void Success(Window? owner, string message, string title = "") =>
            Show(owner, message, title, ToastType.Success);

        public static void Info(Window? owner, string message, string title = "") =>
            Show(owner, message, title, ToastType.Info);

        public static void Warning(Window? owner, string message, string title = "") =>
            Show(owner, message, title, ToastType.Warning);

        public static void Error(Window? owner, string message, string title = "") =>
            Show(owner, message, title, ToastType.Error);

        public static void Alarm(Window? owner, string message, string title = "") =>
            Show(owner, message, title, ToastType.Alarm);

        private static void SpawnToast(
            Window? owner,
            string message,
            string title,
            ToastType type,
            int durationMs,
            Action? onClick,
            ToastStackPosition pos)
        {
            var toast = new ZToastNotification(owner, message, title, type, durationMs, onClick, pos);

            toast.Loaded += (s, e) =>
            {
                // Compute initial target location
                var loc = CalculateLocation(owner, toast.ActualWidth, toast.ActualHeight, _activeToasts.IndexOf(toast), pos);
                toast.Left = loc.X;
                toast.Top = loc.Y;
            };

            toast.Closed += (s, e) => OnToastClosed(toast, owner);

            _activeToasts.Add(toast);
            toast.Show();
        }

        private static void OnToastClosed(ZToastNotification toast, Window? owner)
        {
            lock (_syncLock)
            {
                _activeToasts.Remove(toast);

                if (_pendingQueue.Count > 0 && _activeToasts.Count < MaxVisibleToasts)
                {
                    var next = _pendingQueue.Dequeue();
                    SpawnToast(next.Owner, next.Message, next.Title, next.Type, next.DurationMs, next.OnClick, next.Position);
                }

                RestackActiveToasts(owner);
            }
        }

        private static void RestackActiveToasts(Window? owner)
        {
            for (int i = 0; i < _activeToasts.Count; i++)
            {
                var t = _activeToasts[i];
                if (t.IsLoaded)
                {
                    var target = CalculateLocation(owner, t.ActualWidth, t.ActualHeight, i, t.Position);
                    t.AnimateTo(target.X, target.Y);
                }
            }
        }

        private static Point CalculateLocation(
            Window? owner,
            double toastWidth,
            double toastHeight,
            int stackIndex,
            ToastStackPosition position)
        {
            double ownerLeft, ownerTop, ownerWidth, ownerHeight;

            if (owner != null && owner.IsLoaded && owner.WindowState != WindowState.Minimized)
            {
                ownerLeft = owner.Left;
                ownerTop = owner.Top;
                ownerWidth = owner.ActualWidth;
                ownerHeight = owner.ActualHeight;
            }
            else
            {
                var workArea = SystemParameters.WorkArea;
                ownerLeft = workArea.Left;
                ownerTop = workArea.Top;
                ownerWidth = workArea.Width;
                ownerHeight = workArea.Height;
            }

            double cumulativeOffset = stackIndex * (toastHeight + Gap);
            double x;
            double y;

            switch (position)
            {
                case ToastStackPosition.TopRight:
                    x = ownerLeft + ownerWidth - toastWidth - MarginX;
                    y = ownerTop + MarginY + cumulativeOffset;
                    break;

                case ToastStackPosition.BottomRight:
                    x = ownerLeft + ownerWidth - toastWidth - MarginX;
                    y = ownerTop + ownerHeight - MarginY - toastHeight - cumulativeOffset;
                    break;

                case ToastStackPosition.TopLeft:
                    x = ownerLeft + MarginX;
                    y = ownerTop + MarginY + cumulativeOffset;
                    break;

                case ToastStackPosition.BottomLeft:
                    x = ownerLeft + MarginX;
                    y = ownerTop + ownerHeight - MarginY - toastHeight - cumulativeOffset;
                    break;

                case ToastStackPosition.TopCenter:
                    x = ownerLeft + (ownerWidth - toastWidth) / 2;
                    y = ownerTop + MarginY + cumulativeOffset;
                    break;

                case ToastStackPosition.BottomCenter:
                    x = ownerLeft + (ownerWidth - toastWidth) / 2;
                    y = ownerTop + ownerHeight - MarginY - toastHeight - cumulativeOffset;
                    break;

                default:
                    x = ownerLeft + ownerWidth - toastWidth - MarginX;
                    y = ownerTop + MarginY + cumulativeOffset;
                    break;
            }

            return new Point(x, y);
        }

        public static void Clear()
        {
            lock (_syncLock)
            {
                _pendingQueue.Clear();
                var activeCopy = new List<ZToastNotification>(_activeToasts);
                foreach (var toast in activeCopy)
                {
                    try { toast.Close(); } catch { }
                }
                _activeToasts.Clear();
            }
        }
    }

    /// <summary>
    /// Backward-compatibility alias for <see cref="ToastNotification"/>.
    /// </summary>
    [Obsolete("ZeroToast is deprecated. Use ToastNotification instead.")]
    public class ZeroToast : ToastNotification
    {
        public ZeroToast(Window? owner, string message, ToastType type, int durationMs = 3000)
            : base(owner, message, string.Empty, type, durationMs) { }

        public ZeroToast(Window? owner, string message, WpfToastType type, int durationMs = 3000)
            : base(owner, message, string.Empty, (ToastType)type, durationMs) { }

        public new static void Success(Window? owner, string message, int durationMs = 3000) =>
            ToastNotification.Success(owner, message, string.Empty, durationMs);

        public new static void Warning(Window? owner, string message, int durationMs = 3500) =>
            ToastNotification.Warning(owner, message, string.Empty, durationMs);

        public new static void Error(Window? owner, string message, int durationMs = 4000) =>
            ToastNotification.Error(owner, message, string.Empty, durationMs);

        public new static void Info(Window? owner, string message, int durationMs = 3000) =>
            ToastNotification.Info(owner, message, string.Empty, durationMs);

        public new static void Alarm(Window? owner, string message, int durationMs = 4000) =>
            ToastNotification.Alarm(owner, message, string.Empty, durationMs);
    }

    /// <summary>
    /// Canonical Z-prefixed alias for <see cref="ToastNotification"/>.
    /// </summary>
    /// <summary>
    /// Legacy shim for ToastNotification.
    /// </summary>
    [Obsolete("ToastNotification is deprecated and will be removed in 5 release cycles. Please migrate to ZToastNotification instead.")]
    public class ToastNotification : ZToastNotification
    {
        public ToastNotification(
            Window? owner,
            string message,
            string title = "",
            ToastType type = ToastType.Info,
            int durationMs = 3000,
            Action? onClick = null,
            ToastStackPosition position = ToastStackPosition.TopRight)
            : base(owner, message, title, type, durationMs, onClick, position) { }

        public ToastNotification(Window? owner, string message, ToastType type = ToastType.Info, int durationMs = 3000)
            : base(owner, message, type, durationMs) { }
    }

    /// <summary>
    /// Canonical Z-prefixed short alias for <see cref="ZToastNotification"/>.
    /// </summary>
    public class ZToast : ZToastNotification
    {
        public ZToast(
            Window? owner,
            string message,
            string title = "",
            ToastType type = ToastType.Info,
            int durationMs = 3000,
            Action? onClick = null,
            ToastStackPosition position = ToastStackPosition.TopRight)
            : base(owner, message, title, type, durationMs, onClick, position) { }

        public ZToast(Window? owner, string message, ToastType type = ToastType.Info, int durationMs = 3000)
            : base(owner, message, type, durationMs) { }
    }
}
