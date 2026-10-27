using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

namespace ZeroGraphics.Rhi.Null
{
    /// <summary>
    /// Headless software null implementation of IRhiDevice.
    /// Operates in system RAM without GPU hardware, ideal for CI/CD runners and unit tests.
    /// </summary>
    public sealed class NullRhiDevice : IRhiDevice
    {
        public RhiBackend Backend => RhiBackend.Null;
        public string DeviceName => "ZeroGraphics Null Reference Rasterizer (Headless)";
        public bool IsDisposed => _isDisposed;

        private bool _isDisposed;

        public IRhiBuffer CreateBuffer(in RhiBufferDesc desc, ReadOnlySpan<byte> initialData = default)
        {
            var buffer = new NullRhiBuffer(desc);
            if (!initialData.IsEmpty)
            {
                buffer.UpdateRawBytes(initialData, 0);
            }
            return buffer;
        }

        public IRhiTexture CreateTexture(in RhiTextureDesc desc, ReadOnlySpan<byte> initialData = default)
        {
            var texture = new NullRhiTexture(desc);
            if (!initialData.IsEmpty)
            {
                int pitch = desc.Width * GetBytesPerPixel(desc.Format);
                texture.UpdateRawBytes(initialData, pitch);
            }
            return texture;
        }

        public IRhiShader CreateShader(RhiShaderStage stage, byte[] bytecode)
        {
            return new NullRhiShader(stage, bytecode);
        }

        public IRhiPipelineState CreatePipelineState(RhiPipelineStateDesc desc)
        {
            return new NullRhiPipelineState(desc);
        }

        public IRhiSwapChain CreateSwapChain(IntPtr windowHandle, int width, int height, RhiPresentMode presentMode = RhiPresentMode.Fifo)
        {
            return new NullRhiSwapChain(this, width, height);
        }

        public IRhiCommandBuffer CreateCommandBuffer()
        {
            return new NullRhiCommandBuffer();
        }

        public IRhiFence CreateFence(ulong initialValue = 0)
        {
            return new NullRhiFence(initialValue);
        }

        public void Dispose()
        {
            _isDisposed = true;
        }

        internal static int GetBytesPerPixel(RhiFormat format)
        {
            switch (format)
            {
                case RhiFormat.R8_UNorm: return 1;
                case RhiFormat.R16_Float: return 2;
                case RhiFormat.R8G8B8A8_UNorm:
                case RhiFormat.B8G8R8A8_UNorm:
                case RhiFormat.D24_UNorm_S8_UInt:
                case RhiFormat.R32_Float:
                case RhiFormat.D32_Float: return 4;
                case RhiFormat.R16G16B16A16_Float:
                case RhiFormat.R32G32_Float: return 8;
                case RhiFormat.R32G32B32_Float: return 12;
                case RhiFormat.R32G32B32A32_Float: return 16;
                default: return 4;
            }
        }
    }

    internal sealed class NullRhiBuffer : IRhiBuffer
    {
        public RhiBufferType Type { get; }
        public RhiBufferUsage Usage { get; }
        public int SizeInBytes { get; }
        public byte[] Storage { get; }

        public NullRhiBuffer(in RhiBufferDesc desc)
        {
            Type = desc.Type;
            Usage = desc.Usage;
            SizeInBytes = desc.SizeInBytes;
            Storage = new byte[desc.SizeInBytes];
        }

        public unsafe void UpdateData<T>(ReadOnlySpan<T> data, int offsetBytes = 0) where T : unmanaged
        {
            int byteLength = data.Length * sizeof(T);
            if (offsetBytes + byteLength > SizeInBytes)
                throw new ArgumentOutOfRangeException(nameof(data), "Data exceeds buffer size.");

            fixed (T* srcPtr = data)
            fixed (byte* dstPtr = &Storage[offsetBytes])
            {
                Buffer.MemoryCopy(srcPtr, dstPtr, SizeInBytes - offsetBytes, byteLength);
            }
        }

        public void UpdateRawBytes(ReadOnlySpan<byte> bytes, int offsetBytes)
        {
            bytes.CopyTo(Storage.AsSpan(offsetBytes));
        }

