using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Data
{
    public enum TreeListDropPosition
    {
        None,
        Above,
        Below,
        AsChild
    }

    public class TreeListBeforeDragEventArgs : CancelEventArgs
    {
        public ZeroTreeNode Node { get; }
        public TreeListBeforeDragEventArgs(ZeroTreeNode node) => Node = node;
    }

    public class TreeListDragOverEventArgs : EventArgs
    {
        public ZeroTreeNode DragNode { get; }
        public ZeroTreeNode TargetNode { get; }
        public TreeListDropPosition Position { get; set; }
        public DragDropEffects Effect { get; set; } = DragDropEffects.Move;

        public TreeListDragOverEventArgs(ZeroTreeNode dragNode, ZeroTreeNode targetNode, TreeListDropPosition position)
        {
            DragNode = dragNode;
            TargetNode = targetNode;
            Position = position;
        }
    }

    public class TreeListAfterDropEventArgs : EventArgs
    {
        public ZeroTreeNode DragNode { get; }
        public ZeroTreeNode TargetNode { get; }
        public TreeListDropPosition Position { get; }

        public TreeListAfterDropEventArgs(ZeroTreeNode dragNode, ZeroTreeNode targetNode, TreeListDropPosition position)
        {
            DragNode = dragNode;
            TargetNode = targetNode;
            Position = position;
        }
    }

    public partial class ZTreeList
    {
        [Category("Behavior")]
        [DefaultValue(false)]
        [Description("Enables node drag-and-drop hierarchy re-parenting and sibling reordering.")]
        public bool AllowDragDrop
        {
            get => _allowDragDrop;
            set
            {
                if (_allowDragDrop != value)
                {
                    _allowDragDrop = value;
                    AllowDrop = value;
                }
            }
        }

        protected override void OnDragOver(DragEventArgs drgevent)
        {
            base.OnDragOver(drgevent);
            if (!_allowDragDrop)
            {
                drgevent.Effect = DragDropEffects.None;
                return;
            }

            if (drgevent.Data == null || !drgevent.Data.GetDataPresent(typeof(ZeroTreeNode)))
            {
                drgevent.Effect = DragDropEffects.None;
                return;
            }

            var dragNode = drgevent.Data.GetData(typeof(ZeroTreeNode)) as ZeroTreeNode;
            if (dragNode == null)
            {
                drgevent.Effect = DragDropEffects.None;
                return;
            }

            Point clientPt = PointToClient(new Point(drgevent.X, drgevent.Y));

            int headerOffset = (_showColumnHeaders && _columns.Count > 0) ? _headerHeight : 0;
            int index = ((clientPt.Y - headerOffset) / _rowHeight) + _scrollOffset;

            if (index >= 0 && index < _visibleNodes.Count)
            {
                var targetNode = _visibleNodes[index];
                int rowY = headerOffset + (index - _scrollOffset) * _rowHeight;
                int offsetY = clientPt.Y - rowY;

                TreeListDropPosition pos;
                if (offsetY < _rowHeight * 0.25) pos = TreeListDropPosition.Above;
                else if (offsetY > _rowHeight * 0.75) pos = TreeListDropPosition.Below;
                else pos = TreeListDropPosition.AsChild;

                if (CanDropNode(dragNode, targetNode, pos))
                {
                    var overArgs = new TreeListDragOverEventArgs(dragNode, targetNode, pos);
                    DragOverNode?.Invoke(this, overArgs);
                    drgevent.Effect = overArgs.Effect;
                    _dropTargetNode = targetNode;
                    _dropPosition = overArgs.Position;
                    Invalidate();
                    return;
                }
            }

            drgevent.Effect = DragDropEffects.None;
            if (_dropTargetNode != null)
            {
                _dropTargetNode = null;
                _dropPosition = TreeListDropPosition.None;
                Invalidate();
            }
        }

        protected override void OnDragLeave(EventArgs e)
        {
            base.OnDragLeave(e);
            if (_dropTargetNode != null)
            {
                _dropTargetNode = null;
                _dropPosition = TreeListDropPosition.None;
                Invalidate();
            }
        }

        protected override void OnDragDrop(DragEventArgs drgevent)
        {
            base.OnDragDrop(drgevent);
            if (!_allowDragDrop || drgevent.Data == null || !drgevent.Data.GetDataPresent(typeof(ZeroTreeNode)))
            {
                return;
            }

            var dragNode = drgevent.Data.GetData(typeof(ZeroTreeNode)) as ZeroTreeNode;
            var targetNode = _dropTargetNode;
            var pos = _dropPosition;

            _dropTargetNode = null;
            _dropPosition = TreeListDropPosition.None;

            if (dragNode != null && targetNode != null && pos != TreeListDropPosition.None && CanDropNode(dragNode, targetNode, pos))
            {
                ExecuteDrop(dragNode, targetNode, pos);
            }
            Invalidate();
        }

        private bool CanDropNode(ZeroTreeNode dragNode, ZeroTreeNode targetNode, TreeListDropPosition pos)
        {
            if (dragNode == null || targetNode == null) return false;
            if (dragNode == targetNode) return false;

            // Cannot drop a parent onto any of its descendants
            if (IsDescendantOf(targetNode, dragNode)) return false;

            return true;
        }

        private void ExecuteDrop(ZeroTreeNode dragNode, ZeroTreeNode targetNode, TreeListDropPosition pos)
        {
            // 1. Remove from old parent
            var oldParent = dragNode.Parent;
            if (oldParent != null)
            {
                oldParent.Children.Remove(dragNode);
                oldParent.UpdateParentCheckState();
            }
            else
            {
                _nodes.Remove(dragNode);
            }

            // 2. Insert into new location
            if (pos == TreeListDropPosition.AsChild)
            {
                targetNode.AddChild(dragNode);
                targetNode.IsExpanded = true;
            }
            else
            {
                var newParent = targetNode.Parent;
                dragNode.Parent = newParent;

                if (newParent != null)
                {
                    int targetIdx = newParent.Children.IndexOf(targetNode);
                    int insertIdx = pos == TreeListDropPosition.Above ? targetIdx : targetIdx + 1;
                    insertIdx = Math.Max(0, Math.Min(newParent.Children.Count, insertIdx));
                    newParent.Children.Insert(insertIdx, dragNode);
                    newParent.UpdateParentCheckState();
                }
                else
                {
                    int targetIdx = _nodes.IndexOf(targetNode);
                    int insertIdx = pos == TreeListDropPosition.Above ? targetIdx : targetIdx + 1;
                    insertIdx = Math.Max(0, Math.Min(_nodes.Count, insertIdx));
                    _nodes.Insert(insertIdx, dragNode);
                }
            }

            RecalculateRollups();
            if (_showFooter) RecalculateSummaries();

            UpdateVisibleNodes();
            AfterDropNode?.Invoke(this, new TreeListAfterDropEventArgs(dragNode, targetNode, pos));
        }

        internal void DrawDragDropIndicator(Graphics g, ZeroThemePalette palette, int clientWidth)
        {
            if (_dropTargetNode == null || _dropPosition == TreeListDropPosition.None) return;

            int visualIdx = _visibleNodes.IndexOf(_dropTargetNode);
            if (visualIdx < 0) return;

            int headerOffset = (_showColumnHeaders && _columns.Count > 0) ? _headerHeight : 0;
            int y = headerOffset + (visualIdx - _scrollOffset) * _rowHeight;

            using var penAccent = new Pen(palette.Primary, 2.5f);
            using var brushAccent = new SolidBrush(palette.Primary);

            if (_dropPosition == TreeListDropPosition.Above)
            {
                g.DrawLine(penAccent, 0, y, clientWidth, y);
                g.FillPolygon(brushAccent, new[] { new PointF(0, y - 4), new PointF(8, y), new PointF(0, y + 4) });
            }
            else if (_dropPosition == TreeListDropPosition.Below)
            {
                int lineY = y + _rowHeight - 1;
                g.DrawLine(penAccent, 0, lineY, clientWidth, lineY);
                g.FillPolygon(brushAccent, new[] { new PointF(0, lineY - 4), new PointF(8, lineY), new PointF(0, lineY + 4) });
            }
            else if (_dropPosition == TreeListDropPosition.AsChild)
            {
                using var boxPen = new Pen(palette.Primary, 1.8f) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash };
                using var boxBrush = new SolidBrush(Color.FromArgb(25, palette.Primary));
                var boxRect = new Rectangle(1, y + 1, clientWidth - 2, _rowHeight - 2);
                g.FillRectangle(boxBrush, boxRect);
                g.DrawRectangle(boxPen, boxRect);
            }
        }
    }
}
