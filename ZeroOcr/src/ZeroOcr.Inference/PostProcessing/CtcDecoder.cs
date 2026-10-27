using System;
using System.Collections.Generic;
using System.Text;
using ZeroOcr.Inference.Vocab;

namespace ZeroOcr.Inference.PostProcessing;

/// <summary>
/// Result of CTC decoding for a single recognized sequence.
/// </summary>
public sealed class CtcDecodeResult
{
    public string Text { get; }
    public float MeanConfidence { get; }
    public IReadOnlyList<float> CharacterConfidences { get; }

    public CtcDecodeResult(string text, float meanConfidence, IReadOnlyList<float> characterConfidences)
    {
        Text = text;
        MeanConfidence = meanConfidence;
        CharacterConfidences = characterConfidences;
    }
}

/// <summary>
/// High-speed Connectionist Temporal Classification (CTC) sequence decoder.
/// Extracts text sequences and true token probabilities from model softmax/log-softmax output tensors.
/// </summary>
public static class CtcDecoder
{
    /// <summary>
    /// Performs greedy argmax CTC decoding with blank token (index 0) suppression, duplicate collapsing,
    /// and temporal blank-gap whitespace reconstruction.
    /// </summary>
    /// <param name="probabilities">Flattened probabilities tensor with shape [timeSteps, vocabSize].</param>
    /// <param name="timeSteps">Number of temporal frame slices.</param>
    /// <param name="vocabSize">Size of the character dictionary.</param>
    /// <param name="charMap">Vocabulary character map.</param>
    /// <param name="blankIndex">Index representing the CTC blank symbol (default 0).</param>
    /// <param name="blankGapThreshold">Minimum consecutive blank frames to trigger word-space reconstruction (default 2).</param>
    /// <returns>Decoded string with per-character and mean confidence.</returns>
    public static CtcDecodeResult DecodeGreedy(
        ReadOnlySpan<float> probabilities,
        int timeSteps,
        int vocabSize,
        VietnameseCharacterMap charMap,
        int blankIndex = 0,
        int blankGapThreshold = 6)
    {
        if (timeSteps <= 0 || vocabSize <= 0)
            return new CtcDecodeResult(string.Empty, 0f, Array.Empty<float>());

        var sb = new StringBuilder(timeSteps);
        var confidences = new List<float>(timeSteps);

        int lastClass = -1;
        int blankRunLength = 0;

        for (int t = 0; t < timeSteps; t++)
        {
            int stepOffset = t * vocabSize;
            int maxIdx = 0;
            float maxProb = probabilities[stepOffset];

            for (int c = 1; c < vocabSize; c++)
            {
                float prob = probabilities[stepOffset + c];
                if (prob > maxProb)
                {
                    maxProb = prob;
                    maxIdx = c;
                }
            }

            if (maxIdx == blankIndex)
            {
                blankRunLength++;
            }
            else
            {
                // Temporal blank gap: if enough blank frames occurred since last non-blank token, insert a space
                if (blankGapThreshold > 0 && blankRunLength >= blankGapThreshold && sb.Length > 0 && sb[sb.Length - 1] != ' ')
                {
                    sb.Append(' ');
                    confidences.Add(0.95f);
                }
                blankRunLength = 0;

                // CTC rule: collapse identical consecutive tokens
                if (maxIdx != lastClass)
                {
                    char ch = charMap.GetChar(maxIdx);
                    // Avoid appending duplicate spaces if model explicitly emitted space
                    if (ch != ' ' || (sb.Length > 0 && sb[sb.Length - 1] != ' '))
                    {
                        sb.Append(ch);
                        confidences.Add(maxProb);
                    }
                }
            }

            lastClass = maxIdx;
        }

        string text = sb.ToString().Trim();
        float meanConf = 0f;
        if (confidences.Count > 0)
        {
            float sum = 0f;
            for (int i = 0; i < confidences.Count; i++) sum += confidences[i];
            meanConf = sum / confidences.Count;
        }

        return new CtcDecodeResult(text, meanConf, confidences);
    }
}
