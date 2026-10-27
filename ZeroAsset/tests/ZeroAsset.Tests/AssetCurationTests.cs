using Xunit;
using ZeroAsset.Cache;
using ZeroAsset.Curation;

namespace ZeroAsset.Tests
{
    public class AssetCurationTests
    {
        [Fact]
        public void Test_Curation_Cloning_And_Equality()
        {
            var c1 = new AssetCuration(5, AssetFlag.Pick, AssetColorLabel.Red, new[] { "Landscape", "Sunset" });
            var c2 = c1.Clone();

            Assert.Equal(c1, c2);
            Assert.Equal(c1.GetHashCode(), c2.GetHashCode());

            // Mutating clone does not affect original
            c2.Rating = 4;
            c2.Keywords.Add("HDR");

            Assert.NotEqual(c1, c2);
            Assert.Equal(5, c1.Rating);
            Assert.Equal(2, c1.Keywords.Count);
        }

        [Fact]
        public void Test_CacheKey_Isolates_Variants()
        {
            string master = "C:/Photos/Photo.jpg";
            string copy1 = "C:/Photos/Photo#vc1.jpg";

            string keyMaster = AssetCacheKey.ComputeKey(master);
            string keyCopy1 = AssetCacheKey.ComputeKey(copy1);

            Assert.NotEmpty(keyMaster);
            Assert.NotEmpty(keyCopy1);
            Assert.NotEqual(keyMaster, keyCopy1);
        }

        [Fact]
        public void Test_AssetCuration_GeneratesSortableUlid()
        {
            var c1 = new AssetCuration(5);
            var c2 = new AssetCuration(4);

            Assert.NotEqual(ZeroPrimitives.Core.Identifiers.FastUlid.Empty, c1.AssetId);
            Assert.NotEqual(ZeroPrimitives.Core.Identifiers.FastUlid.Empty, c2.AssetId);
            Assert.NotEqual(c1.AssetId, c2.AssetId);

            // Clone preserves AssetId
            var clone = c1.Clone();
            Assert.Equal(c1.AssetId, clone.AssetId);
        }
    }
}
