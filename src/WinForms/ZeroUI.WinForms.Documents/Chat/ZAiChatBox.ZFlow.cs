using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ZeroUI.Core.AiMl;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Documents
{
    public partial class ZAiChatBox
    {
        #region Generic Flow & Toolbar Fields

        private IChatFlowProvider? _flowProvider;
        private ChatContextState _sessionContext = new ChatContextState();
        private ObservableCollection<ChatToolbarItem> _toolbarItems = new ObservableCollection<ChatToolbarItem>();
        private string _workflowId = string.Empty;
        private bool _useStreaming = true;
        private bool _showReasoning = true;
        private bool _showContextBar;
        private string _contextBarPrefix = "📌 Context:";
        private string _contextBarEmptyText = "Global";
        private string _clearContextButtonText = "× Clear";
        private string _attachmentDataKey = "image_base64";
        private CancellationTokenSource? _activeFlowCts;

        private Panel? _contextBarPanel;
        private Label? _contextBarLabel;
        private Button? _btnClearContext;

        #endregion

        #region Generic Flow & Component Properties

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IChatFlowProvider? FlowProvider
        {
            get => _flowProvider;
            set
            {
                _flowProvider = value;
                UpdateContextBarUI();
            }
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public ChatContextState SessionContext
        {
            get => _sessionContext;
            set
            {
                _sessionContext = value ?? new ChatContextState();
                UpdateContextBarUI();
            }
        }

        [Category("ZeroUI")]
        [Description("Customizable quick-action buttons displayed in the toolbar above the input box.")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public ObservableCollection<ChatToolbarItem> ToolbarItems
        {
            get => _toolbarItems;
            set
            {
                if (_toolbarItems != value)
                {
                    _toolbarItems.CollectionChanged -= OnToolbarItemsCollectionChanged;
                    _toolbarItems = value ?? new ObservableCollection<ChatToolbarItem>();
                    _toolbarItems.CollectionChanged += OnToolbarItemsCollectionChanged;
                    RebuildToolbarItems();
                }
            }
        }

        [Category("ZeroUI")]
        [Description("Target workflow identifier sent to the active IChatFlowProvider.")]
        [DefaultValue("")]
        public string WorkflowId
        {
            get => _workflowId;
            set => _workflowId = value ?? string.Empty;
        }

        [Category("ZeroUI")]
        [Description("Uses real-time streaming when executing against the active IChatFlowProvider.")]
        [DefaultValue(true)]
        public bool UseStreaming
        {
            get => _useStreaming;
            set => _useStreaming = value;
        }

        [Category("ZeroUI")]
        [Description("Displays optional reasoning/trace header inside assistant message bubbles.")]
        [DefaultValue(true)]
        public bool ShowReasoning
        {
            get => _showReasoning;
            set
            {
                if (_showReasoning != value)
                {
                    _showReasoning = value;
                    RebuildAllMessages();
                }
            }
        }

        [Category("ZeroUI")]
        [Description("Shows or hides the active session context bar below the header.")]
        [DefaultValue(false)]
        public bool ShowContextBar
        {
            get => _showContextBar;
            set
            {
                _showContextBar = value;
                UpdateContextBarUI();
            }
        }

        [Category("ZeroUI")]
        [Description("Prefix label displayed in the context bar.")]
        [DefaultValue("📌 Context:")]
        public string ContextBarPrefix
        {
            get => _contextBarPrefix;
            set
            {
                _contextBarPrefix = value ?? string.Empty;
                UpdateContextBarUI();
            }
        }

        [Category("ZeroUI")]
        [Description("Placeholder text displayed in the context bar when no tags are pinned.")]
        [DefaultValue("Global")]
        public string ContextBarEmptyText
        {
            get => _contextBarEmptyText;
            set
            {
                _contextBarEmptyText = value ?? string.Empty;
                UpdateContextBarUI();
            }
        }

        [Category("ZeroUI")]
        [Description("Context parameter key used when attaching encoded files to a flow request.")]
        [DefaultValue("image_base64")]
        public string AttachmentDataKey
        {
            get => _attachmentDataKey;
            set => _attachmentDataKey = string.IsNullOrWhiteSpace(value) ? "image_base64" : value;
        }

        #endregion

        #region Generic Events

        /// <summary>
        /// Raised when the user clicks an interactive ChatActionItem chip inside a message bubble.
        /// </summary>
        public event EventHandler<ChatActionItem>? ActionItemClicked;

        /// <summary>
        /// Raised when the user clicks a custom ChatToolbarItem in the toolbar.
        /// </summary>
        public event EventHandler<ChatToolbarItem>? ToolbarItemClicked;

        /// <summary>
        /// Raised when an IChatFlowProvider execution completes.
        /// </summary>
        public event EventHandler<ChatFlowResult>? FlowCompleted;

        /// <summary>
        /// Raised when SessionContext tags/updates change or are cleared.
        /// </summary>
        public event EventHandler<ChatContextState>? ContextStateChanged;

        #endregion

        #region Generic Flow Execution Pipeline

        /// <summary>
        /// Configures this control using a fluent <see cref="ZFlowChatOptions"/> builder action.
        /// </summary>
        public ZAiChatBox ConfigureFlow(Action<ZFlowChatOptions> configure, IChatFlowProvider? customProvider = null)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            var options = new ZFlowChatOptions();
            configure(options);
            return ConfigureFlow(options, customProvider);
        }

        /// <summary>
        /// Applies external <see cref="ZFlowChatOptions"/> (provider, context state, toolbar items, and suggestions) to this control.
        /// </summary>
        public ZAiChatBox ConfigureFlow(ZFlowChatOptions options, IChatFlowProvider? customProvider = null)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            _workflowId = options.WorkflowId;
            _useStreaming = options.UseStreaming;
            _showContextBar = options.ShowContextBar;
            _showReasoning = options.ShowReasoning;
            _contextBarPrefix = options.ContextBarPrefix;
            _contextBarEmptyText = options.ContextBarEmptyText;
            _sessionContext = options.BuildContextState();
            _flowProvider = customProvider ?? options.BuildProvider();

            if (options.ReplaceDefaultToolbarItems)
            {
                _toolbarItems.Clear();
            }

            foreach (var item in options.ToolbarItems)
            {
                _toolbarItems.Add(item);
            }

            _promptSuggestions.Clear();
            foreach (var s in options.PromptSuggestions)
            {
                _promptSuggestions.Add(s);
            }
            RebuildPromptSuggestions();

            UpdateContextBarUI();
            return this;
        }

        public ZAiChatBox SetFlowContextParameter(string key, object? value, string? displayTagLabel = null)
        {
            _sessionContext.SetParameter(key, value, displayTagLabel);
            UpdateContextBarUI();
            return this;
        }

        private sealed class FlowStreamState
        {
            public readonly object SyncRoot = new object();
            public readonly System.Text.StringBuilder PendingTokens = new System.Text.StringBuilder();
            public string? PendingStatusText;
            public bool UiFlushScheduled;
            public ChatMessage? AssistantMessage;
        }

        /// <summary>
        /// Executes a prompt using the assigned <see cref="FlowProvider"/> and updates the chat UI.
        /// </summary>
        public async Task<ChatFlowResult?> ExecuteFlowPromptAsync(string prompt, string? attachmentDataUri = null)
        {
            if (IsDisposed || _flowProvider == null || string.IsNullOrWhiteSpace(prompt))
                return null;

            CancelActiveFlowRequest();
            _activeFlowCts = new CancellationTokenSource();
            var token = _activeFlowCts.Token;

            var reqContext = new Dictionary<string, object?>(_sessionContext.Parameters, StringComparer.OrdinalIgnoreCase);
            string? effectiveAttachment = !string.IsNullOrWhiteSpace(attachmentDataUri)
                ? attachmentDataUri
                : _sessionContext.PendingAttachmentData;

            if (!string.IsNullOrWhiteSpace(effectiveAttachment))
            {
                reqContext[_attachmentDataKey] = effectiveAttachment;
                _sessionContext.PendingAttachmentData = null;
            }

            var request = new ChatFlowRequest
            {
                WorkflowId = _workflowId,
                InputText = prompt,
                SessionId = _sessionContext.SessionId,
                Context = reqContext
            };

            IsGenerating = true;
            ShowThinkingIndicator("Đang xử lý...");

            var streamState = new FlowStreamState();

            try
            {
                ChatFlowResult result;
                if (_useStreaming)
                {
                    result = await _flowProvider.StreamAsync(
                        request,
                        chunk => OnFlowStreamChunkReceived(chunk, streamState),
                        token);
                }
                else
                {
                    result = await _flowProvider.ExecuteAsync(request, token);
                }

                if (IsDisposed) return result;

                if (InvokeRequired)
                {
                    if (IsHandleCreated)
                    {
                        var tcs = new TaskCompletionSource<bool>();
                        BeginInvoke(new Action(() =>
                        {
                            try
                            {
                                FinalizeFlowExecutionOnUiThread(result, streamState);
                                tcs.TrySetResult(true);
                            }
                            catch (Exception ex)
                            {
                                tcs.TrySetException(ex);
                            }
                        }));
                        await tcs.Task;
                    }
                }
                else
                {
                    FinalizeFlowExecutionOnUiThread(result, streamState);
                }

                return result;
            }
            catch (Exception ex)
            {
                if (!IsDisposed)
                {
                    Action handleErr = () =>
                    {
                        if (IsDisposed) return;
                        FlushFlowStreamStateOnUiThread(streamState);
                        HideThinkingIndicator();
                        if (streamState.AssistantMessage == null)
                        {
                            AppendAssistantMessage($"⚠️ {ex.Message}");
                        }
                        StreamingState = ChatStreamingState.Error;
                    };

                    if (InvokeRequired)
                    {
                        if (IsHandleCreated) BeginInvoke(handleErr);
                    }
                    else
                    {
                        handleErr();
                    }
                }
                return null;
            }
            finally
            {
                if (!IsDisposed)
                {
                    IsGenerating = false;
                    if (StreamingState != ChatStreamingState.Error)
                    {
                        StreamingState = ChatStreamingState.Idle;
                    }
                }
            }
        }

        private void OnFlowStreamChunkReceived(ChatStreamChunk chunk, FlowStreamState state)
        {
            if (IsDisposed || chunk == null) return;

            lock (state.SyncRoot)
            {
                switch (chunk.EventType)
                {
                    case "status":
                        if (!string.IsNullOrWhiteSpace(chunk.StatusText))
                        {
                            state.PendingStatusText = chunk.StatusText;
                        }
                        break;

                    case "token":
                        if (!string.IsNullOrEmpty(chunk.Token))
                        {
                            state.PendingTokens.Append(chunk.Token);
                        }
                        break;
                }

                if (InvokeRequired)
                {
                    if (!state.UiFlushScheduled && IsHandleCreated && !IsDisposed)
                    {
                        state.UiFlushScheduled = true;
                        BeginInvoke(new Action(() => FlushFlowStreamStateOnUiThread(state)));
                    }
                    return;
                }
            }

            FlushFlowStreamStateOnUiThread(state);
        }

        private void FlushFlowStreamStateOnUiThread(FlowStreamState state)
        {
            if (IsDisposed) return;

            string? statusText;
            string tokenBatch;

            lock (state.SyncRoot)
            {
                statusText = state.PendingStatusText;
                state.PendingStatusText = null;

                tokenBatch = state.PendingTokens.Length > 0
                    ? state.PendingTokens.ToString()
                    : string.Empty;
                state.PendingTokens.Clear();

                state.UiFlushScheduled = false;
            }

            if (state.AssistantMessage == null && !string.IsNullOrWhiteSpace(statusText) && string.IsNullOrEmpty(tokenBatch))
            {
                ShowThinkingIndicator($"{statusText}...");
            }

            if (!string.IsNullOrEmpty(tokenBatch))
            {
                if (state.AssistantMessage == null)
                {
                    HideThinkingIndicator();
                    state.AssistantMessage = AppendAssistantMessage(string.Empty);
                    StreamingState = ChatStreamingState.Streaming;
                }
                StreamToken(state.AssistantMessage.Id, tokenBatch);
            }
        }

        private void FinalizeFlowExecutionOnUiThread(ChatFlowResult result, FlowStreamState streamState)
        {
            if (IsDisposed) return;

            // Ensure any buffered tokens are flushed into the single message bubble first
            FlushFlowStreamStateOnUiThread(streamState);
            HideThinkingIndicator();

            var assistantMsg = streamState.AssistantMessage;
            if (assistantMsg == null)
            {
                assistantMsg = AppendAssistantMessage(result.Output ?? string.Empty);
                streamState.AssistantMessage = assistantMsg;
            }
            else if (!string.IsNullOrWhiteSpace(result.Output))
            {
                assistantMsg.Content = result.Output;
            }

            assistantMsg.IsStreaming = false;
            assistantMsg.Reasoning = result.Reasoning;
            assistantMsg.LatencyMs = result.DurationMs > 0 ? result.DurationMs : null;

            assistantMsg.Actions.Clear();
            if (result.Actions != null && result.Actions.Count > 0)
            {
                assistantMsg.Actions.AddRange(result.Actions);
            }

            assistantMsg.ContextData.Clear();
            if (result.ContextUpdates != null && result.ContextUpdates.Count > 0)
            {
                foreach (var kv in result.ContextUpdates)
                {
                    assistantMsg.ContextData[kv.Key] = kv.Value;
                }
                _sessionContext.MergeContextUpdates(result.ContextUpdates);
                UpdateContextBarUI();
                ContextStateChanged?.Invoke(this, _sessionContext);
            }

            RebuildSingleBubble(assistantMsg.Id);
            FlowCompleted?.Invoke(this, result);
        }

        public void CancelActiveFlowRequest()
        {
            try
            {
                _activeFlowCts?.Cancel();
                _activeFlowCts?.Dispose();
            }
            catch { }
            _activeFlowCts = null;
        }

        #endregion

        #region Generic Context Bar & Customizable Toolbar UI

        private void InitializeGenericComponents()
        {
            _toolbarItems.CollectionChanged += OnToolbarItemsCollectionChanged;

            // Default generic attachment items (can be cleared or replaced by consumer)
            _toolbarItems.Add(new ChatToolbarItem(
                id: "attach_image",
                text: "📷 Đính kèm ảnh",
                commandType: ChatToolbarCommandType.AttachFile,
                accentColorHex: "#3B82F6")
            {
                FileDialogTitle = "Chọn tệp hình ảnh",
                FileFilter = "Image Files (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp|All Files (*.*)|*.*",
                AttachmentType = "image"
            });

            _toolbarItems.Add(new ChatToolbarItem(
                id: "attach_pdf",
                text: "📄 Đính kèm PDF",
                commandType: ChatToolbarCommandType.AttachFile,
                accentColorHex: "#EC4899")
            {
                FileDialogTitle = "Chọn tệp PDF",
                FileFilter = "PDF Files (*.pdf)|*.pdf|All Files (*.*)|*.*",
                AttachmentType = "pdf"
            });

            // Generic Context Bar Panel (docked below Header)
            _contextBarPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 28,
                BackColor = Color.FromArgb(23, 33, 53),
                Padding = new Padding(12, 4, 12, 4),
                Visible = false
            };

            _contextBarLabel = new Label
            {
                Text = string.Empty,
                Font = new Font("Segoe UI", 8f, FontStyle.Regular),
                ForeColor = Color.FromArgb(147, 197, 253),
                AutoSize = true,
                Location = new Point(12, 6)
            };
            _contextBarPanel.Controls.Add(_contextBarLabel);

            _btnClearContext = new Button
            {
                Text = _clearContextButtonText,
                Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(248, 113, 113),
                BackColor = Color.Transparent,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(72, 20),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(Width - 84, 4),
                Cursor = Cursors.Hand,
                Visible = false
            };
            _btnClearContext.FlatAppearance.BorderSize = 0;
            _btnClearContext.Click += (s, e) =>
            {
                _sessionContext.ClearTagsAndParameters(preserveIdentity: true);
                UpdateContextBarUI();
                ContextStateChanged?.Invoke(this, _sessionContext);
            };
            _contextBarPanel.Controls.Add(_btnClearContext);

            RebuildToolbarItems();
        }

        private void OnToolbarItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            RebuildToolbarItems();
        }

        public void RebuildToolbarItems()
        {
            if (_attachmentToolbar == null) return;
            if (InvokeRequired)
            {
                BeginInvoke(new Action(RebuildToolbarItems));
                return;
            }

            _attachmentToolbar.SuspendLayout();
            _attachmentToolbar.Controls.Clear();

            bool isDark = ZeroTheme.IsDark;
            foreach (var item in _toolbarItems)
            {
                Color accent = ParseHexColor(item.AccentColorHex, isDark ? Color.FromArgb(59, 130, 246) : Color.FromArgb(37, 99, 235));
                Color btnBg = isDark ? Color.FromArgb(30, 41, 59) : Color.FromArgb(255, 255, 255);
                Color btnFore = isDark ? ControlPaint.Light(accent, 0.35f) : accent;

                var btn = new Button
                {
                    Text = item.Text,
                    Font = new Font("Segoe UI", 8.25f, FontStyle.Bold),
                    ForeColor = btnFore,
                    BackColor = btnBg,
                    FlatStyle = FlatStyle.Flat,
                    AutoSize = true,
                    Height = 28,
                    Padding = new Padding(6, 0, 6, 0),
                    Margin = new Padding(0, 0, 8, 0),
                    Cursor = Cursors.Hand,
                    Tag = item
                };
                btn.FlatAppearance.BorderColor = accent;
                btn.FlatAppearance.BorderSize = 1;
                btn.MouseEnter += (s, e) =>
                {
                    btn.BackColor = accent;
                    btn.ForeColor = Color.White;
                };
                btn.MouseLeave += (s, e) =>
                {
                    btn.BackColor = btnBg;
                    btn.ForeColor = btnFore;
                };
                btn.Click += (s, e) => ExecuteToolbarItem(item);

                _attachmentToolbar.Controls.Add(btn);
            }

            _attachmentToolbar.Visible = _enableAttachments && _toolbarItems.Count > 0;
            _attachmentToolbar.ResumeLayout();
        }

        private void ExecuteToolbarItem(ChatToolbarItem item)
        {
            switch (item.CommandType)
            {
                case ChatToolbarCommandType.AttachFile:
                    using (var ofd = new OpenFileDialog
                    {
                        Title = item.FileDialogTitle ?? "Select file",
                        Filter = item.FileFilter ?? "All Files (*.*)|*.*"
                    })
                    {
                        if (ofd.ShowDialog() == DialogResult.OK)
                        {
                            string fileName = System.IO.Path.GetFileName(ofd.FileName);
                            string attachType = item.AttachmentType ?? "file";
                            string promptText = !string.IsNullOrWhiteSpace(item.CommandValue)
                                ? item.CommandValue!
                                : $"[{item.Text}]: {fileName}";

                            AppendUserMessage(promptText);
                            FileAttached?.Invoke(this, (ofd.FileName, attachType));

                            if (_flowProvider != null)
                            {
                                try
                                {
                                    string dataUri = ZFlowChatClient.EncodeFileToDataUri(ofd.FileName);
                                    _ = ExecuteFlowPromptAsync(promptText, dataUri);
                                }
                                catch (Exception ex)
                                {
                                    AppendAssistantMessage($"⚠️ File encoding error: {ex.Message}");
                                }
                            }
                        }
                    }
                    break;

                case ChatToolbarCommandType.PrefillInput:
                    if (!string.IsNullOrEmpty(item.CommandValue))
                    {
                        _promptInputBox.Text = item.CommandValue!;
                        _promptInputBox.Focus();
                        _promptInputBox.SelectionStart = _promptInputBox.Text.Length;
                    }
                    ToolbarItemClicked?.Invoke(this, item);
                    break;

                case ChatToolbarCommandType.SendPrompt:
                    if (!string.IsNullOrWhiteSpace(item.CommandValue) && !_isGenerating)
                    {
                        string text = item.CommandValue!.Trim();
                        AppendUserMessage(text);
                        SendMessageRequested?.Invoke(this, text);
                        if (_flowProvider != null)
                        {
                            _ = ExecuteFlowPromptAsync(text);
                        }
                    }
                    ToolbarItemClicked?.Invoke(this, item);
                    break;

                default:
                    ToolbarItemClicked?.Invoke(this, item);
                    break;
            }
        }

        public void UpdateContextBarUI()
        {
            if (_contextBarPanel == null || _contextBarLabel == null || _btnClearContext == null) return;
            if (InvokeRequired)
            {
                BeginInvoke(new Action(UpdateContextBarUI));
                return;
            }

            string summary = _sessionContext.FormatDisplaySummary();
            bool hasTags = !string.IsNullOrWhiteSpace(summary);

            _contextBarPanel.Visible = _showContextBar || hasTags;
            _contextBarLabel.Text = hasTags
                ? $"{_contextBarPrefix} {summary}".Trim()
                : $"{_contextBarPrefix} {_contextBarEmptyText}".Trim();

            _btnClearContext.Visible = hasTags;
            _btnClearContext.Location = new Point(Math.Max(160, _contextBarPanel.Width - 84), 4);
        }

        private void ApplyGenericComponentTheme(bool isDark)
        {
            if (_contextBarPanel != null)
            {
                _contextBarPanel.BackColor = isDark ? Color.FromArgb(23, 33, 53) : Color.FromArgb(238, 242, 255);
            }
            if (_contextBarLabel != null)
            {
                _contextBarLabel.ForeColor = isDark ? Color.FromArgb(147, 197, 253) : Color.FromArgb(37, 99, 235);
            }
            RebuildToolbarItems();
        }

        private static Color ParseHexColor(string? hex, Color fallback)
        {
            if (string.IsNullOrWhiteSpace(hex)) return fallback;
            try
            {
                return ColorTranslator.FromHtml(hex!.Trim());
            }
            catch
            {
                return fallback;
            }
        }

        #endregion
    }
}
