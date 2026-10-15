using System;
using System.Text;
using Xunit;
using ZeroSecurity.Anomaly;
using ZeroSecurity.Common;
using ZeroSecurity.Crypto;
using ZeroSecurity.Hashing;
using ZeroSecurity.Matching;

namespace ZeroSecurity.Tests;

public class SecurityTests
{
    [Fact]
    public void FastSha256_KnownVectors_MatchExpectations()
    {
        // Vector: "abc" -> ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad
        byte[] abc = Encoding.UTF8.GetBytes("abc");
        string hash = FastSha256.HashHex(abc);
        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", hash);

        // Vector: "" (empty) -> e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855
        string emptyHash = FastSha256.HashHex(ReadOnlySpan<byte>.Empty);
        Assert.Equal("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", emptyHash);
    }

    [Fact]
    public void Blake3_SingleChunkAndMultiChunk_ProducesConsistent32ByteHashes()
    {
        // Empty string
        string emptyHash = Blake3.HashHex(ReadOnlySpan<byte>.Empty);
        Assert.Equal(64, emptyHash.Length);

        // Single chunk (<1024 bytes)
        byte[] shortData = Encoding.UTF8.GetBytes("ZeroUniverse Sovereign Security Engine");
        string shortHash = Blake3.HashHex(shortData);
        Assert.Equal(64, shortHash.Length);
        Assert.NotEqual(emptyHash, shortHash);

        // Multi chunk (>1024 bytes)
        byte[] largeData = new byte[2500];
        for (int i = 0; i < largeData.Length; i++) largeData[i] = (byte)(i & 0xFF);
        string largeHash = Blake3.HashHex(largeData);
        Assert.Equal(64, largeHash.Length);
        Assert.NotEqual(shortHash, largeHash);
    }

    [Fact]
    public void SsdeepFuzzyHash_SimilarInputs_YieldsHighScore()
    {
        byte[] original = Encoding.UTF8.GetBytes(new string('A', 500) + "malicious_payload_1" + new string('B', 500));
        byte[] modified = Encoding.UTF8.GetBytes(new string('A', 500) + "malicious_payload_2" + new string('B', 500));
        byte[] different = Encoding.UTF8.GetBytes(new string('X', 1000));

        string hash1 = SsdeepFuzzyHash.Compute(original);
        string hash2 = SsdeepFuzzyHash.Compute(modified);
        string hash3 = SsdeepFuzzyHash.Compute(different);

        Assert.NotEmpty(hash1);
        Assert.NotEmpty(hash2);
        Assert.NotEmpty(hash3);

        int similarity = SsdeepFuzzyHash.Compare(hash1, hash2);
        int diffScore = SsdeepFuzzyHash.Compare(hash1, hash3);

        Assert.True(similarity > 50, $"Expected similarity > 50, got {similarity}");
        Assert.True(diffScore < similarity, $"Expected different score < similarity, got {diffScore} vs {similarity}");
    }

    [Fact]
    public void BloomFilter_AddAndContains_WorksWithSerialization()
    {
        var filter = new BloomFilter(1000, 0.01);
        filter.Add("malware_hash_1");
        filter.Add("malware_hash_2");
        filter.Add("c2.evil-domain.com");

        Assert.True(filter.Contains("malware_hash_1"));
        Assert.True(filter.Contains("malware_hash_2"));
        Assert.True(filter.Contains("c2.evil-domain.com"));
        Assert.False(filter.Contains("benign_application.exe"));

        // Test serialization roundtrip
        byte[] bytes = filter.ToByteArray();
        var restored = BloomFilter.FromByteArray(bytes);

        Assert.True(restored.Contains("malware_hash_1"));
        Assert.True(restored.Contains("c2.evil-domain.com"));
        Assert.False(restored.Contains("benign_application.exe"));
    }

    [Fact]
    public void AhoCorasick_MultiPattern_FindsKeywordsSimultaneously()
    {
        var patterns = new[] { "powershell", "-enc", "mimikatz", "cmd.exe" };
        var matcher = new AhoCorasick(patterns);

        string commandLine = "C:\\Windows\\System32\\cmd.exe /c powershell.exe -enc aW1wb3J0...";
        var matches = matcher.Search(commandLine);

        Assert.Contains(matches, m => m.Keyword == "cmd.exe");
        Assert.Contains(matches, m => m.Keyword == "powershell");
        Assert.Contains(matches, m => m.Keyword == "-enc");
        Assert.DoesNotContain(matches, m => m.Keyword == "mimikatz");
    }

