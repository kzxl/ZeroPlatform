#if NETFRAMEWORK
using System;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace ZeroUI.Core.Communication
{
    internal static class SocketTaskExtensions
    {
        public static Task ConnectAsync(this Socket socket, string host, int port)
        {
            if (socket == null) throw new ArgumentNullException(nameof(socket));
            return Task.Factory.FromAsync(
                (h, p, callback, state) => ((Socket)state).BeginConnect(h, p, callback, state),
                asyncResult => ((Socket)asyncResult.AsyncState).EndConnect(asyncResult),
                host, port, state: socket);
        }

        public static Task<int> SendAsync(this Socket socket, ArraySegment<byte> buffer, SocketFlags socketFlags)
        {
            if (socket == null) throw new ArgumentNullException(nameof(socket));
            return Task.Factory.FromAsync(
                (b, f, callback, state) => ((Socket)state).BeginSend(b.Array, b.Offset, b.Count, f, callback, state),
                asyncResult => ((Socket)asyncResult.AsyncState).EndSend(asyncResult),
                buffer, socketFlags, state: socket);
        }

        public static Task<int> ReceiveAsync(this Socket socket, ArraySegment<byte> buffer, SocketFlags socketFlags)
        {
            if (socket == null) throw new ArgumentNullException(nameof(socket));
            return Task.Factory.FromAsync(
                (b, f, callback, state) => ((Socket)state).BeginReceive(b.Array, b.Offset, b.Count, f, callback, state),
                asyncResult => ((Socket)asyncResult.AsyncState).EndReceive(asyncResult),
                buffer, socketFlags, state: socket);
        }
    }
}
#endif
