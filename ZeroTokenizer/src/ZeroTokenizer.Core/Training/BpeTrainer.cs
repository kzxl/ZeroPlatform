using System;
using System.Collections.Generic;
using System.Text;
using ZeroTokenizer.Core.Bpe;

namespace ZeroTokenizer.Core.Training
{
    /// <summary>
    /// Pure C# Byte-Pair Encoding (BPE) Vocabulary Trainer.
    /// Learns statistical subword and phrase merges from an arbitrary text corpus without external Python or native dependencies.
    /// Produces a deterministic, compact vocabulary suitable for domain-specific Small Language Models (SLMs).
    /// </summary>
    public sealed class BpeTrainer
    {
        public int TargetVocabSize { get; }
        public int MinFrequency { get; }
        public IReadOnlyList<string> SpecialTokens { get; }

        public BpeTrainer(
            int targetVocabSize = 8000,
            int minFrequency = 2,
            IEnumerable<string>? specialTokens = null)
        {
            if (targetVocabSize < 300)
            {
                throw new ArgumentException("Target vocabulary size must be at least 300 to accommodate special tokens and 256 byte tokens.", nameof(targetVocabSize));
            }

            TargetVocabSize = targetVocabSize;
            MinFrequency = Math.Max(1, minFrequency);
            SpecialTokens = new List<string>(specialTokens ?? Core.SpecialTokens.All);
        }

        /// <summary>
        /// Trains a BPE tokenizer from an input text corpus.
        /// </summary>
        public BpeTokenizer Train(IEnumerable<string> corpus)
        {
            if (corpus == null) throw new ArgumentNullException(nameof(corpus));

            var vocab = new Dictionary<string, int>(StringComparer.Ordinal);
            var idToToken = new Dictionary<int, string>();
            var ranks = new Dictionary<(int, int), int>();
            int nextId = 0;

            // 1. Add Special tokens
            foreach (var sp in SpecialTokens)
            {
                if (!vocab.ContainsKey(sp))
                {
                    vocab[sp] = nextId;
                    idToToken[nextId] = sp;
                    nextId++;
                }
            }

            // 2. Add 256 single-byte fallback tokens (<0x00> - <0xFF>)
            for (int b = 0; b < 256; b++)
            {
                string byteTok = $"<0x{b:X2}>";
                if (!vocab.ContainsKey(byteTok))
                {
                    vocab[byteTok] = nextId;
                    idToToken[nextId] = byteTok;
                    nextId++;
                }
            }

            // 3. Pre-tokenize corpus into words and initial byte token sequences
            var wordFrequencies = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var doc in corpus)
            {
                if (string.IsNullOrWhiteSpace(doc)) continue;

                // Split words by whitespace
                string[] words = doc.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                foreach (var w in words)
                {
                    if (wordFrequencies.TryGetValue(w, out int count))
                    {
                        wordFrequencies[w] = count + 1;
                    }
                    else
                    {
                        wordFrequencies[w] = 1;
                    }
                }
            }

            // Convert unique words to lists of token IDs
            var tokenizedWords = new List<(List<int> Tokens, int Freq)>(wordFrequencies.Count);
            foreach (var kvp in wordFrequencies)
            {
                byte[] bytes = Encoding.UTF8.GetBytes(kvp.Key);
                var tokens = new List<int>(bytes.Length);
                for (int i = 0; i < bytes.Length; i++)
                {
                    string byteStr = $"<0x{bytes[i]:X2}>";
                    tokens.Add(vocab[byteStr]);
                }
                tokenizedWords.Add((tokens, kvp.Value));
            }

            // 4. Iterative BPE Merge Loop
            int currentRank = 0;
            while (vocab.Count < TargetVocabSize)
            {
                // Count pair frequencies
                var pairFreqs = new Dictionary<(int, int), int>();

                for (int w = 0; w < tokenizedWords.Count; w++)
                {
                    var (tokens, freq) = tokenizedWords[w];
                    if (tokens.Count < 2) continue;

                    for (int i = 0; i < tokens.Count - 1; i++)
                    {
                        var pair = (tokens[i], tokens[i + 1]);
                        if (pairFreqs.TryGetValue(pair, out int pCount))
                        {
                            pairFreqs[pair] = pCount + freq;
                        }
                        else
                        {
                            pairFreqs[pair] = freq;
                        }
                    }
                }

                if (pairFreqs.Count == 0) break;

                // Find highest frequency pair
                (int, int) bestPair = default;
                int maxFreq = -1;

                foreach (var kvp in pairFreqs)
                {
                    if (kvp.Value > maxFreq)
                    {
                        maxFreq = kvp.Value;
                        bestPair = kvp.Key;
                    }
                }

                if (maxFreq < MinFrequency)
                {
                    // No pairs meet minimum frequency threshold
                    break;
                }

                // Create merged token string
                string strA = idToToken[bestPair.Item1];
                string strB = idToToken[bestPair.Item2];

                // If byte tokens, decode or concatenate cleanly
                string mergedStr = strA + strB;
                int newId = nextId++;
                vocab[mergedStr] = newId;
                idToToken[newId] = mergedStr;
                ranks[bestPair] = currentRank++;

                // Replace all occurrences of bestPair in tokenizedWords
                for (int w = 0; w < tokenizedWords.Count; w++)
                {
                    var tokens = tokenizedWords[w].Tokens;
                    if (tokens.Count < 2) continue;

                    int i = 0;
                    while (i < tokens.Count - 1)
                    {
                        if (tokens[i] == bestPair.Item1 && tokens[i + 1] == bestPair.Item2)
                        {
                            tokens[i] = newId;
                            tokens.RemoveAt(i + 1);
                        }
                        else
                        {
                            i++;
                        }
                    }
                }
            }

            return new BpeTokenizer(vocab, ranks, SpecialTokens);
        }
    }
}
