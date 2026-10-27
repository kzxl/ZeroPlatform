using System;
using System.Collections.Generic;
using System.IO;
using Xunit;
using ZeroDocuments.Excel;

namespace ZeroDocuments.Tests
{
    public class DeepPocoHydrationTests
    {
        public enum OrderStatus
        {
            Draft = 0,
            Processing = 1,
            Shipped = 2,
            Delivered = 3,
            Cancelled = 4
        }

        public class BaseEntity
        {
            public int Id { get; set; }
            public DateTime CreatedAt { get; set; }
        }

        public class AdvancedOrderDto : BaseEntity
        {
            public Guid TrackingId { get; set; }
            public OrderStatus Status { get; set; }
            public OrderStatus? NullableStatus { get; set; }
            public TimeSpan LeadTime { get; set; }
            public DateTimeOffset OffsetTimestamp { get; set; }
            public int Quantity { get; set; }
            public decimal UnitPrice { get; set; }

            // Computed read-only property (no setter)
            public decimal Total => Quantity * UnitPrice;
        }

        [Fact]
        public void DeepPoco_EnumGuidTimeSpanAndInheritance_ShouldHydrateAccurately()
        {
            var expectedTrackingId = Guid.NewGuid();
            var expectedTime = DateTime.UtcNow;
            var expectedOffset = new DateTimeOffset(2026, 9, 20, 14, 30, 0, TimeSpan.FromHours(7));
            var expectedLeadTime = TimeSpan.FromHours(36);

            var orders = new List<AdvancedOrderDto>
            {
                new AdvancedOrderDto
                {
                    Id = 1001,
                    CreatedAt = expectedTime,
                    TrackingId = expectedTrackingId,
                    Status = OrderStatus.Shipped,
                    NullableStatus = OrderStatus.Delivered,
                    LeadTime = expectedLeadTime,
                    OffsetTimestamp = expectedOffset,
                    Quantity = 5,
                    UnitPrice = 20.0m
                },
                new AdvancedOrderDto
                {
                    Id = 1002,
                    CreatedAt = expectedTime.AddDays(1),
                    TrackingId = Guid.Empty,
                    Status = OrderStatus.Processing,
                    NullableStatus = null,
                    LeadTime = TimeSpan.FromMinutes(45),
                    OffsetTimestamp = expectedOffset.AddDays(1),
                    Quantity = 2,
                    UnitPrice = 100.0m
                }
            };

            using var ms = new MemoryStream();
            ExcelWriter.WriteToStream(ms, orders);

            ms.Position = 0;
            var readBack = ExcelReader.Read<AdvancedOrderDto>(ms);

            Assert.Equal(2, readBack.Count);

            // Row 1 assertions
            var row1 = readBack[0];
            Assert.Equal(1001, row1.Id); // Inherited property from BaseEntity
            Assert.Equal(expectedTrackingId, row1.TrackingId);
            Assert.Equal(OrderStatus.Shipped, row1.Status);
            Assert.Equal(OrderStatus.Delivered, row1.NullableStatus);
            Assert.Equal(expectedLeadTime, row1.LeadTime);
            Assert.Equal(5, row1.Quantity);
            Assert.Equal(20.0m, row1.UnitPrice);
            Assert.Equal(100.0m, row1.Total); // Computed property works

            // Row 2 assertions
            var row2 = readBack[1];
            Assert.Equal(1002, row2.Id);
            Assert.Equal(Guid.Empty, row2.TrackingId);
            Assert.Equal(OrderStatus.Processing, row2.Status);
            Assert.Null(row2.NullableStatus);
            Assert.Equal(TimeSpan.FromMinutes(45), row2.LeadTime);
            Assert.Equal(200.0m, row2.Total);
        }

        [Fact]
        public void DeepPoco_NumericEnumValue_ShouldParseSuccessfully()
        {
            // Simulate Excel sheet where Status column has integer value "2" instead of string "Shipped"
            var rows = new List<IReadOnlyList<object?>>
            {
                new object?[] { 5001, "2" } // 2 is OrderStatus.Shipped
            };

            var headers = new[] { "Id", "Status" };

            using var ms = new MemoryStream();
            ExcelWriter.WriteRowsToStream(ms, rows, headers);

            ms.Position = 0;
            var readBack = ExcelReader.Read<AdvancedOrderDto>(ms);

            Assert.Single(readBack);
            Assert.Equal(5001, readBack[0].Id);
            Assert.Equal(OrderStatus.Shipped, readBack[0].Status);
        }
    }
}
