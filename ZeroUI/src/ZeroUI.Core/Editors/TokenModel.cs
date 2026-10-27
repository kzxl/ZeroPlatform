using System;
using System.Collections.Generic;
using System.Linq;

namespace ZeroUI.Core.Editors
{
    /// <summary>
    /// Headless state coordinator and collection manager for multi-tag token editors (<c>TokenEdit</c>).
    /// Handles duplicate suppression, token limits, delimited string parsing, and suggestion filtering.
    /// </summary>
    public class TokenModel
    {
        private readonly List<TokenItem> _tokens = new List<TokenItem>();
        private IEnumerable<TokenItem>? _availableTokens;
        private bool _allowDuplicates;
        private int _maxTokens;

        public event EventHandler<TokenItem>? TokenAdded;
        public event EventHandler<TokenItem>? TokenRemoved;
        public event EventHandler? TokensChanged;

        public IReadOnlyList<TokenItem> Tokens => _tokens;

        public IEnumerable<TokenItem>? AvailableTokens
        {
            get => _availableTokens;
            set => _availableTokens = value;
        }

        public bool AllowDuplicates
        {
            get => _allowDuplicates;
            set => _allowDuplicates = value;
        }

        public int MaxTokens
        {
            get => _maxTokens;
            set => _maxTokens = value;
        }

        public int Count => _tokens.Count;

        public bool Add(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            return Add(new TokenItem(text.Trim()));
        }

        public bool Add(TokenItem item)
        {
            if (item is null || string.IsNullOrWhiteSpace(item.Text)) return false;
            if (_maxTokens > 0 && _tokens.Count >= _maxTokens) return false;
            if (!_allowDuplicates && _tokens.Any(t => string.Equals(t.Text, item.Text, StringComparison.OrdinalIgnoreCase)))
                return false;

            _tokens.Add(item);
            TokenAdded?.Invoke(this, item);
            TokensChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }

        public bool Remove(TokenItem item)
        {
            if (item is null) return false;
            bool removed = _tokens.Remove(item);
            if (removed)
            {
                TokenRemoved?.Invoke(this, item);
                TokensChanged?.Invoke(this, EventArgs.Empty);
            }
            return removed;
        }

        public bool RemoveLast()
        {
            if (_tokens.Count == 0) return false;
            var last = _tokens[_tokens.Count - 1];
            return Remove(last);
        }

        public void Clear()
        {
            if (_tokens.Count == 0) return;
            _tokens.Clear();
            TokensChanged?.Invoke(this, EventArgs.Empty);
        }

        public void SetFromDelimitedString(string? text, char[]? delimiters = null)
        {
            Clear();
            if (string.IsNullOrWhiteSpace(text)) return;

            var seps = delimiters ?? new[] { ',', ';' };
            var parts = text!.Split(seps, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                Add(part.Trim());
            }
        }

        public string ToDelimitedString(string delimiter = ", ")
        {
            return string.Join(delimiter, _tokens.Select(t => t.Text));
        }

        public List<TokenItem> FilterSuggestions(string? query, int maxResults = 10)
        {
            if (_availableTokens is null) return new List<TokenItem>();

            var q = query?.Trim() ?? string.Empty;
            var list = new List<TokenItem>();

            foreach (var item in _availableTokens)
            {
                if (list.Count >= maxResults) break;

                // Exclude already added tokens if duplicates not allowed
                if (!_allowDuplicates && _tokens.Any(t => string.Equals(t.Text, item.Text, StringComparison.OrdinalIgnoreCase)))
                    continue;

                if (string.IsNullOrEmpty(q) || item.Text.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    list.Add(item);
                }
            }

            return list;
        }
    }
}
