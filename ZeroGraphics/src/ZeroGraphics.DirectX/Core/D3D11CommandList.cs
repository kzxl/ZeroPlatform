using System;
using ZeroGraphics.DirectX.Native;

namespace ZeroGraphics.DirectX.Core
{
    /// <summary>
    /// Encapsulates a recorded Direct3D 11 Command List (<c>ID3D11CommandList</c>).
    /// Enables multi-threaded CPU command generation across Deferred Contexts
    /// for high-throughput batch and tile rendering.
    /// </summary>
    public sealed class D3D11CommandList : ComObjectWrapper
    {
        public D3D11CommandList(IntPtr handle) : base(handle)
        {
        }
    }
}
