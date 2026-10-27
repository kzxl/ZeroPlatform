using System.Text;
using ZeroDocuments.Common;

namespace ZeroDocuments.Csv
{
    /// <summary>
    /// Configuration options for writing CSV files.
    /// </summary>
    public sealed class CsvWriterOptions
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

        /// <summary>
        /// Delimiter character. Default is comma (',').
        /// </summary>
        public char Delimiter { get; set; } = ',';

        /// <summary>
        /// Whether to output a header row. Default is true.
        /// </summary>
        public bool IncludeHeaders { get; set; } = true;

        /// <summary>
        /// Encoding to use. Default is UTF-8 with BOM.
        /// </summary>
        public Encoding? Encoding { get; set; }
    }
}
