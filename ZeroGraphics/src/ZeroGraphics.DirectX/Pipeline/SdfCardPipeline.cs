using System;
using System.Drawing;
using System.Runtime.InteropServices;
using ZeroGraphics.DirectX.Core;
using ZeroGraphics.DirectX.Native;
using ZeroGraphics.DirectX.Pipeline.Shaders;

namespace ZeroGraphics.DirectX.Pipeline
{
    [StructLayout(LayoutKind.Sequential)]
    public struct SdfVertex
    {
        public float X, Y;
        public float U, V;

        public SdfVertex(float x, float y, float u, float v)
        {
            X = x; Y = y;
            U = u; V = v;
        }
    }

    [StructLayout(LayoutKind.Sequential, Size = 112)]
    public struct SdfCardConstants
    {
        public float CardX, CardY, CardWidth, CardHeight;
        public float CardR, CardG, CardB, CardA;
        public float BorderR, BorderG, BorderB, BorderA;
        public float ShadowR, ShadowG, ShadowB, ShadowA;
        public float GlowR, GlowG, GlowB, GlowA;
        public float CornerRadius, BorderWidth, BlurRadius, Elevation;
        public float GlowIntensity, ViewportWidth, ViewportHeight, Padding;
    }

    public struct SdfCardData
    {
        public float X, Y, Width, Height;
        public float CornerRadius;
        public float BorderWidth;
        public float BlurRadius;
        public float Elevation;
        public float GlowIntensity;
        public Color CardColor;
        public Color BorderColor;
        public Color ShadowColor;
        public Color GlowColor;

        public SdfCardData(
            float x, float y, float width, float height,
            float cornerRadius = 8f, float borderWidth = 1f, float blurRadius = 12f,
            float elevation = 6f, float glowIntensity = 0f,
            Color? cardColor = null, Color? borderColor = null,
            Color? shadowColor = null, Color? glowColor = null)
        {
            X = x; Y = y; Width = width; Height = height;
            CornerRadius = cornerRadius;
            BorderWidth = borderWidth;
            BlurRadius = blurRadius;
            Elevation = elevation;
            GlowIntensity = glowIntensity;
            CardColor = cardColor ?? Color.FromArgb(22, 27, 38);
            BorderColor = borderColor ?? Color.FromArgb(42, 51, 71);
            ShadowColor = shadowColor ?? Color.FromArgb(120, 0, 0, 0);
            GlowColor = glowColor ?? Color.FromArgb(0, 229, 255);
        }
    }

    /// <summary>
    /// Hardware-accelerated pipeline for rendering analytical Signed Distance Field (SDF) cards,
    /// rounded corners, anti-aliased borders, soft drop shadows, and neon glow effects via Direct3D 11.
    /// </summary>
    public sealed class SdfCardPipeline : IDisposable
    {
        private readonly D3D11Device _device;
        private readonly D3D11DeviceContext _context;

        private D3D11VertexShader? _vertexShader;
        private D3D11PixelShader? _pixelShader;
        private D3D11InputLayout? _inputLayout;
        private D3D11Buffer? _vertexBuffer;
        private D3D11Buffer? _constantBuffer;
        private D3D11BlendState? _blendState;
        private D3D11RasterizerState? _rasterizerState;

        private bool _disposed;

        public SdfCardPipeline(D3D11Device device, D3D11DeviceContext context)
        {
            _device = device ?? throw new ArgumentNullException(nameof(device));
            _context = context ?? throw new ArgumentNullException(nameof(context));

            InitializePipeline();
        }

        private unsafe void InitializePipeline()
        {
            byte[] vsBytecode = ShaderBytecodes.SdfVertexShaderBytecode;
            byte[] psBytecode = ShaderBytecodes.SdfPixelShaderBytecode;

            // 1. Shaders
            _vertexShader = _device.CreateVertexShader(vsBytecode);
            _pixelShader = _device.CreatePixelShader(psBytecode);

            // 2. Input Layout
            IntPtr pPosSemantic = Marshal.StringToHGlobalAnsi("POSITION");
            IntPtr pTexSemantic = Marshal.StringToHGlobalAnsi("TEXCOORD");
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
                    },
                    new D3D11_INPUT_ELEMENT_DESC
                    {
                        SemanticName = pTexSemantic,
                        SemanticIndex = 0,
                        Format = DXGI_FORMAT.DXGI_FORMAT_R32G32_FLOAT,
                        InputSlot = 0,
                        AlignedByteOffset = 8,
                        InputSlotClass = 0,
                        InstanceDataStepRate = 0
                    }
                };

