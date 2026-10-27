using System;
using System.Diagnostics;
using Xunit;
using ZeroData.Core;
using ZeroUI.Core.Virtualization;

namespace ZeroUI.Core.Tests
{
    public class SelectionMaskVirtualizationTests
    {
        [Fact]
        public void ApplySelectionMask_Null_ThrowsArgumentNullException()
        {
            var map = new RowIndexMap(100);
            Assert.Throws<ArgumentNullException>(() => map.ApplySelectionMask(null!));
        }

        [Fact]
        public void ApplySelectionMask_AllTrue_RestoresIdentity()
        {
            const int count = 5000;
            var mask = SelectionMask.CreateAllSelected(count);
            var map = new RowIndexMap(100);

            // Break identity first
            map.EnsureMaterialized();
            Assert.False(map.IsIdentity);

            map.ApplySelectionMask(mask);

            Assert.True(map.IsIdentity);
            Assert.Equal(count, map.ActiveCount);
            for (int i = 0; i < count; i += 100)
            {
                Assert.Equal(i, map[i]);
            }
        }

        [Fact]
        public void ApplySelectionMask_AllFalse_ClearsRows()
        {
            const int count = 5000;
            var mask = SelectionMask.CreateEmpty(count);
            var map = new RowIndexMap(count);
            map.ResetIdentity(count);

            map.ApplySelectionMask(mask);

            Assert.False(map.IsIdentity);
            Assert.Equal(0, map.ActiveCount);
        }

        [Fact]
        public void ApplySelectionMask_SparseBits_ExtractsExactIndices()
        {
            const int totalRows = 2000;
            var mask = SelectionMask.CreateEmpty(totalRows);

            int[] targetIndices = new[] { 0, 7, 8, 63, 64, 65, 127, 128, 500, 1023, 1024, 1999 };
            foreach (var idx in targetIndices)
            {
                mask.SetSelected(idx, true);
            }

            var map = new RowIndexMap();
            map.ApplySelectionMask(mask);

            Assert.Equal(targetIndices.Length, map.ActiveCount);
            for (int i = 0; i < targetIndices.Length; i++)
            {
                Assert.Equal(targetIndices[i], map[i]);
            }
        }

        [Fact]
        public void ApplySelectionMask_DenseAndSparseMixed_MatchesExpectedIndices()
        {
            const int totalRows = 50000;
            var mask = SelectionMask.CreateEmpty(totalRows);

            // Block 1: Continuous 128 rows (2 dense 64-bit words)
            for (int i = 100; i < 228; i++) mask.SetSelected(i, true);

            // Block 2: Alternating even rows
            for (int i = 1000; i < 2000; i += 2) mask.SetSelected(i, true);

            // Block 3: Prime-like steps
            for (int i = 5000; i < 10000; i += 37) mask.SetSelected(i, true);

            int[] expected = mask.ToIndices();

            var map = new RowIndexMap();
            map.ApplySelectionMask(mask);

            Assert.Equal(expected.Length, map.ActiveCount);
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.Equal(expected[i], map[i]);
            }
        }

        [Fact]
        public void ApplySelectionMask_1MillionRows_HighPerformance()
        {
            const int totalRows = 1_000_000;
            var mask = SelectionMask.CreateEmpty(totalRows);

            // Select 20% of rows (200,000 matches)
            for (int i = 0; i < totalRows; i += 5)
            {
                mask.SetSelected(i, true);
            }

            var map = new RowIndexMap(totalRows);

            var sw = Stopwatch.StartNew();
            map.ApplySelectionMask(mask);
            sw.Stop();

            Assert.Equal(200_000, map.ActiveCount);
            Assert.True(sw.ElapsedMilliseconds < 50, $"ApplySelectionMask on 1M rows took {sw.ElapsedMilliseconds}ms, expected < 50ms");
            Assert.Equal(0, map[0]);
            Assert.Equal(5, map[1]);
            Assert.Equal(10, map[2]);
        }
    }
}
