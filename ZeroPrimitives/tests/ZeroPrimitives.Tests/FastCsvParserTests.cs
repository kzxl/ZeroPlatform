using System;
using System.Collections.Generic;
using Xunit;
using ZeroPrimitives.Parsing;

namespace ZeroPrimitives.Tests
{
    public class FastCsvParserTests
    {
        [Fact]
        public void StandardCsv_EnumeratesRowsAndCells()
        {
            string csv = "Id,Name,Price\r\n1,Widget,10.50\r\n2,Gadget,25.00";

            var rows = new List<List<string>>();
            foreach (var row in FastCsvParser.EnumerateRows(csv.AsSpan()))
            {
                var cells = new List<string>();
                foreach (var cell in FastCsvParser.EnumerateCells(row, ','))
                {
                    cells.Add(cell.ToString());
                }
                rows.Add(cells);
            }

            Assert.Equal(3, rows.Count);
            Assert.Equal(new[] { "Id", "Name", "Price" }, rows[0]);
            Assert.Equal(new[] { "1", "Widget", "10.50" }, rows[1]);
            Assert.Equal(new[] { "2", "Gadget", "25.00" }, rows[2]);
        }

        [Fact]
        public void QuotedCells_HandlesCommasAndEscapedQuotes()
        {
            string csv = "1,\"Widget, Large\",\"He said: \"\"Hello\"\"\",99.9";

            var cells = new List<string>();
            Span<char> buf = stackalloc char[256];
            foreach (var cell in FastCsvParser.EnumerateCells(csv.AsSpan(), ','))
            {
                var unquoted = FastCsvParser.Unquote(cell, buf, out int written);
                cells.Add(unquoted.ToString());
            }

            Assert.Equal(4, cells.Count);
            Assert.Equal("1", cells[0]);
            Assert.Equal("Widget, Large", cells[1]);
            Assert.Equal("He said: \"Hello\"", cells[2]);
            Assert.Equal("99.9", cells[3]);
        }
    }
}
