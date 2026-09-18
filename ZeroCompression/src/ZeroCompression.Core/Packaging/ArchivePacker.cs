using System;
using System.Collections.Generic;
using System.Formats.Tar;
using System.IO;
using System.Threading;
using ZeroCompression.Core.Buffers;
using ZeroCompression.Core.Streams;

namespace ZeroCompression.Core.Packaging
{
    /// <summary>One file or directory entry inside an archive container.</summary>
    public readonly record struct ArchiveEntryInfo(string Name, long Size, bool IsDirectory);

    /// <summary>
    /// Pure streaming packaging utilities for TAR containers with zero temporary files.
    /// </summary>
    public static class ArchivePacker
    {
        public static long PackToTar(string sourcePath, Stream destination, IProgress<long>? progress = null, CancellationToken cancel = default)
        {
            ArgumentNullException.ThrowIfNull(destination);
            bool isDir = Directory.Exists(sourcePath);
            bool isFile = File.Exists(sourcePath);
            if (!isDir && !isFile)
                throw new FileNotFoundException("Source path not found.", sourcePath);

            var meter = new ObservableStream(destination, computeCrc: false, progress: progress, leaveOpen: true, cancel: cancel);
            if (isDir)
            {
                TarFile.CreateFromDirectory(sourcePath, meter, includeBaseDirectory: true);
            }
            else
            {
                using var tar = new TarWriter(meter, leaveOpen: true);
                tar.WriteEntry(sourcePath, Path.GetFileName(sourcePath));
            }
            meter.Flush();
            return meter.BytesObserved;
        }

        public static void ExtractFromTar(Stream tarStream, string destinationDir, IProgress<long>? progress = null, CancellationToken cancel = default)
        {
            ArgumentNullException.ThrowIfNull(tarStream);
            Directory.CreateDirectory(destinationDir);

            using var meter = new ObservableStream(tarStream, computeCrc: false, progress: progress, leaveOpen: true, cancel: cancel);
            TarFile.ExtractToDirectory(meter, destinationDir, overwriteFiles: true);
        }

        public static IReadOnlyList<ArchiveEntryInfo> ListTarEntries(Stream tarStream)
        {
            ArgumentNullException.ThrowIfNull(tarStream);
            var entries = new List<ArchiveEntryInfo>();
            using var reader = new TarReader(tarStream, leaveOpen: true);
            TarEntry? entry;
            while ((entry = reader.GetNextEntry()) != null)
            {
                bool isDir = entry.EntryType is TarEntryType.Directory;
                entries.Add(new ArchiveEntryInfo(entry.Name, isDir ? -1 : entry.Length, isDir));
            }
            return entries;
        }

        public static void DiscardTar(Stream tarStream, CancellationToken cancel = default)
        {
            ArgumentNullException.ThrowIfNull(tarStream);
            using var pooled = BufferPool.Scope(BufferPool.DefaultCopySize);
            byte[] buf = pooled.Array;
            int read;
            while ((read = tarStream.Read(buf, 0, buf.Length)) > 0)
            {
                cancel.ThrowIfCancellationRequested();
            }
        }
    }
}
