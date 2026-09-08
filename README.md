# ZeroPlatform

High-performance, enterprise-grade application ecosystem for .NET (Windows Forms / WPF / Modern .NET). 

Designed around the **Platform & Satellites (Core & Satellites)** architecture, unifying industrial UI controls and direct hardware-accelerated graphics under independent, highly-decoupled repositories.

---

## 🏛️ Ecosystem Architecture

```
ZeroPlatform/                                  # Workspace Container
├── ZeroPlatform.slnx                          # Master XML Solution (debug & develop all projects)
├── README.md                                  # Platform overview & satellite guidelines
├── .gitignore
│
├── ZeroUI/                                    # [INDEPENDENT GIT REPO 1: UI SYSTEM]
│   ├── .git/                                  # Standalone Git history & versioning
│   ├── src/
│   │   ├── ZeroUI.Core/                       # Layout engines, theme manager, base models
│   │   ├── ZeroUI.WinForms/                   # Industrial controls, docking, grids, cards
│   │   └── ZeroUI.Wpf/                        # Modern WPF parity controls
│   ├── samples/                               # Interactive benchmark demos
│   └── tests/                                 # 408 automated unit & integration tests
│
├── ZeroGraphics/                              # [INDEPENDENT GIT REPO 2: HARDWARE GRAPHICS]
│   ├── .git/                                  # Standalone Git history & versioning
│   ├── src/
│   │   ├── ZeroGraphics.Core/                 # SDF Math, LTTB Decimation, GPU Telemetry
│   │   ├── ZeroGraphics.Direct2D/             # DirectWrite ClearType Subpixel typography
│   │   ├── ZeroGraphics.DirectX/              # Direct3D 11 SDF cards, drop shadows, glow
│   │   └── ZeroGraphics.Waveform/             # Real-time LineStrip oscilloscope (100k pts)
│   ├── samples/                               # GPU hardware acceleration demos
│   └── tests/                                 # 16 hardware pipeline tests
│
├── Satellites/                                # [SATELLITE EXPANSION ZONE]
│   └── README.md                              # Blueprint for adding future satellites
│       ├── ZeroPlatform.Charts/               # (Planned) High-speed financial & telemetry charts
│       ├── ZeroPlatform.IoT/                  # (Planned) Industrial protocol streaming (Modbus, OPC-UA)
│       └── ZeroPlatform.Reports/              # (Planned) Document & thermal printing engine
│
└── samples/
    └── ZeroPlatform.Samples.Showcase/         # Full integration showcase (ZeroUI + ZeroGraphics)
```

---

## 🚀 Key Architectural Pillars

1. **Dual Independent Repositories (Multi-Repo)**:
   - `ZeroUI` and `ZeroGraphics` have **completely separate Git repositories**, release lifecycles, and commit histories.
   - Core graphics developers work exclusively within `ZeroGraphics` without touching UI business logic.
   - UI developers work within `ZeroUI` without needing C++ or HLSL shader compilers.

2. **Unified Workspace Container (`ZeroPlatform.slnx`)**:
   - The master solution `ZeroPlatform.slnx` allows developers to open, edit, build, and debug across the entire platform in a single IDE instance.

3. **Zero External Dependencies**:
   - Both `ZeroUI` and `ZeroGraphics` adhere to a pure .NET foundation with zero third-party runtime package requirements.
   - Direct COM VTable P/Invoke is utilized for Direct3D 11 and Direct2D.

4. **Dual-Targeting Support**:
   - Fully supports **.NET Framework 4.6.2** for enterprise ERP/MDS systems and **.NET 8.0/9.0+ Windows** for next-generation apps.

---

## ⚡ Quick Start

### Build Entire Platform
```bash
dotnet build ZeroPlatform.slnx
```

### Run All Unit & Integration Tests (424+ tests)
```bash
dotnet test ZeroUI/tests/ZeroUI.Core.Tests/ZeroUI.Core.Tests.csproj
dotnet test ZeroGraphics/tests/ZeroGraphics.Tests/ZeroGraphics.Tests.csproj
```

### Launch Unified Showcase Application
```bash
dotnet run --project samples/ZeroPlatform.Samples.Showcase/ZeroPlatform.Samples.Showcase.csproj -f net8.0-windows
```

---

## 📄 Authors & License

Developed and engineered by Phong Võ. MIT License.
