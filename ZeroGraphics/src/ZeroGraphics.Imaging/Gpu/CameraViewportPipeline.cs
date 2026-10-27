using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using ZeroGraphics.DirectX.Core;
using ZeroGraphics.DirectX.Native;
using ZeroGraphics.Imaging.Core;
using ZeroGraphics.Imaging.Gpu.Shaders;

namespace ZeroGraphics.Imaging.Gpu
{
    [StructLayout(LayoutKind.Sequential)]
    public struct CameraVertex
    {
        public float X, Y;
        public float U, V;

        public CameraVertex(float x, float y, float u, float v)
        {
            X = x;
            Y = y;
            U = u;
            V = v;
        }
    }

    /// <summary>
    /// Represents an inspection or identification bounding box overlay.
    /// Coordinates are in camera native sensor pixels.
    /// </summary>
    public struct CameraOverlayBox
    {
        public RectangleF Rect { get; set; }
        public Color Color { get; set; }
        public float Thickness { get; set; }
        public string? Label { get; set; }

        public CameraOverlayBox(RectangleF rect, Color color, float thickness = 2.0f, string? label = null)
        {
            Rect = rect;
            Color = color;
            Thickness = Math.Max(1.0f, thickness);
            Label = label;
        }

        public CameraOverlayBox(float x, float y, float width, float height, Color color, float thickness = 2.0f, string? label = null)
            : this(new RectangleF(x, y, width, height), color, thickness, label)
        {
        }
    }

    /// <summary>
    /// Ultra-high-speed Direct3D 11 camera viewport rendering pipeline.
    /// Features hardware R8_UNORM (Grayscale) and BGRA32 direct sampling, zero-allocation uploads,
    /// smooth zoom/pan transformations, and real-time inspection bounding box overlays.
    /// </summary>
    public sealed class CameraViewportPipeline : IDisposable
    {
        private readonly D3D11Device _device;
        private readonly D3D11DeviceContext _context;

        private D3D11VertexShader? _vertexShader;
        private D3D11PixelShader? _pixelShaderR8;
        private D3D11PixelShader? _pixelShaderColor;
        private D3D11PixelShader? _pixelShaderFlatColor;

        private D3D11InputLayout? _inputLayout;
        private D3D11Buffer? _vertexBuffer;
        private D3D11Buffer? _cbViewport;
        private D3D11Buffer? _cbColor;
        private D3D11SamplerState? _linearSampler;
        private D3D11SamplerState? _pointSampler;
        private D3D11BlendState? _blendState;
        private D3D11RasterizerState? _rasterizerState;

        private D3D11Texture2D? _cameraTexture;
        private D3D11ShaderResourceView? _cameraSrv;
        private int _camWidth;
        private int _camHeight;
        private ImageFormatMode _camFormat;

        private bool _disposed;

        public int CameraWidth => _camWidth;
        public int CameraHeight => _camHeight;
        public ImageFormatMode CameraFormat => _camFormat;
        public bool HasFrame => _cameraTexture != null && _cameraSrv != null && _cameraSrv.IsValid;

        public CameraViewportPipeline(D3D11Device device, D3D11DeviceContext context)
        {
            _device = device ?? throw new ArgumentNullException(nameof(device));
            _context = context ?? throw new ArgumentNullException(nameof(context));

            InitializePipeline();
        }

