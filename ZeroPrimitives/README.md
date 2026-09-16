# ZeroPrimitives

[![NuGet Version](https://img.shields.io/badge/nuget-v1.1.0-blue.svg)](https://www.nuget.org/packages/ZeroPrimitives.Core/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Zero External Dependencies](https://img.shields.io/badge/Dependencies-0%20External-brightgreen.svg)]()
[![Tests: 153 Passed](https://img.shields.io/badge/Tests-153%20Passed%20(100%25)-brightgreen.svg)]()
[![Multi-Targeting](https://img.shields.io/badge/.NET-8.0%20%7C%204.6.2%20%7C%20Standard%202.0-orange.svg)]()

> **Architectural Standard**: 100% Pure C#, Zero External Dependencies, Multi-Targeting across `.NET 8.0`, `.NET Framework 4.6.2`, and `.NET Standard 2.0`.

`ZeroPrimitives` is a sovereign, high-throughput .NET library engineered for ultra-fast, zero-allocation primitive conversions, low-level span/pointer number parsing, cryptographic/non-cryptographic hashing, binary buffer streaming, lock-free concurrency, and expression-compiled object mapping.

It replaces slow legacy conversion methods (`Convert.To*`, `value.ToString()`, `int.TryParse` with intermediate heap allocations) with raw CPU register unboxing, `ReadOnlySpan<char>` / `ReadOnlySpan<byte>` slicing, hardware intrinsics (SSE4.2 / ARM64), and cache-line aligned data structures.

---

## 🏛️ Comprehensive Feature Suite

### 1. Zero-Allocation Type Conversion (`FastConvert`)
- **Direct Register Unboxing**: Immediate unpack for boxed value types (`int`, `long`, `decimal`, `double`, `float`, `short`, `byte`, `bool`, `Guid`, `DateTime`) without calling `.ToString()`.
- **Nullable Variants**: `AsNullableInt`, `AsNullableLong`, `AsNullableDecimal`, `AsNullableDouble`, `AsNullableBool`, `AsNullableDateTime`, `AsNullableGuid`.
- **Enum Conversion**: `AsEnum<TEnum>()`, `AsNullableEnum<TEnum>()` (case-insensitive string/number conversion).
- **Delimited Collections**: `FromDelimitedString<T>()` and `AsDelimitedString<T>()` for fast CSV/list parsing.

### 2. Low-Level Span/Pointer Parsers (`FastNumberParser`, `FastDateParser`, `FastCsvParser`)
- **Pointer-based Integer Loops**: `(acc << 3) + (acc << 1) + (*ptr - '0')` over both `ReadOnlySpan<char>` and `ReadOnlySpan<byte>`.
- **Intelligent Separator Analysis**: Automatically resolves international vs. Vietnamese delimiters (`1,234.56` vs `1.234,56` vs `1.500.000 VNĐ`).
- **Zero-Allocation CSV Tokenizer**: `FastCsvParser.EnumerateRows` and `FastCsvParser.EnumerateCells` with RFC 4180 quote unescaping without heap string allocations.
- **SQL Server DateTime Safety**: Zero-allocation ISO 8601 and `dd/MM/yyyy` parsing with clamping to SQL Server `DATETIME` range (`1753-01-01` to `9999-12-31`).

### 3. Binary Streaming & Memory Buffers (`SpanReader`, `SpanWriter`, `FastBinary`, `ArrayPoolRentScope`)
- **Sequential Streaming**: `SpanReader` and `SpanWriter` ref structs for safe, bounds-checked binary serialization (LE, BE, VarInt, UTF-8 strings).
- **Automatic Pool Return**: `ArrayPoolRentScope<T>` ref struct pattern guarantees buffer return to `ArrayPool<T>.Shared` on exiting scope.
- **Variable-Length Integers**: `VarIntCodec` (LEB128 & ZigZag 32/64-bit) for ultra-compact telemetry and serialization.
- **GZip Compression**: `FastBuffer.GzipCompress` and `FastBuffer.GzipDecompressToString`.

### 4. High-Throughput Concurrency & Lock-Free Structures (`SpscQueue`, `FastSpinLock`)
- **Cache-Line Padded SPSC Queue**: `SpscQueue<T>` bounded FIFO queue with 64-byte padding between `_head` and `_tail` to eliminate L1/L2 cache-line bouncing (False Sharing).
- **1-Word Micro SpinLock**: `FastSpinLock` (4 bytes) eliminates OS kernel transition overhead for critical sections under 50 nanoseconds.

### 5. Industrial Barcode & Streaming JSON (`FastGs1Parser`, `FastJsonReader`, `FastJsonWriter`)
- **GS1 Barcode Engine**: `FastGs1Parser` decodes GS1-128 and GS1 DataMatrix identifiers (`(01) GTIN`, `(10) Lot`, `(17) Expiry`, `(21) Serial`) from both human-readable bracketed strings and raw FNC1 (`\u001d`) scanner streams without heap allocation.
- **Forward-Only JSON Stream Tokenizer**: `FastJsonReader` parses UTF-8 JSON payloads directly from byte spans without creating AST/DOM trees.
- **Micro JSON Writer**: `FastJsonWriter` writes compact JSON using stack buffers or rented byte pools.

### 6. Hardware-Accelerated Cryptography & Checksums (`FastCrc`, `FastHash`)
- **Hardware-Accelerated CRC32C**: `FastCrc.Crc32C` utilizes native CPU instructions (`SSE4.2` on x86/x64 or `ARM64`) for single-cycle 8-byte computation, falling back to 4-way loop unrolled tables on older runtimes.
- **Industrial Checksums**: `Crc16Modbus` (RS485/scales/PLCs), `Crc16Ccitt`, and `Crc32` (IEEE 802.3 Ethernet/ZIP).
- **Hashing**: Ultra-fast 32/64-bit `FNV-1a`, plus zero-allocation `Md5Hex`, `Sha1Hex`, `Sha256Hex`.

### 7. Vietnamese Enterprise Master Data & Finance (`VietnameseSearchNormalizer`, `VnMasterDataValidators`, `VnFinancialRounding`)
- **Diacritic Normalization & SEO Slugs**: `VietnameseSearchNormalizer.NormalizeForSearch` and `ToSlug` strip accents, normalize `Đ/đ` to `d`, and format search tokens directly on stack spans.
- **Master Data Validators**: Tax Code (MST Modulo 11 check digit, 10/13 digits), Citizen ID (CCCD 12-digit), Phone numbers.
- **Financial Rounding**: VAS/Circular 200 compliant commercial rounding (`AwayFromZero`) and VAT line-item discrepancy reconciliation.

### 9. Hexadecimal & Low-Level Codecs (`FastHex`)
- **Zero-Allocation Hex Encoder/Decoder**: `FastHex.Encode`, `FastHex.Decode`, `FastHex.TryDecode`, `FastHex.ToString`, and `FastHex.IsValid`.
- **RFID & IoT Native**: Converts 12-byte/24-character EPC/TID strings without intermediate heap allocations across `.NET Standard 2.0`, `.NET 4.6.2`, and `.NET 8.0`.

### 10. Zero-Allocation Tokenizer (`SpanSplitter`)
- **Allocation-Free String Splitting**: `span.SplitFast(';')`, `str.SplitFast(';')`, and string delimiter `span.SplitFast("::")` using ref struct enumerators.
- **Binary Frame Splitting**: `span.SplitFast((byte)0x00)` for network byte streams without allocating arrays.

### 11. Cross-Platform Bit Manipulation (`BitOps`)
- **Hardware-Accelerated Bit Operations**: Parity with `System.Numerics.BitOperations` on `.NET Standard 2.0` and `.NET 4.6.2`.
- **Operations**: `PopCount`, `LeadingZeroCount` (LZCNT), `TrailingZeroCount` (TZCNT), `RotateLeft`, `RotateRight`, `IsPowerOfTwo`, `RoundUpToPowerOfTwo`.

### 12. Streaming Buffers & Diagnostics (`ArrayPoolBufferWriter`, `ByteRingBuffer`, `ValueStopwatch`)
- **ArrayPoolBufferWriter<T>**: `IBufferWriter<T>` renting from `ArrayPool<T>.Shared` to prevent Large Object Heap (LOH) fragmentation during report export.
- **ByteRingBuffer**: Circular byte buffer for TCP sockets and Serial COM ports without memory shifting (`Array.Copy`).
- **ValueStopwatch**: Zero-allocation `readonly struct` for microsecond latency profiling.

---

## 🎯 Pragmatic Dual-Tier Allocation Standard

`ZeroPrimitives` adheres to the sovereign ZeroUniverse performance directive:
> **"Zero-allocation on Hot-Paths, Minimal Allocation & Buffer Pooling on Application-Paths, Maximum Performance & Ergonomics Everywhere."**

### 🔹 Tier 1: Hot-Path Engine (Strict 100% Zero-Allocation)
- **Target Use-Case**: High-frequency network socket loops, 100k RFID packet streams/sec, math/crypto kernels, and sequential binary parsing.
- **Underlying Primitives**: `Span<T>`, `ReadOnlySpan<T>`, `stackalloc`, `ref struct` (`SpanReader`, `SpanWriter`, `SpanSplitter`, `FastHex`), and SIMD hardware intrinsics.
- **Zero GC Churn**: Operates strictly on the CPU Stack and registers with **0 bytes allocated on the Managed Heap**, eliminating Gen 0/1 GC pause spikes entirely.

### 🔹 Tier 2: Ergonomic & Buffer Pooling (Minimal & Exact Allocation)
- **Target Use-Case**: Enterprise business layers (MDS ERP, WinForms, WebApi controllers, large Excel/report export, async/await I/O pipelines).
- **Underlying Primitives**: `ArrayPoolBufferWriter<T>`, `ByteRingBuffer(useArrayPool: true)`, `Memory<T>`, `ReadOnlyMemory<T>`, and exact-sized string creation (`FastHex.ToString`).
- **LOH Protection**: Reuses pooled buffers across operations to prevent Large Object Heap fragmentation, providing familiar, convenient APIs without creating throw-away intermediate garbage.

---

## 📐 Uniform Method Naming Convention

To guarantee predictability and ease of use across the entire ecosystem:

| Convention | Description | Standard Example |
| :--- | :--- | :--- |
| **`Try[Action]`** | Never throws, returns `bool`, final argument is `out T result`. | `TryReadInt32LittleEndian`, `TryDecode`, `TryParseInt64` |
| **`[Action]` (Direct)** | Returns value directly, throws via non-inlined `ThrowHelper` on failure. | `ReadInt32LittleEndian`, `Decode`, `ParseInt64` |
| **`Encode` / `Decode`** | Two-way transformation between binary spans and character/text spans. | `FastHex.Encode(...)`, `FastHex.Decode(...)` |
| **`SplitFast` / `Enumerate*`** | Zero-allocation `ref struct` iteration over tokens or rows. | `text.SplitFast(';')`, `FastCsvParser.EnumerateRows(...)` |
| **`[Type]LittleEndian` / `BigEndian`** | Explicit endianness specifier for binary network and hardware I/O. | `ReadUInt16BigEndian`, `WriteInt32LittleEndian` |

---

## 📊 Real-World Hardware Benchmarks

The following benchmarks were executed under release compilation (`-c Release`), measuring execution time and heap allocations against traditional .NET BCL and standard approaches.

### Environment Specification
- **CPU**: Intel(R) Core(TM) i5-10400 CPU @ 2.90GHz (6 Cores, 12 Logical Processors)
- **RAM**: 32.0 GB DDR4
- **Operating System**: Microsoft Windows 10 Pro (x64)
- **Runtime Environment**: .NET 8.0 (x64, Server GC default)

### Benchmark Results

| Scenario | Operations | C# Standard / BCL | ZeroPrimitives | Speedup | Heap Memory Saved |
| :--- | :---: | :---: | :---: | :---: | :---: |
| **CRC32C HW Checksum** | 50,000 x 256B | 230 ms (40 B) | **7 ms** (40 B) | **30.6x Faster** | Hardware SSE4.2 Accelerated |
| **JSON Stream Tokenizer** | 50,000 docs | 77 ms (3.4 MB) | **17 ms** (40 B) | **4.4x Faster** | **3.4 MB (100% Saved)** |
| **Unboxing & Cast** | 100,000 ops | 0 ms (40 B) | **0 ms** (40 B) | **3.3x Faster** | Direct Register Cast |
| **Hex RFID EPC Encode** | 50,000 tags | 14 ms (8.0 MB) | **5 ms** (40 B) | **2.7x Faster** | **8.0 MB (100% Saved)** |
| **SPSC Queue** | 100,000 items | 7 ms (ConcurrentQueue) | **2 ms** (Lock-free) | **2.6x Faster** | Zero False-Sharing Padding |
| **VN Search Normalizer** | 50,000 texts | 115 ms (25.6 MB) | **72 ms** (40 B) | **1.6x Faster** | **25.6 MB (100% Saved)** |
| **SpanSplitter Tokenizer** | 50,000 lines | 9 ms (17.2 MB) | **6 ms** (40 B) | **1.5x Faster** | **17.2 MB (100% Saved)** |
| **Delimited CSV Parse (RFC 4180)** | 50,000 lines | 11 ms (12.6 MB) | **11 ms** (40 B) | **1.1x Faster** | **12.6 MB (100% Saved)** |
| **Binary Packet Read** | 50,000 pkts | 4 ms (10.7 MB) | **5 ms** (40 B) | Zero GC Pause | **10.7 MB (100% Saved)** |

> **Key Architectural Takeaway**: By shifting from intermediate heap strings and stream wrappers to `Span<T>` stack buffers and hardware intrinsics, `ZeroPrimitives` eliminates tens of megabytes of Gen 0/Gen 1 GC churn while boosting throughput up to **30x**.

---

## ⚡ Quick Examples

### 1. RFID EPC Hex Encoding & Parsing
```csharp
using ZeroPrimitives.Buffers;

// Encode raw 12-byte EPC to hex on stack
Span<char> hexBuffer = stackalloc char[24];
byte[] epcBytes = new byte[] { 0xE2, 0x80, 0x11, 0x70, 0x00, 0x00, 0x02, 0x0B, 0x12, 0x34, 0x56, 0x78 };
FastHex.Encode(epcBytes, hexBuffer);

// Decode hex back to binary without allocations
Span<byte> decodedBytes = stackalloc byte[12];
if (FastHex.TryDecode(hexBuffer, decodedBytes, out int written))
{
    // Process decoded binary EPC
}
```

### 2. Zero-Allocation Tokenization (`SpanSplitter`)
```csharp
using ZeroPrimitives.Text;

string config = "ORDER_2026_001;CUSTOMER_ABC;WAREHOUSE_NORTH;SKU_999;QTY_100";

// Zero heap allocations: replaces string.Split(';')
foreach (ReadOnlySpan<char> token in config.AsSpan().SplitFast(';'))
{
    // Process token
}
```

### 3. Circular Streaming Buffer for TCP / Serial COM Port
```csharp
using ZeroPrimitives.Buffers;

var ring = new ByteRingBuffer(capacity: 4096, useArrayPool: true);

// Incoming socket chunk
ring.Write(receivedSocketBytes);

// Peek header frame without shifting memory
Span<byte> header = stackalloc byte[4];
if (ring.Peek(header) == 4 && header[0] == 0xAA)
{
    ring.Advance(4); // Consume header
}
```

### 4. Hardware-Accelerated CRC32C & Lock-Free SPSC Queue
```csharp
using ZeroPrimitives.Cryptography;
using ZeroPrimitives.Concurrency;

// Hardware instruction SSE4.2 / ARM64 (8 bytes / single clock cycle)
uint checksum = FastCrc.Crc32C(packetSpan);

// High-speed Single-Producer Single-Consumer queue between I/O and processing threads
var queue = new SpscQueue<int>(capacityPowerOfTwo: 1024);
queue.TryEnqueue(42);
if (queue.TryDequeue(out int val))
{
    // Process without thread lock contention
}
```

---

## 📜 Release History

| Version | Release Date | Key Milestones & Highlights |
| :--- | :---: | :--- |
| **`v1.1.0`** | 2026-09-16 | **Hardware Acceleration & High-Performance Parsers**:<br/>• Integrated CPU hardware intrinsics (SSE4.2 on x86/x64, ARM64) in `FastCrc.Crc32C` for single-cycle 8-byte checksums (30x speedup).<br/>• Added pointer-based integer, decimal, and float loops with auto-delimiters in `FastNumberParser`.<br/>• Added zero-allocation RFC 4180 CSV tokenizer (`FastCsvParser.EnumerateRows`, `EnumerateCells`).<br/>• Enhanced `FastConvert` and `FastDateParser` with SQL Server DATETIME safety.<br/>• Verified across 153 automated tests (100% pass rate). |
| **`v1.0.0`** | 2026-09-10 | **Initial Sovereign Release**:<br/>• Direct register unboxing `FastConvert` for primitive types and enums.<br/>• Zero-allocation binary buffer streaming (`SpanReader`, `SpanWriter`, `VarIntCodec`).<br/>• Cache-line padded `SpscQueue` (False Sharing elimination) and 4-byte `FastSpinLock`.<br/>• GS1 barcode tokenizer (`FastGs1Parser`), `FastHex`, `SpanSplitter`, `ByteRingBuffer`.<br/>• Multi-targeting .NET 8.0, .NET Framework 4.6.2, and .NET Standard 2.0. |

---

## Multi-Targeting Support

- **.NET 8.0+** (High-throughput cloud services, edge AI, IoT, and IPC)
- **.NET Framework 4.6.2+** (Enterprise WinForms / WPF applications)
- **.NET Standard 2.0** (Universal cross-platform compatibility)

---

## License

MIT License. Copyright © 2026 Phong Võ (`kzxl`).
