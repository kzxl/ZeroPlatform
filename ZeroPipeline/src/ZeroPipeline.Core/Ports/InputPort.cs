using System;
using System.Collections.Generic;
using System.Threading;
using ZeroPipeline.Core.Nodes;

namespace ZeroPipeline.Core.Ports
{
    /// <summary>
    /// Strongly-typed input communication port buffering data delivered from connected output ports.
    /// Supports bounded queue capacities, thread-safe synchronization, and configurable backpressure policies.
    /// </summary>
    /// <typeparam name="T">Payload data type.</typeparam>
    public sealed class InputPort<T> : IPort
    {
        private readonly Queue<DataPacket<T>> _queue;
        private readonly object _lock = new object();
        private int _connectionCount;

        public string Name { get; }
        public Type DataType => typeof(T);
        public PortDirection Direction => PortDirection.Input;
        public IPipelineNode OwnerNode { get; }
        public bool IsConnected => _connectionCount > 0;
        public int ConnectionCount => _connectionCount;

        public int Capacity { get; }
        public BackpressurePolicy Policy { get; }

        public int Count
        {
            get
            {
                lock (_lock) return _queue.Count;
            }
        }

        public bool HasData
        {
            get
            {
                lock (_lock) return _queue.Count > 0;
            }
        }

        public InputPort(
            IPipelineNode ownerNode,
            string name,
            int capacity = 16,
            BackpressurePolicy policy = BackpressurePolicy.Block)
        {
            OwnerNode = ownerNode ?? throw new ArgumentNullException(nameof(ownerNode));
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Capacity = capacity > 0 ? capacity : 16;
            Policy = policy;
            _queue = new Queue<DataPacket<T>>(Capacity);
        }

        /// <summary>
        /// Delivers a data packet into this input port following the designated BackpressurePolicy.
        /// </summary>
        public void Deliver(DataPacket<T> packet)
        {
            lock (_lock)
            {
                while (_queue.Count >= Capacity)
                {
                    switch (Policy)
                    {
                        case BackpressurePolicy.DropOldest:
                            _queue.Dequeue();
                            break;

                        case BackpressurePolicy.DropNewest:
                            return; // Discard incoming packet

                        case BackpressurePolicy.ThrowException:
                            throw new InvalidOperationException(
                                $"InputPort '{Name}' on node '{OwnerNode.Name}' queue overflow. Capacity: {Capacity}");

                        case BackpressurePolicy.Block:
                        default:
                            Monitor.Wait(_lock);
                            break;
                    }
                }

                _queue.Enqueue(packet);
                Monitor.PulseAll(_lock);
            }
        }

        /// <summary>
        /// Attempts to retrieve the next available packet without blocking.
        /// </summary>
        public bool TryReceive(out DataPacket<T> packet)
        {
            lock (_lock)
            {
                if (_queue.Count > 0)
                {
                    packet = _queue.Dequeue();
                    Monitor.PulseAll(_lock);
                    return true;
                }
            }

            packet = DataPacket<T>.Empty;
            return false;
        }

        /// <summary>
        /// Receives the next available packet, blocking until one arrives or timeout expires.
        /// </summary>
        public bool TryReceiveWait(int timeoutMilliseconds, out DataPacket<T> packet)
        {
            lock (_lock)
            {
                if (_queue.Count == 0)
                {
                    if (!Monitor.Wait(_lock, timeoutMilliseconds))
                    {
                        packet = DataPacket<T>.Empty;
                        return false;
                    }
                }

                if (_queue.Count > 0)
                {
                    packet = _queue.Dequeue();
                    Monitor.PulseAll(_lock);
                    return true;
                }
            }

            packet = DataPacket<T>.Empty;
            return false;
        }

        /// <summary>
        /// Peeks at the next available packet without dequeuing it.
        /// </summary>
        public bool TryPeek(out DataPacket<T> packet)
        {
            lock (_lock)
            {
                if (_queue.Count > 0)
                {
                    packet = _queue.Peek();
                    return true;
                }
            }

            packet = DataPacket<T>.Empty;
            return false;
        }

        /// <summary>
        /// Clears all buffered packets from the input queue.
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                _queue.Clear();
                Monitor.PulseAll(_lock);
            }
        }

        internal void IncrementConnection() => Interlocked.Increment(ref _connectionCount);
        internal void DecrementConnection() => Interlocked.Decrement(ref _connectionCount);

        public override string ToString() =>
            $"InputPort<{typeof(T).Name}>[{Name}, Buffered={Count}/{Capacity}]";
    }
}
