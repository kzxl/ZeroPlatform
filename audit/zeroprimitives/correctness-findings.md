# ZeroPrimitives Correctness & Edge-Case Findings

## 1. FastNumberParser & FastDateParser
- **Status:** **VERIFIED (Pass)**
- **Boundary Handling:** Correctly detects overflow and underflow on integer types (`int.MinValue`, `int.MaxValue`, `long.MinValue`, `long.MaxValue`).
- **Invalid Input Semantics:** Returns `false` immediately upon encountering non-digit characters without throwing exceptions or allocating strings.
- **Whitespace Handling:** Gracefully ignores leading and trailing ASCII whitespace.
- **Limitation:** In `FastNumberParser.TryParseDecimal`, exponent scientific notation (`1.23e4`) is not fully parsed; however, standard financial fixed-point strings (`123,456.78` and `123456.78`) are handled accurately with culture-invariant formatting.

## 2. FastConvert & Type Unboxing
- **Status:** **VERIFIED (Pass with Design Constraint)**
- **Behavior with Null & DBNull:** Correctly intercepts `null` and `DBNull.Value` at the very beginning of methods, returning configured default values without throwing `NullReferenceException`.
- **Boxing Reality:** When passing a raw value type (e.g., calling `FastConvert.AsInt(1234)`), the argument `object? value` causes a managed heap boxing operation at the call-site. `FastConvert` is explicitly designed for weakly-typed ADO.NET and reflection scenarios (`DataRow["Column"]`, `IDataRecord.GetValue(i)`), where the runtime object is *already* boxed by the underlying driver.

## 3. FastMapper & FastTableMapper
- **Status:** **PARTIALLY VERIFIED (Defects & Omissions Identified)**
- **Defect 1: Nullable Unbox Failure on Null Values in Expression:**
  In [`FastMapper.cs`](file:///E:/15.%20Other/ZeroUniverse/ZeroPlatform/ZeroPrimitives/src/ZeroPrimitives.Core/Mapping/FastMapper.cs#L201-L206):
  ```csharp
  var underlyingSource = Nullable.GetUnderlyingType(matchSource.PropertyType) ?? matchSource.PropertyType;
  var underlyingTarget = Nullable.GetUnderlyingType(targetProp.PropertyType) ?? targetProp.PropertyType;
  if (underlyingSource == underlyingTarget)
  {
      var cast = Expression.Convert(sourceAccess, targetProp.PropertyType);
      var assign = Expression.Assign(Expression.Property(targetParam, targetProp), cast);
      blockExpressions.Add(assign);
  }
  ```
  If `source` has property `int? Value = null`, and `target` has property `int Value`, the compiled expression executes `(int)source.Value`. In the CLR, casting a null `Nullable<T>` to `T` throws `InvalidOperationException: Nullable object must have a value`. It lacks a coalescing guard (`source.Value ?? default`).
- **Defect 2: No Struct Support:**
  Public methods enforce `where TSource : class` and `where TTarget : class, new()`, making it impossible to map readonly record structs or stack-allocated DTOs.
- **Omission in FastTableMapper (Missed Zero-Alloc ADO.NET Path):**
  In [`FastTableMapper.CompileRecordMapper`](file:///E:/15.%20Other/ZeroUniverse/ZeroPlatform/ZeroPrimitives/src/ZeroPrimitives.Core/Mapping/FastTableMapper.cs#L170), the compiler binds to `IDataRecord.GetValue(ordinal)` instead of typed accessors (`GetInt32`, `GetDecimal`, `GetDateTime`). In SQL Server client drivers (`SqlClient`), `GetValue` allocates a boxed object for every scalar cell, defeating zero-allocation goals during database reader streaming.

## 4. Off-Heap Memory Allocators (ArenaAllocator & NativeMemoryPool)
- **Status:** **VERIFIED (Memory-Safe with Lifetime Constraints)**
- **Double Disposal & Leak Prevention:** All allocators implement idempotent disposal guards via `Interlocked.Exchange(ref _disposed, 1)`. Finalizers (`~ArenaAllocator()`) are implemented as safety nets to prevent OS memory leaks if caller drops references.
- **Buffer Overrun Risk:** In Release builds, pointer arithmetic advances without hardware guard pages. An out-of-bounds write past the allocated arena capacity will corrupt adjacent unmanaged blocks. Adding Debug-only guard pages is recommended for enterprise stability.
