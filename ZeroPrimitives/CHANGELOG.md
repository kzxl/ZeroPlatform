# Changelog

All notable changes to `ZeroPrimitives.Core` are documented in this file.

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