                _inputLayout = _device.CreateInputLayout(layoutDescs, vsBytecode);
            }
            finally
            {
                Marshal.FreeHGlobal(pPosSemantic);
                Marshal.FreeHGlobal(pTexSemantic);
            }

            // 3. Fullscreen Quad Vertex Buffer (2 Triangles in NDC space)
            SdfVertex[] quadVertices = new[]
            {
                new SdfVertex(-1.0f,  1.0f, 0.0f, 0.0f),
                new SdfVertex( 1.0f,  1.0f, 1.0f, 0.0f),
                new SdfVertex(-1.0f, -1.0f, 0.0f, 1.0f),
                new SdfVertex(-1.0f, -1.0f, 0.0f, 1.0f),
                new SdfVertex( 1.0f,  1.0f, 1.0f, 0.0f),
                new SdfVertex( 1.0f, -1.0f, 1.0f, 1.0f)
            };

            fixed (SdfVertex* pVerts = quadVertices)
            {
                D3D11_BUFFER_DESC vbDesc = new D3D11_BUFFER_DESC
                {
                    ByteWidth = (uint)(sizeof(SdfVertex) * quadVertices.Length),
                    Usage = D3D11_USAGE.D3D11_USAGE_IMMUTABLE,
                    BindFlags = D3D11_BIND_FLAG.D3D11_BIND_VERTEX_BUFFER,
                    CPUAccessFlags = 0,
                    MiscFlags = 0,
                    StructureByteStride = (uint)sizeof(SdfVertex)
                };

                D3D11_SUBRESOURCE_DATA initData = new D3D11_SUBRESOURCE_DATA
                {
                    pSysMem = (IntPtr)pVerts,
                    SysMemPitch = 0,
                    SysMemSlicePitch = 0
                };

                _vertexBuffer = _device.CreateBuffer(ref vbDesc, &initData);
            }

            // 4. Constant Buffer (112 bytes)
            D3D11_BUFFER_DESC cbDesc = new D3D11_BUFFER_DESC
            {
                ByteWidth = (uint)Marshal.SizeOf(typeof(SdfCardConstants)),
                Usage = D3D11_USAGE.D3D11_USAGE_DEFAULT,
                BindFlags = D3D11_BIND_FLAG.D3D11_BIND_CONSTANT_BUFFER,
                CPUAccessFlags = 0,
                MiscFlags = 0,
                StructureByteStride = 0
            };
            _constantBuffer = _device.CreateBuffer(ref cbDesc);

            // 5. Alpha Blend State
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

        public unsafe void RenderCard(
            D3D11RenderTargetView rtv,
            int viewportWidth,
            int viewportHeight,
            float cardX,
            float cardY,
            float cardWidth,
            float cardHeight,
            float cornerRadius,
            float borderWidth,
            float blurRadius,
            float elevation,
            float glowIntensity,
            Color cardColor,
            Color borderColor,
            Color shadowColor,
            Color glowColor)
        {
            if (_disposed || rtv == null || !rtv.IsValid || _vertexBuffer == null || _constantBuffer == null)
                return;

            // 1. Prepare Constant Buffer
            SdfCardConstants cb = new SdfCardConstants
            {
                CardX = cardX,
                CardY = cardY,
                CardWidth = cardWidth,
                CardHeight = cardHeight,

                CardR = cardColor.R / 255.0f,
                CardG = cardColor.G / 255.0f,
                CardB = cardColor.B / 255.0f,
                CardA = cardColor.A / 255.0f,

                BorderR = borderColor.R / 255.0f,
                BorderG = borderColor.G / 255.0f,
                BorderB = borderColor.B / 255.0f,
                BorderA = borderColor.A / 255.0f,

                ShadowR = shadowColor.R / 255.0f,
                ShadowG = shadowColor.G / 255.0f,
                ShadowB = shadowColor.B / 255.0f,
                ShadowA = shadowColor.A / 255.0f,

                GlowR = glowColor.R / 255.0f,
                GlowG = glowColor.G / 255.0f,
                GlowB = glowColor.B / 255.0f,
                GlowA = glowColor.A / 255.0f,

                CornerRadius = cornerRadius,
                BorderWidth = borderWidth,
                BlurRadius = blurRadius,
                Elevation = elevation,

                GlowIntensity = glowIntensity,
                ViewportWidth = viewportWidth,
                ViewportHeight = viewportHeight,
                Padding = 0.0f
            };

            _context.UpdateSubresource(_constantBuffer, ref cb);

            // 2. Set Pipeline State
            _context.OMSetRenderTargets(rtv);
            _context.RSSetViewports(new D3D11_VIEWPORT(0, 0, viewportWidth, viewportHeight));
            _context.RSSetState(_rasterizerState);
            _context.OMSetBlendState(_blendState);

            _context.IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY.D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST);
            if (_inputLayout != null) _context.IASetInputLayout(_inputLayout);
            _context.IASetVertexBuffers(0, _vertexBuffer, (uint)sizeof(SdfVertex), 0);

            if (_vertexShader != null) _context.VSSetShader(_vertexShader);
            _context.VSSetConstantBuffers(0, _constantBuffer);

            if (_pixelShader != null) _context.PSSetShader(_pixelShader);
            _context.PSSetConstantBuffers(0, _constantBuffer);

            // 3. Draw Quad
            _context.Draw(6, 0);
        }

        public unsafe void RenderCards(
            D3D11RenderTargetView rtv,
            int viewportWidth,
            int viewportHeight,
            ReadOnlySpan<SdfCardData> cards)
        {
            if (_disposed || rtv == null || !rtv.IsValid || _vertexBuffer == null || _constantBuffer == null || cards.IsEmpty)
                return;

            // 1. Set Pipeline State ONCE for entire batch
            _context.OMSetRenderTargets(rtv);
            _context.RSSetViewports(new D3D11_VIEWPORT(0, 0, viewportWidth, viewportHeight));
            _context.RSSetState(_rasterizerState);
            _context.OMSetBlendState(_blendState);

            _context.IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY.D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST);
            if (_inputLayout != null) _context.IASetInputLayout(_inputLayout);
            _context.IASetVertexBuffers(0, _vertexBuffer, (uint)sizeof(SdfVertex), 0);

            if (_vertexShader != null) _context.VSSetShader(_vertexShader);
            _context.VSSetConstantBuffers(0, _constantBuffer);

            if (_pixelShader != null) _context.PSSetShader(_pixelShader);
            _context.PSSetConstantBuffers(0, _constantBuffer);

            // 2. Stream Constant Buffers in tight loop
            for (int i = 0; i < cards.Length; i++)
            {
                ref readonly SdfCardData c = ref cards[i];

                SdfCardConstants cb = new SdfCardConstants
                {
                    CardX = c.X,
                    CardY = c.Y,
                    CardWidth = c.Width,
                    CardHeight = c.Height,

                    CardR = c.CardColor.R / 255.0f,
                    CardG = c.CardColor.G / 255.0f,
                    CardB = c.CardColor.B / 255.0f,
                    CardA = c.CardColor.A / 255.0f,

                    BorderR = c.BorderColor.R / 255.0f,
                    BorderG = c.BorderColor.G / 255.0f,
                    BorderB = c.BorderColor.B / 255.0f,
                    BorderA = c.BorderColor.A / 255.0f,

                    ShadowR = c.ShadowColor.R / 255.0f,
                    ShadowG = c.ShadowColor.G / 255.0f,
                    ShadowB = c.ShadowColor.B / 255.0f,
                    ShadowA = c.ShadowColor.A / 255.0f,

                    GlowR = c.GlowColor.R / 255.0f,
                    GlowG = c.GlowColor.G / 255.0f,
                    GlowB = c.GlowColor.B / 255.0f,
                    GlowA = c.GlowColor.A / 255.0f,

                    CornerRadius = c.CornerRadius,
                    BorderWidth = c.BorderWidth,
                    BlurRadius = c.BlurRadius,
                    Elevation = c.Elevation,

                    GlowIntensity = c.GlowIntensity,
                    ViewportWidth = viewportWidth,
                    ViewportHeight = viewportHeight,
                    Padding = 0.0f
                };

                _context.UpdateSubresource(_constantBuffer, ref cb);
                _context.Draw(6, 0);
            }
        }

        public void RenderCards(
            D3D11RenderTargetView rtv,
            int viewportWidth,
            int viewportHeight,
            SdfCardData[] cards)
        {
            if (cards == null) return;
            RenderCards(rtv, viewportWidth, viewportHeight, new ReadOnlySpan<SdfCardData>(cards));
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
