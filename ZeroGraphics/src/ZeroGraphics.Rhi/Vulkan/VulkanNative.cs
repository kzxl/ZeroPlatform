using System;
using System.Runtime.InteropServices;

namespace ZeroGraphics.Rhi.Vulkan
{
    #region Vulkan Enums

    public enum VkResult
    {
        Success = 0,
        NotReady = 1,
        Timeout = 2,
        EventSet = 3,
        EventReset = 4,
        Incomplete = 5,
        ErrorOutOfHostMemory = -1,
        ErrorOutOfDeviceMemory = -2,
        ErrorInitializationFailed = -3,
        ErrorDeviceLost = -4,
        ErrorMemoryMapFailed = -5,
        ErrorLayerNotPresent = -6,
        ErrorExtensionNotPresent = -7,
        ErrorFeatureNotPresent = -8,
        ErrorIncompatibleDriver = -9,
        ErrorTooManyObjects = -10,
        ErrorFormatNotSupported = -11,
        ErrorFragmentedPool = -12,
        ErrorUnknown = -13
    }

    public enum VkStructureType
    {
        ApplicationInfo = 0,
        InstanceCreateInfo = 1,
        DeviceQueueCreateInfo = 2,
        DeviceCreateInfo = 3,
        SubmitInfo = 4,
        MemoryAllocateInfo = 5,
        FenceCreateInfo = 8,
        SemaphoreCreateInfo = 9,
        BufferCreateInfo = 12,
        ImageCreateInfo = 14,
        CommandPoolCreateInfo = 39,
        CommandBufferAllocateInfo = 40,
        CommandBufferBeginInfo = 42,
        RenderPassBeginInfo = 43,
        BufferMemoryBarrier = 44,
        ImageMemoryBarrier = 45,
        MemoryBarrier = 46,
        GraphicsPipelineCreateInfo = 28
    }

    [Flags]
    public enum VkBufferUsageFlags : uint
    {
        TransferSrc = 0x00000001,
        TransferDst = 0x00000002,
        UniformTexelBuffer = 0x00000004,
        StorageTexelBuffer = 0x00000008,
        UniformBuffer = 0x00000010,
        StorageBuffer = 0x00000020,
        IndexBuffer = 0x00000040,
        VertexBuffer = 0x00000080,
        IndirectBuffer = 0x00000100
    }

    [Flags]
    public enum VkMemoryPropertyFlags : uint
    {
        DeviceLocal = 0x00000001,
        HostVisible = 0x00000002,
        HostCoherent = 0x00000004,
        HostCached = 0x00000008,
        LazilyAllocated = 0x00000010
    }

    [Flags]
    public enum VkQueueFlags : uint
    {
        Graphics = 0x00000001,
        Compute = 0x00000002,
        Transfer = 0x00000004,
        SparseBinding = 0x00000008
    }

    [Flags]
    public enum VkFenceCreateFlags : uint
    {
        None = 0,
        Signaled = 0x00000001
    }

    public enum VkCommandBufferLevel
    {
        Primary = 0,
        Secondary = 1
    }

    [Flags]
    public enum VkCommandBufferUsageFlags : uint
    {
        OneTimeSubmit = 0x00000001,
        RenderPassContinue = 0x00000002,
        SimultaneousUse = 0x00000004
    }

    public enum VkPipelineBindPoint
    {
        Graphics = 0,
        Compute = 1
    }

    public enum VkIndexType
    {
        Uint16 = 0,
        Uint32 = 1
    }

    #endregion

