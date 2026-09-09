using System;
using System.Data;
using Xunit;
using ZeroPrimitives.Mapping;

namespace ZeroPrimitives.Tests
{
    public class FastTableMapperTests
    {
        public class EmployeeDto
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public decimal Salary { get; set; }
            public double Rating { get; set; }
            public bool IsActive { get; set; }
            public DateTime HireDate { get; set; }
            public Guid TenantId { get; set; }
        }

        [Fact]
        public void TestDataTableToListMapping()
        {
            var table = new DataTable();
            table.Columns.Add("Id", typeof(int));
            table.Columns.Add("Name", typeof(string));
            table.Columns.Add("Salary", typeof(decimal));
            table.Columns.Add("Rating", typeof(double));
            table.Columns.Add("IsActive", typeof(bool));
            table.Columns.Add("HireDate", typeof(DateTime));
            table.Columns.Add("TenantId", typeof(Guid));

            var g1 = Guid.NewGuid();
            var g2 = Guid.NewGuid();
            var dt1 = new DateTime(2023, 1, 15);
            var dt2 = new DateTime(2024, 6, 20);

            table.Rows.Add(1, "Alice", 50000.50m, 4.8, true, dt1, g1);
            table.Rows.Add(2, "Bob", 65000.00m, 4.2, false, dt2, g2);

            var list = table.ToList<EmployeeDto>();

            Assert.Equal(2, list.Count);

            Assert.Equal(1, list[0].Id);
            Assert.Equal("Alice", list[0].Name);
            Assert.Equal(50000.50m, list[0].Salary);
            Assert.Equal(4.8, list[0].Rating);
            Assert.True(list[0].IsActive);
            Assert.Equal(dt1, list[0].HireDate);
            Assert.Equal(g1, list[0].TenantId);

            Assert.Equal(2, list[1].Id);
            Assert.Equal("Bob", list[1].Name);
            Assert.Equal(65000.00m, list[1].Salary);
            Assert.Equal(4.2, list[1].Rating);
            Assert.False(list[1].IsActive);
            Assert.Equal(dt2, list[1].HireDate);
            Assert.Equal(g2, list[1].TenantId);
        }

        [Fact]
        public void TestDataRowToDtoWithDbNull()
        {
            var table = new DataTable();
            table.Columns.Add("Id", typeof(int));
            table.Columns.Add("Name", typeof(string));
            table.Columns.Add("Salary", typeof(decimal));

            var row = table.NewRow();
            row["Id"] = 10;
            row["Name"] = DBNull.Value;
            row["Salary"] = DBNull.Value;
            table.Rows.Add(row);

            var dto = row.To<EmployeeDto>();

            Assert.Equal(10, dto.Id);
            Assert.Equal(string.Empty, dto.Name); // default initialized
            Assert.Equal(0.0m, dto.Salary);       // default decimal
        }

        [Fact]
        public void TestTypeCoercionMapping()
        {
            // Table has strings, DTO has strongly-typed int and decimal
            var table = new DataTable();
            table.Columns.Add("Id", typeof(string));
            table.Columns.Add("Salary", typeof(string));

            table.Rows.Add("101", "12345.67");

            var list = table.ToList<EmployeeDto>();

            Assert.Single(list);
            Assert.Equal(101, list[0].Id);
            Assert.Equal(12345.67m, list[0].Salary);
        }
    }
}
