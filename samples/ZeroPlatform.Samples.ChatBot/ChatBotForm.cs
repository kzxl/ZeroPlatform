using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using ZeroAgent.Dialog.DST;
using ZeroAgent.Tools.Safety;
using ZeroUI.Core.AiMl;
using ZeroUI.WinForms.Containers;
using ZeroUI.WinForms.Documents;
using ZeroUI.WinForms.Feedback;
using ZeroUI.WinForms.Layout;
using ZeroUI.WinForms.Navigation;
using ZeroUI.WinForms.Theme;
using ZeroTabPage = ZeroUI.WinForms.Navigation.ZeroTabPage;
using ZeroTabControl = ZeroUI.WinForms.Navigation.ZeroTabControl;
using ZeroCard = ZeroUI.WinForms.Containers.ZeroCard;
using ZeroDescriptions = ZeroUI.WinForms.Containers.ZeroDescriptions;

namespace ZeroPlatform.Samples.ChatBot
{
    public sealed class ChatBotForm : Form
    {
        private readonly ChatBotEnvironment _env;
        private readonly string _sessionId = "operator-session-01";

        // UI Controls
        private ZAiChatBox? _chatBox;
        private Label? _lblIntent;
        private Label? _lblConfidence;
        private Label? _lblDstState;
        private Label? _lblActiveEntity;
        private Label? _lblLatency;
        private ListBox? _lstSlots;
        
        // HITL Panel
        private Panel? _pnlHitlAlert;
        private Label? _lblHitlMessage;
        private Button? _btnApprove;
        private Button? _btnReject;
        private HitlApprovalRequest? _activeHitlRequest;
        private ListBox? _lstAuditLog;

        // Tool Log & Memory
        private ListBox? _lstToolInvocations;
        private TextBox? _txtToolDetail;
        private ListBox? _lstSemanticMemory;
        private TextBox? _txtMemoryDetail;

        public ChatBotForm()
        {
            _env = new ChatBotEnvironment();

            Text = "ZeroPlatform - Autonomous Industrial AI Copilot & Chatbot Studio";
            Size = new Size(1380, 850);
            MinimumSize = new Size(1100, 700);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(15, 23, 42); // Slate 900
            ForeColor = Color.FromArgb(241, 245, 249);
            Font = new Font("Segoe UI", 9f, FontStyle.Regular);

            InitializeComponents();
            WireEnvironmentEvents();
            LoadInitialMemoryData();
        }

        private void InitializeComponents()
        {
            // 1. Top Header Banner
            var pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 64,
                BackColor = Color.FromArgb(30, 41, 59),
                Padding = new Padding(16, 10, 16, 10)
            };

            var lblLogo = new Label
            {
                Text = "🤖",
                Font = new Font("Segoe UI Emoji", 20f),
                ForeColor = Color.FromArgb(56, 189, 248),
                AutoSize = true,
                Location = new Point(16, 10)
            };
            pnlHeader.Controls.Add(lblLogo);

            var lblTitle = new Label
            {
                Text = "ZEROPLATFORM INDUSTRIAL AI COPILOT & CHATBOT STUDIO",
                Font = new Font("Segoe UI", 11.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(248, 250, 252),
                AutoSize = true,
                Location = new Point(60, 10)
            };
            pnlHeader.Controls.Add(lblTitle);

            var lblSub = new Label
            {
                Text = "ZeroAgent ReAct & Reflex NLU • 4-Tier Memory Engine • Human-in-the-Loop Safety Gate • Dynamic SQL Analytics",
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = Color.FromArgb(148, 163, 184),
                AutoSize = true,
                Location = new Point(62, 34)
            };
            pnlHeader.Controls.Add(lblSub);

            // Right Header Badges
            var flowBadges = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(0, 16, 0, 0)
            };

            flowBadges.Controls.Add(CreateBadge("● REFLEX NLU (SUB-5MS)", Color.FromArgb(52, 211, 153)));
            flowBadges.Controls.Add(CreateBadge("● HITL SAFETY: ACTIVE", Color.FromArgb(251, 191, 36)));
            flowBadges.Controls.Add(CreateBadge("● 4-TIER MEMORY", Color.FromArgb(96, 165, 250)));
            flowBadges.Controls.Add(CreateBadge("● PURE C# (ZERO GPU)", Color.FromArgb(167, 139, 250)));
            pnlHeader.Controls.Add(flowBadges);

