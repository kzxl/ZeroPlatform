# Benchmark Limitations and Experimental Boundaries

## 1. Test Environment Specifications
- **Operating System:** Windows 10 Pro (Build 19045), 64-bit
- **Hardware Architecture:** AMD/Intel x64, 12 Logical Processors
- **Primary Runtime Under Test:**
  - Modern: .NET 8.0.31 (JIT Tier1 / PGO enabled)
  - Legacy Target: .NET Framework 4.6.2 (CLR 4.0.30319.42000, 64-bit RyuJIT)

## 2. Inherent Measurement Limitations

### Microbenchmarks vs Production Workloads
- Microbenchmarks evaluate isolated CPU and memory hot paths in tight loops. Real-world applications feature L1/L2/L3 cache thrashing, context switches, thread preemption, and GC pauses from unrelated threads.
- In `AuditRunner.cs`, multi-threaded producer-consumer queues (`ZeroMpmcRingBuffer` and `ConcurrentQueue`) were tested with 4 concurrent producers and 4 consumers. Under higher core counts (e.g. 64-core AMD EPYC server), false sharing effects and cache line invalidation will amplify non-linearly.

### Stopwatch & GC.GetTotalMemory vs BenchmarkDotNet
- Due to the strict requirement of supporting and running both `.NET 8.0` and `.NET Framework 4.6.2` in a single unified audit harness on Windows, custom microbenchmarks leveraging `Stopwatch.GetTimestamp()` with forced full GCs (`GC.Collect(2, GCCollectionMode.Forced, true)`) were utilized.
- While this measures gross allocations and execution throughput accurately across millions of iterations, it does not isolate nanosecond-level P99 tail latency distributions or JIT warm-up variances with the statistical rigor of BenchmarkDotNet's multiple process isolation runs.

### Absence of Hardware Intrinsics on .NET Framework 4.6.2
- In .NET Framework 4.6.2, `System.Runtime.Intrinsics` does not exist. All SIMD benchmarks in `ZeroPrimitives.Core` gracefully fall back to scalar code or `System.Numerics.Vectors`, preventing direct measurement of AVX2/AVX-512 acceleration on the legacy framework.
