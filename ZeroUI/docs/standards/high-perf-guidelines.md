# ZeroUI High-Performance C# Coding Guidelines

## 1. Zero Garbage Collection Allocation in Hot Paths

Hot paths (methods executed per-frame, per-scroll, or per-cell render) must achieve **0 allocations (0 B/op)**.

### Mandatory Rules:
1. **No String Concatenation or Boxing in Render Loops:**
   * ❌ **Forbidden:** `string text = $"Row: {row}, Col: {col}";`
   * ❌ **Forbidden:** `object val = cell.Value; string s = val.ToString();`
   * ✔️ **Required:** Use pre-formatted char buffers, `ISpanFormattable`, or direct glyph index drawing.
2. **Buffer Pooling over Heap Allocation:**
   * ❌ **Forbidden:** `var buffer = new byte[width * 4];` inside `OnPaint`.
   * ✔️ **Required:** Rent from `ArrayPool<T>.Shared` and return inside a `try ... finally` block:
     ```csharp
     byte[] buffer = ArrayPool<byte>.Shared.Rent(requiredSize);
     try
     {
         ProcessPixels(buffer.AsSpan(0, requiredSize));
     }
     finally
     {
         ArrayPool<byte>.Shared.Return(buffer);
     }
     ```
3. **Stack Allocation for Transient Micro-Buffers (<1 KB):**
   * Use `stackalloc` with `Span<T>` for local math or rectangle transforms:
     ```csharp
     Span<int> columnOffsets = stackalloc int[visibleColCount];
     ```

---

## 2. Structs, Value Types & Parameter Passing

1. **`readonly struct` for Data Transfer:**
   All coordinate, layout, and event payloads must be immutable value types:
   ```csharp
   public readonly struct CellBounds
   {
       public readonly int X;
       public readonly int Y;
       public readonly int Width;
       public readonly int Height;

       public CellBounds(int x, int y, int width, int height) =>
           (X, Y, Width, Height) = (x, y, width, height);
   }
   ```
2. **Pass Large Structs with `in` (Pass by Readonly Reference):**
   * Prevents copying overhead on 64-bit architectures:
     ```csharp
     public void RenderCell(in CellBounds bounds, in RenderStyle style) { ... }
     ```

---

## 3. Unsafe Memory & SIMD Vectorization

1. **Pixel Blending & Shading:**
   * Use raw pointer arithmetic for innermost pixel loops:
     ```csharp
     unsafe
     {
         uint* pPixel = (uint*)pBuffer.ToPointer();
         // Process scanlines sequentially to maximize L1/L2 data cache locality
     }
     ```
2. **SIMD Acceleration:**
   * When clearing buffers, computing bounding boxes, or blending colors, leverage `System.Numerics.Vector<T>` or `System.Runtime.Intrinsics.X86.Avx2` when available, falling back to a scalar remainder loop.

---

## 4. Collection Rules
* ❌ **Never** use `List<T>` or `IEnumerable<T>` inside the render or hit-test pipeline (causes boxing and enumerator allocations).
* ✔️ Use raw arrays (`T[]`), `Span<T>`, or custom unmanaged fixed-size ring buffers.

---

## 5. Cross-Target Runtime Compatibility Matrix

To support enterprise environments running **.NET Framework 4.6.2** while maximizing the raw speed of modern **.NET 8.0 / 9.0**, adhere to the following target-specific patterns:

| Feature / API | Modern Target (`net8.0-windows`) | Legacy Enterprise (`net462` / `netstandard2.0`) |
| :--- | :--- | :--- |
| **Unmanaged Memory** | `NativeMemory.Alloc` / `NativeMemory.Free` | `Marshal.AllocHGlobal` / `Marshal.FreeHGlobal` |
| **SIMD Intrinsics** | `System.Runtime.Intrinsics.X86.Avx2` | `System.Numerics.Vector<T>` (via NuGet) |
| **Number Formatting** | `ISpanFormattable` / `int.TryFormat(Span<char>)` | Custom zero-alloc `FastNumberFormatter` |
| **Bit Operations** | `System.Numerics.BitOperations` | Software bit-twiddling intrinsics |

### The `ZeroMemory` Core Wrapper
To avoid cluttering codebase with repetitive preprocessor directives, use `ZeroMemory`:

