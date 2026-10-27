using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;
using System.Windows.Forms;
using ZeroUI.Core.Icons;
using ZeroUI.Core.Process;
using ZeroUI.WinForms.Icons;
using ZeroUI.WinForms.Rendering;
using ZeroUI.WinForms.Theme;
using ZeroUI.WinForms.Editors;

namespace ZeroUI.WinForms.Workflow
{
    /// <summary>
    /// High-performance interactive business flowchart and process map control.
    /// Supports multi-lane workflow visualization, card tasks, decision diamonds,
    /// orthogonal transition arrows with branching labels, UserControl action triggers,
    /// and interactive design-mode customization.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Industrial & SCADA")]
    [DefaultEvent("NodeClicked")]
    [Description("Interactive Business Process Flowchart & Navigation Map control with custom action binding")]
    [ToolboxBitmap(typeof(ZeroIcons), "ProcessMap.bmp")]
    public class ZProcessMap : Control
    {
        private ProcessFlowDefinition _definition = new ProcessFlowDefinition();
        private bool _isDesignMode = false;
        private bool _showGrid = true;
        private bool _autoExecuteAction = true;

        private float _zoom = 1.0f;
        private PointF _panOffset = new PointF(0, 0);

        private bool _isPanning = false;
        private PointF _panStartMouse;
        private PointF _panStartOffset;

        private ProcessFlowNode? _selectedNode;
        private ProcessFlowNode? _hoveredNode;
        private bool _isDraggingNode = false;
        private PointF _nodeDragStartMouse;
        private PointF _nodeDragStartPos;

        private bool _isConnecting = false;
        private ProcessFlowNode? _connectSourceNode;
        private ProcessPortPosition _connectSourcePort;
        private PointF _connectCurrentMouse;
        private ProcessPortPosition? _hoveredPort;
        private ProcessFlowNode? _hoveredPortNode;

        private ProcessFlowConnection? _selectedConnection;
        private ProcessFlowConnection? _hoveredConnection;

        private enum ZoomHudButton
        {
            None,
            ZoomOut,
            ResetZoom,
            ZoomIn,
            Fit
        }

        private bool _wheelZoomRequiresCtrl = true;
        private bool _enableWheelPan = true;
        private ZoomHudButton _hoveredHudButton = ZoomHudButton.None;

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
        private PointF _laneResizeStartMouse;
        private RectangleF _laneInitialBounds;
        private Dictionary<string, PointF> _laneNodeInitialPositions = new Dictionary<string, PointF>();

        private enum ControlBarButton
        {
            None,
            View,
            Design,
            Save,
            Reset
        }
        private ControlBarButton _hoveredControlBarButton = ControlBarButton.None;
        private bool _showTitle = true;
        private bool _showControlBar = false;
        private bool _isDirty = false;

        private ContextMenuStrip _contextMenu = null!;
        private ToolStripMenuItem _mnuEditTitle = null!;
        private ToolStripMenuItem _mnuConnectTo = null!;
        private ToolStripMenuItem _mnuAssignAction = null!;
        private ToolStripMenuItem _mnuChangeShape = null!;
        private ToolStripMenuItem _mnuDeleteNode = null!;
        private ToolStripMenuItem _mnuAddStep = null!;

        private ToolStripMenuItem _mnuCreateLaneFromSelection = null!;
        private ToolStripMenuItem _mnuAddNewLane = null!;
        private ToolStripMenuItem _mnuDeleteLane = null!;
        private ToolStripMenuItem _mnuFitLanes = null!;
        private ToolStripMenuItem _mnuAutoArrange = null!;
        private ToolStripMenuItem _mnuRenameLane = null!;
        private ToolStripMenuItem _mnuChangeLaneColor = null!;
        private ToolStripMenuItem _mnuAlignSteps = null!;

        private ContextMenuStrip _connContextMenu = null!;
        private ToolStripMenuItem _mnuEditConnLabel = null!;
        private ToolStripMenuItem _mnuDeleteConn = null!;
        private ToolStripMenuItem _mnuChangeConnColor = null!;

        // Route Caching
        private readonly Dictionary<string, PointF[]> _routeCache = new Dictionary<string, PointF[]>();

        // Minimap Radar Overlay
        private bool _showMinimap = true;
        private bool _isDraggingMinimap = false;

        // Multi-Node Selection & Rubber-band Marquee Box
        private readonly HashSet<ProcessFlowNode> _selectedNodes = new HashSet<ProcessFlowNode>();
        private bool _isMarqueeSelecting = false;
        private PointF _marqueeStartWorld;
        private PointF _marqueeCurrentWorld;
        private readonly Dictionary<string, PointF> _multiNodeDragInitialPositions = new Dictionary<string, PointF>();

        public event EventHandler<ProcessFlowNode>? NodeClicked;
        public event EventHandler<ProcessFlowLane>? LaneClicked;
        public event EventHandler<ProcessActionContext>? ActionTriggered;
        public event EventHandler? DefinitionChanged;
        public event EventHandler? SaveRequested;
        public event EventHandler? ResetRequested;

        public ZProcessMap()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.Selectable |
                ControlStyles.SupportsTransparentBackColor, true);

            Size = new Size(900, 600);
            Font = new Font("Segoe UI", 9f);
            BackColor = Color.FromArgb(248, 250, 252);

            InitContextMenu();

