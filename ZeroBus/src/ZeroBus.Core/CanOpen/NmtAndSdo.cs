using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using ZeroBus.Can;

namespace ZeroBus.CanOpen
{
    /// <summary>
    /// CiA 301 Network Management (NMT) Master.
    /// Manages CANopen slave lifecycle states and monitors node heartbeats.
    /// </summary>
    public class NmtMaster
    {
        private readonly ICanTransport _transport;
        private readonly ConcurrentDictionary<byte, (NmtState State, long LastHeartbeatUs)> _nodeStates =
            new ConcurrentDictionary<byte, (NmtState, long)>();

        public event Action<byte, NmtState>? OnNodeStateChanged;

        public NmtMaster(ICanTransport transport)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _transport.OnFrameReceived += HandleFrame;
        }

        public async Task SendCommandAsync(byte cs, byte nodeId = 0, CancellationToken ct = default)
        {
            // COB-ID: 0x000, Data: [CS, NodeID]
            byte[] data = new byte[2] { cs, nodeId };
            var frame = CanFrame.CreateStandard(CanOpenConstants.CobIdNmt, data);
            await _transport.SendFrameAsync(frame, ct).ConfigureAwait(false);
        }

        public Task StartNodeAsync(byte nodeId = 0, CancellationToken ct = default) =>
            SendCommandAsync(CanOpenConstants.NmtStartNode, nodeId, ct);

        public Task StopNodeAsync(byte nodeId = 0, CancellationToken ct = default) =>
            SendCommandAsync(CanOpenConstants.NmtStopNode, nodeId, ct);

        public Task EnterPreOperationalAsync(byte nodeId = 0, CancellationToken ct = default) =>
            SendCommandAsync(CanOpenConstants.NmtEnterPreOp, nodeId, ct);

        public Task ResetNodeAsync(byte nodeId = 0, CancellationToken ct = default) =>
            SendCommandAsync(CanOpenConstants.NmtResetNode, nodeId, ct);

        public Task ResetCommunicationAsync(byte nodeId = 0, CancellationToken ct = default) =>
            SendCommandAsync(CanOpenConstants.NmtResetComm, nodeId, ct);

        public NmtState GetNodeState(byte nodeId)
        {
            return _nodeStates.TryGetValue(nodeId, out var tuple) ? tuple.State : NmtState.Stopped;
        }

        private void HandleFrame(CanFrame frame)
        {
            // Heartbeat check: COB-ID 0x700 + NodeID
            if (frame.Id >= CanOpenConstants.CobIdHeartbeatBase && frame.Id <= (CanOpenConstants.CobIdHeartbeatBase + 127))
            {
                byte nodeId = (byte)(frame.Id - CanOpenConstants.CobIdHeartbeatBase);
                if (frame.Data.Length > 0)
                {
                    var state = (NmtState)frame.Data[0];
                    _nodeStates[nodeId] = (state, frame.TimestampUs);
                    OnNodeStateChanged?.Invoke(nodeId, state);
                }
            }
        }
    }

    /// <summary>
    /// CiA 301 Service Data Object (SDO) Client.
    /// Provides expedited and segmented read/write access to remote slave Object Dictionaries.
    /// </summary>
    public class SdoClient
    {
        private readonly ICanTransport _transport;
        private readonly ConcurrentDictionary<uint, TaskCompletionSource<CanFrame>> _pendingRequests =
            new ConcurrentDictionary<uint, TaskCompletionSource<CanFrame>>();

        public SdoClient(ICanTransport transport)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _transport.OnFrameReceived += HandleFrame;
        }

        public async Task WriteU32Async(byte nodeId, ushort index, byte subIndex, uint value, int timeoutMs = 1000, CancellationToken ct = default)
        {
            byte[] payload = new byte[8];
            payload[0] = CanOpenConstants.SdoInitiateDownloadExpedited; // 4 bytes data
            payload[1] = (byte)(index & 0xFF);
            payload[2] = (byte)((index >> 8) & 0xFF);
            payload[3] = subIndex;
            payload[4] = (byte)(value & 0xFF);
            payload[5] = (byte)((value >> 8) & 0xFF);
            payload[6] = (byte)((value >> 16) & 0xFF);
            payload[7] = (byte)((value >> 24) & 0xFF);

            await SendSdoRequestAsync(nodeId, payload, timeoutMs, ct).ConfigureAwait(false);
        }

        public async Task WriteU16Async(byte nodeId, ushort index, byte subIndex, ushort value, int timeoutMs = 1000, CancellationToken ct = default)
        {
            byte[] payload = new byte[8];
            payload[0] = CanOpenConstants.SdoInitiateDownloadExpedited2B; // 2 bytes data
            payload[1] = (byte)(index & 0xFF);
            payload[2] = (byte)((index >> 8) & 0xFF);
            payload[3] = subIndex;
            payload[4] = (byte)(value & 0xFF);
            payload[5] = (byte)((value >> 8) & 0xFF);

            await SendSdoRequestAsync(nodeId, payload, timeoutMs, ct).ConfigureAwait(false);
        }

        public async Task WriteU8Async(byte nodeId, ushort index, byte subIndex, byte value, int timeoutMs = 1000, CancellationToken ct = default)
        {
            byte[] payload = new byte[8];
            payload[0] = CanOpenConstants.SdoInitiateDownloadExpedited1B; // 1 byte data
            payload[1] = (byte)(index & 0xFF);
            payload[2] = (byte)((index >> 8) & 0xFF);
            payload[3] = subIndex;
            payload[4] = value;

            await SendSdoRequestAsync(nodeId, payload, timeoutMs, ct).ConfigureAwait(false);
        }

        public async Task<uint> ReadU32Async(byte nodeId, ushort index, byte subIndex, int timeoutMs = 1000, CancellationToken ct = default)
        {
            byte[] payload = new byte[8];
            payload[0] = CanOpenConstants.SdoInitiateUploadRequest;
            payload[1] = (byte)(index & 0xFF);
            payload[2] = (byte)((index >> 8) & 0xFF);
            payload[3] = subIndex;

            var resp = await SendSdoRequestAsync(nodeId, payload, timeoutMs, ct).ConfigureAwait(false);
            if (resp.Data[0] == CanOpenConstants.SdoAbort)
            {
                uint abortCode = BitConverter.ToUInt32(resp.Data, 4);
                throw new InvalidOperationException($"SDO Read aborted with code 0x{abortCode:X8}.");
            }

            return BitConverter.ToUInt32(resp.Data, 4);
        }

        private async Task<CanFrame> SendSdoRequestAsync(byte nodeId, byte[] payload, int timeoutMs, CancellationToken ct)
        {
            uint rxCobId = CanOpenConstants.CobIdSdoTxBase + nodeId; // Response comes on 0x580 + NodeID
            uint txCobId = CanOpenConstants.CobIdSdoRxBase + nodeId; // Request goes to 0x600 + NodeID

            var tcs = new TaskCompletionSource<CanFrame>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pendingRequests[rxCobId] = tcs;

            var frame = CanFrame.CreateStandard(txCobId, payload);
            await _transport.SendFrameAsync(frame, ct).ConfigureAwait(false);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeoutMs);

            using (cts.Token.Register(() => tcs.TrySetCanceled()))
            {
                try
                {
                    return await tcs.Task.ConfigureAwait(false);
                }
                finally
                {
                    _pendingRequests.TryRemove(rxCobId, out _);
                }
            }
        }

        private void HandleFrame(CanFrame frame)
        {
            if (_pendingRequests.TryGetValue(frame.Id, out var tcs))
            {
                tcs.TrySetResult(frame);
            }
        }
    }
}
