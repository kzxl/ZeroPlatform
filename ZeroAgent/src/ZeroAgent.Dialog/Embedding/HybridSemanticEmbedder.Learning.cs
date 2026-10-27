using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ZeroVector.Core.Metrics;

namespace ZeroAgent.Dialog.Embedding
{
    public sealed partial class HybridSemanticEmbedder
    {
        private readonly List<(string TextA, string TextB, bool IsSimilar)> _replayBuffer = new List<(string, string, bool)>();
        private const int MaxReplayBufferSize = 500;

        /// <summary>
        /// Populates default domain synonyms for industrial and manufacturing dialogues.
        /// </summary>
        private void SeedDefaultIndustrialSynonyms()
        {
            AddSynonymGroup("động cơ", "motor", "mô tơ", "engine", "máy");
            AddSynonymGroup("dừng", "ngắt điện", "tắt", "stop", "shutdown", "ngắt nguồn", "dừng khẩn cấp");
            AddSynonymGroup("nhiệt độ", "nhiệt", "nóng", "temperature", "temp", "quá nhiệt");
            AddSynonymGroup("áp suất", "áp lực", "pressure", "psi", "bar", "tăng áp");
            AddSynonymGroup("độ rung", "rung", "vibration", "bearing", "bạc đạn");
            AddSynonymGroup("sự cố", "lỗi", "hỏng", "hư", "fault", "defect", "error", "alarm", "cảnh báo");
            AddSynonymGroup("quy trình", "hướng dẫn", "tài liệu", "sop", "manual", "hướng dẫn vận hành");
        }

        /// <summary>
        /// Registers a cluster of synonyms and binds them to a shared semantic anchor vector.
        /// Guarantees high cosine similarity between any synonyms in the cluster regardless of character differences.
        /// </summary>
        public void AddSynonymGroup(params string[] words)
        {
            if (words == null || words.Length < 2) return;

            var cluster = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var w in words)
            {
                string clean = w.Trim().ToLowerInvariant();
                if (!string.IsNullOrEmpty(clean)) cluster.Add(clean);
            }

            if (cluster.Count < 2) return;

            // Compute composite anchor vector across all members
            float[] anchor = new float[Dimension];
            Span<float> temp = stackalloc float[Dimension];

            foreach (var word in cluster)
            {
                base.Embed(word, temp);
                for (int d = 0; d < Dimension; d++) anchor[d] += temp[d];
            }

            VectorMetrics.NormalizeL2(anchor);

            lock (_clusterLock)
            {
                _synonymClusters.Add(cluster);
                foreach (var word in cluster)
                {
                    _semanticCodebook[word] = (float[])anchor.Clone();
                    _semanticCodebook[word.Replace(' ', '_')] = (float[])anchor.Clone();
                }
            }
        }

        /// <summary>
        /// Performs online continual learning with contrastive margin loss on CPU in sub-millisecond time.
        /// If isSimilar is true, pulls the representations of textA and textB closer together.
        /// If isSimilar is false, pushes them apart to avoid false positive matches.
        /// Automatically replays past verified samples to prevent catastrophic forgetting.
        /// </summary>
        public void OnlineLearnPair(string textA, string textB, bool isSimilar, float learningRate = 0.05f)
        {
            if (string.IsNullOrWhiteSpace(textA) || string.IsNullOrWhiteSpace(textB)) return;

            lock (_trainLock)
            {
                ExecuteContrastiveStep(textA, textB, isSimilar, learningRate);

                // Add to experience replay buffer
                if (_replayBuffer.Count >= MaxReplayBufferSize)
                {
                    _replayBuffer.RemoveAt(0);
                }
                _replayBuffer.Add((textA, textB, isSimilar));

                // Replay 2 random historical samples to regularize weights
                if (_replayBuffer.Count > 4)
                {
                    var rnd = new Random();
                    for (int r = 0; r < 2; r++)
                    {
                        var past = _replayBuffer[rnd.Next(_replayBuffer.Count)];
                        ExecuteContrastiveStep(past.TextA, past.TextB, past.IsSimilar, learningRate * 0.5f);
                    }
                }
            }
        }

        private void ExecuteContrastiveStep(string textA, string textB, bool isSimilar, float lr)
        {
            var vecA = Embed(textA);
            var vecB = Embed(textB);
            float sim = VectorMetrics.CosineSimilarity(vecA, vecB);

            // Margin parameters
            float targetSim = isSimilar ? 0.90f : 0.15f;
            float diff = targetSim - sim;

            if (isSimilar && diff <= 0.05f) return; // Already sufficiently close
            if (!isSimilar && diff >= -0.05f) return; // Already sufficiently distant

            float gradientScale = lr * diff;

            // Update latent codebook vectors for extracted terms in textA and textB
            UpdateTermsCodebook(textA, vecB, gradientScale);
            UpdateTermsCodebook(textB, vecA, gradientScale);
        }

        private void UpdateTermsCodebook(string text, float[] targetVector, float scale)
        {
            string clean = text.Trim().ToLowerInvariant();
            var words = clean.Split(new[] { ' ', '\t', '\r', '\n', ',', '.', ';', ':', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < words.Length; i++)
            {
                string w = words[i];
                if (w.Length >= 2)
                {
                    var vec = _semanticCodebook.GetOrAdd(w, _ => Embed(w));
                    for (int d = 0; d < Dimension; d++)
                    {
                        vec[d] += targetVector[d] * scale;
                    }
                    VectorMetrics.NormalizeL2(vec);
                }
            }
        }

        /// <summary>
        /// Online feedback method: Reinforces or penalizes association between a user utterance and an intent category.
        /// </summary>
        public void OnlineLearnIntent(string userQuery, string targetIntent, bool isPositive)
        {
            OnlineLearnPair(userQuery, targetIntent, isPositive, learningRate: 0.08f);
        }

        /// <summary>
        /// Saves the learned semantic codebook and projection state to a binary stream.
        /// </summary>
        public void Save(Stream stream)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
            {
                // Magic header & version
                writer.Write("ZSEM");
                writer.Write((int)1); // Version
                writer.Write(Dimension);
                writer.Write(Alpha);

                // Codebook
                var entries = _semanticCodebook.ToArray();
                writer.Write(entries.Length);
                foreach (var kvp in entries)
                {
                    writer.Write(kvp.Key);
                    for (int d = 0; d < Dimension; d++)
                    {
                        writer.Write(kvp.Value[d]);
                    }
                }
            }
        }

        /// <summary>
        /// Loads learned semantic codebook entries from a binary stream.
        /// </summary>
        public void Load(Stream stream)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            using (var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true))
            {
                string magic = reader.ReadString();
                if (magic != "ZSEM") throw new InvalidDataException("Invalid semantic model format.");

                int version = reader.ReadInt32();
                int dim = reader.ReadInt32();
                if (dim != Dimension) throw new InvalidDataException($"Dimension mismatch: model={dim}, current={Dimension}");

                Alpha = reader.ReadSingle();

                int count = reader.ReadInt32();
                for (int i = 0; i < count; i++)
                {
                    string key = reader.ReadString();
                    float[] vec = new float[Dimension];
                    for (int d = 0; d < Dimension; d++)
                    {
                        vec[d] = reader.ReadSingle();
                    }
                    _semanticCodebook[key] = vec;
                }
            }
        }
    }
}
