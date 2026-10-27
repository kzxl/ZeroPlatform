using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ZeroAgent.Dialog.Embedding;
using ZeroAgent.Dialog.Neural;
using ZeroVector.Core.Metrics;

namespace ZeroAgent.Dialog.DST
{
    /// <summary>
    /// Dialogue State Tracker (DST).
    /// Extracts slots, resolves pending requests, and tracks conversation state across multiple turns.
    /// Supports hybrid intent classification (Deterministic fast-path + Deep Neural MLP + Lexical Cosine).
    /// </summary>
    public sealed partial class DialogueStateTracker
    {
        private readonly List<DialogueIntent> _intents = new List<DialogueIntent>();
        private readonly LexicalSemanticEmbedder _embedder;
        private readonly List<(DialogueIntent Intent, float[] SampleEmbedding)> _sampleEmbeddings = new List<(DialogueIntent, float[])>();

        /// <summary>
        /// Gets or sets the optional deep neural intent classifier.
        /// </summary>
        public INeuralIntentClassifier? NeuralClassifier { get; set; }

        public DialogueStateTracker(LexicalSemanticEmbedder embedder)
        {
            _embedder = embedder ?? throw new ArgumentNullException(nameof(embedder));
        }

        public void RegisterIntent(DialogueIntent intent)
        {
            if (intent == null) throw new ArgumentNullException(nameof(intent));
            _intents.Add(intent);

            foreach (var sample in intent.SampleUtterances)
            {
                var vec = _embedder.Embed(sample);
                _sampleEmbeddings.Add((intent, vec));
            }
        }

        /// <summary>
        /// Automatically trains and attaches the deep neural intent classifier on all registered intents.
        /// </summary>
        public void EnableNeuralClassifier(int epochs = 80, float learningRate = 0.05f)
        {
            var neural = new NeuralIntentClassifier(_embedder);
            neural.Train(_intents, epochs, learningRate);
            NeuralClassifier = neural;
        }

        /// <summary>
        /// Matches the most semantically relevant intent for the given query vector.
        /// Supports Hybrid routing: Deterministic fast-path -> Deep Neural MLP -> Lexical Cosine fallback.
        /// Resilient against unaccented Vietnamese, typos, and keyword occurrences.
        /// </summary>
        public (DialogueIntent? Intent, float Score) MatchIntent(ReadOnlySpan<float> queryEmbedding, string rawText)
        {
            string cleanRaw = rawText.Trim();
            string rawNormalized = NormalizeDiacritics(cleanRaw);

            // 1. Exact phrase / keyword match (Deterministic Fast Path)
            for (int i = 0; i < _sampleEmbeddings.Count; i++)
            {
                var item = _sampleEmbeddings[i];
                for (int s = 0; s < item.Intent.SampleUtterances.Count; s++)
                {
                    string sample = item.Intent.SampleUtterances[s];
                    if (string.Equals(cleanRaw, sample, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(rawNormalized, NormalizeDiacritics(sample), StringComparison.OrdinalIgnoreCase))
                    {
                        return (item.Intent, 1.0f);
                    }
                }
            }

            // 2. Neural Deep Learning Inference (if trained and confident)
            if (NeuralClassifier != null && NeuralClassifier.IsTrained)
            {
                var (neuralIntent, confidence, _) = NeuralClassifier.Predict(queryEmbedding);
                if (neuralIntent != null && confidence >= 0.70f)
                {
                    // Gatekeeper against closed-world Softmax overconfidence:
                    // Verify the query actually has positive semantic proximity to the intent's sample utterances.
                    float maxSampleSim = GetMaxSampleSimilarity(queryEmbedding, neuralIntent);
                    if (maxSampleSim >= 0.28f)
                    {
                        return (neuralIntent, confidence);
                    }
                }
            }

            // 3. Fallback to Lexical Cosine Similarity matching
            DialogueIntent? bestIntent = null;
            float maxScore = -1.0f;

            for (int i = 0; i < _sampleEmbeddings.Count; i++)
            {
                var item = _sampleEmbeddings[i];
                float sim = VectorMetrics.CosineSimilarity(queryEmbedding, item.SampleEmbedding);

                // Exact phrase / keyword boost (supporting both accented and unaccented Vietnamese)
                for (int s = 0; s < item.Intent.SampleUtterances.Count; s++)
                {
                    string sample = item.Intent.SampleUtterances[s];
                    if (cleanRaw.IndexOf(sample, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        rawNormalized.IndexOf(NormalizeDiacritics(sample), StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        sim += 0.35f;
                        break;
                    }
                }

                if (sim > maxScore)
                {
                    maxScore = sim;
                    bestIntent = item.Intent;
                }
            }

            if (maxScore < 0.25f)
            {
                return (null, maxScore);
            }

            return (bestIntent, maxScore);
        }

        private static string NormalizeDiacritics(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            string normalizedString = text.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(normalizedString.Length);

            for (int i = 0; i < normalizedString.Length; i++)
            {
                char c = normalizedString[i];
                var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                {
                    sb.Append(c);
                }
            }

            return sb.ToString().Normalize(NormalizationForm.FormC).Replace('đ', 'd').Replace('Đ', 'D');
        }

        private float GetMaxSampleSimilarity(ReadOnlySpan<float> queryEmbedding, DialogueIntent intent)
        {
            float max = -1.0f;
            for (int i = 0; i < _sampleEmbeddings.Count; i++)
            {
                if (ReferenceEquals(_sampleEmbeddings[i].Intent, intent))
                {
                    float sim = VectorMetrics.CosineSimilarity(queryEmbedding, _sampleEmbeddings[i].SampleEmbedding);
                    if (sim > max) max = sim;
                }
            }
            return max;
        }
    }
}
