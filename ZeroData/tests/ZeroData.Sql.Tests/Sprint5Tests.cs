using ZeroData.Sql.Mapping;
using ZeroData.Sql.Sql;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Xunit;

namespace ZeroData.Sql.Tests
{
    #region Test Entities for One-to-Many

    [Table(Name = "Authors")]
    public class Author
    {
        [Column(Name = "Id", IsPrimaryKey = true, IsDbGenerated = true)]
        public long Id { get; set; }

        [Column(Name = "Name")]
        public string Name { get; set; }

        /// <summary>
        /// One-to-many: Author has many Books.
        /// IsForeignKey=false means THIS side is the parent (holds PK).
        /// ThisKey="Id" = Author.Id (PK on parent)
        /// OtherKey="AuthorId" = Book.AuthorId (FK on child)
        /// </summary>
        [Association(ThisKey = "Id", OtherKey = "AuthorId", IsForeignKey = false)]
        public List<Book> Books { get; set; }
    }

    [Table(Name = "Books")]
    public class Book
    {
        [Column(Name = "Id", IsPrimaryKey = true, IsDbGenerated = true)]
        public long Id { get; set; }

        [Column(Name = "Title")]
        public string Title { get; set; }

        [Column(Name = "AuthorId")]
        public long AuthorId { get; set; }

        [Column(Name = "Year")]
        public int Year { get; set; }

        /// <summary>
        /// Many-to-one: Book belongs to Author.
        /// IsForeignKey=true means THIS side holds the FK.
        /// </summary>
        [Association(ThisKey = "AuthorId", OtherKey = "Id", IsForeignKey = true)]
        public Author Author { get; set; }
    }

    #endregion

    public class Sprint5Tests : IDisposable
    {
        private readonly SqliteConnection _connection;

        public Sprint5Tests()
        {
            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();

            using var cmd = _connection.CreateCommand();
            cmd.CommandText = @"
                CREATE TABLE Authors (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL
                );
                CREATE TABLE Books (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Title TEXT NOT NULL,
                    AuthorId INTEGER NOT NULL,
                    Year INTEGER NOT NULL,
                    FOREIGN KEY (AuthorId) REFERENCES Authors(Id)
                );
                CREATE TABLE Items (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    Quantity INTEGER NOT NULL DEFAULT 0,
                    Price REAL
                );
            ";
            cmd.ExecuteNonQuery();

            // Clear compiled query cache between tests
            CompiledQueryCache.Clear();
        }

        #region Phase 11 — One-to-Many Navigation

        private void SeedAuthorsAndBooks()
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO Authors (Name) VALUES ('Author A');
                INSERT INTO Authors (Name) VALUES ('Author B');
                INSERT INTO Authors (Name) VALUES ('Author C');
                INSERT INTO Books (Title, AuthorId, Year) VALUES ('Book 1', 1, 2020);
                INSERT INTO Books (Title, AuthorId, Year) VALUES ('Book 2', 1, 2021);
                INSERT INTO Books (Title, AuthorId, Year) VALUES ('Book 3', 1, 2022);
                INSERT INTO Books (Title, AuthorId, Year) VALUES ('Book 4', 2, 2023);
                INSERT INTO Books (Title, AuthorId, Year) VALUES ('Book 5', 2, 2024);
            ";
            cmd.ExecuteNonQuery();
        }

        [Fact]
        public void OneToMany_AutoLoad_PopulatesChildCollection()
        {
            // Arrange: seed 3 authors, 5 books (3 for A, 2 for B, 0 for C)
            SeedAuthorsAndBooks();

            // Act: load all authors — should auto-load Books collection
            using var ctx = new SqlContext(_connection);
            var authors = ctx.GetTable<Author>().ToList();

            // Assert
            Assert.Equal(3, authors.Count);

            var authorA = authors.First(a => a.Name == "Author A");
            Assert.NotNull(authorA.Books);
            Assert.Equal(3, authorA.Books.Count);
            Assert.Contains(authorA.Books, b => b.Title == "Book 1");
            Assert.Contains(authorA.Books, b => b.Title == "Book 2");
            Assert.Contains(authorA.Books, b => b.Title == "Book 3");

            var authorB = authors.First(a => a.Name == "Author B");
            Assert.NotNull(authorB.Books);
            Assert.Equal(2, authorB.Books.Count);
            Assert.Contains(authorB.Books, b => b.Title == "Book 4");

            // Author C has no books — should be empty list, not null
            var authorC = authors.First(a => a.Name == "Author C");
            Assert.NotNull(authorC.Books);
            Assert.Empty(authorC.Books);
        }

