using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ZeroPipeline.Core.Execution;
using ZeroPipeline.Core.Graph;
using ZeroPipeline.Core.Nodes;
using ZeroPipeline.Core.Ports;
using ZeroPipeline.Nodes.Comm;
using ZeroPipeline.Nodes.Inference;
using ZeroPipeline.Nodes.Inspection;
using ZeroPipeline.Nodes.Storage;
using ZeroPipeline.Nodes.Vision;
using ZeroPipeline.Recipe.Builder;
using ZeroPipeline.Recipe.Models;
using ZeroPipeline.Recipe.Registry;
using ZeroPipeline.Recipe.Serialization;
using ZeroPipeline.UI.Controls;
using ZeroPipeline.UI.Models;

namespace ZeroPipeline.UI.Studio
{
    /// <summary>
    /// Interactive Pipeline Studio hosting the node canvas, collapsible toolbox palette,
    /// dynamic property inspector, and continuous execution control toolbar.
    /// </summary>
    public class ZeroPipelineStudioControl : UserControl
    {
        // Core Visual Components
        public ZeroPipelineCanvas Canvas { get; }
        private readonly Panel _topToolbar;
        private readonly Panel _leftToolbox;
        private readonly Panel _rightInspector;
        private readonly Panel _toolboxContent;
        private readonly Panel _inspectorContent;

        // Toolbar Controls
        private readonly Button _btnRun;
        private readonly Button _btnStep;
        private readonly Button _btnPause;
        private readonly Button _btnReset;
        private readonly Button _btnFit;
        private readonly Button _btnLoad;
        private readonly Button _btnSave;
        private readonly Label _lblStatus;

        // Execution engine state
        private PipelineExecutor? _executor;
        private CancellationTokenSource? _continuousCts;
        private bool _isRunningContinuous;
        private long _cycleCount;
        private DateTime _lastCycleTime = DateTime.UtcNow;

        // Registered Node Types for Palette
        public List<PaletteNodeItem> AvailableNodes { get; } = new List<PaletteNodeItem>();

        // Colors
        private static readonly Color BgDark = Color.FromArgb(18, 21, 28);
        private static readonly Color PanelBg = Color.FromArgb(24, 29, 40);
        private static readonly Color HeaderBg = Color.FromArgb(32, 39, 54);
        private static readonly Color BorderColor = Color.FromArgb(45, 55, 75);
        private static readonly Color TextPrimary = Color.FromArgb(240, 245, 255);
        private static readonly Color TextMuted = Color.FromArgb(144, 164, 174);
        private static readonly Color AccentCyan = Color.FromArgb(0, 229, 255);
        private static readonly Color AccentGreen = Color.FromArgb(0, 230, 118);
        private static readonly Color AccentRed = Color.FromArgb(255, 23, 68);

        public ZeroPipelineStudioControl()
        {
            BackColor = BgDark;
            DoubleBuffered = true;
            Font = new Font("Segoe UI", 8.75f, FontStyle.Regular);

            // 0. Center Canvas
            Canvas = new ZeroPipelineCanvas
            {
                Dock = DockStyle.Fill
            };

            // 1. Top Toolbar
            _topToolbar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 44,
                BackColor = PanelBg,
                Padding = new Padding(6, 6, 6, 6)
            };

            _btnRun = CreateToolbarButton("▶ Run", Color.FromArgb(0, 200, 83), (s, e) => ToggleRunContinuous());
            _btnStep = CreateToolbarButton("⏭ Step", Color.FromArgb(3, 169, 244), async (s, e) => await StepExecutionAsync());
            _btnPause = CreateToolbarButton("⏸ Pause", Color.FromArgb(255, 179, 0), (s, e) => PauseExecution());
            _btnReset = CreateToolbarButton("🔄 Reset", Color.FromArgb(120, 144, 156), async (s, e) => await ResetExecutionAsync());
            _btnFit = CreateToolbarButton("⛶ Fit", Color.FromArgb(149, 117, 205), (s, e) => Canvas.ZoomToFit());
            _btnLoad = CreateToolbarButton("📂 Load", Color.FromArgb(96, 125, 139), (s, e) => PromptLoadRecipe());
            _btnSave = CreateToolbarButton("💾 Save", Color.FromArgb(96, 125, 139), (s, e) => PromptSaveRecipe());

