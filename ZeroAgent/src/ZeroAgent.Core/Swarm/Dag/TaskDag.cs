using System;
using System.Collections.Generic;
using System.Linq;

namespace ZeroAgent.Core.Swarm.Dag
{
    /// <summary>
    /// Directed Acyclic Graph (DAG) for orchestrating interdependent multi-agent tasks.
    /// Supports cycle detection, topological readiness checks, and dynamic dependency resolution.
    /// </summary>
    public sealed class TaskDag
    {
        private readonly Dictionary<string, DagTaskNode> _nodes = new Dictionary<string, DagTaskNode>(StringComparer.OrdinalIgnoreCase);

        public int Count => _nodes.Count;
        public IEnumerable<DagTaskNode> Nodes => _nodes.Values;

        public void AddNode(DagTaskNode node)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));
            _nodes[node.Id] = node;
        }

        public DagTaskNode? GetNode(string id)
        {
            return _nodes.TryGetValue(id, out var node) ? node : null;
        }

        /// <summary>
        /// Validates that the graph contains no cycles using Kahn's algorithm.
        /// Throws InvalidOperationException if a cycle is detected.
        /// </summary>
        public void ValidateAcyclic()
        {
            var inDegree = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var adj = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var kvp in _nodes)
            {
                inDegree[kvp.Key] = 0;
                adj[kvp.Key] = new List<string>();
            }

            foreach (var node in _nodes.Values)
            {
                foreach (var dep in node.Dependencies)
                {
                    if (_nodes.ContainsKey(dep))
                    {
                        inDegree[node.Id]++;
                        adj[dep].Add(node.Id);
                    }
                }
            }

            var queue = new Queue<string>(_nodes.Keys.Where(k => inDegree[k] == 0));
            int visitedCount = 0;

            while (queue.Count > 0)
            {
                string u = queue.Dequeue();
                visitedCount++;

                foreach (var v in adj[u])
                {
                    inDegree[v]--;
                    if (inDegree[v] == 0)
                    {
                        queue.Enqueue(v);
                    }
                }
            }

            if (visitedCount < _nodes.Count)
            {
                throw new InvalidOperationException("Cycle detected in task DAG. Interdependent subtasks cannot have circular dependencies.");
            }
        }

        /// <summary>
        /// Returns all pending nodes whose prerequisite dependencies are all completed.
        /// </summary>
        public List<DagTaskNode> GetReadyNodes()
        {
            var ready = new List<DagTaskNode>();
            foreach (var node in _nodes.Values)
            {
                if (node.Status != DagTaskStatus.Pending) continue;

                bool allDepsCompleted = true;
                foreach (var depId in node.Dependencies)
                {
                    if (!_nodes.TryGetValue(depId, out var depNode) || depNode.Status != DagTaskStatus.Completed)
                    {
                        allDepsCompleted = false;
                        break;
                    }
                }

                if (allDepsCompleted)
                {
                    ready.Add(node);
                }
            }
            return ready;
        }

        public bool IsFinished()
        {
            return _nodes.Values.All(n => n.Status == DagTaskStatus.Completed || n.Status == DagTaskStatus.Failed || n.Status == DagTaskStatus.Skipped);
        }
    }
}
