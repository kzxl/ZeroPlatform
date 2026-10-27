using System;
using Xunit;
using ZeroPrimitives.Buffers;
using ZeroPrimitives.Text;

namespace ZeroPrimitives.Tests
{
    public class NewPrimitivesTests
    {
        #region ValueList Tests

        [Fact]
        public void ValueList_StackBuffer_AddsAndIndexesWithZeroAllocation()
        {
            Span<int> stackBuf = stackalloc int[4];
            using var list = new ValueList<int>(stackBuf);

            list.Add(10);
            list.Add(20);
            list.Add(30);

            Assert.Equal(3, list.Count);
            Assert.Equal(4, list.Capacity);
            Assert.Equal(10, list[0]);
            Assert.Equal(20, list[1]);
            Assert.Equal(30, list[2]);
        }

        [Fact]
        public void ValueList_ExceedsInitialBuffer_GrowsViaArrayPool()
        {
            Span<int> stackBuf = stackalloc int[2];
            using var list = new ValueList<int>(stackBuf);

            list.Add(1);
            list.Add(2);
            list.Add(3); // triggers growth
            list.Add(4);
            list.Add(5);

            Assert.Equal(5, list.Count);
            Assert.True(list.Capacity >= 16);
            for (int i = 0; i < 5; i++)
            {
                Assert.Equal(i + 1, list[i]);
            }
        }

        [Fact]
        public void ValueList_AddRange_AppendsCorrectly()
        {
            Span<int> stackBuf = stackalloc int[4];
            using var list = new ValueList<int>(stackBuf);

            ReadOnlySpan<int> items = stackalloc int[] { 100, 200, 300, 400, 500 };
            list.AddRange(items);

            Assert.Equal(5, list.Count);
            Assert.Equal(100, list[0]);
            Assert.Equal(500, list[4]);
        }

        #endregion

        #region FixedString Tests

        [Fact]
        public void FixedString32_CreatesAndComparesAccurately()
        {
            FixedString32 fs1 = "HELLO_WORLD";
            FixedString32 fs2 = "HELLO_WORLD";
            FixedString32 fs3 = "DIFFERENT";

            Assert.Equal(11, fs1.Length);
            Assert.False(fs1.IsEmpty);
            Assert.Equal("HELLO_WORLD", fs1.ToString());

            Assert.True(fs1 == fs2);
            Assert.True(fs1.Equals(fs2));
            Assert.False(fs1 == fs3);
            Assert.Equal(fs1.GetHashCode(), fs2.GetHashCode());
        }

        [Fact]
        public void FixedString64_HandlesLongerStrings()
        {
            string original = "MDS-ORD-2026-XYZ-VERY-LONG-IDENTIFIER-TELEMETRY-PAYLOAD-CODE";
            FixedString64 fs = original;

            Assert.Equal(original.Length, fs.Length);
            Assert.Equal(original, fs.ToString());
        }

        [Fact]
        public void FixedString_EmptyString_HandledProperly()
        {
            FixedString32 fs = string.Empty;
            Assert.Equal(0, fs.Length);
            Assert.True(fs.IsEmpty);
            Assert.Equal(string.Empty, fs.ToString());
        }

        #endregion

        #region BitSpan Tests

        [Fact]
        public void BitSpan_SetClearToggle_ManipulatesBitsAccurately()
        {
            Span<byte> buffer = stackalloc byte[4]; // 32 bits
            var bitSpan = new BitSpan(buffer);

            Assert.Equal(32, bitSpan.LengthBits);
            Assert.Equal(4, bitSpan.LengthBytes);

            bitSpan.Set(0);
            bitSpan.Set(7);
            bitSpan.Set(15);
            bitSpan.Set(31);

            Assert.True(bitSpan.IsSet(0));
            Assert.True(bitSpan.IsSet(7));
            Assert.True(bitSpan.IsSet(15));
            Assert.True(bitSpan.IsSet(31));
            Assert.False(bitSpan.IsSet(1));
            Assert.False(bitSpan.IsSet(14));

            Assert.Equal(4, bitSpan.CountSetBits());

            bitSpan.Clear(7);
            Assert.False(bitSpan.IsSet(7));
            Assert.Equal(3, bitSpan.CountSetBits());

            bitSpan.Toggle(7);
            Assert.True(bitSpan.IsSet(7));
            Assert.Equal(4, bitSpan.CountSetBits());
        }

        [Fact]
        public void BitSpan_FindNextSetBit_ScansCorrectly()
        {
            Span<byte> buffer = stackalloc byte[4];
            var bitSpan = new BitSpan(buffer);

            bitSpan.Set(5);
            bitSpan.Set(18);

            Assert.Equal(5, bitSpan.FindNextSetBit(0));
            Assert.Equal(5, bitSpan.FindNextSetBit(5));
            Assert.Equal(18, bitSpan.FindNextSetBit(6));
            Assert.Equal(-1, bitSpan.FindNextSetBit(19));
        }

        [Fact]
        public void BitSpan_SetAllAndClearAll_FunctionsProperly()
        {
            Span<byte> buffer = stackalloc byte[8];
            var bitSpan = new BitSpan(buffer);

            bitSpan.SetAll();
            Assert.Equal(64, bitSpan.CountSetBits());

            bitSpan.ClearAll();
            Assert.Equal(0, bitSpan.CountSetBits());
        }

        #endregion
    }
}
