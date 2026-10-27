using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace ZeroGraphics.DirectX.Interception
{
    /// <summary>
    /// Pure C# low-level COM VTable hooking engine.
    /// Intercepts native interface method slots via memory protection manipulation (VirtualProtect).
    /// Safe, zero-dependency, and deterministic with automatic restoration on dispose.
    /// </summary>
    public sealed unsafe class ComVTableHook : IDisposable
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool VirtualProtect(IntPtr lpAddress, UIntPtr dwSize, uint flNewProtect, out uint lpflOldProtect);

        private const uint PAGE_EXECUTE_READWRITE = 0x40;

        private readonly IntPtr _comObject;
        private readonly IntPtr* _vtable;
        private readonly Dictionary<int, IntPtr> _originalPointers = new Dictionary<int, IntPtr>();
        private bool _disposed;

        /// <summary>
        /// Gets the raw COM object instance pointer.
        /// </summary>
        public IntPtr ComObject => _comObject;

        /// <summary>
        /// Initializes a new VTable hook for the specified COM interface pointer.
        /// </summary>
        /// <param name="comObject">Pointer to COM interface (e.g. IDXGISwapChain* or ID3D11DeviceContext*).</param>
        public ComVTableHook(IntPtr comObject)
        {
            if (comObject == IntPtr.Zero) throw new ArgumentNullException(nameof(comObject));
            _comObject = comObject;
            // A COM object pointer points directly to its VTable pointer (*(void**)comObject)
            _vtable = *(IntPtr**)_comObject;
        }

        /// <summary>
        /// Gets the original function pointer at the specified VTable slot index.
        /// </summary>
        public IntPtr GetOriginalMethod(int slotIndex)
        {
            if (_originalPointers.TryGetValue(slotIndex, out var orig))
            {
                return orig;
            }
            return _vtable[slotIndex];
        }

        /// <summary>
        /// Hooks a specific VTable slot, swapping the original pointer with the detour function pointer.
        /// </summary>
        /// <param name="slotIndex">0-indexed VTable method slot.</param>
        /// <param name="detourFunctionPointer">Pointer to unmanaged detour method or native delegate.</param>
        /// <returns>The original function pointer before hooking.</returns>
        public IntPtr HookMethod(int slotIndex, IntPtr detourFunctionPointer)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ComVTableHook));
            if (detourFunctionPointer == IntPtr.Zero) throw new ArgumentNullException(nameof(detourFunctionPointer));

            if (_originalPointers.ContainsKey(slotIndex))
            {
                throw new InvalidOperationException($"VTable slot {slotIndex} is already hooked.");
            }

            IntPtr* targetSlot = &_vtable[slotIndex];
            IntPtr originalPtr = *targetSlot;

            // Change protection of the slot to PAGE_EXECUTE_READWRITE
            if (!VirtualProtect((IntPtr)targetSlot, (UIntPtr)sizeof(IntPtr), PAGE_EXECUTE_READWRITE, out uint oldProtect))
            {
                int error = Marshal.GetLastWin32Error();
                throw new InvalidOperationException($"VirtualProtect failed on slot {slotIndex} with Win32 error {error}");
            }

            // Atomic pointer swap
            *targetSlot = detourFunctionPointer;

            // Restore original protection
            VirtualProtect((IntPtr)targetSlot, (UIntPtr)sizeof(IntPtr), oldProtect, out _);

            _originalPointers[slotIndex] = originalPtr;
            return originalPtr;
        }

        /// <summary>
        /// Restores a hooked VTable slot back to its original native function pointer.
        /// </summary>
        public bool UnhookMethod(int slotIndex)
        {
            if (!_originalPointers.TryGetValue(slotIndex, out var originalPtr))
            {
                return false;
            }

            IntPtr* targetSlot = &_vtable[slotIndex];
            if (VirtualProtect((IntPtr)targetSlot, (UIntPtr)sizeof(IntPtr), PAGE_EXECUTE_READWRITE, out uint oldProtect))
            {
                *targetSlot = originalPtr;
                VirtualProtect((IntPtr)targetSlot, (UIntPtr)sizeof(IntPtr), oldProtect, out _);
            }

            _originalPointers.Remove(slotIndex);
            return true;
        }

        /// <summary>
        /// Restores all hooked slots and cleans up resources.
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                foreach (var kvp in _originalPointers)
                {
                    IntPtr* targetSlot = &_vtable[kvp.Key];
                    if (VirtualProtect((IntPtr)targetSlot, (UIntPtr)sizeof(IntPtr), PAGE_EXECUTE_READWRITE, out uint oldProtect))
                    {
                        *targetSlot = kvp.Value;
                        VirtualProtect((IntPtr)targetSlot, (UIntPtr)sizeof(IntPtr), oldProtect, out _);
                    }
                }
                _originalPointers.Clear();
            }
        }
    }
}
