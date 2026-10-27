# 🔤 ZeroTokenizer: Sovereign Pure C# BPE & Tiktoken Engine

[![Version: 1.1.0](https://img.shields.io/badge/Version-1.1.0-blue.svg)](https://github.com/kzxl/ZeroTokenizer)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET Multi-Targeting](https://img.shields.io/badge/.NET-8.0%20%7C%204.6.2%20%7C%20Standard%202.0-purple.svg)](https://dotnet.microsoft.com/)
[![Zero External Dependencies](https://img.shields.io/badge/Dependencies-0%20(Pure%20C%23)-brightgreen.svg)]()

**ZeroTokenizer** is an ultra-fast, zero-allocation Byte-Pair Encoding (BPE), WordPiece, and Tiktoken tokenizer engine engineered in 100% pure C# for .NET. Operating within **Tier 3 (Perception & AI)** of the [ZeroPlatform](https://github.com/kzxl/ZeroPlatform) ecosystem, it provides native tokenization for Small Language Models (`ZeroLlm`) and autonomous agents (`ZeroAgent`).

---

## ⚡ Key Capabilities

- **Zero-Allocation Tokenizer Pipeline**:
  - Direct span-based encoding: `int Encode(ReadOnlySpan<char> text, Span<int> destinationTokenIds)`.
  - Fast token counter: `int CountTokens(ReadOnlySpan<char> text)` without allocating integer arrays or string chunks.
  - Streaming decoding: `int Decode(ReadOnlySpan<int> tokenIds, Span<char> destinationText)`.
- **Vietnamese ERP Specialized BPE (`VietnameseErpTokenizer`)**:
  - Domain-specialized vocabulary with 16,000 subword merges optimized for industrial ERP terminology, warehouse codes, equipment tags, and accented Vietnamese syllables.
  - High compression ratio ($< 1.8$ characters/token on Vietnamese ERP text vs $3.5+$ characters/token on generic tokenizers).
- **Canonical Special Tokens (`SpecialTokens`)**:
  - Pre-mapped control tokens for multi-turn dialogues and role framing: `<bos>`, `<eos>`, `<pad>`, `<unk>`, `<system>`, `</system>`, `<user>`, `</user>`, `<assistant>`, `</assistant>`, `<thought>`, `</thought>`, `<tool_call>`, `</tool_call>`, `<tool_result>`, `</tool_result>`, `<response>`, `</response>`.
- **Serialization & Model Portability**:
  - Direct binary and plain-text vocabulary persistence (`Save` / `Load`) for fast startup and embedding in deployment artifacts.
- **Tiktoken Engine**:
  - Pre-configured regex patterns for `cl100k_base` (OpenAI GPT-4 / ChatGPT) and `o200k_base` (GPT-4o).
- **Context Token Budgeter (`TokenBudgeter`)**:
  - Greedy Knapsack packing algorithm for LLM prompts, ensuring conversation histories, system instructions, and RAG knowledge fragments never overflow the context window.
- **Zero External Dependencies & Multi-Targeting**:
  - Compatible with `.NET 8.0+`, `.NET Framework 4.6.2+`, and `.NET Standard 2.0`.

---

## 🚀 Quick Start

### 1. Count Tokens Without Memory Allocations

```csharp
using ZeroTokenizer.Core.Tiktoken;

var tokenizer = TiktokenTokenizer.CreateCl100kBase();

ReadOnlySpan<char> prompt = "System: Analyze factory vibration telemetry at node 4.";
int tokenCount = tokenizer.CountTokens(prompt);

Console.WriteLine($"Token count: {tokenCount}");
```

### 2. Specialized Vietnamese ERP BPE Tokenizer

```csharp
using ZeroTokenizer.Core.Vietnamese;

var tokenizer = VietnameseErpTokenizer.CreateDefault();

// Encode Vietnamese domain text
int[] tokens = tokenizer.Encode("Kiểm tra tồn kho mặt hàng thép cuộn 10mm tại Kho Tổng");
string decoded = tokenizer.Decode(tokens);

Console.WriteLine($"Vocabulary Size: {tokenizer.VocabularySize}");
Console.WriteLine($"Encoded Tokens Count: {tokens.Length}");
```

### 3. Save and Load Custom BPE Vocabularies

```csharp
using ZeroTokenizer.Core.Bpe;

var bpe = new BpeTokenizer(vocabDict, mergesList);

// Save vocabulary to file
bpe.Save("models/vietnamese_erp.bpe");

// Load trained tokenizer
var loaded = BpeTokenizer.Load("models/vietnamese_erp.bpe");
```

---

## 📄 License

Architected and developed by **Phong Võ** (`kzxl`) for the **ZeroUniverse / ZeroPlatform** ecosystem. Released under the **MIT License**.
