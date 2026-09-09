using System;
using Xunit;
using ZeroPrimitives;
using ZeroPrimitives.Extensions;

namespace ZeroPrimitives.Tests
{
    public class FastConvertTests
    {
        [Fact]
        public void AsInt_DirectPrimitiveTypes_ReturnsExpected()
        {
            Assert.Equal(123, FastConvert.AsInt(123));
            Assert.Equal(123, FastConvert.AsInt(123L));
            Assert.Equal(123, FastConvert.AsInt((short)123));
            Assert.Equal(123, FastConvert.AsInt((byte)123));
            Assert.Equal(123, FastConvert.AsInt(123.45m));
            Assert.Equal(123, FastConvert.AsInt(123.99));
            Assert.Equal(1, FastConvert.AsInt(true));
            Assert.Equal(0, FastConvert.AsInt(false));
        }

        [Fact]
        public void AsInt_NullAndDBNull_ReturnsDefault()
        {
            Assert.Equal(0, FastConvert.AsInt(null));
            Assert.Equal(99, FastConvert.AsInt(null, 99));
            Assert.Equal(0, FastConvert.AsInt(DBNull.Value));
            Assert.Equal(99, FastConvert.AsInt(DBNull.Value, 99));
        }

        [Fact]
        public void AsInt_StringInputs_ParsesProperly()
        {
            Assert.Equal(12345, FastConvert.AsInt("12345"));
            Assert.Equal(-12345, FastConvert.AsInt("-12345"));
            Assert.Equal(12345, FastConvert.AsInt(" +12345 "));
            Assert.Equal(12345, FastConvert.AsInt("12,345"));
            Assert.Equal(12345, FastConvert.AsInt("12.345 VNĐ"));
            Assert.Equal(12345, FastConvert.AsInt(" 12345 VNĐ "));
            Assert.Equal(12345, FastConvert.AsInt(" $12345 "));
            Assert.Equal(123, FastConvert.AsInt("123.45")); // Truncate decimals
        }

        [Fact]
        public void AsNullableInt_HandlesValuesCorrectly()
        {
            Assert.Null(FastConvert.AsNullableInt(null));
            Assert.Null(FastConvert.AsNullableInt(DBNull.Value));
            Assert.Null(FastConvert.AsNullableInt(""));
            Assert.Null(FastConvert.AsNullableInt("   "));
            Assert.Null(FastConvert.AsNullableInt("invalid"));
            Assert.Equal(42, FastConvert.AsNullableInt("42"));
            Assert.Equal(42, FastConvert.AsNullableInt(42));
        }

        [Fact]
        public void AsDecimal_VariedInputs_ParsesAccurately()
        {
            Assert.Equal(1234.56m, FastConvert.AsDecimal(1234.56m));
            Assert.Equal(1234.56m, FastConvert.AsDecimal("1234.56"));
            Assert.Equal(1234.56m, FastConvert.AsDecimal("1,234.56"));
            Assert.Equal(1234.56m, FastConvert.AsDecimal("1.234,56"));
            Assert.Equal(1500000m, FastConvert.AsDecimal("1.500.000 VNĐ"));
            Assert.Equal(1500000m, FastConvert.AsDecimal("1,500,000 đ"));
            Assert.Equal(0m, FastConvert.AsDecimal(null));
            Assert.Equal(0m, FastConvert.AsDecimal(DBNull.Value));
        }

        [Fact]
        public void AsBool_TruthinessAndStringParsing()
        {
            Assert.True(FastConvert.AsBool(true));
            Assert.False(FastConvert.AsBool(false));
            Assert.True(FastConvert.AsBool(1));
            Assert.False(FastConvert.AsBool(0));
            Assert.True(FastConvert.AsBool("true"));
            Assert.True(FastConvert.AsBool("TRUE"));
            Assert.True(FastConvert.AsBool("1"));
            Assert.True(FastConvert.AsBool("yes"));
            Assert.True(FastConvert.AsBool("Y"));
            Assert.False(FastConvert.AsBool("false"));
            Assert.False(FastConvert.AsBool("0"));
            Assert.False(FastConvert.AsBool("no"));
            Assert.False(FastConvert.AsBool(null));
        }

        [Fact]
        public void ExtensionMethods_MatchFastConvert()
        {
            object objInt = 789;
            object objStr = " 1,234 VNĐ ";
            object? objNull = null;

            Assert.True(objInt.HasValue());
            Assert.True(objStr.HasValue());
            Assert.False(objNull.HasValue());

            Assert.Equal(789, objInt.AsInt());
            Assert.Equal(1234, objStr.AsInt());
            Assert.Equal(1234m, objStr.AsDecimal());
            Assert.Equal(0, objNull.AsInt());
        }

        [Fact]
        public void AsGuid_ValidAndInvalidGuids()
        {
            var expected = Guid.NewGuid();
            Assert.Equal(expected, FastConvert.AsGuid(expected));
            Assert.Equal(expected, FastConvert.AsGuid(expected.ToString()));
            Assert.Equal(expected, FastConvert.AsGuid(expected.ToByteArray()));
            Assert.Equal(Guid.Empty, FastConvert.AsGuid(null));
            Assert.Equal(Guid.Empty, FastConvert.AsGuid("invalid-guid"));
            Assert.Null(FastConvert.AsNullableGuid(null));
            Assert.Equal(expected, FastConvert.AsNullableGuid(expected.ToString()));
        }

        [Fact]
        public void AsDouble_And_AsLong_HandlesInputsCorrectly()
        {
            Assert.Equal(123.456, FastConvert.AsDouble(123.456));
            Assert.Equal(123.456, FastConvert.AsDouble("123.456"));
            Assert.Equal(0.0, FastConvert.AsDouble(null));
            Assert.Equal(0.0, FastConvert.AsDouble(DBNull.Value));

            long big = 9876543210L;
            Assert.Equal(big, FastConvert.AsLong(big));
            Assert.Equal(big, FastConvert.AsLong("9,876,543,210"));
            Assert.Equal(0L, FastConvert.AsLong(null));
            Assert.Null(FastConvert.AsNullableLong(null));
            Assert.Equal(big, FastConvert.AsNullableLong(big));
        }
    }
}
