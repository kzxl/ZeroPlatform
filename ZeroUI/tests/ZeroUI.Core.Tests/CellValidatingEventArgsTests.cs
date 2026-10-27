using Xunit;
using ZeroUI.Core.Common;
using ZeroUI.Core.Data;

namespace ZeroUI.Core.Tests
{
    public class CellValidatingEventArgsTests
    {
        [Fact]
        public void CellValidatingEventArgs_InitializesPropertiesCorrectly()
        {
            var args = new CellValidatingEventArgs(
                visualRowIndex: 2,
                modelRowIndex: 10,
                columnIndex: 3,
                oldValue: "100",
                newValue: "-50");

            Assert.Equal(2, args.VisualRowIndex);
            Assert.Equal(10, args.ModelRowIndex);
            Assert.Equal(3, args.ColumnIndex);
            Assert.Equal("100", args.OldValue);
            Assert.Equal("-50", args.NewValue);
            Assert.False(args.Cancel);
            Assert.Null(args.ErrorMessage);
        }

        [Fact]
        public void CellValidatingEventArgs_CanCancelAndSetErrorMessage()
        {
            var args = new CellValidatingEventArgs(0, 0, 1, "A", "B");
            args.Cancel = true;
            args.ErrorMessage = "Value must not be B";

            Assert.True(args.Cancel);
            Assert.Equal("Value must not be B", args.ErrorMessage);
        }

        [Fact]
        public void ZeroColumn_ColumnTypeDefaultsToTextAndCanBeConfigured()
        {
            var col = new ZeroColumn("Test", 100);
            Assert.Equal(GridColumnType.Text, col.ColumnType);

            col.ColumnType = GridColumnType.Numeric;
            Assert.Equal(GridColumnType.Numeric, col.ColumnType);

            col.ColumnType = GridColumnType.Boolean;
            Assert.Equal(GridColumnType.Boolean, col.ColumnType);

            col.ColumnType = GridColumnType.DateTime;
            Assert.Equal(GridColumnType.DateTime, col.ColumnType);

            col.ColumnType = GridColumnType.Masked;
            Assert.Equal(GridColumnType.Masked, col.ColumnType);
        }

        [Fact]
        public void ZeroColumn_MaskAndCustomValidatorSupport()
        {
            var col = new ZeroColumn("Serial", 120)
            {
                ColumnType = GridColumnType.Masked,
                Mask = "SN-0000-AAAA",
                CustomValidator = val =>
                {
                    if (string.IsNullOrWhiteSpace(val) || val.Contains("_"))
                        return (false, "Serial must not contain blanks");
                    return (true, null);
                }
            };

            Assert.Equal("SN-0000-AAAA", col.Mask);
            Assert.NotNull(col.CustomValidator);

            var (invalid, err) = col.CustomValidator("SN-12__-____");
            Assert.False(invalid);
            Assert.Equal("Serial must not contain blanks", err);

            var (valid, okMsg) = col.CustomValidator("SN-1234-ABCD");
            Assert.True(valid);
            Assert.Null(okMsg);
        }
    }
}
