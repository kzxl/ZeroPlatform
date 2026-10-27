using System;

namespace ZeroTensor.Core
{
    /// <summary>
    /// Static factory methods for creating and initializing tensors.
    /// </summary>
    public static partial class Tensor
    {
        /// <summary>
        /// Creates a tensor initialized with data for the specified shape.
        /// </summary>
        public static Tensor<T> Create<T>(int[] shape, T[] data) where T : unmanaged, IEquatable<T>
        {
            return FromArray(data, shape);
        }

        /// <summary>
        /// Creates a tensor with the specified shape initialized to zeros.
        /// </summary>
        public static Tensor<T> Create<T>(params int[] shape) where T : unmanaged, IEquatable<T>
        {
            return Zeros<T>(shape);
        }

        /// <summary>
        /// Creates a tensor filled with zeros.
        /// </summary>
        public static Tensor<T> Zeros<T>(params int[] shape) where T : unmanaged, IEquatable<T>
        {
            return new Tensor<T>(new TensorShape(shape));
        }

        /// <summary>
        /// Creates a tensor filled with ones.
        /// </summary>
        public static Tensor<float> Ones(params int[] shape) => Full(1f, shape);

        /// <summary>
        /// Creates a tensor filled with ones of type T.
        /// </summary>
        public static Tensor<T> Ones<T>(params int[] shape) where T : unmanaged, IEquatable<T>
        {
            var tensor = new Tensor<T>(new TensorShape(shape));
            T oneVal = (T)Convert.ChangeType(1, typeof(T));
            tensor.Fill(oneVal);
            return tensor;
        }

        /// <summary>
        /// Creates a tensor filled with a specified value.
        /// </summary>
        public static Tensor<T> Full<T>(T value, params int[] shape) where T : unmanaged, IEquatable<T>
        {
            var tensor = new Tensor<T>(new TensorShape(shape));
            tensor.Fill(value);
            return tensor;
        }

        /// <summary>
        /// Creates an O(1) zero-copy tensor view over an existing array buffer with custom offset, shape, and strides.
        /// Ideal for interop with camera buffers, DMA memory, and GPU staging textures.
        /// </summary>
        public static Tensor<T> CreateView<T>(T[] buffer, int offset, TensorShape shape, int[] strides) where T : unmanaged, IEquatable<T>
        {
            return new Tensor<T>(buffer, offset, shape, strides);
        }

        /// <summary>
        /// Creates an O(1) zero-copy tensor view over any abstract ITensorStorage backend with custom offset, shape, and strides.
        /// </summary>
        public static Tensor<T> CreateView<T>(Storage.ITensorStorage<T> storage, int offset, TensorShape shape, int[] strides) where T : unmanaged, IEquatable<T>
        {
            return new Tensor<T>(storage, offset, shape, strides);
        }

        /// <summary>
        /// Creates a tensor by wrapping or copying a flat array into the specified shape.
        /// </summary>
        public static Tensor<T> FromArray<T>(T[] data, params int[] shape) where T : unmanaged, IEquatable<T>
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (shape == null || shape.Length == 0)
            {
                shape = new[] { data.Length };
            }

            var tensorShape = new TensorShape(shape);

            if (data.Length != tensorShape.TotalElements)
            {
                throw new ArgumentException($"Data array length {data.Length} does not match shape total elements {tensorShape.TotalElements}.", nameof(data));
            }

            var tensor = new Tensor<T>(tensorShape);
            Array.Copy(data, tensor.Buffer, data.Length);
            return tensor;
        }

        /// <summary>
        /// Creates a 2D tensor from a 2D array.
        /// </summary>
        public static Tensor<T> From2DArray<T>(T[,] data) where T : unmanaged, IEquatable<T>
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            int rows = data.GetLength(0);
            int cols = data.GetLength(1);

