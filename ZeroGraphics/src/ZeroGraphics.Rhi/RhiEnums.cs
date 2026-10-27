using System;

namespace ZeroGraphics.Rhi
{
    /// <summary>
    /// Underlying hardware graphics backend API.
    /// </summary>
    public enum RhiBackend
    {
        Null = 0,
        Direct3D11 = 1,
        Direct3D12 = 2,
        Vulkan = 3,
        Metal = 4,
        OpenGL = 5
    }

    /// <summary>
    /// Data and texture pixel/vertex element formats.
    /// </summary>
    public enum RhiFormat
    {
        Unknown = 0,
        R8_UNorm = 1,
        R8G8B8A8_UNorm = 2,
        B8G8R8A8_UNorm = 3,
        R16_Float = 4,
        R16G16B16A16_Float = 5,
        R32_Float = 6,
        R32G32_Float = 7,
        R32G32B32_Float = 8,
        R32G32B32A32_Float = 9,
        D24_UNorm_S8_UInt = 10,
        D32_Float = 11
    }

    /// <summary>
    /// Functional type of a GPU memory buffer.
    /// </summary>
    public enum RhiBufferType
    {
        Vertex = 0,
        Index = 1,
        Constant = 2,
        Staging = 3,
        Structured = 4
    }

    /// <summary>
    /// Expected memory mutability and frequency of update for buffers.
    /// </summary>
    public enum RhiBufferUsage
    {
        Default = 0,
        Dynamic = 1,
        Immutable = 2,
        Staging = 3
    }

    /// <summary>
    /// Usage capabilities and binding targets for a GPU texture.
    /// </summary>
    [Flags]
    public enum RhiTextureUsage
    {
        None = 0,
        ShaderResource = 1,
        RenderTarget = 2,
        DepthStencil = 4,
        ComputeStorage = 8
    }

    /// <summary>
    /// Geometric primitive assembly topology.
    /// </summary>
    public enum RhiPrimitiveTopology
    {
        TriangleList = 0,
        TriangleStrip = 1,
        LineList = 2,
        LineStrip = 3,
        PointList = 4
    }

    /// <summary>
    /// Format of index buffer indices.
    /// </summary>
    public enum RhiIndexFormat
    {
        SixteenBit = 0,
        ThirtyTwoBit = 1
    }

    /// <summary>
    /// Programmable shader execution stage.
    /// </summary>
    [Flags]
    public enum RhiShaderStage
    {
        Vertex = 1,
        Pixel = 2,
        Compute = 4
    }

    /// <summary>
    /// Color blend mode.
    /// </summary>
    public enum RhiBlendMode
    {
        Opaque = 0,
        AlphaBlend = 1,
        Additive = 2,
        NonPremultiplied = 3
    }

    /// <summary>
    /// Triangle face culling mode.
    /// </summary>
    public enum RhiCullMode
    {
        None = 0,
        Front = 1,
        Back = 2
    }

    /// <summary>
    /// Polygon fill mode.
    /// </summary>
    public enum RhiFillMode
    {
        Solid = 0,
        Wireframe = 1
    }

    /// <summary>
    /// Flags indicating which render target buffers to clear.
    /// </summary>
    [Flags]
    public enum RhiClearFlags
    {
        None = 0,
        Color = 1,
        Depth = 2,
        Stencil = 4,
        All = Color | Depth | Stencil
    }

    /// <summary>
    /// Frame presentation mode and vertical synchronization behaviour.
    /// </summary>
    public enum RhiPresentMode
    {
        Immediate = 0, // V-Sync Off (Uncapped)
        Fifo = 1,      // V-Sync On (Tear-Free)
        Mailbox = 2    // Triple-Buffering Low Latency
    }

    /// <summary>
    /// Explicit resource access states for D3D12/Vulkan synchronization and transition barriers.
    /// </summary>
    [Flags]
    public enum RhiResourceState
    {
        Common = 0,
        VertexBuffer = 1 << 0,
        IndexBuffer = 1 << 1,
        ConstantBuffer = 1 << 2,
        RenderTarget = 1 << 3,
        DepthWrite = 1 << 4,
        DepthRead = 1 << 5,
        ShaderResource = 1 << 6,
        ComputeStorage = 1 << 7,
        CopySource = 1 << 8,
        CopyDest = 1 << 9,
        Present = 1 << 10
    }
}
