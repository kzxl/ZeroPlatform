using System;
using System.Collections.Generic;

namespace ZeroLlm.Core.Sampling
{
    /// <summary>
    /// Delegate for applying arbitrary in-place logit modifications (e.g. grammar logit masking, token bans).
    /// </summary>
    public delegate void LogitProcessor(Span<float> logits);

    /// <summary>
    /// Delegate for applying context-aware in-place logit modifications with access to past tokens.
    /// </summary>
    public delegate void ContextAwareLogitProcessor(Span<float> logits, ReadOnlySpan<int> pastTokens);

    /// <summary>
    /// Pure C# token sampling engine supporting Greedy, Temperature scaling, Top-K, Top-P, Min-P, and Repetition Penalty.
    /// </summary>
    public static class LlmSampler
    {
        public static int Sample(
            Span<float> logits,
            ReadOnlySpan<int> pastTokens,
            SamplingConfig config,
            Random? random = null,
            LogitProcessor? logitProcessor = null,
            ContextAwareLogitProcessor? contextLogitProcessor = null)
        {
            if (logits.Length == 0) throw new ArgumentException("Logits span cannot be empty.", nameof(logits));

            // 0. Apply custom Logit Processors
            var proc = logitProcessor ?? config.LogitProcessor;
            proc?.Invoke(logits);

            var ctxProc = contextLogitProcessor ?? config.ContextLogitProcessor;
            ctxProc?.Invoke(logits, pastTokens);

            // 1. Apply Repetition Penalty
            if (config.RepetitionPenalty > 1.0f && pastTokens.Length > 0)
            {
                float penalty = config.RepetitionPenalty;
                for (int i = 0; i < pastTokens.Length; i++)
                {
                    int tok = pastTokens[i];
                    if ((uint)tok < (uint)logits.Length)
                    {
                        if (logits[tok] > 0)
                        {
                            logits[tok] /= penalty;
                        }
                        else
                        {
                            logits[tok] *= penalty;
                        }
                    }
                }
            }

            // 2. Greedy sampling (ArgMax)
            if (config.Temperature <= 0.001f || config.TopK == 1)
            {
                int bestIdx = 0;
                float bestVal = logits[0];
                for (int i = 1; i < logits.Length; i++)
                {
                    if (logits[i] > bestVal)
                    {
                        bestVal = logits[i];
                        bestIdx = i;
                    }
                }
                return bestIdx;
            }

            // 3. Temperature scaling
            float invTemp = 1.0f / config.Temperature;
            for (int i = 0; i < logits.Length; i++)
            {
                logits[i] *= invTemp;
            }

            // 4. Softmax over entire vocab or candidate pool
            float maxLogit = float.NegativeInfinity;
            for (int i = 0; i < logits.Length; i++)
            {
                if (logits[i] > maxLogit) maxLogit = logits[i];
            }

            float sumExp = 0.0f;
            for (int i = 0; i < logits.Length; i++)
            {
                float exp = (float)Math.Exp(logits[i] - maxLogit);
                logits[i] = exp;
                sumExp += exp;
            }

            float invSum = sumExp > 0 ? 1.0f / sumExp : 0.0f;
            for (int i = 0; i < logits.Length; i++)
            {
                logits[i] *= invSum;
            }

            // 5. Top-K & Top-P filtering
            var candidates = new List<KeyValuePair<int, float>>(config.TopK > 0 ? config.TopK : 128);
            for (int i = 0; i < logits.Length; i++)
            {
                if (logits[i] > 1e-6f)
                {
                    candidates.Add(new KeyValuePair<int, float>(i, logits[i]));
                }
            }

            candidates.Sort((a, b) => b.Value.CompareTo(a.Value));

            // Truncate by Top-K
            if (config.TopK > 0 && candidates.Count > config.TopK)
            {
                candidates.RemoveRange(config.TopK, candidates.Count - config.TopK);
            }

            // Truncate by Min-P
            if (config.MinP > 0.0f && candidates.Count > 0)
            {
                float minThreshold = candidates[0].Value * config.MinP;
                for (int i = candidates.Count - 1; i >= 1; i--)
                {
                    if (candidates[i].Value < minThreshold)
                    {
                        candidates.RemoveAt(i);
                    }
                }
            }

            // Truncate by Top-P (Nucleus)
            float cumulative = 0.0f;
            int cutoff = candidates.Count;
            for (int i = 0; i < candidates.Count; i++)
            {
                cumulative += candidates[i].Value;
                if (cumulative >= config.TopP)
                {
                    cutoff = i + 1;
                    break;
                }
            }

            if (cutoff < candidates.Count)
            {
                candidates.RemoveRange(cutoff, candidates.Count - cutoff);
            }

            // Renormalize candidate probabilities
            float totalP = 0.0f;
            for (int i = 0; i < candidates.Count; i++) totalP += candidates[i].Value;
            float invTotal = totalP > 0 ? 1.0f / totalP : 1.0f;

            // 6. Random selection via CDF
            random ??= new Random();
            float sample = (float)random.NextDouble();
            float running = 0.0f;

            for (int i = 0; i < candidates.Count; i++)
            {
                running += candidates[i].Value * invTotal;
                if (sample <= running)
                {
                    return candidates[i].Key;
                }
            }

            return candidates.Count > 0 ? candidates[0].Key : 0;
        }
    }
}
