using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ZeroTokenizer.Core.Abstractions;

namespace ZeroTokenizer.Core.Bpe
{
    /// <summary>
    /// Pure C# Byte-Pair Encoding (BPE) tokenizer with byte-level fallback.
    /// Guarantees that any UTF-8 string can be tokenized and restored losslessly without out-of-vocabulary (OOV) errors.
    /// </summary>
    public class BpeTokenizer : ITokenizer
    {
        private readonly Dictionary<string, int> _tokenToId;
        private readonly Dictionary<int, string> _idToToken;
        private readonly Dictionary<(int, int), int> _bpeRanks;
        private readonly HashSet<string> _specialTokens;
        private readonly int _maxTokenId;

        public int VocabularySize => _tokenToId.Count;
        public IReadOnlyDictionary<string, int> TokenToId => _tokenToId;
        public IReadOnlyDictionary<int, string> IdToToken => _idToToken;
        public IReadOnlyDictionary<(int, int), int> BpeRanks => _bpeRanks;
        public IReadOnlyCollection<string> SpecialTokens => _specialTokens;

        public BpeTokenizer(
            Dictionary<string, int> vocabulary,
            Dictionary<(int, int), int> bpeRanks,
            IEnumerable<string>? specialTokens = null)
        {
            _tokenToId = new Dictionary<string, int>(vocabulary);
            _idToToken = new Dictionary<int, string>(vocabulary.Count);
            _bpeRanks = new Dictionary<(int, int), int>(bpeRanks);
            _specialTokens = new HashSet<string>(specialTokens ?? Array.Empty<string>(), StringComparer.Ordinal);

            int maxId = 0;
            foreach (var kvp in vocabulary)
            {
                _idToToken[kvp.Value] = kvp.Key;
                if (kvp.Value > maxId) maxId = kvp.Value;
            }
            _maxTokenId = maxId;

            // Ensure special tokens are present in vocabulary
            foreach (var sp in _specialTokens)
            {
                if (!_tokenToId.ContainsKey(sp))
                {
                    int id = _tokenToId.Count;
                    _tokenToId[sp] = id;
                    _idToToken[id] = sp;
                }
            }

            // Ensure all 256 byte tokens exist
            EnsureByteTokens();
        }

        private void EnsureByteTokens()
        {
            for (int b = 0; b < 256; b++)
            {
                string byteStr = $"<0x{b:X2}>";
                if (!_tokenToId.ContainsKey(byteStr))
                {
                    int id = _tokenToId.Count;
                    _tokenToId[byteStr] = id;
                    _idToToken[id] = byteStr;
                }
            }
        }

        public int CountTokens(ReadOnlySpan<char> text)
        {
            if (text.IsEmpty) return 0;

            // Fast path approximation for count or full tokenization into a pooled buffer
            var tokens = EncodeInternal(text);
            return tokens.Count;
        }

        public int Encode(ReadOnlySpan<char> text, Span<int> destinationTokenIds)
        {
            if (text.IsEmpty) return 0;

            var tokens = EncodeInternal(text);
            if (destinationTokenIds.Length < tokens.Count)
                throw new ArgumentException($"Destination span too small. Required: {tokens.Count}, Available: {destinationTokenIds.Length}");

            for (int i = 0; i < tokens.Count; i++)
            {
                destinationTokenIds[i] = tokens[i];
            }
            return tokens.Count;
        }

        public int[] Encode(string text)
        {
            if (string.IsNullOrEmpty(text)) return Array.Empty<int>();

            var tokens = EncodeInternal(text.AsSpan());
            return tokens.ToArray();
        }

        public string Decode(ReadOnlySpan<int> tokenIds)
        {
            if (tokenIds.IsEmpty) return string.Empty;

            var sb = new StringBuilder();
            var byteBuffer = new List<byte>();

            for (int i = 0; i < tokenIds.Length; i++)
            {
                int id = tokenIds[i];
                if (_idToToken.TryGetValue(id, out string? token))
                {
                    if (IsByteTokenSequence(token))
                    {
                        ParseByteTokenSequence(token, byteBuffer);
                        continue;
                    }

                    // Flush any pending byte buffer
                    FlushBytes(sb, byteBuffer);
                    sb.Append(token);
                }
            }

            FlushBytes(sb, byteBuffer);
            return sb.ToString();
        }

        public int Decode(ReadOnlySpan<int> tokenIds, Span<char> destinationText)
        {
            string decoded = Decode(tokenIds);
            if (destinationText.Length < decoded.Length)
                throw new ArgumentException("Destination span too small.");

            decoded.AsSpan().CopyTo(destinationText);
            return decoded.Length;
        }

