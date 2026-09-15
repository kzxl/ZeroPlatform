using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using ZeroRfid.Core.Models;
using ZeroRfid.Simulator;

namespace ZeroRfid.Tests;

public class SimulatorReaderTests
{
    [Fact]
    public async Task SimulatorReader_LifecycleAndStreaming_EmitsExpectedEvents()
    {
        var options = new SimulatorOptions
        {
            Scenario = SimulationScenario.StaticShelfInventory,
            TagCount = 5,
            EmissionIntervalMs = 10
        };

        await using var reader = new SimulatorRfidReader("SIM-01", options);
        Assert.Equal(ReaderState.Disconnected, reader.State);

        await reader.ConnectAsync();
        Assert.Equal(ReaderState.Connected, reader.State);

        var receivedTags = new List<TagReadEvent>();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        await reader.StartInventoryAsync();
        Assert.Equal(ReaderState.InventoryRunning, reader.State);

        try
        {
            await foreach (var tag in reader.ReadTagsAsync(cts.Token))
            {
                receivedTags.Add(tag);
                if (receivedTags.Count >= 10)
                    break;
            }
        }
        catch (OperationCanceledException)
        {
            // Expected upon timeout
        }

        await reader.StopInventoryAsync();
        Assert.Equal(ReaderState.Connected, reader.State);
        Assert.NotEmpty(receivedTags);
    }

    [Fact]
    public async Task SimulatorReader_MemoryAccessAndGpio_ExecutesSuccessfully()
    {
        string targetEpc = "30340001A1B2C3D4E5F60001";
        var options = new SimulatorOptions
        {
            CustomEpcs = new[] { targetEpc }
        };

        await using var reader = new SimulatorRfidReader("SIM-01", options);

        // 1. Memory Access
        byte[] userPayload = new byte[] { 0xAA, 0xBB, 0xCC, 0xDD };
        bool writeSuccess = await reader.WriteBankAsync(targetEpc, MemoryBank.User, startWord: 0, userPayload);
        Assert.True(writeSuccess);

        byte[] readBack = await reader.ReadBankAsync(targetEpc, MemoryBank.User, startWord: 0, wordCount: 2);
        Assert.Equal(0xAA, readBack[0]);
        Assert.Equal(0xBB, readBack[1]);
        Assert.Equal(0xCC, readBack[2]);
        Assert.Equal(0xDD, readBack[3]);

        // Rewrite EPC
        string newEpc = "E28011602000112233445566";
        bool rewriteEpc = await reader.WriteEpcAsync(targetEpc, newEpc);
        Assert.True(rewriteEpc);

        // 2. GPIO control
        await reader.SetGpoAsync(pinIndex: 1, isHigh: true);

        GpioPinChangedEvent? capturedGpi = null;
        reader.GpiChanged += (_, e) => capturedGpi = e;

        reader.SimulateGpiTrigger(pinIndex: 2, isHigh: true);
        Assert.NotNull(capturedGpi);
        Assert.Equal(2, capturedGpi!.PinIndex);
        Assert.True(capturedGpi.IsHigh);

        bool gpiState = await reader.GetGpiAsync(2);
        Assert.True(gpiState);
    }
}
