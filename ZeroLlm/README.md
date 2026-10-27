# 🧠 ZeroLlm: Sovereign Pure C# Small Language Model (SLM) Runtime

[![Version: 1.3.0](https://img.shields.io/badge/Version-1.3.0-blue.svg)](https://github.com/kzxl/ZeroLlm)
[![ZeroPlatform Tier](https://img.shields.io/badge/ZeroPlatform-Tier%203%20(Perception%20%26%20AI)-7c3aed.svg)](https://github.com/kzxl/ZeroPlatform)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Zero Dependencies](https://img.shields.io/badge/Dependencies-0%20(Pure%20C%23)-brightgreen.svg)]()
[![Multi-Targeting](https://img.shields.io/badge/.NET-8.0%20%7C%204.6.2%20%7C%20Standard%202.0-orange.svg)]()
[![Inference Speed](https://img.shields.io/badge/Decoding%20Speed-176.4%20tok%2Fs%20(CPU)-brightgreen.svg)]()

> **Architectural Standard**: 100% Pure C#, Zero Unmanaged Wrappers (no `llama.cpp` or Python runtime required), Zero External Dependencies, Multi-Targeting across `.NET 8.0`, `.NET Framework 4.6.2`, and `.NET Standard 2.0`.

`ZeroLlm` is an in-process, high-throughput Small Language Model (SLM) inference runtime and Transformer execution engine engineered for mission-critical edge gateways, industrial controllers, and enterprise agent systems. Operating within **Tier 3 (Perception & AI)** of the **ZeroPlatform** ecosystem, it bridges high-speed tokenization (`ZeroTokenizer`), prompt grammar constraints (`ZeroPrompt`), and cognitive agent swarms (`ZeroAgent`).

---

## ⚡ Performance Benchmarks (Pure C# .NET 8 CPU)

Through rigorous algorithmic and low-level kernel optimizations (Precomputed RoPE, Zero-Lock KV SequenceView, Pinned SwiGLU unrolling, and 4-way pipelined accumulator streams), `ZeroLlm` achieves **20.3x speedup** on pure CPU:

| Optimization Level | Data Type | Prompt Tokens | Generated Tokens | Decoding Speed | Total Latency | Speedup | Working Memory |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **Baseline (Scalar)** | FP32 | 52 | 20 | 8.7 tok/s | 2,298 ms | 1.0x | ~80 MB |
| **SIMD Vectorized** | FP32 | 52 | 100 | 147.5 tok/s | 677 ms | 17.0x | ~48 MB |
| **Deep-Optimized (v1.3.0)** | **FP32** | 52 | 100 | **176.4 tok/s** | **566.8 ms** | **20.3x** 🚀 | ~48 MB |
| **Deep-Optimized Quantized** | **Q8_0** | 52 | 100 | **99.0 tok/s** | **1,010.5 ms** | **11.4x** | **13.68 MB** ⚡ |

---

## 🏛️ Key Subsystem Capabilities

| Component | Namespace | Description |
| :--- | :--- | :--- |
| **`MoELayer`** | `ZeroLlm.Core.Layers` | DeepSeek-style Sparse Mixture-of-Experts with **1 Shared Expert + Top-2 Routed Experts** and auxiliary load-balancing loss. |
| **`PagedKvCache` & `KvSequenceView`** | `ZeroLlm.Core.Memory` | Virtual memory page-block allocator (16 tokens/block). `KvSequenceView` resolves physical blocks once per layer, eliminating **2,400 lock acquisitions/token**. |
| **`RoPE` (Precomputed Cache)** | `ZeroLlm.Core.Layers` | Precomputed trigonometric $(\cos, \sin)$ table lookup. Eliminates **1,152 transcendental mathematical calls/token**. |
| **`QuantizedKernels`** | `ZeroLlm.Core.Quantization` | High-performance Q8_0 and Q4_0 dot-product kernels unrolled with **4 independent accumulation streams** to eliminate CPU pipeline latency stalls. |
| **`SwiGLU`** | `ZeroLlm.Core.Layers` | Pinned-pointer unrolled Swish-Gated Linear Unit kernel ($Swish(gate) \odot up$) with zero array bounds checking overhead. |
| **`GqaAttention`** | `ZeroLlm.Core.Layers` | Grouped-Query Attention (GQA) kernel with multi-query head mapping and vectorized SIMD Value accumulation. |
| **`LlmSampler`** | `ZeroLlm.Core.Sampling` | Token sampling engine supporting Greedy, Temperature scaling, Top-K, Top-P (Nucleus), Min-P, Repetition penalty, and Grammar Logit Processors. |
| **`LlmEngine`** | `ZeroLlm.Core.Engine` | Zero-allocation autoregressive execution pipeline supporting synchronous completions and asynchronous token streaming (`IAsyncEnumerable<string>`). |
| **`GgufReader` & `GgufWriter`** | `ZeroLlm.Core.Format` | Pure C# binary reader and serializer for GGUF v2/v3 model formats, supporting FP32, Q8_0, and Q4_0 quantized tensors. |
| **`LlmTrainer`** | `ZeroLlm.Core.Training` | Masked Causal LM trainer with AdamW optimizer supporting Full parameter SFT, Head/Embedding adaptation, and MoE routing loss. |

---

## 🚀 Quickstart

### 1. Execute Text Completion with Pre-Quantized Model (Q8_0)

```csharp
using ZeroLlm.Core.Engine;
using ZeroLlm.Core.Format;
using ZeroLlm.Core.Sampling;
using ZeroTokenizer.Core.Vietnamese;

// 1. Load GGUF v3 Model
var model = GgufReader.LoadModel("models/vietnamese_erp_mds_official_q8_0.gguf");
var tokenizer = VietnameseErpTokenizer.CreateDefault();

// 2. Initialize Engine
using var engine = new LlmEngine(model, tokenizer, SamplingConfig.Greedy);

// 3. Generate Completion
string response = await engine.CompleteAsync("Kiểm tra tồn kho mặt hàng thép cuộn 10mm tại Kho Tổng");
Console.WriteLine(response);
```

### 2. Stream Tokens Asynchronously

```csharp
await foreach (string token in engine.GenerateStreamAsync("Tra cứu đơn hàng bán SO-2026-MDS01"))
{
    Console.Write(token);
}
```

### 3. Grammar-Constrained Tool Calling Decoding

```csharp
using ZeroPrompt.Core.Grammar;

var grammarProcessor = registry.CreateGrammarProcessor(tokenizer);

var sampling = new SamplingConfig
{
    Temperature = 0.0f,
    MaxTokens = 128,
    ContextLogitProcessor = grammarProcessor.Process
};

string toolCall = await engine.CompleteAsync(prompt, sampling);
// Guaranteed to produce 100% syntactically valid JSON tool call
```

---

## 📄 License

Architected and developed by **Phong Võ** (`kzxl`) for the **ZeroUniverse / ZeroPlatform** ecosystem. Released under the **MIT License**.
