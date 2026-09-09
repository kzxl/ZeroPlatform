using System;

namespace ZeroPipeline.Core.Ports
{
    /// <summary>
    /// Direction of a pipeline port.
    /// </summary>
    public enum PortDirection
    {
        Input = 0,
        Output = 1
    }

    /// <summary>
    /// Policy defining behavior when an input port's queue capacity is exceeded.
    /// </summary>
    public enum BackpressurePolicy
    {
        /// <summary>
        /// Blocks the delivering thread or caller until queue space becomes available.
        /// </summary>
        Block = 0,

        /// <summary>
        /// Drops the oldest packet in the queue to make space for the new incoming packet.
        /// </summary>
        DropOldest = 1,

        /// <summary>
        /// Discards the incoming packet, preserving existing buffered items.
        /// </summary>
        DropNewest = 2,

        /// <summary>
        /// Throws an InvalidOperationException when the queue overflows.
        /// </summary>
        ThrowException = 3
    }
}
