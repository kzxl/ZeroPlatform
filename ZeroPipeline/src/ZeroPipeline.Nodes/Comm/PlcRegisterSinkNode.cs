using System;
using System.Threading;
using System.Threading.Tasks;
using ZeroComm.Core.Modbus;
using ZeroPipeline.Core.Execution;
using ZeroPipeline.Core.Nodes;
using ZeroPipeline.Nodes.Inspection;

namespace ZeroPipeline.Nodes.Comm
{
    /// <summary>
    /// Pipeline terminal sink node that dispatches inspection results to PLC memory / Modbus holding registers.
    /// Can operate directly against an industrial ModbusTcpMaster client or in-memory register emulator.
    /// </summary>
    public sealed class PlcRegisterSinkNode : SinkNode<InspectionResult>
    {
        private readonly ModbusTcpMaster? _modbusClient;
        private readonly byte _unitId;
        private readonly ushort _statusRegisterAddress;
        private readonly ushort _valueRegisterAddress;
        private readonly Action<ushort, ushort>? _registerWrittenCallback;

        public ushort LastStatusWritten { get; private set; }
        public ushort LastValueWritten { get; private set; }

        public PlcRegisterSinkNode(
            ModbusTcpMaster? modbusClient = null,
            byte unitId = 1,
            ushort statusRegisterAddress = 100,
            ushort valueRegisterAddress = 101,
            Action<ushort, ushort>? registerWrittenCallback = null,
            string? name = null)
            : base(name ?? $"PLC_RegisterSink[Addr={statusRegisterAddress}]")
        {
            _modbusClient = modbusClient;
            _unitId = unitId;
            _statusRegisterAddress = statusRegisterAddress;
            _valueRegisterAddress = valueRegisterAddress;
            _registerWrittenCallback = registerWrittenCallback;
        }

        protected override async Task ConsumeAsync(InspectionResult input, PipelineContext context, CancellationToken cancellationToken)
        {
            if (input == null) return;

            // 1: Passed (OK), 2: Failed (NG), 3: Warning, 4: Skipped
            ushort statusCode = (ushort)(input.Status switch
            {
                InspectionStatus.Passed => 1,
                InspectionStatus.Failed => 2,
                InspectionStatus.Warning => 3,
                _ => 4
            });

            // Convert measured value to fixed-point integer (x100 scale, e.g. 12.34mm -> 1234)
            ushort scaledValue = (ushort)Math.Max(0, Math.Min(ushort.MaxValue, (int)Math.Round(input.MeasuredValue * 100.0)));

            LastStatusWritten = statusCode;
            LastValueWritten = scaledValue;

            if (_modbusClient != null)
            {
                await _modbusClient.WriteSingleRegisterAsync(_unitId, _statusRegisterAddress, statusCode, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                await _modbusClient.WriteSingleRegisterAsync(_unitId, _valueRegisterAddress, scaledValue, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            }

            _registerWrittenCallback?.Invoke(statusCode, scaledValue);
        }
    }
}
