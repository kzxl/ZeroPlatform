using Xunit;

namespace ZeroDocuments.Tests
{
    /// <summary>
    /// Serializes test classes that read or mutate the process-wide
    /// <c>ExcelWriter.FormulaInjectionProtection</c> / <c>CsvWriter.FormulaInjectionProtection</c> flags,
    /// preventing cross-class races under xUnit's parallel class execution.
    /// </summary>
    [CollectionDefinition(Name)]
    public sealed class GlobalWriterSettingsCollection
    {
        public const string Name = "GlobalWriterSettings";
    }
}
