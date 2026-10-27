using System;
using System.Threading;
using System.Threading.Tasks;

namespace ZeroNetwork.PubSub
{
    /// <summary>
    /// Represents an active subscription token that can be disposed to cancel the subscription.
    /// </summary>
    public interface IZeroSubscription : IDisposable
    {
        /// <summary>
        /// Gets the subscribed topic pattern or type name.
        /// </summary>
        string Topic { get; }

        /// <summary>
        /// Gets whether the subscription is currently active.
        /// </summary>
        bool IsActive { get; }
    }

    /// <summary>
    /// Universal high-performance, zero-allocation Pub/Sub message bus abstraction for ZeroPlatform (Tier 2).
    /// Supports strongly-typed in-process events, hierarchical wildcard topic routing, and cross-process IPC.
    /// </summary>
    public interface IZeroBus : IDisposable
    {
        /// <summary>
        /// Publishes a strongly-typed message to all active subscribers of type <typeparamref name="T"/>.
        /// </summary>
        ValueTask PublishAsync<T>(T message, CancellationToken cancellationToken = default);

        /// <summary>
        /// Subscribes an asynchronous handler to strongly-typed messages of type <typeparamref name="T"/>.
        /// </summary>
        IZeroSubscription Subscribe<T>(Func<T, CancellationToken, ValueTask> handler);

        /// <summary>
        /// Subscribes a synchronous handler to strongly-typed messages of type <typeparamref name="T"/>.
        /// </summary>
        IZeroSubscription Subscribe<T>(Action<T> handler);

        /// <summary>
        /// Publishes a raw byte payload to a specific hierarchical topic (e.g. "plants/hanoi/line1/press").
        /// </summary>
        ValueTask PublishTopicAsync(string topic, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default);

        /// <summary>
        /// Subscribes an asynchronous handler to a topic pattern supporting wildcards ('+' for single level, '#' for multi-level).
        /// </summary>
        IZeroSubscription SubscribeTopic(string topicPattern, Func<string, ReadOnlyMemory<byte>, CancellationToken, ValueTask> handler);

        /// <summary>
        /// Subscribes a synchronous handler to a topic pattern supporting wildcards.
        /// </summary>
        IZeroSubscription SubscribeTopic(string topicPattern, Action<string, ReadOnlyMemory<byte>> handler);
    }
}
