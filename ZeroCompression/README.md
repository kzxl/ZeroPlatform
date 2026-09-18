# 🗜️ ZeroCompression — High-Performance Sovereign Compression Subsystem

Part of the **ZeroUniverse** industrial computing ecosystem.

## 📖 Overview
`ZeroCompression` provides high-throughput, low-allocation streaming compression, containerization, and authenticated payload encryption for ZeroUniverse applications and services.

## 🌟 Key Features
- ⚡ **Multi-Codec Pipeline**: Zstandard (ultra-fast, multi-threaded), Brotli (BCL), Deflate, GZip, Store.
- 🚀 **Zero-Allocation Hot-Paths**: Integrated buffer pooling (`ArrayPool<byte>.Shared`) for multi-gigabyte streams without GC pauses.
- 🔐 **Authenticated Stream AEAD**: Chunked AES-256-GCM authenticated encryption with instant PBKDF2 header verification.
- 🔄 **Single-Pass Streaming**: Stream observation, on-the-fly CRC32 verification, and progress reporting without 2-pass disk overhead.
- ✂️ **Multi-Part Volume Management**: Stream-level splitting and recombining (.001, .002...).
- 📦 **Container Packaging**: POSIX TAR and ZIP container stream serialization.

## 🏛 Architecture
Adheres to **Universe Architecture v4.0** with strict `ICompressionCodec` contract abstraction and `CodecRegistry` self-registration.
