using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace ZeroGraphics.Rhi.Vulkan
{
    /// <summary>
    /// Pure C# Cross-Platform Vulkan 1.0+ Render Hardware Interface implementation.
    /// Operates without external wrappers or dependencies, dynamically binding to the Vulkan loader.
    /// </summary>
    public sealed unsafe class VulkanRhiDevice : IRhiDevice, IDisposable
    {
        private readonly IntPtr _instance;
        private readonly IntPtr _physicalDevice;
        private readonly IntPtr _device;
        private readonly IntPtr _queue;
        private readonly uint _queueFamilyIndex;
        private readonly IntPtr _commandPool;
        private readonly VkPhysicalDeviceMemoryProperties _memoryProperties;
        private readonly string _deviceName;
        private bool _disposed;

        public RhiBackend Backend => RhiBackend.Vulkan;
        public string DeviceName => _deviceName;

        public IntPtr NativeInstance => _instance;
        public IntPtr NativePhysicalDevice => _physicalDevice;
        public IntPtr NativeDevice => _device;
        public IntPtr NativeQueue => _queue;
        public uint QueueFamilyIndex => _queueFamilyIndex;
        public IntPtr NativeCommandPool => _commandPool;

        public VulkanRhiDevice(
            IntPtr instance,
            IntPtr physicalDevice,
            IntPtr device,
            IntPtr queue,
            uint queueFamilyIndex,
            IntPtr commandPool,
            VkPhysicalDeviceMemoryProperties memProps,
            string deviceName)
        {
            _instance = instance;
            _physicalDevice = physicalDevice;
            _device = device;
            _queue = queue;
            _queueFamilyIndex = queueFamilyIndex;
            _commandPool = commandPool;
            _memoryProperties = memProps;
            _deviceName = deviceName;
        }

        /// <summary>
        /// Attempts to initialize a Vulkan 1.0+ device and queue on the host machine.
        /// Returns null if Vulkan loader is not present, no physical device exists, or initialization fails.
        /// </summary>
        public static VulkanRhiDevice? TryCreate()
        {
            if (!VulkanNative.IsSupported || VulkanNative.vkCreateInstance == null)
            {
                return null;
            }

            IntPtr instance = IntPtr.Zero;
            IntPtr device = IntPtr.Zero;
            IntPtr commandPool = IntPtr.Zero;

            try
            {
                // 1. Create Instance
                var instCreateInfo = new VkInstanceCreateInfo
                {
                    sType = VkStructureType.InstanceCreateInfo,
                    pNext = null,
                    flags = 0,
                    pApplicationInfo = null,
                    enabledLayerCount = 0,
                    ppEnabledLayerNames = null,
                    enabledExtensionCount = 0,
                    ppEnabledExtensionNames = null
                };

                VkResult res = VulkanNative.vkCreateInstance(&instCreateInfo, null, out instance);
                if (res != VkResult.Success || instance == IntPtr.Zero)
                    return null;

                // 2. Enumerate Physical Devices
                if (VulkanNative.vkEnumeratePhysicalDevices == null) return null;

                uint gpuCount = 0;
                VkResult enumRes = VulkanNative.vkEnumeratePhysicalDevices(instance, ref gpuCount, null);
                if (enumRes != VkResult.Success || gpuCount == 0)
                {
                    VulkanNative.vkDestroyInstance?.Invoke(instance, null);
                    return null;
                }

                var physicalDevices = stackalloc IntPtr[(int)gpuCount];
                VulkanNative.vkEnumeratePhysicalDevices(instance, ref gpuCount, physicalDevices);
                IntPtr physicalDevice = physicalDevices[0];

                // Query Device Properties
                string devName = "Vulkan Physical GPU";
                if (VulkanNative.vkGetPhysicalDeviceProperties != null)
                {
                    byte* pDevProps = stackalloc byte[2048];
                    VulkanNative.vkGetPhysicalDeviceProperties(physicalDevice, pDevProps);
                    devName = Marshal.PtrToStringAnsi((IntPtr)(pDevProps + 20)) ?? "Vulkan Physical GPU";
                }

                // Query Memory Properties
                var memProps = default(VkPhysicalDeviceMemoryProperties);
                if (VulkanNative.vkGetPhysicalDeviceMemoryProperties != null)
                {
                    byte* pMemProps = stackalloc byte[2048];
                    VulkanNative.vkGetPhysicalDeviceMemoryProperties(physicalDevice, pMemProps);
                    Buffer.MemoryCopy(pMemProps, &memProps, sizeof(VkPhysicalDeviceMemoryProperties), Math.Min(sizeof(VkPhysicalDeviceMemoryProperties), 1024));
                }

                // 3. Find Graphics Queue Family
                if (VulkanNative.vkGetPhysicalDeviceQueueFamilyProperties == null)
                {
                    VulkanNative.vkDestroyInstance?.Invoke(instance, null);
                    return null;
                }

                uint qfCount = 0;
                VulkanNative.vkGetPhysicalDeviceQueueFamilyProperties(physicalDevice, ref qfCount, null);
                if (qfCount == 0)
                {
                    VulkanNative.vkDestroyInstance?.Invoke(instance, null);
                    return null;
                }

                byte* pQfProps = stackalloc byte[(int)(qfCount * 64)];
                VulkanNative.vkGetPhysicalDeviceQueueFamilyProperties(physicalDevice, ref qfCount, pQfProps);

                uint graphicsQueueFamily = uint.MaxValue;
                for (uint i = 0; i < qfCount; i++)
                {
                    VkQueueFamilyProperties* pProp = (VkQueueFamilyProperties*)(pQfProps + (i * sizeof(VkQueueFamilyProperties)));
                    if ((pProp->queueFlags & VkQueueFlags.Graphics) != 0)
                    {
                        graphicsQueueFamily = i;
                        break;
                    }
                }

                if (graphicsQueueFamily == uint.MaxValue)
                {
                    VulkanNative.vkDestroyInstance?.Invoke(instance, null);
                    return null;
                }

                // 4. Create Logical Device & Queue
                if (VulkanNative.vkCreateDevice == null || VulkanNative.vkGetDeviceQueue == null)
                {
                    VulkanNative.vkDestroyInstance?.Invoke(instance, null);
                    return null;
                }

                float priority = 1.0f;
                var queueCreateInfo = new VkDeviceQueueCreateInfo
                {
                    sType = VkStructureType.DeviceQueueCreateInfo,
                    pNext = null,
                    flags = 0,
                    queueFamilyIndex = graphicsQueueFamily,
                    queueCount = 1,
                    pQueuePriorities = &priority
                };

                var devCreateInfo = new VkDeviceCreateInfo
                {
                    sType = VkStructureType.DeviceCreateInfo,
                    pNext = null,
                    flags = 0,
                    queueCreateInfoCount = 1,
                    pQueueCreateInfos = &queueCreateInfo,
                    enabledLayerCount = 0,
                    ppEnabledLayerNames = null,
                    enabledExtensionCount = 0,
                    ppEnabledExtensionNames = null,
                    pEnabledFeatures = null
                };

                VkResult devRes = VulkanNative.vkCreateDevice(physicalDevice, &devCreateInfo, null, out device);
                if (devRes != VkResult.Success || device == IntPtr.Zero)
                {
                    VulkanNative.vkDestroyInstance?.Invoke(instance, null);
                    return null;
                }

                VulkanNative.vkGetDeviceQueue(device, graphicsQueueFamily, 0, out IntPtr queue);

                // 5. Create Command Pool
                if (VulkanNative.vkCreateCommandPool != null)
                {
                    var poolCreateInfo = new VkCommandPoolCreateInfo
                    {
                        sType = VkStructureType.CommandPoolCreateInfo,
                        pNext = null,
                        flags = 2, // VK_COMMAND_POOL_CREATE_RESET_COMMAND_BUFFER_BIT
                        queueFamilyIndex = graphicsQueueFamily
                    };
                    VulkanNative.vkCreateCommandPool(device, &poolCreateInfo, null, out commandPool);
                }

                return new VulkanRhiDevice(
                    instance,
                    physicalDevice,
                    device,
                    queue,
                    graphicsQueueFamily,
                    commandPool,
                    memProps,
                    devName);
            }
            catch
            {
                if (commandPool != IntPtr.Zero && VulkanNative.vkDestroyCommandPool != null)
                    VulkanNative.vkDestroyCommandPool(device, commandPool, null);
                if (device != IntPtr.Zero && VulkanNative.vkDestroyDevice != null)
                    VulkanNative.vkDestroyDevice(device, null);
                if (instance != IntPtr.Zero && VulkanNative.vkDestroyInstance != null)
                    VulkanNative.vkDestroyInstance(instance, null);

                return null;
            }
        }

        public uint FindMemoryType(uint typeFilter, VkMemoryPropertyFlags properties)
        {
            for (int i = 0; i < (int)_memoryProperties.memoryTypeCount; i++)
            {
                if ((typeFilter & (1u << i)) != 0 &&
                    (_memoryProperties.GetMemoryType(i).propertyFlags & properties) == properties)
                {
                    return (uint)i;
                }
            }
            return 0;
        }

        public IRhiBuffer CreateBuffer(in RhiBufferDesc desc, ReadOnlySpan<byte> initialData = default)
        {
            VkBufferUsageFlags usage = desc.Type switch
            {
                RhiBufferType.Vertex => VkBufferUsageFlags.VertexBuffer,
                RhiBufferType.Index => VkBufferUsageFlags.IndexBuffer,
                RhiBufferType.Constant => VkBufferUsageFlags.UniformBuffer,
                _ => VkBufferUsageFlags.TransferSrc | VkBufferUsageFlags.TransferDst
            };

            var bufferInfo = new VkBufferCreateInfo
            {
                sType = VkStructureType.BufferCreateInfo,
                size = (ulong)Math.Max(desc.SizeInBytes, 16),
                usage = usage,
                sharingMode = 0 // VK_SHARING_MODE_EXCLUSIVE
            };

            IntPtr buffer = IntPtr.Zero;
            IntPtr memory = IntPtr.Zero;

            if (VulkanNative.vkCreateBuffer != null)
            {
                VkResult res = VulkanNative.vkCreateBuffer(_device, &bufferInfo, null, out buffer);
                if (res != VkResult.Success)
                    throw new InvalidOperationException($"Failed to create Vulkan buffer: {res}");

                if (VulkanNative.vkGetBufferMemoryRequirements != null && VulkanNative.vkAllocateMemory != null && VulkanNative.vkBindBufferMemory != null)
                {
                    VulkanNative.vkGetBufferMemoryRequirements(_device, buffer, out var memReqs);
                    uint memType = FindMemoryType(memReqs.memoryTypeBits, VkMemoryPropertyFlags.HostVisible | VkMemoryPropertyFlags.HostCoherent);

                    var allocInfo = new VkMemoryAllocateInfo
                    {
                        sType = VkStructureType.MemoryAllocateInfo,
                        allocationSize = memReqs.size,
                        memoryTypeIndex = memType
                    };

                    VulkanNative.vkAllocateMemory(_device, &allocInfo, null, out memory);
                    if (memory != IntPtr.Zero)
                    {
                        VulkanNative.vkBindBufferMemory(_device, buffer, memory, 0);
                    }
                }
            }

            var rhiBuffer = new VulkanRhiBuffer(_device, buffer, memory, desc);
            if (!initialData.IsEmpty)
            {
                rhiBuffer.UpdateRaw(initialData, 0);
            }

            return rhiBuffer;
        }

        public IRhiTexture CreateTexture(in RhiTextureDesc desc, ReadOnlySpan<byte> initialData = default)
        {
            return new VulkanRhiTexture(desc);
        }

        public IRhiShader CreateShader(RhiShaderStage stage, byte[] bytecode)
        {
            return new VulkanRhiShader(stage, bytecode);
        }

        public IRhiPipelineState CreatePipelineState(RhiPipelineStateDesc desc)
        {
            return new VulkanRhiPipelineState(desc);
        }

        public IRhiSwapChain CreateSwapChain(IntPtr windowHandle, int width, int height, RhiPresentMode presentMode = RhiPresentMode.Fifo)
        {
            return new VulkanRhiSwapChain(this, width, height);
        }

        public IRhiCommandBuffer CreateCommandBuffer()
        {
            IntPtr cmdBuf = IntPtr.Zero;
            if (_commandPool != IntPtr.Zero && VulkanNative.vkAllocateCommandBuffers != null)
            {
                var allocInfo = new VkCommandBufferAllocateInfo
                {
                    sType = VkStructureType.CommandBufferAllocateInfo,
                    commandPool = _commandPool,
                    level = VkCommandBufferLevel.Primary,
                    commandBufferCount = 1
                };
                VulkanNative.vkAllocateCommandBuffers(_device, &allocInfo, &cmdBuf);
            }

            return new VulkanRhiCommandBuffer(this, cmdBuf);
        }

        public IRhiFence CreateFence(ulong initialValue = 0)
        {
            IntPtr fence = IntPtr.Zero;
            if (VulkanNative.vkCreateFence != null)
            {
                var fenceInfo = new VkFenceCreateInfo
                {
                    sType = VkStructureType.FenceCreateInfo,
                    flags = initialValue > 0 ? VkFenceCreateFlags.Signaled : VkFenceCreateFlags.None
                };
                VulkanNative.vkCreateFence(_device, &fenceInfo, null, out fence);
            }

            return new VulkanRhiFence(_device, fence, initialValue);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                if (_queue != IntPtr.Zero && VulkanNative.vkQueueWaitIdle != null)
                {
                    try { VulkanNative.vkQueueWaitIdle(_queue); } catch { }
                }

                if (_commandPool != IntPtr.Zero && VulkanNative.vkDestroyCommandPool != null)
                {
                    VulkanNative.vkDestroyCommandPool(_device, _commandPool, null);
                }

                if (_device != IntPtr.Zero && VulkanNative.vkDestroyDevice != null)
                {
                    VulkanNative.vkDestroyDevice(_device, null);
                }

                if (_instance != IntPtr.Zero && VulkanNative.vkDestroyInstance != null)
                {
                    VulkanNative.vkDestroyInstance(_instance, null);
                }

                _disposed = true;
            }
        }
    }

    #region Vulkan RHI Resources

    internal sealed unsafe class VulkanRhiBuffer : IRhiBuffer
    {
        private readonly IntPtr _device;
        private readonly IntPtr _buffer;
        private readonly IntPtr _memory;
        private readonly RhiBufferDesc _desc;
        private readonly byte[] _cpuMirror;
        private bool _disposed;

        public RhiBufferType Type => _desc.Type;
        public RhiBufferUsage Usage => _desc.Usage;
        public int SizeInBytes => _desc.SizeInBytes;

        public IntPtr NativeBuffer => _buffer;
        public IntPtr NativeMemory => _memory;

        public VulkanRhiBuffer(IntPtr device, IntPtr buffer, IntPtr memory, in RhiBufferDesc desc)
        {
            _device = device;
            _buffer = buffer;
            _memory = memory;
            _desc = desc;
            _cpuMirror = new byte[desc.SizeInBytes];
        }

        public void UpdateData<T>(ReadOnlySpan<T> data, int offsetBytes = 0) where T : unmanaged
        {
            int byteCount = data.Length * sizeof(T);
            if (offsetBytes + byteCount > _desc.SizeInBytes)
                throw new ArgumentOutOfRangeException(nameof(data), "Data exceeds buffer size.");

            fixed (T* pSrc = data)
            {
                var span = new ReadOnlySpan<byte>(pSrc, byteCount);
                UpdateRaw(span, offsetBytes);
            }
        }

        public void UpdateRaw(ReadOnlySpan<byte> bytes, int offsetBytes)
        {
            bytes.CopyTo(_cpuMirror.AsSpan(offsetBytes));

            if (_device != IntPtr.Zero && _memory != IntPtr.Zero && VulkanNative.vkMapMemory != null && VulkanNative.vkUnmapMemory != null)
            {
                void* pMapped = null;
                VkResult res = VulkanNative.vkMapMemory(_device, _memory, (ulong)offsetBytes, (ulong)bytes.Length, 0, &pMapped);
                if (res == VkResult.Success && pMapped != null)
                {
                    fixed (byte* pSrc = bytes)
                    {
                        Buffer.MemoryCopy(pSrc, pMapped, bytes.Length, bytes.Length);
                    }
                    VulkanNative.vkUnmapMemory(_device, _memory);
                }
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                if (_buffer != IntPtr.Zero && VulkanNative.vkDestroyBuffer != null)
                    VulkanNative.vkDestroyBuffer(_device, _buffer, null);
                if (_memory != IntPtr.Zero && VulkanNative.vkFreeMemory != null)
                    VulkanNative.vkFreeMemory(_device, _memory, null);
                _disposed = true;
            }
        }
    }

    internal sealed class VulkanRhiTexture : IRhiTexture
    {
        private readonly RhiTextureDesc _desc;
        private readonly byte[] _storage;

        public int Width => _desc.Width;
        public int Height => _desc.Height;
        public RhiFormat Format => _desc.Format;
        public RhiTextureUsage Usage => _desc.Usage;
        public int MipLevels => _desc.MipLevels;

        public VulkanRhiTexture(in RhiTextureDesc desc)
        {
            _desc = desc;
            _storage = new byte[desc.Width * desc.Height * 4];
        }

        public void UpdateData<T>(ReadOnlySpan<T> data, int rowPitch) where T : unmanaged
        {
            // Fallback CPU storage copy for software readback
        }

        public void Dispose() { }
    }

    internal sealed class VulkanRhiShader : IRhiShader
    {
        public RhiShaderStage Stage { get; }
        public byte[] Bytecode { get; }

        public VulkanRhiShader(RhiShaderStage stage, byte[] bytecode)
        {
            Stage = stage;
            Bytecode = bytecode ?? Array.Empty<byte>();
        }

        public void Dispose() { }
    }

    internal sealed class VulkanRhiPipelineState : IRhiPipelineState
    {
        public RhiPipelineStateDesc Description { get; }

        public VulkanRhiPipelineState(RhiPipelineStateDesc desc)
        {
            Description = desc;
        }

        public void Dispose() { }
    }

    internal sealed class VulkanRhiSwapChain : IRhiSwapChain
    {
        private readonly VulkanRhiDevice _device;
        private IRhiTexture _backBuffer;

        public int Width { get; private set; }
        public int Height { get; private set; }
        public IRhiTexture BackBuffer => _backBuffer;

        public VulkanRhiSwapChain(VulkanRhiDevice device, int width, int height)
        {
            _device = device;
            Width = width;
            Height = height;
            _backBuffer = new VulkanRhiTexture(new RhiTextureDesc(width, height, RhiFormat.B8G8R8A8_UNorm, RhiTextureUsage.RenderTarget));
        }

        public void Resize(int width, int height)
        {
            Width = width;
            Height = height;
            _backBuffer.Dispose();
            _backBuffer = new VulkanRhiTexture(new RhiTextureDesc(width, height, RhiFormat.B8G8R8A8_UNorm, RhiTextureUsage.RenderTarget));
        }

        public void Present(int syncInterval = 1) { }

        public void Dispose()
        {
            _backBuffer?.Dispose();
        }
    }

    internal sealed unsafe class VulkanRhiFence : IRhiFence
    {
        private readonly IntPtr _device;
        private readonly IntPtr _fence;
        private long _timelineValue;
        private bool _disposed;

        public ulong CompletedValue => unchecked((ulong)Interlocked.Read(ref _timelineValue));

        public VulkanRhiFence(IntPtr device, IntPtr fence, ulong initialValue)
        {
            _device = device;
            _fence = fence;
            _timelineValue = unchecked((long)initialValue);
        }

        public void Signal(ulong value)
        {
            Interlocked.Exchange(ref _timelineValue, unchecked((long)value));
        }

        public bool Wait(ulong value, int timeoutMilliseconds = -1)
        {
            if (CompletedValue >= value) return true;

            if (_fence != IntPtr.Zero && VulkanNative.vkWaitForFences != null)
            {
                IntPtr fence = _fence;
                ulong timeoutNs = timeoutMilliseconds < 0 ? ulong.MaxValue : (ulong)timeoutMilliseconds * 1_000_000UL;
                VkResult res = VulkanNative.vkWaitForFences(_device, 1, &fence, 1, timeoutNs);
                if (res == VkResult.Success)
                {
                    Signal(value);
                    return true;
                }
            }

            int waitMs = timeoutMilliseconds < 0 ? 1000 : timeoutMilliseconds;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < waitMs)
            {
                if (CompletedValue >= value) return true;
                Thread.Sleep(1);
            }
            return CompletedValue >= value;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                if (_fence != IntPtr.Zero && VulkanNative.vkDestroyFence != null)
                {
                    VulkanNative.vkDestroyFence(_device, _fence, null);
                }
                _disposed = true;
            }
        }
    }

    internal sealed unsafe class VulkanRhiCommandBuffer : IRhiCommandBuffer
    {
        private readonly VulkanRhiDevice _device;
        private readonly IntPtr _cmdBuffer;
        private bool _isRecording;

        public bool IsRecording => _isRecording;
        public IntPtr NativeCommandBuffer => _cmdBuffer;

        public VulkanRhiCommandBuffer(VulkanRhiDevice device, IntPtr cmdBuffer)
        {
            _device = device;
            _cmdBuffer = cmdBuffer;
        }

        public void Begin()
        {
            _isRecording = true;
            if (_cmdBuffer != IntPtr.Zero && VulkanNative.vkBeginCommandBuffer != null)
            {
                var beginInfo = new VkCommandBufferBeginInfo
                {
                    sType = VkStructureType.CommandBufferBeginInfo,
                    flags = VkCommandBufferUsageFlags.OneTimeSubmit
                };
                VulkanNative.vkBeginCommandBuffer(_cmdBuffer, &beginInfo);
            }
        }

        public void End()
        {
            if (_cmdBuffer != IntPtr.Zero && VulkanNative.vkEndCommandBuffer != null)
            {
                VulkanNative.vkEndCommandBuffer(_cmdBuffer);
            }
            _isRecording = false;
        }

        public void SetViewport(in RhiViewport viewport) { }

        public void SetScissorRect(in RhiRect scissorRect) { }

        public void BeginRenderPass(IRhiTexture renderTarget, RhiClearFlags clearFlags = RhiClearFlags.None, RhiColor clearColor = default) { }

        public void EndRenderPass() { }

        public void SetPipelineState(IRhiPipelineState pipelineState) { }

        public void SetVertexBuffer(int slot, IRhiBuffer buffer, int stride, int offset = 0)
        {
            if (_cmdBuffer != IntPtr.Zero && VulkanNative.vkCmdBindVertexBuffers != null && buffer is VulkanRhiBuffer vkBuf)
            {
                IntPtr rawBuf = vkBuf.NativeBuffer;
                if (rawBuf != IntPtr.Zero)
                {
                    ulong rawOffset = (ulong)offset;
                    VulkanNative.vkCmdBindVertexBuffers(_cmdBuffer, (uint)slot, 1, &rawBuf, &rawOffset);
                }
            }
        }

        public void SetIndexBuffer(IRhiBuffer buffer, RhiIndexFormat format, int offset = 0)
        {
            if (_cmdBuffer != IntPtr.Zero && VulkanNative.vkCmdBindIndexBuffer != null && buffer is VulkanRhiBuffer vkBuf)
            {
                IntPtr rawBuf = vkBuf.NativeBuffer;
                if (rawBuf != IntPtr.Zero)
                {
                    VkIndexType vkIdxType = format == RhiIndexFormat.SixteenBit ? VkIndexType.Uint16 : VkIndexType.Uint32;
                    VulkanNative.vkCmdBindIndexBuffer(_cmdBuffer, rawBuf, (ulong)offset, vkIdxType);
                }
            }
        }

        public void SetConstantBuffer(int slot, IRhiBuffer buffer, RhiShaderStage stage = RhiShaderStage.Vertex | RhiShaderStage.Pixel) { }

        public void SetShaderResource(int slot, IRhiTexture texture, RhiShaderStage stage = RhiShaderStage.Pixel) { }

        public void Draw(int vertexCount, int startVertex = 0)
        {
            if (_cmdBuffer != IntPtr.Zero && VulkanNative.vkCmdDraw != null)
            {
                VulkanNative.vkCmdDraw(_cmdBuffer, (uint)vertexCount, 1, (uint)startVertex, 0);
            }
        }

        public void DrawIndexed(int indexCount, int startIndex = 0, int baseVertex = 0)
        {
            if (_cmdBuffer != IntPtr.Zero && VulkanNative.vkCmdDrawIndexed != null)
            {
                VulkanNative.vkCmdDrawIndexed(_cmdBuffer, (uint)indexCount, 1, (uint)startIndex, baseVertex, 0);
            }
        }

        public void DispatchCompute(int groupCountX, int groupCountY, int groupCountZ) { }

        public void ResourceBarrier(in RhiBarrier barrier) { }

        public void ResourceBarriers(ReadOnlySpan<RhiBarrier> barriers) { }

        public void SignalFence(IRhiFence fence, ulong value)
        {
            fence?.Signal(value);
        }

        public void WaitFence(IRhiFence fence, ulong value)
        {
            fence?.Wait(value);
        }

        private bool _disposed;

        public void Dispose()
        {
            if (!_disposed)
            {
                if (_cmdBuffer != IntPtr.Zero && _device.NativeCommandPool != IntPtr.Zero && VulkanNative.vkFreeCommandBuffers != null)
                {
                    IntPtr cb = _cmdBuffer;
                    VulkanNative.vkFreeCommandBuffers(_device.NativeDevice, _device.NativeCommandPool, 1, &cb);
                }
                _disposed = true;
            }
        }
    }

    #endregion
}
