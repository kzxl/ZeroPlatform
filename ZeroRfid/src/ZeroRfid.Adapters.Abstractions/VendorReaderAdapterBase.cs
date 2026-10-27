using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using ZeroRfid.Core.Abstractions;
using ZeroRfid.Core.Models;

namespace ZeroRfid.Adapters.Abstractions;

/// <summary>
/// Thread-safe base adapter facilitating integration of vendor-proprietary C# SDKs (e.g. Zebra RFID3, Impinj Octane, Hopeland) into ZeroPlatform.
/// </summary>
public abstract class VendorReaderAdapterBase : IRfidReader
{
    private readonly Channel<TagReadEvent> _tagChannel;
    private int _stateInt = (int)ReaderState.Disconnected;
    private readonly object _stateLock = new();

    public string ReaderId { get; }
    public abstract RfidCapabilities Capabilities { get; }

    public ReaderState State => (ReaderState)_stateInt;

    public event EventHandler<TagReadEvent>? TagRead;
    public event EventHandler<ReaderState>? StateChanged;

    protected VendorReaderAdapterBase(string readerId, int channelCapacity = 10000)
    {
        ReaderId = readerId ?? throw new ArgumentNullException(nameof(readerId));
        _tagChannel = Channel.CreateBounded<TagReadEvent>(new BoundedChannelOptions(channelCapacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = false,
            SingleWriter = false
        });
    }

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        lock (_stateLock)
        {
            if (State == ReaderState.Connected || State == ReaderState.InventoryRunning)
                return;
            SetState(ReaderState.Connecting);
        }

        try
        {
            await OnConnectAsync(ct).ConfigureAwait(false);
            SetState(ReaderState.Connected);
        }
        catch
        {
            SetState(ReaderState.Faulted);
            throw;
        }
    }

    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        lock (_stateLock)
        {
            if (State == ReaderState.Disconnected)
                return;
        }

        try
        {
            if (State == ReaderState.InventoryRunning)
            {
                await StopInventoryAsync(ct).ConfigureAwait(false);
            }

            await OnDisconnectAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            SetState(ReaderState.Disconnected);
        }
    }

    public async Task StartInventoryAsync(InventorySettings? settings = null, CancellationToken ct = default)
    {
        lock (_stateLock)
        {
            if (State != ReaderState.Connected)
                throw new InvalidOperationException($"Cannot start inventory when reader is in state '{State}'.");
        }

        await OnStartInventoryAsync(settings ?? InventorySettings.Default, ct).ConfigureAwait(false);
        SetState(ReaderState.InventoryRunning);
    }

    public async Task StopInventoryAsync(CancellationToken ct = default)
    {
        lock (_stateLock)
        {
            if (State != ReaderState.InventoryRunning)
                return;
        }

        await OnStopInventoryAsync(ct).ConfigureAwait(false);
        SetState(ReaderState.Connected);
    }

    public async IAsyncEnumerable<TagReadEvent> ReadTagsAsync([EnumeratorCancellation] CancellationToken ct = default)
    {
        while (await _tagChannel.Reader.WaitToReadAsync(ct).ConfigureAwait(false))
        {
            while (_tagChannel.Reader.TryRead(out var item))
            {
                yield return item;
            }
        }
    }

    /// <summary>
    /// Invoked by vendor SDK callbacks when a raw tag has been translated into a normalized TagReadEvent.
    /// </summary>
    protected void PublishTagEvent(TagReadEvent tagEvent)
    {
        if (tagEvent == null) return;

        // Push to channel for high-throughput stream consumers
        _tagChannel.Writer.TryWrite(tagEvent);

        // Dispatch event for direct event subscribers (UI thread handlers, etc.)
        try
        {
            TagRead?.Invoke(this, tagEvent);
        }
        catch
        {
            // Suppress callback exceptions to prevent vendor SDK thread termination
        }
    }

    protected void SetState(ReaderState newState)
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
            // Ignored
        }
    }

    protected abstract Task OnConnectAsync(CancellationToken ct);
    protected abstract Task OnDisconnectAsync(CancellationToken ct);
    protected abstract Task OnStartInventoryAsync(InventorySettings settings, CancellationToken ct);
    protected abstract Task OnStopInventoryAsync(CancellationToken ct);

    public virtual async ValueTask DisposeAsync()
    {
        try
        {
            await DisconnectAsync().ConfigureAwait(false);
        }
        catch
        {
            // Ignored on cleanup
        }

        _tagChannel.Writer.TryComplete();
        GC.SuppressFinalize(this);
    }
}
