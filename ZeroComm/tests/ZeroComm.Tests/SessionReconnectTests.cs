using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using ZeroComm.Core.Transport;

namespace ZeroComm.Tests
{
    public class SessionReconnectTests
    {
        private class MockSession : IProtocolSession
        {
            public int ConnectCalls { get; private set; }
            public int DisconnectCalls { get; private set; }

            public Task OnSessionConnectedAsync(CancellationToken cancellationToken = default)
            {
                ConnectCalls++;
                return Task.CompletedTask;
            }

            public Task OnSessionDisconnectedAsync()
            {
                DisconnectCalls++;
                return Task.CompletedTask;
            }
        }

        [Fact]
        public void AttachSession_AttachesAndDetachesCorrectly()
        {
            var transport = new AsyncTcpTransport("127.0.0.1", 102);
            var session = new MockSession();

            transport.AttachSession(session);
            Assert.Same(session, transport.AttachedSession);

            transport.DetachSession();
            Assert.Null(transport.AttachedSession);
        }

        [Fact]
        public async Task ProtocolSession_ReceivesNotifications()
        {
            var session = new MockSession();

            await session.OnSessionConnectedAsync();
            Assert.Equal(1, session.ConnectCalls);

            await session.OnSessionDisconnectedAsync();
            Assert.Equal(1, session.DisconnectCalls);
        }
    }
}
