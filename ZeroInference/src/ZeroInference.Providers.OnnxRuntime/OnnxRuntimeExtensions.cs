using System;
using Microsoft.ML.OnnxRuntime;
using ZeroInference.Core.Engine;

namespace ZeroInference.Providers.OnnxRuntime
{
    /// <summary>
    /// Extension and factory methods for initializing OnnxRuntime sessions from ZeroInference.
    /// </summary>
    public static class OnnxRuntimeExtensions
    {
        /// <summary>
        /// Creates an ONNX Runtime hardware-accelerated session from a model file.
        /// </summary>
        public static IInferenceSession CreateOnnxSession(string modelPath, SessionOptions? options = null)
        {
            return new OnnxRuntimeSession(modelPath, options);
        }

        /// <summary>
        /// Creates an ONNX Runtime hardware-accelerated session from in-memory model bytes.
        /// </summary>
        public static IInferenceSession CreateOnnxSession(byte[] modelBytes, SessionOptions? options = null)
        {
            return new OnnxRuntimeSession(modelBytes, options);
        }
    }
}