        private static bool IsByteTokenSequence(string token)
        {
            if (string.IsNullOrEmpty(token) || token.Length % 6 != 0) return false;
            for (int i = 0; i < token.Length; i += 6)
            {
                if (token[i] != '<' || token[i + 1] != '0' || token[i + 2] != 'x' || token[i + 5] != '>')
                    return false;
            }
            return true;
        }

        private static void ParseByteTokenSequence(string token, List<byte> byteBuffer)
        {
            for (int i = 0; i < token.Length; i += 6)
            {
                if (byte.TryParse(token.Substring(i + 3, 2), System.Globalization.NumberStyles.HexNumber, null, out byte b))
                {
                    byteBuffer.Add(b);
                }
            }
        }

        private static void FlushBytes(StringBuilder sb, List<byte> byteBuffer)
        {
            if (byteBuffer.Count > 0)
            {
                string text = Encoding.UTF8.GetString(byteBuffer.ToArray());
                sb.Append(text);
                byteBuffer.Clear();
            }
        }

        private List<int> EncodeInternal(ReadOnlySpan<char> text)
        {
            var result = new List<int>();
            if (text.IsEmpty) return result;

            int pos = 0;
            while (pos < text.Length)
            {
                // 1. Check if any special token matches at current position
                if (_specialTokens.Count > 0)
                {
                    string? matchedSpecial = null;
                    foreach (var sp in _specialTokens)
                    {
                        if (pos + sp.Length <= text.Length && text.Slice(pos, sp.Length).SequenceEqual(sp.AsSpan()))
                        {
                            if (matchedSpecial == null || sp.Length > matchedSpecial.Length)
                            {
                                matchedSpecial = sp;
                            }
                        }
                    }

                    if (matchedSpecial != null)
                    {
                        if (_tokenToId.TryGetValue(matchedSpecial, out int spId))
                        {
                            result.Add(spId);
                        }
                        pos += matchedSpecial.Length;
                        continue;
                    }
                }

                // 2. Find next special token boundary so text tokenization stops before it
                int limit = text.Length;
                if (_specialTokens.Count > 0)
                {
                    for (int checkPos = pos; checkPos < text.Length; checkPos++)
                    {
                        foreach (var sp in _specialTokens)
                        {
                            if (checkPos + sp.Length <= text.Length && text.Slice(checkPos, sp.Length).SequenceEqual(sp.AsSpan()))
                            {
                                limit = checkPos;
                                break;
                            }
                        }
                        if (limit < text.Length) break;
                    }
                }

                // 3. Tokenize regular text slice [pos, limit)
                EncodeTextSegment(text.Slice(pos, limit - pos), result);
                pos = limit;
            }

            return result;
        }

        private void EncodeTextSegment(ReadOnlySpan<char> text, List<int> result)
        {
            if (text.IsEmpty) return;

            int start = 0;
            while (start < text.Length)
            {
                // Word splitting by whitespace
                int end = start;
                bool isSpace = char.IsWhiteSpace(text[start]);

                while (end < text.Length && char.IsWhiteSpace(text[end]) == isSpace)
                {
                    end++;
                }

                string segment = text.Slice(start, end - start).ToString();
                start = end;

                // Check if segment itself is a known token
                if (_tokenToId.TryGetValue(segment, out int directId))
                {
                    result.Add(directId);
                    continue;
                }

                // Convert segment to UTF-8 bytes and BPE merge
                byte[] bytes = Encoding.UTF8.GetBytes(segment);
                var wordTokens = new List<int>(bytes.Length);
                for (int b = 0; b < bytes.Length; b++)
                {
                    string byteStr = $"<0x{bytes[b]:X2}>";
                    if (_tokenToId.TryGetValue(byteStr, out int bId))
                    {
                        wordTokens.Add(bId);
                    }
                }

                // Repeatedly merge lowest-rank pairs
                BpeMerge(wordTokens);

                result.AddRange(wordTokens);
            }
        }

        private void BpeMerge(List<int> tokens)
        {
            if (tokens.Count <= 1) return;

            while (tokens.Count > 1)
            {
                int bestPairIndex = -1;
                int minRank = int.MaxValue;

                for (int i = 0; i < tokens.Count - 1; i++)
                {
                    var pair = (tokens[i], tokens[i + 1]);
                    if (_bpeRanks.TryGetValue(pair, out int rank))
                    {
                        if (rank < minRank)
                        {
                            minRank = rank;
                            bestPairIndex = i;
                        }
                    }
                }

                if (bestPairIndex == -1)
                {
                    // No more mergeable pairs
                    break;
                }

                // Merge tokens[bestPairIndex] and tokens[bestPairIndex + 1]
                int tokenA = tokens[bestPairIndex];
                int tokenB = tokens[bestPairIndex + 1];

                string strA = _idToToken.TryGetValue(tokenA, out var sa) ? sa : "";
                string strB = _idToToken.TryGetValue(tokenB, out var sb) ? sb : "";
                string merged = strA + strB;

                if (_tokenToId.TryGetValue(merged, out int mergedId))
                {
                    tokens[bestPairIndex] = mergedId;
                    tokens.RemoveAt(bestPairIndex + 1);
                }
                else
                {
                    break;
                }
            }
        }

