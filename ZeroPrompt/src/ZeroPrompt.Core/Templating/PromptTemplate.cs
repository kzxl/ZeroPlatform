using System;
using System.Collections.Generic;
using System.Text;

namespace ZeroPrompt.Core.Templating
{
    public enum ChunkType
    {
        Literal,
        Variable,
        IfCondition,
        EndIf
    }

    public sealed class TemplateChunk
    {
        public ChunkType Type { get; }
        public string Text { get; }
        public string? Format { get; }

        public TemplateChunk(ChunkType type, string text, string? format = null)
        {
            Type = type;
            Text = text;
            Format = format;
        }
    }

    /// <summary>
    /// High-performance prompt templating engine supporting zero-allocation string rendering.
    /// </summary>
    public sealed class PromptTemplate
    {
        private readonly List<TemplateChunk> _chunks = new List<TemplateChunk>();
        public string RawTemplate { get; }

        public PromptTemplate(string template)
        {
            RawTemplate = template ?? throw new ArgumentNullException(nameof(template));
            ParseChunks(template);
        }

        public static PromptTemplate Parse(string template) => new PromptTemplate(template);

        private void ParseChunks(string template)
        {
            int idx = 0;
            while (idx < template.Length)
            {
                int openIdx = template.IndexOf("{{", idx, StringComparison.Ordinal);
                if (openIdx < 0)
                {
                    _chunks.Add(new TemplateChunk(ChunkType.Literal, template.Substring(idx)));
                    break;
                }

                if (openIdx > idx)
                {
                    _chunks.Add(new TemplateChunk(ChunkType.Literal, template.Substring(idx, openIdx - idx)));
                }

                int closeIdx = template.IndexOf("}}", openIdx + 2, StringComparison.Ordinal);
                if (closeIdx < 0)
                {
                    // Malformed, treat remainder as literal
                    _chunks.Add(new TemplateChunk(ChunkType.Literal, template.Substring(openIdx)));
                    break;
                }

                string tag = template.Substring(openIdx + 2, closeIdx - openIdx - 2).Trim();
                if (tag.StartsWith("#if ", StringComparison.OrdinalIgnoreCase))
                {
                    string varName = tag.Substring(4).Trim();
                    _chunks.Add(new TemplateChunk(ChunkType.IfCondition, varName));
                }
                else if (tag.Equals("/if", StringComparison.OrdinalIgnoreCase))
                {
                    _chunks.Add(new TemplateChunk(ChunkType.EndIf, string.Empty));
                }
                else
                {
                    int colonIdx = tag.IndexOf(':');
                    if (colonIdx > 0)
                    {
                        string varName = tag.Substring(0, colonIdx).Trim();
                        string format = tag.Substring(colonIdx + 1).Trim();
                        _chunks.Add(new TemplateChunk(ChunkType.Variable, varName, format));
                    }
                    else
                    {
                        _chunks.Add(new TemplateChunk(ChunkType.Variable, tag));
                    }
                }

                idx = closeIdx + 2;
            }
        }

        public string Render(IReadOnlyDictionary<string, object> parameters)
        {
            var sb = new StringBuilder(RawTemplate.Length * 2);
            RenderInternal(parameters, sb);
            return sb.ToString();
        }

        public string Render(PromptContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            return Render(context.Values);
        }

        private void RenderInternal(IReadOnlyDictionary<string, object> parameters, StringBuilder sb)
        {
            bool skipping = false;
            int conditionDepth = 0;

            for (int i = 0; i < _chunks.Count; i++)
            {
                var chunk = _chunks[i];

                if (chunk.Type == ChunkType.IfCondition)
                {
                    conditionDepth++;
                    if (!parameters.TryGetValue(chunk.Text, out var val) || IsFalsy(val))
                    {
                        skipping = true;
                    }
                    continue;
                }

                if (chunk.Type == ChunkType.EndIf)
                {
                    conditionDepth--;
                    if (conditionDepth <= 0)
                    {
                        skipping = false;
                        conditionDepth = 0;
                    }
                    continue;
                }

                if (skipping) continue;

                if (chunk.Type == ChunkType.Literal)
                {
                    sb.Append(chunk.Text);
                }
                else if (chunk.Type == ChunkType.Variable)
                {
                    if (parameters.TryGetValue(chunk.Text, out var val) && val != null)
                    {
                        if (chunk.Format != null && val is IFormattable formattable)
                        {
                            sb.Append(formattable.ToString(chunk.Format, null));
                        }
                        else
                        {
                            sb.Append(val.ToString());
                        }
                    }
                }
            }
        }

        private static bool IsFalsy(object? val)
        {
            if (val == null) return true;
            if (val is bool b) return !b;
            if (val is string s) return string.IsNullOrEmpty(s);
            if (val is int i) return i == 0;
            if (val is double d) return Math.Abs(d) < 1e-9;
            return false;
        }
    }
}