    #region Vulkan Structs

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct VkApplicationInfo
    {
        public VkStructureType sType;
        public void* pNext;
        public byte* pApplicationName;
        public uint applicationVersion;
        public byte* pEngineName;
        public uint engineVersion;
        public uint apiVersion;
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct VkInstanceCreateInfo
    {
        public VkStructureType sType;
        public void* pNext;
        public uint flags;
        public VkApplicationInfo* pApplicationInfo;
        public uint enabledLayerCount;
        public byte** ppEnabledLayerNames;
        public uint enabledExtensionCount;
        public byte** ppEnabledExtensionNames;
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct VkDeviceQueueCreateInfo
    {
        public VkStructureType sType;
        public void* pNext;
        public uint flags;
        public uint queueFamilyIndex;
        public uint queueCount;
        public float* pQueuePriorities;
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct VkDeviceCreateInfo
    {
        public VkStructureType sType;
        public void* pNext;
        public uint flags;
        public uint queueCreateInfoCount;
        public VkDeviceQueueCreateInfo* pQueueCreateInfos;
        public uint enabledLayerCount;
        public byte** ppEnabledLayerNames;
        public uint enabledExtensionCount;
        public byte** ppEnabledExtensionNames;
        public void* pEnabledFeatures;
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct VkBufferCreateInfo
    {
        public VkStructureType sType;
        public void* pNext;
        public uint flags;
        public ulong size;
        public VkBufferUsageFlags usage;
        public int sharingMode;
        public uint queueFamilyIndexCount;
        public uint* pQueueFamilyIndices;
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct VkMemoryAllocateInfo
    {
        public VkStructureType sType;
        public void* pNext;
        public ulong allocationSize;
        public uint memoryTypeIndex;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct VkMemoryRequirements
    {
        public ulong size;
        public ulong alignment;
        public uint memoryTypeBits;
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct VkFenceCreateInfo
    {
        public VkStructureType sType;
        public void* pNext;
        public VkFenceCreateFlags flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct VkCommandPoolCreateInfo
    {
        public VkStructureType sType;
        public void* pNext;
        public uint flags;
        public uint queueFamilyIndex;
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct VkCommandBufferAllocateInfo
    {
        public VkStructureType sType;
        public void* pNext;
        public IntPtr commandPool;
        public VkCommandBufferLevel level;
        public uint commandBufferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct VkCommandBufferBeginInfo
    {
        public VkStructureType sType;
        public void* pNext;
        public VkCommandBufferUsageFlags flags;
        public void* pInheritanceInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct VkSubmitInfo
    {
        public VkStructureType sType;
        public void* pNext;
        public uint waitSemaphoreCount;
        public IntPtr* pWaitSemaphores;
        public uint* pWaitDstStageMask;
        public uint commandBufferCount;
        public IntPtr* pCommandBuffers;
        public uint signalSemaphoreCount;
        public IntPtr* pSignalSemaphores;
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct VkMemoryType
    {
        public VkMemoryPropertyFlags propertyFlags;
        public uint heapIndex;
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct VkMemoryHeap
    {
        public ulong size;
        public uint flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct VkPhysicalDeviceMemoryProperties
    {
        public uint memoryTypeCount;
        public fixed byte memoryTypesRaw[32 * 8]; // 32 * sizeof(VkMemoryType)
        public uint memoryHeapCount;
        public fixed byte memoryHeapsRaw[16 * 16]; // 16 * sizeof(VkMemoryHeap)

        public VkMemoryType GetMemoryType(int index)
        {
            fixed (byte* ptr = memoryTypesRaw)
            {
                return ((VkMemoryType*)ptr)[index];
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct VkPhysicalDeviceProperties
    {
        public uint apiVersion;
        public uint driverVersion;
        public uint vendorID;
        public uint deviceID;
        public uint deviceType;
        public fixed byte deviceName[256];
        public fixed byte pipelineCacheUUID[16];
        public fixed byte limits[504];
        public fixed byte sparseProperties[20];

        public string GetDeviceName()
        {
            fixed (byte* ptr = deviceName)
            {
                return Marshal.PtrToStringAnsi((IntPtr)ptr) ?? "Vulkan Device";
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct VkQueueFamilyProperties
    {
        public VkQueueFlags queueFlags;
        public uint queueCount;
        public uint timestampValidBits;
        public uint minImageTransferGranularityWidth;
        public uint minImageTransferGranularityHeight;
        public uint minImageTransferGranularityDepth;
    }

    #endregion

    /// <summary>
    /// Cross-platform dynamic loader and function table for Vulkan 1.0+ runtime.
    /// Safely probes for libvulkan on Windows, Linux, and macOS without hard dependencies.
    /// </summary>
    public static unsafe class VulkanNative
    {
        #region Native Loader Helper

        private static class Loader
        {
            [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
            private static extern IntPtr LoadLibraryA(string lpFileName);

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            private static extern bool FreeLibrary(IntPtr hModule);

            [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
            private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

            [DllImport("libdl.so.2", EntryPoint = "dlopen")]
            private static extern IntPtr dlopen_linux2(string filename, int flags);

            [DllImport("libdl.so", EntryPoint = "dlopen")]
            private static extern IntPtr dlopen_linux(string filename, int flags);

            [DllImport("libdl.dylib", EntryPoint = "dlopen")]
            private static extern IntPtr dlopen_mac(string filename, int flags);

            [DllImport("libdl.so.2", EntryPoint = "dlsym")]
            private static extern IntPtr dlsym_linux2(IntPtr handle, string symbol);

            [DllImport("libdl.so", EntryPoint = "dlsym")]
            private static extern IntPtr dlsym_linux(IntPtr handle, string symbol);

            [DllImport("libdl.dylib", EntryPoint = "dlsym")]
            private static extern IntPtr dlsym_mac(IntPtr handle, string symbol);

            public static IntPtr Load(string[] names)
            {
#if NET8_0_OR_GREATER || NETCOREAPP
                foreach (var name in names)
                {
                    if (NativeLibrary.TryLoad(name, out IntPtr lib))
                        return lib;
                }
#endif
#if NET8_0_OR_GREATER || NETCOREAPP
                bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
                bool isLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
                bool isMac = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
#else
                bool isWindows = Environment.OSVersion.Platform == PlatformID.Win32NT;
                bool isLinux = Environment.OSVersion.Platform == PlatformID.Unix;
                bool isMac = Environment.OSVersion.Platform == PlatformID.MacOSX;
#endif

                foreach (var name in names)
                {
                    try
                    {
                        if (isWindows)
                        {
                            IntPtr h = LoadLibraryA(name);
                            if (h != IntPtr.Zero) return h;
                        }
                        else if (isLinux)
                        {
                            IntPtr h = IntPtr.Zero;
                            try { h = dlopen_linux2(name, 2); } catch { }
                            if (h == IntPtr.Zero) { try { h = dlopen_linux(name, 2); } catch { } }
                            if (h != IntPtr.Zero) return h;
                        }
                        else if (isMac)
                        {
                            try
                            {
                                IntPtr h = dlopen_mac(name, 2);
                                if (h != IntPtr.Zero) return h;
                            }
                            catch { }
                        }
                    }
                    catch
                    {
                        // Ignore and try next
                    }
                }
                return IntPtr.Zero;
            }

            public static IntPtr GetSymbol(IntPtr handle, string symbol)
            {
                if (handle == IntPtr.Zero) return IntPtr.Zero;

#if NET8_0_OR_GREATER || NETCOREAPP
                if (NativeLibrary.TryGetExport(handle, symbol, out IntPtr address))
                    return address;
#endif
#if NET8_0_OR_GREATER || NETCOREAPP
                bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
                bool isLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
                bool isMac = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
#else
                bool isWindows = Environment.OSVersion.Platform == PlatformID.Win32NT;
                bool isLinux = Environment.OSVersion.Platform == PlatformID.Unix;
                bool isMac = Environment.OSVersion.Platform == PlatformID.MacOSX;
#endif

                try
                {
                    if (isWindows)
                        return GetProcAddress(handle, symbol);
                    if (isLinux)
                    {
                        try { return dlsym_linux2(handle, symbol); }
                        catch { return dlsym_linux(handle, symbol); }
                    }
                    if (isMac)
                        return dlsym_mac(handle, symbol);
                }
                catch { }

                return IntPtr.Zero;
            }
        }

        #endregion

        #region Delegates

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate IntPtr PFN_vkGetInstanceProcAddr(IntPtr instance, byte* pName);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate VkResult PFN_vkCreateInstance(VkInstanceCreateInfo* pCreateInfo, void* pAllocator, out IntPtr pInstance);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void PFN_vkDestroyInstance(IntPtr instance, void* pAllocator);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate VkResult PFN_vkEnumeratePhysicalDevices(IntPtr instance, ref uint pPhysicalDeviceCount, IntPtr* pPhysicalDevices);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void PFN_vkGetPhysicalDeviceProperties(IntPtr physicalDevice, void* pProperties);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void PFN_vkGetPhysicalDeviceQueueFamilyProperties(IntPtr physicalDevice, ref uint pQueueFamilyPropertyCount, void* pQueueFamilyProperties);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void PFN_vkGetPhysicalDeviceMemoryProperties(IntPtr physicalDevice, void* pMemoryProperties);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate VkResult PFN_vkCreateDevice(IntPtr physicalDevice, VkDeviceCreateInfo* pCreateInfo, void* pAllocator, out IntPtr pDevice);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void PFN_vkDestroyDevice(IntPtr device, void* pAllocator);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void PFN_vkGetDeviceQueue(IntPtr device, uint queueFamilyIndex, uint queueIndex, out IntPtr pQueue);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate VkResult PFN_vkCreateCommandPool(IntPtr device, VkCommandPoolCreateInfo* pCreateInfo, void* pAllocator, out IntPtr pCommandPool);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void PFN_vkDestroyCommandPool(IntPtr device, IntPtr commandPool, void* pAllocator);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate VkResult PFN_vkAllocateCommandBuffers(IntPtr device, VkCommandBufferAllocateInfo* pAllocateInfo, IntPtr* pCommandBuffers);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void PFN_vkFreeCommandBuffers(IntPtr device, IntPtr commandPool, uint commandBufferCount, IntPtr* pCommandBuffers);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate VkResult PFN_vkBeginCommandBuffer(IntPtr commandBuffer, VkCommandBufferBeginInfo* pBeginInfo);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate VkResult PFN_vkEndCommandBuffer(IntPtr commandBuffer);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate VkResult PFN_vkCreateBuffer(IntPtr device, VkBufferCreateInfo* pCreateInfo, void* pAllocator, out IntPtr pBuffer);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void PFN_vkDestroyBuffer(IntPtr device, IntPtr buffer, void* pAllocator);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void PFN_vkGetBufferMemoryRequirements(IntPtr device, IntPtr buffer, out VkMemoryRequirements pMemoryRequirements);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate VkResult PFN_vkAllocateMemory(IntPtr device, VkMemoryAllocateInfo* pAllocateInfo, void* pAllocator, out IntPtr pMemory);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void PFN_vkFreeMemory(IntPtr device, IntPtr memory, void* pAllocator);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate VkResult PFN_vkBindBufferMemory(IntPtr device, IntPtr buffer, IntPtr memory, ulong memoryOffset);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate VkResult PFN_vkMapMemory(IntPtr device, IntPtr memory, ulong offset, ulong size, uint flags, void** ppData);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void PFN_vkUnmapMemory(IntPtr device, IntPtr memory);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate VkResult PFN_vkCreateFence(IntPtr device, VkFenceCreateInfo* pCreateInfo, void* pAllocator, out IntPtr pFence);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void PFN_vkDestroyFence(IntPtr device, IntPtr fence, void* pAllocator);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate VkResult PFN_vkGetFenceStatus(IntPtr device, IntPtr fence);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate VkResult PFN_vkWaitForFences(IntPtr device, uint fenceCount, IntPtr* pFences, uint waitAll, ulong timeout);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate VkResult PFN_vkResetFences(IntPtr device, uint fenceCount, IntPtr* pFences);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate VkResult PFN_vkQueueSubmit(IntPtr queue, uint submitCount, VkSubmitInfo* pSubmits, IntPtr fence);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate VkResult PFN_vkQueueWaitIdle(IntPtr queue);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void PFN_vkCmdBindVertexBuffers(IntPtr commandBuffer, uint firstBinding, uint bindingCount, IntPtr* pBuffers, ulong* pOffsets);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void PFN_vkCmdBindIndexBuffer(IntPtr commandBuffer, IntPtr buffer, ulong offset, VkIndexType indexType);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void PFN_vkCmdDraw(IntPtr commandBuffer, uint vertexCount, uint instanceCount, uint firstVertex, uint firstInstance);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void PFN_vkCmdDrawIndexed(IntPtr commandBuffer, uint indexCount, uint instanceCount, uint firstIndex, int vertexOffset, uint firstInstance);

        #endregion

        private static readonly IntPtr _libraryHandle;
        private static readonly bool _isSupported;

        public static bool IsSupported => _isSupported;

        public static readonly PFN_vkGetInstanceProcAddr? vkGetInstanceProcAddr;
        public static readonly PFN_vkCreateInstance? vkCreateInstance;
        public static readonly PFN_vkDestroyInstance? vkDestroyInstance;
        public static readonly PFN_vkEnumeratePhysicalDevices? vkEnumeratePhysicalDevices;
        public static readonly PFN_vkGetPhysicalDeviceProperties? vkGetPhysicalDeviceProperties;
        public static readonly PFN_vkGetPhysicalDeviceQueueFamilyProperties? vkGetPhysicalDeviceQueueFamilyProperties;
        public static readonly PFN_vkGetPhysicalDeviceMemoryProperties? vkGetPhysicalDeviceMemoryProperties;
        public static readonly PFN_vkCreateDevice? vkCreateDevice;
        public static readonly PFN_vkDestroyDevice? vkDestroyDevice;
        public static readonly PFN_vkGetDeviceQueue? vkGetDeviceQueue;
        public static readonly PFN_vkCreateCommandPool? vkCreateCommandPool;
        public static readonly PFN_vkDestroyCommandPool? vkDestroyCommandPool;
        public static readonly PFN_vkAllocateCommandBuffers? vkAllocateCommandBuffers;
        public static readonly PFN_vkFreeCommandBuffers? vkFreeCommandBuffers;
        public static readonly PFN_vkBeginCommandBuffer? vkBeginCommandBuffer;
        public static readonly PFN_vkEndCommandBuffer? vkEndCommandBuffer;
        public static readonly PFN_vkCreateBuffer? vkCreateBuffer;
        public static readonly PFN_vkDestroyBuffer? vkDestroyBuffer;
        public static readonly PFN_vkGetBufferMemoryRequirements? vkGetBufferMemoryRequirements;
        public static readonly PFN_vkAllocateMemory? vkAllocateMemory;
        public static readonly PFN_vkFreeMemory? vkFreeMemory;
        public static readonly PFN_vkBindBufferMemory? vkBindBufferMemory;
        public static readonly PFN_vkMapMemory? vkMapMemory;
        public static readonly PFN_vkUnmapMemory? vkUnmapMemory;
        public static readonly PFN_vkCreateFence? vkCreateFence;
        public static readonly PFN_vkDestroyFence? vkDestroyFence;
        public static readonly PFN_vkGetFenceStatus? vkGetFenceStatus;
        public static readonly PFN_vkWaitForFences? vkWaitForFences;
        public static readonly PFN_vkResetFences? vkResetFences;
        public static readonly PFN_vkQueueSubmit? vkQueueSubmit;
        public static readonly PFN_vkQueueWaitIdle? vkQueueWaitIdle;
        public static readonly PFN_vkCmdBindVertexBuffers? vkCmdBindVertexBuffers;
        public static readonly PFN_vkCmdBindIndexBuffer? vkCmdBindIndexBuffer;
        public static readonly PFN_vkCmdDraw? vkCmdDraw;
        public static readonly PFN_vkCmdDrawIndexed? vkCmdDrawIndexed;

        static VulkanNative()
        {
            string[] candidateLibs = new[]
            {
                "vulkan-1.dll",
                "libvulkan.so.1",
                "libvulkan.so",
                "libvulkan.dylib",
                "libMoltenVK.dylib"
            };

            try
            {
                _libraryHandle = Loader.Load(candidateLibs);
                if (_libraryHandle != IntPtr.Zero)
                {
                    vkGetInstanceProcAddr = GetExport<PFN_vkGetInstanceProcAddr>("vkGetInstanceProcAddr");
                    vkCreateInstance = GetExport<PFN_vkCreateInstance>("vkCreateInstance");
                    vkDestroyInstance = GetExport<PFN_vkDestroyInstance>("vkDestroyInstance");
                    vkEnumeratePhysicalDevices = GetExport<PFN_vkEnumeratePhysicalDevices>("vkEnumeratePhysicalDevices");
                    vkGetPhysicalDeviceProperties = GetExport<PFN_vkGetPhysicalDeviceProperties>("vkGetPhysicalDeviceProperties");
                    vkGetPhysicalDeviceQueueFamilyProperties = GetExport<PFN_vkGetPhysicalDeviceQueueFamilyProperties>("vkGetPhysicalDeviceQueueFamilyProperties");
                    vkGetPhysicalDeviceMemoryProperties = GetExport<PFN_vkGetPhysicalDeviceMemoryProperties>("vkGetPhysicalDeviceMemoryProperties");
                    vkCreateDevice = GetExport<PFN_vkCreateDevice>("vkCreateDevice");
                    vkDestroyDevice = GetExport<PFN_vkDestroyDevice>("vkDestroyDevice");
                    vkGetDeviceQueue = GetExport<PFN_vkGetDeviceQueue>("vkGetDeviceQueue");
                    vkCreateCommandPool = GetExport<PFN_vkCreateCommandPool>("vkCreateCommandPool");
                    vkDestroyCommandPool = GetExport<PFN_vkDestroyCommandPool>("vkDestroyCommandPool");
                    vkAllocateCommandBuffers = GetExport<PFN_vkAllocateCommandBuffers>("vkAllocateCommandBuffers");
                    vkFreeCommandBuffers = GetExport<PFN_vkFreeCommandBuffers>("vkFreeCommandBuffers");
                    vkBeginCommandBuffer = GetExport<PFN_vkBeginCommandBuffer>("vkBeginCommandBuffer");
                    vkEndCommandBuffer = GetExport<PFN_vkEndCommandBuffer>("vkEndCommandBuffer");
                    vkCreateBuffer = GetExport<PFN_vkCreateBuffer>("vkCreateBuffer");
                    vkDestroyBuffer = GetExport<PFN_vkDestroyBuffer>("vkDestroyBuffer");
                    vkGetBufferMemoryRequirements = GetExport<PFN_vkGetBufferMemoryRequirements>("vkGetBufferMemoryRequirements");
                    vkAllocateMemory = GetExport<PFN_vkAllocateMemory>("vkAllocateMemory");
                    vkFreeMemory = GetExport<PFN_vkFreeMemory>("vkFreeMemory");
                    vkBindBufferMemory = GetExport<PFN_vkBindBufferMemory>("vkBindBufferMemory");
                    vkMapMemory = GetExport<PFN_vkMapMemory>("vkMapMemory");
                    vkUnmapMemory = GetExport<PFN_vkUnmapMemory>("vkUnmapMemory");
                    vkCreateFence = GetExport<PFN_vkCreateFence>("vkCreateFence");
                    vkDestroyFence = GetExport<PFN_vkDestroyFence>("vkDestroyFence");
                    vkGetFenceStatus = GetExport<PFN_vkGetFenceStatus>("vkGetFenceStatus");
                    vkWaitForFences = GetExport<PFN_vkWaitForFences>("vkWaitForFences");
                    vkResetFences = GetExport<PFN_vkResetFences>("vkResetFences");
                    vkQueueSubmit = GetExport<PFN_vkQueueSubmit>("vkQueueSubmit");
                    vkQueueWaitIdle = GetExport<PFN_vkQueueWaitIdle>("vkQueueWaitIdle");
                    vkCmdBindVertexBuffers = GetExport<PFN_vkCmdBindVertexBuffers>("vkCmdBindVertexBuffers");
                    vkCmdBindIndexBuffer = GetExport<PFN_vkCmdBindIndexBuffer>("vkCmdBindIndexBuffer");
                    vkCmdDraw = GetExport<PFN_vkCmdDraw>("vkCmdDraw");
                    vkCmdDrawIndexed = GetExport<PFN_vkCmdDrawIndexed>("vkCmdDrawIndexed");

                    _isSupported = vkCreateInstance != null;
                }
            }
            catch
            {
                _isSupported = false;
            }
        }

        private static T? GetExport<T>(string name) where T : Delegate
        {
            IntPtr ptr = Loader.GetSymbol(_libraryHandle, name);
            if (ptr == IntPtr.Zero) return null;
            return Marshal.GetDelegateForFunctionPointer(ptr, typeof(T)) as T;
        }

        public static uint MakeApiVersion(uint variant, uint major, uint minor, uint patch)
        {
            return (variant << 29) | (major << 22) | (minor << 12) | patch;
        }
    }
}
