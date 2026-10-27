# ZeroVideo: Enterprise Pure C# Video Streaming, Transport & Multimedia Engine

[![ZeroPlatform Tier](https://img.shields.io/badge/ZeroPlatform-Tier%203%20(Perception%20%26%20AI)-7c3aed.svg)](https://github.com/kzxl/ZeroPlatform)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET Multi-Targeting](https://img.shields.io/badge/.NET-8.0%20%7C%204.6.2%20%7C%20Standard%202.0-purple.svg)](https://dotnet.microsoft.com/)
[![NuGet Version](https://img.shields.io/badge/nuget-v1.2.1-blue.svg)](https://www.nuget.org/packages/ZeroVideo)
[![Tests: 59 Passed](https://img.shields.io/badge/Tests-59%20Passed%20(100%25)-brightgreen.svg)]()

**ZeroVideo** is a sovereign, high-performance, pure C# video streaming, camera network transport, and multimedia playback engine for .NET. Completely free from heavyweight native C++ bindings (no FFmpeg or VLC unmanaged binaries required), it provides low-latency RTSP/RTP/H.264 stream ingestion, industrial Motion JPEG (MJPEG) streaming, lossless bitmap snapshot generation, strided multi-format video frame buffers, fast BT.601 color conversions, and clock-synchronized video playback.

Operating as a core member of **Tier 3 (Perception & Intelligence)** within the **[ZeroPlatform](https://github.com/kzxl/ZeroPlatform)** ecosystem.

---

## 🏛️ Ecosystem Architectural Alignment

- **Architectural Tier**: **Tier 3 (Perception & Intelligence)**
- **Permitted Upstream Dependencies**: Tier 0 (`ZeroPrimitives`, `ZeroConcurrency`, `ZeroSecurity`), Tier 1 (`ZeroCompute`, `ZeroTensor`), Tier 2 (`ZeroNetwork`)
- **Downstream Consumers**: Tier 4 (`ZeroGraphics`), Tier 5 (`ZeroPipeline`, `ZeroUI`)
- **Core Guarantees**: Pure C# execution, Zero Large Object Heap (LOH) GC allocations on hot streaming loops, zero unmanaged runtime binary dependencies.

---

## Key Features

- **Zero-LOH Frame Memory Pool (`VideoFramePool`, `PooledVideoFrameBuffer`)**:
  - High-throughput buffer pooling using `ArrayPool<byte>.Shared` to eliminate Large Object Heap (LOH) GC pauses during continuous high-FPS streaming.
  - Implements `IDisposable` with zero-allocation span accessors (`AsSpan`, `AsReadOnlySpan`, `GetRowSpan`).
- **Industrial Motion JPEG Multipart Client (`MjpegClient`)**:
  - Ingests standard `multipart/x-mixed-replace` HTTP video streams from industrial IP cameras (Basler, FLIR, Axis, Hikvision, ESP32-CAM, OctoPrint) with zero external libraries.
  - Non-blocking asynchronous reading loop (`ReadAsync`) with fault-tolerant JPEG SOI (`0xFF, 0xD8`) and EOI (`0xFF, 0xD9`) marker search.
  - Event-driven frame dispatching (`FrameReceived`).
- **Lossless Snapshot BMP Exporter (`SnapshotExporter`)**:
  - Pure C# Windows Bitmap (.bmp) generator without GDI+ or `System.Drawing` dependencies.
  - Full support for 8-bit Gray8 (with 256-color grayscale palette), 24-bit BGR/RGB, 32-bit BGRA/RGBA, and planar YUV420P/NV12.
- **Strided Video Frame Buffer (`VideoFrameBuffer`)**:
  - Low-latency pixel memory wrapping supporting Gray8, RGB24, BGR24, RGBA32, BGRA32, NV12, and YUV420P with nanosecond presentation timestamps (PTS).
- **RTSP 1.0 Client Transport (`RtspClient`)**:
  - Pure C# session negotiation (OPTIONS, DESCRIBE, SETUP, PLAY, PAUSE, TEARDOWN) with SDP video track parsing for IP security and industrial cameras.
- **RFC 3550 RTP Stream Demuxing (`RtpPacket`)**:
  - Fast binary packet header parsing and serialization with SSRC and sequence number tracking.
- **H.264 AVC NAL Unit Scanner (`H264NaluParser`)**:
  - Annex B start code detection (`0x000001` / `0x00000001`) and parameter set (SPS/PPS) extraction.
- **RFC 6184 FU-A Reassembly with Packet Loss Guard (`H264FuAReassembler`)**:
  - Seamless reassembly of fragmented NAL units across RTP packet boundaries using reusable assembly memory and sequence number continuity validation to discard corrupted frames.
- **H.264 SPS Bitstream Metadata Parser (`H264SpsParser`)**:
  - Pure C# Exp-Golomb bitstream decoder extracting video dimensions (width, height), profile, and level from raw Sequence Parameter Sets without external decoders.
- **Fixed-Point BT.601 Color Conversion (`ColorConverter`)**:
  - Integer-scaled conversion from YUV420P and NV12 to packed RGB24 and BGR24 with 2x horizontal chrominance calculation reuse and aggressive inlining.
- **Master Playback Clock & Backpressured Player (`VideoClock`, `VideoPlayer`)**:
  - Drift-free PTS synchronization, bounded queue backpressure control (`MaxQueueCapacity`, `FrameDropStrategy.DropOldest`), variable playback rate, seeking, and forensic frame stepping.

---

## Multi-Targeting

- `.NET 8.0+`
- `.NET Framework 4.6.2+`
- `.NET Standard 2.0`

---

## License

MIT License. Copyright © 2026 Phong Võ (`kzxl`).
