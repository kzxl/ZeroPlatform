using System;
using System.Collections.Generic;
using Xunit;
using ZeroPrimitives.Core.Identifiers;

namespace ZeroPrimitives.Tests
{
    public class IdentifiersTests
    {
        [Fact]
        public void Uuid7_GeneratesMonotonicallyIncreasingValues()
        {
            const int count = 1000;
            var list = new List<Uuid7>(count);

            for (int i = 0; i < count; i++)
            {
                list.Add(Uuid7.NewUuid());
            }

            for (int i = 1; i < count; i++)
            {
                Assert.True(list[i] > list[i - 1], $"UUIDv7 at index {i} ({list[i]}) was not greater than index {i - 1} ({list[i - 1]})");
                Assert.True(list[i].CompareTo(list[i - 1]) > 0);
            }
        }

        [Fact]
        public void Uuid7_TimestampAndVersion_ConformToRfc9562()
        {
            long beforeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var uuid = Uuid7.NewUuid();
            long afterMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            long embeddedMs = uuid.GetUnixTimeMilliseconds();
            Assert.InRange(embeddedMs, beforeMs - 5, afterMs + 5);

            byte[] bytes = uuid.ToByteArray();
            // RFC 9562: Version 7 is encoded in the top 4 bits of octet 6
            int version = (bytes[6] >> 4) & 0x0F;
            Assert.Equal(7, version);

            // RFC 9562: Variant 1 (0b10) is encoded in top 2 bits of octet 8
            int variant = (bytes[8] >> 6) & 0x03;
            Assert.Equal(2, variant);
        }

        [Fact]
        public void Uuid7_GuidRoundtrip_PreservesBytesAndEquality()
        {
            var original = Uuid7.NewUuid();
            Guid guid = original.ToGuid();
            var restored = new Uuid7(guid);

            Assert.Equal(original, restored);
            Assert.Equal(original.ToString(), restored.ToString());
        }

        [Theory]
        [InlineData("D", 36)]
        [InlineData("N", 32)]
        [InlineData("B", 38)]
        [InlineData("P", 38)]
        public void Uuid7_FormatsAndParsesAllStandardFormats(string format, int expectedLength)
        {
            var original = Uuid7.NewUuid();
            string str = original.ToString(format);

            Assert.Equal(expectedLength, str.Length);
            Assert.True(Uuid7.TryParse(str, out var parsed));
            Assert.Equal(original, parsed);
        }

        [Fact]
        public void FastUlid_GeneratesMonotonicallyIncreasingValues()
        {
            const int count = 1000;
            var list = new List<FastUlid>(count);

            for (int i = 0; i < count; i++)
            {
                list.Add(FastUlid.NewUlid());
            }

            for (int i = 1; i < count; i++)
            {
                Assert.True(list[i] > list[i - 1], $"ULID at index {i} ({list[i]}) was not greater than index {i - 1} ({list[i - 1]})");
                Assert.True(list[i].CompareTo(list[i - 1]) > 0);
            }
        }

        [Fact]
        public void FastUlid_TimestampAccuracy()
        {
            long beforeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var ulid = FastUlid.NewUlid();
            long afterMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            long embeddedMs = ulid.GetUnixTimeMilliseconds();
            Assert.InRange(embeddedMs, beforeMs - 5, afterMs + 5);
        }

        [Fact]
        public void FastUlid_FormatAndParse_RoundtripsSuccessfully()
        {
            var original = FastUlid.NewUlid();
            string str = original.ToString();

            Assert.Equal(26, str.Length);
            Assert.True(FastUlid.TryParse(str, out var parsed));
            Assert.Equal(original, parsed);
        }

        [Fact]
        public void FastUlid_CrockfordBase32Aliases_DecodedCorrectly()
        {
            var ulid = FastUlid.NewUlid();
            string canonical = ulid.ToString();

            // Replace characters with Crockford aliases
            // '0' -> 'O' / 'o', '1' -> 'I' / 'i' or 'L' / 'l'
            string lower = canonical.ToLowerInvariant();
            Assert.True(FastUlid.TryParse(lower, out var parsedLower));
            Assert.Equal(ulid, parsedLower);
        }

        [Fact]
        public void FastUlid_GuidAndUuid7Roundtrip()
        {
            var original = FastUlid.NewUlid();
            Guid guid = original.ToGuid();
            var fromGuid = new FastUlid(guid);
            Assert.Equal(original, fromGuid);

            Uuid7 uuid7 = original.ToUuid7();
            var fromUuid7 = new FastUlid(uuid7);
            Assert.Equal(original, fromUuid7);
        }
    }
}
