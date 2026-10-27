using System;
using Xunit;
using ZeroPrimitives.Text;

namespace ZeroPrimitives.Tests
{
    public class FastJsonWriterTests
    {
        [Fact]
        public void SimpleObject_GeneratesValidJson()
        {
            Span<byte> buffer = stackalloc byte[256];
            using var writer = new FastJsonWriter(buffer);

            writer.WriteStartObject();
            writer.WriteString("id", "PROD-001");
            writer.WriteNumber("qty", 100);
            writer.WriteNumber("price", 12.50m);
            writer.WriteBoolean("active", true);
            writer.WriteNull("notes");
            writer.WriteEndObject();

            string json = writer.ToString();
            Assert.Equal("{\"id\":\"PROD-001\",\"qty\":100,\"price\":12.50,\"active\":true,\"notes\":null}", json);
        }

        [Fact]
        public void NestedObjectAndArray_FormatsCorrectly()
        {
            Span<byte> buffer = stackalloc byte[512];
            using var writer = new FastJsonWriter(buffer);

            writer.WriteStartObject();
            writer.WriteString("title", "Batch Report");
            writer.WritePropertyName("items");
            writer.WriteStartArray();
            writer.WriteString("Item1");
            writer.WriteString("Item2");
            writer.WriteEndArray();
            writer.WriteEndObject();

            string json = writer.ToString();
            Assert.Equal("{\"title\":\"Batch Report\",\"items\":[\"Item1\",\"Item2\"]}", json);
        }

        [Fact]
        public void StringEscaping_HandlesSpecialCharacters()
        {
            Span<byte> buffer = stackalloc byte[256];
            using var writer = new FastJsonWriter(buffer);

            writer.WriteStartObject();
            writer.WriteString("quote", "Line1\nLine2 \"Quote\" \tTab");
            writer.WriteEndObject();

            string json = writer.ToString();
            Assert.Equal("{\"quote\":\"Line1\\nLine2 \\\"Quote\\\" \\tTab\"}", json);
        }
    }
}
