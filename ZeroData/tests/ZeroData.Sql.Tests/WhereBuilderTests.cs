using ZeroData.Sql.Mapping;
using ZeroData.Sql.Sql;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace ZeroData.Sql.Tests
{
    [Table(Name = "Products")]
    public class WbProduct
    {
        [Column(Name = "Id", IsPrimaryKey = true, IsDbGenerated = true)]
        public int Id { get; set; }

        [Column(Name = "ProductName")]
        public string Name { get; set; }

        [Column(Name = "Price")]
        public decimal Price { get; set; }

        [Column(Name = "CategoryId")]
        public int CategoryId { get; set; }

        [Column(Name = "IsActive")]
        public bool IsActive { get; set; }

        [Column(Name = "Description")]
        public string Description { get; set; }
    }

    public class WhereBuilderTests
    {
        private readonly EntityMapping _mapping;
        private readonly WhereBuilder _builder;

        public WhereBuilderTests()
        {
            _mapping = MappingCache.GetMapping<WbProduct>();
            _builder = new WhereBuilder(_mapping);
        }

        [Fact]
        public void SimpleEquals_GeneratesCorrectSql()
        {
            var (sql, parameters) = _builder.Build<WbProduct>(p => p.Id == 5);
            Assert.Equal("[Id] = @w0", sql);
            Assert.Equal(5, parameters["@w0"]);
        }

        [Fact]
        public void NotEquals_GeneratesCorrectSql()
        {
            var (sql, parameters) = _builder.Build<WbProduct>(p => p.CategoryId != 3);
            Assert.Equal("[CategoryId] <> @w0", sql);
            Assert.Equal(3, parameters["@w0"]);
        }

        [Fact]
        public void GreaterThan_GeneratesCorrectSql()
        {
            var (sql, parameters) = _builder.Build<WbProduct>(p => p.Price > 100m);
            Assert.Equal("[Price] > @w0", sql);
            Assert.Equal(100m, parameters["@w0"]);
        }

        [Fact]
        public void AndAlso_GeneratesCorrectSql()
        {
            var (sql, parameters) = _builder.Build<WbProduct>(
                p => p.IsActive && p.Price >= 50m);
            Assert.Contains("[IsActive]", sql);
            Assert.Contains("AND", sql);
            Assert.Contains("[Price] >= @w", sql);
        }

        [Fact]
        public void OrElse_WrapsInParentheses()
        {
            var (sql, _) = _builder.Build<WbProduct>(
                p => p.CategoryId == 1 || p.CategoryId == 2);
            Assert.StartsWith("(", sql);
            Assert.Contains("OR", sql);
        }

        [Fact]
        public void NullComparison_GeneratesIsNull()
        {
            var (sql, _) = _builder.Build<WbProduct>(p => p.Description == null);
            Assert.Contains("IS NULL", sql);
        }

        [Fact]
        public void NotNull_GeneratesIsNotNull()
        {
            var (sql, _) = _builder.Build<WbProduct>(p => p.Description != null);
            Assert.Contains("IS NOT NULL", sql);
        }

        [Fact]
        public void StringContains_GeneratesLike()
        {
            var (sql, parameters) = _builder.Build<WbProduct>(
                p => p.Name.Contains("Widget"));
            Assert.Contains("LIKE", sql);
            Assert.Contains("%Widget%", parameters.Values.Cast<object>().First().ToString());
        }

        [Fact]
        public void StringStartsWith_GeneratesLike()
        {
            var (sql, parameters) = _builder.Build<WbProduct>(
                p => p.Name.StartsWith("Pre"));
            Assert.Contains("LIKE", sql);
            Assert.Contains("Pre%", parameters.Values.Cast<object>().First().ToString());
        }

        [Fact]
        public void CollectionContains_GeneratesIn()
        {
            var ids = new List<int> { 1, 2, 3 };
            var (sql, parameters) = _builder.Build<WbProduct>(
                p => ids.Contains(p.CategoryId));
            Assert.Contains("IN", sql);
            Assert.Equal(3, parameters.Count);
        }

        [Fact]
        public void ClosureVariable_EvaluatedCorrectly()
        {
            var minPrice = 99.99m;
            var (sql, parameters) = _builder.Build<WbProduct>(p => p.Price > minPrice);
            Assert.Equal("[Price] > @w0", sql);
            Assert.Equal(99.99m, parameters["@w0"]);
        }

        [Fact]
        public void NotOperator_GeneratesNot()
        {
            var (sql, _) = _builder.Build<WbProduct>(p => !p.IsActive);
            Assert.Contains("NOT", sql);
        }
    }
}
