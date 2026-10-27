using System;
using ZeroPrimitives.Memory;
using ZeroTensor.Core.Storage;

namespace ZeroTensor.Core
{
    /// <summary>
    /// Static execution workspace providing zero-allocation ping-pong double buffering
    /// across iterative neural network layers (Transformer Decoders, Diffusion DiT blocks).
    /// Eliminates garbage collection pressure entirely during autoregressive or diffusion step loops.
    /// <para>
    /// <b>Architectural Boundary:</b> In the ZeroPlatform ecosystem, this class is a Layer 2 (Tensor Substrate)
    /// primitive designed for sequential ping-pong memory reuse between repeating layers. It intentionally does
    /// NOT perform whole-graph static memory planning, topological node scheduling, or cross-operator lifecycle
    /// analysis; those higher-level DAG execution capabilities belong to Layer 3 (<c>ZeroInference.MemoryPlanner</c>).
    /// </para>
    /// </summary>
    public sealed class InferenceWorkspace<T> : IDisposable where T : unmanaged, IEquatable<T>
    {
        private readonly TensorShape _maxShape;
        private readonly Tensor<T> _bufferA;
        private readonly Tensor<T> _bufferB;
        private bool _isAPrimary;
        private int _disposed;

        /// <summary>
        /// Gets the first pre-allocated static buffer.
        /// </summary>
        public Tensor<T> BufferA => _bufferA;

        /// <summary>
        /// Gets the second pre-allocated static buffer.
        /// </summary>
        public Tensor<T> BufferB => _bufferB;

        /// <summary>
        /// Gets the active input tensor for the current layer forward pass.
        /// </summary>
        public Tensor<T> CurrentInput => _isAPrimary ? _bufferA : _bufferB;

        /// <summary>
        /// Gets the active output target tensor for the current layer forward pass.
        /// </summary>
        public Tensor<T> CurrentOutput => _isAPrimary ? _bufferB : _bufferA;

        /// <summary>
        /// Gets the allocated maximum tensor shape.
        /// </summary>
        public TensorShape Shape => _maxShape;

        public InferenceWorkspace(TensorShape maxShape, bool useNativeMemory = false)
        {
            _maxShape = maxShape ?? throw new ArgumentNullException(nameof(maxShape));

            if (useNativeMemory)
            {
                var storageA = NativeMemoryStorage<T>.Allocate(maxShape.TotalElements);
                var storageB = NativeMemoryStorage<T>.Allocate(maxShape.TotalElements);
                var strides = TensorStrides.ComputeContiguousStrides(maxShape);

                _bufferA = new Tensor<T>(storageA, 0, maxShape, strides);
                _bufferB = new Tensor<T>(storageB, 0, maxShape, strides);
            }
            else
            {
                _bufferA = new Tensor<T>(maxShape);
                _bufferB = new Tensor<T>(maxShape);
            }

            _isAPrimary = true;
        }

        public InferenceWorkspace(params int[] dimensions)
            : this(new TensorShape(dimensions))
        {
        }

        /// <summary>
        /// Resets the workspace and initializes CurrentInput with initial activations.
        /// </summary>
        public void Reset(Tensor<T> initialInput)
        {
            if (initialInput == null) throw new ArgumentNullException(nameof(initialInput));
            _isAPrimary = true;

            var inSpan = initialInput.AsReadOnlySpan();
            var targetSpan = CurrentInput.AsSpan();
            inSpan.CopyTo(targetSpan);
        }

        /// <summary>
        /// Advances to the next layer, ping-ponging the active input and output buffers in O(1) time.
        /// </summary>
        public void Step()
        {
            _isAPrimary = !_isAPrimary;
        }

        public void Dispose()
        {
            if (System.Threading.Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                _bufferA.Dispose();
                _bufferB.Dispose();
            }
        }
    }
}
