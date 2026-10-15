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

        [Fact]
        public void Test_HierarchicalSorting_MutatedVariantSortFields_MaintainsStrictWeakOrderingAndContiguity()
        {
            // Master A has Rating = 2, but its virtual copy A#vc1 has Rating = 5.
            // Master B has Rating = 3, and its virtual copy B#vc1 has Rating = 1.
            // Without master resolution, sorting descending by Rating creates a cycle:
            // A#vc1 (5) < B (3) < A (2) < A#vc1 (variant of A).
            var items = new List<TestAsset>
            {
                new TestAsset { Path = "C:/Photos/A#vc1.jpg", Rating = 5 },
                new TestAsset { Path = "C:/Photos/B.jpg", Rating = 3 },
                new TestAsset { Path = "C:/Photos/A.jpg", Rating = 2 },
                new TestAsset { Path = "C:/Photos/B#vc1.jpg", Rating = 1 },
                new TestAsset { Path = "C:/Photos/A#vc2.jpg", Rating = 4 },
            };

            // Verify exhaustive pairwise + triple transitivity of the dataset-indexed comparer
            var comparer = new HierarchicalContiguousComparer<TestAsset>(
                items,
                a => a.Path,
                (a, b) => b.Rating.CompareTo(a.Rating) // Rating descending
            );

            for (int i = 0; i < items.Count; i++)
            {
                Assert.Equal(0, comparer.Compare(items[i], items[i]));
                for (int j = 0; j < items.Count; j++)
                {
                    int ij = Math.Sign(comparer.Compare(items[i], items[j]));
                    int ji = Math.Sign(comparer.Compare(items[j], items[i]));
                    Assert.Equal(-ij, ji);

                    for (int k = 0; k < items.Count; k++)
                    {
                        int jk = Math.Sign(comparer.Compare(items[j], items[k]));
                        int ik = Math.Sign(comparer.Compare(items[i], items[k]));
                        if (ij < 0 && jk < 0)
                        {
                            Assert.Equal(-1, ik);
                        }
                    }
                }
            }

            // Sort using SortContiguous helper
            HierarchicalContiguousComparer<TestAsset>.SortContiguous(
                items,
                a => a.Path,
                (a, b) => b.Rating.CompareTo(a.Rating)
            );

            // Family B (master rating 3) must precede Family A (master rating 2),
            // and all variants must cluster contiguously after their master in copy index order!
            Assert.Equal("C:/Photos/B.jpg", items[0].Path);
            Assert.Equal("C:/Photos/B#vc1.jpg", items[1].Path);
            Assert.Equal("C:/Photos/A.jpg", items[2].Path);
            Assert.Equal("C:/Photos/A#vc1.jpg", items[3].Path);
            Assert.Equal("C:/Photos/A#vc2.jpg", items[4].Path);
        }
    }
}
