using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace ZeroNetwork.RealTime
{
    /// <summary>
    /// Represents a target client or group of clients to invoke methods upon.
    /// </summary>
    public interface IClientProxy
    {
        /// <summary>
        /// Invokes a method on the target client(s) with the specified arguments.
        /// </summary>
        Task SendCoreAsync(string method, object?[]? args, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Extension methods for <see cref="IClientProxy"/> providing convenient typed overloads.
    /// </summary>
    public static class ClientProxyExtensions
    {
        public static Task SendAsync(this IClientProxy proxy, string method, CancellationToken cancellationToken = default)
            => proxy.SendCoreAsync(method, Array.Empty<object?>(), cancellationToken);

        public static Task SendAsync(this IClientProxy proxy, string method, object? arg1, CancellationToken cancellationToken = default)
            => proxy.SendCoreAsync(method, new[] { arg1 }, cancellationToken);

        public static Task SendAsync(this IClientProxy proxy, string method, object? arg1, object? arg2, CancellationToken cancellationToken = default)
            => proxy.SendCoreAsync(method, new[] { arg1, arg2 }, cancellationToken);

        public static Task SendAsync(this IClientProxy proxy, string method, object? arg1, object? arg2, object? arg3, CancellationToken cancellationToken = default)
            => proxy.SendCoreAsync(method, new[] { arg1, arg2, arg3 }, cancellationToken);
    }

    /// <summary>
    /// Accessor for groups, caller, and all connected clients.
    /// </summary>
    public interface IHubClients
    {
        IClientProxy All { get; }
        IClientProxy Client(string connectionId);
        IClientProxy Group(string groupName);
    }

    /// <summary>
    /// Accessor for caller and other clients during an active Hub invocation.
    /// </summary>
    public interface IHubCallerClients : IHubClients
    {
        IClientProxy Caller { get; }
        IClientProxy Others { get; }
    }

    /// <summary>
    /// Encapsulates context information for an active Hub caller connection.
    /// </summary>
    public sealed class HubCallerContext
    {
        public string ConnectionId { get; }
        public EndPoint? RemoteEndPoint { get; }
        public IDictionary<object, object?> Items { get; } = new ConcurrentDictionary<object, object?>();

        public HubCallerContext(string connectionId, EndPoint? remoteEndPoint)
        {
            ConnectionId = connectionId;
            RemoteEndPoint = remoteEndPoint;
        }
    }

    /// <summary>
    /// Manages client memberships within SignalR Hub groups.
    /// </summary>
    public interface IGroupManager
    {
        Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default);
        Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Base class for ASP.NET Core SignalR Protocol v1 compatible server hubs in ZeroPlatform.
    /// </summary>
    public abstract class ZeroHub : IDisposable
    {
        public IHubCallerClients Clients { get; internal set; } = null!;
        public HubCallerContext Context { get; internal set; } = null!;
        public IGroupManager Groups { get; internal set; } = null!;

        /// <summary>
        /// Called when a new connection is established with the hub.
        /// </summary>
        public virtual Task OnConnectedAsync() => Task.CompletedTask;

        /// <summary>
        /// Called when a connection is disconnected from the hub.
        /// </summary>
        public virtual Task OnDisconnectedAsync(Exception? exception) => Task.CompletedTask;

        public virtual void Dispose() { }
    }
}
