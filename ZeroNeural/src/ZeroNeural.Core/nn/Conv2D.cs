using System;
using System.Collections.Generic;
using ZeroNeural.Core.Autograd;
using ZeroTensor.Core;

namespace ZeroNeural.Core.nn
{
    /// <summary>
    /// 2D Convolutional neural network layer: transforms (B, C_in, H, W) to (B, C_out, H_out, W_out).
    /// </summary>
    public class Conv2D : Module
    {
        public Variable Weight { get; }
        public Variable? Bias { get; }

        public int InChannels { get; }
        public int OutChannels { get; }
        public int KernelHeight { get; }
        public int KernelWidth { get; }
        public int Stride { get; }
        public int Padding { get; }

        public Conv2D(
            int inChannels,
            int outChannels,
            int kernelSize,
            int stride = 1,
            int padding = 0,
            bool hasBias = true,
            int seed = 42)
            : this(inChannels, outChannels, kernelSize, kernelSize, stride, padding, hasBias, seed)
        {
        }

        public Conv2D(
            int inChannels,
            int outChannels,
            int kernelHeight,
            int kernelWidth,
            int stride = 1,
            int padding = 0,
            bool hasBias = true,
            int seed = 42)
        {
            if (inChannels <= 0) throw new ArgumentOutOfRangeException(nameof(inChannels));
            if (outChannels <= 0) throw new ArgumentOutOfRangeException(nameof(outChannels));

            InChannels = inChannels;
            OutChannels = outChannels;
            KernelHeight = kernelHeight;
            KernelWidth = kernelWidth;
            Stride = stride;
            Padding = padding;

            float k = 1.0f / (inChannels * kernelHeight * kernelWidth);
            float bound = (float)Math.Sqrt(k);

            var wTensor = Tensor.Uniform(-bound, bound, seed, outChannels, inChannels, kernelHeight, kernelWidth);
            Weight = new Variable(wTensor, requiresGrad: true);

            if (hasBias)
            {
                var bTensor = Tensor.Uniform(-bound, bound, seed + 1, outChannels);
                Bias = new Variable(bTensor, requiresGrad: true);
            }
        }

        public override Variable Forward(Variable input)
        {
            return new Conv2DOp(input, Weight, Bias, Stride, Padding).Output;
        }
    }

    internal sealed class Conv2DOp : IOpNode
    {
        private readonly Variable _input;
        private readonly Variable _weight;
        private readonly Variable? _bias;
        private readonly int _stride;
        private readonly int _padding;

        public IReadOnlyList<Variable> Inputs { get; }
        public Variable Output { get; }

        public Conv2DOp(Variable input, Variable weight, Variable? bias, int stride, int padding)
        {
            _input = input;
            _weight = weight;
            _bias = bias;
            _stride = stride;
            _padding = padding;

            var inputsList = new List<Variable> { input, weight };
            if (bias != null) inputsList.Add(bias);
            Inputs = inputsList;

            // Input: (B, C_in, H, W)
            // Weight: (C_out, C_in, KH, KW)
            int batch = input.Shape[0];
            int inC = input.Shape[1];
            int inH = input.Shape[2];
            int inW = input.Shape[3];

            int outC = weight.Shape[0];
            int kH = weight.Shape[2];
            int kW = weight.Shape[3];

            int outH = (inH + 2 * padding - kH) / stride + 1;
            int outW = (inW + 2 * padding - kW) / stride + 1;

            var outTensor = new Tensor<float>(batch, outC, outH, outW);

            for (int b = 0; b < batch; b++)
            {
                for (int oc = 0; oc < outC; oc++)
                {
                    float biasVal = bias != null ? bias.Data[oc] : 0f;

                    for (int oh = 0; oh < outH; oh++)
                    {
                        int inYOrigin = oh * stride - padding;

                        for (int ow = 0; ow < outW; ow++)
                        {
                            int inXOrigin = ow * stride - padding;
                            float sum = biasVal;

                            for (int ic = 0; ic < inC; ic++)
                            {
                                for (int kh = 0; kh < kH; kh++)
                                {
                                    int iy = inYOrigin + kh;
                                    if ((uint)iy >= (uint)inH) continue;

                                    for (int kw = 0; kw < kW; kw++)
                                    {
                                        int ix = inXOrigin + kw;
                                        if ((uint)ix >= (uint)inW) continue;

                                        sum += input.Data[b, ic, iy, ix] * weight.Data[oc, ic, kh, kw];
                                    }
                                }
                            }

                            outTensor[b, oc, oh, ow] = sum;
                        }
                    }
                }
            }

            bool requiresGrad = input.RequiresGrad || weight.RequiresGrad || (bias?.RequiresGrad ?? false);
            Output = new Variable(outTensor, requiresGrad: requiresGrad, creator: this);
        }

