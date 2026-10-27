using System;
using System.Buffers;
using System.Drawing;
using System.Runtime.InteropServices;
using ZeroGraphics.Core.Data;
using ZeroGraphics.DirectX.Core;
using ZeroGraphics.DirectX.Native;
using ZeroGraphics.Waveform.Pipeline.Shaders;

namespace ZeroGraphics.Waveform.Pipeline
{
    [StructLayout(LayoutKind.Sequential)]
    public struct WaveformVertex
    {
        public float X;
        public float Y;

        public WaveformVertex(float x, float y)
        {
            X = x;
            Y = y;
        }
    }

    [StructLayout(LayoutKind.Sequential, Size = 64)]
    public struct WaveformConstants
    {
        public float ScaleX, ScaleY, OffsetX, OffsetY;
        public float TraceR, TraceG, TraceB, TraceA;
        public float GridR, GridG, GridB, GridA;
        public float ViewportWidth, ViewportHeight, PointCount, Padding;
    }

    /// <summary>
    /// High-performance GPU pipeline for real-time oscilloscope and telemetry line-strip rendering via Direct3D 11.
    /// Integrates zero-allocation streaming and automated LTTB decimation for multi-million point series.
    /// </summary>
    public sealed class WaveformPipeline : IDisposable
    {
        private readonly D3D11Device _device;
        private readonly D3D11DeviceContext _context;

        private D3D11VertexShader? _vertexShader;
        private D3D11PixelShader? _pixelShader;
        private D3D11InputLayout? _inputLayout;
        private D3D11Buffer? _vertexBuffer;
        private D3D11Buffer? _constantBuffer;
        private D3D11RasterizerState? _rasterizerState;
        private D3D11BlendState? _blendState;

        private int _vertexCapacity;
        private bool _disposed;

        public WaveformPipeline(D3D11Device device, D3D11DeviceContext context)
        {
            _device = device ?? throw new ArgumentNullException(nameof(device));
            _context = context ?? throw new ArgumentNullException(nameof(context));

            InitializePipeline();
        }

        private unsafe void InitializePipeline()
        {
            byte[] vsBytecode = WaveformBytecodes.WaveformVertexShaderBytecode;
            byte[] psBytecode = WaveformBytecodes.WaveformPixelShaderBytecode;

            // 1. Shaders
            _vertexShader = _device.CreateVertexShader(vsBytecode);
            _pixelShader = _device.CreatePixelShader(psBytecode);

            // 2. Input Layout (float2 POSITION)
            IntPtr pPosSemantic = Marshal.StringToHGlobalAnsi("POSITION");
            try
            {
                D3D11_INPUT_ELEMENT_DESC[] layoutDescs = new[]
                {
                    new D3D11_INPUT_ELEMENT_DESC
                    {
                        SemanticName = pPosSemantic,
                        SemanticIndex = 0,
                        Format = DXGI_FORMAT.DXGI_FORMAT_R32G32_FLOAT,
                        InputSlot = 0,
                        AlignedByteOffset = 0,
                        InputSlotClass = 0,
                        InstanceDataStepRate = 0
                    }
                };

                _inputLayout = _device.CreateInputLayout(layoutDescs, vsBytecode);
            }
            finally
            {
                Marshal.FreeHGlobal(pPosSemantic);
            }

            // 3. Initial Dynamic Vertex Buffer (16,384 vertices initial capacity)
            EnsureVertexCapacity(16384);

            // 4. Constant Buffer (64 bytes)
            D3D11_BUFFER_DESC cbDesc = new D3D11_BUFFER_DESC
            {
                ByteWidth = (uint)Marshal.SizeOf(typeof(WaveformConstants)),
                Usage = D3D11_USAGE.D3D11_USAGE_DEFAULT,
                BindFlags = D3D11_BIND_FLAG.D3D11_BIND_CONSTANT_BUFFER,
                CPUAccessFlags = 0,
                MiscFlags = 0,
                StructureByteStride = 0
            };
            _constantBuffer = _device.CreateBuffer(ref cbDesc);

            // 5. Blend State (Alpha Blending for soft glow)
            D3D11_BLEND_DESC blendDesc = new D3D11_BLEND_DESC();
            blendDesc.RenderTarget0.BlendEnable = 1;
            blendDesc.RenderTarget0.SrcBlend = D3D11_BLEND.D3D11_BLEND_SRC_ALPHA;
            blendDesc.RenderTarget0.DestBlend = D3D11_BLEND.D3D11_BLEND_INV_SRC_ALPHA;
            blendDesc.RenderTarget0.BlendOp = D3D11_BLEND_OP.D3D11_BLEND_OP_ADD;
            blendDesc.RenderTarget0.SrcBlendAlpha = D3D11_BLEND.D3D11_BLEND_ONE;
            blendDesc.RenderTarget0.DestBlendAlpha = D3D11_BLEND.D3D11_BLEND_INV_SRC_ALPHA;
            blendDesc.RenderTarget0.BlendOpAlpha = D3D11_BLEND_OP.D3D11_BLEND_OP_ADD;
            blendDesc.RenderTarget0.RenderTargetWriteMask = (byte)D3D11_COLOR_WRITE_ENABLE.D3D11_COLOR_WRITE_ENABLE_ALL;

            _blendState = _device.CreateBlendState(ref blendDesc);

            // 6. Rasterizer State (No culling)
            D3D11_RASTERIZER_DESC rastDesc = new D3D11_RASTERIZER_DESC
            {
                FillMode = D3D11_FILL_MODE.D3D11_FILL_SOLID,
                CullMode = D3D11_CULL_MODE.D3D11_CULL_NONE,
                DepthClipEnable = 0
            };
            _rasterizerState = _device.CreateRasterizerState(ref rastDesc);
        }

