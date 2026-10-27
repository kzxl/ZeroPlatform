using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using ZeroInference.Core.Engine;
using ZeroTensorFloat = ZeroTensor.Core.Tensor<float>;
using ZeroTensorFactory = ZeroTensor.Core.Tensor;

namespace ZeroInference.Providers.OnnxRuntime
{
    /// <summary>
    /// High-performance execution session powered by the official Microsoft.ML.OnnxRuntime engine.
    /// Supports CPU, CUDA, DirectML, and TensorRT hardware-accelerated inference with seamless ZeroTensor interop.
    /// </summary>
    public sealed class OnnxRuntimeSession : IInferenceSession
    {
        private readonly InferenceSession _session;
        private readonly List<string> _inputNames;
        private readonly List<string> _outputNames;
        private bool _disposed;

        /// <summary>
        /// Gets the underlying Microsoft.ML.OnnxRuntime session instance.
        /// </summary>
        public InferenceSession NativeSession => _session;

        /// <inheritdoc />
        public IReadOnlyList<string> InputNames => _inputNames;

        /// <inheritdoc />
        public IReadOnlyList<string> OutputNames => _outputNames;

        /// <summary>
        /// Initializes a new session from an ONNX model file.
        /// </summary>
        /// <param name="modelPath">Path to the .onnx model file.</param>
        /// <param name="options">Optional session options (e.g. GPU execution providers).</param>
        public OnnxRuntimeSession(string modelPath, SessionOptions? options = null)
        {
            if (string.IsNullOrEmpty(modelPath)) throw new ArgumentNullException(nameof(modelPath));
            if (!File.Exists(modelPath)) throw new FileNotFoundException("ONNX model file not found.", modelPath);

            _session = options != null ? new InferenceSession(modelPath, options) : new InferenceSession(modelPath);
            _inputNames = _session.InputMetadata.Keys.ToList();
            _outputNames = _session.OutputMetadata.Keys.ToList();
        }

        /// <summary>
        /// Initializes a new session directly from in-memory ONNX protobuf bytes.
        /// </summary>
        /// <param name="modelBytes">Raw byte array containing the ONNX model.</param>
        /// <param name="options">Optional session options.</param>
        public OnnxRuntimeSession(byte[] modelBytes, SessionOptions? options = null)
        {
            if (modelBytes == null || modelBytes.Length == 0)
                throw new ArgumentException("Model bytes cannot be null or empty.", nameof(modelBytes));

            _session = options != null ? new InferenceSession(modelBytes, options) : new InferenceSession(modelBytes);
            _inputNames = _session.InputMetadata.Keys.ToList();
            _outputNames = _session.OutputMetadata.Keys.ToList();
        }

        /// <summary>
        /// Initializes a new session by wrapping an existing pre-configured InferenceSession.
        /// </summary>
        public OnnxRuntimeSession(InferenceSession session)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _inputNames = _session.InputMetadata.Keys.ToList();
            _outputNames = _session.OutputMetadata.Keys.ToList();
        }

        /// <inheritdoc />
        public ZeroTensorFloat Run(ZeroTensorFloat input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (_inputNames.Count == 0)
                throw new InvalidOperationException("ONNX model declares no inputs.");

            return Run(new Dictionary<string, ZeroTensorFloat>(StringComparer.OrdinalIgnoreCase)
            {
                { _inputNames[0], input }
            });
        }

        /// <inheritdoc />
        public ZeroTensorFloat Run(IReadOnlyDictionary<string, ZeroTensorFloat> inputs)
        {
            if (inputs == null) throw new ArgumentNullException(nameof(inputs));
            if (_disposed) throw new ObjectDisposedException(nameof(OnnxRuntimeSession));

            var onnxInputs = new List<NamedOnnxValue>(inputs.Count);
            try
            {
                foreach (var kvp in inputs)
                {
                    var tensor = kvp.Value;
                    int[] dims = tensor.Shape.ToArray();
                    float[] data = tensor.ToArray();

                    var denseTensor = new DenseTensor<float>(data, dims);
                    onnxInputs.Add(NamedOnnxValue.CreateFromTensor(kvp.Key, denseTensor));
                }

                using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = _session.Run(onnxInputs);
                if (results.Count == 0)
                    throw new InvalidOperationException("Inference session produced zero output tensors.");

                var primaryOutput = results.First();
                var outTensor = primaryOutput.AsTensor<float>();

                int[] outDims = outTensor.Dimensions.ToArray();
                float[] outData = outTensor.ToArray();

                return ZeroTensorFactory.FromArray(outData, outDims);
            }
            finally
            {
                foreach (var val in onnxInputs)
                {
                    if (val is IDisposable d) d.Dispose();
                }
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (!_disposed)
            {
                _session.Dispose();
                _disposed = true;
            }
        }
    }
}
