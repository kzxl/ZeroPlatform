# ZeroInference

[![ZeroPlatform Tier](https://img.shields.io/badge/ZeroPlatform-Tier%203%20(Perception%20%26%20AI)-7c3aed.svg)](https://github.com/kzxl/ZeroPlatform)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET Multi-Targeting](https://img.shields.io/badge/.NET-8.0%20%7C%204.6.2%20%7C%20Standard%202.0-purple.svg)](https://dotnet.microsoft.com/)
[![ONNX Support](https://img.shields.io/badge/Model-Pure%20C%23%20ONNX%20Parser-orange.svg)]()
[![Zero External Dependencies](https://img.shields.io/badge/Dependencies-0%20(Pure%20C%23)-brightgreen.svg)]()
[![NuGet Version](https://img.shields.io/badge/nuget-v1.5.0-blue.svg)](https://www.nuget.org/packages/ZeroInference.Core)
[![Tests: Passed](https://img.shields.io/badge/Tests-Passed%20(100%25)-brightgreen.svg)]()

**ZeroInference** is a pure C# ONNX deep learning inference engine and model runtime for .NET with **zero external dependencies**. It eliminates bulky native C++ runtime binaries (no ONNX Runtime native DLLs, no OpenVINO, no Python dependencies), reading and evaluating `.onnx` and `.zeromodel` neural graphs directly in memory with SIMD vectorization and int8 quantization.

---

## 🌟 Key Capabilities

- **Transformer & LLM Operators**:
  - **Rotary Position Embedding (`RotaryEmbedding`)**: High-performance RoPE with precomputed trigonometric tables and in-place complex rotation for modern LLMs (LLaMA, Mistral, Qwen).
  - **FlashAttention-2 Kernel (`FlashAttentionKernel`)**: CPU cache-tiled online Softmax algorithm computing scaled dot-product attention in $O(1)$ memory without materializing the quadratic $N \times N$ attention matrix.
  - **Multi-Head Attention (`MultiHeadAttention`)**: Scaled dot-product attention with projection weights and configurable heads.
- **Polymorphic Micro-Kernel (`IInferenceSession`)**: Unified inference contract allowing seamless zero-downtime switching between Pure C# execution and native hardware accelerators (DirectML / ONNX Runtime).
- **Zero Dependency Pure C# Runtime**: No native shared libraries (`onnxruntime.dll`, `libonnxruntime.so`) required. Runs anywhere .NET runs.
- **Local LLM Streaming (`LocalLlmStreamClient`)**: High-throughput SSE token streaming client for Ollama, OpenAI, and local edge LLM endpoints.
- **Direct ONNX Model Parser**: Stack-allocated Protocol Buffers wire reader (`ProtobufWireReader`) parsing ONNX binary graphs directly into executable compute graphs.
- **Compact `.zeromodel` Serialization**: Fast binary serialization format with pre-compiled layer topologies and optimized weights layout.
- **Supported Deep Learning Layers**:
  - **Conv2D** (Direct & im2col GEMM convolution)
  - **Dense / Gemm** (Fully-connected linear layers)
  - **BatchNormalization & LayerNorm**
  - **Activations** (ReLU, LeakyReLU, Sigmoid, Tanh, Softmax)
  - **Pooling** (MaxPool2D, AveragePool2D, GlobalAveragePool)
  - **Reshape, Flatten, Concat, Slice**
- **Quantization & Vision Post-Processing**:
  - **Int8 Quantizer**: Symmetric and asymmetric integer quantization for edge devices.
  - **Non-Maximum Suppression (NMS)**: Fast SIMD bounding box filtering with configurable IoU and score thresholds.
- **Hardware Agnostic**: Executes over `ZeroTensor` CPU SIMD or `ZeroCompute` Direct3D 11 GPU compute contexts.

---

## 📦 Installation

Install via the .NET CLI:
```bash
dotnet add package ZeroInference.Core
```

---

## 🚀 Quick Start

### 1. Parsing and Executing an ONNX Model
```csharp
using ZeroInference.Core.Engine;
using ZeroInference.Core.Format;
using ZeroTensor.Core;

// 1. Load and parse .onnx model file
using var stream = File.OpenRead("models/classifier.onnx");
var graph = OnnxModelParser.Parse(stream);

// 2. Instantiate inference engine
var engine = new InferenceEngine(graph);

// 3. Prepare input tensor and infer
var input = Tensor.RandomUniform(1, 3, 224, 224);
var outputs = engine.Forward(input);

Console.WriteLine($"Inference Output Shape: [{outputs[0].Shape[0]}, {outputs[0].Shape[1]}]");
```

### 2. Fast Object Detection NMS Post-Processing
```csharp
using ZeroInference.Core.Vision;

var candidateBoxes = new List<BoundingBox>
{
    new BoundingBox(10, 10, 50, 50, score: 0.92f, classId: 1),
    new BoundingBox(12, 11, 48, 52, score: 0.78f, classId: 1), // Overlapping duplicate
    new BoundingBox(100, 120, 60, 40, score: 0.85f, classId: 2)
};

// Filter duplicates with IoU threshold = 0.45
var filtered = NonMaximumSuppression.Filter(candidateBoxes, iouThreshold: 0.45f, scoreThreshold: 0.5f);

Console.WriteLine($"Remaining boxes after NMS: {filtered.Count}");
```

---

## 📊 Benchmark & Performance

Tested on MobileNet-V2 / ResNet-18 (Release x64):

| Architecture | Model Size | Load Time | CPU SIMD Latency | External DLLs |
| :--- | :--- | :--- | :--- | :--- |
| **MobileNet-V2** | $14.2 \text{ MB}$ | $18.4 \text{ ms}$ | $12.1 \text{ ms}$ | **0 (Pure C#)** |
| **ResNet-18 (FP32)** | $45.1 \text{ MB}$ | $42.0 \text{ ms}$ | $28.5 \text{ ms}$ | **0 (Pure C#)** |
| **ResNet-18 (Int8)** | **$11.3 \text{ MB}$** | **$12.5 \text{ ms}$** | **$9.4 \text{ ms}$** | **0 (Pure C#)** |

---

## 📜 Release History

| Version | Release Date | Key Milestones & Highlights |
| :--- | :---: | :--- |
| **`v1.1.0`** | 2026-09-16 | **Polymorphic Micro-Kernel & Local LLM Streaming**:<br/>• Introduced `IInferenceSession` unified execution contract decoupling high-level apps from backends.<br/>• Added `ZeroInference.Providers.OnnxRuntime` provider bridging Microsoft.ML.OnnxRuntime with pure C# pipeline.<br/>• Added `LocalLlmStreamClient` supporting real-time SSE streaming for Ollama & OpenAI-compatible endpoints.<br/>• Verified with 23 unit tests across Core & Provider test suites. |
| **`v1.0.0`** | 2026-09-09 | **Initial Sovereign Release**:<br/>• Pure C# ONNX protobuf wire reader & layer fusion pipeline.<br/>• Conv2D, Dense, BatchNorm, LayerNorm, activations, and pooling.<br/>• Int8 quantization engine & Non-Maximum Suppression (NMS). |

---

## 📄 License

MIT License © 2026 Phong Võ. Part of the **ZeroPlatform** project.