        [Fact]
        public void OneToMany_ChildrenHaveCorrectFKValues()
        {
            SeedAuthorsAndBooks();

            using var ctx = new SqlContext(_connection);
            var authors = ctx.GetTable<Author>().ToList();

            var authorA = authors.First(a => a.Name == "Author A");
            // All books should have AuthorId matching the parent
            Assert.All(authorA.Books, b => Assert.Equal(authorA.Id, b.AuthorId));
        }

        [Fact]
        public void OneToMany_WithWhere_LoadsChildrenForFilteredParents()
        {
            SeedAuthorsAndBooks();

            using var ctx = new SqlContext(_connection);
            var filtered = ctx.GetTable<Author>().Where(a => a.Name == "Author B");

            Assert.Single(filtered);
            var authorB = filtered[0];
            Assert.NotNull(authorB.Books);
            Assert.Equal(2, authorB.Books.Count);
            Assert.All(authorB.Books, b => Assert.Equal(authorB.Id, b.AuthorId));
        }

        [Fact]
        public void ManyToOne_BookLoadsAuthor()
        {
            SeedAuthorsAndBooks();

            using var ctx = new SqlContext(_connection);
            var books = ctx.GetTable<Book>().Where(b => b.Title == "Book 1");

            Assert.Single(books);
            Assert.NotNull(books[0].Author);
            Assert.Equal("Author A", books[0].Author.Name);
        }

        [Fact]
        public void OneToMany_Include_SelectiveLoading()
        {
            SeedAuthorsAndBooks();

            using var ctx = new SqlContext(_connection);
            // Use Include to selectively load Books
            var authors = ctx.GetTable<Author>()
                .Include(a => a.Books)
                .ToList();

            Assert.Equal(3, authors.Count);
            var authorA = authors.First(a => a.Name == "Author A");
            Assert.NotNull(authorA.Books);
            Assert.Equal(3, authorA.Books.Count);
        }

        [Fact]
        public async Task OneToMany_Async_PopulatesChildCollection()
        {
            SeedAuthorsAndBooks();

            using var ctx = new SqlContext(_connection);
            var authors = await ctx.GetTable<Author>().ToListAsync();

            Assert.Equal(3, authors.Count);
            var authorA = authors.First(a => a.Name == "Author A");
            Assert.NotNull(authorA.Books);
            Assert.Equal(3, authorA.Books.Count);

            var authorC = authors.First(a => a.Name == "Author C");
            Assert.NotNull(authorC.Books);
            Assert.Empty(authorC.Books);
        }

        [Fact]
        public void OneToMany_BatchIN_AvoidNPlus1()
        {
            // Insert 10 authors with 5 books each = 50 books
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "";
            for (int i = 1; i <= 10; i++)
            {
                cmd.CommandText += $"INSERT INTO Authors (Name) VALUES ('A{i}');\n";
            }
            for (int a = 1; a <= 10; a++)
            {
                for (int b = 1; b <= 5; b++)
                {
                    cmd.CommandText += $"INSERT INTO Books (Title, AuthorId, Year) VALUES ('B{a}_{b}', {a}, 2020);\n";
                }
            }
            cmd.ExecuteNonQuery();

            // Act: load all authors once — should batch-load all 50 books in 1 query
            using var ctx = new SqlContext(_connection);
            var authors = ctx.GetTable<Author>().ToList();

            Assert.Equal(10, authors.Count);
            Assert.All(authors, a =>
            {
                Assert.NotNull(a.Books);
                Assert.Equal(5, a.Books.Count);
            });
        }

        #endregion

        #region Phase 7.5 — Compiled Query Cache

        [Fact]
        public void CompiledQueryCache_SameStructure_SameKey()
        {
            // Two expressions with same structure but different constant values
            Expression<Func<TestItem, bool>> expr1 = x => x.Name == "Alice";
            Expression<Func<TestItem, bool>> expr2 = x => x.Name == "Bob";

            var key1 = CompiledQueryCache.GetStructureKey(expr1);
            var key2 = CompiledQueryCache.GetStructureKey(expr2);

            Assert.Equal(key1, key2); // Same structure, different constants → same key
        }

