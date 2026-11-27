using System;
using System.Text;
using Xunit;
using ZeroPrimitives.Cryptography;

namespace ZeroPrimitives.Tests
{
    public class FastHashTests
    {
        [Fact]
        public void Fnv1a32_And_Fnv1a64_ProduceConsistentHashes()
        {
            byte[] data = Encoding.UTF8.GetBytes("ZeroPlatform.Primitives");
            uint h32_1 = FastHash.Fnv1a32(data);
            uint h32_2 = FastHash.Fnv1a32(data);
            Assert.Equal(h32_1, h32_2);
            Assert.NotEqual(0u, h32_1);

            ulong h64_1 = FastHash.Fnv1a64(data);
            ulong h64_2 = FastHash.Fnv1a64(data);
            Assert.Equal(h64_1, h64_2);
            Assert.NotEqual(0ul, h64_1);

            // Chars overload
            ulong h64_chars = FastHash.Fnv1a64("ZeroPlatform.Primitives".AsSpan());
            Assert.NotEqual(0ul, h64_chars);
        }

        [Fact]
        public void Md5Hex_MatchesKnownVector()
        {
            // MD5("hello") = 5d41402abc4b2a76b9719d911017c592
            string hash = FastHash.Md5Hex("hello");
            Assert.Equal("5D41402ABC4B2A76B9719D911017C592", hash);
        }

        [Fact]
        public void Sha256Hex_MatchesKnownVector()
        {
            // SHA256("hello") = 2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824
            string hash = FastHash.Sha256Hex("hello");
            Assert.Equal("2CF24DBA5FB0A30E26E83B2AC5B9E29E1B161E5C1FA7425E73043362938B9824", hash);
        }

        [Fact]
        public void Sha1Hex_MatchesKnownVector()
        {
            // SHA1("hello") = aaf4c61ddcc5e8a2dabede0f3b482cd9aea9434d
            string hash = FastHash.Sha1Hex("hello");
            Assert.Equal("AAF4C61DDCC5E8A2DABEDE0F3B482CD9AEA9434D", hash);
        }

        [Fact]
        public void CryptographicHex_LargePooledInputs_MatchBclOutputs()
        {
            string large = new string('x', 2000);
            byte[] utf8 = Encoding.UTF8.GetBytes(large);

            using var sha256 = System.Security.Cryptography.SHA256.Create();
            string expectedSha256 = BitConverter.ToString(sha256.ComputeHash(utf8)).Replace("-", "");
            Assert.Equal(expectedSha256, FastHash.Sha256Hex(large));

            using var md5 = System.Security.Cryptography.MD5.Create();
            string expectedMd5 = BitConverter.ToString(md5.ComputeHash(utf8)).Replace("-", "");
            Assert.Equal(expectedMd5, FastHash.Md5Hex(large));

            using var sha1 = System.Security.Cryptography.SHA1.Create();
            string expectedSha1 = BitConverter.ToString(sha1.ComputeHash(utf8)).Replace("-", "");
            Assert.Equal(expectedSha1, FastHash.Sha1Hex(large));
        }
    }
}
