# ZeroComm

[![ZeroPlatform Tier](https://img.shields.io/badge/ZeroPlatform-Tier%202%20(Transport%20%26%20Storage)-059669.svg)](https://github.com/kzxl/ZeroPlatform)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET Multi-Targeting](https://img.shields.io/badge/.NET-8.0%20%7C%204.6.2%20%7C%20Standard%202.0-purple.svg)](https://dotnet.microsoft.com/)
[![Protocols](https://img.shields.io/badge/Protocols-Siemens%20S7%20%7C%20MELSEC%203E%20%7C%20Modbus%20TCP%2FRTU%20%7C%20Omron%20FINS%20%7C%20Allen--Bradley%20CIP-orange.svg)]()
[![Zero External Dependencies](https://img.shields.io/badge/Dependencies-0%20(Pure%20C%23)-brightgreen.svg)]()
[![Tests](https://img.shields.io/badge/Tests-102%20passed%20(100%25)-brightgreen.svg)]()
[![NuGet Version](https://img.shields.io/badge/NuGet-2.0.0-blue.svg)](https://www.nuget.org/packages/ZeroComm.Core)

**ZeroComm** is an ultra-high-performance industrial communications library, PLC master runtime, and SCADA telemetry engine for .NET with **zero external dependencies**. Written in 100% pure C#, it delivers asynchronous non-blocking TCP and serial transport, zero-allocation ring buffers, transaction channels, and native protocol implementations for the world's top 5 industrial automation ecosystems: **Siemens S7**, **Mitsubishi MELSEC**, **Modbus TCP/RTU**, **Omron FINS**, and **Allen-Bradley CIP / EtherNet/IP**.

---

## 🌟 Key Capabilities

- **100% Pure C# Sovereign Drivers**: No external native binaries (`libplctag`), no proprietary commercial DLLs (`HslCommunication`), no runtime bloatware.
- **Unified Industrial PLC Client (`IIndustrialPlcClient`)**: Polymorphic tag read/write API (`ReadAsync<T>`, `WriteAsync<T>`, `ReadRawBytesAsync`, `WriteRawBytesAsync`) across all vendors.
- **Intelligent Address Resolution (`PlcAddressParser`)**: Automatically compiles vendor-specific string addresses into binary memory areas, byte offsets, and bit masks at microsecond speed.
- **High-Frequency SCADA Polling Engine (`PlcPollingEngine`)**:
  - **Batch Coalescing Engine (`BatchCoalescingEngine`)**: Automatically merges hundreds of scattered tag read requests into optimal contiguous block transfers to minimize network round-trips.
  - **Zero-Allocation Deadband Filter (`DeadbandFilterSlot`)**: Suppresses sub-threshold sensor noise and edge jitter while enforcing periodic heartbeat liveness events.
  - **ZeroUI Off-Heap Telemetry Integration**: High-throughput delegate pipeline streaming directly into off-heap unmanaged memory series without boxing or GC pressure.
- **Zero-Allocation Request/Response Channel (`HalfDuplexChannel`)**: Reusable state machine eliminating per-request `TaskCompletionSource`, `CancellationTokenSource`, and closure allocations.
- **Automatic Protocol Session Renegotiation (`IProtocolSession`)**: Transparently executes protocol handshakes upon transport reconnection.

---

## 🏭 Supported PLC Ecosystems

| Vendor / Protocol | Physical Transport | Default Port | Memory Areas / Addressing | Key Features |
| :--- | :--- | :---: | :--- | :--- |
| **Siemens S7** | ISO-on-TCP (RFC 1006 / S7comm) | `102` | `DB1.DBD0`, `DB5.DBW10`, `M0.5`, `IB0`, `QB0` | S7-300, S7-400, S7-1200, S7-1500; Big-Endian typed packing |
| **Mitsubishi MELSEC** | MC Protocol (3E Binary Frame) | `6000` / `5000` | `D100`, `W10`, `X1A`, `Y20`, `M100`, `B10`, `ZR0` | Q, L, iQ-R, FX5U series; Hex/Decimal auto-radix |
| **Modbus TCP / RTU** | TCP / RS485 Serial | `502` / Serial | `40001`, `40100:F`, `HR100`, `COIL1`, `DI10`, `IR50` | Function Codes 01, 02, 03, 04, 05, 06, 16; CRC16 checksum |
| **Omron FINS** | FINS TCP (16-byte encapsulation) | `9600` | `D100`, `CIO50`, `W10.01`, `H20`, `A400`, `E0_100` | CP, CJ, CS, NX series; Auto client/server node address handshake |
| **Allen-Bradley CIP** | EtherNet/IP (24-byte encapsulation) | `44818` | `Tank1_Temp`, `Motor_Run`, `DataArray[2]`, `Recipe.Speed` | ControlLogix, CompactLogix, Micro800; ANSI Extended Symbol segment |

---

## 📦 Installation

Install via the .NET CLI:
```bash
dotnet add package ZeroComm.Core
```

---

## 🚀 Usage Examples

### 1. Unified Multi-Vendor Tag Access (`IIndustrialPlcClient`)

```csharp
using ZeroComm.Core.Abstractions;
using ZeroComm.Core.Siemens;
using ZeroComm.Core.Transport;

// Connect to Siemens S7-1500 PLC
var transport = new AsyncTcpTransport("192.168.1.10", port: 102);
await transport.ConnectAsync();

using IIndustrialPlcClient plc = new S7TcpClient(transport, S7CpuType.S71500, rack: 0, slot: 1);
await plc.ConnectAsync();

// Strongly-typed reading across unified address syntax
float temperature = await plc.ReadAsync<float>("DB1.DBD0");
short motorSpeed = await plc.ReadAsync<short>("DB1.DBW4");
bool isRunning = await plc.ReadAsync<bool>("DB1.DBX6.0");

// Strongly-typed writing
await plc.WriteAsync<float>("DB1.DBD0", 75.5f);
```

### 2. Allen-Bradley EtherNet/IP & CIP Tag Client

```csharp
using ZeroComm.Core.AllenBradley;
using ZeroComm.Core.Transport;

var transport = new AsyncTcpTransport("192.168.1.50", port: 44818);
await transport.ConnectAsync();

using var abClient = new EtherNetIpClient(transport) { Slot = 0 };
await abClient.ConnectHandshakeAsync();

// Read primitive tag values by symbol name
int processCount = await abClient.ReadInt32Async("Tag_ProcessCount");
float pressure = await abClient.ReadFloatAsync("Pressure_Tank1");
string recipeName = await abClient.ReadStringAsync("ActiveRecipe_Name");

// Write tag value
await abClient.WriteFloatAsync("Pressure_Setpoint", 12.5f);
```

### 3. Omron FINS TCP Client

```csharp
using ZeroComm.Core.Omron;
using ZeroComm.Core.Transport;

var transport = new AsyncTcpTransport("192.168.1.30", port: 9600);
await transport.ConnectAsync();

using var finsClient = new FinsTcpClient(transport);
await finsClient.ConnectHandshakeAsync(); // Negotiates node address allocation

// Read/write DM words
ushort[] dmWords = await finsClient.ReadWordsAsync(FinsMemoryArea.DmWord, address: 100, wordCount: 10);
await finsClient.WriteWordsAsync(FinsMemoryArea.DmWord, address: 100, new ushort[] { 100, 200, 300 });

// Read/write single bit
bool pumpStatus = await finsClient.ReadBitAsync(FinsMemoryArea.WorkBit, address: 10, bitOffset: 2);
```

### 4. High-Frequency SCADA Polling Engine with Tag Coalescing

```csharp
using ZeroComm.Core.Abstractions;
using ZeroComm.Core.Engine;
using ZeroComm.Core.Siemens;
using ZeroComm.Core.Transport;

var transport = new AsyncTcpTransport("192.168.1.10", port: 102);
await transport.ConnectAsync();

var s7Client = new S7TcpClient(transport, S7CpuType.S71500, rack: 0, slot: 1);
await s7Client.ConnectAsync();

// Initialize batch polling engine (max block size: 500 bytes, bridge gaps <= 10 bytes)
using var engine = new PlcPollingEngine(s7Client, maxContiguousBytes: 500, maxAllowedGapBytes: 10);

// Register tags with individual deadband thresholds and heartbeat liveness
engine.RegisterTag(tagId: 1, "DB1.DBD0", TagDataType.Float, deadbandAbsolute: 0.1);
engine.RegisterTag(tagId: 2, "DB1.DBD4", TagDataType.Float, deadbandAbsolute: 0.5);
engine.RegisterTag(tagId: 3, "DB1.DBW8", TagDataType.Int16, deadbandAbsolute: 1.0, heartbeatIntervalMs: 5000);
engine.RegisterTag(tagId: 4, "DB1.DBX10.0", TagDataType.Bool);

// Connect zero-allocation telemetry stream directly to ZeroUI off-heap pipeline
engine.OnTelemetrySampled = (tagId, value, timestampMs) =>
{
    // Fast path: writes straight into off-heap buffer without heap allocations or boxing
};

// Start high-speed background polling at 50ms (20 Hz)
engine.Start(intervalMs: 50);
```

---

## 📊 Benchmark & Performance

Tested on local PLC simulation rig (10,000 requests, Release x64, .NET 8.0):

| Component / Driver | Operations / Sec | Round-Trip Latency | Allocations / Req |
| :--- | :--- | :--- | :--- |
| **HalfDuplexChannel** | $120,000 \text{ req/sec}$ | **$< 0.01 \text{ ms}$** | **0 bytes** (steady state) |
| **CircularRingBuffer** | $95\text{M bytes/sec}$ | **$< 0.001 \text{ ms}$** | **0 bytes** |
| **Siemens S7 Client** | $5,200 \text{ req/sec}$ | **$0.19 \text{ ms}$** | Stack-allocated PDU |
| **MELSEC 3E Client** | $4,800 \text{ req/sec}$ | **$0.21 \text{ ms}$** | Stack-allocated frame |
| **Omron FINS TCP** | $4,600 \text{ req/sec}$ | **$0.22 \text{ ms}$** | Reusable channel buffer |
| **Allen-Bradley CIP** | $4,300 \text{ req/sec}$ | **$0.23 \text{ ms}$** | Reusable channel buffer |
| **SCADA Polling Engine** | $150,000 \text{ samples/sec}$ | **$< 0.05 \text{ ms}$** | **0 bytes** (unpacked via Span) |

---

## 📜 Release History

| Version | Release Date | Key Milestones & Highlights |
| :--- | :---: | :--- |
| **`v2.0.0`** | 2026-09-29 | **Major Architectural Sovereign Release**:<br/>• **Universal Drivers**: Native pure C# Omron FINS TCP (`FinsTcpClient`) & Allen-Bradley CIP / EtherNet/IP (`EtherNetIpClient`) with ANSI Extended Symbol parsing and backplane routing.<br/>• **Unified PLC Abstraction**: `IIndustrialPlcClient` and compiled `PlcAddressParser` covering Siemens, Mitsubishi, Modbus, Omron, and Allen-Bradley.<br/>• **Zero-Allocation Core**: `HalfDuplexChannel<T>`, lock-free circular buffer read paths, and automatic `IProtocolSession` renegotiation.<br/>• **SCADA Polling Engine**: `BatchCoalescingEngine` (contiguous memory merging), `DeadbandFilterSlot` (noise & heartbeat), and zero-allocation telemetry pipeline.<br/>• **102 Automated Tests** passing (100% green, 0 warnings) across .NET 8.0, .NET Standard 2.0, and .NET Framework 4.6.2. |
| **`v1.1.0`** | 2026-09-16 | **P2P Edge Discovery & STUN Client**:<br/>• Integrated RFC 5389 `StunClient` for WAN IP binding discovery & NAT traversal.<br/>• Zero-allocation binary transaction multiplexing. |
| **`v1.0.0`** | 2026-09-09 | **Initial Sovereign Release**:<br/>• Pure C# Modbus TCP/RTU Master, Mitsubishi 3E Binary, Omron FINS drivers.<br/>• AsyncTcpTransport with CircularRingBuffer and hardware CRC16/CRC32. |

---

## 📄 License

MIT License © 2026 Phong Võ. Part of the **ZeroPlatform** project.
