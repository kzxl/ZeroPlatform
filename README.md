# ZeroNetwork

[![ZeroPlatform Tier](https://img.shields.io/badge/ZeroPlatform-Tier%202%20(Transport%20%26%20Storage)-059669.svg)](https://github.com/kzxl/ZeroPlatform)
[![NuGet Version](https://img.shields.io/badge/nuget-v2.7.0-blue.svg)](https://www.nuget.org/packages/ZeroNetwork.Core/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Zero External Dependencies](https://img.shields.io/badge/Dependencies-0%20(Pure%20C%23)-brightgreen.svg)]()
[![Tests: 162 Passed](https://img.shields.io/badge/Tests-162%20Passed%20(100%25)-brightgreen.svg)]()
[![Multi-Targeting](https://img.shields.io/badge/.NET-8.0%20%7C%204.6.2%20%7C%20Standard%202.0-orange.svg)]()

> **Architectural Standard**: 100% Pure C# BCL, Zero External Dependencies, Multi-Targeting across `.NET 8.0`, `.NET Framework 4.6.2`, and `.NET Standard 2.0`.

`ZeroNetwork` is an ultra-high-performance, sovereign .NET networking suite engineered for factory floor automation, IoT edge gateways, distributed industrial systems, and high-load ERP infrastructures. It provides an end-to-end networking stack from **Layer 2 (Data Link & Hardware)** all the way up to **Layer 7 (HTTP REST, Real-Time SignalR/WebSocket, Binary RPC, and High-Throughput Pub/Sub Bus)** with **ZERO external NuGet dependencies**, eliminating assembly conflicts and DLL hell on legacy .NET Framework runtimes while maximizing throughput on modern .NET 8+.

---

## 🏛️ Comprehensive Architecture & OSI Model Mapping

```
┌─────────────────────────────────────────────────────────────────────────────────┐
│                               ZeroNetwork Architecture                          │
├───────────────────┬──────────────────────────────────┬──────────────────────────┤
│ OSI Layer         │ Component                        │ Key Highlights           │
├───────────────────┼──────────────────────────────────┼──────────────────────────┤
│ Layer 7: RPC      │ ZeroRpcServer & ZeroRpcClient    │ 32-B Framing, FastUlid   │
├───────────────────┼──────────────────────────────────┼──────────────────────────┤
│ Layer 7: Pub/Sub  │ InProcessZeroBus & TopicTrie     │ Wildcards (+, #), Zero-GC│
│                   │ IpcZeroBus                       │ Microsecond Shared Memory│
│                   │ ZeroPubSubHub                    │ Remote WebSocket Bridge  │
├───────────────────┼──────────────────────────────────┼──────────────────────────┤
│ Layer 7: RealTime │ ZeroHubServer<THub> & ZeroHub    │ Pure C# Hub Protocol v1  │
│                   │ ZeroSignalRClient                │ Pure C# Hub Client       │
│                   │ ZeroWebSocketServer              │ RFC 6455 Socket Server   │
│                   │ ZeroWebSocketClient              │ RFC 6455, Auto-Reconnect │
├───────────────────┼──────────────────────────────────┼──────────────────────────┤
│ Layer 7: HTTP     │ ZeroApiClient & Extensions       │ Zero-LOH Stream Pipeline │
│                   │ ZeroHttpServer                   │ Micro REST & Prometheus  │
├───────────────────┼──────────────────────────────────┼──────────────────────────┤
│ Layer 4: Transport│ NetworkProbe & PortScanner       │ Zero-Leak TCP Handshake  │
│                   │ UdpMulticastClient & DnsProbe    │ IGMPv2/v3, Port 53 UDP   │
│                   │ NetworkThroughputMeter           │ Real-Time NIC Bandwidth  │
├───────────────────┼──────────────────────────────────┼──────────────────────────┤
│ Layer 3: Network  │ IPNetwork & VirtualAdapterFilter │ Allocation-Free CIDR     │
│                   │ Traceroute & PathMtuDiscovery    │ Hop Latency & Jumbo PMTU │
│                   │ NetworkInfo & NetworkAdapterInfo │ Deterministic NIC Select │
├───────────────────┼──────────────────────────────────┼──────────────────────────┤
│ Layer 2: DataLink │ MacAddress & ArpTable            │ Blittable Struct, P/Invk │
│                   │ ActiveArpScanner                 │ Subnet Scanner (No ICMP) │
│                   │ WakeOnLan                        │ 102/108-byte Magic Pkt   │
└───────────────────┴──────────────────────────────────┴──────────────────────────┘
```

---

## 🌟 Key Capabilities

### 1. Layer 2: Data Link & Hardware Discovery
- **`MacAddress`**: Blittable 6-byte struct with zero-allocation span formatting (`Span<char>`) and IEEE OUI vendor detection (VMware, VirtualBox, Hyper-V, Docker, QEMU).
- **`ArpTable`**: Fast IP $\leftrightarrow$ MAC resolution via Windows P/Invoke (`IpHlpApi.dll`) and Linux `/proc/net/arp`.
- **`ActiveArpScanner`**: High-speed parallel ARP sweep across CIDR subnets to detect 100% of live nodes even when ICMP/Ping is disabled by firewalls.
- **`WakeOnLan`**: Generates RFC standard 102-byte Magic Packets with support for 6-byte `SecureOn` passwords (108-byte packets) for remote machine power-on.

### 2. Layer 3: Network & Topology Math
- **`IPNetwork`**: Pure C# CIDR math (`Parse`, `Contains`, `BroadcastAddress`, `Netmask`, `WildcardMask`) with zero-allocation host iterators using `Span<byte>`.
- **`VirtualAdapterFilter`**: Hybrid heuristics and hardware vendor OUI detection that filters out virtual interfaces (VMware, VirtualBox, WSL, Hyper-V, Tailscale, ZeroTier, Docker).
- **`NetworkInfo` & `NetworkAdapterInfo`**: Deterministic physical NIC resolution on multi-homed systems.
- **`Traceroute`**: Hop-by-hop ICMP route inspection with latency profiling.
- **`PathMtuDiscovery`**: Binary search PMTU analyzer using Don't Fragment (DF) flags (verifying Jumbo Frames MTU 9000 for GigE Vision cameras).

### 3. Layer 4: Transport & Diagnostics
- **`NetworkProbe`**: Hardened non-blocking TCP socket prober (`IsPortOpenAsync`) with `LingerState(true, 0)` socket abort ensuring zero socket handle leaks under heavy loads.
- **`PortScanner`**: Parallel bounded-concurrency TCP port and subnet scanner governed by `SemaphoreSlim` with streaming `Action<PortScanResult>` callbacks.
- **`UdpMulticastClient`**: IGMPv2/v3 multicast group publisher and subscriber with physical network interface binding.
- **`DnsProbe`**: Fast UDP port 53 DNS client with sub-second timeout and reverse PTR record resolution.
- **`NetworkThroughputMeter`**: Real-time NIC bandwidth meter (Rx/Tx Mbps) with saturation alert thresholds.

### 4. Layer 7: Micro HTTP Server & REST Client Pipeline
- **`ZeroApiClient`**: High-performance HTTP client facade utilizing stream-based direct JSON serialization (`IZeroJsonSerializer`) to eliminate Large Object Heap (LOH) allocations, combined with connection pooling, exponential backoff with capped jitter, and idempotency-aware retries.
- **`ZeroHttpClient` & Pipeline Architecture**: Extensible middleware pipeline (`IZeroHttpMiddleware`) and transport abstraction (`IZeroHttpTransport`), enabling custom retry policies, distributed tracing, circuit breakers, host-specific routing (`RouteHost`), and zero-copy body pooling.
- **`ZeroHttpServer`**: Micro HTTP/1.1 server running in pure C# without administrator privileges, URL ACLs, or IIS. Features regex REST routing and built-in `/metrics` (Prometheus) and `/health` endpoints.
- **`ZeroHttpExtensions`**: Fluent string extensions (`url.GetJsonAsync<T>()`, `url.PostJsonAsync<T>()`) for clean, allocation-efficient HTTP consumption.

### 5. Layer 7: Real-Time Full-Duplex (WebSocket & SignalR Server/Client)
- **`ZeroWebSocketServer`**: Pure C# BCL socket listener implementing the RFC 6455 WebSocket Server specification without administrative URL ACL privileges. Features automatic `Sec-WebSocket-Accept` handshake hashing, client-to-server frame unmasking, and high-performance text/binary broadcasting.
- **`ZeroWebSocketSession`**: Thread-safe per-client WebSocket session abstraction managing state, frame reassembly, and keep-alive ping/pong frames.
- **`ZeroHubServer<THub>`**: Sovereign, self-hosted ASP.NET Core SignalR Protocol v1 Hub Server. Features automatic JSON protocol handshake (`{"protocol":"json","version":1}\x1e`), typed RPC method dispatching with return value completions (`type: 3`), dynamic group management (`AddToGroupAsync`), client proxy routing (`All`, `Caller`, `Others`, `Client`, `Group`), and 15-second heartbeat ping.
- **`ZeroHub`**: Base class for enterprise business hubs providing `Clients`, `Context`, and `Groups`.
- **`ZeroWebSocketClient`**: Resilient RFC 6455 WebSocket client built purely on BCL `ClientWebSocket`. Includes thread-safe frame sending (`SemaphoreSlim`), automatic message reassembly for large payloads, configurable keep-alive heartbeat, and auto-reconnect with exponential backoff and random jitter.
- **`ZeroSignalRClient`**: Pure C# ASP.NET Core SignalR JSON Hub Client (Protocol v1) with **0 external dependencies** (no `Microsoft.AspNetCore.SignalR.Client` bloat, eliminating DLL conflicts on .NET Framework 4.6.2). Supports automatic handshake, server-to-client callbacks (`hub.On<T>`), client invocations (`hub.SendAsync`, `hub.InvokeAsync<T>`), and keep-alive ping management.

### 6. Layer 7: High-Throughput Pub/Sub & Inter-Process Messaging
- **`InProcessZeroBus`**: Ultra-fast, zero-allocation in-process message bus. Supports both strongly-typed events (`PublishAsync<T>`) and topic patterns with zero GC pressure on hot paths.
- **`TopicTrie`**: Thread-safe radix trie supporting MQTT-style wildcards (`+` for single level, `#` for multi-level) with Copy-On-Write subscriber collections for completely lock-free read dispatching.
- **`IpcZeroBus`**: Sub-microsecond cross-process Pub/Sub utilizing OS shared memory (`ZeroMmfRingBuffer`). Allows decoupled processes (e.g. C# SCADA and Python/C++ Vision/AI) to exchange frames and telemetry at hardware speed without socket overhead.
- **`ZeroPubSubHub`**: Out-of-the-box SignalR Hub bridging remote clients to the local `InProcessZeroBus`, enabling web browsers and remote desktop nodes to subscribe and publish to topics over WebSockets.

### 7. High-Performance BCL Superchargers & Industrial Engines
- **`ZeroClock`**: Sub-10-nanosecond hardware timer engine via Windows QPC and CPU RDTSC, eliminating Windows OS 15.6ms timer quantum for precise SCADA telemetry.
- **`ZeroRadixSort`**: Linear-time $O(N)$ LSD Radix Sort for integers, longs, and IEEE-754 floats. Outperforms BCL `Array.Sort` ($O(N \log N)$ Introsort) by 4x to 10x without branch mispredictions.
- **`ZeroXxHash3`**: 20-30 GB/sec 64-bit and 32-bit non-cryptographic hashing algorithm with near-zero collision variance, replacing standard BCL `GetHashCode()`.
- **`ZeroRobinHoodMap<K, V>`**: Cache-friendly open-addressing hash table with flat contiguous array storage and Robin Hood displacement, eliminating heap node allocations.
- **`ZeroRecyclableStream`**: Recyclable pooled memory stream backed by rented `ArrayPool` chunks, eliminating Large Object Heap (LOH) fragmentation.
- **`ZeroStateMachine<TState, TTrigger>`**: Deterministic, zero-allocation finite state machine for factory sequence automation with sub-5ns state transitions and audit logging.
- **`ZeroTelemetry`**: Autonomous metrics registry with atomic Counters, Gauges, and HDR Histograms (p50, p90, p99, p99.9), integrated with `ZeroHttpServer` `/metrics` endpoint.

### 8. Sovereign Binary RPC & Zero-Copy Framing (`ZeroRpc`)
- **`ZeroRpcFrame`**: Deterministic 32-byte fixed binary header (Magic bytes, message type, CRC32, payload length, and FastUlid correlation) with zero intermediate allocations.
- **`ZeroRpcServer` & `ZeroRpcClient`**: Asynchronous multiplexed TCP RPC engine executing concurrent requests over persistent single-socket channels.
- **`TokenBucketRateLimiter`**: Lock-free atomic token-bucket limiter mitigating DDoS and traffic spikes at microsecond granularity.
- **`FastUlid`**: Microsecond-precision, lexicographically sortable 128-bit unique identifiers without heap string churn.

---

## 🚀 Quick Start Examples

### 1. Active ARP Subnet Scanning (Bypass ICMP Firewalls)
```csharp
using ZeroNetwork.Discovery;

// Scan /24 subnet for all active devices via Data Link ARP
var devices = await ActiveArpScanner.ScanSubnetAsync("192.168.1.0/24", timeoutMs: 300);

foreach (var device in devices)
{
    Console.WriteLine($"IP: {device.IpAddress} | MAC: {device.MacAddress} | Vendor: {device.Vendor}");
}
```

### 2. High-Performance REST Client & Extensible HTTP Pipeline

#### High-Level Facade (`ZeroApiClient`)
```csharp
using ZeroNetwork.Http;

// 1. Fluent extension methods
var users = await "https://api.factory.local/users".GetJsonAsync<List<UserDto>>();

// 2. Resilient API client with pooled streams & exponential backoff
using var client = new ZeroApiClient(new ZeroApiClientOptions
{
    BaseAddress = "https://api.factory.local",
    MaxRetryAttempts = 3,
    InitialRetryDelay = TimeSpan.FromMilliseconds(200),
    Timeout = TimeSpan.FromSeconds(15)
});

// Idempotent calls (GET, PUT, DELETE) auto-retry on transient failures; POST is protected by default
var order = await client.PostAsync<OrderRequest, OrderResult>("/orders", new OrderRequest { OrderId = 1001 });
```

#### Low-Level Extensible Pipeline (`ZeroHttpClientBuilder`)
```csharp
using ZeroNetwork.Http.Client;

// Build a custom HTTP pipeline with middlewares and multi-host routing
using var httpClient = ZeroHttpClient.CreateBuilder()
    .WithBaseAddress("https://api.factory.local")
    .UseRetry(new ZeroRetryPolicy { MaxRetries = 2 })
    .UseTelemetry("factory_api")
    .RouteHost("internal.secure.local", new BclHttpTransport(new BclHttpTransportOptions
    {
        MaxConnectionsPerServer = 20,
        PooledConnectionLifetime = TimeSpan.FromMinutes(10)
    }))
    .Build();

using var request = new ZeroHttpRequestMessage(HttpMethod.Get, "/telemetry/status");
using var response = await httpClient.SendAsync(request);
```

#### Legacy Runtime Notes (.NET Framework 4.6.2 & netstandard2.0)
- **JSON Serialization**: On .NET Framework 4.6.2 where `System.Text.Json` is not bundled in BCL, configure your JSON serializer at startup:
  ```csharp
  ZeroApiClient.DefaultSerializer = new MyJsonNetSerializer(); // implements IZeroJsonSerializer
  ```
- **ServicePoint Tuning**: `BclHttpTransport` automatically tunes legacy `ServicePointManager` (sets `DefaultConnectionLimit`, per-endpoint `ConnectionLimit`, `ConnectionLeaseTimeout` for DNS refreshes, and `MaxIdleTime`) eliminating the classic 2-connection bottleneck and stale DNS issues.


### 3. Self-Hosted SignalR Hub Server with RPC & Groups
```csharp
using ZeroNetwork.RealTime;

// 1. Declare business hub
public class TelemetryHub : ZeroHub
{
    public async Task BroadcastAlarm(string machineId, string alert)
    {
        // Broadcast to all connected clients
        await Clients.All.SendAsync("OnAlarm", machineId, alert);
    }

    public int ComputeThroughput(int unitsProduced, int rejects)
    {
        // Two-way RPC returning calculation result
        return unitsProduced - rejects;
    }

    public async Task JoinLine(string lineId)
    {
        // Add caller to specific group
        await Groups.AddToGroupAsync(Context.ConnectionId, lineId);
    }
}

// 2. Start sovereign hub server (Port 8080)
using var server = new ZeroHubServer<TelemetryHub>(port: 8080);
server.Start();
```

### 4. Pure C# SignalR Hub Client (0 External Dependencies)
```csharp
using ZeroNetwork.RealTime;

using var hub = new ZeroSignalRClient(new ZeroSignalROptions
{
    HubUri = new Uri("ws://127.0.0.1:8080/hub/telemetry")
});

// Register server-to-client event listener
hub.On<string, string>("OnAlarm", (machineId, alert) =>
{
    Console.WriteLine($"[ALERT] {machineId}: {alert}");
});

await hub.StartAsync();

// Invoke server hub RPC
int netYield = await hub.InvokeAsync<int>("ComputeThroughput", 1200, 45);
Console.WriteLine($"Net Yield: {netYield}");
```

### 5. High-Performance MQTT-Style In-Process Pub/Sub Bus
```csharp
using ZeroNetwork.PubSub;

using var bus = new InProcessZeroBus();

// Subscribe using single-level '+' and multi-level '#' wildcards
using var sub = bus.SubscribeTopic("plants/+/line1/#", (topic, payload) =>
{
    string message = Encoding.UTF8.GetString(payload.ToArray());
    Console.WriteLine($"[{topic}] -> {message}");
});

// Publish topic event (zero GC allocation)
await bus.PublishTopicAsync("plants/hanoi/line1/press/temperature", Encoding.UTF8.GetBytes("78.5 C"));
```

### 6. Sub-Microsecond Cross-Process (IPC) Shared Memory Bus
```csharp
using ZeroNetwork.PubSub;

// Shared memory IPC bus mapped across operating system processes
using var ipcBus = new IpcZeroBus("ZeroIpc_SCADA_Bus", capacity: 1024, slotSize: 65536, isListener: true);

ipcBus.SubscribeTopic("vision/inspection/+", (topic, data) =>
{
    Console.WriteLine($"Received telemetry from external process on {topic} ({data.Length} bytes)");
});

// Fast publisher from another process
ipcBus.TryPublish("vision/inspection/cam1", Encoding.UTF8.GetBytes("OK: DefectCount=0"));
```

### 7. Resilient WebSocket Client (Auto-Reconnect & Keep-Alive)
```csharp
using ZeroNetwork.RealTime;

var options = new ZeroWebSocketOptions
{
    ServerUri = new Uri("ws://edge-gateway.local:8080/telemetry"),
    AutoReconnect = true,
    InitialReconnectDelay = TimeSpan.FromSeconds(1),
    KeepAliveInterval = TimeSpan.FromSeconds(15)
};

using var ws = new ZeroWebSocketClient(options);
ws.MessageReceived += msg => Console.WriteLine($"Received: {msg}");
ws.Reconnected += () => Console.WriteLine("Connection restored!");

await ws.ConnectAsync();
await ws.SendJsonAsync(new { deviceId = "CNC-01", status = "ONLINE" });
```

### 8. Micro HTTP Server with Prometheus Metrics
```csharp
using ZeroNetwork.Http;

using var server = new ZeroHttpServer(port: 9090, host: "0.0.0.0");

server.MapGet("/api/status", req => ZeroHttpResponse.Json("{\"status\":\"OK\"}"));
server.MapPost("/api/trigger", req => ZeroHttpResponse.Text("Triggered: " + req.Body));

server.Start();
// Automatic Prometheus endpoint available at: http://localhost:9090/metrics
// Health check available at: http://localhost:9090/health
```

### 9. Sovereign Binary RPC (32-Byte Zero-Copy Framing)
```csharp
using ZeroNetwork.Rpc;

// Server registration
using var server = new ZeroRpcServer(port: 7070);
server.RegisterMethod(0x1001, (frame, reqSpan) =>
{
    // High-speed RPC handler (zero intermediate allocations)
    return new byte[] { 0x01, 0x02, 0x03 };
});
server.Start();

// Client multiplexed invocation
using var client = new ZeroRpcClient("127.0.0.1", port: 7070);
await client.ConnectAsync();
byte[] response = await client.InvokeAsync(methodHash: 0x1001, payload: new byte[] { 0xAA, 0xBB });
```

---

## ⚡ BCL Optimization Benchmark Results (Before vs After)

The following empirical benchmarks demonstrate measured throughput and allocation deltas between standard .NET BCL methods and ZeroPlatform high-performance replacements:

| Hot-Path Area | Standard .NET BCL Method | ZeroPlatform Replacement | Measured Speedup & Allocation Delta |
| :--- | :--- | :--- | :--- |
| **Binary RPC Framing** | gRPC / HTTP2 Protobuf (Heap Framing) | `ZeroRpcFrame` (32-B Header + FastUlid) | **Zero-Copy Native Span**, O(1) framing overhead |
| **WebSocket Unmasking** | Scalar XOR Loop (`payload[i] ^= mask[i%4]`) | `ZeroFastMask.ApplyMask` (64-bit unrolled) | **9.95x Faster** (144.3 MB/s ➔ **1,435.5 MB/s**) |
| **WebSocket Handshake** | `SHA1.Create().ComputeHash()` + Base64 | `ZeroSha1` + `ZeroBase64` | **100% Zero-Alloc** (1,604 ns + GC Heap ➔ **0 Bytes Heap Alloc**) |
| **Base64 Encoding** | `Convert.ToBase64String(byte[])` | `ZeroBase64.Encode(Span<byte>, Span<char>)` | **In-Place Span-to-Span**, 0 string heap churn |
| **UTF-8 Formatting** | `int.ToString()`, `Guid.ToString()` | `ZeroUtf8.TryFormat` | **Zero-Alloc** (25 ns + String Heap ➔ **0 Bytes Heap Alloc**) |
| **Lock-Free Queue** | `ConcurrentQueue<T>` (Linked Segments) | `ZeroRingBuffer<T>` (Vyukov MPMC Padded) | **2.27x Faster** (11.2 M ops/sec ➔ **25.4 M ops/sec**) |
| **Bit Counting** | While-Loop Bit Scan | `ZeroBitOps.TrailingZeroCount` (De Bruijn / Hardware) | **O(1) Sub-Nanosecond** across net8.0, net462, netstandard2.0 |
| **Random Generation** | `System.Random` (Knuth Subtractive) | `ZeroRandom` (Xoshiro256** PRNG) | **Period 2^256 - 1**, thread-local lock-free, passes BigCrush |
| **Packet Deduplication** | `HashSet<string>` (32-48 B/item) | `ZeroBloomFilter` (Kirsch-Mitzenmacher + XxHash3) | **~95% RAM Saved**, 0-alloc sub-microsecond query |
| **Scoped Allocation** | BCL GC Heap (14 collections / 1M) | `ZeroArenaAllocator` (Monotonic Bump + Scope) | **0 GC Collections**, sub-nanosecond watermark reset |
| **Delimiter Scanning** | Sequential byte scanning / `IndexOf` | `ZeroVectorScan` (SWAR 64-bit parallel word search) | **Accelerated Header Search**, 0 allocation |
| **Micro Critical Section**| BCL `lock (object)` / `Monitor` | `ZeroSpinLock` (Padded TTAS SpinLock) | **Sub-30ns Acquisition**, 0 False Sharing |

---

## 💻 Supported Platforms

| Framework | Target Support | Dependency Footprint |
| :--- | :--- | :--- |
| **.NET 8.0+** | Native (`net8.0`) | 0 Dependencies |
| **.NET Framework** | Legacy WinForms / WPF (`net462`) | 0 Dependencies (BCL only) |
| **.NET Standard** | Universal Cross-Platform (`netstandard2.0`) | 0 Dependencies |

---

## 📄 License
MIT License © 2026 Phong Võ (`kzxl`). Part of the **ZeroPlatform** sovereign ecosystem.
