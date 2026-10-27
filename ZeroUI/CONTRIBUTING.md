# Contributing to ZeroUI ⚡

Thank you for your interest in contributing to **ZeroUI**! We welcome contributions that improve performance, expand industrial UI capabilities, fix bugs, or enhance documentation.

Please take a moment to review this guide before submitting issues or pull requests.

---

## 📜 Code of Conduct

All contributors and participants are expected to adhere to our [Code of Conduct](CODE_OF_CONDUCT.md). Please treat others with respect and professionalism.

---

## 🛠️ Getting Started

### Prerequisites
- **Operating System**: Windows 10/11 x64
- **.NET SDK**: .NET 8.0 SDK (or later)
- **Target Framework Packs**:
  - `.NET 8.0-windows`
  - `.NET Framework 4.6.2` (for legacy enterprise runtime support)
  - `.NET Standard 2.0`
- **IDE**: Visual Studio 2022 (v17.8+), JetBrains Rider, or VS Code with C# Dev Kit.

### Repository Setup
1. Fork and clone the repository:
   ```bash
   git clone https://github.com/kzxl/ZeroUI.git
   cd ZeroUI
   ```
2. Restore dependencies and build the solution:
   ```bash
   dotnet build ZeroUI.slnx
   ```
3. Run the test suite:
   ```bash
   dotnet test
   ```

---

## 📐 Architecture & Engineering Standards

ZeroUI is built specifically for ultra-high-throughput industrial and mission-critical UI scenarios (SCADA, HMI, machine vision). Every contribution must follow these principles:

### 1. Zero-Allocation Hot Path Policy
- **No heap allocations in render or measurement loops**: Never allocate objects, lambdas, or boxed structs during redraws, viewport calculations, or scroll events.
- **Utilize Spans and Memory Pooling**: Prefer `ReadOnlySpan<T>`, `stackalloc`, and reusable buffer pools (`ArrayPool<T>`) for temporary computations.
- **Struct Layouts**: Use `readonly struct` where appropriate to avoid defensive copying.

### 2. Zero External Dependencies
- ZeroUI maintains **zero third-party external runtime dependencies**. All core algorithms, rendering pipelines, and controls are written in pure C# or utilize standard platform APIs.

### 3. Multi-Targeting Discipline
- Ensure new APIs compile cleanly across:
  - `net8.0-windows`
  - `net462`
  - `netstandard2.0`
- When using modern C# features, provide polyfills or conditional compilation (`#if NET8_0_OR_GREATER`) if the feature is unavailable in older frameworks.

### 4. Code Formatting & Naming
- **Language**: All code, XML doc comments, variable names, and commit messages must be in **Standard Technical English**.
- **Naming**:
  - `PascalCase` for types, namespaces, methods, and properties.
  - `camelCase` for local variables and method parameters.
  - `_camelCase` for private instance fields.
- **Documentation**: Public APIs must have clear XML documentation comments (`<summary>`, `<param>`, `<returns>`).

---

## 🔄 Development Workflow & Commit Conventions

We enforce [Conventional Commits](https://www.conventionalcommits.org/) for clean, readable history:

- `feat(scope)`: New feature or UI control
- `fix(scope)`: Bug fix
- `perf(scope)`: Performance optimization (zero-alloc, CPU/GPU)
- `refactor(scope)`: Code refactoring without behavioral changes
- `test(scope)`: Adding or improving tests
- `docs(scope)`: Documentation updates or additions
- `chore(scope)`: Build system, version bump, or repo housekeeping

**Example:**
```bash
feat(zerogrid): add Excel-style column distinct filter dropdown
fix(wpf): correct DropShadowEffect opacity in DarkChromeWindow
perf(render): eliminate temporary brush allocations in cell painter
```

---

## 🚀 Submitting a Pull Request

1. **Create a branch**:
   ```bash
   git checkout -b feat/your-feature-name
   ```
2. **Implement changes and add tests**:
   - Verify that all existing and new tests pass: `dotnet test`.
   - Ensure zero GC allocations on hot paths if modifying rendering or virtualization logic.
3. **Commit cleanly**:
   - Write clear, concise commit messages following the convention above.
4. **Open a Pull Request**:
   - Complete the [Pull Request Template](.github/pull_request_template.md).
   - Reference any relevant issues (e.g., `Fixes #123`).

---

## 🐛 Reporting Issues

- **Bug Reports**: Use our [Bug Report Template](https://github.com/kzxl/ZeroUI/issues/new?template=bug_report.yml). Include reproduction steps, environment details, and relevant logs or screenshots.
- **Feature Requests**: Use our [Feature Request Template](https://github.com/kzxl/ZeroUI/issues/new?template=feature_request.yml). Explain the industrial problem, proposed API or control behavior, and alternatives considered.

Thank you for helping make ZeroUI faster, lighter, and more reliable! 🚀
