using System;
using System.Collections.Generic;
using Xunit;
using ZeroUI.Core.Icons;

namespace ZeroUI.Core.Tests
{
    public class IconSystemEnterpriseTests
    {
        [Theory]
        [InlineData(IconKey.Save, "Save")]
        [InlineData(IconKey.Add, "Add")]
        [InlineData(IconKey.Delete, "Delete")]
        [InlineData(IconKey.AlignLeft, "AlignLeft")]
        [InlineData(IconKey.DistributeHorizontal, "DistributeHorizontal")]
        [InlineData(IconKey.ZoomFit, "ZoomFit")]
        public void IconKey_ToKeyString_ReturnsExpectedEnumName(IconKey key, string expected)
        {
            Assert.Equal(expected, key.ToKeyString());
        }

        [Theory]
        [InlineData("Save", true, IconKey.Save)]
        [InlineData("save", true, IconKey.Save)]
        [InlineData("SAVE", true, IconKey.Save)]
        [InlineData("align-left", true, IconKey.AlignLeft)]
        [InlineData("align_left", true, IconKey.AlignLeft)]
        [InlineData("distribute-horizontal", true, IconKey.DistributeHorizontal)]
        [InlineData("distribute_vertical", true, IconKey.DistributeVertical)]
        [InlineData("zoom-in", true, IconKey.ZoomIn)]
        [InlineData("auto-layout", true, IconKey.AutoLayout)]
        [InlineData("non_existent_key_xyz", false, IconKey.Document)]
        [InlineData("", false, IconKey.Document)]
        [InlineData(null, false, IconKey.Document)]
        public void IconKeyExtensions_TryParseKey_ParsesNormalizedStringsAccurately(string? input, bool expectedSuccess, IconKey expectedKey)
        {
            bool success = IconKeyExtensions.TryParseKey(input, out var resolved);
            Assert.Equal(expectedSuccess, success);
            if (expectedSuccess)
            {
                Assert.Equal(expectedKey, resolved);
            }
        }

        [Fact]
        public void IconKey_AllEnumValues_AreUniqueAndValid()
        {
            var values = (IconKey[])Enum.GetValues(typeof(IconKey));
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var val in values)
            {
                string name = val.ToString();
                Assert.False(string.IsNullOrWhiteSpace(name));
                Assert.True(names.Add(name), $"Duplicate icon key name detected: {name}");
            }

            Assert.True(values.Length >= 40, $"Expected >= 40 standard enterprise icon keys, found {values.Length}");
        }

        [Fact]
        public void IZeroIconProvider_CustomImplementation_ProvidesExpectedKeys()
        {
            var provider = new TestBrandIconProvider();

            Assert.Equal("TestBrand", provider.ProviderName);
            Assert.True(provider.HasIcon("CompanyLogo"));
            Assert.True(provider.HasIcon("Save"));
            Assert.False(provider.HasIcon("UnknownKey"));
            Assert.Equal(2, provider.AvailableKeys.Count);
        }

        private sealed class TestBrandIconProvider : IZeroIconProvider
        {
            public string ProviderName => "TestBrand";

            private readonly HashSet<string> _keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "CompanyLogo",
                "Save"
            };

            public bool HasIcon(string key) => _keys.Contains(key);

            public IReadOnlyCollection<string> AvailableKeys => _keys;
        }
    }
}
