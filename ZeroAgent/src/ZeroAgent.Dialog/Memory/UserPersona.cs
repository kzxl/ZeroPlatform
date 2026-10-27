using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ZeroAgent.Dialog.Memory
{
    public enum CommunicationTone
    {
        Formal,
        Respectful,
        Friendly,
        Casual,
        Direct
    }

    /// <summary>
    /// Long-term User Persona & Behavioral Personalization Memory.
    /// Tracks communication pronouns, formality, and frequent business domains
    /// across conversations (similar to ChatGPT Custom Instructions & Context History)
    /// without making arbitrary entity assumptions.
    /// </summary>
    public sealed class UserPersona
    {
        private static readonly Regex TaoMayRegex = new Regex(@"\b(tao|mày|may|thằng này|con bot này)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex AnhEmRegex = new Regex(@"\b(anh|anh ơi|em ơi|chú|bác|chị|chị ơi)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex ToiBanRegex = new Regex(@"\b(tôi|bạn|toi|ban|mình)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public string UserPronoun { get; set; } = "bạn";
        public string BotPronoun { get; set; } = "tôi";
        public CommunicationTone Tone { get; set; } = CommunicationTone.Formal;

        public ConcurrentDictionary<string, int> TopicFrequencies { get; } = new ConcurrentDictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        public DateTime LastUpdatedUtc { get; private set; } = DateTime.UtcNow;

        public string? DominantDomain
        {
            get
            {
                if (TopicFrequencies.IsEmpty) return null;
                return TopicFrequencies.OrderByDescending(kvp => kvp.Value).FirstOrDefault().Key;
            }
        }

        public void RecordUtterance(string userMessage)
        {
            if (string.IsNullOrWhiteSpace(userMessage)) return;

            LastUpdatedUtc = DateTime.UtcNow;

            // 1. Detect casual/brusque: tao - mày
            if (TaoMayRegex.IsMatch(userMessage))
            {
                UserPronoun = "tao";
                BotPronoun = "mày";
                Tone = CommunicationTone.Casual;
                return;
            }

            // 2. Detect respectful/elder: anh/chị - em
            var anhEmMatch = AnhEmRegex.Match(userMessage);
            if (anhEmMatch.Success)
            {
                string matchVal = anhEmMatch.Value.ToLowerInvariant();
                if (matchVal.Contains("chị"))
                {
                    UserPronoun = "chị";
                    BotPronoun = "em";
                    Tone = CommunicationTone.Respectful;
                }
                else
                {
                    UserPronoun = "anh";
                    BotPronoun = "em";
                    Tone = CommunicationTone.Respectful;
                }
                return;
            }

            // 3. Detect standard formal: tôi - bạn / mình
            if (ToiBanRegex.IsMatch(userMessage) && Tone == CommunicationTone.Formal)
            {
                UserPronoun = "tôi";
                BotPronoun = "bạn";
                Tone = CommunicationTone.Formal;
            }
        }

        public void RecordInteraction(string? intentName, IReadOnlyDictionary<string, string>? slots = null)
        {
            if (!string.IsNullOrEmpty(intentName))
            {
                TopicFrequencies.AddOrUpdate(intentName!, 1, (_, count) => count + 1);
            }
        }

        public string FormatPersonalizedResponse(string rawResponse, string? userName = null)
        {
            if (string.IsNullOrWhiteSpace(rawResponse)) return string.Empty;

            string displayName = !string.IsNullOrWhiteSpace(userName)
                && userName != "DefaultOperator"
                && userName != "Operator"
                && userName != "Khách"
                && userName != "Guest"
                && userName != "GuestUser"
                ? " " + userName
                : string.Empty;

            switch (Tone)
            {
                case CommunicationTone.Respectful:
                    if (rawResponse.StartsWith("Chào bạn", StringComparison.OrdinalIgnoreCase))
                    {
                        return $"Dạ chào {UserPronoun}{displayName}, {BotPronoun} " + rawResponse.Substring(8).TrimStart(',', ' ');
                    }
                    if (!rawResponse.StartsWith("Dạ", StringComparison.OrdinalIgnoreCase))
                    {
                        return $"Dạ {UserPronoun}{displayName}, " + rawResponse;
                    }
                    break;

                case CommunicationTone.Casual:
                    string casual = rawResponse.Replace("Chào bạn, ", string.Empty)
                                               .Replace("Chào bạn", string.Empty)
                                               .Replace("của bạn", "của mày");
                    return casual.TrimEnd('.', '!') + " nhé!";

                case CommunicationTone.Formal:
                default:
                    break;
            }

            return rawResponse;
        }

        public string GetPersonaPromptSummary(string? userName = null)
        {
            var sb = new StringBuilder();
            sb.AppendLine("[USER PERSONA & HISTORICAL PREFERENCES]");
            if (!string.IsNullOrWhiteSpace(userName))
            {
                sb.AppendLine($"- User Name: {userName}");
            }
            sb.AppendLine($"- Communication Style: User addresses self as '{UserPronoun}', refers to agent as '{BotPronoun}' (Tone: {Tone})");
            if (!string.IsNullOrEmpty(DominantDomain))
            {
                sb.AppendLine($"- Dominant Inquired Topic: {DominantDomain} (Queried {TopicFrequencies[DominantDomain!]} times)");
            }

            return sb.ToString().TrimEnd();
        }
    }
}