            Controls.Add(pnlHeader);

            // 2. Main Dual-Pane Splitter
            var splitMain = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 780,
                SplitterWidth = 6,
                BackColor = Color.FromArgb(30, 41, 59)
            };
            splitMain.Panel1.BackColor = Color.FromArgb(15, 23, 42);
            splitMain.Panel2.BackColor = Color.FromArgb(15, 23, 42);

            // Left: ZAiChatBox
            BuildChatPanel(splitMain.Panel1);

            // Right: Telemetry & Inspector Tabs
            BuildTelemetryPanel(splitMain.Panel2);

            Controls.Add(splitMain);
        }

        private static Label CreateBadge(string text, Color color)
        {
            return new Label
            {
                Text = text,
                Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
                ForeColor = color,
                BackColor = Color.FromArgb(20, color.R, color.G, color.B),
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(6, 3, 6, 3),
                Margin = new Padding(4, 0, 4, 0),
                AutoSize = true
            };
        }

        private void BuildChatPanel(Panel parent)
        {
            _chatBox = new ZAiChatBox
            {
                Dock = DockStyle.Fill,
                AssistantName = "ZeroCopilot",
                ModelName = "Reflex NLU + Tool Calling"
            };

            // Prompt Suggestions Chips
            _chatBox.PromptSuggestions.Add("🌡️ Nhiệt độ máy CNC-01 hiện tại bao nhiêu?");
            _chatBox.PromptSuggestions.Add("🛑 Yêu cầu dừng khẩn cấp máy CNC-01");
            _chatBox.PromptSuggestions.Add("📊 Cho tôi xem bảng máy móc sản xuất (SQL)");
            _chatBox.PromptSuggestions.Add("⚠️ Có cảnh báo hệ thống SCADA nào không?");
            _chatBox.PromptSuggestions.Add("📖 Tra cứu SOP quy trình xử lý quá nhiệt lò nung F-01");
            _chatBox.PromptSuggestions.Add("🔍 Sự cố quá nhiệt bạc đạn máy CNC-01 trước đây xử lý thế nào?");
            _chatBox.PromptSuggestions.Add("💡 Nó có nóng không?");
            _chatBox.PromptSuggestions.Add("📈 Kiểm tra áp suất máy PRESS-03");

            var welcome = _chatBox.AppendAssistantMessage(
                "Xin chào! Tôi là **ZeroCopilot** - Trợ lý AI vận hành công nghiệp và phân tích hệ thống.\n\n" +
                "Tôi hỗ trợ bạn:\n" +
                "• Giám sát thông số cảm biến SCADA & PLC (nhiệt độ, áp suất, độ rung).\n" +
                "• Thực thi lệnh điều khiển an toàn có cơ chế xác nhận **Human-in-the-Loop (HITL)**.\n" +
                "• Tra cứu quy trình vận hành chuẩn (**SOP Semantic Memory**) và sự cố lịch sử (**Episodic Memory**).\n" +
                "• Phân tích số liệu và truy vấn dữ liệu thời gian thực (**Columnar DataFrames & SQL**).\n\n" +
                "👉 Hãy nhấn vào một câu hỏi gợi ý bên dưới hoặc nhập nội dung bất kỳ!");
            welcome.IsStreaming = false;

            _chatBox.SendMessageRequested += async (s, userText) => await ProcessUserMessageAsync(userText);
            _chatBox.ClearChatRequested += (s, e) => ResetChatContext();

            parent.Controls.Add(_chatBox);
        }

        private void BuildTelemetryPanel(Panel parent)
        {
            var pnlContainer = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12),
                BackColor = Color.FromArgb(15, 23, 42)
            };

            // 1. HITL Alert Banner (Initially Hidden)
            _pnlHitlAlert = new Panel
            {
                Dock = DockStyle.Top,
                Height = 84,
                BackColor = Color.FromArgb(69, 26, 3), // Amber 950
                Padding = new Padding(12),
                Visible = false
            };
            _pnlHitlAlert.Paint += (s, e) =>
            {
                using var p = new Pen(Color.FromArgb(245, 158, 11), 1.5f);
                e.Graphics.DrawRectangle(p, 0, 0, _pnlHitlAlert.Width - 1, _pnlHitlAlert.Height - 1);
            };

            var lblHitlTitle = new Label
            {
                Text = "⚠️ SENSITIVE OPERATION INTERCEPTED (HUMAN-IN-THE-LOOP)",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(251, 191, 36),
                Dock = DockStyle.Top,
                Height = 20
            };
            _pnlHitlAlert.Controls.Add(lblHitlTitle);

            _lblHitlMessage = new Label
            {
                Text = "Lệnh dừng thiết bị nhạy cảm đang chờ phê duyệt từ Ca trưởng.",
                Font = new Font("Segoe UI", 8.25f),
                ForeColor = Color.FromArgb(254, 243, 199),
                Dock = DockStyle.Top,
                Height = 22
            };
            _pnlHitlAlert.Controls.Add(_lblHitlMessage);

            var pnlHitlBtns = new Panel { Dock = DockStyle.Bottom, Height = 28 };
            _btnApprove = new Button
            {
                Text = "✅ Duyệt thực thi (Approve)",
                Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(16, 185, 129),
                FlatStyle = FlatStyle.Flat,
                Dock = DockStyle.Left,
                Width = 170,
                Cursor = Cursors.Hand
            };
            _btnApprove.FlatAppearance.BorderSize = 0;
            _btnApprove.Click += (s, e) => ApproveActiveHitl();

            _btnReject = new Button
            {
                Text = "❌ Hủy lệnh (Reject)",
                Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(239, 68, 68),
                FlatStyle = FlatStyle.Flat,
                Dock = DockStyle.Left,
                Width = 150,
                Margin = new Padding(8, 0, 0, 0),
                Cursor = Cursors.Hand
            };
            _btnReject.FlatAppearance.BorderSize = 0;
            _btnReject.Click += (s, e) => RejectActiveHitl();

            pnlHitlBtns.Controls.Add(_btnReject);
            pnlHitlBtns.Controls.Add(new Panel { Dock = DockStyle.Left, Width = 8 });
            pnlHitlBtns.Controls.Add(_btnApprove);
            _pnlHitlAlert.Controls.Add(pnlHitlBtns);

            pnlContainer.Controls.Add(_pnlHitlAlert);

            var spacerHitl = new Panel { Dock = DockStyle.Top, Height = 8, BackColor = Color.Transparent };
            pnlContainer.Controls.Add(spacerHitl);

            // 2. Tabbed Workspace
            var subTabs = new ZeroTabControl
            {
                Dock = DockStyle.Fill,
                Orientation = ZeroTabOrientation.Horizontal,
                TabHeight = 34,
                TabStyle = ZeroTabStyle.Pill
            };

            var tabNlu = new ZeroTabPage("Cognitive NLU & State", "🧠", p => BuildNluTab(p));
            var tabTools = new ZeroTabPage("Tools & SQL Execution", "⚡", p => BuildToolsTab(p));
            var tabMemory = new ZeroTabPage("Agentic Memory (SOP & Incidents)", "💾", p => BuildMemoryTab(p));
            var tabAudit = new ZeroTabPage("HITL Audit Log", "🛡️", p => BuildAuditTab(p));

            subTabs.AddTab(tabNlu);
            subTabs.AddTab(tabTools);
            subTabs.AddTab(tabMemory);
            subTabs.AddTab(tabAudit);

            pnlContainer.Controls.Add(subTabs);
            parent.Controls.Add(pnlContainer);
        }

        #region Sub-Tabs Construction

        private void BuildNluTab(ZeroTabPage page)
        {
            page.BackColor = Color.FromArgb(15, 23, 42);
            page.Padding = new Padding(8);

            var stack = new ZeroStackPanel
            {
                Dock = DockStyle.Fill,
                Orientation = StackOrientation.Vertical,
                Spacing = 12,
                AutoScroll = true
            };

            // Card 1: Intent & Confidence
            var cardIntent = new ZeroCard
            {
                Title = "NLU INTENT CLASSIFIER",
                Subtitle = "Deterministic Fast-Path + Neural Classification",
                Height = 160,
                Dock = DockStyle.Top
            };

            var descIntent = new ZeroDescriptions
            {
                Dock = DockStyle.Fill,
                Columns = 1,
                RowHeight = 24,
                LabelColor = Color.FromArgb(148, 163, 184),
                ValueColor = Color.FromArgb(241, 245, 249)
            };

            _lblIntent = new Label { Text = "IDLE (Chờ tương tác)", ForeColor = Color.FromArgb(52, 211, 153), Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
            _lblConfidence = new Label { Text = "100.0%", ForeColor = Color.FromArgb(56, 189, 248), Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
            _lblDstState = new Label { Text = "Idle", ForeColor = Color.FromArgb(241, 245, 249) };
            _lblActiveEntity = new Label { Text = "None", ForeColor = Color.FromArgb(251, 191, 36) };
            _lblLatency = new Label { Text = "< 1 ms", ForeColor = Color.FromArgb(52, 211, 153), Font = new Font("Segoe UI", 9f, FontStyle.Bold) };

            descIntent.Add("Recognized Intent", _lblIntent.Text);
            descIntent.Add("Inference Confidence", _lblConfidence.Text);
            descIntent.Add("DST Tracking State", _lblDstState.Text);
            descIntent.Add("Active Coref Entity", _lblActiveEntity.Text);
            descIntent.Add("Cognitive Latency", _lblLatency.Text);

            cardIntent.Controls.Add(descIntent);
            stack.Controls.Add(cardIntent);

            // Card 2: Extracted Slots Table
            var cardSlots = new ZeroCard
            {
                Title = "EXTRACTED SLOTS & WORKING MEMORY",
                Subtitle = "Dialogue State Tracking (DST) Slot-Filling Registry",
                Height = 230,
                Dock = DockStyle.Top
            };

            _lstSlots = new ListBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(30, 41, 59),
                ForeColor = Color.FromArgb(226, 232, 240),
                BorderStyle = BorderStyle.None,
                Font = new Font("Cascadia Code", 8.5f),
                ItemHeight = 20
            };
            _lstSlots.Items.Add("[Working Memory Initialized]");
            _lstSlots.Items.Add("Session ID : " + _sessionId);
            _lstSlots.Items.Add("State      : SessionState.Idle");
            _lstSlots.Items.Add("Slot machine_id : (waiting user input)");

            cardSlots.Controls.Add(_lstSlots);
            stack.Controls.Add(cardSlots);

            page.Controls.Add(stack);
        }

        private void BuildToolsTab(ZeroTabPage page)
        {
            page.BackColor = Color.FromArgb(15, 23, 42);
            page.Padding = new Padding(8);

            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 220,
                SplitterWidth = 6,
                BackColor = Color.FromArgb(30, 41, 59)
            };
            split.Panel1.BackColor = Color.FromArgb(15, 23, 42);
            split.Panel2.BackColor = Color.FromArgb(15, 23, 42);

            // Top: Tool Execution Log
            var pnlTop = new Panel { Dock = DockStyle.Fill };
            var lblTop = new Label
            {
                Text = "⚡ RECENT TOOL CALLS & DATA INVOCATIONS",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(148, 163, 184),
                Dock = DockStyle.Top,
                Height = 24
            };
            pnlTop.Controls.Add(lblTop);

            _lstToolInvocations = new ListBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(30, 41, 59),
                ForeColor = Color.FromArgb(241, 245, 249),
                BorderStyle = BorderStyle.None,
                Font = new Font("Cascadia Code", 8.25f),
                ItemHeight = 18
            };
            _lstToolInvocations.SelectedIndexChanged += (s, e) =>
            {
                if (_lstToolInvocations.SelectedIndex >= 0 && _lstToolInvocations.SelectedIndex < _env.ToolLogs.Count)
                {
                    var log = _env.ToolLogs[_lstToolInvocations.SelectedIndex];
                    _txtToolDetail!.Text = $"Tool: {log.ToolName}\nTime: {log.Timestamp:HH:mm:ss.fff}\nHazardous: {log.IsHazardous}\n\nArguments:\n{log.Arguments}\n\nResult / Output:\n{log.Result}";
                }
            };
            pnlTop.Controls.Add(_lstToolInvocations);
            split.Panel1.Controls.Add(pnlTop);

            // Bottom: Payload & Output Details
            var pnlBottom = new Panel { Dock = DockStyle.Fill };
            var lblBottom = new Label
            {
                Text = "📄 TOOL EXECUTION PAYLOAD & RETURN DATA",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(148, 163, 184),
                Dock = DockStyle.Top,
                Height = 24
            };
            pnlBottom.Controls.Add(lblBottom);

            _txtToolDetail = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                BackColor = Color.FromArgb(24, 32, 47),
                ForeColor = Color.FromArgb(226, 232, 240),
                BorderStyle = BorderStyle.None,
                Font = new Font("Cascadia Code", 8.5f),
                ScrollBars = ScrollBars.Vertical,
                Text = "Chọn một tool call ở danh sách trên để xem chi tiết đối số và kết quả trả về."
            };
            pnlBottom.Controls.Add(_txtToolDetail);
            split.Panel2.Controls.Add(pnlBottom);

            page.Controls.Add(split);
        }

        private void BuildMemoryTab(ZeroTabPage page)
        {
            page.BackColor = Color.FromArgb(15, 23, 42);
            page.Padding = new Padding(8);

            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 200,
                SplitterWidth = 6,
                BackColor = Color.FromArgb(30, 41, 59)
            };

            // Top: List of memories
            var pnlTop = new Panel { Dock = DockStyle.Fill };
            var lblTop = new Label
            {
                Text = "💾 AGENTIC MEMORY INDEX (SOP MANUALS & EPISODIC INCIDENTS)",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(148, 163, 184),
                Dock = DockStyle.Top,
                Height = 24
            };
            pnlTop.Controls.Add(lblTop);

            _lstSemanticMemory = new ListBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(30, 41, 59),
                ForeColor = Color.FromArgb(241, 245, 249),
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 8.75f),
                ItemHeight = 22
            };
            _lstSemanticMemory.SelectedIndexChanged += (s, e) => DisplaySelectedMemory();
            pnlTop.Controls.Add(_lstSemanticMemory);
            split.Panel1.Controls.Add(pnlTop);

            // Bottom: Memory Content Preview
            var pnlBottom = new Panel { Dock = DockStyle.Fill };
            var lblBottom = new Label
            {
                Text = "📖 MEMORY CONTENT & METADATA",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(148, 163, 184),
                Dock = DockStyle.Top,
                Height = 24
            };
            pnlBottom.Controls.Add(lblBottom);

            _txtMemoryDetail = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                BackColor = Color.FromArgb(24, 32, 47),
                ForeColor = Color.FromArgb(226, 232, 240),
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 9f),
                ScrollBars = ScrollBars.Vertical
            };
            pnlBottom.Controls.Add(_txtMemoryDetail);
            split.Panel2.Controls.Add(pnlBottom);

            page.Controls.Add(split);
        }

        private void BuildAuditTab(ZeroTabPage page)
        {
            page.BackColor = Color.FromArgb(15, 23, 42);
            page.Padding = new Padding(8);

            var lblTitle = new Label
            {
                Text = "🛡️ HUMAN-IN-THE-LOOP AUDIT & COMPLIANCE LOG",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(148, 163, 184),
                Dock = DockStyle.Top,
                Height = 24
            };
            page.Controls.Add(lblTitle);

            _lstAuditLog = new ListBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(30, 41, 59),
                ForeColor = Color.FromArgb(241, 245, 249),
                BorderStyle = BorderStyle.None,
                Font = new Font("Cascadia Code", 8.25f),
                ItemHeight = 20
            };
            page.Controls.Add(_lstAuditLog);
        }

        #endregion

        #region Interaction & Telemetry Updates

        private void WireEnvironmentEvents()
        {
            _env.ToolExecuted += (s, log) =>
            {
                if (InvokeRequired)
                {
                    Invoke(new Action(() => OnToolExecuted(log)));
                    return;
                }
                OnToolExecuted(log);
            };

            _env.HitlIntercepted += (s, req) =>
            {
                if (InvokeRequired)
                {
                    Invoke(new Action(() => OnHitlIntercepted(req)));
                    return;
                }
                OnHitlIntercepted(req);
            };
        }

        private void OnToolExecuted(ToolInvocationLog log)
        {
            string hazardBadge = log.IsHazardous ? " [⚠️ HAZARD]" : "";
            _lstToolInvocations?.Items.Insert(0, $"[{log.Timestamp:HH:mm:ss}] {log.ToolName}{hazardBadge}");
        }

        private void OnHitlIntercepted(HitlApprovalRequest req)
        {
            _activeHitlRequest = req;
            if (_pnlHitlAlert != null)
            {
                _lblHitlMessage!.Text = $"Yêu cầu: [{req.ToolName}] với đối số '{req.Argument}' cần xác nhận của Ca trưởng.";
                _pnlHitlAlert.Visible = true;
            }
        }

        private void ApproveActiveHitl()
        {
            if (_activeHitlRequest != null)
            {
                _env.SafetyGate.Approve(_activeHitlRequest.RequestId, "Chief-Operator-Nguyen");
                _pnlHitlAlert!.Visible = false;
                _lstAuditLog?.Items.Insert(0, $"[{DateTime.Now:HH:mm:ss}] APPROVED: {_activeHitlRequest.ToolName} by Chief-Operator-Nguyen");
                _activeHitlRequest = null;
            }
        }

        private void RejectActiveHitl()
        {
            if (_activeHitlRequest != null)
            {
                _env.SafetyGate.Reject(_activeHitlRequest.RequestId, "Chief-Operator-Nguyen");
                _pnlHitlAlert!.Visible = false;
                _lstAuditLog?.Items.Insert(0, $"[{DateTime.Now:HH:mm:ss}] REJECTED: {_activeHitlRequest.ToolName} by Chief-Operator-Nguyen");
                _activeHitlRequest = null;
            }
        }

        private async Task ProcessUserMessageAsync(string userMessage)
        {
            if (string.IsNullOrWhiteSpace(userMessage) || _chatBox == null) return;

            _chatBox.IsGenerating = true;
            _chatBox.StreamingState = ChatStreamingState.Thinking;

            try
            {
                // Execute in Background
                var result = await Task.Run(() => _env.SendMessageAsync(_sessionId, userMessage));

                // Update Telemetry Panel
                UpdateTelemetry(result.RecognizedIntent, result.Confidence, result.Latency);

                // Append Assistant Message with streamed token animation
                _chatBox.StreamingState = ChatStreamingState.Streaming;
                var msg = _chatBox.AppendAssistantMessage("");

                string fullText = result.Response.Text;
                string[] tokens = fullText.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                for (int i = 0; i < tokens.Length; i++)
                {
                    _chatBox.StreamToken(msg.Id, (i == 0 ? "" : " ") + tokens[i]);
                    await Task.Delay(15); // realistic token streaming cadence
                }

                _chatBox.CompleteStreaming(msg.Id);
            }
            catch (Exception ex)
            {
                _chatBox.AppendAssistantMessage($"⚠️ Có lỗi xảy ra trong quá trình xử lý: {ex.Message}");
            }
            finally
            {
                _chatBox.IsGenerating = false;
                _chatBox.StreamingState = ChatStreamingState.Idle;
            }
        }

        private void UpdateTelemetry(string? intent, double confidence, TimeSpan latency)
        {
            var session = _env.Engine.GetOrCreateSession(_sessionId);
            var workingMem = _env.Engine.Memory.GetWorkingMemory(_sessionId);

            if (_lblIntent != null)
            {
                _lblIntent.Text = string.IsNullOrEmpty(intent) ? "NLU_REFLEX_DEFAULT" : intent;
                _lblConfidence!.Text = $"{confidence * 100:F1}%";
                _lblDstState!.Text = session.State.ToString();
                _lblActiveEntity!.Text = workingMem.CurrentSubject ?? "None";
                _lblLatency!.Text = $"{latency.TotalMilliseconds:F2} ms (Sub-5ms)";
            }

            if (_lstSlots != null)
            {
                _lstSlots.Items.Clear();
                _lstSlots.Items.Add($"=== DST SLOTS REGISTRY (Turn: {workingMem.Turns.Count}) ===");
                _lstSlots.Items.Add($"Active Subject: {workingMem.CurrentSubject ?? "<null>"}");
                _lstSlots.Items.Add($"Session State : {session.State}");
                
                foreach (var kvp in session.Slots)
                {
                    _lstSlots.Items.Add($"Slot '{kvp.Key}' = {kvp.Value}");
                }

                if (session.Slots.Count == 0)
                {
                    _lstSlots.Items.Add("(Chưa trích xuất slot nào trong lượt thoại này)");
                }
            }
        }

        private void LoadInitialMemoryData()
        {
            _lstSemanticMemory?.Items.Add("📖 SOP: Quy trình xử lý quá nhiệt Lò nung F-01");
            _lstSemanticMemory?.Items.Add("📖 SOP: Quy chuẩn bôi trơn bạc đạn máy CNC");
            _lstSemanticMemory?.Items.Add("📖 SOP: Quy trình xử lý Cảnh báo Áp suất Thủy lực Máy PRESS-03");
            _lstSemanticMemory?.Items.Add("🔍 EPISODIC: Lỗi sự cố quá nhiệt bạc đạn máy CNC-01 (15/09)");
            _lstSemanticMemory?.Items.Add("🔍 EPISODIC: Sự cố tụt áp thủy lực máy PRESS-03 (20/09)");

            if (_lstSemanticMemory?.Items.Count > 0)
            {
                _lstSemanticMemory.SelectedIndex = 0;
            }
        }

        private void DisplaySelectedMemory()
        {
            if (_lstSemanticMemory == null || _txtMemoryDetail == null) return;
            int idx = _lstSemanticMemory.SelectedIndex;

            switch (idx)
            {
                case 0:
                    _txtMemoryDetail.Text = "TIÊU ĐỀ: Quy trình xử lý quá nhiệt Lò nung F-01\nLOẠI: SOP Standard Operating Procedure\nTAG: SOP, NHÀ NUNG\n\nNỘI DUNG:\n1. Kiểm tra van tuần hoàn làm mát C-2.\n2. Giảm công suất gia nhiệt về 60%.\n3. Nếu nhiệt độ vượt 1,200°C, kích hoạt dừng khẩn cấp và báo ca trưởng.";
                    break;
                case 1:
                    _txtMemoryDetail.Text = "TIÊU ĐỀ: Quy chuẩn bôi trơn bạc đạn máy CNC\nLOẠI: SOP Bảo trì phòng ngừa\nTAG: BẢO TRÌ, CNC\n\nNỘI DUNG:\nBạc đạn trục chính CNC yêu cầu mỡ bôi trơn ISO VG 68, thay định kỳ mỗi 2,000 giờ chạy máy.";
                    break;
                case 2:
                    _txtMemoryDetail.Text = "TIÊU ĐỀ: Quy trình xử lý Cảnh báo Áp suất Thủy lực Máy PRESS-03\nLOẠI: SOP-SCADA\nTAG: THỦY LỰC, PRESS-03\n\nNỘI DUNG:\n1. Kiểm tra van an toàn thủy lực SV-101.\n2. Quan sát áp suất đồng hồ thứ cấp.\n3. Nếu vượt quá 250 Bar, ấn nút xả tải và ngắt bơm chính P-1.";
                    break;
                case 3:
                    _txtMemoryDetail.Text = "TIÊU ĐỀ: Lỗi sự cố quá nhiệt bạc đạn máy CNC-01 (15/09)\nLOẠI: Episodic Incident Record\nKẾT QUẢ: Thành công (Success)\n\nGIẢI PHÁP ĐÃ THỰC HIỆN:\nPhát hiện kẹt cánh quạt làm mát số 3. Đã thay quạt và bổ sung mỡ bôi trơn. Thiết bị hoạt động ổn định.";
                    break;
                case 4:
                    _txtMemoryDetail.Text = "TIÊU ĐỀ: Sự cố tụt áp thủy lực máy PRESS-03 ca đêm (20/09)\nLOẠI: Episodic Incident Record\nKẾT QUẢ: Thành công (Success)\n\nGIẢI PHÁP ĐÃ THỰC HIỆN:\nGioăng phớt xi lanh số 2 bị mòn gây rò rỉ dầu. Đã thay gioăng Viton chịu nhiệt và châm thêm 20L dầu thủy lực ISO 46.";
                    break;
            }
        }

        private void ResetChatContext()
        {
            _env.Engine.Sessions.Remove(_sessionId);
            UpdateTelemetry(null, 1.0, TimeSpan.Zero);
            _txtToolDetail!.Clear();
            _pnlHitlAlert!.Visible = false;
        }

        #endregion
    }
}
