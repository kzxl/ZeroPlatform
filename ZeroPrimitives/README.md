# ZeroPrimitives

> **Architectural Standard**: 100% Pure C#, Zero External Dependencies, Multi-Targeting across `.NET 8.0`, `.NET Framework 4.6.2`, and `.NET Standard 2.0`.

`ZeroPrimitives` is a sovereign, high-throughput .NET library engineered for ultra-fast, zero-allocation primitive conversions, low-level span/pointer number parsing, and expression-compiled object mapping.

It replaces slow legacy conversion methods (`Convert.To*`, `value.ToString()`, `int.TryParse` with intermediate heap allocations) with raw CPU register unboxing, `ReadOnlySpan<char>` slicing, and pointer arithmetic.

---

## Key Features

- **Direct Register Unboxing**: Immediate unpack for boxed value types (`int`, `long`, `decimal`, `double`, `short`, `byte`, `bool`) without calling `.ToString()`.
- **Zero-Allocation Number Parsing**:
  - Direct pointer loops: `acc = (acc * 10) + (*ptr - '0')`.
  - Automatic thousand separator handling (comma, dot, space).
  - In-place currency stripping (`VNĐ`, `VND`, `đ`, `$`, `€`, `¥`).
- **SQL Server DateTime Safety**:
  - Zero-allocation ISO 8601, SQL date, and Vietnamese date formats (`dd/MM/yyyy`).
  - Automatic clamping to SQL Server `DATETIME` range (`1753-01-01` to `9999-12-31`).
- **Compiled Expression FastMapper**:
  - Replaces slow reflection with native IL delegates compiled once per type-pair.
- **Drop-in Extension Methods**:
  - `val.AsInt()`, `val.AsNullableInt()`, `val.AsLong()`, `val.AsDecimal()`, `val.AsDate()`, `val.AsSqlDateString()`, `val.HasValue()`.

---

## Multi-Targeting Support

- **.NET 8.0+** (Modern high-throughput cloud & edge runtimes)
- **.NET Framework 4.6.2+** (Enterprise WinForms / WPF applications)
- **.NET Standard 2.0** (Cross-platform compatibility)

---

## Quick Example

```csharp
using ZeroPrimitives;
using ZeroPrimitives.Extensions;

// 1. Direct Unbox (0 bytes allocated)
object boxed = 12345;
int num = boxed.AsInt(); // 12345

// 2. Formatted Currency Parse (0 bytes allocated)
object price = " 1,500,000.50 VNĐ ";
decimal amount = price.AsDecimal(); // 1500000.50m

// 3. Date Parsing & SQL Clamping
object rawDate = "25/12/2026 14:30:00";
DateTime dt = rawDate.AsDate(); // 2026-12-25 14:30:00
string sqlDate = rawDate.AsSqlDateString(); // "2026-12-25"

// 4. Expression-Compiled FastMapper
var dto = FastMapper.Map<SourceEntity, TargetDto>(entity);
```

---

## License

MIT License. Copyright © 2026 Phong Võ (`kzxl`).
