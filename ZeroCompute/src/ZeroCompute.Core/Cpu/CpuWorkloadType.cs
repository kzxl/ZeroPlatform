using System;

namespace ZeroCompute.Core.Cpu
{
    /// <summary>
    /// Categorizes the arithmetic intensity and memory access pattern of a workload.
    /// Guides the runtime execution planner in worker core allocation, chunking, and SIMD dispatch.
    /// </summary>
    public enum CpuWorkloadType
    {
        /// <summary>
        /// Automatically infer or balance between compute and memory characteristics.
        /// </summary>
        Auto = 0,

        /// <summary>
        /// Arithmetic intensity is low (&lt; 0.5 FLOP/byte). Dominated by RAM and memory controller bandwidth.
        /// Worker allocation is capped to prevent memory bus saturation and cache thrashing.
        /// </summary>
        MemoryBound = 1,

        /// <summary>
        /// Arithmetic intensity is high (&gt; 2.0 FLOPs/byte). Dominated by CPU ALU and FPU registers.
        /// Scales across all available physical cores.
        /// </summary>
        ComputeBound = 2
    }
}
