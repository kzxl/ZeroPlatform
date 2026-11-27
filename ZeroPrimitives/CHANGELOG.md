# Changelog

All notable changes to `ZeroPrimitives.Core` are documented in this file.

## Unreleased

- No changes yet.

## 3.2.0 — 2026-11-27

- Add `ZeroPrimitives.Core.Identifiers.SnowflakeId` (64-bit `BIGINT`-native distributed ID) and thread-safe monotonic `SnowflakeGenerator`.
- Add `ZeroPrimitives.Cryptography.CheckDigit` (Luhn Mod-10, GS1 GTIN/EAN/SSCC Mod-10, ISO 7064 Mod 97-10 / IBAN, and Damm quasigroup) and `FastXxHash64` / `XxHash64State` (deterministic cross-process hashing with normalized `decimal` support).
- Add zero-allocation `FastBase64` (standard + Base64Url) and `FastBase32` (Crockford + RFC 4648), `NaturalStringComparer`, `ValueResult<T, TError>`, and `ScaledInt64` (8-byte fixed-point Q4) with SIMD `SimdAggregations.Sum` / `MinMax` over `long`, `int` (widened), and `ScaledInt64`.
- Add `ZeroPrimitives.Numerics.DecimalMath` (`RoundingMode`, `Round`, `RoundToStep`, `Normalize`, `GetScale`, `SafeDivide`, `Percentage`, `WeightedAverage`) and `ProportionalAllocator` (Largest Remainder exact-sum allocation for `decimal` and `long`).
- Add `ZeroPrimitives.Time.CompactDate` (4-byte `DateOnly` polyfill) and `DateRange` (half-open `[Start, EndExclusive)` calendar intervals with `TryIntersect`, `TryUnion`, `Subtract`, `SplitByMonth`).
- Add bounded span-lookup `StringInternPool` (`GetOrAdd` / `GetOrAddUtf8`) and RFC 4180 UTF-8 `FastCsvWriter` with optional formula-injection neutralization.
- Add `Uuid7` and `FastUlid` SQL Server `UNIQUEIDENTIFIER` sort-order mapping (`ToSqlServerGuid`, `FromSqlServerGuid`), plus `Uuid7.MinValueForTimestamp`/`MaxValueForTimestamp`, `TryFormatUtf8`, and `TryParse(ReadOnlySpan<byte>)`.
- Extend `FastFormat` with zero-allocation `TryFormatDecimal`/`Utf8`, `TryFormatDecimalGrouped`, `TryFormatIsoDate`/`IsoDateTime`/`Date`, `TryFormatGuid`/`Utf8`, `TryFormatInt32Utf8`, and `TryFormatUInt64`/`Utf8` (wired into `FastJsonWriter` and `ValueStringBuilder` on legacy TFMs).
- Add SQL-safe half-open boundary helpers (`StartOfNextDay/Month/Quarter/Year`, `EndOfDaySql`) to `DateTimeExtensions`.
- Harden `FastDateParser` for full ISO 8601 fractional seconds/timezone offsets, compact `yyyyMMdd`, day-first single-digit `d/M/yyyy`, and UTF-8 `ReadOnlySpan<byte>` parsing (`TryParse`, `TryParseDateTimeOffset`).
- Fix UTF-8 multi-byte rune-boundary truncation in `FixedString32`/`FixedString64`, add `TryCreate` overloads, and introduce 128-byte `FixedString128`.
- Harden `FastNumberParser` against `Int64` complex-path overflow and unbounded `stackalloc` in extended decimals, add accounting parentheses negative support `(x)`, and fix UTF-8 multi-byte currency decoding on `ReadOnlySpan<byte>` overloads.
- Add JSON string unescaping (`\uXXXX` & standard escapes) in `FastJsonReader.GetString`, plus `TryGetUnescapedString`, `ValueEquals`, and `Skip`.
- Reject non-finite `double` (`NaN`/`Infinity`) in `FastJsonWriter` for RFC 8259 compliance and add array-element value overloads.
- Fix `FastConvert.AsNullableBool` default fallback on unrecognized strings and add bounds-checked `FastCsvParser.TryUnquote`.
- FastBinary delegates to BinaryPrimitives (single-instruction loads/stores); short spans now throw ArgumentOutOfRangeException instead of IndexOutOfRangeException.
- Remove legacy-TFM intermediate string/array allocations in SpanWriter UTF-8 writes, Uuid7.ToString, AlphaSequence.Increment, and FastHash (ArrayPool UTF-8 encoding + FastHex uppercase formatting).
- Add safe bounds-checked VarIntCodec.GetEncodedLength and TryWriteVarUInt32/TryWriteVarInt32/TryWriteVarUInt64/TryWriteVarInt64 APIs.
- Enable XML documentation, snupkg symbol packages with embedded SourceLink, package validation against baseline 3.1.2, NativeAOT/trimming compatibility on net8.0+, and dev prerelease suffix for CI packs.

### Deprecated
- `ZP0001`: `VnFinancialRounding` (move to application/ERP layer; removal in `4.0.0`).
- `ZP0002`: `FastGs1Parser` and `Gs1Element` (moving to `ZeroRfid`; removal in `4.0.0`).
- `ZP0003`: `SimdColorConverter` (moving to `ZeroGraphics.Imaging`/`ZeroVideo`; removal in `4.0.0`).
- `ZP0004`: `SpscQueue<T>` (use `ZeroConcurrency.DataPlane.ZeroRingBuffer`; removal in `4.0.0`).
- `ZP0005`: `SharedMemoryRingBuffer` (use `ZeroConcurrency.Ipc.ZeroMmfRingBuffer`; removal in `4.0.0`).
- `ZP0006`: `FastTableBinary` and `FastTableMapper` (moving to `ZeroData.Core`; removal in `4.0.0`).

## 3.1.2 — 2026-11-27

- **D-01**: Replace unaligned `*(Vector<T>*)` pointer dereferences in `SimdVector` and `SimdOps` with `Unsafe.ReadUnaligned` and `Unsafe.WriteUnaligned`.
- **D-02**: Fix `FastNumberParser.TryParseDouble` to handle IEEE-754 scientific notation, large doubles beyond `decimal.MaxValue`, and non-decimal `defaultValue` inputs without overflow.
- **D-03**: Widen `FastJsonWriter` depth state bitmask to 64 bits with explicit nesting limit (`MaxDepth = 63`) and emit valid 4-byte UTF-8 for surrogate pairs (with `\uXXXX` fallback for lone surrogates).
- **D-04**: Clear rented arrays containing managed references on `ArrayPool<T>.Shared.Return` in `ArrayPoolRentScope<T>`, `ArrayPoolBufferWriter<T>`, and `ValueList<T>`.
- **D-05**: Track and decrement `NativeMemoryTracker` on disposal/finalization of unpooled `NativeMemoryPool.Rent` blocks exceeding `MaxBucketSize` (>64 MB).
- **D-06**: Reject 5th-byte `VarUInt32` and 10th-byte `VarUInt64` overflow payloads in `VarIntCodec`.
- **D-07**: Use `long` dimension arithmetic in `SimdColorConverter` bounds validation to prevent 32-bit integer overflow.

## 3.1.1 — 2026-11-27

- Independent versioning and release pipeline (`primitives-v*`).
