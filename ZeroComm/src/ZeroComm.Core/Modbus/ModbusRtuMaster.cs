using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ZeroComm.Core.Buffers;
using ZeroComm.Core.Transport;

namespace ZeroComm.Core.Modbus
{
    /// <summary>
    /// Industrial asynchronous Modbus RTU Master client for RS-232 / RS-485 and serial gateways.
    /// Manages half-duplex transaction serialization, CRC16 verification, and streaming response extraction.
    /// Pure C# with zero external dependencies.
    /// </summary>
    public class ModbusRtuMaster : IDisposable
    {
        private readonly ITransport _transport;
        private readonly CircularRingBuffer _ringBuffer = new CircularRingBuffer(32768);
        private readonly object _ringLock = new object();
        private readonly Channels.HalfDuplexChannel<byte[]> _channel = new Channels.HalfDuplexChannel<byte[]>();
        private byte _expectedUnitId;
        private byte _expectedFunctionCode;
        private bool _isDisposed;

        public ITransport Transport => _transport;
        public int DefaultTimeoutMs { get; set; } = 3000;

        public ModbusRtuMaster(ITransport transport)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _transport.DataReceived += OnDataReceived;
            _transport.OnDisconnected += OnDisconnected;
        }

        private void OnDataReceived(byte[] buffer, int offset, int count)
        {
            lock (_ringLock)
            {
                _ringBuffer.Write(buffer, offset, count);

                while (StreamingFrameParser.TryExtractModbusRtuFrame(_ringBuffer, out byte[] frame))
                {
                    if (frame.Length >= 2)
                    {
                        byte unitId = frame[0];
                        byte fc = frame[1];

                        // Match response to active half-duplex request
                        if (unitId == _expectedUnitId &&
                            (fc == _expectedFunctionCode || fc == (_expectedFunctionCode | 0x80)))
                        {
                            _channel.TrySetResponse(frame);
                            break;
                        }
                    }
                }
            }
        }

        private void OnDisconnected()
        {
            _channel.FaultPending(new IOException("Transport disconnected while awaiting Modbus RTU response."));
        }

        private Task<byte[]> SendAndReceiveAsync(
            byte unitId,
            byte functionCode,
            byte[] requestBytes,
            CancellationToken cancellationToken)
        {
            if (!_transport.IsConnected)
                throw new InvalidOperationException("Modbus RTU transport is not connected.");

            lock (_ringLock)
            {
                _ringBuffer.Clear();
            }

            _expectedUnitId = unitId;
            _expectedFunctionCode = functionCode;

            return _channel.ExecuteRequestAsync(
                token => _transport.SendAsync(requestBytes, 0, requestBytes.Length, token),
                DefaultTimeoutMs,
                cancellationToken);
        }

        public async Task<ushort[]> ReadHoldingRegistersAsync(
            byte unitId,
            ushort startAddress,
            ushort numberOfPoints,
            CancellationToken cancellationToken = default)
        {
            byte[] request = ModbusRtuFrame.CreateReadHoldingRegistersRequest(unitId, startAddress, numberOfPoints);
            byte[] response = await SendAndReceiveAsync(unitId, (byte)ModbusFunctionCode.ReadHoldingRegisters, request, cancellationToken);

            if (!ModbusRtuFrame.ParseReadRegistersResponse(response, 0, response.Length, out _, out var registers, out var exc))
                throw new IOException("Failed to parse ReadHoldingRegisters RTU response.");

            if (exc != ModbusExceptionCode.None)
                throw new ModbusException((byte)ModbusFunctionCode.ReadHoldingRegisters, exc);

            return registers;
        }

        public async Task<ushort[]> ReadInputRegistersAsync(
            byte unitId,
            ushort startAddress,
            ushort numberOfPoints,
            CancellationToken cancellationToken = default)
        {
            byte[] request = ModbusRtuFrame.CreateReadInputRegistersRequest(unitId, startAddress, numberOfPoints);
            byte[] response = await SendAndReceiveAsync(unitId, (byte)ModbusFunctionCode.ReadInputRegisters, request, cancellationToken);

            if (!ModbusRtuFrame.ParseReadRegistersResponse(response, 0, response.Length, out _, out var registers, out var exc))
                throw new IOException("Failed to parse ReadInputRegisters RTU response.");

            if (exc != ModbusExceptionCode.None)
                throw new ModbusException((byte)ModbusFunctionCode.ReadInputRegisters, exc);

            return registers;
        }

        public async Task<bool[]> ReadCoilsAsync(
            byte unitId,
            ushort startAddress,
            ushort numberOfPoints,
            CancellationToken cancellationToken = default)
        {
            byte[] request = ModbusRtuFrame.CreateReadCoilsRequest(unitId, startAddress, numberOfPoints);
            byte[] response = await SendAndReceiveAsync(unitId, (byte)ModbusFunctionCode.ReadCoils, request, cancellationToken);

            if (!ModbusRtuFrame.ParseReadCoilsResponse(response, 0, response.Length, numberOfPoints, out _, out var coils, out var exc))
                throw new IOException("Failed to parse ReadCoils RTU response.");

            if (exc != ModbusExceptionCode.None)
                throw new ModbusException((byte)ModbusFunctionCode.ReadCoils, exc);

            return coils;
        }

        public async Task WriteSingleRegisterAsync(
            byte unitId,
            ushort registerAddress,
            ushort value,
            CancellationToken cancellationToken = default)
        {
            byte[] request = ModbusRtuFrame.CreateWriteSingleRegisterRequest(unitId, registerAddress, value);
            byte[] response = await SendAndReceiveAsync(unitId, (byte)ModbusFunctionCode.WriteSingleRegister, request, cancellationToken);

            if (response.Length >= 3 && (response[1] & 0x80) != 0)
                throw new ModbusException(response[1], (ModbusExceptionCode)response[2]);
        }

        public async Task WriteMultipleRegistersAsync(
            byte unitId,
            ushort startAddress,
            ushort[] values,
            CancellationToken cancellationToken = default)
        {
            byte[] request = ModbusRtuFrame.CreateWriteMultipleRegistersRequest(unitId, startAddress, values);
            byte[] response = await SendAndReceiveAsync(unitId, (byte)ModbusFunctionCode.WriteMultipleRegisters, request, cancellationToken);

            if (response.Length >= 3 && (response[1] & 0x80) != 0)
                throw new ModbusException(response[1], (ModbusExceptionCode)response[2]);
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                _isDisposed = true;
                _transport.DataReceived -= OnDataReceived;
                _transport.OnDisconnected -= OnDisconnected;
                _channel.Dispose();
            }
        }
    }
}
