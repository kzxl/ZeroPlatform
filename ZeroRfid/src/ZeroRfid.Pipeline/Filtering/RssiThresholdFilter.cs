using System.Threading;
using System.Threading.Tasks;
using ZeroRfid.Core.Abstractions;
using ZeroRfid.Core.Models;

namespace ZeroRfid.Pipeline.Filtering;

/// <summary>
/// Suppresses tag reads that fall below a configured minimum RSSI power threshold (in dBm).
/// </summary>
public sealed class RssiThresholdFilter : ITagStreamFilter
{
    public double MinRssiDbm { get; set; }

    public RssiThresholdFilter(double minRssiDbm)
    {
        MinRssiDbm = minRssiDbm;
    }

    public ValueTask<TagReadEvent?> ProcessAsync(TagReadEvent tagEvent, CancellationToken ct = default)
    {
        if (tagEvent == null || tagEvent.Rssi < MinRssiDbm)
        {
            return new ValueTask<TagReadEvent?>((TagReadEvent?)null);
        }

        return new ValueTask<TagReadEvent?>(tagEvent);
    }
}
