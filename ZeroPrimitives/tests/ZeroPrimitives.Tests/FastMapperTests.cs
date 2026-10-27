using System.Collections.Generic;
using Xunit;
using ZeroPrimitives.Mapping;

namespace ZeroPrimitives.Tests
{
    public class FastMapperTests
    {
        public class SampleDto
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public decimal Price { get; set; }
            public bool IsActive { get; set; }
            public int? NullableCount { get; set; }
        }

        public class SampleEntity
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public decimal Price { get; set; }
            public bool IsActive { get; set; }
            public int NullableCount { get; set; }
            public string ExtraField { get; set; } = "Default";
        }

        [Fact]
        public void CopyTo_CopiesMatchingProperties()
        {
            var dto = new SampleDto
            {
                Id = 101,
                Name = "Alpha Sensor",
                Price = 250000m,
                IsActive = true,
                NullableCount = 5
            };

            var entity = new SampleEntity();
            FastMapper.CopyTo(dto, entity);

            Assert.Equal(101, entity.Id);
            Assert.Equal("Alpha Sensor", entity.Name);
            Assert.Equal(250000m, entity.Price);
            Assert.True(entity.IsActive);
            Assert.Equal(5, entity.NullableCount);
            Assert.Equal("Default", entity.ExtraField);
        }

        [Fact]
        public void Map_CreatesAndPopulatesInstance()
        {
            var entity = new SampleEntity
            {
                Id = 202,
                Name = "Beta Gateway",
                Price = 5000000m,
                IsActive = false,
                NullableCount = 10
            };

            var dto = FastMapper.Map<SampleEntity, SampleDto>(entity);

            Assert.Equal(202, dto.Id);
            Assert.Equal("Beta Gateway", dto.Name);
            Assert.Equal(5000000m, dto.Price);
            Assert.False(dto.IsActive);
            Assert.Equal(10, dto.NullableCount);
        }

        [Fact]
        public void MapList_MapsCollectionOfItems()
        {
            var entities = new List<SampleEntity>
            {
                new SampleEntity { Id = 1, Name = "Item 1", Price = 100m },
                new SampleEntity { Id = 2, Name = "Item 2", Price = 200m }
            };

            var dtos = FastMapper.MapList<SampleEntity, SampleDto>(entities);

            Assert.Equal(2, dtos.Count);
            Assert.Equal(1, dtos[0].Id);
            Assert.Equal("Item 1", dtos[0].Name);
            Assert.Equal(2, dtos[1].Id);
            Assert.Equal("Item 2", dtos[1].Name);
        }

        [Fact]
        public void DynamicMapping_MapsUntypedObjectsAndLists()
        {
            object rawEntity = new SampleEntity
            {
                Id = 303,
                Name = "Dynamic Device",
                Price = 99000m,
                IsActive = true
            };

            var dto = FastMapper.MapDynamic<SampleDto>(rawEntity);
            Assert.NotNull(dto);
            Assert.Equal(303, dto.Id);
            Assert.Equal("Dynamic Device", dto.Name);
            Assert.Equal(99000m, dto.Price);
            Assert.True(dto.IsActive);

            var rawList = new List<object>
            {
                new SampleEntity { Id = 10, Name = "Batch 1" },
                new SampleEntity { Id = 20, Name = "Batch 2" }
            };

            var dtoList = FastMapper.MapListDynamic<SampleDto>(rawList);
            Assert.Equal(2, dtoList.Count);
            Assert.Equal(10, dtoList[0].Id);
            Assert.Equal("Batch 1", dtoList[0].Name);
            Assert.Equal(20, dtoList[1].Id);
            Assert.Equal("Batch 2", dtoList[1].Name);
        }
    }
}