        public void Dispose() { }
    }

    internal sealed class NullRhiTexture : IRhiTexture
    {
        public int Width { get; }
        public int Height { get; }
        public RhiFormat Format { get; }
        public RhiTextureUsage Usage { get; }
        public int MipLevels { get; }
        public byte[] PixelData { get; }

        public NullRhiTexture(in RhiTextureDesc desc)
        {
            Width = desc.Width;
            Height = desc.Height;
            Format = desc.Format;
            Usage = desc.Usage;
            MipLevels = desc.MipLevels;
            int bpp = NullRhiDevice.GetBytesPerPixel(Format);
            PixelData = new byte[Width * Height * bpp];
        }

        public unsafe void UpdateData<T>(ReadOnlySpan<T> data, int rowPitch) where T : unmanaged
        {
            int byteLength = data.Length * sizeof(T);
            int copyBytes = Math.Min(byteLength, PixelData.Length);
            fixed (T* srcPtr = data)
            fixed (byte* dstPtr = PixelData)
            {
                Buffer.MemoryCopy(srcPtr, dstPtr, PixelData.Length, copyBytes);
            }
        }

        public void UpdateRawBytes(ReadOnlySpan<byte> data, int rowPitch)
        {
            int copyBytes = Math.Min(data.Length, PixelData.Length);
            data.Slice(0, copyBytes).CopyTo(PixelData.AsSpan());
        }

        public void Dispose() { }
    }

    internal sealed class NullRhiShader : IRhiShader
    {
        public RhiShaderStage Stage { get; }
        public byte[] Bytecode { get; }

        public NullRhiShader(RhiShaderStage stage, byte[] bytecode)
        {
            Stage = stage;
            Bytecode = bytecode ?? Array.Empty<byte>();
        }

        public void Dispose() { }
    }

    internal sealed class NullRhiPipelineState : IRhiPipelineState
    {
        public RhiPipelineStateDesc Description { get; }

        public NullRhiPipelineState(RhiPipelineStateDesc desc)
        {
            Description = desc;
        }

        public void Dispose() { }
    }

    internal sealed class NullRhiSwapChain : IRhiSwapChain
    {
        private readonly NullRhiDevice _device;
        public int Width { get; private set; }
        public int Height { get; private set; }
        public IRhiTexture BackBuffer { get; private set; }

        public NullRhiSwapChain(NullRhiDevice device, int width, int height)
        {
            _device = device;
            Width = Math.Max(1, width);
            Height = Math.Max(1, height);
            BackBuffer = _device.CreateTexture(new RhiTextureDesc(Width, Height, RhiFormat.B8G8R8A8_UNorm, RhiTextureUsage.RenderTarget));
        }

        public void Resize(int width, int height)
        {
            Width = Math.Max(1, width);
            Height = Math.Max(1, height);
            BackBuffer.Dispose();
            BackBuffer = _device.CreateTexture(new RhiTextureDesc(Width, Height, RhiFormat.B8G8R8A8_UNorm, RhiTextureUsage.RenderTarget));
        }

        public void Present(int syncInterval = 1)
        {
            // Null present operation
        }

        public void Dispose()
        {
            BackBuffer.Dispose();
        }
    }

    internal sealed class NullRhiCommandBuffer : IRhiCommandBuffer
    {
        public List<string> RecordedCommands { get; } = new List<string>();
        public int TotalDrawCalls { get; private set; }
        public int TotalDispatches { get; private set; }

        public void Begin()
        {
            RecordedCommands.Clear();
            RecordedCommands.Add("Begin");
        }

        public void End()
        {
            RecordedCommands.Add("End");
        }

        public void SetViewport(in RhiViewport viewport)
        {
            RecordedCommands.Add($"SetViewport({viewport.Width}x{viewport.Height})");
        }

        public void SetScissorRect(in RhiRect scissorRect)
        {
            RecordedCommands.Add($"SetScissorRect({scissorRect.Width}x{scissorRect.Height})");
        }

