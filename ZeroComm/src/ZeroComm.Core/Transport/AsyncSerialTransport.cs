using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace ZeroComm.Core.Transport
{
    /// <summary>
    /// High-performance asynchronous serial transport (RS-232, RS-485, USB Virtual COM) for industrial automation.
    /// Uses native Win32 COM port I/O with non-blocking background streaming and zero external dependencies.
    /// Also supports in-memory stream loopback for virtual unit testing.
    /// </summary>
    public sealed class AsyncSerialTransport : ITransport
    {
        private readonly string _portName;
        private readonly int _baudRate;
        private readonly int _dataBits;
        private readonly int _parity;
        private readonly int _stopBits;

        private IntPtr _hComm = (IntPtr)(-1);
        private Stream? _customStream;
        private CancellationTokenSource? _cts;
        private Task? _readLoopTask;
        private bool _isConnected;
        private bool _disposed;
        private readonly object _syncLock = new object();

        public string PortName => _portName;
        public int BaudRate => _baudRate;
        public int DataBits => _dataBits;
        public int Parity => _parity;
        public int StopBits => _stopBits;

        public bool IsConnected => _isConnected && !_disposed;

        public event Action<byte[], int, int>? DataReceived;
        public event Action? OnConnected;
        public event Action<Exception>? OnError;
        public event Action? OnDisconnected;

        public AsyncSerialTransport(string portName, int baudRate = 9600, int dataBits = 8, int parity = 0, int stopBits = 1)
        {
            _portName = portName ?? throw new ArgumentNullException(nameof(portName));
            _baudRate = baudRate;
            _dataBits = dataBits;
            _parity = parity;
            _stopBits = stopBits;
        }

        /// <summary>
        /// Creates an AsyncSerialTransport backed by an in-memory stream for virtual loopback testing.
        /// </summary>
        public AsyncSerialTransport(Stream stream) : this("VIRTUAL_COM", 9600)
        {
            _customStream = stream ?? throw new ArgumentNullException(nameof(stream));
        }

        public Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            lock (_syncLock)
            {
                if (_isConnected) return Task.CompletedTask;

                if (_customStream != null)
                {
                    _isConnected = true;
                    _cts = new CancellationTokenSource();
                    _readLoopTask = Task.Run(() => ReadLoopStreamAsync(_customStream, _cts.Token));
                    OnConnected?.Invoke();
                    return Task.CompletedTask;
                }

                // Native Win32 COM Port Opening
                string fullPort = _portName.StartsWith(@"\\.\", StringComparison.OrdinalIgnoreCase) ? _portName : @"\\.\" + _portName;

                _hComm = Win32Serial.CreateFile(
                    fullPort,
                    Win32Serial.GENERIC_READ | Win32Serial.GENERIC_WRITE,
                    0,
                    IntPtr.Zero,
                    Win32Serial.OPEN_EXISTING,
                    0,
                    IntPtr.Zero);

                if (_hComm == (IntPtr)(-1) || _hComm == IntPtr.Zero)
                {
                    int err = Marshal.GetLastWin32Error();
                    throw new IOException($"Failed to open serial port '{_portName}'. Win32 Error: {err}");
                }

                // Configure DCB
                var dcb = new Win32Serial.DCB();
                dcb.DCBlength = (uint)Marshal.SizeOf(typeof(Win32Serial.DCB));

                if (!Win32Serial.GetCommState(_hComm, ref dcb))
                {
                    Win32Serial.CloseHandle(_hComm);
                    _hComm = (IntPtr)(-1);
                    throw new IOException($"Failed to get comm state for '{_portName}'.");
                }

                dcb.BaudRate = (uint)_baudRate;
                dcb.ByteSize = (byte)_dataBits;
                dcb.Parity = (byte)_parity;
                dcb.StopBits = (byte)(_stopBits == 1 ? 0 : 2);
                dcb.fBinary = 1;

                if (!Win32Serial.SetCommState(_hComm, ref dcb))
                {
                    Win32Serial.CloseHandle(_hComm);
                    _hComm = (IntPtr)(-1);
                    throw new IOException($"Failed to set comm state for '{_portName}'.");
                }

                // Set Timeouts (Non-blocking reads)
                var timeouts = new Win32Serial.COMMTIMEOUTS
                {
                    ReadIntervalTimeout = 50,
                    ReadTotalTimeoutConstant = 50,
                    ReadTotalTimeoutMultiplier = 10,
                    WriteTotalTimeoutConstant = 50,
                    WriteTotalTimeoutMultiplier = 10
                };
                Win32Serial.SetCommTimeouts(_hComm, ref timeouts);

                _isConnected = true;
                _cts = new CancellationTokenSource();
                _readLoopTask = Task.Run(() => ReadLoopNativeAsync(_hComm, _cts.Token));
                OnConnected?.Invoke();

                return Task.CompletedTask;
            }
        }

        public async Task DisconnectAsync()
        {
            CancellationTokenSource? cts;
            Task? loopTask;

            lock (_syncLock)
            {
                if (!_isConnected) return;
                _isConnected = false;
                cts = _cts;
                loopTask = _readLoopTask;
            }

            try
            {
                cts?.Cancel();
                if (loopTask != null)
                {
                    await Task.WhenAny(loopTask, Task.Delay(200));
                }
            }
            catch { }

            lock (_syncLock)
            {
                if (_hComm != (IntPtr)(-1) && _hComm != IntPtr.Zero)
                {
                    Win32Serial.CloseHandle(_hComm);
                    _hComm = (IntPtr)(-1);
                }
                _cts?.Dispose();
                _cts = null;
            }

            OnDisconnected?.Invoke();
        }

        public async Task SendAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken = default)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || count < 0 || offset + count > buffer.Length)
                throw new ArgumentOutOfRangeException();

            if (!IsConnected)
                throw new InvalidOperationException("Serial transport is not connected.");

            if (_customStream != null)
            {
                await _customStream.WriteAsync(buffer, offset, count, cancellationToken);
                await _customStream.FlushAsync(cancellationToken);
                return;
            }

            await Task.Run(() =>
            {
                unsafe
                {
                    fixed (byte* pBuf = &buffer[offset])
                    {
                        if (!Win32Serial.WriteFile(_hComm, (IntPtr)pBuf, (uint)count, out uint written, IntPtr.Zero))
                        {
                            int err = Marshal.GetLastWin32Error();
                            throw new IOException($"Win32 serial write error on '{_portName}': {err}");
                        }
                    }
                }
            }, cancellationToken);
        }

        /// <summary>
        /// Sends a read-only memory buffer asynchronously through the serial port.
        /// </summary>
        public async Task SendAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (buffer.Length == 0) return;
            if (System.Runtime.InteropServices.MemoryMarshal.TryGetArray(buffer, out var segment) && segment.Array != null)
            {
                await SendAsync(segment.Array, segment.Offset, segment.Count, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                byte[] arr = buffer.ToArray();
                await SendAsync(arr, 0, arr.Length, cancellationToken).ConfigureAwait(false);
            }
        }

        private async Task ReadLoopStreamAsync(Stream stream, CancellationToken ct)
        {
            byte[] readBuf = new byte[4096];
            while (!ct.IsCancellationRequested && _isConnected)
            {
                try
                {
                    int bytesRead = await stream.ReadAsync(readBuf, 0, readBuf.Length, ct);
                    if (bytesRead > 0)
                    {
                        DataReceived?.Invoke(readBuf, 0, bytesRead);
                    }
                    else
                    {
                        await Task.Delay(10, ct);
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    OnError?.Invoke(ex);
                    break;
                }
            }
        }

        private void ReadLoopNativeAsync(IntPtr hComm, CancellationToken ct)
        {
            byte[] readBuf = new byte[4096];
            while (!ct.IsCancellationRequested && _isConnected)
            {
                try
                {
                    unsafe
                    {
                        fixed (byte* pBuf = readBuf)
                        {
                            if (Win32Serial.ReadFile(hComm, (IntPtr)pBuf, (uint)readBuf.Length, out uint bytesRead, IntPtr.Zero))
                            {
                                if (bytesRead > 0)
                                {
                                    DataReceived?.Invoke(readBuf, 0, (int)bytesRead);
                                }
                                else
                                {
                                    Thread.Sleep(5);
                                }
                            }
                            else
                            {
                                int err = Marshal.GetLastWin32Error();
                                if (err != 0)
                                {
                                    OnError?.Invoke(new IOException($"Serial read error: {err}"));
                                    break;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    OnError?.Invoke(ex);
                    break;
                }
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _ = DisconnectAsync();
            }
        }

        #region Win32 P/Invoke

        private static class Win32Serial
        {
            public const uint GENERIC_READ = 0x80000000;
            public const uint GENERIC_WRITE = 0x40000000;
            public const uint OPEN_EXISTING = 3;

            [StructLayout(LayoutKind.Sequential)]
            public struct COMMTIMEOUTS
            {
                public uint ReadIntervalTimeout;
                public uint ReadTotalTimeoutMultiplier;
                public uint ReadTotalTimeoutConstant;
                public uint WriteTotalTimeoutMultiplier;
                public uint WriteTotalTimeoutConstant;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct DCB
            {
                public uint DCBlength;
                public uint BaudRate;
                public uint Flags;
                public ushort wReserved;
                public ushort XonLim;
                public ushort XoffLim;
                public byte ByteSize;
                public byte Parity;
                public byte StopBits;
                public sbyte XonChar;
                public sbyte XoffChar;
                public sbyte ErrorChar;
                public sbyte EofChar;
                public sbyte EvtChar;
                public ushort wReserved1;

                public uint fBinary { get => Flags & 1; set => Flags = (Flags & ~1u) | (value & 1u); }
            }

            [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            public static extern IntPtr CreateFile(
                string lpFileName,
                uint dwDesiredAccess,
                uint dwShareMode,
                IntPtr lpSecurityAttributes,
                uint dwCreationDisposition,
                uint dwFlagsAndAttributes,
                IntPtr hTemplateFile);

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool CloseHandle(IntPtr hObject);

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool GetCommState(IntPtr hFile, ref DCB lpDCB);

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool SetCommState(IntPtr hFile, ref DCB lpDCB);

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool SetCommTimeouts(IntPtr hFile, ref COMMTIMEOUTS lpCommTimeouts);

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool ReadFile(
                IntPtr hFile,
                IntPtr lpBuffer,
                uint nNumberOfBytesToRead,
                out uint lpNumberOfBytesRead,
                IntPtr lpOverlapped);

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool WriteFile(
                IntPtr hFile,
                IntPtr lpBuffer,
                uint nNumberOfBytesToWrite,
                out uint lpNumberOfBytesWritten,
                IntPtr lpOverlapped);
        }

        #endregion
    }
}
