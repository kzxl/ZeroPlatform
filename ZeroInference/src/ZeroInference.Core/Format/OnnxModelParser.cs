using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ZeroInference.Core.Graph;
using ZeroTensor.Core;

namespace ZeroInference.Core.Format
{
    /// <summary>
    /// Pure C# Open Neural Network Exchange (ONNX) binary model parser.
    /// Directly extracts computation graph nodes, tensor weights, and operator attributes
    /// from standard ONNX protobuf streams with zero third-party dependencies.
    /// </summary>
    public static class OnnxModelParser
    {
        private sealed class RawNodeInfo
        {
            public string Name = string.Empty;
            public string OpType = string.Empty;
            public readonly List<string> Inputs = new List<string>();
            public readonly List<string> Outputs = new List<string>();
            public readonly Dictionary<string, object> Attributes = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Parses an ONNX model from a file path.
        /// </summary>
        public static InferenceGraph ParseFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentNullException(nameof(filePath));
            byte[] bytes = File.ReadAllBytes(filePath);
            return Parse(bytes);
        }

        /// <summary>
        /// Parses an ONNX model directly from in-memory protobuf bytes.
        /// </summary>
        public static InferenceGraph Parse(byte[] onnxBytes)
        {
            if (onnxBytes == null || onnxBytes.Length == 0)
                throw new ArgumentException("ONNX bytes cannot be null or empty.", nameof(onnxBytes));

            var reader = new ProtobufWireReader(onnxBytes);
            InferenceGraph? graph = null;

            // ModelProto
            while (reader.TryReadTag(out int fieldNumber, out int wireType))
            {
                if (fieldNumber == 7 && wireType == 2) // graph (GraphProto)
                {
                    var graphReader = reader.ReadSubReader();
                    graph = ParseGraphProto(ref graphReader);
                }
                else
                {
                    reader.Skip(wireType);
                }
            }

            if (graph == null)
                throw new InvalidDataException("Failed to find valid GraphProto within ONNX stream.");

            return graph;
        }

