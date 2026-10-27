using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ZeroUI.Core.Icons;
using ZeroUI.Core.Process;
using ZeroUI.Wpf.Theme;
using ZeroUI.Wpf.Editors;

namespace ZeroUI.Wpf.Process
{
    /// <summary>
    /// High-performance interactive business process map and workflow navigation control for WPF.
    /// Supports multi-lane workflow visualization, card tasks, decision diamonds,
    /// orthogonal transition arrows with branching labels, UserControl action triggers,
    /// and interactive design-mode customization.
    /// </summary>
    public class ZProcessMap : FrameworkElement
    {
        private ProcessFlowDefinition _definition = new ProcessFlowDefinition();
        private bool _isDesignMode = false;
        private bool _showGrid = true;
        private bool _autoExecuteAction = true;
        private bool _wheelZoomRequiresCtrl = true;
        private bool _enableWheelPan = true;

        private double _zoom = 1.0;
        private Point _panOffset = new Point(0, 0);

        private bool _isPanning = false;
        private Point _panStartMouse;
        private Point _panStartOffset;

        private ProcessFlowNode? _selectedNode;
        private ProcessFlowNode? _hoveredNode;
        private bool _isDraggingNode = false;
        private Point _nodeDragStartMouse;
        private Point _nodeDragStartPos;
        private Point _rightClickLocation;

        private ProcessFlowLane? _selectedLane;
        private ProcessFlowLane? _hoveredLane;
        private enum LaneResizeHandle
        {
            None,
            HeaderMove,
            TopLeft,
            Top,
            TopRight,
            Right,
            BottomRight,
            Bottom,
            BottomLeft,
            Left
        }
        private LaneResizeHandle _activeLaneHandle = LaneResizeHandle.None;
        private LaneResizeHandle _hoveredLaneHandle = LaneResizeHandle.None;
        private Point _laneResizeStartMouse;
        private Rect _laneInitialBounds;
        private Dictionary<string, Point> _laneNodeInitialPositions = new Dictionary<string, Point>();

        private ContextMenu _contextMenu = null!;

        public event EventHandler<ProcessFlowNode>? NodeClicked;
        public event EventHandler<ProcessFlowLane>? LaneClicked;
        public event EventHandler<ProcessActionContext>? ActionTriggered;
        public event EventHandler? DefinitionChanged;

