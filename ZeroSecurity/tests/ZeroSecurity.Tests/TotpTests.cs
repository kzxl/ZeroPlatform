using System;
using System.Text;
using Xunit;
using ZeroSecurity.Tokens;

namespace ZeroSecurity.Tests
{
    public class TotpTests
    {
        // RFC 4226 Appendix D official test vectors:
        // Secret = "12345678901234567890" (ASCII)
        private static readonly byte[] Rfc4226Secret = Encoding.ASCII.GetBytes("12345678901234567890");

        [Theory]
        [InlineData(0, "755224")]
        [InlineData(1, "287082")]
        [InlineData(2, "359152")]
        [InlineData(3, "969429")]
        [InlineData(4, "338314")]
        [InlineData(5, "254676")]
        [InlineData(6, "287922")]
        [InlineData(7, "162583")]
        [InlineData(8, "399871")]
        [InlineData(9, "520489")]
        public void Hotp_MatchesRfc4226TestVectors(long counter, string expectedOtp)
        {
            Span<char> buffer = stackalloc char[6];
            bool success = Totp.TryGenerateHotp(Rfc4226Secret, counter, buffer, out int written, digits: 6, algorithm: TotpHashAlgorithm.Sha1);

            Assert.True(success);
            Assert.Equal(6, written);
            Assert.Equal(expectedOtp, buffer.ToString());
        }

        [Fact]
        public void Totp_GeneratesAndVerifiesSuccessfully()
        {
            byte[] secret = Encoding.ASCII.GetBytes("MySuperSecretKeyForTestingTotp!!");
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            string code = Totp.GenerateTotp(secret, unixTimeSeconds: now, digits: 6);
            Assert.Equal(6, code.Length);

            // Valid at current time
            Assert.True(Totp.VerifyTotp(secret, code.AsSpan(), window: 1, unixTimeSeconds: now, digits: 6));

            // Valid within 1 step (30 seconds in past or future)
            Assert.True(Totp.VerifyTotp(secret, code.AsSpan(), window: 1, unixTimeSeconds: now + 25, digits: 6));
            Assert.True(Totp.VerifyTotp(secret, code.AsSpan(), window: 1, unixTimeSeconds: now - 25, digits: 6));

            // Invalid when outside tolerance window (> 60 seconds)
            Assert.False(Totp.VerifyTotp(secret, code.AsSpan(), window: 1, unixTimeSeconds: now + 75, digits: 6));
            Assert.False(Totp.VerifyTotp(secret, code.AsSpan(), window: 1, unixTimeSeconds: now - 75, digits: 6));

            // Invalid with wrong code
            Assert.False(Totp.VerifyTotp(secret, "000000".AsSpan(), window: 1, unixTimeSeconds: now, digits: 6));
        }

        [Fact]
        public void Totp_Sha256Algorithm_GeneratesValidCodes()
        {
            byte[] secret = Encoding.ASCII.GetBytes("Sha256SecretKeyExampleForAuth!!");
            long now = 1700000000;

            string codeSha256 = Totp.GenerateTotp(secret, unixTimeSeconds: now, digits: 8, algorithm: TotpHashAlgorithm.Sha256);
            Assert.Equal(8, codeSha256.Length);

            Assert.True(Totp.VerifyTotp(secret, codeSha256.AsSpan(), window: 0, unixTimeSeconds: now, digits: 8, algorithm: TotpHashAlgorithm.Sha256));
        }

        [Theory]
        [InlineData("MY======", "f")]
        [InlineData("MZXQ====", "fo")]
        [InlineData("MZXW6===", "foo")]
        [InlineData("MZXW6YQ=", "foob")]
        [InlineData("MZXW6YTB", "fooba")]
        [InlineData("MZXW6YTBOI======", "foobar")]
        public void Base32_MatchesRfc4648TestVectors(string base32Input, string expectedAscii)
        {
            byte[] decoded = Base32.Decode(base32Input.AsSpan());
            string result = Encoding.ASCII.GetString(decoded);
            Assert.Equal(expectedAscii, result);
        }

        [Fact]
        public void Base32_HandlesSpacesAndCaseInsensitivity()
        {
            // Typical authenticator format: "JBSW Y3DP EHPK 3PXP"
            string formatted = "jbsw-y3dp ehpk 3pxp";
            byte[] decoded = Base32.Decode(formatted.AsSpan());

            Assert.True(decoded.Length > 0);
        }
    }
}