        private unsafe void InitializePipeline()
        {
            // 1. Shaders
            _vertexShader = _device.CreateVertexShader(CameraViewportBytecodes.VsMainBytecode);
            _pixelShaderR8 = _device.CreatePixelShader(CameraViewportBytecodes.PsR8Bytecode);
            _pixelShaderColor = _device.CreatePixelShader(CameraViewportBytecodes.PsColorBytecode);
            _pixelShaderFlatColor = _device.CreatePixelShader(CameraViewportBytecodes.PsFlatColorBytecode);

            // 2. Input Layout (float2 POSITION, float2 TEXCOORD0)
            IntPtr pPosSemantic = Marshal.StringToHGlobalAnsi("POSITION");
            IntPtr pTexSemantic = Marshal.StringToHGlobalAnsi("TEXCOORD");
            try
            {
                var layoutDescs = new[]
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

                _inputLayout = _device.CreateInputLayout(layoutDescs, CameraViewportBytecodes.VsMainBytecode);
            }
            finally
            {
                Marshal.FreeHGlobal(pPosSemantic);
                Marshal.FreeHGlobal(pTexSemantic);
            }

            // 3. Dynamic Vertex Buffer (128 vertices capacity for quad and overlay lines)
            D3D11_BUFFER_DESC vbDesc = new D3D11_BUFFER_DESC
            {
                ByteWidth = (uint)(sizeof(CameraVertex) * 128),
                Usage = D3D11_USAGE.D3D11_USAGE_DYNAMIC,
                BindFlags = D3D11_BIND_FLAG.D3D11_BIND_VERTEX_BUFFER,
                CPUAccessFlags = D3D11_CPU_ACCESS_FLAG.D3D11_CPU_ACCESS_WRITE,
                MiscFlags = 0,
                StructureByteStride = (uint)sizeof(CameraVertex)
            };
            _vertexBuffer = _device.CreateBuffer(ref vbDesc);

            // 4. Constant Buffers
            D3D11_BUFFER_DESC cbDesc = new D3D11_BUFFER_DESC
            {
                ByteWidth = 16, // float2 ViewportSize + float2 Padding
                Usage = D3D11_USAGE.D3D11_USAGE_DEFAULT,
                BindFlags = D3D11_BIND_FLAG.D3D11_BIND_CONSTANT_BUFFER,
                CPUAccessFlags = 0,
                MiscFlags = 0,
                StructureByteStride = 0
            };
            _cbViewport = _device.CreateBuffer(ref cbDesc);

            cbDesc.ByteWidth = 16; // float4 BoxColor
            _cbColor = _device.CreateBuffer(ref cbDesc);

            // 5. Samplers
            D3D11_SAMPLER_DESC linearSamplerDesc = new D3D11_SAMPLER_DESC
            {
                Filter = D3D11_FILTER.D3D11_FILTER_MIN_MAG_MIP_LINEAR,
                AddressU = D3D11_TEXTURE_ADDRESS_MODE.D3D11_TEXTURE_ADDRESS_CLAMP,
                AddressV = D3D11_TEXTURE_ADDRESS_MODE.D3D11_TEXTURE_ADDRESS_CLAMP,
                AddressW = D3D11_TEXTURE_ADDRESS_MODE.D3D11_TEXTURE_ADDRESS_CLAMP,
                ComparisonFunc = D3D11_COMPARISON_FUNC.D3D11_COMPARISON_NEVER,
                MinLOD = 0,
                MaxLOD = float.MaxValue
            };
            _linearSampler = _device.CreateSamplerState(ref linearSamplerDesc);

            D3D11_SAMPLER_DESC pointSamplerDesc = linearSamplerDesc;
            pointSamplerDesc.Filter = D3D11_FILTER.D3D11_FILTER_MIN_MAG_MIP_POINT;
            _pointSampler = _device.CreateSamplerState(ref pointSamplerDesc);

            // 6. Blend State (Alpha Blending for overlays)
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

            // 7. Rasterizer State (No culling)
            D3D11_RASTERIZER_DESC rastDesc = new D3D11_RASTERIZER_DESC
            {
                FillMode = D3D11_FILL_MODE.D3D11_FILL_SOLID,
                CullMode = D3D11_CULL_MODE.D3D11_CULL_NONE,
                DepthClipEnable = 0
            };
            _rasterizerState = _device.CreateRasterizerState(ref rastDesc);
        }

        private void EnsureCameraTexture(int width, int height, ImageFormatMode format)
        {
            if (_cameraTexture != null && _camWidth == width && _camHeight == height && _camFormat == format)
                return;

            _cameraSrv?.Dispose();
            _cameraTexture?.Dispose();

            _camWidth = width;
            _camHeight = height;
            _camFormat = format;

            DXGI_FORMAT dxFormat = (format == ImageFormatMode.Bgra32)
                ? DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM
                : DXGI_FORMAT.DXGI_FORMAT_R8_UNORM;

            D3D11_TEXTURE2D_DESC desc = new D3D11_TEXTURE2D_DESC
            {
                Width = (uint)width,
                Height = (uint)height,
                MipLevels = 1,
                ArraySize = 1,
                Format = dxFormat,
                SampleDesc = new DXGI_SAMPLE_DESC(1, 0),
                Usage = D3D11_USAGE.D3D11_USAGE_DEFAULT,
                BindFlags = D3D11_BIND_FLAG.D3D11_BIND_SHADER_RESOURCE,
                CPUAccessFlags = 0,
                MiscFlags = 0
            };

            _cameraTexture = _device.CreateTexture2D(ref desc);
            _cameraSrv = _device.CreateShaderResourceView(_cameraTexture.Handle);
        }

