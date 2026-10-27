using ZeroDocuments.Common;

namespace ZeroDocuments.Excel.Models
{
    /// <summary>
    /// Configuration options for writing Excel (.xlsx) packages.
    /// </summary>
    public sealed class ExcelWriterOptions
    {
        /// <summary>
        /// Gets or sets the formula injection mitigation strategy.
        /// Default is <see cref="FormulaInjectionMode.PrefixQuote"/>.
        /// </summary>
        public FormulaInjectionMode FormulaInjection { get; set; } = FormulaInjectionMode.PrefixQuote;

        /// <summary>
        /// Convenience property: when true, uses <see cref="FormulaInjectionMode.PrefixQuote"/>;
        /// when false, uses <see cref="FormulaInjectionMode.Disabled"/>.
        /// </summary>
        public bool FormulaInjectionProtection
        {
            get => FormulaInjection != FormulaInjectionMode.Disabled;
            set => FormulaInjection = value ? FormulaInjectionMode.PrefixQuote : FormulaInjectionMode.Disabled;
        }
    }
}
