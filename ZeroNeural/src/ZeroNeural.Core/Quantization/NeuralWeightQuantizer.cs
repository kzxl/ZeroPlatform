using System;
using System.IO;
using ZeroTensor.Core;

namespace ZeroNeural.Core.Quantization
{
    /// <summary>
    /// Quantized 8-bit signed integer tensor with symmetric scaling.
    /// Provides 4x memory compression over FP32 and accelerates integer inner-product calculations.
    /// </summary>
    public sealed class QuantizedTensor
    {
        public sbyte[] Data { get; }
        public float Scale { get; }
        public int[] Shape { get; }
        public int Length => Data.Length;

        public QuantizedTensor(sbyte[] data, float scale, int[] shape)
        {
            Data = data ?? throw new ArgumentNullException(nameof(data));
            Scale = scale;
            Shape = shape ?? throw new ArgumentNullException(nameof(shape));
        }

        /// <summary>
        /// Dequantizes the INT8 tensor back to a standard FP32 Tensor.
        /// </summary>
        public Tensor<float> Dequantize()
        {
            float[] fp32Data = new float[Data.Length];
            for (int i = 0; i < Data.Length; i++)
            {
                fp32Data[i] = Data[i] * Scale;
            }
            return Tensor.FromArray(fp32Data, Shape);
        }

        /// <summary>
        /// Zero-allocation dequantization directly into caller-supplied destination span.
        /// </summary>
        public void DequantizeTo(Span<float> destination)
        {
            if (destination.Length < Data.Length)
            {
                throw new ArgumentException($"Destination span length {destination.Length} is smaller than tensor length {Data.Length}.");
            }

            for (int i = 0; i < Data.Length; i++)
            {
                destination[i] = Data[i] * Scale;
            }
        }

        /// <summary>
        /// Computes exact dot product between two quantized 1D vectors using integer multiplication and scale accumulation.
        /// </summary>
        public static float DotProduct(QuantizedTensor a, QuantizedTensor b)
        {
            if (a == null) throw new ArgumentNullException(nameof(a));
            if (b == null) throw new ArgumentNullException(nameof(b));
            if (a.Length != b.Length)
            {
                throw new ArgumentException($"Tensors must have equal length: {a.Length} vs {b.Length}.");
            }

            int sum = 0;
            var aData = a.Data;
            var bData = b.Data;
            int len = a.Length;

            for (int i = 0; i < len; i++)
            {
                sum += aData[i] * bData[i];
            }

            return sum * (a.Scale * b.Scale);
        }

        /// <summary>
        /// Serializes the quantized tensor to a binary stream.
        /// </summary>
        public void Serialize(BinaryWriter writer)
        {
            if (writer == null) throw new ArgumentNullException(nameof(writer));

            writer.Write(Scale);
            writer.Write(Shape.Length);
            for (int i = 0; i < Shape.Length; i++)
            {
                writer.Write(Shape[i]);
            }

            writer.Write(Data.Length);
            for (int i = 0; i < Data.Length; i++)
            {
                writer.Write(Data[i]);
            }
        }

        /// <summary>
        /// Deserializes a quantized tensor from a binary stream.
        /// </summary>
        public static QuantizedTensor Deserialize(BinaryReader reader)
        {
            if (reader == null) throw new ArgumentNullException(nameof(reader));

            float scale = reader.ReadSingle();
            int rank = reader.ReadInt32();
            int[] shape = new int[rank];
            for (int i = 0; i < rank; i++)
            {
                shape[i] = reader.ReadInt32();
            }

            int length = reader.ReadInt32();
            sbyte[] data = new sbyte[length];
            for (int i = 0; i < length; i++)
            {
                data[i] = reader.ReadSByte();
            }

            return new QuantizedTensor(data, scale, shape);
        }
    }

    /// <summary>
    /// High-performance symmetric INT8 neural weight quantizer.
    /// Maps continuous FP32 weight tensors to [-127, 127] with dynamic scaling.
    /// </summary>
    public static class NeuralWeightQuantizer
    {
        /// <summary>
        /// Quantizes a FP32 tensor to symmetric INT8 format.
        /// </summary>
        public static QuantizedTensor Quantize(Tensor<float> tensor)
        {
            if (tensor == null) throw new ArgumentNullException(nameof(tensor));

            int len = tensor.Length;
            sbyte[] qData = new sbyte[len];

            // 1. Find absolute maximum value
            float maxAbs = 0f;
            tensor.ForEachElement(v =>
            {
                float abs = Math.Abs(v);
                if (abs > maxAbs) maxAbs = abs;
            });

            if (maxAbs < 1e-8f)
            {
                return new QuantizedTensor(qData, scale: 1.0f, tensor.Shape.ToArray());
            }

            // 2. Symmetric scale: scale = maxAbs / 127.0f
            float scale = maxAbs / 127.0f;
            float invScale = 1.0f / scale;

            int idx = 0;
            tensor.ForEachElement(v =>
            {
                float scaled = v * invScale;
                int rounded = (int)Math.Round(scaled);
                if (rounded > 127) rounded = 127;
                if (rounded < -127) rounded = -127;
                qData[idx++] = (sbyte)rounded;
            });

            return new QuantizedTensor(qData, scale, tensor.Shape.ToArray());
        }

        /// <summary>
        /// Computes Mean Absolute Error (MAE) between original FP32 tensor and its INT8 dequantized reconstruction.
        /// </summary>
        public static float ComputeQuantizationError(Tensor<float> original, QuantizedTensor quantized)
        {
            if (original == null) throw new ArgumentNullException(nameof(original));
            if (quantized == null) throw new ArgumentNullException(nameof(quantized));

            var recon = quantized.Dequantize();
            float totalError = 0f;

            int idx = 0;
            var reconArray = recon.ToArray();
            original.ForEachElement(origVal =>
            {
                float reconVal = reconArray[idx++];
                totalError += Math.Abs(origVal - reconVal);
            });

            return totalError / original.Length;
        }
    }
}
