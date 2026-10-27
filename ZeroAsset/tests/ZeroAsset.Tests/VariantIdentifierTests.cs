using System.IO;
using Xunit;
using ZeroAsset.Variants;

namespace ZeroAsset.Tests
{
    public class VariantIdentifierTests
    {
        [Theory]
        [InlineData("C:/Photos/IMG_001.jpg", false, 0, "C:/Photos/IMG_001.jpg")]
        [InlineData("C:/Photos/IMG_001#vc1.jpg", true, 1, "C:/Photos/IMG_001.jpg")]
        [InlineData("C:/Photos/IMG_001#vc42.NEF", true, 42, "C:/Photos/IMG_001.NEF")]
        [InlineData("C:/Photos/#vc_folder/IMG_001.jpg", false, 0, "C:/Photos/#vc_folder/IMG_001.jpg")]
        public void Test_Variant_Parsing(string path, bool isVariant, int expectedIndex, string expectedMaster)
        {
            Assert.Equal(isVariant, VariantIdentifier.IsVariant(path));
            Assert.Equal(expectedIndex, VariantIdentifier.GetVariantIndex(path));

            string actualMaster = VariantIdentifier.GetMasterPath(path).Replace('\\', '/');
            string expectedNorm = expectedMaster.Replace('\\', '/');
            Assert.Equal(expectedNorm, actualMaster);
        }

        [Fact]
        public void Test_BuildVariantPath_Prevents_Nesting()
        {
            string master = "C:/Photos/Landscape.jpg";
            string copy1 = VariantIdentifier.BuildVariantPath(master, 1);
            Assert.Equal("C:/Photos/Landscape#vc1.jpg", copy1.Replace('\\', '/'));

            // Calling on an already branched copy replaces the suffix instead of nesting Landscape#vc1#vc2.jpg
            string copy2 = VariantIdentifier.BuildVariantPath(copy1, 2);
            Assert.Equal("C:/Photos/Landscape#vc2.jpg", copy2.Replace('\\', '/'));
        }

        [Fact]
        public void Test_FormatDisplayName()
        {
            Assert.Equal("Photo.jpg", VariantIdentifier.FormatDisplayName("Photo.jpg", 0));
            Assert.Equal("Photo.jpg (Copy 1)", VariantIdentifier.FormatDisplayName("Photo.jpg", 1));
            Assert.Equal("Photo.jpg (Copy 5)", VariantIdentifier.FormatDisplayName("Photo.jpg", 5));
        }
    }
}
