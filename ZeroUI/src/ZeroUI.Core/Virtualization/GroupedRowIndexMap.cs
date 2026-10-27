using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace ZeroUI.Core.Virtualization
{
    /// <summary>
    /// Compact 64-bit packed visual row representation.
    /// Bit 63: 1 = Group Header Row, 0 = Leaf Data Row.
    /// Bits 62..56: Hierarchy Nesting Level (0..63).
    /// Bit 55: IsExpanded state (1 = Expanded, 0 = Collapsed).
    /// Bits 54..32: Group Identifier / Metadata Index.
    /// Bits 31..0: Model Row Index (for Data Rows) or Group Identifier (for Group Rows).
    /// </summary>
    public readonly struct VisualRowEntry : IEquatable<VisualRowEntry>
    {
        public readonly ulong Packed;

        public const ulong GroupFlag = 1UL << 63;
        public const ulong ExpandedFlag = 1UL << 55;

        public bool IsGroup => (Packed & GroupFlag) != 0;
        public bool IsData => (Packed & GroupFlag) == 0;
        public int Level => (int)((Packed >> 56) & 0x7F);
        public bool IsExpanded => (Packed & ExpandedFlag) != 0;
        public int GroupId => (int)((Packed >> 32) & 0x7FFFFF);
        public int ModelRowIndex => (int)(Packed & 0xFFFFFFFF);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VisualRowEntry(ulong packed)
        {
            Packed = packed;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VisualRowEntry CreateData(int modelRowIndex, int level = 0)
        {
            ulong packed = ((ulong)(level & 0x7F) << 56) | ((uint)modelRowIndex);
            return new VisualRowEntry(packed);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VisualRowEntry CreateGroup(int groupId, int level, bool isExpanded)
        {
            ulong packed = GroupFlag | ((ulong)(level & 0x7F) << 56);
            if (isExpanded) packed |= ExpandedFlag;
            packed |= ((ulong)(groupId & 0x7FFFFF) << 32) | ((uint)groupId);
            return new VisualRowEntry(packed);
        }

        public bool Equals(VisualRowEntry other) => Packed == other.Packed;
        public override bool Equals(object? obj) => obj is VisualRowEntry other && Equals(other);
        public override int GetHashCode() => Packed.GetHashCode();
        public override string ToString() => IsGroup
            ? $"[Group L{Level} ID:{GroupId} {(IsExpanded ? "Expanded" : "Collapsed")}]"
            : $"[Data L{Level} ModelRow:{ModelRowIndex}]";
    }

    /// <summary>
    /// Metadata descriptor for a hierarchical group node.
    /// </summary>
    public sealed class GroupRowInfo
    {
        public int GroupId { get; }
        public int ParentGroupId { get; }
        public int Level { get; }
        public int ColumnIndex { get; }
        public string GroupKey { get; }
        public bool IsExpanded { get; set; } = true;

        public List<int> DataRowIndices { get; } = new List<int>();
        public List<GroupRowInfo> SubGroups { get; } = new List<GroupRowInfo>();
        public Dictionary<int, double>? Summaries { get; set; }
        public Dictionary<(int ColumnIndex, ZeroUI.Core.Data.GroupSummaryType Type), double> DetailedSummaries { get; } = new Dictionary<(int, ZeroUI.Core.Data.GroupSummaryType), double>();
        public string? FormattedSummaryText { get; set; }

        public double GetSummary(int columnIndex, ZeroUI.Core.Data.GroupSummaryType type = ZeroUI.Core.Data.GroupSummaryType.Sum)
        {
            if (DetailedSummaries.TryGetValue((columnIndex, type), out double val)) return val;
            if (Summaries != null && Summaries.TryGetValue(columnIndex, out double legacyVal)) return legacyVal;
            return 0.0;
        }

        public int TotalDataRowCount => DataRowIndices.Count;

        public GroupRowInfo(int groupId, int parentGroupId, int level, int columnIndex, string groupKey)
        {
            GroupId = groupId;
            ParentGroupId = parentGroupId;
            Level = level;
            ColumnIndex = columnIndex;
            GroupKey = groupKey ?? string.Empty;
        }
    }

    /// <summary>
    /// High-performance, zero-allocation hierarchical virtual row mapping array.
    /// Supports multi-level grouping, expand/collapse toggles, and group summaries
    /// while preserving $O(1)$ index traversal during high-speed viewport rendering.
    /// </summary>
    public sealed class GroupedRowIndexMap
    {
        private VisualRowEntry[] _map;
        private int _activeCount;
        private bool _isIdentity;

        private readonly List<GroupRowInfo> _allGroups = new List<GroupRowInfo>();
        private readonly List<GroupRowInfo> _rootGroups = new List<GroupRowInfo>();

        public GroupedRowIndexMap(int initialCapacity = 2048)
        {
            _map = new VisualRowEntry[initialCapacity];
            _activeCount = 0;
            _isIdentity = true;
        }

        public bool IsIdentity => _isIdentity;
        public int ActiveCount => _activeCount;
        public bool HasGrouping => _rootGroups.Count > 0;
        public IReadOnlyList<GroupRowInfo> RootGroups => _rootGroups;

        public VisualRowEntry this[int visualIndex]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _isIdentity ? VisualRowEntry.CreateData(visualIndex, 0) : _map[visualIndex];
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                if (_isIdentity) EnsureMaterialized();
                _map[visualIndex] = value;
            }
        }

        public void EnsureCapacity(int count)
        {
            if (_map.Length < count)
            {
                int newCap = Math.Max(count, _map.Length * 2);
                Array.Resize(ref _map, newCap);
            }
        }

        public void EnsureMaterialized()
        {
            if (!_isIdentity) return;
            EnsureCapacity(_activeCount);
            for (int i = 0; i < _activeCount; i++)
            {
                _map[i] = VisualRowEntry.CreateData(i, 0);
            }
            _isIdentity = false;
        }

        /// <summary>
        /// Resets the index map to a flat 1:1 identity projection (no grouping) in O(1) time without allocations.
        /// </summary>
        public void ResetIdentity(int totalDataRows)
        {
            _allGroups.Clear();
            _rootGroups.Clear();
            _activeCount = Math.Max(0, totalDataRows);
            _isIdentity = true;
        }

        /// <summary>
        /// Rebuilds multi-level grouping from the source rows using specified group column indices.
        /// </summary>
        public void BuildGroups(int totalDataRows, int[] groupColumnIndices, Func<int, int, string> getCellText)
        {
            _allGroups.Clear();
            _rootGroups.Clear();

            if (groupColumnIndices == null || groupColumnIndices.Length == 0 || totalDataRows <= 0)
            {
                ResetIdentity(totalDataRows);
                return;
            }

            // Create root group level
            var initialRowList = new List<int>(totalDataRows);
            for (int i = 0; i < totalDataRows; i++) initialRowList.Add(i);

            BuildSubGroupsRecursive(initialRowList, groupColumnIndices, 0, -1, _rootGroups, getCellText);
            RebuildVisualMap();
        }

        private void BuildSubGroupsRecursive(
            List<int> candidateRows,
            int[] groupColumnIndices,
            int groupColPointer,
            int parentGroupId,
            List<GroupRowInfo> destinationList,
            Func<int, int, string> getCellText)
        {
            int colIdx = groupColumnIndices[groupColPointer];
            var bucketDict = new Dictionary<string, List<int>>(StringComparer.Ordinal);
            var keyOrder = new List<string>();

            for (int i = 0; i < candidateRows.Count; i++)
            {
                int modelRow = candidateRows[i];
                string key = getCellText(modelRow, colIdx) ?? string.Empty;
                if (!bucketDict.TryGetValue(key, out var rowList))
                {
                    rowList = new List<int>();
                    bucketDict[key] = rowList;
                    keyOrder.Add(key);
                }
                rowList.Add(modelRow);
            }

            foreach (var key in keyOrder)
            {
                int groupId = _allGroups.Count;
                var groupInfo = new GroupRowInfo(groupId, parentGroupId, groupColPointer, colIdx, key)
                {
                    IsExpanded = true
                };
                groupInfo.DataRowIndices.AddRange(bucketDict[key]);
                _allGroups.Add(groupInfo);
                destinationList.Add(groupInfo);

                if (groupColPointer + 1 < groupColumnIndices.Length)
                {
                    BuildSubGroupsRecursive(bucketDict[key], groupColumnIndices, groupColPointer + 1, groupId, groupInfo.SubGroups, getCellText);
                }
            }
        }

        /// <summary>
        /// Flattens the active expanded group hierarchy into the linear visual index map.
        /// </summary>
        public void RebuildVisualMap()
        {
            if (_rootGroups.Count == 0) return;

            int estimatedRows = 0;
            for (int i = 0; i < _allGroups.Count; i++) estimatedRows += _allGroups[i].DataRowIndices.Count + 1;
            EnsureCapacity(estimatedRows);

            int writePointer = 0;
            foreach (var root in _rootGroups)
            {
                FlattenGroupRecursive(root, ref writePointer);
            }

            _activeCount = writePointer;
            _isIdentity = false;
        }

        private void FlattenGroupRecursive(GroupRowInfo group, ref int writePointer)
        {
            _map[writePointer++] = VisualRowEntry.CreateGroup(group.GroupId, group.Level, group.IsExpanded);

            if (!group.IsExpanded) return;

            if (group.SubGroups.Count > 0)
            {
                foreach (var sub in group.SubGroups)
                {
                    FlattenGroupRecursive(sub, ref writePointer);
                }
            }
            else
            {
                int dataLevel = group.Level + 1;
                var rows = group.DataRowIndices;
                int count = rows.Count;
                for (int i = 0; i < count; i++)
                {
                    _map[writePointer++] = VisualRowEntry.CreateData(rows[i], dataLevel);
                }
            }
        }

        /// <summary>
        /// Toggles expansion state of the group at the given visual row index.
        /// </summary>
        public bool ToggleGroup(int visualRowIndex)
        {
            if (visualRowIndex < 0 || visualRowIndex >= _activeCount) return false;

            var entry = _map[visualRowIndex];
            if (!entry.IsGroup) return false;

            int gId = entry.GroupId;
            if (gId >= 0 && gId < _allGroups.Count)
            {
                _allGroups[gId].IsExpanded = !_allGroups[gId].IsExpanded;
                RebuildVisualMap();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Expands all group nodes in the hierarchy.
        /// </summary>
        public void ExpandAll()
        {
            for (int i = 0; i < _allGroups.Count; i++)
            {
                _allGroups[i].IsExpanded = true;
            }
            RebuildVisualMap();
        }

        /// <summary>
        /// Collapses all group nodes in the hierarchy.
        /// </summary>
        public void CollapseAll()
        {
            for (int i = 0; i < _allGroups.Count; i++)
            {
                _allGroups[i].IsExpanded = false;
            }
            RebuildVisualMap();
        }

        public GroupRowInfo? GetGroupInfo(int groupId)
        {
            if (groupId >= 0 && groupId < _allGroups.Count)
            {
                return _allGroups[groupId];
            }
            return null;
        }

        /// <summary>
        /// Calculates multi-level aggregations across all grouped hierarchy nodes.
        /// </summary>
        public void CalculateSummaries(IReadOnlyList<ZeroUI.Core.Data.GroupSummaryItem> summaryItems, Func<int, int, double> getNumericValue)
        {
            if (summaryItems == null || summaryItems.Count == 0 || _allGroups.Count == 0)
            {
                for (int g = 0; g < _allGroups.Count; g++)
                {
                    _allGroups[g].FormattedSummaryText = null;
                    _allGroups[g].Summaries?.Clear();
                }
                return;
            }

            var sb = new System.Text.StringBuilder(128);

            for (int g = 0; g < _allGroups.Count; g++)
            {
                var group = _allGroups[g];
                group.Summaries ??= new Dictionary<int, double>();
                group.Summaries.Clear();
                group.DetailedSummaries.Clear();
                sb.Clear();

                var rows = group.DataRowIndices;
                int rowCount = rows.Count;

                for (int s = 0; s < summaryItems.Count; s++)
                {
                    var item = summaryItems[s];
                    double result = 0;

                    switch (item.SummaryType)
                    {
                        case ZeroUI.Core.Data.GroupSummaryType.Count:
                            result = rowCount;
                            break;

                        case ZeroUI.Core.Data.GroupSummaryType.Sum:
                            for (int r = 0; r < rowCount; r++)
                            {
                                result += getNumericValue(rows[r], item.ColumnIndex);
                            }
                            break;

                        case ZeroUI.Core.Data.GroupSummaryType.Average:
                            if (rowCount > 0)
                            {
                                double sum = 0;
                                for (int r = 0; r < rowCount; r++) sum += getNumericValue(rows[r], item.ColumnIndex);
                                result = sum / rowCount;
                            }
                            break;

                        case ZeroUI.Core.Data.GroupSummaryType.Min:
                            if (rowCount > 0)
                            {
                                result = getNumericValue(rows[0], item.ColumnIndex);
                                for (int r = 1; r < rowCount; r++)
                                {
                                    double v = getNumericValue(rows[r], item.ColumnIndex);
                                    if (v < result) result = v;
                                }
                            }
                            break;

                        case ZeroUI.Core.Data.GroupSummaryType.Max:
                            if (rowCount > 0)
                            {
                                result = getNumericValue(rows[0], item.ColumnIndex);
                                for (int r = 1; r < rowCount; r++)
                                {
                                    double v = getNumericValue(rows[r], item.ColumnIndex);
                                    if (v > result) result = v;
                                }
                            }
                            break;

                        case ZeroUI.Core.Data.GroupSummaryType.Custom:
                            if (item.CustomAggregator != null)
                            {
                                result = item.CustomAggregator(rows);
                            }
                            break;
                    }

                    group.DetailedSummaries[(item.ColumnIndex, item.SummaryType)] = result;
                    group.Summaries[item.ColumnIndex] = result;

                    if (sb.Length > 0) sb.Append("  •  ");
                    sb.Append(item.FormatValue(result));
                }

                group.FormattedSummaryText = sb.ToString();
            }
        }

        public ReadOnlySpan<VisualRowEntry> AsSpan() => new ReadOnlySpan<VisualRowEntry>(_map, 0, _activeCount);
    }
}
