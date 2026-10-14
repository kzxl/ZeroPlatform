using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;
using ZeroOcr.Core.Models;
using ZeroUI.Core.AiMl;
using ZeroUI.WinForms.Containers;
using ZeroUI.WinForms.Documents;
using ZeroUI.WinForms.Feedback;
using ZeroUI.WinForms.Media;
using ZeroUI.WinForms.Navigation;
using ZeroUI.WinForms.Theme;
using ZeroTabPage = ZeroUI.WinForms.Navigation.ZeroTabPage;
using ZeroTabControl = ZeroUI.WinForms.Navigation.ZeroTabControl;

namespace ZeroUI.Samples.WinformDemo.Forms
{
    public sealed partial class MainForm
    {
        private ZAiChatBox? _winformsChatBox;
        private ZOcrViewer? _winformsOcrViewer;
        private ZDocumentDeskew? _winformsDeskew;

        private System.Windows.Forms.Timer? _winformsAiTimer;
        private string? _winformsActiveStreamingId;
        private readonly Queue<string> _winformsPendingTokens = new();
        private CheckBox? _chkWpfAiFastMode;

        private Label? _lblOcrTokenText;
        private Label? _lblOcrTokenConfidence;
        private Label? _lblOcrTokenBounds;
        private Label? _lblDeskewAngleVal;
        private Label? _lblDeskewThresholdVal;
        private TrackBar? _tbDeskewAngle;
        private TrackBar? _tbDeskewThreshold;
        private CheckBox? _chkDeskewBinarize;

        private void InitializeAiOcrCluster(ZeroTabPage page)
        {
            var colors = ZeroTheme.Colors;
            page.BackColor = colors.Background;

            var mainContainer = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = colors.Background,
                Padding = new Padding(12)
            };

            // 1. Alert Banner
            var banner = new AlertBanner
            {
                Dock = DockStyle.Top,
                Height = 64,
                Severity = ZeroAlertSeverity.Info,
                Title = "🤖 PHASE 8: AI CHAT ASSISTANT & COMPUTER VISION STUDIO",
                Message = "Enterprise streaming AI copilot, high-precision OCR token inspection, interactive ROI selection, perspective document straightening, and adaptive threshold binarization."
            };
            mainContainer.Controls.Add(banner);

            var spacerBanner = new Panel { Dock = DockStyle.Top, Height = 8, BackColor = Color.Transparent };
            mainContainer.Controls.Add(spacerBanner);

            // 2. Tabbed Sub-Workspace
            var subTabs = new ZeroTabControl
            {
                Dock = DockStyle.Fill,
                Orientation = ZeroTabOrientation.Horizontal,
                TabHeight = 36,
                TabStyle = ZeroTabStyle.Pill
            };

            var tabAiChat = new ZeroTabPage("AI Assistant (ZeroCopilot)", "🤖", p => BuildAiChatTab(p));
            var tabOcrVision = new ZeroTabPage("OCR Vision & Document Deskew", "🔍", p => BuildOcrVisionTab(p));

            subTabs.AddTab(tabAiChat);
            subTabs.AddTab(tabOcrVision);

            mainContainer.Controls.Add(subTabs);
            page.Controls.Add(mainContainer);
        }

        #region AI Chat Tab

        private void BuildAiChatTab(ZeroTabPage page)
        {
            var colors = ZeroTheme.Colors;
            page.BackColor = colors.Background;

            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 860,
                SplitterWidth = 8,
                BackColor = colors.Border
            };
            split.Panel1.BackColor = colors.Background;
            split.Panel2.BackColor = colors.Surface;

            // --- Left: ZAiChatBox ---
            _winformsChatBox = new ZAiChatBox
            {
                Dock = DockStyle.Fill,
                AssistantName = "ZeroCopilot",
                ModelName = "ZeroInference Edge (FP16)"
            };

            _winformsChatBox.PromptSuggestions.Add("⚡ Analyze SCADA Telemetry Anomalies");
            _winformsChatBox.PromptSuggestions.Add("📑 Extract Invoice OCR Summary");
            _winformsChatBox.PromptSuggestions.Add("🔄 Query ISA-88 Batch State");
            _winformsChatBox.PromptSuggestions.Add("🚀 Benchmark ZeroGrid 10M Virtualization");

            var welcome = _winformsChatBox.AppendAssistantMessage(
                "Welcome to **ZeroUI AI Assistant (WinForms)**! 🤖\n\n" +
                "I am your edge-deployed reasoning copilot. You can inspect SCADA alarms, analyze document OCR extractions, or query live operations.\n\n" +
                "Try typing a prompt or clicking one of the suggested actions below!");
            welcome.IsStreaming = false;

            _winformsChatBox.SendMessageRequested += OnWinFormsChatSendMessageRequested;
            _winformsChatBox.StopGenerationRequested += (s, e) => StopWinFormsAiStreaming();
            _winformsChatBox.ClearChatRequested += (s, e) => StopWinFormsAiStreaming();

            split.Panel1.Controls.Add(_winformsChatBox);

