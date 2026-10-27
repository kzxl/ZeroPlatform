using System;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace ZeroIoT.Mqtt
{
    /// <summary>
    /// Configuration options for MQTT client connection, including TLS/X.509 mTLS.
    /// </summary>
    public class MqttClientOptions
    {
        public string Host { get; set; } = "localhost";
        public int Port { get; set; } = 1883;
        public string? ClientId { get; set; }
        public ushort KeepAliveSeconds { get; set; } = 60;
        public string? Username { get; set; }
        public string? Password { get; set; }
        public bool CleanSession { get; set; } = true;

        /// <summary>
        /// Enable TLS/SSL transport encryption.
        /// </summary>
        public bool UseTls { get; set; }

        /// <summary>
        /// Target host for SSL certificate verification. If null, Host is used.
        /// </summary>
        public string? TargetHost { get; set; }

        /// <summary>
        /// Client certificates for mutual TLS (mTLS) authentication.
        /// </summary>
        public X509CertificateCollection? ClientCertificates { get; set; }

        /// <summary>
        /// Optional custom validation callback for server certificate.
        /// </summary>
        public RemoteCertificateValidationCallback? CertificateValidationCallback { get; set; }
    }
}
