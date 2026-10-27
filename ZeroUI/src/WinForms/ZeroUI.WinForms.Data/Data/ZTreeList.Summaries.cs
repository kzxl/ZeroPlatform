using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Data
{
    public enum TreeListSummaryType
    {
        None,
        Sum,
        Count,
        Average,
        Min,
        Max,
        Custom
    }

    public enum TreeListRollupMode
    {
        None,
        ChildSum,
        ChildAverage,
        ChildMin,
        ChildMax,
        Custom
    }

    public class CustomSummaryEventArgs : EventArgs
    {
        public TreeListColumn Column { get; }
        public IReadOnlyList<ZeroTreeNode> Nodes { get; }
        public object? Result { get; set; }

        public CustomSummaryEventArgs(TreeListColumn column, IReadOnlyList<ZeroTreeNode> nodes)
        {
            Column = column;
            Nodes = nodes;
        }
    }

    public partial class ZTreeList
    {
        private bool _showFooter = false;
        private int _footerHeight = 28;
        private readonly Dictionary<string, object?> _columnSummaries = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        public event EventHandler<CustomSummaryEventArgs>? CustomSummaryCalculate;

        [Category("Appearance")]
        [DefaultValue(false)]
        [Description("Displays a summary footer row at the bottom of the TreeList.")]
        public bool ShowFooter
        {
            get => _showFooter;
            set
            {
                if (_showFooter != value)
                {
                    _showFooter = value;
                    if (_showFooter) RecalculateSummaries();
                    UpdateScrollBar();
                    Invalidate();
                }
            }
        }

        [Category("Appearance")]
        [DefaultValue(28)]
        [Description("Height in pixels of the summary footer row.")]
        public int FooterHeight
        {
            get => _footerHeight;
            set
            {
                if (_footerHeight != value && value >= 20)
                {
                    _footerHeight = value;
                    UpdateScrollBar();
                    Invalidate();
                }
            }
        }

        [Browsable(false)]
        public IReadOnlyDictionary<string, object?> ColumnSummaries => _columnSummaries;

        /// <summary>
        /// Recalculates all footer column summaries across all nodes.
        /// </summary>
        public void RecalculateSummaries()
        {
            _columnSummaries.Clear();
            if (_columns.Count == 0) return;

            var allNodes = new List<ZeroTreeNode>();
            void Flatten(ZeroTreeNode node)
            {
                allNodes.Add(node);
                foreach (var child in node.Children) Flatten(child);
            }
            foreach (var root in _nodes) Flatten(root);

            foreach (var col in _columns)
            {
                if (col.SummaryType == TreeListSummaryType.None) continue;

                if (col.SummaryType == TreeListSummaryType.Count)
                {
                    _columnSummaries[col.Name] = allNodes.Count;
                }
                else if (col.SummaryType == TreeListSummaryType.Custom)
                {
                    var args = new CustomSummaryEventArgs(col, allNodes);
                    CustomSummaryCalculate?.Invoke(this, args);
                    _columnSummaries[col.Name] = args.Result;
                }
                else
                {
                    decimal sum = 0;
                    int countWithVal = 0;
                    decimal min = decimal.MaxValue;
                    decimal max = decimal.MinValue;
                    bool hasNumeric = false;

                    foreach (var n in allNodes)
                    {
                        var raw = n[col.Name];
                        if (raw != null && TryParseDecimal(raw, out decimal d))
                        {
                            hasNumeric = true;
                            sum += d;
                            countWithVal++;
                            if (d < min) min = d;
                            if (d > max) max = d;
                        }
                    }

                    if (hasNumeric)
                    {
                        switch (col.SummaryType)
                        {
                            case TreeListSummaryType.Sum:
                                _columnSummaries[col.Name] = sum;
                                break;
                            case TreeListSummaryType.Average:
                                _columnSummaries[col.Name] = countWithVal > 0 ? (sum / countWithVal) : 0m;
                                break;
                            case TreeListSummaryType.Min:
                                _columnSummaries[col.Name] = min;
                                break;
                            case TreeListSummaryType.Max:
                                _columnSummaries[col.Name] = max;
                                break;
                        }
                    }
                    else
                    {
                        _columnSummaries[col.Name] = null;
                    }
                }
            }

            SummariesRecalculated?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Bottom-up hierarchical rollup: aggregates child values into parent nodes recursively.
        /// Essential for BOM costs, project progress percentages, and WBS budgeting.
        /// </summary>
        public void RecalculateRollups()
        {
            if (_columns.Count == 0 || _nodes.Count == 0) return;

            bool hasRollup = false;
            foreach (var col in _columns)
            {
                if (col.RollupMode != TreeListRollupMode.None)
                {
                    hasRollup = true;
                    break;
                }
            }
            if (!hasRollup) return;

            foreach (var root in _nodes)
            {
                RollupNodeRecursive(root);
            }
        }

        private void RollupNodeRecursive(ZeroTreeNode node)
        {
            if (!node.HasChildren) return;

            // Process deepest descendants first (post-order traversal)
            foreach (var child in node.Children)
            {
                RollupNodeRecursive(child);
            }

            foreach (var col in _columns)
            {
                if (col.RollupMode == TreeListRollupMode.None) continue;

                if (col.RollupMode == TreeListRollupMode.ChildSum)
                {
                    decimal sum = 0;
                    bool any = false;
                    foreach (var child in node.Children)
                    {
                        var raw = child[col.Name];
                        if (raw != null && TryParseDecimal(raw, out decimal d))
                        {
                            sum += d;
                            any = true;
                        }
                    }
                    if (any) node[col.Name] = sum;
                }
                else if (col.RollupMode == TreeListRollupMode.ChildAverage)
                {
                    decimal sum = 0;
                    int cnt = 0;
                    foreach (var child in node.Children)
                    {
                        var raw = child[col.Name];
                        if (raw != null && TryParseDecimal(raw, out decimal d))
                        {
                            sum += d;
                            cnt++;
                        }
                    }
                    if (cnt > 0) node[col.Name] = sum / cnt;
                }
                else if (col.RollupMode == TreeListRollupMode.ChildMax)
                {
                    decimal max = decimal.MinValue;
                    bool any = false;
                    foreach (var child in node.Children)
                    {
                        var raw = child[col.Name];
                        if (raw != null && TryParseDecimal(raw, out decimal d))
                        {
                            if (!any || d > max) max = d;
                            any = true;
                        }
                    }
                    if (any) node[col.Name] = max;
                }
                else if (col.RollupMode == TreeListRollupMode.ChildMin)
                {
                    decimal min = decimal.MaxValue;
                    bool any = false;
                    foreach (var child in node.Children)
                    {
                        var raw = child[col.Name];
                        if (raw != null && TryParseDecimal(raw, out decimal d))
                        {
                            if (!any || d < min) min = d;
                            any = true;
                        }
                    }
                    if (any) node[col.Name] = min;
                }
            }
        }

        private static bool TryParseDecimal(object? val, out decimal result)
        {
            if (val == null)
            {
                result = 0;
                return false;
            }
            if (val is decimal dec) { result = dec; return true; }
            if (val is int i) { result = i; return true; }
            if (val is long l) { result = l; return true; }
            if (val is double dbl) { result = (decimal)dbl; return true; }
            if (val is float flt) { result = (decimal)flt; return true; }
            return decimal.TryParse(val.ToString(), NumberStyles.Any, CultureInfo.CurrentCulture, out result) ||
                   decimal.TryParse(val.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out result);
        }

        internal void DrawFooter(Graphics g, ZeroThemePalette palette, int clientWidth, int headerOffset)
        {
            int footerY = Height - _footerHeight;
            using var bgBrush = new SolidBrush(palette.HeaderBackground);
            using var borderPen = new Pen(palette.Border, 1f);
            using var textBrush = new SolidBrush(palette.TextPrimary);
            using var fontBold = new Font(Font.FontFamily, 8.5f, FontStyle.Bold);

            g.FillRectangle(bgBrush, 0, footerY, clientWidth, _footerHeight);
            g.DrawLine(borderPen, 0, footerY, clientWidth, footerY);

            int curColX = 0;
            for (int c = 0; c < _columns.Count; c++)
            {
                var col = _columns[c];
                if (!col.Visible) continue;
                int colW = col.Width;

                // Vertical separator
                g.DrawLine(borderPen, curColX + colW - 1, footerY, curColX + colW - 1, footerY + _footerHeight);

                if (_columnSummaries.TryGetValue(col.Name, out var sumVal) && sumVal != null)
                {
                    string formatted;
                    try
                    {
                        formatted = string.Format(CultureInfo.CurrentCulture, col.SummaryFormat, sumVal);
                    }
                    catch
                    {
                        formatted = sumVal.ToString() ?? "";
                    }

                    var cellTextRect = new Rectangle(curColX + 6, footerY, Math.Max(10, colW - 12), _footerHeight);
                    TextFormatFlags tff = TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine;
                    tff |= col.Alignment switch
                    {
                        HorizontalAlignment.Center => TextFormatFlags.HorizontalCenter,
                        HorizontalAlignment.Right => TextFormatFlags.Right,
                        _ => TextFormatFlags.Left
                    };
                    TextRenderer.DrawText(g, formatted, fontBold, cellTextRect, palette.Primary, tff);
                }

                curColX += colW;
            }
        }
    }
}