        public void BeginRenderPass(IRhiTexture renderTarget, RhiClearFlags clearFlags = RhiClearFlags.None, RhiColor clearColor = default)
        {
            RecordedCommands.Add($"BeginRenderPass({renderTarget.Width}x{renderTarget.Height}, {clearFlags})");
        }

        public void EndRenderPass()
        {
            RecordedCommands.Add("EndRenderPass");
        }

        public void SetPipelineState(IRhiPipelineState pipelineState)
        {
            RecordedCommands.Add("SetPipelineState");
        }

        public void SetVertexBuffer(int slot, IRhiBuffer buffer, int stride, int offset = 0)
        {
            RecordedCommands.Add($"SetVertexBuffer(Slot:{slot}, Stride:{stride})");
        }

        public void SetIndexBuffer(IRhiBuffer buffer, RhiIndexFormat format, int offset = 0)
        {
            RecordedCommands.Add($"SetIndexBuffer(Format:{format})");
        }

        public void SetConstantBuffer(int slot, IRhiBuffer buffer, RhiShaderStage stage = RhiShaderStage.Vertex | RhiShaderStage.Pixel)
        {
            RecordedCommands.Add($"SetConstantBuffer(Slot:{slot}, Stage:{stage})");
        }

        public void SetShaderResource(int slot, IRhiTexture texture, RhiShaderStage stage = RhiShaderStage.Pixel)
        {
            RecordedCommands.Add($"SetShaderResource(Slot:{slot}, Stage:{stage})");
        }

        public void Draw(int vertexCount, int startVertex = 0)
        {
            TotalDrawCalls++;
            RecordedCommands.Add($"Draw({vertexCount}, {startVertex})");
        }

        public void DrawIndexed(int indexCount, int startIndex = 0, int baseVertex = 0)
        {
            TotalDrawCalls++;
            RecordedCommands.Add($"DrawIndexed({indexCount})");
        }

        public void DispatchCompute(int groupCountX, int groupCountY, int groupCountZ)
        {
            TotalDispatches++;
            RecordedCommands.Add($"DispatchCompute({groupCountX}, {groupCountY}, {groupCountZ})");
        }

        public void ResourceBarrier(in RhiBarrier barrier)
        {
            string target = barrier.Texture != null ? $"Texture({barrier.Texture.Width}x{barrier.Texture.Height})" : "Buffer";
            RecordedCommands.Add($"Barrier({target}, {barrier.StateBefore} -> {barrier.StateAfter})");
        }

        public void ResourceBarriers(ReadOnlySpan<RhiBarrier> barriers)
        {
            for (int i = 0; i < barriers.Length; i++)
            {
                ResourceBarrier(in barriers[i]);
            }
        }

        public void SignalFence(IRhiFence fence, ulong value)
        {
            if (fence == null) throw new ArgumentNullException(nameof(fence));
            RecordedCommands.Add($"SignalFence({fence.GetType().Name}, {value})");
            fence.Signal(value);
        }

        public void WaitFence(IRhiFence fence, ulong value)
        {
            if (fence == null) throw new ArgumentNullException(nameof(fence));
            RecordedCommands.Add($"WaitFence({fence.GetType().Name}, {value})");
        }

        public void Dispose() { }
    }

    /// <summary>
    /// Software-backed timeline fence for Null reference device and testing.
    /// </summary>
    public sealed class NullRhiFence : IRhiFence
    {
        private long _currentValue;
        private readonly ManualResetEventSlim _event = new(false);
        private bool _disposed;

        public NullRhiFence(ulong initialValue)
        {
            _currentValue = (long)initialValue;
            if (initialValue > 0) _event.Set();
        }

        public ulong CompletedValue => (ulong)Interlocked.Read(ref _currentValue);

        public void Signal(ulong value)
        {
            Interlocked.Exchange(ref _currentValue, (long)value);
            _event.Set();
        }

        public bool Wait(ulong value, int timeoutMilliseconds = -1)
        {
            while (CompletedValue < value)
            {
                _event.Reset();
                if (CompletedValue >= value) return true;
                if (!_event.Wait(timeoutMilliseconds))
                {
                    return CompletedValue >= value;
                }
            }
            return true;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _event.Dispose();
            }
        }
    }
}
