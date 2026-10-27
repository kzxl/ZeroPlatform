namespace ZeroDocuments.Common
{
    /// <summary>
    /// Specifies the formula injection (CWE-1236) mitigation strategy.
    /// </summary>
    public enum FormulaInjectionMode
    {
        /// <summary>
        /// Prefixes a single quote (') to trigger strings (=, +, -, @, \t, \r).
        /// Default behavior compliant with CWE-1236 standard text-level sanitization.
        /// </summary>
        PrefixQuote,

        /// <summary>
        /// Applies OpenXML style attribute <c>quotePrefix="1"</c> without mutating the cell text payload.
        /// Excel treats the cell as text in the formula bar while keeping the original string intact.
        /// (In CSV, falls back to quote prefixing as CSV lacks style metadata).
        /// </summary>
        QuotePrefixStyle,

        /// <summary>
        /// Mitigation is disabled; cell contents are written verbatim.
        /// </summary>
        Disabled
    }
}