```csharp
internal static unsafe class ZeroMemory
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void* Alloc(nuint byteCount)
    {
#if NET8_0_OR_GREATER
        return NativeMemory.Alloc(byteCount);
#else
        return (void*)Marshal.AllocHGlobal((IntPtr)(long)byteCount);
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Free(void* ptr)
    {
#if NET8_0_OR_GREATER
        NativeMemory.Free(ptr);
#else
        Marshal.FreeHGlobal((IntPtr)ptr);
#endif
    }
}
```

---

## 6. Industrial UI Timer Elimination & Animation Clock Standardization

Scattered timers (`System.Windows.Forms.Timer`, `System.Threading.Timer`, `System.Timers.Timer`) in individual controls cause thread pool thrashing, OS message pump congestion, and out-of-phase visual jitter when dozens of animated components run simultaneously.

### Mandatory Rules:
1. ❌ **Strictly Forbidden:** Instantiating `Timer` inside controls (`ZeroIndustrialPump`, `ZeroIndustrialValve`, `ZeroDrawer`, `ZeroPipeFlow`, `ZeroStatusBadge`, etc.).
2. ✔️ **Centralized Clock Primitive:** All animated or blinking controls must implement `IAnimatable` and register with `ZeroAnimationClock`:
   ```csharp
   // Inside control initialization
   ZeroAnimationClock.Subscribe(this);
   
   // Inside Dispose(bool disposing)
   ZeroAnimationClock.Unsubscribe(this);
   ```
3. ✔️ **Lock-Free Copy-On-Write Dispatch:** `ZeroAnimationClock` distributes ticks via an immutable snapshot array, ensuring that unsubscriptions or subscriptions during an animation tick never lock or allocate.
4. ✔️ **Standardized ISA-18.2 Phase Alignment:** All alarms and status lights must synchronize their blinking frequencies with the global clock phases:
   - `BlinkFast`: 2.0 Hz (50% duty cycle) for unacknowledged alarms.
   - `BlinkSlow`: 1.0 Hz (50% duty cycle) for warning / advisory indicators.
   - `PulsePhase`: 0.0 to 1.0 continuous saw/sine wave for radar/status glows.
   - `FluidPhase`: 0.0 to 1.0 continuous phase for fluid/pipe flow vectors.

---

## 7. High-Frequency Telemetry Ingestion & Triple Buffering

High-speed field buses (Modbus, Siemens S7, EtherNet/IP, OPC UA) produce tag updates at frequencies between 1 kHz and 10 kHz. Directly updating UI controls or calling `Invalidate()` from the ingestion loop causes immediate UI freeze and thread starvation.

### Mandatory Rules:
1. ❌ **Strictly Forbidden:** Calling `Control.Invalidate()` or `Control.Refresh()` from PLC polling callbacks or worker threads.
2. ❌ **Strictly Forbidden:** Direct data binding of high-frequency telemetry channels directly to UI properties.
3. ✔️ **Decoupled 3-Tier Pipeline:** Ingest into Tier 1 (Fast 10kHz), process in Tier 2 (Medium 100–1000Hz), and decouple to Tier 3 (Slow 30–60Hz) using `ZeroTripleBuffer<T>` or `ScadaPipelineCoordinator`.
4. ✔️ **Zero-Lock Pointer Swapping:** Read from the triple buffer's display snapshot on the UI thread without blocking the ingestion or calculation threads:
   ```csharp
   // On UI render pass (30-60 Hz)
   if (_tripleBuffer.TryAcquireLatest(out var snapshot))
   {
       RenderTelemetry(snapshot);
   }
   ```

---

## 8. Single-HWND Scene Graph vs Control Sprawl

Standard industrial mimic screens composed of hundreds of individual `Control` instances (e.g., 50 pumps, 100 valves, 30 tanks) quickly exhaust the Windows OS 10,000 `HWND` ceiling and suffer from severe z-ordering paint artifacts and flicker.

### Mandatory Rules:
1. ❌ **Strictly Forbidden:** Instantiating separate WinForms controls for static or semi-static plant layout elements.
2. ✔️ **Lightweight Scene Graph (`ZeroScene`):** Factory mimic and P&ID screens must be constructed as a single `ZeroPlantMimicCanvas` (Single-HWND) hosting a `ZeroScene` containing `SceneNode` primitives:
   - `TankNode`, `PumpNode`, `PipeNode`, `ValveNode`, `SensorNode`, `AlarmNode`.
3. ✔️ **Spatial Indexing & Viewport Culling:** The scene graph must leverage spatial partitioning (`GridSpatialIndex` / `ISpatialIndex`) to query only the nodes intersecting the current viewport prior to issuing draw commands.


