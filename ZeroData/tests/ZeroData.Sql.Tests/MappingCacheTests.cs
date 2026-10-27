using ZeroData.Sql.Mapping;
using Xunit;

namespace ZeroData.Sql.Tests
{
    #region Test Models

    [Table(Name = "dbo.Products")]
    public class Product
    {
        [Column(Name = "ProductId", IsPrimaryKey = true, IsDbGenerated = true)]
        public int Id { get; set; }

        [Column(Name = "ProductName")]
        public string Name { get; set; }

        [Column(Name = "UnitPrice", CanBeNull = true)]
        public decimal? Price { get; set; }

        [Column(Name = "CategoryId")]
        public int CategoryId { get; set; }
    }

    [Table(Name = "Users")]
    public class User
    {
        [Column(IsPrimaryKey = true)]
        public string Username { get; set; }

        [Column]
        public string FullName { get; set; }

        [Column]
        public string Email { get; set; }
    }

    // Convention-based model (no [Column] attributes)
    [Table(Name = "Logs")]
    public class LogEntry
    {
        public int Id { get; set; }
        public string Message { get; set; }
        public string Level { get; set; }
    }

    // No attributes at all (full convention)
    public class SimpleEntity
    {
        public int Id { get; set; }
        public string Value { get; set; }
    }

    #endregion

    public class MappingCacheTests
    {
        public MappingCacheTests()
        {
            MappingCache.Clear();
        }

        [Fact]
        public void GetMapping_WithTableAttribute_ReturnsCorrectTableName()
        {
            var mapping = MappingCache.GetMapping<Product>();
            Assert.Equal("dbo.Products", mapping.TableName);
        }

        [Fact]
        public void GetMapping_WithoutTableAttribute_UsesClassName()
        {
            var mapping = MappingCache.GetMapping<SimpleEntity>();
            Assert.Equal("SimpleEntity", mapping.TableName);
        }

        [Fact]
        public void GetMapping_WithColumnAttributes_MapsCorrectly()
        {
            var mapping = MappingCache.GetMapping<Product>();

            Assert.Equal(4, mapping.Columns.Count);

            var pkColumn = mapping.Columns[0];
            Assert.Equal("ProductId", pkColumn.ColumnName);
            Assert.True(pkColumn.IsPrimaryKey);
            Assert.True(pkColumn.IsDbGenerated);

            var nameColumn = mapping.Columns[1];
            Assert.Equal("ProductName", nameColumn.ColumnName);
            Assert.False(nameColumn.IsPrimaryKey);
        }

        [Fact]
        public void GetMapping_PrimaryKeys_IdentifiedCorrectly()
        {
            var mapping = MappingCache.GetMapping<Product>();
            Assert.Single(mapping.PrimaryKeys);
            Assert.Equal("ProductId", mapping.PrimaryKeys[0].ColumnName);
        }

        [Fact]
        public void GetMapping_InsertableColumns_ExcludesDbGenerated()
        {
            var mapping = MappingCache.GetMapping<Product>();

            // ProductId is IsDbGenerated, so should NOT be in InsertableColumns
            Assert.Equal(3, mapping.InsertableColumns.Count);
            Assert.DoesNotContain(mapping.InsertableColumns, c => c.ColumnName == "ProductId");
        }

        [Fact]
        public void GetMapping_UpdatableColumns_ExcludesPKAndDbGenerated()
        {
            var mapping = MappingCache.GetMapping<Product>();

            // Exclude ProductId (PK + DbGenerated)
            Assert.Equal(3, mapping.UpdatableColumns.Count);
            Assert.DoesNotContain(mapping.UpdatableColumns, c => c.ColumnName == "ProductId");
        }

        [Fact]
        public void GetMapping_ConventionBased_MapsAllPublicProperties()
        {
            var mapping = MappingCache.GetMapping<LogEntry>();

            // Table name from [Table] attribute
            Assert.Equal("Logs", mapping.TableName);

            // All 3 properties mapped (convention-based, no [Column])
            Assert.Equal(3, mapping.Columns.Count);
            Assert.Contains(mapping.Columns, c => c.ColumnName == "Id");
            Assert.Contains(mapping.Columns, c => c.ColumnName == "Message");
            Assert.Contains(mapping.Columns, c => c.ColumnName == "Level");
        }

        [Fact]
        public void GetMapping_NonGenericPrimaryKey_StringKey()
        {
            var mapping = MappingCache.GetMapping<User>();

            Assert.Single(mapping.PrimaryKeys);
            Assert.Equal("Username", mapping.PrimaryKeys[0].ColumnName);
        }

        [Fact]
        public void GetMapping_IsCached()
        {
            var mapping1 = MappingCache.GetMapping<Product>();
            var mapping2 = MappingCache.GetMapping<Product>();
            Assert.Same(mapping1, mapping2);
        }
    }
}
