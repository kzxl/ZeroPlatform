using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using ZeroLlm.Core.Engine;
using ZeroLlm.Core.Layers;
using ZeroTokenizer.Core.Abstractions;

namespace ZeroLlm.Core.Training
{
    /// <summary>
    /// Pure C# Masked Causal Language Model Trainer with AdamW optimization.
    /// Supports full parameter Supervised Fine-Tuning (SFT), head/embedding adaptation, and domain pre-training.
    /// </summary>
    public sealed class LlmTrainer
    {
        private readonly LlmModel _model;
        private readonly TrainingConfig _config;
        private int _step;
        private int _accumulatedCount;

        // Trainable parameter tracking
        private readonly List<ParamGrad> _trainableParams = new List<ParamGrad>();
        private readonly ParamGrad _embParam;
        private readonly ParamGrad _finalNormParam;
        private readonly ParamGrad _lmHeadParam;
        private readonly LayerParamGrad[] _layerParams;

        private sealed class ParamGrad
        {
            public float[] Weights { get; }
            public float[] Grads { get; }
            public float[] M { get; }
            public float[] V { get; }

            public ParamGrad(float[] weights)
            {
                Weights = weights ?? throw new ArgumentNullException(nameof(weights));
                Grads = new float[weights.Length];
                M = new float[weights.Length];
                V = new float[weights.Length];
            }

            public void ZeroGrad() => Array.Clear(Grads, 0, Grads.Length);
        }

        private sealed class LayerParamGrad
        {
            public ParamGrad AttnNorm { get; }
            public ParamGrad Wq { get; }
            public ParamGrad Wk { get; }
            public ParamGrad Wv { get; }
            public ParamGrad Wo { get; }
            public ParamGrad FfnNorm { get; }
            public ParamGrad? Wgate { get; }
            public ParamGrad? Wup { get; }
            public ParamGrad? Wdown { get; }

            // MoE parameters
            public bool IsMoE { get; }
            public ParamGrad? Wrouter { get; }
            public ParamGrad[]? ExpertWgate { get; }
            public ParamGrad[]? ExpertWup { get; }
            public ParamGrad[]? ExpertWdown { get; }

            public ParamGrad[]? SharedExpertWgate { get; }
            public ParamGrad[]? SharedExpertWup { get; }
            public ParamGrad[]? SharedExpertWdown { get; }

            public LayerParamGrad(LlmLayerWeights layer)
            {
                AttnNorm = new ParamGrad(layer.AttnNorm);
                Wq = new ParamGrad(layer.Wq);
                Wk = new ParamGrad(layer.Wk);
                Wv = new ParamGrad(layer.Wv);
                Wo = new ParamGrad(layer.Wo);
                FfnNorm = new ParamGrad(layer.FfnNorm);
                IsMoE = layer.IsMoE;

                if (layer.IsMoE)
                {
                    if (layer.Experts != null && layer.Experts.Length > 0)
                    {
                        Wrouter = new ParamGrad(layer.Wrouter ?? new float[layer.Experts.Length * layer.AttnNorm.Length]);
                        ExpertWgate = new ParamGrad[layer.Experts.Length];
                        ExpertWup = new ParamGrad[layer.Experts.Length];
                        ExpertWdown = new ParamGrad[layer.Experts.Length];

                        for (int e = 0; e < layer.Experts.Length; e++)
                        {
                            ExpertWgate[e] = new ParamGrad(layer.Experts[e].Wgate);
                            ExpertWup[e] = new ParamGrad(layer.Experts[e].Wup);
                            ExpertWdown[e] = new ParamGrad(layer.Experts[e].Wdown);
                        }
                    }

                    if (layer.SharedExperts != null && layer.SharedExperts.Length > 0)
                    {
                        SharedExpertWgate = new ParamGrad[layer.SharedExperts.Length];
                        SharedExpertWup = new ParamGrad[layer.SharedExperts.Length];
                        SharedExpertWdown = new ParamGrad[layer.SharedExperts.Length];

                        for (int se = 0; se < layer.SharedExperts.Length; se++)
                        {
                            SharedExpertWgate[se] = new ParamGrad(layer.SharedExperts[se].Wgate);
                            SharedExpertWup[se] = new ParamGrad(layer.SharedExperts[se].Wup);
                            SharedExpertWdown[se] = new ParamGrad(layer.SharedExperts[se].Wdown);
                        }
                    }
                }
                else
                {
                    Wgate = new ParamGrad(layer.Wgate);
                    Wup = new ParamGrad(layer.Wup);
                    Wdown = new ParamGrad(layer.Wdown);
                }
            }

            public void RegisterParams(List<ParamGrad> list)
            {
                list.Add(AttnNorm);
                list.Add(Wq);
                list.Add(Wk);
                list.Add(Wv);
                list.Add(Wo);
                list.Add(FfnNorm);

                if (IsMoE)
                {
                    if (Wrouter != null) list.Add(Wrouter);
                    if (ExpertWgate != null)
                    {
                        for (int e = 0; e < ExpertWgate.Length; e++)
                        {
                            list.Add(ExpertWgate[e]);
                            list.Add(ExpertWup![e]);
                            list.Add(ExpertWdown![e]);
                        }
                    }
                    if (SharedExpertWgate != null)
                    {
                        for (int se = 0; se < SharedExpertWgate.Length; se++)
                        {
                            list.Add(SharedExpertWgate[se]);
                            list.Add(SharedExpertWup![se]);
                            list.Add(SharedExpertWdown![se]);
                        }
                    }
                }
                else
                {
                    if (Wgate != null) list.Add(Wgate);
                    if (Wup != null) list.Add(Wup);
                    if (Wdown != null) list.Add(Wdown);
                }
            }
        }

        public LlmModel Model => _model;
        public TrainingConfig Config => _config;
        public int CurrentStep => _step;

        public LlmTrainer(LlmModel model, TrainingConfig? config = null)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _config = config ?? new TrainingConfig();

            _embParam = new ParamGrad(_model.TokenEmbeddings);
            _finalNormParam = new ParamGrad(_model.FinalNorm);
            _lmHeadParam = new ParamGrad(_model.LmHead);

            _layerParams = new LayerParamGrad[_model.Config.LayerCount];
            for (int l = 0; l < _model.Config.LayerCount; l++)
            {
                _layerParams[l] = new LayerParamGrad(_model.Layers[l]);
            }

            // Register active parameters according to mode
            RebuildTrainableParams();
        }

        private void RebuildTrainableParams()
        {
            _trainableParams.Clear();

            switch (_config.Mode)
            {
                case TrainingMode.Full:
                    _trainableParams.Add(_embParam);
                    for (int l = 0; l < _layerParams.Length; l++)
                    {
                        _layerParams[l].RegisterParams(_trainableParams);
                    }
                    _trainableParams.Add(_finalNormParam);
                    _trainableParams.Add(_lmHeadParam);
                    break;

                case TrainingMode.HeadAndEmbeddings:
                    _trainableParams.Add(_embParam);
                    _trainableParams.Add(_finalNormParam);
                    _trainableParams.Add(_lmHeadParam);
                    break;

                case TrainingMode.HeadOnly:
                    _trainableParams.Add(_lmHeadParam);
                    break;
            }
        }

