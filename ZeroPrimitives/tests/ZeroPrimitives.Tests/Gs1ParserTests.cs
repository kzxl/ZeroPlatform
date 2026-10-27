using System;
using Xunit;
using ZeroPrimitives.Parsing;

namespace ZeroPrimitives.Tests
{
    public class Gs1ParserTests
    {
        [Fact]
        public void FastGs1Parser_BracketedBarcode_ParsesAllElements()
        {
            // Human-readable GS1 bracketed format
            string barcode = "(01)08881234567890(17)261231(10)LOT2026A(21)SN998877";

            var parser = new FastGs1Parser(barcode.AsSpan());

            Assert.True(parser.MoveNext(out var gtin));
            Assert.True(gtin.IsGtin);
            Assert.Equal("01", gtin.Ai.ToString());
            Assert.Equal("08881234567890", gtin.Value.ToString());

            Assert.True(parser.MoveNext(out var expiry));
            Assert.True(expiry.IsExpirationDate);
            Assert.Equal("17", expiry.Ai.ToString());
            Assert.Equal("261231", expiry.Value.ToString());
            Assert.True(expiry.TryGetDate(out var expiryDate));
            Assert.Equal(new DateTime(2026, 12, 31), expiryDate);

            Assert.True(parser.MoveNext(out var lot));
            Assert.True(lot.IsLot);
            Assert.Equal("10", lot.Ai.ToString());
            Assert.Equal("LOT2026A", lot.Value.ToString());

            Assert.True(parser.MoveNext(out var serial));
            Assert.True(serial.IsSerial);
            Assert.Equal("21", serial.Ai.ToString());
            Assert.Equal("SN998877", serial.Value.ToString());

            Assert.False(parser.MoveNext(out _));
        }

        [Fact]
        public void FastGs1Parser_RawStreamWithFnc1_ParsesVariableLengths()
        {
            // Raw scanner format: 01 (14 chars fixed) + 17 (6 chars fixed) + 10 (variable, terminated by \u001d) + 21 (variable)
            string barcode = "01088812345678901726051510BATCH-XYZ\u001d21SERIAL123";

            var parser = new FastGs1Parser(barcode.AsSpan());

            Assert.True(parser.MoveNext(out var gtin));
            Assert.Equal("01", gtin.Ai.ToString());
            Assert.Equal("08881234567890", gtin.Value.ToString());

            Assert.True(parser.MoveNext(out var exp));
            Assert.Equal("17", exp.Ai.ToString());
            Assert.Equal("260515", exp.Value.ToString());
            Assert.True(exp.TryGetDate(out var expDate));
            Assert.Equal(new DateTime(2026, 5, 15), expDate);

            Assert.True(parser.MoveNext(out var batch));
            Assert.Equal("10", batch.Ai.ToString());
            Assert.Equal("BATCH-XYZ", batch.Value.ToString());

            Assert.True(parser.MoveNext(out var sn));
            Assert.Equal("21", sn.Ai.ToString());
            Assert.Equal("SERIAL123", sn.Value.ToString());

            Assert.False(parser.MoveNext(out _));
        }
    }
}