            _lblStatus = new Label
            {
                Text = "Status: Ready | Cycles: 0 | Exec: 0.00 ms",
                ForeColor = TextMuted,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleRight,
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 8.25f, FontStyle.Regular)
            };

            // Layout toolbar buttons from left
            var buttonFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Left,
                AutoSize = true,
                WrapContents = false,
                BackColor = Color.Transparent,
                Margin = new Padding(0)
            };
            buttonFlow.Controls.Add(_btnRun);
            buttonFlow.Controls.Add(_btnStep);
            buttonFlow.Controls.Add(_btnPause);
            buttonFlow.Controls.Add(_btnReset);
            buttonFlow.Controls.Add(_btnFit);
            buttonFlow.Controls.Add(_btnLoad);
            buttonFlow.Controls.Add(_btnSave);

            _topToolbar.Controls.Add(_lblStatus);
            _topToolbar.Controls.Add(buttonFlow);

            // 2. Left Toolbox Panel
            _leftToolbox = new Panel
            {
                Dock = DockStyle.Left,
                Width = 210,
                BackColor = PanelBg
            };

            var toolboxHeader = CreatePanelHeader("TOOLBOX PALETTE", _leftToolbox, 210);
            _toolboxContent = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = PanelBg,
                Padding = new Padding(6)
            };
            _leftToolbox.Controls.Add(_toolboxContent);
            _leftToolbox.Controls.Add(toolboxHeader);

            // 3. Right Property Inspector Panel
            _rightInspector = new Panel
            {
                Dock = DockStyle.Right,
                Width = 240,
                BackColor = PanelBg
            };

            var inspectorHeader = CreatePanelHeader("PROPERTY INSPECTOR", _rightInspector, 240);
            _inspectorContent = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = PanelBg,
                Padding = new Padding(8)
            };
            _rightInspector.Controls.Add(_inspectorContent);
            _rightInspector.Controls.Add(inspectorHeader);

            Canvas.NodeSelected += (s, node) => RefreshInspector(node);
            Canvas.ConnectionSelected += (s, conn) => RefreshInspectorForConnection(conn);

            // Add controls
            Controls.Add(Canvas);
            Controls.Add(_leftToolbox);
            Controls.Add(_rightInspector);
            Controls.Add(_topToolbar);

            // Populate Toolbox items
            RegisterDefaultPaletteNodes();
            PopulateToolboxPalette();

            // Initial Inspector state
            RefreshInspector(null);
        }

        private Button CreateToolbarButton(string text, Color accent, EventHandler onClick)
        {
            var btn = new Button
            {
                Text = text,
                FlatStyle = FlatStyle.Flat,
                ForeColor = TextPrimary,
                BackColor = Color.FromArgb(36, 43, 58),
                Font = new Font("Segoe UI", 8.0f, FontStyle.Bold),
                Height = 30,
                AutoSize = true,
                Cursor = Cursors.Hand,
                Margin = new Padding(2, 0, 4, 0)
            };
            btn.FlatAppearance.BorderSize = 1;
            btn.FlatAppearance.BorderColor = accent;
            btn.Click += onClick;
            return btn;
        }

        private Panel CreatePanelHeader(string title, Panel parentPanel, int originalWidth)
        {
            var header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 28,
                BackColor = HeaderBg,
                Padding = new Padding(6, 0, 4, 0)
            };

            var lbl = new Label
            {
                Text = title,
                ForeColor = TextMuted,
                Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
                Dock = DockStyle.Left,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoSize = true
            };

            var btnToggle = new Button
            {
                Text = "⮜",
                FlatStyle = FlatStyle.Flat,
                ForeColor = TextMuted,
                BackColor = Color.Transparent,
                Dock = DockStyle.Right,
                Width = 24,
                Cursor = Cursors.Hand
            };
            btnToggle.FlatAppearance.BorderSize = 0;

            bool isCollapsed = false;
            btnToggle.Click += (s, e) =>
            {
                isCollapsed = !isCollapsed;
                if (isCollapsed)
                {
                    parentPanel.Width = 28;
                    btnToggle.Text = "⮞";
                    lbl.Visible = false;
                }
                else
                {
                    parentPanel.Width = originalWidth;
                    btnToggle.Text = "⮜";
                    lbl.Visible = true;
                }
            };

            header.Controls.Add(btnToggle);
            header.Controls.Add(lbl);
            return header;
        }

        private void RegisterDefaultPaletteNodes()
        {
            AvailableNodes.Clear();
            AvailableNodes.Add(new PaletteNodeItem("SyntheticCameraNode", "Camera (Vision)", "Vision", Color.FromArgb(0, 229, 255), () =>
            {
                var n = new CanvasNode("cam_" + Guid.NewGuid().ToString("N").Substring(0, 4), "Synthetic Camera", "SyntheticCameraNode", "Vision");
                n.AddOutputPin("Frame", typeof(byte[]));
                n.AddOutputPin("FrameIndex", typeof(long));
                n.Properties["Width"] = "640";
                n.Properties["Height"] = "480";
                n.Properties["FrameRate"] = "30";
                return n;
            }));

            AvailableNodes.Add(new PaletteNodeItem("ImageThresholdNode", "Threshold (Vision)", "Vision", Color.FromArgb(0, 229, 255), () =>
            {
                var n = new CanvasNode("thresh_" + Guid.NewGuid().ToString("N").Substring(0, 4), "Image Threshold", "ImageThresholdNode", "Vision");
                n.AddInputPin("SourceImage", typeof(byte[]));
                n.AddOutputPin("BinaryImage", typeof(byte[]));
                n.Properties["Threshold"] = "128";
                n.Properties["Invert"] = "false";
                return n;
            }));

            AvailableNodes.Add(new PaletteNodeItem("MetrologyCaliperNode", "Caliper (Metrology)", "Inspection", Color.FromArgb(179, 136, 255), () =>
            {
                var n = new CanvasNode("caliper_" + Guid.NewGuid().ToString("N").Substring(0, 4), "Metrology Caliper", "MetrologyCaliperNode", "Inspection");
                n.AddInputPin("Image", typeof(byte[]));
                n.AddOutputPin("Measurement", typeof(InspectionResult));
                n.Properties["ExpectedWidthMm"] = "45.0";
                n.Properties["ToleranceMm"] = "0.05";
                return n;
            }));

            AvailableNodes.Add(new PaletteNodeItem("BarcodeReaderNode", "Barcode Reader", "Inspection", Color.FromArgb(255, 152, 0), () =>
            {
                var n = new CanvasNode("barcode_" + Guid.NewGuid().ToString("N").Substring(0, 4), "Barcode Reader", "BarcodeReaderNode", "Inspection");
                n.AddInputPin("Image", typeof(byte[]));
                n.AddOutputPin("Barcode", typeof(string));
                n.Properties["Symbology"] = "Code128";
                return n;
            }));

            AvailableNodes.Add(new PaletteNodeItem("TensorInferenceNode", "AI Tensor Inference", "Inference", Color.FromArgb(255, 215, 0), () =>
            {
                var n = new CanvasNode("ai_" + Guid.NewGuid().ToString("N").Substring(0, 4), "Tensor Inference", "TensorInferenceNode", "Inference");
                n.AddInputPin("InputTensor", typeof(object));
                n.AddOutputPin("OutputTensor", typeof(object));
                n.Properties["ModelPath"] = "models/inspection_model.onnx";
                return n;
            }));

            AvailableNodes.Add(new PaletteNodeItem("TsdbStorageNode", "TSDB Storage Sink", "Storage", Color.FromArgb(0, 255, 136), () =>
            {
                var n = new CanvasNode("tsdb_" + Guid.NewGuid().ToString("N").Substring(0, 4), "TSDB Sink", "TsdbStorageNode", "Storage");
                n.AddInputPin("Record", typeof(InspectionResult));
                n.Properties["RetentionDays"] = "30";
                return n;
            }));

            AvailableNodes.Add(new PaletteNodeItem("ModbusSinkNode", "Modbus / PLC Sink", "Comm", Color.FromArgb(244, 67, 54), () =>
            {
                var n = new CanvasNode("plc_" + Guid.NewGuid().ToString("N").Substring(0, 4), "Modbus PLC", "ModbusSinkNode", "Comm");
                n.AddInputPin("Inspection", typeof(InspectionResult));
                n.Properties["IpAddress"] = "192.168.1.100";
                n.Properties["Port"] = "502";
                return n;
            }));
        }

        private void PopulateToolboxPalette()
        {
            _toolboxContent.Controls.Clear();

            var groups = AvailableNodes.GroupBy(n => n.Category);
            int currentY = 4;

            foreach (var group in groups)
            {
                var lblCat = new Label
                {
                    Text = group.Key.ToUpperInvariant(),
                    ForeColor = AccentCyan,
                    Font = new Font("Segoe UI", 7.25f, FontStyle.Bold),
                    Location = new Point(4, currentY),
                    AutoSize = true
                };
                _toolboxContent.Controls.Add(lblCat);
                currentY += 18;

                foreach (var item in group)
                {
                    var btn = new Button
                    {
                        Text = "+ " + item.DisplayName,
                        FlatStyle = FlatStyle.Flat,
                        ForeColor = TextPrimary,
                        BackColor = Color.FromArgb(32, 38, 52),
                        Font = new Font("Segoe UI", 8.0f, FontStyle.Regular),
                        Location = new Point(4, currentY),
                        Size = new Size(_toolboxContent.Width - 16, 26),
                        TextAlign = ContentAlignment.MiddleLeft,
                        Cursor = Cursors.Hand
                    };
                    btn.FlatAppearance.BorderSize = 1;
                    btn.FlatAppearance.BorderColor = Color.FromArgb(45, 55, 75);

                    var currentItem = item;
                    btn.Click += (s, e) => SpawnNodeFromPalette(currentItem);

                    _toolboxContent.Controls.Add(btn);
                    currentY += 30;
                }

                currentY += 8;
            }
        }

        private void SpawnNodeFromPalette(PaletteNodeItem item)
        {
            var node = item.CreateNode();
            // Stagger position
            int count = Canvas.Nodes.Count;
            node.X = 80f + (count % 5) * 40f;
            node.Y = 80f + (count % 5) * 30f;
            Canvas.AddNode(node);
        }

        private void RefreshInspector(CanvasNode? node)
        {
            _inspectorContent.Controls.Clear();

            if (node == null)
            {
                var lblEmpty = new Label
                {
                    Text = "No node selected.\n\nClick a node on the canvas to inspect its ports and parameters.",
                    ForeColor = TextMuted,
                    Dock = DockStyle.Top,
                    Height = 80
                };
                _inspectorContent.Controls.Add(lblEmpty);
                return;
            }

            int curY = 4;

            // Header Title
            var lblTitle = new Label
            {
                Text = node.Name,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = AccentCyan,
                Location = new Point(4, curY),
                AutoSize = true
            };
            _inspectorContent.Controls.Add(lblTitle);
            curY += 22;

            // Id & Type
            var lblMeta = new Label
            {
                Text = $"ID: {node.Id}\nType: {node.NodeType}\nCategory: {node.Category}",
                ForeColor = TextMuted,
                Font = new Font("Segoe UI", 7.5f, FontStyle.Regular),
                Location = new Point(4, curY),
                Size = new Size(210, 42)
            };
            _inspectorContent.Controls.Add(lblMeta);
            curY += 46;

            // Name edit
            var lblNamePrompt = new Label { Text = "Display Name:", ForeColor = TextPrimary, Location = new Point(4, curY), AutoSize = true };
            _inspectorContent.Controls.Add(lblNamePrompt);
            curY += 18;

            var txtName = new TextBox
            {
                Text = node.Name,
                Location = new Point(4, curY),
                Width = 200,
                BackColor = Color.FromArgb(32, 38, 52),
                ForeColor = TextPrimary,
                BorderStyle = BorderStyle.FixedSingle
            };
            txtName.TextChanged += (s, e) =>
            {
                node.Name = txtName.Text;
                lblTitle.Text = txtName.Text;
                Canvas.Invalidate();
            };
            _inspectorContent.Controls.Add(txtName);
            curY += 30;

            // Parameters section
            var lblParamHead = new Label
            {
                Text = "PARAMETERS",
                ForeColor = AccentCyan,
                Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
                Location = new Point(4, curY),
                AutoSize = true
            };
            _inspectorContent.Controls.Add(lblParamHead);
            curY += 20;

            foreach (var kvp in node.Properties.ToList())
            {
                var lblKey = new Label
                {
                    Text = kvp.Key,
                    ForeColor = TextPrimary,
                    Location = new Point(4, curY),
                    AutoSize = true
                };
                _inspectorContent.Controls.Add(lblKey);
                curY += 16;

                var txtVal = new TextBox
                {
                    Text = kvp.Value,
                    Location = new Point(4, curY),
                    Width = 200,
                    BackColor = Color.FromArgb(32, 38, 52),
                    ForeColor = TextPrimary,
                    BorderStyle = BorderStyle.FixedSingle
                };
                string key = kvp.Key;
                txtVal.TextChanged += (s, e) =>
                {
                    node.Properties[key] = txtVal.Text;
                };
                _inspectorContent.Controls.Add(txtVal);
                curY += 28;
            }

            // Ports Overview
            curY += 6;
            var lblPorts = new Label
            {
                Text = $"Inputs: {node.Inputs.Count} | Outputs: {node.Outputs.Count}",
                ForeColor = TextMuted,
                Location = new Point(4, curY),
                AutoSize = true
            };
            _inspectorContent.Controls.Add(lblPorts);
            curY += 24;

            // Delete button
            var btnDel = new Button
            {
                Text = "🗑 Delete Node",
                ForeColor = AccentRed,
                BackColor = Color.FromArgb(36, 42, 56),
                FlatStyle = FlatStyle.Flat,
                Location = new Point(4, curY),
                Width = 200,
                Height = 28,
                Cursor = Cursors.Hand
            };
            btnDel.FlatAppearance.BorderColor = AccentRed;
            btnDel.Click += (s, e) =>
            {
                Canvas.RemoveNode(node);
                RefreshInspector(null);
            };
            _inspectorContent.Controls.Add(btnDel);
        }

        private void RefreshInspectorForConnection(CanvasConnection? conn)
        {
            _inspectorContent.Controls.Clear();

            if (conn == null)
            {
                RefreshInspector(null);
                return;
            }

            int curY = 4;
            var lblTitle = new Label
            {
                Text = "CONNECTION",
                Font = new Font("Segoe UI", 9.0f, FontStyle.Bold),
                ForeColor = AccentCyan,
                Location = new Point(4, curY),
                AutoSize = true
            };
            _inspectorContent.Controls.Add(lblTitle);
            curY += 24;

            var lblDetails = new Label
            {
                Text = $"From:\n  {conn.SourcePin.OwnerNode.Name}.{conn.SourcePin.Name}\n\nTo:\n  {conn.TargetPin.OwnerNode.Name}.{conn.TargetPin.Name}\n\nData Type:\n  {conn.SourcePin.DataType.Name}",
                ForeColor = TextPrimary,
                Location = new Point(4, curY),
                Size = new Size(210, 140)
            };
            _inspectorContent.Controls.Add(lblDetails);
            curY += 150;

            var btnDel = new Button
            {
                Text = "🗑 Remove Wire",
                ForeColor = AccentRed,
                BackColor = Color.FromArgb(36, 42, 56),
                FlatStyle = FlatStyle.Flat,
                Location = new Point(4, curY),
                Width = 200,
                Height = 28,
                Cursor = Cursors.Hand
            };
            btnDel.FlatAppearance.BorderColor = AccentRed;
            btnDel.Click += (s, e) =>
            {
                Canvas.RemoveConnection(conn);
                RefreshInspector(null);
            };
            _inspectorContent.Controls.Add(btnDel);
        }

        public void AttachExecutor(PipelineExecutor executor)
        {
            if (_executor != null)
            {
                _executor.NodeExecuting -= OnNodeExecuting;
                _executor.NodeCompleted -= OnNodeCompleted;
                _executor.NodeFaulted -= OnNodeFaulted;
            }

            _executor = executor;

            if (_executor != null)
            {
                _executor.NodeExecuting += OnNodeExecuting;
                _executor.NodeCompleted += OnNodeCompleted;
                _executor.NodeFaulted += OnNodeFaulted;
            }
        }

        private void OnNodeExecuting(IPipelineNode node, PipelineContext ctx)
        {
            SafeInvoke(() =>
            {
                var cNode = Canvas.Nodes.FirstOrDefault(n => n.Id == node.Id || n.Name == node.Name);
                if (cNode != null)
                {
                    cNode.SetManualExecutionState(NodeState.Running);
                    Canvas.Invalidate();
                }
            });
        }

        private void OnNodeCompleted(IPipelineNode node, double durationMs)
        {
            SafeInvoke(() =>
            {
                var cNode = Canvas.Nodes.FirstOrDefault(n => n.Id == node.Id || n.Name == node.Name);
                if (cNode != null)
                {
                    cNode.SetManualExecutionState(NodeState.Completed, durationMs, cNode.ExecutionCount + 1);
                    Canvas.Invalidate();
                }
            });
        }

        private void OnNodeFaulted(IPipelineNode node, Exception ex)
        {
            SafeInvoke(() =>
            {
                var cNode = Canvas.Nodes.FirstOrDefault(n => n.Id == node.Id || n.Name == node.Name);
                if (cNode != null)
                {
                    cNode.SetManualExecutionState(NodeState.Faulted);
                    Canvas.Invalidate();
                }
            });
        }

        public async Task StepExecutionAsync()
        {
            if (_executor == null)
            {
                BuildExecutorFromCanvas();
            }

            if (_executor == null) return;

            try
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                await _executor.ExecuteStepAsync().ConfigureAwait(false);
                sw.Stop();

                _cycleCount++;
                SafeInvoke(() =>
                {
                    _lblStatus.Text = $"Status: Stepped | Cycles: {_cycleCount} | Last: {sw.Elapsed.TotalMilliseconds:F2} ms";
                    Canvas.Invalidate();
                });
            }
            catch (Exception ex)
            {
                SafeInvoke(() =>
                {
                    _lblStatus.Text = $"Status: Faulted ({ex.Message})";
                    Canvas.Invalidate();
                });
            }
        }

        public void ToggleRunContinuous()
        {
            if (_isRunningContinuous)
            {
                PauseExecution();
            }
            else
            {
                StartContinuousExecution();
            }
        }

        public void StartContinuousExecution()
        {
            if (_isRunningContinuous) return;

            if (_executor == null)
            {
                BuildExecutorFromCanvas();
            }

            if (_executor == null) return;

            _isRunningContinuous = true;
            _btnRun.Text = "⏹ Stop";
            _btnRun.BackColor = AccentRed;
            _continuousCts = new CancellationTokenSource();

            var token = _continuousCts.Token;
            Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    try
                    {
                        await _executor.ExecuteStepAsync(cancellationToken: token).ConfigureAwait(false);
                        _cycleCount++;
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        SafeInvoke(() =>
                        {
                            _lblStatus.Text = $"Fault: {ex.Message}";
                        });
                        break;
                    }

                    sw.Stop();
                    double elapsed = sw.Elapsed.TotalMilliseconds;

                    SafeInvoke(() =>
                    {
                        _lblStatus.Text = $"Status: Running | Cycles: {_cycleCount} | Step: {elapsed:F2} ms";
                    });

                    // Modest yield to keep UI responsive
                    await Task.Delay(10, token).ConfigureAwait(false);
                }

                SafeInvoke(() =>
                {
                    _isRunningContinuous = false;
                    _btnRun.Text = "▶ Run";
                    _btnRun.BackColor = Color.FromArgb(0, 200, 83);
                    _lblStatus.Text = $"Status: Stopped | Cycles: {_cycleCount}";
                    Canvas.Invalidate();
                });
            }, token);
        }

        public void PauseExecution()
        {
            if (_continuousCts != null)
            {
                _continuousCts.Cancel();
                _continuousCts.Dispose();
                _continuousCts = null;
            }

            _isRunningContinuous = false;
            _btnRun.Text = "▶ Run";
            _btnRun.BackColor = Color.FromArgb(0, 200, 83);
            _lblStatus.Text = $"Status: Paused | Cycles: {_cycleCount}";
        }

        public async Task ResetExecutionAsync()
        {
            PauseExecution();

            if (_executor != null)
            {
                await _executor.ResetAsync().ConfigureAwait(false);
            }

            _cycleCount = 0;
            foreach (var node in Canvas.Nodes)
            {
                node.SetManualExecutionState(NodeState.Idle, 0, 0);
            }

            SafeInvoke(() =>
            {
                _lblStatus.Text = "Status: Reset | Cycles: 0";
                Canvas.Invalidate();
            });
        }

        private void BuildExecutorFromCanvas()
        {
            try
            {
                var recipe = ExportToRecipe();
                var builder = new RecipeGraphBuilder();
                var graph = builder.BuildGraph(recipe);
                AttachExecutor(new PipelineExecutor(graph));
            }
            catch (Exception ex)
            {
                _lblStatus.Text = $"Build Error: {ex.Message}";
            }
        }

        public RecipeModel ExportToRecipe(string recipeName = "Canvas Recipe")
        {
            var recipe = new RecipeModel
            {
                Name = recipeName,
                CreatedAtUtc = DateTime.UtcNow
            };

            foreach (var node in Canvas.Nodes)
            {
                var nodeModel = new NodeRecipeModel
                {
                    Id = node.Id,
                    Name = node.Name,
                    NodeType = node.NodeType
                };

                // Store node coordinates in parameters for full visual state persistence
                nodeModel.Parameters["__X"] = node.X.ToString(CultureInfo.InvariantCulture);
                nodeModel.Parameters["__Y"] = node.Y.ToString(CultureInfo.InvariantCulture);

                foreach (var p in node.Properties)
                {
                    nodeModel.Parameters[p.Key] = p.Value;
                }

                recipe.Nodes.Add(nodeModel);
            }

            foreach (var conn in Canvas.Connections)
            {
                recipe.Connections.Add(new ConnectionRecipeModel
                {
                    SourceNodeId = conn.SourcePin.OwnerNode.Id,
                    SourcePortName = conn.SourcePin.Name,
                    TargetNodeId = conn.TargetPin.OwnerNode.Id,
                    TargetPortName = conn.TargetPin.Name
                });
            }

            return recipe;
        }

        public void LoadFromRecipe(RecipeModel recipe)
        {
            if (recipe == null) return;

            Canvas.Clear();
            var nodeMap = new Dictionary<string, CanvasNode>(StringComparer.OrdinalIgnoreCase);

            foreach (var nodeModel in recipe.Nodes)
            {
                float x = 100f, y = 100f;
                if (nodeModel.Parameters.TryGetValue("__X", out var xStr) && float.TryParse(xStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedX))
                    x = parsedX;
                if (nodeModel.Parameters.TryGetValue("__Y", out var yStr) && float.TryParse(yStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedY))
                    y = parsedY;

                // Lookup in palette to get pin signatures
                var paletteItem = AvailableNodes.FirstOrDefault(a => string.Equals(a.NodeType, nodeModel.NodeType, StringComparison.OrdinalIgnoreCase));
                CanvasNode cNode;
                if (paletteItem != null)
                {
                    cNode = paletteItem.CreateNode();
                    cNode.Id = nodeModel.Id;
                    cNode.Name = nodeModel.Name;
                    cNode.X = x;
                    cNode.Y = y;
                }
                else
                {
                    cNode = new CanvasNode(nodeModel.Id, nodeModel.Name, nodeModel.NodeType, "General", x, y);
                }

                // Copy user parameters
                foreach (var kvp in nodeModel.Parameters)
                {
                    if (kvp.Key.StartsWith("__")) continue;
                    cNode.Properties[kvp.Key] = kvp.Value;
                }

                Canvas.AddNode(cNode);
                nodeMap[cNode.Id] = cNode;
                if (!string.IsNullOrEmpty(cNode.Name))
                {
                    nodeMap[cNode.Name] = cNode;
                }
            }

            // Connect pins
            foreach (var connModel in recipe.Connections)
            {
                if (nodeMap.TryGetValue(connModel.SourceNodeId, out var srcNode) &&
                    nodeMap.TryGetValue(connModel.TargetNodeId, out var tgtNode))
                {
                    var srcPin = srcNode.Outputs.FirstOrDefault(p => string.Equals(p.Name, connModel.SourcePortName, StringComparison.OrdinalIgnoreCase));
                    var tgtPin = tgtNode.Inputs.FirstOrDefault(p => string.Equals(p.Name, connModel.TargetPortName, StringComparison.OrdinalIgnoreCase));

                    if (srcPin != null && tgtPin != null)
                    {
                        Canvas.Connect(srcPin, tgtPin);
                    }
                }
            }

            Canvas.ZoomToFit();
        }

        public string SaveRecipeJson()
        {
            var recipe = ExportToRecipe();
            return RecipeJsonSerializer.Serialize(recipe);
        }

        public void LoadRecipeJson(string json)
        {
            var recipe = RecipeJsonSerializer.Deserialize(json);
            LoadFromRecipe(recipe);
        }

        private void PromptSaveRecipe()
        {
            using var sfd = new SaveFileDialog
            {
                Filter = "Pipeline Recipe (*.recipe.json)|*.recipe.json|All Files (*.*)|*.*",
                FileName = "inspection_pipeline.recipe.json"
            };

            if (sfd.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    string json = SaveRecipeJson();
                    File.WriteAllText(sfd.FileName, json);
                    _lblStatus.Text = $"Saved: {Path.GetFileName(sfd.FileName)}";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error saving recipe: {ex.Message}", "Save Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void PromptLoadRecipe()
        {
            using var ofd = new OpenFileDialog
            {
                Filter = "Pipeline Recipe (*.recipe.json)|*.recipe.json|All Files (*.*)|*.*"
            };

            if (ofd.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    string json = File.ReadAllText(ofd.FileName);
                    LoadRecipeJson(json);
                    _lblStatus.Text = $"Loaded: {Path.GetFileName(ofd.FileName)}";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error loading recipe: {ex.Message}", "Load Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void SafeInvoke(Action action)
        {
            if (IsDisposed) return;
            if (InvokeRequired)
            {
                try { BeginInvoke(action); } catch { }
            }
            else
            {
                action();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                PauseExecution();
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// Descriptor for registered node palette items that can be spawned onto the canvas.
    /// </summary>
    public sealed class PaletteNodeItem
    {
        public string NodeType { get; }
        public string DisplayName { get; }
        public string Category { get; }
        public Color CategoryColor { get; }
        public Func<CanvasNode> CreateNode { get; }

        public PaletteNodeItem(string nodeType, string displayName, string category, Color categoryColor, Func<CanvasNode> createNode)
        {
            NodeType = nodeType;
            DisplayName = displayName;
            Category = category;
            CategoryColor = categoryColor;
            CreateNode = createNode;
        }

        public override string ToString() => $"{DisplayName} ({Category})";
    }
}
