using System;
using System.Text;
using Xunit;
using ZeroPrimitives.Buffers;
using ZeroPrimitives.Concurrency;
using ZeroPrimitives.Parsing;

namespace ZeroPrimitives.Tests
{
    public class SafetyAndOverflowTests
    {
        [Fact]
        public void FastNumberParser_ExcessiveDigits_ReturnsFalseWithoutOverflow()
        {
            // 25 digits number exceeds 64-bit int
            string excessive = "9999999999999999999999999";
            Assert.False(FastNumberParser.TryParseInt64(excessive.AsSpan(), out _));
            Assert.False(FastNumberParser.TryParseInt64(Encoding.UTF8.GetBytes(excessive), out _));

            string excessiveNegative = "-9999999999999999999999999";
            Assert.False(FastNumberParser.TryParseInt64(excessiveNegative.AsSpan(), out _));
            Assert.False(FastNumberParser.TryParseInt64(Encoding.UTF8.GetBytes(excessiveNegative), out _));

            // Excessive 32-bit int
            string excessive32 = "99999999999";
            Assert.False(FastNumberParser.TryParseInt32(excessive32.AsSpan(), out _));
            Assert.False(FastNumberParser.TryParseInt32(Encoding.UTF8.GetBytes(excessive32), out _));
        }

        [Fact]
        public void FastNumberParser_BoundaryValues_ParseExactMinMax()
        {
            // Int64 Min/Max
            string max64 = long.MaxValue.ToString();
            Assert.True(FastNumberParser.TryParseInt64(max64.AsSpan(), out long parsedMax64));
            Assert.Equal(long.MaxValue, parsedMax64);

            string min64 = long.MinValue.ToString();
            Assert.True(FastNumberParser.TryParseInt64(min64.AsSpan(), out long parsedMin64));
            Assert.Equal(long.MinValue, parsedMin64);

            // Int32 Min/Max
            string max32 = int.MaxValue.ToString();
            Assert.True(FastNumberParser.TryParseInt32(max32.AsSpan(), out int parsedMax32));
            Assert.Equal(int.MaxValue, parsedMax32);

            string min32 = int.MinValue.ToString();
            Assert.True(FastNumberParser.TryParseInt32(min32.AsSpan(), out int parsedMin32));
            Assert.Equal(int.MinValue, parsedMin32);
        }

        [Fact]
        public void SpanReader_TryReadMethods_HandleTruncatedBuffersGracefully()
        {
            byte[] truncated = new byte[3]; // Need 4 bytes for Int32
            var reader = new SpanReader(truncated);

            Assert.False(reader.TryReadInt32LittleEndian(out _));
            Assert.False(reader.TryReadInt32BigEndian(out _));
            Assert.False(reader.TryReadInt64LittleEndian(out _));

            // Int16 needs 2 bytes -> should succeed
            Assert.True(reader.TryReadInt16LittleEndian(out _));
            // Only 1 byte left -> Int16 should now fail
            Assert.False(reader.TryReadInt16LittleEndian(out _));
        }

        [Fact]
        public void SpanWriter_TryWriteMethods_PreventBufferOverruns()
        {
            Span<byte> buffer = stackalloc byte[5];
            var writer = new SpanWriter(buffer);

            Assert.True(writer.TryWriteInt32LittleEndian(100)); // 4 bytes used, 1 remaining
            Assert.False(writer.TryWriteInt32LittleEndian(200)); // Needs 4 bytes -> fails safely
            Assert.True(writer.TryWriteByte(0xFF));              // 1 byte -> succeeds
            Assert.False(writer.TryWriteByte(0xAA));             // Full -> fails safely
        }

        [Fact]
        public void FastJsonReader_UnterminatedString_SetsHasErrorFlag()
        {
            string brokenJson = "{\"title\": \"Unfinished string without closing quote";
            byte[] bytes = Encoding.UTF8.GetBytes(brokenJson);

            var reader = new FastJsonReader(bytes);
            Assert.True(reader.Read()); // StartObject
            Assert.True(reader.Read()); // PropertyName "title"
            Assert.False(reader.Read()); // Fails due to unclosed string
            Assert.True(reader.HasError);
        }

        [Fact]
        public void FastCsvParser_VectorizedParsing_HandlesMixedQuotedAndUnquotedRows()
        {
            string csv = "1,Item A,100\r\n2,\"Item, B\",200\r\n3,\"Item \"\"C\"\"\",300";
            int rowCount = 0;

            foreach (var row in FastCsvParser.EnumerateRows(csv.AsSpan()))
            {
                rowCount++;
                int cellCount = 0;
                foreach (var cell in FastCsvParser.EnumerateCells(row, ','))
                {
                    cellCount++;
                }
                Assert.Equal(3, cellCount);
            }

            Assert.Equal(3, rowCount);
        }
    }
}
