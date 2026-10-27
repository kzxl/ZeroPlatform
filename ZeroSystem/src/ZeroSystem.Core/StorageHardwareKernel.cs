using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using ZeroSystem.Native;

namespace ZeroSystem;

#region Storage Data Models

/// <summary>
/// Hardware bus and connection interface for a physical storage drive.
/// </summary>
public enum StorageBusInterface
{
    Unknown = 0,
    Scsi = 1,
    Atapi = 2,
    Ata = 3,
    FireWire = 4,
    Ssa = 5,
    FibreChannel = 6,
    Usb = 7,
    Raid = 8,
    IScsi = 9,
    Sas = 10,
    Sata = 11,
    Sd = 12,
    Mmc = 13,
    Virtual = 14,
    FileBackedVirtual = 15,
    StorageSpaces = 16,
    Nvme = 17,
    Scm = 18,
    Ufs = 19
}

/// <summary>
/// Detailed low-level hardware diagnostics of a physical drive.
/// </summary>
public sealed record PhysicalDiskHardwareInfo(
    int DiskIndex,
    bool IsSolidState,
    StorageBusInterface BusType,
    long TotalSizeBytes,
    string Model,
    string SerialNumber,
    string FirmwareRevision,
    uint BytesPerSector);

#endregion

/// <summary>
/// Low-level storage and drive hardware kernel.
/// Interacts directly with physical drive IOCTLs without WMI overhead (SSD seek penalty, NVMe bus detection, geometry).
/// </summary>
public static class StorageHardwareKernel
{
    /// <summary>
    /// Checks whether the specified drive letter (e.g. 'C') resides on a solid-state drive (SSD / NVMe) or spinning hard disk (HDD).
    /// Leverages IOCTL_STORAGE_QUERY_PROPERTY (Seek Penalty Descriptor) with sub-millisecond execution.
    /// </summary>
    public static bool IsSolidStateDrive(char driveLetter)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return false;

        string volumePath = $@"\\.\{char.ToUpperInvariant(driveLetter)}:";
        IntPtr hVolume = NativeMethods.CreateFile(
            volumePath,
            0, // Query access only
            NativeMethods.FILE_SHARE_READ | NativeMethods.FILE_SHARE_WRITE,
            IntPtr.Zero,
            NativeMethods.OPEN_EXISTING,
            0,
            IntPtr.Zero);

        if (hVolume == NativeMethods.INVALID_HANDLE_VALUE)
            return false;

        try
        {
            var query = new NativeMethods.STORAGE_PROPERTY_QUERY
            {
                PropertyId = NativeMethods.STORAGE_PROPERTY_ID.StorageDeviceSeekPenaltyProperty,
                QueryType = NativeMethods.STORAGE_QUERY_TYPE.PropertyStandardQuery
            };

            int querySize = Marshal.SizeOf(query);
            IntPtr inBuf = Marshal.AllocHGlobal(querySize);
            Marshal.StructureToPtr(query, inBuf, false);

            var penaltyDesc = new NativeMethods.DEVICE_SEEK_PENALTY_DESCRIPTOR();
            int outSize = Marshal.SizeOf(penaltyDesc);
            IntPtr outBuf = Marshal.AllocHGlobal(outSize);

            try
            {
                if (NativeMethods.DeviceIoControl(
                        hVolume,
                        NativeMethods.IOCTL_STORAGE_QUERY_PROPERTY,
                        inBuf,
                        (uint)querySize,
                        outBuf,
                        (uint)outSize,
                        out _,
                        IntPtr.Zero))
                {
                    penaltyDesc = Marshal.PtrToStructure<NativeMethods.DEVICE_SEEK_PENALTY_DESCRIPTOR>(outBuf);
                    // If IncursSeekPenalty is false, it's an SSD!
                    return !penaltyDesc.IncursSeekPenalty;
                }
            }
            finally
            {
                Marshal.FreeHGlobal(inBuf);
                Marshal.FreeHGlobal(outBuf);
            }
        }
        finally
        {
            NativeMethods.CloseHandle(hVolume);
        }

