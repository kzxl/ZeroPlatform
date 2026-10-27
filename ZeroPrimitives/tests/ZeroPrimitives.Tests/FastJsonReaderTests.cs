using System;
using System.Text;
using Xunit;
using ZeroPrimitives.Parsing;

namespace ZeroPrimitives.Tests
{
    public class FastJsonReaderTests
    {
        [Fact]
        public void FastJsonReader_ParsesSampleObjectWithoutAllocations()
        {
            string json = @"{ ""id"": 12345, ""code"": ""MDS-PART"", ""price"": 99.50, ""active"": true }";
            byte[] bytes = Encoding.UTF8.GetBytes(json);

            var reader = new FastJsonReader(bytes);

            Assert.True(reader.Read());
            Assert.Equal(FastJsonTokenType.StartObject, reader.TokenType);

            Assert.True(reader.Read());
            Assert.Equal(FastJsonTokenType.PropertyName, reader.TokenType);
            Assert.Equal("id", reader.GetString());

            Assert.True(reader.Read());
            Assert.Equal(FastJsonTokenType.Number, reader.TokenType);
            Assert.Equal(12345, reader.GetInt32());

            Assert.True(reader.Read());
            Assert.Equal(FastJsonTokenType.PropertyName, reader.TokenType);
            Assert.Equal("code", reader.GetString());

            Assert.True(reader.Read());
            Assert.Equal(FastJsonTokenType.String, reader.TokenType);
            Assert.Equal("MDS-PART", reader.GetString());

            Assert.True(reader.Read());
            Assert.Equal(FastJsonTokenType.PropertyName, reader.TokenType);
            Assert.Equal("price", reader.GetString());

            Assert.True(reader.Read());
            Assert.Equal(FastJsonTokenType.Number, reader.TokenType);
            Assert.Equal(99.50m, reader.GetDecimal());

            Assert.True(reader.Read());
            Assert.Equal(FastJsonTokenType.PropertyName, reader.TokenType);
            Assert.Equal("active", reader.GetString());

            Assert.True(reader.Read());
            Assert.Equal(FastJsonTokenType.True, reader.TokenType);
            Assert.True(reader.GetBoolean());

            Assert.True(reader.Read());
            Assert.Equal(FastJsonTokenType.EndObject, reader.TokenType);

            Assert.False(reader.Read());
        }
    }
}
