# ZeroPrimitives Technical Source Audit

## 1. Repository Inventory & Metadata
- **Repository URL:** `https://github.com/kzxl/ZeroPrimitives.git`
- **Audit Commit:** `8669e0ae29b4e0ab2ede0cb13c4f72fe20eb1406` (master)
- **Target Frameworks:** `netstandard2.0;net462;net8.0`
- **C# Language Version:** `latest`
- **Unsafe Code Allowed:** `true`
- **Zero Platform Layer:** Layer 0 (Root Bedrock - Core Foundation)
- **Direct Dependencies:**
  - `net8.0`: None (0 external package dependencies).
  - `net462` / `netstandard2.0`: `System.Memory` (4.5.5), `System.Buffers` (4.5.1), `System.Runtime.CompilerServices.Unsafe` (6.0.0), `System.Numerics.Vectors` (4.5.0), `System.ValueTuple` (4.5.0).
- **Public API Volume:** 55 source files in `src/ZeroPrimitives.Core`.
- **Existing Test Coverage:** `tests/ZeroPrimitives.Tests` contains 232 test cases, executed and passing on both `net8.0` and `net462`.

## 2. Component Taxonomy & Architectural Mapping

| Functional Domain | Source Files | Intended Purpose | Audit Classification |
| :--- | :--- | :--- | :--- |
| **Parsing & Formatting** | `FastNumberParser.cs`, `FastDateParser.cs`, `FastCsvParser.cs`, `FastGs1Parser.cs`, `FastJsonReader.cs` | Zero-allocation text-to-binary parsing over `ReadOnlySpan<char>` and raw pointers. | **VERIFIED (Production-Grade)** |
| **Off-Heap Allocators** | `ArenaAllocator.cs`, `PagingArenaAllocator.cs`, `SlabAllocator.cs`, `NativeMemoryPool.cs`, `StructArenaPool.cs` | Linear bump and pooled unmanaged memory to bypass .NET GC and LOH churn. | **STRONGLY SUPPORTED (High Value, NativeMemoryPool requires refactoring)** |
| **Type Conversion** | `FastConvert.cs`, `PrimitiveExtensions.cs` | Fast unboxing and type coercion for weakly-typed ADO.NET and JSON payloads. | **VERIFIED (High Practical Utility in ERP/ADO.NET)** |
| **Object Mapping** | `FastMapper.cs`, `FastTableMapper.cs`, `FastTableBinary.cs` | Expression-tree compiled shallow mapping and ADO.NET `IDataReader`/`DataTable` to POCOs. | **PARTIALLY VERIFIED (Functional, but misses typed ADO.NET getters)** |
| **Hardware Intrinsics** | `SimdOps.cs`, `SimdVector.cs`, `SimdComparison.cs`, `SimdAggregations.cs`, `SimdColorConverter.cs` | AVX2 / SSE2 vector acceleration with fallback to `Vector<T>` and scalar loops. | **VERIFIED (.NET 8 only; scalar fallback on .NET Framework 4.6.2)** |
| **Buffers & Encoders** | `FastHex.cs`, `BitOps.cs`, `BitSpan.cs`, `ByteRingBuffer.cs`, `SpanReader.cs`, `SpanWriter.cs`, `ArrayPoolBufferWriter.cs` | Low-level binary manipulation, bit manipulation, and stream serialization. | **VERIFIED (Solid Engineering)** |
| **Identifiers & Crypto** | `FastUlid.cs`, `Uuid7.cs`, `FastCrc.cs` (CRC32C), `FastHash.cs` | Monotonic sortable identifiers and hardware-accelerated checksums. | **VERIFIED (Solid Engineering)** |
| **Concurrency Primitives**| `FastSpinLock.cs`, `SpscQueue.cs` | Basic synchronization primitives embedded in Layer 0. | **OVERLAPPING (Duplicated with ZeroConcurrency)** |

## 3. Code Quality Observations
1. **Clean Codebase Hygiene:** Zero instances of `TODO`, `FIXME`, or `NotImplementedException`.
2. **Defensive Inlining:** Extensive use of `[MethodImpl(MethodImplOptions.AggressiveInlining)]` on hot conversion and parsing routines.
3. **Multi-Targeting Cleanliness:** Clean `#if NET8_0_OR_GREATER` preprocessor directives separating modern hardware intrinsics (`System.Runtime.Intrinsics`) and modern APIs (`NativeMemory.Alloc`) from legacy polyfills (`Marshal.AllocHGlobal`, `System.Numerics.Vectors`).
