namespace ZeroDocuments.Common
{
    /// <summary>
    /// Single source of truth for CWE-1236 (Formula / CSV Injection) mitigation.
    /// A value is considered dangerous when its first character is one of: = + - @ \t \r.
    /// Dangerous values are neutralized by prefixing a single quote.
    /// </summary>
    internal static class FormulaInjectionGuard
    {
        /// <summary>
        /// Returns true when the character is a spreadsheet formula trigger.
        /// </summary>
        public static bool IsTrigger(char ch) =>
            ch == '=' || ch == '+' || ch == '-' || ch == '@' || ch == '\t' || ch == '\r';

        /// <summary>
        /// Prefixes a single quote when the text starts with a formula trigger character.
        /// Returns <see cref="string.Empty"/> for null or empty input.
        /// </summary>
        public static string Sanitize(string? text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return IsTrigger(text![0]) ? "'" + text : text;
        }
    }
}
