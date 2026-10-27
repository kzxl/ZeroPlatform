using System;
using System.Collections.Generic;
using Xunit;
using ZeroAgent.Tools.Data;
using ZeroData.Sql.Dialects;

namespace ZeroAgent.Tests
{
    public class DynamicSqlBuilderTests
    {
        [Fact]
        public void DynamicSqlBuilder_SimpleQueryWithPaging_GeneratesValidSqlServer()
        {
            var spec = new AnalyticalQuerySpec
            {
                Table = "factory_machines",
                SelectColumns = new List<string> { "machine_id", "status", "efficiency" },
                Filters = new List<WhereConditionSpec>
                {
                    new WhereConditionSpec { Column = "status", Operator = "=", Value = "RUNNING" }
                },
                OrderBy = new List<OrderBySpec> { new OrderBySpec("efficiency", descending: true) },
                Limit = 10,
                Offset = 5
            };

            var result = DynamicSqlBuilder.Build(spec, dialect: new SqlServerDialect());

            Assert.True(result.IsValid);
            Assert.Contains("SELECT [machine_id], [status], [efficiency] FROM [factory_machines]", result.Sql);
            Assert.Contains("WHERE [status] = @p0", result.Sql);
            Assert.Contains("ORDER BY [efficiency] DESC", result.Sql);
            Assert.Contains("OFFSET 5 ROWS FETCH NEXT 10 ROWS ONLY", result.Sql);
            Assert.Equal("RUNNING", result.Parameters["@p0"]);
        }

        [Fact]
        public void DynamicSqlBuilder_AggregationsAndGroupByWithHaving_GeneratesCorrectSql()
        {
            var spec = new AnalyticalQuerySpec
            {
                Table = "factory_machines",
                SelectColumns = new List<string> { "status", "COUNT(*) AS total_count", "AVG(efficiency) AS avg_eff" },
                GroupBy = new List<string> { "status" },
                Having = new List<WhereConditionSpec>
                {
                    new WhereConditionSpec { Column = "COUNT(*)", Operator = ">", Value = 1 }
                },
                OrderBy = new List<OrderBySpec> { new OrderBySpec("avg_eff", descending: true) }
            };

            var result = DynamicSqlBuilder.Build(spec, dialect: new SqliteDialect());

            Assert.True(result.IsValid);
            Assert.Contains("SELECT \"status\", COUNT(*) AS \"total_count\", AVG(\"efficiency\") AS \"avg_eff\" FROM \"factory_machines\"", result.Sql);
            Assert.Contains("GROUP BY \"status\"", result.Sql);
            Assert.Contains("HAVING COUNT(*) > @p0", result.Sql);
            Assert.Contains("ORDER BY \"avg_eff\" DESC", result.Sql);
            Assert.Equal(1, result.Parameters["@p0"]);
        }

        [Fact]
        public void DynamicSqlBuilder_MultiTableJoin_GeneratesValidJoinSyntax()
        {
            var spec = new AnalyticalQuerySpec
            {
                Table = "tb_orders",
                SelectColumns = new List<string> { "tb_orders.id", "tb_orders.order_no", "tb_customers.name" },
                Joins = new List<JoinConditionSpec>
                {
                    new JoinConditionSpec
                    {
                        Table = "tb_customers",
                        LeftColumn = "tb_orders.customer_id",
                        RightColumn = "tb_customers.id",
                        JoinType = "LEFT"
                    }
                },
                Filters = new List<WhereConditionSpec>
                {
                    new WhereConditionSpec { Column = "tb_orders.total_amount", Operator = ">=", Value = 500 }
                }
            };

            var result = DynamicSqlBuilder.Build(spec, dialect: new SqlServerDialect());

            Assert.True(result.IsValid);
            Assert.Contains("LEFT JOIN [tb_customers] ON [tb_orders].[customer_id] = [tb_customers].[id]", result.Sql);
            Assert.Contains("WHERE [tb_orders].[total_amount] >= @p0", result.Sql);
        }

        [Fact]
        public void DynamicSqlBuilder_BetweenAndNullOperators_GeneratesCorrectParameters()
        {
            var spec = new AnalyticalQuerySpec
            {
                Table = "factory_metrics",
                Filters = new List<WhereConditionSpec>
                {
                    new WhereConditionSpec { Column = "temperature", Operator = "BETWEEN", Value = 60.0, SecondValue = 90.0 },
                    new WhereConditionSpec { Column = "error_code", Operator = "IS NULL", LogicalOp = "AND" }
                }
            };

            var result = DynamicSqlBuilder.Build(spec, dialect: new SqlServerDialect());

            Assert.True(result.IsValid);
            Assert.Contains("[temperature] BETWEEN @p0 AND @p1", result.Sql);
            Assert.Contains("AND [error_code] IS NULL", result.Sql);
            Assert.Equal(60.0, result.Parameters["@p0"]);
            Assert.Equal(90.0, result.Parameters["@p1"]);
        }

        [Fact]
        public void DynamicSqlBuilder_SqlInjectionAttempt_IsRejectedBySanitizer()
        {
            var maliciousSpec = new AnalyticalQuerySpec
            {
                Table = "users; DROP TABLE users; --",
                Filters = new List<WhereConditionSpec>
                {
                    new WhereConditionSpec { Column = "id", Operator = "=", Value = 1 }
                }
            };

            var result = DynamicSqlBuilder.Build(maliciousSpec, dialect: new SqlServerDialect());

            Assert.False(result.IsValid);
            Assert.Contains("Invalid or dangerous table name", result.ErrorMessage);
        }
    }
}
