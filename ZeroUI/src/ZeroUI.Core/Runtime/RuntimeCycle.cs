using System;

namespace ZeroUI.Core.Runtime
{
    /// <summary>
    /// Identifies the deterministic industrial execution cycle managed by <see cref="ZeroRuntime"/>.
    /// Guaranteed to execute in strict sequence when multiple cycles align on the same tick:
    /// Plc -> Logic -> Telemetry -> Historian -> Ui -> Animation -> Cleanup.
    /// </summary>
    public enum RuntimeCycle
    {
        /// <summary>
        /// Field PLC communications, Modbus register block polling, device frame ingestion (Default: 10 ms / 100 Hz).
        /// Executes on background worker thread.
        /// </summary>
        Plc = 0,

        /// <summary>
        /// Safety interlocks, trip logic, PackML state machines, inter-tag rules (Default: 10 ms / 100 Hz).
        /// Executes on background worker thread immediately following PLC ingestion.
        /// </summary>
        Logic = 1,

        /// <summary>
        /// Sliding-window aggregations (SMA, RMS, Min/Max), calculation passes, publishing to TripleBuffer (Default: 16 ms / ~60 Hz).
        /// Executes on background worker thread.
        /// </summary>
        Telemetry = 2,

        /// <summary>
        /// SQLite WAL buffer commits, store-and-forward batch flushes, multi-resolution pyramid rollups (Default: 100 ms / 10 Hz).
        /// Executes on background worker thread.
        /// </summary>
        Historian = 3,

        /// <summary>
        /// Coalesced dirty UI tag batch flush (FlushUiBatch), control data binding updates (Default: 16 ms / 60 Hz).
        /// Dispatched strictly to the UI STA thread.
        /// </summary>
        Ui = 4,

        /// <summary>
        /// Scene graph node animations (UpdateAnimation), rotating impellers, pipe dashes, flashing alarms (Default: 16 ms / 60 Hz).
        /// Dispatched strictly to the UI STA thread.
        /// </summary>
        Animation = 5,

        /// <summary>
        /// Historian ring buffer trims, expired alarm shelf resets, dead connection cleanups, memory maintenance (Default: 1000 ms / 1 Hz).
        /// Executes on background worker thread.
        /// </summary>
        Cleanup = 6
    }

    /// <summary>
    /// Operational execution mode for <see cref="ZeroRuntime"/>.
    /// </summary>
    public enum RuntimeMode
    {
        /// <summary>
        /// Real-time clock driven by high-resolution monotonic master timer.
        /// </summary>
        RealTime = 0,

        /// <summary>
        /// Deterministic virtual time advanced manually via Step() or AdvanceTime().
        /// Ideal for simulations, replays, and unit testing without thread sleeping.
        /// </summary>
        VirtualTime = 1
    }
}
