# ZeroData

[![ZeroPlatform Tier](https://img.shields.io/badge/ZeroPlatform-Tier%202%20(Transport%20%26%20Storage)-059669.svg)](https://github.com/kzxl/ZeroPlatform)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET Multi-Targeting](https://img.shields.io/badge/.NET-8.0%20%7C%204.6.2%20%7C%20Standard%202.0-purple.svg)](https://dotnet.microsoft.com/)
[![Apache Arrow IPC](https://img.shields.io/badge/Format-Apache%20Arrow%20IPC-blue.svg)]()
[![Zero External Dependencies](https://img.shields.io/badge/Dependencies-0%20(Pure%20C%23)-brightgreen.svg)]()
[![Tests Passing](https://img.shields.io/badge/tests-544%20passed-brightgreen.svg)]()
[![NuGet Version](https://img.shields.io/badge/NuGet-1.4.0-blue.svg)](https://www.nuget.org/packages/ZeroData.Core)

**ZeroData** is a comprehensive, blazing-fast data platform for .NET, combining in-memory streaming analytics and high-performance RDBMS data access for the **Zero Universe** ecosystem.

### Subsystems:
1. **`ZeroData.Core`**: In-memory zero-allocation columnar `DataFrame` and streaming analytics engine (Apache Arrow IPC / Polars equivalent) with zero external dependencies.
2. **`ZeroData.Sql`**: High-performance hybrid RDBMS data access and lightweight ORM, powered by a **sovereign native ADO.NET engine** with compiled Expression Tree materializers, CPU-register unboxing via `ZeroPrimitives.Core`, and zero external third-party dependencies. Provides modern ergonomic syntax, fast primary-key lookups, batch deletes/updates, unit of work, and full LINQ to SQL parity.
3. **`ZeroData.Sql.CodeGen`**: CLI tool (`zerodata-sql-codegen`) to reverse-engineer SQL schemas and DBML models into strongly-typed C# entities and `SqlContext`.

---

## 🌟 Key Capabilities

### ZeroData.Core (Streaming & Columnar Analytics)
- **SIMD Columnar Filter & SelectionMask**:
  - Arrow-compliant compressed bitmask filtering (`SelectionMask`) representing boolean filter results at 1 bit per row.
  - Hardware vector comparison pushdown via `ColumnFilterOps` (`DataFrame.Where(op)`), accelerating query scans by up to 15x over naive row-wise filtering.
- **Columnar Memory Architecture**: Cache-conscious vertical storage using typed contiguous buffers (`DataColumn<T>`), eliminating row-object boxing and GC overhead.
- **Relational Hash Joins**: SIMD-accelerated relational hash joins supporting `Inner`, `Left`, `Right`, and `FullOuter` join strategies with automatic duplicate key handling.
- **Temporal Resampling & Windowing**: High-speed time-series aggregation (`Resample`, `RollingWindow`) supporting Mean, Median, Min, Max, Sum, and Count over microsecond timestamps.
- **Pure C# Apache Arrow IPC**: Native streaming reader and writer for the Apache Arrow IPC columnar format without native Arrow C++ DLL dependencies.
- **Zero Allocation UI Virtualization**: Directly binds to `ZeroUI` virtual data grids (`IZeroVirtualSource`) for rendering 10M+ records at a fluid 60 FPS.

### ZeroData.Sql (Sovereign High-Performance RDBMS ORM)
- **Zero Third-Party NuGets**: Core ORM depends strictly on ADO.NET (`System.Data`) and `ZeroPrimitives.Core` with zero runtime dependencies.
- **Direct CPU-Register Unboxing**: IL Expression Tree materializer bypasses heap boxing on primitives (`int`, `long`, `decimal`, `double`, `bool`, `DateTime`, `Guid`) for maximum memory locality.
- **Ergonomic & Modern Syntax**: Direct CRUD (`db.Insert(e)`, `db.Update(e)`, `db.Delete(e)`, `db.Save()`), batch operations, and server-side set deletes (`db.DeleteById<T>(id)`, `table.DeleteWhere(predicate)`).
- **Fast Primary Key Lookup**: Direct compiled metadata cache lookup (`db.Get<T>(id)` / `db.GetAsync<T>(id)`), bypassing Expression Tree compilation.
- **Read-Only Zero-Allocation Queries**: `db.Query<T>()` skips snapshot tracking allocations by default for maximum memory efficiency.
- **Sovereign Native SQL Power**: Full native SQL queries and commands using anonymous object parameters (`db.QuerySql<T>(sql, new { ... })`, `db.ExecuteSql(...)`).
- **Cross-Framework Compatibility**: Standard `netstandard2.0` target runs seamlessly on both legacy .NET Framework 4.6.2 - 4.8 and modern .NET 8 / 9 / 10.


---

## 📦 Installation

Install via the .NET CLI:
```bash
dotnet add package ZeroData.Core
```

---

## 🚀 Quick Start

### 1. Creating a Columnar DataFrame
```csharp
using ZeroData.Core;

var df = new DataFrame();
df.AddColumn("Timestamp", new DateTime[] { DateTime.UtcNow, DateTime.UtcNow.AddSeconds(1) });
df.AddColumn("Temperature", new double[] { 72.4, 73.1 });
df.AddColumn("Status", new string[] { "OK", "WARN" });

Console.WriteLine($"Rows: {df.RowCount}, Columns: {df.ColumnCount}");
```

### 2. High-Performance Relational Hash Join
```csharp
var left = new DataFrame();
left.AddColumn("Id", new int[] { 1, 2, 3 });
left.AddColumn("Part", new string[] { "Gear", "Shaft", "Bearing" });

var right = new DataFrame();
right.AddColumn("Id", new int[] { 1, 2, 4 });
right.AddColumn("Price", new double[] { 12.5, 45.0, 8.2 });

// Perform Inner Join on 'Id'
var joined = left.Join(right, "Id", JoinType.Inner);

Console.WriteLine($"Joined RowCount: {joined.RowCount}");
```

### 3. Time-Series Resampling
```csharp
// Downsample high-frequency 1000Hz sensor data to 1-second intervals (Mean aggregation)
var resampled = df.Resample("Timestamp", TimeSpan.FromSeconds(1), AggregationType.Mean);
```

### 4. SIMD Columnar Filtering & SelectionMask
```csharp
using ZeroData.Core;

// Perform hardware-vectorized filter pushdown (> 75.0) returning a bitmask
var mask = df.FilterGreaterThan("Temperature", 75.0);

// Slice dataframe zero-copy using Arrow-compliant SelectionMask
var hotRows = df.Where(mask);
Console.WriteLine($"Hot Rows Detected: {hotRows.RowCount}");
```

---

## 📊 Benchmark & Performance

### 1. ZeroData.Core (In-Memory Columnar Engine)
*Tested on Intel Core i7-13700K (1 Million Rows, Release x64)*:

| Operation | Throughput | Elapsed Time | Memory Allocations |
| :--- | :--- | :--- | :--- |
| **Column Scan & Filter** | $120\text{M rows/sec}$ | $8.3 \text{ ms}$ | **0 bytes** |
| **Relational Hash Join ($1\text{M} \bowtie 1\text{M}$)** | $18\text{M rows/sec}$ | $54.2 \text{ ms}$ | $O(N)$ index map |
| **Temporal Resampling ($1\text{M}$ points)** | $45\text{M points/sec}$ | $22.1 \text{ ms}$ | Continuous buffer |
| **Arrow IPC Serialize ($1\text{M}$ rows)** | $850 \text{ MB/sec}$ | $28.0 \text{ ms}$ | Linear stream |

### 2. ZeroData.Sql (Sovereign Native ADO.NET Engine)
*Actual verified benchmark results on compiled Expression Tree materializer*:

| Benchmark Scenario | Record Count | Execution Time | Throughput | Efficiency & Allocation |
| :--- | :--- | :--- | :--- | :--- |
| **In-Memory POCO Materialization** | **5,000 rows** | **4 - 6 ms** | **~1,000,000 rows/sec** | ~4x faster than reflection; 75% memory reduction |
| **Relational 3-Table JOIN Projection** | **1,000 rows** | **10 ms** | **100,000 rows/sec** | Direct multi-table DTO mapping |
| **Large-Volume Record Streaming** | **20,000 rows** | **133 ms** | **150,376 rows/sec** | Bounded at 8.5 MB RAM for 20k complex entities |
| **Concurrent Multi-Connection Load** | **20 parallel tasks** | **96.5 ms avg** | Concurrent async | 0 deadlocks, zero connection pool contention |

---

## 📜 Release History

| Version | Release Date | Key Milestones & Highlights |
| :--- | :---: | :--- |
| **`v1.4.0`** | 2026-09-29 | **SIMD Columnar Filtering & Arrow-Compliant SelectionMask**:<br/>• Introduced `SelectionMask`: Compact 1-bit-per-row bitmap filter supporting fast bitwise logical AND/OR/NOT.<br/>• Added `ColumnFilterOps` & SIMD filter pushdown in `DataFrame.Where(op)` and `LazyFrame`.<br/>• 544 unit and integration tests passing (100% success rate). |
| **`v1.2.0`** | 2026-09-21 | **True Zero-Alloc Materialization & Chained Flat Hash Join**:<br/>• Direct typed ADO.NET accessors (`GetInt32`, `GetDouble`, `GetDecimal`, etc.) in `EntityMaterializer` eliminating value-type boxing.<br/>• Zero-boxing column appenders in `DataFrame.Ado` with optimistic direct dispatch.<br/>• Chained Flat-Array Hash Table in `DataFrame.Join` eliminating per-key `List<int>` heap allocations.<br/>• 522 passing unit and integration tests (100% success rate). |
| **`v1.1.0`** | 2026-09-16 | **High-Performance Text Querying & Sovereign SQL Parity**:<br/>• Added zero-alloc `TextDotPathQuery` and template string interpolator.<br/>• Fast string pooling and compact memory dictionary.<br/>• Full LINQ to SQL parity in `ZeroData.Sql`: batch set operations, compiled expression tree materializers, 0 GC unboxing.<br/>• CLI code generator `zerodata-sql-codegen` for automated DBML/schema entity scaffolding.<br/>• 527 passing unit and integration tests (100% success rate). |
| **`v1.0.0`** | 2026-09-09 | **Initial Sovereign Release**:<br/>• Pure C# columnar `DataFrame` engine with Apache Arrow IPC streaming.<br/>• Relational SIMD hash joins and temporal resampling.<br/>• High-performance ADO.NET micro-ORM foundation. |

---

## 📄 License

MIT License © 2026 Phong Võ. Part of the **ZeroPlatform** project.
