# ZeroNeural

[![ZeroPlatform Tier](https://img.shields.io/badge/ZeroPlatform-Tier%203%20(Perception%20%26%20AI)-7c3aed.svg)](https://github.com/kzxl/ZeroPlatform)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET Multi-Targeting](https://img.shields.io/badge/.NET-8.0%20%7C%204.6.2%20%7C%20Standard%202.0-purple.svg)](https://dotnet.microsoft.com/)
[![Autograd Engine](https://img.shields.io/badge/Autograd-Reverse--Mode%20DAG-brightgreen.svg)]()
[![Zero External Dependencies](https://img.shields.io/badge/Dependencies-0%20(Pure%20C%23)-brightgreen.svg)]()
[![NuGet Version](https://img.shields.io/badge/NuGet-1.0.0-blue.svg)](https://www.nuget.org/packages/ZeroNeural.Core)

**ZeroNeural** is a pure C# deep learning framework and automatic differentiation (Autograd) library for .NET with **zero external dependencies**. Providing a clean, PyTorch-like programming interface, it enables dynamic computational graph construction, reverse-mode automatic differentiation, neural network layer composition, and gradient optimization directly inside standard .NET applications without Python or native Torch binaries.

---

## 🌟 Key Capabilities

- **Reverse-Mode Autograd Engine**: Dynamic tape-based computational DAG recording operations and backpropagating analytical vector-Jacobian products (`Backward()`).
- **PyTorch-Style Neural Layers (`ZeroNeural.Core.nn`)**:
  - `Linear` (Dense fully-connected layer with Xavier/Kaiming initialization)
  - `Sequential` (Composed container for multi-stage pipelines)
  - `Conv2D` (Spatial convolution with backpropagation)
  - `Dropout` (Inverted dropout for regularized training)
  - `BatchNorm` (Batch normalization with running mean/variance)
  - `Activations`: ReLU, Sigmoid, Tanh, Softmax, GELU
- **Standard Loss Functions**:
  - `MseLoss` (Mean Squared Error for regression)
  - `CrossEntropyLoss` (Multi-class classification with numerical log-sum-exp stability)
  - `BceWithLogitsLoss` (Binary classification with integrated sigmoid)
- **First-Class Optimizers (`ZeroNeural.Core.Optim`)**:
  - `Adam` and `AdamW` (Adaptive Moment Estimation with decoupled weight decay)
  - `SGD` (Stochastic Gradient Descent with Nesterov momentum)
  - `RMSProp`
- **Zero External Dependencies**: Standard .NET runtime only.

---

## 📦 Installation

Install via the .NET CLI:
```bash
dotnet add package ZeroNeural.Core
```

---

## 🚀 Quick Start

### 1. Reverse-Mode Autograd Gradient Computation
```csharp
using ZeroNeural.Core.Autograd;
using ZeroTensor.Core;

// Create leaf variable with gradient tracking
var x = new Variable(Tensor.Create(new[] { 1 }, new float[] { 3.0f }), requiresGrad: true);

// Compute y = x^2 + 2x + 1
var y = x * x + x * 2.0f + 1.0f;

// Backpropagate gradients: dy/dx = 2x + 2 = 8
y.Backward();

Console.WriteLine($"dy/dx at x=3: {x.Grad[0]} (Expected: 8.0)");
```

### 2. Training a Multi-Layer Perceptron (MLP)
```csharp
using ZeroNeural.Core.nn;
using ZeroNeural.Core.Optim;
using ZeroNeural.Core.Loss;
using ZeroTensor.Core;

// Define model
var model = new Sequential(
    new Linear(inFeatures: 4, outFeatures: 16),
    new ReLU(),
    new Linear(inFeatures: 16, outFeatures: 2)
);

var optimizer = new AdamW(model.Parameters(), lr: 0.01f);
var criterion = new CrossEntropyLoss();

// Training step
var xTrain = Tensor.RandomUniform(32, 4);
var yTrain = Tensor.Create(new[] { 32 }, new int[] { /* target classes */ });

optimizer.ZeroGrad();
var pred = model.Forward(xTrain);
var loss = criterion.Compute(pred, yTrain);
loss.Backward();
optimizer.Step();

Console.WriteLine($"Epoch Loss: {loss.Value:F4}");
```

---

## 📊 Benchmark & Performance

Tested on Intel Core i7-13700K (Release x64):

| Task | Configuration | Time / Epoch | Memory Overhead |
| :--- | :--- | :--- | :--- |
| **Autograd Backward Pass** | $1000$ operations DAG | $0.18 \text{ ms}$ | Linear tape memory |
| **MLP Training ($32 \times 64 \times 10$)** | Batch 128, Forward + Backward + Adam | $1.42 \text{ ms}$ | Reusable gradient buffers |
| **Conv2D Forward + Backward** | $3 \times 32 \times 32 \to 16 \times 30 \times 30$ | $8.60 \text{ ms}$ | Cache-tiled SIMD |

---

## 📄 License

MIT License © 2026 Phong Võ. Part of the **ZeroPlatform** project.