        private static InferenceGraph ParseGraphProto(ref ProtobufWireReader reader)
        {
            var graph = new InferenceGraph();
            var initializers = new Dictionary<string, Tensor<float>>(StringComparer.OrdinalIgnoreCase);
            var rawNodes = new List<RawNodeInfo>();
            var graphInputs = new List<string>();
            var graphOutputs = new List<string>();

            while (reader.TryReadTag(out int fieldNumber, out int wireType))
            {
                switch (fieldNumber)
                {
                    case 1: // node (NodeProto)
                        if (wireType == 2)
                        {
                            var nodeReader = reader.ReadSubReader();
                            rawNodes.Add(ParseNodeProto(ref nodeReader));
                        }
                        else reader.Skip(wireType);
                        break;

                    case 5: // initializer (TensorProto)
                        if (wireType == 2)
                        {
                            var tensorReader = reader.ReadSubReader();
                            var tensor = ParseTensorProto(ref tensorReader, out string tensorName);
                            if (!string.IsNullOrEmpty(tensorName) && tensor != null)
                            {
                                initializers[tensorName] = tensor;
                            }
                        }
                        else reader.Skip(wireType);
                        break;

                    case 11: // input (ValueInfoProto)
                        if (wireType == 2)
                        {
                            var viReader = reader.ReadSubReader();
                            string inputName = ParseValueInfoName(ref viReader);
                            if (!string.IsNullOrEmpty(inputName))
                            {
                                graphInputs.Add(inputName);
                            }
                        }
                        else reader.Skip(wireType);
                        break;

                    case 12: // output (ValueInfoProto)
                        if (wireType == 2)
                        {
                            var viReader = reader.ReadSubReader();
                            string outputName = ParseValueInfoName(ref viReader);
                            if (!string.IsNullOrEmpty(outputName))
                            {
                                graphOutputs.Add(outputName);
                            }
                        }
                        else reader.Skip(wireType);
                        break;

                    default:
                        reader.Skip(wireType);
                        break;
                }
            }

            // Filter out initializers from real inputs
            var trueInputs = graphInputs.Where(i => !initializers.ContainsKey(i)).ToArray();
            graph.SetInputs(trueInputs.Length > 0 ? trueInputs : graphInputs.ToArray());
            graph.SetOutputs(graphOutputs.ToArray());

            // Convert Raw Nodes into InferenceNode DAG
            int nodeCounter = 0;
            foreach (var rawNode in rawNodes)
            {
                nodeCounter++;
                string nodeName = string.IsNullOrEmpty(rawNode.Name) ? $"{rawNode.OpType}_{nodeCounter}" : rawNode.Name;
                string outputName = rawNode.Outputs.Count > 0 ? rawNode.Outputs[0] : $"out_{nodeCounter}";

                switch (rawNode.OpType.ToUpperInvariant())
                {
                    case "CONV":
                        {
                            string inName = rawNode.Inputs.Count > 0 ? rawNode.Inputs[0] : string.Empty;
                            Tensor<float>? weight = rawNode.Inputs.Count > 1 && initializers.TryGetValue(rawNode.Inputs[1], out var w) ? w : null;
                            Tensor<float>? bias = rawNode.Inputs.Count > 2 && initializers.TryGetValue(rawNode.Inputs[2], out var b) ? b : null;

                            var node = new InferenceNode(nodeName, NodeKind.Conv2D, new[] { inName }, outputName, weight, bias);
                            if (rawNode.Attributes.TryGetValue("strides", out var stridesObj) && stridesObj is long[] strides && strides.Length > 0)
                                node.Stride = (int)strides[0];
                            if (rawNode.Attributes.TryGetValue("pads", out var padsObj) && padsObj is long[] pads && pads.Length > 0)
                                node.Padding = (int)pads[0];
                            if (rawNode.Attributes.TryGetValue("kernel_shape", out var ksObj) && ksObj is long[] ks && ks.Length > 0)
                                node.KernelSize = (int)ks[0];

                            graph.AddNode(node);
                        }
                        break;

                    case "RELU":
                        {
                            string inName = rawNode.Inputs.Count > 0 ? rawNode.Inputs[0] : string.Empty;
                            graph.AddNode(new InferenceNode(nodeName, NodeKind.Relu, new[] { inName }, outputName));
                        }
                        break;

                    case "LEAKYRELU":
                        {
                            string inName = rawNode.Inputs.Count > 0 ? rawNode.Inputs[0] : string.Empty;
                            var node = new InferenceNode(nodeName, NodeKind.LeakyRelu, new[] { inName }, outputName);
                            if (rawNode.Attributes.TryGetValue("alpha", out var alphaObj) && alphaObj is float alpha)
                                node.Alpha = alpha;
                            graph.AddNode(node);
                        }
                        break;

                    case "GEMM":
                    case "MATMUL":
                        {
                            string inName = rawNode.Inputs.Count > 0 ? rawNode.Inputs[0] : string.Empty;
                            Tensor<float>? weight = rawNode.Inputs.Count > 1 && initializers.TryGetValue(rawNode.Inputs[1], out var w) ? w : null;
                            Tensor<float>? bias = rawNode.Inputs.Count > 2 && initializers.TryGetValue(rawNode.Inputs[2], out var b) ? b : null;
                            graph.AddNode(new InferenceNode(nodeName, NodeKind.Linear, new[] { inName }, outputName, weight, bias));
                        }
                        break;

                    case "BATCHNORMALIZATION":
                        {
                            string inName = rawNode.Inputs.Count > 0 ? rawNode.Inputs[0] : string.Empty;
                            Tensor<float>? scale = rawNode.Inputs.Count > 1 && initializers.TryGetValue(rawNode.Inputs[1], out var s) ? s : null;
                            Tensor<float>? bias = rawNode.Inputs.Count > 2 && initializers.TryGetValue(rawNode.Inputs[2], out var b) ? b : null;
                            Tensor<float>? mean = rawNode.Inputs.Count > 3 && initializers.TryGetValue(rawNode.Inputs[3], out var m) ? m : null;
                            Tensor<float>? var = rawNode.Inputs.Count > 4 && initializers.TryGetValue(rawNode.Inputs[4], out var v) ? v : null;

                            var node = new InferenceNode(nodeName, NodeKind.BatchNormalization, new[] { inName }, outputName, scale, bias);
                            if (scale != null) node.ExtraAttributes["Scale"] = scale;
                            if (mean != null) node.ExtraAttributes["RunningMean"] = mean;
                            if (var != null) node.ExtraAttributes["RunningVar"] = var;
                            if (rawNode.Attributes.TryGetValue("epsilon", out var epsObj) && epsObj is float eps)
                                node.Epsilon = eps;

                            graph.AddNode(node);
                        }
                        break;

                    case "ADD":
                        {
                            graph.AddNode(new InferenceNode(nodeName, NodeKind.Add, rawNode.Inputs.ToArray(), outputName));
                        }
                        break;

                    case "MAXPOOL":
                        {
                            string inName = rawNode.Inputs.Count > 0 ? rawNode.Inputs[0] : string.Empty;
                            var node = new InferenceNode(nodeName, NodeKind.MaxPool2D, new[] { inName }, outputName);
                            if (rawNode.Attributes.TryGetValue("strides", out var stridesObj) && stridesObj is long[] strides && strides.Length > 0)
                                node.Stride = (int)strides[0];
                            if (rawNode.Attributes.TryGetValue("kernel_shape", out var ksObj) && ksObj is long[] ks && ks.Length > 0)
                                node.KernelSize = (int)ks[0];
                            graph.AddNode(node);
                        }
                        break;

                    case "AVERAGEPOOL":
                    case "GLOBALAVERAGEPOOL":
                        {
                            string inName = rawNode.Inputs.Count > 0 ? rawNode.Inputs[0] : string.Empty;
                            var kind = rawNode.OpType.Equals("GLOBALAVERAGEPOOL", StringComparison.OrdinalIgnoreCase)
                                ? NodeKind.GlobalAvgPool2D : NodeKind.AvgPool2D;
                            graph.AddNode(new InferenceNode(nodeName, kind, new[] { inName }, outputName));
                        }
                        break;

                    case "FLATTEN":
                        {
                            string inName = rawNode.Inputs.Count > 0 ? rawNode.Inputs[0] : string.Empty;
                            graph.AddNode(new InferenceNode(nodeName, NodeKind.Flatten, new[] { inName }, outputName));
                        }
                        break;

                    case "SOFTMAX":
                        {
                            string inName = rawNode.Inputs.Count > 0 ? rawNode.Inputs[0] : string.Empty;
                            var node = new InferenceNode(nodeName, NodeKind.Softmax, new[] { inName }, outputName);
                            if (rawNode.Attributes.TryGetValue("axis", out var axisObj) && axisObj is long ax)
                                node.Axis = (int)ax;
                            graph.AddNode(node);
                        }
                        break;

                    case "SIGMOID":
                        {
                            string inName = rawNode.Inputs.Count > 0 ? rawNode.Inputs[0] : string.Empty;
                            graph.AddNode(new InferenceNode(nodeName, NodeKind.Sigmoid, new[] { inName }, outputName));
                        }
                        break;

                    case "TANH":
                        {
                            string inName = rawNode.Inputs.Count > 0 ? rawNode.Inputs[0] : string.Empty;
                            graph.AddNode(new InferenceNode(nodeName, NodeKind.Tanh, new[] { inName }, outputName));
                        }
                        break;

                    default:
                        // Generic pass-through node for unmapped ops
                        {
                            string inName = rawNode.Inputs.Count > 0 ? rawNode.Inputs[0] : string.Empty;
                            graph.AddNode(new InferenceNode(nodeName, NodeKind.Relu, new[] { inName }, outputName));
                        }
                        break;
                }
            }

            return graph;
        }

