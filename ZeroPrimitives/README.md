# ZeroPrimitives

> **Architectural Standard**: 100% Pure C#, Zero External Dependencies, Multi-Targeting across `.NET 8.0`, `.NET Framework 4.6.2`, and `.NET Standard 2.0`.

`ZeroPrimitives` is a sovereign, high-throughput .NET library engineered for ultra-fast, zero-allocation primitive conversions, low-level span/pointer number parsing, cryptographic/non-cryptographic hashing, binary buffer manipulation, and expression-compiled object mapping.

It replaces slow legacy conversion methods (`Convert.To*`, `value.ToString()`, `int.TryParse` with intermediate heap allocations) with raw CPU register unboxing, `ReadOnlySpan<char>` slicing, and pointer arithmetic.

---

## 🏛️ Comprehensive Feature Suite

### 1. Zero-Allocation Type Conversion (`FastConvert`)
- **Direct Register Unboxing**: Immediate unpack for boxed value types (`int`, `long`, `decimal`, `double`, `float`, `short`, `byte`, `bool`, `Guid`, `DateTime`) without calling `.ToString()`.
- **Nullable Variants**: `AsNullableInt`, `AsNullableLong`, `AsNullableDecimal`, `AsNullableDouble`, `AsNullableBool`, `AsNullableDateTime`, `AsNullableGuid`.
- **Enum Conversion**: `AsEnum<TEnum>()`, `AsNullableEnum<TEnum>()` (case-insensitive string/number conversion).
- **Delimited Collections**: `FromDelimitedString<T>()` and `AsDelimitedString<T>()` for fast CSV/list parsing.

### 2. Low-Level Span/Pointer Parsers (`FastNumberParser`, `FastDateParser`)
- **Pointer-based Integer Loops**: `(acc << 3) + (acc << 1) + (*ptr - '0')`.
- **Intelligent Separator Analysis**: Handles both international and Vietnamese thousand/decimal delimiters (`1,234.56` vs `1.234,56` vs `1.500.000 VNĐ`).
- **SQL Server DateTime Safety**: Zero-allocation ISO 8601 and `dd/MM/yyyy` parsing with clamping to SQL Server `DATETIME` range (`1753-01-01` to `9999-12-31`).

### 3. Cryptography & Hashing (`FastHash`)
- **Non-Cryptographic**: Ultra-fast 32-bit and 64-bit **FNV-1a** for hash tables, caching, and span lookup.
- **Cryptographic**: `Md5Hex`, `Sha1Hex`, and `Sha256Hex` formatting directly to hexadecimal.

### 4. Binary Buffers & Endianness (`FastBuffer`)
- **GZip Compression**: `GzipCompress` and `GzipDecompressToString` with optimized memory streams.
- **Bit-level Endian Swapping**: `SwapInt16`, `SwapUInt16`, `SwapInt32`, `SwapUInt32`, `SwapInt64`, `SwapUInt64`.

### 5. Calendar & Date Extensions (`DateTimeExtensions`)
- **Boundaries**: `FirstDayOfMonth`, `LastDayOfMonth`, `FirstDayOfQuarter`, `LastDayOfQuarter`, `FirstDayOfYear`, `LastDayOfYear`, `StartOfDay`, `EndOfDay`.
- **Unix Timestamps**: `ToUnixTimestampSeconds`, `ToUnixTimestampMilliseconds`, `FromUnixSeconds`, `FromUnixMilliseconds`.
- **Formatters**: `AsDateString_ddMMyyyy`, `AsDateString_ddMMyyyyHHmmss`, `ToOrdinalDateString` (`6th Jan, 2026`).

### 6. String & Functional Utilities (`StringExtensions`, `FunctionalExtensions`)
- **String Manipulation**: `NullIfEmpty`, `Truncate`, `TrimChars`, `AppendPathSegments`, `SetQueryParam`, `RemoveLetterToInt`, `RemoveDiacritics`.
- **Functional Pipeline**: `Let<T, TResult>`, `AsTask<T>`.
- **Sanitation**: `ReplaceNullStrings` (replaces null string properties with `""` before SQL inserts).

### 7. Compiled Expression Object Mapper (`FastMapper`)
- Compiles Expression Trees into native IL delegates cached per type-pair, delivering raw assignment speed with zero reflection overhead.

---

## ⚡ Quick Example

```csharp
using ZeroPrimitives;
using ZeroPrimitives.Buffers;
using ZeroPrimitives.Cryptography;
using ZeroPrimitives.Extensions;

// 1. Direct Unbox (0 bytes allocated)
object boxed = 12345;
int num = boxed.AsInt(); // 12345

// 2. Formatted Currency Parse (0 bytes allocated)
object price = " 1,500,000.50 VNĐ ";
decimal amount = price.AsDecimal(); // 1500000.50m

// 3. Date Parsing & Calendar Helpers
DateTime dt = "25/12/2026 14:30:00".AsDate();
DateTime monthStart = dt.FirstDayOfMonth(); // 2026-12-01
string sqlDate = dt.AsSqlDateString();      // "2026-12-25"

// 4. Fast Hash & Endianness
string md5 = FastHash.Md5Hex("ZeroPlatform");
int beInt = FastBuffer.SwapInt32(0x12345678); // 0x78563412

// 5. Expression-Compiled FastMapper
var dto = FastMapper.Map<SourceEntity, TargetDto>(entity);
```

---

## Multi-Targeting Support

- **.NET 8.0+** (High-throughput cloud, edge AI & IPC)
- **.NET Framework 4.6.2+** (Enterprise WinForms / WPF applications)
- **.NET Standard 2.0** (Universal cross-platform compatibility)

---

## License

MIT License. Copyright © 2026 Phong Võ (`kzxl`).
