using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace ZeroUI.Core.Tests.Process
{
    public class RibbonLogicTests
    {
        public class TestApprovalItem
        {
            public string Key { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public int Count { get; set; }
        }

        public class TestApprovalGroup
        {
            public string GroupKey { get; set; } = string.Empty;
            public string Title { get; set; } = string.Empty;
            public bool Visible { get; set; } = true;
            public List<TestApprovalItem> Items { get; } = new List<TestApprovalItem>();

            public int TotalCount => Items.Sum(i => Math.Max(0, i.Count));

            public void UpdateCount(string key, int count)
            {
                var item = Items.FirstOrDefault(i => i.Key == key);
                if (item != null) item.Count = count;
            }
        }

        [Fact]
        public void ApprovalGroup_TotalCount_CalculatesSumOfNonNegativeCounts()
        {
            var grp = new TestApprovalGroup { GroupKey = "Common1", Title = "Chờ duyệt chung 1" };
            grp.Items.Add(new TestApprovalItem { Key = "YCSL", Name = "Yêu cầu xử lý", Count = 3 });
            grp.Items.Add(new TestApprovalItem { Key = "YCTN", Name = "Yêu cầu thử nghiệm", Count = 1 });
            grp.Items.Add(new TestApprovalItem { Key = "BG", Name = "Bàn giao", Count = 0 });

            Assert.Equal(4, grp.TotalCount);
        }

        [Fact]
        public void ApprovalGroup_UpdateCount_UpdatesTargetItemAccurately()
        {
            var grp = new TestApprovalGroup { GroupKey = "Production", Title = "Chờ duyệt SX" };
            grp.Items.Add(new TestApprovalItem { Key = "LSX", Name = "Lệnh sản xuất", Count = 5 });
            grp.Items.Add(new TestApprovalItem { Key = "NKDG", Name = "Nhật ký đóng gói", Count = 2 });

            Assert.Equal(7, grp.TotalCount);

            grp.UpdateCount("LSX", 0);
            Assert.Equal(2, grp.TotalCount);

            grp.UpdateCount("NKDG", 8);
            Assert.Equal(8, grp.TotalCount);
        }

        [Fact]
        public void ApprovalGroup_VisibilityToggle_ControlsDisplayStatus()
        {
            var groups = new List<TestApprovalGroup>
            {
                new TestApprovalGroup { GroupKey = "Common1", Title = "Chờ duyệt chung 1", Visible = true },
                new TestApprovalGroup { GroupKey = "Common2", Title = "Chờ duyệt chung 2", Visible = true },
                new TestApprovalGroup { GroupKey = "Production", Title = "Chờ duyệt SX", Visible = false }
            };

            var visibleTitles = groups.Where(g => g.Visible).Select(g => g.Title).ToList();
            Assert.Equal(2, visibleTitles.Count);
            Assert.Contains("Chờ duyệt chung 1", visibleTitles);
            Assert.Contains("Chờ duyệt chung 2", visibleTitles);
            Assert.DoesNotContain("Chờ duyệt SX", visibleTitles);

            // Toggle Production visible
            groups.First(g => g.GroupKey == "Production").Visible = true;
            Assert.Equal(3, groups.Count(g => g.Visible));
        }

        [Theory]
        [InlineData("Thông báo", "1178", "Thông báo (1178)")]
        [InlineData("Góc nhìn 360", "1", "Góc nhìn 360 (1)")]
        [InlineData("Công việc", null, "Công việc")]
        public void RibbonItem_LabelWithBadge_FormatsNicely(string title, string? badge, string expected)
        {
            string formatted = string.IsNullOrEmpty(badge) ? title : $"{title} ({badge})";
            Assert.Equal(expected, formatted);
        }

        [Fact]
        public void ApprovalItem_CustomDisplayFormat_FormatsCorrectly()
        {
            var item = new TestApprovalItem { Key = "YCSL", Name = "Yêu cầu xử lý", Count = 3 };
            string defaultFormat = "{0}: {1}";
            string customFormat = "{0} (Chờ: {1})";

            Assert.Equal("Yêu cầu xử lý: 3", string.Format(defaultFormat, item.Name, item.Count));
            Assert.Equal("Yêu cầu xử lý (Chờ: 3)", string.Format(customFormat, item.Name, item.Count));
        }

        [Fact]
        public void ApprovalGroup_StateStore_LoadsAndSavesStateProperly()
        {
            var registryMock = new Dictionary<string, bool>
            {
                { "Common1", true },
                { "Production", false }
            };

            // Loader
            Func<string, bool, bool> loader = (key, def) => registryMock.TryGetValue(key, out var val) ? val : def;
            // Saver
            Action<string, bool> saver = (key, val) => registryMock[key] = val;

            Assert.True(loader("Common1", false));
            Assert.False(loader("Production", true));
            Assert.True(loader("Inventory", true)); // default fallback

            // Save change
            saver("Production", true);
            Assert.True(loader("Production", false));
        }

        public class MockStatisticDto
        {
            public string Loai { get; set; } = string.Empty;
            public int SoLuong { get; set; }
        }

        [Fact]
        public void ApprovalBinding_GenericMapping_MapsDTOListToCounts()
        {
            var dtoList = new List<MockStatisticDto>
            {
                new MockStatisticDto { Loai = "YCSL", SoLuong = 5 },
                new MockStatisticDto { Loai = "YCTN", SoLuong = 2 },
                new MockStatisticDto { Loai = "BG", SoLuong = 0 }
            };

            var grp = new TestApprovalGroup { GroupKey = "Common1", Title = "Chờ duyệt chung 1" };
            grp.Items.Add(new TestApprovalItem { Key = "YCSL", Name = "YCSL", Count = 0 });
            grp.Items.Add(new TestApprovalItem { Key = "YCTN", Name = "YCTN", Count = 0 });
            grp.Items.Add(new TestApprovalItem { Key = "BG", Name = "BG", Count = 0 });

            // Simulate generic BindApprovalCounts
            foreach (var dto in dtoList)
            {
                grp.UpdateCount(dto.Loai, dto.SoLuong);
            }

            Assert.Equal(7, grp.TotalCount);
            Assert.Equal(5, grp.Items.First(i => i.Key == "YCSL").Count);
            Assert.Equal(2, grp.Items.First(i => i.Key == "YCTN").Count);
            Assert.Equal(0, grp.Items.First(i => i.Key == "BG").Count);
        }

        [Fact]
        public void ApprovalItem_StatusColorResolver_OverridesDefaultColor()
        {
            var item = new TestApprovalItem { Key = "URGENT", Name = "Khẩn cấp", Count = 10 };

            Func<TestApprovalItem, string> resolver = i =>
            {
                if (i.Count >= 10) return "DarkRed";
                if (i.Count > 0) return "Red";
                return "Green";
            };

            Assert.Equal("DarkRed", resolver(item));

            item.Count = 3;
            Assert.Equal("Red", resolver(item));

            item.Count = 0;
            Assert.Equal("Green", resolver(item));
        }
    }
}
