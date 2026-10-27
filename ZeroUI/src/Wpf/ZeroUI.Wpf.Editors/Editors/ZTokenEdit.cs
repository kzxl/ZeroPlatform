using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using ZeroUI.Core.Editors;

namespace ZeroUI.Wpf.Editors
{
    /// <summary>
    /// Modern multi-tag / chip editor supporting inline badge tokens, keyboard separation,
    /// backspace deletion, autocomplete suggestions, and clipboard support.
    /// </summary>
    [TemplatePart(Name = "PART_TokensPanel", Type = typeof(Panel))]
    [TemplatePart(Name = "PART_Input", Type = typeof(TextBox))]
    [TemplatePart(Name = "PART_Popup", Type = typeof(Popup))]
    [TemplatePart(Name = "PART_SuggestionsList", Type = typeof(ListBox))]
    public class ZTokenEdit : Control, IZeroEditor
    {
        public static readonly DependencyProperty TokensSourceProperty =
            DependencyProperty.Register(
                nameof(TokensSource),
                typeof(IEnumerable),
                typeof(ZTokenEdit),
                new PropertyMetadata(null, OnTokensSourceChanged));

        public static readonly DependencyProperty AvailableTokensProperty =
            DependencyProperty.Register(
                nameof(AvailableTokens),
                typeof(IEnumerable),
                typeof(ZTokenEdit),
                new PropertyMetadata(null));

        public static readonly DependencyProperty AllowDuplicatesProperty =
            DependencyProperty.Register(
                nameof(AllowDuplicates),
                typeof(bool),
                typeof(ZTokenEdit),
                new PropertyMetadata(false));

        public static readonly DependencyProperty MaxTokensProperty =
            DependencyProperty.Register(
                nameof(MaxTokens),
                typeof(int),
                typeof(ZTokenEdit),
                new PropertyMetadata(0));

        public static readonly DependencyProperty PlaceholderProperty =
            DependencyProperty.Register(
                nameof(Placeholder),
                typeof(string),
                typeof(ZTokenEdit),
                new PropertyMetadata("Add tag..."));

        public static readonly DependencyProperty CornerRadiusProperty =
            DependencyProperty.Register(
                nameof(CornerRadius),
                typeof(CornerRadius),
                typeof(ZTokenEdit),
                new PropertyMetadata(new CornerRadius(6)));

        public static readonly DependencyProperty ReadOnlyProperty =
            DependencyProperty.Register(
                nameof(ReadOnly),
                typeof(bool),
                typeof(ZTokenEdit),
                new PropertyMetadata(false));

        public static readonly DependencyProperty IsModifiedProperty =
            DependencyProperty.Register(
                nameof(IsModified),
                typeof(bool),
                typeof(ZTokenEdit),
                new PropertyMetadata(false));

        public static readonly DependencyProperty EditValueProperty =
            DependencyProperty.Register(
                nameof(EditValue),
                typeof(object),
                typeof(ZTokenEdit),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnEditValueChanged));

        private static readonly DependencyPropertyKey TokensPropertyKey =
            DependencyProperty.RegisterReadOnly(
                nameof(Tokens),
                typeof(ObservableCollection<TokenItem>),
                typeof(ZTokenEdit),
                new FrameworkPropertyMetadata(null));

        public static readonly DependencyProperty TokensProperty = TokensPropertyKey.DependencyProperty;

        public ObservableCollection<TokenItem> Tokens
        {
            get => (ObservableCollection<TokenItem>)GetValue(TokensProperty);
            private set => SetValue(TokensPropertyKey, value);
        }

        public IEnumerable? TokensSource
        {
            get => (IEnumerable?)GetValue(TokensSourceProperty);
            set => SetValue(TokensSourceProperty, value);
        }

        public IEnumerable? AvailableTokens
        {
            get => (IEnumerable?)GetValue(AvailableTokensProperty);
            set => SetValue(AvailableTokensProperty, value);
        }

        public bool AllowDuplicates
        {
            get => (bool)GetValue(AllowDuplicatesProperty);
            set => SetValue(AllowDuplicatesProperty, value);
        }

        public int MaxTokens
        {
            get => (int)GetValue(MaxTokensProperty);
            set => SetValue(MaxTokensProperty, value);
        }

        public string Placeholder
        {
            get => (string)GetValue(PlaceholderProperty);
            set => SetValue(PlaceholderProperty, value);
        }

