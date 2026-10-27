using System;
using System.Collections.Generic;

namespace ZeroUI.Core.Overlays
{
    /// <summary>
    /// Specifies the screen anchor position for stacked toast notifications.
    /// </summary>
    public enum ToastStackPosition
    {
        TopRight,
        BottomRight,
        TopLeft,
        BottomLeft,
        TopCenter,
        BottomCenter
    }

    /// <summary>
    /// Categorizes toast notifications by operational priority and severity.
    /// </summary>
    public enum CoreToastType
    {
        Info,
        Success,
        Warning,
        Error,
        Alarm
    }

    /// <summary>
    /// Encapsulates individual toast notification state within the stacking coordinate engine.
    /// Pure core model independent of UI platform.
    /// </summary>
    public class ToastNotificationItem
    {
        public string Id { get; }
        public string Title { get; set; }
        public string Message { get; set; }
        public CoreToastType Type { get; set; }
        public int DurationMs { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int TargetX { get; set; }
        public int TargetY { get; set; }
        public int CurrentX { get; set; }
        public int CurrentY { get; set; }
        public object? Tag { get; set; }
        public DateTime CreatedAt { get; }

        public ToastNotificationItem(
            string message,
            string title = "",
            CoreToastType type = CoreToastType.Info,
            int durationMs = 3000,
            int width = 340,
            int height = 64)
        {
            Id = Guid.NewGuid().ToString("N");
            Message = message ?? string.Empty;
            Title = title ?? string.Empty;
            Type = type;
            DurationMs = Math.Max(1000, durationMs);
            Width = Math.Max(100, width);
            Height = Math.Max(40, height);
            CreatedAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// High-performance stacking and coordinate calculation engine for toast notifications.
    /// Handles multi-position anchors, overflow FIFO queueing, and relocation interpolation.
    /// Zero external dependencies, pure business logic.
    /// </summary>
    public class ToastStackEngine
    {
        private readonly List<ToastNotificationItem> _activeItems = new List<ToastNotificationItem>();
        private readonly Queue<ToastNotificationItem> _pendingQueue = new Queue<ToastNotificationItem>();

        public int MaxVisibleToasts { get; set; } = 5;
        public int MarginX { get; set; } = 24;
        public int MarginY { get; set; } = 24;
        public int Gap { get; set; } = 10;
        public ToastStackPosition Position { get; set; } = ToastStackPosition.TopRight;

        public IReadOnlyList<ToastNotificationItem> ActiveItems => _activeItems;
        public int PendingCount => _pendingQueue.Count;

        /// <summary>
        /// Attempts to enqueue a new toast. If active capacity is reached, it is staged in the FIFO queue.
        /// </summary>
        /// <returns>True if the toast is immediately activated; false if queued.</returns>
        public bool Enqueue(
            ToastNotificationItem item,
            int containerWidth,
            int containerHeight,
            int containerX = 0,
            int containerY = 0)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));

            if (_activeItems.Count < MaxVisibleToasts)
            {
                _activeItems.Add(item);
                RecalculatePositions(containerWidth, containerHeight, containerX, containerY);
                item.CurrentX = item.TargetX;
                item.CurrentY = item.TargetY;
                return true;
            }

            _pendingQueue.Enqueue(item);
            return false;
        }

        /// <summary>
        /// Removes an active toast by Id. Recalculates remaining target coordinates
        /// and activates the next pending toast from the queue if available.
        /// </summary>
        /// <returns>True if an active toast was removed.</returns>
        public bool Remove(
            string id,
            int containerWidth,
            int containerHeight,
            int containerX,
            int containerY,
            out ToastNotificationItem? newlyActivated)
        {
            newlyActivated = null;
            int index = _activeItems.FindIndex(t => t.Id == id);
            if (index < 0) return false;

            _activeItems.RemoveAt(index);

            // Dequeue next pending toast if room exists
            if (_pendingQueue.Count > 0 && _activeItems.Count < MaxVisibleToasts)
            {
                newlyActivated = _pendingQueue.Dequeue();
                _activeItems.Add(newlyActivated);
            }

            RecalculatePositions(containerWidth, containerHeight, containerX, containerY);

            if (newlyActivated != null)
            {
                newlyActivated.CurrentX = newlyActivated.TargetX;
                newlyActivated.CurrentY = newlyActivated.TargetY;
            }

            return true;
        }

        /// <summary>
        /// Recomputes target X and Y coordinates for all currently active items based on anchor and stacking rules.
        /// </summary>
        public void RecalculatePositions(
            int containerWidth,
            int containerHeight,
            int containerX = 0,
            int containerY = 0)
        {
            int cumulativeOffset = 0;

            for (int i = 0; i < _activeItems.Count; i++)
            {
                var item = _activeItems[i];
                var (tx, ty) = CalculateTargetLocation(
                    i,
                    item.Width,
                    item.Height,
                    cumulativeOffset,
                    containerWidth,
                    containerHeight,
                    containerX,
                    containerY);

                item.TargetX = tx;
                item.TargetY = ty;

                cumulativeOffset += item.Height + Gap;
            }
        }

        /// <summary>
        /// Calculates the exact target screen coordinate for an item given its index and cumulative offset.
        /// </summary>
        public (int X, int Y) CalculateTargetLocation(
            int index,
            int itemWidth,
            int itemHeight,
            int cumulativeOffset,
            int containerWidth,
            int containerHeight,
            int containerX = 0,
            int containerY = 0)
        {
            int x;
            int y;

            switch (Position)
            {
                case ToastStackPosition.TopRight:
                    x = containerX + containerWidth - itemWidth - MarginX;
                    y = containerY + MarginY + cumulativeOffset;
                    break;

                case ToastStackPosition.BottomRight:
                    x = containerX + containerWidth - itemWidth - MarginX;
                    y = containerY + containerHeight - MarginY - itemHeight - cumulativeOffset;
                    break;

                case ToastStackPosition.TopLeft:
                    x = containerX + MarginX;
                    y = containerY + MarginY + cumulativeOffset;
                    break;

                case ToastStackPosition.BottomLeft:
                    x = containerX + MarginX;
                    y = containerY + containerHeight - MarginY - itemHeight - cumulativeOffset;
                    break;

                case ToastStackPosition.TopCenter:
                    x = containerX + (containerWidth - itemWidth) / 2;
                    y = containerY + MarginY + cumulativeOffset;
                    break;

                case ToastStackPosition.BottomCenter:
                    x = containerX + (containerWidth - itemWidth) / 2;
                    y = containerY + containerHeight - MarginY - itemHeight - cumulativeOffset;
                    break;

                default:
                    x = containerX + containerWidth - itemWidth - MarginX;
                    y = containerY + MarginY + cumulativeOffset;
                    break;
            }

            return (x, y);
        }

        /// <summary>
        /// Clears all active and pending toasts.
        /// </summary>
        public void Clear()
        {
            _activeItems.Clear();
            _pendingQueue.Clear();
        }
    }
}
