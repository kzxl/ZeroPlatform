using System;
using System.IO;

namespace ZeroSystem;

/// <summary>
/// Provides resilient file access methods that bypass non-exclusive file locks held by other active processes.
/// </summary>
public static class UnsafeFileStreamFactory
{
    /// <summary>
    /// Opens a read-only FileStream using FileShare.ReadWrite | FileShare.Delete to safely read files
    /// even if they are currently opened by active databases, web servers, or editors.
    /// </summary>
    public static FileStream OpenPermissiveReadStream(string filePath, int bufferSize = 65536)
    {
        return new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            bufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
    }

    /// <summary>
    /// Attempts to read all bytes of a file using permissive sharing.
    /// </summary>
    public static byte[] ReadAllBytesPermissive(string filePath)
    {
        using var stream = OpenPermissiveReadStream(filePath);
        using var ms = new MemoryStream((int)stream.Length);
        stream.CopyTo(ms);
        return ms.ToArray();
    }
}
