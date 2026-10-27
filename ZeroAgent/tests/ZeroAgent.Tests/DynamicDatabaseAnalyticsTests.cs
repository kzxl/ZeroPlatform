using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;
using ZeroAgent.Core.Tools;
using ZeroAgent.Tools.Data;
using ZeroData.Sql.Mapping;

namespace ZeroAgent.Tests
{
    [Table(Name = "tb_test_factory_parts")]
    public class TestFactoryPartEntity
    {
        [Column(IsPrimaryKey = true)]
        public int Id { get; set; }

        [Column]
        public string PartName { get; set; } = string.Empty;

        [Column]
        public double UnitCost { get; set; }
    }

    public class DynamicDatabaseAnalyticsTests
    {
        [Fact]
        public async Task DynamicDatabaseQueryTool_ExecuteAnalytics_DataFrameAggregationAndGroupBy()
        {
            var registry = new AgentToolRegistry();
            DynamicDatabaseQueryTool.RegisterAll(registry);

            var spec = new AnalyticalQuerySpec
            {
                Table = "factory_machines",
                SelectColumns = new List<string> { "status", "COUNT(*) AS total_count", "AVG(efficiency) AS avg_eff" },
                GroupBy = new List<string> { "status" },
                OrderBy = new List<OrderBySpec> { new OrderBySpec("avg_eff", descending: true) }
            };

            string jsonArg = JsonSerializer.Serialize(spec);
            string responseJson = await registry.ExecuteAsync("db_execute_analytics", jsonArg);

            Assert.Contains("\"success\":true", responseJson);
            Assert.Contains("DATAFRAME_IN_MEMORY", responseJson);
            Assert.Contains("RUNNING", responseJson);
            Assert.Contains("avg_eff", responseJson);
        }

        [Fact]
        public async Task DynamicDatabaseQueryTool_BuildQueryTool_GeneratesSafeSql()
        {
            var registry = new AgentToolRegistry();
            DynamicDatabaseQueryTool.RegisterAll(registry);

            var spec = new AnalyticalQuerySpec
            {
                Table = "factory_machines",
                SelectColumns = new List<string> { "machine_id", "efficiency" },
                Filters = new List<WhereConditionSpec>
                {
                    new WhereConditionSpec { Column = "efficiency", Operator = "<", Value = 80.0 }
                },
                Limit = 5
            };

            string responseJson = await registry.ExecuteAsync("db_build_query", JsonSerializer.Serialize(spec));

            Assert.Contains("\"success\":true", responseJson);
            Assert.Contains("SELECT [machine_id], [efficiency] FROM [factory_machines]", responseJson);
            Assert.Contains("WHERE [efficiency] < @p0", responseJson);
            Assert.Contains("@p0", responseJson);
        }

        [Fact]
        public void DynamicDatabaseQueryTool_RegisterEntity_IntrospectsZeroDataSqlMapping()
        {
            DynamicDatabaseQueryTool.RegisterEntity<TestFactoryPartEntity>("Test factory manufacturing parts catalog");

            string promptContext = DynamicDatabaseQueryTool.GenerateSchemaContext("tb_test_factory_parts");

            Assert.Contains("tb_test_factory_parts", promptContext);
            Assert.Contains("`Id`: Int32 [PK]", promptContext);
            Assert.Contains("`PartName`: String", promptContext);
            Assert.Contains("`UnitCost`: Double", promptContext);
        }

        [Fact]
        public async Task DynamicDatabaseQueryTool_SchemaPromptTool_ReturnsFormattedCatalog()
        {
            var registry = new AgentToolRegistry();
            DynamicDatabaseQueryTool.RegisterAll(registry);

            string schemaPrompt = await registry.ExecuteAsync("db_get_schema_prompt", "");

            Assert.Contains("### Database Schema Catalog", schemaPrompt);
            Assert.Contains("factory_machines", schemaPrompt);
            Assert.Contains("machine_id", schemaPrompt);
            Assert.Contains("defect_count", schemaPrompt);
        }
    }
}
