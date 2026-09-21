# Contributing to ZeroPlatform 🌐

Thank you for your interest in contributing to **ZeroPlatform**! ZeroPlatform is a sovereign, enterprise-grade software ecosystem for industrial automation, high-performance edge computing, computer vision, DSP, TSDB storage, and hardware-accelerated HMI/SCADA controls.

---

## 📜 Code of Conduct

All participants must abide by our [Code of Conduct](CODE_OF_CONDUCT.md). Please treat all community members with respect and professionalism.

---

## 🏛️ Ecosystem Structure & Multi-Repo Satellite Architecture

ZeroPlatform is organized as an umbrella orchestrator coordinating independent satellite repositories across 12+ domains:
- **`ZeroPrimitives`**: Pure C# primitive parsers and zero-alloc memory utilities.
- **`ZeroTensor`**: N-D strided tensor layout and linear algebra.
- **`ZeroCompute`**: Direct3D 11 Compute Shader dispatcher & AVX2 SIMD fallback.
- **`ZeroData`**: High-performance Columnar DataFrame & hash joins.
- **`ZeroStorage`**: Embedded TSDB, Gorilla Delta-of-Delta compression.
- **`ZeroInference`**: Pure C# ONNX binary inference runtime.
- **`ZeroNeural`**: Reverse-mode automatic differentiation & deep learning.
- **`ZeroSignal`**: FFT, STFT, Butterworth filters, Extended Kalman Filter.
- **`ZeroGeometry`**: 3D point cloud ICP, KdTree, Delaunay triangulation.
- **`ZeroComm`**: Industrial protocols (Modbus TCP/RTU, MC Protocol 3E, Omron FINS).
- **`ZeroGraphics`**: Direct3D 11 GPU rendering, computer vision, Mertens HDR.
- **`ZeroUI`**: 10M+ rows virtual grid, 60 FPS SCADA controls, dark theme.
- **`ZeroPipeline`**: DAG scheduler and inspection canvas.
- **`ZeroNetwork`**: Embedded HTTP server, packet parser, parallel scanner.
- **`ZeroDocuments`**: Pure C# OpenXML Excel (.xlsx) & CSV parser.

---

## 🛠️ Development & Build Setup

### Prerequisites
- Windows 10/11 x64
- .NET 8.0 SDK (or later)
- Visual Studio 2022 / JetBrains Rider / VS Code

### Cloning & Building
1. Clone the platform repository:
   ```bash
   git clone https://github.com/kzxl/ZeroPlatform.git
   cd ZeroPlatform
   ```
2. Clone or synchronize the satellite repositories:
   ```powershell
   .\clone-ecosystem.ps1
   ```
3. Build the entire platform solution:
   ```bash
   dotnet build ZeroPlatform.slnx
   ```
4. Run all ecosystem tests:
   ```bash
   dotnet test ZeroPlatform.slnx
   ```

---

## 📐 Engineering Principles

1. **Zero External Runtime Dependencies**: All subsystems are engineered in 100% pure C# or standard platform APIs.
2. **Zero-Allocation Hot Paths**: Use `Span<T>`, `ReadOnlySpan<T>`, and memory pools in tight computational loops.
3. **Multi-Targeting**: Maintain compatibility across `net8.0`, `net462`, and `netstandard2.0` where specified.
4. **Standard Technical English**: All code, documentation, XML comments, and commit messages must be in clean, technical English.

---

## 🔄 Commit & PR Guidelines

- Follow [Conventional Commits](https://www.conventionalcommits.org/): `feat:`, `fix:`, `perf:`, `refactor:`, `test:`, `docs:`, `chore:`.
- Ensure all tests pass before submitting a Pull Request.
- Complete the [Pull Request Template](.github/pull_request_template.md).

Thank you for building the future of sovereign industrial software with us! 🚀