        /// <summary>
        /// Uploads an ImageBuffer directly into the GPU texture with 0 managed heap allocations.
        /// </summary>
        public unsafe void UploadFrame(ImageBuffer buffer)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            UploadFrameRaw((IntPtr)buffer.Scan0, buffer.Width, buffer.Height, buffer.Stride, buffer.Format);
        }

        /// <summary>
        /// Uploads raw camera unmanaged frame memory directly into the GPU texture with 0 managed heap allocations.
        /// </summary>
        public void UploadFrameRaw(IntPtr pScan0, int width, int height, int stride, ImageFormatMode format)
        {
            if (pScan0 == IntPtr.Zero) throw new ArgumentNullException(nameof(pScan0));
            if (width <= 0 || height <= 0) return;

            if (stride <= 0)
            {
                int bpp = (format == ImageFormatMode.Bgra32) ? 4 : 1;
                stride = ((width * bpp) + 3) & ~3;
            }

            EnsureCameraTexture(width, height, format);

            ComVTableHelper.UpdateSubresource(
                _context.Handle,
                _cameraTexture!.Handle,
                0,
                IntPtr.Zero,
                pScan0,
                (uint)stride,
                0);
        }

        /// <summary>
        /// Renders the camera frame into the specified RenderTargetView using the given viewport placement and zoom.
        /// </summary>
        public unsafe void Render(
            D3D11RenderTargetView rtv,
            int viewportWidth,
            int viewportHeight,
            float destX,
            float destY,
            float destWidth,
            float destHeight,
            bool usePointFilter = false,
            IReadOnlyList<CameraOverlayBox>? overlays = null)
        {
            if (rtv == null || !rtv.IsValid) throw new ArgumentNullException(nameof(rtv));
            if (!HasFrame) return;

            // 1. Setup Viewport & Render Targets
            D3D11_VIEWPORT vp = new D3D11_VIEWPORT
            {
                TopLeftX = 0,
                TopLeftY = 0,
                Width = viewportWidth,
                Height = viewportHeight,
                MinDepth = 0.0f,
                MaxDepth = 1.0f
            };
            _context.RSSetViewports(vp);
            _context.OMSetRenderTargets(rtv);

            // 2. Setup Pipeline States
            _context.RSSetState(_rasterizerState);
            float[] blendFactor = new[] { 0f, 0f, 0f, 0f };
            _context.OMSetBlendState(_blendState, blendFactor, 0xFFFFFFFF);

            // 3. Update Viewport Constant Buffer
            float[] vpData = new[] { (float)viewportWidth, (float)viewportHeight, 0f, 0f };
            fixed (float* pVp = vpData)
            {
                ComVTableHelper.UpdateSubresource(
                    _context.Handle,
                    _cbViewport!.Handle,
                    0,
                    IntPtr.Zero,
                    (IntPtr)pVp,
                    0,
                    0);
            }
            _context.VSSetConstantBuffers(0, _cbViewport);

            // 4. Update Dynamic Vertex Buffer for Image Quad
            CameraVertex[] quad = new[]
            {
                new CameraVertex(destX, destY, 0.0f, 0.0f),
                new CameraVertex(destX + destWidth, destY, 1.0f, 0.0f),
                new CameraVertex(destX, destY + destHeight, 0.0f, 1.0f),
                new CameraVertex(destX + destWidth, destY + destHeight, 1.0f, 1.0f)
            };

            int hr = ComVTableHelper.Map(_context.Handle, _vertexBuffer!.Handle, 0, D3D11_MAP.D3D11_MAP_WRITE_DISCARD, 0, out var mapped);
            if (hr >= 0 && mapped.pData != IntPtr.Zero)
            {
                fixed (CameraVertex* pQuad = quad)
                {
                    Buffer.MemoryCopy(pQuad, (void*)mapped.pData, sizeof(CameraVertex) * 4, sizeof(CameraVertex) * 4);
                }
                ComVTableHelper.Unmap(_context.Handle, _vertexBuffer.Handle, 0);
            }

            // 5. Draw Image Quad
            _context.IASetInputLayout(_inputLayout!);
            _context.IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY.D3D11_PRIMITIVE_TOPOLOGY_TRIANGLESTRIP);
            _context.IASetVertexBuffers(0, _vertexBuffer!, (uint)sizeof(CameraVertex), 0);