        public void Backward(Tensor<float> gradOutput)
        {
            int batch = _input.Shape[0];
            int inC = _input.Shape[1];
            int inH = _input.Shape[2];
            int inW = _input.Shape[3];

            int outC = _weight.Shape[0];
            int kH = _weight.Shape[2];
            int kW = _weight.Shape[3];

            int outH = gradOutput.Shape[2];
            int outW = gradOutput.Shape[3];

            // 1. Bias gradient
            if (_bias != null && _bias.RequiresGrad)
            {
                var gradBias = new Tensor<float>(outC);
                for (int oc = 0; oc < outC; oc++)
                {
                    float sum = 0f;
                    for (int b = 0; b < batch; b++)
                    {
                        for (int oh = 0; oh < outH; oh++)
                        {
                            for (int ow = 0; ow < outW; ow++)
                            {
                                sum += gradOutput[b, oc, oh, ow];
                            }
                        }
                    }
                    gradBias[oc] = sum;
                }
                AutogradEngine.AccumulateGrad(_bias, gradBias);
            }

            // 2. Weight gradient
            if (_weight.RequiresGrad)
            {
                var gradWeight = new Tensor<float>(_weight.Data.Shape);

                for (int oc = 0; oc < outC; oc++)
                {
                    for (int ic = 0; ic < inC; ic++)
                    {
                        for (int kh = 0; kh < kH; kh++)
                        {
                            for (int kw = 0; kw < kW; kw++)
                            {
                                float sum = 0f;

                                for (int b = 0; b < batch; b++)
                                {
                                    for (int oh = 0; oh < outH; oh++)
                                    {
                                        int iy = oh * _stride - _padding + kh;
                                        if ((uint)iy >= (uint)inH) continue;

                                        for (int ow = 0; ow < outW; ow++)
                                        {
                                            int ix = ow * _stride - _padding + kw;
                                            if ((uint)ix >= (uint)inW) continue;

                                            sum += gradOutput[b, oc, oh, ow] * _input.Data[b, ic, iy, ix];
                                        }
                                    }
                                }

                                gradWeight[oc, ic, kh, kw] = sum;
                            }
                        }
                    }
                }

                AutogradEngine.AccumulateGrad(_weight, gradWeight);
            }

            // 3. Input gradient
            if (_input.RequiresGrad)
            {
                var gradInput = new Tensor<float>(_input.Data.Shape);

                for (int b = 0; b < batch; b++)
                {
                    for (int oc = 0; oc < outC; oc++)
                    {
                        for (int oh = 0; oh < outH; oh++)
                        {
                            int inYOrigin = oh * _stride - _padding;

                            for (int ow = 0; ow < outW; ow++)
                            {
                                int inXOrigin = ow * _stride - _padding;
                                float gOut = gradOutput[b, oc, oh, ow];

                                for (int ic = 0; ic < inC; ic++)
                                {
                                    for (int kh = 0; kh < kH; kh++)
                                    {
                                        int iy = inYOrigin + kh;
                                        if ((uint)iy >= (uint)inH) continue;

                                        for (int kw = 0; kw < kW; kw++)
                                        {
                                            int ix = inXOrigin + kw;
                                            if ((uint)ix >= (uint)inW) continue;

                                            gradInput[b, ic, iy, ix] += gOut * _weight.Data[oc, ic, kh, kw];
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                AutogradEngine.AccumulateGrad(_input, gradInput);
            }
        }
    }
}
