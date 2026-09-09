using System;
using Xunit;
using ZeroPrimitives.Validation;

namespace ZeroPrimitives.Tests
{
    public class VnMasterDataValidatorsTests
    {
        [Fact]
        public void TestValidTaxCodes()
        {
            // Viettel MST
            Assert.True(VnMasterDataValidators.IsValidMst("0100109106".AsSpan()));

            // Vinamilk MST
            Assert.True(VnMasterDataValidators.IsValidMst("0300588569".AsSpan()));

            // Viettel Branch 001
            Assert.True(VnMasterDataValidators.IsValidMst("0100109106-001".AsSpan()));
            Assert.True(VnMasterDataValidators.IsValidMst("0100109106001".AsSpan()));
        }

        [Fact]
        public void TestInvalidTaxCodes()
        {
            // Wrong check digit
            Assert.False(VnMasterDataValidators.IsValidMst("0100109105".AsSpan()));

            // Invalid length
            Assert.False(VnMasterDataValidators.IsValidMst("01001091".AsSpan()));

            // Invalid characters
            Assert.False(VnMasterDataValidators.IsValidMst("010010910A".AsSpan()));

            // Invalid branch code 000
            Assert.False(VnMasterDataValidators.IsValidMst("0100109106-000".AsSpan()));
        }

        [Fact]
        public void TestMstNormalization()
        {
            Span<char> buffer = stackalloc char[20];

            bool ok10 = VnMasterDataValidators.TryNormalizeMst("0100109106".AsSpan(), buffer, out int written10);
            Assert.True(ok10);
            Assert.Equal("0100109106", new string(buffer.Slice(0, written10).ToArray()));

            bool ok13 = VnMasterDataValidators.TryNormalizeMst("0100109106001".AsSpan(), buffer, out int written13);
            Assert.True(ok13);
            Assert.Equal("0100109106-001", new string(buffer.Slice(0, written13).ToArray()));
        }

        [Fact]
        public void TestCccdValidationAndMetadataDecoding()
        {
            // HCM City (079), Male born 1995 (0), Seq 012345
            bool ok1 = VnMasterDataValidators.IsValidCccd(
                "079095012345".AsSpan(),
                out int birthYear1,
                out bool isMale1,
                out string? prov1);

            Assert.True(ok1);
            Assert.Equal(1995, birthYear1);
            Assert.True(isMale1);
            Assert.Equal("TP. Hồ Chí Minh", prov1);

            // Hanoi (001), Female born 2003 (3), Seq 000888
            bool ok2 = VnMasterDataValidators.IsValidCccd(
                "001303000888".AsSpan(),
                out int birthYear2,
                out bool isMale2,
                out string? prov2);

            Assert.True(ok2);
            Assert.Equal(2003, birthYear2);
            Assert.False(isMale2);
            Assert.Equal("Hà Nội", prov2);

            // Invalid province (999)
            Assert.False(VnMasterDataValidators.IsValidCccd("999095012345".AsSpan(), out _, out _, out _));
        }

        [Fact]
        public void TestPhoneNormalization()
        {
            Assert.True(VnMasterDataValidators.IsValidPhone("0901234567".AsSpan()));
            Assert.True(VnMasterDataValidators.IsValidPhone("+84901234567".AsSpan()));
            Assert.True(VnMasterDataValidators.IsValidPhone("090.123.4567".AsSpan()));
            Assert.True(VnMasterDataValidators.IsValidPhone("(090) 123-4567".AsSpan()));

            // Normalize to local 0901234567
            string? local = VnMasterDataValidators.NormalizePhone("+84 90 123 4567", international: false);
            Assert.Equal("0901234567", local);

            // Normalize to international +84901234567
            string? intl = VnMasterDataValidators.NormalizePhone("090.123.4567", international: true);
            Assert.Equal("+84901234567", intl);

            // Obsolete 11-digit prefix or invalid mobile prefix
            Assert.False(VnMasterDataValidators.IsValidPhone("0123456789".AsSpan()));
            Assert.Null(VnMasterDataValidators.NormalizePhone("12345"));
        }
    }
}
