using System;
using System.IO;

namespace ZeroDocuments.Common
{
    /// <summary>
    /// Writes files to disk atomically using a sibling temporary file, guaranteeing that
    /// destination files are never left half-written or corrupted if an error occurs.
    /// </summary>
    internal static class AtomicFileWriter
    {
        public static void Write(string filePath, Action<Stream> writeAction)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));
            if (writeAction == null)
                throw new ArgumentNullException(nameof(writeAction));

            string? directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string tempPath = filePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            bool success = false;

            try
            {
                using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    writeAction(stream);
                }

                ReplaceFile(tempPath, filePath);
                success = true;
            }
            finally
            {
                if (!success && File.Exists(tempPath))
                {
                    try
                    {
                        File.Delete(tempPath);
                    }
                    catch
                    {
                        // Ignore deletion failure during cleanup
                    }
                }
            }
        }

        private static void ReplaceFile(string source, string destination)
        {
#if NET8_0_OR_GREATER
            File.Move(source, destination, overwrite: true);
#else
            if (File.Exists(destination))
            {
                File.Delete(destination);
            }
            File.Move(source, destination);
#endif
        }
    }
}