        /// <summary>
        /// Executes a single masked causal language modeling training step over a sequence of tokens.
        /// Tokens prior to targetStartPos are treated as prompt context (masked from loss).
        /// </summary>
        public TrainStepResult TrainStep(int[] tokens, int? targetStartPos = null)
        {
            if (tokens == null || tokens.Length < 2)
            {
                return new TrainStepResult(0f, 0, 0f);
            }

            int seqLen = tokens.Length;
            int numPositions = seqLen - 1;
            int targetStart = targetStartPos ?? 1;
            if (targetStart < 1) targetStart = 1;

            var cfg = _model.Config;
            int embDim = cfg.EmbeddingDim;
            int qDim = cfg.HeadCount * cfg.HeadDim;
            int kvDim = cfg.HeadCountKv * cfg.HeadDim;
            int headDim = cfg.HeadDim;
            int ffnDim = cfg.FeedForwardDim;
            int vocabSize = cfg.VocabSize;
            int layers = cfg.LayerCount;

            int accumSteps = Math.Max(1, _config.GradientAccumulationSteps);

            // Zero all gradients at start of accumulation window
            if (_accumulatedCount == 0)
            {
                Parallel.For(0, _trainableParams.Count, i => _trainableParams[i].ZeroGrad());
            }

            // 1. Forward Pass Storage
            // xIn per token
            float[][] xIn = new float[numPositions][];
            // activations per (pos, layer)
            float[][][] xNormAttn = new float[numPositions][][];
            float[][][] q = new float[numPositions][][];
            float[][][] k = new float[numPositions][][];
            float[][][] v = new float[numPositions][][];
            float[][][][] attnWeights = new float[numPositions][][][]; // [pos][layer][head][tau]
            float[][][] attnOut = new float[numPositions][][];
            float[][][] xMid = new float[numPositions][][];
            float[][][] xNormFfn = new float[numPositions][][];
            float[][][] gate = new float[numPositions][][];
            float[][][] up = new float[numPositions][][];
            float[][][] swiglu = new float[numPositions][][];
            float[][][] xOut = new float[numPositions][][];

            // MoE activation tracking
            bool hasMoE = cfg.IsMoE;
            int[][][] moeTopIndices = hasMoE ? new int[numPositions][][] : null!;
            float[][][] moeTopWeights = hasMoE ? new float[numPositions][][] : null!;
            float[][][][] moeGate = hasMoE ? new float[numPositions][][][] : null!;
            float[][][][] moeUp = hasMoE ? new float[numPositions][][][] : null!;
            float[][][][] moeSwiglu = hasMoE ? new float[numPositions][][][] : null!;
            float[][][][] moeDown = hasMoE ? new float[numPositions][][][] : null!;
            float[][][] moeRouterLogits = hasMoE ? new float[numPositions][][] : null!;
            float[][][][] moeSharedGate = hasMoE ? new float[numPositions][][][] : null!;
            float[][][][] moeSharedUp = hasMoE ? new float[numPositions][][][] : null!;
            float[][][][] moeSharedSwiglu = hasMoE ? new float[numPositions][][][] : null!;
            float[][][][] moeSharedDown = hasMoE ? new float[numPositions][][][] : null!;

            float[][] xNormFinal = new float[numPositions][];
            float[][] logits = new float[numPositions][];

            float[] currentX = new float[embDim];

            for (int pos = 0; pos < numPositions; pos++)
            {
                xIn[pos] = new float[embDim];
                xNormAttn[pos] = new float[layers][];
                q[pos] = new float[layers][];
                k[pos] = new float[layers][];
                v[pos] = new float[layers][];
                attnWeights[pos] = new float[layers][][];
                attnOut[pos] = new float[layers][];
                xMid[pos] = new float[layers][];
                xNormFfn[pos] = new float[layers][];
                gate[pos] = new float[layers][];
                up[pos] = new float[layers][];
                swiglu[pos] = new float[layers][];
                xOut[pos] = new float[layers][];

                if (hasMoE)
                {
                    moeTopIndices[pos] = new int[layers][];
                    moeTopWeights[pos] = new float[layers][];
                    moeGate[pos] = new float[layers][][];
                    moeUp[pos] = new float[layers][][];
                    moeSwiglu[pos] = new float[layers][][];
                    moeDown[pos] = new float[layers][][];
                    moeRouterLogits[pos] = new float[layers][];
                    moeSharedGate[pos] = new float[layers][][];
                    moeSharedUp[pos] = new float[layers][][];
                    moeSharedSwiglu[pos] = new float[layers][][];
                    moeSharedDown[pos] = new float[layers][][];
                }

                bool isTarget = (pos + 1 >= targetStart);
                if (isTarget)
                {
                    xNormFinal[pos] = new float[embDim];
                    logits[pos] = new float[vocabSize];
                }

                // Embedding lookup
                int tok = tokens[pos];
                int safeTok = (tok >= 0 && tok < vocabSize) ? tok : 0;
                Array.Copy(_model.TokenEmbeddings, safeTok * embDim, xIn[pos], 0, embDim);
                Array.Copy(xIn[pos], currentX, embDim);

                for (int l = 0; l < layers; l++)
                {
                    var layer = _model.Layers[l];
                    xNormAttn[pos][l] = new float[embDim];
                    q[pos][l] = new float[qDim];
                    k[pos][l] = new float[kvDim];
                    v[pos][l] = new float[kvDim];
                    attnOut[pos][l] = new float[qDim];
                    xMid[pos][l] = new float[embDim];
                    xNormFfn[pos][l] = new float[embDim];
                    gate[pos][l] = new float[ffnDim];
                    up[pos][l] = new float[ffnDim];
                    swiglu[pos][l] = new float[ffnDim];
                    xOut[pos][l] = new float[embDim];

                    // Attn RMSNorm
                    RmsNorm.Forward(currentX, layer.AttnNorm, xNormAttn[pos][l], cfg.RmsNormEps);

                    // Q, K, V Projections
                    MatVec(xNormAttn[pos][l], layer.Wq, embDim, qDim, q[pos][l]);
                    MatVec(xNormAttn[pos][l], layer.Wk, embDim, kvDim, k[pos][l]);
                    MatVec(xNormAttn[pos][l], layer.Wv, embDim, kvDim, v[pos][l]);

                    // RoPE
                    for (int h = 0; h < cfg.HeadCount; h++)
                    {
                        RoPE.ApplyInplace(q[pos][l].AsSpan(h * headDim, headDim), pos, headDim, cfg.RopeFreqBase);
                    }
                    for (int h = 0; h < cfg.HeadCountKv; h++)
                    {
                        RoPE.ApplyInplace(k[pos][l].AsSpan(h * headDim, headDim), pos, headDim, cfg.RopeFreqBase);
                    }

                    // Attention computation across tau = 0..pos
                    attnWeights[pos][l] = new float[cfg.HeadCount][];
                    ForwardAttention(
                        q[pos][l], k, v, pos, l,
                        cfg.HeadCount, cfg.HeadCountKv, headDim,
                        attnOut[pos][l], attnWeights[pos][l]);

                    // Wo projection + Residual Add
                    Array.Copy(currentX, xMid[pos][l], embDim);
                    MatVecAdd(attnOut[pos][l], layer.Wo, qDim, embDim, xMid[pos][l]);

                    // FFN RMSNorm
                    RmsNorm.Forward(xMid[pos][l], layer.FfnNorm, xNormFfn[pos][l], cfg.RmsNormEps);

                    if (layer.IsMoE)
                    {
                        Array.Copy(xMid[pos][l], xOut[pos][l], embDim);

                        // 1. Shared Experts (DeepSeek / Qwen style: always active)
                        if (layer.SharedExperts != null && layer.SharedExperts.Length > 0)
                        {
                            int seCount = layer.SharedExperts.Length;
                            moeSharedGate[pos][l] = new float[seCount][];
                            moeSharedUp[pos][l] = new float[seCount][];
                            moeSharedSwiglu[pos][l] = new float[seCount][];
                            moeSharedDown[pos][l] = new float[seCount][];

                            for (int se = 0; se < seCount; se++)
                            {
                                var sExp = layer.SharedExperts[se];
                                moeSharedGate[pos][l][se] = new float[ffnDim];
                                moeSharedUp[pos][l][se] = new float[ffnDim];
                                moeSharedSwiglu[pos][l][se] = new float[ffnDim];
                                moeSharedDown[pos][l][se] = new float[embDim];

                                MatVec(xNormFfn[pos][l], sExp.Wgate, embDim, ffnDim, moeSharedGate[pos][l][se]);
                                MatVec(xNormFfn[pos][l], sExp.Wup, embDim, ffnDim, moeSharedUp[pos][l][se]);
                                SwiGLU.Forward(moeSharedGate[pos][l][se], moeSharedUp[pos][l][se], moeSharedSwiglu[pos][l][se]);
                                MatVec(moeSharedSwiglu[pos][l][se], sExp.Wdown, ffnDim, embDim, moeSharedDown[pos][l][se]);

                                for (int d = 0; d < embDim; d++)
                                {
                                    xOut[pos][l][d] += moeSharedDown[pos][l][se][d];
                                }
                            }
                        }

                        // 2. Routed Top-K Experts
                        if (layer.Experts != null && layer.Experts.Length > 0)
                        {
                            int expCount = layer.Experts.Length;
                            int kUsed = Math.Min(Math.Max(1, cfg.ExpertUsedCount), expCount);

                            moeTopIndices[pos][l] = new int[kUsed];
                            moeTopWeights[pos][l] = new float[kUsed];
                            moeGate[pos][l] = new float[kUsed][];
                            moeUp[pos][l] = new float[kUsed][];
                            moeSwiglu[pos][l] = new float[kUsed][];
                            moeDown[pos][l] = new float[kUsed][];
                            moeRouterLogits[pos][l] = new float[expCount];

                            // Router Logits
                            MatVec(xNormFfn[pos][l], layer.Wrouter!, embDim, expCount, moeRouterLogits[pos][l]);

                            // Top-K selection
                            SelectTopK(moeRouterLogits[pos][l], moeTopIndices[pos][l], moeTopWeights[pos][l], kUsed);
                            ComputeSoftmax(moeTopWeights[pos][l]);

                            for (int ki = 0; ki < kUsed; ki++)
                            {
                                int expIdx = moeTopIndices[pos][l][ki];
                                float w = moeTopWeights[pos][l][ki];
                                var exp = layer.Experts[expIdx];

                                moeGate[pos][l][ki] = new float[ffnDim];
                                moeUp[pos][l][ki] = new float[ffnDim];
                                moeSwiglu[pos][l][ki] = new float[ffnDim];
                                moeDown[pos][l][ki] = new float[embDim];

                                MatVec(xNormFfn[pos][l], exp.Wgate, embDim, ffnDim, moeGate[pos][l][ki]);
                                MatVec(xNormFfn[pos][l], exp.Wup, embDim, ffnDim, moeUp[pos][l][ki]);
                                SwiGLU.Forward(moeGate[pos][l][ki], moeUp[pos][l][ki], moeSwiglu[pos][l][ki]);
                                MatVec(moeSwiglu[pos][l][ki], exp.Wdown, ffnDim, embDim, moeDown[pos][l][ki]);

                                for (int d = 0; d < embDim; d++)
                                {
                                    xOut[pos][l][d] += w * moeDown[pos][l][ki][d];
                                }
                            }
                        }
                    }
                    else
                    {
                        // SwiGLU (Dense)
                        MatVec(xNormFfn[pos][l], layer.Wgate, embDim, ffnDim, gate[pos][l]);
                        MatVec(xNormFfn[pos][l], layer.Wup, embDim, ffnDim, up[pos][l]);
                        SwiGLU.Forward(gate[pos][l], up[pos][l], swiglu[pos][l]);

                        // Wdown + Residual Add
                        Array.Copy(xMid[pos][l], xOut[pos][l], embDim);
                        MatVecAdd(swiglu[pos][l], layer.Wdown, ffnDim, embDim, xOut[pos][l]);
                    }

                    Array.Copy(xOut[pos][l], currentX, embDim);
                }

                // Final RMSNorm and LM Head Projection (Target Tokens Only)
                if (isTarget)
                {
                    RmsNorm.Forward(currentX, _model.FinalNorm, xNormFinal[pos], cfg.RmsNormEps);
                    MatVec(xNormFinal[pos], _model.LmHead, embDim, vocabSize, logits[pos]);
                }
            }

            // 2. Cross-Entropy Loss & dLogits
            float totalLoss = 0f;
            int activeTargets = 0;
            float[][] dLogits = new float[numPositions][];

            for (int pos = 0; pos < numPositions; pos++)
            {
                bool isTarget = (pos + 1 >= targetStart);
                if (!isTarget) continue;

                dLogits[pos] = new float[vocabSize];
                int targetTokenId = tokens[pos + 1];

                // Softmax
                float maxVal = float.NegativeInfinity;
                for (int i = 0; i < vocabSize; i++)
                {
                    if (logits[pos][i] > maxVal) maxVal = logits[pos][i];
                }

                float sumExp = 0f;
                for (int i = 0; i < vocabSize; i++)
                {
                    float e = (float)Math.Exp(logits[pos][i] - maxVal);
                    dLogits[pos][i] = e;
                    sumExp += e;
                }

                float invSum = 1.0f / (sumExp + 1e-9f);
                for (int i = 0; i < vocabSize; i++)
                {
                    dLogits[pos][i] *= invSum;
                }

                int safeTarget = (targetTokenId >= 0 && targetTokenId < vocabSize) ? targetTokenId : 0;
                float targetProb = Math.Max(dLogits[pos][safeTarget], 1e-9f);
                float loss = -(float)Math.Log(targetProb);

                totalLoss += loss;
                activeTargets++;

                // Softmax gradient: P[i] - 1{i == target}
                dLogits[pos][safeTarget] -= 1.0f;
            }

            if (activeTargets == 0)
            {
                return new TrainStepResult(0f, 0, 0f);
            }

            // Scale gradients by 1 / activeTargets and 1 / accumSteps
            float scale = (1.0f / activeTargets) / accumSteps;
            for (int pos = 0; pos < numPositions; pos++)
            {
                if (pos + 1 < targetStart) continue;
                for (int i = 0; i < vocabSize; i++)
                {
                    dLogits[pos][i] *= scale;
                }
            }

            // 3. Backward Pass
            // Cumulative key and value gradients across time positions
            float[][][] dK = new float[numPositions][][];
            float[][][] dV = new float[numPositions][][];
            for (int p = 0; p < numPositions; p++)
            {
                dK[p] = new float[layers][];
                dV[p] = new float[layers][];
                for (int l = 0; l < layers; l++)
                {
                    dK[p][l] = new float[kvDim];
                    dV[p][l] = new float[kvDim];
                }
            }

            float[] dxNormFinal = new float[embDim];
            float[] dx = new float[embDim];
            float[] dSwiglu = new float[ffnDim];
            float[] dGate = new float[ffnDim];
            float[] dUp = new float[ffnDim];
            float[] dxNormFfn = new float[embDim];
            float[] dResidFfn = new float[embDim];
            float[] dxMid = new float[embDim];
            float[] dAttnOut = new float[qDim];
            float[] dQ = new float[qDim];
            float[] dxNormAttn = new float[embDim];
            float[] dResidAttn = new float[embDim];
            Span<float> dTopWeightsBuffer = stackalloc float[cfg.ExpertCount > 0 ? cfg.ExpertCount : 8];

            for (int pos = numPositions - 1; pos >= 0; pos--)
            {
                if (pos + 1 < targetStart) continue;

                // 3.1 LM Head gradient
                Array.Clear(dxNormFinal, 0, embDim);
                unsafe
                {
                    fixed (float* pXNorm = xNormFinal[pos])
                    fixed (float* pLmHead = _model.LmHead)
                    fixed (float* pGrads = _lmHeadParam.Grads)
                    fixed (float* pDxNorm = dxNormFinal)
                    {
                        int vecSize = Vector<float>.Count;
                        int simdLimit = embDim - (embDim % vecSize);

                        for (int vIdx = 0; vIdx < vocabSize; vIdx++)
                        {
                            float dL = dLogits[pos][vIdx];
                            if (dL == 0f) continue;
                            int rowOff = vIdx * embDim;
                            var vDl = new Vector<float>(dL);

                            for (int i = 0; i < simdLimit; i += vecSize)
                            {
                                var vX = *(Vector<float>*)(pXNorm + i);
                                var vW = *(Vector<float>*)(pLmHead + rowOff + i);
                                var vGrad = *(Vector<float>*)(pGrads + rowOff + i);
                                var vDx = *(Vector<float>*)(pDxNorm + i);

                                *(Vector<float>*)(pGrads + rowOff + i) = vGrad + (vDl * vX);
                                *(Vector<float>*)(pDxNorm + i) = vDx + (vDl * vW);
                            }

                            for (int i = simdLimit; i < embDim; i++)
                            {
                                pGrads[rowOff + i] += dL * pXNorm[i];
                                pDxNorm[i] += dL * pLmHead[rowOff + i];
                            }
                        }
                    }
                }

                // 3.2 Final RMSNorm backward
                BackpropRmsNorm(dxNormFinal, xOut[pos][layers - 1], _model.FinalNorm, _finalNormParam.Grads, dx, cfg.RmsNormEps);

                if (_config.Mode == TrainingMode.HeadOnly) continue;

                if (_config.Mode == TrainingMode.HeadAndEmbeddings)
                {
                    int tok = tokens[pos];
                    int safeTok = (tok >= 0 && tok < vocabSize) ? tok : 0;
                    int embOff = safeTok * embDim;
                    for (int i = 0; i < embDim; i++)
                    {
                        _embParam.Grads[embOff + i] += dx[i];
                    }
                    continue;
                }

                // 3.3 Full Layer Backward
                for (int l = layers - 1; l >= 0; l--)
                {
                    var layer = _model.Layers[l];
                    var lParams = _layerParams[l];

                    if (layer.IsMoE)
                    {
                        Array.Clear(dxNormFfn, 0, embDim);

                        // 1. Shared Experts Backprop (DeepSeek MoE architecture)
                        if (layer.SharedExperts != null && layer.SharedExperts.Length > 0 && lParams.SharedExpertWdown != null)
                        {
                            int seCount = layer.SharedExperts.Length;
                            for (int se = 0; se < seCount; se++)
                            {
                                var sExp = layer.SharedExperts[se];
                                var seWdown = lParams.SharedExpertWdown[se];
                                var seWgate = lParams.SharedExpertWgate![se];
                                var seWup = lParams.SharedExpertWup![se];

                                Array.Clear(dSwiglu, 0, ffnDim);
                                for (int o = 0; o < embDim; o++)
                                {
                                    float dX_o = dx[o]; // Shared expert weight is 1.0
                                    if (dX_o == 0f) continue;
                                    int rowOff = o * ffnDim;
                                    for (int i = 0; i < ffnDim; i++)
                                    {
                                        seWdown.Grads[rowOff + i] += dX_o * moeSharedSwiglu[pos][l][se][i];
                                        dSwiglu[i] += dX_o * sExp.Wdown[rowOff + i];
                                    }
                                }

                                for (int i = 0; i < ffnDim; i++)
                                {
                                    float gVal = moeSharedGate[pos][l][se][i];
                                    float uVal = moeSharedUp[pos][l][se][i];
                                    float sig = 1.0f / (1.0f + (float)Math.Exp(-gVal));
                                    float swish = gVal * sig;
                                    float dSwish = sig * (1.0f + gVal * (1.0f - sig));
                                    dUp[i] = dSwiglu[i] * swish;
                                    dGate[i] = dSwiglu[i] * uVal * dSwish;
                                }

                                for (int kIdx = 0; kIdx < ffnDim; kIdx++)
                                {
                                    float dg = dGate[kIdx];
                                    float du = dUp[kIdx];
                                    int rowOff = kIdx * embDim;
                                    for (int j = 0; j < embDim; j++)
                                    {
                                        seWgate.Grads[rowOff + j] += dg * xNormFfn[pos][l][j];
                                        seWup.Grads[rowOff + j] += du * xNormFfn[pos][l][j];
                                        dxNormFfn[j] += dg * sExp.Wgate[rowOff + j] + du * sExp.Wup[rowOff + j];
                                    }
                                }
                            }
                        }

                        // 2. Routed Experts Backprop
                        if (layer.Experts != null && layer.Experts.Length > 0 && moeTopIndices[pos][l] != null)
                        {
                            int expCount = layer.Experts.Length;
                            int kUsed = moeTopIndices[pos][l].Length;

                            Span<float> dTopWeights = dTopWeightsBuffer.Slice(0, kUsed);
                            dTopWeights.Clear();

                            for (int ki = 0; ki < kUsed; ki++)
                            {
                                int expIdx = moeTopIndices[pos][l][ki];
                                float w = moeTopWeights[pos][l][ki];
                                var exp = layer.Experts[expIdx];
                                var expParamsWdown = lParams.ExpertWdown![expIdx];
                                var expParamsWgate = lParams.ExpertWgate![expIdx];
                                var expParamsWup = lParams.ExpertWup![expIdx];

                                // 1. dw: derivative wrt routing weight w_k
                                float dw = 0f;
                                for (int d = 0; d < embDim; d++)
                                {
                                    dw += dx[d] * moeDown[pos][l][ki][d];
                                }
                                dTopWeights[ki] = dw;

                                // 2. Expert Wdown backprop
                                Array.Clear(dSwiglu, 0, ffnDim);
                                for (int o = 0; o < embDim; o++)
                                {
                                    float dX_o = dx[o] * w;
                                    if (dX_o == 0f) continue;
                                    int rowOff = o * ffnDim;
                                    for (int i = 0; i < ffnDim; i++)
                                    {
                                        expParamsWdown.Grads[rowOff + i] += dX_o * moeSwiglu[pos][l][ki][i];
                                        dSwiglu[i] += dX_o * exp.Wdown[rowOff + i];
                                    }
                                }

                                // 3. SwiGLU backward
                                for (int i = 0; i < ffnDim; i++)
                                {
                                    float gVal = moeGate[pos][l][ki][i];
                                    float uVal = moeUp[pos][l][ki][i];
                                    float sig = 1.0f / (1.0f + (float)Math.Exp(-gVal));
                                    float swish = gVal * sig;
                                    float dSwish = sig * (1.0f + gVal * (1.0f - sig));
                                    dUp[i] = dSwiglu[i] * swish;
                                    dGate[i] = dSwiglu[i] * uVal * dSwish;
                                }

                                // 4. Expert Wgate & Wup backprop
                                for (int kIdx = 0; kIdx < ffnDim; kIdx++)
                                {
                                    float dg = dGate[kIdx];
                                    float du = dUp[kIdx];
                                    int rowOff = kIdx * embDim;
                                    for (int j = 0; j < embDim; j++)
                                    {
                                        expParamsWgate.Grads[rowOff + j] += dg * xNormFfn[pos][l][j];
                                        expParamsWup.Grads[rowOff + j] += du * xNormFfn[pos][l][j];
                                        dxNormFfn[j] += dg * exp.Wgate[rowOff + j] + du * exp.Wup[rowOff + j];
                                    }
                                }
                            }

                            // 5. Router Gating Backprop (Softmax derivative over Top-K)
                            float dotVal = 0f;
                            for (int ki = 0; ki < kUsed; ki++)
                            {
                                dotVal += moeTopWeights[pos][l][ki] * dTopWeights[ki];
                            }

                            if (lParams.Wrouter != null)
                            {
                                for (int ki = 0; ki < kUsed; ki++)
                                {
                                    int expIdx = moeTopIndices[pos][l][ki];
                                    float p = moeTopWeights[pos][l][ki];
                                    float dLogit = p * (dTopWeights[ki] - dotVal);

                                    int rOff = expIdx * embDim;
                                    for (int j = 0; j < embDim; j++)
                                    {
                                        lParams.Wrouter.Grads[rOff + j] += dLogit * xNormFfn[pos][l][j];
                                        dxNormFfn[j] += dLogit * layer.Wrouter![rOff + j];
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        // Wdown backprop
                        Array.Clear(dSwiglu, 0, ffnDim);
                        for (int o = 0; o < embDim; o++)
                        {
                            float dX_o = dx[o];
                            if (dX_o == 0f) continue;
                            int rowOff = o * ffnDim;
                            for (int i = 0; i < ffnDim; i++)
                            {
                                lParams.Wdown!.Grads[rowOff + i] += dX_o * swiglu[pos][l][i];
                                dSwiglu[i] += dX_o * layer.Wdown[rowOff + i];
                            }
                        }

                        // SwiGLU backward
                        for (int i = 0; i < ffnDim; i++)
                        {
                            float gVal = gate[pos][l][i];
                            float uVal = up[pos][l][i];
                            float sig = 1.0f / (1.0f + (float)Math.Exp(-gVal));
                            float swish = gVal * sig;
                            float dSwish = sig * (1.0f + gVal * (1.0f - sig));
                            dUp[i] = dSwiglu[i] * swish;
                            dGate[i] = dSwiglu[i] * uVal * dSwish;
                        }

                        // Wgate & Wup backprop
                        Array.Clear(dxNormFfn, 0, embDim);
                        for (int kIdx = 0; kIdx < ffnDim; kIdx++)
                        {
                            float dg = dGate[kIdx];
                            float du = dUp[kIdx];
                            int rowOff = kIdx * embDim;
                            for (int j = 0; j < embDim; j++)
                            {
                                lParams.Wgate!.Grads[rowOff + j] += dg * xNormFfn[pos][l][j];
                                lParams.Wup!.Grads[rowOff + j] += du * xNormFfn[pos][l][j];
                                dxNormFfn[j] += dg * layer.Wgate[rowOff + j] + du * layer.Wup[rowOff + j];
                            }
                        }
                    }

                    // FfnNorm backward
                    BackpropRmsNorm(dxNormFfn, xMid[pos][l], layer.FfnNorm, lParams.FfnNorm.Grads, dResidFfn, cfg.RmsNormEps);

                    // Residual connection
                    for (int i = 0; i < embDim; i++)
                    {
                        dxMid[i] = dx[i] + dResidFfn[i];
                    }

                    // Wo backprop
                    Array.Clear(dAttnOut, 0, qDim);
                    for (int o = 0; o < embDim; o++)
                    {
                        float dXm_o = dxMid[o];
                        if (dXm_o == 0f) continue;
                        int rowOff = o * qDim;
                        for (int kIdx = 0; kIdx < qDim; kIdx++)
                        {
                            lParams.Wo.Grads[rowOff + kIdx] += dXm_o * attnOut[pos][l][kIdx];
                            dAttnOut[kIdx] += dXm_o * layer.Wo[rowOff + kIdx];
                        }
                    }

                    // Attention backward across tau = 0..pos
                    Array.Clear(dQ, 0, qDim);
                    BackwardAttention(
                        dAttnOut, q[pos][l], k, v, pos, l,
                        cfg.HeadCount, cfg.HeadCountKv, headDim,
                        attnWeights[pos][l], dQ, dK, dV);

                    // Inverse RoPE on dQ and dK
                    for (int h = 0; h < cfg.HeadCount; h++)
                    {
                        RoPE.ApplyInverseInplace(dQ.AsSpan(h * headDim, headDim), pos, headDim, cfg.RopeFreqBase);
                    }
                    for (int h = 0; h < cfg.HeadCountKv; h++)
                    {
                        RoPE.ApplyInverseInplace(dK[pos][l].AsSpan(h * headDim, headDim), pos, headDim, cfg.RopeFreqBase);
                    }

                    // Wq, Wk, Wv backprop
                    Array.Clear(dxNormAttn, 0, embDim);
                    for (int kIdx = 0; kIdx < qDim; kIdx++)
                    {
                        float dqVal = dQ[kIdx];
                        if (dqVal == 0f) continue;
                        int rowOff = kIdx * embDim;
                        for (int j = 0; j < embDim; j++)
                        {
                            lParams.Wq.Grads[rowOff + j] += dqVal * xNormAttn[pos][l][j];
                            dxNormAttn[j] += dqVal * layer.Wq[rowOff + j];
                        }
                    }

                    for (int kIdx = 0; kIdx < kvDim; kIdx++)
                    {
                        float dkVal = dK[pos][l][kIdx];
                        float dvVal = dV[pos][l][kIdx];
                        int rowOff = kIdx * embDim;
                        for (int j = 0; j < embDim; j++)
                        {
                            if (dkVal != 0f)
                            {
                                lParams.Wk.Grads[rowOff + j] += dkVal * xNormAttn[pos][l][j];
                                dxNormAttn[j] += dkVal * layer.Wk[rowOff + j];
                            }
                            if (dvVal != 0f)
                            {
                                lParams.Wv.Grads[rowOff + j] += dvVal * xNormAttn[pos][l][j];
                                dxNormAttn[j] += dvVal * layer.Wv[rowOff + j];
                            }
                        }
                    }

                    // AttnNorm backward
                    float[] prevInput = (l == 0) ? xIn[pos] : xOut[pos][l - 1];
                    BackpropRmsNorm(dxNormAttn, prevInput, layer.AttnNorm, lParams.AttnNorm.Grads, dResidAttn, cfg.RmsNormEps);

                    // Add to residual for next lower layer
                    for (int i = 0; i < embDim; i++)
                    {
                        dx[i] = dxMid[i] + dResidAttn[i];
                    }
                }

                // 3.4 Token embedding gradient
                int inputTok = tokens[pos];
                int safeInputTok = (inputTok >= 0 && inputTok < vocabSize) ? inputTok : 0;
                int embRow = safeInputTok * embDim;
                for (int i = 0; i < embDim; i++)
                {
                    _embParam.Grads[embRow + i] += dx[i];
                }
            }

            // 4. Auxiliary Load Balancing Loss for MoE layers
            if (hasMoE && _config.AuxiliaryLossWeight > 0f && activeTargets > 0)
            {
                float totalAuxLoss = 0f;
                Span<float> fullProbsBuffer = stackalloc float[cfg.ExpertCount > 0 ? cfg.ExpertCount : 8];

                for (int l = 0; l < layers; l++)
                {
                    var layer = _model.Layers[l];
                    if (!layer.IsMoE || layer.Experts == null || layer.Experts.Length == 0) continue;

                    int expCount = layer.Experts.Length;
                    int kUsed = Math.Min(Math.Max(1, cfg.ExpertUsedCount), expCount);

                    float[] fCount = new float[expCount];
                    float[] pMean = new float[expCount];
                    Span<float> fullProbs = fullProbsBuffer.Slice(0, expCount);

                    for (int pos = 0; pos < numPositions; pos++)
                    {
                        if (pos + 1 < targetStart) continue;

                        for (int ki = 0; ki < kUsed; ki++)
                        {
                            int idx = moeTopIndices[pos][l][ki];
                            if (idx >= 0 && idx < expCount) fCount[idx] += 1.0f;
                        }

                        // Softmax over all router logits
                        moeRouterLogits[pos][l].AsSpan().CopyTo(fullProbs);
                        ComputeSoftmax(fullProbs);

                        for (int e = 0; e < expCount; e++)
                        {
                            pMean[e] += fullProbs[e];
                        }
                    }

                    float invTotal = 1.0f / (activeTargets * kUsed);
                    float invTargets = 1.0f / activeTargets;
                    float layerAux = 0f;

                    for (int e = 0; e < expCount; e++)
                    {
                        float fe = fCount[e] * invTotal;
                        float pe = pMean[e] * invTargets;
                        layerAux += fe * pe;
                    }

                    totalAuxLoss += _config.AuxiliaryLossWeight * expCount * layerAux;
                }

                totalLoss += totalAuxLoss * activeTargets;
            }

            // 5. Parameter Update Step (subject to gradient accumulation)
            _accumulatedCount++;
            float gradNorm = 0f;
            if (_accumulatedCount >= accumSteps)
            {
                _step++;
                gradNorm = ApplyAdamW();
                _accumulatedCount = 0;
            }

            float avgLoss = totalLoss / activeTargets;
            return new TrainStepResult(avgLoss, activeTargets, gradNorm);
        }

        /// <summary>
        /// Flushes any pending accumulated gradients by executing an AdamW parameter update.
        /// </summary>
        public float FlushGradients()
        {
            if (_accumulatedCount > 0)
            {
                _step++;
                float gradNorm = ApplyAdamW();
                _accumulatedCount = 0;
                return gradNorm;
            }
            return 0f;
        }

        private float ApplyAdamW()
        {
            // Global gradient norm clipping (vectorized reduction across parameters)
            double sumSq = 0.0;
            object syncObj = new object();

            Parallel.For(0, _trainableParams.Count, () => 0.0, (p, loopState, localSum) =>
            {
                var grads = _trainableParams[p].Grads;
                int len = grads.Length;
                int vecSize = Vector<float>.Count;
                int simdLimit = len - (len % vecSize);

                unsafe
                {
                    fixed (float* pG = grads)
                    {
                        Vector<float> vAcc = Vector<float>.Zero;
                        for (int i = 0; i < simdLimit; i += vecSize)
                        {
                            var v = *(Vector<float>*)(pG + i);
                            vAcc += v * v;
                        }
                        for (int j = 0; j < vecSize; j++) localSum += vAcc[j];
                        for (int i = simdLimit; i < len; i++) localSum += pG[i] * pG[i];
                    }
                }
                return localSum;
            },
            localSum =>
            {
                lock (syncObj) sumSq += localSum;
            });

            float totalNorm = (float)Math.Sqrt(sumSq);
            float clipScale = 1.0f;
            if (_config.MaxGradNorm > 0 && totalNorm > _config.MaxGradNorm)
            {
                clipScale = _config.MaxGradNorm / (totalNorm + 1e-6f);
            }

            float b1 = _config.Beta1;
            float b2 = _config.Beta2;
            float lr = _config.LearningRate;
            float wd = _config.WeightDecay;
            float eps = _config.Epsilon;

            float bc1 = 1.0f - (float)Math.Pow(b1, _step);
            float bc2 = 1.0f - (float)Math.Pow(b2, _step);

            Parallel.For(0, _trainableParams.Count, p =>
            {
                var param = _trainableParams[p];
                var w = param.Weights;
                var g = param.Grads;
                var m = param.M;
                var v = param.V;

                int len = w.Length;
                int vecSize = Vector<float>.Count;
                int simdLimit = len - (len % vecSize);

                var vClip = new Vector<float>(clipScale);
                var vB1 = new Vector<float>(b1);
                var vOneMinusB1 = new Vector<float>(1.0f - b1);
                var vB2 = new Vector<float>(b2);
                var vOneMinusB2 = new Vector<float>(1.0f - b2);
                var vInvBc1 = new Vector<float>(1.0f / bc1);
                var vInvBc2 = new Vector<float>(1.0f / bc2);
                var vEps = new Vector<float>(eps);
                var vLr = new Vector<float>(lr);
                var vWd = new Vector<float>(wd);

                unsafe
                {
                    fixed (float* pW = w)
                    fixed (float* pG = g)
                    fixed (float* pM = m)
                    fixed (float* pV = v)
                    {
                        for (int i = 0; i < simdLimit; i += vecSize)
                        {
                            var vGrad = *(Vector<float>*)(pG + i) * vClip;
                            var vM_old = *(Vector<float>*)(pM + i);
                            var vV_old = *(Vector<float>*)(pV + i);

                            var vM_new = (vB1 * vM_old) + (vOneMinusB1 * vGrad);
                            var vV_new = (vB2 * vV_old) + (vOneMinusB2 * (vGrad * vGrad));

                            *(Vector<float>*)(pM + i) = vM_new;
                            *(Vector<float>*)(pV + i) = vV_new;

                            var vMHat = vM_new * vInvBc1;
                            var vVHat = vV_new * vInvBc2;

                            var vW_old = *(Vector<float>*)(pW + i);
                            var vDenom = Vector.SquareRoot(vVHat) + vEps;
                            var vStep = (vMHat / vDenom) + (vWd * vW_old);

                            *(Vector<float>*)(pW + i) = vW_old - (vLr * vStep);
                        }

                        for (int i = simdLimit; i < len; i++)
                        {
                            float grad = pG[i] * clipScale;
                            pM[i] = b1 * pM[i] + (1.0f - b1) * grad;
                            pV[i] = b2 * pV[i] + (1.0f - b2) * grad * grad;

                            float mHat = pM[i] / bc1;
                            float vHat = pV[i] / bc2;

                            pW[i] -= lr * (mHat / ((float)Math.Sqrt(vHat) + eps) + wd * pW[i]);
                        }
                    }
                }
            });

            return totalNorm;
        }

        private static void ForwardAttention(
            float[] q,
            float[][][] kAll,
            float[][][] vAll,
            int pos,
            int layer,
            int qHeadCount,
            int kvHeadCount,
            int headDim,
            float[] output,
            float[][] weightsOut)
        {
            int groupSize = qHeadCount / kvHeadCount;
            float scale = 1.0f / (float)Math.Sqrt(headDim);
            int seqLen = pos + 1;

            for (int qh = 0; qh < qHeadCount; qh++)
            {
                int kvHead = qh / groupSize;
                int qOffset = qh * headDim;
                int outOffset = qh * headDim;

                weightsOut[qh] = new float[seqLen];
                float maxScore = float.NegativeInfinity;

                // 1. Dot product
                for (int t = 0; t < seqLen; t++)
                {
                    var kVec = kAll[t][layer];
                    int kOffset = kvHead * headDim;

                    float dot = 0f;
                    for (int d = 0; d < headDim; d++)
                    {
                        dot += q[qOffset + d] * kVec[kOffset + d];
                    }
                    float s = dot * scale;
                    weightsOut[qh][t] = s;
                    if (s > maxScore) maxScore = s;
                }

                // 2. Softmax
                float sumExp = 0f;
                for (int t = 0; t < seqLen; t++)
                {
                    float exp = (float)Math.Exp(weightsOut[qh][t] - maxScore);
                    weightsOut[qh][t] = exp;
                    sumExp += exp;
                }

                float invSum = sumExp > 0f ? 1.0f / sumExp : 0f;
                for (int t = 0; t < seqLen; t++)
                {
                    weightsOut[qh][t] *= invSum;
                }

                // 3. Value aggregation
                for (int d = 0; d < headDim; d++) output[outOffset + d] = 0f;

                for (int t = 0; t < seqLen; t++)
                {
                    float w = weightsOut[qh][t];
                    if (w <= 0f) continue;

                    var vVec = vAll[t][layer];
                    int vOffset = kvHead * headDim;

                    for (int d = 0; d < headDim; d++)
                    {
                        output[outOffset + d] += w * vVec[vOffset + d];
                    }
                }
            }
        }

        private static void BackwardAttention(
            float[] dAttnOut,
            float[] q,
            float[][][] kAll,
            float[][][] vAll,
            int pos,
            int layer,
            int qHeadCount,
            int kvHeadCount,
            int headDim,
            float[][] weights,
            float[] dQ,
            float[][][] dK,
            float[][][] dV)
        {
            int groupSize = qHeadCount / kvHeadCount;
            float scale = 1.0f / (float)Math.Sqrt(headDim);
            int seqLen = pos + 1;

            // 1. dV accumulation and dWeights computation scratch buffer
            Span<float> dAlpha = stackalloc float[Math.Min(seqLen, 1024)];
            float[]? rentedAlpha = null;
            if (seqLen > 1024)
            {
                rentedAlpha = new float[seqLen];
                dAlpha = rentedAlpha;
            }

            for (int qh = 0; qh < qHeadCount; qh++)
            {
                int kvHead = qh / groupSize;
                int qOffset = qh * headDim;
                int outOffset = qh * headDim;

                for (int t = 0; t < seqLen; t++)
                {
                    float alpha = weights[qh][t];
                    var vVec = vAll[t][layer];
                    var dVVec = dV[t][layer];
                    int vOffset = kvHead * headDim;

                    float dot = 0f;
                    for (int d = 0; d < headDim; d++)
                    {
                        float dOut = dAttnOut[outOffset + d];
                        dVVec[vOffset + d] += alpha * dOut;
                        dot += dOut * vVec[vOffset + d];
                    }
                    dAlpha[t] = dot;
                }

                // 2. Softmax backward
                float sumAlphaDAlpha = 0f;
                for (int t = 0; t < seqLen; t++)
                {
                    sumAlphaDAlpha += weights[qh][t] * dAlpha[t];
                }

                for (int t = 0; t < seqLen; t++)
                {
                    float alpha = weights[qh][t];
                    float ds = alpha * (dAlpha[t] - sumAlphaDAlpha);

                    // 3. dQ and dK accumulation
                    var kVec = kAll[t][layer];
                    var dKVec = dK[t][layer];
                    int kOffset = kvHead * headDim;

                    float scaledDs = ds * scale;
                    for (int d = 0; d < headDim; d++)
                    {
                        dQ[qOffset + d] += scaledDs * kVec[kOffset + d];
                        dKVec[kOffset + d] += scaledDs * q[qOffset + d];
                    }
                }
            }
        }

        private static void BackpropRmsNorm(
            ReadOnlySpan<float> dy,
            ReadOnlySpan<float> x,
            ReadOnlySpan<float> weight,
            Span<float> dWeight,
            Span<float> dx,
            float eps = 1e-5f)
        {
            int len = x.Length;
            float sumSq = 0f;
            for (int i = 0; i < len; i++) sumSq += x[i] * x[i];
            float meanSq = sumSq / len;
            float invRms = 1.0f / (float)Math.Sqrt(meanSq + eps);
            float invRms3 = invRms * invRms * invRms;

            float sumDyWx = 0f;
            for (int i = 0; i < len; i++)
            {
                float dy_w = dy[i] * weight[i];
                sumDyWx += dy_w * x[i];
                dWeight[i] += dy[i] * x[i] * invRms;
            }

            float factor = (sumDyWx / len) * invRms3;
            for (int i = 0; i < len; i++)
            {
                dx[i] = invRms * dy[i] * weight[i] - x[i] * factor;
            }
        }

        private static unsafe void MatVec(ReadOnlySpan<float> input, ReadOnlySpan<float> weight, int inDim, int outDim, Span<float> output)
        {
            fixed (float* pInput = input)
            fixed (float* pWeight = weight)
            fixed (float* pOutput = output)
            {
                int vecSize = Vector<float>.Count;
                int simdLimit = inDim - (inDim % vecSize);

                if (outDim >= 1024)
                {
                    IntPtr inPtr = (IntPtr)pInput;
                    IntPtr wPtr = (IntPtr)pWeight;
                    IntPtr outPtr = (IntPtr)pOutput;

                    Parallel.For(0, outDim, o =>
                    {
                        float* pIn = (float*)inPtr;
                        float* pW = (float*)wPtr;
                        float* pOut = (float*)outPtr;

                        float* pRow = pW + (o * inDim);
                        Vector<float> vSum = Vector<float>.Zero;

                        for (int i = 0; i < simdLimit; i += vecSize)
                        {
                            var vIn = *(Vector<float>*)(pIn + i);
                            var vWRow = *(Vector<float>*)(pRow + i);
                            vSum += vIn * vWRow;
                        }

                        float dot = 0f;
                        for (int j = 0; j < vecSize; j++) dot += vSum[j];

                        for (int i = simdLimit; i < inDim; i++)
                        {
                            dot += pIn[i] * pRow[i];
                        }

                        pOut[o] = dot;
                    });
                }
                else
                {
                    for (int o = 0; o < outDim; o++)
                    {
                        float* pRow = pWeight + (o * inDim);
                        Vector<float> vSum = Vector<float>.Zero;

                        for (int i = 0; i < simdLimit; i += vecSize)
                        {
                            var vIn = *(Vector<float>*)(pInput + i);
                            var vW = *(Vector<float>*)(pRow + i);
                            vSum += vIn * vW;
                        }

                        float dot = 0f;
                        for (int j = 0; j < vecSize; j++) dot += vSum[j];

                        for (int i = simdLimit; i < inDim; i++)
                        {
                            dot += pInput[i] * pRow[i];
                        }

                        pOutput[o] = dot;
                    }
                }
            }
        }

        private static unsafe void MatVecAdd(ReadOnlySpan<float> input, ReadOnlySpan<float> weight, int inDim, int outDim, Span<float> accOutput)
        {
            fixed (float* pInput = input)
            fixed (float* pWeight = weight)
            fixed (float* pAcc = accOutput)
            {
                int vecSize = Vector<float>.Count;
                int simdLimit = inDim - (inDim % vecSize);

                for (int o = 0; o < outDim; o++)
                {
                    float* pRow = pWeight + (o * inDim);
                    Vector<float> vSum = Vector<float>.Zero;

                    for (int i = 0; i < simdLimit; i += vecSize)
                    {
                        var vIn = *(Vector<float>*)(pInput + i);
                        var vW = *(Vector<float>*)(pRow + i);
                        vSum += vIn * vW;
                    }

                    float dot = 0f;
                    for (int j = 0; j < vecSize; j++) dot += vSum[j];

                    for (int i = simdLimit; i < inDim; i++)
                    {
                        dot += pInput[i] * pRow[i];
                    }

                    pAcc[o] += dot;
                }
            }
        }

        /// <summary>
        /// Convenience training method for Supervised Fine-Tuning (SFT) over prompt-response pairs.
        /// Automatically formats tokens and sets targetStartPos to the start of the response.
        /// </summary>
        public TrainStepResult TrainStep(string prompt, string response, ITokenizer tokenizer)
        {
            if (tokenizer == null) throw new ArgumentNullException(nameof(tokenizer));

            int[] promptTokens = tokenizer.Encode(prompt ?? string.Empty);
            int[] responseTokens = tokenizer.Encode(response ?? string.Empty);

            var combined = new int[promptTokens.Length + responseTokens.Length];
            Array.Copy(promptTokens, 0, combined, 0, promptTokens.Length);
            Array.Copy(responseTokens, 0, combined, promptTokens.Length, responseTokens.Length);

            return TrainStep(combined, promptTokens.Length);
        }

        /// <summary>
        /// Runs a complete training epoch across an enumeration of token sequences.
        /// </summary>
        public TrainEpochResult TrainEpoch(
            IEnumerable<int[]> dataset,
            int epoch = 1,
            int? targetStartPos = null,
            Action<TrainProgress>? onProgress = null)
        {
            if (dataset == null) throw new ArgumentNullException(nameof(dataset));

            var sw = System.Diagnostics.Stopwatch.StartNew();
            float runningLoss = 0f;
            int totalTokens = 0;
            int step = 0;

            foreach (var seq in dataset)
            {
                step++;
                var result = TrainStep(seq, targetStartPos);
                runningLoss += result.Loss;
                totalTokens += result.TargetTokenCount;

                onProgress?.Invoke(new TrainProgress(step, -1, result.Loss, runningLoss / step, result.GradNorm));
            }

            FlushGradients();
            sw.Stop();
            return new TrainEpochResult
            {
                Epoch = epoch,
                AverageLoss = step > 0 ? runningLoss / step : 0f,
                TotalTrainedTokens = totalTokens,
                Elapsed = sw.Elapsed
            };
        }

        private static void SelectTopK(ReadOnlySpan<float> logits, Span<int> topIndices, Span<float> topWeights, int k)
        {
            int n = logits.Length;
            Span<bool> picked = stackalloc bool[n];

            for (int i = 0; i < k; i++)
            {
                float maxVal = float.MinValue;
                int maxIdx = 0;

                for (int j = 0; j < n; j++)
                {
                    if (!picked[j] && logits[j] > maxVal)
                    {
                        maxVal = logits[j];
                        maxIdx = j;
                    }
                }

                picked[maxIdx] = true;
                topIndices[i] = maxIdx;
                topWeights[i] = maxVal;
            }
        }

        private static void ComputeSoftmax(Span<float> weights)
        {
            int len = weights.Length;
            if (len == 0) return;

            float maxVal = weights[0];
            for (int i = 1; i < len; i++)
            {
                if (weights[i] > maxVal) maxVal = weights[i];
            }

            float sumExp = 0.0f;
            for (int i = 0; i < len; i++)
            {
                float expVal = (float)Math.Exp(weights[i] - maxVal);
                weights[i] = expVal;
                sumExp += expVal;
            }

            if (sumExp > 1e-12f)
            {
                float invSum = 1.0f / sumExp;
                for (int i = 0; i < len; i++)
                {
                    weights[i] *= invSum;
                }
            }
            else
            {
                float uniform = 1.0f / len;
                for (int i = 0; i < len; i++)
                {
                    weights[i] = uniform;
                }
            }
        }
    }
}