        private unsafe void EnsureVertexCapacity(int requiredCount)
        {
            if (_vertexBuffer != null && _vertexCapacity >= requiredCount) return;

            _vertexBuffer?.Dispose();
            _vertexCapacity = Math.Max(requiredCount, Math.Max(_vertexCapacity * 2, 16384));

            D3D11_BUFFER_DESC vbDesc = new D3D11_BUFFER_DESC
            {
                ByteWidth = (uint)(sizeof(WaveformVertex) * _vertexCapacity),
                Usage = D3D11_USAGE.D3D11_USAGE_DYNAMIC,
                BindFlags = D3D11_BIND_FLAG.D3D11_BIND_VERTEX_BUFFER,
                CPUAccessFlags = D3D11_CPU_ACCESS_FLAG.D3D11_CPU_ACCESS_WRITE,
                MiscFlags = 0,
                StructureByteStride = (uint)sizeof(WaveformVertex)
            };

            _vertexBuffer = _device.CreateBuffer(ref vbDesc);
        }

        public unsafe void RenderWaveform(
            D3D11RenderTargetView rtv,
            int viewportWidth,
            int viewportHeight,
            ReadOnlySpan<float> values,
            float minY,
            float maxY,
            Color traceColor,
            WaveformDecimationMode decimationMode = WaveformDecimationMode.MinMax)
        {
            if (_disposed || rtv == null || !rtv.IsValid || values.Length < 2) return;

            int count = values.Length;

            // 1. Check if decimation is needed for massive multi-million point series
            int maxPixels = Math.Max(viewportWidth, 200);
            int targetPoints = Math.Min(count, maxPixels * 4);

            WaveformVertex[] rented = ArrayPool<WaveformVertex>.Shared.Rent(targetPoints);
            try
            {
                float rangeY = (maxY - minY);
                if (Math.Abs(rangeY) < 1e-6f) rangeY = 1.0f;
                float invRangeY = 1.0f / rangeY;

                if (count == targetPoints || decimationMode == WaveformDecimationMode.None)
                {
                    if (count == targetPoints)
                    {
                        // Direct 1-to-1 mapping
                        float invCount = 1.0f / (count - 1);
                        for (int i = 0; i < count; i++)
                        {
                            float normX = i * invCount;
                            float normY = (values[i] - minY) * invRangeY;
                            rented[i] = new WaveformVertex(normX, normY);
                        }
                    }
                    else
                    {
                        // Stride-based sampling
                        float step = (float)(count - 1) / (targetPoints - 1);
                        float invTarget = 1.0f / (targetPoints - 1);
                        for (int i = 0; i < targetPoints; i++)
                        {
                            int srcIndex = (int)(i * step);
                            if (srcIndex >= count) srcIndex = count - 1;

                            float normX = i * invTarget;
                            float normY = (values[srcIndex] - minY) * invRangeY;
                            rented[i] = new WaveformVertex(normX, normY);
                        }
                    }
                }
                else
                {
                    // Algorithmic Peak-Preserving or LTTB Decimation
                    TimePoint[] rentedSrc = ArrayPool<TimePoint>.Shared.Rent(count);
                    TimePoint[] rentedDst = ArrayPool<TimePoint>.Shared.Rent(targetPoints);
                    try
                    {
                        for (int i = 0; i < count; i++)
                        {
                            rentedSrc[i] = new TimePoint(i, values[i]);
                        }

                        int actualPoints = targetPoints;
                        if (decimationMode == WaveformDecimationMode.MinMax)
                        {
                            actualPoints = MinMaxDecimation.Downsample(rentedSrc.AsSpan(0, count), rentedDst.AsSpan(0, targetPoints), targetPoints);
                        }
                        else if (decimationMode == WaveformDecimationMode.Lttb)
                        {
                            actualPoints = LttbDecimation.Downsample(rentedSrc.AsSpan(0, count), rentedDst.AsSpan(0, targetPoints), targetPoints);
                        }

                        float invCount = 1.0f / (count - 1);
                        for (int i = 0; i < actualPoints; i++)
                        {
                            float normX = (float)(rentedDst[i].X * invCount);
                            float normY = (float)((rentedDst[i].Y - minY) * invRangeY);
                            rented[i] = new WaveformVertex(normX, normY);
                        }
                        targetPoints = actualPoints;
                    }
                    finally
                    {
                        ArrayPool<TimePoint>.Shared.Return(rentedSrc);
                        ArrayPool<TimePoint>.Shared.Return(rentedDst);
                    }
                }

                // 2. Upload vertices to GPU using zero-lock Map(D3D11_MAP_WRITE_DISCARD)
                EnsureVertexCapacity(targetPoints);

                int hrMap = _context.Map(_vertexBuffer!, 0, D3D11_MAP.D3D11_MAP_WRITE_DISCARD, 0, out D3D11_MAPPED_SUBRESOURCE mapped);
                if (hrMap >= 0 && mapped.pData != IntPtr.Zero)
                {
                    fixed (WaveformVertex* pSrc = rented)
                    {
                        Buffer.MemoryCopy(pSrc, (void*)mapped.pData, sizeof(WaveformVertex) * _vertexCapacity, sizeof(WaveformVertex) * targetPoints);
                    }
                    _context.Unmap(_vertexBuffer!, 0);
                }

                // 3. Update Constant Buffer
                WaveformConstants cb = new WaveformConstants
                {
                    // Maps [0, 1]x[0, 1] to NDC [-1, 1]x[-1, 1] with slight margin
                    ScaleX = 1.96f,
                    ScaleY = 1.92f,
                    OffsetX = -0.98f,
                    OffsetY = -0.96f,

                    TraceR = traceColor.R / 255f,
                    TraceG = traceColor.G / 255f,
                    TraceB = traceColor.B / 255f,
                    TraceA = traceColor.A / 255f,

                    GridR = 0.2f, GridG = 0.25f, GridB = 0.35f, GridA = 0.5f,
                    ViewportWidth = viewportWidth,
                    ViewportHeight = viewportHeight,
                    PointCount = targetPoints,
                    Padding = 0.0f
                };

                _context.UpdateSubresource(_constantBuffer!, ref cb);

                // 4. Set Pipeline State & Draw LineStrip
                _context.OMSetRenderTargets(rtv);
                _context.RSSetViewports(new D3D11_VIEWPORT(0, 0, viewportWidth, viewportHeight));
                _context.RSSetState(_rasterizerState);
                _context.OMSetBlendState(_blendState);

                _context.IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY.D3D11_PRIMITIVE_TOPOLOGY_LINESTRIP);
                if (_inputLayout != null) _context.IASetInputLayout(_inputLayout);
                _context.IASetVertexBuffers(0, _vertexBuffer!, (uint)sizeof(WaveformVertex), 0);

                if (_vertexShader != null) _context.VSSetShader(_vertexShader);
                _context.VSSetConstantBuffers(0, _constantBuffer!);

                if (_pixelShader != null) _context.PSSetShader(_pixelShader);
                _context.PSSetConstantBuffers(0, _constantBuffer!);

                _context.Draw((uint)targetPoints, 0);
            }
            finally
            {
                ArrayPool<WaveformVertex>.Shared.Return(rented);
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _rasterizerState?.Dispose();
                _blendState?.Dispose();
                _constantBuffer?.Dispose();
                _vertexBuffer?.Dispose();
                _inputLayout?.Dispose();
                _pixelShader?.Dispose();
                _vertexShader?.Dispose();
                _disposed = true;
            }
        }
    }
}
