using System;
using System.Collections.Generic;
using ZeroCompute.Core.Cpu;
using ZeroInference.Core.Graph;
using ZeroTensor.Core;

namespace ZeroInference.Core.Engine
{
    /// <summary>
    /// Thread-safe execution session for an optimized InferenceGraph.
    /// Manages pre-allocated static tensor arena for allocation-free inference loops.
    /// </summary>
    public sealed class ExecutionSession : IInferenceSession
    {
        private readonly InferenceGraph _graph;
        private readonly List<InferenceNode> _executionOrder;
        private readonly MemoryPlan _memoryPlan;
        private readonly StaticTensorArena _arena;
        private readonly Dictionary<string, Tensor<float>> _tensorSlots;

        public InferenceGraph Graph => _graph;
        public MemoryPlan MemoryPlan => _memoryPlan;
        public IReadOnlyList<string> InputNames => _graph.InputNames;
        public IReadOnlyList<string> OutputNames => _graph.OutputNames;

        public ExecutionSession(InferenceGraph graph, int arenaSlotCapacity = 2 * 1024 * 1024)
        {
            _graph = graph ?? throw new ArgumentNullException(nameof(graph));
            _graph.Validate();
            _executionOrder = _graph.GetTopologicalOrder();
            _memoryPlan = MemoryPlanner.Plan(_graph);
            _arena = new StaticTensorArena(_memoryPlan.TotalSlots, arenaSlotCapacity);
            _tensorSlots = new Dictionary<string, Tensor<float>>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Runs inference on the given input tensor.
        /// </summary>
        public Tensor<float> Run(Tensor<float> input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (_graph.InputNames.Count == 0)
                throw new InvalidOperationException("Graph has no declared input names.");

            return Run(new Dictionary<string, Tensor<float>>(StringComparer.OrdinalIgnoreCase)
            {
                { _graph.InputNames[0], input }
            });
        }

        /// <summary>
        /// Runs inference with multiple input feeds.
        /// </summary>
        public Tensor<float> Run(IReadOnlyDictionary<string, Tensor<float>> inputs)
        {
            if (inputs == null) throw new ArgumentNullException(nameof(inputs));

            // Set inputs
            foreach (var kvp in inputs)
            {
                _tensorSlots[kvp.Key] = kvp.Value;
            }

            // Execute nodes sequentially
            for (int i = 0; i < _executionOrder.Count; i++)
            {
                var node = _executionOrder[i];
                var outputTensor = ExecuteNode(node);
                _tensorSlots[node.OutputName] = outputTensor;
            }

            // Return first output
            string primaryOutput = _graph.OutputNames[0];
            return _tensorSlots[primaryOutput];
        }

        private Tensor<float> ExecuteNode(InferenceNode node)
        {
            switch (node.Kind)
            {
                case NodeKind.Conv2D:
                case NodeKind.FusedConvRelu:
                    return ExecuteConv(node, isFusedRelu: node.Kind == NodeKind.FusedConvRelu);

                case NodeKind.Linear:
                case NodeKind.FusedLinearRelu:
                    return ExecuteLinear(node, isFusedRelu: node.Kind == NodeKind.FusedLinearRelu);

                case NodeKind.BatchNormalization:
                    return ExecuteBatchNorm(node);

                case NodeKind.Relu:
                    return ExecuteActivation(node, x => Math.Max(0.0f, x));

                case NodeKind.LeakyRelu:
                    return ExecuteActivation(node, x => x >= 0.0f ? x : node.Alpha * x);

                case NodeKind.Sigmoid:
                    return ExecuteActivation(node, x => 1.0f / (1.0f + (float)Math.Exp(-x)));

                case NodeKind.Tanh:
                    return ExecuteActivation(node, x => (float)Math.Tanh(x));

                case NodeKind.Gelu:
                    return ExecuteActivation(node, x => 0.5f * x * (1.0f + (float)Math.Tanh(0.79788456f * (x + 0.044715f * x * x * x))));

                case NodeKind.MaxPool2D:
                    return ExecuteMaxPool(node);

                case NodeKind.GlobalAvgPool2D:
                    return ExecuteGlobalAvgPool(node);

                case NodeKind.Add:
                    return ExecuteAdd(node);

                case NodeKind.Flatten:
                    return ExecuteFlatten(node);

                case NodeKind.Softmax:
                    return ExecuteSoftmax(node);

                default:
                    throw new NotSupportedException($"Unsupported node kind: {node.Kind}");
            }
        }

        private Tensor<float> ExecuteConv(InferenceNode node, bool isFusedRelu)
        {
            var x = _tensorSlots[node.InputNames[0]];
            var w = node.Weight ?? throw new InvalidOperationException($"Node '{node.Name}' has null Weight.");
            var b = node.Bias;

            int batch = x.Shape[0];
            int inChannels = x.Shape[1];
            int inH = x.Shape[2];
            int inW = x.Shape[3];

            int outChannels = w.Shape[0];
            int kH = w.Shape[2];
            int kW = w.Shape[3];

            int stride = node.Stride;
            int pad = node.Padding;

            int outH = (inH + 2 * pad - kH) / stride + 1;
            int outW = (inW + 2 * pad - kW) / stride + 1;

            var outShape = new TensorShape(batch, outChannels, outH, outW);
            var result = AllocateIntermediate(node.OutputName, outShape);

            float alpha = node.Alpha;

            Compute.For(batch * outChannels, item =>
            {
                int bi = item / outChannels;
                int oc = item % outChannels;
                float biasVal = (b != null) ? b[oc] : 0.0f;

                for (int oh = 0; oh < outH; oh++)
                {
                    int ihBase = oh * stride - pad;

                    for (int ow = 0; ow < outW; ow++)
                    {
                        int iwBase = ow * stride - pad;
                        float sum = biasVal;

                        for (int ic = 0; ic < inChannels; ic++)
                        {
                            for (int kh = 0; kh < kH; kh++)
                            {
                                int ih = ihBase + kh;
                                if (ih < 0 || ih >= inH) continue;

                                for (int kw = 0; kw < kW; kw++)
                                {
                                    int iw = iwBase + kw;
                                    if (iw < 0 || iw >= inW) continue;

                                    sum += x[bi, ic, ih, iw] * w[oc, ic, kh, kw];
                                }
                            }
                        }

                        if (isFusedRelu)
                        {
                            sum = sum >= 0.0f ? sum : alpha * sum;
                        }

                        result[bi, oc, oh, ow] = sum;
                    }
                }
            });

            return result;
        }

        private Tensor<float> ExecuteLinear(InferenceNode node, bool isFusedRelu)
        {
            var x = _tensorSlots[node.InputNames[0]];
            var w = node.Weight ?? throw new InvalidOperationException($"Node '{node.Name}' has null Weight.");
            var b = node.Bias;

            int batch = x.Shape[0];
            int inFeats = x.Shape[1];
            int outFeats = w.Shape[0];

            var outShape = new TensorShape(batch, outFeats);
            var result = AllocateIntermediate(node.OutputName, outShape);
            float alpha = node.Alpha;

            Compute.For(batch * outFeats, idx =>
            {
                int bi = idx / outFeats;
                int of = idx % outFeats;

                float sum = (b != null) ? b[of] : 0.0f;
                for (int inf = 0; inf < inFeats; inf++)
                {
                    sum += x[bi, inf] * w[of, inf];
                }

                if (isFusedRelu)
                {
                    sum = sum >= 0.0f ? sum : alpha * sum;
                }

                result[bi, of] = sum;
            });

            return result;
        }

        private Tensor<float> ExecuteBatchNorm(InferenceNode node)
        {
            var x = _tensorSlots[node.InputNames[0]];
            var mean = (Tensor<float>)node.ExtraAttributes["RunningMean"];
            var varT = (Tensor<float>)node.ExtraAttributes["RunningVar"];
            var gamma = (Tensor<float>)node.ExtraAttributes["Gamma"];
            var beta = (Tensor<float>)node.ExtraAttributes["Beta"];
            float eps = node.Epsilon;

            var result = AllocateIntermediate(node.OutputName, x.Shape);

            int batch = x.Shape[0];
            int channels = x.Shape[1];
            int spatial = x.Length / (batch * channels);

            for (int bi = 0; bi < batch; bi++)
            {
                for (int c = 0; c < channels; c++)
                {
                    float g = gamma[c];
                    float b = beta[c];
                    float m = mean[c];
                    float v = varT[c];
                    float invStd = 1.0f / (float)Math.Sqrt(v + eps);

                    for (int s = 0; s < spatial; s++)
                    {
                        int idx = bi * channels * spatial + c * spatial + s;
                        result.AsSpan()[idx] = (x.AsReadOnlySpan()[idx] - m) * invStd * g + b;
                    }
                }
            }

            return result;
        }

        private Tensor<float> ExecuteActivation(InferenceNode node, Func<float, float> op)
        {
            var x = _tensorSlots[node.InputNames[0]];
            var result = AllocateIntermediate(node.OutputName, x.Shape);

            Compute.Map(x, result, op);

            return result;
        }

        private Tensor<float> ExecuteMaxPool(InferenceNode node)
        {
            var x = _tensorSlots[node.InputNames[0]];
            int batch = x.Shape[0];
            int channels = x.Shape[1];
            int inH = x.Shape[2];
            int inW = x.Shape[3];

            int k = node.KernelSize;
            int stride = node.Stride;

            int outH = (inH - k) / stride + 1;
            int outW = (inW - k) / stride + 1;

            var result = AllocateIntermediate(node.OutputName, new TensorShape(batch, channels, outH, outW));

            for (int bi = 0; bi < batch; bi++)
            {
                for (int c = 0; c < channels; c++)
                {
                    for (int oh = 0; oh < outH; oh++)
                    {
                        int ihBase = oh * stride;
                        for (int ow = 0; ow < outW; ow++)
                        {
                            int iwBase = ow * stride;
                            float maxVal = float.MinValue;

                            for (int kh = 0; kh < k; kh++)
                            {
                                for (int kw = 0; kw < k; kw++)
                                {
                                    float val = x[bi, c, ihBase + kh, iwBase + kw];
                                    if (val > maxVal) maxVal = val;
                                }
                            }

                            result[bi, c, oh, ow] = maxVal;
                        }
                    }
                }
            }

            return result;
        }

        private Tensor<float> ExecuteGlobalAvgPool(InferenceNode node)
        {
            var x = _tensorSlots[node.InputNames[0]];
            int batch = x.Shape[0];
            int channels = x.Shape[1];
            int spatial = x.Shape[2] * x.Shape[3];

            var result = AllocateIntermediate(node.OutputName, new TensorShape(batch, channels, 1, 1));
            float invSpatial = 1.0f / spatial;

            for (int bi = 0; bi < batch; bi++)
            {
                for (int c = 0; c < channels; c++)
                {
                    float sum = 0.0f;
                    for (int h = 0; h < x.Shape[2]; h++)
                    {
                        for (int w = 0; w < x.Shape[3]; w++)
                        {
                            sum += x[bi, c, h, w];
                        }
                    }
                    result[bi, c, 0, 0] = sum * invSpatial;
                }
            }

            return result;
        }

        private Tensor<float> ExecuteAdd(InferenceNode node)
        {
            var a = _tensorSlots[node.InputNames[0]];
            var b = _tensorSlots[node.InputNames[1]];

            var result = AllocateIntermediate(node.OutputName, a.Shape);
            Compute.Vector.Add(a, b, result);

            return result;
        }

        private Tensor<float> ExecuteFlatten(InferenceNode node)
        {
            var x = _tensorSlots[node.InputNames[0]];
            int batch = x.Shape[0];
            int feats = x.Length / batch;

            var result = AllocateIntermediate(node.OutputName, new TensorShape(batch, feats));
            x.AsReadOnlySpan().CopyTo(result.AsSpan());
            return result;
        }

        private Tensor<float> ExecuteSoftmax(InferenceNode node)
        {
            var x = _tensorSlots[node.InputNames[0]];
            var result = AllocateIntermediate(node.OutputName, x.Shape);

            int batch = x.Shape[0];
            int numClasses = x.Length / batch;

            var inSpan = x.AsReadOnlySpan();
            var outSpan = result.AsSpan();

            for (int bi = 0; bi < batch; bi++)
            {
                int offset = bi * numClasses;

                float maxVal = float.MinValue;
                for (int c = 0; c < numClasses; c++)
                {
                    if (inSpan[offset + c] > maxVal) maxVal = inSpan[offset + c];
                }

                float sumExp = 0.0f;
                for (int c = 0; c < numClasses; c++)
                {
                    float exp = (float)Math.Exp(inSpan[offset + c] - maxVal);
                    outSpan[offset + c] = exp;
                    sumExp += exp;
                }

                float invSum = 1.0f / sumExp;
                for (int c = 0; c < numClasses; c++)
                {
                    outSpan[offset + c] *= invSum;
                }
            }

            return result;
        }

        private Tensor<float> AllocateIntermediate(string outputName, TensorShape shape)
        {
            if (_memoryPlan.OutputToSlotMap.TryGetValue(outputName, out int slotId))
            {
                var buffer = _arena.GetSlotBuffer(slotId);
                return Tensor.CreateView(buffer, 0, shape, TensorStrides.ComputeContiguousStrides(shape));
            }

            return new Tensor<float>(shape);
        }

        /// <summary>
        /// Disposes any session resources. No-op for managed CPU memory arena.
        /// </summary>
        public void Dispose()
        {
            // Pure C# managed arena requires no unmanaged cleanup
        }
    }
}