        public ZProcessMap()
        {
            ClipToBounds = true;
            Focusable = true;
            Cursor = Cursors.Arrow;

            InitContextMenu();

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        #region Public Properties

        public ProcessFlowDefinition Definition
        {
            get => _definition;
            set
            {
                _definition = value ?? new ProcessFlowDefinition();
                _selectedNode = null;
                _hoveredNode = null;
                InvalidateVisual();
                DefinitionChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public bool IsDesignMode
        {
            get => _isDesignMode;
            set
            {
                _isDesignMode = value;
                Cursor = Cursors.Arrow;
                InvalidateVisual();
            }
        }

        public bool ShowGrid
        {
            get => _showGrid;
            set
            {
                _showGrid = value;
                InvalidateVisual();
            }
        }

        public bool AutoExecuteAction
        {
            get => _autoExecuteAction;
            set => _autoExecuteAction = value;
        }

        public bool WheelZoomRequiresCtrl
        {
            get => _wheelZoomRequiresCtrl;
            set => _wheelZoomRequiresCtrl = value;
        }

        public bool EnableWheelPan
        {
            get => _enableWheelPan;
            set => _enableWheelPan = value;
        }

        public double ZoomFactor
        {
            get => _zoom;
            set
            {
                _zoom = Math.Max(0.2, Math.Min(3.0, value));
                InvalidateVisual();
            }
        }

        public Point PanOffset
        {
            get => _panOffset;
            set
            {
                _panOffset = value;
                InvalidateVisual();
            }
        }

        public ProcessFlowNode? SelectedNode
        {
            get => _selectedNode;
            set
            {
                if (_selectedNode != value)
                {
                    _selectedNode = value;
                    if (_selectedNode != null) _selectedLane = null;
                    InvalidateVisual();
                }
            }
        }

        public ProcessFlowLane? SelectedLane
        {
            get => _selectedLane;
            set
            {
                if (_selectedLane != value)
                {
                    _selectedLane = value;
                    if (_selectedLane != null) _selectedNode = null;
                    InvalidateVisual();
                    if (_selectedLane != null) LaneClicked?.Invoke(this, _selectedLane);
                }
            }
        }

        public void CreateLaneFromSelectedNodes(string title = "1. SALES & R&D ENGINEERING")
        {
            var targets = new List<ProcessFlowNode>();
            if (_selectedNode != null) targets.Add(_selectedNode);
            var lane = _definition.CreateLaneFromSelection(targets, title);
            SelectedLane = lane;
            SelectedNode = null;
            InvalidateVisual();
            DefinitionChanged?.Invoke(this, EventArgs.Empty);
        }

        public void AutoArrangeLayout(bool horizontal = true)
        {
            _definition.AutoArrangeLayout(horizontal);
            InvalidateVisual();
            DefinitionChanged?.Invoke(this, EventArgs.Empty);
        }

        public void FitLanesToNodes()
        {
            _definition.FitLanesToNodes();
            InvalidateVisual();
            DefinitionChanged?.Invoke(this, EventArgs.Empty);
        }

        public ProcessFlowLane? HitTestLane(Point worldPt)
        {
            for (int i = _definition.Lanes.Count - 1; i >= 0; i--)
            {
                var lane = _definition.Lanes[i];
                if (worldPt.X >= lane.X && worldPt.X <= lane.X + lane.Width &&
                    worldPt.Y >= lane.Y && worldPt.Y <= lane.Y + lane.Height)
                {
                    return lane;
                }
            }
            return null;
        }

        private LaneResizeHandle HitTestLaneHandle(ProcessFlowLane lane, Point worldPt)
        {
            double hs = 10 / _zoom;
            double x = lane.X;
            double y = lane.Y;
            double w = lane.Width;
            double h = lane.Height;

            if (new Rect(x - hs, y - hs, hs * 2, hs * 2).Contains(worldPt)) return LaneResizeHandle.TopLeft;
            if (new Rect(x + w - hs, y - hs, hs * 2, hs * 2).Contains(worldPt)) return LaneResizeHandle.TopRight;
            if (new Rect(x + w - hs, y + h - hs, hs * 2, hs * 2).Contains(worldPt)) return LaneResizeHandle.BottomRight;
            if (new Rect(x - hs, y + h - hs, hs * 2, hs * 2).Contains(worldPt)) return LaneResizeHandle.BottomLeft;

            if (new Rect(x + w / 2 - hs, y - hs, hs * 2, hs * 2).Contains(worldPt)) return LaneResizeHandle.Top;
            if (new Rect(x + w / 2 - hs, y + h - hs, hs * 2, hs * 2).Contains(worldPt)) return LaneResizeHandle.Bottom;
            if (new Rect(x - hs, y + h / 2 - hs, hs * 2, hs * 2).Contains(worldPt)) return LaneResizeHandle.Left;
            if (new Rect(x + w - hs, y + h / 2 - hs, hs * 2, hs * 2).Contains(worldPt)) return LaneResizeHandle.Right;

            if (worldPt.X >= x && worldPt.X <= x + w && worldPt.Y >= y && worldPt.Y <= y + 34)
            {
                return LaneResizeHandle.HeaderMove;
            }

            return LaneResizeHandle.None;
        }

        private void ApplyLaneResize(double dx, double dy)
        {
            if (_selectedLane == null) return;
            const double minW = 160;
            const double minH = 100;

            double x = _laneInitialBounds.X;
            double y = _laneInitialBounds.Y;
            double w = _laneInitialBounds.Width;
            double h = _laneInitialBounds.Height;

            switch (_activeLaneHandle)
            {
                case LaneResizeHandle.HeaderMove:
                    _selectedLane.X = Math.Max(0, x + dx);
                    _selectedLane.Y = Math.Max(0, y + dy);
                    foreach (var kvp in _laneNodeInitialPositions)
                    {
                        var n = _definition.Nodes.FirstOrDefault(node => node.Id == kvp.Key);
                        if (n != null)
                        {
                            n.X = Math.Max(0, kvp.Value.X + dx);
                            n.Y = Math.Max(0, kvp.Value.Y + dy);
                        }
                    }
                    break;

                case LaneResizeHandle.Right:
                    _selectedLane.Width = Math.Max(minW, w + dx);
                    break;

                case LaneResizeHandle.Bottom:
                    _selectedLane.Height = Math.Max(minH, h + dy);
                    break;

                case LaneResizeHandle.BottomRight:
                    _selectedLane.Width = Math.Max(minW, w + dx);
                    _selectedLane.Height = Math.Max(minH, h + dy);
                    break;

                case LaneResizeHandle.Left:
                    double newWLeft = w - dx;
                    if (newWLeft >= minW)
                    {
                        _selectedLane.X = x + dx;
                        _selectedLane.Width = newWLeft;
                    }
                    else
                    {
                        _selectedLane.X = x + (w - minW);
                        _selectedLane.Width = minW;
                    }
                    break;

                case LaneResizeHandle.Top:
                    double newHTop = h - dy;
                    if (newHTop >= minH)
                    {
                        _selectedLane.Y = y + dy;
                        _selectedLane.Height = newHTop;
                    }
                    else
                    {
                        _selectedLane.Y = y + (h - minH);
                        _selectedLane.Height = minH;
                    }
                    break;

                case LaneResizeHandle.TopLeft:
                    double newWTL = w - dx;
                    if (newWTL >= minW)
                    {
                        _selectedLane.X = x + dx;
                        _selectedLane.Width = newWTL;
                    }
                    else
                    {
                        _selectedLane.X = x + (w - minW);
                        _selectedLane.Width = minW;
                    }

                    double newHTL = h - dy;
                    if (newHTL >= minH)
                    {
                        _selectedLane.Y = y + dy;
                        _selectedLane.Height = newHTL;
                    }
                    else
                    {
                        _selectedLane.Y = y + (h - minH);
                        _selectedLane.Height = minH;
                    }
                    break;

                case LaneResizeHandle.TopRight:
                    _selectedLane.Width = Math.Max(minW, w + dx);
                    double newHTR = h - dy;
                    if (newHTR >= minH)
                    {
                        _selectedLane.Y = y + dy;
                        _selectedLane.Height = newHTR;
                    }
                    else
                    {
                        _selectedLane.Y = y + (h - minH);
                        _selectedLane.Height = minH;
                    }
                    break;

                case LaneResizeHandle.BottomLeft:
                    double newWBL = w - dx;
                    if (newWBL >= minW)
                    {
                        _selectedLane.X = x + dx;
                        _selectedLane.Width = newWBL;
                    }
                    else
                    {
                        _selectedLane.X = x + (w - minW);
                        _selectedLane.Width = minW;
                    }
                    _selectedLane.Height = Math.Max(minH, h + dy);
                    break;
            }
        }

        #endregion

        #region Coordinate Transforms

        public Point ScreenToWorld(Point screen)
        {
            return new Point((screen.X - _panOffset.X) / _zoom, (screen.Y - _panOffset.Y) / _zoom);
        }

        public Point WorldToScreen(Point world)
        {
            return new Point(world.X * _zoom + _panOffset.X, world.Y * _zoom + _panOffset.Y);
        }

        #endregion

        #region Context Menu

        private void InitContextMenu()
        {
            _contextMenu = new ContextMenu();
            _contextMenu.Opened += OnContextMenuOpened;
        }

        private void OnContextMenuOpened(object sender, RoutedEventArgs e)
        {
            _contextMenu.Items.Clear();

            if (_selectedLane != null)
            {
                var lane = _selectedLane;
                var mnuRename = new MenuItem { Header = MenuIcons.Format(MenuIcons.Rename, "Configure Swimlane...") };
                mnuRename.Click += (s, ev) => ShowRenameLaneDialog(lane);
                _contextMenu.Items.Add(mnuRename);

                var mnuColor = new MenuItem { Header = MenuIcons.Format(MenuIcons.Palette, "Change Swimlane Color") };
                foreach (var preset in ProcessFlowLane.DefaultPresets)
                {
                    var p = preset;
                    var itm = new MenuItem { Header = p.Name };
                    itm.Click += (s, ev) =>
                    {
                        lane.HeaderColorHex = p.HeaderHex;
                        lane.BackgroundColorHex = p.BgHex;
                        InvalidateVisual();
                        DefinitionChanged?.Invoke(this, EventArgs.Empty);
                    };
                    mnuColor.Items.Add(itm);
                }

                mnuColor.Items.Add(new Separator());

                var mnuCustomHeader = new MenuItem { Header = "Custom Header / Border Color..." };
                mnuCustomHeader.Click += (s, ev) =>
                {
                    var chosen = ColorPickEdit.PickColor(Window.GetWindow(this), ParseColor(lane.HeaderColorHex, Colors.Gray), "Choose Swimlane Header Color");
                    if (chosen.HasValue)
                    {
                        lane.HeaderColorHex = $"#{chosen.Value.R:X2}{chosen.Value.G:X2}{chosen.Value.B:X2}";
                        InvalidateVisual();
                        DefinitionChanged?.Invoke(this, EventArgs.Empty);
                    }
                };
                mnuColor.Items.Add(mnuCustomHeader);

                var mnuCustomBg = new MenuItem { Header = "Custom Background Color..." };
                mnuCustomBg.Click += (s, ev) =>
                {
                    var chosen = ColorPickEdit.PickColor(Window.GetWindow(this), ParseColor(lane.BackgroundColorHex, Color.FromRgb(248, 250, 252)), "Choose Swimlane Background Color");
                    if (chosen.HasValue)
                    {
                        lane.BackgroundColorHex = $"#{chosen.Value.R:X2}{chosen.Value.G:X2}{chosen.Value.B:X2}";
                        InvalidateVisual();
                        DefinitionChanged?.Invoke(this, EventArgs.Empty);
                    }
                };
                mnuColor.Items.Add(mnuCustomBg);

                _contextMenu.Items.Add(mnuColor);

                var mnuFit = new MenuItem { Header = MenuIcons.Format(MenuIcons.FitToContent, "Fit Lanes to Nodes") };
                mnuFit.Click += (s, ev) => FitLanesToNodes();
                _contextMenu.Items.Add(mnuFit);

                var mnuAuto = new MenuItem { Header = MenuIcons.Format(MenuIcons.AutoLayout, "Auto-Arrange Flow") };
                mnuAuto.Click += (s, ev) => AutoArrangeLayout(true);
                _contextMenu.Items.Add(mnuAuto);

                _contextMenu.Items.Add(new Separator());

                var mnuDeleteLane = new MenuItem { Header = MenuIcons.Format(MenuIcons.Delete, "Delete Swimlane") };
                mnuDeleteLane.Click += (s, ev) =>
                {
                    if (_selectedLane != null)
                    {
                        foreach (var n in _definition.Nodes.Where(n => n.LaneId == _selectedLane.Id))
                        {
                            n.LaneId = null;
                        }
                        _definition.Lanes.Remove(_selectedLane);
                        _selectedLane = null;
                        InvalidateVisual();
                        DefinitionChanged?.Invoke(this, EventArgs.Empty);
                    }
                };
                _contextMenu.Items.Add(mnuDeleteLane);
                return;
            }

            if (_selectedNode == null)
            {
                var mnuAdd = new MenuItem { Header = MenuIcons.Format(MenuIcons.Add, "Add New Step Here") };
                mnuAdd.Click += (s, ev) =>
                {
                    int nextIdx = _definition.Nodes.Count + 1;
                    var newNode = new ProcessFlowNode(
                        Guid.NewGuid().ToString("N"),
                        $"Step {nextIdx}",
                        "Process description",
                        _rightClickLocation.X,
                        _rightClickLocation.Y,
                        220,
                        75
                    )
                    {
                        Shape = ProcessNodeShape.TaskCard,
                        HeaderColorHex = "#3B82F6",
                        IconGlyph = MenuIcons.Document
                    };
                    _definition.Nodes.Add(newNode);
                    SelectedNode = newNode;
                    InvalidateVisual();
                    DefinitionChanged?.Invoke(this, EventArgs.Empty);
                };
                _contextMenu.Items.Add(mnuAdd);

                var mnuAddLane = new MenuItem { Header = MenuIcons.Format(MenuIcons.Add, "Add New Swimlane...") };
                mnuAddLane.Click += (s, ev) =>
                {
                    var newLane = new ProcessFlowLane("lane_" + Guid.NewGuid().ToString("N").Substring(0, 8), "1. BUSINESS & DESIGN", _rightClickLocation.X, _rightClickLocation.Y, 680, 340);
                    _definition.Lanes.Add(newLane);
                    SelectedLane = newLane;
                    SelectedNode = null;
                    InvalidateVisual();
                    DefinitionChanged?.Invoke(this, EventArgs.Empty);
                };
                _contextMenu.Items.Add(mnuAddLane);

                _contextMenu.Items.Add(new Separator());

                var mnuAutoCanvas = new MenuItem { Header = MenuIcons.Format(MenuIcons.AutoLayout, "Auto-Arrange Flow") };
                mnuAutoCanvas.Click += (s, ev) => AutoArrangeLayout(true);
                _contextMenu.Items.Add(mnuAutoCanvas);

                var mnuFitCanvas = new MenuItem { Header = MenuIcons.Format(MenuIcons.FitToContent, "Fit Lanes to Nodes") };
                mnuFitCanvas.Click += (s, ev) => FitLanesToNodes();
                _contextMenu.Items.Add(mnuFitCanvas);

                return;
            }

            // Node Context Menu
            var mnuCreateLane = new MenuItem { Header = MenuIcons.Format(MenuIcons.Frame, "Create Frame from Selection") };
            mnuCreateLane.Click += (s, ev) => CreateLaneFromSelectedNodes();
            _contextMenu.Items.Add(mnuCreateLane);
            _contextMenu.Items.Add(new Separator());

            // Connect to Step Submenu
            var mnuConnect = new MenuItem { Header = MenuIcons.Format(MenuIcons.Connect, "Connect to Step...") };
            var otherNodes = _definition.Nodes.Where(n => n.Id != _selectedNode.Id).ToList();
            if (otherNodes.Count == 0)
            {
                mnuConnect.Items.Add(new MenuItem { Header = "(No other steps)", IsEnabled = false });
            }
            else
            {
                foreach (var target in otherNodes)
                {
                    var tgt = target;
                    var item = new MenuItem { Header = $"{MenuIcons.ArrowRight} {tgt.Title}" };
                    item.Click += (s, ev) =>
                    {
                        if (_selectedNode != null)
                        {
                            var sp = ProcessPortPosition.Right;
                            var tp = ProcessPortPosition.Left;
                            if (_selectedNode.X > tgt.X)
                            {
                                sp = ProcessPortPosition.Left;
                                tp = ProcessPortPosition.Right;
                            }
                            else if (Math.Abs(_selectedNode.X - tgt.X) < 100)
                            {
                                sp = _selectedNode.Y < tgt.Y ? ProcessPortPosition.Bottom : ProcessPortPosition.Top;
                                tp = _selectedNode.Y < tgt.Y ? ProcessPortPosition.Top : ProcessPortPosition.Bottom;
                            }
                            AddConnection(_selectedNode.Id, tgt.Id, "", "#0EA5E9", sp, tp);
                        }
                    };
                    mnuConnect.Items.Add(item);
                }
            }
            _contextMenu.Items.Add(mnuConnect);

            // Node Context Menu
            var mnuEdit = new MenuItem { Header = "Edit Title & Subtitle..." };
            mnuEdit.Click += (s, ev) => ShowEditTitleDialog();
            _contextMenu.Items.Add(mnuEdit);

            // Assign Action Submenu
            var mnuAction = new MenuItem { Header = "Assign Navigation Action" };
            var mnuClearAction = new MenuItem { Header = "(None / Clear Action)" };
            mnuClearAction.Click += (s, ev) =>
            {
                if (_selectedNode != null)
                {
                    _selectedNode.ActionKey = string.Empty;
                    InvalidateVisual();
                }
            };
            mnuAction.Items.Add(mnuClearAction);
            mnuAction.Items.Add(new Separator());

            var actions = ProcessActionRegistry.GetAllActions();
            if (actions.Count == 0)
            {
                mnuAction.Items.Add(new MenuItem { Header = "(No registered actions)", IsEnabled = false });
            }
            else
            {
                var groups = actions.GroupBy(a => a.Category);
                foreach (var grp in groups)
                {
                    var catMenu = new MenuItem { Header = grp.Key };
                    foreach (var act in grp)
                    {
                        string actionKey = act.Key;
                        var actItem = new MenuItem
                        {
                            Header = act.Title,
                            IsChecked = (_selectedNode?.ActionKey == actionKey)
                        };
                        actItem.Click += (s, ev) =>
                        {
                            if (_selectedNode != null)
                            {
                                _selectedNode.ActionKey = actionKey;
                                InvalidateVisual();
                            }
                        };
                        catMenu.Items.Add(actItem);
                    }
                    mnuAction.Items.Add(catMenu);
                }
            }
            _contextMenu.Items.Add(mnuAction);

            // Change Shape Submenu
            var mnuShape = new MenuItem { Header = "Change Shape" };
            AddShapeOption(mnuShape, "Task Card", ProcessNodeShape.TaskCard);
            AddShapeOption(mnuShape, "Decision Diamond", ProcessNodeShape.DecisionDiamond);
            AddShapeOption(mnuShape, "Start Terminal", ProcessNodeShape.StartTerminal);
            AddShapeOption(mnuShape, "End Terminal", ProcessNodeShape.EndTerminal);
            _contextMenu.Items.Add(mnuShape);

            _contextMenu.Items.Add(new Separator());

            var mnuDelete = new MenuItem { Header = "Delete Step" };
            mnuDelete.Click += (s, ev) =>
            {
                if (_selectedNode != null)
                {
                    string id = _selectedNode.Id;
                    _definition.Nodes.Remove(_selectedNode);
                    _definition.Connections.RemoveAll(c => c.SourceNodeId == id || c.TargetNodeId == id);
                    _selectedNode = null;
                    InvalidateVisual();
                    DefinitionChanged?.Invoke(this, EventArgs.Empty);
                }
            };
            _contextMenu.Items.Add(mnuDelete);
        }

        private void AddShapeOption(MenuItem parent, string title, ProcessNodeShape shape)
        {
            var item = new MenuItem { Header = title };
            item.Click += (s, ev) =>
            {
                if (_selectedNode != null)
                {
                    _selectedNode.Shape = shape;
                    InvalidateVisual();
                    DefinitionChanged?.Invoke(this, EventArgs.Empty);
                }
            };
            parent.Items.Add(item);
        }

        private void ShowEditTitleDialog()
        {
            if (_selectedNode == null) return;

            var win = new Window
            {
                Title = "Configure Process Node",
                Width = 380,
                Height = 260,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.NoResize,
                Background = ZeroWpfTheme.BgCard
            };

            var stack = new StackPanel { Margin = new Thickness(16) };

            stack.Children.Add(new TextBlock { Text = "Title:", Foreground = ZeroWpfTheme.TextPrimary, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 4) });
            var txtTitle = new TextEdit { Text = _selectedNode.Title, Margin = new Thickness(0, 0, 0, 10), Padding = new Thickness(4), Height = 30 };
            stack.Children.Add(txtTitle);

            stack.Children.Add(new TextBlock { Text = "Subtitle / Description:", Foreground = ZeroWpfTheme.TextPrimary, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 4) });
            var txtSub = new TextEdit { Text = _selectedNode.Subtitle, Height = 60, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true, Margin = new Thickness(0, 0, 0, 10), Padding = new Thickness(4) };
            stack.Children.Add(txtSub);

            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 6, 0, 0) };
            var btnOk = new SimpleButton { Content = "OK", Variant = ButtonVariant.Primary, Width = 75, Height = 28, Margin = new Thickness(0, 0, 8, 0), IsDefault = true };
            var btnCancel = new SimpleButton { Content = "Cancel", Variant = ButtonVariant.Secondary, Width = 75, Height = 28, IsCancel = true };

