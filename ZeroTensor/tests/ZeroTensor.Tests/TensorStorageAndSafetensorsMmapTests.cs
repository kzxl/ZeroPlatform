using System;
using System.Collections.Generic;
using System.IO;
using Xunit;
using ZeroPrimitives.Memory;
using ZeroTensor.Core;
using ZeroTensor.Core.Storage;

namespace ZeroTensor.Tests
{
    public class TensorStorageAndSafetensorsMmapTests
    {
        [Fact]
        public void ManagedArrayStorage_BasicReadWrite()
        {
            using var storage = new ManagedArrayStorage<float>(8);
            Assert.Equal(8, storage.Length);
            Assert.Equal(DeviceType.Cpu, storage.Device);
            Assert.True(storage.IsCpuAccessible);
            Assert.False(storage.IsDisposed);

            var span = storage.AsSpan(0, 8);
            for (int i = 0; i < 8; i++) span[i] = i * 2.5f;

            var readSpan = storage.AsReadOnlySpan(0, 8);
            for (int i = 0; i < 8; i++) Assert.Equal(i * 2.5f, readSpan[i]);

            var arr = storage.TryGetArray(out int offset);
            Assert.NotNull(arr);
            Assert.Equal(0, offset);
        }

        [Fact]
        public void NativeMemoryStorage_AllocateAndWrapTensor()
        {
            using var storage = NativeMemoryStorage<float>.Allocate(12);
            Assert.Equal(12, storage.Length);
            Assert.Equal(DeviceType.Cpu, storage.Device);
            Assert.True(storage.IsCpuAccessible);
            Assert.False(storage.IsDisposed);

            var span = storage.AsSpan(0, 12);
            for (int i = 0; i < 12; i++) span[i] = (float)i;

            // Wrap into a 3x4 Tensor view
            var shape = new TensorShape(3, 4);
            var strides = TensorStrides.ComputeContiguousStrides(shape);
            var tensor = new Tensor<float>(storage, 0, shape, strides);

            Assert.Equal(3, tensor.Shape[0]);
            Assert.Equal(4, tensor.Shape[1]);
            Assert.Equal(5f, tensor[1, 1]);
            Assert.Equal(11f, tensor[2, 3]);

            // Arithmetic operation
            var doubled = tensor * 2f;
            Assert.Equal(10f, doubled[1, 1]);
            Assert.Equal(22f, doubled[2, 3]);
        }

        [Fact]
        public void FromNativeBlock_ZeroCopyExecution()
        {
            using var block = NativeMemoryBlock.Allocate(64 * sizeof(float));
            var tensor = Tensor.FromNativeBlock<float>(block, 8, 8);

            Assert.Equal(new[] { 8, 8 }, tensor.Shape.Dimensions);
            Assert.IsType<NativeMemoryStorage<float>>(tensor.Storage);

            tensor[3, 4] = 42f;
            Assert.Equal(42f, tensor[3, 4]);

            // SubTensor zero-copy slice
            var row3 = tensor.SubTensor(3);
            Assert.Equal(42f, row3[4]);
            row3[4] = 100f;
            Assert.Equal(100f, tensor[3, 4]);
        }

