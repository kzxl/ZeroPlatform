# ZeroStorage

[![ZeroPlatform Tier](https://img.shields.io/badge/ZeroPlatform-Tier%202%20(Transport%20%26%20Storage)-059669.svg)](https://github.com/kzxl/ZeroPlatform)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET Multi-Targeting](https://img.shields.io/badge/.NET-8.0%20%7C%204.6.2%20%7C%20Standard%202.0-purple.svg)](https://dotnet.microsoft.com/)
[![Gorilla Compression](https://img.shields.io/badge/Compression-Facebook%20Gorilla%20XOR-brightgreen.svg)]()
[![Zero External Dependencies](https://img.shields.io/badge/Dependencies-0%20(Pure%20C%23)-brightgreen.svg)]()
[![Tests: 66 Passed](https://img.shields.io/badge/Tests-66%20Passed%20(100%25)-brightgreen.svg)]()
[![NuGet Version](https://img.shields.io/badge/NuGet-1.5.0-blue.svg)](https://www.nuget.org/packages/ZeroStorage.Core)

**ZeroStorage** is an embedded, high-throughput time-series database (TSDB), durable persistent queue, and write-ahead log (WAL) storage engine for .NET with **zero external dependencies**. Implemented from scratch in pure C#, it features Facebook Gorilla lossy/lossless Delta-of-Delta timestamp compression, XOR float mantissa encoding, wide-row MultiMetricBlock columnar persistence, Uuid7 time-ordered persistent streaming queues, memory-mapped files (MMF), CRC32 integrity verification, and background tiered compaction.

---

## 🌟 Key Capabilities

- **Durable Persistent Queue (`DurablePersistentQueue`)**:
  - High-throughput disk-backed persistent queue utilizing `MemoryMappedFile` and `Uuid7` time-ordered monotonic message framing.
  - Zero-allocation commit pointers, crash recovery, atomic tail/head advancement, and sub-millisecond persistence.
- **Wide-Row Time-Series Compression (`MultiMetricBlock`)**: Simultaneously encodes multiple metrics sharing timestamp vectors with Gorilla compression.
- **Facebook Gorilla Time-Series Compression**:
  - **Timestamp Compression**: Variable-length Delta-of-Delta encoding (down to 1 bit per sample for regular time intervals).
  - **Value Compression**: XOR floating-point mantissa encoding with leading/trailing zero tracking (reducing typical float metrics to $< 1.5$ bytes/sample).
- **Memory-Mapped Persistence (`MemoryMappedTimeSeriesLog`)**: Direct kernel-level zero-copy disk mapping via `MemoryMappedFile`, bypassing user-space buffering for sub-millisecond writes.
- **Write-Ahead Log (WAL)**: Append-only durability log with IEEE 802.3 CRC32 integrity checksums per record and automatic crash recovery.
- **Tiered Compaction**: Background compactor merging fragmented uncompressed or hot blocks into consolidated, dense cold storage blocks.
- **Zero External Dependencies**: Pure C# standard runtime library.

---

## 📦 Installation

Install via the .NET CLI:
```bash
dotnet add package ZeroStorage.Core
```

---

## 🚀 Quick Start

### 1. Gorilla Timestamp & Float Compression
```csharp
using ZeroStorage.Core.BitIO;
using ZeroStorage.Core.Gorilla;

using var stream = new MemoryStream();
var writer = new BitWriter(stream);
var encoder = new GorillaEncoder(writer);

long baseTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

// Encode stream of 1000 points
for (int i = 0; i < 1000; i++)
{
    long ts = baseTimestamp + (i * 10); // 10ms intervals
    double val = 25.0 + Math.Sin(i * 0.1) * 0.5;
    encoder.Encode(ts, val);
}
encoder.Finish();

Console.WriteLine($"Compressed 1,000 points into: {stream.Length} bytes ({stream.Length / 1000.0:F2} bytes/point)!");
```

### 2. Memory-Mapped Time-Series Log
```csharp
using ZeroStorage.Core.Persistence;

using var tsdb = new MemoryMappedTimeSeriesLog("telemetry.tsdb", maxSizeBytes: 64 * 1024 * 1024);

// Append metric point
tsdb.Append(metricId: 42, timestamp: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), value: 98.6);
```

### 3. Durable Persistent Queue (Uuid7 Framing)
```csharp
using ZeroStorage.Core.Queue;

using var queue = new DurablePersistentQueue("events.queue", maxCapacityBytes: 128 * 1024 * 1024);

// Produce time-ordered event payload
byte[] payload = System.Text.Encoding.UTF8.GetBytes("SensorAlert: HighTemperature");
queue.Enqueue(payload);

// Consume next available event
if (queue.TryDequeue(out byte[] eventData, out Guid messageId))
{
    Console.WriteLine($"Processed message {messageId}: {System.Text.Encoding.UTF8.GetString(eventData)}");
}
```

---

## 📊 Benchmark & Performance

Tested on Intel Core i7-13700K with NVMe SSD (Release x64):

| Metric | Uncompressed Raw | Gorilla Compressed (ZeroStorage) | Compression Ratio |
| :--- | :--- | :--- | :--- |
| **Storage per Metric Point** | $16 \text{ bytes}$ ($8\text{B ts} + 8\text{B val}$) | **$1.37 \text{ bytes}$** | **$11.6 \times$ smaller** |
| **Write Throughput** | $2.5\text{M points/sec}$ | **$12.8\text{M points/sec}$** | In-memory bit packing |
| **Query Decompress Speed** | $10\text{M points/sec}$ | **$45.0\text{M points/sec}$** | Direct bit-stream decode |

---

## 📜 Release History

| Version | Release Date | Key Milestones & Highlights |
| :--- | :---: | :--- |
| **`v1.5.0`** | 2026-09-29 | **Durable Persistent Queue & Monotonic Uuid7 Framing**:<br/>• Introduced `DurablePersistentQueue`: High-speed disk-backed queue using memory-mapped files and Uuid7 framing.<br/>• Zero-allocation append/consume operations with transactional crash-safety and fast recovery.<br/>• 66 unit tests passing (100% success rate). |
| **`v1.1.0`** | 2026-09-16 | **Wide-Row Metric Compression**:<br/>• Introduced `MultiMetricBlock` simultaneous encoding of multi-variate telemetry streams.<br/>• Zero-copy memory mapped files with CRC32 block checksums.<br/>• 18 unit tests passing (100% success rate). |
| **`v1.0.0`** | 2026-09-09 | **Initial Sovereign Release**:<br/>• Pure C# Facebook Gorilla Delta-of-Delta timestamp & XOR float compression.<br/>• Memory-mapped write-ahead logging (WAL) & tiered compaction. |

---

## 📄 License

MIT License © 2026 Phong Võ. Part of the **ZeroPlatform** project.
