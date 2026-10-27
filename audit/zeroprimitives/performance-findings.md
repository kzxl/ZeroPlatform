# ZeroPrimitives Verified Performance Findings

## 1. Summary of Measured Results

The following measurements were collected empirically using the dedicated audit harness [`AuditRunner`](file:///E:/15.%20Other/ZeroUniverse/ZeroPlatform/audit/benchmark/source/Program.cs) across both modern .NET 8.0 and .NET Framework 4.6.2 on an x64 architecture (12 cores):

| Benchmark Test | Implementation | Target Framework | Latency / Op | Throughput | Alloc / Op | Gen0 Collections | Verified Finding |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **Number Parsing** | `FastNumberParser.TryParseInt32` | **.NET 4.6.2** | **16.2 ns** | **61.6M ops/s** | **0 B** | **0** | **4.66x FASTER than BCL** |
| | `int.TryParse` (BCL) | .NET 4.6.2 | 75.7 ns | 13.2M ops/s | 0 B | 0 | Baseline |
| | `FastNumberParser.TryParseInt32` | **.NET 8.0** | 24.3 ns | 41.1M ops/s | 0 B | 0 | Comparable / 0.81x vs BCL |
| | `int.TryParse` (BCL) | .NET 8.0 | 19.6 ns | 51.0M ops/s | 0 B | 0 | JIT Vectorized BCL |
| **Object Mapping** | `FastMapper.Map<Poco, Dto>` | .NET 4.6.2 | 227.2 ns | 4.4M ops/s | ~1.2 KB | 5 / 500k ops | **21x SLOWER than Manual** |
| | Manual Property Assignment | .NET 4.6.2 | 10.7 ns | 93.1M ops/s | ~1.2 KB | 5 / 500k ops | Inlined scalar baseline |
| | `FastMapper.Map<Poco, Dto>` | .NET 8.0 | 130.4 ns | 7.7M ops/s | ~1.2 KB | 5 / 500k ops | **10x SLOWER than Manual** |
| | Manual Property Assignment | .NET 8.0 | 13.0 ns | 77.0M ops/s | ~1.2 KB | 5 / 500k ops | Inlined scalar baseline |
| **Off-Heap Allocator** | `PagingArenaAllocator (16KB)` | **.NET 4.6.2** | **7.3 ns** | **136.4M ops/s**| **0 B** | **0** | **75x FASTER than Heap, 8x vs ArrayPool** |
| | `ArrayPool<byte>.Shared` | .NET 4.6.2 | 58.0 ns | 17.3M ops/s | 0 B | 0 | BCL pooled baseline |
| | Managed Heap (`new byte[16KB]`) | .NET 4.6.2 | 549.1 ns | 1.8M ops/s | 16.3 KB | 522 / 200k ops | Heavy GC Gen0 pressure |
| | `PagingArenaAllocator (16KB)` | **.NET 8.0** | **3.8 ns** | **263.8M ops/s**| **0 B** | **0** | **150x FASTER than Heap, 7x vs ArrayPool** |
| | `ArrayPool<byte>.Shared` | .NET 8.0 | 26.4 ns | 37.9M ops/s | 0 B | 0 | BCL pooled baseline |
| | Managed Heap (`new byte[16KB]`) | .NET 8.0 | 578.8 ns | 1.7M ops/s | 16.3 KB | 522 / 200k ops | Heavy GC Gen0 pressure |

## 2. Deep Performance Insights

### Insight A: `PagingArenaAllocator` is an Exceptional High-ROI Component
- Bypassing the GC via linear pointer increment (`_currentChunk->Offset += size`) yields **263.8 million allocations/sec** on modern .NET and **136.4 million allocations/sec** on .NET Framework 4.6.2.
- A single cycle `arena.Reset()` reclaims 100% of memory across linked unmanaged chunks without triggering a single Gen0, Gen1, or Gen2 collection.
- For high-frequency query pipelines, frame renderers, or batch import calculations, `PagingArenaAllocator` delivers genuine 75x to 150x speedups over managed heap allocation.

### Insight B: The `NativeMemoryPool` Allocation Paradox
- While `NativeMemoryPool` allocates underlying buffers off-heap, it wraps each rented memory region in a managed class instance (`NativeMemoryBlock`).
- Under heavy churn (e.g. 500k iterations in `ZeroPrimitives.Benchmarks`), creating and dropping `NativeMemoryBlock` instances allocated **16 MB of managed GC objects**.
- **Architectural Fix Required:** `NativeMemoryBlock` should be refactored into a `readonly ref struct` or a value-type struct wrapper returning `Span<byte>` directly, eliminating the managed wrapper allocation entirely.

### Insight C: `FastNumberParser` is a Critical Asset for .NET Framework 4.6.2
- On legacy .NET Framework 4.6.2, standard `int.Parse` / `int.TryParse` incurs culture lookup overhead and lacks hardware-accelerated span parsing, running at 75.7 ns.
- `FastNumberParser.TryParseInt32` reduces this to **16.2 ns (4.66x faster)** while operating directly on `ReadOnlySpan<char>` via the `System.Memory` polyfill.
- On modern .NET 8.0, Microsoft's BCL contains heavily vectorized, JIT-intrinsified parsers, meaning `FastNumberParser` offers no significant speed advantage on .NET 8/10.

### Insight D: Reality Check on `FastMapper`
- Micro-mappers compiling dynamic Expression Trees are frequently claimed to be "zero-overhead".
- In verified testing, calling the compiled delegate incurred **10x to 21x slower throughput** than simple inline property assignments (`target.Prop = source.Prop`).
- Furthermore, because both approaches instantiate `new DTO()`, `FastMapper` saves 0 bytes of heap memory.
