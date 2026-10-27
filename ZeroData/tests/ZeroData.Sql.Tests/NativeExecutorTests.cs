using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Xunit;
using ZeroData.Sql.Mapping;

namespace ZeroData.Sql.Tests
{
    public enum UserRole
    {
        Guest = 0,
        Member = 1,
        Administrator = 2
    }

    [Table(Name = "ComprehensiveUsers")]
    public class ComprehensiveUser
    {
        [Column(Name = "Id", IsPrimaryKey = true)]
        public int Id { get; set; }

        [Column(Name = "Username")]
        public string Username { get; set; }

        [Column(Name = "Balance")]
        public decimal Balance { get; set; }

        [Column(Name = "Score")]
        public double Score { get; set; }

        [Column(Name = "IsActive")]
        public bool IsActive { get; set; }

        [Column(Name = "CreatedAt")]
        public DateTime CreatedAt { get; set; }

        [Column(Name = "UserGuid")]
        public Guid UserGuid { get; set; }

        [Column(Name = "Role")]
        public UserRole Role { get; set; }

        [Column(Name = "NullableAge")]
        public int? NullableAge { get; set; }

        [Column(Name = "Bio")]
        public string NullableBio { get; set; }
    }

    public class NativeExecutorTests : IDisposable
    {
        private readonly SqliteConnection _connection;

        public NativeExecutorTests()
        {
            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();

            using var cmd = _connection.CreateCommand();
            cmd.CommandText = @"
                CREATE TABLE ComprehensiveUsers (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Username TEXT NOT NULL,
                    Balance NUMERIC NOT NULL,
                    Score REAL NOT NULL,
                    IsActive INTEGER NOT NULL,
                    CreatedAt TEXT NOT NULL,
                    UserGuid TEXT NOT NULL,
                    Role INTEGER NOT NULL,
                    NullableAge INTEGER NULL,
                    Bio TEXT NULL
                );";
            cmd.ExecuteNonQuery();
        }

        public void Dispose()
        {
            _connection?.Dispose();
        }

        [Fact]
        public void Query_MaterializesAllTypesAccurately()
        {
            var now = new DateTime(2026, 9, 13, 12, 0, 0, DateTimeKind.Utc);
            var guid = Guid.NewGuid();

            _connection.Execute(@"
                INSERT INTO ComprehensiveUsers 
                (Username, Balance, Score, IsActive, CreatedAt, UserGuid, Role, NullableAge, Bio)
                VALUES (@Username, @Balance, @Score, @IsActive, @CreatedAt, @UserGuid, @Role, @NullableAge, @Bio)",
                new
                {
                    Username = "ZeroUser",
                    Balance = 1250.75m,
                    Score = 98.6,
                    IsActive = true,
                    CreatedAt = now.ToString("o"),
                    UserGuid = guid.ToString(),
                    Role = UserRole.Administrator,
                    NullableAge = (int?)28,
                    Bio = "Platform Architect"
                });

            var users = _connection.Query<ComprehensiveUser>("SELECT * FROM ComprehensiveUsers").ToList();

            Assert.Single(users);
            var u = users[0];
            Assert.Equal(1, u.Id);
            Assert.Equal("ZeroUser", u.Username);
            Assert.Equal(1250.75m, u.Balance);
            Assert.Equal(98.6, u.Score);
            Assert.True(u.IsActive);
            Assert.Equal(now, u.CreatedAt);
            Assert.Equal(guid, u.UserGuid);
            Assert.Equal(UserRole.Administrator, u.Role);
            Assert.Equal(28, u.NullableAge);
            Assert.Equal("Platform Architect", u.NullableBio);
        }

        [Fact]
        public void Query_HandlesNullAndNullableFields_WithoutException()
        {
            _connection.Execute(@"
                INSERT INTO ComprehensiveUsers 
                (Username, Balance, Score, IsActive, CreatedAt, UserGuid, Role, NullableAge, Bio)
                VALUES (@Username, @Balance, @Score, @IsActive, @CreatedAt, @UserGuid, @Role, @NullableAge, @Bio)",
                new
                {
                    Username = "NullTester",
                    Balance = 0m,
                    Score = 0.0,
                    IsActive = false,
                    CreatedAt = DateTime.UtcNow.ToString("o"),
                    UserGuid = Guid.NewGuid().ToString(),
                    Role = UserRole.Guest,
                    NullableAge = (int?)null,
                    Bio = (string)null
                });

            var user = _connection.QueryFirstOrDefault<ComprehensiveUser>("SELECT * FROM ComprehensiveUsers WHERE Username = @Username",
                new { Username = "NullTester" });

            Assert.NotNull(user);
            Assert.Null(user.NullableAge);
            Assert.Null(user.NullableBio);
        }

