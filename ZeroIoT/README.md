# ZeroIoT: Industrial Protocol Connectors for .NET

[![ZeroPlatform Tier](https://img.shields.io/badge/ZeroPlatform-Tier%202%20(Transport%20%26%20Storage)-059669.svg)](https://github.com/kzxl/ZeroPlatform)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET Multi-Targeting](https://img.shields.io/badge/.NET-8.0%20%7C%204.6.2%20%7C%20Standard%202.0-purple.svg)](https://dotnet.microsoft.com/)
[![NuGet Version](https://img.shields.io/badge/NuGet-1.2.0-blue.svg)](https://www.nuget.org/packages/ZeroIoT)

**ZeroIoT** is a high-throughput, pure C# industrial protocol connector library for .NET with **zero external dependencies**. It provides zero-allocation memory pipeline decoders, binary encoders, and network clients for industrial edge computing, PLC integration, and SCADA telemetry.

## Key Features

- **MQTT Exactly-Once QoS 2 & mTLS**: Ultra-lightweight asynchronous client (`MqttClient`), zero-allocation packet encoder/decoder (`MqttPacketEncoder`, `MqttPacketDecoder`) with full 4-way QoS 2 handshake (`PUBREC`/`PUBREL`/`PUBCOMP`), in-flight tracking table (`Qos2FlightTable`), and X.509 mTLS mutual authentication (`MqttClientOptions`).
- **OPC-UA Binary Protocol & TLS**: Pure C# binary encoder/decoder (`UaBinaryEncoder`, `UaBinaryDecoder`), variant serialization (`UaVariant`), NodeId addressing, asynchronous TCP transport (`UaTcpTransport`), and X.509 TLS/mTLS encryption (`OpcUaClientOptions`).
- **Sparkplug B**: Industrial IoT edge payload codec (`SparkplugCodec`) and typed metric models (`SparkplugMetric`).
- **Telemetry Bridges**: Integrated streaming bridge to `ZeroData` DataFrame (`IotDataFrameBridge`) and `ZeroStorage` Gorilla TSDB sink (`IotGorillaTsdbSink`).
- **Zero External Dependencies**: 100% Pure C# across .NET 8, .NET Standard 2.0, and .NET Framework 4.6.2.

## Usage Examples

### 1. MQTT Client with QoS 2 and X.509 mTLS

```csharp
using System.Security.Cryptography.X509Certificates;
using ZeroIoT.Mqtt;

var options = new MqttClientOptions
{
    Host = "broker.hivemq.com",
    Port = 8883,
    UseTls = true,
    ClientCertificates = new X509CertificateCollection
    {
        new X509Certificate2("client.pfx", "password")
    }
};

using var client = new MqttClient(options);
await client.ConnectAsync();

// Publish with Exactly-Once QoS 2 semantics
byte[] payload = System.Text.Encoding.UTF8.GetBytes("{\"temperature\": 24.5}");
await client.PublishAsync("factory/line1/sensor", payload, MqttQoS.ExactlyOnce);
```

### 2. OPC-UA Client with X.509 TLS Security

```csharp
using ZeroIoT.OpcUa;

var options = new OpcUaClientOptions
{
    Host = "192.168.1.100",
    Port = 4840,
    UseTls = true
};

using var client = new OpcUaClient(options);
await client.ConnectAsync();

var value = client.Read(new NodeId(2, "MotorSpeed"));
```

## Multi-Targeting

- `.NET 8.0+`
- `.NET Framework 4.6.2+`
- `.NET Standard 2.0`

## License

MIT License. Copyright © 2026 Phong Võ (`kzxl`). Part of the **ZeroPlatform** project.
