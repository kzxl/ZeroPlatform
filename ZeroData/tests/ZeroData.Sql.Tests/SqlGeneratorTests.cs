using ZeroData.Sql.Mapping;
using ZeroData.Sql.Sql;
using System.Collections.Generic;
using Xunit;

namespace ZeroData.Sql.Tests
{
    public class SqlGeneratorTests
    {
        [Fact]
        public void GenerateSelectAll_ProducesCorrectSql()
        {
            var mapping = MappingCache.GetMapping<Product>();
            var sql = SqlGenerator.GenerateSelectAll(mapping);
            Assert.Equal("SELECT * FROM [dbo].[Products]", sql);
        }

        [Fact]
        public void GenerateInsert_WithDbGeneratedPK_IncludesScopeIdentity()
        {
            var mapping = MappingCache.GetMapping<Product>();
            var product = new Product { Name = "Widget", Price = 9.99m, CategoryId = 1 };

            var (sql, parameters) = SqlGenerator.GenerateInsert(mapping, product);

            Assert.Contains("INSERT INTO [dbo].[Products]", sql);
            Assert.Contains("[ProductName]", sql);
            Assert.Contains("[UnitPrice]", sql);
            Assert.Contains("[CategoryId]", sql);
            Assert.DoesNotContain("[ProductId]", sql); // DbGenerated excluded
            Assert.DoesNotContain("SCOPE_IDENTITY()", sql); // Identity handled by SqlContext

            Assert.Equal("Widget", parameters["@ProductName"]);
            Assert.Equal(9.99m, parameters["@UnitPrice"]);
            Assert.Equal(1, parameters["@CategoryId"]);
        }

        [Fact]
        public void GenerateInsert_WithoutDbGeneratedPK_NoScopeIdentity()
        {
            var mapping = MappingCache.GetMapping<User>();
            var user = new User { Username = "admin", FullName = "Admin User", Email = "admin@test.com" };

            var (sql, parameters) = SqlGenerator.GenerateInsert(mapping, user);

            Assert.Contains("INSERT INTO [Users]", sql);
            Assert.Contains("[Username]", sql);
            Assert.DoesNotContain("SCOPE_IDENTITY()", sql);
        }

        [Fact]
        public void GenerateDelete_ProducesCorrectWhereClause()
        {
            var mapping = MappingCache.GetMapping<Product>();
            var product = new Product { Id = 42 };

            var (sql, parameters) = SqlGenerator.GenerateDelete(mapping, product);

            Assert.Equal("DELETE FROM [dbo].[Products] WHERE [ProductId] = @pk_ProductId", sql);
            Assert.Equal(42, parameters["@pk_ProductId"]);
        }

        [Fact]
        public void GenerateUpdate_ProducesCorrectSql()
        {
            var mapping = MappingCache.GetMapping<Product>();
            var product = new Product { Id = 1, Name = "Updated", Price = 19.99m, CategoryId = 2 };

            var (sql, parameters) = SqlGenerator.GenerateUpdate(mapping, product);

            Assert.Contains("UPDATE [dbo].[Products]", sql);
            Assert.Contains("SET", sql);
            Assert.Contains("[ProductName] = @ProductName", sql);
            Assert.Contains("[UnitPrice] = @UnitPrice", sql);
            Assert.Contains("[CategoryId] = @CategoryId", sql);
            Assert.Contains("WHERE [ProductId] = @pk_ProductId", sql);

            Assert.Equal("Updated", parameters["@ProductName"]);
            Assert.Equal(19.99m, parameters["@UnitPrice"]);
            Assert.Equal(1, parameters["@pk_ProductId"]);
        }

        [Fact]
        public void ConvertPositionalParameters_ReplacesCorrectly()
        {
            var (sql, parameters) = SqlGenerator.ConvertPositionalParameters(
                "SELECT * FROM Users WHERE Name = {0} AND Age > {1}", 
                new object[] { "John", 25 });

            Assert.Equal("SELECT * FROM Users WHERE Name = @p0 AND Age > @p1", sql);
            Assert.Equal("John", parameters["@p0"]);
            Assert.Equal(25, parameters["@p1"]);
        }

        [Fact]
        public void ConvertPositionalParameters_NoParams_ReturnsOriginal()
        {
            var (sql, parameters) = SqlGenerator.ConvertPositionalParameters(
                "SELECT * FROM Users", null);

            Assert.Equal("SELECT * FROM Users", sql);
            Assert.Empty(parameters);
        }
    }
}