        private static string ParseValueInfoName(ref ProtobufWireReader reader)
        {
            string name = string.Empty;
            while (reader.TryReadTag(out int fieldNumber, out int wireType))
            {
                if (fieldNumber == 1 && wireType == 2)
                    name = reader.ReadString();
                else
                    reader.Skip(wireType);
            }
            return name;
        }

        private static RawNodeInfo ParseNodeProto(ref ProtobufWireReader reader)
        {
            var node = new RawNodeInfo();
            while (reader.TryReadTag(out int fieldNumber, out int wireType))
            {
                switch (fieldNumber)
                {
                    case 1: // input
                        if (wireType == 2) node.Inputs.Add(reader.ReadString());
                        else reader.Skip(wireType);
                        break;
                    case 2: // output
                        if (wireType == 2) node.Outputs.Add(reader.ReadString());
                        else reader.Skip(wireType);
                        break;
                    case 3: // name
                        if (wireType == 2) node.Name = reader.ReadString();
                        else reader.Skip(wireType);
                        break;
                    case 4: // op_type
                        if (wireType == 2) node.OpType = reader.ReadString();
                        else reader.Skip(wireType);
                        break;
                    case 5: // attribute (AttributeProto)
                        if (wireType == 2)
                        {
                            var attrReader = reader.ReadSubReader();
                            ParseAttributeProto(ref attrReader, node.Attributes);
                        }
                        else reader.Skip(wireType);
                        break;
                    default:
                        reader.Skip(wireType);
                        break;
                }
            }
            return node;
        }

