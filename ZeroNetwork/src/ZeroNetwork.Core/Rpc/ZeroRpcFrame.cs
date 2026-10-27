using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ZeroPrimitives.Core.Identifiers;

namespace ZeroNetwork.Rpc
{
    /// <summary>
    /// Flags indicating the purpose and characteristics of a <see cref="ZeroRpcHeader"/>.
    /// </summary>
    [Flags]
    public enum ZeroRpcFlags : byte
    {
        None = 0,
        Request = 1 << 0,   // 0x01: Request requiring a response
        Response = 1 << 1,  // 0x02: Response to a preceding request
        Error = 1 << 2,     // 0x04: Execution error indication
        OneWay = 1 << 3,    // 0x08: Fire-and-forget event without response
        Heartbeat = 1 << 4  // 0x10: Connection keep-alive ping/pong
    }

    /// <summary>
    /// Standardized RPC status codes.
    /// </summary>
    public enum ZeroRpcStatus : ushort
    {
        Ok = 0,
        BadRequest = 400,
        Unauthorized = 401,
        NotFound = 404,
        Timeout = 408,
        TooManyRequests = 429,
        InternalError = 500
    }

    /// <summary>
    /// 32-byte cache-line friendly binary RPC frame header.
    /// Packed without padding to ensure zero-allocation binary wire serialization.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = HeaderLength)]
    public struct ZeroRpcHeader
    {
        public const ushort ExpectedMagic = 0x5A52; // "ZR" (ZeroRpc)
        public const byte CurrentVersion = 1;
        public const int HeaderLength = 32;

        [FieldOffset(0)]  public ushort Magic;
        [FieldOffset(2)]  public byte   Version;
        [FieldOffset(3)]  public byte   Flags;
        [FieldOffset(4)]  public ushort MethodId;
        [FieldOffset(6)]  public ushort StatusCode;
        [FieldOffset(8)]  public FastUlid CorrelationId;
        [FieldOffset(24)] public int    PayloadLength;
        [FieldOffset(28)] public uint   Checksum;

        public bool IsRequest => (Flags & (byte)ZeroRpcFlags.Request) != 0;
        public bool IsResponse => (Flags & (byte)ZeroRpcFlags.Response) != 0;
        public bool IsError => (Flags & (byte)ZeroRpcFlags.Error) != 0;
        public bool IsOneWay => (Flags & (byte)ZeroRpcFlags.OneWay) != 0;
        public bool IsHeartbeat => (Flags & (byte)ZeroRpcFlags.Heartbeat) != 0;

        /// <summary>
        /// Creates a request header with an auto-generated <see cref="FastUlid"/> correlation identifier.
        /// </summary>
        public static ZeroRpcHeader CreateRequest(ushort methodId, int payloadLength, uint checksum = 0, FastUlid correlationId = default)
        {
            return new ZeroRpcHeader
            {
                Magic = ExpectedMagic,
                Version = CurrentVersion,
                Flags = (byte)ZeroRpcFlags.Request,
                MethodId = methodId,
                StatusCode = (ushort)ZeroRpcStatus.Ok,
                CorrelationId = correlationId == default ? FastUlid.NewUlid() : correlationId,
                PayloadLength = payloadLength,
                Checksum = checksum
            };
        }

        /// <summary>
        /// Creates a response header matching a request's <see cref="FastUlid"/> correlation identifier.
        /// </summary>
        public static ZeroRpcHeader CreateResponse(FastUlid correlationId, ushort methodId, int payloadLength, ZeroRpcStatus status = ZeroRpcStatus.Ok, uint checksum = 0)
        {
            byte flags = (byte)ZeroRpcFlags.Response;
            if (status != ZeroRpcStatus.Ok)
            {
                flags |= (byte)ZeroRpcFlags.Error;
            }

            return new ZeroRpcHeader
            {
                Magic = ExpectedMagic,
                Version = CurrentVersion,
                Flags = flags,
                MethodId = methodId,
                StatusCode = (ushort)status,
                CorrelationId = correlationId,
                PayloadLength = payloadLength,
                Checksum = checksum
            };
        }

        /// <summary>
        /// Creates a one-way fire-and-forget event header.
        /// </summary>
        public static ZeroRpcHeader CreateOneWay(ushort methodId, int payloadLength, uint checksum = 0)
        {
            return new ZeroRpcHeader
            {
                Magic = ExpectedMagic,
                Version = CurrentVersion,
                Flags = (byte)ZeroRpcFlags.OneWay,
                MethodId = methodId,
                StatusCode = (ushort)ZeroRpcStatus.Ok,
                CorrelationId = FastUlid.NewUlid(),
                PayloadLength = payloadLength,
                Checksum = checksum
            };
        }

        /// <summary>
        /// Creates a heartbeat ping/pong header.
        /// </summary>
        public static ZeroRpcHeader CreateHeartbeat(bool isPong = false, FastUlid correlationId = default)
        {
            byte flags = (byte)ZeroRpcFlags.Heartbeat;
            if (isPong) flags |= (byte)ZeroRpcFlags.Response;
            else flags |= (byte)ZeroRpcFlags.Request;

            return new ZeroRpcHeader
            {
                Magic = ExpectedMagic,
                Version = CurrentVersion,
                Flags = flags,
                MethodId = 0,
                StatusCode = (ushort)ZeroRpcStatus.Ok,
                CorrelationId = correlationId == default ? FastUlid.NewUlid() : correlationId,
                PayloadLength = 0,
                Checksum = 0
            };
        }

        /// <summary>
        /// Encodes this header into a destination byte span without allocations.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Encode(Span<byte> destination)
        {
            if (destination.Length < HeaderLength)
                throw new ArgumentException($"Destination span too small for header (requires {HeaderLength} bytes).", nameof(destination));

            BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(0, 2), Magic);
            destination[2] = Version;
            destination[3] = Flags;
            BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(4, 2), MethodId);
            BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(6, 2), StatusCode);

            CorrelationId.TryWriteBytes(destination.Slice(8, 16));

            BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(24, 4), PayloadLength);
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(28, 4), Checksum);
        }

        /// <summary>
        /// Attempts to decode a 32-byte header from a source byte span.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryDecode(ReadOnlySpan<byte> source, out ZeroRpcHeader header)
        {
            header = default;
            if (source.Length < HeaderLength)
                return false;

            ushort magic = BinaryPrimitives.ReadUInt16LittleEndian(source.Slice(0, 2));
            if (magic != ExpectedMagic)
                return false;

            byte version = source[2];
            byte flags = source[3];
            ushort methodId = BinaryPrimitives.ReadUInt16LittleEndian(source.Slice(4, 2));
            ushort status = BinaryPrimitives.ReadUInt16LittleEndian(source.Slice(6, 2));

            var correlationId = new FastUlid(source.Slice(8, 16));
            int payloadLength = BinaryPrimitives.ReadInt32LittleEndian(source.Slice(24, 4));
            uint checksum = BinaryPrimitives.ReadUInt32LittleEndian(source.Slice(28, 4));

            header = new ZeroRpcHeader
            {
                Magic = magic,
                Version = version,
                Flags = flags,
                MethodId = methodId,
                StatusCode = status,
                CorrelationId = correlationId,
                PayloadLength = payloadLength,
                Checksum = checksum
            };
            return true;
        }
    }
}
