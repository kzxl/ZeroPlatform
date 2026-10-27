using System;
using System.Collections.Generic;

namespace ZeroPrompt.Core.Grammar
{
    /// <summary>
    /// High-performance JSON Schema Pushdown Automaton (PDA).
    /// Enforces property name whitelisting, required property completeness, and strict data type constraints
    /// using zero-allocation bitmasks and fixed-size bracket buffers.
    /// </summary>
    public sealed class SchemaConstrainedJsonGrammar
    {
        private const int MaxBrackets = 16;
        private readonly char[] _bracketBuffer = new char[MaxBrackets];
        private int _bracketTop = 0;

        public JsonParseState State { get; private set; } = JsonParseState.Start;
        public string CurrentKey { get; private set; } = string.Empty;
        private bool _isEscaped = false;

        public JsonSchemaConstraint? Schema { get; }

        // Bitmask optimization for properties (up to 64 properties per tool schema)
        private readonly string[] _propertyNames;
        private readonly SchemaPropertyType[] _propertyTypes;
        private readonly ulong _requiredMask;
        private ulong _seenMask = 0;

        public bool IsCompleted => State == JsonParseState.Completed && _bracketTop == 0;
        public bool IsInError => State == JsonParseState.Error;

        public SchemaConstrainedJsonGrammar(JsonSchemaConstraint? schema = null)
        {
            Schema = schema;

            if (schema != null && schema.Properties.Count > 0)
            {
                int count = Math.Min(64, schema.Properties.Count);
                _propertyNames = new string[count];
                _propertyTypes = new SchemaPropertyType[count];

                int idx = 0;
                ulong req = 0;
                foreach (var kvp in schema.Properties)
                {
                    if (idx >= 64) break;
                    _propertyNames[idx] = kvp.Key;
                    _propertyTypes[idx] = kvp.Value;

                    if (schema.RequiredProperties.Contains(kvp.Key))
                    {
                        req |= (1UL << idx);
                    }
                    idx++;
                }
                _requiredMask = req;
            }
            else
            {
                _propertyNames = Array.Empty<string>();
                _propertyTypes = Array.Empty<SchemaPropertyType>();
                _requiredMask = 0;
            }
        }

        private SchemaConstrainedJsonGrammar(
            SchemaConstrainedJsonGrammar source,
            JsonParseState state,
            string currentKey,
            bool isEscaped,
            ulong seenMask,
            int bracketTop,
            char[] bracketBuffer)
        {
            Schema = source.Schema;
            _propertyNames = source._propertyNames;
            _propertyTypes = source._propertyTypes;
            _requiredMask = source._requiredMask;

            State = state;
            CurrentKey = currentKey;
            _isEscaped = isEscaped;
            _seenMask = seenMask;
            _bracketTop = bracketTop;
            Array.Copy(bracketBuffer, _bracketBuffer, bracketTop);
        }

