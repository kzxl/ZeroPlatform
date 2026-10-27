using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using ZeroUI.Core.Common;
using ZeroUI.Core.Data;
using ZeroUI.Core.DataGrid;

namespace ZeroUI.Core.Tests
{
    public class EnterpriseGridExpansionTests
    {
        private static string ReadCellValue(IZeroVirtualSource source, int row, int col)
        {
            CellValueBuffer buf = new CellValueBuffer();
            source.GetCellValue(row, col, ref buf);
            return buf.Text.ToString();
        }

        [Fact]
        public void GridLevelTree_CanBuildHierarchyAndFindRelation()
        {
            var tree = new GridLevelTree();
            var ordersNode = tree.Add("Orders", "OrdersTemplate");
            ordersNode.LevelTemplate = "OrdersTemplate";
            var detailsNode = ordersNode.AddChild("OrderDetails", "DetailsTemplate");
            detailsNode.LevelTemplate = "DetailsTemplate";
            var serialsNode = detailsNode.AddChild("SerialNumbers", "SerialsTemplate");
            serialsNode.LevelTemplate = "SerialsTemplate";

            Assert.Single(tree.Nodes);
            Assert.Equal("Orders", tree.Nodes[0].RelationName);
            Assert.Single(ordersNode.Nodes);
            Assert.Equal("OrderDetails", ordersNode.Nodes[0].RelationName);
            Assert.Single(detailsNode.Nodes);
            Assert.Equal("SerialNumbers", detailsNode.Nodes[0].RelationName);

            // Test recursive lookup
            var found = tree.FindNode("SerialNumbers");
            Assert.NotNull(found);
            Assert.Equal("SerialNumbers", found!.RelationName);
            Assert.Equal("SerialsTemplate", found.LevelTemplate);

            var notFound = tree.FindNode("NonExistent");
            Assert.Null(notFound);
        }

        [Fact]
        public async Task AsyncVirtualGridSource_LoadsChunksAndFetchesOnDemand()
        {
            int totalRows = 1000;
            int totalCols = 3;

            var source = new AsyncVirtualGridSource<string[]>(
                totalRows,
                totalCols,
                (startRow, count, ct) =>
                {
                    var chunk = new List<string[]>();
                    for (int i = 0; i < count; i++)
                    {
                        chunk.Add(new[] { $"ID_{startRow + i}", $"Name_{startRow + i}", $"100" });
                    }
                    return Task.FromResult<IReadOnlyList<string[]>>(chunk);
                },
                (row, col) => row[col],
                chunkSize: 50);

            Assert.Equal(1000, source.TotalRowCount);
            Assert.Equal(3, source.TotalColumnCount);
            Assert.Equal(50, source.ChunkSize);

            // Row not yet loaded -> returns shimmer placeholder and queues fetch
            string initialVal = ReadCellValue(source, 0, 0);
            Assert.Equal("...", initialVal);

            // Wait brief moment for background Task to finish
            await Task.Delay(100);

            // Now row 0 should be cached
            string cachedId = ReadCellValue(source, 0, 0);
            Assert.Equal("ID_0", cachedId);

            string cachedName = ReadCellValue(source, 0, 1);
            Assert.Equal("Name_0", cachedName);

            Assert.True(source.CachedChunkCount >= 1);

            // Clear cache
            source.ClearCache();
            Assert.Equal(0, source.CachedChunkCount);
        }

        [Fact]
        public void ZeroColumn_SupportsMultiRowRecordAndRepositoryProperties()
        {
            var col = new ZeroColumn("Description", 200, CellAlignment.Left)
            {
                FieldName = "Desc",
                BandRow = 1,
                BandRowSpan = 2,
                ColumnEdit = "CustomTextRepository"
            };

            Assert.Equal("Desc", col.FieldName);
            Assert.Equal(1, col.BandRow);
            Assert.Equal(2, col.BandRowSpan);
            Assert.Equal("CustomTextRepository", col.ColumnEdit);
        }

        [Fact]
        public void ZeroClipboardHelper_ParsesMultiRowMultiColumnTsvAccurately()
        {
            string tsv = "Col1\tCol2\tCol3\r\nVal1\tVal2\tVal3\r\n100\t200\t300";

            var parsed = new List<(int Row, int Col, string Text)>();
            ZeroClipboardHelper.ParseTsv(tsv.AsSpan(), (r, c, span) =>
            {
                parsed.Add((r, c, span.ToString()));
            });

            Assert.Equal(9, parsed.Count);
            Assert.Equal((0, 0, "Col1"), parsed[0]);
            Assert.Equal((0, 1, "Col2"), parsed[1]);
            Assert.Equal((0, 2, "Col3"), parsed[2]);
            Assert.Equal((1, 0, "Val1"), parsed[3]);
            Assert.Equal((1, 1, "Val2"), parsed[4]);
            Assert.Equal((1, 2, "Val3"), parsed[5]);
            Assert.Equal((2, 0, "100"), parsed[6]);
            Assert.Equal((2, 1, "200"), parsed[7]);
            Assert.Equal((2, 2, "300"), parsed[8]);
        }
    }
}
