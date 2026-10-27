using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ZeroUI.Core.Layout;

namespace ZeroUI.Wpf.Layout
{
    /// <summary>
    /// Responsive card and widget layout container for WPF that automatically wraps child items
    /// into dynamic rows based on viewport width. Features animated drag-and-drop tile reordering
    /// and JSON layout state serialization for customizable plant dashboards.
    /// </summary>
    public class ZFlowLayout : Panel
    {
        public static readonly DependencyProperty HGapProperty =
            DependencyProperty.Register(
                nameof(HGap),
                typeof(double),
                typeof(ZFlowLayout),
                new FrameworkPropertyMetadata(8.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

        public static readonly DependencyProperty VGapProperty =
            DependencyProperty.Register(
                nameof(VGap),
                typeof(double),
                typeof(ZFlowLayout),
                new FrameworkPropertyMetadata(8.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

        public static readonly DependencyProperty AllowDragReorderingProperty =
            DependencyProperty.Register(
                nameof(AllowDragReordering),
                typeof(bool),
                typeof(ZFlowLayout),
                new FrameworkPropertyMetadata(true));

        public static readonly DependencyProperty DragThresholdProperty =
            DependencyProperty.Register(
                nameof(DragThreshold),
                typeof(double),
                typeof(ZFlowLayout),
                new FrameworkPropertyMetadata(6.0));

        private UIElement? _draggedElement = null;
        private Point _dragStartPoint;
        private bool _isDragging = false;
        private readonly List<LayoutRect> _lastSlots = new List<LayoutRect>();

        public event EventHandler? OrderChanged;

        #region Properties

        public double HGap
        {
            get => (double)GetValue(HGapProperty);
            set => SetValue(HGapProperty, Math.Max(0.0, value));
        }

        public double VGap
        {
            get => (double)GetValue(VGapProperty);
            set => SetValue(VGapProperty, Math.Max(0.0, value));
        }

        public bool AllowDragReordering
        {
            get => (bool)GetValue(AllowDragReorderingProperty);
            set => SetValue(AllowDragReorderingProperty, value);
        }

        public double DragThreshold
        {
            get => (double)GetValue(DragThresholdProperty);
            set => SetValue(DragThresholdProperty, Math.Max(1.0, value));
        }

        #endregion

        public ZFlowLayout()
        {
            ClipToBounds = true;
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            int count = InternalChildren.Count;
            if (count == 0) return new Size(0, 0);

            var itemSizes = new List<(int Width, int Height)>(count);
            for (int i = 0; i < count; i++)
            {
                UIElement child = InternalChildren[i];
                child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                itemSizes.Add(((int)Math.Ceiling(child.DesiredSize.Width), (int)Math.Ceiling(child.DesiredSize.Height)));
            }

            int availW = double.IsPositiveInfinity(availableSize.Width) ? 800 : (int)Math.Max(100.0, availableSize.Width);
            FlowLayoutEngine.CalculateLayout(
                itemSizes,
                availW,
                (int)Math.Round(HGap),
                (int)Math.Round(VGap),
                0,
                0,
                out int totalHeight);

            double returnW = double.IsPositiveInfinity(availableSize.Width) ? availW : availableSize.Width;
            return new Size(returnW, totalHeight);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            int count = InternalChildren.Count;
            if (count == 0) return finalSize;

            var itemSizes = new List<(int Width, int Height)>(count);
            for (int i = 0; i < count; i++)
            {
                UIElement child = InternalChildren[i];
                itemSizes.Add(((int)Math.Ceiling(child.DesiredSize.Width), (int)Math.Ceiling(child.DesiredSize.Height)));
            }

            var slots = FlowLayoutEngine.CalculateLayout(
                itemSizes,
                (int)Math.Max(100.0, finalSize.Width),
                (int)Math.Round(HGap),
                (int)Math.Round(VGap),
                0,
                0,
                out int totalHeight);

            _lastSlots.Clear();
            _lastSlots.AddRange(slots);

            for (int i = 0; i < count && i < slots.Count; i++)
            {
                var s = slots[i];
                InternalChildren[i].Arrange(new Rect(s.X, s.Y, s.Width, s.Height));
            }

            return new Size(finalSize.Width, Math.Max(finalSize.Height, totalHeight));
        }

        #region Drag-and-Drop Reordering

        protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnPreviewMouseLeftButtonDown(e);

            if (!AllowDragReordering) return;

            Point pos = e.GetPosition(this);
            UIElement? hit = HitTestChild(pos);
            if (hit != null)
            {
                _draggedElement = hit;
                _dragStartPoint = pos;
                _isDragging = false;
            }
        }

        protected override void OnPreviewMouseMove(MouseEventArgs e)
        {
            base.OnPreviewMouseMove(e);

            if (!AllowDragReordering || _draggedElement == null) return;

            Point current = e.GetPosition(this);
            double dx = current.X - _dragStartPoint.X;
            double dy = current.Y - _dragStartPoint.Y;

            if (!_isDragging && (Math.Abs(dx) >= DragThreshold || Math.Abs(dy) >= DragThreshold))
            {
                _isDragging = true;
                CaptureMouse();
            }

            if (_isDragging)
            {
                int targetSlot = FlowLayoutEngine.HitTestSlot((int)current.X, (int)current.Y, _lastSlots);
                int currentIdx = InternalChildren.IndexOf(_draggedElement);

                if (targetSlot >= 0 && targetSlot < InternalChildren.Count && targetSlot != currentIdx)
                {
                    InternalChildren.RemoveAt(currentIdx);
                    InternalChildren.Insert(targetSlot, _draggedElement);
                    InvalidateArrange();
                    OrderChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnPreviewMouseLeftButtonUp(e);

            if (_isDragging)
            {
                _isDragging = false;
                ReleaseMouseCapture();
            }
            _draggedElement = null;
        }

        private UIElement? HitTestChild(Point p)
        {
            for (int i = 0; i < InternalChildren.Count && i < _lastSlots.Count; i++)
            {
                if (_lastSlots[i].Contains((int)p.X, (int)p.Y))
                {
                    return InternalChildren[i];
                }
            }
            return null;
        }

        #endregion

        #region Serialization

        /// <summary>
        /// Exports the current order of child FrameworkElements by Name or Tag into a lightweight JSON array.
        /// Example: ["cardOee", "cardRpm", "cardThermal"].
        /// </summary>
        public string ExportLayoutJson()
        {
            var names = new List<string>(InternalChildren.Count);
            for (int i = 0; i < InternalChildren.Count; i++)
            {
                if (InternalChildren[i] is FrameworkElement fe)
                {
                    string name = !string.IsNullOrEmpty(fe.Name) ? fe.Name : fe.Tag?.ToString() ?? $"item_{i}";
                    names.Add($"\"{name}\"");
                }
            }
            return "[" + string.Join(",", names) + "]";
        }

        /// <summary>
        /// Re-orders child controls matching the provided JSON name array.
        /// </summary>
        public void ImportLayoutJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return;

            string trimmed = json.Trim().TrimStart('[').TrimEnd(']');
            if (string.IsNullOrWhiteSpace(trimmed)) return;

            string[] tokens = trimmed.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            int targetIdx = 0;

            for (int i = 0; i < tokens.Length; i++)
            {
                string name = tokens[i].Trim().Trim('\"', '\'');
                if (string.IsNullOrEmpty(name)) continue;

                for (int c = 0; c < InternalChildren.Count; c++)
                {
                    if (InternalChildren[c] is FrameworkElement fe)
                    {
                        string childName = !string.IsNullOrEmpty(fe.Name) ? fe.Name : fe.Tag?.ToString() ?? string.Empty;
                        if (string.Equals(childName, name, StringComparison.OrdinalIgnoreCase))
                        {
                            if (c != targetIdx && targetIdx < InternalChildren.Count)
                            {
                                UIElement elem = InternalChildren[c];
                                InternalChildren.RemoveAt(c);
                                InternalChildren.Insert(targetIdx, elem);
                            }
                            targetIdx++;
                            break;
                        }
                    }
                }
            }

            InvalidateMeasure();
            InvalidateArrange();
            OrderChanged?.Invoke(this, EventArgs.Empty);
        }

        #endregion
    }

    /// <summary>
    /// Backward-compatibility alias for <see cref="ZFlowLayout"/>.
    /// </summary>
    [Obsolete("FlowLayoutControl is deprecated and will be removed in 5 release cycles. Please migrate to ZFlowLayout instead.")]
    public class FlowLayoutControl : ZFlowLayout
    {
    }

    /// <summary>
    /// Legacy alias for <see cref="ZFlowLayout"/>.
    /// </summary>
    [Obsolete("ZeroFlowLayout is deprecated and will be removed in 5 release cycles. Please migrate to ZFlowLayout instead.")]
    public class ZeroFlowLayout : ZFlowLayout
    {
    }
}
