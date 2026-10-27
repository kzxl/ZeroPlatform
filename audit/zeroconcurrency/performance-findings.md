# ZeroConcurrency Verified Performance Findings

## 1. Summary of Measured Results

Measurements were collected empirically via [`AuditRunner`](file:///E:/15.%20Other/ZeroUniverse/ZeroPlatform/audit/benchmark/source/Program.cs) across modern .NET 8.0 and .NET Framework 4.6.2 (x64, 12 cores):

| Benchmark Test | Implementation | Target Framework | Latency / Op | Throughput | Alloc / Op | Gen0 Collections | Verified Finding |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **SPSC Queue (2-Thread)** | `ZeroRingBuffer<int>` | **.NET 4.6.2** | **13.5 ns** | **73.8M ops/s** | **0 B** | **0** | **2.51x FASTER than ConcurrentQueue** |
| | `ConcurrentQueue<int>` | .NET 4.6.2 | 34.0 ns | 29.4M ops/s | 8 B | 2 / 2M ops | BCL Baseline |
| | `ZeroRingBuffer<int>` | **.NET 8.0** | **13.4 ns** | **74.4M ops/s** | **0 B** | **0** | **2.83x FASTER than ConcurrentQueue** |
| | `ConcurrentQueue<int>` | .NET 8.0 | 38.0 ns | 26.3M ops/s | 7 B | 1 / 2M ops | BCL Baseline |
| **MPMC Queue (4P / 4C)** | `ZeroMpmcRingBuffer<int>` | .NET 4.6.2 | 108.0 ns | 9.3M ops/s | 0 B | 0 | **SLOWER (0.79x) vs ConcurrentQueue** |
| | `ConcurrentQueue<int>` | .NET 4.6.2 | 85.7 ns | 11.7M ops/s | 8 B | 2 / 2M ops | BCL Segmented Array Wins |
| | `ZeroMpmcRingBuffer<int>` | .NET 8.0 | 104.0 ns | 9.6M ops/s | 0 B | 0 | Marginally faster (1.18x) |
| | `ConcurrentQueue<int>` | .NET 8.0 | 122.2 ns | 8.2M ops/s | 2 B | 0 | BCL Segmented Array Baseline |
| **Streaming Channel** | `ZeroChannel<int>` | **.NET 4.6.2** | 143.0 ns | 7.0M ops/s | 0 B | 0 | Comparable (0.97x) vs BCL |
| | `System.Threading.Channels` | .NET 4.6.2 | 138.3 ns | 7.2M ops/s | 0 B | 0 | BCL Baseline |
| | `ZeroChannel<int>` | **.NET 8.0** | **133.2 ns** | **7.5M ops/s** | **0 B** | **0** | **1.23x FASTER than BCL Channels** |
| | `System.Threading.Channels` | .NET 8.0 | 163.5 ns | 6.1M ops/s | 0 B | 0 | BCL Baseline |
| **Async Promise** | `ZeroPromise<int>` (Pooled) | **.NET 4.6.2** | **61.1 ns** | **16.4M ops/s** | **0 B** | **0** | **Saves 3.48 MB GC & 16 Gen0 pauses** |
| | `TaskCompletionSource<int>` | .NET 4.6.2 | 63.0 ns | 15.9M ops/s | ~3.5 B | 16 / 1M ops | Standard BCL Task Allocation |
| | `ZeroPromise<int>` (Pooled) | .NET 8.0 | 59.2 ns | 16.9M ops/s | 0 B | 0 | Zero GC, but 0.64x vs TCS single-thread |
| | `TaskCompletionSource<int>` | .NET 8.0 | 38.2 ns | 26.2M ops/s | ~1.8 B | 15 / 1M ops | Optimized .NET 8 JIT |

## 2. Deep Performance Insights

### Insight A: SPSC Dominance (`ZeroRingBuffer`)
- For dedicated Single-Producer Single-Consumer pipelines (e.g. reading from a network socket / serial port and handing off to a single processing thread), `ZeroRingBuffer` delivers **74.4 million messages per second** with **zero GC allocations**.
- It outperforms `ConcurrentQueue<T>` by **2.5x to 2.8x** because it eliminates segment node allocations and volatile CAS retries on the consumer side.
- *Caveat:* The P0 wraparound defect documented in `concurrency-correctness.md` must be patched before production use.

### Insight B: MPMC Contention & False Sharing
- In multi-producer multi-consumer configurations (4 threads enqueuing, 4 threads dequeuing concurrently), `ZeroMpmcRingBuffer` does not achieve the expected lock-free speedup. On .NET 4.6.2, it is **20% slower than Microsoft's `ConcurrentQueue`** (9.3M ops/s vs 11.7M ops/s).
- Microsoft's `ConcurrentQueue<T>` employs 32-element linked segments (`ConcurrentQueue<T>.Segment`) where cache line contention is localized to discrete memory segments, whereas `ZeroMpmcRingBuffer` forces all threads onto a single continuous array of contiguous `Cell` structures.

### Insight C: `ZeroChannel` is a Production-Grade Star
- `ZeroChannel<T>` provides Go-like CSP semantics, direct handoff between waiting readers and writers, and bounded backpressure.
- On modern .NET 8.0, it is **23% faster than `System.Threading.Channels`** while maintaining zero GC churn during active streaming.
- On legacy .NET Framework 4.6.2, it performs identically to Microsoft's backported package while avoiding external package version binding conflicts.
