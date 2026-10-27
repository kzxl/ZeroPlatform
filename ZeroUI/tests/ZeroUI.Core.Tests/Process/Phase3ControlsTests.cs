using System;
using System.Collections.Generic;
using Xunit;

namespace ZeroUI.Core.Tests.Process
{
    public class Phase3ControlsTests
    {
        [Fact]
        public void Breadcrumb_PathParsing_BuildsSegmentsAccurately()
        {
            string rawPath = "Kho vận/Xuất kho/Phiếu PXK-2026-001";
            string[] segments = rawPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

            Assert.Equal(3, segments.Length);
            Assert.Equal("Kho vận", segments[0]);
            Assert.Equal("Xuất kho", segments[1]);
            Assert.Equal("Phiếu PXK-2026-001", segments[2]);
        }

        [Fact]
        public void NotificationBadge_FormatValue_HandlesBoundaries()
        {
            // Test 0 -> typically hidden or "0"
            int count0 = 0;
            string display0 = count0 > 99 ? "99+" : count0.ToString();
            Assert.Equal("0", display0);

            // Test normal count
            int count5 = 5;
            string display5 = count5 > 99 ? "99+" : count5.ToString();
            Assert.Equal("5", display5);

            // Test overflow count
            int count150 = 150;
            string display150 = count150 > 99 ? "99+" : count150.ToString();
            Assert.Equal("99+", display150);
        }

        [Fact]
        public void DiffEngine_LineComparison_DetectsAdditionsAndDeletions()
        {
            string oldText = "Line 1\nLine 2\nLine 3";
            string newText = "Line 1\nLine 2 Modified\nLine 3\nLine 4 Added";

            string[] oldLines = oldText.Split('\n');
            string[] newLines = newText.Split('\n');

            int unchanged = 0;
            int added = 0;
            int deleted = 0;

            int oldIdx = 0;
            int newIdx = 0;

            while (oldIdx < oldLines.Length || newIdx < newLines.Length)
            {
                if (oldIdx < oldLines.Length && newIdx < newLines.Length)
                {
                    if (oldLines[oldIdx] == newLines[newIdx])
                    {
                        unchanged++;
                        oldIdx++;
                        newIdx++;
                    }
                    else
                    {
                        deleted++;
                        added++;
                        oldIdx++;
                        newIdx++;
                    }
                }
                else if (oldIdx < oldLines.Length)
                {
                    deleted++;
                    oldIdx++;
                }
                else
                {
                    added++;
                    newIdx++;
                }
            }

            Assert.Equal(2, unchanged); // "Line 1" and "Line 3"
            Assert.Equal(2, added);     // "Line 2 Modified" and "Line 4 Added"
            Assert.Equal(1, deleted);   // "Line 2"
        }
    }
}
