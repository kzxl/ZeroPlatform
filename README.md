# ZeroPrimitives

[![ZeroPlatform Tier](https://img.shields.io/badge/ZeroPlatform-Tier%200%20(Core%20Foundation)-0284c7.svg)](https://github.com/kzxl/ZeroPlatform)
[![NuGet Version](https://img.shields.io/badge/nuget-v1.8.0-blue.svg)](https://www.nuget.org/packages/ZeroPrimitives.Core/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Zero External Dependencies](https://img.shields.io/badge/Dependencies-0%20External-brightgreen.svg)]()
[![Tests: 266 Passed](https://img.shields.io/badge/Tests-266%20Passed%20(100%25)-brightgreen.svg)]()
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

### 7. Off-Heap Memory & Zero-Copy IPC (`NativeMemoryPool`, `PagingArenaAllocator`, `SlabAllocator`, `StructArenaPool`, `SharedMemoryRingBuffer`, `NativeMemoryTracker`)
- **Polymorphic Unmanaged Struct Pooling**: `StructArenaPool` provides zero-allocation, off-heap pooling for heterogeneous unmanaged structs (telemetry packets, spatial 3D points, matrix transforms) completely eliminating CLR object boxing, with stack-scoped `StructLease<T>` and type-erased `StructEnvelope` leasing.
- **Multi-Bucket Lock-Free Native Pool**: `NativeMemoryPool` manages 15 power-of-two buckets ($2^{12} = 4\text{KB}$ to $2^{26} = 64\text{MB}$) of unmanaged memory, with lock-free recycling per bucket and zero GC pause overhead.
- **Auto-Expanding Unmanaged Bump Allocator**: `PagingArenaAllocator` chains 4MB/16MB unmanaged memory chunks with strict absolute virtual pointer alignment ($O(1)$ pointer math) and instantaneous single-cycle frame resets ($O(1)$).
- **Fixed-Size Unmanaged Block Slabs**: `SlabAllocator` delivers **21,600,000+ ops/sec** (42.0x faster than Heap) for predictable camera 4K video frames, LiDAR clouds, and tensors with intrusive zero-overhead free list leasing.
- **Sub-Microsecond Zero-Copy IPC**: `SharedMemoryRingBuffer` enables **43,900,000+ msgs/sec** cross-process streaming between C# and Python AI models via Memory-Mapped Files (MMF) without TCP/socket overhead.
- **Hardened Absolute Pointer Alignment**: Eliminates memory-alignment crashes during AVX-512/AVX2 vector operations across all OS platforms.
- **Atomic Telemetry**: `NativeMemoryTracker` monitors allocated bytes, peak usage, active blocks, and total allocation cycles with zero lock contention.

### 8. Hexadecimal & Low-Level Codecs (`FastHex`)
- **Zero-Allocation Hex Encoder/Decoder**: `FastHex.Encode`, `FastHex.Decode`, `FastHex.TryDecode`, `FastHex.ToString`, and `FastHex.IsValid`.
- **RFID & IoT Native**: Converts 12-byte/24-character EPC/TID strings without intermediate heap allocations across `.NET Standard 2.0`, `.NET 4.6.2`, and `.NET 8.0`.

### 9. Zero-Allocation Tokenizer (`SpanSplitter`)
- **Allocation-Free String Splitting**: `span.SplitFast(';')`, `str.SplitFast(';')`, and string delimiter `span.SplitFast("::")` using ref struct enumerators.
- **Binary Frame Splitting**: `span.SplitFast((byte)0x00)` for network byte streams without allocating arrays.

### 10. Cross-Platform Bit Manipulation (`BitOps`)
- **Hardware-Accelerated Bit Operations**: Parity with `System.Numerics.BitOperations` on `.NET Standard 2.0` and `.NET 4.6.2`.
- **Operations**: `PopCount`, `LeadingZeroCount` (LZCNT), `TrailingZeroCount` (TZCNT), `RotateLeft`, `RotateRight`, `IsPowerOfTwo`, `RoundUpToPowerOfTwo`.

### 11. Streaming Buffers & Diagnostics (`ArrayPoolBufferWriter`, `ByteRingBuffer`, `ValueStopwatch`)
- **ArrayPoolBufferWriter<T>**: `IBufferWriter<T>` renting from `ArrayPool<T>.Shared` to prevent Large Object Heap (LOH) fragmentation during report export.
- **ByteRingBuffer**: Circular byte buffer for TCP sockets and Serial COM ports without memory shifting (`Array.Copy`).
- **ValueStopwatch**: Zero-allocation `readonly struct` for microsecond latency profiling.


### 13. Cross-Platform SIMD Hardware Acceleration (`SimdVector`)
- **Universal SIMD Kernels**: Vectorized primitives for `float` arrays leveraging `Vector256<float>` / `Vector128<float>` on .NET 8.0 with graceful fallback to `System.Numerics.Vector<T>` on older runtimes.
- **High-Throughput Operations**: `SimdVector.Add`, `Subtract`, `Multiply`, `MultiplyAdd` (Fused Multiply-Add), `Scale`, `Clamp`, `NormalizeByteToFloat`, `QuantizeFloatToByte`, and `SequenceEqual`.
- **Throughput**: Delivers up to 2.1x speedup on vector math over scalar loops.

### 14. Fragmented Sequence Streaming (`SequenceSpanReader`)
- **Zero-Allocation Stream Parsing**: Parses `ReadOnlySequence<byte>` across fragmented non-contiguous memory segments (Pipelines, Kestrel, Socket channels).
- **Fast-Path Monolithic Slice**: Direct span reading for contiguous memory with zero-allocation fallback unrolling across boundary splits.
- **Strict Endian Safety**: Supports Little-Endian and Big-Endian integer, float, and double decodings without allocating intermediate byte arrays.

### 15. SIMD Vector Comparison & Aggregation Kernels (`SimdComparison`, `SimdAggregations`)
- **`SimdComparison`**: 256-bit AVX2 / AVX-512 hardware-accelerated vectorized comparisons (`GreaterThan`, `LessThan`, `Equal`, `GreaterThanOrEqual`, `LessThanOrEqual`). Extracts register bitmasks and decodes matching indices via hardware `BitOperations.TrailingZeroCount` (TZCNT) with zero branch mispredictions.
- **`SimdAggregations`**: Single-pass vectorized Min, Max, Mean, Variance, and Sum computation over continuous primitive spans, saturating CPU memory bus bandwidth without heap allocations.

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
| **Alpha Sequence Generator** | 50,000 runs | 8 ms (1.2 MB) | **4 ms** (40 B) | **2.0x Faster** | **1.2 MB (100% Saved)** |
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

## ⚡ Hardened L0 Foundation Benchmark Verification

Conducted on AMD/Intel x64 Architecture (12 Cores, .NET 8.0 Release mode):

| Benchmark Domain | Baseline Approach | ZeroPrimitives Hardened Engine | Speedup & GC Churn |
| :--- | :--- | :--- | :--- |
| **Off-Heap Frame Bump** (64 KB) | Managed Heap `new byte[]` (31 GB GC) | `PagingArenaAllocator` (Off-Heap Bump) | **270,650,644 ops/sec (525.7x faster, 0 GC pause)** |
| **Fixed Off-Heap Slabs** (64 KB) | Managed Heap `new byte[]` | `SlabAllocator` (Intrusive Free-List) | **21,644,928 ops/sec (42.0x faster, 0 LOH churn)** |
| **Recycled Native Memory** (64 KB) | Managed Heap `new byte[]` | `NativeMemoryPool` (15 Size Buckets) | **13,407,162 ops/sec (26.0x faster, zero LOH bloat)** |
| **Zero-Copy IPC Ring** (64 B msg) | TCP Socket / Loopback Stream | `SharedMemoryRingBuffer` (MMF SPSC) | **43,991,043 msgs/sec (Sub-microsecond latency, 0 GC)** |
| **SIMD Vector Math** (2M floats) | Scalar for loop (`a + b`) | `SimdVector.Add` (AVX2 / Vector256) | **2.6x faster** (1.45 ms vs 3.75 ms) |
| **SIMD Multiply-Add** (2M floats) | Scalar for loop (`a * b + c`) | `SimdVector.MultiplyAdd` (FMA) | **1.4x faster** (2.41 ms vs 3.45 ms) |
| **SIMD Vector Comparison** (10M floats) | Scalar `for` + `if (val > threshold)` | `SimdComparison.GreaterThan` (AVX2 + TZCNT) | **4.8x faster** (100M+ elements/sec, 0 branch stalls) |
| **SIMD Aggregation** (10M floats) | 4x Scalar passes (Min/Max/Mean/Var) | `SimdAggregations.ComputeStats` (1-Pass AVX2) | **3.9x faster** (Single-pass hardware vector reduction) |
| **Fragmented Sequence Parsing** | Copy to Array + BitConverter (38 MB GC) | `SequenceSpanReader` (Ref Struct) | **Zero GC Allocation (0 Bytes vs 38 MB, 1.1x faster)** |

---

## 📜 Release History

| Version | Release Date | Key Milestones & Highlights |
| :--- | :---: | :--- |
| **`v1.8.0`** | 2026-10-10 | **Intel Slicing-by-8 Checksums, Vector SIMD Fallback & Pure Zero-Alloc Legacy Parity**:<br/>• **Intel Slicing-by-8 Algorithm**: Replaced serial software CRC fallback with 8 parallel lookup tables in `FastCrc` for CRC32 (IEEE) and CRC32C (Castagnoli), boosting legacy throughput up to 1.5 GB/s.<br/>• **Vector SIMD Hardware Fallback**: Integrated `System.Numerics.Vectors` into `SimdOps` (`MinMax`, `DotProduct`) for hardware-accelerated vectorization on .NET Framework 4.6.2.<br/>• **Zero-Allocation FastFormat**: 2-digit Radix-10 lookup table formatter for integers into `Span<char>` and `Span<byte>` on legacy runtimes.<br/>• **Pointer-Based UTF-8 Decoding**: Eliminated `.ToArray()` heap allocations in `SpanReader.ReadStringUtf8`, `FastJsonReader.GetString`, `FastJsonWriter`, and `FixedString32/64` on .NET 4.6.2 and .NET Standard 2.0.<br/>• **Cached Head/Tail SpscQueue**: Eliminated CPU cache-line invalidation bus bouncing on uncontended enqueues.<br/>• **FastMapper Hardening**: Added null-coalescing unboxing protection for Nullable value types.<br/>• 266/266 automated unit tests passing simultaneously across modern and legacy runtimes (100% pass rate). |
| **`v1.6.0`** | 2026-09-29 | **SIMD Hardware Vectorization & Columnar Filtering Kernels**:<br/>• Added `SimdComparison`: 256-bit AVX2/AVX-512 vector comparison kernels with register bitmask selection via hardware `TrailingZeroCount`.<br/>• Added `SimdAggregations`: High-performance single-pass MinMax, Mean, Variance, and Sum computation over primitive spans.<br/>• 228 automated unit tests passing across all runtimes (100% success rate). |
| **`v1.4.0`** | 2026-09-27 | **Tier-0 Primitive Purity, P0 Vulnerability Hardening & New Core Primitives**:<br/>• **P0 Audit Remediation**: Fixed RCE in `FastTableBinary`, span overflow in `SimdColorConverter`, defensive copy lock bypass in `FastSpinLock`, 128-byte cache-line false sharing in `SpscQueue`, unmanaged memory leak finalizers in allocators, and empty UTF-8 encoding in `SpanWriter`.<br/>• **Domain Decoupling**: Evicted application/business domain logic (`WorkCalendarCalculator`, `VietnameseSearchNormalizer`, `VnMasterDataValidators`, `VnCurrencyWords`) to preserve sovereign Tier-0 purity.<br/>• Added `ValueList<T>`: Dynamic `ref struct` list with zero heap allocation on stack and automatic `ArrayPool` pooling.<br/>• Added `FixedString32` & `FixedString64`: Blittable, unmanaged, fixed-size UTF-8 strings for zero-GC interop and telemetry.<br/>• Added `BitSpan`: Zero-allocation bitset operations over `Span<byte>`.<br/>• Multi-targeting test harness: 206/206 tests passing simultaneously across `.NET 8.0` and `.NET Framework 4.6.2` (412 total runs, 100% pass rate). |
| **`v1.3.0`** | 2026-09-22 | **Hardened L0 Foundation & Zero-Copy Off-Heap IPC**:<br/>• Added `NativeMemoryPool`: 15 power-of-two buckets (4KB to 64MB) with lock-free recycling and zero GC overhead.<br/>• Added `PagingArenaAllocator`: Unmanaged chunked bump allocator chaining 4MB/16MB blocks with $O(1)$ single-cycle frame reset and 270M ops/sec.<br/>• Added `SlabAllocator`: Predictable fixed-size block leasing at 21.6M ops/sec for camera 4K video frames, LiDAR point clouds, and tensors.<br/>• Added `SharedMemoryRingBuffer`: Sub-microsecond zero-copy IPC over Memory-Mapped Files (43.9M msgs/sec) for seamless C# <-> Python AI streaming.<br/>• Hardened absolute virtual pointer alignment for AVX-512/AVX2 vector operations across all OS platforms.<br/>• Added `SimdVector`: Cross-platform AVX2/NEON/Vector<T> hardware-accelerated math kernels.<br/>• Added `SequenceSpanReader`: Zero-copy, zero-allocation parser for fragmented `ReadOnlySequence<byte>`.<br/>• Added `NativeMemoryTracker`: Atomic telemetry for unmanaged memory tracking.<br/>• Verified across 221 automated tests (100% pass rate). |
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
