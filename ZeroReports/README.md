# ZeroReports: Enterprise Pure C# Banded Report, Vector PDF & Thermal Label Engine

[![ZeroPlatform Tier](https://img.shields.io/badge/ZeroPlatform-Tier%205%20(Presentation%20%26%20Apps)-e11d48.svg)](https://github.com/kzxl/ZeroPlatform)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET Multi-Targeting](https://img.shields.io/badge/.NET-8.0%20%7C%204.6.2%20%7C%20Standard%202.0-purple.svg)](https://dotnet.microsoft.com/)
[![NuGet Version](https://img.shields.io/badge/NuGet-1.3.0-blue.svg)](https://www.nuget.org/packages/ZeroReports)

**ZeroReports** is an enterprise-grade, zero-external-dependency banded report engine, vector PDF 1.4 document generator, and industrial thermal printer engine for .NET. Inspired by DevExpress XtraReports, it delivers native banded pagination, rich table formatting, master-detail hierarchy, vector barcodes, security watermarks, and zero-copy columnar data binding with `ZeroData`.

---

## Key Features

### 1. Enterprise Banded Report Engine (DevExpress XtraReports Parity)
- **7-Tier Hierarchical Bands**: `ReportHeader`, `PageHeader`, `GroupHeader`, `Detail`, `GroupFooter`, `PageFooter`, and `ReportFooter`.
- **Master-Detail Sub-Reports**: Arbitrary multi-level nested detail reporting via `DetailReportBand`.
- **Dynamic Aggregate Expressions**: In-band calculations (`[SUM(Field):C0]`, `[COUNT(Field)]`, `[AVG(Field):N2]`, `[MIN]`, `[MAX]`) evaluated over group scopes or the entire report.
- **Two-Pass Pagination**: Smart page breaking with automatic calculation of total pages (`{PageNumber}` of `{TotalPages}`).

### 2. Structured Report Table (`ReportTable`)
- Equivalent to DevExpress `XRTable`, `XRTableRow`, and `XRTableCell`.
- Exact column alignment across headers, details, and summary footers.
- **ColSpan** support for multi-column spanning cells.
- Granular **CellBorders** (`Top`, `Bottom`, `Left`, `Right`, `All`, `None`) and background highlights.

### 3. Pure C# Vector Barcode Encoders (Zero Dependencies)
- **Code 128 (ISO/IEC 15417)**: Auto-switching Sets A/B/C with Modulo-103 checksum calculation.
- **QR Code (ISO/IEC 18004)**: Galois Field $GF(2^8)$ arithmetic, Reed-Solomon Error Correction, and automatic mask selection.
- **EAN-13 (ISO/IEC 15420)**: Modulo-10 check digit, variable parity encoding (L/G/R), and quiet zones.
- Embedded directly into vector PDF streams and SVG HTML without bitmap rasterization or blur.

### 4. Zero-Copy Data Binding
- Native, zero-memory-copy binding to `ZeroData.Core.DataFrame`.
- Strongly-typed reflection binding via `ObjectReportDataSource<T>` with fast scalar unboxing through `ZeroPrimitives.FastConvert`.
- Flexible key-value dictionary binding via `DictionaryReportDataSource`.

### 5. Conditional Formatting & Security Watermark
- **ConditionalFormattingRule**: Dynamically highlight critical thresholds, change foreground/background colors, or toggle bold typography based on record conditions (`[Status] == 'FAIL'`).
- **ReportWatermark**: Angle-rotated background text watermarks (e.g. `CONFIDENTIAL`, `DRAFT`) with configurable opacity and typography.

### 6. Industrial Thermal Print Codecs
- Native command stream generators for **Zebra ZPL II** (`^XA...^XZ`) and **TSC TSPL** industrial barcode printers.

---

## Quick Start

```csharp
using ZeroReports.Barcodes;
using ZeroReports.Data;
using ZeroReports.Engine;
using ZeroReports.Engine.Formatting;
using ZeroReports.Engine.Table;

// 1. Initialize Report & Geometry
var report = new Report("SalesInvoice")
{
    Title = "Sales Invoice Report"
};
report.SetWatermark("ORIGINAL", opacity: 0.15f);

// 2. Configure Bands & Table
report.ReportHeader.Height = 40f;
report.ReportHeader.AddLabel("COMMERCIAL INVOICE", 0f, 10f, 500f, 25f, fontSize: 18f, isBold: true);

report.Detail.Height = 25f;
report.Detail.AddExpression("[ProductCode]", 0f, 2f, 120f, 18f);
report.Detail.AddExpression("[ProductName]", 130f, 2f, 250f, 18f);
report.Detail.AddExpression("[TotalAmount:C0]", 390f, 2f, 120f, 18f, alignment: ReportTextAlignment.Right);

// 3. Conditional Rule
var failRule = new ConditionalFormattingRule("Status", "==", "REJECTED")
{
    TextColor = (1f, 0f, 0f),
    IsBold = true
};
report.Detail.Controls[0].FormattingRules.Add(failRule);

// 4. Barcode Control
report.ReportFooter.Height = 80f;
report.ReportFooter.AddBarcode(BarcodeSymbology.QrCode, "https://zerouniverse.vn/verify/INV-9021", 0f, 10f, 60f, 60f);
report.ReportFooter.AddExpression("Grand Total: [SUM(TotalAmount):C0]", 200f, 20f, 300f, 20f, fontSize: 13f, isBold: true);

// 5. Bind Data & Export
report.SetDataSource(salesList);
byte[] pdfData = report.ExportToPdf();
string htmlPreview = report.ExportToHtml();
```

---

## Multi-Targeting

- `.NET 8.0+`
- `.NET Framework 4.6.2+`
- `.NET Standard 2.0`

---

## 🏛️ Ecosystem Architectural Alignment

ZeroReports is a sovereign member of **Tier 5 (Presentation & Orchestration)** within the **ZeroPlatform** industrial automation ecosystem.

```
┌──────────────────────────────────────────────────────────┐
│ Tier 5: Presentation & Orchestration (ZeroReports)       │
└────────────────────────────┬─────────────────────────────┘
                             │ consumes
              ┌──────────────┴──────────────┐
              ▼                             ▼
┌──────────────────────────┐  ┌────────────────────────────┐
│ Tier 0: Primitives       │  │ Tier 1: Data & Streaming   │
│ (ZeroPrimitives.Core)    │  │ (ZeroData.Core)            │
└──────────────────────────┘  └────────────────────────────┘
```

- **Upstream Ingestion**: Consumes Tier 0 foundational primitives (`ZeroPrimitives.Core 1.3.0`) and Tier 1 high-performance columnar DataFrame buffers (`ZeroData.Core 1.3.0`).
- **Strict DAG Conformance**: Zero references to parallel presentation or higher layers.
- **Packaging & CI/CD**: Standardized under `Company = ZeroPlatform`, `Authors = Phong Võ`, `<ZeroTier>5</ZeroTier>`.

---

## License

MIT License. Copyright © 2026 Phong Võ (`kzxl`).
