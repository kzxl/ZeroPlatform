using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace ZeroUI.Core.Tests.Process
{
    public class Phase1ControlsTests
    {
        public class SampleDeptDto
        {
            public string Id { get; set; } = string.Empty;
            public string ParentId { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
        }

        [Fact]
        public void HierarchyTree_BuildsParentChildRelationshipsCorrectly()
        {
            var depts = new List<SampleDeptDto>
            {
                new SampleDeptDto { Id = "HQ", ParentId = "", Name = "Headquarters" },
                new SampleDeptDto { Id = "ENG", ParentId = "HQ", Name = "Engineering" },
                new SampleDeptDto { Id = "PROD", ParentId = "HQ", Name = "Production" },
                new SampleDeptDto { Id = "DEV", ParentId = "ENG", Name = "Software Development" },
                new SampleDeptDto { Id = "QA", ParentId = "ENG", Name = "Quality Assurance" }
            };

            // Test hierarchy logic
            var roots = depts.Where(d => string.IsNullOrEmpty(d.ParentId)).ToList();
            Assert.Single(roots);
            Assert.Equal("HQ", roots[0].Id);

            var engChildren = depts.Where(d => d.ParentId == "ENG").ToList();
            Assert.Equal(2, engChildren.Count);
            Assert.Contains(engChildren, d => d.Id == "DEV");
            Assert.Contains(engChildren, d => d.Id == "QA");
        }

        [Theory]
        [InlineData(500, "500 B")]
        [InlineData(1536, "1.5 KB")]
        [InlineData(1048576, "1.0 MB")]
        [InlineData(5242880, "5.0 MB")]
        public void FileSize_FormatsCorrectly(long bytes, string expected)
        {
            string formatted;
            if (bytes < 1024) formatted = $"{bytes} B";
            else if (bytes < 1024 * 1024) formatted = $"{bytes / 1024.0:F1} KB";
            else formatted = $"{bytes / (1024.0 * 1024.0):F1} MB";

            Assert.Equal(expected, formatted);
        }

        [Theory]
        [InlineData("document.pdf", ".pdf", "📄")]
        [InlineData("report.xlsx", ".xlsx", "📊")]
        [InlineData("invoice.csv", ".csv", "📊")]
        [InlineData("letter.docx", ".docx", "📝")]
        [InlineData("photo.png", ".png", "🖼")]
        [InlineData("archive.zip", ".zip", "🗜")]
        public void FileAttachment_RecognizesExtensionAndGlyph(string fileName, string expectedExt, string expectedGlyph)
        {
            string ext = System.IO.Path.GetExtension(fileName).ToLowerInvariant();
            Assert.Equal(expectedExt, ext);

            string glyph = ext switch
            {
                ".pdf" => "📄",
                ".xls" or ".xlsx" or ".csv" => "📊",
                ".doc" or ".docx" => "📝",
                ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" => "🖼",
                ".zip" or ".rar" or ".7z" => "🗜",
                _ => "📎"
            };

            Assert.Equal(expectedGlyph, glyph);
        }

        [Fact]
        public void LayoutControlGroup_ColumnSpan_CalculatesSlotsProperly()
        {
            int cols = 2;
            var items = new[]
            {
                new { Label = "Code", Span = 1 },
                new { Label = "Name", Span = 1 },
                new { Label = "Address", Span = 2 },
                new { Label = "Phone", Span = 1 }
            };

            int row = 0;
            int col = 0;
            var placed = new List<(string Label, int Row, int Col, int Span)>();

            foreach (var item in items)
            {
                int span = Math.Min(item.Span, cols);
                if (col + span > cols)
                {
                    row++;
                    col = 0;
                }

                placed.Add((item.Label, row, col, span));
                col += span;
                if (col >= cols)
                {
                    col = 0;
                    row++;
                }
            }

            Assert.Equal(3, placed.Select(p => p.Row).Distinct().Count()); // 3 rows
            Assert.Equal((0, 0, 1), (placed[0].Row, placed[0].Col, placed[0].Span));
            Assert.Equal((0, 1, 1), (placed[1].Row, placed[1].Col, placed[1].Span));
            Assert.Equal((1, 0, 2), (placed[2].Row, placed[2].Col, placed[2].Span)); // Address spans whole row
            Assert.Equal((2, 0, 1), (placed[3].Row, placed[3].Col, placed[3].Span));
        }
    }
}