            // --- Right: Telemetry & Actions Sidebar ---
            var pnlSidebar = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(16),
                AutoScroll = true
            };

            var lblTitle = new Label
            {
                Text = "⚡ AI REASONING WORKBENCH",
                Font = new Font(this.Font.FontFamily, 9f, FontStyle.Bold),
                ForeColor = colors.TextMuted,
                Dock = DockStyle.Top,
                Height = 22
            };
            pnlSidebar.Controls.Add(lblTitle);

            var lblDesc = new Label
            {
                Text = "ZeroUI ZAiChatBox provides low-latency token streaming, enterprise message bubbles, prompt suggestion chips, and responsive conversation layout for edge AI models.",
                Font = new Font(this.Font.FontFamily, 8.5f, FontStyle.Regular),
                ForeColor = colors.TextMuted,
                Dock = DockStyle.Top,
                Height = 52
            };
            pnlSidebar.Controls.Add(lblDesc);

            // Telemetry Group Card
            var pnlTelemetry = new Panel
            {
                Dock = DockStyle.Top,
                Height = 130,
                BackColor = colors.Background,
                Padding = new Padding(12),
                Margin = new Padding(0, 8, 0, 16)
            };
            pnlTelemetry.Paint += (s, e) =>
            {
                using var p = new Pen(colors.Border);
                e.Graphics.DrawRectangle(p, 0, 0, pnlTelemetry.Width - 1, pnlTelemetry.Height - 1);
            };

            var lblTelTitle = new Label
            {
                Text = "INFERENCE ENGINE STATUS",
                Font = new Font(this.Font.FontFamily, 8f, FontStyle.Bold),
                ForeColor = colors.Primary,
                Dock = DockStyle.Top,
                Height = 20
            };
            var lblRuntime = new Label
            {
                Text = "Runtime: Local ONNX / GGUF (FP16 DirectML)",
                Font = new Font(this.Font.FontFamily, 8f, FontStyle.Regular),
                ForeColor = colors.TextPrimary,
                Dock = DockStyle.Top,
                Height = 20
            };
            var lblSpeed = new Label
            {
                Text = "Stream Speed: ~42.5 tokens/sec (0.4ms TTFT)",
                Font = new Font(this.Font.FontFamily, 8f, FontStyle.Bold),
                ForeColor = colors.Success,
                Dock = DockStyle.Top,
                Height = 20
            };
            var lblBuffer = new Label
            {
                Text = "Ring Buffer: Zero-Alloc Token Chunks",
                Font = new Font(this.Font.FontFamily, 8f, FontStyle.Regular),
                ForeColor = colors.Primary,
                Dock = DockStyle.Top,
                Height = 20
            };
            pnlTelemetry.Controls.Add(lblBuffer);
            pnlTelemetry.Controls.Add(lblSpeed);
            pnlTelemetry.Controls.Add(lblRuntime);
            pnlTelemetry.Controls.Add(lblTelTitle);
            pnlSidebar.Controls.Add(pnlTelemetry);

            var spacerCard = new Panel { Dock = DockStyle.Top, Height = 12, BackColor = Color.Transparent };
            pnlSidebar.Controls.Add(spacerCard);

            // Action Buttons
            var lblActions = new Label
            {
                Text = "SIMULATION SCENARIOS",
                Font = new Font(this.Font.FontFamily, 8.5f, FontStyle.Bold),
                ForeColor = colors.TextMuted,
                Dock = DockStyle.Top,
                Height = 24
            };
            pnlSidebar.Controls.Add(lblActions);

            var btnScada = CreateSidebarButton("⚡ Analyze SCADA Telemetry", () =>
            {
                _winformsChatBox.AppendUserMessage("⚡ Analyze SCADA Telemetry Anomalies");
                OnWinFormsChatSendMessageRequested(_winformsChatBox, "Analyze SCADA Telemetry Anomalies");
            });
            var btnOcr = CreateSidebarButton("📑 Extract Invoice OCR Summary", () =>
            {
                _winformsChatBox.AppendUserMessage("📑 Extract Invoice OCR Summary");
                OnWinFormsChatSendMessageRequested(_winformsChatBox, "Extract Invoice OCR Summary");
            });
            var btnBatch = CreateSidebarButton("🔄 Query ISA-88 Batch State", () =>
            {
                _winformsChatBox.AppendUserMessage("🔄 Query ISA-88 Batch State");
                OnWinFormsChatSendMessageRequested(_winformsChatBox, "Query ISA-88 Batch State");
            });
            var btnClear = CreateSidebarButton("🧹 Clear Message History", () =>
            {
                _winformsChatBox.ClearMessages();
                StopWinFormsAiStreaming();
            });

            _chkWpfAiFastMode = new CheckBox
            {
                Text = "⚡ Fast Stream Mode (15ms)",
                ForeColor = colors.TextPrimary,
                Font = new Font(this.Font.FontFamily, 8.5f, FontStyle.Regular),
                Dock = DockStyle.Top,
                Height = 28
            };

            var chkLiveZFlow = new CheckBox
            {
                Text = "🔗 Live ZFlow (:8000)",
                ForeColor = colors.Primary,
                Font = new Font(this.Font.FontFamily, 8.5f, FontStyle.Bold),
                Dock = DockStyle.Top,
                Height = 28
            };
            chkLiveZFlow.CheckedChanged += (s, e) =>
            {
                if (_winformsChatBox == null) return;
                if (chkLiveZFlow.Checked)
                {
                    _winformsChatBox.ConfigureFlow(opt => opt
                        .WithEndpoint("http://127.0.0.1:8000", "erp-ContinuousLearning-Copilot-v1")
                        .WithIdentity(userId: "USER_001", userRole: "SALES")
                        .WithContextParameter("department", "SALES", "Dept")
                        .WithFeaturePermission("SO_CREATE", canView: true, canAdd: true, canEdit: true)
                        .WithContinuousLearningTools());
                }
                else
                {
                    _winformsChatBox.FlowProvider = null;
                }
            };

            pnlSidebar.Controls.Add(chkLiveZFlow);
            pnlSidebar.Controls.Add(_chkWpfAiFastMode);
            pnlSidebar.Controls.Add(btnClear);
            pnlSidebar.Controls.Add(btnBatch);
            pnlSidebar.Controls.Add(btnOcr);
            pnlSidebar.Controls.Add(btnScada);

            split.Panel2.Controls.Add(pnlSidebar);
            page.Controls.Add(split);
        }

        private Button CreateSidebarButton(string text, Action onClick)
        {
            var colors = ZeroTheme.Colors;
            var btn = new Button
            {
                Text = text,
                Dock = DockStyle.Top,
                Height = 34,
                FlatStyle = FlatStyle.Flat,
                BackColor = colors.Background,
                ForeColor = colors.TextPrimary,
                Font = new Font(this.Font.FontFamily, 8.5f, FontStyle.Regular),
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 0, 0, 6),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderColor = colors.Border;
            btn.Click += (s, e) => onClick();
            return btn;
        }

        private void OnWinFormsChatSendMessageRequested(object? sender, string prompt)
        {
            if (_winformsChatBox == null) return;
            StopWinFormsAiStreaming();

            var botMsg = _winformsChatBox.AppendAssistantMessage();
            _winformsActiveStreamingId = botMsg.Id;

            _winformsChatBox.IsGenerating = true;
            _winformsChatBox.StreamingState = ChatStreamingState.Streaming;

            string fullResponse = GenerateMockWinFormsAiResponse(prompt);
            string[] words = fullResponse.Split(new[] { ' ' }, StringSplitOptions.None);
            _winformsPendingTokens.Clear();
            for (int i = 0; i < words.Length; i++)
            {
                string token = words[i] + (i < words.Length - 1 ? " " : "");
                _winformsPendingTokens.Enqueue(token);
            }

            int intervalMs = _chkWpfAiFastMode?.Checked == true ? 15 : 30;
            _winformsAiTimer = new System.Windows.Forms.Timer
            {
                Interval = intervalMs
            };
            _winformsAiTimer.Tick += (s, e) =>
            {
                if (_winformsPendingTokens.Count > 0 && _winformsActiveStreamingId != null)
                {
                    string chunk = _winformsPendingTokens.Dequeue();
                    _winformsChatBox.StreamToken(_winformsActiveStreamingId, chunk);
                }
                else
                {
                    StopWinFormsAiStreaming();
                }
            };
            _winformsAiTimer.Start();
        }

        private void StopWinFormsAiStreaming()
        {
            if (_winformsAiTimer != null)
            {
                _winformsAiTimer.Stop();
                _winformsAiTimer.Dispose();
                _winformsAiTimer = null;
            }

            if (_winformsChatBox != null && _winformsActiveStreamingId != null)
            {
                _winformsChatBox.CompleteStreaming(_winformsActiveStreamingId);
                _winformsActiveStreamingId = null;
                _winformsChatBox.IsGenerating = false;
                _winformsChatBox.StreamingState = ChatStreamingState.Completed;
            }

            _winformsPendingTokens.Clear();
        }

        private static string GenerateMockWinFormsAiResponse(string prompt)
        {
            string p = prompt.ToLowerInvariant();
            if (p.Contains("scada") || p.Contains("telemetry") || p.Contains("anomal"))
            {
                return "⚡ **SCADA Telemetry Analysis Complete**:\n\n" +
                       "- **Reactor Temp (R-101)**: 84.6°C (Target: 85°C, stability 99.4%)\n" +
                       "- **Feed Pump P-201**: Flow rate 142 L/min, pressure nominal at 4.2 bar.\n" +
                       "- **Vibration Sensors**: FFT spectrum confirms zero harmonic resonance.\n\n" +
                       "✅ **Diagnosis**: All 16 automation nodes are operating within ISA-18.2 Class A envelopes. Zero warnings logged.";
            }

            if (p.Contains("ocr") || p.Contains("invoice") || p.Contains("document"))
            {
                return "📑 **ZeroOCR Extraction Summary**:\n\n" +
                       "- **Document Type**: Commercial Tax Invoice\n" +
                       "- **Invoice No**: `INV-2026-09-881` (Confidence: 98.5%)\n" +
                       "- **Customer**: ACME Industrial Automation Corp\n" +
                       "- **Line Items Detected**:\n" +
                       "  1. ZeroGrid High-Performance Engine 10M — $4,200.00\n" +
                       "  2. SCADA Edge Telemetry Gateway — $1,850.00\n" +
                       "  3. ZeroOCR Vision & Deskew Processor — $1,500.00\n" +
                       "- **Total Payable**: **$8,305.00 USD** (Subtotal: $7,550.00, VAT 10%: $755.00)\n\n" +
                       "✨ All 14 token bounding boxes aligned with zero skew.";
            }

            if (p.Contains("batch") || p.Contains("isa-88") || p.Contains("state"))
            {
                return "🔄 **ISA-88 Batch Execution Status**:\n\n" +
                       "- **Current Unit Procedure**: `UP-CRYSTALLIZE-04`\n" +
                       "- **Current Operation**: `OP-PRECIPITATION-B`\n" +
                       "- **Current Phase**: `PHASE-AGITATE-AND-COOL` (Step 3/6, Active 00:14:22)\n" +
                       "- **Interlocks**: All 8 safety permissives verified.\n\n" +
                       "Next transition scheduled in 2 min 40 sec upon reaching temperature setpoint 22.0°C.";
            }

            if (p.Contains("zerogrid") || p.Contains("grid") || p.Contains("10m") || p.Contains("benchmark"))
            {
                return "🚀 **ZeroGrid 10M Virtualization Benchmark Metrics**:\n\n" +
                       "- **Total Dataset Size**: 10,000,000 rows x 15 columns in memory.\n" +
                       "- **Visible Viewport Window**: 28 items dynamically rendered.\n" +
                       "- **Allocation Rate**: **0 bytes/frame** during continuous 100K row scrolls.\n" +
                       "- **Render Latency**: **0.42 ms** (capped at 60+ FPS smooth lock).\n\n" +
                       "ZeroUI uses custom direct GDI+ rendering bypassing standard DataGridView cell overhead.";
            }

            return $"Analyzed input: \"{prompt}\".\n\n" +
                   "ZeroUI Edge AI Engine completed localized inference in **18 ms**.\n" +
                   "Data pipelines and interactive controls are synchronized and ready.";
        }

        #endregion

        #region OCR Vision & Deskew Tab

        private void BuildOcrVisionTab(ZeroTabPage page)
        {
            var colors = ZeroTheme.Colors;
            page.BackColor = colors.Background;

            var container = new Panel { Dock = DockStyle.Fill, BackColor = colors.Background };

            // 1. Top Toolbar
            var pnlToolbar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 44,
                BackColor = colors.Surface,
                Padding = new Padding(12, 6, 12, 6)
            };
            pnlToolbar.Paint += (s, e) =>
            {
                using var p = new Pen(colors.Border);
                e.Graphics.DrawLine(p, 0, pnlToolbar.Height - 1, pnlToolbar.Width, pnlToolbar.Height - 1);
            };

            var chkBoxes = new CheckBox
            {
                Text = "Bounding Boxes",
                Checked = true,
                ForeColor = colors.TextPrimary,
                AutoSize = true,
                Location = new Point(14, 12),
                Cursor = Cursors.Hand
            };
            var chkBadges = new CheckBox
            {
                Text = "Confidence Badges",
                Checked = true,
                ForeColor = colors.TextPrimary,
                AutoSize = true,
                Location = new Point(140, 12),
                Cursor = Cursors.Hand
            };
            var chkText = new CheckBox
            {
                Text = "Text Overlay",
                Checked = false,
                ForeColor = colors.TextPrimary,
                AutoSize = true,
                Location = new Point(285, 12),
                Cursor = Cursors.Hand
            };

            var btnZoomIn = new Button
            {
                Text = "🔍+ In",
                Size = new Size(58, 28),
                Location = new Point(410, 8),
                FlatStyle = FlatStyle.Flat,
                ForeColor = colors.TextPrimary,
                BackColor = colors.Background,
                Cursor = Cursors.Hand
            };
            btnZoomIn.FlatAppearance.BorderColor = colors.Border;

            var btnZoomOut = new Button
            {
                Text = "🔍- Out",
                Size = new Size(62, 28),
                Location = new Point(474, 8),
                FlatStyle = FlatStyle.Flat,
                ForeColor = colors.TextPrimary,
                BackColor = colors.Background,
                Cursor = Cursors.Hand
            };
            btnZoomOut.FlatAppearance.BorderColor = colors.Border;

            var btnZoomFit = new Button
            {
                Text = "📐 Fit",
                Size = new Size(55, 28),
                Location = new Point(542, 8),
                FlatStyle = FlatStyle.Flat,
                ForeColor = colors.TextPrimary,
                BackColor = colors.Background,
                Cursor = Cursors.Hand
            };
            btnZoomFit.FlatAppearance.BorderColor = colors.Border;

            var btnClearSel = new Button
            {
                Text = "🧹 Clear",
                Size = new Size(68, 28),
                Location = new Point(603, 8),
                FlatStyle = FlatStyle.Flat,
                ForeColor = colors.TextPrimary,
                BackColor = colors.Background,
                Cursor = Cursors.Hand
            };
            btnClearSel.FlatAppearance.BorderColor = colors.Border;

            pnlToolbar.Controls.Add(chkBoxes);
            pnlToolbar.Controls.Add(chkBadges);
            pnlToolbar.Controls.Add(chkText);
            pnlToolbar.Controls.Add(btnZoomIn);
            pnlToolbar.Controls.Add(btnZoomOut);
            pnlToolbar.Controls.Add(btnZoomFit);
            pnlToolbar.Controls.Add(btnClearSel);
            container.Controls.Add(pnlToolbar);

            // 2. Center Workspace Split
            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 780,
                SplitterWidth = 8,
                BackColor = colors.Border
            };
            split.Panel1.BackColor = colors.Background;
            split.Panel2.BackColor = colors.Surface;

            // --- Panel 1: ZOcrViewer & Token Inspector Strip ---
            var pnlOcrViewerHost = new Panel { Dock = DockStyle.Fill, BackColor = colors.Background };

            _winformsOcrViewer = new ZOcrViewer
            {
                Dock = DockStyle.Fill,
                ShowBoundingBoxes = true,
                ShowConfidenceBadges = true,
                ShowTextOverlay = false,
                MinConfidenceThreshold = 0.70f
            };

            var docBmp = CreateWinFormsSampleDocumentBitmap(skewed: false);
            var ocrResult = CreateWinFormsSampleOcrResult();
            _winformsOcrViewer.Image = docBmp;
            _winformsOcrViewer.Result = ocrResult;

            // Inspector Bottom Strip
            var pnlTokenStrip = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 44,
                BackColor = colors.Surface,
                Padding = new Padding(12, 6, 12, 6)
            };
            pnlTokenStrip.Paint += (s, e) =>
            {
                using var p = new Pen(colors.Border);
                e.Graphics.DrawLine(p, 0, 0, pnlTokenStrip.Width, 0);
            };

            var lblPrompt = new Label
            {
                Text = "Token:",
                ForeColor = colors.TextMuted,
                AutoSize = true,
                Location = new Point(12, 14),
                Font = new Font(this.Font.FontFamily, 8.5f, FontStyle.Regular)
            };
            _lblOcrTokenText = new Label
            {
                Text = "(Click a token or drag ROI)",
                ForeColor = colors.Primary,
                Font = new Font(this.Font.FontFamily, 8.5f, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(56, 14)
            };
            var lblConfHeader = new Label
            {
                Text = "Confidence:",
                ForeColor = colors.TextMuted,
                AutoSize = true,
                Location = new Point(280, 14),
                Font = new Font(this.Font.FontFamily, 8.5f, FontStyle.Regular)
            };
            _lblOcrTokenConfidence = new Label
            {
                Text = "--",
                ForeColor = colors.TextPrimary,
                Font = new Font(this.Font.FontFamily, 8.5f, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(355, 14)
            };
            var lblBoundsHeader = new Label
            {
                Text = "Bounds:",
                ForeColor = colors.TextMuted,
                AutoSize = true,
                Location = new Point(410, 14),
                Font = new Font(this.Font.FontFamily, 8.5f, FontStyle.Regular)
            };
            _lblOcrTokenBounds = new Label
            {
                Text = "--",
                ForeColor = colors.TextMuted,
                Font = new Font(this.Font.FontFamily, 8f, FontStyle.Regular),
                AutoSize = true,
                Location = new Point(460, 14)
            };

            var btnCopyText = new Button
            {
                Text = "📋 Copy Text",
                Dock = DockStyle.Right,
                Width = 100,
                FlatStyle = FlatStyle.Flat,
                ForeColor = colors.TextPrimary,
                BackColor = colors.Background,
                Cursor = Cursors.Hand
            };
            btnCopyText.FlatAppearance.BorderColor = colors.Border;
            btnCopyText.Click += (s, e) =>
            {
                string? text = _winformsOcrViewer.GetSelectedText();
                if (string.IsNullOrEmpty(text))
                {
                    text = _winformsOcrViewer.GetAllText();
                }

                if (!string.IsNullOrEmpty(text))
                {
                    Clipboard.SetText(text);
                    MessageBox.Show($"Copied {text.Length} characters to clipboard:\n\n{text}", "ZeroOCR Text Copied", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };

            pnlTokenStrip.Controls.Add(lblPrompt);
            pnlTokenStrip.Controls.Add(_lblOcrTokenText);
            pnlTokenStrip.Controls.Add(lblConfHeader);
            pnlTokenStrip.Controls.Add(_lblOcrTokenConfidence);
            pnlTokenStrip.Controls.Add(lblBoundsHeader);
            pnlTokenStrip.Controls.Add(_lblOcrTokenBounds);
            pnlTokenStrip.Controls.Add(btnCopyText);

            pnlOcrViewerHost.Controls.Add(_winformsOcrViewer);
            pnlOcrViewerHost.Controls.Add(pnlTokenStrip);
            split.Panel1.Controls.Add(pnlOcrViewerHost);

            // Wire Toolbar Actions
            chkBoxes.CheckedChanged += (s, e) => _winformsOcrViewer.ShowBoundingBoxes = chkBoxes.Checked;
            chkBadges.CheckedChanged += (s, e) => _winformsOcrViewer.ShowConfidenceBadges = chkBadges.Checked;
            chkText.CheckedChanged += (s, e) => _winformsOcrViewer.ShowTextOverlay = chkText.Checked;
            btnZoomIn.Click += (s, e) => _winformsOcrViewer.Zoom = Math.Min(5.0, _winformsOcrViewer.Zoom + 0.25);
            btnZoomOut.Click += (s, e) => _winformsOcrViewer.Zoom = Math.Max(0.25, _winformsOcrViewer.Zoom - 0.25);
            btnZoomFit.Click += (s, e) => _winformsOcrViewer.ZoomToFit();
            btnClearSel.Click += (s, e) =>
            {
                _winformsOcrViewer.ClearSelection();
                _lblOcrTokenText.Text = "(Click a token or drag ROI)";
                _lblOcrTokenConfidence.Text = "--";
                _lblOcrTokenBounds.Text = "--";
            };

            _winformsOcrViewer.WordSelected += (s, word) =>
            {
                if (word != null)
                {
                    _lblOcrTokenText.Text = $"\"{word.Text}\"";
                    _lblOcrTokenConfidence.Text = $"{(int)(word.Confidence * 100)}%";
                    _lblOcrTokenBounds.Text = $"[{word.BoundingBox.X}, {word.BoundingBox.Y}, {word.BoundingBox.Width}x{word.BoundingBox.Height}]";
                }
            };

            // --- Panel 2: ZDocumentDeskew & Controls ---
            var pnlDeskewHost = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = colors.Surface,
                Padding = new Padding(14)
            };

            var lblDeskewTitle = new Label
            {
                Text = "DOCUMENT DESKEW & BINARIZATION",
                Font = new Font(this.Font.FontFamily, 9f, FontStyle.Bold),
                ForeColor = colors.TextMuted,
                Dock = DockStyle.Top,
                Height = 22
            };
            var lblDeskewDesc = new Label
            {
                Text = "Hardware-accelerated perspective deskew and adaptive threshold binarization for document preprocessing.",
                Font = new Font(this.Font.FontFamily, 8f, FontStyle.Regular),
                ForeColor = colors.TextMuted,
                Dock = DockStyle.Top,
                Height = 36
            };

            // Deskew Viewer
            var pnlDeskewCanvas = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = colors.Background,
                Padding = new Padding(4)
            };
            pnlDeskewCanvas.Paint += (s, e) =>
            {
                using var p = new Pen(colors.Border);
                e.Graphics.DrawRectangle(p, 0, 0, pnlDeskewCanvas.Width - 1, pnlDeskewCanvas.Height - 1);
            };

            _winformsDeskew = new ZDocumentDeskew
            {
                Dock = DockStyle.Fill,
                DeskewAngle = -8.5,
                Threshold = 128
            };
            var skewedBmp = CreateWinFormsSampleDocumentBitmap(skewed: true, skewAngle: -8.5);
            _winformsDeskew.Image = skewedBmp;
            pnlDeskewCanvas.Controls.Add(_winformsDeskew);

            // Controls below deskew
            var pnlDeskewControls = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 160,
                BackColor = colors.Surface,
                Padding = new Padding(0, 8, 0, 0)
            };

            var pnlAngleHeader = new Panel { Dock = DockStyle.Top, Height = 20 };
            var lblAngleName = new Label { Text = "Deskew Angle:", ForeColor = colors.TextMuted, AutoSize = true, Dock = DockStyle.Left };
            _lblDeskewAngleVal = new Label { Text = "-8.5°", ForeColor = colors.Primary, Font = new Font(this.Font.FontFamily, 8.5f, FontStyle.Bold), AutoSize = true, Dock = DockStyle.Right };
            pnlAngleHeader.Controls.Add(lblAngleName);
            pnlAngleHeader.Controls.Add(_lblDeskewAngleVal);

            _tbDeskewAngle = new TrackBar
            {
                Dock = DockStyle.Top,
                Height = 30,
                Minimum = -30,
                Maximum = 30,
                Value = -8,
                TickFrequency = 5
            };
            _tbDeskewAngle.ValueChanged += (s, e) =>
            {
                _winformsDeskew.DeskewAngle = _tbDeskewAngle.Value;
                _lblDeskewAngleVal.Text = $"{_tbDeskewAngle.Value:F1}°";
            };

            var pnlThHeader = new Panel { Dock = DockStyle.Top, Height = 22 };
            _chkDeskewBinarize = new CheckBox
            {
                Text = "Adaptive Threshold Binarization",
                ForeColor = colors.TextPrimary,
                AutoSize = true,
                Dock = DockStyle.Left,
                Checked = false
            };
            _lblDeskewThresholdVal = new Label { Text = "128", ForeColor = colors.TextMuted, AutoSize = true, Dock = DockStyle.Right };
            pnlThHeader.Controls.Add(_chkDeskewBinarize);
            pnlThHeader.Controls.Add(_lblDeskewThresholdVal);

            _tbDeskewThreshold = new TrackBar
            {
                Dock = DockStyle.Top,
                Height = 30,
                Minimum = 10,
                Maximum = 245,
                Value = 128,
                TickFrequency = 20
            };
            _tbDeskewThreshold.ValueChanged += (s, e) =>
            {
                byte val = (byte)Math.Max(0, Math.Min(255, _tbDeskewThreshold.Value));
                _winformsDeskew.Threshold = val;
                _lblDeskewThresholdVal.Text = val.ToString();
            };
            _chkDeskewBinarize.CheckedChanged += (s, e) =>
            {
                _winformsDeskew.IsBinarizationEnabled = _chkDeskewBinarize.Checked;
            };

            var pnlDeskewBtns = new Panel { Dock = DockStyle.Top, Height = 34, Padding = new Padding(0, 4, 0, 0) };
            var btnAutoStraighten = new Button
            {
                Text = "📐 Auto Straighten",
                Width = 140,
                Dock = DockStyle.Left,
                FlatStyle = FlatStyle.Flat,
                ForeColor = colors.TextPrimary,
                BackColor = colors.Background,
                Cursor = Cursors.Hand
            };
            btnAutoStraighten.FlatAppearance.BorderColor = colors.Border;
            btnAutoStraighten.Click += (s, e) =>
            {
                _tbDeskewAngle.Value = 0;
            };

            var btnSendToOcr = new Button
            {
                Text = "🚀 Send to OCR",
                Dock = DockStyle.Fill,
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                BackColor = colors.Primary,
                Font = new Font(this.Font.FontFamily, 8.5f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Margin = new Padding(8, 0, 0, 0)
            };
            btnSendToOcr.FlatAppearance.BorderSize = 0;
            btnSendToOcr.Click += (s, e) =>
            {
                var processed = _winformsDeskew.GetProcessedBitmap();
                if (processed != null)
                {
                    _winformsOcrViewer.Image = processed;
                    MessageBox.Show("Straightened document transferred to ZeroOCR Viewer!", "ZeroOCR Deskew Pipeline", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };

            pnlDeskewBtns.Controls.Add(btnSendToOcr);
            var spacerBtn = new Panel { Dock = DockStyle.Left, Width = 8 };
            pnlDeskewBtns.Controls.Add(spacerBtn);
            pnlDeskewBtns.Controls.Add(btnAutoStraighten);

            pnlDeskewControls.Controls.Add(pnlDeskewBtns);
            pnlDeskewControls.Controls.Add(_tbDeskewThreshold);
            pnlDeskewControls.Controls.Add(pnlThHeader);
            pnlDeskewControls.Controls.Add(_tbDeskewAngle);
            pnlDeskewControls.Controls.Add(pnlAngleHeader);

            pnlDeskewHost.Controls.Add(pnlDeskewCanvas);
            pnlDeskewHost.Controls.Add(pnlDeskewControls);
            pnlDeskewHost.Controls.Add(lblDeskewDesc);
            pnlDeskewHost.Controls.Add(lblDeskewTitle);

            split.Panel2.Controls.Add(pnlDeskewHost);
            container.Controls.Add(split);
            page.Controls.Add(container);
        }

        private static Bitmap CreateWinFormsSampleDocumentBitmap(bool skewed, double skewAngle = 0.0)
        {
            int width = 720;
            int height = 460;
            var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                g.Clear(Color.FromArgb(248, 250, 252));

                if (skewed && Math.Abs(skewAngle) > 0.01)
                {
                    g.TranslateTransform(width / 2.0f, height / 2.0f);
                    g.RotateTransform((float)skewAngle);
                    g.TranslateTransform(-width / 2.0f, -height / 2.0f);
                }

                // Border & background
                using (var penBorder = new Pen(Color.FromArgb(203, 213, 225), 1.5f))
                {
                    g.DrawRectangle(penBorder, 10, 10, width - 20, height - 20);
                }

                // Header bar
                using (var brushHeader = new SolidBrush(Color.FromArgb(30, 41, 59)))
                {
                    g.FillRectangle(brushHeader, 10, 10, width - 20, 50);
                }

                using var fontHeader = new Font("Segoe UI", 13f, FontStyle.Bold);
                using var fontBold = new Font("Segoe UI", 9.5f, FontStyle.Bold);
                using var fontBody = new Font("Segoe UI", 9.5f, FontStyle.Regular);
                using var fontLarge = new Font("Segoe UI", 11f, FontStyle.Bold);

                using var brushWhite = new SolidBrush(Color.White);
                using var brushDark = new SolidBrush(Color.FromArgb(30, 41, 59));
                using var brushSlate = new SolidBrush(Color.FromArgb(71, 85, 105));
                using var brushGreen = new SolidBrush(Color.FromArgb(16, 185, 129));
                using var brushBlack = new SolidBrush(Color.Black);

                g.DrawString("ZERO PLATFORM ENTERPRISE - TAX INVOICE", fontHeader, brushWhite, 30, 24);
                g.DrawString("INVOICE NO: INV-2026-09-881", fontBold, brushDark, 30, 78);
                g.DrawString("DATE: 2026-09-30", fontBold, brushDark, 420, 78);
                g.DrawString("CUSTOMER: ACME INDUSTRIAL AUTOMATION CORP", fontBody, brushSlate, 30, 108);

                // Table Header
                using (var brushThBg = new SolidBrush(Color.FromArgb(241, 245, 249)))
                {
                    g.FillRectangle(brushThBg, 30, 138, width - 60, 28);
                }
                g.DrawString("ITEM DESCRIPTION", fontBold, brushSlate, 40, 144);
                g.DrawString("QTY", fontBold, brushSlate, 380, 144);
                g.DrawString("UNIT PRICE", fontBold, brushSlate, 460, 144);
                g.DrawString("AMOUNT", fontBold, brushSlate, 580, 144);

                // Line items
                g.DrawString("ZeroGrid High-Performance Engine 10M", fontBody, brushBlack, 40, 178);
                g.DrawString("1", fontBody, brushBlack, 390, 178);
                g.DrawString("$4,200.00", fontBody, brushBlack, 465, 178);
                g.DrawString("$4,200.00", fontBold, brushBlack, 585, 178);

                g.DrawString("SCADA Edge Telemetry Gateway OPC", fontBody, brushBlack, 40, 212);
                g.DrawString("2", fontBody, brushBlack, 390, 212);
                g.DrawString("$925.00", fontBody, brushBlack, 470, 212);
                g.DrawString("$1,850.00", fontBold, brushBlack, 585, 212);

                g.DrawString("ZeroOCR Vision & Deskew Processor", fontBody, brushBlack, 40, 246);
                g.DrawString("1", fontBody, brushBlack, 390, 246);
                g.DrawString("$1,500.00", fontBody, brushBlack, 465, 246);
                g.DrawString("$1,500.00", fontBold, brushBlack, 585, 246);

                // Divider
                using (var penLine = new Pen(Color.FromArgb(203, 213, 225), 1))
                {
                    g.DrawLine(penLine, 30, 285, width - 30, 285);
                }

                // Totals
                g.DrawString("SUBTOTAL:", fontBold, brushSlate, 460, 300);
                g.DrawString("$7,550.00", fontBold, brushBlack, 585, 300);

                g.DrawString("VAT TAX (10%):", fontBold, brushSlate, 460, 328);
                g.DrawString("$755.00", fontBold, brushBlack, 585, 328);

                g.DrawString("TOTAL PAYABLE:", fontLarge, brushGreen, 430, 362);
                g.DrawString("$8,305.00 USD", fontLarge, brushGreen, 565, 362);

                g.DrawString("STATUS: VERIFIED & CLEARED", fontBold, brushSlate, 30, 410);
            }
            return bmp;
        }

        private static OcrResult CreateWinFormsSampleOcrResult()
        {
            var lines = new List<OcrLine>();

            // Line 1: Header
            var wTitle1 = new OcrWord("ZERO", new OcrRect(30, 24, 48, 22), 0.99f);
            var wTitle2 = new OcrWord("PLATFORM", new OcrRect(84, 24, 96, 22), 0.98f);
            var wTitle3 = new OcrWord("INVOICE", new OcrRect(300, 24, 75, 22), 0.97f);
            lines.Add(new OcrLine("ZERO PLATFORM INVOICE", new[] { wTitle1, wTitle2, wTitle3 }));

            // Line 2: Invoice No
            var wInv1 = new OcrWord("INVOICE", new OcrRect(30, 78, 65, 18), 0.99f);
            var wInv2 = new OcrWord("NO:", new OcrRect(98, 78, 28, 18), 0.98f);
            var wInv3 = new OcrWord("INV-2026-09-881", new OcrRect(130, 78, 125, 18), 0.96f);
            lines.Add(new OcrLine("INVOICE NO: INV-2026-09-881", new[] { wInv1, wInv2, wInv3 }));

            // Line 3: Date
            var wDate1 = new OcrWord("DATE:", new OcrRect(420, 78, 45, 18), 0.97f);
            var wDate2 = new OcrWord("2026-09-30", new OcrRect(470, 78, 85, 18), 0.98f);
            lines.Add(new OcrLine("DATE: 2026-09-30", new[] { wDate1, wDate2 }));

            // Line 4: Item 1
            var wItm1 = new OcrWord("ZeroGrid", new OcrRect(40, 178, 65, 18), 0.95f);
            var wItm2 = new OcrWord("High-Performance", new OcrRect(110, 178, 120, 18), 0.92f);
            var wItm3 = new OcrWord("$4,200.00", new OcrRect(585, 178, 75, 18), 0.98f);
            lines.Add(new OcrLine("ZeroGrid High-Performance $4,200.00", new[] { wItm1, wItm2, wItm3 }));

            // Line 5: Item 2
            var wScada1 = new OcrWord("SCADA", new OcrRect(40, 212, 55, 18), 0.94f);
            var wScada2 = new OcrWord("Gateway", new OcrRect(145, 212, 65, 18), 0.88f);
            var wScada3 = new OcrWord("$1,850.00", new OcrRect(585, 212, 75, 18), 0.96f);
            lines.Add(new OcrLine("SCADA Gateway $1,850.00", new[] { wScada1, wScada2, wScada3 }));

            // Line 6: Item 3
            var wOcr1 = new OcrWord("ZeroOCR", new OcrRect(40, 246, 68, 18), 0.93f);
            var wOcr2 = new OcrWord("Processor", new OcrRect(165, 246, 75, 18), 0.89f);
            var wOcr3 = new OcrWord("$1,500.00", new OcrRect(585, 246, 75, 18), 0.97f);
            lines.Add(new OcrLine("ZeroOCR Processor $1,500.00", new[] { wOcr1, wOcr2, wOcr3 }));

            // Line 7: Totals
            var wTot1 = new OcrWord("TOTAL", new OcrRect(430, 362, 55, 20), 0.99f);
            var wTot2 = new OcrWord("PAYABLE:", new OcrRect(490, 362, 70, 20), 0.97f);
            var wTot3 = new OcrWord("$8,305.00", new OcrRect(565, 362, 85, 20), 0.99f);
            var wTot4 = new OcrWord("USD", new OcrRect(655, 362, 38, 20), 0.72f);
            lines.Add(new OcrLine("TOTAL PAYABLE: $8,305.00 USD", new[] { wTot1, wTot2, wTot3, wTot4 }));

            return OcrResult.Create(lines, TimeSpan.FromMilliseconds(38), "en");
        }

        #endregion
    }
}
