using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using ZeroGraphics.Imaging.Core;
using ZeroGraphics.Imaging.Gpu;
using ZeroGraphics.Vision.Codes;

namespace ZeroGraphics.Vision.Pipeline
{
    /// <summary>
    /// Configuration for the multi-stage asynchronous inspection pipeline.
    /// </summary>
    public sealed class InspectionPipelineOptions
    {
        public int SlotCount { get; set; } = 16;
        public int WorkerCount { get; set; } = Math.Max(1, Math.Min(4, Environment.ProcessorCount / 2));
        public int MaxFrameWidth { get; set; } = 2592;
        public int MaxFrameHeight { get; set; } = 2048;
        public int MaxBytesPerPixel { get; set; } = 4;
        public ThreadPriority WorkerPriority { get; set; } = ThreadPriority.AboveNormal;

        public static InspectionPipelineOptions Default => new InspectionPipelineOptions();
    }

    /// <summary>
    /// High-performance asynchronous camera inspection pipeline orchestrator.
    /// Decouples Stage 1 (Camera Ingestion), Stage 2 (Parallel Worker Inspection), and Stage 3 (UI Presentation).
    /// </summary>
    public sealed unsafe class InspectionPipelineHost : IDisposable
    {
        private readonly UnmanagedFrameRingBuffer _ringBuffer;
        private readonly Thread[] _workers;
        private readonly Dictionary<int, object?> _slotUserData;
        private volatile bool _running = true;
        private long _frameSequence;
        private bool _disposed;

        /// <summary>
        /// Custom inspection delegate. If not specified, uses the default automated multi-barcode inspection.
        /// </summary>
        public Func<ImageBuffer, FrameInspectionContext, InspectionResultPayload>? Inspector { get; set; }

        /// <summary>
        /// Fired asynchronously whenever a frame inspection is completed.
        /// Ideal for updating ERP QC records (ucMaterialInspection) or signaling industrial PLC hardware.
        /// </summary>
        public event Action<InspectionResultPayload>? OnInspectionCompleted;

        /// <summary>
        /// Fired when an inspected frame and its visual overlays are ready for UI rendering.
        /// (IntPtr pScan0, int width, int height, ImageFormatMode format, int stride, IReadOnlyList<CameraOverlayBox> overlays).
        /// </summary>
        public event Action<IntPtr, int, int, ImageFormatMode, int, IReadOnlyList<CameraOverlayBox>>? OnDisplayFrameReady;

        public UnmanagedFrameRingBuffer RingBuffer => _ringBuffer;
        public int WorkerCount => _workers.Length;
        public bool IsRunning => _running;

        public InspectionPipelineHost(InspectionPipelineOptions? options = null)
        {
            options ??= InspectionPipelineOptions.Default;

            _ringBuffer = new UnmanagedFrameRingBuffer(
                slotCount: options.SlotCount,
                maxFrameWidth: options.MaxFrameWidth,
                maxFrameHeight: options.MaxFrameHeight,
                maxBytesPerPixel: options.MaxBytesPerPixel);

            _slotUserData = new Dictionary<int, object?>(options.SlotCount);
            for (int i = 0; i < options.SlotCount; i++) _slotUserData[i] = null;

            // Initialize worker threads
            _workers = new Thread[options.WorkerCount];
            for (int i = 0; i < options.WorkerCount; i++)
            {
                int workerId = i + 1;
                _workers[i] = new Thread(() => WorkerLoop(workerId))
                {
                    Name = $"ZeroGraphics_VisionWorker_{workerId}",
                    IsBackground = true,
                    Priority = options.WorkerPriority
                };
                _workers[i].Start();
            }
        }

        /// <summary>
        /// Stage 1 Ingestion: Pushes a raw unmanaged camera frame into the lock-free ring buffer.
        /// Sub-millisecond execution (< 0.1ms) called directly from camera SDK callback threads.
        /// </summary>
        public bool PushFrame(
            IntPtr pScan0,
            int width,
            int height,
            ImageFormatMode format,
            int stride = 0,
            object? userData = null)
        {
            if (pScan0 == IntPtr.Zero || width <= 0 || height <= 0 || _disposed) return false;

            if (stride <= 0)
            {
                int bpp = (format == ImageFormatMode.Bgra32) ? 4 : 1;
                stride = ((width * bpp) + 3) & ~3;
            }

            int requiredBytes = stride * height;
            if (requiredBytes > _ringBuffer.SlotCapacityBytes)
                throw new ArgumentException($"Frame size ({requiredBytes} bytes) exceeds pre-allocated slot capacity ({_ringBuffer.SlotCapacityBytes} bytes).");

            if (_ringBuffer.TryAcquireWritingSlot(out int slotIndex))
            {
                long frameId = Interlocked.Increment(ref _frameSequence);
                ref FrameSlot slot = ref _ringBuffer.GetSlot(slotIndex);

                // Zero-allocation unmanaged block copy
                Buffer.MemoryCopy((void*)pScan0, slot.Scan0, _ringBuffer.SlotCapacityBytes, requiredBytes);

                lock (_slotUserData)
                {
                    _slotUserData[slotIndex] = userData;
                }

                _ringBuffer.CommitWritingSlot(slotIndex, frameId, width, height, format, stride);
                return true;
            }

