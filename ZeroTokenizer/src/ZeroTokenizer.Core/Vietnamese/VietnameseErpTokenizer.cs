using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ZeroTokenizer.Core.Abstractions;
using ZeroTokenizer.Core.Bpe;

namespace ZeroTokenizer.Core.Vietnamese
{
    /// <summary>
    /// Specialized Byte-Pair Encoding (BPE) Tokenizer tailored for Vietnamese Enterprise Resource Planning (ERP)
    /// and Industrial Operational Systems.
    /// Incorporates full byte-level UTF-8 fallback, canonical agentic special tokens, and high-frequency Vietnamese business syllables.
    /// </summary>
    public sealed class VietnameseErpTokenizer : ITokenizer
    {
        private readonly BpeTokenizer _bpe;

        public int VocabularySize => _bpe.VocabularySize;
        public IReadOnlyDictionary<string, int> Vocabulary => _bpe.TokenToId;
        public IReadOnlyDictionary<(int, int), int> BpeRanks => _bpe.BpeRanks;
        public IReadOnlyCollection<string> SpecialTokensList => _bpe.SpecialTokens;

        public VietnameseErpTokenizer(BpeTokenizer bpe)
        {
            _bpe = bpe ?? throw new ArgumentNullException(nameof(bpe));
        }

        public int CountTokens(ReadOnlySpan<char> text) => _bpe.CountTokens(text);
        public int Encode(ReadOnlySpan<char> text, Span<int> destinationTokenIds) => _bpe.Encode(text, destinationTokenIds);
        public int[] Encode(string text) => _bpe.Encode(text);
        public string Decode(ReadOnlySpan<int> tokenIds) => _bpe.Decode(tokenIds);
        public int Decode(ReadOnlySpan<int> tokenIds, Span<char> destinationText) => _bpe.Decode(tokenIds, destinationText);

        /// <summary>
        /// Creates a production-ready Vietnamese ERP tokenizer pre-seeded with canonical special tokens,
        /// 256 byte-fallback tokens, high-frequency Vietnamese operational syllables, and standard ERP abbreviations.
        /// </summary>
        public static VietnameseErpTokenizer CreateDefault()
        {
            var vocab = new Dictionary<string, int>(StringComparer.Ordinal);
            var ranks = new Dictionary<(int, int), int>();
            int nextId = 0;

            // 1. Special tokens
            foreach (var sp in SpecialTokens.All)
            {
                if (!vocab.ContainsKey(sp))
                {
                    vocab[sp] = nextId++;
                }
            }

            // 2. Single-byte fallback tokens (<0x00> - <0xFF>)
            for (int b = 0; b < 256; b++)
            {
                string byteTok = $"<0x{b:X2}>";
                if (!vocab.ContainsKey(byteTok))
                {
                    vocab[byteTok] = nextId++;
                }
            }

            // 3. High-frequency Vietnamese syllables & ERP terminology
            string[] erpTokens = new[]
            {
                // Basic structural tokens & whitespace
                " ", "\n", "\r\n", "\t", ":", ";", ",", ".", "-", "_", "/", "\\", "(", ")", "[", "]", "{", "}", "=", "+", "*", "%",

                // Common Vietnamese words & conjunctions
                "là", "và", "của", "cho", "được", "có", "trong", "đã", "đang", "sẽ", "không", "với", "tại", "về",
                "các", "những", "này", "đó", "ra", "vào", "lại", "đến", "từ", "theo", "khi", "như", "nếu", "thì",
                "để", "do", "bởi", "vì", "vậy", "nên", "hay", "hoặc", "nhưng", "rồi", "qua", "lên", "xuống", "sau",
                "trước", "giữa", "dưới", "trên", "nào", "gì", "sao", "ai", "đâu", "mấy", "bao", "nhiêu", "xin", "lỗi",
                "cảm", "ơn", "vui", "lòng", "chào", "bạn", "tôi", "anh", "chị", "em", "ông", "bà", "hệ", "thống",

                // Inventory & Warehouse domain
                "kho", "tồn", "tồn kho", "nhập kho", "xuất kho", "chuyển kho", "thẻ kho", "vị trí", "kệ", "ô",
                "mã", "mã hàng", "sku", "SKU", "vật tư", "nguyên liệu", "phụ liệu", "thành phẩm", "bán thành phẩm",
                "kiểm kê", "tối thiểu", "tối đa", "an toàn", "khả dụng", "giữ chỗ", "FIFO", "LIFO",

                // Sales & Commercial domain
                "bán hàng", "đơn hàng", "đơn bán", "SO", "báo giá", "khách hàng", "đối tác", "hợp đồng",
                "chiết khấu", "doanh số", "doanh thu", "giao hàng", "vận chuyển", "công nợ", "phải thu",

                // Purchasing & Sourcing domain
                "mua hàng", "đơn mua", "PO", "nhà cung cấp", "đề xuất", "yêu cầu mua", "nhập khẩu", "báo giá ncc",

                // Production & Manufacturing domain
                "sản xuất", "lệnh sản xuất", "MO", "WO", "LSX", "định mức", "BOM", "chuyền", "chuyền may",
                "xưởng", "nhà máy", "ca làm", "kế hoạch", "tiến độ", "năng suất", "hoàn thành", "QC", "QA",

                // Finance & Accounting domain
                "tài chính", "kế toán", "sổ cái", "sổ quỹ", "tiền mặt", "ngân hàng", "tài khoản", "số dư",
                "hóa đơn", "hoá đơn", "VAT", "phiếu thu", "phiếu chi", "thu chi", "chi phí", "lợi nhuận",
                "VND", "VNĐ", "USD", "EUR", "triệu", "tỷ", "nghìn", "đồng",

                // Technical, Unit & Industrial metrics
                "thiết bị", "máy", "CNC", "PLC", "TSDB", "MES", "WMS", "SCADA", "IoT",
                "nhiệt độ", "áp suất", "độ rung", "công suất", "tốc độ", "lỗi", "cảnh báo", "khẩn cấp",
                "cái", "chiếc", "bộ", "kg", "Kg", "tấn", "thùng", "hộp", "cuộn", "mét", "m", "mm", "cm", "m2", "m3", "lít", "PCS", "pcs",
                "°C", "PSI", "bar", "rpm"
            };

            int rankCounter = 0;
            foreach (var tok in erpTokens)
            {
                if (!vocab.ContainsKey(tok))
                {
                    vocab[tok] = nextId++;
                }
            }

            // Build ranks for multi-word or composite tokens
            foreach (var tok in erpTokens)
            {
                if (tok.Length >= 2)
                {
                    string first = tok.Substring(0, 1);
                    string rest = tok.Substring(1);
                    if (vocab.TryGetValue(first, out int idA) && vocab.TryGetValue(rest, out int idB))
                    {
                        ranks[(idA, idB)] = rankCounter++;
                    }
                }
            }

            var bpe = new BpeTokenizer(vocab, ranks, SpecialTokens.All);
            return new VietnameseErpTokenizer(bpe);
        }

