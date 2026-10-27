# ZeroAudioVisual: Acoustic Vibration Monitoring & Audio-Visual Streaming for .NET

[![ZeroPlatform Tier](https://img.shields.io/badge/ZeroPlatform-Tier%203%20(Perception%20%26%20AI)-7c3aed.svg)](https://github.com/kzxl/ZeroPlatform)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET Multi-Targeting](https://img.shields.io/badge/.NET-8.0%20%7C%204.6.2%20%7C%20Standard%202.0-purple.svg)](https://dotnet.microsoft.com/)

**ZeroAudioVisual** is a real-time acoustic signal processing, predictive maintenance (PdM), and video stream processing library for .NET.

## Key Features

- **Predictive Maintenance (PdM)**: Rolling bearing defect diagnostic (`BearingDefectDetector`) calculating statistical indicators (RMS, Kurtosis, Crest Factor) and characteristic bearing fault frequencies (BPFI, BPFO, BSF, FTF).
- **Time-Frequency Spectral Analysis**: Short-Time Fourier Transform engine (`SpectrogramEngine`) generating high-speed 2D spectrograms with Hanning windowing.
- **Lock-Free Acoustic Buffers**: Circular ring buffer (`AcousticWaveBuffer`) optimized for high-sampling-rate industrial DAQ cards.
- **RTSP / RTP Video Frame Transport**: Pure C# network frame buffer (`VideoFrameBuffer`) and RTP protocol parser (`RtspProtocol`) for industrial H.264 camera feeds.

## Multi-Targeting

- `.NET 8.0+`
- `.NET Framework 4.6.2+`
- `.NET Standard 2.0`

## License

MIT License. Copyright © 2026 Phong Võ (`kzxl`).