            _context.VSSetShader(_vertexShader!);

            var activePs = (_camFormat == ImageFormatMode.Bgra32) ? _pixelShaderColor : _pixelShaderR8;
            _context.PSSetShader(activePs!);

            var activeSampler = usePointFilter ? _pointSampler : _linearSampler;
            _context.PSSetSamplers(0, activeSampler!);
            _context.PSSetShaderResources(0, _cameraSrv!);

            _context.Draw(4);

            // 6. Draw Overlay Bounding Boxes (if any)
            if (overlays != null && overlays.Count > 0 && _camWidth > 0 && _camHeight > 0)
            {
                _context.PSSetShader(_pixelShaderFlatColor!);
                _context.PSSetConstantBuffers(1, _cbColor!);
                _context.IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY.D3D11_PRIMITIVE_TOPOLOGY_LINESTRIP);

                float scaleX = destWidth / _camWidth;
                float scaleY = destHeight / _camHeight;

                CameraVertex[] boxVertices = new CameraVertex[5];

                for (int i = 0; i < overlays.Count; i++)
                {
                    var box = overlays[i];
                    float bx = destX + (box.Rect.X * scaleX);
                    float by = destY + (box.Rect.Y * scaleY);
                    float bw = box.Rect.Width * scaleX;
                    float bh = box.Rect.Height * scaleY;

                    // 5 points forming a closed rectangular line-strip
                    boxVertices[0] = new CameraVertex(bx, by, 0, 0);
                    boxVertices[1] = new CameraVertex(bx + bw, by, 0, 0);
                    boxVertices[2] = new CameraVertex(bx + bw, by + bh, 0, 0);
                    boxVertices[3] = new CameraVertex(bx, by + bh, 0, 0);
                    boxVertices[4] = new CameraVertex(bx, by, 0, 0);

                    // Update flat color constant buffer
                    float[] colData = new[]
                    {
                        box.Color.R / 255.0f,
                        box.Color.G / 255.0f,
                        box.Color.B / 255.0f,
                        box.Color.A / 255.0f
                    };
                    fixed (float* pCol = colData)
                    {
                        ComVTableHelper.UpdateSubresource(_context.Handle, _cbColor!.Handle, 0, IntPtr.Zero, (IntPtr)pCol, 0, 0);
                    }

                    // Map vertices
                    hr = ComVTableHelper.Map(_context.Handle, _vertexBuffer!.Handle, 0, D3D11_MAP.D3D11_MAP_WRITE_DISCARD, 0, out mapped);
                    if (hr >= 0 && mapped.pData != IntPtr.Zero)
                    {
                        fixed (CameraVertex* pBox = boxVertices)
                        {
                            Buffer.MemoryCopy(pBox, (void*)mapped.pData, sizeof(CameraVertex) * 5, sizeof(CameraVertex) * 5);
                        }
                        ComVTableHelper.Unmap(_context.Handle, _vertexBuffer.Handle, 0);
                        _context.Draw(5);
                    }
                }
            }

            // Unbind SRV to allow subsequent texture updates without pipeline lock
            _context.PSSetShaderResources(0);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _cameraSrv?.Dispose();
                _cameraSrv = null;

                _cameraTexture?.Dispose();
                _cameraTexture = null;

                _rasterizerState?.Dispose();
                _blendState?.Dispose();
                _pointSampler?.Dispose();
                _linearSampler?.Dispose();
                _cbColor?.Dispose();
                _cbViewport?.Dispose();
                _vertexBuffer?.Dispose();
                _inputLayout?.Dispose();

                _pixelShaderFlatColor?.Dispose();
                _pixelShaderColor?.Dispose();
                _pixelShaderR8?.Dispose();
                _vertexShader?.Dispose();

                _disposed = true;
            }
        }
    }
}
