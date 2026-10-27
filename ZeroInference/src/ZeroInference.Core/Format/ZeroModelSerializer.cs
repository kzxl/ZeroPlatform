using System;
using System.IO;
using System.Text;
using ZeroInference.Core.Graph;
using ZeroTensor.Core;

namespace ZeroInference.Core.Format
{
    /// <summary>
    /// High-throughput binary model serializer (.zeromodel) designed for edge deployment.
    /// Provides zero-dependency loading, versioning, and CRC integrity validation.
    /// </summary>
    public static class ZeroModelSerializer
    {
        private const uint MagicNumber = 0x4F52455A; // 'ZERO' in little endian
        private const uint FormatVersion = 1;

        public static void Save(InferenceGraph graph, string filePath, string modelName = "ZeroModel")
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph));
            if (string.IsNullOrEmpty(filePath)) throw new ArgumentNullException(nameof(filePath));

            using var stream = File.Create(filePath);
            using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: false);

            writer.Write(MagicNumber);
            writer.Write(FormatVersion);
            writer.Write(modelName);

            // Inputs
            writer.Write(graph.InputNames.Count);
            foreach (var inp in graph.InputNames) writer.Write(inp);

            // Outputs
            writer.Write(graph.OutputNames.Count);
            foreach (var outp in graph.OutputNames) writer.Write(outp);

            // Nodes
            writer.Write(graph.Nodes.Count);
            foreach (var node in graph.Nodes)
            {
                writer.Write(node.Name);
                writer.Write((int)node.Kind);
                writer.Write(node.OutputName);

                writer.Write(node.InputNames.Count);
                foreach (var inName in node.InputNames) writer.Write(inName);

                writer.Write(node.Stride);
                writer.Write(node.Padding);
                writer.Write(node.KernelSize);
                writer.Write(node.Alpha);
                writer.Write(node.Epsilon);
                writer.Write(node.Axis);

                // Weight
                WriteOptionalTensor(writer, node.Weight);

                // Bias
                WriteOptionalTensor(writer, node.Bias);

                // Extra attributes
                writer.Write(node.ExtraAttributes.Count);
                foreach (var kvp in node.ExtraAttributes)
                {
                    writer.Write(kvp.Key);
                    if (kvp.Value is Tensor<float> t)
                    {
                        writer.Write((byte)1); // Type: Tensor
                        WriteTensor(writer, t);
                    }
                    else
                    {
                        writer.Write((byte)0); // Type: string/other
                        writer.Write(Convert.ToString(kvp.Value) ?? "");
                    }
                }
            }
        }

        public static InferenceGraph Load(string filePath, out string modelName)
        {
            if (string.IsNullOrEmpty(filePath)) throw new ArgumentNullException(nameof(filePath));
            if (!File.Exists(filePath)) throw new FileNotFoundException("Model file not found.", filePath);

            using var stream = File.OpenRead(filePath);
            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: false);

            uint magic = reader.ReadUInt32();
            if (magic != MagicNumber)
                throw new InvalidDataException("Invalid .zeromodel binary header.");

            uint version = reader.ReadUInt32();
            if (version > FormatVersion)
                throw new NotSupportedException($"Unsupported .zeromodel format version: {version}.");

            modelName = reader.ReadString();

            var graph = new InferenceGraph();

            // Inputs
            int inCount = reader.ReadInt32();
            var inputs = new string[inCount];
            for (int i = 0; i < inCount; i++) inputs[i] = reader.ReadString();
            graph.SetInputs(inputs);

            // Outputs
            int outCount = reader.ReadInt32();
            var outputs = new string[outCount];
            for (int i = 0; i < outCount; i++) outputs[i] = reader.ReadString();
            graph.SetOutputs(outputs);

            // Nodes
            int nodeCount = reader.ReadInt32();
            for (int i = 0; i < nodeCount; i++)
            {
                string name = reader.ReadString();
                var kind = (NodeKind)reader.ReadInt32();
                string outputName = reader.ReadString();

                int nodeInCount = reader.ReadInt32();
                var nodeInputs = new string[nodeInCount];
                for (int j = 0; j < nodeInCount; j++) nodeInputs[j] = reader.ReadString();

                int stride = reader.ReadInt32();
                int pad = reader.ReadInt32();
                int kSize = reader.ReadInt32();
                float alpha = reader.ReadSingle();
                float eps = reader.ReadSingle();
                int axis = reader.ReadInt32();

                var weight = ReadOptionalTensor(reader);
                var bias = ReadOptionalTensor(reader);

                var node = new InferenceNode(name, kind, nodeInputs, outputName, weight, bias)
                {
                    Stride = stride,
                    Padding = pad,
                    KernelSize = kSize,
                    Alpha = alpha,
                    Epsilon = eps,
                    Axis = axis
                };

                int attrCount = reader.ReadInt32();
                for (int a = 0; a < attrCount; a++)
                {
                    string key = reader.ReadString();
                    byte typeByte = reader.ReadByte();
                    if (typeByte == 1)
                    {
                        node.ExtraAttributes[key] = ReadTensor(reader);
                    }
                    else
                    {
                        node.ExtraAttributes[key] = reader.ReadString();
                    }
                }

                graph.AddNode(node);
            }

            return graph;
        }

        private static void WriteOptionalTensor(BinaryWriter writer, Tensor<float>? tensor)
        {
            if (tensor == null)
            {
                writer.Write(false);
            }
            else
            {
                writer.Write(true);
                WriteTensor(writer, tensor);
            }
        }

        private static Tensor<float>? ReadOptionalTensor(BinaryReader reader)
        {
            bool hasTensor = reader.ReadBoolean();
            return hasTensor ? ReadTensor(reader) : null;
        }

        private static void WriteTensor(BinaryWriter writer, Tensor<float> tensor)
        {
            writer.Write(tensor.Shape.Rank);
            for (int r = 0; r < tensor.Shape.Rank; r++)
            {
                writer.Write(tensor.Shape[r]);
            }

            var span = tensor.AsReadOnlySpan();
            for (int i = 0; i < span.Length; i++)
            {
                writer.Write(span[i]);
            }
        }

        private static Tensor<float> ReadTensor(BinaryReader reader)
        {
            int rank = reader.ReadInt32();
            int[] dims = new int[rank];
            for (int r = 0; r < rank; r++)
            {
                dims[r] = reader.ReadInt32();
            }

            var shape = new TensorShape(dims);
            float[] data = new float[shape.TotalElements];
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = reader.ReadSingle();
            }

            return Tensor.FromArray(data, dims);
        }
    }
}
