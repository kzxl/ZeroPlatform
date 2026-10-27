using System;
using System.Net;
using System.Text;
using ZeroPrimitives.Core.Identifiers;

namespace ZeroNetwork.Rpc
{
    /// <summary>
    /// Represents the result received from a <see cref="ZeroRpcClient"/> invocation.
    /// </summary>
    public readonly struct ZeroRpcResponse
    {
        public FastUlid CorrelationId { get; }
        public ushort MethodId { get; }
        public ZeroRpcStatus Status { get; }
        public ReadOnlyMemory<byte> Payload { get; }

        public bool IsSuccess => Status == ZeroRpcStatus.Ok;

        public ZeroRpcResponse(FastUlid correlationId, ushort methodId, ZeroRpcStatus status, ReadOnlyMemory<byte> payload)
        {
            CorrelationId = correlationId;
            MethodId = methodId;
            Status = status;
            Payload = payload;
        }

        public string GetErrorString()
        {
            if (Payload.Length == 0) return Status.ToString();
            try
            {
                unsafe
                {
                    fixed (byte* p = Payload.Span)
                    {
                        return Encoding.UTF8.GetString(p, Payload.Length);
                    }
                }
            }
            catch
            {
                return Status.ToString();
            }
        }
    }

    /// <summary>
    /// Contextual state for a single server-side RPC invocation.
    /// </summary>
    public sealed class ZeroRpcContext
    {
        public ZeroRpcHeader Header { get; }
        public ReadOnlyMemory<byte> Payload { get; }
        public EndPoint? RemoteEndPoint { get; }

        public ushort MethodId => Header.MethodId;
        public FastUlid CorrelationId => Header.CorrelationId;
        public bool IsOneWay => Header.IsOneWay;

        public ZeroRpcContext(ZeroRpcHeader header, ReadOnlyMemory<byte> payload, EndPoint? remoteEndPoint)
        {
            Header = header;
            Payload = payload;
            RemoteEndPoint = remoteEndPoint;
        }

        /// <summary>
        /// Creates a successful response payload.
        /// </summary>
        public static (ZeroRpcStatus Status, ReadOnlyMemory<byte> Payload) Success(ReadOnlyMemory<byte> payload)
        {
            return (ZeroRpcStatus.Ok, payload);
        }

        /// <summary>
        /// Creates an error response with an optional error message string.
        /// </summary>
        public static (ZeroRpcStatus Status, ReadOnlyMemory<byte> Payload) Error(ZeroRpcStatus status, string? errorMessage = null)
        {
            ReadOnlyMemory<byte> payload = string.IsNullOrEmpty(errorMessage)
                ? ReadOnlyMemory<byte>.Empty
                : Encoding.UTF8.GetBytes(errorMessage);
            return (status, payload);
        }
    }
}
