using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.Sqlite;
using Xunit;
using ZeroData.Sql.ChangeTracking;
using ZeroData.Sql.Execution;
using ZeroData.Sql.Mapping;

namespace ZeroData.Sql.Tests
{
    [Table(Name = "HotPathEntities")]
    public class HotPathEntity
    {
        [Column(Name = "Id", IsPrimaryKey = true, IsDbGenerated = true)]
        public int Id { get; set; }

        [Column(Name = "Title")]
        public string Title { get; set; }

        [Column(Name = "Value")]
        public decimal Value { get; set; }
    }

    [Table(Name = "HotPathOtherEntities")]
    public class HotPathOtherEntity
    {
        [Column(Name = "Id", IsPrimaryKey = true, IsDbGenerated = true)]
        public int Id { get; set; }

        [Column(Name = "Description")]
        public string Description { get; set; }
    }

    public class HotPathOptimizationTests
    {
        private SqliteConnection CreateDatabase()
        {
            var conn = new SqliteConnection("Data Source=:memory:");
            conn.Open();

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    CREATE TABLE HotPathEntities (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Title TEXT NOT NULL,
                        Value NUMERIC NOT NULL
                    );
                    CREATE TABLE HotPathOtherEntities (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Description TEXT NOT NULL
                    );
                ";
                cmd.ExecuteNonQuery();
            }

            return conn;
        }

        [Fact]
        public void EntityMaterializer_GetMaterializer_PreResolvesAndExecutesCorrectly()
        {
            using var conn = CreateDatabase();
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "INSERT INTO HotPathEntities (Title, Value) VALUES ('Item 1', 10.5), ('Item 2', 20.5);";
                cmd.ExecuteNonQuery();
            }

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT Id, Title, Value FROM HotPathEntities ORDER BY Id";
                using var reader = cmd.ExecuteReader();

                var materializer = EntityMaterializer.GetMaterializer<HotPathEntity>(reader);
                var items = new List<HotPathEntity>();
                while (reader.Read())
                {
                    items.Add(materializer(reader, null));
                }

                Assert.Equal(2, items.Count);
                Assert.Equal(1, items[0].Id);
                Assert.Equal("Item 1", items[0].Title);
                Assert.Equal(10.5m, items[0].Value);
                Assert.Equal(2, items[1].Id);
                Assert.Equal("Item 2", items[1].Title);
                Assert.Equal(20.5m, items[1].Value);
            }
        }

        [Fact]
        public void ColumnMapping_Setter_CompiledDelegateSetsPropertyValue()
        {
            var mapping = MappingCache.GetMapping<HotPathEntity>();
            var idCol = mapping.Columns.First(c => c.ColumnName == "Id");
            var titleCol = mapping.Columns.First(c => c.ColumnName == "Title");

            Assert.NotNull(idCol.Setter);
            Assert.NotNull(titleCol.Setter);

            var entity = new HotPathEntity();
            idCol.Setter(entity, 123);
            titleCol.Setter(entity, "FastSetterTest");

            Assert.Equal(123, entity.Id);
            Assert.Equal("FastSetterTest", entity.Title);
        }

        [Fact]
        public void ChangeTracker_DetectAllChanges_SinglePassDetectsMultiTableUpdates()
        {
            var tracker = new ChangeTracker();
            var item1 = new HotPathEntity { Id = 1, Title = "Original Title 1", Value = 100m };
            var item2 = new HotPathOtherEntity { Id = 10, Description = "Original Desc 10" };

            tracker.TrackLoaded(item1, MappingCache.GetMapping<HotPathEntity>());
            tracker.TrackLoaded(item2, MappingCache.GetMapping<HotPathOtherEntity>());

            // Mutate both entities across different tables
            item1.Title = "Modified Title 1";
            item2.Description = "Modified Desc 10";

            var updates = tracker.DetectAllChanges(type => MappingCache.GetMapping(type));

            Assert.Equal(2, updates.Count);
            Assert.Contains(updates, u => u.EntityType == typeof(HotPathEntity) && ReferenceEquals(u.Entity, item1));
            Assert.Contains(updates, u => u.EntityType == typeof(HotPathOtherEntity) && ReferenceEquals(u.Entity, item2));

            var u1 = updates.First(u => ReferenceEquals(u.Entity, item1));
            Assert.Contains("Title", u1.ChangedProperties);

            var u2 = updates.First(u => ReferenceEquals(u.Entity, item2));
            Assert.Contains("Description", u2.ChangedProperties);
        }

        [Fact]
        public void SqlContext_InsertAutoGeneratedId_UsesCompiledSetter()
        {
            using var conn = CreateDatabase();
            using var db = new SqlContext(conn);

            var entity = new HotPathEntity { Title = "Inserted", Value = 99.9m };
            db.Insert(entity);
            db.SubmitChanges();

            Assert.True(entity.Id > 0);

            var loaded = db.GetTable<HotPathEntity>().Get(entity.Id);
            Assert.NotNull(loaded);
            Assert.Equal("Inserted", loaded.Title);
        }
    }
}
