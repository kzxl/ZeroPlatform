# 🗜️ ZeroCompression — High-Performance Sovereign Compression Subsystem

[![ZeroPlatform Tier](https://img.shields.io/badge/ZeroPlatform-Tier%201%20(Compute%20%26%20System)-4f46e5.svg)](https://github.com/kzxl/ZeroPlatform)

Part of the **ZeroUniverse** industrial computing ecosystem.

## 📖 Overview
`ZeroCompression` provides high-throughput, low-allocation streaming compression, containerization, sub-millisecond heuristic data classification, and authenticated payload encryption for ZeroUniverse applications and services.

## 🌟 Key Features
- ⚡ **Multi-Codec Pipeline**: Zstandard (ultra-fast, multi-threaded with Long-Distance Matching), LZMA, Brotli (BCL), Deflate, GZip, Store.
- 🔬 **ZeroTelemetry Engine**: Specialized IoT & SCADA time-series codec implementing double-delta timestamp compression and Gorilla XOR IEEE-754 floating-point bit packing.
- 🧠 **Adaptive Heuristic Classifier (`DataClassifier`)**: Sub-millisecond inspection of magic headers, Shannon entropy ($H \ge 7.92$), printable grammar, structural JSON density, and time-series monotonic regularity to automatically recommend the optimal compression profile.
- 🚀 **Zero-Allocation Hot-Paths**: Integrated buffer pooling (`ArrayPool<byte>.Shared`) for multi-gigabyte streams without GC pauses.
- 🔐 **Authenticated Stream AEAD**: Chunked AES-256-GCM authenticated encryption with instant PBKDF2 header verification (<5ms).
- 🔄 **Single-Pass Streaming**: Stream observation, on-the-fly CRC32 verification, and progress reporting without 2-pass disk overhead.
- 📊 **Parallel Benchmark Suite (`CompressionBenchmarkSuite`)**: In-memory and dataset roundtrip verification measuring compression ratios and throughput (MB/s).
- ✂️ **Multi-Part Volume Management**: Stream-level splitting and recombining (.001, .002...).
- 📦 **Container Packaging**: POSIX TAR and ZIP container stream serialization.

## 🏛 Architecture
Adheres to **Universe Architecture v4.0** with strict `ICompressionCodec` contract abstraction and `CodecRegistry` self-registration.

## 📦 NuGet Packages
- `ZeroCompression.Core`: Multi-targeting `.NET 8.0` & `.NET 10.0`.
