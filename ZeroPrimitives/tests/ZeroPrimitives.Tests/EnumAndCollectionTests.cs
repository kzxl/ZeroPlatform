using Xunit;
using ZeroPrimitives;
using ZeroPrimitives.Extensions;

namespace ZeroPrimitives.Tests
{
    public class EnumAndCollectionTests
    {
        public enum DeviceStatus
        {
            Offline = 0,
            Online = 1,
            Maintenance = 2,
            Error = 99
        }

        [Fact]
        public void AsEnum_ConvertsFromIntStringAndExactEnum()
        {
            Assert.Equal(DeviceStatus.Online, FastConvert.AsEnum<DeviceStatus>(1));
            Assert.Equal(DeviceStatus.Maintenance, FastConvert.AsEnum<DeviceStatus>("Maintenance"));
            Assert.Equal(DeviceStatus.Maintenance, FastConvert.AsEnum<DeviceStatus>("maintenance"));
            Assert.Equal(DeviceStatus.Error, FastConvert.AsEnum<DeviceStatus>("99"));
            Assert.Equal(DeviceStatus.Offline, FastConvert.AsEnum<DeviceStatus>(null));
            Assert.Equal(DeviceStatus.Offline, FastConvert.AsEnum<DeviceStatus>("invalid-status"));

            // Extension method syntax
            object statusObj = "Online";
            Assert.Equal(DeviceStatus.Online, statusObj.AsEnum<DeviceStatus>());
        }

        [Fact]
        public void AsNullableEnum_HandlesNullAndBlanks()
        {
            Assert.Null(FastConvert.AsNullableEnum<DeviceStatus>(null));
            Assert.Null(FastConvert.AsNullableEnum<DeviceStatus>(""));
            Assert.Null(FastConvert.AsNullableEnum<DeviceStatus>("   "));
            Assert.Equal(DeviceStatus.Online, FastConvert.AsNullableEnum<DeviceStatus>("Online"));
        }

        [Fact]
        public void DelimitedString_ParsesAndJoinsProperly()
        {
            string csv = "1, 2, 3, 4, 5";
            int[] numbers = FastConvert.FromDelimitedString<int>(csv);
            Assert.Equal(new[] { 1, 2, 3, 4, 5 }, numbers);

            string joined = FastConvert.AsDelimitedString(numbers, ";");
            Assert.Equal("1;2;3;4;5", joined);

            string statusCsv = "Online, Maintenance, Error";
            DeviceStatus[] statuses = FastConvert.FromDelimitedString<DeviceStatus>(statusCsv);
            Assert.Equal(new[] { DeviceStatus.Online, DeviceStatus.Maintenance, DeviceStatus.Error }, statuses);
        }

        [Fact]
        public void AsNumberString_FormatsDecimalStandard()
        {
            object obj = 1234.56m;
            Assert.Equal("1234.56", obj.AsNumberString());

            object? nullObj = null;
            Assert.Equal(string.Empty, nullObj.AsNumberString());
        }
    }
}
