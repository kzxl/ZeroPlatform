using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using ZeroGraphics.DirectX.Core;
using ZeroGraphics.DirectX.Native;
using ZeroGraphics.Imaging.Core;

namespace ZeroGraphics.Imaging.Gpu
{
    /// <summary>
    /// Represents an independent GPU or software compute node in the heterogeneous cluster.
    /// </summary>
    public sealed class HeterogeneousComputeNode : IDisposable
    {
        public uint AdapterIndex { get; }
        public string Name { get; }
        public double DedicatedMemoryMb { get; }
        public bool IsSoftware { get; }
        public GpuImageContext Context { get; }

        public HeterogeneousComputeNode(uint index, string name, double memoryMb, bool isSoftware, GpuImageContext context)
        {
            AdapterIndex = index;
            Name = name ?? string.Empty;
            DedicatedMemoryMb = memoryMb;
            IsSoftware = isSoftware;
            Context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public void Dispose()
        {
            Context.Dispose();
        }
    }

    /// <summary>
    /// Level 14 Heterogeneous & Multi-GPU Scheduler.
    /// Discovers and coordinates multiple physical GPU adapters (Discrete + Integrated),
    /// WARP software devices, and CPU parallel workers with automatic load balancing and fallback.
    /// </summary>
    public sealed class HeterogeneousScheduler : IDisposable
    {
        private readonly List<HeterogeneousComputeNode> _nodes = new List<HeterogeneousComputeNode>();
        private int _roundRobinCounter = 0;
        private bool _disposed;

        public IReadOnlyList<HeterogeneousComputeNode> Nodes => _nodes;
        public int NodeCount => _nodes.Count;

        public HeterogeneousScheduler()
        {
            DiscoverComputeNodes();
        }

        private void DiscoverComputeNodes()
        {
            try
            {
                Guid factoryIid = DirectXNative.IID_IDXGIFactory1;
                int hr = DirectXNative.CreateDXGIFactory1(ref factoryIid, out IntPtr pFactory);
                if (hr >= 0 && pFactory != IntPtr.Zero)
                {
                    try
                    {
                        uint index = 0;
                        while (true)
                        {
                            hr = ComVTableHelper.EnumAdapters1(pFactory, index, out IntPtr pAdapter);
                            if (hr < 0 || pAdapter == IntPtr.Zero) break;

                            try
                            {
                                hr = ComVTableHelper.GetDesc1(pAdapter, out DXGI_ADAPTER_DESC1 desc);
                                if (hr >= 0)
                                {
                                    string name = desc.GetDescription();
                                    double memMb = (double)desc.DedicatedVideoMemory.ToUInt64() / (1024.0 * 1024.0);
                                    bool isSoftware = (desc.Flags & DXGI_ADAPTER_FLAG.DXGI_ADAPTER_FLAG_SOFTWARE) != 0;

                                    var context = TryCreateContextForAdapter(pAdapter);
                                    if (context != null)
                                    {
                                        _nodes.Add(new HeterogeneousComputeNode(index, name, memMb, isSoftware, context));
                                    }
                                }
                            }
                            finally
                            {
                                ComVTableHelper.Release(pAdapter);
                            }

                            index++;
                        }
                    }
                    finally
                    {
                        ComVTableHelper.Release(pFactory);
                    }
                }
            }
            catch
            {
                // Non-fatal enumeration failure; fallback to default single context
            }

            // Fallback: If no dedicated adapter could be created, use default shared context
            if (_nodes.Count == 0 && D3D11DeviceManager.IsSupported)
            {
                _nodes.Add(new HeterogeneousComputeNode(
                    0,
                    "Primary D3D11 Adapter",
                    1024,
                    false,
                    GpuImageContext.CreateDefault()));
            }
        }

        private static GpuImageContext? TryCreateContextForAdapter(IntPtr pAdapter)
        {
            try
            {
                D3D_FEATURE_LEVEL[] featureLevels = new[]
                {
                    D3D_FEATURE_LEVEL.D3D_FEATURE_LEVEL_11_1,
                    D3D_FEATURE_LEVEL.D3D_FEATURE_LEVEL_11_0,
                    D3D_FEATURE_LEVEL.D3D_FEATURE_LEVEL_10_1,
                    D3D_FEATURE_LEVEL.D3D_FEATURE_LEVEL_10_0
                };

                D3D11_CREATE_DEVICE_FLAG flags = D3D11_CREATE_DEVICE_FLAG.D3D11_CREATE_DEVICE_BGRA_SUPPORT;

                // Note: When pAdapter != Zero, DriverType MUST be D3D_DRIVER_TYPE_UNKNOWN
                int hr = DirectXNative.D3D11CreateDevice(
                    pAdapter,
                    D3D_DRIVER_TYPE.D3D_DRIVER_TYPE_UNKNOWN,
                    IntPtr.Zero,
                    flags,
                    featureLevels,
                    (uint)featureLevels.Length,
                    DirectXNative.D3D11_SDK_VERSION,
                    out IntPtr pDevice,
                    out D3D_FEATURE_LEVEL obtainedLevel,
                    out IntPtr pContext);

                if (hr >= 0 && pDevice != IntPtr.Zero)
                {
                    var device = new D3D11Device(pDevice, obtainedLevel);
                    var context = new D3D11DeviceContext(pContext);
                    return new GpuImageContext(device, context, ownsDevice: true);
                }
            }
            catch
            {
                // Silently fallback if specific adapter does not support DirectCompute
            }

            return null;
        }

        /// <summary>
        /// Dispatches a batch of image buffers across all available heterogeneous nodes concurrently.
        /// </summary>
        public ImageBuffer[] DispatchBatch(
            IReadOnlyList<ImageBuffer> inputs,
            Func<GpuImageContext, ImageBuffer, ImageBuffer> processor)
        {
            if (inputs == null) throw new ArgumentNullException(nameof(inputs));
            if (processor == null) throw new ArgumentNullException(nameof(processor));
            if (_nodes.Count == 0) throw new InvalidOperationException("No heterogeneous compute nodes available.");

            int count = inputs.Count;
            var results = new ImageBuffer[count];

            // Partition work across nodes concurrently
            Parallel.For(0, count, i =>
            {
                int nodeIndex = (Interlocked.Increment(ref _roundRobinCounter) & 0x7FFFFFFF) % _nodes.Count;
                var node = _nodes[nodeIndex];

                lock (node.Context)
                {
                    results[i] = processor(node.Context, inputs[i]);
                }
            });

            return results;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                foreach (var node in _nodes)
                {
                    node.Dispose();
                }
                _nodes.Clear();
                _disposed = true;
            }
        }
    }
}
