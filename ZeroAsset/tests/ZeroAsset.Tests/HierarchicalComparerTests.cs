using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using ZeroAsset.Sorting;

namespace ZeroAsset.Tests
{
    public class HierarchicalComparerTests
    {
        private class TestAsset
        {
            public string Path { get; set; } = string.Empty;
            public DateTime Date { get; set; }
            public int Rating { get; set; }

            public override string ToString() => Path;
        }

        [Fact]
        public void Test_HierarchicalSorting_Guarantees_Contiguity_And_Order()
        {
            var dt1 = new DateTime(2026, 1, 1);
            var dt2 = new DateTime(2026, 2, 1);

            var items = new List<TestAsset>
            {
                new TestAsset { Path = "C:/Photos/B.jpg", Date = dt2 },
                new TestAsset { Path = "C:/Photos/A#vc2.jpg", Date = dt1 },
                new TestAsset { Path = "C:/Photos/B#vc1.jpg", Date = dt2 },
                new TestAsset { Path = "C:/Photos/A.jpg", Date = dt1 },
                new TestAsset { Path = "C:/Photos/A#vc1.jpg", Date = dt1 },
            };

            // Sort by Date ascending
            var comparer = new HierarchicalContiguousComparer<TestAsset>(
                a => a.Path,
                (a, b) => a.Date.CompareTo(b.Date)
            );

            items.Sort(comparer);

            var paths = items.Select(x => x.Path).ToList();

            // Family A (dt1) must come before Family B (dt2)
            // Within Family A: A.jpg (master), A#vc1.jpg (copy 1), A#vc2.jpg (copy 2)
            // Within Family B: B.jpg (master), B#vc1.jpg (copy 1)
            Assert.Equal("C:/Photos/A.jpg", paths[0]);
            Assert.Equal("C:/Photos/A#vc1.jpg", paths[1]);
            Assert.Equal("C:/Photos/A#vc2.jpg", paths[2]);
            Assert.Equal("C:/Photos/B.jpg", paths[3]);
            Assert.Equal("C:/Photos/B#vc1.jpg", paths[4]);
        }
    }
}
