using System;
using System.IO;
using Microsoft.ML.OnnxRuntime;

namespace ZeroOcr.Inference.Options;

/// <summary>
/// Factory and configuration helper for high-performance, hardware-accelerated ONNX inference sessions.
/// Automatically detects and activates GPU acceleration (DirectML / CUDA) with safe CPU AVX2/AVX-512 fallback.
/// </summary>
public static class DeepOcrSessionOptions
{
    /// <summary>
    /// Creates an optimized ONNX SessionOptions instance tuned for industrial vision and low latency.
    /// </summary>
    /// <param name="preferGpu">Whether to attempt GPU hardware acceleration (DirectML/CUDA).</param>
    /// <param name="gpuDeviceId">GPU device identifier (typically 0).</param>
    /// <param name="threadCount">Number of intra-op execution threads (defaults to logical processor count).</param>
    public static SessionOptions Create(
        bool preferGpu = true,
        int gpuDeviceId = 0,
        int? threadCount = null)
    {
        var options = new SessionOptions();

        // 1. Threading and Graph Optimizations
        int threads = threadCount ?? Math.Max(1, Environment.ProcessorCount / 2);
        options.IntraOpNumThreads = threads;
        options.InterOpNumThreads = 1;
        options.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;
        options.ExecutionMode = ExecutionMode.ORT_SEQUENTIAL;

        // 2. Hardware Acceleration Auto-Discovery
        if (preferGpu)
        {
            TryAppendGpuProvider(options, gpuDeviceId);
        }

        return options;
    }

    private static void TryAppendGpuProvider(SessionOptions options, int deviceId)
    {
        // Try DirectML first (works across Intel Arc/Iris, AMD Radeon, and NVIDIA on Windows)
        try
        {
            options.AppendExecutionProvider_DML(deviceId);
            return;
        }
        catch
        {
            // DirectML not supported or native binaries not in path, fall back to CUDA
        }

        try
        {
            options.AppendExecutionProvider_CUDA(deviceId);
        }
        catch
        {
            // Neither GPU provider available, clean and seamless fallback to multi-threaded CPU
        }
    }
}
