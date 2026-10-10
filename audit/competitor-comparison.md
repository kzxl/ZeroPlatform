# Direct Competitor Comparison Analysis

## 1. ZeroPrimitives vs Premier Ecosystem Libraries

### A. Number Parsing: `FastNumberParser` vs BCL `int.TryParse`
| Metric / Feature | `FastNumberParser` | .NET Framework 4.6.2 `int.TryParse` | .NET 8.0 `int.TryParse` |
| :--- | :--- | :--- | :--- |
| **Throughput (.NET 4.6.2)** | **61.6M ops/s (16.2 ns)** | 13.2M ops/s (75.7 ns) | N/A |
| **Throughput (.NET 8.0)** | 41.1M ops/s (24.3 ns) | N/A | **51.0M ops/s (19.6 ns)** |
| **Allocation** | 0 Bytes | 0 Bytes | 0 Bytes |
| **Span Input Support** | Native `ReadOnlySpan<char>` | Requires String on .NET 4.6.2 | Native `ReadOnlySpan<char>` |
| **Culture Handling** | Invariant / Fast ASCII only | Full CultureInfo lookup | Vectorized Invariant / Culture |
| **Verdict** | **Massive 4.66x Win on .NET 4.6.2**; redundant on modern .NET 8. | Slow legacy implementation. | State of the art. |

### B. Off-Heap Memory: `PagingArenaAllocator` vs `ArrayPool<T>.Shared` vs `CommunityToolkit.HighPerformance`
| Metric / Feature | `PagingArenaAllocator` | `ArrayPool<byte>.Shared` | `CommunityToolkit.HighPerformance` |
| :--- | :--- | :--- | :--- |
| **Throughput (.NET 8.0)** | **263.8M ops/s (3.8 ns)** | 37.9M ops/s (26.4 ns) | ~35M ops/s |
| **Throughput (.NET 4.6.2)** | **136.4M ops/s (7.3 ns)** | 17.3M ops/s (58.0 ns) | N/A (Limited .NET 4.6.2 support) |
| **Allocation per Cycle** | **0 Bytes** | 0 Bytes | 0 Bytes |
| **Bulk Reclamation** | **O(1) Single Pointer Reset** | Must return every rented array individually | Manual disposal |
| **GC Gen2 / LOH Fragmentation** | **Zero (Completely Off-Heap)** | Low, but subject to pool trim | Varies |
| **Verdict** | **Superior for batch queries, frame loops, and temporary buffers.** | Standard general-purpose pool. | Specialized memory spans. |

### C. ADO.NET Mapping: `FastTableMapper` vs `Dapper`
| Metric / Feature | `FastTableMapper` | `Dapper` (v2.1.35) |
| :--- | :--- | :--- |
| **Compilation Model** | Expression Trees to IL delegate | DynamicMethod IL generation |
| **Column-to-Property Binding** | Case-insensitive ordinal mapping | Highly optimized IL deserializer |
| **Field Accessor Strategy** | Calls `IDataRecord.GetValue(i)` (Causes boxing on scalar DB types) | Calls typed accessors (`GetInt32`, `GetDecimal`) — **Zero Boxing** |
| **Dependency Footprint** | Built into `ZeroPrimitives.Core` | External NuGet dependency |
| **Verdict** | Good lightweight micro-mapper, but **Dapper is measurably superior on allocations** for scalar SQL queries. | Industry standard; recommended when zero boxing from database is paramount. |

---

## 2. ZeroConcurrency vs Premier Concurrency Frameworks

### A. SPSC Lock-Free Streaming: `ZeroRingBuffer<T>` vs `ConcurrentQueue<T>`
| Metric / Feature | `ZeroRingBuffer<T>` | `ConcurrentQueue<T>` |
| :--- | :--- | :--- |
| **Throughput (.NET 8.0)** | **74.4M ops/s (13.4 ns)** | 26.3M ops/s (38.0 ns) |
| **Throughput (.NET 4.6.2)** | **73.8M ops/s (13.5 ns)** | 29.4M ops/s (34.0 ns) |
| **GC Allocations** | **0 Bytes (Pre-allocated contiguous array)** | Node/Segment allocation churn (~8-16 MB per 2M items) |
| **Cache Line Padding** | Explicit 64-byte padding on head/tail | Segment-based internal padding |
| **Wraparound Integrity** | **FAILED (Critical bug near int.MaxValue)** | Correct across all limits |
| **Verdict** | **2.5x - 2.8x faster**, but requires immediate P0 wraparound patch. | Reliable, battle-tested, but higher latency and GC churn. |

### B. Streaming Channels: `ZeroChannel<T>` vs `System.Threading.Channels`
| Metric / Feature | `ZeroChannel<T>` | `System.Threading.Channels` |
| :--- | :--- | :--- |
| **Throughput (.NET 8.0)** | **7.5M ops/s (133.2 ns)** | 6.1M ops/s (163.5 ns) |
| **Throughput (.NET 4.6.2)** | 7.0M ops/s (143.0 ns) | 7.2M ops/s (138.3 ns) |
| **Allocation per Message** | **0 Bytes** | **0 Bytes** |
| **Direct Promise Handoff** | Recyclable `ManualResetValueTaskSourceCore` | Internal reader/writer linked list queues |
| **Framework Independence** | Built into Zero ecosystem | Requires external NuGet package on .NET 4.6.2 |
| **Verdict** | **1.23x faster on .NET 8**, zero package baggage on .NET 4.6.2. | Rock-solid BCL standard authored by Stephen Toub. |

### C. High-Performance Disruptor: `DisruptorRing<T>` vs `Disruptor-net`
| Metric / Feature | `DisruptorRing<T>` | `Disruptor-net` (Port of LMAX) |
| :--- | :--- | :--- |
| **Implementation Complexity** | Single clean C# file (~315 lines) | Multi-project enterprise port (~15,000 lines) |
| **Throughput & Zero-Alloc** | In-place event mutation via `ref T this[seq]` | In-place event mutation via ring buffer array |
| **Wait Strategies** | `YieldingWaitStrategy`, `BusySpinWaitStrategy` | Blocking, Sleeping, Yielding, BusySpin, PhasedOff |
| **Audit Verdict** | **Excellent lightweight zero-dependency implementation** for embedded/pipeline tasks. | Full-featured LMAX port for ultra-complex multi-stage DAG event pipelines. |
