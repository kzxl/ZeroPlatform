using System;
using System.Collections.Generic;
using System.IO;

namespace ZeroSystem;

public static partial class HardwareTelemetry
{
    /// <summary>
    /// Enumerates logical disk partitions, volume labels, file systems, and space metrics.
    /// </summary>
    public static IReadOnlyList<DriveInfoSnapshot> GetStorageDrives()
    {
        var drives = new List<DriveInfoSnapshot>();

        try
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (!drive.IsReady)
                {
                    drives.Add(new DriveInfoSnapshot(
                        DriveLetter: drive.Name,
                        VolumeLabel: string.Empty,
                        FileSystemName: string.Empty,
                        DriveType: drive.DriveType,
                        TotalBytes: 0,
                        FreeBytes: 0,
                        FreePercent: 0.0f));
                    continue;
                }

                ulong total = (ulong)drive.TotalSize;
                ulong free = (ulong)drive.AvailableFreeSpace;
                float freePct = total > 0 ? (float)free * 100.0f / total : 0.0f;

                drives.Add(new DriveInfoSnapshot(
                    DriveLetter: drive.Name,
                    VolumeLabel: drive.VolumeLabel ?? string.Empty,
                    FileSystemName: drive.DriveFormat ?? string.Empty,
                    DriveType: drive.DriveType,
                    TotalBytes: total,
                    FreeBytes: free,
                    FreePercent: freePct));
            }
        }
        catch
        {
            // Drive query fallback
        }

        return drives;
    }
}
