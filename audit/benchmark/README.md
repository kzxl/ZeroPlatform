# ZeroPrimitives & ZeroConcurrency Independent Benchmark Suite

This directory contains the independent, non-intrusive performance and correctness test harness for `ZeroPrimitives` and `ZeroConcurrency`. It adheres to strict audit rules: no production source code has been altered.

## Structure

- `source/`: Reproducible benchmark runner project (`AuditRunner.csproj`, `Program.cs`, `WraparoundAuditTest.cs`).
- `results/`: Captured benchmark CSV output across runtimes:
  - `benchmark_run_x64_8.csv`: Executed on modern .NET 8.0 x64.
  - `benchmark_run_x64_4.csv`: Executed on .NET Framework 4.6.2 (CLR 4.0.30319.42000) x64.

## How to Re-Run

### Prerequisites
- .NET SDK (8.0 or 10.0 preview)
- .NET Framework 4.6.2 Developer Pack / Runtime (Windows)

### Execution Commands

```powershell
# Run modern .NET 8.0 benchmark
dotnet run --project "audit/benchmark/source/AuditRunner.csproj" -f net8.0 -c Release

# Run .NET Framework 4.6.2 benchmark
dotnet run --project "audit/benchmark/source/AuditRunner.csproj" -f net462 -c Release
```
