using System;
using ZeroPipeline.Core.Nodes;

namespace ZeroPipeline.Core.Ports
{
    /// <summary>
    /// Contract defining an input or output communication port on a pipeline node.
    /// </summary>
    public interface IPort
    {
        /// <summary>
        /// Unique port identifier within its owning node.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// The payload CLR data type accepted or emitted by this port.
        /// </summary>
        Type DataType { get; }

        /// <summary>
        /// Input or Output direction.
        /// </summary>
        PortDirection Direction { get; }

        /// <summary>
        /// The pipeline node that owns and hosts this port.
        /// </summary>
        IPipelineNode OwnerNode { get; }

        /// <summary>
        /// Indicates whether this port is connected to one or more peer ports.
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// Number of connected peer ports.
        /// </summary>
        int ConnectionCount { get; }
    }
}
