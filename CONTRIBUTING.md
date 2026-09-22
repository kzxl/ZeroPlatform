# Contributing to ZeroPlatform 🌐

Thank you for your interest in contributing to **ZeroPlatform**! ZeroPlatform is a sovereign, enterprise-grade software ecosystem for industrial automation, high-performance edge computing, computer vision, DSP, TSDB storage, and hardware-accelerated HMI/SCADA controls.

---

## 📜 Code of Conduct

All participants must abide by our [Code of Conduct](CODE_OF_CONDUCT.md). Please treat all community members with respect and professionalism.

---

## 🏛️ Ecosystem Structure & 6-Tier Architecture

ZeroPlatform is organized as an umbrella orchestrator coordinating **28 autonomous satellite repositories** classified under a strict **6-Tier Directed Acyclic Graph (DAG)**:

- **Tier 0: Core Foundation (The Bedrock)**: [`ZeroPrimitives`](https://github.com/kzxl/ZeroPrimitives), [`ZeroConcurrency`](https://github.com/kzxl/ZeroConcurrency), [`ZeroSecurity`](https://github.com/kzxl/ZeroSecurity)
- **Tier 1: Compute & System**: [`ZeroSystem`](https://github.com/kzxl/ZeroSystem), [`ZeroCompression`](https://github.com/kzxl/ZeroCompression), [`ZeroTensor`](https://github.com/kzxl/ZeroTensor), [`ZeroCompute`](https://github.com/kzxl/ZeroCompute)
- **Tier 2: Transport & Storage**: [`ZeroNetwork`](https://github.com/kzxl/ZeroNetwork), [`ZeroComm`](https://github.com/kzxl/ZeroComm), [`ZeroIoT`](https://github.com/kzxl/ZeroIoT), [`ZeroRfid`](https://github.com/kzxl/ZeroRfid), [`ZeroStorage`](https://github.com/kzxl/ZeroStorage), [`ZeroData`](https://github.com/kzxl/ZeroData)
- **Tier 3: Perception & Intelligence**: [`ZeroSignal`](https://github.com/kzxl/ZeroSignal), [`ZeroGeometry`](https://github.com/kzxl/ZeroGeometry), [`ZeroVideo`](https://github.com/kzxl/ZeroVideo), [`ZeroAudioVisual`](https://github.com/kzxl/ZeroAudioVisual), [`ZeroInference`](https://github.com/kzxl/ZeroInference), [`ZeroNeural`](https://github.com/kzxl/ZeroNeural)
- **Tier 4: Graphics & Spatial 3D**: [`ZeroGraphics`](https://github.com/kzxl/ZeroGraphics), [`ZeroCharts`](https://github.com/kzxl/ZeroCharts), [`ZeroTwin3D`](https://github.com/kzxl/ZeroTwin3D), [`Zero3D`](https://github.com/kzxl/Zero3D)
- **Tier 5: Presentation & Orchestration**: [`ZeroDocuments`](https://github.com/kzxl/ZeroDocuments), [`ZeroReports`](https://github.com/kzxl/ZeroReports), [`ZeroPipeline`](https://github.com/kzxl/ZeroPipeline), [`ZeroUI`](https://github.com/kzxl/ZeroUI), [`ZeroUI.React`](https://github.com/kzxl/ZeroUI.React)

👉 Read the authoritative specifications:
- **[Tier Taxonomy & Architectural Governance (SPEC-ARCH-001)](docs/architect/tier-taxonomy-specification.md)**
- **[Subsystem Catalog & Git Repository Standards (GOV-REPO-001)](docs/governance/subsystem-catalog-and-git-descriptions.md)**
- **[Autonomous Satellites Architecture (SPEC-ARCH-002)](docs/architect/satellites-architecture.md)**

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
2. Clone or synchronize all 28 satellite repositories:
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

## 📐 Strict Architectural Invariants

1. **The Downstream Invariant (Strict DAG)**: Subsystems in Tier $N$ may only reference subsystems in Tier $< N$. Upward or cyclic dependencies will fail automated validation.
2. **Tier 0 Independence**: Core foundation libraries (`ZeroPrimitives`, `ZeroConcurrency`, `ZeroSecurity`) must have **0 external and 0 internal platform dependencies**.
3. **Zero External Runtime Dependencies**: All core subsystems are engineered in 100% pure C# or standard platform APIs (no unmanaged native C++ binaries, Python wrappers, or external redistributables).
4. **Hybrid Linking**: Satellite references to other satellites must use conditional project/package linking:
   ```xml
   <ItemGroup Condition="Exists('..\..\ZeroConcurrency\src\ZeroConcurrency\ZeroConcurrency.csproj')">
     <ProjectReference Include="..\..\ZeroConcurrency\src\ZeroConcurrency\ZeroConcurrency.csproj" />
   </ItemGroup>
   <ItemGroup Condition="!Exists('..\..\ZeroConcurrency\src\ZeroConcurrency\ZeroConcurrency.csproj')">
     <PackageReference Include="ZeroConcurrency" Version="1.0.0" />
   </ItemGroup>
   ```
5. **Zero-Allocation Hot Paths**: Use `Span<T>`, `ReadOnlySpan<T>`, and memory pools in tight computational loops.
6. **Multi-Targeting**: Maintain compatibility across `net8.0`, `net462`, and `netstandard2.0` where specified.
7. **Standard Technical English**: All code, documentation, XML comments, and commit messages must be written in clean, professional technical English.

---

## 🔄 Commit & PR Guidelines

- Follow [Conventional Commits](https://www.conventionalcommits.org/): `feat:`, `fix:`, `perf:`, `refactor:`, `test:`, `docs:`, `chore:`.
- Ensure all tests pass before submitting a Pull Request.
- Complete the [Pull Request Template](.github/pull_request_template.md).

Thank you for building the future of sovereign industrial software with us! 🚀
