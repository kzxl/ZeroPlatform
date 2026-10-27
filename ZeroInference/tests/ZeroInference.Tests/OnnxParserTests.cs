using System;
using System.IO;
using System.Text;
using Xunit;
using ZeroInference.Core.Engine;
using ZeroInference.Core.Format;
using ZeroInference.Core.Graph;
using ZeroTensor.Core;

namespace ZeroInference.Tests
{
    public class OnnxParserTests
    {
        private static void WriteVarint(Stream stream, ulong value)
        {
            while (value >= 0x80)
            {
                stream.WriteByte((byte)((value & 0x7F) | 0x80));
                value >>= 7;
            }
            stream.WriteByte((byte)(value & 0x7F));
        }

        private static void WriteTag(Stream stream, int fieldNumber, int wireType)
        {
            WriteVarint(stream, (ulong)((fieldNumber << 3) | wireType));
        }

        private static void WriteLengthDelimited(Stream stream, int fieldNumber, byte[] payload)
        {
            WriteTag(stream, fieldNumber, 2);
            WriteVarint(stream, (ulong)payload.Length);
            stream.Write(payload, 0, payload.Length);
        }

        private static void WriteString(Stream stream, int fieldNumber, string str)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(str);
            WriteLengthDelimited(stream, fieldNumber, bytes);
        }

        [Fact]
        public void OnnxModelParser_ParsesValidBinaryStream_AndExecutesSession()
        {
            // =========================================================================
            // Construct a synthetic valid ONNX protobuf ModelProto stream in memory
            // =========================================================================
            using var modelMs = new MemoryStream();
            using (var graphMs = new MemoryStream())
            {
                // GraphProto: Tag 2 = name ("ToyVisionNet")
                WriteString(graphMs, 2, "ToyVisionNet");

                // GraphProto: Tag 11 = input ("input_0")
                using (var viMs = new MemoryStream())
                {
                    WriteString(viMs, 1, "input_0");
                    WriteLengthDelimited(graphMs, 11, viMs.ToArray());
                }

                // GraphProto: Tag 12 = output ("relu_out")
                using (var voMs = new MemoryStream())
                {
                    WriteString(voMs, 1, "relu_out");
                    WriteLengthDelimited(graphMs, 12, voMs.ToArray());
                }

                // Initializer: "conv_w" (shape 1x1x3x3 = 9 floats)
                using (var tMs = new MemoryStream())
                {
                    WriteString(tMs, 8, "conv_w");
                    // dims: 1, 1, 3, 3
                    WriteTag(tMs, 1, 0); WriteVarint(tMs, 1);
                    WriteTag(tMs, 1, 0); WriteVarint(tMs, 1);
                    WriteTag(tMs, 1, 0); WriteVarint(tMs, 3);
                    WriteTag(tMs, 1, 0); WriteVarint(tMs, 3);
                    // data_type = 1 (FLOAT)
                    WriteTag(tMs, 2, 0); WriteVarint(tMs, 1);
                    // raw_data
                    float[] wFloats = new float[] { 0.1f, 0.2f, 0.1f, 0.0f, 0.5f, 0.0f, -0.1f, 0.2f, 0.1f };
                    byte[] rawBytes = new byte[wFloats.Length * 4];
                    Buffer.BlockCopy(wFloats, 0, rawBytes, 0, rawBytes.Length);
                    WriteLengthDelimited(tMs, 7, rawBytes);

                    WriteLengthDelimited(graphMs, 5, tMs.ToArray());
                }

                // Node 1: Conv ("Conv_1", op_type = "Conv", inputs=["input_0", "conv_w"], output=["conv_out"])
                using (var n1Ms = new MemoryStream())
                {
                    WriteString(n1Ms, 3, "Conv_1");
                    WriteString(n1Ms, 4, "Conv");
                    WriteString(n1Ms, 1, "input_0");
                    WriteString(n1Ms, 1, "conv_w");
                    WriteString(n1Ms, 2, "conv_out");
                    WriteLengthDelimited(graphMs, 1, n1Ms.ToArray());
                }

                // Node 2: Relu ("Relu_1", op_type = "Relu", inputs=["conv_out"], output=["relu_out"])
                using (var n2Ms = new MemoryStream())
                {
                    WriteString(n2Ms, 3, "Relu_1");
                    WriteString(n2Ms, 4, "Relu");
                    WriteString(n2Ms, 1, "conv_out");
                    WriteString(n2Ms, 2, "relu_out");
                    WriteLengthDelimited(graphMs, 1, n2Ms.ToArray());
                }

                // ModelProto: Tag 7 = graph
                WriteLengthDelimited(modelMs, 7, graphMs.ToArray());
            }

            byte[] onnxBytes = modelMs.ToArray();

            // Parse ONNX model
            var parsedGraph = OnnxModelParser.Parse(onnxBytes);
            Assert.NotNull(parsedGraph);
            Assert.Single(parsedGraph.InputNames);
            Assert.Equal("input_0", parsedGraph.InputNames[0]);
            Assert.Single(parsedGraph.OutputNames);
            Assert.Equal("relu_out", parsedGraph.OutputNames[0]);

            Assert.Equal(2, parsedGraph.Nodes.Count);
            var convNode = parsedGraph.Nodes[0];
            Assert.Equal(NodeKind.Conv2D, convNode.Kind);
            Assert.NotNull(convNode.Weight);
            Assert.Equal(9, convNode.Weight.Length);

            var reluNode = parsedGraph.Nodes[1];
            Assert.Equal(NodeKind.Relu, reluNode.Kind);

            // =========================================================================
            // Save to native .zeromodel format & reload
            // =========================================================================
            string tempModelPath = Path.Combine(Path.GetTempPath(), $"onnx_to_zero_{Guid.NewGuid():N}.zeromodel");
            try
            {
                ZeroModelSerializer.Save(parsedGraph, tempModelPath, "ConvertedOnnxModel");
                var reloadedGraph = ZeroModelSerializer.Load(tempModelPath, out string loadedModelName);
                Assert.Equal("ConvertedOnnxModel", loadedModelName);
                Assert.Equal(2, reloadedGraph.Nodes.Count);
                Assert.Equal("input_0", reloadedGraph.InputNames[0]);
                Assert.Equal("relu_out", reloadedGraph.OutputNames[0]);

                // Compile into ExecutionSession and forward pass
                var inputTensor = Tensor.Zeros<float>(1, 1, 5, 5);
                inputTensor.Fill(1.0f);

                var session = InferenceEngine.CreateSession(reloadedGraph);
                var output = session.Run(inputTensor);

                Assert.NotNull(output);
                Assert.True(output.Length > 0);
            }
            finally
            {
                if (File.Exists(tempModelPath))
                {
                    try { File.Delete(tempModelPath); } catch { }
                }
            }
        }
    }
}
