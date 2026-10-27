using System;
using System.IO;
using System.Runtime.InteropServices;
using ZeroLlm.Core.Engine;
using ZeroLlm.Core.Format;

namespace ZeroLlm.Core.Quantization
{
    /// <summary>
    /// Pure C# neural weight quantizer converting FP32 Transformer models
    /// into GGML standard 8-bit (Q8_0) and 4-bit (Q4_0) quantized representations.
    /// Reduces memory footprint by up to 86% and enables cache-resident inference on pure CPU.
    /// </summary>
    public static unsafe class LlmQuantizer
    {
        /// <summary>
        /// Quantizes a contiguous span of float32 weights into an array of GGML Q8_0 blocks.
        /// Length must be a multiple of 32.
        /// </summary>
        public static BlockQ8_0[] QuantizeToQ8_0(ReadOnlySpan<float> source)
        {
            if (source.Length % BlockQ8_0.BlockSize != 0)
            {
                throw new ArgumentException($"Source length ({source.Length}) must be a multiple of {BlockQ8_0.BlockSize}.", nameof(source));
            }

            int numBlocks = source.Length / BlockQ8_0.BlockSize;
            var blocks = new BlockQ8_0[numBlocks];

            for (int b = 0; b < numBlocks; b++)
            {
                blocks[b] = BlockQ8_0.Quantize(source.Slice(b * BlockQ8_0.BlockSize, BlockQ8_0.BlockSize));
            }

            return blocks;
        }

        /// <summary>
        /// Quantizes a contiguous span of float32 weights into an array of GGML Q4_0 blocks.
        /// Length must be a multiple of 32.
        /// </summary>
        public static BlockQ4_0[] QuantizeToQ4_0(ReadOnlySpan<float> source)
        {
            if (source.Length % BlockQ4_0.BlockSize != 0)
            {
                throw new ArgumentException($"Source length ({source.Length}) must be a multiple of {BlockQ4_0.BlockSize}.", nameof(source));
            }

            int numBlocks = source.Length / BlockQ4_0.BlockSize;
            var blocks = new BlockQ4_0[numBlocks];

            for (int b = 0; b < numBlocks; b++)
            {
                blocks[b] = BlockQ4_0.Quantize(source.Slice(b * BlockQ4_0.BlockSize, BlockQ4_0.BlockSize));
            }

            return blocks;
        }

        /// <summary>
        /// Converts an array of Q8_0 blocks into a raw byte array for GGUF serialization.
        /// </summary>
        public static byte[] Q8BlocksToBytes(BlockQ8_0[] blocks)
        {
            if (blocks == null) throw new ArgumentNullException(nameof(blocks));
            byte[] bytes = new byte[blocks.Length * sizeof(BlockQ8_0)];
            fixed (BlockQ8_0* pSrc = blocks)
            fixed (byte* pDst = bytes)
            {
                Buffer.MemoryCopy(pSrc, pDst, bytes.Length, bytes.Length);
            }
            return bytes;
        }

        /// <summary>
        /// Reconstructs an array of Q8_0 blocks from a raw GGUF byte array.
        /// </summary>
        public static BlockQ8_0[] BytesToQ8Blocks(byte[] bytes)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            int count = bytes.Length / sizeof(BlockQ8_0);
            var blocks = new BlockQ8_0[count];
            fixed (byte* pSrc = bytes)
            fixed (BlockQ8_0* pDst = blocks)
            {
                Buffer.MemoryCopy(pSrc, pDst, bytes.Length, bytes.Length);
            }
            return blocks;
        }

        /// <summary>
        /// Converts an array of Q4_0 blocks into a raw byte array for GGUF serialization.
        /// </summary>
        public static byte[] Q4BlocksToBytes(BlockQ4_0[] blocks)
        {
            if (blocks == null) throw new ArgumentNullException(nameof(blocks));
            byte[] bytes = new byte[blocks.Length * sizeof(BlockQ4_0)];
            fixed (BlockQ4_0* pSrc = blocks)
            fixed (byte* pDst = bytes)
            {
                Buffer.MemoryCopy(pSrc, pDst, bytes.Length, bytes.Length);
            }
            return bytes;
        }

