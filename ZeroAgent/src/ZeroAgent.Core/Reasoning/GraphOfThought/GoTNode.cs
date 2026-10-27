using System;
using System.Collections.Generic;

namespace ZeroAgent.Core.Reasoning.GraphOfThought
{
    /// <summary>
    /// Node in a Graph-of-Thought (GoT) network.
    /// Unlike trees, a GoT node can have MULTIPLE parents, enabling thought merging,
    /// synthesis, and multi-perspective aggregation.
    /// </summary>
    public sealed class GoTNode
    {
        public string Id { get; }
        public string Content { get; }
        public float Score { get; set; }
        public HashSet<string> ParentIds { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> ChildIds { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public int Depth { get; }
        public bool IsAggregated => ParentIds.Count > 1;

        public GoTNode(string id, string content, float score, int depth = 0)
        {
            Id = id ?? Guid.NewGuid().ToString("N");
            Content = content ?? string.Empty;
            Score = score;
            Depth = depth;
        }

        public void AddParent(string parentId)
        {
            if (!string.IsNullOrWhiteSpace(parentId))
            {
                ParentIds.Add(parentId);
            }
        }

        public void AddChild(string childId)
        {
            if (!string.IsNullOrWhiteSpace(childId))
            {
                ChildIds.Add(childId);
            }
        }
    }

    /// <summary>
    /// Directed Acyclic Thought Graph supporting multi-branch exploration,
    /// backtracking, and cross-branch synthesis (Aggregation).
    /// </summary>
    public sealed class GraphOfThought
    {
        private readonly Dictionary<string, GoTNode> _nodes = new Dictionary<string, GoTNode>(StringComparer.OrdinalIgnoreCase);
        private int _counter = 1;

        public IReadOnlyDictionary<string, GoTNode> Nodes => _nodes;
        public int NodeCount => _nodes.Count;

        /// <summary>
        /// Adds a standard sequential or divergent thought node.
        /// </summary>
        public GoTNode AddThought(string content, float score, params string[] parentIds)
        {
            string id = $"thought_{_counter++}";
            int depth = 0;
            if (parentIds != null && parentIds.Length > 0)
            {
                foreach (var pid in parentIds)
                {
                    if (_nodes.TryGetValue(pid, out var pNode))
                    {
                        depth = Math.Max(depth, pNode.Depth + 1);
                    }
                }
            }

            var node = new GoTNode(id, content, score, depth);
            if (parentIds != null)
            {
                foreach (var pid in parentIds)
                {
                    node.AddParent(pid);
                    if (_nodes.TryGetValue(pid, out var pNode))
                    {
                        pNode.AddChild(id);
                    }
                }
            }

            _nodes[id] = node;
            return node;
        }

        /// <summary>
        /// Aggregates / synthesizes multiple divergent thought nodes into a single consolidated thought.
        /// </summary>
        public GoTNode Aggregate(string aggregatedContent, float score, IReadOnlyList<string> sourceThoughtIds)
        {
            if (sourceThoughtIds == null || sourceThoughtIds.Count == 0)
            {
                throw new ArgumentException("At least one parent thought required for aggregation.", nameof(sourceThoughtIds));
            }

            string id = $"agg_{_counter++}";
            int depth = 0;
            foreach (var pid in sourceThoughtIds)
            {
                if (_nodes.TryGetValue(pid, out var pNode))
                {
                    depth = Math.Max(depth, pNode.Depth + 1);
                }
            }

            var node = new GoTNode(id, aggregatedContent, score, depth);
            foreach (var pid in sourceThoughtIds)
            {
                node.AddParent(pid);
                if (_nodes.TryGetValue(pid, out var pNode))
                {
                    pNode.AddChild(id);
                }
            }

            _nodes[id] = node;
            return node;
        }

        /// <summary>
        /// Finds the highest-scoring terminal or leaf node in the graph.
        /// </summary>
        public GoTNode? FindBestNode()
        {
            GoTNode? best = null;
            float maxScore = float.NegativeInfinity;

            foreach (var kvp in _nodes)
            {
                if (kvp.Value.Score > maxScore)
                {
                    maxScore = kvp.Value.Score;
                    best = kvp.Value;
                }
            }

            return best;
        }

        /// <summary>
        /// Traces the full ancestral path leading to the specified node.
        /// </summary>
        public List<GoTNode> TraceLineage(string nodeId)
        {
            var result = new List<GoTNode>();
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void Traverse(string id)
            {
                if (!visited.Add(id)) return;
                if (_nodes.TryGetValue(id, out var node))
                {
                    foreach (var pid in node.ParentIds)
                    {
                        Traverse(pid);
                    }
                    result.Add(node);
                }
            }

            Traverse(nodeId);
            return result;
        }
    }
}
