using System;
using System.Threading.Tasks;
using Xunit;
using ZeroRfid.Core.Models;
using ZeroRfid.Pipeline.Direction;

namespace ZeroRfid.Tests;

public class PortalGateDirectionTests
{
    [Fact]
    public async Task ProcessAsync_ResolvesInboundDirectionAccurately()
    {
        using var resolver = new PortalGateDirectionResolver(inboundAntenna: 1, outboundAntenna: 2, passageWindow: TimeSpan.FromSeconds(5));

        string epc = "E28011602000000000000099";
        DateTime t0 = DateTime.UtcNow;

        // Sequence representing a pallet moving from outside (Ant 1) to inside (Ant 2)
        // Ant 1 peaks at t0 + 100ms
        await resolver.ProcessAsync(new TagReadEvent(epc, 1, -60.0, "R1", t0));
        await resolver.ProcessAsync(new TagReadEvent(epc, 1, -45.0, "R1", t0.AddMilliseconds(100))); // Peak Ant 1
        await resolver.ProcessAsync(new TagReadEvent(epc, 1, -65.0, "R1", t0.AddMilliseconds(200)));

        // Ant 2 peaks at t0 + 400ms (later)
        await resolver.ProcessAsync(new TagReadEvent(epc, 2, -62.0, "R1", t0.AddMilliseconds(250)));
        await resolver.ProcessAsync(new TagReadEvent(epc, 2, -42.0, "R1", t0.AddMilliseconds(400))); // Peak Ant 2
        await resolver.ProcessAsync(new TagReadEvent(epc, 2, -58.0, "R1", t0.AddMilliseconds(500)));

        var evaluation = resolver.ResolveImmediate(epc);

        Assert.NotNull(evaluation);
        Assert.Equal(GateDirection.Inbound, evaluation!.Direction);
        Assert.True(evaluation.Confidence >= 0.85);
    }

    [Fact]
    public async Task ProcessAsync_ResolvesOutboundDirectionAccurately()
    {
        using var resolver = new PortalGateDirectionResolver(inboundAntenna: 1, outboundAntenna: 2, passageWindow: TimeSpan.FromSeconds(5));

        string epc = "E28011602000000000000088";
        DateTime t0 = DateTime.UtcNow;

        // Sequence representing movement from inside (Ant 2) to outside (Ant 1)
        // Ant 2 peaks at t0 + 100ms
        await resolver.ProcessAsync(new TagReadEvent(epc, 2, -43.0, "R1", t0.AddMilliseconds(100))); // Peak Ant 2

        // Ant 1 peaks at t0 + 350ms
        await resolver.ProcessAsync(new TagReadEvent(epc, 1, -44.0, "R1", t0.AddMilliseconds(350))); // Peak Ant 1

        var evaluation = resolver.ResolveImmediate(epc);

        Assert.NotNull(evaluation);
        Assert.Equal(GateDirection.Outbound, evaluation!.Direction);
        Assert.True(evaluation.Confidence >= 0.85);
    }
}
