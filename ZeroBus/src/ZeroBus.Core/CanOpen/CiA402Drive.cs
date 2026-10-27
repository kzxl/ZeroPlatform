using System;
using System.Threading;
using System.Threading.Tasks;
using ZeroBus.Can;

namespace ZeroBus.CanOpen
{
    /// <summary>
    /// CiA 402 Device Profile for Drives and Motion Control.
    /// Manages servo drive state machine transitions and cyclic motion control over CANopen / EtherCAT CoE.
    /// </summary>
    public class CiA402Drive
    {
        private readonly byte _nodeId;
        private readonly ICanTransport _transport;
        private readonly SdoClient _sdo;

        public byte NodeId => _nodeId;
        public ushort CurrentStatusword { get; private set; }
        public ushort CurrentControlword { get; private set; }
        public int ActualPosition { get; private set; }
        public int ActualVelocity { get; private set; }
        public CiA402State CurrentState { get; private set; } = CiA402State.SwitchOnDisabled;
        public CiA402ModeOfOperation CurrentMode { get; private set; } = CiA402ModeOfOperation.NoMode;

        public event Action<CiA402State>? OnStateChanged;

        public CiA402Drive(byte nodeId, ICanTransport transport, SdoClient sdo)
        {
            _nodeId = nodeId;
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _sdo = sdo ?? throw new ArgumentNullException(nameof(sdo));
            _transport.OnFrameReceived += HandlePdoFrame;
        }

        public async Task SetModeOfOperationAsync(CiA402ModeOfOperation mode, CancellationToken ct = default)
        {
            CurrentMode = mode;
            await _sdo.WriteU8Async(_nodeId, CanOpenConstants.IndexModesOfOperation, 0, (byte)mode, ct: ct).ConfigureAwait(false);
        }

        public async Task SendControlwordAsync(ushort controlword, CancellationToken ct = default)
        {
            CurrentControlword = controlword;
            await _sdo.WriteU16Async(_nodeId, CanOpenConstants.IndexControlword, 0, controlword, ct: ct).ConfigureAwait(false);
        }

        public Task ShutdownAsync(CancellationToken ct = default) => SendControlwordAsync(0x0006, ct);
        public Task SwitchOnAsync(CancellationToken ct = default) => SendControlwordAsync(0x0007, ct);
        public Task EnableOperationAsync(CancellationToken ct = default) => SendControlwordAsync(0x000F, ct);
        public Task DisableOperationAsync(CancellationToken ct = default) => SendControlwordAsync(0x0007, ct);
        public Task QuickStopAsync(CancellationToken ct = default) => SendControlwordAsync(0x0002, ct);
        public Task QuickStopPdoAsync(CancellationToken ct = default) => SendCspPdoAsync(0x0002, ActualPosition, ct);
        public Task ResetFaultAsync(CancellationToken ct = default) => SendControlwordAsync(0x0080, ct);

        public async Task StartupServoAsync(CancellationToken ct = default)
        {
            await ShutdownAsync(ct).ConfigureAwait(false);
            await Task.Delay(10, ct).ConfigureAwait(false);
            await SwitchOnAsync(ct).ConfigureAwait(false);
            await Task.Delay(10, ct).ConfigureAwait(false);
            await EnableOperationAsync(ct).ConfigureAwait(false);
        }

        public async Task SetTargetPositionAsync(int targetPos, CancellationToken ct = default)
        {
            await _sdo.WriteU32Async(_nodeId, CanOpenConstants.IndexTargetPosition, 0, (uint)targetPos, ct: ct).ConfigureAwait(false);
        }

        public async Task SetTargetVelocityAsync(int targetVel, CancellationToken ct = default)
        {
            await _sdo.WriteU32Async(_nodeId, CanOpenConstants.IndexTargetVelocity, 0, (uint)targetVel, ct: ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Sends high-frequency cyclic synchronous position (CSP) command via RPDO1 (0x200 + NodeID).
        /// Format: Controlword (2 bytes) + Target Position (4 bytes).
        /// </summary>
        public async Task SendCspPdoAsync(ushort controlword, int targetPosition, CancellationToken ct = default)
        {
            uint cobId = CanOpenConstants.CobIdRpdo1Base + _nodeId;
            byte[] data = new byte[6];
            data[0] = (byte)(controlword & 0xFF);
            data[1] = (byte)((controlword >> 8) & 0xFF);
            data[2] = (byte)(targetPosition & 0xFF);
            data[3] = (byte)((targetPosition >> 8) & 0xFF);
            data[4] = (byte)((targetPosition >> 16) & 0xFF);
            data[5] = (byte)((targetPosition >> 24) & 0xFF);

            var frame = CanFrame.CreateStandard(cobId, data);
            await _transport.SendFrameAsync(frame, ct).ConfigureAwait(false);
        }

        public void ProcessStatusword(ushort sw)
        {
            CurrentStatusword = sw;
            var newState = DecodeStatusword(sw);
            if (newState != CurrentState)
            {
                CurrentState = newState;
                OnStateChanged?.Invoke(newState);
            }
        }

        private void HandlePdoFrame(CanFrame frame)
        {
            // TPDO1: 0x180 + NodeId: Statusword (2B) + Actual Position (4B)
            if (frame.Id == (CanOpenConstants.CobIdTpdo1Base + _nodeId) && frame.Data.Length >= 6)
            {
                ushort sw = BitConverter.ToUInt16(frame.Data, 0);
                int pos = BitConverter.ToInt32(frame.Data, 2);
                ActualPosition = pos;
                ProcessStatusword(sw);
            }
        }

        public static CiA402State DecodeStatusword(ushort sw)
        {
            // Statusword mask bits per IEC 61800-7-201 / CiA 402:
            // bit 0: Ready to switch on
            // bit 1: Switched on
            // bit 2: Operation enabled
            // bit 3: Fault
            // bit 5: Quick stop
            // bit 6: Switch on disabled
            if ((sw & 0x004F) == 0x0000) return CiA402State.NotReadyToSwitchOn;
            if ((sw & 0x004F) == 0x0040) return CiA402State.SwitchOnDisabled;
            if ((sw & 0x006F) == 0x0021) return CiA402State.ReadyToSwitchOn;
            if ((sw & 0x006F) == 0x0023) return CiA402State.SwitchedOn;
            if ((sw & 0x006F) == 0x0027) return CiA402State.OperationEnabled;
            if ((sw & 0x006F) == 0x0007) return CiA402State.QuickStopActive;
            if ((sw & 0x004F) == 0x000F) return CiA402State.FaultReactionActive;
            if ((sw & 0x004F) == 0x0008) return CiA402State.Fault;

            return CiA402State.SwitchOnDisabled;
        }
    }
}
