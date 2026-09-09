using System;
using System.Diagnostics;
using Xunit;
using ZeroPrimitives;
using ZeroPrimitives.Extensions;
using ZeroPrimitives.Parsing;

namespace ZeroPrimitives.Tests
{
    public class PerformanceThroughputTests
    {
        [Fact]
        public void IntegerFastPath_100kConversions_ExecutesInMilliseconds()
        {
            // Warm up
            FastConvert.AsInt("12345");

            var sw = Stopwatch.StartNew();
            int sum = 0;
            for (int i = 0; i < 100_000; i++)
            {
                sum += FastConvert.AsInt("12345");
            }
            sw.Stop();

            Assert.Equal(12345 * 100_000, sum);
            // 100k conversions should complete in under 50ms (typically 3-10ms)
            Assert.True(sw.ElapsedMilliseconds < 50, $"Elapsed: {sw.ElapsedMilliseconds}ms for 100,000 conversions");
        }

        [Fact]
        public void DecimalFastPath_100kConversions_ExecutesInMilliseconds()
        {
            // Warm up
            FastConvert.AsDecimal("1234.56");

            var sw = Stopwatch.StartNew();
            decimal sum = 0;
            for (int i = 0; i < 100_000; i++)
            {
                sum += FastConvert.AsDecimal("1234.56");
            }
            sw.Stop();

            Assert.Equal(1234.56m * 100_000m, sum);
            // 100k conversions should complete in under 50ms
            Assert.True(sw.ElapsedMilliseconds < 50, $"Elapsed: {sw.ElapsedMilliseconds}ms for 100,000 conversions");
        }

        [Fact]
        public void GenericTo_ZeroBoxing_ExecutesAtRegisterSpeed()
        {
            object obj = "999";

            var sw = Stopwatch.StartNew();
            int sum = 0;
            for (int i = 0; i < 100_000; i++)
            {
                sum += obj.To<int>();
            }
            sw.Stop();

            Assert.Equal(999 * 100_000, sum);
            Assert.True(sw.ElapsedMilliseconds < 50, $"Elapsed: {sw.ElapsedMilliseconds}ms for 100,000 generic conversions");
        }

        [Fact]
        public void ExtractDigitsToInt_SinglePass_ExecutesInMilliseconds()
        {
            string code = "ORDER-2026-9999-XYZ";

            var sw = Stopwatch.StartNew();
            long sum = 0;
            for (int i = 0; i < 100_000; i++)
            {
                sum += code.ExtractDigitsToInt();
            }
            sw.Stop();

            Assert.Equal(20269999L * 100_000L, sum);
            Assert.True(sw.ElapsedMilliseconds < 50, $"Elapsed: {sw.ElapsedMilliseconds}ms for 100,000 digit extractions");
        }
    }
}
