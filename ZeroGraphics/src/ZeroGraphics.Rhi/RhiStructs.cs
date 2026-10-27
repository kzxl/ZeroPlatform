using System;
using System.Runtime.InteropServices;

namespace ZeroGraphics.Rhi
{
    /// <summary>
    /// Four-component floating point RGBA color.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct RhiColor : IEquatable<RhiColor>
    {
        public readonly float R;
        public readonly float G;
        public readonly float B;
        public readonly float A;

        public RhiColor(float r, float g, float b, float a = 1.0f)
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }

        public static readonly RhiColor Transparent = new RhiColor(0f, 0f, 0f, 0f);
        public static readonly RhiColor Black = new RhiColor(0f, 0f, 0f, 1f);
        public static readonly RhiColor White = new RhiColor(1f, 1f, 1f, 1f);
        public static readonly RhiColor DarkSlate = new RhiColor(0.05f, 0.07f, 0.09f, 1f);
        public static readonly RhiColor Emerald = new RhiColor(0.06f, 0.72f, 0.50f, 1f);
        public static readonly RhiColor Crimson = new RhiColor(0.90f, 0.15f, 0.20f, 1f);

        public bool Equals(RhiColor other) =>
            R.Equals(other.R) && G.Equals(other.G) && B.Equals(other.B) && A.Equals(other.A);