    [Fact]
    public void RadixIpTrie_SubnetLookup_ReturnsCorrectThreatLabel()
    {
        var trie = new RadixIpTrie();
        trie.AddCidr("185.220.101.0/24", "Tor Exit Node");
        trie.AddCidr("10.0.0.0/8", "Internal RFC1918");
        trie.AddCidr("192.168.1.100/32", "Compromised Host");

        Assert.True(trie.TryMatch("185.220.101.45", out var tag1));
        Assert.Equal("Tor Exit Node", tag1);

        Assert.True(trie.TryMatch("10.20.30.40", out var tag2));
        Assert.Equal("Internal RFC1918", tag2);

        Assert.True(trie.TryMatch("192.168.1.100", out var tag3));
        Assert.Equal("Compromised Host", tag3);

        Assert.False(trie.TryMatch("8.8.8.8", out _));
    }

    [Fact]
    public void ShannonEntropy_DetectsRandomOrEncryptedData()
    {
        // 1. Uniform identical data -> 0 entropy
        byte[] uniform = new byte[1000];
        double e1 = ShannonEntropy.Calculate(uniform);
        Assert.Equal(0.0, e1, 3);
        Assert.False(ShannonEntropy.IsLikelyEncryptedOrPacked(uniform));

        // 2. Encrypted / Pseudo-random data -> High entropy (> 7.5)
        byte[] randomData = new byte[1000];
        var rng = new Random(42);
        rng.NextBytes(randomData);
        double e2 = ShannonEntropy.Calculate(randomData);
        Assert.True(e2 > 7.5, $"Expected entropy > 7.5, got {e2}");
        Assert.True(ShannonEntropy.IsLikelyEncryptedOrPacked(randomData));
    }

    [Fact]
    public void WelfordStats_OnlineZScore_DetectsOutliers()
    {
        var stats = new WelfordStats();

        // Baseline: normal distribution around 10.0
        for (int i = 0; i < 50; i++)
        {
            stats.Update(10.0 + (i % 5) * 0.1);
        }

        Assert.True(stats.Mean >= 9.9 && stats.Mean <= 10.3);
        Assert.False(stats.IsAnomaly(10.2));

        // Sudden massive anomaly (e.g. process spawned 1000 child processes or burst outbound traffic)
        Assert.True(stats.IsAnomaly(100.0, zThreshold: 3.0));
    }

    [Fact]
    public void ChaCha20Poly1305_EncryptDecryptRoundtrip_SucceedsAndDetectsTampering()
    {
        byte[] key = new byte[32];
        byte[] nonce = new byte[12];
        byte[] aad = Encoding.UTF8.GetBytes("SystemMetadata");
        byte[] plaintext = Encoding.UTF8.GetBytes("TopSecretQuarantineExecutablePayloadContent");

        for (int i = 0; i < 32; i++) key[i] = (byte)(i + 1);
        for (int i = 0; i < 12; i++) nonce[i] = (byte)(i + 10);

        byte[] ciphertext = new byte[plaintext.Length];
        byte[] tag = new byte[16];

        // 1. Encrypt
        ChaCha20Poly1305.Encrypt(key, nonce, aad, plaintext, ciphertext, tag);
        Assert.NotEqual(plaintext, ciphertext);

        // 2. Decrypt & Verify
        byte[] decrypted = new byte[plaintext.Length];
        bool ok = ChaCha20Poly1305.Decrypt(key, nonce, aad, ciphertext, tag, decrypted);
        Assert.True(ok);
        Assert.Equal(plaintext, decrypted);

        // 3. Tamper with ciphertext -> verification must fail
        ciphertext[0] ^= 0xFF;
        byte[] failBuffer = new byte[plaintext.Length];
        bool tamperOk = ChaCha20Poly1305.Decrypt(key, nonce, aad, ciphertext, tag, failBuffer);
        Assert.False(tamperOk);
    }

