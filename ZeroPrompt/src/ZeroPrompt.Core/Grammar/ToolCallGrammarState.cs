using System;
using System.Collections.Generic;

namespace ZeroPrompt.Core.Grammar
{
    public enum ToolCallGrammarPhase
    {
        WaitTag,
        ExpectRootBrace,
        ExpectToolKey,
        ExpectToolColon,
        InToolName,
        ExpectComma,
        ExpectParamsKey,
        ExpectParamsColon,
        InParameters,
        ExpectClosingBrace,
        ExpectCloseTag,
        Completed,
        Error
    }

    /// <summary>
    /// Strict Finite Automaton enforcing 100% valid tool call syntax:
    /// &lt;tool_call&gt;{"tool": "&lt;tool_name&gt;", "parameters": { &lt;schema_params&gt; }}&lt;/tool_call&gt;
    /// Guarantees that:
    /// 1. Only registered tool names can ever be emitted.
    /// 2. Only valid properties matching that tool's schema are allowed.
    /// 3. All required parameters are satisfied before closing.
    /// 4. Tag closes cleanly without trailing characters.
    /// </summary>
    public sealed class ToolCallGrammarState
    {
        private readonly IReadOnlyDictionary<string, JsonSchemaConstraint> _toolSchemas;
        private readonly string[] _toolNames;

        public ToolCallGrammarPhase Phase { get; private set; } = ToolCallGrammarPhase.WaitTag;
        public string SelectedToolName { get; private set; } = string.Empty;
        public SchemaConstrainedJsonGrammar? ParametersGrammar { get; private set; }

        private string _buffer = string.Empty;
        private int _expectedTextIndex = 0;
        private string _expectedText = string.Empty;

        public bool IsCompleted => Phase == ToolCallGrammarPhase.Completed;
        public bool IsInError => Phase == ToolCallGrammarPhase.Error;

        public ToolCallGrammarState(IReadOnlyDictionary<string, JsonSchemaConstraint> toolSchemas, bool requireStartTag = true)
        {
            _toolSchemas = toolSchemas ?? throw new ArgumentNullException(nameof(toolSchemas));
            var names = new List<string>(toolSchemas.Keys);
            _toolNames = names.ToArray();

            if (requireStartTag)
            {
                Phase = ToolCallGrammarPhase.WaitTag;
                _expectedText = "<tool_call>";
                _expectedTextIndex = 0;
            }
            else
            {
                Phase = ToolCallGrammarPhase.ExpectRootBrace;
            }
        }

        private ToolCallGrammarState(
            ToolCallGrammarState source,
            ToolCallGrammarPhase phase,
            string selectedToolName,
            SchemaConstrainedJsonGrammar? parametersGrammar,
            string buffer,
            int expectedTextIndex,
            string expectedText)
        {
            _toolSchemas = source._toolSchemas;
            _toolNames = source._toolNames;

            Phase = phase;
            SelectedToolName = selectedToolName;
            ParametersGrammar = parametersGrammar?.Clone();
            _buffer = buffer;
            _expectedTextIndex = expectedTextIndex;
            _expectedText = expectedText;
        }