        [Fact]
        public void Safetensors_OpenMemoryMapped_TrueZeroCopy()
        {
            string tempFile = Path.Combine(Path.GetTempPath(), $"safetensors_test_{Guid.NewGuid():N}.safetensors");
            try
            {
                // 1. Create tensors and save to Safetensors file
                var w = Tensor.FromArray(new float[]
                {
                    1f, 2f,
                    3f, 4f
                }, 2, 2);

                var b = Tensor.FromArray(new float[] { 0.5f, -0.5f }, 2);

                var dict = new Dictionary<string, Tensor<float>>
                {
                    { "linear.weight", w },
                    { "linear.bias", b }
                };

                SafetensorsFile.Save(tempFile, dict);
                Assert.True(File.Exists(tempFile));

                // 2. Open via true zero-copy OS memory-mapping
                var loaded = SafetensorsFile.OpenMemoryMapped(tempFile);
                Assert.Equal(2, loaded.Count);

                var loadedW = loaded["linear.weight"];
                var loadedB = loaded["linear.bias"];

                // Verify backing storage is MemoryMappedStorage
                Assert.IsType<MemoryMappedStorage<float>>(loadedW.Storage);
                Assert.IsType<MemoryMappedStorage<float>>(loadedB.Storage);

                // Verify values
                Assert.Equal(1f, loadedW[0, 0]);
                Assert.Equal(2f, loadedW[0, 1]);
                Assert.Equal(3f, loadedW[1, 0]);
                Assert.Equal(4f, loadedW[1, 1]);
                Assert.Equal(0.5f, loadedB[0]);
                Assert.Equal(-0.5f, loadedB[1]);

                // 3. Compute inference using memory-mapped weights
                var x = Tensor.FromArray(new float[] { 10f, 20f }, 1, 2);
                // x @ W^T + b: [1, 2] @ [2, 2] = [1, 2]
                var y = TensorBlas.MatMul(x, loadedW) + loadedB;

                // y[0,0] = 10*1 + 20*3 + 0.5 = 70.5
                // y[0,1] = 10*2 + 20*4 - 0.5 = 99.5
                Assert.Equal(70.5f, y[0, 0]);
                Assert.Equal(99.5f, y[0, 1]);

                // Dispose tensors
                loadedW.Dispose();
                loadedB.Dispose();
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    try { File.Delete(tempFile); } catch { }
                }
            }
        }

        [Fact]
        public void BatchedMatMul_BroadcastingWithoutCopyTrap()
        {
            // A has shape [1, 2, 3] (e.g. weights shared across batch)
            var a = Tensor.FromArray(new float[]
            {
                1f, 2f, 3f,
                4f, 5f, 6f
            }, 1, 2, 3);

            // B has shape [4, 3, 2] (batch of 4 items)
            var bData = new float[4 * 3 * 2];
            for (int i = 0; i < bData.Length; i++) bData[i] = i + 1;
            var b = Tensor.FromArray(bData, 4, 3, 2);

            // Batched multiplication with broadcast: [1, 2, 3] x [4, 3, 2] -> [4, 2, 2]
            var c = TensorBlas.MatMul(a, b);

            Assert.Equal(new[] { 4, 2, 2 }, c.Shape.Dimensions);

            // Verify each batch slice independently
            var a2D = a.SubTensor(0); // [2, 3]
            for (int batch = 0; batch < 4; batch++)
            {
                var b2D = b.SubTensor(batch); // [3, 2]
                var expectedSlice = TensorBlas.MatMul2D(a2D, b2D); // [2, 2]

                var actualSlice = c.SubTensor(batch);
                for (int r = 0; r < 2; r++)
                {
                    for (int col = 0; col < 2; col++)
                    {
                        Assert.Equal(expectedSlice[r, col], actualSlice[r, col]);
                    }
                }
            }
        }

