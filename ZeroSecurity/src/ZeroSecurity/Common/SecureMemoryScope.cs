using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using ZeroPrimitives.Memory;

namespace ZeroSecurity.Common;

/// <summary>
/// Sovereign unmanaged memory scope for highly sensitive cryptographic secrets, keys, and tokens.
/// Guarantees that sensitive data resides outside of the managed GC heap (preventing heap compaction copies),
/// and enforces zero-fill memory sanitization upon disposal via <see cref="CryptoMemory.SecureZero(Span{byte})"/>.
/// </summary>
public sealed unsafe class SecureMemoryScope : IDisposable
{
    private static readonly bool IsWindows = Environment.OSVersion.Platform == PlatformID.Win32NT;

    private IntPtr _pointer;
    private readonly int _length;
    private bool _isPageLocked;
    private int _disposed;

    /// <summary>
    /// Gets the length of the secure memory buffer in bytes.
    /// </summary>
    public int Length => _length;

    /// <summary>
    /// Gets whether the underlying unmanaged pages are locked into physical RAM (preventing OS swap-to-disk).
    /// </summary>
    public bool IsPageLocked => _isPageLocked && !IsDisposed;

    /// <summary>
    /// Gets whether this scope has been disposed.
    /// </summary>
    public bool IsDisposed => Volatile.Read(ref _disposed) != 0;

    /// <summary>
    /// Gets a pointer to the unmanaged memory buffer. Throws <see cref="ObjectDisposedException"/> if disposed.
    /// </summary>
    public byte* Pointer
    {
        get
        {
            ThrowIfDisposed();
            return (byte*)_pointer;
        }
    }

    /// <summary>
    /// Gets a span representation over the secure unmanaged buffer. Throws <see cref="ObjectDisposedException"/> if disposed.
    /// </summary>
    public Span<byte> Span
    {
        get
        {
            ThrowIfDisposed();
            return new Span<byte>((void*)_pointer, _length);
        }
    }

    /// <summary>
    /// Gets a read-only span representation over the secure unmanaged buffer. Throws <see cref="ObjectDisposedException"/> if disposed.
    /// </summary>
    public ReadOnlySpan<byte> ReadOnlySpan => Span;

    /// <summary>
    /// Allocates a new secure unmanaged memory scope of the specified size in bytes.
    /// All bytes are initialized to zero and best-effort locked into physical RAM pages.
    /// </summary>
    /// <param name="byteCount">Size of buffer in bytes.</param>
    public SecureMemoryScope(int byteCount)
    {
        if (byteCount < 0)
            throw new ArgumentOutOfRangeException(nameof(byteCount), "Byte count must be non-negative.");

        _length = byteCount;
        if (byteCount > 0)
        {
            _pointer = Marshal.AllocHGlobal(byteCount);
            // Zero memory immediately upon allocation
            CryptoMemory.SecureZero(new Span<byte>((void*)_pointer, _length));
            // Best-effort OS page lock to prevent swapping secret keys to disk
            _isPageLocked = TryLockMemory(_pointer, (UIntPtr)byteCount);
        }
        else
        {
            _pointer = IntPtr.Zero;
        }
    }

    /// <summary>
    /// Finalizer ensures memory sanitization and release if Dispose was not invoked.
    /// </summary>
    ~SecureMemoryScope()
    {
        DisposeCore();
    }

    /// <summary>
    /// Creates a secure memory scope from the specified byte span.
    /// The secret data is copied into unmanaged memory.
    /// </summary>
    public static SecureMemoryScope FromBytes(ReadOnlySpan<byte> secret)
    {
        var scope = new SecureMemoryScope(secret.Length);
        if (secret.Length > 0)
        {
            secret.CopyTo(scope.Span);
        }
        return scope;
    }

