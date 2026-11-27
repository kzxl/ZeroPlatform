using System;
using System.Runtime.CompilerServices;

namespace ZeroPrimitives.Internal
{
    /// <summary>
    /// Compile-time cached type metadata for reference containment checks across modern and legacy TFMs.
    /// </summary>
    internal static class TypeTraits<T>
    {
        public static readonly bool ContainsReferences = Compute();

        private static bool Compute()
        {
#if NETCOREAPP2_0_OR_GREATER || NET5_0_OR_GREATER
            return RuntimeHelpers.IsReferenceOrContainsReferences<T>();
#else
            Type t = typeof(T);
            return !(t.IsPrimitive || t.IsEnum || t == typeof(decimal) || t == typeof(DateTime) || t == typeof(Guid));
#endif
        }
    }
}
