using System;
using System.Collections.Generic;
using ZeroAgent.Core.Tools;

namespace ZeroAgent.Core.Reasoning.TreeOfThought
{
    /// <summary>
    /// Represents an individual exploration node within the Tree-of-Thought (ToT) reasoning tree.
    /// Captures the hypothesis (thought), the selected action, observation, and cognitive value score.
    /// </summary>
    public sealed class ThoughtNode
    {
        public string Id { get; }
        public string? ParentId { get; }
        public int Depth { get; }
        public string Thought { get; }
        public ToolCallRequest? Action { get; set; }
        public string? Observation { get; set; }
        public float ValueScore { get; set; } = 0.5f;
        public bool IsTerminal { get; set; }
        public string? FinalAnswer { get; set; }
        public List<string> ChildrenIds { get; } = new List<string>();

        public ThoughtNode(string id, string? parentId, int depth, string thought)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            ParentId = parentId;
            Depth = depth;
            Thought = thought ?? string.Empty;
        }

        public override string ToString() =>
            $"[Node {Id} (d={Depth}, score={ValueScore:F2})] {Thought}";
    }
}