            var tensor = new Tensor<T>(rows, cols);
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    tensor[r, c] = data[r, c];
                }
            }

            return tensor;
        }

        /// <summary>
        /// Creates a 1D tensor with values evenly spaced within a given half-open interval [start, stop) with step.
        /// </summary>
        public static Tensor<float> Arange(float start, float stop, float step = 1.0f)
        {
            if (step == 0) throw new ArgumentException("Step cannot be zero.", nameof(step));
            if ((step > 0 && start >= stop) || (step < 0 && start <= stop))
            {
                return new Tensor<float>(0);
            }

            int count = (int)Math.Ceiling((stop - start) / step);
            var tensor = new Tensor<float>(count);
            for (int i = 0; i < count; i++)
            {
                tensor[i] = start + i * step;
            }

            return tensor;
        }

        /// <summary>
        /// Creates a 1D tensor with 'num' evenly spaced numbers over the closed interval [start, stop].
        /// </summary>
        public static Tensor<float> Linspace(float start, float stop, int num)
        {
            if (num < 0) throw new ArgumentOutOfRangeException(nameof(num), "Number of samples must be non-negative.");
            if (num == 0) return new Tensor<float>(0);
            if (num == 1) return FromArray(new[] { start }, 1);

            var tensor = new Tensor<float>(num);
            float step = (stop - start) / (num - 1);

            for (int i = 0; i < num; i++)
            {
                tensor[i] = i == num - 1 ? stop : start + i * step;
            }

            return tensor;
        }

        /// <summary>
        /// Creates an N x N 2D identity matrix (1s on the main diagonal, 0s elsewhere).
        /// </summary>
        public static Tensor<float> Eye(int n) => Eye<float>(n);

        /// <summary>
        /// Creates an N x N 2D identity matrix of type T (1s on the main diagonal, 0s elsewhere).
        /// </summary>
        public static Tensor<T> Eye<T>(int n) where T : unmanaged, IEquatable<T> => Eye<T>(n, n);

        /// <summary>
        /// Creates a rows x cols 2D identity matrix of type T (1s on the main diagonal, 0s elsewhere).
        /// </summary>
        public static Tensor<T> Eye<T>(int rows, int cols) where T : unmanaged, IEquatable<T>
        {
            if (rows < 0) throw new ArgumentOutOfRangeException(nameof(rows), "Rows must be non-negative.");
            if (cols < 0) throw new ArgumentOutOfRangeException(nameof(cols), "Cols must be non-negative.");

            var tensor = new Tensor<T>(rows, cols);
            T oneVal = (T)Convert.ChangeType(1, typeof(T));
            int minDim = Math.Min(rows, cols);
            for (int i = 0; i < minDim; i++)
            {
                tensor[i, i] = oneVal;
            }

            return tensor;
        }

        /// <summary>
        /// Creates a 1D tensor with values evenly spaced within [start, stop) with step (double precision).
        /// </summary>
        public static Tensor<double> Arange(double start, double stop, double step = 1.0)
        {
            if (step == 0) throw new ArgumentException("Step cannot be zero.", nameof(step));
            if ((step > 0 && start >= stop) || (step < 0 && start <= stop))
            {
                return new Tensor<double>(0);
            }

            int count = (int)Math.Ceiling((stop - start) / step);
            var tensor = new Tensor<double>(count);
            for (int i = 0; i < count; i++)
            {
                tensor[i] = start + i * step;
            }

            return tensor;
        }

        /// <summary>
        /// Creates a 1D tensor with 'num' evenly spaced numbers over [start, stop] (double precision).
        /// </summary>
        public static Tensor<double> Linspace(double start, double stop, int num)
        {
            if (num < 0) throw new ArgumentOutOfRangeException(nameof(num), "Number of samples must be non-negative.");
            if (num == 0) return new Tensor<double>(0);
            if (num == 1) return FromArray(new[] { start }, 1);

            var tensor = new Tensor<double>(num);
            double step = (stop - start) / (num - 1);

            for (int i = 0; i < num; i++)
            {
                tensor[i] = i == num - 1 ? stop : start + i * step;
            }

            return tensor;
        }

        /// <summary>
        /// Creates a tensor filled with pseudo-random values drawn from a uniform distribution [min, max).
        /// </summary>
        public static Tensor<float> Uniform(float min = 0f, float max = 1f, int seed = 42, params int[] shape)
        {
            var rng = new Random(seed);
            var tensor = new Tensor<float>(shape);
            var span = tensor.AsSpan();

            float range = max - min;
            for (int i = 0; i < span.Length; i++)
            {
                span[i] = min + (float)rng.NextDouble() * range;
            }

            return tensor;
        }

        /// <summary>
        /// Creates a 2D matrix filled with uniform random values (convenience alias).
        /// </summary>
        public static Tensor<float> RandomUniform(int rows, int cols, float min = 0f, float max = 1f, int seed = 42) =>
            Uniform(min, max, seed, rows, cols);

        /// <summary>
        /// Creates a tensor filled with pseudo-random values drawn from a standard normal distribution N(mean, stdDev^2) via Box-Muller transform.
        /// </summary>
        public static Tensor<float> Normal(float mean = 0f, float stdDev = 1f, int seed = 42, params int[] shape)
        {
            var rng = new Random(seed);
            var tensor = new Tensor<float>(shape);
            var span = tensor.AsSpan();

            for (int i = 0; i < span.Length; i += 2)
            {
                double u1 = 1.0 - rng.NextDouble();
                double u2 = 1.0 - rng.NextDouble();

                double randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
                span[i] = (float)(mean + stdDev * randStdNormal);

                if (i + 1 < span.Length)
                {
                    double randStdNormal2 = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
                    span[i + 1] = (float)(mean + stdDev * randStdNormal2);
                }
            }

            return tensor;
        }

        /// <summary>
        /// Creates a true zero-copy Tensor wrapping an unmanaged NativeMemoryBlock without GC heap allocations.
        /// </summary>
        public static unsafe Tensor<T> FromNativeBlock<T>(ZeroPrimitives.Memory.NativeMemoryBlock block, params int[] shape) where T : unmanaged, IEquatable<T>
        {
            if (block == null) throw new ArgumentNullException(nameof(block));
            var tensorShape = new TensorShape(shape);
            int requiredBytes = tensorShape.TotalElements * sizeof(T);
            if (block.Capacity < requiredBytes)
                throw new ArgumentException($"Native block capacity ({block.Capacity} bytes) is smaller than required tensor size ({requiredBytes} bytes).", nameof(block));

            var storage = new Storage.NativeMemoryStorage<T>(block, 0, tensorShape.TotalElements);
            var strides = TensorStrides.ComputeContiguousStrides(tensorShape);
            return new Tensor<T>(storage, 0, tensorShape, strides);
        }

        /// <summary>
        /// Creates a true zero-copy Tensor wrapping a raw unmanaged native pointer.
        /// </summary>
        public static unsafe Tensor<T> FromNativePointer<T>(T* pointer, TensorShape shape, bool ownsPointer = false) where T : unmanaged, IEquatable<T>
        {
            if (pointer == null) throw new ArgumentNullException(nameof(pointer));
            if (shape == null) throw new ArgumentNullException(nameof(shape));

            var storage = new Storage.NativeMemoryStorage<T>(pointer, shape.TotalElements, ownsPointer);
            var strides = TensorStrides.ComputeContiguousStrides(shape);
            return new Tensor<T>(storage, 0, shape, strides);
        }

        /// <summary>
        /// Copies the tensor elements into an unmanaged NativeMemoryBlock allocated from off-heap native memory.
        /// </summary>
        public static unsafe ZeroPrimitives.Memory.NativeMemoryBlock ToNativeBlock<T>(this Tensor<T> tensor) where T : unmanaged, IEquatable<T>
        {
            if (tensor == null) throw new ArgumentNullException(nameof(tensor));
            var contig = tensor.IsContiguous ? tensor : tensor.ToContiguous();
            int byteCount = contig.Length * sizeof(T);
            var block = ZeroPrimitives.Memory.NativeMemoryBlock.Allocate(byteCount);
            var destSpan = new Span<T>(block.Pointer, contig.Length);
            contig.AsSpan().CopyTo(destSpan);
            block.Length = byteCount;
            return block;
        }
    }
}
