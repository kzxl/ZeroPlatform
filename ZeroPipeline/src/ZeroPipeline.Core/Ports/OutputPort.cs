using System;
using System.Collections.Generic;
using ZeroPipeline.Core.Nodes;

namespace ZeroPipeline.Core.Ports
{
    /// <summary>
    /// Strongly-typed output communication port that broadcasts emitted data packets to all connected input ports.
    /// Supports high-speed fan-out with zero buffer copying across the pipeline topology.
    /// </summary>
    /// <typeparam name="T">Payload data type.</typeparam>
    public sealed class OutputPort<T> : IPort
    {
        private readonly List<InputPort<T>> _targets;
        private readonly object _lock = new object();

        public string Name { get; }
        public Type DataType => typeof(T);
        public PortDirection Direction => PortDirection.Output;
        public IPipelineNode OwnerNode { get; }
        public bool IsConnected
        {
            get
            {
                lock (_lock) return _targets.Count > 0;
            }
        }

        public int ConnectionCount
        {
            get
            {
                lock (_lock) return _targets.Count;
            }
        }

        public OutputPort(IPipelineNode ownerNode, string name)
        {
            OwnerNode = ownerNode ?? throw new ArgumentNullException(nameof(ownerNode));
            Name = name ?? throw new ArgumentNullException(nameof(name));
            _targets = new List<InputPort<T>>();
        }

        /// <summary>
        /// Connects this output port to a target input port.
        /// </summary>
        public void ConnectTo(InputPort<T> targetInput)
        {
            if (targetInput == null) throw new ArgumentNullException(nameof(targetInput));

            lock (_lock)
            {
                if (!_targets.Contains(targetInput))
                {
                    _targets.Add(targetInput);
                    targetInput.IncrementConnection();
                }
            }
        }

        /// <summary>
        /// Disconnects this output port from a target input port.
        /// </summary>
        public bool Disconnect(InputPort<T> targetInput)
        {
            if (targetInput == null) return false;

            lock (_lock)
            {
                if (_targets.Remove(targetInput))
                {
                    targetInput.DecrementConnection();
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Emits a new payload value wrapped in a DataPacket, broadcasting to all connected target input ports.
        /// </summary>
        public void Emit(T payload, long sequenceNumber = 0)
        {
            EmitPacket(new DataPacket<T>(payload, sequenceNumber));
        }

        /// <summary>
        /// Broadcasts an existing DataPacket to all connected target input ports.
        /// </summary>
        public void EmitPacket(DataPacket<T> packet)
        {
            InputPort<T>[] targetsSnapshot;
            lock (_lock)
            {
                if (_targets.Count == 0) return;
                targetsSnapshot = _targets.ToArray();
            }

            for (int i = 0; i < targetsSnapshot.Length; i++)
            {
                targetsSnapshot[i].Deliver(packet);
            }
        }

        /// <summary>
        /// Broadcasts an EndOfStream sentinel packet to all connected target input ports.
        /// </summary>
        public void EmitEndOfStream(long sequenceNumber = 0)
        {
            EmitPacket(DataPacket<T>.EndOfStream(sequenceNumber));
        }

        public override string ToString() =>
            $"OutputPort<{typeof(T).Name}>[{Name}, FanOut={ConnectionCount}]";
    }
}