        [Fact]
        public void CompiledQueryCache_DifferentStructure_DifferentKey()
        {
            Expression<Func<TestItem, bool>> expr1 = x => x.Name == "Alice";
            Expression<Func<TestItem, bool>> expr2 = x => x.Quantity > 10;

            var key1 = CompiledQueryCache.GetStructureKey(expr1);
            var key2 = CompiledQueryCache.GetStructureKey(expr2);

            Assert.NotEqual(key1, key2); // Different members/operators → different keys
        }

        [Fact]
        public void CompiledQueryCache_ComplexExpression_ConsistentKey()
        {
            // Complex AND expression
            Expression<Func<TestItem, bool>> expr1 = x => x.Name == "A" && x.Quantity > 5;
            Expression<Func<TestItem, bool>> expr2 = x => x.Name == "B" && x.Quantity > 100;

            var key1 = CompiledQueryCache.GetStructureKey(expr1);
            var key2 = CompiledQueryCache.GetStructureKey(expr2);

            Assert.Equal(key1, key2); // Same structure despite different values
        }

        [Fact]
        public void CompiledQueryCache_CacheAndRetrieve()
        {
            var key = "test_key_123";
            var sql = "SELECT * FROM Items WHERE [Name] = @w0";

            CompiledQueryCache.CacheSql(key, sql);
            var cached = CompiledQueryCache.GetCachedSql(key);

            Assert.Equal(sql, cached);
        }

        [Fact]
        public void CompiledQueryCache_MissReturnsNull()
        {
            var cached = CompiledQueryCache.GetCachedSql("nonexistent_key");
            Assert.Null(cached);
        }

        [Fact]
        public void CompiledQueryCache_CacheSizeTracking()
        {
            CompiledQueryCache.Clear();
            Assert.Equal(0, CompiledQueryCache.CacheSize);

            CompiledQueryCache.CacheSql("k1", "sql1");
            CompiledQueryCache.CacheSql("k2", "sql2");
            Assert.Equal(2, CompiledQueryCache.CacheSize);

            CompiledQueryCache.Clear();
            Assert.Equal(0, CompiledQueryCache.CacheSize);
        }

        [Fact]
        public void CompiledQueryCache_ClosureVariable_SameStructure()
        {
            // Closure variables should produce same structural key
            var name1 = "Alice";
            var name2 = "Bob";

            Expression<Func<TestItem, bool>> expr1 = x => x.Name == name1;
            Expression<Func<TestItem, bool>> expr2 = x => x.Name == name2;

            var key1 = CompiledQueryCache.GetStructureKey(expr1);
            var key2 = CompiledQueryCache.GetStructureKey(expr2);

            Assert.Equal(key1, key2); // Closures are normalized
        }

        [Fact]
        public void CompiledQueryCache_MethodCall_DifferentMethods_DifferentKey()
        {
            Expression<Func<TestItem, bool>> expr1 = x => x.Name.Contains("test");
            Expression<Func<TestItem, bool>> expr2 = x => x.Name.StartsWith("test");

            var key1 = CompiledQueryCache.GetStructureKey(expr1);
            var key2 = CompiledQueryCache.GetStructureKey(expr2);

            Assert.NotEqual(key1, key2); // Different methods → different keys
        }

        [Fact]
        public void CompiledQueryCache_OrExpression_CorrectKey()
        {
            Expression<Func<TestItem, bool>> expr1 = x => x.Name == "A" || x.Quantity == 5;
            Expression<Func<TestItem, bool>> expr2 = x => x.Name == "B" || x.Quantity == 10;

            var key1 = CompiledQueryCache.GetStructureKey(expr1);
            var key2 = CompiledQueryCache.GetStructureKey(expr2);

            Assert.Equal(key1, key2);

            // vs different structure
            Expression<Func<TestItem, bool>> expr3 = x => x.Name == "A" && x.Quantity == 5;
            var key3 = CompiledQueryCache.GetStructureKey(expr3);

            Assert.NotEqual(key1, key3); // OR vs AND → different keys
        }

        #endregion

        public void Dispose()
        {
            CompiledQueryCache.Clear();
            _connection?.Dispose();
        }
    }
}