        public bool TryConsume(char c)
        {
            if (IsInError) return false;

            // Whitespace handling outside string literals
            if (char.IsWhiteSpace(c) && State != JsonParseState.InKey && State != JsonParseState.InStringValue)
            {
                return true;
            }

            switch (State)
            {
                case JsonParseState.Start:
                    if (c == '{')
                    {
                        PushBracket('}');
                        State = JsonParseState.ExpectKeyOrEnd;
                        return true;
                    }
                    if (c == '[' && Schema == null)
                    {
                        PushBracket(']');
                        State = JsonParseState.ExpectValue;
                        return true;
                    }
                    State = JsonParseState.Error;
                    return false;

                case JsonParseState.ExpectKeyOrEnd:
                    if (c == '}' && _bracketTop > 0 && PeekBracket() == '}')
                    {
                        // Check if all required properties have been satisfied
                        if (_bracketTop == 1 && (_seenMask & _requiredMask) != _requiredMask)
                        {
                            // Missing required properties! Cannot close.
                            State = JsonParseState.Error;
                            return false;
                        }

                        PopBracket();
                        State = _bracketTop == 0 ? JsonParseState.Completed : JsonParseState.ExpectCommaOrEnd;
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
                        if (Schema != null && _propertyNames.Length > 0)
                        {
                            int propIdx = FindPropertyIndex(CurrentKey);
                            if (propIdx < 0)
                            {
                                // Unknown property not in schema!
                                State = JsonParseState.Error;
                                return false;
                            }
                            _seenMask |= (1UL << propIdx);
                        }
                        State = JsonParseState.ExpectColon;
                        return true;
                    }

                    if (c == '\\' && !_isEscaped)
                    {
                        _isEscaped = true;
                        return true;
                    }

                    _isEscaped = false;
                    string candidateKey = CurrentKey + c;

                    // Prefix check against schema properties if schema is specified
                    if (Schema != null && _propertyNames.Length > 0)
                    {
                        if (!HasPropertyWithPrefix(candidateKey))
                        {
                            State = JsonParseState.Error;
                            return false;
                        }
                    }

                    CurrentKey = candidateKey;
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
                    if (Schema != null && _propertyNames.Length > 0)
                    {
                        int pIdx = FindPropertyIndex(CurrentKey);
                        if (pIdx >= 0)
                        {
                            var expectedType = _propertyTypes[pIdx];
                            if (expectedType == SchemaPropertyType.String && c != '"')
                            {
                                State = JsonParseState.Error;
                                return false;
                            }
                            if (expectedType == SchemaPropertyType.Number && !char.IsDigit(c) && c != '-')
                            {
                                State = JsonParseState.Error;
                                return false;
                            }
                            if (expectedType == SchemaPropertyType.Boolean && c != 't' && c != 'f')
                            {
                                State = JsonParseState.Error;
                                return false;
                            }
                        }
                    }

                    if (c == '"')
                    {
                        State = JsonParseState.InStringValue;
                        return true;
                    }
                    if (c == '{')
                    {
                        PushBracket('}');
                        State = JsonParseState.ExpectKeyOrEnd;
                        return true;
                    }
                    if (c == '[')
                    {
                        PushBracket(']');
                        return true;
                    }
                    if (char.IsDigit(c) || c == '-')
                    {
                        State = JsonParseState.InNumberValue;
                        return true;
                    }
                    if (c == 't' || c == 'f' || c == 'n')
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
                        State = _bracketTop > 0 && PeekBracket() == ']' ? JsonParseState.ExpectValue : JsonParseState.ExpectKeyOrEnd;
                        return true;
                    }
                    if (c == '}' || c == ']')
                    {
                        if (_bracketTop > 0 && PeekBracket() == c)
                        {
                            PopBracket();
                            State = _bracketTop == 0 ? JsonParseState.Completed : JsonParseState.ExpectCommaOrEnd;
                            return true;
                        }
                    }
                    State = JsonParseState.Error;
                    return false;

                case JsonParseState.ExpectCommaOrEnd:
                    if (c == ',')
                    {
                        State = _bracketTop > 0 && PeekBracket() == ']' ? JsonParseState.ExpectValue : JsonParseState.ExpectKeyOrEnd;
                        return true;
                    }
                    if (c == '}' || c == ']')
                    {
                        if (_bracketTop > 0 && PeekBracket() == c)
                        {
                            if (c == '}' && _bracketTop == 1 && (_seenMask & _requiredMask) != _requiredMask)
                            {
                                State = JsonParseState.Error;
                                return false;
                            }

                            PopBracket();
                            State = _bracketTop == 0 ? JsonParseState.Completed : JsonParseState.ExpectCommaOrEnd;
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

        public SchemaConstrainedJsonGrammar Clone()
        {
            return new SchemaConstrainedJsonGrammar(
                this,
                State,
                CurrentKey,
                _isEscaped,
                _seenMask,
                _bracketTop,
                _bracketBuffer);
        }

        private void PushBracket(char b)
        {
            if (_bracketTop < MaxBrackets)
            {
                _bracketBuffer[_bracketTop++] = b;
            }
        }

        private char PopBracket()
        {
            return _bracketTop > 0 ? _bracketBuffer[--_bracketTop] : '\0';
        }

        private char PeekBracket()
        {
            return _bracketTop > 0 ? _bracketBuffer[_bracketTop - 1] : '\0';
        }

        private int FindPropertyIndex(string key)
        {
            for (int i = 0; i < _propertyNames.Length; i++)
            {
                if (string.Equals(_propertyNames[i], key, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
            return -1;
        }

        private bool HasPropertyWithPrefix(string prefix)
        {
            for (int i = 0; i < _propertyNames.Length; i++)
            {
                if ((_seenMask & (1UL << i)) == 0) // not yet emitted
                {
                    if (_propertyNames[i].StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }
}
