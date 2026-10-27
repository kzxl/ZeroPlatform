using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using ZeroNetwork.RealTime;

namespace ZeroNetwork.PubSub
{
    /// <summary>
    /// Sovereign, out-of-the-box SignalR Protocol v1 Hub for topic-based Pub/Sub across Web, Desktop, and Edge devices.
    /// Bridges remote WebSocket clients to the high-performance local <see cref="InProcessZeroBus"/>.
    /// </summary>
    public class ZeroPubSubHub : ZeroHub
    {
        private static readonly InProcessZeroBus SharedBus = new InProcessZeroBus();
        private static readonly ConcurrentDictionary<string, List<IZeroSubscription>> ClientSubscriptions =
            new ConcurrentDictionary<string, List<IZeroSubscription>>();

        /// <summary>
        /// Subscribes the calling client to a topic pattern (supporting '+' and '#' wildcards).
        /// When a message matching this pattern is published, the client receives an 'OnTopicMessage' invocation.
        /// </summary>
        /// <param name="topicPattern">Topic pattern, e.g. "plants/+/sensors/temperature" or "alarms/#".</param>
        public Task Subscribe(string topicPattern)
        {
            if (string.IsNullOrEmpty(topicPattern))
                throw new ArgumentNullException(nameof(topicPattern));

            string callerId = Context.ConnectionId;
            var sub = SharedBus.SubscribeTopic(topicPattern, async (topic, payload) =>
            {
                string text = Encoding.UTF8.GetString(payload.ToArray());
                await Clients.Client(callerId).SendAsync("OnTopicMessage", topic, text).ConfigureAwait(false);
            });

            var list = ClientSubscriptions.GetOrAdd(callerId, _ => new List<IZeroSubscription>());
            lock (list)
            {
                list.Add(sub);
            }

            return Groups.AddToGroupAsync(callerId, topicPattern);
        }

        /// <summary>
        /// Publishes a payload string to a topic across all local and remote subscribers.
        /// </summary>
        /// <param name="topic">Concrete topic name.</param>
        /// <param name="payload">Payload string (UTF-8 encoded).</param>
        public async Task Publish(string topic, string payload)
        {
            if (string.IsNullOrEmpty(topic))
                throw new ArgumentNullException(nameof(topic));

            byte[] bytes = Encoding.UTF8.GetBytes(payload ?? string.Empty);
            await SharedBus.PublishTopicAsync(topic, bytes).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public override Task OnDisconnectedAsync(Exception? exception)
        {
            if (ClientSubscriptions.TryRemove(Context.ConnectionId, out var subs))
            {
                lock (subs)
                {
                    for (int i = 0; i < subs.Count; i++)
                    {
                        subs[i].Dispose();
                    }
                    subs.Clear();
                }
            }
            return Task.CompletedTask;
        }
    }
}