        [Fact]
        public void GgufFile_OpenMemoryMapped_ZeroCopy()
        {
            string tempFile = Path.Combine(Path.GetTempPath(), $"gguf_test_{Guid.NewGuid():N}.gguf");
            try
            {
                // Write a valid mini-GGUF v3 file
                using (var fs = new FileStream(tempFile, FileMode.Create, FileAccess.Write))
                using (var writer = new BinaryWriter(fs, System.Text.Encoding.UTF8))
                {
                    // 1. Header: magic 'GGUF', version 3, tensor_count 1, metadata_kv_count 1
                    writer.Write(0x46554747u);
                    writer.Write(3u);
                    writer.Write((ulong)1); // 1 tensor
                    writer.Write((ulong)1); // 1 metadata

                    // 2. Metadata: key "general.architecture" -> string "llama"
                    WriteGgufString(writer, "general.architecture");
                    writer.Write((uint)8); // type 8 = string
                    WriteGgufString(writer, "llama");

                    // 3. Tensor Info: "model.embed"
                    WriteGgufString(writer, "model.embed");
                    writer.Write((uint)2); // n_dims = 2
                    // GGUF dims in reverse (col, row): width=3, height=2 -> C# shape [2, 3]
                    writer.Write((ulong)3);
                    writer.Write((ulong)2);
                    writer.Write((uint)GgmlType.F32); // type 0
                    writer.Write((ulong)0); // relative offset = 0

                    // 4. Align stream to 32 bytes
                    long curPos = fs.Position;
                    long rem = curPos % 32;
                    long pad = rem == 0 ? 0 : 32 - rem;
                    for (int i = 0; i < pad; i++) writer.Write((byte)0);

                    // 5. Binary data: 2 x 3 float values
                    for (int i = 1; i <= 6; i++)
                    {
                        writer.Write((float)i * 1.5f);
                    }
                }

                // Open using GgufFile with zero-copy
                using var archive = GgufFile.OpenMemoryMapped(tempFile);
                Assert.NotNull(archive);
                Assert.Equal("llama", archive.Metadata["general.architecture"]);
                Assert.Single(archive.TensorInfos);
                Assert.Single(archive.Tensors);

                var tensor = archive.Tensors["model.embed"];
                Assert.Equal(new[] { 2, 3 }, tensor.Shape.Dimensions);
                Assert.IsType<MemoryMappedStorage<float>>(tensor.Storage);

                Assert.Equal(1.5f, tensor[0, 0]);
                Assert.Equal(3.0f, tensor[0, 1]);
                Assert.Equal(4.5f, tensor[0, 2]);
                Assert.Equal(6.0f, tensor[1, 0]);
                Assert.Equal(7.5f, tensor[1, 1]);
                Assert.Equal(9.0f, tensor[1, 2]);
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    try { File.Delete(tempFile); } catch { }
                }
            }
        }

        [Fact]
        public void DeviceMemoryStorage_TransferToDeviceAndBackToCpu()
        {
            var mockFactory = new MockDeviceStorageFactory<float>();

            // 1. Create a CPU tensor
            var cpuTensor = Tensor.FromArray(new float[] { 10f, 20f, 30f, 40f, 50f, 60f }, 2, 3);
            Assert.Equal(DeviceType.Cpu, cpuTensor.Device);
            Assert.True(cpuTensor.Storage.IsCpuAccessible);

            // 2. Allocate mock device storage and transfer
            var devStorage = mockFactory.AllocateDeviceStorage(cpuTensor.Length, DeviceType.Cuda);
            var gpuTensor = cpuTensor.ToDevice(devStorage);
            Assert.Equal(DeviceType.Cuda, gpuTensor.Device);
            Assert.False(gpuTensor.Storage.IsCpuAccessible);
            Assert.IsType<DeviceMemoryStorage<float>>(gpuTensor.Storage);
            Assert.NotEqual(IntPtr.Zero, ((DeviceMemoryStorage<float>)gpuTensor.Storage).DeviceHandle);

            // Accessing CPU span directly on device tensor must throw NotSupportedException
            Assert.Throws<NotSupportedException>(() => gpuTensor.Storage.AsSpan(0, 6));

            // 3. Transfer back to CPU
            var backToCpu = gpuTensor.ToCpu();
            Assert.Equal(DeviceType.Cpu, backToCpu.Device);
            Assert.True(backToCpu.Storage.IsCpuAccessible);
            Assert.Equal(new[] { 2, 3 }, backToCpu.Shape.Dimensions);

            Assert.Equal(10f, backToCpu[0, 0]);
            Assert.Equal(20f, backToCpu[0, 1]);
            Assert.Equal(30f, backToCpu[0, 2]);
            Assert.Equal(40f, backToCpu[1, 0]);
            Assert.Equal(50f, backToCpu[1, 1]);
            Assert.Equal(60f, backToCpu[1, 2]);

            // Dispose device tensor
            gpuTensor.Dispose();
            Assert.True(gpuTensor.Storage.IsDisposed);
        }

        private class MockDeviceStorageFactory<T> where T : unmanaged, IEquatable<T>
        {
            private readonly Dictionary<IntPtr, T[]> _deviceSimulatedMemory = new Dictionary<IntPtr, T[]>();
            private long _nextHandle = 0x1000;

            public DeviceMemoryStorage<T> AllocateDeviceStorage(int length, DeviceType device)
            {
                var handle = new IntPtr(++_nextHandle);
                var simulatedBuffer = new T[length];
                _deviceSimulatedMemory[handle] = simulatedBuffer;

                return new DeviceMemoryStorage<T>(
                    device,
                    length,
                    handle,
                    copyToHost: (offset, len, dest) => simulatedBuffer.AsSpan(offset, len).CopyTo(dest),
                    copyFromHost: (offset, len, src) => src.CopyTo(simulatedBuffer.AsSpan(offset, len)),
                    onDispose: () => _deviceSimulatedMemory.Remove(handle)
                );
            }
        }

        [Fact]
        public void GgufFile_DequantizeQ4_0_And_Q8_0()
        {
            string tempFile = Path.Combine(Path.GetTempPath(), $"gguf_quant_test_{Guid.NewGuid():N}.gguf");
            try
            {
                using (var fs = new FileStream(tempFile, FileMode.Create, FileAccess.Write))
                using (var writer = new BinaryWriter(fs, System.Text.Encoding.UTF8))
                {
                    // 1. Header: magic 'GGUF', version 3, tensor_count 2, metadata_kv_count 0
                    writer.Write(0x46554747u);
                    writer.Write(3u);
                    writer.Write((ulong)2); // 2 tensors
                    writer.Write((ulong)0); // 0 metadata

                    // 2. Tensor Info 1: "weight.q4" (1D size 32)
                    WriteGgufString(writer, "weight.q4");
                    writer.Write((uint)1); // 1 dim
                    writer.Write((ulong)32); // length 32
                    writer.Write((uint)GgmlType.Q4_0);
                    writer.Write((ulong)0); // rel offset = 0

                    // 3. Tensor Info 2: "weight.q8" (1D size 32)
                    WriteGgufString(writer, "weight.q8");
                    writer.Write((uint)1); // 1 dim
                    writer.Write((ulong)32); // length 32
                    writer.Write((uint)GgmlType.Q8_0);
                    // Q4_0 block is 18 bytes -> align to 32 bytes gives offset 32
                    writer.Write((ulong)32);

                    // 4. Align header to 32 bytes
                    long curPos = fs.Position;
                    long pad1 = (32 - (curPos % 32)) % 32;
                    for (int i = 0; i < pad1; i++) writer.Write((byte)0);

                    // 5. Binary Payload:
                    // Block 1 (Q4_0, 18 bytes):
                    // Scale = 0.5f (Half 0x3800)
                    writer.Write((ushort)0x3800);
                    // 16 bytes: byte 0 = 0x9A ((low=10-8=2)*0.5 = 1.0f, (high=9-8=1)*0.5 = 0.5f)
                    writer.Write((byte)0x9A);
                    for (int i = 1; i < 16; i++) writer.Write((byte)0x88); // 8-8 = 0

                    // Pad between block 1 and block 2 (18 bytes -> 32 bytes: 14 bytes padding)
                    for (int i = 0; i < 14; i++) writer.Write((byte)0);

                    // Block 2 (Q8_0, 34 bytes at offset 32):
                    // Scale = 2.0f (Half 0x4000)
                    writer.Write((ushort)0x4000);
                    // 32 signed bytes
                    writer.Write((sbyte)5);
                    writer.Write((sbyte)-3);
                    for (int i = 2; i < 32; i++) writer.Write((sbyte)0);
                }

                // Open archive and dequantize
                using var archive = GgufFile.OpenMemoryMapped(tempFile);
                Assert.NotNull(archive);

                // Test Q4_0 dequantization
                var tQ4 = archive.GetFloatTensor("weight.q4");
                Assert.Equal(32, tQ4.Length);
                Assert.Equal(1.0f, tQ4[0]);
                Assert.Equal(0.5f, tQ4[16]);
                Assert.Equal(0.0f, tQ4[1]);

                // Test Q8_0 dequantization
                var tQ8 = archive.GetFloatTensor("weight.q8");
                Assert.Equal(32, tQ8.Length);
                Assert.Equal(10.0f, tQ8[0]);
                Assert.Equal(-6.0f, tQ8[1]);
                Assert.Equal(0.0f, tQ8[2]);
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    try { File.Delete(tempFile); } catch { }
                }
            }
        }

        private static void WriteGgufString(BinaryWriter writer, string s)
        {
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(s);
            writer.Write((ulong)bytes.Length);
            writer.Write(bytes);
        }
    }
}
