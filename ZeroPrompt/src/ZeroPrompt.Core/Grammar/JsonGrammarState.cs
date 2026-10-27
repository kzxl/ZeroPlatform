using System;
using System.Collections.Generic;

namespace ZeroPrompt.Core.Grammar
{
    public enum JsonParseState
    {
        Start,
        ExpectKeyOrEnd,
        InKey,
        ExpectColon,
        ExpectValue,
        InStringValue,
        InNumberValue,
        ExpectCommaOrEnd,
        Completed,
        Error
    }

    /// <summary>
    /// Pushdown Automaton (PDA) tracking valid character transitions for JSON structured outputs.
    /// </summary>
    public sealed class JsonGrammarState
    {
        private readonly Stack<char> _bracketStack = new Stack<char>();
        public JsonParseState State { get; private set; } = JsonParseState.Start;
        public string CurrentKey { get; private set; } = string.Empty;
        private bool _isEscaped = false;

        public bool IsCompleted => State == JsonParseState.Completed && _bracketStack.Count == 0;
        public bool IsInError => State == JsonParseState.Error;

        public bool TryConsume(char c)
        {
            if (IsInError) return false;

            // Skip whitespace when not inside a string
            if (char.IsWhiteSpace(c) && State != JsonParseState.InKey && State != JsonParseState.InStringValue)
            {
                return true;
            }

            switch (State)
            {
                case JsonParseState.Start:
                    if (c == '{')
                    {
                        _bracketStack.Push('}');
                        State = JsonParseState.ExpectKeyOrEnd;
                        return true;
                    }
                    if (c == '[')
                    {
                        _bracketStack.Push(']');
                        State = JsonParseState.ExpectValue;
                        return true;
                    }
                    State = JsonParseState.Error;
                    return false;

                case JsonParseState.ExpectKeyOrEnd:
                    if (c == '}' && _bracketStack.Count > 0 && _bracketStack.Peek() == '}')
                    {
                        _bracketStack.Pop();
                        State = _bracketStack.Count == 0 ? JsonParseState.Completed : JsonParseState.ExpectCommaOrEnd;
                        return true;
                    }
                    if (c == '"')
                    {
                        CurrentKey = string.Empty;
                        State = JsonParseState.InKey;
                        return true;
                    }
                    State = JsonParseState.Error;
                    return false;

                case JsonParseState.InKey:
                    if (c == '"' && !_isEscaped)
                    {
                        State = JsonParseState.ExpectColon;
                        return true;
                    }
                    if (c == '\\' && !_isEscaped)
                    {
                        _isEscaped = true;
                    }
                    else
                    {
                        _isEscaped = false;
                        CurrentKey += c;
                    }
                    return true;

                case JsonParseState.ExpectColon:
                    if (c == ':')
                    {
                        State = JsonParseState.ExpectValue;
                        return true;
                    }
                    State = JsonParseState.Error;
                    return false;

                case JsonParseState.ExpectValue:
                    if (c == '"')
                    {
                        State = JsonParseState.InStringValue;
                        return true;
                    }
                    if (c == '{')
                    {
                        _bracketStack.Push('}');
                        State = JsonParseState.ExpectKeyOrEnd;
                        return true;
                    }
                    if (c == '[')
                    {
                        _bracketStack.Push(']');
                        return true;
                    }
                    if (char.IsDigit(c) || c == '-')
                    {
                        State = JsonParseState.InNumberValue;
                        return true;
                    }
                    if (c == 't' || c == 'f' || c == 'n') // true, false, null
                    {
                        State = JsonParseState.ExpectCommaOrEnd;
                        return true;
                    }
                    State = JsonParseState.Error;
                    return false;

                case JsonParseState.InStringValue:
                    if (c == '"' && !_isEscaped)
                    {
                        State = JsonParseState.ExpectCommaOrEnd;
                        return true;
                    }
                    if (c == '\\' && !_isEscaped)
                    {
                        _isEscaped = true;
                    }
                    else
                    {
                        _isEscaped = false;
                    }
                    return true;

                case JsonParseState.InNumberValue:
                    if (char.IsDigit(c) || c == '.' || c == 'e' || c == 'E' || c == '+' || c == '-')
                    {
                        return true;
                    }
                    if (c == ',')
                    {
                        State = _bracketStack.Count > 0 && _bracketStack.Peek() == ']' ? JsonParseState.ExpectValue : JsonParseState.ExpectKeyOrEnd;
                        return true;
                    }
                    if (c == '}' || c == ']')
                    {
                        if (_bracketStack.Count > 0 && _bracketStack.Peek() == c)
                        {
                            _bracketStack.Pop();
                            State = _bracketStack.Count == 0 ? JsonParseState.Completed : JsonParseState.ExpectCommaOrEnd;
                            return true;
                        }
                    }
                    State = JsonParseState.Error;
                    return false;

                case JsonParseState.ExpectCommaOrEnd:
                    if (c == ',')
                    {
                        State = _bracketStack.Count > 0 && _bracketStack.Peek() == ']' ? JsonParseState.ExpectValue : JsonParseState.ExpectKeyOrEnd;
                        return true;
                    }
                    if (c == '}' || c == ']')
                    {
                        if (_bracketStack.Count > 0 && _bracketStack.Peek() == c)
                        {
                            _bracketStack.Pop();
                            State = _bracketStack.Count == 0 ? JsonParseState.Completed : JsonParseState.ExpectCommaOrEnd;
                            return true;
                        }
                    }
                    State = JsonParseState.Error;
                    return false;

                case JsonParseState.Completed:
                    return false;

                default:
                    return false;
            }
        }

        public bool CanAcceptNext(ReadOnlySpan<char> snippet)
        {
            var clone = Clone();
            for (int i = 0; i < snippet.Length; i++)
            {
                if (!clone.TryConsume(snippet[i]))
                {
                    return false;
                }
            }
            return true;
        }

        public JsonGrammarState Clone()
        {
            var copy = new JsonGrammarState
            {
                State = this.State,
                CurrentKey = this.CurrentKey,
                _isEscaped = this._isEscaped
            };

            var temp = new Stack<char>(_bracketStack);
            var reverse = new Stack<char>(temp);
            while (reverse.Count > 0)
            {
                copy._bracketStack.Push(reverse.Pop());
            }

            return copy;
        }
    }
}
