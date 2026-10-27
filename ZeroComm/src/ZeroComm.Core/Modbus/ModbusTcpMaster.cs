using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ZeroComm.Core.Buffers;
using ZeroComm.Core.Transport;

namespace ZeroComm.Core.Modbus
{
    /// <summary>
    /// Industrial asynchronous Modbus TCP Master client.
    /// Operates over any <see cref="ITransport"/> (typically <see cref="AsyncTcpTransport"/>),
    /// managing transaction IDs, concurrent request multiplexing, and zero-copy ring buffer frame extraction.
    /// </summary>
    public class ModbusTcpMaster : IDisposable
    {
        private readonly ITransport _transport;
        private readonly CircularRingBuffer _ringBuffer = new CircularRingBuffer(65536);
        private readonly object _ringLock = new object();
        private readonly ConcurrentDictionary<ushort, TaskCompletionSource<byte[]>> _pendingRequests =
            new ConcurrentDictionary<ushort, TaskCompletionSource<byte[]>>();

        private int _transactionIdCounter;
        private bool _isDisposed;

        /// <summary>
        /// Gets the underlying transport instance.
        /// </summary>
        public ITransport Transport => _transport;

        /// <summary>
        /// Gets or sets the default request-response timeout in milliseconds (default: 3000ms).
        /// </summary>
        public int DefaultTimeoutMs { get; set; } = 3000;

        /// <summary>
        /// Initializes a new instance of the <see cref="ModbusTcpMaster"/> class.
        /// </summary>
        /// <param name="transport">The active transport stream.</param>
        public ModbusTcpMaster(ITransport transport)
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