            btnOk.Click += (s, e) =>
            {
                _selectedNode.Title = txtTitle.Text;
                _selectedNode.Subtitle = txtSub.Text;
                InvalidateVisual();
                DefinitionChanged?.Invoke(this, EventArgs.Empty);
                win.DialogResult = true;
                win.Close();
            };

            btnPanel.Children.Add(btnOk);
            btnPanel.Children.Add(btnCancel);
            stack.Children.Add(btnPanel);

            win.Content = stack;
            win.ShowDialog();
        }

        private void ShowRenameLaneDialog(ProcessFlowLane lane)
        {
            if (lane == null) return;

            var win = new Window
            {
                Title = "Configure Swimlane / Group Frame",
                Width = 460,
                Height = 390,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.NoResize,
                Background = ZeroWpfTheme.BgCard
            };

            string selectedHeaderHex = lane.HeaderColorHex;
            string selectedBgHex = lane.BackgroundColorHex;

            var stack = new StackPanel { Margin = new Thickness(16) };
            stack.Children.Add(new TextBlock { Text = "Lane Title:", Foreground = ZeroWpfTheme.TextPrimary, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 4) });
            var txtTitle = new TextEdit { Text = lane.Title, Margin = new Thickness(0, 0, 0, 12), Padding = new Thickness(4), Height = 30 };
            stack.Children.Add(txtTitle);