    [Fact]
    public void CryptoMemory_SecureZeroAndConstantTimeEquals_BehavesCorrectly()
    {
        byte[] buffer = { 1, 2, 3, 4, 5, 6, 7, 8 };
        CryptoMemory.SecureZero(buffer);
        Assert.All(buffer, b => Assert.Equal(0, b));

        byte[] a = { 10, 20, 30, 40 };
        byte[] b = { 10, 20, 30, 40 };
        byte[] c = { 10, 20, 30, 41 };

        Assert.True(CryptoMemory.ConstantTimeEquals(a, b));
        Assert.False(CryptoMemory.ConstantTimeEquals(a, c));
    }

    [Fact]
    public void HmacSha256_Rfc4231_KnownTestVectors()
    {
        // RFC 4231 Test Case 1:
        // Key: 0x0b repeated 20 times
        // Data: "Hi There"
        // Expected: b0344c61d8db38535ca8afceaf0bf12b881dc200c9833da726e9376c2e32cff7
        byte[] key1 = new byte[20];
        for (int i = 0; i < key1.Length; i++) key1[i] = 0x0b;
        byte[] data1 = Encoding.UTF8.GetBytes("Hi There");
        string hmac1 = HmacSha256.HashHex(key1, data1);
        Assert.Equal("b0344c61d8db38535ca8afceaf0bf12b881dc200c9833da726e9376c2e32cff7", hmac1);

        // RFC 4231 Test Case 2:
        // Key: "Jefe"
        // Data: "what do ya want for nothing?"
        // Expected: 5bdcc146bf60754e6a042426089575c75a003f089d2739839dec58b964ec3843
        byte[] key2 = Encoding.UTF8.GetBytes("Jefe");
        byte[] data2 = Encoding.UTF8.GetBytes("what do ya want for nothing?");
        string hmac2 = HmacSha256.HashHex(key2, data2);
        Assert.Equal("5bdcc146bf60754e6a042426089575c75a003f089d2739839dec58b964ec3843", hmac2);

        // RFC 4231 Test Case 6 (Key > 64 bytes):
        // Key: 0xaa repeated 131 times
        // Data: "Test Using Larger Than Block-Size Key - Hash Key First"
        // Expected: 60e431591ee0b67f0d8a26aacbf5b77f8e0bc621372b0713fedad64e01f930ac
        byte[] key6 = new byte[131];
        for (int i = 0; i < key6.Length; i++) key6[i] = 0xaa;
        byte[] data6 = Encoding.UTF8.GetBytes("Test Using Larger Than Block-Size Key - Hash Key First");
        string hmac6 = HmacSha256.HashHex(key6, data6);
        using var bcl6 = new System.Security.Cryptography.HMACSHA256(key6);
        string expected6 = FastHex.ToHex(bcl6.ComputeHash(data6));
        Assert.Equal(expected6, hmac6);
    }

    [Fact]
    public void Hkdf_Rfc5869_TestVectors()
    {
        // RFC 5869 Test Case 1:
        // IKM = 0x0b repeated 22 times
        // Salt = 0x000102030405060708090a0b0c (13 bytes)
        // Info = 0xf0f1f2f3f4f5f6f7f8f9 (10 bytes)
        // L = 42 bytes
        byte[] ikm = new byte[22];
        for (int i = 0; i < ikm.Length; i++) ikm[i] = 0x0b;

        byte[] salt = { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0a, 0x0b, 0x0c };
        byte[] info = { 0xf0, 0xf1, 0xf2, 0xf3, 0xf4, 0xf5, 0xf6, 0xf7, 0xf8, 0xf9 };

        Span<byte> prk = stackalloc byte[32];
        Hkdf.Extract(salt, ikm, prk);
        Assert.Equal("077709362c2e32df0ddc3f0dc47bba6390b6c73bb50f9c3122ec844ad7c2b3e5", FastHex.ToHex(prk));

        Span<byte> okm = stackalloc byte[42];
        Hkdf.Expand(prk, info, okm);
        byte[] bclOkm = System.Security.Cryptography.HKDF.Expand(System.Security.Cryptography.HashAlgorithmName.SHA256, prk.ToArray(), 42, info);
        Assert.Equal(FastHex.ToHex(bclOkm), FastHex.ToHex(okm));

        // One-step DeriveKey
        Span<byte> derived = stackalloc byte[42];
        Hkdf.DeriveKey(salt, ikm, info, derived);
        Assert.Equal(FastHex.ToHex(bclOkm), FastHex.ToHex(derived));
    }

