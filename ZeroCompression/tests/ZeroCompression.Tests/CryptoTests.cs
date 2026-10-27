using System;
using System.IO;
using System.Text;
using Xunit;
using ZeroCompression.Core.Crypto;

namespace ZeroCompression.Tests
{
    public class CryptoTests
    {
        [Fact]
        public void Encrypt_Decrypt_Roundtrip_Works()
        {
            var plain = Encoding.UTF8.GetBytes("Confidential ZeroUniverse Telemetry Data!");
            string password = "StrongPassword@2026";

            using var encryptedMs = new MemoryStream();
            using (var enc = PayloadCrypto.CreateEncryptor(encryptedMs, password))
            {
                enc.Write(plain, 0, plain.Length);
            }

            byte[] cipherBytes = encryptedMs.ToArray();

            // Instant password verification check
            using var probeStream = new MemoryStream(cipherBytes);
            Assert.True(PayloadCrypto.CheckPassword(probeStream, password));
            Assert.False(PayloadCrypto.CheckPassword(probeStream, "WrongPassword"));

            // Decrypt
            using var decSource = new MemoryStream(cipherBytes);
            using var dec = PayloadCrypto.CreateDecryptor(decSource, password);
            using var decryptedMs = new MemoryStream();
            dec.CopyTo(decryptedMs);

            Assert.Equal(plain, decryptedMs.ToArray());
        }

        [Fact]
        public void Decrypt_WithWrongPassword_ThrowsInvalidDataException()
        {
            var plain = Encoding.UTF8.GetBytes("Secret payload");
            using var encryptedMs = new MemoryStream();
            using (var enc = PayloadCrypto.CreateEncryptor(encryptedMs, "CorrectPassword"))
            {
                enc.Write(plain, 0, plain.Length);
            }

            using var decSource = new MemoryStream(encryptedMs.ToArray());
            Assert.Throws<InvalidDataException>(() =>
            {
                using var dec = PayloadCrypto.CreateDecryptor(decSource, "WrongPassword");
                dec.ReadByte();
            });
        }
    }
}