                while (StreamingFrameParser.TryExtractModbusTcpFrame(_ringBuffer, out byte[] frame))
                {
                    if (frame.Length >= 2)
                    {
                        ushort txId = (ushort)((frame[0] << 8) | frame[1]);
                        if (_pendingRequests.TryRemove(txId, out var tcs))
                        {
                            tcs.TrySetResult(frame);
                        }
                    }
                }
            }
        }

        private void OnDisconnected()
        {
            foreach (var kvp in _pendingRequests)
            {
                if (_pendingRequests.TryRemove(kvp.Key, out var tcs))
                {
                    tcs.TrySetException(new IOException("Transport disconnected while awaiting Modbus response."));
                }
            }
        }

        private ushort NextTransactionId()
        {
            int val = Interlocked.Increment(ref _transactionIdCounter);
            return (ushort)(val & 0xFFFF);
        }

        private async Task<byte[]> SendAndReceiveAsync(byte[] request, ushort txId, int timeoutMs, CancellationToken ct)
        {
            ThrowIfDisposed();

            var tcs = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pendingRequests[txId] = tcs;

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            linkedCts.CancelAfter(timeoutMs);

            try
            {
                await _transport.SendAsync(request, 0, request.Length, ct).ConfigureAwait(false);

#if NET8_0_OR_GREATER
                return await tcs.Task.WaitAsync(linkedCts.Token).ConfigureAwait(false);
#else
                var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(timeoutMs, linkedCts.Token)).ConfigureAwait(false);
                if (completedTask != tcs.Task)
                {
                    _pendingRequests.TryRemove(txId, out _);
                    throw new TimeoutException($"Modbus request timed out after {timeoutMs}ms (TransactionId: {txId}).");
                }
                return await tcs.Task.ConfigureAwait(false);
#endif
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                _pendingRequests.TryRemove(txId, out _);
                throw new TimeoutException($"Modbus request timed out after {timeoutMs}ms (TransactionId: {txId}).");
            }
            catch
            {
                _pendingRequests.TryRemove(txId, out _);
                throw;
            }
        }

        /// <summary>
        /// Reads one or more Holding Registers (Function Code 03) asynchronously.
        /// </summary>
        public async Task<ushort[]> ReadHoldingRegistersAsync(
            byte unitId,
            ushort startAddress,
            ushort count,
            int? timeoutMs = null,
            CancellationToken cancellationToken = default)
        {
            ushort txId = NextTransactionId();
            byte[] req = ModbusTcpFrame.CreateReadHoldingRegistersRequest(txId, unitId, startAddress, count);
            byte[] resp = await SendAndReceiveAsync(req, txId, timeoutMs ?? DefaultTimeoutMs, cancellationToken).ConfigureAwait(false);

            if (!ModbusTcpFrame.ParseReadRegistersResponse(resp, 0, resp.Length, out _, out _, out var registers, out var exc))
            {
                throw new InvalidDataException("Failed to parse Modbus ReadHoldingRegisters response frame.");
            }

            if (exc != ModbusExceptionCode.None)
            {
                throw new ModbusException((byte)ModbusFunctionCode.ReadHoldingRegisters, exc);
            }

            return registers;
        }

        /// <summary>
        /// Reads one or more Input Registers (Function Code 04) asynchronously.
        /// </summary>
        public async Task<ushort[]> ReadInputRegistersAsync(
            byte unitId,
            ushort startAddress,
            ushort count,
            int? timeoutMs = null,
            CancellationToken cancellationToken = default)
        {
            ushort txId = NextTransactionId();
            byte[] req = ModbusTcpFrame.CreateReadInputRegistersRequest(txId, unitId, startAddress, count);
            byte[] resp = await SendAndReceiveAsync(req, txId, timeoutMs ?? DefaultTimeoutMs, cancellationToken).ConfigureAwait(false);

            if (!ModbusTcpFrame.ParseReadRegistersResponse(resp, 0, resp.Length, out _, out _, out var registers, out var exc))
            {
                throw new InvalidDataException("Failed to parse Modbus ReadInputRegisters response frame.");
            }

            if (exc != ModbusExceptionCode.None)
            {
                throw new ModbusException((byte)ModbusFunctionCode.ReadInputRegisters, exc);
            }

            return registers;
        }

        /// <summary>
        /// Reads one or more Coils (Function Code 01) asynchronously.
        /// </summary>
        public async Task<bool[]> ReadCoilsAsync(
            byte unitId,
            ushort startAddress,
            ushort count,
            int? timeoutMs = null,
            CancellationToken cancellationToken = default)
        {
            ushort txId = NextTransactionId();
            byte[] req = ModbusTcpFrame.CreateReadCoilsRequest(txId, unitId, startAddress, count);
            byte[] resp = await SendAndReceiveAsync(req, txId, timeoutMs ?? DefaultTimeoutMs, cancellationToken).ConfigureAwait(false);

            if (!ModbusTcpFrame.ParseReadCoilsResponse(resp, 0, resp.Length, count, out _, out _, out var coils, out var exc))
            {
                throw new InvalidDataException("Failed to parse Modbus ReadCoils response frame.");
            }

            if (exc != ModbusExceptionCode.None)
            {
                throw new ModbusException((byte)ModbusFunctionCode.ReadCoils, exc);
            }

            return coils;
        }

        /// <summary>
        /// Reads one or more Discrete Inputs (Function Code 02) asynchronously.
        /// </summary>
        public async Task<bool[]> ReadDiscreteInputsAsync(
            byte unitId,
            ushort startAddress,
            ushort count,
            int? timeoutMs = null,
            CancellationToken cancellationToken = default)
        {
            ushort txId = NextTransactionId();
            byte[] req = ModbusTcpFrame.CreateReadDiscreteInputsRequest(txId, unitId, startAddress, count);
            byte[] resp = await SendAndReceiveAsync(req, txId, timeoutMs ?? DefaultTimeoutMs, cancellationToken).ConfigureAwait(false);

            if (!ModbusTcpFrame.ParseReadCoilsResponse(resp, 0, resp.Length, count, out _, out _, out var inputs, out var exc))
            {
                throw new InvalidDataException("Failed to parse Modbus ReadDiscreteInputs response frame.");
            }

            if (exc != ModbusExceptionCode.None)
            {
                throw new ModbusException((byte)ModbusFunctionCode.ReadDiscreteInputs, exc);
            }

            return inputs;
        }

        /// <summary>
        /// Writes a single register (Function Code 06) asynchronously.
        /// </summary>
        public async Task WriteSingleRegisterAsync(
            byte unitId,
            ushort registerAddress,
            ushort value,
            int? timeoutMs = null,
            CancellationToken cancellationToken = default)
        {
            ushort txId = NextTransactionId();
            byte[] req = ModbusTcpFrame.CreateWriteSingleRegisterRequest(txId, unitId, registerAddress, value);
            byte[] resp = await SendAndReceiveAsync(req, txId, timeoutMs ?? DefaultTimeoutMs, cancellationToken).ConfigureAwait(false);

            if (resp.Length >= 8 && (resp[7] & 0x80) != 0)
            {
                var exc = resp.Length >= 9 ? (ModbusExceptionCode)resp[8] : ModbusExceptionCode.SlaveDeviceFailure;
                throw new ModbusException((byte)ModbusFunctionCode.WriteSingleRegister, exc);
            }
        }

        /// <summary>
        /// Writes multiple registers (Function Code 16 / 0x10) asynchronously.
        /// </summary>
        public async Task WriteMultipleRegistersAsync(
            byte unitId,
            ushort startAddress,
            ushort[] values,
            int? timeoutMs = null,
            CancellationToken cancellationToken = default)
        {
            ushort txId = NextTransactionId();
            byte[] req = ModbusTcpFrame.CreateWriteMultipleRegistersRequest(txId, unitId, startAddress, values);
            byte[] resp = await SendAndReceiveAsync(req, txId, timeoutMs ?? DefaultTimeoutMs, cancellationToken).ConfigureAwait(false);

            if (resp.Length >= 8 && (resp[7] & 0x80) != 0)
            {
                var exc = resp.Length >= 9 ? (ModbusExceptionCode)resp[8] : ModbusExceptionCode.SlaveDeviceFailure;
                throw new ModbusException((byte)ModbusFunctionCode.WriteMultipleRegisters, exc);
            }
        }

        /// <summary>
        /// Writes a single coil (Function Code 05) asynchronously.
        /// </summary>
        public async Task WriteSingleCoilAsync(
            byte unitId,
            ushort coilAddress,
            bool state,
            int? timeoutMs = null,
            CancellationToken cancellationToken = default)
        {
            ushort txId = NextTransactionId();
            byte[] req = ModbusTcpFrame.CreateWriteSingleCoilRequest(txId, unitId, coilAddress, state);
            byte[] resp = await SendAndReceiveAsync(req, txId, timeoutMs ?? DefaultTimeoutMs, cancellationToken).ConfigureAwait(false);

            if (resp.Length >= 8 && (resp[7] & 0x80) != 0)
            {
                var exc = resp.Length >= 9 ? (ModbusExceptionCode)resp[8] : ModbusExceptionCode.SlaveDeviceFailure;
                throw new ModbusException((byte)ModbusFunctionCode.WriteSingleCoil, exc);
            }
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(ModbusTcpMaster));
        }

        /// <summary>
        /// Disposes master resources and unhooks transport events.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            _transport.DataReceived -= OnDataReceived;
            _transport.OnDisconnected -= OnDisconnected;
            OnDisconnected();
        }
    }
}
