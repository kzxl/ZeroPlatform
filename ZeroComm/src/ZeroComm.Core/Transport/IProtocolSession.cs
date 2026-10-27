using System.Threading;
using System.Threading.Tasks;

namespace ZeroComm.Core.Transport
{
    /// <summary>
    /// Represents an application-layer industrial protocol session that requires handshake,
    /// authentication, or PDU negotiation upon physical transport connection or reconnection.
    /// Enables automatic session recovery when the underlying socket reconnects.
    /// </summary>
    public interface IProtocolSession
    {
        /// <summary>
        /// Invoked when the physical transport connection is established (or re-established)
        /// and application-layer session negotiation must take place.
        /// </summary>
        Task OnSessionConnectedAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Invoked when the physical connection is severed or about to be closed.
        /// </summary>
        Task OnSessionDisconnectedAsync();
    }
}
