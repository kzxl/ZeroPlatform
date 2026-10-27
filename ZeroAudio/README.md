# 🎵 ZeroAudio: Sovereign Pure C# Audio Engineering & DSP Engine

[![ZeroPlatform Tier](https://img.shields.io/badge/ZeroPlatform-Tier%203%20(Perception%20%26%20AI)-7c3aed.svg)](https://github.com/kzxl/ZeroPlatform)
[![NuGet Version](https://img.shields.io/badge/nuget-v1.0.0-blue.svg)](https://www.nuget.org/packages/ZeroAudio.Core/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Zero External Dependencies](https://img.shields.io/badge/Dependencies-0%20(Pure%20C%23)-brightgreen.svg)]()
[![Tests: 27 Passed](https://img.shields.io/badge/Tests-27%20Passed%20(100%25)-brightgreen.svg)]()
[![Multi-Targeting](https://img.shields.io/badge/.NET-8.0%20%7C%204.6.2%20%7C%20Standard%202.0-orange.svg)]()

> **Architectural Standard**: 100% Pure C# BCL, Zero External Unmanaged Dependencies (no NAudio, FFmpeg, or BASS DLLs required), Pragmatic Minimal Memory Allocation, Multi-Targeting across `.NET 8.0`, `.NET Framework 4.6.2`, and `.NET Standard 2.0`.

`ZeroAudio` is a sovereign, high-throughput audio engineering, digital signal processing (DSP), and streaming engine for .NET. Operating within **Tier 3 (Perception & AI)** of the **ZeroPlatform** / **Zero Universe** ecosystem, it provides pure C# primitives for RIFF/WAVE container encoding/decoding, low-latency SPSC circular ring buffers, multi-channel matrix routing, Catmull-Rom cubic resampling, peak limiting, dynamic compression, biquad equalizers, and waveform synthesis.

---

## 🏛️ Design Philosophy: Minimal Allocation & Pragmatic Efficiency

Unlike dogmatic zero-allocation frameworks that compromise readability with overly complex pointer gymnastics, `ZeroAudio` adopts the modern **ZeroPlatform Pragmatic Standard**:
- **Hot-path Zero Allocation**: Critical real-time audio sample processing loops (`Span<float>`, `ReadOnlySpan<byte>`) execute without allocating on the managed heap.
- **Pooled Buffers**: Audio memory blocks rent and return memory via `ArrayPool<T>.Shared` to eliminate garbage collector (GC) pauses and audio stuttering (*pops and clicks*).
- **Pure C# Sovereignty**: Eliminates brittle native C++ wrapper dependencies, enabling immediate cross-platform deployment across Windows, Linux, macOS, and edge IoT devices.

---

## 📦 Key Subsystem Capabilities

| Component | Namespace | Description |
| :--- | :--- | :--- |
| **`WavReader` & `WavWriter`** | `ZeroAudio.Formats` | Streaming RIFF/WAVE encoder and decoder supporting 8/16/24/32-bit linear PCM and 32-bit IEEE Float with automatic header size finalization. |
| **`AudioBuffer`** | `ZeroAudio.Buffers` | Pooled, multi-channel float audio frame container with instant RMS, peak dBFS, gain scaling, and channel deinterleaving. |
| **`AudioRingBuffer`** | `ZeroAudio.Buffers` | Lock-free Single-Producer Single-Consumer (SPSC) circular FIFO buffer with power-of-two bitwise wrapping for real-time soundcard decoupling. |
| **`SampleRateConverter`** | `ZeroAudio.Processing` | High-speed linear interpolation and 4-point 3rd-order Catmull-Rom cubic Hermite resampling between arbitrary sample rates. |
| **`ChannelMatrix`** | `ZeroAudio.Processing` | Mono ↔ Stereo conversion, ITU-R BS.775 5.1 surround-to-stereo downmixing, and channel interleaving / deinterleaving. |
| **`GainController`** | `ZeroAudio.Processing` | Volume attenuation, constant-power / linear panning, linear fade-in/fade-out ramps, and cubic polynomial soft saturation. |
| **`AudioLimiter`** | `ZeroAudio.Dynamics` | Instantaneous attack peak limiter preventing digital clipping distortion above specified dBFS ceilings. |
| **`AudioCompressor`** | `ZeroAudio.Dynamics` | Configurable dynamic range compressor with threshold, ratio, attack, release, and makeup gain. |
| **`NoiseGate`** | `ZeroAudio.Dynamics` | Fast-attack, hold, and smooth-release noise gate attenuating ambient background noise during speech pauses. |
| **`AgcEngine`** | `ZeroAudio.Dynamics` | Automatic Gain Control dynamically leveling speech volume toward target RMS dBFS. |
| **`BiquadFilter`** | `ZeroAudio.Equalization` | Direct Form II Transposed IIR biquad filters: LowPass, HighPass, BandPass, Notch, Peaking EQ, LowShelf, HighShelf. |
| **`GraphicEqualizer`** | `ZeroAudio.Equalization` | 10-band ISO standard equalizer (31Hz to 16kHz) with per-channel cascading biquad filters. |
| **`SignalGenerator`** | `ZeroAudio.Synthesis` | Pure waveform generators (Sine, Square, Triangle, Sawtooth), White/Pink noise, and telephone DTMF dual-tone generation. |
| **`AudioMixer`** | `ZeroAudio.Synthesis` | Multi-track weighted audio summing engine with master bus soft saturation. |

---

## 🚀 Quick Start Examples

### 1. Reading and Writing WAV Files

```csharp
using ZeroAudio.Formats;

// Read all samples from a WAV file
float[] samples = WavReader.ReadAllSamples("input.wav", out AudioFormat format);
Console.WriteLine($"Loaded: {format.SampleRate}Hz, {format.Channels} channels, {format.BitsPerSample}-bit");

// Process samples (e.g. increase volume by +3 dB)
GainController.ApplyGainDb(samples, 3.0f);

// Write back to a standard 16-bit PCM WAV file
WavWriter.WriteAllSamples("output.wav", samples, AudioFormat.Pcm16Stereo44kHz);
```

### 2. High-Performance AudioBuffer Pooling

```csharp
using ZeroAudio.Buffers;
using ZeroAudio.Formats;

var format = AudioFormat.Float32Stereo48kHz;

// Rent pooled buffer without heap garbage
using var buffer = AudioBuffer.Rent(format, frameCount: 1024);

// Access raw float span for DSP
Span<float> samples = buffer.Samples;

// Compute audio telemetry
float peakDb = buffer.CalculatePeakDb();
float rms = buffer.CalculateRms();
```

### 3. Real-Time Dynamic Processing & Equalization

```csharp
using ZeroAudio.Dynamics;
using ZeroAudio.Equalization;

// Configure a 10-band Graphic Equalizer
var eq = new GraphicEqualizer(sampleRate: 48000, channels: 2);
eq.SetBandGain(0, +3.0f); // Bass boost (+3dB at 31Hz)
eq.SetBandGain(5, +2.0f); // Presence boost (+2dB at 1kHz)

// Configure Brickwall Limiter (-0.2 dBFS ceiling)
var limiter = new AudioLimiter(sampleRate: 48000, ceilingDb: -0.2f);

// In your audio rendering callback loop:
Span<float> frameBuffer = GetAudioFrame();
eq.Process(frameBuffer);
limiter.Process(frameBuffer);
```

### 4. Resampling & Channel Downmixing

```csharp
using ZeroAudio.Processing;

// Resample 44.1 kHz audio to 48.0 kHz with cubic Hermite interpolation
int outFrames = SampleRateConverter.CalculateOutputFrames(inFrames, 44100, 48000);
Span<float> resampled = stackalloc float[outFrames * 2];
SampleRateConverter.ResampleCubic(inSamples, resampled, channels: 2, 44100, 48000);

// Downmix 5.1 surround to stereo
Span<float> stereoOut = stackalloc float[inFrames * 2];
ChannelMatrix.DownmixSurroundToStereo(surroundSamples, stereoOut);
```

---

## 🧪 Verification & Unit Testing

`ZeroAudio` is backed by a comprehensive unit test suite covering container round-trips, DSP filter responses, dynamics, and synthesis:

```bash
dotnet test -c Release
```

```text
Passed!  - Failed: 0, Passed: 27, Skipped: 0, Total: 27, Duration: 66 ms - ZeroAudio.Tests.dll (net8.0)
Passed!  - Failed: 0, Passed: 27, Skipped: 0, Total: 27, Duration: 116 ms - ZeroAudio.Tests.dll (net462)
```

---

## 📄 License

Licensed under the [MIT License](LICENSE). Copyright © 2026 Phong Võ (`kzxl`). Part of the **ZeroPlatform** sovereign ecosystem.
