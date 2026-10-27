using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using ZeroUI.Core.AiMl;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Documents
{
    /// <summary>
    /// Enterprise AI chat box control for WinForms with responsive layout, fluid animations,
    /// high-performance token streaming, markdown formatting, attachment ingestion, and zero-flicker double buffering.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI")]
    [DefaultEvent("SendMessageRequested")]
    public partial class ZAiChatBox : Control
    {
        #region Internal Helper Types

        internal sealed class BubbleViewHolder
        {
            public Control BubblePanel { get; set; } = null!;
            public RichTextBox RichTextBox { get; set; } = null!;
            public Panel? FooterPanel { get; set; }
            public ChatMessage Message { get; set; } = null!;
        }

        internal sealed class DoubleBufferedPanel : Panel
        {
            public DoubleBufferedPanel()
            {
                SetStyle(
                    ControlStyles.UserPaint |
                    ControlStyles.AllPaintingInWmPaint |
                    ControlStyles.OptimizedDoubleBuffer |
                    ControlStyles.ResizeRedraw, true);
                UpdateStyles();
            }

            protected override CreateParams CreateParams
            {
                get
                {
                    var cp = base.CreateParams;
                    cp.ExStyle |= 0x02000000; // WS_CLIPCHILDREN
                    return cp;
                }
            }
        }

        #endregion

        #region Fields

        private readonly Panel _headerPanel;
        private readonly DoubleBufferedPanel _messagesContainer;
        private readonly FlowLayoutPanel _suggestionsPanel;
        private readonly Panel _inputPanel;
        private readonly TextBox _promptInputBox;
        private readonly Button _sendButton;
        private readonly Button _stopButton;
        private readonly Button _clearButton;
        private readonly Label _titleLabel;
        private readonly Label _statusLabel;
        private readonly Label _botIcon;
        private bool _showHeaderIcon = true;

        private readonly FlowLayoutPanel _attachmentToolbar;
        private bool _enableAttachments = true;

        private ObservableCollection<ChatMessage> _messages;
        private ObservableCollection<string> _promptSuggestions;
        private string _assistantName = "ZeroCopilot";
        private string? _modelName = "ZeroInference Edge";
        private bool _isGenerating;
        private ChatStreamingState _streamingState = ChatStreamingState.Idle;

        private Control? _thinkingBubble;
        private bool _cancelStreaming;
        private bool _suppressRebuild;

        private readonly Dictionary<string, BubbleViewHolder> _bubbleCache = new();

        #endregion

        #region Constructor

        public ZAiChatBox()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw, true);

            _messages = new ObservableCollection<ChatMessage>();
            _promptSuggestions = new ObservableCollection<string>();

            _messages.CollectionChanged += OnMessagesCollectionChanged;
            _promptSuggestions.CollectionChanged += OnPromptSuggestionsCollectionChanged;

            BackColor = Color.FromArgb(15, 23, 42); // Slate 900

            // 1. Header Panel
            _headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 48,
                BackColor = Color.FromArgb(30, 41, 59),
                Padding = new Padding(12, 8, 12, 8)
            };

            _botIcon = new Label
            {
                Text = "",
                Font = new Font("Segoe UI Emoji", 14f, FontStyle.Regular),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(12, 10),
                Visible = _showHeaderIcon
            };
            _headerPanel.Controls.Add(_botIcon);

            int textLeft = _showHeaderIcon ? 44 : 14;
            _titleLabel = new Label
            {
                Text = FormatTitleText(),
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = Color.FromArgb(241, 245, 249),
                AutoSize = true,
                Location = new Point(textLeft, 6)
            };
            _headerPanel.Controls.Add(_titleLabel);

            _statusLabel = new Label
            {
                Text = "Sẵn sàng",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
                ForeColor = Color.FromArgb(52, 211, 153),
                AutoSize = true,
                Location = new Point(textLeft, 26)
            };
            _headerPanel.Controls.Add(_statusLabel);

            _clearButton = new Button
            {
                Text = "🗑 Clear",
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = Color.FromArgb(148, 163, 184),
                BackColor = Color.Transparent,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(68, 28),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(Width - 80, 10),
                Cursor = Cursors.Hand
            };
            _clearButton.FlatAppearance.BorderSize = 0;
            _clearButton.Click += (s, e) =>
            {
                CancelActiveFlowRequest();
                ClearMessages();
                if (_flowProvider != null)
                {
                    _ = _flowProvider.ResetSessionAsync(_sessionContext.SessionId);
                }
                ClearChatRequested?.Invoke(this, EventArgs.Empty);
            };
            _headerPanel.Controls.Add(_clearButton);

            // 2. Input Panel (Dock Bottom)
            _inputPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 68,
                BackColor = Color.FromArgb(30, 41, 59),
                Padding = new Padding(10, 8, 10, 8)
            };

            _promptInputBox = new TextBox
            {
                Multiline = true,
                AcceptsReturn = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = Color.FromArgb(15, 23, 42),
                ForeColor = Color.FromArgb(248, 250, 252),
                Font = new Font("Segoe UI", 9.5f),
                BorderStyle = BorderStyle.FixedSingle,
                Dock = DockStyle.Fill
            };
            _promptInputBox.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter && !e.Shift)
                {
                    e.SuppressKeyPress = true;
                    SubmitPrompt();
                }
            };

            var buttonPanel = new Panel
            {
                Dock = DockStyle.Right,
                Width = 88,
                Padding = new Padding(8, 0, 0, 0)
            };

            _sendButton = new Button
            {
                Text = "Gửi ➢",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                BackColor = Color.FromArgb(79, 70, 229),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Dock = DockStyle.Fill,
                Cursor = Cursors.Hand
            };
            _sendButton.FlatAppearance.BorderSize = 0;
            _sendButton.MouseEnter += (s, e) => _sendButton.BackColor = Color.FromArgb(99, 102, 241);
            _sendButton.MouseLeave += (s, e) => _sendButton.BackColor = Color.FromArgb(79, 70, 229);
            _sendButton.Click += (s, e) => SubmitPrompt();
            buttonPanel.Controls.Add(_sendButton);

            _stopButton = new Button
            {
                Text = "⏹ Dừng",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                BackColor = Color.FromArgb(220, 38, 38),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Dock = DockStyle.Fill,
                Cursor = Cursors.Hand,
                Visible = false
            };
            _stopButton.FlatAppearance.BorderSize = 0;
            _stopButton.Click += (s, e) =>
            {
                _cancelStreaming = true;
                CancelActiveFlowRequest();
                StopGenerationRequested?.Invoke(this, EventArgs.Empty);
            };
            buttonPanel.Controls.Add(_stopButton);

            _inputPanel.Controls.Add(_promptInputBox);
            _inputPanel.Controls.Add(buttonPanel);

            // 2.5 Customizable Quick-Action & Attachment Toolbar (Dock Bottom above Input)
            _attachmentToolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 36,
                BackColor = Color.FromArgb(20, 28, 44),
                Padding = new Padding(10, 4, 10, 4),
                WrapContents = false
            };

            InitializeGenericComponents();

            // 3. Suggestions Panel (Dock Bottom above Attachment Toolbar)
            _suggestionsPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MaximumSize = new Size(0, 95),
                BackColor = Color.FromArgb(17, 24, 39),
                Padding = new Padding(10, 6, 10, 0),
                WrapContents = true,
                AutoScroll = false,
                Visible = false
            };

            // 4. Messages Container (Dock Fill with Double Buffering & WS_CLIPCHILDREN)
            _messagesContainer = new DoubleBufferedPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.FromArgb(15, 23, 42),
                Padding = new Padding(12)
            };
            _messagesContainer.HorizontalScroll.Maximum = 0;
            _messagesContainer.HorizontalScroll.Visible = false;
            _messagesContainer.HorizontalScroll.Enabled = false;
            _messagesContainer.AutoScrollMinSize = Size.Empty;
            _messagesContainer.Scroll += (s, e) =>
            {
                if (_messagesContainer.HorizontalScroll.Value != 0)
                {
                    _messagesContainer.HorizontalScroll.Value = 0;
                }
            };

            // Reverse docking order
            Controls.Add(_messagesContainer);
            Controls.Add(_suggestionsPanel);
            Controls.Add(_attachmentToolbar);
            Controls.Add(_inputPanel);
            if (_contextBarPanel != null)
            {
                Controls.Add(_contextBarPanel);
            }
            Controls.Add(_headerPanel);

            ApplyTheme();

            ZeroTheme.ThemeChanged += OnThemeChanged;
            Resize += (s, e) =>
            {
                _clearButton.Location = new Point(Width - 80, 10);
                UpdateContextBarUI();
                RebuildAllMessages();
            };
        }

        #endregion

        #region Properties

        [Category("ZeroUI")]
        [Description("Conversation message history collection.")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public ObservableCollection<ChatMessage> Messages
        {
            get => _messages;
            set
            {
                if (_messages != value)
                {
                    _messages.CollectionChanged -= OnMessagesCollectionChanged;
                    _messages = value ?? new ObservableCollection<ChatMessage>();
                    _messages.CollectionChanged += OnMessagesCollectionChanged;
                    RebuildAllMessages();
                }
            }
        }

        [Category("ZeroUI")]
        [Description("Actionable quick prompt suggestions chips.")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public ObservableCollection<string> PromptSuggestions
        {
            get => _promptSuggestions;
            set
            {
                if (_promptSuggestions != value)
                {
                    _promptSuggestions.CollectionChanged -= OnPromptSuggestionsCollectionChanged;
                    _promptSuggestions = value ?? new ObservableCollection<string>();
                    _promptSuggestions.CollectionChanged += OnPromptSuggestionsCollectionChanged;
                    RebuildPromptSuggestions();
                }
            }
        }

        [Category("ZeroUI")]
        [Description("AI Assistant display name.")]
        [DefaultValue("ZeroCopilot")]
        public string AssistantName
        {
            get => _assistantName;
            set
            {
                _assistantName = value;
                UpdateHeaderLayout();
            }
        }

        [Category("ZeroUI")]
        [Description("Target AI model identifier.")]
        [DefaultValue("ZeroInference Edge")]
        public string? ModelName
        {
            get => _modelName;
            set
            {
                _modelName = value;
                UpdateHeaderLayout();
            }
        }

        [Category("ZeroUI")]
        [Description("Show or hide the bot icon in the header.")]
        [DefaultValue(true)]
        public bool ShowHeaderIcon
        {
            get => _showHeaderIcon;
            set
            {
                _showHeaderIcon = value;
                UpdateHeaderLayout();
            }
        }

        private string FormatTitleText()
        {
            if (string.IsNullOrWhiteSpace(_modelName))
                return _assistantName;
            return $"{_assistantName} ({_modelName})";
        }

        private void UpdateHeaderLayout()
        {
            if (_titleLabel == null || _statusLabel == null) return;
            if (_botIcon != null) _botIcon.Visible = _showHeaderIcon;
            int textLeft = _showHeaderIcon ? 44 : 14;
            _titleLabel.Location = new Point(textLeft, 6);
            _statusLabel.Location = new Point(textLeft, 26);
            _titleLabel.Text = FormatTitleText();
        }

        [Category("ZeroUI")]
        [Description("Indicates whether the AI is currently generating tokens.")]
        [DefaultValue(false)]
        public bool IsGenerating
        {
            get => _isGenerating;
            set
            {
                if (IsDisposed) return;
                if (InvokeRequired)
                {
                    if (IsHandleCreated)
                    {
                        BeginInvoke(new Action(() => IsGenerating = value));
                    }
                    else
                    {
                        _isGenerating = value;
                    }
                    return;
                }
                _isGenerating = value;
                _sendButton.Visible = !_isGenerating;
                _stopButton.Visible = _isGenerating;
                if (!_isGenerating && _streamingState == ChatStreamingState.Thinking)
                {
                    StreamingState = ChatStreamingState.Idle;
                }
            }
        }

        [Category("ZeroUI")]
        [Description("Current streaming state of the assistant.")]
        [DefaultValue(ChatStreamingState.Idle)]
        public ChatStreamingState StreamingState
        {
            get => _streamingState;
            set
            {
                if (IsDisposed) return;
                if (InvokeRequired)
                {
                    if (IsHandleCreated)
                    {
                        BeginInvoke(new Action(() => StreamingState = value));
                    }
                    else
                    {
                        _streamingState = value;
                    }
                    return;
                }
                _streamingState = value;
                _statusLabel.Text = value switch
                {
                    ChatStreamingState.Thinking => "Đang suy nghĩ...",
                    ChatStreamingState.Streaming => "Đang phản hồi...",
                    ChatStreamingState.Error => "Lỗi xử lý",
                    _ => "Sẵn sàng"
                };
            }
        }

        [Category("ZeroUI")]
        [Description("Enables or disables the customizable toolbar above the input box.")]
        [DefaultValue(true)]
        public bool EnableAttachments
        {
            get => _enableAttachments;
            set
            {
                _enableAttachments = value;
                _attachmentToolbar.Visible = value && _toolbarItems.Count > 0;
            }
        }

        #endregion

        #region Events

        public event EventHandler<string>? SendMessageRequested;
        public event EventHandler? StopGenerationRequested;
        public event EventHandler? ClearChatRequested;
        public event EventHandler<(string FilePath, string FileType)>? FileAttached;
        public event EventHandler<string>? UndoRequested;

        #endregion

        #region Public Methods

        public void AppendUserMessage(string text)
        {
            var msg = ChatMessage.User(text);
            _messages.Add(msg);
        }

        public ChatMessage AppendAssistantMessage(string initialText = "")
        {
            HideThinkingIndicator();
            var msg = ChatMessage.Assistant(initialText, _modelName);
            _messages.Add(msg);
            return msg;
        }

        public ChatMessage AppendAssistantActionMessage(string content, bool canUndo = true)
        {
            HideThinkingIndicator();
            var msg = ChatMessage.Assistant(content, _modelName);
            msg.CanUndo = canUndo;
            _messages.Add(msg);
            return msg;
        }

        public void ClearMessages()
        {
            HideThinkingIndicator();
            _bubbleCache.Clear();
            _messages.Clear();
            RebuildAllMessages();
        }

        #endregion

        #region Theme & Cleanup

        public void ApplyTheme()
        {
            bool isDark = ZeroTheme.IsDark;
            var palette = ZeroTheme.Colors;

            // 1. Root & Container
            BackColor = isDark ? Color.FromArgb(15, 23, 42) : palette.Background;
            if (_messagesContainer != null)
                _messagesContainer.BackColor = BackColor;

            // 2. Header Panel
            if (_headerPanel != null)
                _headerPanel.BackColor = isDark ? Color.FromArgb(30, 41, 59) : Color.FromArgb(241, 245, 249);

            if (_titleLabel != null)
                _titleLabel.ForeColor = isDark ? Color.FromArgb(241, 245, 249) : Color.FromArgb(15, 23, 42);

            if (_statusLabel != null)
                _statusLabel.ForeColor = isDark ? Color.FromArgb(52, 211, 153) : Color.FromArgb(22, 163, 74);

            if (_clearButton != null)
            {
                _clearButton.ForeColor = isDark ? Color.FromArgb(148, 163, 184) : Color.FromArgb(100, 116, 139);
                _clearButton.BackColor = Color.Transparent;
            }

            // 3. Suggestions Panel
            if (_suggestionsPanel != null)
                _suggestionsPanel.BackColor = isDark ? Color.FromArgb(17, 24, 39) : Color.FromArgb(248, 250, 252);

            // 4. Generic Toolbar & Context Bar
            if (_attachmentToolbar != null)
                _attachmentToolbar.BackColor = isDark ? Color.FromArgb(20, 28, 44) : Color.FromArgb(241, 245, 249);

            ApplyGenericComponentTheme(isDark);

            // 5. Input Panel
            if (_inputPanel != null)
                _inputPanel.BackColor = isDark ? Color.FromArgb(30, 41, 59) : Color.FromArgb(255, 255, 255);

            if (_promptInputBox != null)
            {
                _promptInputBox.BackColor = isDark ? Color.FromArgb(15, 23, 42) : Color.FromArgb(248, 250, 252);
                _promptInputBox.ForeColor = isDark ? Color.FromArgb(248, 250, 252) : Color.FromArgb(15, 23, 42);
            }

            if (_sendButton != null)
            {
                _sendButton.BackColor = isDark ? Color.FromArgb(79, 70, 229) : Color.FromArgb(79, 70, 229);
                _sendButton.ForeColor = Color.White;
            }
        }

        private void OnThemeChanged(object? sender, EventArgs e)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => OnThemeChanged(sender, e)));
                return;
            }
            ApplyTheme();
            RebuildAllMessages();
            RebuildPromptSuggestions();
            Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                CancelActiveFlowRequest();
                ZeroTheme.ThemeChanged -= OnThemeChanged;
            }
            base.Dispose(disposing);
        }

        #endregion
    }
}