        /// <summary>
        /// Serializes tokenizer vocabulary and BPE merge ranks to a stream writer in a fast, robust text format.
        /// </summary>
        public void Save(TextWriter writer)
        {
            if (writer == null) throw new ArgumentNullException(nameof(writer));

            writer.WriteLine("# ZERO_BPE_V1");

            // Write special tokens
            writer.WriteLine($"[SPECIAL_TOKENS_COUNT]:{_specialTokens.Count}");
            foreach (var sp in _specialTokens)
            {
                writer.WriteLine(sp);
            }

            // Write vocabulary
            writer.WriteLine($"[VOCAB_COUNT]:{_tokenToId.Count}");
            foreach (var kvp in _tokenToId)
            {
                string escaped = kvp.Key
                    .Replace("\\", "\\\\")
                    .Replace("\r", "\\r")
                    .Replace("\n", "\\n")
                    .Replace("\t", "\\t");
                writer.WriteLine($"{kvp.Value}\t{escaped}");
            }

            // Write merge ranks
            writer.WriteLine($"[RANKS_COUNT]:{_bpeRanks.Count}");
            foreach (var kvp in _bpeRanks)
            {
                writer.WriteLine($"{kvp.Key.Item1}\t{kvp.Key.Item2}\t{kvp.Value}");
            }
        }

        public void Save(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) throw new ArgumentNullException(nameof(filePath));
            using (var sw = new StreamWriter(filePath, false, Encoding.UTF8))
            {
                Save(sw);
            }
        }

        /// <summary>
        /// Deserializes a BPE tokenizer from a stream reader.
        /// </summary>
        public static BpeTokenizer Load(TextReader reader)
        {
            if (reader == null) throw new ArgumentNullException(nameof(reader));

            string? header = reader.ReadLine();
            if (header != "# ZERO_BPE_V1" && header != "# ZERO_VIETNAMESE_ERP_BPE_V1")
            {
                throw new InvalidDataException("Invalid tokenizer format or unsupported header.");
            }

            var specialTokens = new List<string>();
            var vocab = new Dictionary<string, int>(StringComparer.Ordinal);
            var ranks = new Dictionary<(int, int), int>();

            string? line = reader.ReadLine();
            if (line != null && line.StartsWith("[SPECIAL_TOKENS_COUNT]:"))
            {
                int count = int.Parse(line.Substring("[SPECIAL_TOKENS_COUNT]:".Length));
                for (int i = 0; i < count; i++)
                {
                    specialTokens.Add(reader.ReadLine() ?? string.Empty);
                }
                line = reader.ReadLine();
            }

            if (line != null && line.StartsWith("[VOCAB_COUNT]:"))
            {
                int count = int.Parse(line.Substring("[VOCAB_COUNT]:".Length));
                for (int i = 0; i < count; i++)
                {
                    string? entry = reader.ReadLine();
                    if (string.IsNullOrEmpty(entry)) continue;

                    int tabIdx = entry.IndexOf('\t');
                    if (tabIdx > 0)
                    {
                        int id = int.Parse(entry.Substring(0, tabIdx));
                        string rawKey = entry.Substring(tabIdx + 1)
                            .Replace("\\t", "\t")
                            .Replace("\\n", "\n")
                            .Replace("\\r", "\r")
                            .Replace("\\\\", "\\");
                        vocab[rawKey] = id;
                    }
                }
                line = reader.ReadLine();
            }

            if (line != null && line.StartsWith("[RANKS_COUNT]:"))
            {
                int count = int.Parse(line.Substring("[RANKS_COUNT]:".Length));
                for (int i = 0; i < count; i++)
                {
                    string? entry = reader.ReadLine();
                    if (string.IsNullOrEmpty(entry)) continue;

                    var parts = entry.Split('\t');
                    if (parts.Length >= 3)
                    {
                        int p1 = int.Parse(parts[0]);
                        int p2 = int.Parse(parts[1]);
                        int rank = int.Parse(parts[2]);
                        ranks[(p1, p2)] = rank;
                    }
                }
            }

            return new BpeTokenizer(vocab, ranks, specialTokens);
        }

        public static BpeTokenizer Load(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) throw new ArgumentNullException(nameof(filePath));
            using (var sr = new StreamReader(filePath, Encoding.UTF8))
            {
                return Load(sr);
            }
        }
    }
}