            var cpeHeader = new ColorPickEdit { SelectedColor = ParseColor(selectedHeaderHex, Colors.Gray), Height = 32 };
            var cpeBg = new ColorPickEdit { SelectedColor = ParseColor(selectedBgHex, Color.FromRgb(248, 250, 252)), Height = 32 };

            cpeHeader.ColorChanged += (s, c) =>
            {
                selectedHeaderHex = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
            };

            cpeBg.ColorChanged += (s, c) =>
            {
                selectedBgHex = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
            };

            stack.Children.Add(new TextBlock { Text = "Color Theme Presets:", Foreground = ZeroWpfTheme.TextPrimary, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 6) });
            var wrapPresets = new WrapPanel { Margin = new Thickness(0, 0, 0, 14) };

            Border? selectedBorder = null;

            foreach (var preset in ProcessFlowLane.DefaultPresets)
            {
                var p = preset;
                Color hColor = ParseColor(p.HeaderHex, Color.FromRgb(100, 116, 139));
                Color bColor = ParseColor(p.BgHex, Color.FromRgb(248, 250, 252));

                var border = new Border
                {
                    Width = 40,
                    Height = 28,
                    Margin = new Thickness(3),
                    CornerRadius = new CornerRadius(4),
                    BorderThickness = new Thickness(selectedHeaderHex == p.HeaderHex ? 2.5 : 1.2),
                    BorderBrush = new SolidColorBrush(selectedHeaderHex == p.HeaderHex ? Colors.Black : hColor),
                    Background = new SolidColorBrush(bColor),
                    Cursor = Cursors.Hand,
                    ToolTip = p.Name
                };

                var topBar = new Border
                {
                    Height = 8,
                    VerticalAlignment = VerticalAlignment.Top,
                    CornerRadius = new CornerRadius(3, 3, 0, 0),
                    Background = new SolidColorBrush(hColor)
                };
                border.Child = topBar;

                if (selectedHeaderHex == p.HeaderHex)
                {
                    selectedBorder = border;
                }

                border.MouseLeftButtonDown += (s, e) =>
                {
                    if (selectedBorder != null)
                    {
                        selectedBorder.BorderThickness = new Thickness(1.2);
                        selectedBorder.BorderBrush = new SolidColorBrush(ParseColor(selectedHeaderHex, Colors.Gray));
                    }
                    selectedHeaderHex = p.HeaderHex;
                    selectedBgHex = p.BgHex;
                    selectedBorder = border;
                    border.BorderThickness = new Thickness(2.5);
                    border.BorderBrush = new SolidColorBrush(Colors.Black);
                    cpeHeader.SelectedColor = ParseColor(p.HeaderHex, Colors.Gray);
                    cpeBg.SelectedColor = ParseColor(p.BgHex, Color.FromRgb(248, 250, 252));
                };

                wrapPresets.Children.Add(border);
            }
            stack.Children.Add(wrapPresets);

            var pickersPanel = new Grid { Margin = new Thickness(0, 0, 0, 14) };
            pickersPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            pickersPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16, GridUnitType.Pixel) });
            pickersPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var headerCol = new StackPanel();
            headerCol.Children.Add(new TextBlock { Text = "Header / Accent:", Foreground = ZeroWpfTheme.TextPrimary, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 4) });
            headerCol.Children.Add(cpeHeader);
            Grid.SetColumn(headerCol, 0);

            var bgCol = new StackPanel();
            bgCol.Children.Add(new TextBlock { Text = "Background Color:", Foreground = ZeroWpfTheme.TextPrimary, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 4) });
            bgCol.Children.Add(cpeBg);
            Grid.SetColumn(bgCol, 2);

            pickersPanel.Children.Add(headerCol);
            pickersPanel.Children.Add(bgCol);
            stack.Children.Add(pickersPanel);

            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 6, 0, 0) };
            var btnOk = new SimpleButton { Content = "Save", Variant = ButtonVariant.Primary, Width = 75, Height = 28, Margin = new Thickness(0, 0, 8, 0), IsDefault = true };
            var btnCancel = new SimpleButton { Content = "Cancel", Variant = ButtonVariant.Secondary, Width = 75, Height = 28, IsCancel = true };

            btnOk.Click += (s, e) =>
            {
                lane.Title = txtTitle.Text.Trim();
                lane.HeaderColorHex = selectedHeaderHex;
                lane.BackgroundColorHex = selectedBgHex;
                InvalidateVisual();
                DefinitionChanged?.Invoke(this, EventArgs.Empty);
                win.DialogResult = true;
                win.Close();
            };

            btnPanel.Children.Add(btnOk);
            btnPanel.Children.Add(btnCancel);
            stack.Children.Add(btnPanel);

            win.Content = stack;
            win.ShowDialog();
        }

        #endregion

        #region Rendering Pipeline

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            double w = ActualWidth;
            double h = ActualHeight;
            if (w <= 0 || h <= 0) return;

            // 1. Background
            dc.DrawRectangle(ZeroWpfTheme.BgPrimary, null, new Rect(0, 0, w, h));

            // 2. Background Grid
            if (_showGrid)
            {
                DrawGrid(dc, w, h);
            }

            // 3. Transform Matrix for Pan & Zoom
            dc.PushTransform(new MatrixTransform(_zoom, 0, 0, _zoom, _panOffset.X, _panOffset.Y));

            // 4. Swimlanes
            foreach (var lane in _definition.Lanes)
            {
                DrawLane(dc, lane);
            }

            // 5. Connections (Orthogonal Lines & Arrowheads)
            foreach (var conn in _definition.Connections)
            {
                DrawConnection(dc, conn);
            }

            // 6. Nodes
            foreach (var node in _definition.Nodes)
            {
                DrawNode(dc, node, node == _selectedNode, node == _hoveredNode);
            }

            dc.Pop(); // Restore Transform

            // 7. HUD Overlays
            DrawHudOverlay(dc, w, h);
        }

        private void DrawGrid(DrawingContext dc, double w, double h)
        {
            double step = 25 * _zoom;
            if (step < 8) return;

            double startX = _panOffset.X % step;
            if (startX < 0) startX += step;
            double startY = _panOffset.Y % step;
            if (startY < 0) startY += step;

            var dotBrush = new SolidColorBrush(Color.FromArgb(28, 148, 163, 184));
            dotBrush.Freeze();

            for (double x = startX; x < w; x += step)
            {
                for (double y = startY; y < h; y += step)
                {
                    dc.DrawRectangle(dotBrush, null, new Rect(x - 1, y - 1, 2, 2));
                }
            }
        }

        private void DrawLane(DrawingContext dc, ProcessFlowLane lane)
        {
            var rect = new Rect(lane.X, lane.Y, lane.Width, lane.Height);
            Color bg = ParseColor(lane.BackgroundColorHex, Color.FromArgb(245, 248, 250, 252));
            bool isSelected = (lane == _selectedLane);
            Color border = isSelected ? Color.FromRgb(14, 165, 233) : Color.FromArgb(40, 148, 163, 184);

            var bgBrush = new SolidColorBrush(bg);
            bgBrush.Freeze();
            var borderPen = new Pen(new SolidColorBrush(border), isSelected ? 2.2 : 1.5)
            {
                DashStyle = isSelected ? DashStyles.Dash : DashStyles.Solid
            };
            borderPen.Freeze();

            dc.DrawRoundedRectangle(bgBrush, borderPen, rect, 12, 12);

            // Lane Header Bar
            var headerRect = new Rect(rect.X, rect.Y, rect.Width, 34);
            Color headerBg = ParseColor(lane.HeaderColorHex, Color.FromRgb(14, 165, 233));
            var headerBrush = new SolidColorBrush(Color.FromArgb(24, headerBg.R, headerBg.G, headerBg.B));
            headerBrush.Freeze();
            dc.DrawRoundedRectangle(headerBrush, null, headerRect, 12, 12);

            // Lane Header Title
            string title = "⚙ " + lane.Title.ToUpperInvariant();
            Color headerColor = ParseColor(lane.HeaderColorHex, Color.FromRgb(100, 116, 139));
            var font = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
            var headerText = new FormattedText(
                title,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                font,
                11,
                new SolidColorBrush(headerColor),
                VisualTreeHelper.GetDpi(this).PixelsPerDip
            );

            dc.DrawText(headerText, new Point(rect.X + 16, rect.Y + 9));

            if (_isDesignMode && isSelected)
            {
                DrawLaneHandles(dc, rect);
            }
        }

        private void DrawLaneHandles(DrawingContext dc, Rect b)
        {
            double r = 5.0 / _zoom;
            Point[] points = new Point[]
            {
                new Point(b.Left, b.Top),
                new Point(b.Left + b.Width / 2, b.Top),
                new Point(b.Right, b.Top),
                new Point(b.Right, b.Top + b.Height / 2),
                new Point(b.Right, b.Bottom),
                new Point(b.Left + b.Width / 2, b.Bottom),
                new Point(b.Left, b.Bottom),
                new Point(b.Left, b.Top + b.Height / 2)
            };

            var fill = Brushes.White;
            var pen = new Pen(new SolidColorBrush(Color.FromRgb(14, 165, 233)), 1.8 / _zoom);
            pen.Freeze();

            foreach (var pt in points)
            {
                dc.DrawRectangle(fill, pen, new Rect(pt.X - r, pt.Y - r, r * 2, r * 2));
            }
        }

        private void DrawConnection(DrawingContext dc, ProcessFlowConnection conn)
        {
            var srcNode = _definition.Nodes.FirstOrDefault(n => n.Id == conn.SourceNodeId);
            var tgtNode = _definition.Nodes.FirstOrDefault(n => n.Id == conn.TargetNodeId);
            if (srcNode == null || tgtNode == null) return;

            Point p1 = GetPortLocation(srcNode, conn.SourcePort);
            Point p2 = GetPortLocation(tgtNode, conn.TargetPort);

            Color stroke = ParseColor(conn.StrokeColorHex, Color.FromRgb(14, 165, 233));
            var brush = new SolidColorBrush(stroke);
            brush.Freeze();
            var pen = new Pen(brush, conn.StrokeThickness);
            if (conn.IsDashed) pen.DashStyle = DashStyles.Dash;
            pen.Freeze();

            var points = CalculateOrthogonalRoute(p1, p2, conn.SourcePort, conn.TargetPort);
            if (points.Count >= 2)
            {
                var geometry = new StreamGeometry();
                using (var ctx = geometry.Open())
                {
                    ctx.BeginFigure(points[0], false, false);
                    for (int i = 1; i < points.Count; i++)
                    {
                        ctx.LineTo(points[i], true, false);
                    }
                }
                geometry.Freeze();
                dc.DrawGeometry(null, pen, geometry);

                // Arrowhead
                DrawArrowhead(dc, brush, points[points.Count - 2], points[points.Count - 1], conn.StrokeThickness);

                // Label
                if (!string.IsNullOrWhiteSpace(conn.Label))
                {
                    DrawConnectionLabel(dc, conn.Label, points, stroke);
                }
            }
        }

        private List<Point> CalculateOrthogonalRoute(Point p1, Point p2, ProcessPortPosition sp, ProcessPortPosition tp)
        {
            var list = new List<Point> { p1 };

            if (Math.Abs(p1.X - p2.X) < 4 && p2.Y > p1.Y)
            {
                list.Add(p2);
                return list;
            }

            if (sp == ProcessPortPosition.Bottom && tp == ProcessPortPosition.Top)
            {
                double midY = (p1.Y + p2.Y) / 2;
                list.Add(new Point(p1.X, midY));
                list.Add(new Point(p2.X, midY));
            }
            else if (sp == ProcessPortPosition.Right && tp == ProcessPortPosition.Top)
            {
                list.Add(new Point(p2.X, p1.Y));
            }
            else if (sp == ProcessPortPosition.Left && tp == ProcessPortPosition.Top)
            {
                list.Add(new Point(p2.X, p1.Y));
            }
            else if (sp == ProcessPortPosition.Left && tp == ProcessPortPosition.Right)
            {
                double midX = (p1.X + p2.X) / 2;
                list.Add(new Point(midX, p1.Y));
                list.Add(new Point(midX, p2.Y));
            }
            else
            {
                double midX = (p1.X + p2.X) / 2;
                list.Add(new Point(midX, p1.Y));
                list.Add(new Point(midX, p2.Y));
            }

            list.Add(p2);
            return list;
        }

        private void DrawArrowhead(DrawingContext dc, Brush brush, Point from, Point to, double thickness)
        {
            double arrowSize = 6.0 + thickness;
            double dx = to.X - from.X;
            double dy = to.Y - from.Y;
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 0.001) return;

            double uX = dx / len;
            double uY = dy / len;
            double vX = -uY;
            double vY = uX;

            Point pLeft = new Point(to.X - uX * arrowSize + vX * (arrowSize * 0.5), to.Y - uY * arrowSize + vY * (arrowSize * 0.5));
            Point pRight = new Point(to.X - uX * arrowSize - vX * (arrowSize * 0.5), to.Y - uY * arrowSize - vY * (arrowSize * 0.5));

            var geom = new StreamGeometry();
            using (var ctx = geom.Open())
            {
                ctx.BeginFigure(to, true, true);
                ctx.LineTo(pLeft, true, false);
                ctx.LineTo(pRight, true, false);
            }
            geom.Freeze();
            dc.DrawGeometry(brush, null, geom);
        }

        private void DrawConnectionLabel(DrawingContext dc, string label, List<Point> points, Color color)
        {
            int midIdx = points.Count / 2;
            Point a = points[midIdx - 1];
            Point b = points[midIdx];
            Point center = new Point((a.X + b.X) / 2, (a.Y + b.Y) / 2);

            var typeface = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
            var text = new FormattedText(
                label,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                typeface,
                10,
                new SolidColorBrush(color),
                VisualTreeHelper.GetDpi(this).PixelsPerDip
            );

            var rect = new Rect(center.X - text.Width / 2 - 6, center.Y - text.Height / 2 - 3, text.Width + 12, text.Height + 6);
            var bgBrush = new SolidColorBrush(Color.FromArgb(245, 255, 255, 255));
            bgBrush.Freeze();
            var pen = new Pen(new SolidColorBrush(color), 1.2);
            pen.Freeze();

            dc.DrawRoundedRectangle(bgBrush, pen, rect, 4, 4);
            dc.DrawText(text, new Point(rect.X + 6, rect.Y + 3));
        }

        private void DrawNode(DrawingContext dc, ProcessFlowNode node, bool isSelected, bool isHovered)
        {
            var bounds = new Rect(node.X, node.Y, node.Width, node.Height);

            switch (node.Shape)
            {
                case ProcessNodeShape.DecisionDiamond:
                    DrawDecisionDiamond(dc, node, bounds, isSelected, isHovered);
                    break;
                case ProcessNodeShape.StartTerminal:
                case ProcessNodeShape.EndTerminal:
                    DrawTerminalNode(dc, node, bounds, isSelected, isHovered);
                    break;
                case ProcessNodeShape.TaskCard:
                default:
                    DrawTaskCard(dc, node, bounds, isSelected, isHovered);
                    break;
            }

            if (_isDesignMode && isSelected)
            {
                DrawPortDots(dc, node);
            }
        }

        private void DrawTaskCard(DrawingContext dc, ProcessFlowNode node, Rect bounds, bool isSelected, bool isHovered)
        {
            Color headerColor = ParseColor(node.HeaderColorHex, Color.FromRgb(59, 130, 246));
            Color borderColor = isSelected ? Color.FromRgb(59, 130, 246) : (isHovered ? headerColor : ParseColor(node.BorderColorHex, Color.FromRgb(203, 213, 225)));
            double borderWidth = isSelected ? 2.2 : (isHovered ? 1.8 : 1.0);

            // Card Background & Border
            var borderPen = new Pen(new SolidColorBrush(borderColor), borderWidth);
            if (isSelected && _isDesignMode) borderPen.DashStyle = DashStyles.Dash;
            borderPen.Freeze();

            var bgBrush = new SolidColorBrush(Colors.White);
            bgBrush.Freeze();
            dc.DrawRoundedRectangle(bgBrush, borderPen, bounds, 10, 10);

            // Header Background Ribbon
            var headerRect = new Rect(bounds.X, bounds.Y, bounds.Width, 28);
            var headerBg = new SolidColorBrush(Color.FromArgb(20, headerColor.R, headerColor.G, headerColor.B));
            headerBg.Freeze();
            dc.DrawRoundedRectangle(headerBg, null, headerRect, 10, 10);

            // Header Text (Icon + Title)
            var titleTypeface = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
            var titleText = new FormattedText(
                $"{node.IconGlyph} {node.Title}",
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                titleTypeface,
                11.5,
                new SolidColorBrush(headerColor),
                VisualTreeHelper.GetDpi(this).PixelsPerDip
            );
            dc.DrawText(titleText, new Point(bounds.X + 10, bounds.Y + 5));

            // Subtitle Description
            if (!string.IsNullOrWhiteSpace(node.Subtitle))
            {
                var subTypeface = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Regular, FontStretches.Normal);
                var subText = new FormattedText(
                    node.Subtitle,
                    CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    subTypeface,
                    10.5,
                    new SolidColorBrush(Color.FromRgb(71, 85, 105)),
                    VisualTreeHelper.GetDpi(this).PixelsPerDip
                )
                {
                    MaxTextWidth = Math.Max(10, bounds.Width - 20),
                    MaxTextHeight = Math.Max(10, bounds.Height - 34)
                };
                dc.DrawText(subText, new Point(bounds.X + 10, bounds.Y + 32));
            }

            // Action Badge Indicator
            if (!string.IsNullOrWhiteSpace(node.ActionKey))
            {
                var badgeTypeface = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Regular, FontStretches.Normal);
                var badgeText = new FormattedText(
                    "⚡ Action",
                    CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    badgeTypeface,
                    9.0,
                    new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                    VisualTreeHelper.GetDpi(this).PixelsPerDip
                );
                dc.DrawText(badgeText, new Point(bounds.Right - 46, bounds.Bottom - 16));
            }
        }

        private void DrawDecisionDiamond(DrawingContext dc, ProcessFlowNode node, Rect bounds, bool isSelected, bool isHovered)
        {
            Point top = new Point(bounds.X + bounds.Width / 2, bounds.Y);
            Point right = new Point(bounds.Right, bounds.Y + bounds.Height / 2);
            Point bottom = new Point(bounds.X + bounds.Width / 2, bounds.Bottom);
            Point left = new Point(bounds.X, bounds.Y + bounds.Height / 2);

            Color accent = ParseColor(node.HeaderColorHex, Color.FromRgb(2, 132, 199));
            Color border = isSelected ? Color.FromRgb(59, 130, 246) : (isHovered ? accent : ParseColor(node.BorderColorHex, Color.FromRgb(56, 189, 248)));
            double thick = isSelected ? 2.5 : (isHovered ? 2.0 : 1.4);

            var geom = new StreamGeometry();
            using (var ctx = geom.Open())
            {
                ctx.BeginFigure(top, true, true);
                ctx.LineTo(right, true, false);
                ctx.LineTo(bottom, true, false);
                ctx.LineTo(left, true, false);
            }
            geom.Freeze();

            var fill = new SolidColorBrush(Color.FromRgb(245, 250, 255));
            fill.Freeze();
            var pen = new Pen(new SolidColorBrush(border), thick);
            if (isSelected && _isDesignMode) pen.DashStyle = DashStyles.Dash;
            pen.Freeze();

            dc.DrawGeometry(fill, pen, geom);

            // Centered Title Text
            string text = node.Title;
            if (!string.IsNullOrWhiteSpace(node.Subtitle)) text += "\n" + node.Subtitle;

            var typeface = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
            var formatted = new FormattedText(
                text,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                typeface,
                11,
                new SolidColorBrush(accent),
                VisualTreeHelper.GetDpi(this).PixelsPerDip
            )
            {
                TextAlignment = TextAlignment.Center,
                MaxTextWidth = bounds.Width - 16
            };
            dc.DrawText(formatted, new Point(bounds.X + 8, bounds.Y + (bounds.Height - formatted.Height) / 2));
        }

        private void DrawTerminalNode(DrawingContext dc, ProcessFlowNode node, Rect bounds, bool isSelected, bool isHovered)
        {
            Color bg = ParseColor(node.HeaderColorHex, Color.FromRgb(22, 163, 74));
            var fill = new SolidColorBrush(bg);
            fill.Freeze();
            var pen = new Pen(isSelected ? Brushes.White : new SolidColorBrush(Color.FromArgb(40, 0, 0, 0)), 2.0);
            pen.Freeze();

            dc.DrawRoundedRectangle(fill, pen, bounds, bounds.Height / 2, bounds.Height / 2);

            var typeface = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
            var text = new FormattedText(
                node.Title,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                typeface,
                11,
                Brushes.White,
                VisualTreeHelper.GetDpi(this).PixelsPerDip
            )
            {
                TextAlignment = TextAlignment.Center,
                MaxTextWidth = bounds.Width
            };
            dc.DrawText(text, new Point(bounds.X, bounds.Y + (bounds.Height - text.Height) / 2));
        }

        private void DrawPortDots(DrawingContext dc, ProcessFlowNode node)
        {
            var ports = new ProcessPortPosition[] { ProcessPortPosition.Top, ProcessPortPosition.Bottom, ProcessPortPosition.Left, ProcessPortPosition.Right };
            var brush = new SolidColorBrush(Color.FromRgb(59, 130, 246));
            brush.Freeze();
            var pen = new Pen(Brushes.White, 1.5);
            pen.Freeze();

            foreach (var p in ports)
            {
                var pt = GetPortLocation(node, p);
                dc.DrawEllipse(brush, pen, pt, 4, 4);
            }
        }

        private void DrawHudOverlay(DrawingContext dc, double w, double h)
        {
            // Title & Description
            var titleTypeface = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
            var titleText = new FormattedText(
                _definition.Title,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                titleTypeface,
                13.5,
                ZeroWpfTheme.TextPrimary,
                VisualTreeHelper.GetDpi(this).PixelsPerDip
            );
            dc.DrawText(titleText, new Point(24, 16));

            if (!string.IsNullOrWhiteSpace(_definition.Description))
            {
                var descTypeface = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Regular, FontStretches.Normal);
                var descText = new FormattedText(
                    "ℹ " + _definition.Description,
                    CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    descTypeface,
                    11,
                    ZeroWpfTheme.TextSecondary,
                    VisualTreeHelper.GetDpi(this).PixelsPerDip
                );
                dc.DrawText(descText, new Point(w - descText.Width - 24, 18));
            }

            // Mode Badge - Theme-aware glass pill with high contrast indicator dot & typography
            string modeTag = _isDesignMode ? "DESIGN MODE" : "RUN MODE";
            string modeTips = _isDesignMode 
                ? "•  Drag ports to link  •  Right-click: Configure  •  Del: Remove" 
                : "•  Ctrl+Wheel: Zoom  •  Wheel: Pan  •  Double-click: Fit view";
            Color indicatorColor = _isDesignMode ? Color.FromRgb(245, 158, 11) : Color.FromRgb(16, 185, 129);

            var tagTypeface = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
            var tipTypeface = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Regular, FontStretches.Normal);
            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;

            var tagText = new FormattedText(modeTag, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, tagTypeface, 10.5, new SolidColorBrush(indicatorColor), dpi);
            var tipText = new FormattedText(modeTips, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, tipTypeface, 10.5, ZeroWpfTheme.TextSecondary, dpi);

            double totalW = 10 + 8 + 6 + tagText.Width + 4 + tipText.Width + 12;
            var badgeRect = new Rect(16, h - 36, totalW, 26);

            var badgePen = new Pen(ZeroWpfTheme.BorderDefault, 1.0);
            badgePen.Freeze();
            dc.DrawRoundedRectangle(ZeroWpfTheme.BgCard, badgePen, badgeRect, 13, 13);

            // Indicator dot
            var dotBrush = new SolidColorBrush(indicatorColor);
            dotBrush.Freeze();
            dc.DrawEllipse(dotBrush, null, new Point(badgeRect.X + 14, badgeRect.Y + 13), 4, 4);

            // Mode Tag
            dc.DrawText(tagText, new Point(badgeRect.X + 24, badgeRect.Y + 5));

            // Tips
            dc.DrawText(tipText, new Point(badgeRect.X + 24 + tagText.Width + 4, badgeRect.Y + 5));

            // Zoom Info Pill (Bottom Right)
            var zoomText = new FormattedText(
                $"{(_zoom * 100):0}%",
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                tagTypeface,
                11,
                ZeroWpfTheme.TextPrimary,
                dpi
            );

            var zoomRect = new Rect(w - zoomText.Width - 32, h - 36, zoomText.Width + 24, 26);
            dc.DrawRoundedRectangle(ZeroWpfTheme.BgCard, badgePen, zoomRect, 13, 13);
            dc.DrawText(zoomText, new Point(zoomRect.X + 12, zoomRect.Y + 5));
        }

        #endregion

        #region Helpers & Ports

        public Point GetPortLocation(ProcessFlowNode node, ProcessPortPosition port)
        {
            double x = node.X;
            double y = node.Y;
            double w = node.Width;
            double h = node.Height;

            switch (port)
            {
                case ProcessPortPosition.Top:
                    return new Point(x + w / 2, y);
                case ProcessPortPosition.Bottom:
                    return new Point(x + w / 2, y + h);
                case ProcessPortPosition.Left:
                    return new Point(x, y + h / 2);
                case ProcessPortPosition.Right:
                    return new Point(x + w, y + h / 2);
                case ProcessPortPosition.Center:
                default:
                    return new Point(x + w / 2, y + h / 2);
            }
        }

        private static Color ParseColor(string hex, Color fallback)
        {
            if (string.IsNullOrWhiteSpace(hex)) return fallback;
            try
            {
                return (Color)ColorConverter.ConvertFromString(hex);
            }
            catch
            {
                return fallback;
            }
        }

        #endregion

        #region Mouse Interaction

        private ProcessFlowNode? HitTestNode(Point worldPt)
        {
            for (int i = _definition.Nodes.Count - 1; i >= 0; i--)
            {
                var n = _definition.Nodes[i];
                var rect = new Rect(n.X, n.Y, n.Width, n.Height);
                if (rect.Contains(worldPt))
                {
                    return n;
                }
            }
            return null;
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            Point screenPt = e.GetPosition(this);
            Point worldPt = ScreenToWorld(screenPt);

            if (e.ChangedButton == MouseButton.Middle || (e.ChangedButton == MouseButton.Right && !_isDesignMode))
            {
                _isPanning = true;
                _panStartMouse = screenPt;
                _panStartOffset = _panOffset;
                CaptureMouse();
                e.Handled = true;
                return;
            }

            if (e.ClickCount == 2 && e.ChangedButton == MouseButton.Left)
            {
                if (HitTestNode(worldPt) == null && HitTestLane(worldPt) == null)
                {
                    ZoomToFit();
                    e.Handled = true;
                    return;
                }
            }

            if (e.ChangedButton == MouseButton.Left)
            {
                // 1. Check if interacting with SelectedLane handles in design mode
                if (_isDesignMode && _selectedLane != null)
                {
                    var handle = HitTestLaneHandle(_selectedLane, worldPt);
                    if (handle != LaneResizeHandle.None)
                    {
                        _activeLaneHandle = handle;
                        _laneResizeStartMouse = worldPt;
                        _laneInitialBounds = new Rect(_selectedLane.X, _selectedLane.Y, _selectedLane.Width, _selectedLane.Height);
                        _laneNodeInitialPositions.Clear();
                        foreach (var n in _definition.Nodes.Where(n => n.LaneId == _selectedLane.Id || _selectedLane.Contains(n.X + n.Width / 2, n.Y + n.Height / 2)))
                        {
                            _laneNodeInitialPositions[n.Id] = new Point(n.X, n.Y);
                        }
                        CaptureMouse();
                        e.Handled = true;
                        return;
                    }
                }

                // 2. Check if clicking on node
                var hit = HitTestNode(worldPt);
                if (hit != null)
                {
                    SelectedNode = hit;
                    SelectedLane = null;
                    if (_isDesignMode)
                    {
                        _isDraggingNode = true;
                        _nodeDragStartMouse = screenPt;
                        _nodeDragStartPos = new Point(hit.X, hit.Y);
                        CaptureMouse();
                    }
                    e.Handled = true;
                    return;
                }

                // 3. Check if clicking on Swimlane
                var hitLane = HitTestLane(worldPt);
                if (hitLane != null)
                {
                    SelectedLane = hitLane;
                    SelectedNode = null;
                    if (_isDesignMode && (worldPt.Y <= hitLane.Y + 36 || Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)))
                    {
                        _activeLaneHandle = LaneResizeHandle.HeaderMove;
                        _laneResizeStartMouse = worldPt;
                        _laneInitialBounds = new Rect(hitLane.X, hitLane.Y, hitLane.Width, hitLane.Height);
                        _laneNodeInitialPositions.Clear();
                        foreach (var n in _definition.Nodes.Where(n => n.LaneId == hitLane.Id || hitLane.Contains(n.X + n.Width / 2, n.Y + n.Height / 2)))
                        {
                            _laneNodeInitialPositions[n.Id] = new Point(n.X, n.Y);
                        }
                        CaptureMouse();
                    }
                    e.Handled = true;
                    return;
                }

                // 4. Blank canvas
                SelectedNode = null;
                SelectedLane = null;
                _isPanning = true;
                _panStartMouse = screenPt;
                _panStartOffset = _panOffset;
                CaptureMouse();
                e.Handled = true;
            }
            else if (e.ChangedButton == MouseButton.Right && _isDesignMode)
            {
                _rightClickLocation = worldPt;
                var hitNode = HitTestNode(worldPt);
                if (hitNode != null)
                {
                    SelectedNode = hitNode;
                    SelectedLane = null;
                }
                else
                {
                    var hitLane = HitTestLane(worldPt);
                    SelectedLane = hitLane;
                    SelectedNode = null;
                }
                _contextMenu.PlacementTarget = this;
                _contextMenu.IsOpen = true;
                e.Handled = true;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Point screenPt = e.GetPosition(this);
            Point worldPt = ScreenToWorld(screenPt);

            if (_activeLaneHandle != LaneResizeHandle.None && _selectedLane != null && _isDesignMode)
            {
                ApplyLaneResize(worldPt.X - _laneResizeStartMouse.X, worldPt.Y - _laneResizeStartMouse.Y);
                InvalidateVisual();
                return;
            }

            if (_isPanning)
            {
                _panOffset = new Point(
                    _panStartOffset.X + (screenPt.X - _panStartMouse.X),
                    _panStartOffset.Y + (screenPt.Y - _panStartMouse.Y)
                );
                InvalidateVisual();
                return;
            }

            if (_isDraggingNode && _selectedNode != null && _isDesignMode)
            {
                double dx = (screenPt.X - _nodeDragStartMouse.X) / _zoom;
                double dy = (screenPt.Y - _nodeDragStartMouse.Y) / _zoom;
                _selectedNode.X = Math.Max(0, _nodeDragStartPos.X + dx);
                _selectedNode.Y = Math.Max(0, _nodeDragStartPos.Y + dy);
                InvalidateVisual();
                return;
            }

            if (_isDesignMode && _selectedLane != null && !_isPanning && !_isDraggingNode)
            {
                var handle = HitTestLaneHandle(_selectedLane, worldPt);
                _hoveredLaneHandle = handle;
                switch (handle)
                {
                    case LaneResizeHandle.TopLeft:
                    case LaneResizeHandle.BottomRight:
                        Cursor = Cursors.SizeNWSE;
                        return;
                    case LaneResizeHandle.TopRight:
                    case LaneResizeHandle.BottomLeft:
                        Cursor = Cursors.SizeNESW;
                        return;
                    case LaneResizeHandle.Top:
                    case LaneResizeHandle.Bottom:
                        Cursor = Cursors.SizeNS;
                        return;
                    case LaneResizeHandle.Left:
                    case LaneResizeHandle.Right:
                        Cursor = Cursors.SizeWE;
                        return;
                    case LaneResizeHandle.HeaderMove:
                        Cursor = Cursors.SizeAll;
                        return;
                }
            }

            var hovered = HitTestNode(worldPt);
            var hLane = HitTestLane(worldPt);
            bool laneChanged = (hLane != _hoveredLane);
            _hoveredLane = hLane;

            if (hovered != _hoveredNode || laneChanged)
            {
                _hoveredNode = hovered;
                Cursor = _hoveredNode != null ? (_isDesignMode ? Cursors.SizeAll : Cursors.Hand) : Cursors.Arrow;
                InvalidateVisual();
            }
        }

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            base.OnMouseUp(e);
            ReleaseMouseCapture();

            if (_activeLaneHandle != LaneResizeHandle.None)
            {
                _activeLaneHandle = LaneResizeHandle.None;
                DefinitionChanged?.Invoke(this, EventArgs.Empty);
                InvalidateVisual();
                return;
            }

            bool wasDragging = _isDraggingNode;
            _isPanning = false;
            _isDraggingNode = false;

            Point worldPt = ScreenToWorld(e.GetPosition(this));
            var hit = HitTestNode(worldPt);

            if (e.ChangedButton == MouseButton.Left && !wasDragging && hit != null)
            {
                NodeClicked?.Invoke(this, hit);

                if (_autoExecuteAction && !string.IsNullOrWhiteSpace(hit.ActionKey) && !_isDesignMode)
                {
                    var ctx = new ProcessActionContext(hit, this);
                    ActionTriggered?.Invoke(this, ctx);
                    if (!ctx.Handled)
                    {
                        ProcessActionRegistry.Execute(hit.ActionKey, ctx);
                    }
                }
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (_isDesignMode && e.Key == Key.Delete)
            {
                if (_selectedNode != null)
                {
                    string id = _selectedNode.Id;
                    _definition.Nodes.Remove(_selectedNode);
                    _definition.Connections.RemoveAll(c => c.SourceNodeId == id || c.TargetNodeId == id);
                    _selectedNode = null;
                    InvalidateVisual();
                    DefinitionChanged?.Invoke(this, EventArgs.Empty);
                    e.Handled = true;
                }
                else if (_selectedLane != null)
                {
                    foreach (var n in _definition.Nodes.Where(n => n.LaneId == _selectedLane.Id))
                    {
                        n.LaneId = null;
                    }
                    _definition.Lanes.Remove(_selectedLane);
                    _selectedLane = null;
                    InvalidateVisual();
                    DefinitionChanged?.Invoke(this, EventArgs.Empty);
                    e.Handled = true;
                }
            }
        }


        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            base.OnMouseWheel(e);

            bool isCtrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
            bool isShift = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;

            if (_wheelZoomRequiresCtrl && !isCtrl)
            {
                if (_enableWheelPan)
                {
                    double delta = e.Delta;
                    if (isShift)
                    {
                        _panOffset = new Point(_panOffset.X + delta, _panOffset.Y);
                    }
                    else
                    {
                        _panOffset = new Point(_panOffset.X, _panOffset.Y + delta);
                    }
                    InvalidateVisual();
                    e.Handled = true;
                }
                return;
            }

            double oldZoom = _zoom;
            double zoomDelta = e.Delta > 0 ? 1.15 : 0.87;
            ZoomFactor = _zoom * zoomDelta;

            Point mouse = e.GetPosition(this);
            _panOffset = new Point(
                mouse.X - (mouse.X - _panOffset.X) * (_zoom / oldZoom),
                mouse.Y - (mouse.Y - _panOffset.Y) * (_zoom / oldZoom)
            );
            InvalidateVisual();
            e.Handled = true;
        }

        #endregion

        #region Fluent Builder API & Persistence

        public void Clear()
        {
            _definition.Lanes.Clear();
            _definition.Nodes.Clear();
            _definition.Connections.Clear();
            _selectedNode = null;
            _hoveredNode = null;
            InvalidateVisual();
            DefinitionChanged?.Invoke(this, EventArgs.Empty);
        }

        public ProcessFlowNode AddNode(string id, string title, string subtitle, double x, double y, double width = 220, double height = 75, string actionKey = "", string icon = "📄", string headerColorHex = "#3B82F6", string? laneId = null)
        {
            var node = new ProcessFlowNode(id, title, subtitle, x, y, width, height)
            {
                Shape = ProcessNodeShape.TaskCard,
                ActionKey = actionKey,
                IconGlyph = icon,
                HeaderColorHex = headerColorHex,
                LaneId = laneId
            };
            _definition.Nodes.Add(node);
            InvalidateVisual();
            DefinitionChanged?.Invoke(this, EventArgs.Empty);
            return node;
        }

        public ProcessFlowNode AddDecision(string id, string title, string subtitle, double x, double y, double width = 180, double height = 80, string actionKey = "", string headerColorHex = "#0284C7", string? laneId = null)
        {
            var node = new ProcessFlowNode(id, title, subtitle, x, y, width, height)
            {
                Shape = ProcessNodeShape.DecisionDiamond,
                ActionKey = actionKey,
                IconGlyph = "🔍",
                HeaderColorHex = headerColorHex,
                BorderColorHex = "#38BDF8",
                LaneId = laneId
            };
            _definition.Nodes.Add(node);
            InvalidateVisual();
            DefinitionChanged?.Invoke(this, EventArgs.Empty);
            return node;
        }

        public ProcessFlowLane AddLane(string id, string title, double x, double y, double width, double height, string headerColorHex = "#64748B", string bgColorHex = "#F8FAFC")
        {
            var lane = new ProcessFlowLane(id, title, x, y, width, height)
            {
                HeaderColorHex = headerColorHex,
                BackgroundColorHex = bgColorHex
            };
            _definition.Lanes.Add(lane);
            InvalidateVisual();
            DefinitionChanged?.Invoke(this, EventArgs.Empty);
            return lane;
        }

        public ProcessFlowConnection AddConnection(string sourceNodeId, string targetNodeId, string label = "", string strokeColorHex = "#0EA5E9", ProcessPortPosition sourcePort = ProcessPortPosition.Bottom, ProcessPortPosition targetPort = ProcessPortPosition.Top, bool isDashed = false)
        {
            var conn = new ProcessFlowConnection(sourceNodeId, targetNodeId, label, strokeColorHex)
            {
                SourcePort = sourcePort,
                TargetPort = targetPort,
                IsDashed = isDashed,
                RoutingMode = ProcessRoutingMode.Orthogonal
            };
            _definition.Connections.Add(conn);
            InvalidateVisual();
            DefinitionChanged?.Invoke(this, EventArgs.Empty);
            return conn;
        }

        public void ZoomToFit(double padding = 40)
        {
            if (_definition.Nodes.Count == 0)
            {
                _zoom = 1.0;
                _panOffset = new Point(0, 0);
                InvalidateVisual();
                return;
            }

            double minX = double.MaxValue;
            double minY = double.MaxValue;
            double maxX = double.MinValue;
            double maxY = double.MinValue;

            foreach (var node in _definition.Nodes)
            {
                minX = Math.Min(minX, node.X);
                minY = Math.Min(minY, node.Y);
                maxX = Math.Max(maxX, node.X + node.Width);
                maxY = Math.Max(maxY, node.Y + node.Height);
            }

            double contentWidth = maxX - minX;
            double contentHeight = maxY - minY;

            if (contentWidth <= 0 || contentHeight <= 0)
            {
                _zoom = 1.0;
                _panOffset = new Point(0, 0);
                InvalidateVisual();
                return;
            }

            double availWidth = Math.Max(100, ActualWidth - padding * 2);
            double availHeight = Math.Max(100, ActualHeight - padding * 2);

            double targetZoom = Math.Min(availWidth / contentWidth, availHeight / contentHeight);
            targetZoom = Math.Max(0.25, Math.Min(1.5, targetZoom));

            _zoom = targetZoom;
            _panOffset = new Point(
                padding + (availWidth - contentWidth * _zoom) / 2.0 - minX * _zoom,
                padding + (availHeight - contentHeight * _zoom) / 2.0 - minY * _zoom
            );

            InvalidateVisual();
        }

        public void ZoomIn(double factor = 1.15)
        {
            Point center = new Point(ActualWidth / 2.0, ActualHeight / 2.0);
            double oldZoom = _zoom;
            ZoomFactor = _zoom * factor;
            _panOffset = new Point(
                center.X - (center.X - _panOffset.X) * (_zoom / oldZoom),
                center.Y - (center.Y - _panOffset.Y) * (_zoom / oldZoom)
            );
            InvalidateVisual();
        }

        public void ZoomOut(double factor = 0.87)
        {
            Point center = new Point(ActualWidth / 2.0, ActualHeight / 2.0);
            double oldZoom = _zoom;
            ZoomFactor = _zoom * factor;
            _panOffset = new Point(
                center.X - (center.X - _panOffset.X) * (_zoom / oldZoom),
                center.Y - (center.Y - _panOffset.Y) * (_zoom / oldZoom)
            );
            InvalidateVisual();
        }

        public void ResetZoom()
        {
            Point center = new Point(ActualWidth / 2.0, ActualHeight / 2.0);
            double oldZoom = _zoom;
            ZoomFactor = 1.0;
            _panOffset = new Point(
                center.X - (center.X - _panOffset.X) * (_zoom / oldZoom),
                center.Y - (center.Y - _panOffset.Y) * (_zoom / oldZoom)
            );
            InvalidateVisual();
        }

        public string ExportJson(bool indented = true)
        {
            return ProcessFlowSerializer.ToJson(_definition, indented);
        }

        public void ImportJson(string json)
        {
            Definition = ProcessFlowSerializer.FromJson(json);
        }

        #endregion
    
    private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        ZeroWpfTheme.ThemeChanged += OnThemeChanged;
    }

    private void OnUnloaded(object sender, System.Windows.RoutedEventArgs e)
    {
        ZeroWpfTheme.ThemeChanged -= OnThemeChanged;
    }
    private void OnThemeChanged() => InvalidateVisual();
}
    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZProcessMap"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ProcessMap is deprecated and will be removed in 5 release cycles. Please migrate to ZProcessMap instead.")]
    public class ProcessMap : ZProcessMap { }

    /// <summary>
    /// Legacy alias for <see cref="ZProcessMap"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroProcessMap is deprecated and will be removed in 5 release cycles. Please migrate to ZProcessMap instead.")]
    public class ZeroProcessMap : ZProcessMap { }

    #endregion

}
