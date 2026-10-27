using System;
using System.Collections.Generic;
using System.Linq;
using ZeroTensor.Core;

namespace ZeroInference.Core.Graph
{
    /// <summary>
    /// Static graph optimizer for Edge AI deployment.
    /// Performs Conv+BatchNorm fusion, Conv+ReLU operator fusion, and dead-code elimination.
    /// </summary>
    public static class GraphOptimizer
    {
        public static InferenceGraph Optimize(InferenceGraph graph)
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph));

            var optimized = graph.Clone();

            // Pass 1: Conv + BatchNorm Fusion
            FuseConvBatchNorm(optimized);

            // Pass 2: Conv + ReLU Operator Fusion
            FuseConvActivation(optimized);

            // Pass 3: Linear + ReLU Operator Fusion
            FuseLinearActivation(optimized);

            // Pass 4: Dead node elimination
            EliminateDeadNodes(optimized);

            optimized.Validate();
            return optimized;
        }

        private static void FuseConvBatchNorm(InferenceGraph graph)
        {
            var nodes = graph.Nodes.ToList();

            for (int i = 0; i < nodes.Count; i++)
            {
                var convNode = nodes[i];
                if (convNode.Kind != NodeKind.Conv2D || convNode.Weight == null)
                    continue;

                // Check if the only consumer of convNode is a BatchNormalization node
                var consumers = nodes.Where(n => n.InputNames.Contains(convNode.OutputName, StringComparer.OrdinalIgnoreCase)).ToList();
                if (consumers.Count == 1 && consumers[0].Kind == NodeKind.BatchNormalization)
                {
                    var bnNode = consumers[0];

                    // Extract BatchNorm parameters
                    if (bnNode.ExtraAttributes.TryGetValue("RunningMean", out var meanObj) &&
                        bnNode.ExtraAttributes.TryGetValue("RunningVar", out var varObj) &&
                        bnNode.ExtraAttributes.TryGetValue("Gamma", out var gammaObj) &&
                        bnNode.ExtraAttributes.TryGetValue("Beta", out var betaObj))
                    {
                        var mean = (Tensor<float>)meanObj;
                        var variance = (Tensor<float>)varObj;
                        var gamma = (Tensor<float>)gammaObj;
                        var beta = (Tensor<float>)betaObj;
                        float eps = bnNode.Epsilon;

                        int outChannels = convNode.Weight.Shape[0];
                        var convBias = convNode.Bias ?? Tensor.Zeros<float>(outChannels);

                        // Fuse weights: W_new = W * (gamma / sqrt(var + eps))
                        // Fuse bias: b_new = (b - mean) * (gamma / sqrt(var + eps)) + beta
                        var newWeight = convNode.Weight.Clone();
                        var newBias = Tensor.Zeros<float>(outChannels);

                        var wSpan = newWeight.AsSpan();
                        int spatialKernelSize = newWeight.Length / outChannels;

                        for (int c = 0; c < outChannels; c++)
                        {
                            float g = gamma[c];
                            float b = beta[c];
                            float m = mean[c];
                            float v = variance[c];

                            float scale = g / (float)Math.Sqrt(v + eps);

                            // Scale kernel weights for channel c
                            int startIdx = c * spatialKernelSize;
                            for (int k = 0; k < spatialKernelSize; k++)
                            {
                                wSpan[startIdx + k] *= scale;
                            }

                            // Compute new bias
                            newBias[c] = (convBias[c] - m) * scale + b;
                        }

                        convNode.Weight = newWeight;
                        convNode.Bias = newBias;

                        // The fused Conv node now directly produces bnNode's output
                        string bnOutput = bnNode.OutputName;
                        convNode.OutputName = bnOutput;

                        // Remove bnNode
                        ((List<InferenceNode>)graph.Nodes).Remove(bnNode);
                    }
                }
            }
        }

        private static void FuseConvActivation(InferenceGraph graph)
        {
            var nodes = graph.Nodes.ToList();

            for (int i = 0; i < nodes.Count; i++)
            {
                var convNode = nodes[i];
                if (convNode.Kind != NodeKind.Conv2D) continue;

                var consumers = nodes.Where(n => n.InputNames.Contains(convNode.OutputName, StringComparer.OrdinalIgnoreCase)).ToList();
                if (consumers.Count == 1)
                {
                    var actNode = consumers[0];
                    if (actNode.Kind == NodeKind.Relu || actNode.Kind == NodeKind.LeakyRelu)
                    {
                        convNode.Kind = NodeKind.FusedConvRelu;
                        convNode.Alpha = actNode.Kind == NodeKind.LeakyRelu ? actNode.Alpha : 0.0f;

                        // Bypass activation: redirect actNode consumers to convNode
                        string actOutput = actNode.OutputName;
                        convNode.OutputName = actOutput; // Output directly as the activation output

                        ((List<InferenceNode>)graph.Nodes).Remove(actNode);
                    }
                }
            }
        }

        private static void FuseLinearActivation(InferenceGraph graph)
        {
            var nodes = graph.Nodes.ToList();

            for (int i = 0; i < nodes.Count; i++)
            {
                var linNode = nodes[i];
                if (linNode.Kind != NodeKind.Linear) continue;

                var consumers = nodes.Where(n => n.InputNames.Contains(linNode.OutputName, StringComparer.OrdinalIgnoreCase)).ToList();
                if (consumers.Count == 1)
                {
                    var actNode = consumers[0];
                    if (actNode.Kind == NodeKind.Relu || actNode.Kind == NodeKind.LeakyRelu)
                    {
                        linNode.Kind = NodeKind.FusedLinearRelu;
                        linNode.Alpha = actNode.Kind == NodeKind.LeakyRelu ? actNode.Alpha : 0.0f;

                        linNode.OutputName = actNode.OutputName;
                        ((List<InferenceNode>)graph.Nodes).Remove(actNode);
                    }
                }
            }
        }

        private static void EliminateDeadNodes(InferenceGraph graph)
        {
            var nodes = ((List<InferenceNode>)graph.Nodes);
            bool changed = true;

            while (changed)
            {
                changed = false;
                for (int i = nodes.Count - 1; i >= 0; i--)
                {
                    var node = nodes[i];
                    bool isGraphOutput = graph.OutputNames.Contains(node.OutputName, StringComparer.OrdinalIgnoreCase);
                    bool isConsumed = nodes.Any(other => other.InputNames.Contains(node.OutputName, StringComparer.OrdinalIgnoreCase));

                    if (!isGraphOutput && !isConsumed)
                    {
                        nodes.RemoveAt(i);
                        changed = true;
                    }
                }
            }
        }
    }
}
