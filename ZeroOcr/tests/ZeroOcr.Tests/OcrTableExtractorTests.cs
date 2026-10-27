using System;
using System.Collections.Generic;
using System.Data;
using Xunit;
using ZeroOcr.Core.Models;
using ZeroOcr.Core.Tables;

namespace ZeroOcr.Tests;

public class OcrTableExtractorTests
{
    [Fact]
    public void ExtractTables_FromInvoiceTableTokens_BuildsValidTableStructure()
    {
        // Synthesize words simulating an industrial invoice table
        var words = new List<OcrWord>
        {
            // Row 0: Headers (Y = 100)
            new("Mã VT", new OcrRect(50, 100, 60, 20), 0.98f),
            new("Tên Hàng Hóa", new OcrRect(150, 100, 120, 20), 0.98f),
            new("Số Lượng", new OcrRect(300, 100, 60, 20), 0.98f),
            new("Đơn Giá", new OcrRect(400, 100, 60, 20), 0.98f),
            new("Thành Tiền", new OcrRect(500, 100, 70, 20), 0.98f),

            // Row 1: Item 1 (Y = 130)
            new("VT-01", new OcrRect(50, 130, 50, 20), 0.95f),
            new("Bạc đạn trục", new OcrRect(150, 130, 100, 20), 0.95f),
            new("10", new OcrRect(300, 130, 30, 20), 0.97f),
            new("150000", new OcrRect(400, 130, 50, 20), 0.96f),
            new("1500000", new OcrRect(500, 130, 60, 20), 0.96f),

            // Row 2: Item 2 (Y = 160)
            new("VT-02", new OcrRect(50, 160, 50, 20), 0.95f),
            new("Dây curoa", new OcrRect(150, 160, 80, 20), 0.95f),
            new("5", new OcrRect(300, 160, 20, 20), 0.97f),
            new("80000", new OcrRect(400, 160, 45, 20), 0.96f),
            new("400000", new OcrRect(500, 160, 55, 20), 0.96f)
        };

        var lines = new List<OcrLine>
        {
            new("Mã VT Tên Hàng Hóa Số Lượng Đơn Giá Thành Tiền", words.GetRange(0, 5), new OcrRect(50, 100, 520, 20), 0.98f),
            new("VT-01 Bạc đạn trục 10 150000 1500000", words.GetRange(5, 5), new OcrRect(50, 130, 510, 20), 0.96f),
            new("VT-02 Dây curoa 5 80000 400000", words.GetRange(10, 5), new OcrRect(50, 160, 505, 20), 0.96f)
        };

        var ocrResult = OcrResult.Create(lines, TimeSpan.FromMilliseconds(50), "vi-VN");

        // Execute table extraction
        var tables = OcrTableExtractor.ExtractTables(ocrResult);

        Assert.Single(tables);
        var table = tables[0];

        Assert.Equal(3, table.RowCount);
        Assert.Equal(5, table.ColumnCount);

        // Check Cells
        Assert.Equal("Mã VT", table[0, 0]?.Text);
        Assert.Equal("Tên Hàng Hóa", table[0, 1]?.Text);
        Assert.Equal("Bạc đạn trục", table[1, 1]?.Text);
        Assert.Equal("10", table[1, 2]?.Text);
        Assert.Equal("1500000", table[1, 4]?.Text);
        Assert.Equal("VT-02", table[2, 0]?.Text);
        Assert.Equal("Dây curoa", table[2, 1]?.Text);
        Assert.Equal("400000", table[2, 4]?.Text);

        // Test Export to DataTable
        DataTable dt = table.ToDataTable(firstRowIsHeader: true);
        Assert.NotNull(dt);
        Assert.Equal(5, dt.Columns.Count);
        Assert.Equal("Mã VT", dt.Columns[0].ColumnName);
        Assert.Equal("Tên Hàng Hóa", dt.Columns[1].ColumnName);
        Assert.Equal(2, dt.Rows.Count);
        Assert.Equal("Bạc đạn trục", dt.Rows[0]["Tên Hàng Hóa"]);
        Assert.Equal("1500000", dt.Rows[0]["Thành Tiền"]);
        Assert.Equal("VT-02", dt.Rows[1]["Mã VT"]);
        Assert.Equal("400000", dt.Rows[1]["Thành Tiền"]);

        // Test Export to Markdown
        string md = table.ToMarkdown();
        Assert.Contains("| Mã VT | Tên Hàng Hóa | Số Lượng | Đơn Giá | Thành Tiền |", md);
        Assert.Contains("| VT-01 | Bạc đạn trục | 10 | 150000 | 1500000 |", md);

        // Test Export to CSV
        string csv = table.ToCsv();
        Assert.Contains("Mã VT,Tên Hàng Hóa,Số Lượng,Đơn Giá,Thành Tiền", csv);
        Assert.Contains("VT-01,Bạc đạn trục,10,150000,1500000", csv);

        // Test Export to 2D Grid
        string[,] grid = table.ToGrid();
        Assert.Equal("Mã VT", grid[0, 0]);
        Assert.Equal("Bạc đạn trục", grid[1, 1]);
        Assert.Equal("400000", grid[2, 4]);
    }
}
