using System;
using System.Collections.Generic;

namespace ZeroUI.Core.Input
{
    /// <summary>
    /// Event arguments for selection changes in SelectionModel.
    /// </summary>
    public class SelectionChangedEventArgs<T> : EventArgs
    {
        public int OldIndex { get; }
        public int NewIndex { get; }
        public T? OldItem { get; }
        public T? NewItem { get; }

        public SelectionChangedEventArgs(int oldIndex, int newIndex, T? oldItem, T? newItem)
        {
            OldIndex = oldIndex;
            NewIndex = newIndex;
            OldItem = oldItem;
            NewItem = newItem;
        }
    }

    /// <summary>
    /// High-performance, framework-independent selection coordinator.
    /// Manages single-selection state, cyclic navigation, and boundary enforcement
    /// across ComboBoxes, RadioGroups, Segmented buttons, and Grid editors.
    /// </summary>
    public class SelectionModel<T>
    {
        private IList<T>? _itemsList;
        private Func<int>? _countProvider;
        private Func<int, T>? _itemAccessor;
        private int _selectedIndex = -1;
        private bool _wrapAround = true;

        public event EventHandler<SelectionChangedEventArgs<T>>? SelectionChanged;

        public SelectionModel(IList<T>? items = null)
        {
            _itemsList = items;
        }

        public SelectionModel(Func<int> countProvider, Func<int, T> itemAccessor)
        {
            _countProvider = countProvider ?? throw new ArgumentNullException(nameof(countProvider));
            _itemAccessor = itemAccessor ?? throw new ArgumentNullException(nameof(itemAccessor));
        }

        public void SetSource(IList<T>? items)
        {
            _itemsList = items;
            _countProvider = null;
            _itemAccessor = null;
            if (_selectedIndex >= Count)
            {
                SelectIndex(Count > 0 ? 0 : -1);
            }
        }

        public void SetSource(Func<int> countProvider, Func<int, T> itemAccessor)
        {
            _countProvider = countProvider ?? throw new ArgumentNullException(nameof(countProvider));
            _itemAccessor = itemAccessor ?? throw new ArgumentNullException(nameof(itemAccessor));
            _itemsList = null;
            if (_selectedIndex >= Count)
            {
                SelectIndex(Count > 0 ? 0 : -1);
            }
        }

        public int Count
        {
            get
            {
                if (_countProvider != null) return _countProvider();
                return _itemsList?.Count ?? 0;
            }
        }

        public bool WrapAround
        {
            get => _wrapAround;
            set => _wrapAround = value;
        }

        public bool HasSelection => _selectedIndex >= 0 && _selectedIndex < Count;

        public int SelectedIndex
        {
            get => _selectedIndex;
            set => SelectIndex(value);
        }

        public T? SelectedItem
        {
            get
            {
                if (_selectedIndex >= 0 && _selectedIndex < Count)
                {
                    if (_itemAccessor != null) return _itemAccessor(_selectedIndex);
                    if (_itemsList != null) return _itemsList[_selectedIndex];
                }
                return default;
            }
            set => SelectItem(value);
        }

        public bool SelectIndex(int index)
        {
            int total = Count;
            if (index < 0 || index >= total)
            {
                index = -1;
            }

            if (_selectedIndex != index)
            {
                int oldIndex = _selectedIndex;
                T? oldItem = SelectedItem;
                _selectedIndex = index;
                T? newItem = SelectedItem;

                SelectionChanged?.Invoke(this, new SelectionChangedEventArgs<T>(oldIndex, _selectedIndex, oldItem, newItem));
                return true;
            }
            return false;
        }

        public bool SelectItem(T? item, IEqualityComparer<T>? comparer = null)
        {
            if (item == null)
            {
                return SelectIndex(-1);
            }

            comparer ??= EqualityComparer<T>.Default;
            int total = Count;

            for (int i = 0; i < total; i++)
            {
                T? candidate = _itemAccessor != null ? _itemAccessor(i) : _itemsList![i];
                if (comparer.Equals(candidate, item))
                {
                    return SelectIndex(i);
                }
            }

            return false;
        }

        public bool MoveNext()
        {
            int total = Count;
            if (total == 0) return false;

            if (_selectedIndex < 0)
            {
                return SelectIndex(0);
            }

            if (_selectedIndex + 1 < total)
            {
                return SelectIndex(_selectedIndex + 1);
            }

            if (_wrapAround)
            {
                return SelectIndex(0);
            }

            return false;
        }

        public bool MovePrevious()
        {
            int total = Count;
            if (total == 0) return false;

            if (_selectedIndex < 0)
            {
                return SelectIndex(total - 1);
            }

            if (_selectedIndex > 0)
            {
                return SelectIndex(_selectedIndex - 1);
            }

            if (_wrapAround)
            {
                return SelectIndex(total - 1);
            }

            return false;
        }

        public bool MoveFirst()
        {
            if (Count > 0)
            {
                return SelectIndex(0);
            }
            return false;
        }

        public bool MoveLast()
        {
            int total = Count;
            if (total > 0)
            {
                return SelectIndex(total - 1);
            }
            return false;
        }

        public void ClearSelection()
        {
            SelectIndex(-1);
        }
    }
}
