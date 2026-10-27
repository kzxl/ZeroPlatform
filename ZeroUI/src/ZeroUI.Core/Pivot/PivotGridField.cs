using System;
using System.ComponentModel;

namespace ZeroUI.Core.Pivot
{
    /// <summary>
    /// Represents a data dimension or measure field in a multidimensional PivotGrid.
    /// </summary>
    public class PivotGridField : INotifyPropertyChanged
    {
        private string _fieldName = string.Empty;
        private string _caption = string.Empty;
        private PivotArea _area = PivotArea.FilterArea;
        private int _areaIndex = 0;
        private PivotSummaryType _summaryType = PivotSummaryType.Sum;
        private string? _formatString;
        private PivotSortOrder _sortOrder = PivotSortOrder.Ascending;
        private bool _visible = true;

        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Gets or sets the name of the data source property or column bound to this field.
        /// </summary>
        public string FieldName
        {
            get => _fieldName;
            set
            {
                if (_fieldName != value)
                {
                    _fieldName = value;
                    OnPropertyChanged(nameof(FieldName));
                }
            }
        }

        /// <summary>
        /// Gets or sets the display caption shown in the header area.
        /// Defaults to <see cref="FieldName"/> if empty.
        /// </summary>
        public string Caption
        {
            get => string.IsNullOrEmpty(_caption) ? _fieldName : _caption;
            set
            {
                if (_caption != value)
                {
                    _caption = value;
                    OnPropertyChanged(nameof(Caption));
                }
            }
        }

        /// <summary>
        /// Gets or sets the destination area (Row, Column, Data, or Filter).
        /// </summary>
        public PivotArea Area
        {
            get => _area;
            set
            {
                if (_area != value)
                {
                    _area = value;
                    OnPropertyChanged(nameof(Area));
                }
            }
        }

        /// <summary>
        /// Gets or sets the relative ordinal position within the assigned <see cref="Area"/>.
        /// </summary>
        public int AreaIndex
        {
            get => _areaIndex;
            set
            {
                if (_areaIndex != value)
                {
                    _areaIndex = value;
                    OnPropertyChanged(nameof(AreaIndex));
                }
            }
        }

        /// <summary>
        /// Gets or sets the mathematical aggregation function used when this field is placed in <see cref="PivotArea.DataArea"/>.
        /// </summary>
        public PivotSummaryType SummaryType
        {
            get => _summaryType;
            set
            {
                if (_summaryType != value)
                {
                    _summaryType = value;
                    OnPropertyChanged(nameof(SummaryType));
                }
            }
        }

        /// <summary>
        /// Gets or sets the numeric or date format string applied to formatted cell values.
        /// </summary>
        public string? FormatString
        {
            get => _formatString;
            set
            {
                if (_formatString != value)
                {
                    _formatString = value;
                    OnPropertyChanged(nameof(FormatString));
                }
            }
        }

        /// <summary>
        /// Gets or sets the sorting direction for distinct header category values.
        /// </summary>
        public PivotSortOrder SortOrder
        {
            get => _sortOrder;
            set
            {
                if (_sortOrder != value)
                {
                    _sortOrder = value;
                    OnPropertyChanged(nameof(SortOrder));
                }
            }
        }

        /// <summary>
        /// Gets or sets whether this field is active and rendered in calculations.
        /// </summary>
        public bool Visible
        {
            get => _visible;
            set
            {
                if (_visible != value)
                {
                    _visible = value;
                    OnPropertyChanged(nameof(Visible));
                }
            }
        }

        public PivotGridField() { }

        public PivotGridField(string fieldName, PivotArea area, string? caption = null, PivotSummaryType summaryType = PivotSummaryType.Sum)
        {
            FieldName = fieldName;
            Area = area;
            _caption = caption ?? fieldName;
            SummaryType = summaryType;
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public override string ToString() => $"{Caption} [{Area}]";
    }
}