        return false;
    }

    /// <summary>
    /// Retrieves physical hardware metrics, bus interface (NVMe/SATA/USB), and geometry for a physical disk (e.g. \\.\PhysicalDrive0).
    /// </summary>
    public static PhysicalDiskHardwareInfo? GetPhysicalDiskInfo(int diskIndex = 0)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || diskIndex < 0)
            return null;

        string diskPath = $@"\\.\PhysicalDrive{diskIndex}";
        IntPtr hDisk = NativeMethods.CreateFile(
            diskPath,
            NativeMethods.GENERIC_READ,
            NativeMethods.FILE_SHARE_READ | NativeMethods.FILE_SHARE_WRITE,
            IntPtr.Zero,
            NativeMethods.OPEN_EXISTING,
            0,
            IntPtr.Zero);

        if (hDisk == NativeMethods.INVALID_HANDLE_VALUE)
            return null;

        try
        {
            // 1. Query Seek Penalty (SSD vs HDD)
            bool isSsd = false;
            var querySeek = new NativeMethods.STORAGE_PROPERTY_QUERY
            {
                PropertyId = NativeMethods.STORAGE_PROPERTY_ID.StorageDeviceSeekPenaltyProperty,
                QueryType = NativeMethods.STORAGE_QUERY_TYPE.PropertyStandardQuery
            };

            int querySize = Marshal.SizeOf(querySeek);
            IntPtr inBuf = Marshal.AllocHGlobal(querySize);
            Marshal.StructureToPtr(querySeek, inBuf, false);

            int penaltySize = Marshal.SizeOf<NativeMethods.DEVICE_SEEK_PENALTY_DESCRIPTOR>();
            IntPtr penaltyBuf = Marshal.AllocHGlobal(penaltySize);

            try
            {
                if (NativeMethods.DeviceIoControl(
                        hDisk,
                        NativeMethods.IOCTL_STORAGE_QUERY_PROPERTY,
                        inBuf,
                        (uint)querySize,
                        penaltyBuf,
                        (uint)penaltySize,
                        out _,
                        IntPtr.Zero))
                {
                    var desc = Marshal.PtrToStructure<NativeMethods.DEVICE_SEEK_PENALTY_DESCRIPTOR>(penaltyBuf);
                    isSsd = !desc.IncursSeekPenalty;
                }
            }
            finally
            {
                Marshal.FreeHGlobal(inBuf);
                Marshal.FreeHGlobal(penaltyBuf);
            }

            // 2. Query Device Descriptor (Model, Serial, BusType)
            string model = "Unknown Drive";
            string serial = "Unknown";
            string firmware = "Unknown";
            var busType = StorageBusInterface.Unknown;

            var queryDev = new NativeMethods.STORAGE_PROPERTY_QUERY
            {
                PropertyId = NativeMethods.STORAGE_PROPERTY_ID.StorageDeviceProperty,
                QueryType = NativeMethods.STORAGE_QUERY_TYPE.PropertyStandardQuery
            };

            int devInSize = Marshal.SizeOf(queryDev);
            IntPtr devInBuf = Marshal.AllocHGlobal(devInSize);
            Marshal.StructureToPtr(queryDev, devInBuf, false);

            int devOutSize = 1024;
            IntPtr devOutBuf = Marshal.AllocHGlobal(devOutSize);

            try
            {
                if (NativeMethods.DeviceIoControl(
                        hDisk,
                        NativeMethods.IOCTL_STORAGE_QUERY_PROPERTY,
                        devInBuf,
                        (uint)devInSize,
                        devOutBuf,
                        (uint)devOutSize,
                        out _,
                        IntPtr.Zero))
                {
                    var devDesc = Marshal.PtrToStructure<NativeMethods.STORAGE_DEVICE_DESCRIPTOR>(devOutBuf);
                    busType = (StorageBusInterface)(int)devDesc.BusType;

                    if (devDesc.ProductIdOffset > 0 && devDesc.ProductIdOffset < devOutSize)
                    {
                        model = ReadAsciiString(devOutBuf, (int)devDesc.ProductIdOffset);
                    }

                    if (devDesc.SerialNumberOffset > 0 && devDesc.SerialNumberOffset < devOutSize)
                    {
                        serial = ReadAsciiString(devOutBuf, (int)devDesc.SerialNumberOffset);
                    }

                    if (devDesc.ProductRevisionOffset > 0 && devDesc.ProductRevisionOffset < devOutSize)
                    {
                        firmware = ReadAsciiString(devOutBuf, (int)devDesc.ProductRevisionOffset);
                    }
                }
            }
            finally
            {
                Marshal.FreeHGlobal(devInBuf);
                Marshal.FreeHGlobal(devOutBuf);
            }

            // 3. Query Disk Geometry Ex (Total Capacity)
            long totalSize = 0;
            uint bytesPerSector = 512;

            int geomSize = Marshal.SizeOf<NativeMethods.DISK_GEOMETRY_EX>();
            IntPtr geomBuf = Marshal.AllocHGlobal(geomSize);

            try
            {
                if (NativeMethods.DeviceIoControl(
                        hDisk,
                        NativeMethods.IOCTL_DISK_GET_DRIVE_GEOMETRY_EX,
                        IntPtr.Zero,
                        0,
                        geomBuf,
                        (uint)geomSize,
                        out _,
                        IntPtr.Zero))
                {
                    var geom = Marshal.PtrToStructure<NativeMethods.DISK_GEOMETRY_EX>(geomBuf);
                    totalSize = geom.DiskSize;
                    bytesPerSector = geom.Geometry.BytesPerSector;
                }
            }
            finally
            {
                Marshal.FreeHGlobal(geomBuf);
            }

            return new PhysicalDiskHardwareInfo(
                DiskIndex: diskIndex,
                IsSolidState: isSsd,
                BusType: busType,
                TotalSizeBytes: totalSize,
                Model: model,
                SerialNumber: serial,
                FirmwareRevision: firmware,
                BytesPerSector: bytesPerSector);
        }
        finally
        {
            NativeMethods.CloseHandle(hDisk);
        }
    }

    private static string ReadAsciiString(IntPtr basePtr, int offset)
    {
        IntPtr strPtr = IntPtr.Add(basePtr, offset);
        string? val = Marshal.PtrToStringAnsi(strPtr);
        return val?.Trim() ?? string.Empty;
    }
}