            ZeroTheme.ThemeChanged += OnThemeChanged;
        }

        private void InitContextMenu()
        {
            _contextMenu = new ContextMenuStrip();
            _mnuEditTitle = new ToolStripMenuItem("Edit Title & Subtitle...", null, OnEditTitleClicked);
            _mnuConnectTo = new ToolStripMenuItem(MenuIcons.Format(MenuIcons.Connect, "Connect to Step..."), null);
            _mnuAssignAction = new ToolStripMenuItem("Assign Navigation Action...", null);
            _mnuChangeShape = new ToolStripMenuItem("Change Shape", null);

            var mnuShapeCard = new ToolStripMenuItem("Task Card", null, (s, e) => SetSelectedNodeShape(ProcessNodeShape.TaskCard));
            var mnuShapeDiamond = new ToolStripMenuItem("Decision Diamond", null, (s, e) => SetSelectedNodeShape(ProcessNodeShape.DecisionDiamond));
            var mnuShapeStart = new ToolStripMenuItem("Start Terminal", null, (s, e) => SetSelectedNodeShape(ProcessNodeShape.StartTerminal));
            var mnuShapeEnd = new ToolStripMenuItem("End Terminal", null, (s, e) => SetSelectedNodeShape(ProcessNodeShape.EndTerminal));
            _mnuChangeShape.DropDownItems.AddRange(new ToolStripItem[] { mnuShapeCard, mnuShapeDiamond, mnuShapeStart, mnuShapeEnd });

            _mnuDeleteNode = new ToolStripMenuItem("Delete Step", null, OnDeleteNodeClicked);
            _mnuAddStep = new ToolStripMenuItem("Add New Step Here", null, OnAddStepClicked);

            _mnuCreateLaneFromSelection = new ToolStripMenuItem(MenuIcons.Format(MenuIcons.Frame, "Create Frame from Selection"), null, (s, e) => CreateLaneFromSelectedNodes());
            _mnuAddNewLane = new ToolStripMenuItem(MenuIcons.Format(MenuIcons.Add, "Add New Swimlane..."), null, (s, e) =>
            {
                PointF world = _rightClickLocation;
                var newLane = new ProcessFlowLane("lane_" + Guid.NewGuid().ToString("N").Substring(0, 8), "1. BUSINESS & DESIGN", world.X, world.Y, 680, 340);
                _definition.Lanes.Add(newLane);
                SelectedLane = newLane;
                SelectedNode = null;
                IsDirty = true;
                Invalidate();
                DefinitionChanged?.Invoke(this, EventArgs.Empty);
            });
            _mnuFitLanes = new ToolStripMenuItem(MenuIcons.Format(MenuIcons.FitToContent, "Fit Lanes to Nodes"), null, (s, e) => FitLanesToNodes());
            _mnuAutoArrange = new ToolStripMenuItem(MenuIcons.Format(MenuIcons.AutoLayout, "Auto-Arrange Flow"), null, (s, e) => AutoArrangeLayout(true));
            var mnuAutoH = new ToolStripMenuItem("Horizontal Flow (Left-to-Right)", null, (s, e) => AutoArrangeLayout(true));
            var mnuAutoV = new ToolStripMenuItem("Vertical Flow (Top-to-Bottom)", null, (s, e) => AutoArrangeLayout(false));
            _mnuAutoArrange.DropDownItems.AddRange(new ToolStripItem[] { mnuAutoH, mnuAutoV });
            _mnuDeleteLane = new ToolStripMenuItem(MenuIcons.Format(MenuIcons.Delete, "Delete Swimlane"), null, (s, e) =>
            {
                if (_selectedLane != null)
                {
                    foreach (var n in _definition.Nodes.Where(n => n.LaneId == _selectedLane.Id))
                    {
                        n.LaneId = null;
                    }
                    _definition.Lanes.Remove(_selectedLane);
                    _selectedLane = null;
                    IsDirty = true;
                    Invalidate();
                    DefinitionChanged?.Invoke(this, EventArgs.Empty);
                }
            });
            _mnuRenameLane = new ToolStripMenuItem(MenuIcons.Format(MenuIcons.Rename, "Configure Swimlane..."), null, (s, e) =>
            {
                if (_selectedLane != null) ShowConfigureLaneDialog(_selectedLane);
            });

            _mnuChangeLaneColor = new ToolStripMenuItem(MenuIcons.Format(MenuIcons.Palette, "Change Swimlane Color"), null);
            foreach (var preset in ProcessFlowLane.DefaultPresets)
            {
                var p = preset;
                var hColor = ParseColor(p.HeaderHex, Color.FromArgb(100, 116, 139));
                var bColor = ParseColor(p.BgHex, Color.FromArgb(248, 250, 252));
                var icon = CreateLaneColorSwatch(hColor, bColor);
                var itm = new ToolStripMenuItem(p.Name, icon, (s, e) =>
                {
                    if (_selectedLane != null)
                    {
                        _selectedLane.HeaderColorHex = p.HeaderHex;
                        _selectedLane.BackgroundColorHex = p.BgHex;
                        IsDirty = true;
                        Invalidate();
                        DefinitionChanged?.Invoke(this, EventArgs.Empty);
                    }
                });
                _mnuChangeLaneColor.DropDownItems.Add(itm);
            }

            _mnuChangeLaneColor.DropDownItems.Add(new ToolStripSeparator());

            var mnuCustomLaneHeader = new ToolStripMenuItem("🎨 Custom Header / Border Color...", null, (s, e) =>
            {
                if (_selectedLane == null) return;
                var initial = ParseColor(_selectedLane.HeaderColorHex, Color.FromArgb(100, 116, 139));
                var chosen = ZColorPicker.PickColor(this, initial, "Choose Swimlane Header Color");
                if (chosen.HasValue)
                {
                    _selectedLane.HeaderColorHex = $"#{chosen.Value.R:X2}{chosen.Value.G:X2}{chosen.Value.B:X2}";
                    IsDirty = true;
                    Invalidate();
                    DefinitionChanged?.Invoke(this, EventArgs.Empty);
                }
            });
            _mnuChangeLaneColor.DropDownItems.Add(mnuCustomLaneHeader);

            var mnuCustomLaneBg = new ToolStripMenuItem("🎨 Custom Background Color...", null, (s, e) =>
            {
                if (_selectedLane == null) return;
                var initial = ParseColor(_selectedLane.BackgroundColorHex, Color.FromArgb(248, 250, 252));
                var chosen = ZColorPicker.PickColor(this, initial, "Choose Swimlane Background Color");
                if (chosen.HasValue)
                {
                    _selectedLane.BackgroundColorHex = $"#{chosen.Value.R:X2}{chosen.Value.G:X2}{chosen.Value.B:X2}";
                    IsDirty = true;
                    Invalidate();
                    DefinitionChanged?.Invoke(this, EventArgs.Empty);
                }
            });
            _mnuChangeLaneColor.DropDownItems.Add(mnuCustomLaneBg);

            // Multi-node Alignment Submenu
            _mnuAlignSteps = new ToolStripMenuItem(MenuIcons.Format(MenuIcons.AlignLeft, "Align Steps"), null);
            var mnuAlignLeft = new ToolStripMenuItem("Align Left", null, (s, e) => AlignSelectedNodes(ProcessNodeAlignment.Left));
            var mnuAlignCenter = new ToolStripMenuItem("Align Center (Horizontal)", null, (s, e) => AlignSelectedNodes(ProcessNodeAlignment.Center));
            var mnuAlignRight = new ToolStripMenuItem("Align Right", null, (s, e) => AlignSelectedNodes(ProcessNodeAlignment.Right));
            var mnuAlignTop = new ToolStripMenuItem("Align Top", null, (s, e) => AlignSelectedNodes(ProcessNodeAlignment.Top));
            var mnuAlignMiddle = new ToolStripMenuItem("Align Middle (Vertical)", null, (s, e) => AlignSelectedNodes(ProcessNodeAlignment.Middle));
            var mnuAlignBottom = new ToolStripMenuItem("Align Bottom", null, (s, e) => AlignSelectedNodes(ProcessNodeAlignment.Bottom));
            var mnuDistH = new ToolStripMenuItem("Distribute Horizontally", null, (s, e) => DistributeSelectedNodes(true));
            var mnuDistV = new ToolStripMenuItem("Distribute Vertically", null, (s, e) => DistributeSelectedNodes(false));

            _mnuAlignSteps.DropDownItems.AddRange(new ToolStripItem[] {
                mnuAlignLeft, mnuAlignCenter, mnuAlignRight,
                new ToolStripSeparator(),
                mnuAlignTop, mnuAlignMiddle, mnuAlignBottom,
                new ToolStripSeparator(),
                mnuDistH, mnuDistV
            });

            _contextMenu.Items.AddRange(new ToolStripItem[] {
                _mnuCreateLaneFromSelection,
                _mnuAddStep,
                _mnuAddNewLane,
                new ToolStripSeparator(),
                _mnuConnectTo,
                _mnuEditTitle,
                _mnuRenameLane,
                _mnuChangeLaneColor,
                _mnuAssignAction,
                _mnuChangeShape,
                _mnuAlignSteps,
                new ToolStripSeparator(),
                _mnuAutoArrange,
                _mnuFitLanes,
                new ToolStripSeparator(),
                _mnuDeleteNode,
                _mnuDeleteLane
            });

            _contextMenu.Opening += OnContextMenuOpening;

            // Connection context menu
            _connContextMenu = new ContextMenuStrip();
            _mnuEditConnLabel = new ToolStripMenuItem(MenuIcons.Format(MenuIcons.Edit, "Edit Branch Label..."), null, OnEditConnLabelClicked);
            _mnuChangeConnColor = new ToolStripMenuItem(MenuIcons.Format(MenuIcons.Palette, "Change Line Color"), null);

            var colors = new (string Name, string Hex)[]
            {
                ("Sky Blue (Default)", "#0EA5E9"),
                ("Emerald Green (Pass/Approve)", "#10B981"),
                ("Rose Red (Reject/Fail)", "#EF4444"),
                ("Amber Orange (Warning)", "#F59E0B"),
                ("Purple (Alternative)", "#8B5CF6")
            };
            foreach (var c in colors)
            {
                string hex = c.Hex;
                var itm = new ToolStripMenuItem(c.Name, null, (s, e) =>
                {
                    if (_selectedConnection != null)
                    {
                        _selectedConnection.StrokeColorHex = hex;
                        Invalidate();
                        DefinitionChanged?.Invoke(this, EventArgs.Empty);
                    }
                });
                _mnuChangeConnColor.DropDownItems.Add(itm);
            }

            _mnuChangeConnColor.DropDownItems.Add(new ToolStripSeparator());
            var mnuCustomConn = new ToolStripMenuItem("🎨 Custom Line Color...", null, (s, e) =>
            {
                if (_selectedConnection == null) return;
                var initial = ParseColor(_selectedConnection.StrokeColorHex, Color.FromArgb(14, 165, 233));
                var chosen = ZColorPicker.PickColor(this, initial, "Choose Connection Line Color");
                if (chosen.HasValue)
                {
                    _selectedConnection.StrokeColorHex = $"#{chosen.Value.R:X2}{chosen.Value.G:X2}{chosen.Value.B:X2}";
                    Invalidate();
                    DefinitionChanged?.Invoke(this, EventArgs.Empty);
                }
            });
            _mnuChangeConnColor.DropDownItems.Add(mnuCustomConn);

            var mnuToggleDashed = new ToolStripMenuItem("➖ Toggle Dashed Style", null, (s, e) =>
            {
                if (_selectedConnection != null)
                {
                    _selectedConnection.IsDashed = !_selectedConnection.IsDashed;
                    Invalidate();
                    DefinitionChanged?.Invoke(this, EventArgs.Empty);
                }
            });

            _mnuDeleteConn = new ToolStripMenuItem("🗑 Delete Connection", null, OnDeleteConnClicked);

            _connContextMenu.Items.AddRange(new ToolStripItem[] {
                _mnuEditConnLabel,
                _mnuChangeConnColor,
                mnuToggleDashed,
                new ToolStripSeparator(),
                _mnuDeleteConn
            });
        }

        #region Public Properties

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public ProcessFlowDefinition Definition
        {
            get => _definition;
            set
            {
                _definition = value ?? new ProcessFlowDefinition();
                _selectedNode = null;
                _selectedNodes.Clear();
                _selectedLane = null;
                _selectedConnection = null;
                _hoveredNode = null;
                InvalidateRouteCache();
                Invalidate();
                DefinitionChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        [Category("Behavior")]
        [DefaultValue(false)]
        [Description("Enables visual drag-and-drop workflow editing and connection reconfiguration.")]
        public bool IsDesignMode
        {
            get => _isDesignMode;
            set
            {
                _isDesignMode = value;
                Cursor = Cursors.Default;
                Invalidate();
            }
        }

        [Category("Appearance")]
        [DefaultValue(true)]
        [Description("Displays the background grid dots for spatial alignment.")]
        public bool ShowGrid
        {
            get => _showGrid;
            set
            {
                _showGrid = value;
                Invalidate();
            }
        }

        [Category("Behavior")]
        [DefaultValue(true)]
        [Description("Automatically executes navigation action or launches UserControl on node click.")]
        public bool AutoExecuteAction
        {
            get => _autoExecuteAction;
            set => _autoExecuteAction = value;
        }

        [Category("Behavior")]
        [DefaultValue(true)]
        [Description("Requires holding the CTRL key to zoom with the mouse wheel.")]
        public bool WheelZoomRequiresCtrl
        {
            get => _wheelZoomRequiresCtrl;
            set => _wheelZoomRequiresCtrl = value;
        }

        [Category("Behavior")]
        [DefaultValue(true)]
        [Description("Enables panning the canvas using the mouse wheel.")]
        public bool EnableWheelPan
        {
            get => _enableWheelPan;
            set => _enableWheelPan = value;
        }

        [Category("Appearance")]
        [DefaultValue(1.0f)]
        public float ZoomFactor
        {
            get => _zoom;
            set
            {
                _zoom = Math.Max(0.2f, Math.Min(3.0f, value));
                Invalidate();
            }
        }

        [Browsable(false)]
        public PointF PanOffset
        {
            get => _panOffset;
            set
            {
                _panOffset = value;
                Invalidate();
            }
        }

        [Browsable(false)]
        public ProcessFlowNode? SelectedNode
        {
            get => _selectedNode;
            set
            {
                if (_selectedNode != value)
                {
                    _selectedNode = value;
                    _selectedNodes.Clear();
                    if (_selectedNode != null)
                    {
                        _selectedNodes.Add(_selectedNode);
                        _selectedLane = null;
                    }
                    Invalidate();
                }
            }
        }

        [Browsable(false)]
        public IReadOnlyCollection<ProcessFlowNode> SelectedNodes => _selectedNodes;

        [Category("Appearance")]
        [DefaultValue(true)]
        [Description("Displays the bird-eye minimap radar navigation overlay in the bottom right corner.")]
        public bool ShowMinimap
        {
            get => _showMinimap;
            set { _showMinimap = value; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue(true)]
        [Description("Displays the workflow title and description HUD banner in the top left corner.")]
        public bool ShowTitle
        {
            get => _showTitle;
            set { _showTitle = value; Invalidate(); }
        }


        public void InvalidateRouteCache()
        {
            _routeCache.Clear();
        }

        public void AlignSelectedNodes(ProcessNodeAlignment alignment)
        {
            if (_selectedNodes.Count < 2) return;
            ProcessFlowDefinition.AlignNodes(_selectedNodes, alignment);
            InvalidateRouteCache();
            IsDirty = true;
            Invalidate();
            DefinitionChanged?.Invoke(this, EventArgs.Empty);
        }

        public void DistributeSelectedNodes(bool horizontally)
        {
            if (_selectedNodes.Count < 3) return;
            ProcessFlowDefinition.DistributeNodes(_selectedNodes, horizontally);
            InvalidateRouteCache();
            IsDirty = true;
            Invalidate();
            DefinitionChanged?.Invoke(this, EventArgs.Empty);
        }

        [Browsable(false)]
        public ProcessFlowLane? SelectedLane
        {
            get => _selectedLane;
            set
            {
                if (_selectedLane != value)
                {
                    _selectedLane = value;
                    if (_selectedLane != null) _selectedNode = null;
                    Invalidate();
                    if (_selectedLane != null) LaneClicked?.Invoke(this, _selectedLane);
                }
            }
        }

        [Category("Appearance")]
        [DefaultValue(false)]
        [Description("Displays the top segmented mode and action toolbar (View, Design, Save, Auto-Layout, Reset).")]
        public bool ShowControlBar
        {
            get => _showControlBar;
            set { _showControlBar = value; Invalidate(); }
        }

        [Category("Behavior")]
        [DefaultValue(false)]
        [Description("Indicates whether there are unsaved workflow changes (displays a red badge dot on the Save button).")]
        public bool IsDirty
        {
            get => _isDirty;
            set { _isDirty = value; Invalidate(); }
        }

        public void CreateLaneFromSelectedNodes(string title = "1. BUSINESS & DESIGN (SALES / R&D)")
        {
            var targets = new List<ProcessFlowNode>();
            if (_selectedNodes.Count > 0)
            {
                targets.AddRange(_selectedNodes);
            }
            else if (_selectedNode != null)
            {
                targets.Add(_selectedNode);
            }
            var lane = _definition.CreateLaneFromSelection(targets, title);
            SelectedLane = lane;
            SelectedNode = null;
            _selectedNodes.Clear();
            InvalidateRouteCache();
            IsDirty = true;
            Invalidate();
            DefinitionChanged?.Invoke(this, EventArgs.Empty);
        }

        public void AutoArrangeLayout(bool horizontal = true)
        {
            _definition.AutoArrangeLayout(horizontal);
            InvalidateRouteCache();
            IsDirty = true;
            Invalidate();
            DefinitionChanged?.Invoke(this, EventArgs.Empty);
        }

        public void FitLanesToNodes()
        {
            _definition.FitLanesToNodes();
            InvalidateRouteCache();
            IsDirty = true;
            Invalidate();
            DefinitionChanged?.Invoke(this, EventArgs.Empty);
        }

        public ProcessFlowLane? HitTestLane(PointF worldPt)
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

        private LaneResizeHandle HitTestLaneHandle(ProcessFlowLane lane, PointF worldPt)
        {
            float hs = 10f / _zoom;
            float x = (float)lane.X;
            float y = (float)lane.Y;
            float w = (float)lane.Width;
            float h = (float)lane.Height;

            if (new RectangleF(x - hs, y - hs, hs * 2, hs * 2).Contains(worldPt)) return LaneResizeHandle.TopLeft;
            if (new RectangleF(x + w - hs, y - hs, hs * 2, hs * 2).Contains(worldPt)) return LaneResizeHandle.TopRight;
            if (new RectangleF(x + w - hs, y + h - hs, hs * 2, hs * 2).Contains(worldPt)) return LaneResizeHandle.BottomRight;
            if (new RectangleF(x - hs, y + h - hs, hs * 2, hs * 2).Contains(worldPt)) return LaneResizeHandle.BottomLeft;

            if (new RectangleF(x + w / 2 - hs, y - hs, hs * 2, hs * 2).Contains(worldPt)) return LaneResizeHandle.Top;
            if (new RectangleF(x + w / 2 - hs, y + h - hs, hs * 2, hs * 2).Contains(worldPt)) return LaneResizeHandle.Bottom;
            if (new RectangleF(x - hs, y + h / 2 - hs, hs * 2, hs * 2).Contains(worldPt)) return LaneResizeHandle.Left;
            if (new RectangleF(x + w - hs, y + h / 2 - hs, hs * 2, hs * 2).Contains(worldPt)) return LaneResizeHandle.Right;

            // Header bar (top 34px)
            if (worldPt.X >= x && worldPt.X <= x + w && worldPt.Y >= y && worldPt.Y <= y + 34)
            {
                return LaneResizeHandle.HeaderMove;
            }

            return LaneResizeHandle.None;
        }

        public void ShowConfigureLaneDialog(ProcessFlowLane lane)
        {
            if (lane == null) return;

            using (var dlg = new Form())
            {
                dlg.Text = "Configure Swimlane / Group Frame";
                dlg.Font = new Font("Segoe UI", 9f);
                dlg.Size = new Size(520, 460);
                dlg.StartPosition = FormStartPosition.CenterParent;
                dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
                dlg.MaximizeBox = false;
                dlg.MinimizeBox = false;
                dlg.BackColor = Color.FromArgb(248, 250, 252);

                string selectedHeaderHex = lane.HeaderColorHex;
                string selectedBgHex = lane.BackgroundColorHex;

                var lblTitle = new Label { Text = "Lane Title:", Top = 16, Left = 20, AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
                var txtTitle = new ZTextBox { Text = lane.Title, Top = 38, Left = 20, Width = 464, Height = 32 };

                var lblPresets = new Label { Text = "Color Theme Presets:", Top = 74, Left = 20, AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
                var flowPresets = new FlowLayoutPanel
                {
                    Top = 96,
                    Left = 20,
                    Width = 464,
                    Height = 44,
                    WrapContents = false,
                    AutoScroll = true
                };

                var toolTip = new ToolTip();

                // Custom ColorPickEdit Pickers
                var lblHeaderPicker = new Label { Text = "Header / Accent Color:", Top = 148, Left = 20, AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
                var cpeHeader = new ZColorPicker
                {
                    Top = 170,
                    Left = 20,
                    Width = 220,
                    Height = 34,
                    SelectedColor = ParseColor(selectedHeaderHex, Color.FromArgb(100, 116, 139))
                };

                var lblBgPicker = new Label { Text = "Background Color:", Top = 148, Left = 254, AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
                var cpeBg = new ZColorPicker
                {
                    Top = 170,
                    Left = 254,
                    Width = 230,
                    Height = 34,
                    SelectedColor = ParseColor(selectedBgHex, Color.FromArgb(248, 250, 252))
                };

                // Mini Preview Panel
                var lblPreview = new Label { Text = "Live Preview:", Top = 214, Left = 20, AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
                var pnlPreview = new Panel
                {
                    Top = 236,
                    Left = 20,
                    Width = 464,
                    Height = 118,
                    BackColor = Color.White,
                    BorderStyle = BorderStyle.FixedSingle
                };

                cpeHeader.ColorChanged += (s, e) =>
                {
                    selectedHeaderHex = cpeHeader.HexCode;
                    pnlPreview.Invalidate();
                };

                cpeBg.ColorChanged += (s, e) =>
                {
                    selectedBgHex = cpeBg.HexCode;
                    pnlPreview.Invalidate();
                };

                pnlPreview.Paint += (s, pe) =>
                {
                    var g = pe.Graphics;
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    var b = new RectangleF(10, 10, pnlPreview.Width - 20, pnlPreview.Height - 20);
                    Color bg = ParseColor(selectedBgHex, Color.FromArgb(248, 250, 252));
                    Color header = ParseColor(selectedHeaderHex, Color.FromArgb(100, 116, 139));

                    using (var path = GetRoundedRectPath(b, 8))
                    {
                        using (var fillBrush = new SolidBrush(bg)) g.FillPath(fillBrush, path);
                        using (var pen = new Pen(header, 1.8f)) g.DrawPath(pen, path);
                    }

                    var hb = new RectangleF(b.X, b.Y, b.Width, 26);
                    using (var hPath = GetTopRoundedRectPath(hb, 8))
                    using (var hBrush = new SolidBrush(Color.FromArgb(24, header)))
                    {
                        g.FillPath(hBrush, hPath);
                    }

                    string previewTitle = "⚙ " + (string.IsNullOrWhiteSpace(txtTitle.Text) ? "LANE" : txtTitle.Text.Trim().ToUpperInvariant());
                    using (var font = new Font("Segoe UI", 8.5f, FontStyle.Bold))
                    using (var brush = new SolidBrush(header))
                    {
                        g.DrawString(previewTitle, font, brush, b.X + 12, b.Y + 6);
                    }
                };

                txtTitle.TextChanged += (s, e) => pnlPreview.Invalidate();

                foreach (var preset in ProcessFlowLane.DefaultPresets)
                {
                    var p = preset;
                    var btnSwatch = new Button
                    {
                        Width = 44,
                        Height = 32,
                        Margin = new Padding(2),
                        FlatStyle = FlatStyle.Flat,
                        Cursor = Cursors.Hand
                    };
                    btnSwatch.FlatAppearance.BorderSize = 1;
                    btnSwatch.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
                    toolTip.SetToolTip(btnSwatch, p.Name);

                    Color hColor = ParseColor(p.HeaderHex, Color.FromArgb(100, 116, 139));
                    Color bColor = ParseColor(p.BgHex, Color.FromArgb(248, 250, 252));

                    btnSwatch.Paint += (s, pe) =>
                    {
                        var g = pe.Graphics;
                        using (var bgBrush = new SolidBrush(bColor))
                            g.FillRectangle(bgBrush, 0, 0, btnSwatch.Width, btnSwatch.Height);
                        using (var hBrush = new SolidBrush(hColor))
                            g.FillRectangle(hBrush, 0, 0, btnSwatch.Width, 10);
                        using (var borderPen = new Pen(hColor, 1.5f))
                            g.DrawRectangle(borderPen, 0, 0, btnSwatch.Width - 1, btnSwatch.Height - 1);
                    };

                    btnSwatch.Click += (s, e) =>
                    {
                        selectedHeaderHex = p.HeaderHex;
                        selectedBgHex = p.BgHex;
                        cpeHeader.SelectedColor = ParseColor(p.HeaderHex, Color.FromArgb(100, 116, 139));
                        cpeBg.SelectedColor = ParseColor(p.BgHex, Color.FromArgb(248, 250, 252));
                        pnlPreview.Invalidate();
                    };

                    flowPresets.Controls.Add(btnSwatch);
                }

                var btnOk = new ZButton { Text = "Save", ButtonStyle = ZeroButtonStyle.Primary, DialogResult = DialogResult.OK, Top = 370, Left = 296, Width = 90, Height = 32 };
                var btnCancel = new ZButton { Text = "Cancel", ButtonStyle = ZeroButtonStyle.Secondary, DialogResult = DialogResult.Cancel, Top = 370, Left = 394, Width = 90, Height = 32 };

                dlg.Controls.AddRange(new Control[]
                {
                    lblTitle, txtTitle,
                    lblPresets, flowPresets,
                    lblHeaderPicker, cpeHeader, lblBgPicker, cpeBg,
                    lblPreview, pnlPreview,
                    btnOk, btnCancel
                });
                dlg.AcceptButton = btnOk;
                dlg.CancelButton = btnCancel;

                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    lane.Title = txtTitle.Text.Trim();
                    lane.HeaderColorHex = selectedHeaderHex;
                    lane.BackgroundColorHex = selectedBgHex;
                    IsDirty = true;
                    Invalidate();
                    DefinitionChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        private void ShowRenameLaneDialog(ProcessFlowLane lane) => ShowConfigureLaneDialog(lane);

        #endregion

        #region Coordinate Transforms

        public PointF ScreenToWorld(PointF screen)
        {
            return new PointF((screen.X - _panOffset.X) / _zoom, (screen.Y - _panOffset.Y) / _zoom);
        }

        public PointF WorldToScreen(PointF world)
        {
            return new PointF(world.X * _zoom + _panOffset.X, world.Y * _zoom + _panOffset.Y);
        }

        public RectangleF WorldToScreen(RectangleF world)
        {
            var p = WorldToScreen(new PointF(world.X, world.Y));
            return new RectangleF(p.X, p.Y, world.Width * _zoom, world.Height * _zoom);
        }

        public RectangleF GetMarqueeBoundsWorld()
        {
            float minX = Math.Min(_marqueeStartWorld.X, _marqueeCurrentWorld.X);
            float minY = Math.Min(_marqueeStartWorld.Y, _marqueeCurrentWorld.Y);
            float width = Math.Abs(_marqueeCurrentWorld.X - _marqueeStartWorld.X);
            float height = Math.Abs(_marqueeCurrentWorld.Y - _marqueeStartWorld.Y);
            return new RectangleF(minX, minY, width, height);
        }

        #endregion

        #region Rendering Pipeline

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.InterpolationMode = InterpolationMode.HighQualityBilinear;

            // 1. Background
            Color bg = ZeroTheme.Colors.Background;
            using (var b = new SolidBrush(bg))
            {
                g.FillRectangle(b, ClientRectangle);
            }

            // 2. Background Grid
            if (_showGrid)
            {
                DrawGrid(g);
            }

            // 3. Save Matrix & Apply Pan/Zoom Transform
            var state = g.Save();
            g.TranslateTransform(_panOffset.X, _panOffset.Y);
            g.ScaleTransform(_zoom, _zoom);

            // 4. Swimlanes
            foreach (var lane in _definition.Lanes)
            {
                DrawLane(g, lane);
            }

            // 5. Connections (Orthogonal Lines & Branch Labels)
            foreach (var conn in _definition.Connections)
            {
                DrawConnection(g, conn, conn == _selectedConnection, conn == _hoveredConnection);
            }

            // 6. Nodes (Task Cards, Decision Diamonds, Terminals)
            foreach (var node in _definition.Nodes)
            {
                bool isSel = (node == _selectedNode) || _selectedNodes.Contains(node);
                DrawNode(g, node, isSel, node == _hoveredNode);
            }

            // 6b. Ports & Rubberband Wire in Design Mode
            if (_isDesignMode)
            {
                if (_hoveredNode != null)
                {
                    DrawPortDots(g, _hoveredNode, _hoveredNode == _hoveredPortNode ? _hoveredPort : null);
                }
                if (_selectedNode != null && _selectedNode != _hoveredNode)
                {
                    DrawPortDots(g, _selectedNode, _selectedNode == _hoveredPortNode ? _hoveredPort : null);
                }

                if (_isConnecting && _connectSourceNode != null)
                {
                    PointF p1 = GetPortLocation(_connectSourceNode, _connectSourcePort);
                    PointF p2 = _connectCurrentMouse;
                    using var rubberPen = new Pen(Color.FromArgb(245, 158, 11), 2.5f) { DashStyle = DashStyle.Dash };
                    g.DrawLine(rubberPen, p1, p2);
                    DrawArrowhead(g, rubberPen, p1, p2);
                }

                // 6c. Rubber-band marquee selection box
                if (_isMarqueeSelecting)
                {
                    var mRect = GetMarqueeBoundsWorld();
                    if (mRect.Width > 1 && mRect.Height > 1)
                    {
                        using var mFill = new SolidBrush(Color.FromArgb(35, ZeroTheme.Colors.Primary));
                        using var mPen = new Pen(ZeroTheme.Colors.Primary, 1.2f / _zoom) { DashStyle = DashStyle.Dash };
                        g.FillRectangle(mFill, mRect);
                        g.DrawRectangle(mPen, mRect.X, mRect.Y, mRect.Width, mRect.Height);
                    }
                }
            }

            g.Restore(state);

            // 7. HUD Overlays (Title banner, mode badge, zoom info)
            DrawHudOverlay(g);

            // 8. Bird-Eye Minimap Radar Overlay
            if (_showMinimap)
            {
                DrawMinimap(g, ZeroTheme.Colors);
            }
        }

        private void DrawGrid(Graphics g)
        {
            float step = 25f * _zoom;
            if (step < 8f) return;

            float startX = _panOffset.X % step;
            if (startX < 0) startX += step;
            float startY = _panOffset.Y % step;
            if (startY < 0) startY += step;

            Color dotColor = Color.FromArgb(30, ZeroTheme.Colors.TextSecondary);
            using (var brush = new SolidBrush(dotColor))
            {
                for (float x = startX; x < Width; x += step)
                {
                    for (float y = startY; y < Height; y += step)
                    {
                        g.FillRectangle(brush, x - 1, y - 1, 2, 2);
                    }
                }
            }
        }

        private void DrawLane(Graphics g, ProcessFlowLane lane)
        {
            var bounds = new RectangleF((float)lane.X, (float)lane.Y, (float)lane.Width, (float)lane.Height);
            Color bg = ParseColor(lane.BackgroundColorHex, Color.FromArgb(248, 250, 252));
            bool isSelected = (lane == _selectedLane);
            bool isHovered = (lane == _hoveredLane);
            Color border = isSelected ? ZeroTheme.Colors.Primary : (isHovered ? ParseColor(lane.HeaderColorHex, ZeroTheme.Colors.Primary) : Color.FromArgb(40, ZeroTheme.Colors.Border));
            float borderWidth = isSelected ? 2.2f : (isHovered ? 1.8f : 1.5f);

            using (var path = GetRoundedRectPath(bounds, 12))
            {
                using (var fillBrush = new SolidBrush(bg))
                {
                    g.FillPath(fillBrush, path);
                }
                using (var pen = new Pen(border, borderWidth))
                {
                    if (isSelected && _isDesignMode) pen.DashStyle = DashStyle.Dash;
                    g.DrawPath(pen, path);
                }
            }

            // Lane Header Bar
            var headerRect = new RectangleF(bounds.X, bounds.Y, bounds.Width, 34);
            using (var headerPath = GetTopRoundedRectPath(headerRect, 12))
            using (var headerFill = new SolidBrush(Color.FromArgb(20, ParseColor(lane.HeaderColorHex, ZeroTheme.Colors.Primary))))
            {
                g.FillPath(headerFill, headerPath);
            }

            // Lane Header Tag
            string title = "⚙ " + lane.Title.ToUpperInvariant();
            Color headerColor = ParseColor(lane.HeaderColorHex, ZeroTheme.Colors.TextSecondary);
            var font = ZeroFontCache.Get(Font.FontFamily.Name, 8.5f, FontStyle.Bold);
            using (var brush = new SolidBrush(headerColor))
            {
                g.DrawString(title, font, brush, bounds.X + 16, bounds.Y + 9);
            }

            // Draw 8 resize handles in design mode when selected
            if (_isDesignMode && isSelected)
            {
                DrawLaneHandles(g, bounds);
            }
        }

        private void DrawLaneHandles(Graphics g, RectangleF b)
        {
            float r = 5f / _zoom;
            PointF[] points = new PointF[]
            {
                new PointF(b.Left, b.Top),
                new PointF(b.Left + b.Width / 2, b.Top),
                new PointF(b.Right, b.Top),
                new PointF(b.Right, b.Top + b.Height / 2),
                new PointF(b.Right, b.Bottom),
                new PointF(b.Left + b.Width / 2, b.Bottom),
                new PointF(b.Left, b.Bottom),
                new PointF(b.Left, b.Top + b.Height / 2)
            };

            using var fill = new SolidBrush(Color.White);
            using var stroke = new Pen(ZeroTheme.Colors.Primary, 1.8f / _zoom);
            foreach (var pt in points)
            {
                var hRect = new RectangleF(pt.X - r, pt.Y - r, r * 2, r * 2);
                g.FillRectangle(fill, hRect);
                g.DrawRectangle(stroke, hRect.X, hRect.Y, hRect.Width, hRect.Height);
            }
        }

        private void DrawConnection(Graphics g, ProcessFlowConnection conn, bool isSelected = false, bool isHovered = false)
        {
            var srcNode = _definition.Nodes.FirstOrDefault(n => n.Id == conn.SourceNodeId);
            var tgtNode = _definition.Nodes.FirstOrDefault(n => n.Id == conn.TargetNodeId);
            if (srcNode == null || tgtNode == null) return;

            PointF p1 = GetPortLocation(srcNode, conn.SourcePort);
            PointF p2 = GetPortLocation(tgtNode, conn.TargetPort);

            Color stroke = isSelected
                ? Color.FromArgb(245, 158, 11)
                : (isHovered ? Color.FromArgb(59, 130, 246) : ParseColor(conn.StrokeColorHex, Color.FromArgb(14, 165, 233)));
            float thick = (float)conn.StrokeThickness + (isSelected || isHovered ? 1.5f : 0f);

            using (var pen = new Pen(stroke, thick))
            {
                if (conn.IsDashed) pen.DashStyle = DashStyle.Dash;

                // Orthogonal routing with caching
                string cacheKey = $"{conn.Id}_{p1.X:0.0}_{p1.Y:0.0}_{p2.X:0.0}_{p2.Y:0.0}_{(int)conn.SourcePort}_{(int)conn.TargetPort}";
                if (!_routeCache.TryGetValue(cacheKey, out var points))
                {
                    points = CalculateOrthogonalRoute(p1, p2, conn.SourcePort, conn.TargetPort);
                    _routeCache[cacheKey] = points;
                }

                if (points.Length >= 2)
                {
                    g.DrawLines(pen, points);

                    // Draw arrowhead at end point
                    DrawArrowhead(g, pen, points[points.Length - 2], points[points.Length - 1]);

                    // Draw transition label capsule if present
                    if (!string.IsNullOrWhiteSpace(conn.Label))
                    {
                        DrawConnectionLabel(g, conn.Label, points, stroke);
                    }
                }
            }
        }

        private PointF[] CalculateOrthogonalRoute(PointF p1, PointF p2, ProcessPortPosition sp, ProcessPortPosition tp)
        {
            var list = new List<PointF>();
            list.Add(p1);

            // Same vertical line
            if (Math.Abs(p1.X - p2.X) < 4 && p2.Y > p1.Y)
            {
                list.Add(p2);
                return list.ToArray();
            }

            if (sp == ProcessPortPosition.Bottom && tp == ProcessPortPosition.Top)
            {
                float midY = (p1.Y + p2.Y) / 2;
                list.Add(new PointF(p1.X, midY));
                list.Add(new PointF(p2.X, midY));
            }
            else if (sp == ProcessPortPosition.Right && tp == ProcessPortPosition.Top)
            {
                list.Add(new PointF(p2.X, p1.Y));
            }
            else if (sp == ProcessPortPosition.Left && tp == ProcessPortPosition.Top)
            {
                list.Add(new PointF(p2.X, p1.Y));
            }
            else if (sp == ProcessPortPosition.Left && tp == ProcessPortPosition.Right)
            {
                float midX = (p1.X + p2.X) / 2;
                list.Add(new PointF(midX, p1.Y));
                list.Add(new PointF(midX, p2.Y));
            }
            else
            {
                float midX = (p1.X + p2.X) / 2;
                list.Add(new PointF(midX, p1.Y));
                list.Add(new PointF(midX, p2.Y));
            }

            list.Add(p2);
            return list.ToArray();
        }

        private void DrawArrowhead(Graphics g, Pen pen, PointF from, PointF to)
        {
            float arrowSize = 6.0f + pen.Width;
            float dx = to.X - from.X;
            float dy = to.Y - from.Y;
            float len = (float)Math.Sqrt(dx * dx + dy * dy);
            if (len < 0.001f) return;

            float uX = dx / len;
            float uY = dy / len;
            float vX = -uY;
            float vY = uX;

            PointF pLeft = new PointF(to.X - uX * arrowSize + vX * (arrowSize * 0.5f), to.Y - uY * arrowSize + vY * (arrowSize * 0.5f));
            PointF pRight = new PointF(to.X - uX * arrowSize - vX * (arrowSize * 0.5f), to.Y - uY * arrowSize - vY * (arrowSize * 0.5f));

            using (var brush = new SolidBrush(pen.Color))
            {
                g.FillPolygon(brush, new PointF[] { to, pLeft, pRight });
            }
        }

        private void DrawConnectionLabel(Graphics g, string label, PointF[] points, Color color)
        {
            // Pick midpoint segment
            int midIdx = points.Length / 2;
            PointF a = points[midIdx - 1];
            PointF b = points[midIdx];
            PointF center = new PointF((a.X + b.X) / 2, (a.Y + b.Y) / 2);

            var font = ZeroFontCache.Get(Font.FontFamily.Name, 7.5f, FontStyle.Bold);
            var sz = g.MeasureString(label, font);
            var rect = new RectangleF(center.X - sz.Width / 2 - 6, center.Y - sz.Height / 2 - 3, sz.Width + 12, sz.Height + 6);

            using (var path = GetRoundedRectPath(rect, 4))
            {
                Color connBg = ZeroTheme.IsDark ? Color.FromArgb(240, 24, 28, 44) : Color.FromArgb(245, 255, 255, 255);
                using (var fill = new SolidBrush(connBg))
                {
                    g.FillPath(fill, path);
                }
                using (var pen = new Pen(color, 1.2f))
                {
                    g.DrawPath(pen, path);
                }
            }

            using (var brush = new SolidBrush(color))
            {
                g.DrawString(label, font, brush, rect.X + 6, rect.Y + 3);
            }
        }

        private void DrawNode(Graphics g, ProcessFlowNode node, bool isSelected, bool isHovered)
        {
            var bounds = new RectangleF((float)node.X, (float)node.Y, (float)node.Width, (float)node.Height);

            switch (node.Shape)
            {
                case ProcessNodeShape.DecisionDiamond:
                    DrawDecisionDiamond(g, node, bounds, isSelected, isHovered);
                    break;
                case ProcessNodeShape.StartTerminal:
                case ProcessNodeShape.EndTerminal:
                    DrawTerminalNode(g, node, bounds, isSelected, isHovered);
                    break;
                case ProcessNodeShape.TaskCard:
                default:
                    DrawTaskCard(g, node, bounds, isSelected, isHovered);
                    break;
            }

            // Design mode connection port dots
            if (_isDesignMode && isSelected)
            {
                DrawPortDots(g, node);
            }
        }

        private void DrawTaskCard(Graphics g, ProcessFlowNode node, RectangleF bounds, bool isSelected, bool isHovered)
        {
            float cornerRadius = 10f;
            Color headerColor = ParseColor(node.HeaderColorHex, ZeroTheme.Colors.Primary);
            Color borderColor = isSelected ? ZeroTheme.Colors.Primary : (isHovered ? headerColor : ParseColor(node.BorderColorHex, ZeroTheme.Colors.Border));
            float borderWidth = isSelected ? 2.2f : (isHovered ? 1.8f : 1.0f);

            // Card Shadow if hovered
            if (isHovered)
            {
                var shadowRect = bounds;
                shadowRect.Offset(0, 3);
                using (var shadowPath = GetRoundedRectPath(shadowRect, cornerRadius))
                using (var shadowBrush = new SolidBrush(Color.FromArgb(20, 0, 0, 0)))
                {
                    g.FillPath(shadowBrush, shadowPath);
                }
            }

            using (var path = GetRoundedRectPath(bounds, cornerRadius))
            {
                // Background fill
                using (var fill = new SolidBrush(Color.White))
                {
                    g.FillPath(fill, path);
                }

                // Header stripe (top ~30px)
                float headerHeight = 28f;
                var headerRect = new RectangleF(bounds.X, bounds.Y, bounds.Width, headerHeight);
                using (var headerPath = GetTopRoundedRectPath(headerRect, cornerRadius))
                using (var headerFill = new SolidBrush(Color.FromArgb(16, headerColor)))
                {
                    g.FillPath(headerFill, headerPath);
                }

                // Border
                using (var pen = new Pen(borderColor, borderWidth))
                {
                    if (isSelected && _isDesignMode) pen.DashStyle = DashStyle.Dash;
                    g.DrawPath(pen, path);
                }
            }

            // Icon + Title in Header
            var titleFont = ZeroFontCache.Get(Font.FontFamily.Name, 8.8f, FontStyle.Bold);
            using (var titleBrush = new SolidBrush(headerColor))
            {
                string headerText = $"{node.IconGlyph} {node.Title}";
                g.DrawString(headerText, titleFont, titleBrush, bounds.X + 10, bounds.Y + 6);
            }

            // Subtitle / Description Body
            if (!string.IsNullOrWhiteSpace(node.Subtitle))
            {
                var bodyRect = new RectangleF(bounds.X + 10, bounds.Y + 32, bounds.Width - 20, bounds.Height - 36);
                var bodyFont = ZeroFontCache.Get(Font.FontFamily.Name, 8.0f, FontStyle.Regular);
                using (var bodyBrush = new SolidBrush(Color.FromArgb(71, 85, 105)))
                using (var sf = new StringFormat { Trimming = StringTrimming.EllipsisWord })
                {
                    g.DrawString(node.Subtitle, bodyFont, bodyBrush, bodyRect, sf);
                }
            }

            // Action Indicator Badge (small link icon on bottom-right if bound)
            if (!string.IsNullOrWhiteSpace(node.ActionKey))
            {
                var badgeFont = ZeroFontCache.Get(Font.FontFamily.Name, 7.0f, FontStyle.Regular);
                using (var badgeBrush = new SolidBrush(Color.FromArgb(148, 163, 184)))
                {
                    g.DrawString("⚡ Action", badgeFont, badgeBrush, bounds.Right - 48, bounds.Bottom - 16);
                }
            }
        }

        private void DrawDecisionDiamond(Graphics g, ProcessFlowNode node, RectangleF bounds, bool isSelected, bool isHovered)
        {
            PointF top = new PointF(bounds.X + bounds.Width / 2, bounds.Y);
            PointF right = new PointF(bounds.Right, bounds.Y + bounds.Height / 2);
            PointF bottom = new PointF(bounds.X + bounds.Width / 2, bounds.Bottom);
            PointF left = new PointF(bounds.X, bounds.Y + bounds.Height / 2);
            PointF[] diamond = new PointF[] { top, right, bottom, left };

            Color accent = ParseColor(node.HeaderColorHex, Color.FromArgb(2, 132, 199));
            Color border = isSelected ? ZeroTheme.Colors.Primary : (isHovered ? accent : ParseColor(node.BorderColorHex, Color.FromArgb(56, 189, 248)));
            float thick = isSelected ? 2.5f : (isHovered ? 2.0f : 1.4f);

            // Fill
            using (var fill = new SolidBrush(Color.FromArgb(245, 250, 255)))
            {
                g.FillPolygon(fill, diamond);
            }

            // Border
            using (var pen = new Pen(border, thick))
            {
                if (isSelected && _isDesignMode) pen.DashStyle = DashStyle.Dash;
                g.DrawPolygon(pen, diamond);
            }

            // Text Center
            var titleFont = ZeroFontCache.Get(Font.FontFamily.Name, 8.5f, FontStyle.Bold);
            var subFont = ZeroFontCache.Get(Font.FontFamily.Name, 7.5f, FontStyle.Regular);
            using (var titleBrush = new SolidBrush(accent))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                string text = node.Title;
                if (!string.IsNullOrWhiteSpace(node.Subtitle))
                {
                    text += "\n" + node.Subtitle;
                }
                g.DrawString(text, string.IsNullOrWhiteSpace(node.Subtitle) ? titleFont : subFont, titleBrush, bounds, sf);
            }
        }

        private void DrawTerminalNode(Graphics g, ProcessFlowNode node, RectangleF bounds, bool isSelected, bool isHovered)
        {
            float corner = bounds.Height / 2;
            Color bg = ParseColor(node.HeaderColorHex, ZeroTheme.Colors.Success);

            using (var path = GetRoundedRectPath(bounds, corner))
            {
                using (var fill = new SolidBrush(bg))
                {
                    g.FillPath(fill, path);
                }
                using (var pen = new Pen(isSelected ? Color.White : Color.FromArgb(40, Color.Black), 2f))
                {
                    g.DrawPath(pen, path);
                }
            }

            var font = ZeroFontCache.Get(Font.FontFamily.Name, 8.5f, FontStyle.Bold);
            using (var brush = new SolidBrush(Color.White))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.DrawString(node.Title, font, brush, bounds, sf);
            }
        }

        private void DrawPortDots(Graphics g, ProcessFlowNode node, ProcessPortPosition? activePort = null)
        {
            var ports = new ProcessPortPosition[] { ProcessPortPosition.Top, ProcessPortPosition.Bottom, ProcessPortPosition.Left, ProcessPortPosition.Right };
            using (var fillDefault = new SolidBrush(ZeroTheme.Colors.Primary))
            using (var fillActive = new SolidBrush(Color.FromArgb(245, 158, 11)))
            using (var borderPen = new Pen(Color.White, 1.5f))
            {
                foreach (var p in ports)
                {
                    var pt = GetPortLocation(node, p);
                    bool isActive = (p == activePort);
                    float r = isActive ? 6f : 4f;
                    g.FillEllipse(isActive ? fillActive : fillDefault, pt.X - r, pt.Y - r, r * 2, r * 2);
                    g.DrawEllipse(borderPen, pt.X - r, pt.Y - r, r * 2, r * 2);
                }
            }
        }

        private void DrawHudOverlay(Graphics g)
        {
            // 1. Top Title Bar
            if (_showTitle && !string.IsNullOrWhiteSpace(_definition.Title))
            {
                string title = _definition.Title;
                string desc = _definition.Description;
                var titleFont = ZeroFontCache.Get(Font.FontFamily.Name, 10.5f, FontStyle.Bold);
                var descFont = ZeroFontCache.Get(Font.FontFamily.Name, 8.0f, FontStyle.Regular);
                using (var titleBrush = new SolidBrush(ZeroTheme.Colors.TextPrimary))
                using (var descBrush = new SolidBrush(ZeroTheme.Colors.TextSecondary))
                {
                    g.DrawString(title, titleFont, titleBrush, 24, 16);
                    if (!string.IsNullOrWhiteSpace(desc) && !_showControlBar)
                    {
                        g.DrawString("ℹ " + desc, descFont, descBrush, Width - g.MeasureString("ℹ " + desc, descFont).Width - 24, 18);
                    }
                }
            }

            // Top Control Bar [ 👁 Xem | ✏ Thiết kế | 💾 Lưu 🔴 | 📐 Căn layout | 🔄 Mặc định ]
            if (_showControlBar)
            {
                DrawControlBar(g);
            }

            // 2. Mode Badge (Bottom Left) - Theme-aware glass pill with high contrast status dot & text
            bool isDark = ZeroTheme.IsDark;
            Color hudBgColor = isDark ? Color.FromArgb(240, 24, 28, 44) : Color.FromArgb(250, 255, 255, 255);
            Color hudBorderColor = isDark ? Color.FromArgb(64, 74, 108) : Color.FromArgb(203, 213, 225);
            Color hudTextColor = isDark ? Color.FromArgb(241, 245, 249) : Color.FromArgb(15, 23, 42);
            Color hudHighlightColor = isDark ? Color.FromArgb(50, 62, 95) : Color.FromArgb(226, 232, 240);
            Color hudHighlightTextColor = isDark ? Color.FromArgb(255, 255, 255) : Color.FromArgb(15, 23, 42);
            Color hudDividerColor = isDark ? Color.FromArgb(45, 52, 78) : Color.FromArgb(226, 232, 240);

            string modeTag = _isDesignMode ? "DESIGN MODE" : "RUN MODE";
            string modeTips = _isDesignMode 
                ? "•  Drag ports to link  •  Right-click: Configure  •  Del: Remove" 
                : "•  Ctrl+Wheel: Zoom  •  Wheel: Pan  •  Double-click: Fit view";
            Color indicatorColor = _isDesignMode ? Color.FromArgb(245, 158, 11) : Color.FromArgb(16, 185, 129);

            var tagFont = ZeroFontCache.Get(Font.FontFamily.Name, 8.0f, FontStyle.Bold);
            var tipFont = ZeroFontCache.Get(Font.FontFamily.Name, 8.0f, FontStyle.Regular);
            {
                float tagW = g.MeasureString(modeTag, tagFont).Width;
                float tipW = g.MeasureString(modeTips, tipFont).Width;
                float totalW = 10 + 8 + 6 + tagW + 4 + tipW + 12;
                var rect = new RectangleF(16, Height - 36, totalW, 26);

                using (var path = GetRoundedRectPath(rect, 13))
                using (var fill = new SolidBrush(hudBgColor))
                using (var border = new Pen(hudBorderColor, 1.0f))
                using (var dotBrush = new SolidBrush(indicatorColor))
                using (var tagBrush = new SolidBrush(indicatorColor))
                using (var tipBrush = new SolidBrush(isDark ? Color.FromArgb(203, 213, 225) : Color.FromArgb(71, 85, 105)))
                {
                    g.FillPath(fill, path);
                    g.DrawPath(border, path);

                    // Indicator dot
                    g.FillEllipse(dotBrush, rect.X + 10, rect.Y + 9, 8, 8);

                    // Mode Tag (Bold, color-coded)
                    g.DrawString(modeTag, tagFont, tagBrush, rect.X + 24, rect.Y + 5);

                    // Tips
                    g.DrawString(modeTips, tipFont, tipBrush, rect.X + 24 + tagW + 4, rect.Y + 5);
                }
            }

            // 3. Floating Interactive Zoom HUD (Bottom Right) - Theme-adaptive high-contrast pill
            var hudRect = GetZoomHudRect();
            using (var hudPath = GetRoundedRectPath(hudRect, 14))
            using (var hudBg = new SolidBrush(hudBgColor))
            using (var hudBorder = new Pen(hudBorderColor, 1.0f))
            using (var hudTextBrush = new SolidBrush(hudTextColor))
            using (var hudHighlightTextBrush = new SolidBrush(hudHighlightTextColor))
            using (var highlightBrush = new SolidBrush(hudHighlightColor))
            using (var dividerPen = new Pen(hudDividerColor, 1.0f))
            {
                var boldIconFont = ZeroFontCache.Get(Font.FontFamily.Name, 11f, FontStyle.Bold);
                var btnTextFont = ZeroFontCache.Get(Font.FontFamily.Name, 8.5f, FontStyle.Bold);
                var fitFont = ZeroFontCache.Get(Font.FontFamily.Name, 8.0f, FontStyle.Bold);

                g.FillPath(hudBg, hudPath);

                var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

                var rZoomOut = new RectangleF(hudRect.X, hudRect.Y, 36, hudRect.Height);
                var rReset = new RectangleF(hudRect.X + 36, hudRect.Y, 60, hudRect.Height);
                var rZoomIn = new RectangleF(hudRect.X + 96, hudRect.Y, 36, hudRect.Height);
                var rFit = new RectangleF(hudRect.X + 132, hudRect.Y, 48, hudRect.Height);

                bool isZoomOutHover = _hoveredHudButton == ZoomHudButton.ZoomOut;
                bool isResetHover = _hoveredHudButton == ZoomHudButton.ResetZoom;
                bool isZoomInHover = _hoveredHudButton == ZoomHudButton.ZoomIn;
                bool isFitHover = _hoveredHudButton == ZoomHudButton.Fit;

                // Clip hover fills and dividers seamlessly to the outer pill path (no inner floating frames)
                var oldClip = g.Clip;
                g.SetClip(hudPath, CombineMode.Intersect);

                if (isZoomOutHover) g.FillRectangle(highlightBrush, rZoomOut);
                if (isResetHover) g.FillRectangle(highlightBrush, rReset);
                if (isZoomInHover) g.FillRectangle(highlightBrush, rZoomIn);
                if (isFitHover) g.FillRectangle(highlightBrush, rFit);

                // Subtle button dividers
                g.DrawLine(dividerPen, hudRect.X + 36, hudRect.Y + 6, hudRect.X + 36, hudRect.Bottom - 6);
                g.DrawLine(dividerPen, hudRect.X + 96, hudRect.Y + 6, hudRect.X + 96, hudRect.Bottom - 6);
                g.DrawLine(dividerPen, hudRect.X + 132, hudRect.Y + 6, hudRect.X + 132, hudRect.Bottom - 6);

                g.Clip = oldClip;

                // Outer pill border drawn seamlessly over fills
                g.DrawPath(hudBorder, hudPath);

                // Button Glyphs & Text
                g.DrawString("−", boldIconFont, isZoomOutHover ? hudHighlightTextBrush : hudTextBrush, rZoomOut, sf);
                string zoomText = $"{(_zoom * 100):0}%";
                g.DrawString(zoomText, btnTextFont, isResetHover ? hudHighlightTextBrush : hudTextBrush, rReset, sf);
                g.DrawString("+", boldIconFont, isZoomInHover ? hudHighlightTextBrush : hudTextBrush, rZoomIn, sf);
                g.DrawString("Fit", fitFont, isFitHover ? hudHighlightTextBrush : hudTextBrush, rFit, sf);
            }
        }

        private void DrawControlBar(Graphics g)
        {
            var barRect = GetControlBarRect();
            bool isDark = ZeroTheme.IsDark;
            Color hudBgColor = isDark ? Color.FromArgb(240, 24, 28, 44) : Color.FromArgb(250, 255, 255, 255);
            Color hudBorderColor = isDark ? Color.FromArgb(64, 74, 108) : Color.FromArgb(203, 213, 225);
            Color hudTextColor = isDark ? Color.FromArgb(241, 245, 249) : Color.FromArgb(15, 23, 42);
            Color hudHighlightColor = isDark ? Color.FromArgb(50, 62, 95) : Color.FromArgb(226, 232, 240);
            Color hudHighlightTextColor = isDark ? Color.FromArgb(255, 255, 255) : Color.FromArgb(15, 23, 42);
            Color hudDividerColor = isDark ? Color.FromArgb(45, 52, 78) : Color.FromArgb(226, 232, 240);

            using var path = GetRoundedRectPath(barRect, 14);
            using var bgBrush = new SolidBrush(hudBgColor);
            using var borderPen = new Pen(hudBorderColor, 1.0f);
            using var dividerPen = new Pen(hudDividerColor, 1.0f);
            using var activeBgBrush = new SolidBrush(Color.FromArgb(isDark ? 65 : 35, ZeroTheme.Colors.Primary));
            using var hoverBgBrush = new SolidBrush(hudHighlightColor);
            using var textBrush = new SolidBrush(hudTextColor);
            using var activeTextBrush = new SolidBrush(ZeroTheme.Colors.Primary);
            using var hoverTextBrush = new SolidBrush(hudHighlightTextColor);
            using var badgeBrush = new SolidBrush(Color.FromArgb(239, 68, 68));
            var font = ZeroFontCache.Get(Font.FontFamily.Name, 8.5f, FontStyle.Bold);

            var oldClip = g.Clip;
            g.SetClip(path, CombineMode.Intersect);

            g.FillPath(bgBrush, path);

            var segments = GetControlBarSegments();
            var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

            for (int i = 0; i < segments.Length; i++)
            {
                var seg = segments[i];
                bool isHover = (_hoveredControlBarButton == seg.Btn);

                if (seg.IsActive)
                {
                    g.FillRectangle(activeBgBrush, seg.Rect);
                }
                else if (isHover)
                {
                    g.FillRectangle(hoverBgBrush, seg.Rect);
                }

                if (i > 0)
                {
                    g.DrawLine(dividerPen, seg.Rect.X, seg.Rect.Y + 6, seg.Rect.X, seg.Rect.Bottom - 6);
                }

                var currentBrush = seg.IsActive ? activeTextBrush : (isHover ? hoverTextBrush : textBrush);
                
                // Draw crisp vector icon
                var iconRect = new Rectangle((int)seg.Rect.X + 8, (int)(seg.Rect.Y + (seg.Rect.Height - 14) / 2), 14, 14);
                ZeroIcon.Draw(g, seg.Icon, iconRect, currentBrush.Color);

                // Draw label text
                var textRect = new RectangleF(seg.Rect.X + 24, seg.Rect.Y, seg.Rect.Width - 26, seg.Rect.Height);
                var sfLeft = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };
                g.DrawString(seg.Text, font, currentBrush, textRect, sfLeft);

                if (seg.HasBadge)
                {
                    float bx = seg.Rect.Right - 15f;
                    float by = seg.Rect.Y + 7f;
                    g.FillEllipse(badgeBrush, bx, by, 7f, 7f);
                }
            }

            g.Clip = oldClip;
            g.DrawPath(borderPen, path);
        }

        private RectangleF GetControlBarRect()
        {
            float totalW = 304f;
            float h = 30f;
            float x = Math.Max(260f, Width - totalW - 20f);
            float y = 14f;
            return new RectangleF(x, y, totalW, h);
        }

        private (RectangleF Rect, ControlBarButton Btn, IconKey Icon, string Text, bool IsActive, bool HasBadge)[] GetControlBarSegments()
        {
            var r = GetControlBarRect();
            float curX = r.X;
            return new (RectangleF Rect, ControlBarButton Btn, IconKey Icon, string Text, bool IsActive, bool HasBadge)[]
            {
                (new RectangleF(curX, r.Y, 68, r.Height), ControlBarButton.View, IconKey.Document, "View", !_isDesignMode, false),
                (new RectangleF(curX += 68, r.Y, 86, r.Height), ControlBarButton.Design, IconKey.Edit, "Design", _isDesignMode, false),
                (new RectangleF(curX += 86, r.Y, 64, r.Height), ControlBarButton.Save, IconKey.Save, "Save", false, _isDirty),
                (new RectangleF(curX += 64, r.Y, 86, r.Height), ControlBarButton.Reset, IconKey.Refresh, "Reset", false, false)
            };
        }

        private ControlBarButton HitTestControlBar(PointF screenPt)
        {
            if (!_showControlBar) return ControlBarButton.None;
            var r = GetControlBarRect();
            if (!r.Contains(screenPt)) return ControlBarButton.None;

            var segments = GetControlBarSegments();
            foreach (var seg in segments)
            {
                if (seg.Rect.Contains(screenPt))
                {
                    return seg.Btn;
                }
            }
            return ControlBarButton.None;
        }

        private RectangleF GetZoomHudRect()
        {
            return new RectangleF(Width - 196, Height - 38, 180, 30);
        }

        private ZoomHudButton HitTestZoomHud(PointF screenPt)
        {
            var hud = GetZoomHudRect();
            if (!hud.Contains(screenPt)) return ZoomHudButton.None;

            float relX = screenPt.X - hud.X;
            if (relX < 36) return ZoomHudButton.ZoomOut;
            if (relX < 96) return ZoomHudButton.ResetZoom;
            if (relX < 132) return ZoomHudButton.ZoomIn;
            return ZoomHudButton.Fit;
        }

        #region Minimap Radar Implementation

        private RectangleF GetMinimapRect()
        {
            float w = 180f;
            float h = 120f;
            float x = Width - 196f;
            float y = Height - 38f - h - 8f; // Sits directly above Zoom HUD
            return new RectangleF(x, y, w, h);
        }

        private void GetMinimapTransform(out float scale, out float offsetX, out float offsetY, out RectangleF innerRect, out RectangleF worldBounds)
        {
            var miniRect = GetMinimapRect();
            float innerPadding = 6f;
            innerRect = new RectangleF(miniRect.X + innerPadding, miniRect.Y + innerPadding + 14f, miniRect.Width - innerPadding * 2, miniRect.Height - innerPadding * 2 - 14f);

            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;

            foreach (var lane in _definition.Lanes)
            {
                minX = Math.Min(minX, (float)lane.X);
                minY = Math.Min(minY, (float)lane.Y);
                maxX = Math.Max(maxX, (float)(lane.X + lane.Width));
                maxY = Math.Max(maxY, (float)(lane.Y + lane.Height));
            }
            foreach (var node in _definition.Nodes)
            {
                minX = Math.Min(minX, (float)node.X);
                minY = Math.Min(minY, (float)node.Y);
                maxX = Math.Max(maxX, (float)(node.X + node.Width));
                maxY = Math.Max(maxY, (float)(node.Y + node.Height));
            }

            if (minX >= maxX || minY >= maxY)
            {
                minX = 0; minY = 0; maxX = 1200; maxY = 800;
            }

            float margin = 60f;
            minX -= margin; minY -= margin; maxX += margin; maxY += margin;
            float worldW = maxX - minX;
            float worldH = maxY - minY;
            worldBounds = new RectangleF(minX, minY, worldW, worldH);

            scale = Math.Min(innerRect.Width / worldW, innerRect.Height / worldH);
            offsetX = innerRect.X + (innerRect.Width - worldW * scale) / 2f - minX * scale;
            offsetY = innerRect.Y + (innerRect.Height - worldH * scale) / 2f - minY * scale;
        }

        private void DrawMinimap(Graphics g, ZeroThemePalette colors)
        {
            var miniRect = GetMinimapRect();
            if (miniRect.Width < 20 || miniRect.Height < 20) return;

            bool isDark = ZeroTheme.IsDark;
            Color hudBgColor = isDark ? Color.FromArgb(235, 20, 24, 38) : Color.FromArgb(240, 255, 255, 255);
            Color hudBorderColor = isDark ? Color.FromArgb(64, 74, 108) : Color.FromArgb(203, 213, 225);
            Color hudHeaderColor = isDark ? Color.FromArgb(148, 163, 184) : Color.FromArgb(100, 116, 139);

            using var miniPath = GetRoundedRectPath(miniRect, 10);
            using var bgBrush = new SolidBrush(hudBgColor);
            using var borderPen = new Pen(hudBorderColor, 1.0f);

            g.FillPath(bgBrush, miniPath);
            g.DrawPath(borderPen, miniPath);

            // Radar Header Label
            var headerFont = ZeroFontCache.Get(Font.FontFamily.Name, 7.0f, FontStyle.Bold);
            using (var headerBrush = new SolidBrush(hudHeaderColor))
            {
                g.DrawString("◈ NAVIGATOR", headerFont, headerBrush, miniRect.X + 8, miniRect.Y + 4);
            }

            GetMinimapTransform(out float scale, out float offsetX, out float offsetY, out var innerRect, out _);
            if (scale <= 0.0001f) return;

            RectangleF WorldToMiniRect(RectangleF r) =>
                new RectangleF(r.X * scale + offsetX, r.Y * scale + offsetY, Math.Max(2f, r.Width * scale), Math.Max(2f, r.Height * scale));

            var oldClip = g.Clip;
            g.SetClip(miniPath, CombineMode.Intersect);

            // 1. Draw swimlanes in minimap
            using (var laneBrush = new SolidBrush(Color.FromArgb(isDark ? 30 : 25, colors.Primary)))
            using (var lanePen = new Pen(Color.FromArgb(isDark ? 50 : 40, colors.Border), 1.0f))
            {
                foreach (var lane in _definition.Lanes)
                {
                    var lr = WorldToMiniRect(new RectangleF((float)lane.X, (float)lane.Y, (float)lane.Width, (float)lane.Height));
                    g.FillRectangle(laneBrush, lr);
                    g.DrawRectangle(lanePen, lr.X, lr.Y, lr.Width, lr.Height);
                }
            }

            // 2. Draw connections in minimap
            using (var connPen = new Pen(Color.FromArgb(isDark ? 70 : 60, colors.TextSecondary), 1.0f))
            {
                foreach (var conn in _definition.Connections)
                {
                    var src = _definition.Nodes.FirstOrDefault(n => n.Id == conn.SourceNodeId);
                    var tgt = _definition.Nodes.FirstOrDefault(n => n.Id == conn.TargetNodeId);
                    if (src != null && tgt != null)
                    {
                        var p1 = GetPortLocation(src, conn.SourcePort);
                        var p2 = GetPortLocation(tgt, conn.TargetPort);
                        g.DrawLine(connPen, p1.X * scale + offsetX, p1.Y * scale + offsetY, p2.X * scale + offsetX, p2.Y * scale + offsetY);
                    }
                }
            }

            // 3. Draw nodes in minimap
            using (var nodeBrush = new SolidBrush(Color.FromArgb(200, colors.Primary)))
            using (var selBrush = new SolidBrush(Color.FromArgb(240, 245, 158, 11)))
            {
                foreach (var node in _definition.Nodes)
                {
                    var nr = WorldToMiniRect(new RectangleF((float)node.X, (float)node.Y, (float)node.Width, (float)node.Height));
                    bool isSel = (node == _selectedNode) || _selectedNodes.Contains(node);
                    g.FillRectangle(isSel ? selBrush : nodeBrush, nr);
                }
            }

            // 4. Draw viewport camera frustum
            var vpTL = ScreenToWorld(new PointF(0, 0));
            var vpBR = ScreenToWorld(new PointF(Width, Height));
            var vpWorld = new RectangleF(vpTL.X, vpTL.Y, vpBR.X - vpTL.X, vpBR.Y - vpTL.Y);
            var vpMini = WorldToMiniRect(vpWorld);

            using (var vpFill = new SolidBrush(Color.FromArgb(isDark ? 45 : 30, colors.Primary)))
            using (var vpPen = new Pen(colors.Primary, 1.5f))
            {
                g.FillRectangle(vpFill, vpMini);
                g.DrawRectangle(vpPen, vpMini.X, vpMini.Y, vpMini.Width, vpMini.Height);
            }

            g.Clip = oldClip;
        }

        private void PanToMinimapPoint(PointF mousePt)
        {
            var miniRect = GetMinimapRect();
            if (!miniRect.Contains(mousePt) && !_isDraggingMinimap) return;

            GetMinimapTransform(out float scale, out float offsetX, out float offsetY, out _, out _);
            if (scale <= 0.0001f) return;

            float worldX = (mousePt.X - offsetX) / scale;
            float worldY = (mousePt.Y - offsetY) / scale;

            _panOffset = new PointF(Width / 2f - worldX * _zoom, Height / 2f - worldY * _zoom);
            Invalidate();
        }

        #endregion

        #endregion

        #region Geometry & Port Helpers

        public PointF GetPortLocation(ProcessFlowNode node, ProcessPortPosition port)
        {
            float x = (float)node.X;
            float y = (float)node.Y;
            float w = (float)node.Width;
            float h = (float)node.Height;

            switch (port)
            {
                case ProcessPortPosition.Top:
                    return new PointF(x + w / 2, y);
                case ProcessPortPosition.Bottom:
                    return new PointF(x + w / 2, y + h);
                case ProcessPortPosition.Left:
                    return new PointF(x, y + h / 2);
                case ProcessPortPosition.Right:
                    return new PointF(x + w, y + h / 2);
                case ProcessPortPosition.Center:
                default:
                    return new PointF(x + w / 2, y + h / 2);
            }
        }

        private static GraphicsPath GetRoundedRectPath(RectangleF rect, float radius)
        {
            var path = new GraphicsPath();
            float d = radius * 2;
            if (d > rect.Width) d = rect.Width;
            if (d > rect.Height) d = rect.Height;

            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private static GraphicsPath GetTopRoundedRectPath(RectangleF rect, float radius)
        {
            var path = new GraphicsPath();
            float d = radius * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddLine(rect.Right, rect.Bottom, rect.X, rect.Bottom);
            path.CloseFigure();
            return path;
        }

        private static Color ParseColor(string hex, Color fallback)
        {
            if (string.IsNullOrWhiteSpace(hex)) return fallback;
            try
            {
                return ColorTranslator.FromHtml(hex);
            }
            catch
            {
                return fallback;
            }
        }

        private static Bitmap CreateLaneColorSwatch(Color headerColor, Color bgColor)
        {
            var bmp = new Bitmap(16, 16);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var brush = new SolidBrush(bgColor))
                    g.FillRectangle(brush, 0, 0, 16, 16);
                using (var brush = new SolidBrush(headerColor))
                    g.FillRectangle(brush, 0, 0, 16, 5);
                using (var pen = new Pen(headerColor, 1.2f))
                    g.DrawRectangle(pen, 0, 0, 15, 15);
            }
            return bmp;
        }

        #endregion

        #region Mouse Interaction & Events

        private ProcessFlowNode? HitTestNode(PointF worldPt)
        {
            for (int i = _definition.Nodes.Count - 1; i >= 0; i--)
            {
                var n = _definition.Nodes[i];
                var rect = new RectangleF((float)n.X, (float)n.Y, (float)n.Width, (float)n.Height);
                if (rect.Contains(worldPt))
                {
                    return n;
                }
            }
            return null;
        }

        public bool HitTestPort(PointF worldPt, out ProcessFlowNode? hitNode, out ProcessPortPosition hitPort)
        {
            hitNode = null;
            hitPort = ProcessPortPosition.Center;
            if (!_isDesignMode) return false;

            float threshold = 12f / _zoom;
            var ports = new[] { ProcessPortPosition.Top, ProcessPortPosition.Bottom, ProcessPortPosition.Left, ProcessPortPosition.Right };

            var candidates = new List<ProcessFlowNode>();
            if (_hoveredNode != null) candidates.Add(_hoveredNode);
            if (_selectedNode != null && _selectedNode != _hoveredNode) candidates.Add(_selectedNode);
            foreach (var n in _definition.Nodes)
            {
                if (!candidates.Contains(n)) candidates.Add(n);
            }

            foreach (var node in candidates)
            {
                foreach (var p in ports)
                {
                    var pt = GetPortLocation(node, p);
                    float dx = worldPt.X - pt.X;
                    float dy = worldPt.Y - pt.Y;
                    if ((dx * dx + dy * dy) <= threshold * threshold)
                    {
                        hitNode = node;
                        hitPort = p;
                        return true;
                    }
                }
            }
            return false;
        }

        public ProcessFlowConnection? HitTestConnection(PointF worldPt)
        {
            float threshold = 8f / _zoom;
            foreach (var conn in _definition.Connections)
            {
                var srcNode = _definition.Nodes.FirstOrDefault(n => n.Id == conn.SourceNodeId);
                var tgtNode = _definition.Nodes.FirstOrDefault(n => n.Id == conn.TargetNodeId);
                if (srcNode == null || tgtNode == null) continue;

                PointF p1 = GetPortLocation(srcNode, conn.SourcePort);
                PointF p2 = GetPortLocation(tgtNode, conn.TargetPort);
                var points = CalculateOrthogonalRoute(p1, p2, conn.SourcePort, conn.TargetPort);
                for (int i = 0; i < points.Length - 1; i++)
                {
                    if (DistanceToLineSegment(worldPt, points[i], points[i + 1]) <= threshold)
                    {
                        return conn;
                    }
                }
            }
            return null;
        }

        private static float DistanceToLineSegment(PointF pt, PointF a, PointF b)
        {
            float dx = b.X - a.X;
            float dy = b.Y - a.Y;
            float lenSq = dx * dx + dy * dy;
            if (lenSq < 0.0001f)
            {
                float ddx = pt.X - a.X;
                float ddy = pt.Y - a.Y;
                return (float)Math.Sqrt(ddx * ddx + ddy * ddy);
            }
            float t = Math.Max(0, Math.Min(1, ((pt.X - a.X) * dx + (pt.Y - a.Y) * dy) / lenSq));
            PointF proj = new PointF(a.X + t * dx, a.Y + t * dy);
            float px = pt.X - proj.X;
            float py = pt.Y - proj.Y;
            return (float)Math.Sqrt(px * px + py * py);
        }

        private static ProcessPortPosition GetBestSourcePort(ProcessFlowNode from, ProcessFlowNode to)
        {
            double dx = to.X - from.X;
            double dy = to.Y - from.Y;
            if (Math.Abs(dx) > Math.Abs(dy))
            {
                return dx > 0 ? ProcessPortPosition.Right : ProcessPortPosition.Left;
            }
            else
            {
                return dy > 0 ? ProcessPortPosition.Bottom : ProcessPortPosition.Top;
            }
        }

        private static ProcessPortPosition GetBestTargetPort(ProcessFlowNode from, ProcessFlowNode to, ProcessPortPosition sp)
        {
            return sp switch
            {
                ProcessPortPosition.Right => ProcessPortPosition.Left,
                ProcessPortPosition.Left => ProcessPortPosition.Right,
                ProcessPortPosition.Bottom => ProcessPortPosition.Top,
                ProcessPortPosition.Top => ProcessPortPosition.Bottom,
                _ => ProcessPortPosition.Top
            };
        }

        private void ApplyLaneResize(float dx, float dy)
        {
            if (_selectedLane == null) return;
            const float minW = 160f;
            const float minH = 100f;

            float x = _laneInitialBounds.X;
            float y = _laneInitialBounds.Y;
            float w = _laneInitialBounds.Width;
            float h = _laneInitialBounds.Height;

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
                    float newWLeft = w - dx;
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
                    float newHTop = h - dy;
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
                    float newWTL = w - dx;
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

                    float newHTL = h - dy;
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
                    float newHTR = h - dy;
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
                    float newWBL = w - dx;
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

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();

            // 0a. Check Top Control Bar interaction
            if (e.Button == MouseButtons.Left && _showControlBar)
            {
                var barBtn = HitTestControlBar(e.Location);
                if (barBtn != ControlBarButton.None)
                {
                    switch (barBtn)
                    {
                        case ControlBarButton.View:
                            IsDesignMode = false;
                            break;
                        case ControlBarButton.Design:
                            IsDesignMode = true;
                            break;
                        case ControlBarButton.Save:
                            IsDirty = false;
                            SaveRequested?.Invoke(this, EventArgs.Empty);
                            break;
                        case ControlBarButton.Reset:
                            ResetRequested?.Invoke(this, EventArgs.Empty);
                            break;
                    }
                    Invalidate();
                    return;
                }
            }

            // 0b. Check Zoom HUD interaction
            var hudBtn = HitTestZoomHud(e.Location);
            if (hudBtn != ZoomHudButton.None)
            {
                switch (hudBtn)
                {
                    case ZoomHudButton.ZoomOut:
                        ZoomOut();
                        break;
                    case ZoomHudButton.ResetZoom:
                        ResetZoom();
                        break;
                    case ZoomHudButton.ZoomIn:
                        ZoomIn();
                        break;
                    case ZoomHudButton.Fit:
                        ZoomToFit();
                        break;
                }
                return;
            }

            PointF worldPt = ScreenToWorld(e.Location);

            // 0c. Check Minimap radar interaction
            if (_showMinimap && e.Button == MouseButtons.Left && GetMinimapRect().Contains(e.Location))
            {
                _isDraggingMinimap = true;
                Capture = true;
                PanToMinimapPoint(e.Location);
                return;
            }

            if (e.Button == MouseButtons.Middle || (e.Button == MouseButtons.Right && _selectedNode == null && _selectedConnection == null && _selectedLane == null && _selectedNodes.Count == 0))
            {
                _isPanning = true;
                _panStartMouse = e.Location;
                _panStartOffset = _panOffset;
                Capture = true;
                return;
            }

            if (e.Button == MouseButtons.Left)
            {
                // 1. Check if starting connection drag from a port in design mode
                if (_isDesignMode && HitTestPort(worldPt, out var pNode, out var pPort) && pNode != null)
                {
                    _isConnecting = true;
                    _connectSourceNode = pNode;
                    _connectSourcePort = pPort;
                    _connectCurrentMouse = worldPt;
                    Capture = true;
                    Invalidate();
                    return;
                }

                // 2. Check if interacting with SelectedLane handles in design mode
                if (_isDesignMode && _selectedLane != null)
                {
                    var handle = HitTestLaneHandle(_selectedLane, worldPt);
                    if (handle != LaneResizeHandle.None)
                    {
                        _activeLaneHandle = handle;
                        _laneResizeStartMouse = worldPt;
                        _laneInitialBounds = new RectangleF((float)_selectedLane.X, (float)_selectedLane.Y, (float)_selectedLane.Width, (float)_selectedLane.Height);
                        _laneNodeInitialPositions.Clear();
                        foreach (var n in _definition.Nodes.Where(n => n.LaneId == _selectedLane.Id || _selectedLane.Contains(n.X + n.Width / 2, n.Y + n.Height / 2)))
                        {
                            _laneNodeInitialPositions[n.Id] = new PointF((float)n.X, (float)n.Y);
                        }
                        Capture = true;
                        Invalidate();
                        return;
                    }
                }

                // 3. Check if clicking on node
                var hit = HitTestNode(worldPt);
                if (hit != null)
                {
                    bool isModifier = ModifierKeys.HasFlag(Keys.Control) || ModifierKeys.HasFlag(Keys.Shift);
                    if (isModifier)
                    {
                        if (_selectedNodes.Contains(hit))
                        {
                            _selectedNodes.Remove(hit);
                            _selectedNode = _selectedNodes.LastOrDefault();
                        }
                        else
                        {
                            _selectedNodes.Add(hit);
                            _selectedNode = hit;
                        }
                    }
                    else
                    {
                        if (!_selectedNodes.Contains(hit))
                        {
                            _selectedNodes.Clear();
                            _selectedNodes.Add(hit);
                            _selectedNode = hit;
                        }
                    }

                    _selectedLane = null;
                    _selectedConnection = null;

                    if (_isDesignMode)
                    {
                        _isDraggingNode = true;
                        _nodeDragStartMouse = e.Location;
                        _nodeDragStartPos = new PointF((float)hit.X, (float)hit.Y);
                        _multiNodeDragInitialPositions.Clear();
                        foreach (var node in _selectedNodes)
                        {
                            _multiNodeDragInitialPositions[node.Id] = new PointF((float)node.X, (float)node.Y);
                        }
                        Capture = true;
                    }
                    Invalidate();
                    return;
                }

                // 4. Check if clicking on connection line
                var hitConn = HitTestConnection(worldPt);
                if (hitConn != null)
                {
                    _selectedConnection = hitConn;
                    _selectedNode = null;
                    _selectedNodes.Clear();
                    SelectedLane = null;
                    Invalidate();
                    return;
                }

                // 5. Check if clicking on Swimlane
                var hitLane = HitTestLane(worldPt);
                if (hitLane != null)
                {
                    SelectedLane = hitLane;
                    _selectedNode = null;
                    _selectedNodes.Clear();
                    _selectedConnection = null;

                    if (_isDesignMode && (worldPt.Y <= hitLane.Y + 36 || ModifierKeys.HasFlag(Keys.Shift)))
                    {
                        _activeLaneHandle = LaneResizeHandle.HeaderMove;
                        _laneResizeStartMouse = worldPt;
                        _laneInitialBounds = new RectangleF((float)hitLane.X, (float)hitLane.Y, (float)hitLane.Width, (float)hitLane.Height);
                        _laneNodeInitialPositions.Clear();
                        foreach (var n in _definition.Nodes.Where(n => n.LaneId == hitLane.Id || hitLane.Contains(n.X + n.Width / 2, n.Y + n.Height / 2)))
                        {
                            _laneNodeInitialPositions[n.Id] = new PointF((float)n.X, (float)n.Y);
                        }
                        Capture = true;
                    }
                    Invalidate();
                    return;
                }

                // 6. Clicking on blank canvas
                _selectedNode = null;
                _selectedNodes.Clear();
                SelectedLane = null;
                _selectedConnection = null;

                if (_isDesignMode && !ModifierKeys.HasFlag(Keys.Space))
                {
                    _isMarqueeSelecting = true;
                    _marqueeStartWorld = worldPt;
                    _marqueeCurrentWorld = worldPt;
                    Capture = true;
                    Invalidate();
                }
                else
                {
                    _isPanning = true;
                    _panStartMouse = e.Location;
                    _panStartOffset = _panOffset;
                    Capture = true;
                    Invalidate();
                }
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            // Check Minimap drag
            if (_isDraggingMinimap)
            {
                PanToMinimapPoint(e.Location);
                return;
            }

            // Check Control Bar hover
            if (_showControlBar)
            {
                var barBtn = HitTestControlBar(e.Location);
                if (barBtn != _hoveredControlBarButton)
                {
                    _hoveredControlBarButton = barBtn;
                    Invalidate();
                }
                if (barBtn != ControlBarButton.None)
                {
                    Cursor = Cursors.Hand;
                    return;
                }
            }

            // Check Zoom HUD hover
            var hudBtn = HitTestZoomHud(e.Location);
            if (hudBtn != _hoveredHudButton)
            {
                _hoveredHudButton = hudBtn;
                Invalidate();
            }
            if (hudBtn != ZoomHudButton.None)
            {
                Cursor = Cursors.Hand;
                return;
            }

            // Check Minimap hover
            if (_showMinimap && GetMinimapRect().Contains(e.Location))
            {
                Cursor = Cursors.Hand;
                return;
            }

            PointF worldPt = ScreenToWorld(e.Location);

            if (_isMarqueeSelecting)
            {
                _marqueeCurrentWorld = worldPt;
                var mRect = GetMarqueeBoundsWorld();
                _selectedNodes.Clear();
                foreach (var node in _definition.Nodes)
                {
                    var nRect = new RectangleF((float)node.X, (float)node.Y, (float)node.Width, (float)node.Height);
                    if (mRect.IntersectsWith(nRect))
                    {
                        _selectedNodes.Add(node);
                    }
                }
                _selectedNode = _selectedNodes.LastOrDefault();
                Invalidate();
                return;
            }

            if (_activeLaneHandle != LaneResizeHandle.None && _selectedLane != null && _isDesignMode)
            {
                float dx = worldPt.X - _laneResizeStartMouse.X;
                float dy = worldPt.Y - _laneResizeStartMouse.Y;
                ApplyLaneResize(dx, dy);
                Invalidate();
                return;
            }

            if (_isConnecting)
            {
                _connectCurrentMouse = worldPt;
                HitTestPort(worldPt, out _hoveredPortNode, out var hp);
                _hoveredPort = hp;
                Cursor = Cursors.Cross;
                Invalidate();
                return;
            }

            if (_isPanning)
            {
                _panOffset = new PointF(
                    _panStartOffset.X + (e.X - _panStartMouse.X),
                    _panStartOffset.Y + (e.Y - _panStartMouse.Y)
                );
                Invalidate();
                return;
            }

            if (_isDraggingNode && _isDesignMode)
            {
                float dx = (e.X - _nodeDragStartMouse.X) / _zoom;
                float dy = (e.Y - _nodeDragStartMouse.Y) / _zoom;

                foreach (var node in _selectedNodes)
                {
                    if (_multiNodeDragInitialPositions.TryGetValue(node.Id, out var initPos))
                    {
                        node.X = Math.Max(0, initPos.X + dx);
                        node.Y = Math.Max(0, initPos.Y + dy);
                    }
                }
                InvalidateRouteCache();
                IsDirty = true;
                Invalidate();
                return;
            }

            // Check lane handles cursor in design mode
            if (_isDesignMode && _selectedLane != null && !_isPanning && !_isDraggingNode && !_isConnecting)
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

            // Design mode port hovering
            if (_isDesignMode)
            {
                if (HitTestPort(worldPt, out var pNode, out var pPort))
                {
                    Cursor = Cursors.Cross;
                    if (_hoveredPortNode != pNode || _hoveredPort != pPort)
                    {
                        _hoveredPortNode = pNode;
                        _hoveredPort = pPort;
                        Invalidate();
                    }
                    return;
                }
                else if (_hoveredPortNode != null || _hoveredPort != null)
                {
                    _hoveredPortNode = null;
                    _hoveredPort = null;
                    Invalidate();
                }
            }

            // Connection line hovering
            var hoveredConn = HitTestConnection(worldPt);
            if (hoveredConn != _hoveredConnection)
            {
                _hoveredConnection = hoveredConn;
                Invalidate();
            }

            // Node hovering
            var hovered = HitTestNode(worldPt);
            if (hovered != _hoveredNode)
            {
                _hoveredNode = hovered;
                if (_hoveredNode != null)
                {
                    Cursor = _isDesignMode ? Cursors.SizeAll : Cursors.Hand;
                }
                else if (_hoveredConnection != null)
                {
                    Cursor = Cursors.Hand;
                }
                else
                {
                    Cursor = Cursors.Default;
                }
                Invalidate();
            }

            // Lane hovering
            var hoveredLane = HitTestLane(worldPt);
            if (hoveredLane != _hoveredLane)
            {
                _hoveredLane = hoveredLane;
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            Capture = false;

            if (_isDraggingMinimap)
            {
                _isDraggingMinimap = false;
                return;
            }

            if (_isMarqueeSelecting)
            {
                _isMarqueeSelecting = false;
                Invalidate();
                return;
            }

            if (_activeLaneHandle != LaneResizeHandle.None)
            {
                _activeLaneHandle = LaneResizeHandle.None;
                InvalidateRouteCache();
                IsDirty = true;
                DefinitionChanged?.Invoke(this, EventArgs.Empty);
                Invalidate();
                return;
            }

            if (_isConnecting)
            {
                _isConnecting = false;
                PointF worldPt = ScreenToWorld(e.Location);
                var targetNode = HitTestNode(worldPt);
                if (targetNode != null && _connectSourceNode != null && targetNode.Id != _connectSourceNode.Id)
                {
                    ProcessPortPosition targetPort;
                    if (HitTestPort(worldPt, out var tNode, out var tPort) && tNode?.Id == targetNode.Id)
                    {
                        targetPort = tPort;
                    }
                    else
                    {
                        targetPort = GetBestTargetPort(_connectSourceNode, targetNode, _connectSourcePort);
                    }

                    AddConnection(_connectSourceNode.Id, targetNode.Id, "", "#0EA5E9", _connectSourcePort, targetPort);
                }

                _connectSourceNode = null;
                _hoveredPort = null;
                _hoveredPortNode = null;
                Invalidate();
                return;
            }

            bool wasDragging = _isDraggingNode;
            _isPanning = false;
            _isDraggingNode = false;

            if (wasDragging)
            {
                _multiNodeDragInitialPositions.Clear();
                InvalidateRouteCache();
                IsDirty = true;
                DefinitionChanged?.Invoke(this, EventArgs.Empty);
            }

            PointF worldPtAfter = ScreenToWorld(e.Location);
            var hit = HitTestNode(worldPtAfter);

            if (e.Button == MouseButtons.Left && !wasDragging && hit != null)
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
            else if (e.Button == MouseButtons.Right && _isDesignMode)
            {
                _rightClickLocation = worldPtAfter;

                // Check connection right-click
                var hitConn = HitTestConnection(worldPtAfter);
                if (hitConn != null)
                {
                    _selectedConnection = hitConn;
                    _selectedNode = null;
                    _selectedNodes.Clear();
                    SelectedLane = null;
                    Invalidate();
                    _connContextMenu.Show(this, e.Location);
                    return;
                }

                // Check node right-click
                if (hit != null)
                {
                    if (!_selectedNodes.Contains(hit))
                    {
                        _selectedNodes.Clear();
                        _selectedNodes.Add(hit);
                        _selectedNode = hit;
                    }
                    SelectedLane = null;
                    _selectedConnection = null;
                    Invalidate();
                    _contextMenu.Show(this, e.Location);
                    return;
                }

                // Check lane right-click
                var hitLane = HitTestLane(worldPtAfter);
                if (hitLane != null)
                {
                    SelectedLane = hitLane;
                    _selectedNode = null;
                    _selectedNodes.Clear();
                    _selectedConnection = null;
                    Invalidate();
                    _contextMenu.Show(this, e.Location);
                    return;
                }

                // Blank canvas right-click
                _selectedNode = null;
                _selectedNodes.Clear();
                SelectedLane = null;
                _selectedConnection = null;
                Invalidate();
                _contextMenu.Show(this, e.Location);
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (_isDesignMode && e.KeyCode == Keys.Delete)
            {
                if (_selectedConnection != null)
                {
                    _definition.Connections.Remove(_selectedConnection);
                    _selectedConnection = null;
                    Invalidate();
                    DefinitionChanged?.Invoke(this, EventArgs.Empty);
                    e.Handled = true;
                }
                else if (_selectedNodes.Count > 0 || _selectedNode != null)
                {
                    OnDeleteNodeClicked(this, EventArgs.Empty);
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
                    IsDirty = true;
                    Invalidate();
                    DefinitionChanged?.Invoke(this, EventArgs.Empty);
                    e.Handled = true;
                }
            }
        }

        private PointF _rightClickLocation;

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hoveredHudButton != ZoomHudButton.None || _hoveredControlBarButton != ControlBarButton.None)
            {
                _hoveredHudButton = ZoomHudButton.None;
                _hoveredControlBarButton = ControlBarButton.None;
                Invalidate();
            }
        }

        protected override void OnDoubleClick(EventArgs e)
        {
            base.OnDoubleClick(e);
            if (_selectedLane != null && _selectedNode == null && _selectedConnection == null)
            {
                ShowConfigureLaneDialog(_selectedLane);
                return;
            }
            if (_selectedNode == null && _selectedConnection == null)
            {
                ZoomToFit();
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);

            bool isCtrl = (ModifierKeys & Keys.Control) == Keys.Control;
            bool isShift = (ModifierKeys & Keys.Shift) == Keys.Shift;

            // When WheelZoomRequiresCtrl is true and Ctrl is not pressed, perform smooth panning
            if (_wheelZoomRequiresCtrl && !isCtrl)
            {
                if (_enableWheelPan)
                {
                    float delta = e.Delta;
                    if (isShift)
                    {
                        _panOffset = new PointF(_panOffset.X + delta, _panOffset.Y);
                    }
                    else
                    {
                        _panOffset = new PointF(_panOffset.X, _panOffset.Y + delta);
                    }
                    Invalidate();
                }
                return;
            }

            // Zoom centering around mouse point
            float oldZoom = _zoom;
            float zoomDelta = e.Delta > 0 ? 1.15f : 0.87f;
            ZoomFactor = _zoom * zoomDelta;

            PointF mouse = e.Location;
            _panOffset = new PointF(
                mouse.X - (mouse.X - _panOffset.X) * (_zoom / oldZoom),
                mouse.Y - (mouse.Y - _panOffset.Y) * (_zoom / oldZoom)
            );
            Invalidate();
        }

        #endregion

        #region Context Menu & Customization Handlers

        private void OnContextMenuOpening(object? sender, CancelEventArgs e)
        {
            bool hasNode = _selectedNode != null || _selectedNodes.Count > 0;
            bool hasLane = _selectedLane != null;
            bool isSingleNode = _selectedNodes.Count <= 1;

            // Lane operations
            _mnuCreateLaneFromSelection.Visible = hasNode;
            _mnuAddNewLane.Visible = true;
            _mnuRenameLane.Visible = hasLane;
            _mnuChangeLaneColor.Visible = hasLane;
            _mnuDeleteLane.Visible = hasLane;
            _mnuFitLanes.Visible = true;
            _mnuAutoArrange.Visible = true;

            // Alignment operations (2 or more nodes selected)
            _mnuAlignSteps.Visible = _selectedNodes.Count >= 2;

            // Node operations
            _mnuAddStep.Visible = !hasNode && !hasLane;
            _mnuConnectTo.Visible = hasNode && isSingleNode;
            _mnuEditTitle.Visible = hasNode && isSingleNode;
            _mnuAssignAction.Visible = hasNode && isSingleNode;
            _mnuChangeShape.Visible = hasNode;
            _mnuDeleteNode.Visible = hasNode;

            if (!hasNode || _selectedNode == null)
            {
                return;
            }

            // Populate "Connect to Step..." submenu
            _mnuConnectTo.DropDownItems.Clear();
            var otherNodes = _definition.Nodes.Where(n => n.Id != _selectedNode.Id).ToList();
            if (otherNodes.Count == 0)
            {
                _mnuConnectTo.DropDownItems.Add(new ToolStripMenuItem("(No other steps)") { Enabled = false });
            }
            else
            {
                foreach (var target in otherNodes)
                {
                    var tgt = target;
                    var item = new ToolStripMenuItem($"{MenuIcons.ArrowRight} {tgt.Title}", null, (s, ev) =>
                    {
                        if (_selectedNode != null)
                        {
                            var sp = GetBestSourcePort(_selectedNode, tgt);
                            var tp = GetBestTargetPort(_selectedNode, tgt, sp);
                            AddConnection(_selectedNode.Id, tgt.Id, "", "#0EA5E9", sp, tp);
                        }
                    });
                    _mnuConnectTo.DropDownItems.Add(item);
                }
            }

            // Populate actions submenu from ProcessActionRegistry
            _mnuAssignAction.DropDownItems.Clear();
            var actions = ProcessActionRegistry.GetAllActions();

            var mnuNone = new ToolStripMenuItem("(None / Clear Action)", null, (s, ev) =>
            {
                if (_selectedNode != null)
                {
                    _selectedNode.ActionKey = string.Empty;
                    Invalidate();
                }
            });
            _mnuAssignAction.DropDownItems.Add(mnuNone);
            _mnuAssignAction.DropDownItems.Add(new ToolStripSeparator());

            if (actions.Count == 0)
            {
                var emptyItem = new ToolStripMenuItem("(No registered actions in catalog)") { Enabled = false };
                _mnuAssignAction.DropDownItems.Add(emptyItem);
            }
            else
            {
                // Group by category
                var groups = actions.GroupBy(a => a.Category);
                foreach (var grp in groups)
                {
                    var catMenu = new ToolStripMenuItem(grp.Key);
                    foreach (var act in grp)
                    {
                        string actionKey = act.Key;
                        var actItem = new ToolStripMenuItem(act.Title, null, (s, ev) =>
                        {
                            if (_selectedNode != null)
                            {
                                _selectedNode.ActionKey = actionKey;
                                Invalidate();
                            }
                        })
                        {
                            Checked = (_selectedNode?.ActionKey == actionKey)
                        };
                        catMenu.DropDownItems.Add(actItem);
                    }
                    _mnuAssignAction.DropDownItems.Add(catMenu);
                }
            }
        }

        private void OnEditTitleClicked(object? sender, EventArgs e)
        {
            if (_selectedNode == null) return;

            using (var dlg = new Form())
            {
                dlg.Text = "Configure Process Node";
                dlg.Size = new Size(400, 260);
                dlg.StartPosition = FormStartPosition.CenterParent;
                dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
                dlg.MaximizeBox = false;
                dlg.MinimizeBox = false;

                var lbl1 = new Label { Text = "Title:", Top = 16, Left = 16, Width = 80 };
                var txtTitle = new ZTextBox { Text = _selectedNode.Title, Top = 14, Left = 100, Width = 260, Height = 30 };

                var lbl2 = new Label { Text = "Subtitle:", Top = 50, Left = 16, Width = 80 };
                var txtSub = new ZMemoEdit { Text = _selectedNode.Subtitle, Top = 48, Left = 100, Width = 260, Height = 60 };

                var lbl3 = new Label { Text = "Icon Glyph:", Top = 120, Left = 16, Width = 80 };
                var txtIcon = new ZTextBox { Text = _selectedNode.IconGlyph, Top = 118, Left = 100, Width = 80, Height = 30 };

                var btnOk = new ZButton { Text = "OK", ButtonStyle = ZeroButtonStyle.Primary, DialogResult = DialogResult.OK, Top = 170, Left = 190, Width = 80, Height = 30 };
                var btnCancel = new ZButton { Text = "Cancel", ButtonStyle = ZeroButtonStyle.Secondary, DialogResult = DialogResult.Cancel, Top = 170, Left = 280, Width = 80, Height = 30 };

                dlg.Controls.AddRange(new Control[] { lbl1, txtTitle, lbl2, txtSub, lbl3, txtIcon, btnOk, btnCancel });
                dlg.AcceptButton = btnOk;
                dlg.CancelButton = btnCancel;

                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    _selectedNode.Title = txtTitle.Text;
                    _selectedNode.Subtitle = txtSub.Text;
                    _selectedNode.IconGlyph = txtIcon.Text;
                    Invalidate();
                    DefinitionChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        private void SetSelectedNodeShape(ProcessNodeShape shape)
        {
            var targets = new List<ProcessFlowNode>();
            if (_selectedNodes.Count > 0)
            {
                targets.AddRange(_selectedNodes);
            }
            else if (_selectedNode != null)
            {
                targets.Add(_selectedNode);
            }

            if (targets.Count == 0) return;

            foreach (var node in targets)
            {
                node.Shape = shape;
            }
            Invalidate();
            DefinitionChanged?.Invoke(this, EventArgs.Empty);
        }

        private void OnDeleteNodeClicked(object? sender, EventArgs e)
        {
            var targets = new List<ProcessFlowNode>();
            if (_selectedNodes.Count > 0)
            {
                targets.AddRange(_selectedNodes);
            }
            else if (_selectedNode != null)
            {
                targets.Add(_selectedNode);
            }

            if (targets.Count == 0) return;

            foreach (var node in targets)
            {
                string id = node.Id;
                _definition.Nodes.Remove(node);
                _definition.Connections.RemoveAll(c => c.SourceNodeId == id || c.TargetNodeId == id);
            }

            _selectedNode = null;
            _selectedNodes.Clear();
            InvalidateRouteCache();
            IsDirty = true;
            Invalidate();
            DefinitionChanged?.Invoke(this, EventArgs.Empty);
        }

        private void OnEditConnLabelClicked(object? sender, EventArgs e)
        {
            if (_selectedConnection == null) return;
            using (var dlg = new Form())
            {
                dlg.Text = "Configure Connection Label";
                dlg.Size = new Size(380, 160);
                dlg.StartPosition = FormStartPosition.CenterParent;
                dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
                dlg.MaximizeBox = false;
                dlg.MinimizeBox = false;

                var lbl = new Label { Text = "Branch Label (e.g. Approved, Rejected, In Stock):", Top = 16, Left = 16, Width = 340 };
                var txtLabel = new ZTextBox { Text = _selectedConnection.Label, Top = 42, Left = 16, Width = 330, Height = 30 };
                var btnOk = new ZButton { Text = "OK", ButtonStyle = ZeroButtonStyle.Primary, DialogResult = DialogResult.OK, Top = 80, Left = 170, Width = 80, Height = 30 };
                var btnCancel = new ZButton { Text = "Cancel", ButtonStyle = ZeroButtonStyle.Secondary, DialogResult = DialogResult.Cancel, Top = 80, Left = 266, Width = 80, Height = 30 };

                dlg.Controls.AddRange(new Control[] { lbl, txtLabel, btnOk, btnCancel });
                dlg.AcceptButton = btnOk;
                dlg.CancelButton = btnCancel;

                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    _selectedConnection.Label = txtLabel.Text.Trim();
                    Invalidate();
                    DefinitionChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        private void OnDeleteConnClicked(object? sender, EventArgs e)
        {
            if (_selectedConnection == null) return;
            _definition.Connections.Remove(_selectedConnection);
            _selectedConnection = null;
            Invalidate();
            DefinitionChanged?.Invoke(this, EventArgs.Empty);
        }

        private void OnAddStepClicked(object? sender, EventArgs e)
        {
            int nextIndex = _definition.Nodes.Count + 1;
            var newNode = new ProcessFlowNode(
                Guid.NewGuid().ToString("N"),
                $"Step {nextIndex}",
                "Process description",
                _rightClickLocation.X,
                _rightClickLocation.Y,
                220,
                75
            )
            {
                Shape = ProcessNodeShape.TaskCard,
                HeaderColorHex = "#3B82F6",
                IconGlyph = "📄"
            };

            _definition.Nodes.Add(newNode);
            SelectedNode = newNode;
            Invalidate();
            DefinitionChanged?.Invoke(this, EventArgs.Empty);
        }

        #endregion

        #region Fluent Dynamic Builder API

        /// <summary>
        /// Clears all nodes, lanes, and connections from the process map.
        /// </summary>
        public void Clear()
        {
            _definition.Lanes.Clear();
            _definition.Nodes.Clear();
            _definition.Connections.Clear();
            _selectedNode = null;
            _hoveredNode = null;
            Invalidate();
            DefinitionChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Programmatically adds a process task card node.
        /// </summary>
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
            Invalidate();
            DefinitionChanged?.Invoke(this, EventArgs.Empty);
            return node;
        }

        /// <summary>
        /// Programmatically adds a decision diamond node.
        /// </summary>
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
            Invalidate();
            DefinitionChanged?.Invoke(this, EventArgs.Empty);
            return node;
        }

        /// <summary>
        /// Programmatically adds a swimlane boundary.
        /// </summary>
        public ProcessFlowLane AddLane(string id, string title, double x, double y, double width, double height, string headerColorHex = "#64748B", string bgColorHex = "#F8FAFC")
        {
            var lane = new ProcessFlowLane(id, title, x, y, width, height)
            {
                HeaderColorHex = headerColorHex,
                BackgroundColorHex = bgColorHex
            };
            _definition.Lanes.Add(lane);
            Invalidate();
            DefinitionChanged?.Invoke(this, EventArgs.Empty);
            return lane;
        }

        /// <summary>
        /// Programmatically adds a directional orthogonal connection between two steps.
        /// </summary>
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
            Invalidate();
            DefinitionChanged?.Invoke(this, EventArgs.Empty);
            return conn;
        }

        #endregion

        #region Zoom and Viewport Navigation API

        /// <summary>
        /// Automatically scales and centers the diagram to fit perfectly inside the viewport.
        /// </summary>
        public void ZoomToFit(int padding = 40)
        {
            if (_definition.Nodes.Count == 0)
            {
                _zoom = 1.0f;
                _panOffset = new PointF(0, 0);
                Invalidate();
                return;
            }

            float minX = float.MaxValue;
            float minY = float.MaxValue;
            float maxX = float.MinValue;
            float maxY = float.MinValue;

            foreach (var node in _definition.Nodes)
            {
                minX = Math.Min(minX, (float)node.X);
                minY = Math.Min(minY, (float)node.Y);
                maxX = Math.Max(maxX, (float)(node.X + node.Width));
                maxY = Math.Max(maxY, (float)(node.Y + node.Height));
            }

            float contentWidth = maxX - minX;
            float contentHeight = maxY - minY;

            if (contentWidth <= 0 || contentHeight <= 0)
            {
                _zoom = 1.0f;
                _panOffset = new PointF(0, 0);
                Invalidate();
                return;
            }

            float availWidth = Math.Max(100, Width - padding * 2);
            float availHeight = Math.Max(100, Height - padding * 2);

            float targetZoom = Math.Min(availWidth / contentWidth, availHeight / contentHeight);
            targetZoom = Math.Max(0.25f, Math.Min(1.5f, targetZoom));

            _zoom = targetZoom;
            _panOffset = new PointF(
                padding + (availWidth - contentWidth * _zoom) / 2f - minX * _zoom,
                padding + (availHeight - contentHeight * _zoom) / 2f - minY * _zoom
            );

            Invalidate();
        }

        /// <summary>
        /// Zooms in towards the center of the control viewport.
        /// </summary>
        public void ZoomIn(float factor = 1.15f)
        {
            PointF center = new PointF(Width / 2f, Height / 2f);
            float oldZoom = _zoom;
            ZoomFactor = _zoom * factor;
            _panOffset = new PointF(
                center.X - (center.X - _panOffset.X) * (_zoom / oldZoom),
                center.Y - (center.Y - _panOffset.Y) * (_zoom / oldZoom)
            );
            Invalidate();
        }

        /// <summary>
        /// Zooms out away from the center of the control viewport.
        /// </summary>
        public void ZoomOut(float factor = 0.87f)
        {
            PointF center = new PointF(Width / 2f, Height / 2f);
            float oldZoom = _zoom;
            ZoomFactor = _zoom * factor;
            _panOffset = new PointF(
                center.X - (center.X - _panOffset.X) * (_zoom / oldZoom),
                center.Y - (center.Y - _panOffset.Y) * (_zoom / oldZoom)
            );
            Invalidate();
        }

        /// <summary>
        /// Resets the zoom level to 100% (1.0x).
        /// </summary>
        public void ResetZoom()
        {
            PointF center = new PointF(Width / 2f, Height / 2f);
            float oldZoom = _zoom;
            ZoomFactor = 1.0f;
            _panOffset = new PointF(
                center.X - (center.X - _panOffset.X) * (_zoom / oldZoom),
                center.Y - (center.Y - _panOffset.Y) * (_zoom / oldZoom)
            );
            Invalidate();
        }

        #endregion

        #region Export & Persistence Methods

        /// <summary>
        /// Renders the current diagram onto an off-screen high-resolution bitmap image.
        /// </summary>
        /// <param name="scale">Scale multiplier (e.g. 1.0f for normal, 2.0f or 3.0f for high-DPI print quality).</param>
        /// <param name="padding">Margin padding in pixels around the bounding box.</param>
        public Bitmap RenderToBitmap(float scale = 2.0f, int padding = 40)
        {
            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;

            foreach (var lane in _definition.Lanes)
            {
                minX = Math.Min(minX, (float)lane.X);
                minY = Math.Min(minY, (float)lane.Y);
                maxX = Math.Max(maxX, (float)(lane.X + lane.Width));
                maxY = Math.Max(maxY, (float)(lane.Y + lane.Height));
            }
            foreach (var node in _definition.Nodes)
            {
                minX = Math.Min(minX, (float)node.X);
                minY = Math.Min(minY, (float)node.Y);
                maxX = Math.Max(maxX, (float)(node.X + node.Width));
                maxY = Math.Max(maxY, (float)(node.Y + node.Height));
            }

            if (minX >= maxX || minY >= maxY)
            {
                minX = 0; minY = 0; maxX = 800; maxY = 600;
            }

            float contentW = (maxX - minX) + padding * 2;
            float contentH = (maxY - minY) + padding * 2;
            int bmpW = Math.Max(100, (int)(contentW * scale));
            int bmpH = Math.Max(100, (int)(contentH * scale));

            var bmp = new Bitmap(bmpW, bmpH);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;

                Color bg = ZeroTheme.Colors.Background;
                using (var bgBrush = new SolidBrush(bg))
                {
                    g.FillRectangle(bgBrush, 0, 0, bmpW, bmpH);
                }

                g.ScaleTransform(scale, scale);
                g.TranslateTransform(padding - minX, padding - minY);

                foreach (var lane in _definition.Lanes)
                {
                    DrawLane(g, lane);
                }
                foreach (var conn in _definition.Connections)
                {
                    DrawConnection(g, conn, false, false);
                }
                foreach (var node in _definition.Nodes)
                {
                    DrawNode(g, node, false, false);
                }
            }

            return bmp;
        }

        /// <summary>
        /// Exports the current diagram directly to an image file (PNG, JPEG, BMP).
        /// </summary>
        public void ExportAsImage(string filePath, System.Drawing.Imaging.ImageFormat? format = null, float scale = 2.0f)
        {
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentNullException(nameof(filePath));
            format ??= System.Drawing.Imaging.ImageFormat.Png;
            using var bmp = RenderToBitmap(scale);
            bmp.Save(filePath, format);
        }

        /// <summary>
        /// Exports the current process diagram definition to a clean JSON string.
        /// </summary>
        public string ExportJson(bool indented = true)
        {
            return ProcessFlowSerializer.ToJson(_definition, indented);
        }

        /// <summary>
        /// Imports and loads a process diagram definition from a JSON string.
        /// </summary>
        public void ImportJson(string json)
        {
            Definition = ProcessFlowSerializer.FromJson(json);
        }

        #endregion
    
    private void OnThemeChanged(object? sender, EventArgs e) => Invalidate();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ZeroTheme.ThemeChanged -= OnThemeChanged;
        }
        base.Dispose(disposing);
    }

}

    /// <summary>
    /// Legacy alias for <see cref="ZProcessMap"/>.
    /// </summary>
    [Obsolete("ZeroProcessMap is deprecated and will be removed in 5 release cycles. Please migrate to ZProcessMap instead.")]
    public class ZeroProcessMap : ZProcessMap
    {
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZProcessMap"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ProcessMap is deprecated and will be removed in 5 release cycles. Please migrate to ZProcessMap instead.")]
    [ToolboxItem(false)]
    public class ProcessMap : ZProcessMap
    {
    }

    #endregion
}
