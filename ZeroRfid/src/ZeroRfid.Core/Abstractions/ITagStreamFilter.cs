using System.Threading;
using System.Threading.Tasks;
using ZeroRfid.Core.Models;

namespace ZeroRfid.Core.Abstractions;

/// <summary>
/// Pipeline filter interceptor for processing, mutating, or suppressing tag read events in a stream.
/// </summary>
public interface ITagStreamFilter
{
    /// <summary>
    /// Processes an incoming tag read event. Returns null to suppress the event from downstream propagation.
    /// </summary>
    ValueTask<TagReadEvent?> ProcessAsync(TagReadEvent tagEvent, CancellationToken ct = default);
}
