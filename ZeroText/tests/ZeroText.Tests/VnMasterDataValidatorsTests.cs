using System;
using Xunit;
using ZeroText.Validation;

namespace ZeroText.Tests
{
    public class VnMasterDataValidatorsTests
    {
        [Fact]
        public void ValidTaxCodes()
        {
            // Viettel MST
            Assert.True(VnMasterDataValidators.IsValidMst("0100109106".AsSpan()));
            Assert.True(VnMasterDataValidators.IsValidTaxCode("0100109106"));

            // Vinamilk MST
            Assert.True(VnMasterDataValidators.IsValidMst("0300588569".AsSpan()));
            Assert.True(VnMasterDataValidators.IsValidTaxCode("0300588569"));

            // Viettel Branch 001
            Assert.True(VnMasterDataValidators.IsValidMst("0100109106-001".AsSpan()));
            Assert.True(VnMasterDataValidators.IsValidMst("0100109106001".AsSpan()));
            Assert.True(VnMasterDataValidators.IsValidTaxCode("0100109106-001"));
        }

        [Fact]
        public void InvalidTaxCodes()
        {
            // Wrong check digit
            Assert.False(VnMasterDataValidators.IsValidMst("0100109105".AsSpan()));
            Assert.False(VnMasterDataValidators.IsValidTaxCode("0100109105"));

            // Invalid length
            Assert.False(VnMasterDataValidators.IsValidMst("01001091".AsSpan()));
            Assert.False(VnMasterDataValidators.IsValidTaxCode("01001091"));

            // Invalid characters
            Assert.False(VnMasterDataValidators.IsValidMst("010010910A".AsSpan()));
            Assert.False(VnMasterDataValidators.IsValidTaxCode("010010910A"));

            // Invalid branch code 000
            Assert.False(VnMasterDataValidators.IsValidMst("0100109106-000".AsSpan()));
            Assert.False(VnMasterDataValidators.IsValidTaxCode("0100109106-000"));

            // Null or empty
            Assert.False(VnMasterDataValidators.IsValidTaxCode(null));
            Assert.False(VnMasterDataValidators.IsValidTaxCode(""));
        }

        [Fact]
        public void MstNormalization()
        {
            Span<char> buffer = stackalloc char[20];

            bool ok10 = VnMasterDataValidators.TryNormalizeMst("0100109106".AsSpan(), buffer, out int written10);
            Assert.True(ok10);
            Assert.Equal("0100109106", new string(buffer.Slice(0, written10).ToArray()));

            bool ok13 = VnMasterDataValidators.TryNormalizeMst("0100109106001".AsSpan(), buffer, out int written13);
            Assert.True(ok13);
            Assert.Equal("0100109106-001", new string(buffer.Slice(0, written13).ToArray()));

            // String overload
            Assert.Equal("0100109106", VnMasterDataValidators.NormalizeMst("0100109106"));
            Assert.Equal("0100109106-001", VnMasterDataValidators.NormalizeMst("0100109106001"));
            Assert.Null(VnMasterDataValidators.NormalizeMst("invalid"));
        }

        [Fact]
        public void CccdValidationAndMetadataDecoding()
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

            // String overload
            Assert.True(VnMasterDataValidators.IsValidCccd("079095012345"));
            Assert.True(VnMasterDataValidators.IsValidCccd("001303000888", out int y, out bool m, out string? p));
            Assert.Equal(2003, y);
            Assert.False(m);
            Assert.Equal("Hà Nội", p);

            // Century century test:
            // 21st century (born 2105): gender digit 4 (male)
            Assert.True(VnMasterDataValidators.IsValidCccd("079405012345".AsSpan(), out int y21, out bool m21, out _));
            Assert.Equal(2105, y21);
            Assert.True(m21);

            // Invalid province (999)
            Assert.False(VnMasterDataValidators.IsValidCccd("999095012345".AsSpan(), out _, out _, out _));
            Assert.False(VnMasterDataValidators.IsValidCccd("999095012345"));

            // Invalid length
            Assert.False(VnMasterDataValidators.IsValidCccd("07909501234"));
            Assert.False(VnMasterDataValidators.IsValidCccd(null));
        }

        [Fact]
        public void PhoneNormalization()
        {
            Assert.True(VnMasterDataValidators.IsValidPhone("0901234567".AsSpan()));
            Assert.True(VnMasterDataValidators.IsValidPhone("+84901234567".AsSpan()));
            Assert.True(VnMasterDataValidators.IsValidPhone("090.123.4567".AsSpan()));
            Assert.True(VnMasterDataValidators.IsValidPhone("(090) 123-4567".AsSpan()));
            Assert.True(VnMasterDataValidators.IsValidPhone("0901234567"));

            // Normalize to local 0901234567
            string? local = VnMasterDataValidators.NormalizePhone("+84 90 123 4567", international: false);
            Assert.Equal("0901234567", local);

            // Normalize to international +84901234567
            string? intl = VnMasterDataValidators.NormalizePhone("090.123.4567", international: true);
            Assert.Equal("+84901234567", intl);

            // Prefixes: 03x, 05x, 07x, 08x, 09x
            Assert.True(VnMasterDataValidators.IsValidPhone("0321234567"));
            Assert.True(VnMasterDataValidators.IsValidPhone("0521234567"));
            Assert.True(VnMasterDataValidators.IsValidPhone("0701234567"));
            Assert.True(VnMasterDataValidators.IsValidPhone("0861234567"));

            // Obsolete 11-digit prefix or invalid mobile prefix
            Assert.False(VnMasterDataValidators.IsValidPhone("0123456789".AsSpan()));
            Assert.False(VnMasterDataValidators.IsValidPhone("0241234567")); // Landline not mobile
            Assert.Null(VnMasterDataValidators.NormalizePhone("12345"));
            Assert.Null(VnMasterDataValidators.NormalizePhone(null));
        }
    }
}
