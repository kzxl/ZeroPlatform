using System;

namespace ZeroPipeline.Core.Ports
{
    /// <summary>
    /// Lightweight immutable value wrapper carrying typed data and execution metadata across pipeline nodes.
    /// Eliminates GC allocations when passed between connected ports.
    /// </summary>
    /// <typeparam name="T">Payload data type.</typeparam>
    public readonly struct DataPacket<T>
    {
        public T Payload { get; }
        public long SequenceNumber { get; }
        public long TimestampTicks { get; }
        public bool IsEndOfStream { get; }
        public bool HasValue { get; }

        public DataPacket(T payload, long sequenceNumber = 0, bool isEndOfStream = false)
        {
            Payload = payload;
            SequenceNumber = sequenceNumber;
            TimestampTicks = DateTime.UtcNow.Ticks;
            IsEndOfStream = isEndOfStream;
            HasValue = true;
        }

        public static readonly DataPacket<T> Empty = new DataPacket<T>(default!, 0, false);

        public static DataPacket<T> EndOfStream(long sequenceNumber = 0) =>
            new DataPacket<T>(default!, sequenceNumber, isEndOfStream: true);

        public override string ToString() =>
            IsEndOfStream ? "DataPacket[EndOfStream]" : $"DataPacket<{typeof(T).Name}>[Seq={SequenceNumber}]";
    }
}
