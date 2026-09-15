using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using ZeroRfid.Core.Abstractions;
using ZeroRfid.Core.Codecs;
using ZeroRfid.Core.Models;

namespace ZeroRfid.Simulator;

/// <summary>
/// Sovereign virtual RFID reader simulating continuous inventory streaming, memory bank operations, and GPIO pins.
/// </summary>
public sealed class SimulatorRfidReader : IRfidReader, IRfidMemoryAccess, IRfidGpioController
{
    private sealed class SimulatedTagMemory
    {
        public string Epc { get; set; }
        public string Tid { get; set; }
        public byte[] UserMemory { get; set; } = new byte[64];
        public string? AccessPassword { get; set; }
        public bool IsLocked { get; set; }
        public bool IsKilled { get; set; }

        public SimulatedTagMemory(string epc, string tid)
        {
            Epc = epc;
            Tid = tid;
        }
    }

    private readonly SimulatorOptions _options;
    private readonly Channel<TagReadEvent> _channel;
    private readonly ConcurrentDictionary<string, SimulatedTagMemory> _tags = new(StringComparer.OrdinalIgnoreCase);
    private readonly bool[] _gpoPins = new bool[8];
    private readonly bool[] _gpiPins = new bool[8];
    private readonly object _stateLock = new();

    private int _stateInt = (int)ReaderState.Disconnected;
    private CancellationTokenSource? _inventoryCts;
    private Task? _inventoryTask;

    public string ReaderId { get; }
    public ReaderState State => (ReaderState)_stateInt;

    public RfidCapabilities Capabilities =>
        RfidCapabilities.InventoryStreaming |
        RfidCapabilities.MemoryRead |
        RfidCapabilities.MemoryWrite |
        RfidCapabilities.GpioControl |
        RfidCapabilities.LockAndKill |
        RfidCapabilities.RssiReporting;

    public event EventHandler<TagReadEvent>? TagRead;
    public event EventHandler<ReaderState>? StateChanged;
    public event EventHandler<GpioPinChangedEvent>? GpiChanged;

    public SimulatorRfidReader(string readerId = "SIM-READER-01", SimulatorOptions? options = null)
    {
        ReaderId = readerId;
        _options = options ?? SimulatorOptions.Default;
        _channel = Channel.CreateBounded<TagReadEvent>(new BoundedChannelOptions(5000)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = false,
            SingleWriter = true
        });

