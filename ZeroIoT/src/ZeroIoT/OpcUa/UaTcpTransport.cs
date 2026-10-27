using System;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace ZeroIoT.OpcUa
{
    /// <summary>
    /// Pure C# non-blocking UA-TCP transport layer handling connection establishment,
    /// X.509 TLS/mTLS encryption, and message framing.
    /// </summary>
    public class UaTcpTransport : IDisposable
    {
        private readonly OpcUaClientOptions _options;
        private TcpClient? _client;
        private Stream? _stream;

        public string Host => _options.Host;
        public int Port => _options.Port;
        public string EndpointUrl => _options.EndpointUrl ?? $"opc.tcp://{Host}:{Port}";
        public bool IsConnected => _client != null && _client.Connected;

        public uint ServerProtocolVersion { get; private set; }
        public uint ServerReceiveBufferSize { get; private set; }
        public uint ServerSendBufferSize { get; private set; }
        public uint ServerMaxMessageSize { get; private set; }
        public uint ServerMaxChunkCount { get; private set; }

        public UaTcpTransport(string host, int port = 4840, string? endpointUrl = null)
            : this(new OpcUaClientOptions
            {
                Host = host ?? throw new ArgumentNullException(nameof(host)),
                Port = port,
                EndpointUrl = endpointUrl
            })
        {
        }

        public UaTcpTransport(OpcUaClientOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
        {
            if (IsConnected) return true;

            _client = new TcpClient();
            _client.NoDelay = true;
            await _client.ConnectAsync(Host, Port).ConfigureAwait(false);

            Stream networkStream = _client.GetStream();

            if (_options.UseTls)
            {
                var sslStream = new SslStream(
                    networkStream,
                    leaveInnerStreamOpen: false,
                    userCertificateValidationCallback: _options.CertificateValidationCallback
                );

                string targetHost = _options.TargetHost ?? Host;
                var clientCerts = _options.ClientCertificates ?? new X509CertificateCollection();

                #if NET8_0_OR_GREATER
                var sslOptions = new SslClientAuthenticationOptions
                {
                    TargetHost = targetHost,
                    ClientCertificates = clientCerts,
                    EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                    CertificateRevocationCheckMode = X509RevocationMode.NoCheck
                };
                await sslStream.AuthenticateAsClientAsync(sslOptions, cancellationToken).ConfigureAwait(false);
                #else
                await sslStream.AuthenticateAsClientAsync(
                    targetHost,
                    clientCerts,
                    SslProtocols.Tls12,
                    checkCertificateRevocation: false
                ).ConfigureAwait(false);
                #endif

                _stream = sslStream;
            }
            else
            {
                _stream = networkStream;
            }

            // 1. Send Hello Message (HEL)
            byte[] helloPacket = UaBinaryEncoder.EncodeHello(EndpointUrl);
            await _stream.WriteAsync(helloPacket, 0, helloPacket.Length, cancellationToken).ConfigureAwait(false);

            // 2. Read ACK Header (8 bytes)
            byte[] headerBuf = new byte[8];
            int readTotal = 0;
            while (readTotal < 8)
            {
                int r = await _stream.ReadAsync(headerBuf, readTotal, 8 - readTotal, cancellationToken).ConfigureAwait(false);
                if (r == 0) throw new EndOfStreamException("OPC-UA Server disconnected during HEL/ACK exchange.");
                readTotal += r;
            }

            if (!UaBinaryDecoder.TryReadHeader(headerBuf, out var header))
            {
                throw new InvalidOperationException("Failed to decode UA-TCP header.");
            }

            if (header.MessageType == "ERR")
            {
                throw new InvalidOperationException("OPC-UA Server returned ERR response to Hello.");
            }

            if (header.MessageType != "ACK")
            {
                throw new InvalidOperationException($"Unexpected UA-TCP message type: {header.MessageType}");
            }

            // 3. Read ACK Payload (MessageSize - 8 bytes)
            int payloadSize = (int)header.MessageSize - 8;
            byte[] payloadBuf = new byte[payloadSize];
            readTotal = 0;
            while (readTotal < payloadSize)
            {
                int r = await _stream.ReadAsync(payloadBuf, readTotal, payloadSize - readTotal, cancellationToken).ConfigureAwait(false);
                if (r == 0) throw new EndOfStreamException("OPC-UA Server closed connection prematurely.");
                readTotal += r;
            }

            var decoder = new UaBinaryDecoder(payloadBuf);
            ServerProtocolVersion = decoder.ReadUInt32();
            ServerReceiveBufferSize = decoder.ReadUInt32();
            ServerSendBufferSize = decoder.ReadUInt32();
            ServerMaxMessageSize = decoder.ReadUInt32();
            ServerMaxChunkCount = decoder.ReadUInt32();

            return true;
        }

        public async Task SendMessageAsync(byte[] messageBytes, CancellationToken cancellationToken = default)
        {
            if (!IsConnected || _stream == null)
                throw new InvalidOperationException("Transport is not connected.");

            await _stream.WriteAsync(messageBytes, 0, messageBytes.Length, cancellationToken).ConfigureAwait(false);
        }

        public async Task<byte[]> ReceiveMessageAsync(CancellationToken cancellationToken = default)
        {
            if (!IsConnected || _stream == null)
                throw new InvalidOperationException("Transport is not connected.");

            byte[] headerBuf = new byte[8];
            int readTotal = 0;
            while (readTotal < 8)
            {
                int r = await _stream.ReadAsync(headerBuf, readTotal, 8 - readTotal, cancellationToken).ConfigureAwait(false);
                if (r == 0) throw new EndOfStreamException("Server closed connection while awaiting message.");
                readTotal += r;
            }

            if (!UaBinaryDecoder.TryReadHeader(headerBuf, out var header))
            {
                throw new InvalidOperationException("Malformed UA-TCP header received.");
            }

            int fullSize = (int)header.MessageSize;
            byte[] fullMessage = new byte[fullSize];
            Array.Copy(headerBuf, 0, fullMessage, 0, 8);

            int payloadRemaining = fullSize - 8;
            int offset = 8;
            while (payloadRemaining > 0)
            {
                int r = await _stream.ReadAsync(fullMessage, offset, payloadRemaining, cancellationToken).ConfigureAwait(false);
                if (r == 0) throw new EndOfStreamException("Incomplete message received from server.");
                offset += r;
                payloadRemaining -= r;
            }

            return fullMessage;
        }

        public void Disconnect()
        {
            try { _stream?.Dispose(); } catch { }
            try { _client?.Dispose(); } catch { }
            _stream = null;
            _client = null;
        }

        public void Dispose()
        {
            Disconnect();
        }
    }
}