        [Fact]
        public async Task QueryAsync_WithCancellationToken_WorksCorrectly()
        {
            _connection.Execute(@"
                INSERT INTO ComprehensiveUsers 
                (Username, Balance, Score, IsActive, CreatedAt, UserGuid, Role, NullableAge, Bio)
                VALUES ('AsyncUser', 500, 80, 1, '2026-01-01', '00000000-0000-0000-0000-000000000000', 1, 25, 'Async')");

            using var cts = new CancellationTokenSource();
            var users = (await _connection.QueryAsync<ComprehensiveUser>(
                "SELECT * FROM ComprehensiveUsers", cancellationToken: cts.Token)).ToList();

            Assert.Single(users);
            Assert.Equal("AsyncUser", users[0].Username);
        }

        [Fact]
        public void ExecuteScalar_ReturnsCoercedType()
        {
            _connection.Execute(@"
                INSERT INTO ComprehensiveUsers 
                (Username, Balance, Score, IsActive, CreatedAt, UserGuid, Role, NullableAge, Bio)
                VALUES ('ScalarUser', 100, 50, 1, '2026-01-01', '00000000-0000-0000-0000-000000000000', 1, 30, NULL)");

            var count = _connection.ExecuteScalar<int>("SELECT COUNT(*) FROM ComprehensiveUsers");
            Assert.Equal(1, count);

            var countLong = _connection.ExecuteScalar<long>("SELECT COUNT(*) FROM ComprehensiveUsers");
            Assert.Equal(1L, countLong);

            var username = _connection.ExecuteScalar<string>("SELECT Username FROM ComprehensiveUsers WHERE Id = 1");
            Assert.Equal("ScalarUser", username);
        }

        [Fact]
        public void DynamicQuery_ReturnsZeroRow()
        {
            _connection.Execute(@"
                INSERT INTO ComprehensiveUsers 
                (Username, Balance, Score, IsActive, CreatedAt, UserGuid, Role, NullableAge, Bio)
                VALUES ('DynamicUser', 200, 75, 1, '2026-01-01', '00000000-0000-0000-0000-000000000000', 1, 35, 'Dynamic Test')");

            var dynamicRows = _connection.Query("SELECT Id, Username, Balance FROM ComprehensiveUsers").ToList();

            Assert.Single(dynamicRows);
            dynamic row = dynamicRows[0];

            Assert.Equal("DynamicUser", (string)row.Username);
            Assert.Equal(200L, (long)row.Balance); // SQLite stores numeric as integer/real
        }

        [Fact]
        public void NonGenericQuery_MaterializesGivenType()
        {
            _connection.Execute(@"
                INSERT INTO ComprehensiveUsers 
                (Username, Balance, Score, IsActive, CreatedAt, UserGuid, Role, NullableAge, Bio)
                VALUES ('TypeUser', 300, 85, 1, '2026-01-01', '00000000-0000-0000-0000-000000000000', 1, 40, 'Type Test')");

            var results = _connection.Query(typeof(ComprehensiveUser), "SELECT * FROM ComprehensiveUsers").ToList();

            Assert.Single(results);
            Assert.IsType<ComprehensiveUser>(results[0]);
            var u = (ComprehensiveUser)results[0];
            Assert.Equal("TypeUser", u.Username);
        }

        [Fact]
        public void Query_PartialColumns_CompilesDistinctSchemaDelegates()
        {
            _connection.Execute(@"
                INSERT INTO ComprehensiveUsers 
                (Username, Balance, Score, IsActive, CreatedAt, UserGuid, Role, NullableAge, Bio)
                VALUES ('PartialUser', 400, 90, 1, '2026-01-01', '00000000-0000-0000-0000-000000000000', 1, 45, 'Partial')");

            // Query only 2 columns
            var partial = _connection.Query<ComprehensiveUser>("SELECT Id, Username FROM ComprehensiveUsers").First();
            Assert.Equal(1, partial.Id);
            Assert.Equal("PartialUser", partial.Username);
            Assert.Equal(0m, partial.Balance); // not selected

            // Query full columns
            var full = _connection.Query<ComprehensiveUser>("SELECT * FROM ComprehensiveUsers").First();
            Assert.Equal(1, full.Id);
            Assert.Equal("PartialUser", full.Username);
            Assert.Equal(400m, full.Balance);
        }

        [Fact]
        public void ValueConverter_WorksWithCustomRegisteredTypes()
        {
            var converters = new ValueConverterCollection();
            converters.Add<string, string>(
                toDb: s => "ENCRYPTED:" + s,
                fromDb: s => s.StartsWith("ENCRYPTED:") ? s.Substring("ENCRYPTED:".Length) : s);

            _connection.Execute(
                "INSERT INTO ComprehensiveUsers (Username, Balance, Score, IsActive, CreatedAt, UserGuid, Role) VALUES (@Username, 0, 0, 0, '2026-01-01', '00000000-0000-0000-0000-000000000000', 0)",
                new { Username = "SecretAgent" },
                converters: converters);

            var rawUsername = _connection.ExecuteScalar<string>("SELECT Username FROM ComprehensiveUsers WHERE Id = 1");
            Assert.Equal("ENCRYPTED:SecretAgent", rawUsername);

            var user = _connection.QueryFirstOrDefault<ComprehensiveUser>(
                "SELECT * FROM ComprehensiveUsers WHERE Id = 1",
                converters: converters);

            Assert.NotNull(user);
            Assert.Equal("SecretAgent", user.Username);
        }
    }
}
