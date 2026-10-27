using System.IO;
using Xunit;
using ZeroLlm.Core.Format;

namespace ZeroLlm.Tests
{
    public class GgufFormatTests
    {
        [Fact]
        public void GgufWriter_And_Reader_Should_Roundtrip_Accurately()
        {
            var writer = new GgufWriter();
            writer.AddMetadata("general.architecture", "llama");
            writer.AddMetadata("llama.context_length", (uint)4096);
            writer.AddMetadata("llama.embedding_length", (uint)2048);
            writer.AddMetadata("llama.attention.head_count", (uint)16);
            writer.AddMetadata("llama.attention.head_count_kv", (uint)4);
            writer.AddMetadata("llama.attention.layer_norm_rms_epsilon", 1e-5f);

            byte[] dummyWeight = new byte[64];
            for (int i = 0; i < dummyWeight.Length; i++) dummyWeight[i] = (byte)i;

            writer.AddTensor("token_embd.weight", new ulong[] { 32, 2 }, GgufTensorType.F32, dummyWeight);

            using (var ms = new MemoryStream())
            {
                writer.WriteTo(ms);
                ms.Position = 0;

                var reader = GgufReader.FromStream(ms);

                Assert.Equal((uint)3, reader.Version);
                Assert.Equal((ulong)1, reader.TensorCount);
                Assert.Equal("llama", reader.Metadata.Architecture);
                Assert.Equal(4096u, reader.Metadata.ContextLength);
                Assert.Equal(2048u, reader.Metadata.EmbeddingLength);
                Assert.Equal(16u, reader.Metadata.HeadCount);
                Assert.Equal(4u, reader.Metadata.HeadCountKv);
                Assert.Equal(1e-5f, reader.Metadata.RmsNormEpsilon);

                Assert.Single(reader.Tensors);
                var tensor = reader.Tensors[0];
                Assert.Equal("token_embd.weight", tensor.Name);
                Assert.Equal(GgufTensorType.F32, tensor.Type);
                Assert.Equal(2, tensor.Dimensions.Length);
                Assert.Equal((ulong)32, tensor.Dimensions[0]);
                Assert.Equal((ulong)2, tensor.Dimensions[1]);
                Assert.Equal((ulong)64, tensor.ElementCount);
            }
        }
    }
}
