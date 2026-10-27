using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using ZeroComm.Core.Checksums;
using ZeroComm.Core.Modbus;
using ZeroComm.Core.Transport;

namespace ZeroComm.Tests
{
    public class SerialAndModbusRtuTests
    {
        private sealed class DuplexTestTransport : ITransport
        {
            public bool IsConnected => true;
#pragma warning disable CS0067
            public event Action<byte[], int, int>? DataReceived;
            public event Action? OnConnected;
            public event Action<Exception>? OnError;
            public event Action? OnDisconnected;
#pragma warning restore CS0067

            public Func<byte[], byte[]>? Responder { get; set; }

            public Task ConnectAsync(CancellationToken cancellationToken = default)
            {
                OnConnected?.Invoke();
                return Task.CompletedTask;
            }
            public Task DisconnectAsync() { OnDisconnected?.Invoke(); return Task.CompletedTask; }

            public Task SendAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken = default)
            {
                byte[] request = new byte[count];
                Buffer.BlockCopy(buffer, offset, request, 0, count);

                if (Responder != null)
                {
                    Task.Run(() =>
                    {
                        byte[] response = Responder(request);
                        if (response != null && response.Length > 0)
                        {
                            DataReceived?.Invoke(response, 0, response.Length);
                        }
                    });
                }

                return Task.CompletedTask;
            }

            public Task SendAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
            {
                byte[] request = buffer.ToArray();
                return SendAsync(request, 0, request.Length, cancellationToken);
            }

            public void Dispose() { }
        }

        [Fact]
        public async Task TestModbusRtuMaster_ReadHoldingRegisters_Success()
        {
            var transport = new DuplexTestTransport();
            transport.Responder = req =>
            {
                // Verify request is ReadHoldingRegisters (FC 3)
                Assert.Equal(1, req[0]); // Unit 1
                Assert.Equal(3, req[1]); // FC 3

                // Build response: Unit 1, FC 3, ByteCount 4, Regs [1234, 5678], CRC16
                byte[] resp = new byte[9];
                resp[0] = 1;
                resp[1] = 3;
                resp[2] = 4;
                resp[3] = 1234 >> 8;
                resp[4] = 1234 & 0xFF;
                resp[5] = 5678 >> 8;
                resp[6] = 5678 & 0xFF;
                ushort crc = Crc16.ComputeModbus(resp, 0, 7);
                resp[7] = (byte)(crc & 0xFF);
                resp[8] = (byte)((crc >> 8) & 0xFF);
                return resp;
            };

            using (var master = new ModbusRtuMaster(transport))
            {
                var registers = await master.ReadHoldingRegistersAsync(1, 0, 2);
                Assert.Equal(2, registers.Length);
                Assert.Equal(1234, registers[0]);
                Assert.Equal(5678, registers[1]);
            }
        }

        [Fact]
        public async Task TestModbusRtuMaster_ExceptionResponse_ThrowsException()
        {
            var transport = new DuplexTestTransport();
            transport.Responder = req =>
            {
                // Return Modbus RTU Exception: Unit 1, FC 0x83, IllegalDataAddress (0x02), CRC16
                byte[] resp = new byte[5];
                resp[0] = 1;
                resp[1] = 0x83;
                resp[2] = (byte)ModbusExceptionCode.IllegalDataAddress;
                ushort crc = Crc16.ComputeModbus(resp, 0, 3);
                resp[3] = (byte)(crc & 0xFF);
                resp[4] = (byte)((crc >> 8) & 0xFF);
                return resp;
            };

            using (var master = new ModbusRtuMaster(transport))
            {
                var ex = await Assert.ThrowsAsync<ModbusException>(async () =>
                {
                    await master.ReadHoldingRegistersAsync(1, 100, 1);
                });
                Assert.Equal(ModbusExceptionCode.IllegalDataAddress, ex.ExceptionCode);
            }
        }

        [Fact]
        public async Task TestModbusRtuMaster_WriteSingleRegister_Success()
        {
            var transport = new DuplexTestTransport();
            transport.Responder = req =>
            {
                // Echo request back for write single register
                return req;
            };

            using (var master = new ModbusRtuMaster(transport))
            {
                await master.WriteSingleRegisterAsync(1, 50, 999);
            }
        }
    }
}
