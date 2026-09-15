using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using ZeroRfid.Core.Models;
using ZeroRfid.Pipeline.Deduplication;

namespace ZeroRfid.Tests;

public class DeduplicationFilterTests
{
    [Fact]
    public async Task ProcessAsync_EmitsFirstReadAndSuppressesSubsequentDuplicatesInWindow()
    {
        var options = new DeduplicationOptions
        {
            SlidingWindow = TimeSpan.FromMilliseconds(500),
            RssiMode = RssiAggregationMode.Maximum
        };

        using var filter = new TagDeduplicationFilter(options, enableDisappearanceTimer: false);

        var appearedList = new List<TagReadEvent>();
        filter.TagAppeared += (_, e) => appearedList.Add(e);

        string epc = "E28011602000000000000001";
        DateTime t0 = DateTime.UtcNow;

        // 1st detection -> should emit
        var evt1 = new TagReadEvent(epc, antennaId: 1, rssi: -55.0, readerId: "R1", timestampUtc: t0);
        var res1 = await filter.ProcessAsync(evt1);
        Assert.NotNull(res1);
        Assert.Single(appearedList);

        // 2nd detection 50ms later -> duplicate within window -> should return null
        var evt2 = new TagReadEvent(epc, antennaId: 1, rssi: -50.0, readerId: "R1", timestampUtc: t0.AddMilliseconds(50));
        var res2 = await filter.ProcessAsync(evt2);
        Assert.Null(res2);
        Assert.Single(appearedList); // no second appeared event

        // 3rd detection 100ms later -> duplicate -> should return null
        var evt3 = new TagReadEvent(epc, antennaId: 1, rssi: -52.0, readerId: "R1", timestampUtc: t0.AddMilliseconds(100));
        var res3 = await filter.ProcessAsync(evt3);
        Assert.Null(res3);
        Assert.Single(appearedList);

        // 4th detection 700ms later -> window expired (>500ms) -> should emit again
        var evt4 = new TagReadEvent(epc, antennaId: 1, rssi: -54.0, readerId: "R1", timestampUtc: t0.AddMilliseconds(700));
        var res4 = await filter.ProcessAsync(evt4);
        Assert.NotNull(res4);
        Assert.Equal(2, appearedList.Count);
    }
}
