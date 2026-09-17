using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace ZeroDocuments.Common
{
    /// <summary>
    /// Automatic runtime assembly resolution helper for legacy .NET Framework environments.
    /// Eliminates System.IO.Compression / ZipFile binding mismatches.
    /// </summary>
    internal static class RuntimeAssemblyResolver
    {
        private static bool _initialized = false;
        private static readonly object _lock = new object();

        public static void EnsureInitialized()
        {
            if (_initialized) return;

            lock (_lock)
            {
                if (_initialized) return;

#if NETFRAMEWORK
                AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
                {
                    try
                    {
                        var assemblyName = new AssemblyName(args.Name);
                        if (string.Equals(assemblyName.Name, "System.IO.Compression", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(assemblyName.Name, "System.IO.Compression.FileSystem", StringComparison.OrdinalIgnoreCase))
                        {
                            return AppDomain.CurrentDomain.GetAssemblies()
                                .FirstOrDefault(a => string.Equals(a.GetName().Name, assemblyName.Name, StringComparison.OrdinalIgnoreCase));
                        }
                    }
                    catch
                    {
                        // Silent catch on resolve failure
                    }
                    return null;
                };
#endif
                _initialized = true;
            }
        }
    }
}