    /// <summary>
    /// Encodes a character sequence (password, API key, token) into unmanaged secure memory
    /// using the specified encoding (defaults to UTF-8), without allocating intermediate managed byte arrays.
    /// </summary>
    public static SecureMemoryScope FromChars(ReadOnlySpan<char> secretChars, Encoding? encoding = null)
    {
        encoding ??= Encoding.UTF8;

        if (secretChars.IsEmpty)
            return new SecureMemoryScope(0);

        fixed (char* charPtr = secretChars)
        {
            int byteCount = encoding.GetByteCount(charPtr, secretChars.Length);
            var scope = new SecureMemoryScope(byteCount);
            fixed (byte* bytePtr = scope.Span)
            {
                encoding.GetBytes(charPtr, secretChars.Length, bytePtr, byteCount);
            }
            return scope;
        }
    }

    /// <summary>
    /// Decodes a hexadecimal character sequence directly into secure unmanaged memory.
    /// </summary>
    public static SecureMemoryScope FromHexString(ReadOnlySpan<char> hexChars)
    {
        if (hexChars.Length % 2 != 0)
            throw new FormatException("Hexadecimal string length must be an even number.");

        int byteCount = hexChars.Length / 2;
        var scope = new SecureMemoryScope(byteCount);
        if (byteCount > 0)
        {
            if (!FastHex.TryDecode(hexChars, scope.Span, out int written) || written != byteCount)
            {
                scope.Dispose();
                throw new FormatException("Invalid hexadecimal sequence.");
            }
        }
        return scope;
    }

    /// <summary>
    /// Compares this secure buffer against another span in constant time.
    /// </summary>
    public bool ConstantTimeEquals(ReadOnlySpan<byte> other)
    {
        ThrowIfDisposed();
        return CryptoMemory.ConstantTimeEquals(ReadOnlySpan, other);
    }

    /// <summary>
    /// Compares this secure buffer against another secure memory scope in constant time.
    /// </summary>
    public bool ConstantTimeEquals(SecureMemoryScope other)
    {
        ThrowIfDisposed();
        if (other == null) return false;
        return CryptoMemory.ConstantTimeEquals(ReadOnlySpan, other.ReadOnlySpan);
    }

    /// <summary>
    /// Clears and zeroes the contents of the secure buffer without disposing the scope.
    /// </summary>
    public void Wipe()
    {
        ThrowIfDisposed();
        if (_length > 0)
        {
            CryptoMemory.SecureZero(Span);
        }
    }

    /// <summary>
    /// Cryptographically sanitizes (zero-fills) the unmanaged buffer, unlocks physical pages, and releases memory.
    /// </summary>
    public void Dispose()
    {
        DisposeCore();
        GC.SuppressFinalize(this);
    }

    private void DisposeCore()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        if (_pointer != IntPtr.Zero)
        {
            try
            {
                // Cryptographically wipe memory before releasing
                CryptoMemory.SecureZero(new Span<byte>((void*)_pointer, _length));
            }
            finally
            {
                if (_isPageLocked)
                {
                    TryUnlockMemory(_pointer, (UIntPtr)_length);
                    _isPageLocked = false;
                }
                Marshal.FreeHGlobal(_pointer);
                _pointer = IntPtr.Zero;
            }
        }
    }

    private static bool TryLockMemory(IntPtr ptr, UIntPtr size)
    {
        try
        {
            if (IsWindows)
            {
                return VirtualLock(ptr, size);
            }
            return MLock(ptr, size) == 0;
        }
        catch
        {
            return false;
        }
    }

    private static void TryUnlockMemory(IntPtr ptr, UIntPtr size)
    {
        try
        {
            if (IsWindows)
            {
                VirtualUnlock(ptr, size);
            }
            else
            {
                MUnlock(ptr, size);
            }
        }
        catch
        {
            // Ignore OS unlock errors on disposal
        }
    }

    [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
    private static extern bool VirtualLock(IntPtr lpAddress, UIntPtr dwSize);

    [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
    private static extern bool VirtualUnlock(IntPtr lpAddress, UIntPtr dwSize);

    [DllImport("libc", SetLastError = true, EntryPoint = "mlock")]
    private static extern int MLock(IntPtr addr, UIntPtr len);

    [DllImport("libc", SetLastError = true, EntryPoint = "munlock")]
    private static extern int MUnlock(IntPtr addr, UIntPtr len);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfDisposed()
    {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(SecureMemoryScope));
    }
}