    [Fact]
    public void Pbkdf2_MatchesStandardBclImplementation()
    {
        byte[] salt = Encoding.UTF8.GetBytes("salt");

        // 1 iteration
        Span<byte> dk1 = stackalloc byte[32];
        Pbkdf2.DeriveKey("password", salt, 1, dk1);
        byte[] bcl1 = System.Security.Cryptography.Rfc2898DeriveBytes.Pbkdf2("password", salt, 1, System.Security.Cryptography.HashAlgorithmName.SHA256, 32);
        Assert.Equal(FastHex.ToHex(bcl1), FastHex.ToHex(dk1));

        // 2 iterations
        Span<byte> dk2 = stackalloc byte[32];
        Pbkdf2.DeriveKey("password", salt, 2, dk2);
        byte[] bcl2 = System.Security.Cryptography.Rfc2898DeriveBytes.Pbkdf2("password", salt, 2, System.Security.Cryptography.HashAlgorithmName.SHA256, 32);
        Assert.Equal(FastHex.ToHex(bcl2), FastHex.ToHex(dk2));

        // 4096 iterations
        Span<byte> dk4096 = stackalloc byte[32];
        Pbkdf2.DeriveKey("password", salt, 4096, dk4096);
        byte[] bcl4096 = System.Security.Cryptography.Rfc2898DeriveBytes.Pbkdf2("password", salt, 4096, System.Security.Cryptography.HashAlgorithmName.SHA256, 32);
        Assert.Equal(FastHex.ToHex(bcl4096), FastHex.ToHex(dk4096));
    }

    [Fact]
    public void X25519_Rfc7748_TestVectors_AndSharedSecretAgreement()
    {
        // RFC 7748 Section 6.1:
        // Alice private key:
        byte[] alicePriv = new byte[32];
        FastHex.TryDecode("77076d0a7318a57d3c16c17251b26645df4c2f87ebc0992ab177fba51db92c2a".AsSpan(), alicePriv, out _);

        Span<byte> alicePub = stackalloc byte[32];
        X25519.GetPublicKey(alicePriv, alicePub);
        Assert.Equal("8520f0098930a754748b7ddcb43ef75a0dbf3a0d26381af4eba4a98eaa9b4e6a", FastHex.ToHex(alicePub));

        // Bob private key:
        byte[] bobPriv = new byte[32];
        FastHex.TryDecode("5dab087e624a8a4b79e17f8b83800ee66f3bb1292618b6fd1c2f8b27ff88e0eb".AsSpan(), bobPriv, out _);

        Span<byte> bobPub = stackalloc byte[32];
        X25519.GetPublicKey(bobPriv, bobPub);
        Assert.Equal("de9edb7d7b7dc1b4d35b61c2ece435373f8343c85b78674dadfc7e146f882b4f", FastHex.ToHex(bobPub));

        // Mutual Key Agreement:
        Span<byte> sharedAlice = stackalloc byte[32];
        Span<byte> sharedBob = stackalloc byte[32];

        X25519.Agree(alicePriv, bobPub, sharedAlice);
        X25519.Agree(bobPriv, alicePub, sharedBob);

        string expectedShared = "4a5d9d5ba4ce2de1728e3bf480350f25e07e21c947d19e3376f09b3c1e161742";
        Assert.Equal(expectedShared, FastHex.ToHex(sharedAlice));
        Assert.Equal(expectedShared, FastHex.ToHex(sharedBob));
    }

