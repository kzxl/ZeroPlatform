# ZeroSecurity

[![ZeroPlatform Tier](https://img.shields.io/badge/ZeroPlatform-Tier%200%20(Core%20Foundation)-0284c7.svg)](https://github.com/kzxl/ZeroPlatform)
[![NuGet Version](https://img.shields.io/badge/nuget-v1.1.1-blue.svg)](https://www.nuget.org/packages/ZeroSecurity)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Zero External Dependencies](https://img.shields.io/badge/Dependencies-0%20External-brightgreen.svg)]()
[![Multi-Targeting](https://img.shields.io/badge/.NET-8.0%20%7C%204.6.2%20%7C%20Standard%202.0-orange.svg)]()
[![Tests: 22 Passed](https://img.shields.io/badge/Tests-22%20Passed%20(100%25)-brightgreen.svg)]()

> **Architectural Standard**: 100% Pure C#, Zero External Dependencies, Multi-Targeting across `.NET 8.0`, `.NET Framework 4.6.2`, and `.NET Standard 2.0`.

`ZeroSecurity` is the sovereign, high-throughput cryptographic and cyber-defense foundation of the **ZeroUniverse** ecosystem. It provides low-overhead, zero-allocation algorithms for asymmetric key agreement, authenticated symmetric encryption, key derivation, multi-pattern signature matching, IOC threat intelligence lookups, Shannon byte entropy, streaming UEBA anomaly mathematics, constant-time memory hygiene, and unmanaged secure credential lifetimes.

---

## 🏛️ Architecture & Capabilities

```
ZeroSecurity/
├── Common/
│   ├── FastHex.cs              # Zero-alloc span/pointer hex encoder and decoder
│   ├── CryptoMemory.cs         # Compiler-resistant SecureZero & branchless ConstantTimeEquals (byte, char, string)
│   └── SecureMemoryScope.cs    # Unmanaged secure credential buffer with guaranteed zeroization on Dispose
├── Crypto/
│   ├── X25519.cs               # Pure C# RFC 7748 Montgomery Curve25519 ECDH key exchange
│   ├── ChaCha20Poly1305.cs     # Pure C# RFC 8439 AEAD cipher with zero-alloc integer limb Poly1305
│   └── XChaCha20Poly1305.cs    # Pure C# 192-bit extended-nonce AEAD cipher via HChaCha20
├── Hashing/
│   ├── Blake3.cs               # Pure C# 256-bit BLAKE3 tree hash (zero-alloc on chunks <= 1024B)
│   ├── FastSha256.cs           # Pure managed FIPS 180-4 SHA-256 (one-shot & streaming Sha256Incremental)
│   ├── HmacSha256.cs           # Pure C# RFC 2104 Keyed-Hashing for Message Authentication
│   ├── Hkdf.cs                 # Pure C# RFC 5869 HMAC-based Extract-and-Expand Key Derivation
│   ├── Pbkdf2.cs               # Pure C# RFC 2898 / NIST SP 800-132 Password-Based Key Derivation
│   └── SsdeepFuzzyHash.cs      # Context-Triggered Piecewise Hashing (CTPH) malware similarity
├── Matching/
│   ├── CuckooFilter.cs         # Dynamic O(1) set membership supporting Insert, Contains & Delete
│   ├── BloomFilter.cs          # Double-hashing (Kirsch-Mitzenmacher) filter for millions of IOCs
│   ├── AhoCorasick.cs          # Multi-pattern string/byte matching in a single linear pass O(N + M)
│   └── RadixIpTrie.cs          # Sub-microsecond IPv4 CIDR prefix matching for C2/Tor blacklists
└── Anomaly/
    ├── ShannonEntropy.cs       # SIMD-vectorized entropy (0.0..8.0) for detecting Ransomware / Packers
    └── WelfordStats.cs         # Online streaming mean, variance & Z-score in O(1) memory for UEBA
```

---

## ⚡ Key Modules

### 1. Sovereign Asymmetric & Symmetric Cryptography
- **`X25519`**: RFC 7748 Montgomery ladder on Curve25519. Allows sovereign ZeroWire and industrial edge nodes to negotiate shared secrets in constant time without TLS certificates or external crypto libraries.
- **`XChaCha20Poly1305`**: 192-bit (24-byte) extended-nonce AEAD cipher. Eliminates birthday collisions, enabling safe stateless random nonces for quarantine vaults, disk persistence, and telemetry frames.
- **`ChaCha20Poly1305`**: RFC 8439 AEAD cipher powered by a high-speed, zero-allocation 26-bit limb Poly1305 engine (no `BigInteger` allocations).
- **`CryptoMemory`**: Guarantees zeroization of sensitive private keys via volatile non-optimizable writes (`SecureZero`), preventing memory scraping and dead-store compiler elimination.

### 2. Key Derivation & Message Authentication (KDF / MAC)
- **`HmacSha256`**: RFC 2104 zero-allocation keyed-hash message authentication code.
- **`Hkdf`**: RFC 5869 Extract-and-Expand key derivation for securely expanding ECDH shared secrets into independent encryption, authentication, and IV keys.
- **`Pbkdf2`**: RFC 2898 / NIST SP 800-132 password-based key derivation with HMAC-SHA256 for operator master credentials and sealed vault keys.

### 3. Dynamic IOC & Threat Matching
- **`CuckooFilter`**: Dynamic threat set filter supporting **Insert**, **Contains**, and **Delete** with compact 16-bit fingerprints, high load factors (> 85%), and full binary serialization.
- **`BloomFilter`**: Sub-millisecond $O(1)$ static set membership query across millions of threat hashes with Kirsch-Mitzenmacher double-hashing.
- **`AhoCorasick`**: Scans process command-lines and memory payloads for thousands of keywords (e.g. LOLBAS, PowerShell scripts, malicious strings) simultaneously in a single pass.
- **`RadixIpTrie`**: Sub-microsecond Longest Prefix Matching (LPM) for IP/CIDR threat tags (e.g. Tor Exit Nodes, Cobalt Strike C2s).

### 4. Hashing & Anomaly Detection
- **`Blake3`**: 256-bit tree hash up to 10x faster than SHA-256 with zero allocations on chunks &le; 1024 bytes.
- **`FastSha256`**: Pure managed FIPS 180-4 SHA-256 with streaming `Sha256Incremental` support.
- **`SsdeepFuzzyHash`**: Malware variant similarity calculation via CTPH.
- **`ShannonEntropy`**: Mathematical byte entropy for instant detection of ransomware and packed binaries.
- **`WelfordStats`**: Streaming single-pass mean, variance, and Z-score outlier detection in $O(1)$ memory.

---

## 🚀 Quick Usage

```csharp
using ZeroSecurity.Common;
using ZeroSecurity.Crypto;
using ZeroSecurity.Hashing;
using ZeroSecurity.Matching;

// 1. Peer-to-Peer Sovereign Handshake (X25519)
Span<byte> alicePriv = stackalloc byte[32];
Span<byte> alicePub = stackalloc byte[32];
Span<byte> bobPriv = stackalloc byte[32];
Span<byte> bobPub = stackalloc byte[32];

X25519.GetPublicKey(alicePriv, alicePub);
X25519.GetPublicKey(bobPriv, bobPub);

Span<byte> sharedSecret = stackalloc byte[32];
X25519.Agree(alicePriv, bobPub, sharedSecret);

// 2. Derive Session Keys via HKDF (RFC 5869)
Span<byte> sessionKey = stackalloc byte[32];
Hkdf.DeriveKey(salt: ReadOnlySpan<byte>.Empty, ikm: sharedSecret, info: "ZeroWire-V1"u8, sessionKey);

// 3. Encrypt with Safe 24-Byte Nonce (XChaCha20-Poly1305)
Span<byte> nonce = stackalloc byte[24]; // Safe to generate randomly!
Span<byte> ciphertext = stackalloc byte[payload.Length];
Span<byte> tag = stackalloc byte[16];
XChaCha20Poly1305.Encrypt(sessionKey, nonce, associatedData, payload, ciphertext, tag);

// 4. Memory Hygiene: Purge Sensitive Secrets from RAM
CryptoMemory.SecureZero(sharedSecret);
CryptoMemory.SecureZero(sessionKey);

// 5. Dynamic Threat Intel (CuckooFilter)
var cuckoo = new CuckooFilter(expectedElements: 100_000);
cuckoo.Add("c2.malicious-domain.com");
bool blocked = cuckoo.Contains("c2.malicious-domain.com"); // true
cuckoo.Delete("c2.malicious-domain.com");                   // dynamically unblock
```

---

## 📄 License
Released under the **MIT License**. Part of the **ZeroPlatform** sovereign industrial computing framework.