        public bool TryConsume(char c)
        {
            if (IsInError) return false;

            // Skip whitespace outside of literal strings
            if (char.IsWhiteSpace(c))
            {
                if (Phase == ToolCallGrammarPhase.InParameters && ParametersGrammar != null)
                {
                    return ParametersGrammar.TryConsume(c);
                }
                if (Phase != ToolCallGrammarPhase.WaitTag && 
                    Phase != ToolCallGrammarPhase.ExpectCloseTag &&
                    !(Phase == ToolCallGrammarPhase.InToolName && _expectedTextIndex > 0))
                {
                    return true;
                }
            }

            switch (Phase)
            {
                case ToolCallGrammarPhase.WaitTag:
                    if (c == _expectedText[_expectedTextIndex])
                    {
                        _expectedTextIndex++;
                        if (_expectedTextIndex >= _expectedText.Length)
                        {
                            Phase = ToolCallGrammarPhase.ExpectRootBrace;
                        }
                        return true;
                    }
                    Phase = ToolCallGrammarPhase.Error;
                    return false;

                case ToolCallGrammarPhase.ExpectRootBrace:
                    if (c == '{')
                    {
                        Phase = ToolCallGrammarPhase.ExpectToolKey;
                        _expectedText = "\"tool\"";
                        _expectedTextIndex = 0;
                        return true;
                    }
                    Phase = ToolCallGrammarPhase.Error;
                    return false;

                case ToolCallGrammarPhase.ExpectToolKey:
                    if (c == _expectedText[_expectedTextIndex])
                    {
                        _expectedTextIndex++;
                        if (_expectedTextIndex >= _expectedText.Length)
                        {
                            Phase = ToolCallGrammarPhase.ExpectToolColon;
                        }
                        return true;
                    }
                    Phase = ToolCallGrammarPhase.Error;
                    return false;

                case ToolCallGrammarPhase.ExpectToolColon:
                    if (c == ':')
                    {
                        Phase = ToolCallGrammarPhase.InToolName;
                        _buffer = string.Empty;
                        _expectedText = "\""; // Initial opening quote for tool name
                        _expectedTextIndex = 0;
                        return true;
                    }
                    Phase = ToolCallGrammarPhase.Error;
                    return false;

                case ToolCallGrammarPhase.InToolName:
                    if (_expectedTextIndex == 0)
                    {
                        if (char.IsWhiteSpace(c)) return true;
                        if (c == '"')
                        {
                            _expectedTextIndex = 1;
                            return true;
                        }
                        Phase = ToolCallGrammarPhase.Error;
                        return false;
                    }

                    if (c == '"')
                    {
                        // End of tool name string. Verify exact registered tool name
                        if (!_toolSchemas.ContainsKey(_buffer))
                        {
                            Phase = ToolCallGrammarPhase.Error;
                            return false;
                        }

                        SelectedToolName = _buffer;
                        _toolSchemas.TryGetValue(SelectedToolName, out var schema);
                        ParametersGrammar = new SchemaConstrainedJsonGrammar(schema);

                        Phase = ToolCallGrammarPhase.ExpectComma;
                        return true;
                    }

                    string cand = _buffer + c;
                    if (!HasToolWithPrefix(cand))
                    {
                        Phase = ToolCallGrammarPhase.Error;
                        return false;
                    }

                    _buffer = cand;
                    return true;

                case ToolCallGrammarPhase.ExpectComma:
                    if (c == ',')
                    {
                        Phase = ToolCallGrammarPhase.ExpectParamsKey;
                        _expectedText = "\"parameters\"";
                        _expectedTextIndex = 0;
                        return true;
                    }
                    Phase = ToolCallGrammarPhase.Error;
                    return false;

                case ToolCallGrammarPhase.ExpectParamsKey:
                    if (c == _expectedText[_expectedTextIndex])
                    {
                        _expectedTextIndex++;
                        if (_expectedTextIndex >= _expectedText.Length)
                        {
                            Phase = ToolCallGrammarPhase.ExpectParamsColon;
                        }
                        return true;
                    }
                    Phase = ToolCallGrammarPhase.Error;
                    return false;

                case ToolCallGrammarPhase.ExpectParamsColon:
                    if (c == ':')
                    {
                        Phase = ToolCallGrammarPhase.InParameters;
                        return true;
                    }
                    Phase = ToolCallGrammarPhase.Error;
                    return false;

                case ToolCallGrammarPhase.InParameters:
                    if (ParametersGrammar == null)
                    {
                        Phase = ToolCallGrammarPhase.Error;
                        return false;
                    }

                    bool ok = ParametersGrammar.TryConsume(c);
                    if (!ok)
                    {
                        Phase = ToolCallGrammarPhase.Error;
                        return false;
                    }

                    if (ParametersGrammar.IsCompleted)
                    {
                        Phase = ToolCallGrammarPhase.ExpectClosingBrace;
                    }
                    return true;

                case ToolCallGrammarPhase.ExpectClosingBrace:
                    if (c == '}')
                    {
                        Phase = ToolCallGrammarPhase.ExpectCloseTag;
                        _expectedText = "</tool_call>";
                        _expectedTextIndex = 0;
                        return true;
                    }
                    Phase = ToolCallGrammarPhase.Error;
                    return false;

                case ToolCallGrammarPhase.ExpectCloseTag:
                    if (c == _expectedText[_expectedTextIndex])
                    {
                        _expectedTextIndex++;
                        if (_expectedTextIndex >= _expectedText.Length)
                        {
                            Phase = ToolCallGrammarPhase.Completed;
                        }
                        return true;
                    }
                    Phase = ToolCallGrammarPhase.Error;
                    return false;

                case ToolCallGrammarPhase.Completed:
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

        public ToolCallGrammarState Clone()
        {
            return new ToolCallGrammarState(
                this,
                Phase,
                SelectedToolName,
                ParametersGrammar,
                _buffer,
                _expectedTextIndex,
                _expectedText);
        }

        private bool HasToolWithPrefix(string prefix)
        {
            for (int i = 0; i < _toolNames.Length; i++)
            {
                if (_toolNames[i].StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