    [Fact]
    public void XChaCha20Poly1305_EncryptDecryptAndTamperResistance()
    {
        byte[] key = new byte[32];
        byte[] nonce = new byte[24]; // 192-bit extended nonce
        byte[] aad = Encoding.UTF8.GetBytes("IndustrialTelemetryHeader");
        byte[] plaintext = Encoding.UTF8.GetBytes("CriticalEdgeControllerCommandPayload");

        for (int i = 0; i < key.Length; i++) key[i] = (byte)(i + 7);
        for (int i = 0; i < nonce.Length; i++) nonce[i] = (byte)(i + 13);

        byte[] ciphertext = new byte[plaintext.Length];
        byte[] tag = new byte[16];

        // 1. Encrypt
        XChaCha20Poly1305.Encrypt(key, nonce, aad, plaintext, ciphertext, tag);
        Assert.NotEqual(plaintext, ciphertext);

        // 2. Decrypt
        byte[] decrypted = new byte[plaintext.Length];
        bool ok = XChaCha20Poly1305.Decrypt(key, nonce, aad, ciphertext, tag, decrypted);
        Assert.True(ok);
        Assert.Equal(plaintext, decrypted);

        // 3. Tamper with ciphertext
        ciphertext[5] ^= 0x42;
        byte[] failBuffer = new byte[plaintext.Length];
        bool tamperOk = XChaCha20Poly1305.Decrypt(key, nonce, aad, ciphertext, tag, failBuffer);
        Assert.False(tamperOk);
    }

    [Fact]
    public void CuckooFilter_AddContainsDeleteAndSerialization()
    {
        var filter = new CuckooFilter(expectedElements: 5000);

        filter.Add("c2.evil-attacker.org");
        filter.Add("ransomware.exe.sha256");
        filter.Add("192.168.1.50");

        Assert.True(filter.Contains("c2.evil-attacker.org"));
        Assert.True(filter.Contains("ransomware.exe.sha256"));
        Assert.True(filter.Contains("192.168.1.50"));
        Assert.False(filter.Contains("legitimate-host.net"));

        // Delete test
        bool deleted = filter.Delete("ransomware.exe.sha256");
        Assert.True(deleted);
        Assert.False(filter.Contains("ransomware.exe.sha256"));
        Assert.True(filter.Contains("c2.evil-attacker.org")); // other entries untouched

        // Serialization roundtrip
        byte[] serialized = filter.ToByteArray();
        var restored = CuckooFilter.FromByteArray(serialized);

        Assert.True(restored.Contains("c2.evil-attacker.org"));
        Assert.True(restored.Contains("192.168.1.50"));
        Assert.False(restored.Contains("ransomware.exe.sha256"));
        Assert.False(restored.Contains("legitimate-host.net"));
    }

    [Fact]
    public void SecureMemoryScope_AllocateAndZeroFilled_AutoWipesOnDispose()
    {
        SecureMemoryScope scope = new SecureMemoryScope(64);
        Assert.Equal(64, scope.Length);
        Assert.False(scope.IsDisposed);
        unsafe
        {
            Assert.True(scope.Pointer != null);
        }

        // Must be zero-filled on creation
        for (int i = 0; i < scope.Length; i++)
        {
            Assert.Equal(0, scope.Span[i]);
        }

        // Fill with secret
        scope.Span.Fill(0xAA);
        Assert.Equal(0xAA, scope.Span[0]);

        // Dispose wipes and releases
        scope.Dispose();
        Assert.True(scope.IsDisposed);

        // Operations throw ObjectDisposedException
        Assert.Throws<ObjectDisposedException>(() => scope.Wipe());
        Assert.Throws<ObjectDisposedException>(() => scope.ConstantTimeEquals(ReadOnlySpan<byte>.Empty));

        // Double dispose is completely safe
        scope.Dispose();
    }

    [Fact]
    public void SecureMemoryScope_FromBytes_CopiesAndPreservesSecret()
    {
        byte[] secret = [0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08];
        using var scope = SecureMemoryScope.FromBytes(secret);

        Assert.Equal(8, scope.Length);
        Assert.True(scope.Span.SequenceEqual(secret));
        Assert.True(scope.ConstantTimeEquals(secret));

        byte[] different = [0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x09];
        Assert.False(scope.ConstantTimeEquals(different));

        using var otherScope = SecureMemoryScope.FromBytes(secret);
        Assert.True(scope.ConstantTimeEquals(otherScope));
    }

    [Fact]
    public void SecureMemoryScope_FromChars_EncodesDirectlyWithoutManagedHeapLeak()
    {
        string secretText = "SovereignSecuritySecret123!";
        using var scope = SecureMemoryScope.FromChars(secretText.AsSpan());

        byte[] expectedBytes = Encoding.UTF8.GetBytes(secretText);
        Assert.Equal(expectedBytes.Length, scope.Length);
        Assert.True(scope.Span.SequenceEqual(expectedBytes));

        // Test Wipe()
        scope.Wipe();
        for (int i = 0; i < scope.Length; i++)
        {
            Assert.Equal(0, scope.Span[i]);
        }
    }

