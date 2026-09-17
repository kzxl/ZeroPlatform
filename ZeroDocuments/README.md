# ZeroDocuments

High-performance, zero-dependency document & spreadsheet manipulation library for the Zero Universe ecosystem.

## Features
- **Pure C# OpenXML Excel Reader/Writer**: Read and write modern `.xlsx` without EPPlus, ClosedXML, or DevExpress.
- **Zero-Dependency**: 100% built on .NET BCL (`System.IO.Compression` + `System.Xml`).
- **Cross-Platform**: Full support for `.NET Framework 4.6.2`, `.NET Standard 2.0`, and `.NET 8.0`.
- **Automatic Runtime Assembly Binding**: Solves .NET Framework `System.IO.Compression` assembly binding issues out-of-the-box.
- **High-Speed CSV Engine**: Allocation-efficient delimiter parsing with RFC 4180 compliance.
- **Type-Safe Mapping**: Auto-map spreadsheet rows directly into DTOs or `System.Data.DataTable`.

## Architecture
```
ZeroDocuments
 ├── Excel/
 │    ├── ExcelReader.cs
 │    ├── ExcelWriter.cs
 │    └── Models/
 │         ├── ExcelCellAddress.cs
 │         ├── ExcelRow.cs
 │         └── ExcelCell.cs
 ├── Csv/
 │    ├── CsvReader.cs
 │    └── CsvWriter.cs
 └── Common/
      └── RuntimeAssemblyResolver.cs
```

## Quick Start

### 1. Read Excel File (.xlsx)
```csharp
using ZeroDocuments.Excel;

// Read bounded by header range (e.g. D24:T24) down to max 5000 rows
DataTable table = ExcelReader.ReadByHeaderRange("data.xlsx", "D24:T24", maxRows: 5000);

// Or read specific range
DataTable custom = ExcelReader.ReadToDataTable("data.xlsx", "A2:G100");

// Or stream row-by-row
var rows = ExcelReader.ReadRows("data.xlsx", "A1:Z500");
foreach (var row in rows)
{
    string? val = row["B"]; // Read column B
}
```

### 2. Export to Excel (.xlsx)
```csharp
using ZeroDocuments.Excel;

// Export DataTable directly
ExcelWriter.WriteToFile("export.xlsx", myDataTable, sheetName: "Report");

// Export IEnumerable<T>
var items = new List<ProductDto> { ... };
ExcelWriter.WriteToFile("products.xlsx", items, sheetName: "Products");
```

### 3. Read & Write CSV
```csharp
using ZeroDocuments.Csv;

// Read CSV
DataTable csvTable = CsvReader.ReadToDataTable("data.csv", delimiter: ',');

// Write CSV
CsvWriter.WriteToFile("output.csv", myDataTable, delimiter: ',');
```