        /// <summary>
        /// Reconstructs an array of Q4_0 blocks from a raw GGUF byte array.
        /// </summary>
        public static BlockQ4_0[] BytesToQ4Blocks(byte[] bytes)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            int count = bytes.Length / sizeof(BlockQ4_0);
            var blocks = new BlockQ4_0[count];
            fixed (byte* pSrc = bytes)
            fixed (BlockQ4_0* pDst = blocks)
            {
                Buffer.MemoryCopy(pSrc, pDst, bytes.Length, bytes.Length);
            }
            return blocks;
        }

        /// <summary>
        /// Dequantizes an entire array of Q8_0 blocks back into single-precision float32 values.
        /// </summary>
        public static float[] DequantizeQ8(BlockQ8_0[] blocks)
        {
            if (blocks == null) throw new ArgumentNullException(nameof(blocks));
            var floats = new float[blocks.Length * BlockQ8_0.BlockSize];
            for (int i = 0; i < blocks.Length; i++)
            {
                blocks[i].Dequantize(floats.AsSpan(i * BlockQ8_0.BlockSize, BlockQ8_0.BlockSize));
            }
            return floats;
        }

        /// <summary>
        /// Dequantizes an entire array of Q4_0 blocks back into single-precision float32 values.
        /// </summary>
        public static float[] DequantizeQ4(BlockQ4_0[] blocks)
        {
            if (blocks == null) throw new ArgumentNullException(nameof(blocks));
            var floats = new float[blocks.Length * BlockQ4_0.BlockSize];
            for (int i = 0; i < blocks.Length; i++)
            {
                blocks[i].Dequantize(floats.AsSpan(i * BlockQ4_0.BlockSize, BlockQ4_0.BlockSize));
            }
            return floats;
        }

        /// <summary>
        /// Quantizes an in-memory FP32 model into a quantized LlmModel instance (Q8_0 or Q4_0).
        /// Retains 1D normalizations in FP32 and converts 2D weight projections into quantized blocks.
        /// </summary>
        public static LlmModel Quantize(LlmModel model, GgufTensorType targetType = GgufTensorType.Q8_0)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            if (targetType != GgufTensorType.Q8_0 && targetType != GgufTensorType.Q4_0)
            {
                throw new NotSupportedException($"Target quantization type {targetType} is not supported. Use Q8_0 or Q4_0.");
            }

            var qModel = new LlmModel(model.Config)
            {
                QuantizationType = targetType,
                FinalNorm = (float[])model.FinalNorm.Clone(),
                Layers = new LlmLayerWeights[model.Config.LayerCount]
            };

            if (targetType == GgufTensorType.Q8_0)
            {
                qModel.TokenEmbeddings_Q8 = QuantizeToQ8_0(model.TokenEmbeddings);
                for (int l = 0; l < model.Config.LayerCount; l++)
                {
                    var src = model.Layers[l];
                    var qLayer = new LlmLayerWeights
                    {
                        AttnNorm = (float[])src.AttnNorm.Clone(),
                        FfnNorm = (float[])src.FfnNorm.Clone(),
                        Wq_Q8 = QuantizeToQ8_0(src.Wq),
                        Wk_Q8 = QuantizeToQ8_0(src.Wk),
                        Wv_Q8 = QuantizeToQ8_0(src.Wv),
                        Wo_Q8 = QuantizeToQ8_0(src.Wo),
                    };

                    if (src.IsMoE)
                    {
                        if (src.Wrouter != null && src.Wrouter.Length > 0)
                        {
                            qLayer.Wrouter_Q8 = QuantizeToQ8_0(src.Wrouter);
                        }

                        if (src.Experts != null && src.Experts.Length > 0)
                        {
                            qLayer.Experts = new LlmExpertWeights[src.Experts.Length];
                            for (int e = 0; e < src.Experts.Length; e++)
                            {
                                var expSrc = src.Experts[e];
                                qLayer.Experts[e] = new LlmExpertWeights
                                {
                                    ExpertId = expSrc.ExpertId,
                                    Wgate_Q8 = QuantizeToQ8_0(expSrc.Wgate),
                                    Wup_Q8 = QuantizeToQ8_0(expSrc.Wup),
                                    Wdown_Q8 = QuantizeToQ8_0(expSrc.Wdown)
                                };
                            }
                        }

                        if (src.SharedExperts != null && src.SharedExperts.Length > 0)
                        {
                            qLayer.SharedExperts = new LlmExpertWeights[src.SharedExperts.Length];
                            for (int se = 0; se < src.SharedExperts.Length; se++)
                            {
                                var sExpSrc = src.SharedExperts[se];
                                qLayer.SharedExperts[se] = new LlmExpertWeights
                                {
                                    ExpertId = sExpSrc.ExpertId,
                                    Wgate_Q8 = QuantizeToQ8_0(sExpSrc.Wgate),
                                    Wup_Q8 = QuantizeToQ8_0(sExpSrc.Wup),
                                    Wdown_Q8 = QuantizeToQ8_0(sExpSrc.Wdown)
                                };
                            }
                        }
                    }
                    else
                    {
                        if (src.Wgate != null && src.Wgate.Length > 0) qLayer.Wgate_Q8 = QuantizeToQ8_0(src.Wgate);
                        if (src.Wup != null && src.Wup.Length > 0) qLayer.Wup_Q8 = QuantizeToQ8_0(src.Wup);
                        if (src.Wdown != null && src.Wdown.Length > 0) qLayer.Wdown_Q8 = QuantizeToQ8_0(src.Wdown);
                    }

                    qModel.Layers[l] = qLayer;
                }

                if (model.LmHead != null && model.LmHead.Length > 0 && model.LmHead != model.TokenEmbeddings)
                {
                    qModel.LmHead_Q8 = QuantizeToQ8_0(model.LmHead);
                }
                else
                {
                    qModel.LmHead_Q8 = qModel.TokenEmbeddings_Q8;
                }
            }
            else // Q4_0
            {
                qModel.TokenEmbeddings_Q4 = QuantizeToQ4_0(model.TokenEmbeddings);
                for (int l = 0; l < model.Config.LayerCount; l++)
                {
                    var src = model.Layers[l];
                    var qLayer = new LlmLayerWeights
                    {
                        AttnNorm = (float[])src.AttnNorm.Clone(),
                        FfnNorm = (float[])src.FfnNorm.Clone(),
                        Wq_Q4 = QuantizeToQ4_0(src.Wq),
                        Wk_Q4 = QuantizeToQ4_0(src.Wk),
                        Wv_Q4 = QuantizeToQ4_0(src.Wv),
                        Wo_Q4 = QuantizeToQ4_0(src.Wo),
                    };

                    if (src.IsMoE)
                    {
                        if (src.Wrouter != null && src.Wrouter.Length > 0)
                        {
                            qLayer.Wrouter_Q4 = QuantizeToQ4_0(src.Wrouter);
                        }

                        if (src.Experts != null && src.Experts.Length > 0)
                        {
                            qLayer.Experts = new LlmExpertWeights[src.Experts.Length];
                            for (int e = 0; e < src.Experts.Length; e++)
                            {
                                var expSrc = src.Experts[e];
                                qLayer.Experts[e] = new LlmExpertWeights
                                {
                                    ExpertId = expSrc.ExpertId,
                                    Wgate_Q4 = QuantizeToQ4_0(expSrc.Wgate),
                                    Wup_Q4 = QuantizeToQ4_0(expSrc.Wup),
                                    Wdown_Q4 = QuantizeToQ4_0(expSrc.Wdown)
                                };
                            }
                        }

                        if (src.SharedExperts != null && src.SharedExperts.Length > 0)
                        {
                            qLayer.SharedExperts = new LlmExpertWeights[src.SharedExperts.Length];
                            for (int se = 0; se < src.SharedExperts.Length; se++)
                            {
                                var sExpSrc = src.SharedExperts[se];
                                qLayer.SharedExperts[se] = new LlmExpertWeights
                                {
                                    ExpertId = sExpSrc.ExpertId,
                                    Wgate_Q4 = QuantizeToQ4_0(sExpSrc.Wgate),
                                    Wup_Q4 = QuantizeToQ4_0(sExpSrc.Wup),
                                    Wdown_Q4 = QuantizeToQ4_0(sExpSrc.Wdown)
                                };
                            }
                        }
                    }
                    else
                    {
                        if (src.Wgate != null && src.Wgate.Length > 0) qLayer.Wgate_Q4 = QuantizeToQ4_0(src.Wgate);
                        if (src.Wup != null && src.Wup.Length > 0) qLayer.Wup_Q4 = QuantizeToQ4_0(src.Wup);
                        if (src.Wdown != null && src.Wdown.Length > 0) qLayer.Wdown_Q4 = QuantizeToQ4_0(src.Wdown);
                    }

                    qModel.Layers[l] = qLayer;
                }

                if (model.LmHead != null && model.LmHead.Length > 0 && model.LmHead != model.TokenEmbeddings)
                {
                    qModel.LmHead_Q4 = QuantizeToQ4_0(model.LmHead);
                }
                else
                {
                    qModel.LmHead_Q4 = qModel.TokenEmbeddings_Q4;
                }
            }

            return qModel;
        }

        /// <summary>
        /// Quantizes an entire FP32 model and writes it directly to a GGUF v3 stream.
        /// 1D Normalization vectors are preserved in high-precision FP32; 2D projection matrices are quantized.
        /// </summary>
        public static void QuantizeModel(LlmModel model, Stream outputStream, GgufTensorType targetType = GgufTensorType.Q8_0)
        {
            var qModel = Quantize(model, targetType);
            qModel.SaveGguf(outputStream);
        }

        /// <summary>
        /// Quantizes a GGUF model file on disk and saves the resulting quantized GGUF model.
        /// </summary>
        public static void QuantizeFile(string inputGgufPath, string outputGgufPath, GgufTensorType targetType = GgufTensorType.Q8_0)
        {
            var model = LlmModel.LoadGguf(inputGgufPath);
            using var fs = new FileStream(outputGgufPath, FileMode.Create, FileAccess.Write, FileShare.None);
            QuantizeModel(model, fs, targetType);
        }
    }
}
