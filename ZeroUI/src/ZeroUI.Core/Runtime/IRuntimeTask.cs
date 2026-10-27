using System;

namespace ZeroUI.Core.Runtime
{
    /// <summary>
    /// Contract for class-based deterministic runtime tasks.
    /// Provides zero-allocation execution during high-frequency cycles (avoiding closure delegate allocations).
    /// </summary>
    public interface IRuntimeTask
    {
        /// <summary>
        /// Executes a single cycle slice.
        /// </summary>
        /// <param name="delta">Elapsed time since the last cycle execution.</param>
        /// <param name="cycleIndex">Monotonically increasing execution counter for this specific cycle.</param>
        void Execute(TimeSpan delta, long cycleIndex);
    }
}
