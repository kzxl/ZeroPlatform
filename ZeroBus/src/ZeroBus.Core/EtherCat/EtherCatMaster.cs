using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ZeroBus.EtherCat
{
    public interface IEtherCatTransport : IDisposable
    {
        bool IsConnected { get; }
        Task ConnectAsync(CancellationToken ct = default);
        Task DisconnectAsync(CancellationToken ct = default);
        Task<byte[]> ExchangeFrameAsync(byte[] txFrame, CancellationToken ct = default);
    }

    /// <summary>
    /// In-memory mock/virtual transport for EtherCAT testing and simulation.
    /// Simulates N slaves responding with auto-increment address assignment and working counters.
    /// </summary>
    public class VirtualEtherCatTransport : IEtherCatTransport
    {
        public bool IsConnected { get; private set; }
        public int SimulatedSlaveCount { get; set; } = 2;
        public EtherCatState SimulatedAlState { get; set; } = EtherCatState.Init;

        public Task ConnectAsync(CancellationToken ct = default)
        {
            IsConnected = true;
            return Task.CompletedTask;
        }

        public Task DisconnectAsync(CancellationToken ct = default)
        {
            IsConnected = false;
            return Task.CompletedTask;
        }

        public Task<byte[]> ExchangeFrameAsync(byte[] txFrame, CancellationToken ct = default)
        {
            if (!IsConnected) throw new InvalidOperationException("Transport is not connected.");

            // Deep clone and echo with simulated slave response
            byte[] rxFrame = (byte[])txFrame.Clone();
            int offset = 2; // skip 2 bytes EtherCAT header

            while (offset < rxFrame.Length - 2)
            {
                byte cmd = rxFrame[offset];
                ushort lenField = BitConverter.ToUInt16(rxFrame, offset + 6);
                int dataLen = lenField & 0x07FF;
                int wkcIndex = offset + 10 + dataLen;

                if (cmd == EtherCatConstants.CmdBrd) // Broadcast Read
                {
                    // Increment working counter by number of slaves
                    ushort wkc = (ushort)SimulatedSlaveCount;
                    rxFrame[wkcIndex] = (byte)(wkc & 0xFF);
                    rxFrame[wkcIndex + 1] = (byte)((wkc >> 8) & 0xFF);

                    // If reading AL status (0x0130), return current simulated state
                    uint addr = BitConverter.ToUInt32(rxFrame, offset + 2);
                    if ((addr & 0xFFFF) == EtherCatConstants.RegAlStatus && dataLen >= 2)
                    {
                        rxFrame[offset + 10] = (byte)SimulatedAlState;
                    }
                }
                else if (cmd == EtherCatConstants.CmdBwr || cmd == EtherCatConstants.CmdApwr || cmd == EtherCatConstants.CmdFpwr)
                {
                    // Write command: update simulated state if AL Control
                    uint addr = BitConverter.ToUInt32(rxFrame, offset + 2);
                    if ((addr & 0xFFFF) == EtherCatConstants.RegAlControl && dataLen >= 2)
                    {
                        SimulatedAlState = (EtherCatState)rxFrame[offset + 10];
                    }

                    ushort wkc = 1;
                    rxFrame[wkcIndex] = (byte)(wkc & 0xFF);
                    rxFrame[wkcIndex + 1] = (byte)((wkc >> 8) & 0xFF);
                }
                else if (cmd == EtherCatConstants.CmdLrw)
                {
                    // Process Data Logical Read Write
                    ushort wkc = 3; // R/W success
                    rxFrame[wkcIndex] = (byte)(wkc & 0xFF);
                    rxFrame[wkcIndex + 1] = (byte)((wkc >> 8) & 0xFF);
                }

                offset += 10 + dataLen + 2;
                if ((lenField & 0x8000) == 0) break; // More bit not set
            }

            return Task.FromResult(rxFrame);
        }

        public void Dispose()
        {
            IsConnected = false;
        }
    }

    /// <summary>
    /// Pure C# EtherCAT Master.
    /// Manages network discovery, EtherCAT State Machine (ESM), and high-frequency cyclic PDO exchange.
    /// </summary>
    public class EtherCatMaster
    {
        private readonly IEtherCatTransport _transport;
        private byte _datagramIndex;

        public EtherCatState MasterState { get; private set; } = EtherCatState.Init;
        public int DiscoveredSlaveCount { get; private set; }

        public EtherCatMaster(IEtherCatTransport transport)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        }

        public async Task<int> ScanSlavesAsync(CancellationToken ct = default)
        {
            // Send Broadcast Read (BRD) to address 0x0000 (RegType)
            byte[] data = new byte[2];
            var dgram = new EtherCatDatagram(EtherCatConstants.CmdBrd, unchecked(++_datagramIndex), 0x0000, data);

            var resp = await SendDatagramAsync(dgram, ct).ConfigureAwait(false);
            DiscoveredSlaveCount = resp.WorkingCounter;
            return DiscoveredSlaveCount;
        }

        public async Task TransitionStateAsync(EtherCatState targetState, CancellationToken ct = default)
        {
            // Broadcast write to AL Control register (0x0120)
            byte[] data = new byte[2] { (byte)targetState, 0x00 };
            var dgram = new EtherCatDatagram(EtherCatConstants.CmdBwr, unchecked(++_datagramIndex), EtherCatConstants.RegAlControl, data);

            var resp = await SendDatagramAsync(dgram, ct).ConfigureAwait(false);
            if (resp.WorkingCounter == 0)
            {
                throw new InvalidOperationException($"Failed to set AL Control state to {targetState}. No slaves acknowledged.");
            }

            MasterState = targetState;
        }

        public async Task<EtherCatState> ReadAlStateAsync(CancellationToken ct = default)
        {
            byte[] data = new byte[2];
            var dgram = new EtherCatDatagram(EtherCatConstants.CmdBrd, unchecked(++_datagramIndex), EtherCatConstants.RegAlStatus, data);

            var resp = await SendDatagramAsync(dgram, ct).ConfigureAwait(false);
            if (resp.Data.Length >= 2)
            {
                return (EtherCatState)(resp.Data[0] & 0x0F);
            }
            return EtherCatState.None;
        }

        public async Task<ushort> ExchangeProcessDataAsync(byte[] txPdo, byte[] rxPdo, uint logicalAddress = 0x00010000, CancellationToken ct = default)
        {
            var dgram = new EtherCatDatagram(EtherCatConstants.CmdLrw, unchecked(++_datagramIndex), logicalAddress, txPdo);
            var resp = await SendDatagramAsync(dgram, ct).ConfigureAwait(false);

            if (rxPdo != null && resp.Data.Length > 0)
            {
                int copyLen = System.Math.Min(rxPdo.Length, resp.Data.Length);
                Array.Copy(resp.Data, 0, rxPdo, 0, copyLen);
            }

            return resp.WorkingCounter;
        }

        private async Task<EtherCatDatagram> SendDatagramAsync(EtherCatDatagram dgram, CancellationToken ct)
        {
            // Build raw frame: 2 bytes header + datagram
            int dgramLen = 10 + dgram.Data.Length + 2;
            byte[] frame = new byte[2 + dgramLen];

            // Header: length in low 11 bits, type = 1 (commands)
            ushort header = (ushort)(dgramLen & 0x07FF);
            header |= (1 << 12);
            frame[0] = (byte)(header & 0xFF);
            frame[1] = (byte)((header >> 8) & 0xFF);

            dgram.Serialize(frame.AsSpan().Slice(2));

            byte[] rxFrame = await _transport.ExchangeFrameAsync(frame, ct).ConfigureAwait(false);

            // Parse response datagram
            return EtherCatDatagram.Deserialize(rxFrame.AsSpan().Slice(2), out _);
        }
    }
}