        /// <summary>
        /// Formats an agentic prompt integrating system role, domain expert boundary, user message,
        /// thought deliberation, tool execution, and final response tokens.
        /// </summary>
        public static string FormatAgentPrompt(
            string systemPrompt,
            string userMessage,
            string? domain = null,
            string? thought = null,
            string? toolCall = null,
            string? toolResult = null,
            string? response = null)
        {
            var sb = new StringBuilder();
            sb.Append(SpecialTokens.Bos);
            sb.Append(SpecialTokens.System).Append(systemPrompt).Append(SpecialTokens.System);

            if (!string.IsNullOrWhiteSpace(domain))
            {
                sb.Append(SpecialTokens.Expert).Append(domain).Append(SpecialTokens.EndExpert);
            }

            sb.Append(SpecialTokens.User).Append(userMessage).Append(SpecialTokens.User);

            if (!string.IsNullOrWhiteSpace(thought))
            {
                sb.Append(SpecialTokens.Thought).Append(thought).Append(SpecialTokens.EndThought);
            }

            if (!string.IsNullOrWhiteSpace(toolCall))
            {
                sb.Append(SpecialTokens.ToolCall).Append(toolCall).Append(SpecialTokens.EndToolCall);
            }

            if (!string.IsNullOrWhiteSpace(toolResult))
            {
                sb.Append(SpecialTokens.ToolResult).Append(toolResult).Append(SpecialTokens.EndToolResult);
            }

            if (!string.IsNullOrWhiteSpace(response))
            {
                sb.Append(SpecialTokens.Response).Append(response).Append(SpecialTokens.EndResponse);
            }
            else
            {
                sb.Append(SpecialTokens.Response);
            }

            return sb.ToString();
        }

        /// <summary>
        /// Serializes tokenizer vocabulary and BPE merge ranks to a stream writer in a fast, robust text format.
        /// </summary>
        public void Save(TextWriter writer)
        {
            if (writer == null) throw new ArgumentNullException(nameof(writer));

            writer.WriteLine("# ZERO_VIETNAMESE_ERP_BPE_V1");

            // Write special tokens
            writer.WriteLine($"[SPECIAL_TOKENS_COUNT]:{_bpe.SpecialTokens.Count}");
            foreach (var sp in _bpe.SpecialTokens)
            {
                writer.WriteLine(sp);
            }

            // Write vocabulary
            writer.WriteLine($"[VOCAB_COUNT]:{_bpe.TokenToId.Count}");
            foreach (var kvp in _bpe.TokenToId)
            {
                // Escape newlines and tabs
                string escaped = kvp.Key
                    .Replace("\\", "\\\\")
                    .Replace("\r", "\\r")
                    .Replace("\n", "\\n")
                    .Replace("\t", "\\t");
                writer.WriteLine($"{kvp.Value}\t{escaped}");
            }

            // Write merge ranks
            writer.WriteLine($"[RANKS_COUNT]:{_bpe.BpeRanks.Count}");
            foreach (var kvp in _bpe.BpeRanks)
            {
                writer.WriteLine($"{kvp.Key.Item1}\t{kvp.Key.Item2}\t{kvp.Value}");
            }
        }

        /// <summary>
        /// Deserializes a Vietnamese ERP tokenizer from a stream reader.
        /// </summary>
        public static VietnameseErpTokenizer Load(TextReader reader)
        {
            if (reader == null) throw new ArgumentNullException(nameof(reader));

            string? header = reader.ReadLine();
            if (header != "# ZERO_VIETNAMESE_ERP_BPE_V1")
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
                        string raw = entry.Substring(tabIdx + 1)
                            .Replace("\\r", "\r")
                            .Replace("\\n", "\n")
                            .Replace("\\t", "\t")
                            .Replace("\\\\", "\\");
                        vocab[raw] = id;
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
                    string[] parts = entry.Split('\t');
                    if (parts.Length == 3)
                    {
                        int id1 = int.Parse(parts[0]);
                        int id2 = int.Parse(parts[1]);
                        int rank = int.Parse(parts[2]);
                        ranks[(id1, id2)] = rank;
                    }
                }
            }

            var bpe = new BpeTokenizer(vocab, ranks, specialTokens);
            return new VietnameseErpTokenizer(bpe);
        }
    }
}