        public CornerRadius CornerRadius
        {
            get => (CornerRadius)GetValue(CornerRadiusProperty);
            set => SetValue(CornerRadiusProperty, value);
        }

        public bool ReadOnly
        {
            get => (bool)GetValue(ReadOnlyProperty);
            set => SetValue(ReadOnlyProperty, value);
        }

        public bool IsModified
        {
            get => (bool)GetValue(IsModifiedProperty);
            set => SetValue(IsModifiedProperty, value);
        }

        public object? EditValue
        {
            get => GetValue(EditValueProperty);
            set => SetValue(EditValueProperty, value);
        }

        public event EventHandler<TokenItem>? TokenAdded;
        public event EventHandler<TokenItem>? TokenRemoved;
        public event EventHandler? TokensChanged;
        public event EventHandler? EditValueChanged;

        private readonly TokenModel _model = new TokenModel();
        private TextBox? _input;
        private Popup? _popup;
        private ListBox? _suggestionsList;
        private bool _isUpdating;

        public TokenModel Model => _model;

        static ZTokenEdit()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ZTokenEdit), new FrameworkPropertyMetadata(typeof(ZTokenEdit)));
        }

        public ZTokenEdit()
        {
            var tokens = new ObservableCollection<TokenItem>();
            SetValue(TokensPropertyKey, tokens);
            tokens.CollectionChanged += OnTokensCollectionChanged;
            MouseLeftButtonDown += (s, e) =>
            {
                if (!ReadOnly && _input != null)
                {
                    _input.Focus();
                }
            };
            AddHandler(Button.ClickEvent, new RoutedEventHandler((s, e) =>
            {
                if (e.OriginalSource is Button btn && btn.CommandParameter is TokenItem token)
                {
                    RemoveToken(token);
                    e.Handled = true;
                }
            }));
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            _input = GetTemplateChild("PART_Input") as TextBox;
            _popup = GetTemplateChild("PART_Popup") as Popup;
            _suggestionsList = GetTemplateChild("PART_SuggestionsList") as ListBox;

            if (_input != null)
            {
                _input.PreviewKeyDown += OnInputPreviewKeyDown;
                _input.TextChanged += OnInputTextChanged;
                _input.LostFocus += (s, e) => CommitCurrentInput();
            }

            if (_suggestionsList != null)
            {
                _suggestionsList.SelectionChanged += OnSuggestionSelected;
            }
        }

        private void OnInputPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (ReadOnly) return;

            if (e.Key == Key.Enter || e.Key == Key.Tab)
            {
                if (_popup != null && _popup.IsOpen && _suggestionsList?.SelectedItem != null)
                {
                    SelectCurrentSuggestion();
                    e.Handled = true;
                }
                else if (!string.IsNullOrWhiteSpace(_input?.Text))
                {
                    CommitCurrentInput();
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.Back)
            {
                if (_input != null && string.IsNullOrEmpty(_input.Text) && Tokens.Count > 0)
                {
                    RemoveToken(Tokens[Tokens.Count - 1]);
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.Down)
            {
                if (_popup != null && _popup.IsOpen && _suggestionsList != null)
                {
                    if (_suggestionsList.SelectedIndex < _suggestionsList.Items.Count - 1)
                    {
                        _suggestionsList.SelectedIndex++;
                    }
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.Up)
            {
                if (_popup != null && _popup.IsOpen && _suggestionsList != null)
                {
                    if (_suggestionsList.SelectedIndex > 0)
                    {
                        _suggestionsList.SelectedIndex--;
                    }
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.Escape)
            {
                if (_popup != null) _popup.IsOpen = false;
                e.Handled = true;
            }
        }

        private void OnInputTextChanged(object sender, TextChangedEventArgs e)
        {
            if (ReadOnly || _input == null) return;

            string text = _input.Text;
            if (text.Contains(",") || text.Contains(";"))
            {
                var parts = text.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var part in parts)
                {
                    AddToken(part.Trim());
                }
                _input.Text = string.Empty;
                if (_popup != null) _popup.IsOpen = false;
                return;
            }

            UpdateSuggestions(text);
        }

        private void UpdateSuggestions(string query)
        {
            if (_popup == null || _suggestionsList == null || AvailableTokens == null) return;

            if (string.IsNullOrWhiteSpace(query))
            {
                _popup.IsOpen = false;
                return;
            }

            _model.AllowDuplicates = AllowDuplicates;
            var availableList = new List<TokenItem>();
            foreach (var item in AvailableTokens)
            {
                if (item is TokenItem t) availableList.Add(t);
                else if (item != null) availableList.Add(new TokenItem(item.ToString() ?? string.Empty));
            }
            _model.AvailableTokens = availableList;

            var matches = _model.FilterSuggestions(query);

            if (matches.Count > 0)
            {
                _suggestionsList.ItemsSource = matches;
                _suggestionsList.SelectedIndex = 0;
                _popup.IsOpen = true;
            }
            else
            {
                _popup.IsOpen = false;
            }
        }

        private void OnSuggestionSelected(object sender, SelectionChangedEventArgs e)
        {
            // selection handled explicitly on Enter / Click
        }

        private void SelectCurrentSuggestion()
        {
            if (_suggestionsList?.SelectedItem != null)
            {
                var sel = _suggestionsList.SelectedItem;
                if (sel is TokenItem t)
                {
                    AddToken(t);
                }
                else
                {
                    AddToken(sel.ToString() ?? string.Empty);
                }

                if (_input != null) _input.Text = string.Empty;
                if (_popup != null) _popup.IsOpen = false;
            }
        }

        public void CommitCurrentInput()
        {
            if (_input == null || string.IsNullOrWhiteSpace(_input.Text)) return;
            string val = _input.Text.Trim();
            AddToken(val);
            _input.Text = string.Empty;
            if (_popup != null) _popup.IsOpen = false;
        }

        public bool AddToken(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            return AddToken(new TokenItem(text.Trim()));
        }

        public bool AddToken(TokenItem token)
        {
            if (ReadOnly) return false;
            _model.AllowDuplicates = AllowDuplicates;
            _model.MaxTokens = MaxTokens;

            if (!_model.Add(token))
            {
                return false;
            }

            if (!Tokens.Contains(token))
            {
                Tokens.Add(token);
            }

            IsModified = true;
            TokenAdded?.Invoke(this, token);
            return true;
        }

        public bool RemoveToken(TokenItem token)
        {
            if (ReadOnly) return false;
            _model.Remove(token);
            bool removed = Tokens.Remove(token);
            if (removed)
            {
                IsModified = true;
                TokenRemoved?.Invoke(this, token);
            }
            return removed;
        }

        public void ClearTokens()
        {
            if (ReadOnly) return;
            _model.Clear();
            Tokens.Clear();
            IsModified = true;
        }

        private void OnTokensCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (_isUpdating) return;

            _isUpdating = true;
            try
            {
                EditValue = _model.ToDelimitedString();
                TokensChanged?.Invoke(this, EventArgs.Empty);
                EditValueChanged?.Invoke(this, EventArgs.Empty);
            }
            finally
            {
                _isUpdating = false;
            }
        }

        private static void OnTokensSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZTokenEdit edit && !edit._isUpdating && e.NewValue is IEnumerable source)
            {
                edit._model.Clear();
                edit.Tokens.Clear();
                foreach (var item in source)
                {
                    if (item is TokenItem t)
                    {
                        edit._model.Add(t);
                        edit.Tokens.Add(t);
                    }
                    else if (item != null)
                    {
                        var tok = new TokenItem(item.ToString() ?? string.Empty);
                        edit._model.Add(tok);
                        edit.Tokens.Add(tok);
                    }
                }
            }
        }

        private static void OnEditValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZTokenEdit edit && !edit._isUpdating && e.NewValue is string s)
            {
                edit._isUpdating = true;
                try
                {
                    edit._model.SetFromDelimitedString(s);
                    edit.Tokens.Clear();
                    foreach (var t in edit._model.Tokens)
                    {
                        edit.Tokens.Add(t);
                    }
                }
                finally
                {
                    edit._isUpdating = false;
                }
            }
        }

        public void Reset()
        {
            ClearTokens();
            IsModified = false;
        }

        public void Clear()
        {
            ClearTokens();
        }
    }


    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZTokenEdit"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("TokenEdit is deprecated and will be removed in 5 release cycles. Please migrate to ZTokenEdit instead.")]
    public class TokenEdit : ZTokenEdit { }

    /// <summary>
    /// Legacy alias for <see cref="ZTokenEdit"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroTokenEdit is deprecated and will be removed in 5 release cycles. Please migrate to ZTokenEdit instead.")]
    public class ZeroTokenEdit : ZTokenEdit { }

    #endregion

}
