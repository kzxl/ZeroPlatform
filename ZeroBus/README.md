# ZeroBus

[![ZeroPlatform Tier](https://img.shields.io/badge/ZeroPlatform-Tier%202%20(Transport%20%26%20Storage)-14532d.svg)](https://github.com/kzxl/ZeroPlatform)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET Multi-Targeting](https://img.shields.io/badge/.NET-8.0%20%7C%204.6.2%20%7C%20Standard%202.0-purple.svg)](https://dotnet.microsoft.com/)
[![Pure C#](https://img.shields.io/badge/Dependencies-0%20(Pure%20C%23)-brightgreen.svg)]()

**ZeroBus** is a deterministic industrial motion fieldbus engine for .NET with **zero external dependencies**. It provides:
1. **CAN 2.0A / 2.0B & SocketCAN Core**: Zero-allocation raw CAN frame transport, hardware acceptance filters, and lock-free virtual bus routers.
2. **CANopen Application Layer (CiA 301)**: Network Management (NMT Master), Service Data Object (SDO Client with expedited & segmented transfers), and Process Data Objects (PDO).
3. **CiA 402 Servo Drive & Motion Control**: Multi-axis servo drive state machine transitions (Shutdown, SwitchOn, EnableOperation) with Profile Position (PPM), Profile Velocity (PVM), and Cyclic Synchronous Position (CSP).
4. **EtherCAT Master Stack**: Pure C# EtherCAT state machine (Init, Pre-Op, Safe-Op, Op), Datagram serialization (APRD, FPRD, BRD, LRW), CoE (CANopen over EtherCAT), and sub-millisecond cyclic PDO exchange.

---

## 🏛️ Architecture & Governance

ZeroBus complies strictly with **SPEC-ARCH-001** as a **Tier 2 (Transport & Storage)** subsystem. It cleanly separates real-time deterministic servo motion fieldbuses (`ZeroBus`) from general PLC register polling (`ZeroComm`).

```
       Tier 0: ZeroPrimitives & ZeroConcurrency (Lock-Free Ring Buffers & Schedulers)
                         ↓
       Tier 2: ZeroBus (CAN / CANopen CiA 402 & EtherCAT Master)
                         ↓
       Tier 3: ZeroMotion (Kinematics & Trajectory Feedforward Coordinates)
```

---

## 📦 Developer API Examples

### 1. Multi-Axis Servo Control over CANopen (CiA 402)

```csharp
using ZeroBus.Can;
using ZeroBus.CanOpen;

// 1. Initialize Virtual or SocketCAN Transport
var bus = new VirtualCanBus();
using var transport = new VirtualCanTransport(bus);
await transport.ConnectAsync();

// 2. Attach CANopen NMT Master & SDO Client
var nmt = new NmtMaster(transport);
var sdo = new SdoClient(transport);

// 3. Connect CiA 402 Servo Drive (Node ID 1)
var drive = new CiA402Drive(nodeId: 1, transport, sdo);

// 4. Power up servo state machine
await nmt.StartNodeAsync(1);
await drive.StartupServoAsync();

// 5. Send Cyclic Synchronous Position (CSP) command
await drive.SendCspPdoAsync(controlword: 0x000F, targetPosition: 125000);
```

### 2. EtherCAT Master Scan & State Transition

```csharp
using ZeroBus.EtherCat;

using var ecTransport = new VirtualEtherCatTransport();
await ecTransport.ConnectAsync();

var master = new EtherCatMaster(ecTransport);

// 1. Scan online slaves via Broadcast Read (BRD)
int slaveCount = await master.ScanSlavesAsync();
Console.WriteLine($"Discovered {slaveCount} EtherCAT slaves.");

// 2. Transition slaves from Init -> Pre-Op -> Safe-Op -> Operational
await master.TransitionStateAsync(EtherCatState.PreOp);
await master.TransitionStateAsync(EtherCatState.SafeOp);
await master.TransitionStateAsync(EtherCatState.Op);

// 3. High-frequency cyclic PDO process data exchange
byte[] txPdo = new byte[32]; // Target positions / controlwords
byte[] rxPdo = new byte[32]; // Actual positions / statuswords
ushort wkc = await master.ExchangeProcessDataAsync(txPdo, rxPdo);
```

### 3. Multi-Axis Motion Coordination & Following-Error Trip

```csharp
using ZeroBus.CanOpen;

// Multi-axis coordinator ensures lockstep execution and mechanical safety
var sync = new SyncProducer(transport);
var coordinator = new MotionBusCoordinator(sync);

coordinator.AddAxis(driveAxis1, maxAllowedFollowingError: 500);
coordinator.AddAxis(driveAxis2, maxAllowedFollowingError: 500);

// Emergency trip callback (triggers sub-ms QuickStop on mechanical jamming)
coordinator.OnFollowingErrorTripped += (nodeId, error, max) =>
{
    Console.WriteLine($"[EMERGENCY] Node {nodeId} tripped following error: {error} counts!");
};

// Cyclic synchronous tick (called at 250Hz - 1kHz)
await coordinator.SyncTickAsync(new int[] { targetPos1, targetPos2 });
```

### 4. CANopen Heartbeat & Liveness Monitor

```csharp
var monitor = new HeartbeatMonitor(transport);
monitor.RegisterNode(nodeId: 2);

monitor.OnNodeTimeout += nodeId =>
{
    Console.WriteLine($"[ALERT] Servo Drive Node {nodeId} connection lost!");
};

// Periodically evaluate slave liveness
monitor.CheckTimeouts(TimeSpan.FromMilliseconds(200));
```

### 5. EtherCAT CoE (CANopen over EtherCAT) Mailbox Transfer

```csharp
using ZeroBus.EtherCat;

// Build SDO Download (Write Target Velocity 0x60FF)
byte[] sdoWriteFrame = EtherCatCoeMailbox.BuildSdoDownload(
    index: 0x60FF, subIndex: 0,
    value: BitConverter.GetBytes(50000),
    slaveAddress: 1001
);

// Build SDO Upload (Read Actual Position 0x6064)
byte[] sdoReadFrame = EtherCatCoeMailbox.BuildSdoUpload(index: 0x6064, subIndex: 0);
```

---

## 📄 License

MIT License © 2026 Phong Võ. Part of the **ZeroPlatform** project.
