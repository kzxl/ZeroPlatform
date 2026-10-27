using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ZeroUI.Core.Common
{
    /// <summary>
    /// Cross-runtime memory allocation abstraction bridging NativeMemory (.NET 8+)
    /// and Marshal.AllocHGlobal (.NET Framework / .NET Standard 2.0).
    /// </summary>
    public static unsafe class ZeroMemory
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void* Alloc(nuint byteCount)
        {
#if NET8_0_OR_GREATER
            return NativeMemory.Alloc(byteCount);
#else
            return (void*)Marshal.AllocHGlobal((IntPtr)(long)byteCount);
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void* AllocZeroed(nuint byteCount)
        {
#if NET8_0_OR_GREATER
            return NativeMemory.AllocZeroed(byteCount);
#else
            void* ptr = (void*)Marshal.AllocHGlobal((IntPtr)(long)byteCount);
            Unsafe.InitBlockUnaligned(ptr, 0, (uint)byteCount);
            return ptr;
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Free(void* ptr)
        {
            if (ptr == null) return;
#if NET8_0_OR_GREATER
            NativeMemory.Free(ptr);
#else
            Marshal.FreeHGlobal((IntPtr)ptr);
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Int32BitsToSingle(int value)
        {
#if NET8_0_OR_GREATER
            return BitConverter.Int32BitsToSingle(value);
#else
            return *(float*)&value;
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int SingleToInt32Bits(float value)
        {
#if NET8_0_OR_GREATER
            return BitConverter.SingleToInt32Bits(value);
#else
            return *(int*)&value;
#endif
        }
    }
}