            // Frame dropped due to worker saturation (backpressure protection)
            return false;
        }

        /// <summary>
        /// Stage 1 Ingestion overload for managed ImageBuffer instances.
        /// </summary>
        public bool PushFrame(ImageBuffer image, object? userData = null)
        {
            if (image == null) throw new ArgumentNullException(nameof(image));
            return PushFrame((IntPtr)image.Scan0, image.Width, image.Height, image.Format, image.Stride, userData);
        }

        /// <summary>
        /// Stage 2 Processing: Worker loop running on independent CPU cores.
        /// </summary>
        private void WorkerLoop(int workerId)
        {
            var sw = new Stopwatch();

            while (_running && !_disposed)
            {
                if (_ringBuffer.TryAcquireReadySlot(out int slotIndex))
                {
                    ref FrameSlot slot = ref _ringBuffer.GetSlot(slotIndex);

                    object? uData;
                    lock (_slotUserData)
                    {
                        uData = _slotUserData[slotIndex];
                        _slotUserData[slotIndex] = null;
                    }

                    var ctx = new FrameInspectionContext(
                        frameId: slot.FrameId,
                        timestamp: new DateTime(slot.TimestampTicks, DateTimeKind.Utc),
                        width: slot.Width,
                        height: slot.Height,
                        format: slot.Format,
                        workerThreadId: workerId,
                        userData: uData);

                    sw.Restart();

                    InspectionResultPayload result;
                    using (var image = ImageBuffer.WrapUnmanaged(slot.Scan0, slot.Width, slot.Height, slot.Stride, slot.Format))
                    {
                        if (Inspector != null)
                        {
                            result = Inspector(image, ctx);
                        }
                        else
                        {
                            result = DefaultInspect(image, ctx);
                        }
                    }

                    sw.Stop();

                    // Notify Stage 3 (UI Presentation) with latest frame & overlays
                    OnDisplayFrameReady?.Invoke(
                        (IntPtr)slot.Scan0,
                        slot.Width,
                        slot.Height,
                        slot.Format,
                        slot.Stride,
                        result.Overlays);

                    // Notify Industrial Observers (ERP, PLC, Database)
                    OnInspectionCompleted?.Invoke(result);

                    // Return slot back to Free pool
                    _ringBuffer.ReleaseSlot(slotIndex);
                }
                else
                {
                    // No frames waiting; brief spin/sleep to avoid busy burning CPU
                    Thread.Sleep(1);
                }
            }
        }

        /// <summary>
        /// Default inspection logic: Multi-symbology barcode localization and Pass/Fail bounding box generation.
        /// </summary>
        private static InspectionResultPayload DefaultInspect(ImageBuffer image, FrameInspectionContext ctx)
        {
            var barcode = UniversalBarcodeReader.Decode(image);
            bool isPass = barcode != null;
            string status = isPass
                ? $"Verified {barcode!.Symbology}: {barcode.Text}"
                : "No valid barcode detected.";

            var payload = new InspectionResultPayload(
                ctx.FrameId,
                isPass,
                status,
                durationMs: 0.0,
                barcode: barcode,
                userData: ctx.UserData);

            if (isPass && barcode!.CornerPoints != null && barcode.CornerPoints.Length >= 2)
            {
                float minX = float.MaxValue, minY = float.MaxValue;
                float maxX = float.MinValue, maxY = float.MinValue;

                foreach (var pt in barcode.CornerPoints)
                {
                    if (pt.X < minX) minX = pt.X;
                    if (pt.X > maxX) maxX = pt.X;
                    if (pt.Y < minY) minY = pt.Y;
                    if (pt.Y > maxY) maxY = pt.Y;
                }

                // Add padding
                minX = Math.Max(0, minX - 10);
                minY = Math.Max(0, minY - 10);
                float w = Math.Min(image.Width - minX, (maxX - minX) + 20);
                float h = Math.Min(image.Height - minY, (maxY - minY) + 20);

                payload.Overlays.Add(new CameraOverlayBox(
                    new RectangleF(minX, minY, w, h),
                    Color.FromArgb(0, 255, 128), // Green
                    thickness: 3.0f,
                    label: $"PASS [{barcode.Symbology}: {barcode.Text}]"));
            }
            else
            {
                payload.Overlays.Add(new CameraOverlayBox(
                    new RectangleF(10, 10, image.Width - 20, image.Height - 20),
                    Color.FromArgb(255, 64, 64), // Red
                    thickness: 2.5f,
                    label: "FAIL [NG - Missing/Unreadable Barcode]"));
            }

            return payload;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _running = false;

            foreach (var w in _workers)
            {
                try
                {
                    if (w.IsAlive) w.Join(100);
                }
                catch { }
            }

            _ringBuffer.Dispose();
        }
    }
}