        private static void ParseAttributeProto(ref ProtobufWireReader reader, Dictionary<string, object> attributes)
        {
            string name = string.Empty;
            object? val = null;

            while (reader.TryReadTag(out int fieldNumber, out int wireType))
            {
                switch (fieldNumber)
                {
                    case 1: // name
                        name = reader.ReadString();
                        break;
                    case 2: // f (float)
                        val = reader.ReadFloat();
                        break;
                    case 3: // i (int64)
                        val = reader.ReadInt64();
                        break;
                    case 4: // s (bytes / string)
                        val = reader.ReadString();
                        break;
                    case 7: // floats (repeated float)
                        if (wireType == 2)
                        {
                            var sub = reader.ReadSubReader();
                            var list = new List<float>();
                            while (sub.HasMore) list.Add(sub.ReadFloat());
                            val = list.ToArray();
                        }
                        else if (wireType == 5)
                        {
                            val = new[] { reader.ReadFloat() };
                        }
                        break;
                    case 8: // ints (repeated int64)
                        if (wireType == 2)
                        {
                            var sub = reader.ReadSubReader();
                            var list = new List<long>();
                            while (sub.HasMore) list.Add(sub.ReadInt64());
                            val = list.ToArray();
                        }
                        else if (wireType == 0)
                        {
                            val = new[] { reader.ReadInt64() };
                        }
                        break;
                    default:
                        reader.Skip(wireType);
                        break;
                }
            }

            if (!string.IsNullOrEmpty(name) && val != null)
            {
                attributes[name] = val;
            }
        }

        private static Tensor<float>? ParseTensorProto(ref ProtobufWireReader reader, out string tensorName)
        {
            tensorName = string.Empty;
            var dims = new List<int>();
            int dataType = 1; // 1 = FLOAT
            float[]? floatData = null;
            byte[]? rawData = null;

            while (reader.TryReadTag(out int fieldNumber, out int wireType))
            {
                switch (fieldNumber)
                {
                    case 1: // dims (repeated int64)
                        if (wireType == 2)
                        {
                            var sub = reader.ReadSubReader();
                            while (sub.HasMore) dims.Add((int)sub.ReadInt64());
                        }
                        else if (wireType == 0)
                        {
                            dims.Add((int)reader.ReadInt64());
                        }
                        break;

                    case 2: // data_type
                        dataType = reader.ReadInt32();
                        break;

                    case 4: // float_data (repeated float)
                        if (wireType == 2)
                        {
                            var sub = reader.ReadSubReader();
                            var list = new List<float>();
                            while (sub.HasMore) list.Add(sub.ReadFloat());
                            floatData = list.ToArray();
                        }
                        else if (wireType == 5)
                        {
                            var list = new List<float> { reader.ReadFloat() };
                            floatData = list.ToArray();
                        }
                        break;

                    case 7: // raw_data (bytes)
                        rawData = reader.ReadBytes();
                        break;

                    case 8: // name
                        tensorName = reader.ReadString();
                        break;

                    default:
                        reader.Skip(wireType);
                        break;
                }
            }

            int[] shape = dims.Count > 0 ? dims.ToArray() : new[] { 1 };

            if (rawData != null && rawData.Length >= 4)
            {
                int floatCount = rawData.Length / 4;
                var floats = new float[floatCount];
                Buffer.BlockCopy(rawData, 0, floats, 0, rawData.Length);
                return Tensor.FromArray(floats, shape);
            }

            if (floatData != null && floatData.Length > 0)
            {
                return Tensor.FromArray(floatData, shape);
            }

            return null;
        }
    }
}
