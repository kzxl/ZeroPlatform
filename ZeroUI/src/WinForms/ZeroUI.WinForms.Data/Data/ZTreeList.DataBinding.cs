using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace ZeroUI.WinForms.Data
{
    public partial class ZTreeList
    {
        private object? _dataSource;
        private string _keyFieldName = "Id";
        private string _parentFieldName = "ParentId";
        private object? _rootValue = null;
        private bool _autoGenerateColumns = true;

        [Category("Data")]
        [DefaultValue(null)]
        [Description("The data source to automatically populate the hierarchical tree structure.")]
        public object? DataSource
        {
            get => _dataSource;
            set
            {
                if (!ReferenceEquals(_dataSource, value))
                {
                    _dataSource = value;
                    BindDataSource();
                }
            }
        }

        [Category("Data")]
        [DefaultValue("Id")]
        [Description("The name of the property or column that provides the unique identifier for each node.")]
        public string KeyFieldName
        {
            get => _keyFieldName;
            set
            {
                if (_keyFieldName != value)
                {
                    _keyFieldName = value ?? "Id";
                    if (_dataSource != null) BindDataSource();
                }
            }
        }

        [Category("Data")]
        [DefaultValue("ParentId")]
        [Description("The name of the property or column that references the parent node's identifier.")]
        public string ParentFieldName
        {
            get => _parentFieldName;
            set
            {
                if (_parentFieldName != value)
                {
                    _parentFieldName = value ?? "ParentId";
                    if (_dataSource != null) BindDataSource();
                }
            }
        }

        [Category("Data")]
        [DefaultValue(null)]
        [Description("The value indicating a root-level node (null, empty, or specific root id such as 0).")]
        public object? RootValue
        {
            get => _rootValue;
            set
            {
                if (!Equals(_rootValue, value))
                {
                    _rootValue = value;
                    if (_dataSource != null) BindDataSource();
                }
            }
        }

        [Category("Data")]
        [DefaultValue(true)]
        [Description("Automatically generates columns from the bound data source if no columns have been explicitly added.")]
        public bool AutoGenerateColumns
        {
            get => _autoGenerateColumns;
            set => _autoGenerateColumns = value;
        }

        /// <summary>
        /// Binds a strongly-typed collection of items into a hierarchical tree based on Key and ParentKey properties.
        /// </summary>
        public void SetDataSource<T>(IEnumerable<T> data, string keyFieldName, string parentFieldName, object? rootValue = null)
        {
            _keyFieldName = keyFieldName;
            _parentFieldName = parentFieldName;
            _rootValue = rootValue;
            _dataSource = data;
            BindDataSource();
        }

        /// <summary>
        /// Binds a DataTable into a hierarchical tree based on Key and ParentKey column names.
        /// </summary>
        public void SetDataSource(DataTable table, string keyFieldName, string parentFieldName, object? rootValue = null)
        {
            _keyFieldName = keyFieldName;
            _parentFieldName = parentFieldName;
            _rootValue = rootValue;
            _dataSource = table;
            BindDataSource();
        }

        /// <summary>
        /// Binds and reconstructs the tree hierarchy from the current DataSource.
        /// </summary>
        public void BindDataSource()
        {
            ClearNodes();

            if (_dataSource == null) return;

            if (_dataSource is DataTable table)
            {
                BuildTreeFromDataTable(table);
            }
            else if (_dataSource is IEnumerable enumerable)
            {
                BuildTreeFromEnumerable(enumerable);
            }

            RecalculateRollups();
            UpdateVisibleNodes();
            Invalidate();
        }

        private void BuildTreeFromDataTable(DataTable table)
        {
            if (table == null || table.Rows.Count == 0) return;

            // Auto-generate columns if needed
            if (_columns.Count == 0 && _autoGenerateColumns)
            {
                foreach (DataColumn dc in table.Columns)
                {
                    var col = new TreeListColumn(dc.ColumnName, string.IsNullOrEmpty(dc.Caption) ? dc.ColumnName : dc.Caption)
                    {
                        Alignment = GetAlignmentForType(dc.DataType),
                        Width = Math.Max(80, Math.Min(200, dc.ColumnName.Length * 12 + 40))
                    };
                    _columns.Add(col);
                }
            }

            var nodeMap = new Dictionary<string, ZeroTreeNode>(StringComparer.OrdinalIgnoreCase);
            var parentRelations = new List<(ZeroTreeNode Node, string ParentId)>();

            foreach (DataRow row in table.Rows)
            {
                if (row.RowState == DataRowState.Deleted) continue;

                string id = GetRowValue(row, _keyFieldName)?.ToString() ?? string.Empty;
                string parentId = GetRowValue(row, _parentFieldName)?.ToString() ?? string.Empty;

                var node = new ZeroTreeNode
                {
                    Id = id,
                    Tag = row
                };

                // Populate cell values for all columns
                for (int c = 0; c < _columns.Count; c++)
                {
                    var col = _columns[c];
                    object? cellVal = GetRowValue(row, col.FieldName ?? col.Name);
                    node[col.Name] = cellVal;

                    if (c == 0 && string.IsNullOrEmpty(node.Text))
                    {
                        node.Text = cellVal?.ToString() ?? id;
                    }
                }

                if (!string.IsNullOrEmpty(id))
                {
                    nodeMap[id] = node;
                }

                if (IsRootParent(parentId))
                {
                    _nodes.Add(node);
                }
                else
                {
                    parentRelations.Add((node, parentId));
                }
            }

            // Link parent-child relations with cycle detection
            LinkParentChildRelations(parentRelations, nodeMap);
        }

        private void BuildTreeFromEnumerable(IEnumerable items)
        {
            if (items == null) return;

            PropertyInfo[]? cachedProps = null;
            Type? itemType = null;

            var nodeMap = new Dictionary<string, ZeroTreeNode>(StringComparer.OrdinalIgnoreCase);
            var parentRelations = new List<(ZeroTreeNode Node, string ParentId)>();

            foreach (var item in items)
            {
                if (item == null) continue;

                if (itemType == null)
                {
                    itemType = item.GetType();
                    cachedProps = itemType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

                    // Auto-generate columns if needed
                    if (_columns.Count == 0 && _autoGenerateColumns && cachedProps != null)
                    {
                        foreach (var prop in cachedProps)
                        {
                            if (!prop.CanRead) continue;
                            var col = new TreeListColumn(prop.Name, prop.Name)
                            {
                                Alignment = GetAlignmentForType(prop.PropertyType),
                                Width = Math.Max(80, Math.Min(200, prop.Name.Length * 12 + 40))
                            };
                            _columns.Add(col);
                        }
                    }
                }

                string id = GetObjectPropertyValue(item, _keyFieldName)?.ToString() ?? string.Empty;
                string parentId = GetObjectPropertyValue(item, _parentFieldName)?.ToString() ?? string.Empty;

                var node = new ZeroTreeNode
                {
                    Id = id,
                    Tag = item
                };

                // Populate cell values
                for (int c = 0; c < _columns.Count; c++)
                {
                    var col = _columns[c];
                    object? cellVal = GetObjectPropertyValue(item, col.FieldName ?? col.Name);
                    node[col.Name] = cellVal;

                    if (c == 0 && string.IsNullOrEmpty(node.Text))
                    {
                        node.Text = cellVal?.ToString() ?? id;
                    }
                }

                if (!string.IsNullOrEmpty(id))
                {
                    nodeMap[id] = node;
                }

                if (IsRootParent(parentId))
                {
                    _nodes.Add(node);
                }
                else
                {
                    parentRelations.Add((node, parentId));
                }
            }

            // Link parent-child relations with cycle detection
            LinkParentChildRelations(parentRelations, nodeMap);
        }

        private void LinkParentChildRelations(List<(ZeroTreeNode Node, string ParentId)> relations, Dictionary<string, ZeroTreeNode> nodeMap)
        {
            foreach (var (child, pId) in relations)
            {
                if (nodeMap.TryGetValue(pId, out var parentNode))
                {
                    // Prevent self-referencing and circular ancestor attachment
                    if (parentNode == child || IsDescendantOf(parentNode, child))
                    {
                        _nodes.Add(child);
                    }
                    else
                    {
                        parentNode.AddChild(child);
                    }
                }
                else
                {
                    // Orphan nodes are placed at root level
                    _nodes.Add(child);
                }
            }
        }

        private static bool IsDescendantOf(ZeroTreeNode candidate, ZeroTreeNode target)
        {
            var curr = candidate.Parent;
            while (curr != null)
            {
                if (curr == target) return true;
                curr = curr.Parent;
            }
            return false;
        }

        private bool IsRootParent(string parentId)
        {
            if (string.IsNullOrEmpty(parentId)) return true;
            if (_rootValue != null)
            {
                string rootStr = _rootValue.ToString() ?? "";
                if (string.Equals(parentId, rootStr, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private static object? GetRowValue(DataRow row, string columnName)
        {
            if (row.Table != null && row.Table.Columns.Contains(columnName))
            {
                var val = row[columnName];
                return val == DBNull.Value ? null : val;
            }
            return null;
        }

        private static object? GetObjectPropertyValue(object obj, string propertyName)
        {
            if (obj == null) return null;
            var prop = obj.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            return prop != null && prop.CanRead ? prop.GetValue(obj) : null;
        }

        private static HorizontalAlignment GetAlignmentForType(Type t)
        {
            var underlying = Nullable.GetUnderlyingType(t) ?? t;
            if (underlying == typeof(int) || underlying == typeof(long) || underlying == typeof(short) ||
                underlying == typeof(decimal) || underlying == typeof(double) || underlying == typeof(float))
            {
                return HorizontalAlignment.Right;
            }
            if (underlying == typeof(DateTime) || underlying == typeof(DateTimeOffset) || underlying == typeof(bool))
            {
                return HorizontalAlignment.Center;
            }
            return HorizontalAlignment.Left;
        }

        /// <summary>
        /// Manually or programmatically triggers the dynamic lazy loading pipeline for a specific node.
        /// </summary>
        public void TriggerLazyLoad(ZeroTreeNode node)
        {
            if (node == null || node.HasLoadedChildren) return;
            var args = new TreeListBeforeExpandEventArgs(node);
            BeforeExpand?.Invoke(this, args);
            VirtualLoadChildren?.Invoke(this, new TreeListVirtualLoadEventArgs(node));
            node.HasLoadedChildren = true;
        }
    }

    public class TreeListBeforeExpandEventArgs : EventArgs
    {
        public ZeroTreeNode Node { get; }
        public bool Cancel { get; set; }
        public TreeListBeforeExpandEventArgs(ZeroTreeNode node) => Node = node;
    }

    public class TreeListVirtualLoadEventArgs : EventArgs
    {
        public ZeroTreeNode Node { get; }
        public TreeListVirtualLoadEventArgs(ZeroTreeNode node) => Node = node;
    }
}
