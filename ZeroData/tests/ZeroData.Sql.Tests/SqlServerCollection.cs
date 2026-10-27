using Xunit;

namespace ZeroData.Sql.Tests
{
    /// <summary>
    /// Groups all SQL Server-backed test classes into a single xUnit collection so they run
    /// serially. These classes share one physical database (LiteSqlTest) and create/drop
    /// overlapping tables (e.g. Orders), so parallel execution causes table-collision errors.
    /// </summary>
    [CollectionDefinition("SqlServer")]
    public class SqlServerCollection
    {
    }
}
