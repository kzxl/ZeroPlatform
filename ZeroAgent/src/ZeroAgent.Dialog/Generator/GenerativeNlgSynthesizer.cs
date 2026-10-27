using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ZeroAgent.Core.Engine;

namespace ZeroAgent.Dialog.Generator
{
    /// <summary>
    /// Generative NLG synthesizer powered by an <see cref="ILlmClient"/> (Micro-SLM, 9Router, or External API).
    /// Dynamically verbalizes verified system facts into natural, grammatically fluent Vietnamese prose.
    /// Gracefully falls back to template synthesis if the generative engine is unavailable.
    /// </summary>
    public sealed class GenerativeNlgSynthesizer : INlgSynthesizer
    {
        private readonly ILlmClient _llmClient;
        private readonly INlgSynthesizer _fallback;
        private readonly string? _customSystemDirective;

        public ILlmClient LlmClient => _llmClient;
        public INlgSynthesizer FallbackSynthesizer => _fallback;

        public GenerativeNlgSynthesizer(
            ILlmClient llmClient,
            INlgSynthesizer? fallbackSynthesizer = null,
            string? customSystemDirective = null)
        {
            _llmClient = llmClient ?? throw new ArgumentNullException(nameof(llmClient));
            _fallback = fallbackSynthesizer ?? new TemplateFallbackNlgSynthesizer();
            _customSystemDirective = customSystemDirective;
        }

        public async Task<string> SynthesizeAsync(NlgContext context, CancellationToken cancellationToken = default)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            try
            {
                string prompt = BuildSynthesisPrompt(context);
                string completion = await _llmClient.CompleteAsync(prompt, cancellationToken).ConfigureAwait(false);

                string cleaned = CleanGeneratedResponse(completion);
                if (!string.IsNullOrWhiteSpace(cleaned))
                {
                    return cleaned;
                }
            }
            catch
            {
                // Fall back gracefully to deterministic template when generation fails
            }

            return await _fallback.SynthesizeAsync(context, cancellationToken).ConfigureAwait(false);
        }

        private string BuildSynthesisPrompt(NlgContext context)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Hệ thống: Bạn là trợ lý AI chuyên môn cao cho giải pháp công nghiệp và ERP.");
            if (!string.IsNullOrWhiteSpace(context.Domain))
            {
                sb.AppendLine($"Phân hệ chuyên môn: {context.Domain}");
            }
            sb.AppendLine($"Phong cách phản hồi: {context.PersonaStyle}.");
            sb.AppendLine("Quy tắc bắt buộc:");
            sb.AppendLine("1. Trả lời bằng Tiếng Việt tự nhiên, đúng ngữ pháp, mạch lạc, ngắn gọn và lịch thiệp.");
            sb.AppendLine("2. Chỉ sử dụng thông tin trong mục [DỮ LIỆU SỰ THẬT TỪ HỆ THỐNG]. Tuyệt đối KHÔNG tự bịa số liệu kỹ thuật hoặc mã vật tư.");
            sb.AppendLine("3. Trực tiếp đưa ra câu trả lời, không lặp lại prompt và không dẫn giải thừa thãi.");

            if (!string.IsNullOrWhiteSpace(_customSystemDirective))
            {
                sb.AppendLine(_customSystemDirective);
            }

            sb.AppendLine();
            sb.AppendLine("[DỮ LIỆU SỰ THẬT TỪ HỆ THỐNG]:");
            sb.AppendLine($"- Tác vụ: {context.IntentName}");
            if (context.Facts != null && context.Facts.Count > 0)
            {
                foreach (var kvp in context.Facts)
                {
                    sb.AppendLine($"- {kvp.Key}: {kvp.Value}");
                }
            }
            else if (!string.IsNullOrWhiteSpace(context.FallbackDefault))
            {
                sb.AppendLine($"- Kết quả: {context.FallbackDefault}");
            }

            sb.AppendLine();
            sb.AppendLine("[CÂU HỎI CỦA NGƯỜI VẬN HÀNH]:");
            sb.AppendLine($"\"{context.UserQuery}\"");
            sb.AppendLine();
            sb.AppendLine("[CÂU TRẢ LỜI TỰ NHIÊN]:");

            return sb.ToString();
        }

        private static string CleanGeneratedResponse(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

            string text = raw.Trim();
            // Remove common wrapping markers if LLM outputs markdown quotes or markers
            if (text.StartsWith("```") && text.EndsWith("```"))
            {
                int firstNewline = text.IndexOf('\n');
                if (firstNewline > 0 && text.Length > firstNewline + 3)
                {
                    text = text.Substring(firstNewline + 1, text.Length - firstNewline - 4).Trim();
                }
            }

            if (text.StartsWith("\"") && text.EndsWith("\"") && text.Length > 2)
            {
                text = text.Substring(1, text.Length - 2).Trim();
            }

            return text;
        }
    }
}
