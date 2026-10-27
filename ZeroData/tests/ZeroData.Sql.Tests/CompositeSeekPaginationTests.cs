using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Xunit;
using ZeroData.Sql.Mapping;

namespace ZeroData.Sql.Tests
{
    [Table(Name = "SeekArticles")]
    public class SeekArticle
    {
        [Column(Name = "Id", IsPrimaryKey = true)]
        public int Id { get; set; }

        [Column(Name = "Category")]
        public string Category { get; set; }

        [Column(Name = "Views")]
        public int Views { get; set; }

        [Column(Name = "Rating")]
        public double Rating { get; set; }

        [Column(Name = "IsPublished")]
        public bool IsPublished { get; set; }
    }

    public class CompositeSeekPaginationTests
    {
        private SqliteConnection CreateDatabase()
        {
            var conn = new SqliteConnection("Data Source=:memory:");
            conn.Open();

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    CREATE TABLE SeekArticles (
                        Id INTEGER PRIMARY KEY,
                        Category TEXT NOT NULL,
                        Views INTEGER NOT NULL,
                        Rating REAL NOT NULL,
                        IsPublished INTEGER NOT NULL
                    );
                ";
                cmd.ExecuteNonQuery();
            }

            return conn;
        }

        private void SeedArticles(SqlContext db)
        {
            // Seed 10 articles with duplicate views and ratings to test composite tie-breaking
            var items = new List<SeekArticle>
            {
                new SeekArticle { Id = 1, Category = "Tech", Views = 100, Rating = 4.5, IsPublished = true },
                new SeekArticle { Id = 2, Category = "Tech", Views = 100, Rating = 4.8, IsPublished = true },
                new SeekArticle { Id = 3, Category = "Tech", Views = 200, Rating = 3.9, IsPublished = true },
                new SeekArticle { Id = 4, Category = "Life", Views = 50,  Rating = 4.0, IsPublished = true },
                new SeekArticle { Id = 5, Category = "Life", Views = 100, Rating = 4.2, IsPublished = false },
                new SeekArticle { Id = 6, Category = "Tech", Views = 300, Rating = 4.9, IsPublished = true },
                new SeekArticle { Id = 7, Category = "Life", Views = 50,  Rating = 4.5, IsPublished = true },
                new SeekArticle { Id = 8, Category = "Tech", Views = 200, Rating = 4.1, IsPublished = true },
                new SeekArticle { Id = 9, Category = "Life", Views = 150, Rating = 4.7, IsPublished = true },
                new SeekArticle { Id = 10, Category = "Tech", Views = 100, Rating = 4.5, IsPublished = true }
            };

            db.BulkInsert(items);
        }

        [Fact]
        public void Seek_TwoColumns_SortsAndPaginatesCorrectly()
        {
            using var conn = CreateDatabase();
            using var db = new SqlContext(conn);
            SeedArticles(db);

            var table = db.GetTable<SeekArticle>();

            // Page 1: Top 3 by Views DESC, Id ASC
            // Full order by Views DESC, Id ASC:
            // Id 6 (Views 300)
            // Id 3 (Views 200)
            // Id 8 (Views 200)
            // Id 1 (Views 100)
            // Id 2 (Views 100)
            // Id 5 (Views 100)
            // Id 10 (Views 100)
            // Id 9 (Views 150) -> wait: 300, 200, 200, 150, 100, 100, 100, 100, 50, 50

            // Query first page manually or with first record
            // Top record: Id 6 (Views: 300)
            var page2 = table.Seek(
                x => x.Views, 300, asc1: false,
                x => x.Id, 6, asc2: true,
                pageSize: 2);

            Assert.Equal(2, page2.Count);
            Assert.Equal(3, page2[0].Id); // Views 200, Id 3
            Assert.Equal(8, page2[1].Id); // Views 200, Id 8

            // Page 3 after (Views: 200, Id: 8)
            var page3 = table.Seek(
                x => x.Views, 200, asc1: false,
                x => x.Id, 8, asc2: true,
                pageSize: 2);

            Assert.Equal(2, page3.Count);
            Assert.Equal(9, page3[0].Id); // Views 150, Id 9
            Assert.Equal(1, page3[1].Id); // Views 100, Id 1
        }

        [Fact]
        public async Task SeekAsync_TwoColumns_WithWhereFilter()
        {
            using var conn = CreateDatabase();
            using var db = new SqlContext(conn);
            SeedArticles(db);

            var table = db.GetTable<SeekArticle>();

            // Filter by IsPublished == true
            // Views 100 published: Id 1, Id 2, Id 10
            var page = await table.AndWhere(x => x.IsPublished)
                                  .SeekAsync(
                                      x => x.Views, 100, asc1: false,
                                      x => x.Id, 1, asc2: true,
                                      pageSize: 2);

            Assert.Equal(2, page.Count);
            Assert.Equal(2, page[0].Id);  // Id 2 (Views 100, published)
            Assert.Equal(10, page[1].Id); // Id 10 (Views 100, published)
        }

        [Fact]
        public async Task SeekAsync_ThreeColumns_CorrectTieBreaking()
        {
            using var conn = CreateDatabase();
            using var db = new SqlContext(conn);
            SeedArticles(db);

            // 3 columns: Category ASC, Views DESC, Id ASC
            // In "Tech":
            // Id 6 (Views 300)
            // Id 3 (Views 200)
            // Id 8 (Views 200)
            // Id 1 (Views 100)
            // Id 2 (Views 100)
            // Id 10 (Views 100)

            var results = await db.SeekAsync<SeekArticle, string, int, int>(
                x => x.Category, "Tech", asc1: true,
                x => x.Views, 200, asc2: false,
                x => x.Id, 3, asc3: true,
                pageSize: 3);

            Assert.Equal(3, results.Count);
            Assert.Equal(8, results[0].Id);  // Views 200, Id 8
            Assert.Equal(1, results[1].Id);  // Views 100, Id 1
            Assert.Equal(2, results[2].Id);  // Views 100, Id 2
        }

        [Fact]
        public void Seek_SeekColumnList_ExecutesSuccessfully()
        {
            using var conn = CreateDatabase();
            using var db = new SqlContext(conn);
            SeedArticles(db);

            var columns = new[]
            {
                SeekColumn.Create<SeekArticle, string>(x => x.Category, "Life", ascending: true),
                SeekColumn.Create<SeekArticle, int>(x => x.Views, 50, ascending: true),
                SeekColumn.Create<SeekArticle, int>(x => x.Id, 4, ascending: true)
            };

            var results = db.Seek<SeekArticle>(columns, pageSize: 2);

            Assert.Equal(2, results.Count);
            Assert.Equal(7, results[0].Id); // Life, Views 50, Id 7
            Assert.Equal(5, results[1].Id); // Life, Views 100, Id 5
        }
    }
}
