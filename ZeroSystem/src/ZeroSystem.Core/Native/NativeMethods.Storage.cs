using System;
using System.Runtime.InteropServices;

namespace ZeroSystem.Native;

internal static partial class NativeMethods
{
    // DOS Devices
    public const uint DDD_RAW_TARGET_PATH = 0x00000001;
    public const uint DDD_REMOVE_DEFINITION = 0x00000002;
    public const uint DDD_EXACT_MATCH_ON_REMOVE = 0x00000004;

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DefineDosDevice(uint dwFlags, string lpDeviceName, string? lpTargetPath);

    // Raw File / Physical Drive Creation
    public const uint GENERIC_READ = 0x80000000;
    public const uint GENERIC_WRITE = 0x40000000;
    public const uint FILE_SHARE_READ = 0x00000001;
    public const uint FILE_SHARE_WRITE = 0x00000002;
    public const uint OPEN_EXISTING = 3;

    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern IntPtr CreateFile(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    // DeviceIoControl
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DeviceIoControl(
        IntPtr hDevice,
        uint dwIoControlCode,
        IntPtr lpInBuffer,
        uint nInBufferSize,
        IntPtr lpOutBuffer,
        uint nOutBufferSize,
        out uint lpBytesReturned,
        IntPtr lpOverlapped);

    // IOCTL Storage Properties & Descriptors
    public const uint IOCTL_STORAGE_QUERY_PROPERTY = 0x002D1400;
    public const uint IOCTL_DISK_GET_DRIVE_GEOMETRY_EX = 0x000700A0;

    public enum STORAGE_PROPERTY_ID
    {
        StorageDeviceProperty = 0,
        StorageAdapterProperty = 1,
        StorageDeviceIdProperty = 2,
        StorageDeviceUniqueIdProperty = 3,
        StorageDeviceWriteCacheProperty = 4,
        StorageMiniportProperty = 5,
        StorageAccessAlignmentProperty = 6,
        StorageDeviceSeekPenaltyProperty = 7,
        StorageDeviceTrimProperty = 8
    }

    public enum STORAGE_QUERY_TYPE
    {
        PropertyStandardQuery = 0,
        PropertyExistsQuery = 1,
        PropertyMaskQuery = 2,
        PropertyQueryMaxDefined = 3
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct STORAGE_PROPERTY_QUERY
    {
        public STORAGE_PROPERTY_ID PropertyId;
        public STORAGE_QUERY_TYPE QueryType;
        public byte AdditionalParameters;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DEVICE_SEEK_PENALTY_DESCRIPTOR
    {
        public uint Version;
        public uint Size;
        [MarshalAs(UnmanagedType.I1)]
        public bool IncursSeekPenalty;
    }

    public enum STORAGE_BUS_TYPE
    {
        BusTypeUnknown = 0x00,
        BusTypeScsi = 0x01,
        BusTypeAtapi = 0x02,
        BusTypeAta = 0x03,
        BusType1394 = 0x04,
        BusTypeSsa = 0x05,
        BusTypeFibre = 0x06,
        BusTypeUsb = 0x07,
        BusTypeRAID = 0x08,
        BusTypeiScsi = 0x09,
        BusTypeSas = 0x0A,
        BusTypeSata = 0x0B,
        BusTypeSd = 0x0C,
        BusTypeMmc = 0x0D,
        BusTypeVirtual = 0x0E,
        BusTypeFileBackedVirtual = 0x0F,
        BusTypeSpaces = 0x10,
        BusTypeNvme = 0x11,
        BusTypeSCM = 0x12,
        BusTypeUfs = 0x13,
        BusTypeMax = 0x14
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct STORAGE_DEVICE_DESCRIPTOR
    {
        public uint Version;
        public uint Size;
        public byte DeviceType;
        public byte DeviceTypeModifier;
        [MarshalAs(UnmanagedType.I1)]
        public bool RemovableMedia;
        [MarshalAs(UnmanagedType.I1)]
        public bool CommandQueueing;
        public uint VendorIdOffset;
        public uint ProductIdOffset;
        public uint ProductRevisionOffset;
        public uint SerialNumberOffset;
        public STORAGE_BUS_TYPE BusType;
        public uint RawPropertiesLength;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DISK_GEOMETRY
    {
        public long Cylinders;
        public int MediaType;
        public uint TracksPerCylinder;
        public uint SectorsPerTrack;
        public uint BytesPerSector;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DISK_GEOMETRY_EX
    {
        public DISK_GEOMETRY Geometry;
        public long DiskSize;
        public byte Data;
    }
}
