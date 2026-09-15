using System;
using System.Threading;
using System.Threading.Tasks;
using ZeroRfid.Core.Abstractions;
using ZeroRfid.Core.Codecs;
using ZeroRfid.Core.Models;

namespace ZeroRfid.Pipeline.Filtering;

/// <summary>
/// Suppresses tag reads that do not match a target EPC hexadecimal prefix.
/// </summary>
public sealed class EpcPrefixFilter : ITagStreamFilter
{
    public string NormalizedPrefix { get; }

    public EpcPrefixFilter(string prefixHex)
    {
        NormalizedPrefix = EpcHexCodec.Normalize(prefixHex);
    }

    public ValueTask<TagReadEvent?> ProcessAsync(TagReadEvent tagEvent, CancellationToken ct = default)
    {
        if (tagEvent == null)
            return new ValueTask<TagReadEvent?>((TagReadEvent?)null);

        if (tagEvent.Epc.StartsWith(NormalizedPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return new ValueTask<TagReadEvent?>(tagEvent);
        }

        return new ValueTask<TagReadEvent?>((TagReadEvent?)null);
    }
}
