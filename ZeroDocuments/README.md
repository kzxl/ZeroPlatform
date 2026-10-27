# ZeroDocuments

[![ZeroPlatform Tier](https://img.shields.io/badge/ZeroPlatform-Tier%205%20(Presentation%20%26%20Apps)-e11d48.svg)](https://github.com/kzxl/ZeroPlatform)
[![NuGet Version](https://img.shields.io/badge/nuget-v1.5.0-blue.svg)](https://www.nuget.org/packages/ZeroDocuments.Core/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Zero External Dependencies](https://img.shields.io/badge/Dependencies-0%20External-brightgreen.svg)]()
[![Tests: 130 Passed](https://img.shields.io/badge/Tests-130%20Passed%20(100%25)-brightgreen.svg)]()
[![Multi-Targeting](https://img.shields.io/badge/.NET-8.0%20%7C%204.6.2%20%7C%20Standard%202.0-orange.svg)]()

> **Architectural Standard**: 100% Pure C# BCL, Zero External Dependencies (No EPPlus, ClosedXML, or DevExpress), Multi-Targeting across `.NET 8.0`, `.NET Framework 4.6.2`, and `.NET Standard 2.0`.

`ZeroDocuments` is an ultra-fast, zero-dependency spreadsheet and document processing engine engineered for enterprise applications (MDS ERP, WinForms, Web APIs, and microservices). It reads and writes modern Microsoft Excel files (`.xlsx`) and RFC 4180 CSV streams in pure C# using native BCL primitives (`System.IO.Compression` + `System.Xml`), eliminating the need for bulky third-party libraries (EPPlus, ClosedXML, NPOI) or expensive proprietary packages (DevExpress).

---

## 💡 Why ZeroDocuments?

| Feature | Legacy Libraries (EPPlus, ClosedXML) | DevExpress Spreadsheet | ZeroDocuments |
| :--- | :--- | :--- | :--- |
| **Dependencies** | 5 – 15 transitive packages | Heavy proprietary DLLs (~40MB+) | **0 External Dependencies** (Pure BCL) |
| **License** | Commercial / PolyForm / AGPL | Commercial (Per-Developer License) | **MIT License** (Free & Open Source) |
| **Publish Size** | +15MB – 30MB | +40MB – 80MB | **< 100 KB** |
| **Memory Footprint** | Heavy DOM Tree (>200MB on 100k rows) | Heavy UI/DOM model | **Streaming XmlReader (&lt; 15MB RAM)** |
| **Security** | Vulnerable to CSV Injection if unescaped | Depends on implementation | **CWE-1236 Guard & CWE-409 Zip Bomb Guard** |
| **Reliability** | Direct file overwrite (can corrupt on abort)| Direct file overwrite | **Atomic File Writes (`AtomicFileWriter`)** |
| **.NET 4.6.2 Compatibility**| Prone to `System.IO.Compression` binding issues | Complex assembly deployment | **Native Auto-Resolver built-in** |

---

## 🏛️ Comprehensive Architecture

```
ZeroDocuments.Core
 ├── Excel/
 │    ├── ZeroExcel.cs                   # Fluent multi-sheet workbook factory
 │    ├── ExcelWorkbookBuilder.cs        # Fluent multi-sheet API (images, conditional formatting, autofilter)
 │    ├── ExcelReader.cs                 # Single-pass streaming XmlReader & POCO mapper
 │    ├── ExcelWriter.cs                 # Streaming single-sheet exporter (sources enumerated once, never buffered)
 │    ├── Internal/
 │    │    ├── XlsxPackageWriter.cs      # Single OPC/SpreadsheetML generator shared by Writer & Builder
 │    │    ├── XlsxPackageReader.cs      # OPC relationship resolution, shared strings, date-style detection
 │    │    ├── OADateFormatter.cs        # OLE Automation serial <-> ISO date conversion (1900 & 1904 systems)
 │    │    └── SpreadsheetXml.cs         # Hardened XmlReader/XmlWriter factories, sanitizers, ST_Xstring decoder
 │    └── Models/
 │         ├── ExcelCellAddress.cs       # Coordinate calculations (e.g., "$D$24", "A:C", "1:5")
 │         ├── ExcelRow.cs               # Column-indexed lightweight row model
 │         ├── ExcelCell.cs              # Typed cell value container
 │         ├── ExcelWriterOptions.cs     # Per-call thread-safe writer options
 │         └── ExcelReaderOptions.cs     # Per-call reader options & decompression limit guards
 ├── Csv/
 │    ├── CsvReader.cs                   # Buffered RFC 4180 streaming parser with delimiter flexibility
 │    ├── CsvWriter.cs                   # Streaming DataTable & typed collection CSV generator (Invariant culture)
 │    └── CsvWriterOptions.cs            # Per-call thread-safe CSV writer options
 └── Common/
      ├── PropertyAccessorCache.cs       # Compiled Expression Trees for 30x-50x faster POCO mapping
      ├── ValueConverter.cs              # Culture-invariant, Excel-tolerant text -> CLR type conversion
      ├── TabularSource.cs               # Lazy DataTable / POCO row adapters
      ├── FormulaInjectionGuard.cs       # Single source of truth for CWE-1236 mitigation
      ├── FormulaInjectionMode.cs        # Formula injection modes: PrefixQuote, QuotePrefixStyle, Disabled
      ├── AtomicFileWriter.cs            # Crash-resilient atomic file writes via sibling temp files
      └── RuntimeAssemblyResolver.cs     # Self-healing assembly binder for .NET Framework runtimes
```

---

## 🌟 Key Capabilities

### 1. Low-Memory Streaming Excel Reader (`ExcelReader`)
- **Single-Pass Streaming (`StreamRows`)**: One forward-only `XmlReader` per sheet (no per-row subtree readers); ~4x fewer allocations than v1.3.
- **Compiled POCO Mapping (`Read<T>`)**: Maps worksheets directly into strongly-typed DTOs (classes or structs) via compiled Expression Trees; tolerant of Excel artifacts (`"15.0"` → `int`, `"1E-3"` → `decimal`, OLE serial → `DateTime`).
- **Header-Bounded & Advanced Range Parsing**: Bounded parsing (`D24:T24`), absolute references (`$A$1:$D$50`), full columns (`A:C`), and full rows (`1:5`). Default unbounded range (`A1:XFD1048576`) dynamically trims empty trailing columns.
- **Sheet Inspection**: Query workbook sheet metadata without full decompression via `ExcelReader.GetSheetNames`.
- **Zip-Bomb Decompression Guard (CWE-409)**: Enforces `MaxUncompressedEntryBytes` via `BoundedReadStream` on XML entry streams, preventing decompression denial-of-service.
- **Real Date Semantics**: Date-formatted numeric cells (built-in and custom formats, 1900 and 1904 systems) are returned as ISO text (`yyyy-MM-dd[ HH:mm:ss]`).
- **Third-Party Package Compatibility**: OPC relationship resolution (absolute / `../` targets, tab order), implicit row/cell references, rich text, phonetic-run exclusion, and ECMA-376 ST_Xstring escape decoding (`_x000D_`, `_x005F_`).

### 2. Fluent Multi-Sheet Workbook Builder (`ZeroExcel`) & `ExcelWriter`
- **Multi-Sheet Support**: Add multiple sheets with custom names, data sources (DataTables, POCOs, 2D grids), and header styling (Bold, border layout).
- **Native Excel Dates**: `DateTime` values are written as real date cells (`yyyy-mm-dd` / `yyyy-mm-dd hh:mm:ss`), sortable and filterable in Excel.
- **Formula Injection Mitigation (CWE-1236)**:
  - `QuotePrefixStyle` (Recommended): Uses OpenXML `quotePrefix="1"` cell styling so Excel renders formulas starting with `=`, `+`, `-`, `@` as plain text without modifying or prepending physical quotes to raw data.
  - `PrefixQuote`: Prepends single quote `'` character to string data.
  - `Disabled`: Preserves raw formulas for downstream evaluation.
- **Crash-Resilient Atomic File Writes (`AtomicFileWriter`)**: Writes to a sibling `.tmp` file and atomically commits via `File.Replace` / `File.Move`, ensuring existing files remain intact during application crashes or interruptions.
- **Native Excel AutoFilter**: Emits both `<autoFilter>` markup and native Excel `_xlnm._FilterDatabase` defined names in `workbook.xml`.
- **Schema-Valid Output**: Generated packages validate with 0 errors against the Office 2019 OpenXML schema (sheet names, colors, NaN/Infinity, CR/LF all sanitized).
- **Direct Memory & File Export**: Save to file, stream, or byte array (`ToArray()`).

### 3. Compiled Expression Trees (`PropertyAccessorCache`)
- Eliminates reflection overhead when serializing or deserializing collections of objects, achieving 30x–50x speedups over `PropertyInfo.GetValue`.

### 4. RFC 4180 CSV Engine (`CsvReader` & `CsvWriter`)
- **Flexible Delimiters**: Support for comma (`,`), semicolon (`;`), tab (`\t`), and custom delimiters.
- **Culture-Invariant Formatting**: Numbers, decimals, floating points, booleans, and dates are serialized strictly under `CultureInfo.InvariantCulture`, preventing locale-dependent corruption (e.g. comma decimal separator).
- **CWE-1236 Guard**: Prevents CSV injection attacks by prefixing dangerous trigger characters in text fields with single quotes (typed numbers such as `-5` are never altered).
- **Atomic Writes**: Writes to temporary file before replacing destination file.

### 5. Self-Healing Runtime Assembly Resolver (`RuntimeAssemblyResolver`)
- Automatically resolves `.NET Framework 4.6.2` assembly binding redirects for `System.IO.Compression`.

### 6. OpenXML DrawingML Image Engine (`AddImage` & `ExtractImages`)
- **Direct Image Embedding**: Embed PNG, JPEG, GIF, BMP, TIFF, EMF and WMF images into any worksheet with precise cell anchors, pixel dimensions, and EMU coordinate scaling.
- **Embedded Media Extraction**: Extract all embedded images from existing `.xlsx` packages via `ExcelReader.ExtractImages`.

### 7. Rich Conditional Formatting & AutoFilter
- **Cell Highlight Rules**: Highlight cells matching conditions (GreaterThan, LessThan, Equal, Between) with custom ARGB background fills and bold fonts via OpenXML DXF styles.
- **Color Scales & Data Bars**: Generate 2-color / 3-color gradient heatmaps and horizontal data bars.
- **AutoFilter**: One-line automatic header filtering (`SetAutoFilter`).


---

## 🚀 Quick Start Examples

### 1. Read Excel by Header Range (Enterprise Template Pattern)
```csharp
using ZeroDocuments.Excel;

// Header row D24:T24 names the columns; data is read below it (max 5,000 rows).
DataTable table = ExcelReader.ReadWithHeaders("PurchaseOrder.xlsx", "D24:T24", maxRows: 5000);

foreach (DataRow row in table.Rows)
{
    string itemCode = row["Item Code"]?.ToString() ?? "";
    decimal quantity = Convert.ToDecimal(row["Quantity"], CultureInfo.InvariantCulture);
    Console.WriteLine($"Item: {itemCode} | Qty: {quantity}");
}

// Positional variant: skips the header row and names columns "Column_D" … "Column_T".
DataTable positional = ExcelReader.ReadByHeaderRange("PurchaseOrder.xlsx", "D24:T24", maxRows: 5000);
```

### 2. Stream Rows from Excel File
```csharp
using ZeroDocuments.Excel;

// Stream rows without loading the entire document into memory
var rows = ExcelReader.ReadRows("Inventory.xlsx", "A1:Z500");

foreach (var row in rows)
{
    string? barcode = row["B"]; // Read cell by column letter
    string? name = row["C"];
    Console.WriteLine($"{barcode}: {name}");
}
```

### 3. Multi-Sheet Workbook Builder (ZeroExcel)
```csharp
using ZeroDocuments.Excel;

using var workbook = ZeroExcel.Create();
workbook.AddSheet("Summary", summaryTable);
workbook.AddSheet("Products", productList);
workbook.AddSheet("AuditLogs", logRows, headers: new[] { "Event", "Date", "Status" });

// Save directly to file or byte array
workbook.Save("EnterpriseReport.xlsx");
byte[] rawBytes = workbook.ToArray();
```

### 4. Read Excel to Strongly-Typed POCO Collections
```csharp
using ZeroDocuments.Excel;

// Automatically map Excel columns into ProductDto properties via compiled Expression Trees
List<ProductDto> products = ExcelReader.Read<ProductDto>("Products.xlsx", "A1:E500", "ProductCatalog");

foreach (var p in products)
{
    Console.WriteLine($"ID: {p.Id}, SKU: {p.Sku}, Price: {p.Price:C}");
}
```

### 5. Read and Write CSV Files with CWE-1236 Protection
```csharp
using ZeroDocuments.Csv;

// Read CSV to DataTable
DataTable csvData = CsvReader.ReadToDataTable("telemetry.csv", delimiter: ',');

// Write DataTable to CSV (with automatic formula injection mitigation)
CsvWriter.WriteToFile("output.csv", csvData, delimiter: ';', includeHeaders: true);
```

### 6. Embed Images via OpenXML DrawingML
```csharp
using ZeroDocuments.Excel;

byte[] logoBytes = File.ReadAllBytes("company_logo.png");

using var workbook = ZeroExcel.Create();
workbook.AddSheet("Invoice", invoiceRows, headers: new[] { "Item", "Quantity", "Price" })
        .AddImage("Invoice", logoBytes, format: "png", column: 5, row: 1, widthPx: 140, heightPx: 70, name: "CompanyLogo")
        .Save("InvoiceWithLogo.xlsx");

// Extract embedded media from existing workbooks
List<ExcelEmbeddedImage> images = ExcelReader.ExtractImages("InvoiceWithLogo.xlsx");
```

### 7. Conditional Formatting & AutoFilter
```csharp
using ZeroDocuments.Excel;
using ZeroDocuments.Excel.Models;

using var workbook = ZeroExcel.Create();
workbook.AddSheet("KPI", salesData, headers: new[] { "Region", "Sales", "Target" })
        .SetAutoFilter("KPI") // Enable header filter dropdowns
        .AddHighlightRule("KPI", "B2:B100", CellRuleOperator.LessThan, "5000",
                          fillColorHex: "FFC7CE", fontColorHex: "9C0006", bold: true)
        .AddColorScale("KPI", "C2:C100", minColorHex: "F8696B", maxColorHex: "63BE7B")
        .Save("KpiDashboard.xlsx");
```

### 8. Advanced Writer & Reader Options (v1.5.0)
```csharp
using ZeroDocuments.Common;
using ZeroDocuments.Excel;
using ZeroDocuments.Excel.Models;

// 1. Non-destructive formula injection mitigation (OpenXML quotePrefix style)
var writerOptions = new ExcelWriterOptions
{
    FormulaInjection = FormulaInjectionMode.QuotePrefixStyle // keeps "=SUM(A1)" as text without writing "'"
};
ExcelWriter.WriteToFile("CleanData.xlsx", dataTable, "Sheet1", options: writerOptions);

// 2. Query sheet names without decompressing full worksheets
List<string> sheetNames = ExcelReader.GetSheetNames("CleanData.xlsx");

// 3. Hardened decompression limits against zip bombs (CWE-409)
var readerOptions = new ExcelReaderOptions
{
    MaxUncompressedEntryBytes = 100 * 1024 * 1024 // 100 MB limit per entry
};
DataTable data = ExcelReader.ReadToDataTable("Untrusted.xlsx", options: readerOptions);
```

---

## 💻 Supported Platforms

| Framework | Target Support | Highlights |
| :--- | :--- | :--- |
| **.NET 8.0+** | Native (`net8.0`) | High-throughput ZIP and memory optimization |
| **.NET Framework** | Legacy WinForms / WPF (`net462`) | Includes automatic assembly resolver for compression |
| **.NET Standard** | Universal Cross-Platform (`netstandard2.0`) | Universal compatibility for shared libraries |

---

## 🏛️ Ecosystem Architectural Alignment

ZeroDocuments is a sovereign, self-contained component of **Tier 5 (Presentation & Orchestration)** within the **ZeroPlatform** industrial automation ecosystem.

- **100% Pure BCL Sovereign Footprint**: Zero external dependencies (no EPPlus, ClosedXML, or third-party DLLs), guaranteeing maximum portability, zero supply-chain risk, and instant deployment across ERP and Edge nodes.
- **Strict DAG Conformance**: Independent component with zero cross-tier circularity.
- **Packaging & CI/CD**: Standardized under `Company = ZeroPlatform`, `Authors = Phong Võ`, `<ZeroTier>5</ZeroTier>`.

---

## 📄 License
MIT License © 2026 Phong Võ (`kzxl`). Part of the **ZeroPlatform** sovereign ecosystem.