    [Fact]
    public void SecureMemoryScope_FromHexString_DecodesAccurately()
    {
        string hex = "deadbeef01020304";
        using var scope = SecureMemoryScope.FromHexString(hex.AsSpan());

        Assert.Equal(8, scope.Length);
        byte[] expected = [0xde, 0xad, 0xbe, 0xef, 0x01, 0x02, 0x03, 0x04];
        Assert.True(scope.Span.SequenceEqual(expected));
    }

    [Fact]
    public void CryptoMemory_ConstantTimeEquals_CharsAndStrings()
    {
        string token1 = "Bearer-eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9";
        string token2 = "Bearer-eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9";
        string token3 = "Bearer-eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJx"; // 1 char diff at end
        string tokenShort = "Bearer-eyJhbG";

        Assert.True(CryptoMemory.ConstantTimeEquals(token1.AsSpan(), token2.AsSpan()));
        Assert.False(CryptoMemory.ConstantTimeEquals(token1.AsSpan(), token3.AsSpan()));
        Assert.False(CryptoMemory.ConstantTimeEquals(token1.AsSpan(), tokenShort.AsSpan()));

        Assert.True(CryptoMemory.ConstantTimeEquals(token1, token2));
        Assert.False(CryptoMemory.ConstantTimeEquals(token1, token3));
        Assert.False(CryptoMemory.ConstantTimeEquals(token1, (string?)null));
        Assert.False(CryptoMemory.ConstantTimeEquals((string?)null, token2));
        Assert.True(CryptoMemory.ConstantTimeEquals((string?)null, (string?)null));
    }

    [Fact]
    public void CryptoMemory_SecureZero_Chars()
    {
        char[] password = "UltraSensitiveAdminPassword".ToCharArray();
        Assert.NotEqual('\0', password[0]);

        CryptoMemory.SecureZero(password.AsSpan());

        for (int i = 0; i < password.Length; i++)
        {
            Assert.Equal('\0', password[i]);
        }
    }

    [Fact]
    public void X25519_Agree_RejectsLowOrderPoints_PerRfc7748()
    {
        byte[] privateKey = new byte[32];
        for (int i = 0; i < 32; i++) privateKey[i] = (byte)(i + 1);

        // All-zero low-order public key
        byte[] zeroPoint = new byte[32];
        byte[] sharedSecret = new byte[32];

        Assert.Throws<System.Security.Cryptography.CryptographicException>(() =>
            X25519.Agree(privateKey, zeroPoint, sharedSecret));

        // Order-2 point (1, 0, 0, ...) also produces all-zero shared secret after clamping
        byte[] order2Point = new byte[32];
        order2Point[0] = 1;
        Assert.Throws<System.Security.Cryptography.CryptographicException>(() =>
            X25519.Agree(privateKey, order2Point, sharedSecret));
    }

    [Fact]
    public void CuckooFilter_HighLoadEviction_UsesStatelessXorShiftAndSerializes()
    {
        var filter = new CuckooFilter(expectedElements: 128);
        int inserted = 0;
        for (int i = 0; i < 120; i++)
        {
            if (filter.Add($"ioc-threat-indicator-{i}"))
            {
                inserted++;
            }
        }

        Assert.True(inserted >= 100);
        Assert.Equal(inserted, filter.Count);

        byte[] bytes = filter.ToByteArray();
        var restored = CuckooFilter.FromByteArray(bytes.AsSpan());
        Assert.Equal(filter.Count, restored.Count);
        Assert.Equal(filter.Capacity, restored.Capacity);

        for (int i = 0; i < 20; i++)
        {
            Assert.True(restored.Contains($"ioc-threat-indicator-{i}"));
        }
    }

    [Fact]
    public void SecureMemoryScope_AttemptsOsPageLock_AndUnlocksOnDispose()
    {
        using var scope = new SecureMemoryScope(64);
        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
        {
            Assert.True(scope.IsPageLocked);
        }

        scope.Dispose();
        Assert.False(scope.IsPageLocked);
    }
}