        public override bool Equals(object? obj) => obj is RhiColor other && Equals(other);
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = R.GetHashCode();
                hash = (hash * 397) ^ G.GetHashCode();
                hash = (hash * 397) ^ B.GetHashCode();
                return (hash * 397) ^ A.GetHashCode();
            }
        }
        public static bool operator ==(RhiColor left, RhiColor right) => left.Equals(right);
        public static bool operator !=(RhiColor left, RhiColor right) => !left.Equals(right);

        public override string ToString() => $"RGBA({R:F2}, {G:F2}, {B:F2}, {A:F2})";
    }

    /// <summary>
    /// Viewport definition in screen coordinates.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct RhiViewport : IEquatable<RhiViewport>
    {
        public readonly float X;
        public readonly float Y;
        public readonly float Width;
        public readonly float Height;
        public readonly float MinDepth;
        public readonly float MaxDepth;

        public RhiViewport(float x, float y, float width, float height, float minDepth = 0.0f, float maxDepth = 1.0f)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
            MinDepth = minDepth;
            MaxDepth = maxDepth;
        }

        public bool Equals(RhiViewport other) =>
            X.Equals(other.X) && Y.Equals(other.Y) && Width.Equals(other.Width) && Height.Equals(other.Height) &&
            MinDepth.Equals(other.MinDepth) && MaxDepth.Equals(other.MaxDepth);

        public override bool Equals(object? obj) => obj is RhiViewport other && Equals(other);
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = X.GetHashCode();
                hash = (hash * 397) ^ Y.GetHashCode();
                hash = (hash * 397) ^ Width.GetHashCode();
                hash = (hash * 397) ^ Height.GetHashCode();
                hash = (hash * 397) ^ MinDepth.GetHashCode();
                return (hash * 397) ^ MaxDepth.GetHashCode();
            }
        }
        public static bool operator ==(RhiViewport left, RhiViewport right) => left.Equals(right);
        public static bool operator !=(RhiViewport left, RhiViewport right) => !left.Equals(right);
    }

    /// <summary>
    /// Scissor rectangle definition in integer pixel coordinates.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct RhiRect : IEquatable<RhiRect>
    {
        public readonly int Left;
        public readonly int Top;
        public readonly int Right;
        public readonly int Bottom;

        public int Width => Math.Max(0, Right - Left);
        public int Height => Math.Max(0, Bottom - Top);

        public RhiRect(int left, int top, int right, int bottom)
        {
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
        }

        public bool Equals(RhiRect other) =>
            Left == other.Left && Top == other.Top && Right == other.Right && Bottom == other.Bottom;

        public override bool Equals(object? obj) => obj is RhiRect other && Equals(other);
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Left;
                hash = (hash * 397) ^ Top;
                hash = (hash * 397) ^ Right;
                return (hash * 397) ^ Bottom;
            }
        }
        public static bool operator ==(RhiRect left, RhiRect right) => left.Equals(right);
        public static bool operator !=(RhiRect left, RhiRect right) => !left.Equals(right);
    }

    /// <summary>
    /// Description of an individual vertex input attribute element.
    /// </summary>
    public sealed class RhiVertexElement
    {
        public string SemanticName { get; }
        public int SemanticIndex { get; }
        public RhiFormat Format { get; }
        public int Offset { get; }
        public int Slot { get; }

        public RhiVertexElement(string semanticName, int semanticIndex, RhiFormat format, int offset, int slot = 0)
        {
            SemanticName = semanticName ?? throw new ArgumentNullException(nameof(semanticName));
            SemanticIndex = semanticIndex;
            Format = format;
            Offset = offset;
            Slot = slot;
        }
    }

    /// <summary>
    /// Descriptor for allocating a GPU buffer.
    /// </summary>
    public readonly struct RhiBufferDesc
    {
        public readonly int SizeInBytes;
        public readonly RhiBufferType Type;
        public readonly RhiBufferUsage Usage;

        public RhiBufferDesc(int sizeInBytes, RhiBufferType type, RhiBufferUsage usage = RhiBufferUsage.Default)
        {
            SizeInBytes = sizeInBytes;
            Type = type;
            Usage = usage;
        }
    }

    /// <summary>
    /// Descriptor for allocating a GPU 2D texture.
    /// </summary>
    public readonly struct RhiTextureDesc
    {
        public readonly int Width;
        public readonly int Height;
        public readonly RhiFormat Format;
        public readonly RhiTextureUsage Usage;
        public readonly int MipLevels;

        public RhiTextureDesc(int width, int height, RhiFormat format, RhiTextureUsage usage, int mipLevels = 1)
        {
            Width = width;
            Height = height;
            Format = format;
            Usage = usage;
            MipLevels = Math.Max(1, mipLevels);
        }
    }

    /// <summary>
    /// Descriptor for constructing an immutable Pipeline State Object (PSO).
    /// </summary>
    public sealed class RhiPipelineStateDesc
    {
        public IRhiShader? VertexShader { get; set; }
        public IRhiShader? PixelShader { get; set; }
        public IRhiShader? ComputeShader { get; set; }
        public RhiVertexElement[] InputLayout { get; set; } = Array.Empty<RhiVertexElement>();
        public RhiPrimitiveTopology PrimitiveTopology { get; set; } = RhiPrimitiveTopology.TriangleList;
        public RhiBlendMode BlendMode { get; set; } = RhiBlendMode.Opaque;
        public RhiCullMode CullMode { get; set; } = RhiCullMode.None;
        public RhiFillMode FillMode { get; set; } = RhiFillMode.Solid;
        public bool DepthTestEnabled { get; set; } = false;
        public bool DepthWriteEnabled { get; set; } = false;
    }

    /// <summary>
    /// Explicit resource transition barrier for low-level pipeline synchronization.
    /// Bridges high-level command sequences with modern explicit APIs (D3D12/Vulkan).
    /// </summary>
    public struct RhiBarrier : IEquatable<RhiBarrier>
    {
        public IRhiTexture? Texture { get; }
        public IRhiBuffer? Buffer { get; }
        public RhiResourceState StateBefore { get; }
        public RhiResourceState StateAfter { get; }

        public RhiBarrier(IRhiTexture texture, RhiResourceState stateBefore, RhiResourceState stateAfter)
        {
            Texture = texture ?? throw new ArgumentNullException(nameof(texture));
            Buffer = null;
            StateBefore = stateBefore;
            StateAfter = stateAfter;
        }

        public RhiBarrier(IRhiBuffer buffer, RhiResourceState stateBefore, RhiResourceState stateAfter)
        {
            Buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
            Texture = null;
            StateBefore = stateBefore;
            StateAfter = stateAfter;
        }

        public bool Equals(RhiBarrier other)
        {
            return ReferenceEquals(Texture, other.Texture) &&
                   ReferenceEquals(Buffer, other.Buffer) &&
                   StateBefore == other.StateBefore &&
                   StateAfter == other.StateAfter;
        }

        public override bool Equals(object? obj) => obj is RhiBarrier other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                if (Texture != null) hash = hash * 397 + Texture.GetHashCode();
                if (Buffer != null) hash = hash * 397 + Buffer.GetHashCode();
                hash = hash * 397 + (int)StateBefore;
                hash = hash * 397 + (int)StateAfter;
                return hash;
            }
        }
    }
}
