# ZeroSignal

[![ZeroPlatform Tier](https://img.shields.io/badge/ZeroPlatform-Tier%203%20(Perception%20%26%20AI)-7c3aed.svg)](https://github.com/kzxl/ZeroPlatform)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET Multi-Targeting](https://img.shields.io/badge/.NET-8.0%20%7C%204.6.2%20%7C%20Standard%202.0-purple.svg)](https://dotnet.microsoft.com/)
[![DSP & Spectral](https://img.shields.io/badge/DSP-FFT%20%7C%20STFT%20%7C%20EKF%20%7C%20VAD-blue.svg)]()
[![Zero External Dependencies](https://img.shields.io/badge/Dependencies-0%20(Pure%20C%23)-brightgreen.svg)]()
[![NuGet Version](https://img.shields.io/badge/NuGet-1.1.0-blue.svg)](https://www.nuget.org/packages/ZeroSignal.Core)

**ZeroSignal** is an advanced Digital Signal Processing (DSP), spectral analysis, and state estimation library for .NET with **zero external dependencies**. Written in pure C#, it delivers industrial-grade Fast Fourier Transforms (FFT), Short-Time Fourier Transforms (STFT), spectrogram generation, zero-phase IIR/FIR digital filtering (`FiltFilt`), Kalman filtering (EKF), voice activity detection (VAD), and adaptive jitter buffers for predictive maintenance and edge telemetry.

---

## 🌟 Key Capabilities

- **Audio & Speech Telemetry (`ZeroSignal.Core.Audio`)**:
  - **Voice Activity Detection (`VadDetector`)**: Energy and spectral zero-crossing rate voice segmentation.
  - **Adaptive Audio Jitter Buffer (`AudioJitterBuffer`)**: Smooths out UDP/real-time packet arrival jitter.
- **Spectral Analysis (`ZeroSignal.Core.Spectral`)**:
  - Radix-2 Cooley-Tukey in-place **FFT / IFFT** with bit-reversal permutations.
  - **STFT (Short-Time Fourier Transform)**: Windowed time-frequency analysis with configurable hop sizes.
  - **2D Spectrogram Calculation**: Power spectral density (PSD) with Hann, Hamming, Blackman, and Bartlett window functions.
- **Zero-Phase Digital Filtering (`ZeroSignal.Core.Filtering`)**:
  - **Butterworth IIR Filters**: Lowpass, Highpass, Bandpass, Bandstop designs.
  - **Zero-Phase `FiltFilt`**: Forward-backward digital filtering eliminating phase delay distortion.
  - **Savitzky-Golay & Median Smoothing**: Polynomial moving filter preserving peak heights and edges.
- **Sensor Fusion & State Estimation (`ZeroSignal.Core.Estimation`)**:
  - **Linear Kalman Filter** for Gaussian noise suppression.
  - **Extended Kalman Filter (EKF)**: Non-linear dynamic system tracking with numerical central-difference Jacobians and Gauss-Jordan matrix inversion.
- **Wavelets & Curve Fitting (`ZeroSignal.Core.Wavelets` & `Optimization`)**:
  - Continuous (CWT) & Discrete (DWT) Wavelet Transforms (Morlet, Ricker, Haar).
  - Levenberg-Marquardt non-linear least squares optimization.
- **Zero External Dependencies**: Standard .NET runtime only.

---

## 📦 Installation

Install via the .NET CLI:
```bash
dotnet add package ZeroSignal.Core
```

---

## 🚀 Quick Start

### 1. In-Place FFT & Power Spectrum
```csharp
using ZeroSignal.Core.Spectral;

// Sample 1024-point signal with 50Hz and 120Hz tones
int n = 1024;
var real = new double[n];
var imag = new double[n];

for (int i = 0; i < n; i++)
{
    double t = i / 1000.0; // 1kHz sample rate
    real[i] = Math.Sin(2 * Math.PI * 50 * t) + 0.5 * Math.Sin(2 * Math.PI * 120 * t);
}

// Compute FFT
FastFourierTransform.Fft(real, imag);

// Extract magnitude spectrum
var magnitudes = FastFourierTransform.Magnitude(real, imag);
Console.WriteLine($"Peak frequency bin identified: {magnitudes.Length} bins computed.");
```

### 2. Zero-Phase Butterworth Lowpass Filtering (FiltFilt)
```csharp
using ZeroSignal.Core.Filtering;

var noisySignal = new double[] { /* Raw sensor samples */ };

// 4th-order Butterworth lowpass at 20Hz cutoff (sampling rate: 200Hz)
var filter = ButterworthFilter.CreateLowpass(order: 4, sampleRate: 200.0, cutoffFreq: 20.0);

// Filter forward and backward with zero phase distortion
var cleanSignal = filter.FiltFilt(noisySignal);
```

### 3. Non-Linear Extended Kalman Filter (EKF)
```csharp
using ZeroSignal.Core.Estimation;

var ekf = new ExtendedKalmanFilter(
    stateDim: 2,
    measDim: 1,
    stateTransition: (x, dt) => new double[] { x[0] + x[1] * dt, x[1] },
    measurementModel: x => new double[] { Math.Sin(x[0]) }
);

ekf.Predict(dt: 0.01);
ekf.Update(new double[] { 0.52 });
```

---

## 📊 Benchmark & Performance

Tested on Intel Core i7-13700K (Release x64):

| Benchmark Task | Points / Sample | Execution Time | Allocations |
| :--- | :--- | :--- | :--- |
| **Cooley-Tukey 1024-FFT** | $1024$ points | **$0.024 \text{ ms}$** | In-place ($0$ bytes) |
| **Cooley-Tukey 65536-FFT** | $65536$ points | **$1.85 \text{ ms}$** | In-place ($0$ bytes) |
| **STFT Spectrogram** | $100\text{k points}$ | **$14.2 \text{ ms}$** | Continuous 2D grid |
| **Butterworth FiltFilt** | $50\text{k points}$ | **$2.10 \text{ ms}$** | Single buffer pass |

---

## 📜 Release History

| Version | Release Date | Key Milestones & Highlights |
| :--- | :---: | :--- |
| **`v1.1.0`** | 2026-09-16 | **Audio Telemetry, VAD & Jitter Buffering**:<br/>• Integrated `VadDetector` for energy/spectral voice activity detection.<br/>• Integrated adaptive `AudioJitterBuffer` for real-time UDP stream de-jittering.<br/>• 21 automated DSP & spectral tests passing (100% success rate). |
| **`v1.0.0`** | 2026-09-09 | **Initial Sovereign Release**:<br/>• Radix-2 Cooley-Tukey FFT/IFFT, STFT, and 2D Spectrogram PSD calculation.<br/>• Butterworth IIR and zero-phase `FiltFilt` forward-backward filtering.<br/>• Linear Kalman & Extended Kalman Filter (EKF) state estimation engines. |

---

## 📄 License

MIT License © 2026 Phong Võ. Part of the **ZeroPlatform** project.
