using System;
using System.Runtime.CompilerServices;

namespace ZeroPrimitives.Internal
{
    /// <summary>
    /// Cold-path exception helper.
    /// Isolates exception instantiation and throwing into non-inlined methods,
    /// keeping hot-path call-sites under the 32-byte IL inlining budget for optimal JIT compiler inlining.
    /// </summary>
    internal static class ThrowHelper
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowEndOfBuffer()
        {
            throw new IndexOutOfRangeException("SpanReader has reached the end of the buffer.");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowDestinationBufferFull()
        {
            throw new IndexOutOfRangeException("SpanWriter destination buffer is full.");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowRequestedLengthExceedsBuffer(string paramName)
        {
            throw new ArgumentOutOfRangeException(paramName, "Requested byte count exceeds remaining buffer length.");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowCannotAdvanceBeyondBounds(string paramName)
        {
            throw new ArgumentOutOfRangeException(paramName, "Cannot advance beyond buffer bounds.");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowCannotRewindBeforeStart(string paramName)
        {
            throw new ArgumentOutOfRangeException(paramName, "Cannot rewind before buffer start.");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowInvalidVarIntEncoding(string typeName)
        {
            throw new InvalidOperationException($"Invalid {typeName} encoding in binary stream.");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowArgumentException(string message, string? paramName = null)
        {
            if (paramName != null)
                throw new ArgumentException(message, paramName);
            throw new ArgumentException(message);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowArgumentNullException(string paramName)
        {
            throw new ArgumentNullException(paramName);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowArgumentOutOfRangeException(string paramName, string message)
        {
            throw new ArgumentOutOfRangeException(paramName, message);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowFormatException(string message)
        {
            throw new FormatException(message);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowInvalidOperationException(string message)
        {
            throw new InvalidOperationException(message);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowObjectDisposedException(string objectName)
        {
            throw new ObjectDisposedException(objectName);
        }
    }
}
