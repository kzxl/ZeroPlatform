using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ZeroPipeline.Core.Nodes;
using ZeroPipeline.Core.Ports;

namespace ZeroPipeline.Core.Graph
{
    /// <summary>
    /// Represents a Directed Acyclic Graph (DAG) topology of connected industrial pipeline nodes.
    /// Performs Kahn's algorithm topological sorting, circular dependency detection, and port binding validation.
    /// </summary>
    public sealed class PipelineGraph
    {
        private readonly List<IPipelineNode> _nodes = new List<IPipelineNode>();
        private readonly List<PipelineConnection> _connections = new List<PipelineConnection>();
        private readonly object _lock = new object();

        public IReadOnlyList<IPipelineNode> Nodes
        {
            get
            {
                lock (_lock) return _nodes.ToArray();
            }
        }

        public IReadOnlyList<PipelineConnection> Connections
        {
            get
            {
                lock (_lock) return _connections.ToArray();
            }
        }

        public int NodeCount
        {
            get
            {
                lock (_lock) return _nodes.Count;
            }
        }

        public int ConnectionCount
        {
            get
            {
                lock (_lock) return _connections.Count;
            }
        }

        /// <summary>
        /// Adds a node to the pipeline graph.
        /// </summary>
        public PipelineGraph AddNode(IPipelineNode node)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));

            lock (_lock)
            {
                if (!_nodes.Contains(node))
                {
                    _nodes.Add(node);
                }
            }
            return this;
        }

        /// <summary>
        /// Connects an output port to an input port with strong compile-time type safety.
        /// </summary>
        public PipelineGraph Connect<T>(OutputPort<T> source, InputPort<T> target)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (target == null) throw new ArgumentNullException(nameof(target));

            if (source.OwnerNode == target.OwnerNode)
                throw new CycleDetectedException($"Self-referential circular loop detected on node '{source.OwnerNode.Name}'.");

            lock (_lock)
            {
                AddNode(source.OwnerNode);
                AddNode(target.OwnerNode);

                source.ConnectTo(target);
                var connection = new PipelineConnection(source, target);
                _connections.Add(connection);
            }
            return this;
        }

        /// <summary>
        /// Connects two weakly-typed ports, validating type compatibility and binding dynamically.
        /// </summary>
        public PipelineGraph Connect(IPort source, IPort target)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (target == null) throw new ArgumentNullException(nameof(target));

            if (source.OwnerNode == target.OwnerNode)
                throw new CycleDetectedException($"Self-referential circular loop detected on node '{source.OwnerNode.Name}'.");

            lock (_lock)
            {
                AddNode(source.OwnerNode);
                AddNode(target.OwnerNode);

                var connection = new PipelineConnection(source, target);

                // Dynamically invoke source.ConnectTo(target)
                var connectMethod = source.GetType().GetMethod("ConnectTo", BindingFlags.Public | BindingFlags.Instance);
                if (connectMethod == null)
                    throw new InvalidOperationException($"Port '{source.Name}' does not support ConnectTo.");

                connectMethod.Invoke(source, new object[] { target });
                _connections.Add(connection);
            }
            return this;
        }

        /// <summary>
        /// Validates the graph topology, ensuring all connections are intact and no circular dependencies exist.
        /// Throws CycleDetectedException or PipelineException if validation fails.
        /// </summary>
        public void Validate()
        {
            lock (_lock)
            {
                // Verify all connected nodes exist in graph
                foreach (var conn in _connections)
                {
                    if (!_nodes.Contains(conn.Source.OwnerNode))
                        throw new PipelineException($"Source node '{conn.Source.OwnerNode.Name}' is not registered in the graph.");

                    if (!_nodes.Contains(conn.Target.OwnerNode))
                        throw new PipelineException($"Target node '{conn.Target.OwnerNode.Name}' is not registered in the graph.");
                }

                // Verify DAG acyclic invariant via topological sort
                GetTopologicalOrder();
            }
        }

        /// <summary>
        /// Computes the linear execution sequence of nodes using Kahn's topological sort algorithm.
        /// Guarantees all upstream dependencies are executed before downstream consumers.
        /// Throws CycleDetectedException if a circular dependency loop is discovered.
        /// </summary>
        public IReadOnlyList<IPipelineNode> GetTopologicalOrder()
        {
            lock (_lock)
            {
                if (_nodes.Count == 0)
                    return Array.Empty<IPipelineNode>();

                // Build adjacency list and in-degrees
                var inDegrees = new Dictionary<IPipelineNode, int>(_nodes.Count);
                var outgoingEdges = new Dictionary<IPipelineNode, List<IPipelineNode>>(_nodes.Count);

                foreach (var node in _nodes)
                {
                    inDegrees[node] = 0;
                    outgoingEdges[node] = new List<IPipelineNode>();
                }

                foreach (var conn in _connections)
                {
                    var srcNode = conn.Source.OwnerNode;
                    var dstNode = conn.Target.OwnerNode;

                    if (srcNode != dstNode)
                    {
                        outgoingEdges[srcNode].Add(dstNode);
                        inDegrees[dstNode]++;
                    }
                    else
                    {
                        // Self-referential loop
                        throw new CycleDetectedException(
                            $"Self-referential circular loop detected on node '{srcNode.Name}'.");
                    }
                }

                // Initialize queue with all nodes having in-degree = 0 (root/source nodes)
                var queue = new Queue<IPipelineNode>();
                foreach (var kv in inDegrees)
                {
                    if (kv.Value == 0)
                    {
                        queue.Enqueue(kv.Key);
                    }
                }

                var sorted = new List<IPipelineNode>(_nodes.Count);

                while (queue.Count > 0)
                {
                    var node = queue.Dequeue();
                    sorted.Add(node);

                    foreach (var neighbor in outgoingEdges[node])
                    {
                        inDegrees[neighbor]--;
                        if (inDegrees[neighbor] == 0)
                        {
                            queue.Enqueue(neighbor);
                        }
                    }
                }

                // If not all nodes were processed, at least one cycle exists
                if (sorted.Count != _nodes.Count)
                {
                    var unresolvedNodes = _nodes.Where(n => !sorted.Contains(n)).Select(n => n.Name);
                    throw new CycleDetectedException(
                        $"Circular dependency loop detected in pipeline graph. Unresolved cyclic nodes: [{string.Join(", ", unresolvedNodes)}].");
                }

                return sorted;
            }
        }

        /// <summary>
        /// Finds a node by its unique Id or name.
        /// </summary>
        public IPipelineNode? FindNode(string idOrName)
        {
            if (string.IsNullOrEmpty(idOrName)) return null;

            lock (_lock)
            {
                return _nodes.FirstOrDefault(n => n.Id.Equals(idOrName, StringComparison.OrdinalIgnoreCase) ||
                                                  n.Name.Equals(idOrName, StringComparison.OrdinalIgnoreCase));
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                _connections.Clear();
                _nodes.Clear();
            }
        }
    }
}
