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
        }

        public class SampleEntity
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public decimal Price { get; set; }
            public bool IsActive { get; set; }
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
                IsActive = true
            };

            var entity = new SampleEntity();
            FastMapper.CopyTo(dto, entity);

            Assert.Equal(101, entity.Id);
            Assert.Equal("Alpha Sensor", entity.Name);
            Assert.Equal(250000m, entity.Price);
            Assert.True(entity.IsActive);
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
                IsActive = false
            };

            var dto = FastMapper.Map<SampleEntity, SampleDto>(entity);

            Assert.Equal(202, dto.Id);
            Assert.Equal("Beta Gateway", dto.Name);
            Assert.Equal(5000000m, dto.Price);
            Assert.False(dto.IsActive);
        }
    }
}
