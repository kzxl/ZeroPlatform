using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using ZeroUI.Core.Data;
using ZeroUI.Core.DataGrid;
using ZeroUI.WinForms.Editors;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.DataGrid.Repositories
{
    /// <summary>
    /// Event arguments for dynamically assigning custom in-place editors per row and cell.
    /// </summary>
    public sealed class CustomRowCellEditEventArgs : EventArgs
    {
        public int VisualRow { get; }
        public int ModelRow { get; }
        public ZeroColumn Column { get; }
        public IRepositoryItem? RepositoryItem { get; set; }

        public CustomRowCellEditEventArgs(int visualRow, int modelRow, ZeroColumn column, IRepositoryItem? currentItem)
        {
            VisualRow = visualRow;
            ModelRow = modelRow;
            Column = column;
            RepositoryItem = currentItem;
        }
    }

    /// <summary>
    /// WinForms specific contract for repository items capable of generating in-place editor controls.
    /// </summary>
    public interface IRepositoryItemWinForms : IRepositoryItem
    {
        Control CreateInPlaceEditor();
    }

    /// <summary>
    /// Base repository item for text editing.
    /// </summary>
    public class RepositoryItemTextEdit : IRepositoryItemWinForms
    {
        public virtual string EditorTypeName => "TextEdit";
        public string? NullText { get; set; }

        public virtual Control CreateInPlaceEditor()
        {
            return new TextBox
            {
                BorderStyle = BorderStyle.None,
                BackColor = ZeroTheme.Colors.Surface,
                ForeColor = ZeroTheme.Colors.TextPrimary
            };
        }

        public virtual string FormatValue(object? rawValue)
        {
            if (rawValue == null) return NullText ?? string.Empty;
            return rawValue.ToString() ?? string.Empty;
        }

        public virtual bool ParseEditValue(ReadOnlySpan<char> text, out object? parsedValue)
        {
            parsedValue = text.ToString();
            return true;
        }
    }

    /// <summary>
    /// In-place numeric spin editor repository item.
    /// </summary>
    public class RepositoryItemSpinEdit : IRepositoryItemWinForms
    {
        public string EditorTypeName => "SpinEdit";
        public decimal MinValue { get; set; } = 0m;
        public decimal MaxValue { get; set; } = 1_000_000_000m;
        public int DecimalPlaces { get; set; } = 0;
        public decimal Step { get; set; } = 1m;

        public Control CreateInPlaceEditor()
        {
            return new ZSpinEdit
            {
                MinValue = MinValue,
                MaxValue = MaxValue,
                DecimalPlaces = DecimalPlaces,
                Step = Step
            };
        }

        public string FormatValue(object? rawValue)
        {
            if (rawValue == null) return string.Empty;
            if (DecimalPlaces > 0 && decimal.TryParse(rawValue.ToString(), out decimal dec))
            {
                return dec.ToString("N" + DecimalPlaces, CultureInfo.CurrentCulture);
            }
            return rawValue.ToString() ?? string.Empty;
        }

        public bool ParseEditValue(ReadOnlySpan<char> text, out object? parsedValue)
        {
            string s = text.ToString();
            if (decimal.TryParse(s, NumberStyles.Any, CultureInfo.CurrentCulture, out decimal val))
            {
                parsedValue = val;
                return true;
            }
            parsedValue = null;
            return false;
        }
    }

    /// <summary>
    /// In-place date time picker repository item.
    /// </summary>
    public class RepositoryItemDateEdit : IRepositoryItemWinForms
    {
        public string EditorTypeName => "DateEdit";
        public string DateFormat { get; set; } = "yyyy-MM-dd";

        public Control CreateInPlaceEditor()
        {
            return new ZDateEdit();
        }

        public string FormatValue(object? rawValue)
        {
            if (rawValue is DateTime dt)
            {
                return dt.ToString(DateFormat, CultureInfo.InvariantCulture);
            }
            if (rawValue != null && DateTime.TryParse(rawValue.ToString(), out DateTime parsed))
            {
                return parsed.ToString(DateFormat, CultureInfo.InvariantCulture);
            }
            return string.Empty;
        }

        public bool ParseEditValue(ReadOnlySpan<char> text, out object? parsedValue)
        {
            string s = text.ToString();
            if (DateTime.TryParse(s, out DateTime dt))
            {
                parsedValue = dt;
                return true;
            }
            parsedValue = null;
            return false;
        }
    }

    /// <summary>
    /// In-place dropdown combo repository item.
    /// </summary>
    public class RepositoryItemComboBoxEdit : IRepositoryItemWinForms
    {
        public string EditorTypeName => "ComboBoxEdit";
        public List<object> Items { get; } = new List<object>();

        public Control CreateInPlaceEditor()
        {
            var cb = new ZComboBox();
            for (int i = 0; i < Items.Count; i++)
            {
                cb.Items.Add(Items[i]);
            }
            return cb;
        }

        public string FormatValue(object? rawValue)
        {
            return rawValue?.ToString() ?? string.Empty;
        }

        public bool ParseEditValue(ReadOnlySpan<char> text, out object? parsedValue)
        {
            parsedValue = text.ToString();
            return true;
        }
    }

    /// <summary>
    /// In-place checkbox / boolean toggle repository item.
    /// </summary>
    public class RepositoryItemCheckEdit : IRepositoryItemWinForms
    {
        public string EditorTypeName => "CheckEdit";
        public string TrueText { get; set; } = "True";
        public string FalseText { get; set; } = "False";

        public Control CreateInPlaceEditor()
        {
            return new CheckBox
            {
                BackColor = Color.Transparent,
                Text = string.Empty
            };
        }

        public string FormatValue(object? rawValue)
        {
            if (rawValue is bool b) return b ? TrueText : FalseText;
            if (rawValue != null && bool.TryParse(rawValue.ToString(), out bool pb)) return pb ? TrueText : FalseText;
            return FalseText;
        }

        public bool ParseEditValue(ReadOnlySpan<char> text, out object? parsedValue)
        {
            string s = text.Trim().ToString();
            if (bool.TryParse(s, out bool b))
            {
                parsedValue = b;
                return true;
            }
            if (string.Equals(s, "1", StringComparison.Ordinal) || string.Equals(s, "Y", StringComparison.OrdinalIgnoreCase))
            {
                parsedValue = true;
                return true;
            }
            parsedValue = false;
            return true;
        }
    }
}
