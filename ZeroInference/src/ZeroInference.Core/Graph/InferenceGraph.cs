using System;
using System.Collections.Generic;
using System.Linq;
using ZeroTensor.Core;

namespace ZeroInference.Core.Graph
{
    /// <summary>
    /// Directed Acyclic Graph (DAG) representing an edge AI model.
    /// Supports topological execution, shape propagation, and graph-level compiler optimizations.
    /// </summary>
    public sealed class InferenceGraph
    {
        private readonly List<InferenceNode> _nodes = new List<InferenceNode>();
        private readonly List<string> _inputNames = new List<string>();
        private readonly List<string> _outputNames = new List<string>();

        public IReadOnlyList<InferenceNode> Nodes => _nodes;
        public IReadOnlyList<string> InputNames => _inputNames;
        public IReadOnlyList<string> OutputNames => _outputNames;

        public TensorShape? InputShape { get; set; }

        public InferenceGraph()
        {
        }

        public void SetInputs(params string[] inputNames)
        {
            _inputNames.Clear();
            if (inputNames != null) _inputNames.AddRange(inputNames);
        }

        public void SetOutputs(params string[] outputNames)
        {
            _outputNames.Clear();
            if (outputNames != null) _outputNames.AddRange(outputNames);
        }

        public void AddNode(InferenceNode node)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));
            _nodes.Add(node);
        }

        public InferenceNode? FindNode(string name)
        {
            return _nodes.FirstOrDefault(n => string.Equals(n.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        public InferenceNode? FindNodeByOutput(string outputName)
        {
            return _nodes.FirstOrDefault(n => string.Equals(n.OutputName, outputName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Validates graph integrity: ensures all non-input references are produced by upstream nodes.
        /// </summary>
        public void Validate()
        {
            var produced = new HashSet<string>(_inputNames, StringComparer.OrdinalIgnoreCase);
            foreach (var node in _nodes)
            {
                foreach (var inName in node.InputNames)
                {
                    if (!produced.Contains(inName))
                    {
                        throw new InvalidOperationException($"Graph validation error: Node '{node.Name}' requires input '{inName}', which is neither a graph input nor produced by upstream nodes.");
                    }
                }
                produced.Add(node.OutputName);
            }

            foreach (var outName in _outputNames)
            {
                if (!produced.Contains(outName))
                {
                    throw new InvalidOperationException($"Graph validation error: Required graph output '{outName}' is never produced.");
                }
            }
        }

        /// <summary>
        /// Returns topologically sorted nodes for execution.
        /// </summary>
        public List<InferenceNode> GetTopologicalOrder()
        {
            var result = new List<InferenceNode>();
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var inProgress = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Populate input names as visited
            foreach (var input in _inputNames)
            {
                visited.Add(input);
            }

            var nodeByOutput = new Dictionary<string, InferenceNode>(StringComparer.OrdinalIgnoreCase);
            foreach (var n in _nodes)
            {
                nodeByOutput[n.OutputName] = n;
            }

            void Visit(InferenceNode node)
            {
                if (visited.Contains(node.OutputName)) return;
                if (inProgress.Contains(node.OutputName))
                    throw new InvalidOperationException($"Cyclic dependency detected at node '{node.Name}'.");

                inProgress.Add(node.OutputName);

                foreach (var inName in node.InputNames)
                {
                    if (_inputNames.Contains(inName, StringComparer.OrdinalIgnoreCase))
                        continue;

                    if (nodeByOutput.TryGetValue(inName, out var depNode))
                    {
                        Visit(depNode);
                    }
                }

                inProgress.Remove(node.OutputName);
                visited.Add(node.OutputName);
                result.Add(node);
            }

            foreach (var node in _nodes)
            {
                Visit(node);
            }

            return result;
        }

        /// <summary>
        /// Creates a deep structural clone of the inference graph.
        /// </summary>
        public InferenceGraph Clone()
        {
            var copy = new InferenceGraph();
            copy.SetInputs(_inputNames.ToArray());
            copy.SetOutputs(_outputNames.ToArray());
            copy.InputShape = InputShape;

            foreach (var n in _nodes)
            {
                var nodeCopy = new InferenceNode(
                    n.Name,
                    n.Kind,
                    n.InputNames.ToArray(),
                    n.OutputName,
                    n.Weight?.Clone(),
                    n.Bias?.Clone())
                {
                    Stride = n.Stride,
                    Padding = n.Padding,
                    KernelSize = n.KernelSize,
                    Alpha = n.Alpha,
                    Epsilon = n.Epsilon,
                    Axis = n.Axis,
                    IsQuantized = n.IsQuantized,
                    WeightScale = n.WeightScale,
                    WeightZeroPoint = n.WeightZeroPoint
                };

                foreach (var kvp in n.ExtraAttributes)
                {
                    nodeCopy.ExtraAttributes[kvp.Key] = kvp.Value;
                }
                copy.AddNode(nodeCopy);
            }

            return copy;
        }
    }
}
