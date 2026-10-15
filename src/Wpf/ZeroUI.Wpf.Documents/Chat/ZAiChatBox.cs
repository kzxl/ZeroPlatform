using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using ZeroUI.Core.AiMl;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Documents
{
    /// <summary>
    /// Enterprise AI chat box control for WPF with streaming token display, Markdown-styled message bubbles,
    /// prompt suggestions, and responsive conversation layout.
    /// </summary>
    public class ZAiChatBox : Control
    {
        private Border _rootBorder = null!;
        private ScrollViewer _messagesScrollViewer = null!;
        private StackPanel _messagesPanel = null!;
        private TextBox _promptInputBox = null!;
        private Button _sendButton = null!;
        private Button _stopButton = null!;
        private Button _clearButton = null!;
        private TextBlock _statusTextBlock = null!;
        private StackPanel _suggestionsPanel = null!;
        private StackPanel _toolbarPanel = null!;
        private Border _contextBarBorder = null!;
        private TextBlock _contextBarTextBlock = null!;
        private Button _clearContextButton = null!;
        private TextBlock _modelNameTextBlock = null!;
        private bool _isInternalScroll = false;

        private IChatFlowProvider? _flowProvider;
        private ChatContextState _sessionContext = new ChatContextState();
        private ObservableCollection<ChatToolbarItem> _toolbarItems = new ObservableCollection<ChatToolbarItem>();
        private string _workflowId = string.Empty;
        private bool _useStreaming = true;
        private bool _showReasoning = true;
        private bool _showContextBar = false;
        private string _contextBarPrefix = "📌 Context:";
        private string _contextBarEmptyText = "Global";
        private string _attachmentDataKey = "image_base64";
        private System.Threading.CancellationTokenSource? _activeFlowCts;

        static ZAiChatBox()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ZAiChatBox), new FrameworkPropertyMetadata(typeof(ZAiChatBox)));
        }

        public ZAiChatBox()
        {
            Messages = new ObservableCollection<ChatMessage>();
            PromptSuggestions = new ObservableCollection<string>();

            Messages.CollectionChanged += OnMessagesCollectionChanged;
            PromptSuggestions.CollectionChanged += OnPromptSuggestionsCollectionChanged;
            _toolbarItems.CollectionChanged += (s, e) => RebuildToolbarItems();

            InitializeComponentHierarchy();
            Loaded += (s, e) =>
            {
                RebuildAllMessages();
                RebuildToolbarItems();
                UpdateContextBarUI();
            };
        }

        #region Dependency & Generic Flow Properties

        public static readonly DependencyProperty MessagesProperty =
            DependencyProperty.Register(nameof(Messages), typeof(ObservableCollection<ChatMessage>), typeof(ZAiChatBox),
                new PropertyMetadata(null, OnMessagesPropertyChanged));

        public static readonly DependencyProperty PromptSuggestionsProperty =
            DependencyProperty.Register(nameof(PromptSuggestions), typeof(ObservableCollection<string>), typeof(ZAiChatBox),
                new PropertyMetadata(null));

        public static readonly DependencyProperty AssistantNameProperty =
            DependencyProperty.Register(nameof(AssistantName), typeof(string), typeof(ZAiChatBox),
                new PropertyMetadata("ZeroCopilot", OnAssistantNameChanged));

        public static readonly DependencyProperty ModelNameProperty =
            DependencyProperty.Register(nameof(ModelName), typeof(string), typeof(ZAiChatBox),
                new PropertyMetadata("ZeroInference Edge", OnModelNameChanged));

        public static readonly DependencyProperty IsGeneratingProperty =
            DependencyProperty.Register(nameof(IsGenerating), typeof(bool), typeof(ZAiChatBox),
                new PropertyMetadata(false, OnIsGeneratingChanged));

        public static readonly DependencyProperty StreamingStateProperty =
            DependencyProperty.Register(nameof(StreamingState), typeof(ChatStreamingState), typeof(ZAiChatBox),
                new PropertyMetadata(ChatStreamingState.Idle, OnStreamingStateChanged));

        public ObservableCollection<ChatMessage> Messages
        {
            get => (ObservableCollection<ChatMessage>)GetValue(MessagesProperty);
            set => SetValue(MessagesProperty, value);
        }

        public ObservableCollection<string> PromptSuggestions
        {
            get => (ObservableCollection<string>)GetValue(PromptSuggestionsProperty);
            set => SetValue(PromptSuggestionsProperty, value);
        }

        public ObservableCollection<ChatToolbarItem> ToolbarItems
        {
            get => _toolbarItems;
            set
            {
                _toolbarItems = value ?? new ObservableCollection<ChatToolbarItem>();
                _toolbarItems.CollectionChanged += (s, e) => RebuildToolbarItems();
                RebuildToolbarItems();
            }
        }

        public IChatFlowProvider? FlowProvider
        {
            get => _flowProvider;
            set
            {
                _flowProvider = value;
                UpdateContextBarUI();
            }
        }

        public ChatContextState SessionContext
        {
            get => _sessionContext;
            set
            {
                _sessionContext = value ?? new ChatContextState();
                UpdateContextBarUI();
            }
        }

        public string WorkflowId
        {
            get => _workflowId;
            set => _workflowId = value ?? string.Empty;
        }

        public bool UseStreaming
        {
            get => _useStreaming;
            set => _useStreaming = value;
        }

        public bool ShowReasoning
        {
            get => _showReasoning;
            set
            {
                _showReasoning = value;
                RebuildAllMessages();
            }
        }

        public bool ShowContextBar
        {
            get => _showContextBar;
            set
            {
                _showContextBar = value;
                UpdateContextBarUI();
            }
        }

        public string ContextBarPrefix
        {
            get => _contextBarPrefix;
            set
            {
                _contextBarPrefix = value ?? string.Empty;
                UpdateContextBarUI();
            }
        }

        public string ContextBarEmptyText
        {
            get => _contextBarEmptyText;
            set
            {
                _contextBarEmptyText = value ?? string.Empty;
                UpdateContextBarUI();
            }
        }

        public string AttachmentDataKey
        {
            get => _attachmentDataKey;
            set => _attachmentDataKey = string.IsNullOrWhiteSpace(value) ? "image_base64" : value;
        }

        public string AssistantName
        {
            get => (string)GetValue(AssistantNameProperty);
            set => SetValue(AssistantNameProperty, value);
        }

        public string ModelName
        {
            get => (string)GetValue(ModelNameProperty);
            set => SetValue(ModelNameProperty, value);
        }

        public bool IsGenerating
        {
            get => (bool)GetValue(IsGeneratingProperty);
            set => SetValue(IsGeneratingProperty, value);
        }

        public ChatStreamingState StreamingState
        {
            get => (ChatStreamingState)GetValue(StreamingStateProperty);
            set => SetValue(StreamingStateProperty, value);
        }

        private static void OnMessagesPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZAiChatBox chat)
            {
                if (e.OldValue is ObservableCollection<ChatMessage> oldCol)
                    oldCol.CollectionChanged -= chat.OnMessagesCollectionChanged;
                if (e.NewValue is ObservableCollection<ChatMessage> newCol)
                    newCol.CollectionChanged += chat.OnMessagesCollectionChanged;
                chat.RebuildAllMessages();
            }
        }

        private static void OnAssistantNameChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZAiChatBox chat && chat._modelNameTextBlock != null)
                chat._modelNameTextBlock.Text = $"{e.NewValue} ({chat.ModelName})";
        }

        private static void OnModelNameChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZAiChatBox chat && chat._modelNameTextBlock != null)
                chat._modelNameTextBlock.Text = $"{chat.AssistantName} ({e.NewValue})";
        }

        private static void OnIsGeneratingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZAiChatBox chat)
            {
                bool generating = (bool)e.NewValue;
                chat._sendButton.Visibility = generating ? Visibility.Collapsed : Visibility.Visible;
                chat._stopButton.Visibility = generating ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private static void OnStreamingStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZAiChatBox chat && chat._statusTextBlock != null)
            {
                var state = (ChatStreamingState)e.NewValue;
                chat._statusTextBlock.Text = state switch
                {
                    ChatStreamingState.Thinking => "Thinking...",
                    ChatStreamingState.Streaming => "Generating response...",
                    ChatStreamingState.Error => "Error during generation",
                    _ => "Ready"
                };
            }
        }

        #endregion

        #region Events

        public event EventHandler<string>? SendMessageRequested;
        public event EventHandler? StopGenerationRequested;
        public event EventHandler? ClearChatRequested;
        public event EventHandler<ChatActionItem>? ActionItemClicked;
        public event EventHandler<ChatToolbarItem>? ToolbarItemClicked;
        public event EventHandler<ChatFlowResult>? FlowCompleted;
        public event EventHandler<ChatContextState>? ContextStateChanged;

        #endregion

        #region UI Layout Construction

        private void InitializeComponentHierarchy()
        {
            _rootBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(15, 23, 42)), // Slate 900
                BorderBrush = new SolidColorBrush(Color.FromRgb(51, 65, 85)), // Slate 700
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8)
            };

            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 0: Header
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 1: Context Bar
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // 2: Messages
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 3: Suggestions
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 4: Toolbar
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 5: Input deck

            // 1. Header
            var headerBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(180, 30, 41, 59)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(51, 65, 85)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(14, 10, 14, 10)
            };
            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var titleStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var botIcon = new TextBlock
            {
                Text = "🤖",
                FontSize = 18,
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            titleStack.Children.Add(botIcon);

            var titleInfoStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            _modelNameTextBlock = new TextBlock
            {
                Text = $"{AssistantName} ({ModelName})",
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(241, 245, 249)),
                FontSize = 13
            };
            _statusTextBlock = new TextBlock
            {
                Text = "Ready",
                Foreground = new SolidColorBrush(Color.FromRgb(52, 211, 153)), // Emerald 400
                FontSize = 11
            };
            titleInfoStack.Children.Add(_modelNameTextBlock);
            titleInfoStack.Children.Add(_statusTextBlock);
            titleStack.Children.Add(titleInfoStack);
            headerGrid.Children.Add(titleStack);

            _clearButton = new Button
            {
                Content = "🗑 Clear",
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Padding = new Thickness(8, 4, 8, 4),
                VerticalAlignment = VerticalAlignment.Center
            };
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
            Grid.SetColumn(_clearButton, 1);
            headerGrid.Children.Add(_clearButton);
            headerBorder.Child = headerGrid;
            mainGrid.Children.Add(headerBorder);

            // 1.5 Generic Context Bar
            _contextBarBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(23, 33, 53)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(51, 65, 85)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(14, 4, 14, 4),
                Visibility = Visibility.Collapsed
            };
            var ctxGrid = new Grid();
            ctxGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            ctxGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _contextBarTextBlock = new TextBlock
            {
                Text = string.Empty,
                Foreground = new SolidColorBrush(Color.FromRgb(147, 197, 253)),
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center
            };
            ctxGrid.Children.Add(_contextBarTextBlock);

            _clearContextButton = new Button
            {
                Content = "× Clear",
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(Color.FromRgb(248, 113, 113)),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                FontSize = 10,
                Visibility = Visibility.Collapsed
            };
            _clearContextButton.Click += (s, e) =>
            {
                _sessionContext.ClearTagsAndParameters(preserveIdentity: true);
                UpdateContextBarUI();
                ContextStateChanged?.Invoke(this, _sessionContext);
            };
            Grid.SetColumn(_clearContextButton, 1);
            ctxGrid.Children.Add(_clearContextButton);
            _contextBarBorder.Child = ctxGrid;
            Grid.SetRow(_contextBarBorder, 1);
            mainGrid.Children.Add(_contextBarBorder);

            // 2. Messages Scroller
            _messagesScrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Padding = new Thickness(14)
            };
            _messagesPanel = new StackPanel();
            _messagesScrollViewer.Content = _messagesPanel;
            Grid.SetRow(_messagesScrollViewer, 2);
            mainGrid.Children.Add(_messagesScrollViewer);

            // 3. Suggestions Panel
            _suggestionsPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(14, 4, 14, 6)
            };
            Grid.SetRow(_suggestionsPanel, 3);
            mainGrid.Children.Add(_suggestionsPanel);

            // 3.5 Generic Toolbar Panel
            _toolbarPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(14, 2, 14, 6),
                Visibility = Visibility.Collapsed
            };
            Grid.SetRow(_toolbarPanel, 4);
            mainGrid.Children.Add(_toolbarPanel);

            // 4. Input Deck
            var inputBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(160, 30, 41, 59)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(51, 65, 85)),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(12, 10, 12, 10)
            };
            var inputGrid = new Grid();
            inputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            inputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _promptInputBox = new TextBox
            {
                Background = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
                Foreground = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(71, 85, 105)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 8, 10, 8),
                FontSize = 13,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                MaxHeight = 110,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Margin = new Thickness(0, 0, 8, 0)
            };
            _promptInputBox.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter && !Keyboard.IsKeyDown(Key.LeftShift) && !Keyboard.IsKeyDown(Key.RightShift))
                {
                    e.Handled = true;
                    SubmitPrompt();
                }
            };
            inputGrid.Children.Add(_promptInputBox);

            var buttonStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Bottom };

            _sendButton = new Button
            {
                Content = "Send ➢",
                Background = new SolidColorBrush(Color.FromRgb(79, 70, 229)), // Indigo
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Padding = new Thickness(14, 8, 14, 8)
            };
            _sendButton.Click += (s, e) => SubmitPrompt();
            buttonStack.Children.Add(_sendButton);

            _stopButton = new Button
            {
                Content = "⏹ Stop",
                Background = new SolidColorBrush(Color.FromRgb(220, 38, 38)), // Red
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Padding = new Thickness(14, 8, 14, 8),
                Visibility = Visibility.Collapsed
            };
            _stopButton.Click += (s, e) =>
            {
                CancelActiveFlowRequest();
                StopGenerationRequested?.Invoke(this, EventArgs.Empty);
            };
            buttonStack.Children.Add(_stopButton);

            Grid.SetColumn(buttonStack, 1);
            inputGrid.Children.Add(buttonStack);
            inputBorder.Child = inputGrid;
            Grid.SetRow(inputBorder, 5);
            mainGrid.Children.Add(inputBorder);

            _rootBorder.Child = mainGrid;
            AddVisualChild(_rootBorder);
            AddLogicalChild(_rootBorder);
        }

        protected override int VisualChildrenCount => 1;
        protected override Visual GetVisualChild(int index) => _rootBorder;

        protected override Size MeasureOverride(Size constraint)
        {
            _rootBorder.Measure(constraint);
            return _rootBorder.DesiredSize;
        }

        protected override Size ArrangeOverride(Size arrangeBounds)
        {
            _rootBorder.Arrange(new Rect(arrangeBounds));
            return arrangeBounds;
        }

        #endregion

        #region Prompt & Generic Flow Execution

        public ZAiChatBox ConfigureFlow(Action<ZFlowChatOptions> configure, IChatFlowProvider? customProvider = null)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            var options = new ZFlowChatOptions();
            configure(options);
            return ConfigureFlow(options, customProvider);
        }

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

            if (options.PromptSuggestions.Count > 0)
            {
                PromptSuggestions.Clear();
                foreach (var s in options.PromptSuggestions)
                {
                    PromptSuggestions.Add(s);
                }
            }

            UpdateContextBarUI();
            RebuildToolbarItems();
            return this;
        }

        public ZAiChatBox SetFlowContextParameter(string key, object? value, string? displayTagLabel = null)
        {
            _sessionContext.SetParameter(key, value, displayTagLabel);
            UpdateContextBarUI();
            return this;
        }

        private void SubmitPrompt()
        {
            string text = _promptInputBox.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(text) || IsGenerating) return;

            _promptInputBox.Clear();
            AppendUserMessage(text);
            SendMessageRequested?.Invoke(this, text);

            if (_flowProvider != null)
            {
                _ = ExecuteFlowPromptAsync(text);
            }
        }

        public async System.Threading.Tasks.Task<ChatFlowResult?> ExecuteFlowPromptAsync(string prompt, string? attachmentDataUri = null)
        {
            if (_flowProvider == null || string.IsNullOrWhiteSpace(prompt)) return null;

            CancelActiveFlowRequest();
            _activeFlowCts = new System.Threading.CancellationTokenSource();
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
            StreamingState = ChatStreamingState.Thinking;
            ChatMessage? assistantMsg = null;

            try
            {
                ChatFlowResult result;
                if (_useStreaming)
                {
                    result = await _flowProvider.StreamAsync(
                        request,
                        chunk =>
                        {
                            Dispatcher.Invoke(() =>
                            {
                                if (chunk.EventType == "token" && !string.IsNullOrEmpty(chunk.Token))
                                {
                                    if (assistantMsg == null)
                                    {
                                        assistantMsg = AppendAssistantMessage(string.Empty);
                                        StreamingState = ChatStreamingState.Streaming;
                                    }
                                    StreamToken(assistantMsg.Id, chunk.Token!);
                                }
                            });
                        },
                        token);
                }
                else
                {
                    result = await _flowProvider.ExecuteAsync(request, token);
                }

                if (assistantMsg == null)
                {
                    assistantMsg = AppendAssistantMessage(result.Output ?? string.Empty);
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

                RebuildAllMessages();
                FlowCompleted?.Invoke(this, result);
                return result;
            }
            catch (Exception ex)
            {
                if (assistantMsg == null)
                {
                    AppendAssistantMessage($"⚠️ {ex.Message}");
                }
                StreamingState = ChatStreamingState.Error;
                return null;
            }
            finally
            {
                IsGenerating = false;
                if (StreamingState != ChatStreamingState.Error)
                {
                    StreamingState = ChatStreamingState.Idle;
                }
            }
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

        public void UpdateContextBarUI()
        {
            if (_contextBarBorder == null || _contextBarTextBlock == null || _clearContextButton == null) return;
            string summary = _sessionContext.FormatDisplaySummary();
            bool hasTags = !string.IsNullOrWhiteSpace(summary);

            _contextBarBorder.Visibility = (_showContextBar || hasTags) ? Visibility.Visible : Visibility.Collapsed;
            _contextBarTextBlock.Text = hasTags
                ? $"{_contextBarPrefix} {summary}".Trim()
                : $"{_contextBarPrefix} {_contextBarEmptyText}".Trim();
            _clearContextButton.Visibility = hasTags ? Visibility.Visible : Visibility.Collapsed;
        }

        public void RebuildToolbarItems()
        {
            if (_toolbarPanel == null) return;
            _toolbarPanel.Children.Clear();

            foreach (var item in _toolbarItems)
            {
                var btn = new Button
                {
                    Content = item.Text,
                    Background = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
                    Foreground = new SolidColorBrush(Color.FromRgb(147, 197, 253)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(59, 130, 246)),
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(8, 4, 8, 4),
                    Margin = new Thickness(0, 0, 8, 0),
                    Cursor = Cursors.Hand,
                    FontSize = 11
                };
                var captured = item;
                btn.Click += (s, e) =>
                {
                    if (captured.CommandType == ChatToolbarCommandType.PrefillInput && !string.IsNullOrEmpty(captured.CommandValue))
                    {
                        _promptInputBox.Text = captured.CommandValue!;
                        _promptInputBox.Focus();
                        _promptInputBox.CaretIndex = _promptInputBox.Text.Length;
                    }
                    else if (captured.CommandType == ChatToolbarCommandType.SendPrompt && !string.IsNullOrWhiteSpace(captured.CommandValue) && !IsGenerating)
                    {
                        string text = captured.CommandValue!.Trim();
                        AppendUserMessage(text);
                        SendMessageRequested?.Invoke(this, text);
                        if (_flowProvider != null)
                        {
                            _ = ExecuteFlowPromptAsync(text);
                        }
                    }
                    ToolbarItemClicked?.Invoke(this, captured);
                };
                _toolbarPanel.Children.Add(btn);
            }

            _toolbarPanel.Visibility = _toolbarItems.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        #endregion

        #region Message Bubbles Rendering

        private void OnMessagesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            RebuildAllMessages();
        }

        private void OnPromptSuggestionsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            RebuildPromptSuggestions();
        }

        public void RebuildAllMessages()
        {
            if (_messagesPanel == null) return;
            _messagesPanel.Children.Clear();

            if (Messages == null || Messages.Count == 0)
            {
                var placeholder = new TextBlock
                {
                    Text = "No conversation history. Ask a question to begin.",
                    Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                    FontSize = 13,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 30, 0, 0)
                };
                _messagesPanel.Children.Add(placeholder);
                return;
            }

            foreach (var msg in Messages)
            {
                var bubble = CreateMessageBubble(msg);
                _messagesPanel.Children.Add(bubble);
            }

            ScrollToBottom();
        }

        private UIElement CreateMessageBubble(ChatMessage msg)
        {
            bool isUser = msg.Role == ChatRole.User;

            var container = new Border
            {
                Margin = new Thickness(0, 4, 0, 8),
                HorizontalAlignment = isUser ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                MaxWidth = 650
            };

            var bubbleBorder = new Border
            {
                Background = isUser
                    ? new SolidColorBrush(Color.FromRgb(30, 41, 59))
                    : new SolidColorBrush(Color.FromRgb(17, 24, 39)),
                BorderBrush = isUser
                    ? new SolidColorBrush(Color.FromRgb(99, 102, 241)) // Violet border for user
                    : new SolidColorBrush(Color.FromRgb(51, 65, 85)), // Slate border for assistant
                BorderThickness = new Thickness(1),
                CornerRadius = isUser ? new CornerRadius(10, 10, 2, 10) : new CornerRadius(10, 10, 10, 2),
                Padding = new Thickness(12, 8, 12, 8)
            };

            var bubbleStack = new StackPanel();

            // Header of bubble
            var headerGrid = new Grid { Margin = new Thickness(0, 0, 0, 4) };
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var senderTextBlock = new TextBlock
            {
                Text = isUser ? (string.IsNullOrWhiteSpace(msg.SenderName) || msg.SenderName == "Operator" ? "Bạn" : msg.SenderName) : (msg.ModelName ?? AssistantName),
                FontWeight = FontWeights.SemiBold,
                FontSize = 11,
                Foreground = isUser ? new SolidColorBrush(Color.FromRgb(165, 180, 252)) : new SolidColorBrush(Color.FromRgb(52, 211, 153))
            };
            headerGrid.Children.Add(senderTextBlock);

            string timeAndLatency = msg.Timestamp.ToLocalTime().ToString("HH:mm:ss");
            if (!isUser && msg.LatencyMs.HasValue && msg.LatencyMs.Value > 0)
            {
                timeAndLatency += $"  ⚡ {msg.LatencyMs.Value:F0}ms";
            }

            var timeTextBlock = new TextBlock
            {
                Text = timeAndLatency,
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                Margin = new Thickness(8, 0, 0, 0)
            };
            Grid.SetColumn(timeTextBlock, 1);
            headerGrid.Children.Add(timeTextBlock);
            bubbleStack.Children.Add(headerGrid);

            if (!isUser && _showReasoning && !string.IsNullOrWhiteSpace(msg.Reasoning))
            {
                var reasoningBlock = new TextBlock
                {
                    Text = $"🧠 {msg.Reasoning}",
                    FontSize = 11,
                    FontStyle = FontStyles.Italic,
                    Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                    Margin = new Thickness(0, 0, 0, 4),
                    TextWrapping = TextWrapping.Wrap
                };
                bubbleStack.Children.Add(reasoningBlock);
            }

            // Body text / Markdown rendering
            var contentBlock = new TextBox
            {
                Text = msg.Content,
                IsReadOnly = true,
                TextWrapping = TextWrapping.Wrap,
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(Color.FromRgb(241, 245, 249)),
                BorderThickness = new Thickness(0),
                FontSize = 13,
                Padding = new Thickness(0),
                Cursor = Cursors.Arrow
            };
            bubbleStack.Children.Add(contentBlock);

            // Generic Action Chips
            if (!isUser && msg.Actions != null && msg.Actions.Count > 0)
            {
                var actionsWrap = new WrapPanel { Margin = new Thickness(0, 6, 0, 2) };
                foreach (var act in msg.Actions)
                {
                    string modesBadge = act.AllowedModes != null && act.AllowedModes.Count > 0
                        ? $" [{string.Join("|", act.AllowedModes)}]"
                        : string.Empty;
                    var actBtn = new Button
                    {
                        Content = $"🔗 {act.Label}{modesBadge}",
                        Background = new SolidColorBrush(Color.FromRgb(23, 37, 84)),
                        Foreground = new SolidColorBrush(Color.FromRgb(147, 197, 253)),
                        BorderBrush = new SolidColorBrush(Color.FromRgb(59, 130, 246)),
                        BorderThickness = new Thickness(1),
                        Padding = new Thickness(8, 3, 8, 3),
                        Margin = new Thickness(0, 0, 6, 4),
                        Cursor = Cursors.Hand,
                        FontSize = 11
                    };
                    var captured = act;
                    actBtn.Click += (s, e) => ActionItemClicked?.Invoke(this, captured);
                    actionsWrap.Children.Add(actBtn);
                }
                bubbleStack.Children.Add(actionsWrap);
            }

            // Copy action for assistant messages
            if (!isUser)
            {
                var copyButton = new Button
                {
                    Content = "📋 Copy",
                    Background = Brushes.Transparent,
                    Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                    BorderThickness = new Thickness(0),
                    FontSize = 10,
                    Cursor = Cursors.Hand,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = new Thickness(0, 4, 0, 0)
                };
                copyButton.Click += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(msg.Content))
                    {
                        Clipboard.SetText(msg.Content);
                    }
                };
                bubbleStack.Children.Add(copyButton);
            }

            bubbleBorder.Child = bubbleStack;
            container.Child = bubbleBorder;
            return container;
        }

        public void RebuildPromptSuggestions()
        {
            if (_suggestionsPanel == null) return;
            _suggestionsPanel.Children.Clear();

            foreach (var suggestion in PromptSuggestions)
            {
                var chip = new Button
                {
                    Content = suggestion,
                    Background = new SolidColorBrush(Color.FromArgb(140, 30, 41, 59)),
                    Foreground = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(71, 85, 105)),
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(10, 4, 10, 4),
                    Margin = new Thickness(0, 0, 8, 0),
                    Cursor = Cursors.Hand,
                    FontSize = 11
                };
                chip.Click += (s, e) =>
                {
                    _promptInputBox.Text = suggestion;
                    _promptInputBox.Focus();
                    _promptInputBox.CaretIndex = suggestion.Length;
                };
                _suggestionsPanel.Children.Add(chip);
            }
        }

        #endregion

        #region Public Methods

        public void AppendUserMessage(string text)
        {
            var msg = ChatMessage.User(text);
            Messages.Add(msg);
        }

        public ChatMessage AppendAssistantMessage(string initialText = "")
        {
            var msg = ChatMessage.Assistant(initialText, ModelName);
            Messages.Add(msg);
            return msg;
        }

        public void StreamToken(string messageId, string token)
        {
            var msg = Messages.FirstOrDefault(m => m.Id == messageId);
            if (msg != null)
            {
                msg.Content += token;
                msg.IsStreaming = true;
                RebuildAllMessages();
            }
        }

        public void CompleteStreaming(string messageId)
        {
            var msg = Messages.FirstOrDefault(m => m.Id == messageId);
            if (msg != null)
            {
                msg.IsStreaming = false;
                RebuildAllMessages();
            }
        }

        public void ClearMessages()
        {
            Messages.Clear();
            RebuildAllMessages();
        }

        public void ScrollToBottom()
        {
            _messagesScrollViewer?.ScrollToBottom();
        }

        #endregion
    }

    #region Backward Compatibility Shims

    [Obsolete("AiChatBox is deprecated. Please migrate to ZAiChatBox instead.")]
    public class AiChatBox : ZAiChatBox { }

    [Obsolete("ZeroAiChatBox is deprecated. Please migrate to ZAiChatBox instead.")]
    public class ZeroAiChatBox : ZAiChatBox { }

    [Obsolete("ZCopilotBox is deprecated. Please migrate to ZAiChatBox instead.")]
    public class ZCopilotBox : ZAiChatBox { }

    #endregion
}
