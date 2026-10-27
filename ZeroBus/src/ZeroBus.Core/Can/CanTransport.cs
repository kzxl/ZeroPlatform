using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace ZeroBus.Can
{
    public interface ICanTransport : IDisposable
    {
        bool IsConnected { get; }
        event Action<CanFrame>? OnFrameReceived;
        event Action<Exception>? OnError;

        Task ConnectAsync(CancellationToken cancellationToken = default);
        Task DisconnectAsync(CancellationToken cancellationToken = default);
        Task SendFrameAsync(CanFrame frame, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Bitwise Mask-and-Match CAN hardware acceptance filter.
    /// (Frame.Id &amp; Mask) == Match.
    /// </summary>
    public struct CanFilter
    {
        public uint Mask;
        public uint Match;
        public bool ExtendedOnly;

        public CanFilter(uint mask, uint match, bool extendedOnly = false)
        {
            Mask = mask;
            Match = match;
            ExtendedOnly = extendedOnly;
        }

        public bool Accepts(CanFrame frame)
        {
            if (ExtendedOnly && !frame.IsExtended) return false;
            return (frame.Id & Mask) == Match;
        }
    }

    /// <summary>
    /// High-speed lock-free virtual in-memory CAN bus transport.
    /// Supports multi-node simulation, loopback, and message broadcasting.
    /// </summary>
    public class VirtualCanTransport : ICanTransport
    {
        private readonly VirtualCanBus? _sharedBus;
        private readonly BlockingCollection<CanFrame> _rxQueue = new BlockingCollection<CanFrame>();
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private bool _isConnected;
        private bool _disposed;

        public bool IsConnected => _isConnected;
        public event Action<CanFrame>? OnFrameReceived;
#pragma warning disable CS0067
        public event Action<Exception>? OnError;
#pragma warning restore CS0067

        public VirtualCanTransport(VirtualCanBus? bus = null)
        {
            _sharedBus = bus;
        }

        public Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            _isConnected = true;
            _sharedBus?.Register(this);

            Task.Run(ProcessRxQueue);
            return Task.CompletedTask;
        }

        public Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            _isConnected = false;
            _sharedBus?.Unregister(this);
            return Task.CompletedTask;
        }

        public Task SendFrameAsync(CanFrame frame, CancellationToken cancellationToken = default)
        {
            if (!_isConnected) throw new InvalidOperationException("CAN transport is not connected.");

            if (_sharedBus != null)
            {
                _sharedBus.Broadcast(frame, sender: this);
            }
            else
            {
                // Self loopback
                Deliver(frame);
            }

            return Task.CompletedTask;
        }

        internal void Deliver(CanFrame frame)
        {
            if (_isConnected && !_rxQueue.IsAddingCompleted)
            {
                _rxQueue.TryAdd(frame);
            }
        }

        private void ProcessRxQueue()
        {
            try
            {
                while (!_cts.Token.IsCancellationRequested && !_rxQueue.IsCompleted)
                {
                    if (_rxQueue.TryTake(out var frame, 50, _cts.Token))
                    {
                        OnFrameReceived?.Invoke(frame);
                    }
                }
            }
            catch (OperationCanceledException) { }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _isConnected = false;
            _cts.Cancel();
            _rxQueue.CompleteAdding();
            _sharedBus?.Unregister(this);
            _cts.Dispose();
            _rxQueue.Dispose();
        }
    }

    /// <summary>
    /// Virtual bus router interconnecting multiple VirtualCanTransport nodes.
    /// </summary>
    public class VirtualCanBus
    {
        private readonly ConcurrentDictionary<VirtualCanTransport, byte> _nodes =
            new ConcurrentDictionary<VirtualCanTransport, byte>();

        public void Register(VirtualCanTransport node) => _nodes.TryAdd(node, 0);
        public void Unregister(VirtualCanTransport node) => _nodes.TryRemove(node, out _);

        public void Broadcast(CanFrame frame, VirtualCanTransport sender)
        {
            foreach (var node in _nodes.Keys)
            {
                if (node != sender && node.IsConnected)
                {
                    node.Deliver(frame);
                }
            }
        }
    }
}