        InitializeSimulatedTags();
    }

    public Task ConnectAsync(CancellationToken ct = default)
    {
        lock (_stateLock)
        {
            if (State == ReaderState.Connected || State == ReaderState.InventoryRunning)
                return Task.CompletedTask;

            SetState(ReaderState.Connecting);
            SetState(ReaderState.Connected);
        }
        return Task.CompletedTask;
    }

    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        lock (_stateLock)
        {
            if (State == ReaderState.Disconnected)
                return;
        }

        await StopInventoryAsync(ct).ConfigureAwait(false);
        SetState(ReaderState.Disconnected);
    }

    public Task StartInventoryAsync(InventorySettings? settings = null, CancellationToken ct = default)
    {
        lock (_stateLock)
        {
            if (State != ReaderState.Connected)
                throw new InvalidOperationException($"Cannot start inventory when reader is in state '{State}'.");

            SetState(ReaderState.InventoryRunning);
            _inventoryCts = new CancellationTokenSource();
            _inventoryTask = Task.Run(() => RunSimulationLoopAsync(_inventoryCts.Token));
        }

        return Task.CompletedTask;
    }

    public async Task StopInventoryAsync(CancellationToken ct = default)
    {
        CancellationTokenSource? cts;
        Task? task;

        lock (_stateLock)
        {
            if (State != ReaderState.InventoryRunning)
                return;

            cts = _inventoryCts;
            task = _inventoryTask;
            _inventoryCts = null;
            _inventoryTask = null;
            SetState(ReaderState.Connected);
        }

        if (cts != null)
        {
            cts.Cancel();
            if (task != null)
            {
                try { await task.ConfigureAwait(false); } catch (OperationCanceledException) { }
            }
            cts.Dispose();
        }
    }

    public async IAsyncEnumerable<TagReadEvent> ReadTagsAsync([EnumeratorCancellation] CancellationToken ct = default)
    {
        while (await _channel.Reader.WaitToReadAsync(ct).ConfigureAwait(false))
        {
            while (_channel.Reader.TryRead(out var tag))
            {
                yield return tag;
            }
        }
    }

    #region IRfidMemoryAccess Implementation

    public Task<byte[]> ReadBankAsync(string epc, MemoryBank bank, ushort startWord, ushort wordCount, string? accessPassword = null, CancellationToken ct = default)
    {
        string norm = EpcHexCodec.Normalize(epc);
        if (!_tags.TryGetValue(norm, out var tag) || tag.IsKilled)
            throw new KeyNotFoundException($"Tag with EPC '{epc}' was not found in reader RF field.");

        byte[] source = bank switch
        {
            MemoryBank.Epc => EpcHexCodec.ToBytes(tag.Epc),
            MemoryBank.Tid => EpcHexCodec.ToBytes(tag.Tid),
            MemoryBank.User => tag.UserMemory,
            _ => new byte[wordCount * 2]
        };

        int byteOffset = startWord * 2;
        int byteLen = wordCount * 2;
        byte[] buffer = new byte[byteLen];

        if (byteOffset < source.Length)
        {
            int copyCount = Math.Min(byteLen, source.Length - byteOffset);
            Array.Copy(source, byteOffset, buffer, 0, copyCount);
        }

        return Task.FromResult(buffer);
    }

    public Task<bool> WriteBankAsync(string epc, MemoryBank bank, ushort startWord, byte[] data, string? accessPassword = null, CancellationToken ct = default)
    {
        string norm = EpcHexCodec.Normalize(epc);
        if (!_tags.TryGetValue(norm, out var tag) || tag.IsKilled || tag.IsLocked)
            return Task.FromResult(false);

        if (bank == MemoryBank.User)
        {
            int byteOffset = startWord * 2;
            if (byteOffset + data.Length > tag.UserMemory.Length)
            {
                byte[] expanded = new byte[byteOffset + data.Length];
                Array.Copy(tag.UserMemory, expanded, tag.UserMemory.Length);
                tag.UserMemory = expanded;
            }
            Array.Copy(data, 0, tag.UserMemory, byteOffset, data.Length);
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }

    public Task<bool> WriteEpcAsync(string currentEpc, string newEpc, string? accessPassword = null, CancellationToken ct = default)
    {
        string oldNorm = EpcHexCodec.Normalize(currentEpc);
        string newNorm = EpcHexCodec.Normalize(newEpc);

        if (_tags.TryRemove(oldNorm, out var tag))
        {
            tag.Epc = newNorm;
            _tags[newNorm] = tag;
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }

    public Task<bool> LockTagAsync(string epc, MemoryBank bank, string accessPassword, CancellationToken ct = default)
    {
        string norm = EpcHexCodec.Normalize(epc);
        if (_tags.TryGetValue(norm, out var tag))
        {
            tag.IsLocked = true;
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    public Task<bool> KillTagAsync(string epc, string killPassword, CancellationToken ct = default)
    {
        string norm = EpcHexCodec.Normalize(epc);
        if (_tags.TryGetValue(norm, out var tag))
        {
            tag.IsKilled = true;
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    #endregion

    #region IRfidGpioController Implementation

    public Task SetGpoAsync(int pinIndex, bool isHigh, CancellationToken ct = default)
    {
        if (pinIndex >= 1 && pinIndex <= _gpoPins.Length)
        {
            _gpoPins[pinIndex - 1] = isHigh;
        }
        return Task.CompletedTask;
    }

    public Task<bool> GetGpiAsync(int pinIndex, CancellationToken ct = default)
    {
        if (pinIndex >= 1 && pinIndex <= _gpiPins.Length)
        {
            return Task.FromResult(_gpiPins[pinIndex - 1]);
        }
        return Task.FromResult(false);
    }

    /// <summary>
    /// Helper for tests to simulate external hardware triggering a sensor on a GPI pin.
    /// </summary>
    public void SimulateGpiTrigger(int pinIndex, bool isHigh)
    {
        if (pinIndex >= 1 && pinIndex <= _gpiPins.Length)
        {
            _gpiPins[pinIndex - 1] = isHigh;
            GpiChanged?.Invoke(this, new GpioPinChangedEvent(pinIndex, isHigh));
        }
    }

    #endregion

    private void InitializeSimulatedTags()
    {
        if (_options.CustomEpcs != null && _options.CustomEpcs.Count > 0)
        {
            int index = 1;
            foreach (var epc in _options.CustomEpcs)
            {
                string norm = EpcHexCodec.Normalize(epc);
                string tid = $"E28011602000{index:D12}";
                _tags[norm] = new SimulatedTagMemory(norm, tid);
                index++;
            }
        }
        else
        {
            for (int i = 1; i <= _options.TagCount; i++)
            {
                // Generates SGTIN-96 like tags
                string epc = $"3034{i:D4}A1B2C3D4E5F6{i:D4}";
                string tid = $"E28011602000{i:D12}";
                _tags[epc] = new SimulatedTagMemory(epc, tid);
            }
        }
    }

    private async Task RunSimulationLoopAsync(CancellationToken ct)
    {
        var random = new Random();
        var tagList = _tags.Values.ToList();
        int step = 0;

        while (!ct.IsCancellationRequested)
        {
            foreach (var tag in tagList)
            {
                if (tag.IsKilled) continue;

                int antennaId;
                double rssi;

                switch (_options.Scenario)
                {
                    case SimulationScenario.WarehouseGateInbound:
                        // Antenna 1 peaks early (step 0..20), Antenna 2 peaks later (step 20..40)
                        if (step % 40 < 20)
                        {
                            antennaId = 1;
                            rssi = -45.0 + random.NextDouble() * 5.0;
                        }
                        else
                        {
                            antennaId = 2;
                            rssi = -42.0 + random.NextDouble() * 5.0;
                        }
                        break;

                    case SimulationScenario.WarehouseGateOutbound:
                        // Antenna 2 peaks early, Antenna 1 peaks later
                        if (step % 40 < 20)
                        {
                            antennaId = 2;
                            rssi = -45.0 + random.NextDouble() * 5.0;
                        }
                        else
                        {
                            antennaId = 1;
                            rssi = -42.0 + random.NextDouble() * 5.0;
                        }
                        break;

                    case SimulationScenario.ConveyorBeltFeed:
                        antennaId = 1;
                        rssi = -50.0 + random.NextDouble() * 8.0;
                        break;

                    case SimulationScenario.StaticShelfInventory:
                    default:
                        antennaId = (step % _options.AntennaCount) + 1;
                        rssi = -60.0 + random.NextDouble() * 15.0;
                        break;
                }

                var evt = new TagReadEvent(
                    tag.Epc,
                    antennaId,
                    rssi,
                    ReaderId,
                    DateTime.UtcNow,
                    readCount: 1,
                    tid: tag.Tid);

                _channel.Writer.TryWrite(evt);

                try
                {
                    TagRead?.Invoke(this, evt);
                }
                catch
                {
                    // Suppress subscriber exceptions
                }

                step++;
                await Task.Delay(_options.EmissionIntervalMs, ct).ConfigureAwait(false);
            }
        }
    }

    private void SetState(ReaderState newState)
    {
        ReaderState oldState;
        lock (_stateLock)
        {
            oldState = (ReaderState)_stateInt;
            if (oldState == newState) return;
            _stateInt = (int)newState;
        }

        try
        {
            StateChanged?.Invoke(this, newState);
        }
        catch
        {
            // Suppress
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync().ConfigureAwait(false);
        _channel.Writer.TryComplete();
    }
}
